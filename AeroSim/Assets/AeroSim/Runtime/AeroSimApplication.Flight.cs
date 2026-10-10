using System;
using System.Collections;
using System.IO;
using AeroSim.Domain;
using AeroSim.Input;
using AeroSim.Navigation;
using AeroSim.Simulation;
using UnityEngine;
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
                hud.Add(new TouchStick(true)); hud.Add(new TouchStick(false));
            }
            _pauseOverlay = Element("modal-overlay", "is-hidden");
            var pause = Panel("Flight paused", "Physics, clock and scoring are frozen. No control input is applied while paused.");
            pause.Add(ActionButton("Resume Flight", TogglePause, true));
            pause.Add(ActionButton("Reset Aircraft", () => { _sim.ResetAircraft(); TogglePause(); }));
            pause.Add(ActionButton("Simulate Signal Loss", () => { TogglePause(); _sim.SignalLoss(); }));
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
            if (_sim.Paused) DismissToast();
            FlightInput.TouchLeft = FlightInput.TouchRight = Vector2.zero;
            if (_sim.Paused) _pauseOverlay.Query<Button>().ToList().Find(b => b.enabledInHierarchy)?.Focus();
            else (_root.panel?.focusController.focusedElement as VisualElement)?.Blur();
        }
        private void ShowConfirm(string title, string description, Action confirm)
        {
            _flightDialogClose?.Invoke();
            var overlay = Element("modal-overlay"); var p = Panel(title, description);
            bool pauseForReading = _page == Page.Flight && !_sim.Paused;
            if (pauseForReading) _sim.Pause(true);
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
        private void Update()
        {
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
            var i4 = _sim.CurrentInput; _sticks.text = "INPUT   T " + i4.x.ToString("F2") + "   Y " + i4.y.ToString("F2") + "   P " + i4.z.ToString("F2") + "   R " + i4.w.ToString("F2");
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

        private sealed class TouchStick : VisualElement
        {
            private readonly bool _left;
            private readonly VisualElement _knob;
            private int _pointer = -1;
            public TouchStick(bool left)
            {
                _left = left; AddToClassList("touch-stick"); AddToClassList(left ? "stick-left" : "stick-right");
                Add(Text(left ? "THROTTLE / YAW" : "PITCH / ROLL", "stick-caption")); _knob = Element("stick-knob"); Add(_knob);
                RegisterCallback<PointerDownEvent>(e => { _pointer = e.pointerId; this.CapturePointer(e.pointerId); Move(e.position); e.StopPropagation(); });
                RegisterCallback<PointerMoveEvent>(e => { if (_pointer == e.pointerId) Move(e.position); });
                RegisterCallback<PointerUpEvent>(e => { if (_pointer == e.pointerId) { Reset(); this.ReleasePointer(e.pointerId); } });
                RegisterCallback<PointerCaptureOutEvent>(e => Reset());
            }
            public void ConfigureLayout(float size, float bottom, float edge, bool hideCaption)
            {
                style.width = size; style.height = size; style.bottom = bottom;
                if (_left) style.left = edge; else style.right = edge;
                float knobSize = Mathf.Max(16, size * .4f);
                _knob.style.width = knobSize; _knob.style.height = knobSize;
                var caption = this.Q<Label>(className: "stick-caption");
                caption.style.top = size + 4; caption.style.left = -8; caption.style.right = -8;
                caption.style.display = hideCaption ? DisplayStyle.None : DisplayStyle.Flex;
                tooltip = _left ? "Throttle / yaw touch control" : "Pitch / roll touch control";
            }
            private void Move(Vector3 position)
            {
                var local = this.WorldToLocal(position); float radius = resolvedStyle.width * .5f;
                float travel = Mathf.Max(1, radius - _knob.resolvedStyle.width * .5f);
                var v = Vector2.ClampMagnitude(new Vector2(local.x - radius, radius - local.y) / travel, 1);
                if (_left) FlightInput.TouchLeft = v; else FlightInput.TouchRight = v;
                _knob.style.translate = new Translate(v.x * travel, -v.y * travel);
            }
            private void Reset() { _pointer = -1; if (_left) FlightInput.TouchLeft = Vector2.zero; else FlightInput.TouchRight = Vector2.zero; _knob.style.translate = new Translate(0, 0); }
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
