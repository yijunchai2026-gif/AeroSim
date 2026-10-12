using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.Domain
{
    public enum CameraContext { LiveFlight, Replay, PhotoCapture, SettingsPreview }
    public enum CameraFamily { NosePilot, VideoFpv, Mount, Gimbal, Follow, Observer, Map }
    public enum CameraPreset { Nose, Video, HighMount, GimbalFollow, GimbalFpv, TargetLock, Near, Far, Orbit, Free, Fixed, Map }
    public enum CameraCapability { Available, Experimental, PreviewOnly, Locked }
    public enum CameraBinding { AircraftSocket, TargetRelative, World }

    public readonly struct CameraState : IEquatable<CameraState>
    {
        public CameraFamily Family { get; }
        public CameraPreset Preset { get; }
        public CameraContext Context { get; }
        public CameraCapability Capability { get; }
        public CameraBinding Binding { get; }

        public CameraState(CameraFamily family, CameraPreset preset, CameraContext context)
        {
            var definition = CameraStateCatalog.Get(family, preset);
            Family = family;
            Preset = preset;
            Context = context;
            Capability = definition.Capability;
            Binding = definition.Binding;
        }

        public CameraState WithContext(CameraContext context) => new(Family, Preset, context);
        public bool Equals(CameraState other) => Family == other.Family && Preset == other.Preset && Context == other.Context;
        public override bool Equals(object obj) => obj is CameraState other && Equals(other);
        public override int GetHashCode() => ((int)Family * 397) ^ ((int)Preset * 31) ^ (int)Context;
        public static bool operator ==(CameraState left, CameraState right) => left.Equals(right);
        public static bool operator !=(CameraState left, CameraState right) => !left.Equals(right);
    }

    public sealed class CameraStateDefinition
    {
        public CameraFamily Family { get; }
        public CameraPreset Preset { get; }
        public CameraCapability Capability { get; }
        public CameraBinding Binding { get; }
        public string LegacyViewId { get; }
        public string SocketId { get; }
        public string UnavailableReason { get; }

        public CameraStateDefinition(CameraFamily family, CameraPreset preset, CameraCapability capability,
            CameraBinding binding, string legacyViewId, string socketId = "", string unavailableReason = "")
        {
            Family = family;
            Preset = preset;
            Capability = capability;
            Binding = binding;
            LegacyViewId = legacyViewId;
            SocketId = socketId;
            UnavailableReason = unavailableReason;
        }
    }

    public static class CameraStateCatalog
    {
        public const int SchemaVersion = 1;
        private static readonly CameraStateDefinition[] Definitions =
        {
            new(CameraFamily.NosePilot, CameraPreset.Nose, CameraCapability.Available, CameraBinding.AircraftSocket, "fpv", "flight-camera"),
            new(CameraFamily.VideoFpv, CameraPreset.Video, CameraCapability.Locked, CameraBinding.AircraftSocket, "fpv", "flight-camera", "Video link and lens simulation are not implemented."),
            new(CameraFamily.Mount, CameraPreset.HighMount, CameraCapability.Available, CameraBinding.AircraftSocket, "cockpit", "flight-camera"),
            new(CameraFamily.Gimbal, CameraPreset.GimbalFollow, CameraCapability.Experimental, CameraBinding.AircraftSocket, "gimbal", "flight-camera"),
            new(CameraFamily.Gimbal, CameraPreset.GimbalFpv, CameraCapability.PreviewOnly, CameraBinding.AircraftSocket, "gimbal", "flight-camera", "Independent gimbal FPV arbitration is preview-only."),
            new(CameraFamily.Gimbal, CameraPreset.TargetLock, CameraCapability.Locked, CameraBinding.AircraftSocket, "gimbal", "flight-camera", "Target lock is not implemented."),
            new(CameraFamily.Follow, CameraPreset.Near, CameraCapability.Available, CameraBinding.TargetRelative, "near-follow"),
            new(CameraFamily.Follow, CameraPreset.Far, CameraCapability.Available, CameraBinding.TargetRelative, "far-follow"),
            new(CameraFamily.Observer, CameraPreset.Orbit, CameraCapability.Experimental, CameraBinding.World, "external"),
            new(CameraFamily.Observer, CameraPreset.Free, CameraCapability.Experimental, CameraBinding.World, "external"),
            new(CameraFamily.Observer, CameraPreset.Fixed, CameraCapability.Available, CameraBinding.World, "external"),
            new(CameraFamily.Map, CameraPreset.Map, CameraCapability.Locked, CameraBinding.World, "external", "", "Map camera is not implemented.")
        };

        public static IReadOnlyList<CameraStateDefinition> All => Definitions;

        public static CameraStateDefinition Get(CameraFamily family, CameraPreset preset)
        {
            foreach (var definition in Definitions)
                if (definition.Family == family && definition.Preset == preset) return definition;
            throw new ArgumentException($"Invalid camera family/preset pair: {family}/{preset}.");
        }

        public static bool TryFromLegacyViewId(string viewId, CameraContext context, out CameraState state)
        {
            switch (viewId)
            {
                case "fpv": state = new CameraState(CameraFamily.NosePilot, CameraPreset.Nose, context); return true;
                case "cockpit": state = new CameraState(CameraFamily.Mount, CameraPreset.HighMount, context); return true;
                case "gimbal": state = new CameraState(CameraFamily.Gimbal, CameraPreset.GimbalFollow, context); return true;
                case "chase":
                case "near-follow": state = new CameraState(CameraFamily.Follow, CameraPreset.Near, context); return true;
                case "far-follow": state = new CameraState(CameraFamily.Follow, CameraPreset.Far, context); return true;
                case "external": state = new CameraState(CameraFamily.Observer, CameraPreset.Fixed, context); return true;
                default: state = default; return false;
            }
        }

        public static string LegacyViewId(CameraState state) => Get(state.Family, state.Preset).LegacyViewId;
        public static bool IsSelectable(CameraState state)
        {
            try
            {
                var definition = Get(state.Family, state.Preset);
                return definition.Capability == CameraCapability.Available || definition.Capability == CameraCapability.Experimental;
            }
            catch (ArgumentException) { return false; }
        }
    }

    [Serializable]
    public sealed class CameraSnapshot
    {
        [SerializeField] private int schemaVersion = CameraStateCatalog.SchemaVersion;
        [SerializeField] private string sessionId = "";
        [SerializeField] private float time;
        [SerializeField] private string family = "";
        [SerializeField] private string preset = "";
        [SerializeField] private string context = "";
        [SerializeField] private string capability = "";
        [SerializeField] private string binding = "";
        [SerializeField] private string source = "analysis";
        [SerializeField] private Vector3 position;
        [SerializeField] private Quaternion rotation = Quaternion.identity;
        [SerializeField] private float verticalFovDeg;
        [SerializeField] private float aspect;
        [SerializeField] private string projection = "Perspective";
        [SerializeField] private Matrix4x4 projectionMatrix = Matrix4x4.identity;
        [SerializeField] private string lensProfile = "rectilinear/default";
        [SerializeField] private string build = "";

        public int SchemaVersion => schemaVersion;
        public string SessionId => sessionId;
        public float Time => time;
        public string Family => family;
        public string Preset => preset;
        public string Context => context;
        public string Capability => capability;
        public string Binding => binding;
        public string Source => source;
        public Vector3 Position => position;
        public Quaternion Rotation => rotation;
        public float VerticalFovDeg => verticalFovDeg;
        public float Aspect => aspect;
        public string Projection => projection;
        public Matrix4x4 ProjectionMatrix => projectionMatrix;
        public string LensProfile => lensProfile;
        public string Build => build;
        public bool IsValid => schemaVersion == CameraStateCatalog.SchemaVersion && !string.IsNullOrEmpty(family) &&
            !string.IsNullOrEmpty(preset) && verticalFovDeg > 0 && aspect > 0;

        public CameraSnapshot(string capturedSessionId, float capturedTime, CameraState state, Camera camera,
            string snapshotSource, string capturedLensProfile, string capturedBuild)
        {
            sessionId = capturedSessionId ?? "";
            time = capturedTime;
            family = state.Family.ToString();
            preset = state.Preset.ToString();
            context = state.Context.ToString();
            capability = state.Capability.ToString();
            binding = state.Binding.ToString();
            source = string.IsNullOrEmpty(snapshotSource) ? "analysis" : snapshotSource;
            position = camera == null ? Vector3.zero : camera.transform.position;
            rotation = camera == null ? Quaternion.identity : camera.transform.rotation;
            verticalFovDeg = camera == null ? 0 : camera.fieldOfView;
            aspect = camera == null || camera.aspect <= 0 ? 16f / 9f : camera.aspect;
            projection = camera != null && camera.orthographic ? "Orthographic" : "Perspective";
            projectionMatrix = camera == null ? Matrix4x4.identity : camera.projectionMatrix;
            lensProfile = string.IsNullOrEmpty(capturedLensProfile) ? "rectilinear/default" : capturedLensProfile;
            build = capturedBuild ?? "";
        }

        public bool TryGetState(CameraContext targetContext, out CameraState state)
        {
            if (Enum.TryParse(family, out CameraFamily parsedFamily) && Enum.TryParse(preset, out CameraPreset parsedPreset))
            {
                try { state = new CameraState(parsedFamily, parsedPreset, targetContext); return true; }
                catch (ArgumentException) { }
            }
            state = default;
            return false;
        }
    }
}
