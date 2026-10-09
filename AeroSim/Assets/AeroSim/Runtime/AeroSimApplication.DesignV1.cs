using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AeroSim.Domain;
using AeroSim.Input;
using AeroSim.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AeroSim
{
    public sealed partial class AeroSimApplication
    {
        private string _designCourseId = "a10", _designChallengeMap = "Pacific Coast", _designSettingsTab = "General", _designReadinessTab = "Recreational";
        private string _designCatalogSearch = "";
        private int _designTarget;
        private readonly List<Texture2D> _designEvidenceTextures = new();
        private void ReleaseDesignEvidenceTextures()
        {
            foreach (var texture in _designEvidenceTextures.ToArray())
                if (_designEvidenceTextures.Remove(texture) && texture != null) Destroy(texture);
        }

        // Paths are live vectors, not glyph substitutions or a screenshot with invisible buttons.
        private static VisualElement DesignIcon(string name, float size = 24) => new DesignLineIcon(name, size);
        private static VisualElement DesignBrand()
        {
            var brand = Element("brand"); brand.Add(DesignIcon("logo", 42)); brand.Add(Text("AeroSim", "brand-name")); return brand;
        }
        private static string NavigationIcon(string title)
        {
            if (title.Contains("Home") || title.Contains("Dashboard")) return "home";
            if (title.Contains("Academy") || title.Contains("Catalog")) return "book";
            if (title.Contains("Flight") && !title.Contains("Records")) return "flight";
            if (title.Contains("Challenge")) return "trophy";
            if (title.Contains("Hangar")) return "drone";
            if (title.Contains("Tuning")) return "settings";
            if (title.Contains("Instructor")) return "users";
            if (title.Contains("Mission")) return "edit";
            if (title.Contains("Record") || title.Contains("Report")) return "chart";
            if (title.Contains("OSD")) return "target";
            return "settings";
        }
        private ScrollView DesignPage(string title, string subtitle, string eyebrow = null)
        {
            var page = new ScrollView(); page.AddToClassList("product-scroll"); page.AddToClassList("design-page");
            page.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            if (!string.IsNullOrEmpty(eyebrow)) page.Add(Text(eyebrow.ToUpperInvariant(), "page-eyebrow"));
            page.Add(Text(title, "page-title")); page.Add(Text(subtitle, "page-subtitle", "wrap")); return page;
        }
        private static VisualElement DesignColumns(params VisualElement[] columns)
        { var row = Element("design-columns"); foreach (var column in columns) row.Add(column); return row; }
        private static VisualElement DesignPanel(string title, string subtitle = null, params string[] classes)
        { var p = Panel(title, subtitle); p.AddToClassList("design-panel"); foreach (var c in classes) p.AddToClassList(c); return p; }
        private static VisualElement DesignImage(string path, params string[] classes)
        { var image = Element("design-image"); foreach (var c in classes) image.AddToClassList(c); SetBackground(image, path); return image; }
        private static Button DesignButton(string title, string icon, Action action, bool primary = false)
        {
            var button = new Button(action); button.AddToClassList(primary ? "primary-button" : "action-button"); button.AddToClassList("design-icon-action");
            button.name = "action-" + title.ToLowerInvariant().Replace(" ", "-"); button.Add(DesignIcon(icon, 20)); button.Add(Text(title, "design-button-label", "wrap")); return button;
        }
        private static VisualElement DesignCopy(string title, string body)
        { var copy = Element("design-copy"); copy.Add(Text(title, "design-item-title", "wrap")); copy.Add(Text(body, "muted", "wrap")); return copy; }
        private static string DesignSceneArt(string environment) => environment switch
        { "Desert Range" => "Art/Sunset", "Warehouse" => "Art/DesignV1/FpvIndustrialSunset", "City Park" => "Art/Readiness", _ => "Art/DesignV1/CoastHeroV1" };
        private static string DesignCourseArt(Course course) => course.Id == "a02" ? "Art/Takeoff" : course.Id == "a04" ? "Art/Orientation" : course.Workspace == "FPV" ? "Art/Sunset" : course.Category == "Camera Skills" ? "Art/Cinematic" : course.Category == "Safety & Emergencies" ? "Art/Emergency" : "Art/ReturnHome";
        private VisualElement DesignAircraftImage(string css = "design-aircraft-image")
        {
            var rt = RenderAircraftPreview(AircraftCatalog.Selected); _hangarPreviews.Add(rt);
            var image = new Image { image = rt, scaleMode = ScaleMode.ScaleToFit }; image.AddToClassList(css); return image;
        }

        private VisualElement DesignOnboarding()
        {
            var p = LocalStore.Profile;
            var s = DesignPage(_onboardStep == 0 ? "What would you like to learn?" : _onboardStep == 1 ? "Make AeroSim your own." : "Your first flight starts here.",
                _onboardStep == 0 ? "You can switch training modes anytime." : "Simulation training is not flight authorization.");
            s.AddToClassList("design-onboarding-page");
            if (_onboardStep == 0)
            {
                var cards = Element("design-path-grid");
                string[] titles = { "Aerial Basics", "FPV Flight", "Professional Training" };
                string[] keys = { "Aerial", "FPV", "Professional" };
                string[] arts = { "Art/DesignV1/CoastHeroV1", "Art/DesignV1/FpvIndustrialSunset", "Art/Readiness" };
                string[] bodies = { "Safe takeoff, camera moves, and Return-to-Home", "Manual control, freestyle, and racing", "Standardized missions, checklists, and local reports" };
                for (int i = 0; i < keys.Length; i++)
                {
                    int index = i;
                    var card = new Button(() => { p.workspace = keys[index]; ShowPage(Page.Onboarding); }); card.AddToClassList("design-path-card");
                    card.name = "learning-path-" + keys[i].ToLowerInvariant(); if (p.workspace == keys[i]) card.AddToClassList("design-selected");
                    card.Add(DesignImage(arts[i], "design-path-image"));
                    var body = Element("design-path-copy"); body.Add(DesignIcon(i == 0 ? "flight" : i == 1 ? "drone" : "book", 36)); body.Add(DesignCopy(titles[i], bodies[i])); card.Add(body); cards.Add(card);
                }
                s.Add(cards);
            }
            else if (_onboardStep == 1)
            {
                var card = DesignPanel("Your preferences", "Stored on this device. No sign-in required.", "design-onboarding-form");
                Field(card, "Pilot name", p.settings.pilotName, v => p.settings.pilotName = v);
                Choice(card, "Flying purpose", new[] { "Recreational", "Professional / Part 107", "Still learning" }, p.settings.purpose, v => p.settings.purpose = v);
                Choice(card, "Units", new[] { "U.S. Customary", "Metric" }, p.settings.units, v => p.settings.units = v);
                Choice(card, "Performance", new[] { "30 fps", "60 fps", "120 fps" }, p.settings.targetFps + " fps", v => p.settings.targetFps = int.Parse(v.Split(' ')[0]));
                Check(card, "Show control guidance", p.settings.showControls, v => p.settings.showControls = v);
                Check(card, "High-contrast interface", p.settings.highContrast, v => p.settings.highContrast = v); s.Add(card);
            }
            else
            {
                var card = DesignPanel("Connect a controller or try the keyboard", "No real aircraft connection is required.", "design-onboarding-form");
                card.Add(DesignImage("Art/DesignV1/ControllerCutout", "design-controller-art"));
                card.Add(Text("W / S forward and backward · A / D strafe · R / F climb and descend\nQ / E rotate · H return home · C camera · P photo · Esc pause", "wrap"));
                card.Add(DesignButton("Check Controller", "controller", () => ShowPage(Page.Controller)));
                card.Add(Text("USB HID devices use Unity Input System. Specific DJI transmitter compatibility is not certified.", "muted", "wrap")); s.Add(card);
            }
            var footer = Element("design-footer"); footer.Add(Text("Step " + (_onboardStep + 1) + " of 3", "design-step-label"));
            var steps = Element("design-steps"); for (int i = 0; i < 3; i++) { var dot = Element("design-step-dot"); if (i <= _onboardStep) dot.AddToClassList("design-step-active"); steps.Add(dot); } footer.Add(steps);
            footer.Add(DesignButton("Back", "back", () => { _onboardStep = Math.Max(0, _onboardStep - 1); ShowPage(Page.Onboarding); }));
            footer.Add(DesignButton(_onboardStep == 2 ? "Start Flying" : "Continue", "flight", () => {
                LocalStore.Save(); if (_onboardStep < 2) { _onboardStep++; ShowPage(Page.Onboarding); }
                else { p.onboarded = true; LocalStore.Save(); SelectWorkspace(p.workspace == "FPV" ? TrainingWorkspace.Fpv : p.workspace == "Professional" ? TrainingWorkspace.Professional : TrainingWorkspace.Aerial); }
            }, true)); s.Add(footer); return s;
        }

        private VisualElement DesignController()
        {
            // Keep the validated calibration actions and live input callbacks; distribute their real controls into the reference's three zones.
            var original = (ScrollView)ControllerPage(); var card = original.contentContainer.Children().Last();
            var s = new ScrollView(); s.AddToClassList("product-scroll"); s.AddToClassList("design-page");
            s.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            s.AddToClassList("design-controller-page");
            var rail = DesignPanel("Controller Setup", "Four stages. Keyboard needs no calibration.", "design-rail");
            string[] steps = { "Center sticks", "Capture range", "Verify channels", "Save calibration" };
            for (int i = 0; i < steps.Length; i++)
            { var item = Element("design-calibration-step"); item.Add(Text((i + 1).ToString(), "design-number")); item.Add(Text(steps[i], "wrap")); if (i == _calibrationStep) item.AddToClassList("design-selected"); rail.Add(item); }
            var visual = Element("design-column", "design-controller-center");
            visual.Add(Text("Calibrate Your Controller", "page-title", "wrap"));
            visual.Add(Text("Move both sticks through their full range, then let them return to center.", "page-subtitle", "wrap"));
            var controllerArt = DesignImage("Art/DesignV1/ControllerCutout", "design-controller-art");
            controllerArt.Add(new DesignStickGuidance()); visual.Add(controllerArt);
            visual.Add(Text("SIMULATION INPUT · NOT AN AIRCRAFT LINK", "page-eyebrow"));
            var right = DesignPanel("Live Input", _inputCapabilities?.PrimaryDeviceLabel ?? "Keyboard / Touch", "design-side");
            var advanced = new Foldout { text = "Advanced Input Mapping", value = false }; advanced.AddToClassList("design-input-advanced");
            var footer = Element("design-footer");
            footer.Add(DesignButton("Back", "back", () => ShowPage(Page.Home)));
            bool firstTitle = true;
            int mutedIndex = 0;
            var axes = Element("design-controller-axis-list");
            for (int axis = 0; axis < 4; axis++)
            {
                var row = Element("design-controller-axis-row");
                var label = _axisLabels[axis]; label.RemoveFromHierarchy(); row.Add(label);
                var bar = new DesignInputBar(axis) { lowValue = -1, highValue = 1, value = 0 };
                _axisBars[axis].RemoveFromHierarchy(); _axisBars[axis] = bar; row.Add(bar); axes.Add(row);
            }
            right.Add(axes);
            foreach (var child in card.Children().ToArray())
            {
                child.RemoveFromHierarchy();
                if (child.ClassListContains("panel-title"))
                {
                    if (firstTitle) { firstTitle = false; continue; }
                    child.AddToClassList("design-calibration-instruction"); visual.Add(child);
                }
                else if (child.ClassListContains("muted"))
                {
                    if (mutedIndex++ > 0) { child.AddToClassList("design-controller-input-description"); right.Add(child); }
                }
                else if (child.ClassListContains("product-row"))
                {
                    foreach (var action in child.Children().ToArray()) { action.RemoveFromHierarchy(); footer.Add(action); }
                }
                else if (child.ClassListContains("product-field")) advanced.Add(child);
                else if (!child.ClassListContains("axis-label") && !(child is ProgressBar)) right.Add(child);
            }
            right.Add(advanced); s.Add(DesignColumns(rail, visual, right)); s.Add(footer); return s;
        }

        private VisualElement DesignAcademy()
        {
            bool fpv = _workspace == TrainingWorkspace.Fpv;
            var courses = CourseCatalog.All.Where(c => c.Workspace == (fpv ? "FPV" : "Aerial")).ToList();
            var categories = courses.Select(c => c.Category).Distinct().ToArray();
            if (_academyFilter == "All" || !categories.Contains(_academyFilter)) _academyFilter = categories[0];
            var selected = courses.Find(c => c.Id == _designCourseId && c.Category == _academyFilter) ?? courses.First(c => c.Category == _academyFilter); _designCourseId = selected.Id;
            var s = new ScrollView(); s.AddToClassList("product-scroll"); s.AddToClassList("design-page"); s.AddToClassList("design-academy-page");
            var rail = DesignPanel(fpv ? "FPV Academy" : "Academy", "Master the skills to fly with confidence.", "design-rail");
            foreach (var category in categories)
            {
                string key = category; var subset = courses.Where(c => c.Category == key).ToArray(); int completed = subset.Count(c => LocalStore.Profile.courses.Any(p => p.id == c.Id && p.bestScore >= 70));
                var button = new Button(() => { _academyFilter = key; ShowPage(Page.Academy); }); button.AddToClassList("design-category");
                if (key == _academyFilter) button.AddToClassList("design-selected"); button.Add(DesignIcon(key.Contains("Camera") ? "camera" : key.Contains("Safety") ? "shield" : "flight", 30));
                button.Add(DesignCopy(key, completed + " / " + subset.Length + " lessons completed")); rail.Add(button);
            }
            if (!fpv) rail.Add(DesignButton("U.S. Flight Readiness", "map", () => ShowPage(Page.Readiness)));
            var center = DesignPanel(_academyFilter, "A step-by-step path to confident flight.", "design-column");
            var list = Element("design-course-list");
            foreach (var course in courses.Where(c => c.Category == _academyFilter))
            {
                var item = new Button(() => { _designCourseId = course.Id; ShowPage(Page.Academy); }); item.AddToClassList("design-course-item");
                item.name = "course-" + course.Id; if (selected.Id == course.Id) item.AddToClassList("design-selected");
                item.Add(DesignImage(DesignCourseArt(course), "design-course-thumb")); item.Add(DesignCopy(course.Title, course.Objective));
                var meta = Element("design-course-meta"); meta.Add(Text(LessonStatus(course.Id), "design-course-score", "wrap")); meta.Add(Text(course.Minutes + " min", "muted")); item.Add(meta); list.Add(item);
            }
            center.Add(list);
            var detail = DesignPanel(selected.Title, selected.Objective, "design-side"); detail.Add(Text(selected.Category.ToUpperInvariant(), "page-eyebrow"));
            detail.Add(DesignImage(DesignCourseArt(selected), "design-detail-image")); detail.Add(Row(DesignIcon("clock"), Text(selected.Minutes + " min", "muted"), Text(LessonStatus(selected.Id), "muted")));
            detail.Add(Text("What you’ll learn", "panel-title")); detail.Add(Text(TaskInstructions(selected.Task), "wrap"));
            detail.Add(DesignButton(LocalStore.Profile.courses.Any(p => p.id == selected.Id) ? "Continue Lesson" : "Start Lesson", "play", () => OpenCourse(selected), true));
            detail.Add(DesignButton("Lesson Brief", "book", () => OpenCourse(selected))); s.Add(DesignColumns(rail, center, detail)); return s;
        }

        private VisualElement DesignFreeFlight()
        {
            _mission = null;
            _config = new FlightConfig { workspace = WorkspaceName, aircraft = AircraftCatalog.Selected.Name, mode = _workspace == TrainingWorkspace.Fpv ? "Manual" : "Normal", environment = _workspace == TrainingWorkspace.Fpv ? "Desert Range" : "Pacific Coast" };
            var config = _config;
            var s = new ScrollView(); s.AddToClassList("product-scroll"); s.AddToClassList("design-page"); s.AddToClassList("design-freeflight-page");
            var left = Element("design-column"); var hero = DesignImage(DesignSceneArt(config.environment), "design-environment-hero");
            var shade = Element("design-hero-shade"); hero.Add(shade); var copy = Element("design-environment-copy"); copy.Add(Text("FREE FLIGHT ENVIRONMENT", "page-eyebrow"));
            var name = Text(config.environment, "hero-title"); copy.Add(name); copy.Add(Text("Choose a training environment and make the sky your practice space.", "hero-description", "wrap")); hero.Add(copy); left.Add(hero);
            var scenes = Element("design-scene-grid"); string[] maps = { "Pacific Coast", "Desert Range", "City Park", "Warehouse" };
            foreach (string map in maps)
            {
                var b = new Button(); b.AddToClassList("design-scene-card"); b.Add(DesignImage(DesignSceneArt(map), "design-scene-thumb")); b.Add(Text(map, "design-item-title")); b.Add(Text(map == "Warehouse" ? "Indoor precision flying" : map == "City Park" ? "Parks and urban landmarks" : map == "Desert Range" ? "Open terrain and canyons" : "Coastal cliffs and forests", "muted", "wrap"));
                if (config.environment == map) b.AddToClassList("design-selected");
                b.clicked += () => { config.environment = map; SetBackground(hero, DesignSceneArt(map)); name.text = map; foreach (var child in scenes.Children()) child.RemoveFromClassList("design-selected"); b.AddToClassList("design-selected"); }; scenes.Add(b);
            }
            left.Add(scenes);
            var right = DesignPanel("Flight Setup", null, "design-side");
            right.Add(DesignAircraftImage()); Choice(right, "Aircraft", AircraftCatalog.Names, config.aircraft, v => { config.aircraft = v; LocalStore.Profile.selectedAircraft = AircraftCatalog.Resolve(v).Id; LocalStore.Save(); });
            right.Add(ActionButton("Browse Aircraft in Hangar", () => ShowPage(Page.Hangar)));
            Choice(right, "Flight mode", new[] { "Normal", "Sport", "Manual" }, config.mode, v => config.mode = v);
            Choice(right, "Weather", new[] { "Clear", "Overcast" }, config.weather, v => config.weather = v);
            Range(right, "Wind (mph)", 0, 25, config.windMph, v => config.windMph = v); Range(right, "Time of day (24h)", 6, 20, config.timeOfDay, v => config.timeOfDay = v);
            var advanced = new Foldout { text = "Advanced Flight Settings", value = false }; advanced.AddToClassList("design-input-advanced");
            bool metric = LocalStore.Profile.settings.units == "Metric";
            Range(advanced, metric ? "RTH altitude (m)" : "RTH altitude (ft)", metric ? 10 : 33, metric ? 80 : 262,
                metric ? config.rthAltitude : config.rthAltitude * 3.28084f, v => config.rthAltitude = metric ? v : v / 3.28084f);
            right.Add(advanced);
            s.Add(DesignColumns(left, right));
            var footer = Element("design-footer"); footer.Add(DesignIcon("controller", 32)); footer.Add(DesignCopy(_inputCapabilities?.PrimaryDeviceLabel ?? "Keyboard / Touch", "Local input · No aircraft connected"));
            footer.Add(DesignCopy("Training Airspace", "SIMULATION — NOT FOR NAVIGATION")); footer.Add(ActionButton("Controller Setup", () => ShowPage(Page.Controller)));
            footer.Add(DesignButton("Launch Free Flight", "play", () => StartFlight(config), true)); s.Add(footer); return s;
        }

        private VisualElement DesignFpvHome()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("design-fpv-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.contentContainer.style.flexGrow = 1;
            var root = Element("design-fpv-stage"); SetBackground(root, "Art/DesignV1/FpvIndustrialSunset"); var shade = Element("design-fpv-shade"); root.Add(shade);
            var menu = Element("design-fpv-menu");
            void Tile(string title, string description, string icon, Page page)
            { var b = new Button(() => ShowPage(page)); b.AddToClassList("design-fpv-tile"); b.Add(DesignIcon(icon, 36)); b.Add(DesignCopy(title, description)); b.Add(Text("›", "row-chevron")); menu.Add(b); }
            Tile("FPV Academy", "Learn. Practice. Progress.", "book", Page.Academy); Tile("Challenges", "Test your skills", "trophy", Page.Challenges);
            Tile("Hangar & Tuning", "Quads, rates and settings", "settings", Page.Tuning); Tile("Replays", "Review and improve", "play", Page.Records); root.Add(menu);
            var aircraft = Element("design-fpv-aircraft"); aircraft.Add(DesignAircraftImage()); aircraft.Add(DesignCopy(AircraftCatalog.Selected.Name, "Manual · Camera " + LocalStore.Profile.settings.cameraAngle.ToString("F0") + "°")); root.Add(aircraft);
            var quick = DesignButton("Quick Flight", "play", () => ShowPage(Page.FreeFlight), true); quick.AddToClassList("design-fpv-quick"); root.Add(quick);
            var progress = Element("design-fpv-progress"); int completed = CourseCatalog.All.Count(c => c.Workspace == "FPV" && LocalStore.Profile.courses.Any(p => p.id == c.Id && p.bestScore >= 70));
            progress.Add(DesignCopy("Manual Foundations", completed + " of " + CourseCatalog.All.Count(c => c.Workspace == "FPV") + " lessons completed")); progress.Add(new ProgressBar { value = completed * 100f / CourseCatalog.All.Count(c => c.Workspace == "FPV") }); root.Add(progress);
            root.Add(Text("Simulation training only. Not for real-world operation.", "design-fpv-disclaimer"));
            scroll.Add(root);
            // The 1920 reference uses absolute placements; compact layouts reflow into
            // document order. Give the stage a viewport-sized minimum, never a fixed
            // height, so expanded text remains genuinely scrollable rather than clipped.
            scroll.RegisterCallback<GeometryChangedEvent>(_ => {
                bool compact = _root.ClassListContains("layout-phone") || _root.ClassListContains("layout-tablet") || _root.ClassListContains("text-expanded");
                float minimum = Mathf.Max(scroll.contentViewport.resolvedStyle.height, compact ? 1150 : 0);
                if (!float.IsNaN(minimum) && Mathf.Abs(root.style.minHeight.value.value - minimum) > 1) root.style.minHeight = minimum;
            });
            foreach (var button in root.Query<Button>().ToList())
            { var target = button; target.RegisterCallback<FocusInEvent>(_ => scroll.ScrollTo(target)); }
            return scroll;
        }

        private VisualElement DesignSettings()
        {
            var settings = LocalStore.Profile.settings;
            var s = DesignPage("Settings", "Customize your experience and configure how AeroSim works on this device.");
            s.AddToClassList("design-settings-page");
            var columns = Element("design-columns"); var main = Element("design-column"); var general = DesignPanel(_designSettingsTab, "Settings preview immediately and save locally.");
            if (_designSettingsTab == "General")
            {
                Field(general, "Pilot name", settings.pilotName, v => settings.pilotName = v);
                var language = new DropdownField("Language", new List<string> { "English (United States)" }, 0); language.AddToClassList("product-field"); general.Add(language);
                var region = new DropdownField("Region", new List<string> { "United States" }, 0); region.AddToClassList("product-field"); general.Add(region);
                Choice(general, "Units", new[] { "U.S. Customary", "Metric" }, settings.units, v => { settings.units = v; LocalStore.Save(); RefreshDesignSettingsPreview(); });
                Choice(general, "UI Scale", new[] { "Auto", "80%", "90%", "100%", "110%", "125%", "150%" }, settings.uiScale ?? "Auto", v => { settings.uiScale = v; SaveUiPreferences(); });
                Choice(general, "HUD Scale", new[] { "Auto", "80%", "90%", "100%", "110%", "125%", "150%" }, settings.hudScale ?? "100%", v => { settings.hudScale = v; SaveUiPreferences(); });
                Choice(general, "Text Size", new[] { "100%", "125%", "150%", "200%" }, Mathf.RoundToInt(settings.textScale * 100) + "%", v => { settings.textScale = int.Parse(v.TrimEnd('%')) / 100f; SaveUiPreferences(); });
            }
            else if (_designSettingsTab == "Graphics")
            {
                Choice(general, "Frame-rate limit", new[] { "30", "60", "120" }, settings.targetFps.ToString(), v => { settings.targetFps = int.Parse(v); ApplySettings(); });
                Choice(general, "Quality", new[] { "Low", "Medium", "High" }, new[] { "Low", "Medium", "High" }[Mathf.Clamp(settings.quality, 0, 2)], v => { settings.quality = v == "Low" ? 0 : v == "Medium" ? 1 : 2; ApplySettings(); });
                Check(general, "Reduce camera smoothing", settings.reducedMotion, v => settings.reducedMotion = v);
            }
            else if (_designSettingsTab == "Audio") Range(general, "Motor sound volume", 0, 1, settings.volume, v => settings.volume = v);
            else if (_designSettingsTab == "Controls")
            { Check(general, "Show touch controls on desktop", settings.showTouchControls, v => { settings.showTouchControls = v; SaveUiPreferences(); }); Check(general, "Show control guidance", settings.showControls, v => settings.showControls = v); general.Add(DesignButton("Controller Setup", "controller", () => ShowPage(Page.Controller))); }
            else if (_designSettingsTab == "Accessibility")
            { Check(general, "High-contrast interface", settings.highContrast, v => { settings.highContrast = v; ApplySettings(); }); Check(general, "Reduce camera smoothing", settings.reducedMotion, v => settings.reducedMotion = v); Choice(general, "Text Size", new[] { "100%", "125%", "150%", "200%" }, Mathf.RoundToInt(settings.textScale * 100) + "%", v => { settings.textScale = int.Parse(v.TrimEnd('%')) / 100f; SaveUiPreferences(); }); }
            else general.Add(Text("No cloud sync or analytics connection is enabled. Flight records stay on this device.\n\nStorage: " + LocalStore.DirectoryPath + "\n\nExports can contain your pilot name, training events and flight telemetry. Review files before sharing.", "wrap", "muted"));
            main.Add(general);
            var preview = DesignPanel("Preview", "Sample telemetry only — not a live aircraft status.", "design-side", "design-settings-preview");
            var image = DesignImage("Art/DesignV1/CoastHeroV1", "design-settings-image"); var telemetry = Element("design-preview-telemetry"); var telemetryLabel = Text("", "design-item-title", "wrap"); telemetryLabel.name = "design-units-preview"; telemetry.Add(DesignIcon("flight")); telemetry.Add(telemetryLabel); image.Add(telemetry); preview.Add(image); columns.Add(main); columns.Add(preview); s.Add(columns);
            if (_designSettingsTab == "General")
            { var startup = DesignPanel("Startup", "AeroSim opens your last selected training workspace. No background content update service is connected."); startup.Add(DesignButton("Run Setup Again", "flight", () => { _onboardStep = 0; ShowPage(Page.Onboarding); })); s.Add(startup); }
            var footer = Element("design-footer"); footer.Add(Text("AeroSim " + Application.version + " · Offline training", "muted", "wrap")); footer.Add(DesignButton("Back Home", "back", () => ShowPage(Page.Home))); footer.Add(DesignButton("Save Settings", "save", () => { LocalStore.Save(); ApplySettings(); ShowToast("Settings saved."); }, true)); s.Add(footer);
            telemetryLabel.text = DesignUnitsPreview(); return s;
        }
        private string DesignUnitsPreview() => LocalStore.Profile.settings.units == "Metric" ? "H 25.6 m     11.6 km/h     22°C" : "H 84 ft     7.2 mph     72°F";
        private void RefreshDesignSettingsPreview() { var label = _root.Q<Label>("design-units-preview"); if (label != null) label.text = DesignUnitsPreview(); }
        private void AddDesignSettingsNavigation(VisualElement navigation)
        {
            navigation.Add(Text("Settings", "design-nav-section"));
            string[] tabs = { "General", "Graphics", "Audio", "Controls", "Accessibility", "Privacy & Data" };
            foreach (string tab in tabs)
            { var b = new Button(() => { _designSettingsTab = tab; ShowPage(Page.Settings); }); b.AddToClassList("nav-button"); if (tab == _designSettingsTab) b.AddToClassList("nav-button-selected"); var icon = DesignIcon(tab == "Controls" ? "controller" : tab == "Privacy & Data" ? "shield" : "settings"); icon.AddToClassList("nav-icon"); b.Add(icon); b.Add(Text(tab, "nav-label")); navigation.Add(b); }
        }

        private VisualElement DesignChallenges()
        {
            var s = DesignPage(_workspace == TrainingWorkspace.Fpv ? "FPV Challenges" : "Challenges", "Test your skills across training locations and objectives. Personal records stay on this device.");
            string[] maps = { "Pacific Coast", "Desert Range", "Warehouse", "City Park" };
            var grid = Element("design-challenge-grid", "design-column");
            FlightConfig Config(string map) => new FlightConfig { title = map + " Circuit", workspace = WorkspaceName, environment = map, aircraft = AircraftCatalog.Selected.Name, mode = _workspace == TrainingWorkspace.Fpv ? "Manual" : "Sport", race = true, task = "gates", courseId = "race-" + map, objective = "Fly through all 6 gates in order with as few collisions as possible." };
            foreach (string map in maps)
            {
                var best = LocalStore.Sessions().Where(r => r.courseId == "race-" + map && r.passed).OrderBy(r => r.duration).FirstOrDefault();
                var card = new Button(() => { _designChallengeMap = map; ShowPage(Page.Challenges); }); card.AddToClassList("design-challenge-card"); if (_designChallengeMap == map) card.AddToClassList("design-selected");
                card.Add(DesignImage(DesignSceneArt(map), "design-challenge-image")); card.Add(Text(map + " Circuit", "panel-title", "wrap"));
                card.Add(Text("6 gates · " + (_workspace == TrainingWorkspace.Fpv ? "Manual" : "Sport"), "muted"));
                var score = Element("design-challenge-best"); score.Add(DesignIcon("trophy")); score.Add(DesignCopy(best == null ? "Not attempted" : Duration(best.duration), "Personal Best")); card.Add(score); grid.Add(card);
            }
            var detail = DesignPanel(_designChallengeMap + " Circuit", "Six route gates · Local training record", "design-side"); detail.Add(DesignImage(DesignSceneArt(_designChallengeMap), "design-detail-image"));
            var selectedBest = LocalStore.Sessions().Where(r => r.courseId == "race-" + _designChallengeMap && r.passed).OrderBy(r => r.duration).FirstOrDefault(); detail.Add(Stat(selectedBest == null ? "—" : Duration(selectedBest.duration), "Personal best"));
            detail.Add(Text("Fly through every highlighted gate in order. Four environments share a six-gate route. No online ranking or ghost-flight service is connected.", "muted", "wrap"));
            detail.Add(DesignButton("Start Challenge", "play", () => StartFlight(Config(_designChallengeMap)), true)); detail.Add(DesignButton("Practice", "controller", () => { var c = Config(_designChallengeMap); c.mode = "Normal"; StartFlight(c); }));
            if (selectedBest != null) detail.Add(DesignButton("Replay Personal Best", "play", () => BeginReplay(selectedBest)));
            s.Add(DesignColumns(grid, detail)); return s;
        }

        private VisualElement DesignTuning()
        {
            var settings = LocalStore.Profile.settings;
            var s = DesignPage("Aircraft Tuning", "Shape your response. These values affect the simplified simulator, not a real flight controller.");
            var rail = DesignPanel("My Aircraft", "Selection stays on this device.", "design-rail"); rail.Add(DesignAircraftImage()); rail.Add(Text(AircraftCatalog.Selected.Name, "design-item-title", "wrap")); rail.Add(DesignButton("Browse Hangar", "drone", () => ShowPage(Page.Hangar)));
            rail.Add(Text("One local configuration. Versioned build profiles and A/B comparison are not implemented.", "muted", "wrap"));
            var center = DesignPanel(AircraftCatalog.Selected.Name, "Live response preview", "design-column"); center.Add(DesignAircraftImage("design-tuning-aircraft")); center.Add(Text("Rate Response Curve", "panel-title")); center.Add(new ResponseCurve()); center.Add(Text("Stick deflection → angular response\nChanges preview the simplified expo and sensitivity curve.", "muted", "wrap"));
            var right = DesignPanel("Rates", "Values are shared across roll, pitch and yaw.", "design-side");
            Range(right, "Maximum angular rate (deg/s)", 90, 900, settings.rate, v => settings.rate = v); Range(right, "Stick expo", 0, .85f, settings.expo, v => settings.expo = v);
            Range(right, "Input sensitivity", .3f, 1.5f, settings.sensitivity, v => settings.sensitivity = v); Range(right, "Thrust-to-weight ratio", 1.2f, 5, settings.thrust, v => settings.thrust = v);
            Range(right, "FPV camera tilt (deg)", 0, 60, settings.cameraAngle, v => settings.cameraAngle = v); s.Add(DesignColumns(rail, center, right));
            var footer = Element("design-footer"); footer.Add(Text("Local configuration · Save before testing", "muted"));
            footer.Add(DesignButton("Reset Defaults", "back", () => { settings.rate = 360; settings.expo = .3f; settings.sensitivity = 1; settings.thrust = 2.4f; settings.cameraAngle = 25; LocalStore.Save(); ShowPage(Page.Tuning); }));
            footer.Add(DesignButton("Save & Test", "play", () => { LocalStore.Save(); ShowPage(Page.FreeFlight); }, true)); s.Add(footer); return s;
        }

        private VisualElement DesignOsdEditor()
        {
            var settings = LocalStore.Profile.settings;
            var s = DesignPage("OSD Editor", "Design and position the telemetry items used by your in-flight HUD.");
            var library = DesignPanel("Components", "Drag a preview item to reposition it.", "design-rail");
            Check(library, "Show speed", settings.osdSpeed, v => settings.osdSpeed = v); Check(library, "Show altitude", settings.osdAltitude, v => settings.osdAltitude = v); Check(library, "Show timer", settings.osdTimer, v => settings.osdTimer = v); Check(library, "Show battery", settings.osdBattery, v => settings.osdBattery = v);
            var stage = Element("design-column"); var preview = Element("osd-preview", "design-osd-preview"); SetBackground(preview, "Art/DesignV1/FpvIndustrialSunset");
            OsdToken(preview, "SPEED 65 mph", settings.speedPosition, v => settings.speedPosition = v); OsdToken(preview, "ALT 120 ft", settings.altitudePosition, v => settings.altitudePosition = v);
            OsdToken(preview, "TIMER 02:18", settings.timerPosition, v => settings.timerPosition = v); OsdToken(preview, "BATTERY 82%", settings.batteryPosition, v => settings.batteryPosition = v); stage.Add(preview);
            stage.Add(Text("PREVIEW SAMPLE DATA · NOT LIVE TELEMETRY", "page-eyebrow"));
            var properties = DesignPanel("Layout", "Normalized coordinates preserve your layout across screen sizes.", "design-side");
            void Position(string name, Func<Vector2> get, Action<Vector2> set)
            {
                properties.Add(Text(name, "design-item-title"));
                Range(properties, "Position X", .02f, .78f, get().x, v => { var pos = get(); pos.x = v; set(pos); });
                Range(properties, "Position Y", .02f, .88f, get().y, v => { var pos = get(); pos.y = v; set(pos); });
            }
            Position("Speed", () => settings.speedPosition, v => settings.speedPosition = v); Position("Altitude", () => settings.altitudePosition, v => settings.altitudePosition = v);
            Position("Timer", () => settings.timerPosition, v => settings.timerPosition = v); Position("Battery", () => settings.batteryPosition, v => settings.batteryPosition = v);
            properties.Add(Text("Drag changes preview immediately. Coordinate fields take effect after Preview Layout. Undo history and additional decorative components are not available.", "muted", "wrap"));
            s.Add(DesignColumns(library, stage, properties)); var footer = Element("design-footer"); footer.Add(Text("Custom positions respect in-flight safety areas. On narrow or obstructed viewports, a temporary fallback keeps telemetry visible without changing your saved positions.", "muted", "wrap")); footer.Add(DesignButton("Preview Layout", "target", () => ShowPage(Page.OsdEditor))); footer.Add(DesignButton("Save OSD Layout", "save", () => { LocalStore.Save(); ShowToast("OSD layout saved."); }, true)); s.Add(footer); return s;
        }

        private VisualElement DesignDashboard()
        {
            var records = LocalStore.Sessions(); var m = LocalStore.Profile.missions[0];
            var s = DesignPage("Welcome back, " + LocalStore.Profile.settings.pilotName, "Continue your training and build the skills to fly safely and professionally.", DateTime.Now.ToString("dddd, MMM d", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
            var assignment = DesignPanel(m.title + " Fundamentals", "NEXT TRAINING TASK", "design-column", "design-dashboard-hero"); SetBackground(assignment, "Art/DesignV1/CoastHeroV1");
            var paragraph = Text("Plan the mission, capture each target and review your recorded evidence.", "wrap", "design-assignment-description");
            assignment.Add(paragraph); assignment.Add(DesignButton("Continue Assignment", "play", () => { _mission = m; ShowPage(Page.Brief); }, true));
            var status = DesignPanel("Training Status", "Local simulator readiness — not a professional qualification.", "design-side");
            status.Add(DesignStatus("Controller Input", _inputCapabilities?.PrimaryDeviceLabel ?? "Keyboard / Touch", "controller"));
            status.Add(DesignStatus("Calibration", string.IsNullOrEmpty(LocalStore.Profile.settings.calibration.savedUtc) ? "Not calibrated" : "Saved " + LocalDate(LocalStore.Profile.settings.calibration.savedUtc), "settings"));
            status.Add(DesignStatus("Records", records.Count + " sessions · " + records.Count(r => r.passed) + " passed", "chart")); s.Add(DesignColumns(assignment, status));
            var plan = DesignPanel("Training Plan", "Your local inspection tasks.");
            foreach (var mission in LocalStore.Profile.missions)
            { var row = new Button(() => { _mission = mission; ShowPage(Page.Brief); }); row.AddToClassList("design-table-row"); row.Add(DesignImage(DesignSceneArt(mission.environment), "design-table-thumb")); row.Add(DesignCopy(mission.title, mission.environment + " · " + mission.targets.Count + " capture targets")); row.Add(Text(Duration(mission.timeLimit), "design-table-value")); row.Add(Text("›", "row-chevron")); plan.Add(row); } s.Add(plan);
            var skills = DesignPanel("Skills to Review", "Build confidence before your next inspection.", "design-column"); skills.Add(DesignButton("Obstacle Awareness", "shield", () => OpenCourse(CourseCatalog.Find("a11")))); skills.Add(DesignButton("Return-to-Home", "home", () => OpenCourse(CourseCatalog.Find("a10"))));
            var recent = DesignPanel("Recent Reports", "Real session evidence only.", "design-column");
            if (records.Count == 0) recent.Add(Text("No reports yet. Complete a session to review your performance.", "muted", "wrap"));
            foreach (var r in records.Take(3)) { var row = new Button(() => { _record = r; ShowPage(Page.Report); }); row.AddToClassList("design-table-row"); row.Add(DesignCopy(r.title, LocalDate(r.startedUtc))); row.Add(Text(r.score + " / 100", "design-table-value")); row.Add(Text(r.passed ? "Passed" : "Practice", "muted")); recent.Add(row); }
            s.Add(DesignColumns(skills, recent)); return s;
        }
        private static VisualElement DesignStatus(string title, string value, string icon)
        { var row = Element("design-status-row"); row.Add(DesignIcon(icon, 32)); row.Add(DesignCopy(title, value)); return row; }

        private VisualElement DesignBrief()
        {
            bool professional = _mission != null; _course ??= CourseCatalog.Find("a10"); _config ??= _course.Config();
            var s = DesignPage(professional ? "Mission Brief · " + _mission.title : "Lesson Brief · " + _course.Title,
                professional ? "Inspect the training targets and capture evidence following the planned route." : _course.Objective, professional ? "Professional Training" : _course.Category);
            var visual = DesignPanel(professional ? "Mission Map & Route" : "Lesson Preview", null, "design-column");
            if (professional) { visual.Add(new DesignMissionMap(_mission)); visual.Add(Text("SCHEMATIC TRAINING MAP — NOT FOR NAVIGATION", "page-eyebrow")); }
            else visual.Add(DesignImage(DesignCourseArt(_course), "design-brief-image"));
            var detail = Element("design-side"); var objective = DesignPanel("Objective", professional ? "Capture every target in order, then return home." : _course.Objective); detail.Add(objective);
            var steps = DesignPanel(professional ? "Mission Steps" : "What You’ll Practice");
            if (professional) { for (int i = 0; i < _mission.targetNames.Count; i++) steps.Add(Text((i + 1) + ". " + _mission.targetNames[i], "event-row", "wrap")); }
            else { steps.Add(Text(TaskInstructions(_config.task), "wrap")); Choice(steps, "Flight mode", new[] { "Normal", "Sport", "Manual" }, _config.mode, v => _config.mode = v); } detail.Add(steps);
            var risk = DesignPanel("Risks", "Training boundaries, terrain proximity and wind can affect your session."); risk.Add(Text(professional ? "Wind " + _mission.windMph.ToString("F0") + " mph · RTH " + Length(_mission.rthAltitude) + "\nSafe distance " + Length(_mission.safeDistance) : "Keep clear of terrain. Check RTH altitude before using Return-to-Home.", "wrap", "muted")); detail.Add(risk);
            var criteria = DesignPanel("Passing Criteria", "Complete the objective with a score of at least 70. Each collision subtracts 8 points."); detail.Add(criteria);
            s.Add(DesignColumns(visual, detail)); var footer = Element("design-footer"); footer.Add(Text("Training only. No real-world flight authorization.", "muted", "wrap")); footer.Add(DesignButton("Back to Catalog", "back", () => ShowPage(professional ? Page.Catalog : Page.Academy))); footer.Add(DesignButton("Preflight Check", "play", () => ShowPage(Page.Preflight), true)); s.Add(footer); return s;
        }

        private VisualElement DesignPreflight()
        {
            bool professional = _mission != null;
            var config = professional ? MissionConfig(_mission) : _config ?? new FlightConfig();
            var s = DesignPage("Pre-Flight Check · " + config.title, "Verify your session settings and training readiness. Resolve all required acknowledgements to start.", professional ? "Professional Training" : "Before You Fly");
            s.AddToClassList("design-preflight-page");
            var preview = DesignPanel("Mission Preview", config.environment, "design-column"); if (professional) preview.Add(new DesignMissionMap(_mission)); else preview.Add(DesignImage(DesignSceneArt(config.environment), "design-brief-image")); preview.Add(Text("SIMULATION — NOT FOR NAVIGATION", "page-eyebrow"));
            var checklist = DesignPanel("Pre-Flight Checklist", "Input is detected automatically. Other items are your acknowledgements.", "design-side");
            checklist.Add(DesignStatus("Controller Input", _inputCapabilities?.PrimaryDeviceLabel ?? "Keyboard / Touch", "controller")); checklist.Add(DesignStatus("Aircraft Configuration", AircraftCatalog.Selected.Name + " · " + config.mode, "drone"));
            bool[] values = new bool[4]; var start = DesignButton("Start Session", "play", () => StartFlight(config), true); start.SetEnabled(false);
            string[] checks = { "I understand the session objective", "I know how to pause and reset", "I reviewed the map and simulated weather", "I understand this is training, not a flight authorization" };
            for (int i = 0; i < checks.Length; i++) { int index = i; var row = Element("design-checklist-item"); row.Add(Text((i + 1).ToString(), "design-number")); Check(row, checks[i], false, v => { values[index] = v; start.SetEnabled(values.All(x => x)); }); checklist.Add(row); }
            checklist.Add(DesignStatus("Return-to-Home Altitude", Length(config.rthAltitude) + " relative to takeoff", "home")); checklist.Add(DesignStatus("Weather & Wind", config.weather + " · " + config.windMph.ToString("F0") + " mph (simulated)", "flight")); s.Add(DesignColumns(preview, checklist));
            var footer = Element("design-footer"); footer.Add(Text("Pause: Esc / Start · Training boundary: 394 ft altitude / 755 ft radius", "muted", "wrap")); footer.Add(ActionButton("Controller Setup", () => ShowPage(Page.Controller))); footer.Add(DesignButton("Back to Briefing", "back", () => ShowPage(Page.Brief))); footer.Add(start); s.Add(footer); return s;
        }

        private VisualElement DesignCatalog()
        {
            var missions = LocalStore.Profile.missions; var selected = _mission ?? missions[0];
            var s = DesignPage("Training Catalog", "Browse local training modules, develop new skills, and build your professional flight workflow.", "Professional Training");
            var main = Element("design-column"); var search = new TextField("Search training") { value = _designCatalogSearch }; search.AddToClassList("product-field"); main.Add(search);
            var table = DesignPanel("Training Modules (" + missions.Count + ")"); var header = Element("design-table-row", "design-table-header"); header.Add(Text("TRAINING MODULE", "design-copy")); header.Add(Text("TARGETS", "design-table-value")); header.Add(Text("DURATION", "design-table-value")); table.Add(header);
            void Populate()
            {
                foreach (var old in table.Children().Where(e => e.ClassListContains("design-mission-row")).ToArray()) old.RemoveFromHierarchy();
                foreach (var m in missions.Where(m => m.title.IndexOf(_designCatalogSearch, StringComparison.OrdinalIgnoreCase) >= 0))
                { var row = new Button(() => { _mission = m; ShowPage(Page.Catalog); }); row.AddToClassList("design-table-row"); row.AddToClassList("design-mission-row"); if (m.id == selected.id) row.AddToClassList("design-selected"); row.Add(DesignImage(DesignSceneArt(m.environment), "design-table-thumb")); row.Add(DesignCopy(m.title, m.environment)); row.Add(Text(m.targets.Count.ToString(), "design-table-value")); row.Add(Text(Duration(m.timeLimit), "design-table-value")); table.Add(row); }
                if (!missions.Any(m => m.title.IndexOf(_designCatalogSearch, StringComparison.OrdinalIgnoreCase) >= 0)) { var empty = Text("No matching missions. Clear the search to view all training modules.", "muted", "wrap", "design-mission-row"); table.Add(empty); }
            }
            search.RegisterValueChangedCallback(e => { _designCatalogSearch = e.newValue; Populate(); }); Populate(); main.Add(table);
            var detail = DesignPanel(selected.title, selected.environment, "design-side"); detail.Add(DesignImage(DesignSceneArt(selected.environment), "design-detail-image")); detail.Add(DesignButton("Start Training", "play", () => { _mission = selected; ShowPage(Page.Brief); }, true)); detail.Add(Text("Learning Objectives", "panel-title")); foreach (var target in selected.targetNames) detail.Add(Text("• " + target, "event-row", "wrap")); detail.Add(DesignButton("Edit Mission", "edit", () => { _mission = selected; ShowPage(Page.MissionEditor); })); s.Add(DesignColumns(main, detail));
            s.Add(DesignButton("Create Mission", "edit", () => { _mission = new MissionDefinition { id = Guid.NewGuid().ToString("N"), title = "New Inspection" }; ShowPage(Page.MissionEditor); })); return s;
        }

        private VisualElement DesignMissionEditor()
        {
            _mission ??= LocalStore.Profile.missions[0]; var m = _mission; _designTarget = Mathf.Clamp(_designTarget, 0, m.targets.Count - 1);
            var s = DesignPage("Mission Editor · " + m.title, "Configure named inspection targets. Coordinates are relative to the simulator home pad.", "Professional Training");
            var library = DesignPanel("Object Library", "Select an inspection target to edit it.", "design-rail");
            for (int i = 0; i < m.targets.Count; i++)
            { int index = i; while (m.targetNames.Count <= i) m.targetNames.Add("Target " + (i + 1)); var b = DesignButton("P" + (i + 1) + " · " + m.targetNames[i], "target", () => { _designTarget = index; ShowPage(Page.MissionEditor); }); if (i == _designTarget) b.AddToClassList("design-selected"); library.Add(b); }
            library.Add(DesignButton("Add Target", "target", () => { if (m.targets.Count >= 12) { ShowToast("Maximum 12 targets per mission."); return; } m.targets.Add(new Vector3(0, 15, 35 + m.targets.Count * 8)); m.targetNames.Add("Target " + m.targets.Count); _designTarget = m.targets.Count - 1; ShowPage(Page.MissionEditor); }));
            library.Add(Text("Start points, restricted volumes and scripted events are not editable in this build.", "muted", "wrap"));
            var map = Element("design-column"); map.Add(new DesignMissionMap(m, _designTarget)); map.Add(Text("SCHEMATIC LOCAL COORDINATES · NOT A REAL MAP", "page-eyebrow"));
            var properties = DesignPanel("Inspection Target P" + (_designTarget + 1), null, "design-side"); int selected = _designTarget;
            Field(properties, "Name", m.targetNames[selected], v => m.targetNames[selected] = v);
            Number(properties, "X / East (m)", m.targets[selected].x, v => { var a = m.targets[selected]; a.x = Mathf.Clamp(v, -200, 200); m.targets[selected] = a; map.MarkDirtyRepaint(); });
            Number(properties, "Y / Altitude (m)", m.targets[selected].y, v => { var a = m.targets[selected]; a.y = Mathf.Clamp(v, 2, 100); m.targets[selected] = a; });
            Number(properties, "Z / North (m)", m.targets[selected].z, v => { var a = m.targets[selected]; a.z = Mathf.Clamp(v, -200, 200); m.targets[selected] = a; map.MarkDirtyRepaint(); });
            properties.Add(ActionButton("Remove Target", () => { if (m.targets.Count <= 1) { ShowToast("Keep at least one capture target."); return; } m.targets.RemoveAt(selected); m.targetNames.RemoveAt(selected); _designTarget = 0; ShowPage(Page.MissionEditor); }));
            properties.Add(Text("Mission Parameters", "panel-title")); Field(properties, "Mission title", m.title, v => m.title = v);
            Choice(properties, "Environment", new[] { "City Park", "Pacific Coast", "Desert Range", "Warehouse" }, m.environment, v => m.environment = v);
            Number(properties, "RTH altitude (m)", m.rthAltitude, v => m.rthAltitude = Mathf.Clamp(v, 10, 100)); Number(properties, "Wind (mph)", m.windMph, v => m.windMph = Mathf.Clamp(v, 0, 25));
            Number(properties, "Time limit (seconds)", m.timeLimit, v => m.timeLimit = Mathf.Clamp(v, 30, 1800)); Number(properties, "Minimum safe distance (m)", m.safeDistance, v => m.safeDistance = Mathf.Clamp(v, 1, 20)); s.Add(DesignColumns(library, map, properties));
            var footer = Element("design-footer"); footer.Add(Text(m.targets.Count + " ordered targets · Local draft", "muted"));
            footer.Add(DesignButton("Preview Brief", "play", () => ShowPage(Page.Brief))); footer.Add(DesignButton("Save Mission", "save", () => {
                if (string.IsNullOrWhiteSpace(m.title)) { ShowToast("Enter a mission title."); return; }
                var existing = LocalStore.Profile.missions.FindIndex(x => x.id == m.id); if (existing < 0) LocalStore.Profile.missions.Add(m); else LocalStore.Profile.missions[existing] = m; LocalStore.Save(); ShowToast("Mission saved.");
            }, true)); s.Add(footer); return s;
        }

        private VisualElement DesignInstructor()
        {
            var s = DesignPage("Instructor Review", "Review local sessions and assign practice. Remote classrooms and live video are not connected.", "Professional Training");
            var records = LocalStore.Sessions(); var grid = Element("design-instructor-grid", "design-column");
            foreach (var record in records.Take(4))
            {
                var card = DesignPanel(record.title, "RECORDED SESSION · " + LocalDate(record.startedUtc)); card.Add(DesignImage(DesignSceneArt(record.environment), "design-instructor-image"));
                card.Add(Row(Text(record.score + " / 100", "design-item-title"), Text(Duration(record.duration), "muted"))); card.Add(Row(DesignButton("Review Report", "chart", () => { _record = record; ShowPage(Page.Report); }), DesignButton("Replay Flight", "play", () => BeginReplay(record)))); grid.Add(card);
            }
            if (records.Count == 0) { var empty = DesignPanel("No recorded sessions", "Complete a training flight to review its telemetry, events and captures."); empty.Add(DesignButton("Start Practice", "play", () => ShowPage(Page.Catalog), true)); grid.Add(empty); }
            var assignments = DesignPanel("Assign Practice", "Assignments are stored on this device.", "design-side");
            string selected = CourseCatalog.All[0].Title; Choice(assignments, "Lesson", CourseCatalog.All.Select(c => c.Title), selected, v => selected = v);
            assignments.Add(DesignButton("Assign Lesson", "book", () => { var course = CourseCatalog.All.Find(c => c.Title == selected); if (!LocalStore.Profile.assignedCourses.Contains(course.Id)) LocalStore.Profile.assignedCourses.Add(course.Id); LocalStore.Save(); ShowPage(Page.Instructor); }, true));
            foreach (string id in LocalStore.Profile.assignedCourses.ToArray())
            { var course = CourseCatalog.Find(id); var item = DesignPanel(course.Title, course.Category); item.Add(ActionButton("Start Practice", () => OpenCourse(course))); item.Add(ActionButton("Remove Assignment", () => { LocalStore.Profile.assignedCourses.Remove(id); LocalStore.Save(); ShowPage(Page.Instructor); })); assignments.Add(item); }
            s.Add(DesignColumns(grid, assignments)); s.Add(Text("Recorded evidence only · No remote control or live learner count is represented.", "muted", "wrap")); return s;
        }

        private VisualElement DesignReadiness()
        {
            var s = DesignPage("U.S. Flight Readiness", "Learn the key steps, use trusted resources, and understand where simulation training ends.", "Education Center");
            var content = Element("design-column"); var tabs = Element("design-tabs"); string[] names = { "Recreational", "Part 107", "Visiting the U.S." };
            foreach (string tab in names) { var b = ActionButton(tab, () => { _designReadinessTab = tab; ShowPage(Page.Readiness); }); if (tab == _designReadinessTab) b.AddToClassList("design-selected"); tabs.Add(b); } content.Add(tabs);
            string[] titles = _designReadinessTab == "Recreational" ? new[] { "Take TRUST", "Check Registration", "Understand Remote ID", "Check Airspace", "Review Local Rules" } : _designReadinessTab == "Part 107" ? new[] { "Learn About Part 107", "Review Certification Requirements", "Check Airspace", "Review Operating Requirements" } : new[] { "Review FAA Visitor Guidance", "Check Aircraft Requirements", "Check Airspace", "Review Local Rules" };
            string[] descriptions = { "Read the FAA’s current guidance for your purpose of flight.", "Confirm the requirements that apply to your aircraft and situation.", "Use authoritative sources. Simulator completion is not a certificate.", "Review current restrictions and any authorization you may need.", "Check state, local, land-manager and site rules before flying outdoors." };
            string official = _designReadinessTab == "Part 107" ? "https://www.faa.gov/uas/commercial_operators" : _designReadinessTab == "Visiting the U.S." ? "https://www.faa.gov/uas/resources/foreign_operators" : "https://www.faa.gov/uas/recreational_flyers";
            for (int i = 0; i < titles.Length; i++)
            {
                int index = i; var row = Element("design-readiness-row"); row.Add(DesignImage(i == 0 ? "Art/DesignV1/CoastHeroV1" : "Art/Readiness", "design-readiness-thumb")); row.Add(Text((i + 1).ToString(), "design-number")); row.Add(DesignCopy(titles[i], descriptions[Math.Min(i, descriptions.Length - 1)]));
                row.Add(DesignButton(i == 3 ? "Open FAA Resources" : "Learn", "flight", () => Application.OpenURL(index == 3 ? "https://www.faa.gov/uas" : official))); content.Add(row);
            }
            var side = DesignPanel("Before You Fly Outdoors", "Use current information, not simulated map conditions.", "design-side"); side.Insert(0, DesignImage("Art/Readiness", "design-detail-image"));
            side.Add(DesignStatus("Know the Rules", "TRUST, registration and Remote ID as applicable", "book")); side.Add(DesignStatus("Check Your Location", "Airspace, site access and local restrictions", "map")); side.Add(DesignStatus("Fly Safely", "Weather, people and other aircraft", "shield"));
            side.Add(Text("AeroSim training is not flight authorization. The app cannot verify your legal eligibility, issue certificates or approve real-world flight.", "muted", "wrap"));
            side.Add(DesignButton("FAA Drone Resources", "flight", () => Application.OpenURL("https://www.faa.gov/uas"), true)); side.Add(DesignButton("Continue Safety Course", "play", () => OpenCourse(CourseCatalog.Find("s02")))); s.Add(DesignColumns(content, side)); return s;
        }

        private VisualElement DesignResults()
        {
            if (_record == null) return Records(); var r = _record;
            var s = new ScrollView(); s.AddToClassList("product-scroll"); s.AddToClassList("design-page"); s.AddToClassList("design-results-page"); SetBackground(s, DesignSceneArt(r.environment));
            var panel = DesignPanel(r.passed ? "Lesson Complete" : "Practice Again", r.title, "design-results-panel");
            panel.Q<Label>(className: "panel-title").AddToClassList("design-results-title");
            s.contentContainer.AddToClassList("design-results-content");
            var heading = Element("design-results-heading"); heading.Add(DesignIcon(r.passed ? "check" : "target", 56)); heading.Add(DesignCopy(r.passed ? "Objective achieved" : "Objective not yet achieved", r.feedback)); heading.Add(Stat(r.score.ToString(), "Overall score / 100")); panel.Add(heading);
            var stats = Element("design-result-stats"); stats.Add(DesignResultStat("clock", Duration(r.duration), "Flight Time")); stats.Add(DesignResultStat("flight", Length(r.maxAltitude), "Maximum Altitude")); stats.Add(DesignResultStat("target", r.photos.ToString(), "Captures")); stats.Add(DesignResultStat("shield", r.collisions.ToString(), "Collisions")); panel.Add(stats);
            panel.Add(Text("Flight Events", "panel-title")); var events = Element("design-event-timeline");
            foreach (var e in r.events.Take(5)) { var item = Element("design-event-point"); item.Add(DesignIcon(e.kind == "warning" || e.kind == "collision" ? "shield" : "check", 28)); item.Add(Text(e.message, "wrap")); item.Add(Text(Duration(e.time), "muted")); events.Add(item); } panel.Add(events);
            if (r.events.Count == 0) panel.Add(Text("No events recorded for this session.", "muted"));
            var feedback = DesignPanel("Try Next", r.passed ? "Review the replay, then continue practicing smooth control and safe clearance." : r.feedback); panel.Add(feedback);
            panel.Add(Text("Score reflects objective completion and collision penalties. Image quality, reaction time and formal safety certification are not evaluated.", "muted", "wrap"));
            panel.Add(Row(DesignButton("Back Home", "home", () => ShowPage(Page.Home), true), DesignButton("Retry Session", "back", () => StartFlight(r.config ?? new FlightConfig { title = r.title, environment = r.environment, workspace = r.workspace, mode = r.mode })), DesignButton("Replay Flight", "play", () => BeginReplay(r)), DesignButton("Training Report", "chart", () => ShowPage(Page.Report)))); s.Add(panel); return s;
        }
        private static VisualElement DesignResultStat(string icon, string value, string title)
        { var card = Element("design-result-stat"); card.Add(DesignIcon(icon, 32)); card.Add(DesignCopy(value, title)); return card; }

        private VisualElement DesignReport()
        {
            if (_record == null) return Records(); var r = _record;
            var s = DesignPage("Mission Report · " + r.title, "Detailed results, performance metrics, and event evidence for this local training session.", "Professional Training");
            var summary = DesignPanel("Performance Summary", (r.passed ? "Passed" : "Practice Again") + " · " + r.score + " / 100", "design-rail"); summary.Add(Text(r.feedback, "wrap", "muted"));
            summary.Add(Stat(r.score.ToString(), "Overall score")); summary.Add(Text("Each collision subtracts 8 points. The app does not separately grade photographic image quality, formal certification or safety eligibility.", "muted", "wrap"));
            var route = DesignPanel("Flight Route & Events", "Recorded X/Z path relative to the home point.", "design-column"); route.Add(new DesignRecordMap(r)); route.Add(Text("SCHEMATIC TRAINING PATH — NOT FOR NAVIGATION", "page-eyebrow"));
            var metrics = DesignPanel("Mission Summary", null, "design-side"); metrics.Add(DesignResultStat("target", r.photos.ToString(), "Targets / captures")); metrics.Add(DesignResultStat("shield", r.collisions.ToString(), "Collisions")); metrics.Add(DesignResultStat("clock", Duration(r.duration), "Duration")); metrics.Add(DesignResultStat("flight", Length(r.distance), "Distance")); s.Add(DesignColumns(summary, route, metrics));
            var evidence = DesignPanel("Photo Evidence", "Actual simulator captures. Image quality is not evaluated; photographs do not certify inspection competence.");
            var evidenceGrid = Element("design-evidence-grid");
            foreach (var photo in (r.photoEvidence ?? new List<PhotoEvidence>()).Take(6))
            {
                var card = DesignPanel("Capture " + photo.sequence, Duration(photo.time) + " · " + (photo.hasTarget ? photo.targetName : "Free-flight photograph"), "design-evidence-card");
                string path = EvidencePaths.ResolvePhoto(r, photo);
                if (path != null)
                {
                    Texture2D texture = null;
                    try
                    {
                        texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                        if (texture.LoadImage(File.ReadAllBytes(path)))
                        {
                            _designEvidenceTextures.Add(texture);
                            var thumbnail = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit }; thumbnail.AddToClassList("design-evidence-image");
                            var capturedTexture = texture;
                            thumbnail.RegisterCallback<DetachFromPanelEvent>(_ => { if (_designEvidenceTextures.Remove(capturedTexture)) Destroy(capturedTexture); });
                            card.Add(thumbnail);
                            string capturedPath = path;
                            card.Add(DesignButton("Open Photograph", "camera", () => Application.OpenURL(new Uri(capturedPath).AbsoluteUri)));
                        }
                        else { Destroy(texture); card.Add(Text("Saved image could not be decoded.", "muted", "wrap")); }
                    }
                    catch (Exception)
                    {
                        if (texture != null && !_designEvidenceTextures.Contains(texture)) Destroy(texture);
                        card.Add(Text("Saved image is unavailable for preview.", "muted", "wrap"));
                    }
                }
                else card.Add(Text(photo.status == "saved" ? "Saved image file missing or invalid." : photo.status == "failed" ? "Capture failed: " + photo.failureReason : "Capture pending.", "muted", "wrap"));
                card.Add(Text("Image quality: not evaluated", "muted"));
                if (r.frames != null && r.frames.Count > 1)
                { float capturedTime = photo.time; card.Add(DesignButton("Replay at Capture", "play", () => { BeginReplay(r); _sim.Seek(capturedTime); })); }
                evidenceGrid.Add(card);
            }
            if (r.photoEvidence == null || r.photoEvidence.Count == 0) evidence.Add(Text("No photograph evidence was recorded for this session. Legacy records do not contain fabricated captures.", "muted", "wrap"));
            else { evidence.Add(evidenceGrid); if (r.photoEvidence.Count > 6) evidence.Add(Text("Showing the first 6 captures. Export the printable report for the complete evidence list.", "muted", "wrap")); }
            s.Add(evidence);
            var log = DesignPanel("Event Log", "Click Replay Evidence to review the original session.", "design-column");
            foreach (var e in r.events) { var row = Element("design-table-row"); row.Add(Text(Duration(e.time), "design-table-value")); row.Add(Text(e.kind.ToUpperInvariant(), "design-table-value")); row.Add(Text(e.message, "design-copy", "wrap")); log.Add(row); }
            if (r.events.Count == 0) log.Add(Text("No events recorded.", "muted"));
            var feedback = DesignPanel("Instructor Feedback", "Your notes stay with this local session.", "design-column"); var notes = new TextField("Instructor notes") { value = r.instructorNote, multiline = true }; notes.AddToClassList("product-field"); notes.AddToClassList("design-notes-field"); notes.RegisterValueChangedCallback(e => r.instructorNote = e.newValue); feedback.Add(notes);
            feedback.Add(DesignButton("Save Notes", "save", () => SaveReportNotes(r))); s.Add(DesignColumns(log, feedback));
            var footer = Element("design-footer"); footer.Add(Text("Training record only. Review personal information before sharing an export.", "muted", "wrap")); footer.Add(DesignButton("Export CSV", "chart", () => ExportCsv(r))); footer.Add(DesignButton("Replay Evidence", "play", () => BeginReplay(r))); footer.Add(DesignButton("Export Printable Report", "save", () => ExportPrintableReport(r), true)); s.Add(footer); return s;
        }

        // A vertex-colour gradient remains editable native UI, scales without bitmap blur,
        // and avoids a hard rectangular edge behind the learning hero's text.
        private sealed class DesignHeroGradient : VisualElement
        {
            public DesignHeroGradient() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            private void Draw(MeshGenerationContext context)
            {
                const int segments = 64;
                var rect = contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                var mesh = context.Allocate((segments + 1) * 2, segments * 6);
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float alpha = Mathf.Lerp(.88f, .02f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.10f, .88f, t)));
                    var tint = new Color(2 / 255f, 15 / 255f, 26 / 255f, alpha);
                    mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin + rect.width * t, rect.yMin, Vertex.nearZ), tint = tint, uv = Vector2.zero });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin + rect.width * t, rect.yMax, Vertex.nearZ), tint = tint, uv = Vector2.zero });
                }
                for (int i = 0; i < segments; i++)
                {
                    ushort a = (ushort)(i * 2), b = (ushort)(i * 2 + 1), c = (ushort)(i * 2 + 2), d = (ushort)(i * 2 + 3);
                    mesh.SetNextIndex(a); mesh.SetNextIndex(b); mesh.SetNextIndex(c);
                    mesh.SetNextIndex(c); mesh.SetNextIndex(b); mesh.SetNextIndex(d);
                }
            }
        }

        private sealed class DesignInputBar : ProgressBar
        {
            private readonly Color _channelColor;
            public DesignInputBar(int channel)
            {
                _channelColor = channel == 1 ? new Color(.02f, .86f, .69f) : channel == 3 ? new Color(.57f, .44f, 1) : new Color(.05f, .7f, 1);
                AddToClassList("design-controller-axis-bar");
                foreach (var child in Children()) child.style.display = DisplayStyle.None;
                RegisterCallback<ChangeEvent<float>>(_ => MarkDirtyRepaint());
                generateVisualContent += Draw;
            }
            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D; var r = contentRect;
                float top = (r.height - 18) / 2, center = r.width * .5f, current = Mathf.InverseLerp(-1, 1, value) * r.width;
                void Rect(float x, float y, float w, float h, Color color)
                { if (w <= 0 || h <= 0) return; p.fillColor = color; p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y)); p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h)); p.ClosePath(); p.Fill(); }
                Rect(0, top, r.width, 18, new Color(.15f, .22f, .30f));
                Rect(Mathf.Min(center, current), top, Mathf.Max(2, Mathf.Abs(current - center)), 18, _channelColor);
                Rect(center - 1, top - 5, 2, 28, new Color(.87f, .94f, 1));
            }
        }

        private sealed class DesignStickGuidance : VisualElement
        {
            public DesignStickGuidance()
            {
                AddToClassList("design-stick-guidance"); pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                schedule.Execute(MarkDirtyRepaint).Every(33);
            }
            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D; var r = contentRect; var input = FlightInput.Raw();
                const float aspect = 1671f / 941f;
                float width = Mathf.Min(r.width, r.height * aspect), height = width / aspect;
                float left = (r.width - width) / 2, top = (r.height - height) / 2;
                float radius = Mathf.Clamp(width * .09f, 30, 80);
                p.lineWidth = 1.5f; p.strokeColor = new Color(.08f, .91f, 1, .85f);
                void Line(Vector2 a, Vector2 b) { p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
                void Stick(float x, float horizontal, float vertical)
                {
                    Vector2 center = new Vector2(left + width * x, top + height * .49f);
                    p.BeginPath(); for (int i = 0; i <= 48; i++) { float a = i * Mathf.PI / 24; var pos = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius; if (i == 0) p.MoveTo(pos); else p.LineTo(pos); } p.Stroke();
                    foreach (var direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                    { var end = center + direction * (radius + 12); Line(center + direction * (radius * .63f), end); var tangent = new Vector2(-direction.y, direction.x); Line(end, end - direction * 7 + tangent * 5); Line(end, end - direction * 7 - tangent * 5); }
                    var live = center + new Vector2(horizontal, -vertical) * (radius * .5f);
                    p.fillColor = new Color(.03f, .91f, 1); p.BeginPath(); p.MoveTo(live + Vector2.up * 5); p.LineTo(live + Vector2.right * 5); p.LineTo(live + Vector2.down * 5); p.LineTo(live + Vector2.left * 5); p.ClosePath(); p.Fill();
                }
                Stick(.29f, input.y, input.x); Stick(.71f, input.w, input.z);
            }
        }

        private sealed class DesignLineIcon : VisualElement
        {
            private readonly string _name;
            public DesignLineIcon(string name, float size)
            { _name = name; AddToClassList("design-line-icon"); style.width = size; style.height = size; style.minWidth = size; style.minHeight = size; pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D; float sx = contentRect.width / 24f, sy = contentRect.height / 24f;
                p.strokeColor = resolvedStyle.color; p.fillColor = resolvedStyle.color; p.lineWidth = Mathf.Max(1.5f, sx * 1.75f);
                Vector2 V(float x, float y) => new Vector2(x * sx, y * sy);
                void Path(params float[] points) { p.BeginPath(); p.MoveTo(V(points[0], points[1])); for (int i = 2; i < points.Length; i += 2) p.LineTo(V(points[i], points[i + 1])); p.Stroke(); }
                void Circle(float x, float y, float r) { p.BeginPath(); for (int i = 0; i <= 32; i++) { float a = i * Mathf.PI / 16; var point = V(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r); if (i == 0) p.MoveTo(point); else p.LineTo(point); } p.Stroke(); }
                switch (_name)
                {
                    case "logo": p.strokeColor = new Color(.03f, .77f, 1); p.fillColor = p.strokeColor; p.BeginPath(); p.MoveTo(V(12,1)); p.LineTo(V(23,22)); p.LineTo(V(2,22)); p.LineTo(V(7,13)); p.LineTo(V(13,13)); p.LineTo(V(10,18)); p.LineTo(V(17,18)); p.LineTo(V(12,8)); p.LineTo(V(9,12)); p.LineTo(V(6,12)); p.ClosePath(); p.Fill(); break;
                    case "home": Path(2,11,12,3,22,11); Path(5,9,5,21,10,21,10,14,14,14,14,21,19,21,19,9); break;
                    case "book": Path(12,5,12,21); Path(12,6,8,3,2,3,2,19,8,19,12,21,16,19,22,19,22,3,16,3,12,6); break;
                    case "flight": Path(2,10,22,2,14,22,10,14,2,10); Path(10,14,22,2); break;
                    case "trophy": Path(6,3,18,3,18,9,16,14,12,16,8,14,6,9,6,3); Path(6,5,2,5,2,9,6,11); Path(18,5,22,5,22,9,18,11); Path(12,16,12,21); Path(7,21,17,21); break;
                    case "drone": Circle(5,5,3); Circle(19,5,3); Circle(5,19,3); Circle(19,19,3); Path(6,6,18,18); Path(6,18,18,6); Circle(12,12,3); break;
                    case "chart": Path(4,20,4,14); Path(10,20,10,10); Path(16,20,16,5); Path(22,20,22,2); break;
                    case "controller": Path(6,6,18,6,21,9,23,18,20,20,15,15,9,15,4,20,1,18,3,9,6,6); Path(5,9,5,13); Path(3,11,7,11); Circle(17,10,.7f); Circle(19,12,.7f); break;
                    case "camera": Path(2,7,7,7,9,4,15,4,17,7,22,7,22,21,2,21,2,7); Circle(12,13,4); break;
                    case "map": Path(2,5,8,2,16,5,22,2,22,19,16,22,8,19,2,22,2,5); Path(8,2,8,19); Path(16,5,16,22); break;
                    case "shield": Path(12,2,21,6,20,15,16,20,12,23,8,20,4,15,3,6,12,2); Path(8,12,11,15,16,9); break;
                    case "clock": Circle(12,12,10); Path(12,5,12,12,17,15); break;
                    case "target": Circle(12,12,8); Circle(12,12,3); Path(12,1,12,5); Path(12,19,12,23); Path(1,12,5,12); Path(19,12,23,12); break;
                    case "check": Circle(12,12,10); Path(6,12,10,16,18,8); break;
                    case "play": Path(7,3,21,12,7,21,7,3); break;
                    case "back": Path(12,4,4,12,12,20); Path(4,12,22,12); break;
                    case "save": Path(3,3,18,3,22,7,22,22,3,22,3,3); Path(7,3,7,9,17,9,17,3); Path(7,22,7,14,18,14,18,22); break;
                    case "users": Circle(12,7,3); Circle(4,9,2); Circle(20,9,2); Path(7,21,7,17,9,14,15,14,17,17,17,21); Path(1,20,1,16,4,13,6,14); Path(23,20,23,16,20,13,18,14); break;
                    case "edit": Path(3,17,17,3,21,7,7,21,3,21,3,17); Path(14,6,18,10); break;
                    case "battery": Path(2,6,20,6,20,19,2,19,2,6); Path(20,10,23,10,23,15,20,15); Path(6,10,16,10); Path(6,15,16,15); break;
                    case "pause": Path(8,4,8,20); Path(16,4,16,20); break;
                    case "settings": Circle(12,12,4); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; Path(12 + Mathf.Cos(a) * 7,12 + Mathf.Sin(a) * 7,12 + Mathf.Cos(a) * 10,12 + Mathf.Sin(a) * 10); } Circle(12,12,8); break;
                    default: Circle(12,12,9); Path(12,7,12,13); Circle(12,17,.5f); break;
                }
            }
        }

        private sealed class DesignMissionMap : VisualElement
        {
            private readonly MissionDefinition _mission; private readonly int _selected;
            public DesignMissionMap(MissionDefinition mission, int selected = -1) { _mission = mission; _selected = selected; AddToClassList("design-mission-map"); generateVisualContent += Draw; schedule.Execute(MarkDirtyRepaint).Every(200); }
            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D; float w = contentRect.width, h = contentRect.height;
                if (w <= 0 || h <= 0) return; p.lineWidth = 1; p.strokeColor = new Color(.12f,.24f,.33f);
                for (int i = 1; i < 10; i++) { p.BeginPath(); p.MoveTo(new Vector2(w*i/10,0)); p.LineTo(new Vector2(w*i/10,h)); p.MoveTo(new Vector2(0,h*i/10)); p.LineTo(new Vector2(w,h*i/10)); p.Stroke(); }
                float limit = Mathf.Max(50, _mission.targets.SelectMany(v => new[] { Mathf.Abs(v.x), Mathf.Abs(v.z) }).DefaultIfEmpty(50).Max()) * 1.25f;
                Vector2 Map(Vector3 v) => new(w/2 + v.x/limit*w*.42f, h*.82f-v.z/limit*h*.65f);
                p.lineWidth = 3; p.strokeColor = new Color(.04f,.58f,1); p.BeginPath(); p.MoveTo(Map(Vector3.zero)); foreach (var v in _mission.targets) p.LineTo(Map(v)); p.Stroke();
                for (int i = -1; i < _mission.targets.Count; i++) { Vector2 center = Map(i < 0 ? Vector3.zero : _mission.targets[i]); p.lineWidth = i == _selected ? 4 : 2; p.strokeColor = i == _selected ? new Color(1,.7f,.18f) : new Color(.1f,.85f,.8f); p.BeginPath(); for (int k = 0; k <= 24; k++) { float a=k*Mathf.PI/12; var v=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*9; if(k==0)p.MoveTo(v);else p.LineTo(v); } p.Stroke(); }
            }
        }
        private sealed class DesignRecordMap : VisualElement
        {
            private readonly SessionRecord _record;
            public DesignRecordMap(SessionRecord record) { _record = record; AddToClassList("design-mission-map"); generateVisualContent += Draw; }
            private void Draw(MeshGenerationContext context)
            {
                var frames = _record.frames; if (frames.Count < 2) return; var p = context.painter2D; float w=contentRect.width,h=contentRect.height;
                float minX=frames.Min(f=>f.position.x),maxX=frames.Max(f=>f.position.x),minZ=frames.Min(f=>f.position.z),maxZ=frames.Max(f=>f.position.z);
                p.lineWidth=3;p.strokeColor=new Color(.08f,.83f,.7f);p.BeginPath();
                for(int i=0;i<frames.Count;i++){var point=new Vector2(24+(frames[i].position.x-minX)/Mathf.Max(10,maxX-minX)*(w-48),h-24-(frames[i].position.z-minZ)/Mathf.Max(10,maxZ-minZ)*(h-48));if(i==0)p.MoveTo(point);else p.LineTo(point);}p.Stroke();
            }
        }
    }
}
