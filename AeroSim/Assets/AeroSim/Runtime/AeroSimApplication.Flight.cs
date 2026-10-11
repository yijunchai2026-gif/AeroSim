using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroSim.Domain;
using AeroSim.Input;
using AeroSim.Navigation;
using AeroSim.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AeroSim
{
    public sealed partial class AeroSimApplication
    {
        private Label _flightStatus, _objective, _speed, _altitude, _timer, _battery, _sticks;
        private VisualElement _pauseOverlay;
        private Action _flightDialogClose;
        private Slider _replaySlider;
        private Button _replayPlay;
        private bool _seeking;
        private MiniMap _map;
        private float _uiTick;
        private string _flightInputInterrupt;
        private int _flightScreenWidth, _flightScreenHeight;
        private Rect _flightSafeArea;
        private float _flightPanelScale;
        private TouchStick _leftTouchStick, _rightTouchStick;

        public void StartFlight(FlightConfig config)
        {
            ReleaseHangarPreviews();
            config.aircraft=AircraftCatalog.Resolve(config.aircraft).Name;
            _config = config; _page = Page.Flight;
            EnsureSimulator(); _sim.Begin(config); BuildFlightUi(false);
        }
        public void BeginReplay(SessionRecord record)
        {
            ReleaseHangarPreviews();
            if (record.frames.Count == 0) { ShowToast("No telemetry was recorded for this session."); return; }
            _workspace = record.workspace == "FPV" ? TrainingWorkspace.Fpv : record.workspace == "Professional" ? TrainingWorkspace.Professional : TrainingWorkspace.Aerial;
            _record = record; _page = Page.Replay; EnsureSimulator(); _sim.BeginReplay(record); BuildFlightUi(true);
        }
        private void EnsureSimulator()
        {
            if (_sim != null) return;
            _sim = gameObject.AddComponent<FlightSimulator>();
            _sim.Notice += ShowToast;
            _sim.PhotoCaptureRequested += SavePhotoEvidence;
            _sim.PauseRequested += TogglePause;
            _sim.Finished += record => { _record = record; ShowPage(Page.Results); };
        }
        private void BuildLegacyFlightUi(bool replay)
        {
            _root.Clear(); _root.AddToClassList("flight-root");
            RefreshUiPreferences();
            FlightInput.TouchLeft = FlightInput.TouchRight = Vector2.zero;
            _flightInputInterrupt = null;
            _leftTouchStick = _rightTouchStick = null;
            _flightScreenWidth = Screen.width; _flightScreenHeight = Screen.height;
            _flightSafeArea = Screen.safeArea; _flightPanelScale = _document.panelSettings.scale;
            var hud = Element("flight-hud"); hud.pickingMode = PickingMode.Ignore; _root.Add(hud);
            var bar = Element("flight-bar");
            bar.Add(Text(replay ? "AEROSIM  /  REPLAY" : "AEROSIM  /  " + _config.workspace.ToUpperInvariant(), "flight-brand"));
            _flightStatus = Text(replay ? _record.title : _config.title, "flight-status"); bar.Add(_flightStatus);
            bar.Add(ActionButton(replay ? "Back to Records" : "Pause", () => { if (replay) ShowPage(Page.Records); else TogglePause(); })); hud.Add(bar);
            var objectiveToggle = ActionButton("Objective", () => ShowObjective());
            objectiveToggle.AddToClassList("objective-toggle"); bar.Insert(1, objectiveToggle);

            var objectiveCard = Element("objective-card");
            objectiveCard.Add(Text(replay ? "RECORDED SESSION" : "SESSION OBJECTIVE", "page-eyebrow"));
            objectiveCard.Add(Text(AircraftCatalog.Resolve(_sim.Config.aircraft).Name, "aircraft-hud-name"));
            _objective = Text(replay ? _record.title + " · " + _record.score + "/100" : _config.objective, "wrap"); objectiveCard.Add(_objective); hud.Add(objectiveCard);
            var settings = LocalStore.Profile.settings;
            _speed = HudMetric(hud, "SPEED", settings.speedPosition, settings.osdSpeed);
            _altitude = HudMetric(hud, "ALTITUDE", settings.altitudePosition, settings.osdAltitude);
            _timer = HudMetric(hud, "TIME", settings.timerPosition, settings.osdTimer);
            _battery = HudMetric(hud, "BATTERY", settings.batteryPosition, settings.osdBattery);
            _speed.parent.AddToClassList("speed-metric"); _altitude.parent.AddToClassList("altitude-metric");
            _timer.parent.AddToClassList("timer-metric"); _battery.parent.AddToClassList("battery-metric");
            _sticks = Text("", "input-readout"); hud.Add(_sticks);
            _map = new MiniMap(() => _sim); hud.Add(_map);

            var bottom = Element("flight-bottom");
            if (replay)
            {
                _replayPlay = ActionButton("Play", () => { _sim.ReplayPlaying = !_sim.ReplayPlaying; _replayPlay.text = _sim.ReplayPlaying ? "Pause" : "Play"; }, true); bottom.Add(_replayPlay);
                _replaySlider = new Slider(0, Mathf.Max(.1f, _record.duration)) { value = 0 }; _replaySlider.AddToClassList("replay-slider");
                _replaySlider.RegisterValueChangedCallback(e => { if (!_seeking) _sim.Seek(e.newValue); }); bottom.Add(_replaySlider);
                var rate = 1f; bottom.Add(ActionButton("Speed", () => { rate = rate == 1 ? 2 : rate == 2 ? .5f : 1; _sim.ReplaySpeed = rate; ShowToast("Replay speed: " + rate + "x"); }));
                bottom.Add(ActionButton("Camera", () => _sim.ChaseView = !_sim.ChaseView));
            }
            else
            {
                bottom.Add(ActionButton("Return Home", _sim.StartRth)); bottom.Add(ActionButton("Land", _sim.Land));
                bottom.Add(ActionButton("Camera", () => _sim.ChaseView = !_sim.ChaseView));
                bottom.Add(ActionButton("Photo", () => _sim.CapturePhoto()));
                bottom.Add(ActionButton("Finish Session", _sim.Finish, true));
            }
            var actions = new ScrollView(ScrollViewMode.Horizontal);
            actions.AddToClassList("hud-actions-scroll");
            actions.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            actions.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            bottom.RemoveFromClassList("flight-bottom"); bottom.AddToClassList("hud-actions-row");
            actions.Add(bottom);
            var footer = Element("flight-bottom"); footer.Add(actions); hud.Add(footer);
            if (!replay)
            {
                if (settings.showControls) hud.Add(Text("WASD move  ·  R/F altitude  ·  Q/E yaw  ·  H RTH  ·  C camera  ·  P photo  ·  Esc pause", "flight-guidance", "wrap"));
                bool holdThrottle = string.Equals(_config.mode, "Manual", StringComparison.OrdinalIgnoreCase);
                _leftTouchStick = new TouchStick(true, holdThrottle); _rightTouchStick = new TouchStick(false);
                hud.Add(_leftTouchStick); hud.Add(_rightTouchStick);
            }
            _pauseOverlay = Element("modal-overlay", "is-hidden");
            var pause = Panel("Flight paused", "Physics, clock and scoring are frozen. No control input is applied while paused.");
            pause.Add(ActionButton("Resume Flight", TogglePause, true));
            pause.Add(ActionButton("Reset Aircraft", () => { _sim.ResetAircraft(); TogglePause(); }));
            pause.Add(ActionButton("Simulate Signal Loss", () => { TogglePause(); _sim.SignalLoss(); }));
            pause.Add(ActionButton("Camera & FOV", ShowFlightFovSettings));
            pause.Add(ActionButton("Finish & Save", _sim.Finish));
            pause.Add(ActionButton("Exit Without Saving", () => ShowConfirm("Leave this flight?", "The current session will not be saved. Existing records are unaffected.", () => ShowPage(Page.Home))));
            _pauseOverlay.Add(ModalScroll(pause)); hud.Add(_pauseOverlay);
            InstallToast();
            OnGeometryChanged(null);
        }
        private static Label HudMetric(VisualElement hud, string caption, Vector2 position, bool show)
        {
            var metric = Element("hud-metric"); metric.pickingMode = PickingMode.Ignore;
            metric.style.left = UnityEngine.UIElements.Length.Percent(position.x * 100); metric.style.top = UnityEngine.UIElements.Length.Percent(position.y * 100);
            metric.Add(Text(caption, "hud-caption")); var label = Text("—", "hud-value"); metric.Add(label); hud.Add(metric);
            metric.style.display = show ? DisplayStyle.Flex : DisplayStyle.None; return label;
        }
        private void TogglePause()
        {
            if (_page != Page.Flight || _sim == null) return;
            if (_flightDialogClose != null) { _flightDialogClose(); return; }
            _sim.Pause(!_sim.Paused); _pauseOverlay.EnableInClassList("is-hidden", !_sim.Paused);
            if (_sim.Paused) { _flightInputInterrupt = "Paused · touch again after resume"; ReleaseTouchControls("Paused"); DismissToast(); }
            else _flightInputInterrupt = "Touch a stick or use a flight control to resume input";
            if (_sim.Paused) _pauseOverlay.Query<Button>().ToList().Find(b => b.enabledInHierarchy)?.Focus();
            else (_root.panel?.focusController.focusedElement as VisualElement)?.Blur();
        }
        private void ShowConfirm(string title, string description, Action confirm)
        {
            _flightDialogClose?.Invoke();
            var overlay = Element("modal-overlay"); var p = Panel(title, description);
            bool pauseForReading = _page == Page.Flight && !_sim.Paused;
            if (pauseForReading) _sim.Pause(true);
            if (_page == Page.Flight) { _flightInputInterrupt = "Dialog open · touch again after closing"; ReleaseTouchControls("Paused"); }
            DismissToast();
            void Close() { overlay.RemoveFromHierarchy(); _flightDialogClose = null; if (pauseForReading) _sim?.Pause(false); RestoreFlightDialogFocus(); }
            _flightDialogClose = Close;
            p.Add(Row(ActionButton("Keep Flying", Close), ActionButton("Confirm Exit", () => { Close(); confirm(); }, true)));
            overlay.Add(ModalScroll(p)); InstallDesignModalFocus(overlay); _root.Add(overlay);
            ApplyTypography(overlay);
            overlay.Query<Button>().First()?.Focus();
        }
        private void ShowObjective()
        {
            _flightDialogClose?.Invoke();
            var overlay = Element("modal-overlay");
            var panel = Panel("Session objective", _objective.text);
            bool pauseForReading = _page == Page.Flight && !_sim.Paused;
            if (pauseForReading) _sim.Pause(true);
            if (_page == Page.Flight) { _flightInputInterrupt = "Dialog open · touch again after closing"; ReleaseTouchControls("Paused"); }
            DismissToast();
            void Close() { overlay.RemoveFromHierarchy(); _flightDialogClose = null; if (pauseForReading) _sim?.Pause(false); RestoreFlightDialogFocus(); }
            _flightDialogClose = Close;
            panel.Add(ActionButton("Back to Flight", Close, true));
            overlay.Add(ModalScroll(panel)); InstallDesignModalFocus(overlay); _root.Add(overlay);
            ApplyTypography(overlay);
            overlay.Query<Button>().First()?.Focus();
        }
        private void RestoreFlightDialogFocus()
        {
            if (_page != Page.Flight || _sim == null) return;
            if (_sim.Paused && _pauseOverlay != null && !_pauseOverlay.ClassListContains("is-hidden"))
                _pauseOverlay.Query<Button>().ToList().Find(b => b.enabledInHierarchy)?.Focus();
            else (_root.panel?.focusController.focusedElement as VisualElement)?.Blur();
        }
        private static ScrollView ModalScroll(VisualElement panel)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("modal-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.Add(panel); return scroll;
        }
        private void ReleaseTouchControls(string reason)
        {
            FlightInput.TouchLeft = FlightInput.TouchRight = Vector2.zero;
            _leftTouchStick?.Interrupt(reason);
            _rightTouchStick?.Interrupt(reason);
        }

        private void InterruptFlightInput(string reason, bool pause)
        {
            if (_page != Page.Flight || _sim == null) return;
            _flightInputInterrupt = reason;
            ReleaseTouchControls(reason);
            if (pause && !_sim.Paused) _sim.Pause(true);
            if (pause && _pauseOverlay != null) _pauseOverlay.EnableInClassList("is-hidden", false);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) InterruptFlightInput("Window focus lost · touch again to resume", true);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) InterruptFlightInput("Application interrupted · touch again to resume", true);
        }

        private string DescribeFlightInputSource()
        {
            bool captured = (_leftTouchStick?.Active ?? false) || (_rightTouchStick?.Active ?? false);
            bool touch = captured || FlightInput.TouchLeft.sqrMagnitude > .0001f || FlightInput.TouchRight.sqrMagnitude > .0001f;
            var keyboard = Keyboard.current;
            bool keys = keyboard != null && (keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed ||
                keyboard.qKey.isPressed || keyboard.eKey.isPressed || keyboard.rKey.isPressed || keyboard.fKey.isPressed ||
                keyboard.upArrowKey.isPressed || keyboard.downArrowKey.isPressed || keyboard.leftArrowKey.isPressed || keyboard.rightArrowKey.isPressed ||
                keyboard.pageUpKey.isPressed || keyboard.pageDownKey.isPressed);
            var gamepad = Gamepad.current;
            var joystickDevice = Joystick.current;
            bool pad = (gamepad != null && (gamepad.leftStick.ReadValue().sqrMagnitude > .01f || gamepad.rightStick.ReadValue().sqrMagnitude > .01f)) ||
                (joystickDevice != null && joystickDevice.stick.ReadValue().sqrMagnitude > .01f);
            bool joystick = joystickDevice != null;
            int active = (touch ? 1 : 0) + (keys ? 1 : 0) + (pad ? 1 : 0);
            if (active > 0 && _sim != null && !_sim.Paused) _flightInputInterrupt = null;
            if (!string.IsNullOrEmpty(_flightInputInterrupt)) return "INTERRUPTED · " + _flightInputInterrupt.ToUpperInvariant();
            if (active > 1) return ("MIXED INPUT · " + (touch ? "TOUCH + " : "") + (keys ? "KEYBOARD + " : "") + (pad ? "CONTROLLER" : "")).TrimEnd(' ', '+');
            if (touch) return "TOUCH STICKS · MODE 2";
            if (keys) return "KEYBOARD · ACTIVE";
            if (pad) return "EXTERNAL CONTROLLER · ACTIVE";
            if (gamepad != null || joystick) return "INPUT READY · EXTERNAL + TOUCH + KEYBOARD";
            return Application.isMobilePlatform ? "INPUT READY · TOUCH STICKS · MODE 2" : "INPUT READY · KEYBOARD" + (LocalStore.Profile.settings.showTouchControls ? " + TOUCH" : "");
        }

        private void Update()
        {
            bool flightLayoutChanged = _page == Page.Flight && _flightScreenWidth > 0 &&
                (_flightScreenWidth != Screen.width || _flightScreenHeight != Screen.height ||
                 _flightSafeArea != Screen.safeArea || Mathf.Abs(_flightPanelScale - _document.panelSettings.scale) > .0001f);
            if (flightLayoutChanged)
            {
                _flightScreenWidth = Screen.width; _flightScreenHeight = Screen.height;
                _flightSafeArea = Screen.safeArea; _flightPanelScale = _document.panelSettings.scale;
                ReleaseTouchControls("Layout changed · touch again");
                _flightInputInterrupt = "Layout changed · touch again";
                OnGeometryChanged(null);
            }
            if (_lastWindowWidth != Screen.width || _lastWindowHeight != Screen.height) RefreshUiPreferences();
            if (_page == Page.Controller && _axisLabels != null)
            {
                var raw = FlightInput.Raw();
                for (int i = 0; i < 4; i++)
                {
                    _axisLabels[i].text = new[] { "Throttle", "Yaw", "Pitch", "Roll" }[i] + "   " + raw[i].ToString("F2"); _axisBars[i].value = raw[i];
                    if (_calibrating) { _minimum[i] = Mathf.Min(_minimum[i], raw[i]); _maximum[i] = Mathf.Max(_maximum[i], raw[i]); }
                }
            }
            if (_sim == null || _sim.Body == null || _flightStatus == null || (_page != Page.Flight && _page != Page.Replay)) return;
            _uiTick += Time.unscaledDeltaTime; if (_uiTick < .05f) return; _uiTick = 0;
            bool replay = _page == Page.Replay;
            _flightStatus.text = replay ? (_sim.ReplayPlaying ? "REPLAY PLAYING" : "REPLAY PAUSED") : _sim.Status;
            _speed.text = Velocity(replay ? ReplaySpeedAtTime() : _sim.Speed); _altitude.text = Length(_sim.Position.y);
            _timer.text = Duration(replay ? _sim.ReplayTime : _sim.Elapsed); _battery.text = _sim.Battery.ToString("F0") + "%";
            _battery.EnableInClassList("warning-text", _sim.Battery < 25);
            var i4 = _sim.CurrentInput;
            string inputSource = replay ? "RECORDED INPUT" : DescribeFlightInputSource();
            _sticks.text = inputSource + "   T " + i4.x.ToString("F2") + "   Y " + i4.y.ToString("F2") + "   P " + i4.z.ToString("F2") + "   R " + i4.w.ToString("F2");
            if (_designInputSource != null) _designInputSource.text = inputSource;
            if (replay)
            {
                _seeking = true; _replaySlider.SetValueWithoutNotify(_sim.ReplayTime); _seeking = false;
                var playLabel = _replayPlay.Q<Label>(className: "design-button-label");
                if (playLabel != null) playLabel.text = _sim.ReplayPlaying ? "Pause" : "Play";
                else _replayPlay.text = _sim.ReplayPlaying ? "Pause" : "Play";
            }
            else
            {
                var next=_sim.NextTarget;
                _objective.text = _sim.ObjectiveComplete ? "OBJECTIVE COMPLETE · Finish Session to save your score." : _config.objective + (_config.task == "gates" ? "\nGates: " + _sim.GateIndex + " / 6" : _config.professional ? "\nCaptures: " + _sim.GateIndex + " / " + _config.mission.targets.Count : "") + (next.HasValue?" · Next: "+Length(Vector3.Distance(_sim.Position,next.Value)):"");
            }
            _map.MarkDirtyRepaint();
            TickDesignFlightUi(replay);
        }
        private float ReplaySpeedAtTime()
        {
            var f = _record.frames.FindLast(x => x.time <= _sim.ReplayTime); return f?.speed ?? 0;
        }

        internal bool RunTouchPointerContractDiagnostic()
        {
            if (_leftTouchStick == null || _rightTouchStick == null) return false;
            ReleaseTouchControls("Available");
            bool leftDown = _leftTouchStick.DiagnosticDown(11, new Vector2(.5f, .8f));
            bool rightDown = _rightTouchStick.DiagnosticDown(12, new Vector2(-.5f, .7f));
            bool fourAxes = FlightInput.TouchLeft == new Vector2(.5f, .8f) && FlightInput.TouchRight == new Vector2(-.5f, .7f);
            bool thirdRejected = !_leftTouchStick.DiagnosticDown(13, Vector2.one);
            bool crossStickRejected = !_rightTouchStick.DiagnosticDown(11, Vector2.one);
            _rightTouchStick.DiagnosticCaptureOut(99);
            bool nonOwnerIgnored = _rightTouchStick.Active && FlightInput.TouchRight == new Vector2(-.5f, .7f);
            _leftTouchStick.DiagnosticCancel(11);
            bool leftOnlyCleared = !_leftTouchStick.Active && _rightTouchStick.Active &&
                FlightInput.TouchLeft == Vector2.zero && FlightInput.TouchRight == new Vector2(-.5f, .7f);
            _rightTouchStick.DiagnosticUp(12);
            bool released = !_rightTouchStick.Active && FlightInput.TouchLeft == Vector2.zero && FlightInput.TouchRight == Vector2.zero;
            return leftDown && rightDown && fourAxes && thirdRejected && crossStickRejected && nonOwnerIgnored && leftOnlyCleared && released;
        }

        private sealed class TouchStick : VisualElement
        {
            private static readonly Dictionary<int, TouchStick> PointerOwners = new();
            private readonly bool _left;
            private readonly bool _holdThrottle;
            private readonly VisualElement _knob;
            private readonly Label _caption;
            private int _pointer = -1;
            private float _heldThrottle;
            private bool _compactCaption;

            public TouchStick(bool left, bool holdThrottle = false)
            {
                _left = left;
                _holdThrottle = left && holdThrottle;
                name = left ? "touch-stick-left" : "touch-stick-right";
                AddToClassList("touch-stick"); AddToClassList(left ? "stick-left" : "stick-right");
                focusable = false;
                _caption = Text(left ? "MODE 2 · THROTTLE / YAW" : "MODE 2 · PITCH / ROLL", "stick-caption"); Add(_caption);
                _knob = Element("stick-knob"); Add(_knob);
                SetStatus("Available");
                RegisterCallback<PointerDownEvent>(OnPointerDown);
                RegisterCallback<PointerMoveEvent>(OnPointerMove);
                RegisterCallback<PointerUpEvent>(OnPointerUp);
                RegisterCallback<PointerCancelEvent>(OnPointerCancel);
                RegisterCallback<PointerCaptureOutEvent>(e => HandleCaptureOut(e.pointerId));
                RegisterCallback<DetachFromPanelEvent>(_ => Interrupt("Unavailable"));
            }

            public bool Active => _pointer >= 0;

            public void ConfigureLayout(Rect envelope, bool compactCaption)
            {
                _compactCaption = compactCaption;
                float size = Mathf.Min(envelope.width, envelope.height - (compactCaption ? 18 : 24));
                style.display = DisplayStyle.Flex;
                style.width = size; style.height = size;
                style.left = envelope.x; style.top = envelope.y;
                style.right = new StyleLength(StyleKeyword.Auto); style.bottom = new StyleLength(StyleKeyword.Auto);
                float knobSize = Mathf.Max(44, size * .4f);
                _knob.style.width = knobSize; _knob.style.height = knobSize;
                _caption.style.top = size + (compactCaption ? 1 : 4);
                _caption.style.left = compactCaption ? -2 : -8;
                _caption.style.right = compactCaption ? -2 : -8;
                _caption.style.fontSize = compactCaption ? 9 : 11;
                _caption.style.display = DisplayStyle.Flex;
                SetStatus(CurrentStatus());
                tooltip = (_left ? "Left stick. Mode 2 throttle and yaw. " : "Right stick. Mode 2 pitch and roll. ") +
                    (_holdThrottle ? "Throttle holds on release; interruptions clear it. " : "Axes spring to centre on release. ") + CurrentStatus();
            }

            public void SetLayoutProtected(bool protectedState)
            {
                style.display = protectedState ? DisplayStyle.None : DisplayStyle.Flex;
                if (protectedState && Active) Interrupt("Layout protected");
            }

            public void Interrupt(string reason)
            {
                int pointer = _pointer;
                _pointer = -1;
                if (pointer >= 0 && PointerOwners.TryGetValue(pointer, out var owner) && owner == this) PointerOwners.Remove(pointer);
                _heldThrottle = 0;
                if (_left) FlightInput.TouchLeft = Vector2.zero; else FlightInput.TouchRight = Vector2.zero;
                _knob.style.translate = new Translate(0, 0);
                if (pointer >= 0 && this.HasPointerCapture(pointer)) this.ReleasePointer(pointer);
                SetStatus(reason);
            }

            private bool BeginPointer(int pointerId)
            {
                if (_pointer >= 0 || !enabledInHierarchy || PointerOwners.ContainsKey(pointerId)) return false;
                _pointer = pointerId;
                PointerOwners[pointerId] = this;
                SetStatus("Captured");
                return true;
            }

            private void OnPointerDown(PointerDownEvent e)
            {
                if (!BeginPointer(e.pointerId)) { e.StopImmediatePropagation(); return; }
                this.CapturePointer(e.pointerId);
                Move(e.position);
                e.StopImmediatePropagation();
            }

            private void OnPointerMove(PointerMoveEvent e)
            {
                if (_pointer != e.pointerId || !this.HasPointerCapture(e.pointerId)) return;
                Move(e.position); e.StopPropagation();
            }

            private void OnPointerUp(PointerUpEvent e)
            {
                if (_pointer != e.pointerId) return;
                Release(false); e.StopImmediatePropagation();
            }

            private void OnPointerCancel(PointerCancelEvent e)
            {
                if (_pointer != e.pointerId) return;
                Interrupt("Interrupted"); e.StopImmediatePropagation();
            }

            private void HandleCaptureOut(int pointerId)
            {
                if (_pointer == pointerId) Interrupt("Interrupted");
            }

            internal bool DiagnosticDown(int pointerId, Vector2 value)
            {
                if (!BeginPointer(pointerId)) return false;
                ApplyValue(value);
                return true;
            }

            internal void DiagnosticCaptureOut(int pointerId) => HandleCaptureOut(pointerId);
            internal void DiagnosticCancel(int pointerId) { if (_pointer == pointerId) Interrupt("Interrupted"); }
            internal void DiagnosticUp(int pointerId) { if (_pointer == pointerId) Release(false); }

            private void Move(Vector3 position)
            {
                var local = this.WorldToLocal(position); float radius = resolvedStyle.width * .5f;
                float travel = Mathf.Max(1, radius - _knob.resolvedStyle.width * .5f);
                ApplyValue(Vector2.ClampMagnitude(new Vector2(local.x - radius, radius - local.y) / travel, 1), travel);
            }

            private void ApplyValue(Vector2 value, float travel = -1)
            {
                var normalized = Vector2.ClampMagnitude(value, 1);
                if (_left) { FlightInput.TouchLeft = normalized; _heldThrottle = normalized.y; } else FlightInput.TouchRight = normalized;
                if (travel < 0)
                {
                    float radius = resolvedStyle.width * .5f;
                    travel = Mathf.Max(1, radius - _knob.resolvedStyle.width * .5f);
                }
                _knob.style.translate = new Translate(normalized.x * travel, -normalized.y * travel);
                SetStatus(normalized.sqrMagnitude > .98f ? "Captured · Limit" : "Captured");
            }

            private void Release(bool interrupted)
            {
                int pointer = _pointer; _pointer = -1;
                if (pointer >= 0 && PointerOwners.TryGetValue(pointer, out var owner) && owner == this) PointerOwners.Remove(pointer);
                if (_left && _holdThrottle && !interrupted)
                {
                    FlightInput.TouchLeft = new Vector2(0, _heldThrottle);
                    float radius = resolvedStyle.width * .5f;
                    float travel = Mathf.Max(1, radius - _knob.resolvedStyle.width * .5f);
                    _knob.style.translate = new Translate(0, -_heldThrottle * travel);
                    SetStatus("Throttle held · Touch to adjust");
                }
                else
                {
                    if (_left) FlightInput.TouchLeft = Vector2.zero; else FlightInput.TouchRight = Vector2.zero;
                    _knob.style.translate = new Translate(0, 0); SetStatus("Available");
                }
                if (pointer >= 0 && this.HasPointerCapture(pointer)) this.ReleasePointer(pointer);
            }

            private string CurrentStatus() => _caption.userData as string ?? "Available";
            private void SetStatus(string status)
            {
                _caption.userData = status;
                bool captured = status.StartsWith("Captured", StringComparison.Ordinal);
                bool limit = status.Contains("Limit");
                bool interrupted = !captured && status != "Available" && !status.StartsWith("Throttle held", StringComparison.Ordinal);
                EnableInClassList("stick-captured", captured);
                EnableInClassList("stick-at-limit", limit);
                style.opacity = interrupted ? .55f : 1;
                var ring = interrupted ? new Color(1f, .58f, .18f, .9f) : captured ? new Color(.15f, .9f, 1f, 1f) : new Color(.7f, .9f, .96f, .75f);
                style.borderLeftColor = style.borderRightColor = style.borderTopColor = style.borderBottomColor = ring;
                style.borderLeftWidth = style.borderRightWidth = style.borderTopWidth = style.borderBottomWidth = limit ? 4 : captured ? 3 : 2;
                _knob.style.backgroundColor = limit ? new Color(1f, .75f, .2f, .9f) : captured ? new Color(.2f, .85f, 1f, .85f) : new Color(.6f, .86f, .94f, .65f);
                string controls = _left ? (_compactCaption ? "L · T/Y" : "MODE 2 · T/Y") : (_compactCaption ? "R · P/R" : "MODE 2 · P/R");
                string visibleStatus = _compactCaption && status == "Available" ? "READY" : status.ToUpperInvariant();
                _caption.text = controls + " · " + visibleStatus;
                tooltip = (_left ? "Left stick. Mode 2 throttle and yaw. " : "Right stick. Mode 2 pitch and roll. ") +
                    (_holdThrottle ? "Throttle holds on release; interruptions clear it. " : "Axes spring to centre on release. ") + status;
            }
        }
        private sealed class MiniMap : VisualElement
        {
            private readonly Func<FlightSimulator> _get;
            public MiniMap(Func<FlightSimulator> get) { _get = get; AddToClassList("mini-map"); pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            private void Draw(MeshGenerationContext context)
            {
                var sim = _get(); if (sim?.Record == null) return;
                var p = context.painter2D; var w = contentRect.width; var h = contentRect.height;
                Vector2 Map(Vector3 v) => new(w * .5f + v.x * w / 180, h * .8f - v.z * h / 180);
                p.lineWidth = 1; p.strokeColor = new Color(.2f, .4f, .5f); p.BeginPath(); p.MoveTo(new Vector2(w / 2, 0)); p.LineTo(new Vector2(w / 2, h)); p.MoveTo(new Vector2(0, h * .8f)); p.LineTo(new Vector2(w, h * .8f)); p.Stroke();
                p.strokeColor = new Color(.2f, .8f, 1); p.lineWidth = 2; p.BeginPath();
                bool first = true;
                foreach (var f in sim.Record.frames)
                {
                    if (sim.ReplayMode && f.time > sim.ReplayTime) break;
                    if (first) { p.MoveTo(Map(f.position)); first = false; } else p.LineTo(Map(f.position));
                }
                p.Stroke(); p.fillColor = Color.white; p.BeginPath(); p.Arc(Map(sim.Position), 4, 0, 360); p.Fill();
                p.fillColor = new Color(.3f, 1, .7f); p.BeginPath(); p.Arc(Map(Vector3.zero), 3, 0, 360); p.Fill();
                if(sim.NextTarget.HasValue) { p.strokeColor=new Color(1,.65f,.1f); p.lineWidth=2; p.BeginPath(); p.Arc(Map(sim.NextTarget.Value),5,0,360); p.Stroke(); }
            }
        }
    }
}
