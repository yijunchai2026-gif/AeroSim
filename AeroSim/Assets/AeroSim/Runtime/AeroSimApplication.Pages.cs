using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private Page _page;
        private Course _course;
        private FlightConfig _config;
        private SessionRecord _record;
        private MissionDefinition _mission;
        private FlightSimulator _sim;
        private string _academyFilter = "All";
        private int _onboardStep, _calibrationStep;
        private bool _calibrating;
        private Label[] _axisLabels;
        private ProgressBar[] _axisBars;
        private Vector4 _center, _minimum, _maximum;
        private string WorkspaceName => _workspace == TrainingWorkspace.Fpv ? "FPV" : _workspace == TrainingWorkspace.Professional ? "Professional" : "Aerial";
        public Page CurrentPage => _page;
        public FlightSimulator Simulator => _sim;
        public void ChangeWorkspace(string workspace) => SelectWorkspace(workspace == "FPV" ? TrainingWorkspace.Fpv : workspace == "Professional" ? TrainingWorkspace.Professional : TrainingWorkspace.Aerial);
        public void PreparePreview(Page page)
        {
            if (page == Page.Brief || page == Page.Preflight) { _course = CourseCatalog.Find("a10"); _config = _course.Config(); _mission = _workspace == TrainingWorkspace.Professional ? LocalStore.Profile.missions[0] : null; }
            if (page == Page.Results || page == Page.Report) _record = LocalStore.Sessions().FirstOrDefault();
            ShowPage(page);
        }

        public void ShowPage(Page page)
        {
            ReleaseDesignReplayView();
            ReleaseHangarPreviews();
            if (_sim != null && (page != Page.Flight && page != Page.Replay)) _sim.Stop();
            _calibrating = page == Page.Controller && _calibrating;
            _axisLabels = null;
            _page = page;
            _root.RemoveFromClassList("flight-root");
            BuildInterface();
            ApplySettings();
            RefreshControllerStatus();
        }

        private VisualElement BuildPage(Page page) => page switch
        {
            Page.Home => _workspace == TrainingWorkspace.Aerial ? BuildAerialHome() : _workspace == TrainingWorkspace.Fpv ? DesignFpvHome() : DesignDashboard(),
            Page.Onboarding => DesignOnboarding(), Page.Controller => DesignController(), Page.Academy => DesignAcademy(),
            Page.FreeFlight => DesignFreeFlight(), Page.Challenges => DesignChallenges(), Page.Hangar => Hangar(),
            Page.Tuning => DesignTuning(), Page.OsdEditor => DesignOsdEditor(), Page.Dashboard => DesignDashboard(),
            Page.Catalog => DesignCatalog(), Page.Brief => DesignBrief(), Page.Preflight => DesignPreflight(),
            Page.MissionEditor => DesignMissionEditor(), Page.Instructor => DesignInstructor(), Page.Readiness => DesignReadiness(),
            Page.Settings => DesignSettings(), Page.Records => Records(), Page.Results => DesignResults(),
            Page.Report => DesignReport(), _ => DesignFreeFlight()
        };

        private Button RouteButton(string icon, string title, Page page)
        {
            var b = new Button(() => ShowPage(page));
            b.AddToClassList("nav-button"); var vectorIcon = DesignIcon(NavigationIcon(title)); vectorIcon.AddToClassList("nav-icon"); b.Add(vectorIcon); b.Add(Text(title,"nav-label"));
            if (_page == page) b.AddToClassList("nav-button-selected"); return b;
        }
        private static Button ActionButton(string title, Action action, bool primary = false)
        {
            var b = new Button(action) { text = title }; b.AddToClassList(primary ? "primary-button" : "action-button");
            b.name = "action-" + title.ToLowerInvariant().Replace(" ", "-"); return b;
        }
        private static VisualElement Row(params VisualElement[] children)
        {
            var row = Element("product-row"); foreach (var c in children) row.Add(c); return row;
        }
        private static VisualElement Panel(string title, string subtitle = null)
        {
            var panel = Element("product-panel"); panel.Add(Text(title, "panel-title"));
            if (!string.IsNullOrEmpty(subtitle)) panel.Add(Text(subtitle, "muted", "wrap")); return panel;
        }
        private ScrollView Canvas(string eyebrow, string title, string subtitle)
        {
            var s = new ScrollView(); s.AddToClassList("product-scroll");
            s.Add(Text(eyebrow.ToUpperInvariant(), "page-eyebrow")); s.Add(Text(title, "page-title")); s.Add(Text(subtitle, "page-subtitle", "wrap")); return s;
        }
        private static void Choice(VisualElement parent, string label, IEnumerable<string> options, string value, Action<string> changed)
        {
            var list = options.ToList(); var f = new DropdownField(label, list, Math.Max(0, list.IndexOf(value)));
            f.AddToClassList("product-field"); f.RegisterValueChangedCallback(e => changed(e.newValue)); parent.Add(f);
        }
        private static void Range(VisualElement parent, string label, float low, float high, float value, Action<float> changed)
        {
            var f = new Slider(label, low, high) { value = value, showInputField = true }; f.AddToClassList("product-field");
            f.AddToClassList("design-range-field");
            // Slider creates a TextField for showInputField in Unity 6, not a FloatField.
            // Style the actual editable text node rather than an unrelated outer box.
            VisualElement numeric = f.Q<TextField>();
            numeric ??= f.Q<FloatField>();
            if (numeric != null)
            {
                numeric.AddToClassList("design-range-number");
                foreach (var text in numeric.Query<TextElement>().ToList()) text.AddToClassList("design-range-value");
            }
            f.RegisterValueChangedCallback(e => changed(e.newValue)); parent.Add(f);
        }
        private static void Check(VisualElement parent, string label, bool value, Action<bool> changed)
        {
            var f = new Toggle(label) { value = value }; f.AddToClassList("product-field"); f.AddToClassList("design-check-field");
            f.RegisterValueChangedCallback(e => changed(e.newValue)); parent.Add(f);
        }
        private static void Field(VisualElement parent, string label, string value, Action<string> changed)
        {
            var f = new TextField(label) { value = value }; f.AddToClassList("product-field"); f.RegisterValueChangedCallback(e => changed(e.newValue)); parent.Add(f);
        }
        private static void Number(VisualElement parent, string label, float value, Action<float> changed)
        {
            var f = new FloatField(label) { value = value }; f.AddToClassList("product-field"); f.RegisterValueChangedCallback(e => changed(e.newValue)); parent.Add(f);
        }
        private string LessonStatus(string id)
        {
            var p = LocalStore.Profile.courses.Find(c => c.id == id); return p == null ? "Ready to start" : "Best " + p.bestScore + " / 100";
        }
        private string Length(float meters) => LocalStore.Profile.settings.units == "Metric" ? meters.ToString("F0") + " m" : (meters * 3.28084f).ToString("F0") + " ft";
        private string Velocity(float mps) => LocalStore.Profile.settings.units == "Metric" ? (mps * 3.6f).ToString("F1") + " km/h" : (mps * 2.23694f).ToString("F1") + " mph";
        private static string Duration(float seconds) => TimeSpan.FromSeconds(Mathf.Max(0, seconds)).ToString(@"mm\:ss");
        private static string LocalDate(string utc) => DateTimeOffset.TryParse(utc, out var date) ? date.ToLocalTime().ToString("MMM d, yyyy · h:mm tt", System.Globalization.CultureInfo.GetCultureInfo("en-US")) : utc;

        private VisualElement Onboarding()
        {
            var p = LocalStore.Profile;
            var s = Canvas("Welcome to AeroSim", "Your flight journey starts here.", "A U.S.-localized training simulator. No real aircraft connection. No FAA certification.");
            var card = Panel("Step " + (_onboardStep + 1) + " of 3", "Your preferences stay on this device. You can change them at any time.");
            if (_onboardStep == 0)
            {
                Field(card, "Pilot name", p.settings.pilotName, v => p.settings.pilotName = v);
                Choice(card, "Training goal", new[] { "Aerial", "FPV", "Professional" }, p.workspace, v => p.workspace = v);
                Choice(card, "Flying purpose", new[] { "Recreational", "Professional / Part 107" }, p.settings.purpose, v => p.settings.purpose = v);
            }
            else if (_onboardStep == 1)
            {
                Choice(card, "Units", new[] { "U.S. Customary", "Metric" }, p.settings.units, v => p.settings.units = v);
                Choice(card, "Performance", new[] { "30 fps", "60 fps", "120 fps" }, p.settings.targetFps + " fps", v => p.settings.targetFps = int.Parse(v.Split(' ')[0]));
                Check(card, "Show control guidance", p.settings.showControls, v => p.settings.showControls = v);
                Check(card, "High-contrast interface", p.settings.highContrast, v => p.settings.highContrast = v);
            }
            else
            {
                card.Add(Text("Keyboard: W / S forward and backward · A / D strafe\nR / F climb and descend · Q / E rotate\nH return home · C camera · P photo · Esc pause\n\nGamepad: Mode 2 dual sticks · Start pause · Right bumper photo\niPad / iPhone: drag the two on-screen sticks in flight.", "wrap"));
                card.Add(ActionButton("Check Controller", () => ShowPage(Page.Controller)));
                card.Add(Text("USB controllers are detected through Unity Input System. DJI transmitter compatibility is not assumed or certified.", "muted", "wrap"));
            }
            card.Add(Row(ActionButton("Back", () => { _onboardStep = Math.Max(0, _onboardStep - 1); ShowPage(Page.Onboarding); }), ActionButton(_onboardStep == 2 ? "Start Flying" : "Continue", () =>
            {
                LocalStore.Save(); if (_onboardStep < 2) { _onboardStep++; ShowPage(Page.Onboarding); }
                else { p.onboarded = true; LocalStore.Save(); SelectWorkspace(p.workspace == "FPV" ? TrainingWorkspace.Fpv : p.workspace == "Professional" ? TrainingWorkspace.Professional : TrainingWorkspace.Aerial); }
            }, true)));
            s.Add(card); return s;
        }

        private VisualElement WorkspaceHome()
        {
            bool fpv = _workspace == TrainingWorkspace.Fpv;
            var records = LocalStore.Sessions();
            var s = Canvas(fpv ? "FPV / Race ready" : "Professional / Local operations", fpv ? "Find your next line." : "Train with evidence.", fpv ? "From your first hover to manual flight. Tune your response, choose a course, and take off." : "Plan an inspection, validate the preflight checklist, then review recorded flight evidence.");
            var hero = Panel(fpv ? "Desert Range · Open session" : "Bridge Inspection · Mission training", fpv ? "Manual flight · Six route gates · Reset without losing your progress" : "Three capture targets · Ordered evidence · Local instructor review");
            var image = Element("scene-banner"); SetBackground(image, fpv ? "Art/Sunset" : "Art/Readiness"); hero.Add(image);
            hero.Add(Row(ActionButton(fpv ? "Quick Flight" : "Mission Brief", () => { if (fpv) ShowPage(Page.FreeFlight); else { _mission = LocalStore.Profile.missions[0]; ShowPage(Page.Brief); } }, true), ActionButton(fpv ? "Tune Aircraft" : "Edit Mission", () => ShowPage(fpv ? Page.Tuning : Page.MissionEditor))));
            s.Add(hero);
            var stats = Panel("Your progress", "Real local records — no sample scores.");
            stats.Add(Row(Stat(records.Count.ToString(), "Sessions"), Stat(records.Count(r => r.passed).ToString(), "Passed"), Stat(Duration(records.Sum(r => r.duration)), "Flight time")));
            s.Add(stats);
            s.Add(Row(ActionButton(fpv ? "FPV Academy" : "Training Catalog", () => ShowPage(fpv ? Page.Academy : Page.Catalog)), ActionButton(fpv ? "Race Challenges" : "Instructor Review", () => ShowPage(fpv ? Page.Challenges : Page.Instructor)), ActionButton("Controller Setup", () => ShowPage(Page.Controller)), ActionButton("Flight Records", () => ShowPage(Page.Records))));
            return s;
        }
        private static VisualElement Stat(string value, string caption)
        {
            var s = Element("stat"); s.Add(Text(value, "stat-value")); s.Add(Text(caption, "muted")); return s;
        }

        private VisualElement ControllerPage()
        {
            var c = LocalStore.Profile.settings.calibration;
            var s = Canvas("Input & calibration", "Make every movement count.", "Live device detection, assignable joystick axes, deadzone and a four-step calibration.");
            var card = Panel("Connected input", _inputCapabilities?.PrimaryDeviceLabel ?? "Keyboard / Touch");
            card.Add(Text(Gamepad.current != null ? "Gamepad detected. Mode 2 sticks are mapped automatically." : Joystick.current != null ? "Joystick detected. Choose the matching axes below." : "No USB gamepad or joystick detected. Keyboard and on-screen sticks remain available. Connect a supported device to inspect axes.", "wrap", "muted"));
            var axes = FlightInput.JoystickAxes();
            if (axes.Count > 0)
            {
                var names = axes.Select((a, i) => i + " · " + a.name).ToArray();
                Choice(card, "Throttle", names, names[Mathf.Clamp(c.throttleAxis, 0, names.Length - 1)], v => c.throttleAxis = int.Parse(v.Split(' ')[0]));
                Choice(card, "Yaw", names, names[Mathf.Clamp(c.yawAxis, 0, names.Length - 1)], v => c.yawAxis = int.Parse(v.Split(' ')[0]));
                Choice(card, "Pitch", names, names[Mathf.Clamp(c.pitchAxis, 0, names.Length - 1)], v => c.pitchAxis = int.Parse(v.Split(' ')[0]));
                Choice(card, "Roll", names, names[Mathf.Clamp(c.rollAxis, 0, names.Length - 1)], v => c.rollAxis = int.Parse(v.Split(' ')[0]));
            }
            _axisLabels = new Label[4]; _axisBars = new ProgressBar[4];
            string[] channels = { "Throttle", "Yaw", "Pitch", "Roll" };
            for (int i = 0; i < 4; i++)
            {
                _axisLabels[i] = Text(channels[i] + "   0.00", "axis-label"); card.Add(_axisLabels[i]);
                _axisBars[i] = new ProgressBar { lowValue = -1, highValue = 1, value = 0 }; card.Add(_axisBars[i]);
            }
            Range(card, "Deadzone", .01f, .3f, c.deadzone, v => c.deadzone = v);
            Check(card, "Reverse throttle", c.reverseThrottle, v => c.reverseThrottle = v);
            Check(card, "Reverse yaw", c.reverseYaw, v => c.reverseYaw = v);
            Check(card, "Reverse pitch", c.reversePitch, v => c.reversePitch = v);
            Check(card, "Reverse roll", c.reverseRoll, v => c.reverseRoll = v);
            string[] steps = { "1. Release the sticks to center.", "2. Move all axes through their full range.", "3. Check that all four channels move in the expected direction.", "4. Calibration saved. Test your setup in Free Flight." };
            card.Add(Text(steps[_calibrationStep], "panel-title", "wrap"));
            card.Add(Row(ActionButton(_calibrationStep == 0 ? "Capture Center" : _calibrationStep == 1 ? "Capture Range" : _calibrationStep == 2 ? "Save Calibration" : "Start Again", () =>
            {
                if (_calibrationStep == 0) { _center = FlightInput.Raw(); _minimum = _maximum = _center; _calibrating = true; _calibrationStep = 1; }
                else if (_calibrationStep == 1)
                {
                    if (Gamepad.current == null && Joystick.current == null) { ShowToast("Connect a gamepad or joystick to calibrate. Keyboard needs no calibration."); return; }
                    for (int i = 0; i < 4; i++) if (_maximum[i] - _minimum[i] < .5f) { ShowToast("Move all four axes through their full range, then retry."); return; }
                    _calibrating = false; _calibrationStep = 2;
                }
                else if (_calibrationStep == 2) { c.center = new[] { _center.x, _center.y, _center.z, _center.w }; c.minimum = new[] { _minimum.x, _minimum.y, _minimum.z, _minimum.w }; c.maximum = new[] { _maximum.x, _maximum.y, _maximum.z, _maximum.w }; c.savedUtc = DateTime.UtcNow.ToString("O"); c.device = _inputCapabilities?.PrimaryDeviceLabel; LocalStore.Save(); _calibrationStep = 3; }
                else _calibrationStep = 0;
                ShowPage(Page.Controller);
            }, true), ActionButton("Save Mapping", () => { LocalStore.Save(); ShowToast("Input settings saved."); }), ActionButton("Test in Free Flight", () => ShowPage(Page.FreeFlight))));
            s.Add(card); return s;
        }

        private VisualElement Academy()
        {
            var s = Canvas(WorkspaceName + " / Academy", "Build one skill at a time.", "Choose a lesson. Read its objective and control guidance, then practice in the 3D scene. All lessons are available.");
            var courses = CourseCatalog.All.Where(c => c.Workspace == (_workspace == TrainingWorkspace.Fpv ? "FPV" : "Aerial")).ToList();
            var categories = new[] { "All" }.Concat(courses.Select(c => c.Category).Distinct()).ToList();
            if (!categories.Contains(_academyFilter)) _academyFilter = "All";
            Choice(s, "Category", categories, _academyFilter, v => { _academyFilter = v; ShowPage(Page.Academy); });
            var grid = Element("product-grid");
            foreach (var course in courses.Where(c => _academyFilter == "All" || c.Category == _academyFilter))
            {
                var card = Panel(course.Title, course.Category + " · " + course.Minutes + " min · " + LessonStatus(course.Id));
                card.Add(Text(course.Objective, "wrap")); card.Add(ActionButton("View Lesson", () => OpenCourse(course), true)); grid.Add(card);
            }
            s.Add(grid); return s;
        }
        private void OpenCourse(Course course)
        {
            _course = course; _config = course.Config(); _mission = null;
            if (course.Id == "f01") _config.mode = "Normal";
            if (course.Id == "s01") _config.windMph = 22;
            ShowPage(Page.Brief);
        }
        private VisualElement Brief()
        {
            bool professional = _mission != null;
            _course ??= CourseCatalog.Find("a10");
            _config ??= _course.Config();
            var s = Canvas(professional ? "Mission / Brief" : "Academy / Lesson brief", professional ? _mission.title : _course.Title, professional ? "Capture each highlighted target in order. Return home after the inspection." : _course.Objective);
            var card = Panel("Session plan", professional ? _mission.environment + " · " + _mission.targets.Count + " capture targets" : _course.Category + " · " + _course.Minutes + " min");
            card.Add(Text(professional ? "Inspection markers indicate the required capture position. Move within 59 ft, point the camera toward the structure and press Photo. This local MVP evaluates target proximity and capture order, not real-world image quality." : TaskInstructions(_config.task), "wrap"));
            if (professional) foreach (var item in _mission.targetNames) card.Add(Text("• " + item, "wrap"));
            else Choice(card, "Flight mode", new[] { "Normal", "Sport", "Manual" }, _config.mode, v => _config.mode = v);
            card.Add(Text("R / F altitude · W A S D translation · Q / E yaw · P photo · H RTH\nManual mode: pitch and roll change attitude, not forward speed. Reset with Backspace after a crash.", "muted", "wrap"));
            card.Add(Row(ActionButton("Back to Catalog", () => ShowPage(professional ? Page.Catalog : Page.Academy)), ActionButton("Preflight Check", () => ShowPage(Page.Preflight), true)));
            s.Add(card); return s;
        }
        private static string TaskInstructions(string task) => task switch
        {
            "hover" => "Climb to 26–39 ft (8–12 m). Release the movement sticks and remain below 3.4 mph for 8 uninterrupted seconds.",
            "altitude" => "Hold between 62 and 75 ft (19–23 m) for 8 seconds.",
            "gates" => "Pass through all 6 orange gates in order. Follow the highlighted target. A pass requires being within 15 ft of its center.",
            "rth" => "Fly more than 65 ft from home. Press H or Return Home. Release both sticks and let the aircraft climb, return, and land.",
            "move" => "Fly at least 65 ft (20 m) from home, then return within 10 ft (3 m). Stay above the ground.",
            "land" => "Take off above 7 ft (2 m), return to the home pad, then descend below 3 ft and stop.",
            "brake" => "Reach 10 mph (4.5 m/s), then release the movement sticks. Stop below 1.3 mph.",
            "photo" => "Take off, frame the environment, and capture 3 photographs with P or the Photo button.",
            "yaw" => "Climb above 7 ft, then rotate through a total of 180 degrees while airborne.",
            "acro" => "In Manual mode, roll or pitch to an inverted attitude and recover upright above ground.",
            _ => "Explore the scene, then finish the session. Free flight has no prescribed route."
        };
        private VisualElement Preflight()
        {
            var s = Canvas("Before you fly", "Ready. Checked. Cleared for training.", "These checks apply to this simulator session only. They do not authorize a real flight.");
            var card = Panel("Preflight checklist", "All acknowledgements are required. Input readiness is detected automatically.");
            bool[] values = new bool[4];
            var start = ActionButton("Start Session", () => StartFlight(_mission != null ? MissionConfig(_mission) : _config ?? new FlightConfig()), true); start.SetEnabled(false);
            var checks = new[] { "I understand the session objective", "I know how to pause and reset", "I reviewed the map and simulated weather", "I understand this is training, not a flight authorization" };
            for (int i = 0; i < checks.Length; i++) { int index = i; Check(card, checks[i], false, v => { values[index] = v; start.SetEnabled(values.All(x => x)); }); }
            card.Add(Text("Input: " + (_inputCapabilities?.PrimaryDeviceLabel ?? "Keyboard / Touch") + "\nPause: Esc / Start / Pause button\nTraining boundary: 394 ft altitude · 755 ft radius", "muted", "wrap"));
            card.Add(Row(ActionButton("Controller Setup", () => ShowPage(Page.Controller)), start)); s.Add(card); return s;
        }
        private FlightConfig MissionConfig(MissionDefinition m) => new() { title = m.title, aircraft = AircraftCatalog.Selected.Name, workspace = "Professional", environment = m.environment, professional = true, mission = m, windMph = m.windMph, rthAltitude = m.rthAltitude, task = "inspection", objective = "Capture all " + m.targets.Count + " inspection targets in order." };

        private VisualElement FreeFlight()
        {
            _mission = null;
            _config = new FlightConfig { workspace = WorkspaceName, aircraft = AircraftCatalog.Selected.Name, mode = _workspace == TrainingWorkspace.Fpv ? "Manual" : "Normal", environment = _workspace == TrainingWorkspace.Fpv ? "Desert Range" : "Pacific Coast" };
            var config = _config;
            var s = Canvas(WorkspaceName + " / Free Flight", "Make the sky your practice space.", "Configure a local training environment. Settings below affect the next flight.");
            var card = Panel("Flight setup", "Procedural practice maps — not photorealistic scans or DJI hardware models.");
            var image = Element("scene-banner"); SetBackground(image, "Art/CoastHero"); card.Add(image);
            Choice(card, "Environment", new[] { "Pacific Coast", "Desert Range", "City Park", "Warehouse" }, config.environment, v => config.environment = v);
            Choice(card, "Aircraft", AircraftCatalog.Names, config.aircraft, v => { config.aircraft = v; LocalStore.Profile.selectedAircraft = AircraftCatalog.Resolve(v).Id; LocalStore.Save(); });
            card.Add(ActionButton("Browse Aircraft in Hangar", () => ShowPage(Page.Hangar)));
            Choice(card, "Flight mode", new[] { "Normal", "Sport", "Manual" }, config.mode, v => config.mode = v);
            Choice(card, "Weather", new[] { "Clear", "Overcast" }, config.weather, v => config.weather = v);
            Range(card, "Wind (mph)", 0, 25, config.windMph, v => config.windMph = v);
            Range(card, "Time of day (24h)", 6, 20, config.timeOfDay, v => config.timeOfDay = v);
            Range(card, "RTH altitude (m)", 10, 80, config.rthAltitude, v => config.rthAltitude = v);
            card.Add(Row(ActionButton("Controller Setup", () => ShowPage(Page.Controller)), ActionButton("Launch Free Flight", () => StartFlight(config), true))); s.Add(card); return s;
        }

        private VisualElement Challenges()
        {
            var s = Canvas(WorkspaceName + " / Challenges", "A clean line. A better time.", "Complete every highlighted gate. Results and personal bests are stored locally; no online leaderboard is simulated.");
            var grid = Element("product-grid");
            string[] maps = { "Pacific Coast", "Desert Range", "Warehouse", "City Park" };
            for (int i = 0; i < maps.Length; i++)
            {
                string map = maps[i];
                var best = LocalStore.Sessions().Where(r => r.courseId == "race-" + map && r.passed).OrderBy(r => r.duration).FirstOrDefault();
                var p = Panel(map + " Circuit", "6 gates · " + (best == null ? "No completed lap yet" : "Personal best " + Duration(best.duration)));
                p.Add(Text("The current MVP shares a six-gate route across four environments. Try assisted flight before switching to manual.", "wrap", "muted"));
                p.Add(ActionButton("Start Challenge", () => StartFlight(new FlightConfig { title = map + " Circuit", workspace = WorkspaceName, environment = map, aircraft = AircraftCatalog.Selected.Name, mode = _workspace == TrainingWorkspace.Fpv ? "Manual" : "Sport", race = true, task = "gates", courseId = "race-" + map, objective = "Fly through all 6 gates in order with as few collisions as possible." }), true)); grid.Add(p);
            }
            s.Add(grid); return s;
        }
        private VisualElement Hangar() => BuildHangarPage();
        private VisualElement Tuning()
        {
            var settings = LocalStore.Profile.settings;
            var s = Canvas("FPV / Aircraft tuning", "Shape your response.", "These values affect the simplified flight model, not a real transmitter or flight controller.");
            var p = Panel("Flight response", "Changes are applied on save and remain local.");
            Range(p, "Maximum angular rate (deg/s)", 90, 900, settings.rate, v => settings.rate = v);
            Range(p, "Stick expo", 0, .85f, settings.expo, v => settings.expo = v);
            Range(p, "Input sensitivity", .3f, 1.5f, settings.sensitivity, v => settings.sensitivity = v);
            Range(p, "Thrust-to-weight ratio", 1.2f, 5, settings.thrust, v => settings.thrust = v);
            Range(p, "FPV camera tilt (deg)", 0, 60, settings.cameraAngle, v => settings.cameraAngle = v);
            var curve = new ResponseCurve(); p.Add(curve);
            p.Add(Row(ActionButton("Reset Defaults", () => { settings.rate = 360; settings.expo = .3f; settings.sensitivity = 1; settings.thrust = 2.4f; settings.cameraAngle = 25; LocalStore.Save(); ShowPage(Page.Tuning); }), ActionButton("Save & Test", () => { LocalStore.Save(); ShowPage(Page.FreeFlight); }, true))); s.Add(p); return s;
        }

        private VisualElement OsdEditor()
        {
            var settings = LocalStore.Profile.settings;
            var s = Canvas("FPV / OSD layout", "The information you need. Where you need it.", "Drag a telemetry item to reposition it. The layout is shared with the in-flight HUD.");
            var p = Panel("Telemetry overlay", "Positions are normalized to the screen, so the layout scales across desktop and tablet.");
            var preview = Element("osd-preview"); SetBackground(preview, "Art/CoastHero");
            OsdToken(preview, "65 mph", settings.speedPosition, v => settings.speedPosition = v);
            OsdToken(preview, "120 ft", settings.altitudePosition, v => settings.altitudePosition = v);
            OsdToken(preview, "02:18", settings.timerPosition, v => settings.timerPosition = v);
            OsdToken(preview, "Battery 82%", settings.batteryPosition, v => settings.batteryPosition = v);
            p.Add(preview);
            Check(p, "Show speed", settings.osdSpeed, v => settings.osdSpeed = v); Check(p, "Show altitude", settings.osdAltitude, v => settings.osdAltitude = v);
            Check(p, "Show timer", settings.osdTimer, v => settings.osdTimer = v); Check(p, "Show battery", settings.osdBattery, v => settings.osdBattery = v);
            p.Add(ActionButton("Save OSD Layout", () => { LocalStore.Save(); ShowToast("OSD layout saved."); }, true)); s.Add(p); return s;
        }
        private static void OsdToken(VisualElement preview, string text, Vector2 position, Action<Vector2> changed)
        {
            var token = Text(text, "osd-token"); token.userData = position; preview.Add(token);
            token.RegisterCallback<GeometryChangedEvent>(_ => LayoutOsdPreview(preview));
            preview.RegisterCallback<GeometryChangedEvent>(_ => LayoutOsdPreview(preview));
            bool dragging = false;
            token.RegisterCallback<PointerDownEvent>(e => { dragging = true; token.CapturePointer(e.pointerId); e.StopPropagation(); });
            token.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging) return; var local = preview.WorldToLocal(e.position); var pos = new Vector2(Mathf.Clamp(local.x / preview.resolvedStyle.width, .02f, .78f), Mathf.Clamp(local.y / preview.resolvedStyle.height, .02f, .88f));
                token.userData = pos; LayoutOsdPreview(preview); changed(pos);
            });
            token.RegisterCallback<PointerUpEvent>(e => { dragging = false; token.ReleasePointer(e.pointerId); });
        }

        private VisualElement Catalog()
        {
            var s = Canvas("Professional / Training catalog", "Make each mission repeatable.", "Local missions and checklists. Open a brief or customize your target coordinates.");
            foreach (var m in LocalStore.Profile.missions)
            {
                var p = Panel(m.title, m.environment + " · " + m.targets.Count + " targets · " + m.timeLimit + " sec limit");
                p.Add(Row(ActionButton("Open Brief", () => { _mission = m; ShowPage(Page.Brief); }, true), ActionButton("Edit Mission", () => { _mission = m; ShowPage(Page.MissionEditor); }))); s.Add(p);
            }
            s.Add(ActionButton("Create Mission", () => { _mission = new MissionDefinition { id = Guid.NewGuid().ToString("N"), title = "New Inspection" }; ShowPage(Page.MissionEditor); })); return s;
        }
        private VisualElement MissionEditor()
        {
            _mission ??= LocalStore.Profile.missions[0]; var m = _mission;
            var s = Canvas("Professional / Mission editor", "Plan the task before the flight.", "Edit named capture targets in meters relative to the home pad. Positive Z points forward.");
            var p = Panel("Mission parameters");
            Field(p, "Mission title", m.title, v => m.title = v);
            Choice(p, "Environment", new[] { "City Park", "Pacific Coast", "Desert Range", "Warehouse" }, m.environment, v => m.environment = v);
            Number(p, "RTH altitude (m)", m.rthAltitude, v => m.rthAltitude = Mathf.Clamp(v, 10, 100));
            Number(p, "Wind (mph)", m.windMph, v => m.windMph = Mathf.Clamp(v, 0, 25));
            Number(p, "Time limit (seconds)", m.timeLimit, v => m.timeLimit = Mathf.Clamp(v, 30, 1800));
            Number(p, "Minimum safe distance (m)", m.safeDistance, v => m.safeDistance = Mathf.Clamp(v, 1, 20)); s.Add(p);
            for (int i = 0; i < m.targets.Count; i++)
            {
                int index = i; while (m.targetNames.Count <= i) m.targetNames.Add("Target " + (i + 1));
                var target = Panel("Capture target " + (i + 1)); Field(target, "Name", m.targetNames[i], v => m.targetNames[index] = v);
                Number(target, "X / East (m)", m.targets[i].x, v => { var a = m.targets[index]; a.x = Mathf.Clamp(v, -200, 200); m.targets[index] = a; });
                Number(target, "Y / Altitude (m)", m.targets[i].y, v => { var a = m.targets[index]; a.y = Mathf.Clamp(v, 2, 100); m.targets[index] = a; });
                Number(target, "Z / North (m)", m.targets[i].z, v => { var a = m.targets[index]; a.z = Mathf.Clamp(v, -200, 200); m.targets[index] = a; });
                target.Add(ActionButton("Remove Target", () => { if (m.targets.Count <= 1) { ShowToast("Keep at least one capture target."); return; } m.targets.RemoveAt(index); m.targetNames.RemoveAt(index); ShowPage(Page.MissionEditor); })); s.Add(target);
            }
            s.Add(Row(ActionButton("Add Target", () => { if (m.targets.Count >= 12) { ShowToast("Maximum 12 targets per mission."); return; } m.targets.Add(new Vector3(0, 15, 35 + m.targets.Count * 8)); m.targetNames.Add("Target " + m.targets.Count); ShowPage(Page.MissionEditor); }), ActionButton("Save Mission", () => { if (string.IsNullOrWhiteSpace(m.title)) { ShowToast("Enter a mission title."); return; } var existing = LocalStore.Profile.missions.FindIndex(x => x.id == m.id); if (existing < 0) LocalStore.Profile.missions.Add(m); else LocalStore.Profile.missions[existing] = m; LocalStore.Save(); ShowToast("Mission saved."); }, true), ActionButton("Preview Brief", () => ShowPage(Page.Brief)))); return s;
        }
        private VisualElement Instructor()
        {
            var s = Canvas("Professional / Instructor", "Review. Coach. Repeat.", "Local instructor workflow only. Remote classrooms, accounts and multiplayer are not connected in this build.");
            var p = Panel("Assign practice", "Assignments are stored on this device.");
            var selected = "Return-to-Home"; Choice(p, "Lesson", CourseCatalog.All.Select(c => c.Title), selected, v => selected = v);
            p.Add(ActionButton("Assign Lesson", () => { var course = CourseCatalog.All.Find(c => c.Title == selected); if (!LocalStore.Profile.assignedCourses.Contains(course.Id)) LocalStore.Profile.assignedCourses.Add(course.Id); LocalStore.Save(); ShowPage(Page.Instructor); }, true));
            foreach (string id in LocalStore.Profile.assignedCourses.ToArray())
            {
                var course = CourseCatalog.Find(id); p.Add(Row(Text(course.Title, "wrap"), ActionButton("Start Practice", () => OpenCourse(course)), ActionButton("Remove Assignment", () => { LocalStore.Profile.assignedCourses.Remove(id); LocalStore.Save(); ShowPage(Page.Instructor); })));
            }
            s.Add(p);
            foreach (var record in LocalStore.Sessions().Take(10))
            {
                var r = record; var item = Panel(r.title, r.score + " / 100 · " + Duration(r.duration) + " · " + r.startedUtc);
                item.Add(Row(ActionButton("Review Report", () => { _record = r; ShowPage(Page.Report); }), ActionButton("Replay Flight", () => BeginReplay(r)))); s.Add(item);
            }
            return s;
        }

        private VisualElement Readiness()
        {
            var s = Canvas("United States / Flight readiness", "Know where training ends.", "A checklist and links to official FAA resources. AeroSim cannot grant permission, verify airspace, or issue TRUST / Part 107 certificates.");
            var p = Panel("Before a real flight", "Always check current official requirements. No legal eligibility is inferred from completing these checkboxes.");
            var items = new[] { "Review the rules for your purpose of flight", "Review registration and Remote ID requirements", "Check airspace and any required authorization", "Check weather, people, obstacles and landing options" };
            foreach (string item in items) Check(p, item, LocalStore.Profile.readinessCompleted.Contains(item), v => { if (v && !LocalStore.Profile.readinessCompleted.Contains(item)) LocalStore.Profile.readinessCompleted.Add(item); if (!v) LocalStore.Profile.readinessCompleted.Remove(item); LocalStore.Save(); });
            p.Add(Row(ActionButton("FAA Drone Resources", () => Application.OpenURL("https://www.faa.gov/uas")), ActionButton("Recreational Flyers / TRUST", () => Application.OpenURL("https://www.faa.gov/uas/recreational_flyers")), ActionButton("Commercial / Part 107", () => Application.OpenURL("https://www.faa.gov/uas/commercial_operators"))));
            s.Add(p); return s;
        }
        private void ApplySettings()
        {
            var s = LocalStore.Profile.settings;
            Application.targetFrameRate = s.targetFps;
            QualitySettings.vSyncCount = 0; QualitySettings.SetQualityLevel(Mathf.RoundToInt(Mathf.Clamp(s.quality,0,2) * (QualitySettings.names.Length-1) / 2f));
            _root.EnableInClassList("high-contrast", s.highContrast);
            _root.RemoveFromClassList("large-text");
            RefreshUiPreferences();
            Time.fixedDeltaTime = .005f;
        }
        private VisualElement Settings()
        {
            var settings = LocalStore.Profile.settings;
            var s = Canvas("Preferences / Local profile", "Your simulator. Your setup.", "Settings are stored on this computer. Cloud sync and real aircraft connectivity are not enabled.");
            var p = Panel("General & accessibility");
            Field(p, "Pilot name", settings.pilotName, v => settings.pilotName = v);
            Choice(p, "Units", new[] { "U.S. Customary", "Metric" }, settings.units, v => settings.units = v);
            Choice(p, "Frame-rate limit", new[] { "30", "60", "120" }, settings.targetFps.ToString(), v => settings.targetFps = int.Parse(v));
            Choice(p, "Quality", new[] { "Low", "Medium", "High" }, new[] { "Low", "Medium", "High" }[Mathf.Clamp(settings.quality, 0, 2)], v => settings.quality = v == "Low" ? 0 : v == "Medium" ? 1 : 2);
            Range(p, "Motor sound volume", 0, 1, settings.volume, v => settings.volume = v);
            Check(p, "High-contrast interface", settings.highContrast, v => settings.highContrast = v);
            Choice(p, "UI Scale", new[] { "Auto", "80%", "90%", "100%", "110%", "125%", "150%" }, settings.uiScale ?? "Auto", v => { settings.uiScale = v; SaveUiPreferences(); });
            Choice(p, "HUD Scale", new[] { "Auto", "80%", "90%", "100%", "110%", "125%", "150%" }, settings.hudScale ?? "100%", v => { settings.hudScale = v; SaveUiPreferences(); });
            Choice(p, "Text Size", new[] { "100%", "125%", "150%", "200%" }, Mathf.RoundToInt(settings.textScale * 100) + "%", v => { settings.textScale = int.Parse(v.TrimEnd('%')) / 100f; SaveUiPreferences(); });
            p.Add(Text("Changes preview instantly and save locally. Menu and HUD scales are independent. Enlarged text reflows; long panels scroll.", "muted", "wrap"));
            Check(p, "Show touch controls on desktop", settings.showTouchControls, v => { settings.showTouchControls = v; SaveUiPreferences(); });
            Check(p, "Reduce camera smoothing", settings.reducedMotion, v => settings.reducedMotion = v);
            Check(p, "Show control guidance", settings.showControls, v => settings.showControls = v);
            p.Add(Row(ActionButton("Save Settings", () => { LocalStore.Save(); ApplySettings(); ShowToast("Settings saved."); }, true), ActionButton("Controller Setup", () => ShowPage(Page.Controller)), ActionButton("Run Setup Again", () => { _onboardStep = 0; ShowPage(Page.Onboarding); })));
            p.Add(Text("AeroSim " + Application.version + " · Offline training\nStorage: " + LocalStore.DirectoryPath + "\nNo telemetry leaves this device. Training data is not an FAA certificate.", "muted", "wrap"));
            s.Add(p); return s;
        }

        private VisualElement Records()
        {
            var records = LocalStore.Sessions(); var s = Canvas("Flight records / Local history", "Every flight tells a story.", "Replay recorded position, attitude, stick input and events. Export telemetry or a printable training report.");
            if (records.Count == 0)
            {
                var empty = Panel("Your first flight starts here.", "No sessions recorded on this device yet."); empty.Add(ActionButton("Start Free Flight", () => ShowPage(Page.FreeFlight), true)); s.Add(empty);
            }
            foreach (var r in records)
            {
                var p = Panel(r.title, r.environment + " · " + r.workspace + " · " + LocalDate(r.startedUtc));
                p.Add(Row(Stat(r.score.ToString(), "Score / 100"), Stat(Duration(r.duration), "Duration"), Stat(r.collisions.ToString(), "Collisions")));
                p.Add(Row(ActionButton("View Results", () => { _record = r; ShowPage(Page.Results); }, true), ActionButton("Replay", () => BeginReplay(r)), ActionButton("Export CSV", () => ExportCsv(r)), ActionButton("Report", () => { _record = r; ShowPage(Page.Report); }))); s.Add(p);
            }
            return s;
        }
        private VisualElement Results()
        {
            if (_record == null) return Records(); var r = _record;
            var s = Canvas("Session / Results", r.passed ? "Great work. Keep your momentum." : "Another flight. Another lesson.", r.title + " · " + r.environment);
            var p = Panel(r.passed ? "PASSED" : "PRACTICE AGAIN", r.feedback);
            p.Add(Row(Stat(r.score + " / 100", "Training score"), Stat(Duration(r.duration), "Flight time"), Stat(Length(r.distance), "Distance"), Stat(Length(r.maxAltitude), "Max altitude")));
            p.Add(Row(Stat(r.collisions.ToString(), "Collisions"), Stat(r.photos.ToString(), "Captures"), Stat(r.gates.ToString(), "Gates passed")));
            p.Add(Text("Scoring: objective completion 100; free flight over 5 seconds 90; incomplete practice starts at 35 plus time / gates / captures. Each collision subtracts 8 points. Passing requires 70 and a completed objective (or free flight).", "muted", "wrap"));
            p.Add(Row(ActionButton("Replay Flight", () => BeginReplay(r), true), ActionButton("Retry Session", () => StartFlight(r.config ?? new FlightConfig { title = r.title, environment = r.environment, workspace = r.workspace, mode = r.mode })), ActionButton("Training Report", () => ShowPage(Page.Report)), ActionButton("Back Home", () => ShowPage(Page.Home))));
            s.Add(p); return s;
        }
        private VisualElement Report()
        {
            if (_record == null) return Records(); var r = _record;
            var s = Canvas("Professional / Evidence report", r.title, "Local training record. Review event evidence and add instructor notes.");
            var p = Panel("Performance summary", r.score + " / 100 · " + Duration(r.duration) + " · " + (r.passed ? "Passed" : "Practice again"));
            p.Add(Text(r.feedback, "wrap"));
            foreach (var e in r.events) p.Add(Text(Duration(e.time) + "   " + e.kind.ToUpperInvariant() + "   " + e.message, "event-row", "wrap"));
            var notes = new TextField("Instructor notes") { value = r.instructorNote, multiline = true }; notes.AddToClassList("product-field"); notes.RegisterValueChangedCallback(e => r.instructorNote = e.newValue); p.Add(notes);
            p.Add(Row(ActionButton("Save Notes", () => SaveReportNotes(r)), ActionButton("Export Printable Report", () => ExportPrintableReport(r), true), ActionButton("Export CSV", () => ExportCsv(r)), ActionButton("Replay Evidence", () => BeginReplay(r))));
            s.Add(p); return s;
        }
        private void SaveReportNotes(SessionRecord record)
        {
            try { LocalStore.SaveNotes(record); ShowToast("Instructor notes saved."); }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
            { ShowToast(error is ArgumentException ? "Notes were not saved: invalid session identity." : "Notes were not saved. Check local storage access and available space."); }
        }
        private void ExportCsv(SessionRecord record) => TryExportReport(() => LocalStore.ExportCsv(record), "Telemetry CSV exported.");
        private void ExportPrintableReport(SessionRecord record) => TryExportReport(() => LocalStore.ExportReport(record), "Report exported. Use browser Print / Save as PDF.");
        private void TryExportReport(Func<string> export, string success)
        {
            try { var path = export(); OpenExport(path); ShowToast(success); }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
            { ShowToast(error is ArgumentException ? "Export failed: invalid session identity or evidence path." : "Export failed. Check local storage access and available space."); }
        }
        private static void OpenExport(string path)
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            Application.OpenURL(new Uri(path).AbsoluteUri);
#else
            Debug.Log("Export saved in application documents: " + path);
#endif
        }

        private sealed class ResponseCurve : VisualElement
        {
            public ResponseCurve()
            {
                AddToClassList("response-curve"); generateVisualContent += Draw; schedule.Execute(MarkDirtyRepaint).Every(100);
            }
            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D; var s = LocalStore.Profile.settings;
                p.strokeColor = new Color(.2f, .35f, .48f); p.lineWidth = 1;
                p.BeginPath(); p.MoveTo(new Vector2(24, contentRect.height - 24)); p.LineTo(new Vector2(contentRect.width - 24, contentRect.height - 24)); p.LineTo(new Vector2(contentRect.width - 24, 24)); p.Stroke();
                p.strokeColor = new Color(.1f, .8f, 1); p.lineWidth = 3; p.BeginPath();
                for (int i = 0; i <= 60; i++) { float x = i / 60f; var point = new Vector2(24 + x * (contentRect.width - 48), contentRect.height - 24 - Mathf.Clamp01(Mathf.Lerp(x, x * x * x, s.expo) * s.sensitivity) * (contentRect.height - 48)); if (i == 0) p.MoveTo(point); else p.LineTo(point); } p.Stroke();
            }
        }
    }
}
