using System;
using System.Collections;
using AeroSim.Input;
using AeroSim.Navigation;
using AeroSim.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace AeroSim
{
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class AeroSimApplication : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _workspaceMenu;
        private VisualElement _mainContent;
        private Label _workspaceLabel;
        private Label _controllerDevice;
        private Label _toastLabel;
        private Coroutine _toastRoutine;
        private InputCapabilityService _inputCapabilities;
        private bool _hadPhysicalController;
        private TrainingWorkspace _workspace = TrainingWorkspace.Aerial;

        private static readonly Color Blue = new(0.035f, 0.514f, 1f);

        private void OnEnable()
        {
            LocalStore.Initialize();
            _workspace = LocalStore.Profile.workspace == "FPV" ? TrainingWorkspace.Fpv : LocalStore.Profile.workspace == "Professional" ? TrainingWorkspace.Professional : TrainingWorkspace.Aerial;
            _document = GetComponent<UIDocument>();
            _root = _document.rootVisualElement;
            _root.name = "aerosim-root";

            var styleSheet = Resources.Load<StyleSheet>("UI/AeroSim");
            _page = LocalStore.Profile.onboarded ? Page.Home : Page.Onboarding;
            BuildInterface();
            if (styleSheet != null)
            {
                _root.styleSheets.Add(styleSheet);
                Debug.Log("AeroSim UI stylesheet loaded successfully.");
            }
            else
            {
                Debug.LogError("AeroSim UI stylesheet could not be loaded from Resources/UI/AeroSim.");
            }
            var productStyle = Resources.Load<StyleSheet>("UI/Product");
            if (productStyle != null) _root.styleSheets.Add(productStyle);
            var designFont = Resources.Load<Font>("UI/Fonts/Inter-Regular");
            if (designFont != null) _root.style.unityFont = designFont;
            var responsiveStyle = Resources.Load<StyleSheet>("UI/Responsive");
            if (responsiveStyle != null) _root.styleSheets.Add(responsiveStyle);
            ApplySettings();
            _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            _inputCapabilities = new InputCapabilityService();
            _inputCapabilities.CapabilitiesChanged += OnInputCapabilitiesChanged;
            _inputCapabilities.Start();
            _hadPhysicalController = _inputCapabilities.HasGamepad || _inputCapabilities.HasJoystick;
            RefreshControllerStatus();
        }

        private void OnDisable()
        {
            CancelTypography();
            ReleaseDesignFlightInputGuard();
            ReleaseDesignEvidenceTextures();
            ReleaseDesignReplayView();
            ReleaseHangarPreviews();
            if (_root != null)
            {
                _root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }

            _inputCapabilities?.Dispose();
        }

        private void BuildInterface()
        {
            ReleaseDesignEvidenceTextures();
            _root.Clear();
            _root.AddToClassList("app-root");
            _root.AddToClassList("design-v1");
            _root.EnableInClassList("design-fpv-home", _workspace == TrainingWorkspace.Fpv && _page == Page.Home);
            _root.EnableInClassList("design-onboarding", _page == Page.Onboarding);
            _root.EnableInClassList("design-controller-mode", _page == Page.Controller);

            _root.Add(BuildTopBar());

            var appBody = Element("app-body");
            appBody.Add(BuildNavigation());

            _mainContent = Element("main-content");
            _mainContent.Add(BuildPage(_page));
            var stage = Element("content-stage");
            stage.Add(_mainContent);
            appBody.Add(stage);
            _root.Add(appBody);

            _workspaceMenu = BuildWorkspaceMenu();
            _root.Add(_workspaceMenu);

            InstallToast();
            OnGeometryChanged(null);
        }

        private VisualElement BuildTopBar()
        {
            var bar = Element("top-bar");

            var brandSwitch = new Button(ToggleWorkspaceMenu);
            brandSwitch.AddToClassList("design-brand-switch");
            brandSwitch.name = "action-switch-training-workspace";
            brandSwitch.tooltip = "Switch training workspace";
            brandSwitch.Add(DesignBrand());
            bar.Add(brandSwitch);
            var workspace = new Button(ToggleWorkspaceMenu);
            workspace.AddToClassList("design-top-workspace");
            workspace.Add(DesignIcon(_workspace == TrainingWorkspace.Fpv ? "drone" : _workspace == TrainingWorkspace.Professional ? "book" : "flight"));
            workspace.Add(Text(WorkspaceName + " Training", "workspace-label"));
            workspace.Add(Text("⌄", "workspace-chevron"));
            bar.Add(workspace);

            var statusStrip = Element("status-strip");
            statusStrip.Add(StatusPill("", "Local Training", true));
            statusStrip.Add(StatusPill("", "Offline · No aircraft connected"));
            bar.Add(statusStrip);

            var settings = IconButton("SET", "Settings", () => ShowPage(Page.Settings));
            settings.AddToClassList("top-settings");
            bar.Add(settings);
            return bar;
        }

        private VisualElement BuildNavigation()
        {
            var navigation = new ScrollView();
            navigation.AddToClassList("side-nav");
            navigation.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            navigation.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            var switcher = new Button(ToggleWorkspaceMenu);
            switcher.AddToClassList("workspace-switcher");
            var workspaceIcon = DesignIcon(_workspace == TrainingWorkspace.Fpv ? "drone" : "flight");
            workspaceIcon.AddToClassList("workspace-icon"); switcher.Add(workspaceIcon);
            _workspaceLabel = Text(WorkspaceName, "workspace-label");
            switcher.Add(_workspaceLabel);
            switcher.Add(Text("⌄", "workspace-chevron"));
            navigation.Add(switcher);

            navigation.Add(NavButton("H", "Home", AppDestination.Home, _page == Page.Home));
            navigation.Add(NavButton("A", _workspace == TrainingWorkspace.Professional ? "Training Catalog" : "Academy", AppDestination.Academy));
            navigation.Add(NavButton("F", "Free Flight", AppDestination.FreeFlight));
            navigation.Add(NavButton("C", "Challenges", AppDestination.Challenges));
            navigation.Add(NavButton("D", "Hangar", AppDestination.Hangar));
            if (_page == Page.Settings)
            {
                navigation.Add(NavButton("R", "Flight Records", AppDestination.FlightRecords));
                AddDesignSettingsNavigation(navigation);
            }
            if (_workspace == TrainingWorkspace.Fpv) { navigation.Add(RouteButton("T", "Tuning", Page.Tuning)); navigation.Add(RouteButton("O", "OSD Editor", Page.OsdEditor)); }
            if (_workspace == TrainingWorkspace.Professional) { navigation.Add(RouteButton("M", "Mission Editor", Page.MissionEditor)); navigation.Add(RouteButton("I", "Instructor", Page.Instructor)); }

            var spacer = Element("nav-spacer");
            navigation.Add(spacer);
            if (_page != Page.Settings) navigation.Add(NavButton("R", "Flight Records", AppDestination.FlightRecords));
            return navigation;
        }

        private ScrollView BuildAerialHome()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("home-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            var topRow = Element("top-row");
            topRow.Add(BuildHero());
            topRow.Add(BuildControllerCard());
            scroll.Add(topRow);

            var middleRow = Element("middle-row");
            middleRow.Add(BuildLearningPath());
            middleRow.Add(BuildReadinessCard());
            scroll.Add(middleRow);

            var promoRow = Element("promo-row");
            promoRow.Add(BuildPromoCard(
                "Art/Cinematic",
                "PRACTICE YOUR SKILLS",
                "Cinematic Challenge",
                "Apply what you’ve learned with a guided flight route and capture a cinematic coastal shot.",
                "promo-cinematic"));
            promoRow.Add(BuildPromoCard(
                "Art/Emergency",
                "BE PREPARED",
                "Emergency Practice",
                "Build confidence with real-world scenarios like low battery, signal loss, and obstacle avoidance.",
                "promo-emergency"));
            scroll.Add(promoRow);

            return scroll;
        }

        private VisualElement BuildHero()
        {
            var hero = Element("hero-card", "card");
            SetBackground(hero, "Art/DesignV1/CoastHeroV1");

            var shade = new DesignHeroGradient();
            shade.AddToClassList("hero-shade");
            hero.Add(shade);

            var copy = Element("hero-copy");
            copy.Add(Text("C O N T I N U E   L E A R N I N G", "eyebrow"));
            copy.Add(Text("Lesson 6 ·\nReturn-to-Home", "hero-title"));

            var metrics = Element("hero-metrics");
            metrics.Add(Text(LessonStatus("a10"), "metric"));
            metrics.Add(Text("4 min · Guided session", "metric"));
            copy.Add(metrics);
            copy.Add(Text("Learn how your aircraft automatically returns to a safe home point and how to use RTH confidently.", "hero-description"));

            var continueButton = PrimaryButton("Continue Training", () => OpenCourse(CourseCatalog.Find("a10")));
            copy.Add(continueButton);
            hero.Add(copy);
            return hero;
        }

        private VisualElement BuildControllerCard()
        {
            var card = Element("controller-card", "card");
            card.Add(Text("Controller", "card-title"));

            var status = Element("controller-status");
            var deviceIcon = Element("device-icon");
            deviceIcon.Add(DesignIcon("controller", 30));
            status.Add(deviceIcon);

            var labels = Element("controller-labels");
            labels.Add(Text("Available Input", "connected-label"));
            _controllerDevice = Text("Detecting input…", "controller-device");
            labels.Add(_controllerDevice);
            status.Add(labels);
            card.Add(status);

            var art = Element("controller-art");
            SetBackground(art, "Art/DesignV1/ControllerCutout");
            card.Add(art);

            var settings = new Button(() => ShowPage(Page.Controller));
            settings.AddToClassList("secondary-row-button");
            settings.Add(DesignIcon("settings"));
            settings.Add(Text("Controller Settings", "row-button-label"));
            settings.Add(Text("›", "row-chevron"));
            card.Add(settings);
            return card;
        }

        private VisualElement BuildLearningPath()
        {
            var card = Element("learning-card", "card");
            var heading = Element("section-heading");
            heading.Add(Text("Learning Path", "card-title"));
            heading.Add(Text("Build skills step by step to fly with confidence.", "section-subtitle"));
            card.Add(heading);

            var track = Element("progress-track");
            var completion = Element("progress-complete");
            completion.style.width = UnityEngine.UIElements.Length.Percent(LocalStore.Profile.courses.FindAll(c => c.bestScore >= 70).Count * 100f / CourseCatalog.All.Count);
            track.Add(completion);
            card.Add(track);

            var lessons = Element("lesson-grid");
            lessons.Add(BuildLesson("Art/Takeoff", "1", "1.  Takeoff", LessonStatus("a02"), "complete"));
            lessons.Add(BuildLesson("Art/Orientation", "2", "2.  Orientation", LessonStatus("a04"), "complete"));
            lessons.Add(BuildLesson("Art/ReturnHome", "3", "3.  Return-to-Home", LessonStatus("a10"), "active"));
            lessons.Add(BuildLesson("Art/Sunset", "4", "4.  Cinematic Moves", LessonStatus("c01"), "active"));
            card.Add(lessons);
            return card;
        }

        private VisualElement BuildLesson(string imagePath, string marker, string title, string status, string state)
        {
            var id = title.StartsWith("1") ? "a02" : title.StartsWith("2") ? "a04" : title.StartsWith("3") ? "a10" : "c01";
            var lesson = new Button(() => OpenCourse(CourseCatalog.Find(id)));
            lesson.AddToClassList("lesson");
            lesson.AddToClassList($"lesson-{state}");
            lesson.Add(Text(marker, "lesson-marker"));

            var image = Element("lesson-image");
            SetBackground(image, imagePath);
            lesson.Add(image);
            lesson.Add(Text(title, "lesson-title"));
            lesson.Add(Text(status, "lesson-status"));
            return lesson;
        }

        private VisualElement BuildReadinessCard()
        {
            var card = Element("readiness-card", "card");
            card.Add(Text("U.S. Flight Readiness", "card-title"));

            var image = Element("readiness-image");
            SetBackground(image, "Art/Readiness");
            card.Add(image);

            card.Add(Text("Review FAA resources", "readiness-title"));
            card.Add(Text("FAA rules, airspace, and safe flying in the United States.", "readiness-description"));

            var action = new Button(() => ShowPage(Page.Readiness));
            action.text = "Review resources  ›";
            action.AddToClassList("text-action");
            card.Add(action);
            return card;
        }

        private VisualElement BuildPromoCard(string imagePath, string eyebrow, string title, string body, string modifier)
        {
            var button = new Button(() => OpenCourse(CourseCatalog.Find(modifier == "promo-emergency" ? "s02" : "c01")));
            button.AddToClassList("promo-card");
            button.AddToClassList("card");
            button.AddToClassList(modifier);
            SetBackground(button, imagePath);

            var shade = Element("promo-shade");
            button.Add(shade);
            var copy = Element("promo-copy");
            copy.Add(Text(eyebrow, "promo-eyebrow"));
            copy.Add(Text(title, "promo-title"));
            copy.Add(Text(body, "promo-description"));
            button.Add(copy);
            button.Add(Text("›", "promo-arrow"));
            return button;
        }

        private VisualElement BuildWorkspaceMenu()
        {
            var menu = Element("workspace-menu", "card", "is-hidden");
            menu.Add(Text("TRAINING WORKSPACE", "menu-eyebrow"));
            menu.Add(WorkspaceOption("Aerial Training", "Camera flight, safety, and cinematic skills", TrainingWorkspace.Aerial));
            menu.Add(WorkspaceOption("FPV Racing", "Manual flight, tuning, gates, and race OSD", TrainingWorkspace.Fpv));
            menu.Add(WorkspaceOption("Professional", "Mission planning, inspections, and instruction", TrainingWorkspace.Professional));
            return menu;
        }

        private Button WorkspaceOption(string title, string description, TrainingWorkspace workspace)
        {
            var option = new Button(() => SelectWorkspace(workspace));
            option.AddToClassList("workspace-option");
            option.Add(Text(workspace == _workspace ? "●" : "○", "workspace-radio"));

            var copy = Element("workspace-option-copy");
            copy.Add(Text(title, "workspace-option-title"));
            copy.Add(Text(description, "workspace-option-description"));
            option.Add(copy);
            return option;
        }

        private Button NavButton(string icon, string label, AppDestination destination, bool selected = false)
        {
            var button = new Button(() => Navigate(destination));
            button.AddToClassList("nav-button");
            if (label == "Training Catalog") button.AddToClassList("design-nav-multiline");
            var target = destination switch { AppDestination.Home => Page.Home, AppDestination.Academy => _workspace == TrainingWorkspace.Professional ? Page.Catalog : Page.Academy, AppDestination.FreeFlight => Page.FreeFlight, AppDestination.Challenges => Page.Challenges, AppDestination.Hangar => Page.Hangar, _ => Page.Records };
            if (selected || _page == target) button.AddToClassList("nav-button-selected");
            var vectorIcon = DesignIcon(NavigationIcon(label)); vectorIcon.AddToClassList("nav-icon"); button.Add(vectorIcon);
            button.Add(Text(label, "nav-label"));
            return button;
        }

        private void Navigate(AppDestination destination)
        {
            ShowPage(destination switch { AppDestination.Home => Page.Home, AppDestination.Academy => _workspace == TrainingWorkspace.Professional ? Page.Catalog : Page.Academy, AppDestination.FreeFlight => Page.FreeFlight, AppDestination.Challenges => Page.Challenges, AppDestination.Hangar => Page.Hangar, _ => Page.Records });
        }

        private static string ToDisplayName(AppDestination destination)
        {
            return destination switch
            {
                AppDestination.FreeFlight => "Free Flight",
                AppDestination.FlightRecords => "Flight Records",
                _ => destination.ToString()
            };
        }

        private void ToggleWorkspaceMenu()
        {
            _workspaceMenu.ToggleInClassList("is-hidden");
        }

        private void SelectWorkspace(TrainingWorkspace workspace)
        {
            _workspace = workspace;
            LocalStore.Profile.workspace = WorkspaceName;
            LocalStore.Save();
            ShowPage(Page.Home);
        }

        private void RefreshControllerStatus()
        {
            if (_controllerDevice == null || _inputCapabilities == null) return;
            _controllerDevice.text = $"{_inputCapabilities.PrimaryDeviceLabel}\n✓  Input ready";
        }

        private void OnInputCapabilitiesChanged()
        {
            bool physical = _inputCapabilities.HasGamepad || _inputCapabilities.HasJoystick;
            bool disconnected = _hadPhysicalController && !physical;
            _hadPhysicalController = physical;
            if (_page == Page.Controller) ShowPage(Page.Controller);
            else RefreshControllerStatus();
            if (disconnected && _page == Page.Flight && _sim != null && _sim.Running && !_sim.Paused)
            {
                TogglePause(); _sim.Event("warning", "Controller disconnected; session paused");
                ShowToast("Controller disconnected. Reconnect or resume with keyboard / touch.");
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var scale = _document.panelSettings.scale;
            var width = Screen.width / scale; var height = Screen.height / scale;
            bool phone = width < 760 || height < 500;
            _root.EnableInClassList("layout-desktop", width >= 1280 && !phone);
            _root.EnableInClassList("layout-tablet", width >= 760 && width < 1280 && !phone);
            _root.EnableInClassList("layout-phone", phone);
            _root.EnableInClassList("text-expanded", LocalStore.Profile.settings.textScale > 1.1f);
            _root.EnableInClassList("layout-narrow", width < 1440 && width >= 760);
            _root.EnableInClassList("layout-wide", width >= 1920);
            _root.EnableInClassList("design-home-reference", _page == Page.Home && _workspace == TrainingWorkspace.Aerial &&
                width >= 1600 && height >= 900 && LocalStore.Profile.settings.textScale <= 1.1f);
            ApplyTypography();
            LayoutDesignPages(width, height);
            if (_page == Page.Flight || _page == Page.Replay)
            {
                LayoutFlightHud(width, height);
            }
            if(Application.isMobilePlatform)
            {
                var safe=Screen.safeArea;
                _root.style.paddingLeft=safe.xMin/scale; _root.style.paddingRight=(Screen.width-safe.xMax)/scale;
                _root.style.paddingTop=(Screen.height-safe.yMax)/scale; _root.style.paddingBottom=safe.yMin/scale;
                var hud=_root.Q<VisualElement>(className:"flight-hud");
                if(hud!=null) { hud.style.left=safe.xMin/scale; hud.style.right=(Screen.width-safe.xMax)/scale; hud.style.top=(Screen.height-safe.yMax)/scale; hud.style.bottom=safe.yMin/scale; }
            }
        }

        private void InstallToast()
        {
            DismissToast();
            var toast = Element("toast", "toast-hidden");
            // Transient information must never intercept flight or modal controls.
            toast.pickingMode = PickingMode.Ignore;
            _toastLabel = Text("", "toast-label");
            _toastLabel.pickingMode = PickingMode.Ignore;
            toast.Add(_toastLabel);
            _root.Add(toast);
        }

        private void DismissToast()
        {
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = null;
            _toastLabel?.parent?.AddToClassList("toast-hidden");
        }

        private void ShowToast(string message)
        {
            if (_toastLabel?.parent == null) return;
            _toastLabel.text = message;
            // The pause panel has a persistent OSD availability description.
            // Suppress transient overlays while the user reads or acts on a modal.
            if (_page == Page.Flight && _sim != null && _sim.Paused)
            {
                DismissToast();
                return;
            }
            _toastLabel.parent.RemoveFromClassList("toast-hidden");
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(HideToastAfterDelay());
        }

        private IEnumerator HideToastAfterDelay()
        {
            yield return new WaitForSecondsRealtime(3f);
            _toastLabel?.parent?.AddToClassList("toast-hidden");
            _toastRoutine = null;
        }

        private static VisualElement StatusPill(string icon, string text, bool highlighted = false)
        {
            var pill = Element("status-pill");
            if (highlighted) pill.AddToClassList("status-pill-highlighted");
            if (!string.IsNullOrEmpty(icon)) pill.Add(Text(icon, "status-icon"));
            pill.Add(Text(text, "status-text"));
            return pill;
        }

        private static Button IconButton(string icon, string tooltip, Action clicked)
        {
            var button = new Button(clicked) { tooltip = tooltip };
            button.AddToClassList("icon-button");
            button.Add(DesignIcon(icon == "SET" ? "settings" : NavigationIcon(tooltip)));
            return button;
        }

        private static Button PrimaryButton(string label, Action clicked)
        {
            var button = new Button(clicked) { text = label };
            button.AddToClassList("primary-button");
            return button;
        }

        private static VisualElement Element(params string[] classes)
        {
            var element = new VisualElement();
            foreach (var className in classes) element.AddToClassList(className);
            return element;
        }

        private static Label Text(string text, params string[] classes)
        {
            var label = new Label(text);
            foreach (var className in classes) label.AddToClassList(className);
            return label;
        }

        private static void SetBackground(VisualElement element, string resourcePath)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return;
            element.style.backgroundImage = new StyleBackground(texture);
            element.style.unityBackgroundScaleMode = resourcePath.EndsWith("ControllerCutout", StringComparison.Ordinal) ? ScaleMode.ScaleToFit : ScaleMode.ScaleAndCrop;
            if (element.ClassListContains("scene-banner"))
                element.RegisterCallback<GeometryChangedEvent>(e => {
                    var height = Mathf.Clamp(e.newRect.width * 9f / 16f, 180, 420);
                    if (Mathf.Abs(element.resolvedStyle.height - height) > 1) element.style.height = height;
                });
        }
    }
}
