using System;
using System.Collections.Generic;
using System.Linq;
using AeroSim.Domain;
using AeroSim.Input;
using AeroSim.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace AeroSim
{
    public sealed partial class AeroSimApplication
    {
        private bool _designFlightStyleLoaded;
        private RenderTexture _designReplayTexture;
        private Image _designReplayImage;
        private readonly List<ReplayTelemetryGraph> _designGraphs = new();
        private readonly List<ReplayStickDiagram> _designSticks = new();
        private Label _designDistance, _designVerticalSpeed, _designFlightClock;
        private Label _designReplayTime, _designTargetProgress, _designTargetDistance;
        private Label _designReplayInputs;
        private VisualElement _designReplayMap;
        private readonly List<Label> _designTargetStates = new();
        private VisualElement _designOsdLayer, _designSpeedMetric, _designAltitudeMetric, _designBatteryMetric;
        private string _designOsdSafetyNotice;
        private Label _designOsdPauseNotice;
        private VisualElement _designOsdIndicator;
        private VisualElement _designFlightInputGuardRoot;

        private void BuildFlightUi(bool replay)
        {
            ReleaseDesignReplayView();
            _root.Clear();
            _root.AddToClassList("design-v1");
            _root.RemoveFromClassList("design-fpv-home");
            _root.RemoveFromClassList("design-onboarding");
            if (!_designFlightStyleLoaded)
            {
                var style = Resources.Load<StyleSheet>("UI/FlightDesignV1");
                if (style != null) _root.styleSheets.Add(style);
                _designFlightStyleLoaded = true;
            }
            RefreshUiPreferences();
            InstallDesignFlightInputGuard();
            _root.schedule.Execute(InstallDesignFlightInputGuard);
            FlightInput.TouchLeft = FlightInput.TouchRight = Vector2.zero;
            if (replay) BuildDesignReplay();
            else BuildDesignFlight();
            InstallToast();
            OnGeometryChanged(null);
        }

        private void InstallDesignFlightInputGuard()
        {
            // With no focused element, Unity dispatches navigation to the PANEL root,
            // which is above UIDocument.rootVisualElement. A document-only callback
            // cannot see that event and lets an arrow select the first HUD action.
            var guardRoot = _root?.panel?.visualTree;
            if (guardRoot == null || guardRoot == _designFlightInputGuardRoot) return;
            ReleaseDesignFlightInputGuard();
            _designFlightInputGuardRoot = guardRoot;
            guardRoot.RegisterCallback<NavigationMoveEvent>(GuardDesignFlightNavigation, TrickleDown.TrickleDown);
            guardRoot.RegisterCallback<KeyDownEvent>(GuardDesignFlightArrow, TrickleDown.TrickleDown);
        }

        private void ReleaseDesignFlightInputGuard()
        {
            if (_designFlightInputGuardRoot == null) return;
            _designFlightInputGuardRoot.UnregisterCallback<NavigationMoveEvent>(GuardDesignFlightNavigation, TrickleDown.TrickleDown);
            _designFlightInputGuardRoot.UnregisterCallback<KeyDownEvent>(GuardDesignFlightArrow, TrickleDown.TrickleDown);
            _designFlightInputGuardRoot = null;
        }

        private void GuardDesignFlightNavigation(NavigationMoveEvent e)
        {
            if (_page != Page.Flight || _sim == null || _sim.Paused || e.direction == NavigationMoveEvent.Direction.Next || e.direction == NavigationMoveEvent.Direction.Previous) return;
            _designFlightInputGuardRoot?.panel?.focusController.IgnoreEvent(e);
            e.StopImmediatePropagation();
        }

        private void GuardDesignFlightArrow(KeyDownEvent e)
        {
            if (_page != Page.Flight || _sim == null || _sim.Paused) return;
            if (e.keyCode != KeyCode.UpArrow && e.keyCode != KeyCode.DownArrow && e.keyCode != KeyCode.LeftArrow && e.keyCode != KeyCode.RightArrow) return;
            // Only consume the UI event: FlightInput still reads the raw arrow state.
            e.StopImmediatePropagation();
        }

        private void BuildDesignFlight()
        {
            _root.AddToClassList("flight-root");
            _root.AddToClassList("v1-flight-hud");
            bool fpv = _config.workspace == "FPV";
            _root.AddToClassList(fpv ? "v1-flight-fpv" : _config.professional ? "v1-flight-professional" : "v1-flight-aerial");
            var hud = Element("flight-hud"); hud.pickingMode = PickingMode.Ignore; _root.Add(hud);
            var bar = Element("flight-bar", "v1-flight-bar");
            if (!fpv) bar.Add(DesignBrand());
            var session = Text(_config.title, "v1-flight-title"); bar.Add(session);
            _flightStatus = Text(_sim.Status, "flight-status", "v1-flight-status"); bar.Add(_flightStatus);
            bar.Add(Text("SIMULATION · LOCAL", "v1-simulation-chip"));
            _designFlightClock = Text(Duration(0), "v1-flight-clock"); bar.Add(_designFlightClock);
            _designOsdIndicator = DesignIcon("chart", 16);
            _designOsdIndicator.name = "osd-availability-indicator";
            _designOsdIndicator.pickingMode = PickingMode.Position;
            _designOsdIndicator.style.display = DisplayStyle.None;
            _designOsdIndicator.style.marginRight = 8; bar.Add(_designOsdIndicator);
            bar.Add(FlightAction("Objective", "target", ShowObjective, "objective-toggle"));
            bar.Add(FlightAction("Pause", "pause", TogglePause)); hud.Add(bar);

            var objective = Element("objective-card", "v1-objective-card");
            objective.Add(Text(_config.title, "v1-objective-title"));
            objective.Add(Text(AircraftCatalog.Resolve(_config.aircraft).Name, "aircraft-hud-name"));
            _objective = Text(_config.objective, "wrap", "v1-objective-copy");
            objective.Add(_objective);
            if (_config.professional) BuildInspectionPanel(objective);
            else
            {
                objective.Add(FlightAction("Return Home", "home", _sim.StartRth, "v1-objective-primary"));
                objective.Add(Text("Confirm the return altitude clears nearby obstacles. Simulated flight only.", "v1-objective-note", "wrap"));
                var altitudeRow = Element("v1-rth-row");
                altitudeRow.Add(Text("RTH ALT  " + Length(_config.rthAltitude), "v1-rth-altitude"));
                altitudeRow.Add(FlightAction("−", "minus", () => ChangeReturnAltitude(-5)));
                altitudeRow.Add(FlightAction("+", "plus", () => ChangeReturnAltitude(5)));
                objective.Add(altitudeRow);
            }
            objective.Add(FlightAction("Finish Session", "save", _sim.Finish));
            var objectiveScroll = new ScrollView(ScrollViewMode.Vertical); objectiveScroll.AddToClassList("v1-objective-scroll");
            objectiveScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; objectiveScroll.Add(objective);
            hud.Add(objectiveScroll);

            var telemetry = Element("v1-flight-telemetry");
            _altitude = DesignHudMetric(telemetry, "H", "chart");
            _designDistance = DesignHudMetric(telemetry, "D", "flight");
            _speed = DesignHudMetric(telemetry, "H.S", "clock");
            _designVerticalSpeed = DesignHudMetric(telemetry, "V.S", "flight");
            _battery = DesignHudMetric(telemetry, "BATTERY", "battery");
            _designSpeedMetric = _speed.parent; _designSpeedMetric.name = "osd-speed";
            _designAltitudeMetric = _altitude.parent; _designAltitudeMetric.name = "osd-altitude";
            _designBatteryMetric = _battery.parent; _designBatteryMetric.name = "osd-battery";
            _timer = Text("", "v1-race-timer"); hud.Add(_timer);
            _timer.name = "osd-timer";
            hud.Add(telemetry);
            _designOsdLayer = Element("v1-custom-osd-layer");
            _designOsdLayer.pickingMode = PickingMode.Ignore; hud.Add(_designOsdLayer);
            var settings = LocalStore.Profile.settings;
            _speed.parent.style.display = settings.osdSpeed ? DisplayStyle.Flex : DisplayStyle.None;
            _altitude.parent.style.display = settings.osdAltitude ? DisplayStyle.Flex : DisplayStyle.None;
            _battery.parent.style.display = settings.osdBattery ? DisplayStyle.Flex : DisplayStyle.None;
            _timer.style.display = fpv && settings.osdTimer ? DisplayStyle.Flex : DisplayStyle.None;
            _designFlightClock.style.display = !fpv && settings.osdTimer ? DisplayStyle.Flex : DisplayStyle.None;
            _sticks = Text("", "input-readout"); hud.Add(_sticks);
            _map = new MiniMap(() => _sim); _map.AddToClassList("v1-flight-map");
            foreach (var direction in new[] { "N", "S", "E", "W" }) _map.Add(Text(direction, "v1-compass-" + direction.ToLowerInvariant()));
            _map.Add(Text("SCENE MAP", "v1-map-disclaimer")); hud.Add(_map);

            var tools = Element("v1-camera-tools");
            tools.Add(FlightAction("Photo", "camera", () => _sim.CapturePhoto()));
            tools.Add(FlightAction("Camera", "camera", () => _sim.ChaseView = !_sim.ChaseView));
            tools.Add(FlightAction("Land", "flight", _sim.Land));
            tools.Add(FlightAction("Return Home", "home", _sim.StartRth));
            tools.Add(FlightAction("Finish Session", "save", _sim.Finish)); hud.Add(tools);
            var compact = Element("v1-compact-actions");
            compact.Add(FlightAction("Photo", "camera", () => _sim.CapturePhoto()));
            compact.Add(FlightAction("RTH", "home", _sim.StartRth));
            compact.Add(FlightAction("Finish Session", "save", _sim.Finish)); hud.Add(compact);
            if (settings.showControls) hud.Add(Text("WASD move · R/F altitude · Q/E yaw · H RTH · C camera · P photo · Esc pause", "flight-guidance", "wrap"));
            hud.Add(Text("SIMULATION — NOT FOR NAVIGATION", "v1-flight-disclaimer"));
            hud.Add(new TouchStick(true)); hud.Add(new TouchStick(false));
            BuildDesignPause(hud);
        }

        private static Button FlightAction(string title, string icon, Action action, params string[] classes)
        {
            var button = ActionButton("", action); button.AddToClassList("v1-flight-action");
            button.name = "action-" + title.ToLowerInvariant().Replace(" ", "-");
            var glyph = DesignIcon(icon, 22); glyph.pickingMode = PickingMode.Ignore; button.Insert(0, glyph);
            button.Add(Text(title, "design-button-label"));
            foreach (var className in classes) button.AddToClassList(className);
            return button;
        }

        private static Label DesignHudMetric(VisualElement parent, string caption, string icon)
        {
            var metric = Element("hud-metric", "v1-telemetry-item"); metric.pickingMode = PickingMode.Ignore;
            metric.Add(DesignIcon(icon, 24)); metric.Add(Text(caption, "hud-caption"));
            var value = Text("—", "hud-value"); metric.Add(value); parent.Add(metric); return value;
        }

        private void ChangeReturnAltitude(float delta)
        {
            _config.rthAltitude = Mathf.Clamp(_config.rthAltitude + delta, 5, 120);
            _root.Q<Label>(className: "v1-rth-altitude").text = "RTH ALT  " + Length(_config.rthAltitude);
            _sim.Event("setting", "Return altitude changed to " + _config.rthAltitude.ToString("F0") + " m");
        }

        private void BuildInspectionPanel(VisualElement objective)
        {
            _designTargetProgress = Text("", "v1-inspection-progress"); objective.Add(_designTargetProgress);
            var targets = _config.mission?.targets;
            if (targets == null) return;
            for (int i = 0; i < targets.Count; i++)
            {
                var row = Element("v1-inspection-target");
                var state = Text((i + 1).ToString(), "v1-target-state"); _designTargetStates.Add(state); row.Add(state);
                string name = i < _config.mission.targetNames.Count ? _config.mission.targetNames[i] : "Target " + (i + 1);
                row.Add(Text(name, "wrap", "v1-target-name")); objective.Add(row);
            }
            _designTargetDistance = Text("", "v1-inspection-distance"); objective.Add(_designTargetDistance);
            objective.Add(Text("Evidence checks distance and target order. Image quality is not automatically evaluated.", "wrap", "v1-objective-note"));
            objective.Add(FlightAction("Capture Evidence", "camera", () => _sim.CapturePhoto(), "v1-objective-primary"));
        }

        private void BuildDesignPause(VisualElement hud)
        {
            _pauseOverlay = Element("modal-overlay", "is-hidden");
            var pause = Panel("Flight paused", "Physics, clock and scoring are frozen. Your session is preserved.");
            pause.Add(ActionButton("Resume Flight", TogglePause, true));
            pause.Add(ActionButton("Session Objective", ShowObjective));
            pause.Add(ActionButton("Flight Telemetry", ShowDesignFlightTelemetry));
            pause.Add(ActionButton("Reset Aircraft", () => { _sim.ResetAircraft(); TogglePause(); }));
            pause.Add(ActionButton("Simulate Signal Loss", () => { TogglePause(); _sim.SignalLoss(); }));
            pause.Add(ActionButton("Finish & Save", _sim.Finish));
            pause.Add(ActionButton("Exit Without Saving", () => ShowConfirm("Leave this flight?", "The current session will not be saved. Existing records are unaffected.", () => ShowPage(Page.Home))));
            pause.Add(Text("Touch controls: left stick = throttle / yaw; right stick = pitch / roll.", "muted", "wrap"));
            _designOsdPauseNotice = Text("", "muted", "wrap");
            _designOsdPauseNotice.name = "osd-availability-description";
            _designOsdPauseNotice.style.display = DisplayStyle.None; pause.Add(_designOsdPauseNotice);
            _pauseOverlay.Add(ModalScroll(pause)); InstallDesignModalFocus(_pauseOverlay); hud.Add(_pauseOverlay);
        }

        private void ShowDesignFlightTelemetry()
        {
            if (_page != Page.Flight || _sim == null) return;
            _flightDialogClose?.Invoke();
            bool pauseForReading = !_sim.Paused;
            if (pauseForReading) _sim.Pause(true);
            DismissToast();
            FlightInput.TouchLeft = FlightInput.TouchRight = Vector2.zero;
            var overlay = Element("modal-overlay");
            var panel = Panel("Flight Telemetry", "Actual simulator values, frozen while this panel is open. All metrics remain available when the HUD has no readable safe slot.");
            panel.Add(Text("Altitude   " + Length(_sim.Position.y), "panel-title", "wrap"));
            panel.Add(Text("Distance from home   " + Length(new Vector2(_sim.Position.x, _sim.Position.z).magnitude), "wrap"));
            panel.Add(Text("Horizontal speed   " + Velocity(new Vector2(_sim.Body.linearVelocity.x, _sim.Body.linearVelocity.z).magnitude), "wrap"));
            panel.Add(Text("Vertical speed   " + Velocity(_sim.Body.isKinematic ? 0 : _sim.Body.linearVelocity.y), "wrap"));
            panel.Add(Text("Battery   " + _sim.Battery.ToString("F0") + "%", "wrap"));
            panel.Add(Text("Elapsed   " + Duration(_sim.Elapsed), "wrap"));
            void Close() { overlay.RemoveFromHierarchy(); _flightDialogClose = null; if (pauseForReading) _sim.Pause(false); RestoreFlightDialogFocus(); }
            _flightDialogClose = Close;
            panel.Add(ActionButton("Back to Flight", Close, true));
            overlay.Add(ModalScroll(panel)); InstallDesignModalFocus(overlay); _root.Add(overlay);
            overlay.Query<Button>().First()?.Focus();
        }

        private static void InstallDesignModalFocus(VisualElement modal)
        {
            modal.RegisterCallback<KeyDownEvent>(e => {
                if (e.keyCode != KeyCode.Tab || modal.ClassListContains("is-hidden")) return;
                var buttons = modal.Query<Button>().ToList().Where(b => b.enabledInHierarchy && b.resolvedStyle.display != DisplayStyle.None).ToList();
                if (buttons.Count == 0) return;
                var focused = modal.panel?.focusController.focusedElement;
                int index = buttons.FindIndex(b => b == focused);
                index = (index + (e.shiftKey ? -1 : 1) + buttons.Count) % buttons.Count;
                buttons[index].GetFirstAncestorOfType<ScrollView>()?.ScrollTo(buttons[index]);
                modal.panel?.focusController.IgnoreEvent(e);
                buttons[index].Focus(); e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            modal.RegisterCallback<NavigationMoveEvent>(e => {
                if (modal.ClassListContains("is-hidden") || (e.direction != NavigationMoveEvent.Direction.Next && e.direction != NavigationMoveEvent.Direction.Previous)) return;
                var buttons = modal.Query<Button>().ToList().Where(b => b.enabledInHierarchy && b.resolvedStyle.display != DisplayStyle.None).ToList();
                if (buttons.Count == 0) return;
                int index = buttons.FindIndex(b => b == modal.panel?.focusController.focusedElement);
                index = (index + (e.direction == NavigationMoveEvent.Direction.Previous ? -1 : 1) + buttons.Count) % buttons.Count;
                modal.panel?.focusController.IgnoreEvent(e); buttons[index].Focus(); e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
        }

        private void BuildDesignReplay()
        {
            _root.RemoveFromClassList("flight-root"); _root.AddToClassList("v1-replay");
            _root.Add(BuildTopBar());
            var body = Element("app-body"); body.Add(BuildNavigation());
            var stage = Element("content-stage"); _mainContent = Element("main-content");
            var page = new ScrollView(ScrollViewMode.Vertical); page.AddToClassList("v1-replay-page");
            var heading = Element("v1-replay-heading");
            heading.Add(FlightAction("Back", "back", () => ShowPage(Page.Records)));
            heading.Add(Text("Replay & Analysis", "page-title"));
            heading.Add(Text(_record.title + " · Score " + _record.score, "v1-replay-session"));
            heading.Add(FlightAction("Export CSV", "save", () => ExportCsv(_record))); page.Add(heading);
            var columns = Element("v1-replay-columns");
            var viewport = Element("v1-replay-viewport");
            _designReplayImage = new Image { scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            _designReplayImage.AddToClassList("v1-replay-image"); viewport.Add(_designReplayImage);
            _designReplayImage.RegisterCallback<GeometryChangedEvent>(_ => ResizeDesignReplayTexture());
            var cameras = Element("v1-replay-camera-tabs");
            cameras.Add(FlightAction("Pilot View", "camera", () => SetDesignReplayCamera(false, false)));
            cameras.Add(FlightAction("Third Person", "drone", () => SetDesignReplayCamera(true, false)));
            cameras.Add(FlightAction("Map", "map", () => SetDesignReplayCamera(false, true))); viewport.Add(cameras);
            _map = new MiniMap(() => _sim); _map.AddToClassList("v1-replay-map");
            _map.style.display = DisplayStyle.None; _designReplayMap = _map; viewport.Add(_map);
            var videoControls = Element("v1-replay-video-controls");
            _designReplayTime = Text("00:00 / " + Duration(_record.duration), "v1-replay-time"); videoControls.Add(_designReplayTime);
            videoControls.Add(FlightAction("−10s", "back", () => _sim.Seek(_sim.ReplayTime - 10)));
            _replayPlay = FlightAction("Play", "play", () => _sim.ReplayPlaying = !_sim.ReplayPlaying); videoControls.Add(_replayPlay);
            videoControls.Add(FlightAction("+10s", "play", () => _sim.Seek(_sim.ReplayTime + 10))); viewport.Add(videoControls);
            columns.Add(viewport);

            var telemetry = Element("v1-replay-telemetry");
            telemetry.Add(Text("Flight Telemetry", "panel-title"));
            _altitude = ReplayGraphSection(telemetry, "Altitude", 0, new Color(.05f, .58f, 1), LocalStore.Profile.settings.units == "Metric" ? "m" : "ft");
            _speed = ReplayGraphSection(telemetry, "Ground Speed", 1, new Color(.05f, .88f, .66f), LocalStore.Profile.settings.units == "Metric" ? "km/h" : "mph");
            _battery = ReplayGraphSection(telemetry, "Battery", 2, new Color(1, .72f, .24f), "%");
            telemetry.Add(Text("Stick Inputs", "v1-replay-chart-title"));
            var sticks = Element("v1-replay-stick-row");
            foreach (bool left in new[] { true, false })
            {
                var stick = new ReplayStickDiagram(() => ReplayFrame(), left); _designSticks.Add(stick); sticks.Add(stick);
            }
            telemetry.Add(sticks); _designReplayInputs = Text("", "wrap", "v1-replay-input-labels"); telemetry.Add(_designReplayInputs);
            columns.Add(telemetry); page.Add(columns);

            var timeline = Element("v1-replay-timeline"); timeline.Add(Text("Flight Timeline", "panel-title"));
            var events = new ScrollView(ScrollViewMode.Horizontal); events.AddToClassList("v1-replay-events");
            var eventRow = Element("v1-replay-events-row");
            foreach (var flightEvent in _record.events)
            {
                var captured = flightEvent;
                eventRow.Add(FlightAction(Duration(captured.time) + "  " + captured.message,
                    captured.kind == "warning" ? "shield" : "target", () => _sim.Seek(captured.time), "v1-replay-event"));
            }
            if (_record.events.Count == 0) eventRow.Add(Text("No events recorded for this session.", "muted"));
            events.Add(eventRow); timeline.Add(events);
            var actions = Element("v1-replay-review-actions");
            actions.Add(FlightAction("Practice Again", "play", () => StartFlight(JsonUtility.FromJson<FlightConfig>(JsonUtility.ToJson(_record.config ?? new FlightConfig())))));
            actions.Add(FlightAction("Add Note", "edit", () => ShowPage(Page.Report))); timeline.Add(actions);
            var controls = Element("v1-replay-controls");
            controls.Add(FlightAction("Play / Pause", "play", () => _sim.ReplayPlaying = !_sim.ReplayPlaying));
            _replaySlider = new Slider(0, Mathf.Max(.1f, _record.duration)); _replaySlider.AddToClassList("replay-slider");
            _replaySlider.RegisterValueChangedCallback(e => { if (!_seeking) _sim.Seek(e.newValue); }); controls.Add(_replaySlider);
            var speed = new DropdownField(new List<string> { "0.5x", "1.0x", "2.0x" }, 1); speed.AddToClassList("v1-replay-rate");
            speed.RegisterValueChangedCallback(e => _sim.ReplaySpeed = e.newValue == "0.5x" ? .5f : e.newValue == "2.0x" ? 2 : 1); controls.Add(speed);
            _timer = Text(Duration(0), "v1-replay-clock"); controls.Add(_timer); timeline.Add(controls); page.Add(timeline);
            page.Add(Text("LOCAL RECORDED SESSION · SIMULATION — NOT FOR NAVIGATION", "v1-replay-disclaimer"));
            _flightStatus = Text("REPLAY PAUSED", "v1-replay-status"); heading.Insert(3, _flightStatus);
            _objective = Text(_record.title, "is-hidden"); page.Add(_objective);
            _sticks = Text("", "is-hidden"); page.Add(_sticks);
            _mainContent.Add(page); stage.Add(_mainContent); body.Add(stage); _root.Add(body);
            _workspaceMenu = BuildWorkspaceMenu(); _root.Add(_workspaceMenu);
            SetDesignReplayCamera(true, false);
        }

        private Label ReplayGraphSection(VisualElement parent, string title, int channel, Color color, string units)
        {
            var heading = Element("v1-replay-chart-heading"); heading.Add(Text(title, "v1-replay-chart-title"));
            var value = Text("—", "v1-replay-chart-value"); heading.Add(value); heading.Add(Text(units, "v1-replay-chart-unit")); parent.Add(heading);
            var graph = new ReplayTelemetryGraph(_record, () => _sim.ReplayTime, channel, color, LocalStore.Profile.settings.units != "Metric");
            _designGraphs.Add(graph); parent.Add(graph); return value;
        }

        private TelemetryFrame ReplayFrame() => _record?.frames.FindLast(f => f.time <= _sim.ReplayTime) ?? _record?.frames.FirstOrDefault();

        private void SetDesignReplayCamera(bool chase, bool map)
        {
            _sim.ChaseView = chase;
            _designReplayImage.style.display = map ? DisplayStyle.None : DisplayStyle.Flex;
            _designReplayMap.style.display = map ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ResizeDesignReplayTexture()
        {
            if (_designReplayImage == null || _sim?.View == null || _designReplayImage.layout.width < 16) return;
            int width = Mathf.Clamp(Mathf.RoundToInt(_designReplayImage.layout.width * _document.panelSettings.scale), 320, 1920);
            int height = Mathf.Clamp(Mathf.RoundToInt(_designReplayImage.layout.height * _document.panelSettings.scale), 180, 1080);
            if (_designReplayTexture != null && _designReplayTexture.width == width && _designReplayTexture.height == height) return;
            if (_designReplayTexture != null) { _sim.View.targetTexture = null; _designReplayTexture.Release(); Destroy(_designReplayTexture); }
            _designReplayTexture = new RenderTexture(width, height, 24) { name = "AeroSim recorded session viewport" }; _designReplayTexture.Create();
            _sim.View.targetTexture = _designReplayTexture; _designReplayImage.image = _designReplayTexture;
        }

        private void TickDesignFlightUi(bool replay)
        {
            if (replay)
            {
                if (_designReplayTime != null) _designReplayTime.text = Duration(_sim.ReplayTime) + " / " + Duration(_record.duration);
                foreach (var graph in _designGraphs) graph.MarkDirtyRepaint();
                foreach (var stick in _designSticks) stick.MarkDirtyRepaint();
                var frame = ReplayFrame();
                if (frame != null && _designReplayInputs != null)
                    _designReplayInputs.text = "Throttle " + frame.input.x.ToString("P0") + "   Yaw " + frame.input.y.ToString("P0") + "\nPitch " + frame.input.z.ToString("P0") + "   Roll " + frame.input.w.ToString("P0");
                return;
            }
            if (_designDistance != null) _designDistance.text = Length(new Vector2(_sim.Position.x, _sim.Position.z).magnitude);
            if (_designVerticalSpeed != null) _designVerticalSpeed.text = Velocity(_sim.Body.isKinematic ? 0 : _sim.Body.linearVelocity.y);
            if (_designFlightClock != null) _designFlightClock.text = Duration(_sim.Elapsed);
            if (_config.workspace == "FPV") _timer.text = TimeSpan.FromSeconds(Mathf.Max(0, _sim.Elapsed)).ToString(@"mm\:ss\.fff");
            if (_designTargetProgress != null)
            {
                int count = _config.mission?.targets.Count ?? 0;
                _designTargetProgress.text = "INSPECTION TARGETS     " + _sim.GateIndex + " / " + count + " COMPLETE";
                for (int i = 0; i < _designTargetStates.Count; i++)
                {
                    _designTargetStates[i].text = i < _sim.GateIndex ? "✓" : (i + 1).ToString();
                    _designTargetStates[i].parent.EnableInClassList("v1-target-complete", i < _sim.GateIndex);
                    _designTargetStates[i].parent.EnableInClassList("v1-target-active", i == _sim.GateIndex);
                }
                _designTargetDistance.text = _sim.NextTarget.HasValue ? "ACTIVE TARGET · Distance " + Length(Vector3.Distance(_sim.Position, _sim.NextTarget.Value)) + "\nSafe distance " + Length(_config.mission.safeDistance) : "All target photographs captured.";
            }
        }

        private void LayoutDesignFlightHud(float width, float height)
        {
            bool compact = width < 1560 || height < 700 || LocalStore.Profile.settings.textScale > 1.1f;
            _root.EnableInClassList("hud-compact", compact);
            _root.EnableInClassList("hud-narrow", width < 480);
            _root.EnableInClassList("hud-touch", (Application.isMobilePlatform || width < 760 || height < 500 || LocalStore.Profile.settings.showTouchControls) && _page != Page.Replay);
            if (_page == Page.Replay) return;
            float touchSize = Mathf.Clamp(width * .3f - 32, 44, height < 500 ? 80 : 100);
            float actionHeight = LocalStore.Profile.settings.textScale > 1.1f ? 72 : 56;
            foreach (var stick in _root.Query<TouchStick>().ToList()) stick.ConfigureLayout(touchSize, actionHeight + 24, 16, touchSize < 100 || LocalStore.Profile.settings.textScale > 1.1f);
            var objective = _root.Q<VisualElement>(className: "v1-objective-scroll");
            if (objective != null) objective.style.maxHeight = Mathf.Max(200, height - 380);
            var telemetry = _root.Q<VisualElement>(className: "v1-flight-telemetry");
            if (telemetry != null)
            {
                float available = compact ? width - 32 : Mathf.Min(1040, width - 760);
                telemetry.style.width = Mathf.Max(240, available); telemetry.style.left = compact ? 16 : (width - available) / 2;
            }
            LayoutDesignOsdAnchors(width, height, compact);
        }

        private void LayoutDesignOsdAnchors(float width, float height, bool compact)
        {
            if (_designOsdLayer == null || _page != Page.Flight) return;
            var settings = LocalStore.Profile.settings;
            var anchors = new[] { settings.speedPosition, settings.altitudePosition, settings.batteryPosition, settings.timerPosition, new Vector2(.02f,.1f), new Vector2(.77f,.4f) };
            var defaults = new[] { new Vector2(.6f,.78f), new Vector2(.23f,.78f), new Vector2(.78f,.32f), new Vector2(.46f,.13f) };
            var enabled = new[] { settings.osdSpeed, settings.osdAltitude, settings.osdBattery, settings.osdTimer, true, true };
            var metrics = new[] { _designSpeedMetric, _designAltitudeMetric, _designBatteryMetric, (VisualElement)_timer, _designDistance?.parent, _designVerticalSpeed?.parent };
            var custom = new bool[4];
            bool anyCustom = false;
            for (int i = 0; i < custom.Length; i++) { custom[i] = (anchors[i] - defaults[i]).sqrMagnitude > .000001f; anyCustom |= enabled[i] && custom[i]; }
            // A narrow custom layout temporarily relocates all enabled OSD items together; this is not a profile mutation.
            bool compactCustom = compact && (anyCustom || width < 760 || height < 500);
            var telemetry = _root.Q<VisualElement>(className: "v1-flight-telemetry");
            telemetry.style.display = compactCustom ? DisplayStyle.None : DisplayStyle.Flex;
            _root.EnableInClassList("v1-custom-osd-active", anyCustom);
            bool fpv = _config.workspace == "FPV";
            _designFlightClock.style.display = !compact && !fpv && settings.osdTimer && !custom[3] ? DisplayStyle.Flex : DisplayStyle.None;

            float scale = Mathf.Max(.01f, _document.panelSettings.scale);
            if (Application.isMobilePlatform)
            {
                var safe = Screen.safeArea;
                width -= (Screen.width-safe.width)/scale; height -= (Screen.height-safe.height)/scale;
            }
            if (width < 64 || height < 120) return;
            float text = Mathf.Clamp(settings.textScale,1,2);
            float chipWidth = Mathf.Min(184*text, Mathf.Max(110,width*.3f-32));
            float chipHeight = text > 1.1f ? 84 : 60;
            float bottomReserve = text > 1.1f ? 80 : 64;
            var center = new Rect(width*.3f,height*.31f,width*.4f,height*.38f);
            var occupied = new List<Rect> { new Rect(0,0,width,88), center, new Rect(0,height-bottomReserve,width,bottomReserve) };
            if (!compactCustom)
            {
                float available = compact ? width-32 : Mathf.Min(1040,width-760);
                occupied.Add(new Rect(compact?16:(width-available)/2,compact?96:height-120,Mathf.Max(240,available),compact?Mathf.Max(64,64*text):80));
            }
            if (!compact)
            {
                if (!fpv)
                {
                    bool professional = _config.professional;
                    occupied.Add(new Rect(professional?width-464:32,professional?108:220,440,Mathf.Max(200,height-380)));
                    occupied.Add(new Rect(32,height-384,336,336));
                    if (!professional) occupied.Add(new Rect(width-128,height*.24f,96,426));
                }
                if (fpv && settings.osdTimer && !custom[3]) occupied.Add(new Rect(width*.36f,height-200,width*.28f,60));
                if (!fpv && settings.showControls) occupied.Add(new Rect(width*.3f,100,width*.4f,48*text));
            }
            if (_root.ClassListContains("hud-touch"))
            {
                float size = Mathf.Clamp(width*.3f-32,44,height<500?80:100);
                float bottom = (text>1.1f?72:56)+24;
                float caption = width<480 || text>1.1f ? 0 : 20;
                occupied.Add(new Rect(16,height-bottom-size,size,size+caption));
                occupied.Add(new Rect(width-16-size,height-bottom-size,size,size+caption));
            }
            bool Fits(Rect rectangle)
            {
                if (!float.IsFinite(rectangle.x) || !float.IsFinite(rectangle.y) || rectangle.xMin < 16 || rectangle.xMax > width-16 || rectangle.yMin < 96 || rectangle.yMax > height-bottomReserve) return false;
                return !occupied.Any(other => rectangle.Overlaps(other));
            }
            var fallbackSlots = new List<Rect>();
            var xSlots = new List<float> {16,width-chipWidth-16,width*.3f-chipWidth-8,width*.7f+8};
            for (float x=16;x+chipWidth<=width-16;x+=chipWidth+8) xSlots.Add(x);
            var ySlots = new List<float> {100,center.yMin-chipHeight-8,center.yMax+8,height-bottomReserve-chipHeight};
            for (float y=100;y+chipHeight<=height-bottomReserve;y+=chipHeight+8) ySlots.Add(y);
            foreach (float y in ySlots.Distinct().OrderBy(value=>value))
                foreach (float x in xSlots.Distinct().OrderBy(value=>value)) fallbackSlots.Add(new Rect(x,y,chipWidth,chipHeight));
            int fallbackCount=0, unavailableCount=0;
            for (int i=0;i<metrics.Length;i++)
            {
                var metric = metrics[i]; if (metric == null) continue;
                bool floating = enabled[i] && ((i<4 && custom[i]) || compactCustom);
                metric.EnableInClassList("v1-custom-osd-chip",floating);
                metric.EnableInClassList("v1-custom-osd-timer",floating && i==3);
                if (!floating)
                {
                    // Restore the exact default hierarchy and remove only the overrides owned by custom positioning.
                    if (i!=3 && metric.parent!=telemetry)
                    {
                        int order = i==0 ? 2 : i==1 ? 0 : i==2 ? 4 : i==4 ? 1 : 3;
                        telemetry.Insert(Mathf.Min(order,telemetry.childCount),metric);
                    }
                    else if (i==3 && metric.parent!=_designOsdLayer.parent) _designOsdLayer.parent.Insert(2,metric);
                    metric.style.left=new StyleLength(StyleKeyword.Null); metric.style.top=new StyleLength(StyleKeyword.Null);
                    metric.style.right=new StyleLength(StyleKeyword.Null); metric.style.bottom=new StyleLength(StyleKeyword.Null);
                    metric.style.width=new StyleLength(StyleKeyword.Null); metric.style.height=new StyleLength(StyleKeyword.Null);
                    metric.style.display=enabled[i] && (i!=3 || fpv || compact) ? DisplayStyle.Flex : DisplayStyle.None;
                    metric.tooltip=""; metric.userData=null;
                    if(i==3) { _baseFonts[_timer]=compact?26:42; _timer.style.fontSize=(compact?26:42)*text; }
                    continue;
                }
                if (metric.parent!=_designOsdLayer) _designOsdLayer.Add(metric);
                var preferred = new Rect(anchors[i].x*width,anchors[i].y*height,chipWidth,chipHeight);
                bool preferredFits = Fits(preferred);
                var placement = preferred;
                bool placed = preferredFits;
                if (!placed)
                {
                    foreach(var candidate in fallbackSlots.OrderBy(slot=>(slot.position-preferred.position).sqrMagnitude)) if(Fits(candidate)) { placement=candidate;placed=true;break; }
                }
                // Extreme mixed zoom can leave no readable peripheral slot: never cover controls or the flight centre.
                metric.style.display=placed ? DisplayStyle.Flex : DisplayStyle.None;
                metric.userData=placed ? preferredFits ? "osd-saved-anchor" : "osd-safe-fallback" : "osd-no-safe-slot";
                metric.tooltip=preferredFits ? "Saved OSD position" : placed ? "Temporary safe position; saved anchor is unchanged." : "No readable safe slot at this zoom. Pause → Flight Telemetry shows all values.";
                if (!placed) { unavailableCount++; continue; }
                if (!preferredFits) fallbackCount++;
                occupied.Add(placement);
                metric.style.left=placement.x;metric.style.top=placement.y;
                metric.style.right=new StyleLength(StyleKeyword.Auto); metric.style.bottom=new StyleLength(StyleKeyword.Auto);
                metric.style.width=chipWidth;metric.style.height=chipHeight;
                if(i==3) { _baseFonts[_timer]=22; _timer.style.fontSize=22*text; }
            }
            // Returning from custom placement must not scramble the reference bottom-strip order.
            var defaultOrder=new[] {_designAltitudeMetric,_designDistance?.parent,_designSpeedMetric,_designVerticalSpeed?.parent,_designBatteryMetric};
            int defaultIndex=0;
            foreach(var metric in defaultOrder)
                if(metric!=null && metric.parent==telemetry) { if(telemetry.IndexOf(metric)!=defaultIndex)telemetry.Insert(defaultIndex,metric);defaultIndex++; }
            string notice=unavailableCount>0 ? "Some OSD items cannot fit safely at this zoom. Pause → Flight Telemetry shows all values; saved positions are unchanged."
                : fallbackCount>0 ? "OSD uses temporary safe positions to keep the view and controls clear. Saved positions are unchanged." : "";
            if(notice!=_designOsdSafetyNotice)
            {
                _designOsdSafetyNotice=notice;
            }
            // A large, transient toast covers the exact view this layout protects.
            // Keep the persistent status discoverable without covering live flight.
            if (_designOsdPauseNotice != null)
            {
                _designOsdPauseNotice.text = notice;
                _designOsdPauseNotice.style.display = notice.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_designOsdIndicator != null)
            {
                _designOsdIndicator.style.display = notice.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _designOsdIndicator.tooltip = notice;
            }
            var pauseAction = _root.Q<Button>("action-pause");
            if (pauseAction != null) pauseAction.tooltip = "Pause flight. Flight Telemetry shows all real simulator values." + (notice.Length > 0 ? " " + notice : "");
        }

        private sealed class ReplayTelemetryGraph : VisualElement
        {
            private readonly SessionRecord _record;
            private readonly Func<float> _time;
            private readonly int _channel;
            private readonly Color _color;
            private readonly bool _customary;
            public ReplayTelemetryGraph(SessionRecord record, Func<float> time, int channel, Color color, bool customary)
            {
                _record = record; _time = time; _channel = channel; _color = color; _customary = customary;
                AddToClassList("v1-replay-graph"); pickingMode = PickingMode.Ignore; generateVisualContent += Draw;
            }
            private float Value(TelemetryFrame frame) => _channel == 0 ? frame.position.y * (_customary ? 3.28084f : 1) : _channel == 1 ? frame.speed * (_customary ? 2.23694f : 3.6f) : frame.battery;
            private void Draw(MeshGenerationContext context)
            {
                float w = contentRect.width, h = contentRect.height;
                if (w < 4 || h < 4 || _record.frames.Count == 0) return;
                var painter = context.painter2D;
                painter.strokeColor = new Color(.17f, .27f, .36f); painter.lineWidth = 1;
                painter.BeginPath();
                for (int i = 0; i <= 4; i++) { float y = 4 + i * (h - 8) / 4; painter.MoveTo(new Vector2(4, y)); painter.LineTo(new Vector2(w - 4, y)); }
                for (int i = 0; i <= 6; i++) { float x = 4 + i * (w - 8) / 6; painter.MoveTo(new Vector2(x, 4)); painter.LineTo(new Vector2(x, h - 4)); }
                painter.Stroke();
                float maximum = Mathf.Max(_channel == 2 ? 100 : 1, _record.frames.Max(Value));
                float duration = Mathf.Max(.1f, _record.duration);
                Vector2 Point(TelemetryFrame frame) => new(4 + Mathf.Clamp01(frame.time / duration) * (w - 8), h - 4 - Mathf.Clamp01(Value(frame) / maximum) * (h - 8));
                painter.strokeColor = _color; painter.lineWidth = 2.5f; painter.BeginPath(); bool first = true;
                // Downsample by time buckets, preserving the complete recorded interval.
                int stride = Mathf.Max(1, _record.frames.Count / 800);
                for (int i = 0; i < _record.frames.Count; i += stride)
                {
                    var point = Point(_record.frames[i]); if (first) { painter.MoveTo(point); first = false; } else painter.LineTo(point);
                }
                painter.LineTo(Point(_record.frames[_record.frames.Count - 1])); painter.Stroke();
                var current = _record.frames.FindLast(f => f.time <= _time()) ?? _record.frames[0];
                var marker = Point(current); painter.strokeColor = new Color(.85f, .92f, 1); painter.lineWidth = 1;
                painter.BeginPath(); painter.MoveTo(new Vector2(marker.x, 2)); painter.LineTo(new Vector2(marker.x, h - 2)); painter.Stroke();
                painter.fillColor = _color; painter.BeginPath(); painter.Arc(marker, 5, 0, 360); painter.Fill();
                painter.strokeColor = Color.white; painter.lineWidth = 2; painter.BeginPath(); painter.Arc(marker, 5, 0, 360); painter.Stroke();
            }
        }

        private sealed class ReplayStickDiagram : VisualElement
        {
            private readonly Func<TelemetryFrame> _frame;
            private readonly bool _left;
            public ReplayStickDiagram(Func<TelemetryFrame> frame, bool left)
            {
                _frame = frame; _left = left; AddToClassList("v1-replay-stick"); pickingMode = PickingMode.Ignore; generateVisualContent += Draw;
            }
            private void Draw(MeshGenerationContext context)
            {
                float w = contentRect.width, h = contentRect.height, r = Mathf.Min(w, h) / 2 - 4;
                if (r <= 0) return;
                var painter = context.painter2D; var center = new Vector2(w / 2, h / 2);
                painter.strokeColor = new Color(.2f, .58f, .95f); painter.lineWidth = 1;
                painter.BeginPath(); painter.Arc(center, r, 0, 360); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(center - Vector2.right * r); painter.LineTo(center + Vector2.right * r);
                painter.MoveTo(center - Vector2.up * r); painter.LineTo(center + Vector2.up * r); painter.Stroke();
                var frame = _frame(); Vector4 input = frame?.input ?? Vector4.zero;
                var value = _left ? new Vector2(input.y, -input.x) : new Vector2(input.w, -input.z);
                var point = center + Vector2.ClampMagnitude(value, 1) * (r - 8);
                painter.fillColor = new Color(.02f, .54f, 1); painter.BeginPath(); painter.Arc(point, 8, 0, 360); painter.Fill();
            }
        }

        private void ReleaseDesignReplayView()
        {
            _flightDialogClose?.Invoke();
            _flightDialogClose = null;
            if (_sim?.View != null && _sim.View.targetTexture == _designReplayTexture) _sim.View.targetTexture = null;
            if (_designReplayTexture != null) { _designReplayTexture.Release(); Destroy(_designReplayTexture); _designReplayTexture = null; }
            _designReplayImage = null; _designReplayMap = null; _designGraphs.Clear(); _designSticks.Clear(); _designTargetStates.Clear();
            _designDistance = _designVerticalSpeed = _designFlightClock = _designReplayTime = _designTargetProgress = _designTargetDistance = _designReplayInputs = null;
            _designOsdLayer = _designSpeedMetric = _designAltitudeMetric = _designBatteryMetric = null;
            _designOsdSafetyNotice=null;
            if (_root == null) return;
            foreach (var name in new[] { "v1-flight-hud", "v1-flight-aerial", "v1-flight-fpv", "v1-flight-professional", "v1-custom-osd-active", "v1-replay" }) _root.RemoveFromClassList(name);
        }
    }
}
