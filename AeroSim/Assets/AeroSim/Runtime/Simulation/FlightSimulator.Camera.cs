using System;
using System.Linq;
using AeroSim.Domain;
using UnityEngine;

namespace AeroSim.Simulation
{
    public enum ReplayCameraSource { Recorded, Analysis }

    public sealed partial class FlightSimulator
    {
        private const float CameraCollisionRadius = .18f;
        private const float CameraCollisionPadding = .12f;
        private CameraState _cameraState = new(CameraFamily.NosePilot, CameraPreset.Nose, CameraContext.LiveFlight);
        private Vector3 _observerPosition;
        private Quaternion _observerRotation = Quaternion.identity;
        private bool _observerPoseInitialized;
        private bool _cameraObstructed;
        private float _cameraVisibleFraction = 1;

        public CameraState CameraState => _cameraState;
        public CameraBinding CameraBinding => _cameraState.Binding;
        public bool CameraUsesAircraftSocket => _cameraState.Binding == CameraBinding.AircraftSocket;
        public bool CameraIsWorldObserver => _cameraState.Binding == CameraBinding.World;
        public bool CameraObstructed => _cameraObstructed;
        public float CameraVisibleFraction => _cameraVisibleFraction;
        public ReplayCameraSource ReplayCamera { get; private set; } = ReplayCameraSource.Analysis;
        public string CameraDiagnostic => $"{_cameraState.Context}/{_cameraState.Family}/{_cameraState.Preset} · {_cameraState.Capability} · {_cameraState.Binding}" +
            (_cameraObstructed ? $" · OCCLUDED {_cameraVisibleFraction:P0}" : "");

        public bool SetCameraState(CameraFamily family, CameraPreset preset, CameraContext context, bool immediate = true)
        {
            CameraState next;
            try { next = new CameraState(family, preset, context); }
            catch (ArgumentException) { return false; }
            if (!CameraStateCatalog.IsSelectable(next)) return false;
            return SetCameraState(next, immediate);
        }

        public bool SetCameraState(CameraState state, bool immediate = true)
        {
            if (!CameraStateCatalog.IsSelectable(state)) return false;
            var previous = _cameraState;
            if (previous != state) ClearControlInput();
            _cameraState = state;
            _activeFovViewId = CameraStateCatalog.LegacyViewId(state);
            if (state.Binding == CameraBinding.World && (previous.Binding != CameraBinding.World || !_observerPoseInitialized))
                CaptureObserverPoseFromView();
            ApplyConfiguredFieldOfView();
            ApplyCameraViewPose(immediate);
            if (Running && !ReplayMode && Record != null && previous != state)
                Event("camera", $"{state.Family}/{state.Preset}/{state.Context}");
            return true;
        }

        public void CommitCameraPreview()
        {
            if (_cameraState.Context != CameraContext.SettingsPreview) return;
            _cameraState = _cameraState.WithContext(ReplayMode ? CameraContext.Replay : CameraContext.LiveFlight);
        }

        public bool SetReplayCameraSource(ReplayCameraSource source)
        {
            if (!ReplayMode) return false;
            if (source == ReplayCameraSource.Recorded && (Record?.frames == null || !Record.frames.Any(frame => frame.cameraSnapshot?.IsValid == true)))
            {
                ReplayCamera = ReplayCameraSource.Analysis;
                return false;
            }
            ReplayCamera = source;
            if (source == ReplayCameraSource.Analysis)
                SetCameraState(_cameraState.WithContext(CameraContext.Replay));
            else
                ApplyRecordedCameraAt(ReplayTime);
            return true;
        }

        public void MoveObserver(Vector3 worldDelta)
        {
            if (_cameraState.Binding != CameraBinding.World || !(Paused || ReplayMode)) return;
            _observerPosition += worldDelta;
            ApplyCameraViewPose(true);
        }

        public void RotateObserver(Vector2 yawPitchDelta)
        {
            if (_cameraState.Binding != CameraBinding.World || !(Paused || ReplayMode)) return;
            var euler = _observerRotation.eulerAngles;
            euler.y += yawPitchDelta.x;
            euler.x = Mathf.Clamp(Mathf.DeltaAngle(0, euler.x) - yawPitchDelta.y, -85, 85);
            euler.z = 0;
            _observerRotation = Quaternion.Euler(euler);
            ApplyCameraViewPose(true);
        }

        public void ReturnObserverToAircraft()
        {
            _observerPoseInitialized = false;
            SetCameraState(CameraFamily.Follow, CameraPreset.Near, ReplayMode ? CameraContext.Replay : CameraContext.LiveFlight);
        }

        public CameraSnapshot CreateCameraSnapshot(CameraContext context, string source)
        {
            var state = _cameraState.WithContext(context);
            return new CameraSnapshot(Record?.id, ReplayMode ? ReplayTime : Elapsed, state, View, source,
                state.Family == CameraFamily.VideoFpv ? "video-fpv/unavailable" : "rectilinear/default", Application.version);
        }

        private void InitializeCameraState()
        {
            // v4 default is a true socket-bound Nose view for every workspace; approved templates may override via SetCameraState.
            var initial = new CameraState(CameraFamily.NosePilot, CameraPreset.Nose, CameraContext.LiveFlight);
            _cameraState = initial;
            _activeFovViewId = CameraStateCatalog.LegacyViewId(initial);
            _observerPoseInitialized = false;
            _cameraObstructed = false;
            _cameraVisibleFraction = 1;
            ReplayCamera = ReplayCameraSource.Analysis;
        }

        private void EnterReplayCameraState()
        {
            ReplayCamera = Record?.frames != null && Record.frames.Any(frame => frame.cameraSnapshot?.IsValid == true)
                ? ReplayCameraSource.Recorded : ReplayCameraSource.Analysis;
            _cameraState = _cameraState.WithContext(CameraContext.Replay);
            if (ReplayCamera == ReplayCameraSource.Recorded) ApplyRecordedCameraAt(0);
        }

        private void ApplyRecordedCameraAt(float time)
        {
            if (Record?.frames == null) return;
            var frame = Record.frames.LastOrDefault(value => value.time <= time && value.cameraSnapshot?.IsValid == true)
                ?? Record.frames.FirstOrDefault(value => value.cameraSnapshot?.IsValid == true);
            if (frame?.cameraSnapshot == null || !frame.cameraSnapshot.TryGetState(CameraContext.Replay, out var recorded)) return;
            _cameraState = recorded;
            _activeFovViewId = CameraStateCatalog.LegacyViewId(recorded);
            if (View != null)
            {
                View.transform.SetPositionAndRotation(frame.cameraSnapshot.Position, frame.cameraSnapshot.Rotation);
                if (!global::AeroSim.AeroSimApplication.IsFovXrControlled) View.fieldOfView = frame.cameraSnapshot.VerticalFovDeg;
            }
        }

        private void CaptureObserverPoseFromView()
        {
            if (Body != null && Config != null)
            {
                float distance = Mathf.Max(2f, AircraftCatalog.Resolve(Config.aircraft).ChaseDistance);
                _observerPosition = Position + new Vector3(distance * 1.65f, distance * .72f, -distance * .65f);
                _observerRotation = Quaternion.LookRotation(Position - _observerPosition, Vector3.up);
            }
            else if (View != null)
            {
                _observerPosition = View.transform.position;
                _observerRotation = View.transform.rotation;
            }
            else
            {
                _observerPosition = Position + new Vector3(5, 3, -5);
                _observerRotation = Quaternion.LookRotation(Position - _observerPosition, Vector3.up);
            }
            _observerPoseInitialized = true;
        }

        private Transform AircraftCameraSocket()
        {
            var model = Body == null ? null : Body.GetComponentInChildren<AircraftVisualModel>();
            if (model == null) return null;
            var socketId = CameraStateCatalog.Get(_cameraState.Family, _cameraState.Preset).SocketId;
            return !string.IsNullOrEmpty(socketId) && model.CameraSockets.TryGetValue(socketId, out var socket) ? socket : null;
        }

        private void ResolveCameraPose(out Vector3 desired, out Quaternion rotation, out float followRate)
        {
            float size = Mathf.Max(.1f, AircraftCatalog.Resolve(Config.aircraft).VisualScale);
            float distance = Mathf.Max(2f, AircraftCatalog.Resolve(Config.aircraft).ChaseDistance);
            var socket = AircraftCameraSocket();
            switch (_cameraState.Family)
            {
                case CameraFamily.NosePilot:
                case CameraFamily.VideoFpv:
                    desired = socket != null ? socket.position : Position + Body.rotation * new Vector3(0, size * .03f, size * .60f);
                    rotation = socket != null ? socket.rotation : Body.rotation * Quaternion.Euler(-LocalStore.Profile.settings.cameraAngle, 0, 0);
                    followRate = 0;
                    return;
                case CameraFamily.Mount:
                    desired = socket != null ? socket.TransformPoint(new Vector3(0, size * .29f, -size * .02f)) : Position + Body.rotation * new Vector3(0, size * .24f, size * .18f);
                    rotation = Body.rotation * Quaternion.Euler(-Mathf.Max(4, LocalStore.Profile.settings.cameraAngle * .55f), 0, 0);
                    followRate = 0;
                    return;
                case CameraFamily.Gimbal:
                    desired = socket != null ? socket.position : Position + Body.rotation * new Vector3(size * .16f, -size * .12f, size * .38f);
                    rotation = _cameraState.Preset == CameraPreset.GimbalFpv
                        ? Body.rotation * Quaternion.Euler(18, 0, 0)
                        : Quaternion.LookRotation(Body.rotation * Vector3.forward, Vector3.up);
                    followRate = 18;
                    return;
                case CameraFamily.Follow:
                    if (_cameraState.Preset == CameraPreset.Far)
                    {
                        desired = Position + Body.rotation * new Vector3(distance * .18f, distance * .82f, -distance * 2.25f);
                        rotation = Quaternion.LookRotation(Position + Body.rotation * Vector3.forward * distance * .85f - desired, Vector3.up);
                        followRate = 4;
                    }
                    else
                    {
                        desired = Position + Body.rotation * new Vector3(0, distance * .43f, -distance);
                        rotation = Quaternion.LookRotation(Position + Body.rotation * Vector3.forward * distance * .6f - desired, Vector3.up);
                        followRate = 8;
                    }
                    desired = ResolveFollowCollision(Position + Vector3.up * size * .15f, desired);
                    return;
                case CameraFamily.Observer:
                    if (!_observerPoseInitialized) CaptureObserverPoseFromView();
                    desired = _observerPosition;
                    rotation = _cameraState.Preset == CameraPreset.Fixed ? Quaternion.LookRotation(Position - desired, Vector3.up) : _observerRotation;
                    followRate = 0;
                    return;
                default:
                    desired = Position + Body.rotation * new Vector3(0, distance * .43f, -distance);
                    rotation = Quaternion.LookRotation(Position - desired, Vector3.up);
                    followRate = 8;
                    return;
            }
        }

        private Vector3 ResolveFollowCollision(Vector3 target, Vector3 desired)
        {
            var delta = desired - target;
            float distance = delta.magnitude;
            if (distance <= CameraCollisionPadding)
            {
                _cameraObstructed = true;
                _cameraVisibleFraction = 0;
                return target;
            }
            var direction = delta / distance;
            float nearest = distance;
            foreach (var hit in Physics.SphereCastAll(target + direction * CameraCollisionRadius, CameraCollisionRadius,
                         direction, Mathf.Max(0, distance - CameraCollisionRadius), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null || hit.collider.attachedRigidbody == Body) continue;
                nearest = Mathf.Min(nearest, hit.distance + CameraCollisionRadius - CameraCollisionPadding);
            }
            _cameraObstructed = nearest < distance;
            _cameraVisibleFraction = Mathf.Clamp01(nearest / distance);
            return target + direction * Mathf.Max(CameraCollisionPadding, nearest);
        }

        private void ApplyCameraStatePose(bool immediate)
        {
            if (View == null || Body == null || Config == null) return;
            if (ReplayMode && ReplayCamera == ReplayCameraSource.Recorded)
            {
                ApplyRecordedCameraAt(ReplayTime);
                return;
            }
            ResolveCameraPose(out var desired, out var rotation, out var followRate);
            if (_cameraState.Family != CameraFamily.Follow)
            {
                _cameraObstructed = false;
                _cameraVisibleFraction = 1;
            }
            if (immediate || followRate <= 0 || LocalStore.Profile.settings.reducedMotion)
            {
                View.transform.SetPositionAndRotation(desired, rotation);
                return;
            }
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * followRate);
            View.transform.position = Vector3.Lerp(View.transform.position, desired, blend);
            View.transform.rotation = Quaternion.Slerp(View.transform.rotation, rotation, blend);
        }
    }
}
