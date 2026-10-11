using System;
using System.Collections.Generic;
using System.Linq;
using AeroSim.Domain;
using AeroSim.Navigation;
using AeroSim.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace AeroSim
{
    public sealed partial class AeroSimApplication
    {
        private readonly List<RenderTexture> _hangarPreviews = new();
        private string _hangarGroup = "";
        private string _hangarSearch = "";
        private bool _hangarRecommendedOnly;

        private VisualElement BuildHangarPage()
        {
            var selected = AircraftCatalog.Selected;
            if (string.IsNullOrEmpty(_hangarGroup)) _hangarGroup = WorkspaceAircraftGroup();

            var s = Canvas("Aircraft / Hangar", "Choose the right aircraft for your next flight.",
                "Browse installed runtime aircraft by Aerial, FPV, or Professional use. Search matches names, families, capabilities, and uses.");
            s.AddToClassList("design-page");
            s.AddToClassList("design-hangar-page");
            s.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            s.verticalScrollerVisibility = ScrollerVisibility.Auto;

            var summary = Panel("Selected · " + selected.Name,
                AircraftPurpose(selected) + " · Selection is saved on this device and used by Free Flight, Academy, Challenges, and Missions.");
            summary.AddToClassList("hangar-selected-hero");
            var selectedPreview = RenderAircraftPreview(selected);
            _hangarPreviews.Add(selectedPreview);
            var selectedImage = new Image { image = selectedPreview, scaleMode = ScaleMode.ScaleToFit };
            selectedImage.AddToClassList("hangar-selected-preview");
            summary.Insert(0, selectedImage);
            var flySelected = ActionButton("Fly Selected Aircraft", () => ShowPage(Page.FreeFlight), true);
            flySelected.SetEnabled(selected.Status == AircraftCatalogStatus.Runtime && selected.IsSelectable);
            summary.Add(Row(flySelected, ActionButton("Open Tuning", () => ShowPage(Page.Tuning))));
            s.Add(summary);

            var filters = Panel("Find an aircraft", "Choose a group or search the installed runtime catalog.");
            var search = new TextField("Search") { value = _hangarSearch };
            search.AddToClassList("product-field");
            filters.Add(search);

            // Initialize every element captured by Refresh before wiring callbacks to satisfy C# definite assignment.
            var resultSummary = Text("", "muted", "wrap");
            var grid = Element("product-grid", "aircraft-grid");
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            var cards = new List<KeyValuePair<AircraftProfile, VisualElement>>();
            var empty = Panel("No aircraft match these filters.", "Try another group or remove filters. Your current aircraft has not changed.");
            var emptySummary = Text("", "wrap", "muted");

            var groupRow = Element("product-row", "hangar-filter-row");
            var groupButtons = new Dictionary<string, Button>();
            foreach (var group in new[] { "All", "Aerial", "FPV", "Professional" })
            {
                var capturedGroup = group;
                var button = ActionButton(group, () => { _hangarGroup = capturedGroup; Refresh(); });
                button.name = "hangar-filter-" + group.ToLowerInvariant();
                groupButtons[group] = button;
                groupRow.Add(button);
            }
            filters.Add(groupRow);

            var recommended = new Toggle("Recommended for " + WorkspaceName + " workspace only") { value = _hangarRecommendedOnly };
            recommended.AddToClassList("product-field");
            filters.Add(recommended);
            filters.Add(resultSummary);
            filters.Add(Row(ActionButton("Clear Filters", () => { _hangarGroup = "All"; _hangarRecommendedOnly = false; recommended.SetValueWithoutNotify(false); Refresh(); }),
                ActionButton("Reset All", () => { _hangarGroup = "All"; _hangarRecommendedOnly = false; _hangarSearch = ""; search.SetValueWithoutNotify(""); recommended.SetValueWithoutNotify(false); Refresh(); })));
            s.Add(filters);

            foreach (var profile in AircraftCatalog.All
                         .OrderByDescending(IsCompatibleForCurrentContext)
                         .ThenByDescending(p => p.IsSelectable)
                         .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                var p = Panel(profile.Name, AircraftGroups(profile) + " · " + profile.Category);
                p.AddToClassList("aircraft-card");
                p.name = "aircraft-card-" + profile.Id;
                p.style.flexGrow = 1;
                p.style.flexShrink = 1;
                p.style.minWidth = 0;

                var preview = RenderAircraftPreview(profile);
                _hangarPreviews.Add(preview);
                var image = new Image { image = preview, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("aircraft-preview");
                p.Add(image);

                var compatible = IsCompatibleForCurrentContext(profile);
                var lockReason = AircraftLockReason(profile);
                var selectable = string.IsNullOrEmpty(lockReason);
                var taskBlocked = _mission != null && !compatible;
                if (profile.Id == selected.Id && selectable) p.AddToClassList("aircraft-card-selected");
                p.Add(Text(!selectable ? "LOCKED · " + lockReason : taskBlocked ? "INCOMPATIBLE · CURRENT TASK" : profile.Id == selected.Id ? "SELECTED · AVAILABLE" : compatible ? "AVAILABLE · COMPATIBLE" : "AVAILABLE · OTHER WORKSPACE",
                    selectable && compatible ? "hangar-status-ready" : "hangar-status-warning", "wrap"));
                p.Add(Text("Best for · " + AircraftPurpose(profile), "wrap"));
                p.Add(Text("Capabilities · " + AircraftCapabilities(profile), "wrap", "muted"));
                if (!selectable)
                    p.Add(Text("Locked · " + lockReason + ". " + AircraftRecoveryAction(profile), "wrap", "muted"));
                else if (taskBlocked)
                    p.Add(Text("Compatibility · This aircraft does not advertise the inspection capability required by the current mission. Choose a compatible professional aircraft; selection will not silently change.", "wrap", "muted"));
                else if (!compatible)
                    p.Add(Text("Compatibility · Not recommended for the current " + WorkspaceName + " workspace. It remains selectable for a compatible training flow.", "wrap", "muted"));
                else
                    p.Add(Text("Compatibility · Ready for the current " + WorkspaceName + " workspace.", "wrap", "muted"));

                var details = new Foldout { text = "Details, limits, and resources", value = false };
                details.Add(Text(profile.Description, "wrap", "aircraft-description"));
                details.Add(Text(profile.Reference, "wrap", "muted"));
                details.Add(Text("Modes · " + DisplayList(profile.FlightModes) +
                    "\nUses · " + DisplayList(profile.UseCases) +
                    "\nInput · " + DisplayList(profile.InputRequirements) +
                    "\nResources · " + (profile.IsDownloaded ? "Downloaded" : "Not downloaded") +
                    " · visual " + profile.VisualVersion + " · physics " + profile.PhysicsVersion +
                    "\nLimits · Versioned approximate training values; not manufacturer flight-controller data.", "wrap", "muted"));
                p.Add(details);

                var capturedProfile = profile;
                var canSelect = selectable && !taskBlocked;
                var selectLabel = !selectable ? AircraftRecoveryAction(profile) : taskBlocked ? "View Compatible Aircraft" : "Select " + ShortAircraftName(profile);
                var select = ActionButton(selectLabel, () =>
                {
                    if (!selectable) { ShowToast(lockReason + ". " + AircraftRecoveryAction(capturedProfile)); return; }
                    if (taskBlocked)
                    {
                        _hangarGroup = "Professional";
                        _hangarRecommendedOnly = true;
                        recommended.SetValueWithoutNotify(true);
                        Refresh();
                        ShowToast("Showing aircraft compatible with the current mission.");
                        return;
                    }
                    SelectHangarAircraft(capturedProfile);
                }, canSelect);
                select.name = "hangar-select-" + capturedProfile.Id;
                if (!selectable) select.SetEnabled(false);
                if (profile.Id == selected.Id && selectable && !taskBlocked)
                {
                    select.text = "Selected · " + ShortAircraftName(profile);
                    select.SetEnabled(false);
                }
                p.Add(select);
                cards.Add(new KeyValuePair<AircraftProfile, VisualElement>(profile, p));
                grid.Add(p);
            }
            s.Add(grid);

            empty.Add(emptySummary);
            empty.Add(Row(ActionButton("View All Aircraft", () => { _hangarGroup = "All"; _hangarRecommendedOnly = false; recommended.SetValueWithoutNotify(false); Refresh(); }),
                ActionButton("Clear Search", () => { _hangarSearch = ""; search.SetValueWithoutNotify(""); Refresh(); }, true)));
            empty.style.display = DisplayStyle.None;
            s.Add(empty);

            search.RegisterValueChangedCallback(e => { _hangarSearch = e.newValue ?? ""; Refresh(); });
            recommended.RegisterValueChangedCallback(e => { _hangarRecommendedOnly = e.newValue; Refresh(); });
            grid.RegisterCallback<GeometryChangedEvent>(e =>
            {
                var width = e.newRect.width;
                float cardWidth;
                if (width < 640) cardWidth = 100f;
                else if (width < 1440) cardWidth = 48.5f;
                else if (width < 2200) cardWidth = 32f;
                else cardWidth = 24f;
                foreach (var pair in cards)
                {
                    pair.Value.style.width = new Length(cardWidth, LengthUnit.Percent);
                    pair.Value.style.minWidth = width < 640 ? 0 : 280;
                    pair.Value.style.maxWidth = 480;
                }
            });

            Refresh();
            s.Add(Text("Browsable catalog · Runtime aircraft are selectable · Candidate aircraft remain locked · No real aircraft connection.", "wrap", "muted"));
            return s;

            void Refresh()
            {
                var query = (_hangarSearch ?? "").Trim();
                var searchMatches = new HashSet<AircraftProfile>(AircraftCatalog.Search(query, AircraftWorkspace.All, false));
                var visible = 0;
                foreach (var pair in cards)
                {
                    var groupMatch = _hangarGroup == "All" || HasWorkspace(pair.Key, GroupWorkspace(_hangarGroup));
                    var recommendationMatch = !_hangarRecommendedOnly || IsRecommendedForCurrentWorkspace(pair.Key);
                    var searchMatch = searchMatches.Contains(pair.Key);
                    var show = groupMatch && recommendationMatch && searchMatch;
                    pair.Value.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                    if (show) visible++;
                }

                foreach (var pair in groupButtons)
                {
                    var active = pair.Key == _hangarGroup;
                    pair.Value.EnableInClassList("hangar-filter-selected", active);
                    pair.Value.text = active ? "✓ " + pair.Key : pair.Key;
                }

                var filtersApplied = (_hangarGroup == "All" ? "All groups" : _hangarGroup) +
                    (_hangarRecommendedOnly ? " · Recommended" : "") +
                    (string.IsNullOrEmpty(query) ? "" : " · Search “" + query + "”");
                resultSummary.text = visible + " of " + cards.Count + " aircraft · " + filtersApplied;
                emptySummary.text = (string.IsNullOrEmpty(query) ? "No search term" : "Search “" + query + "”") + " · " +
                    (_hangarGroup == "All" ? "All groups" : _hangarGroup + " group") +
                    (_hangarRecommendedOnly ? " · Recommended only" : "");
                empty.style.display = visible == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                grid.style.display = visible == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void SelectHangarAircraft(AircraftProfile profile)
        {
            if (profile == null || !profile.IsSelectable)
            {
                ShowToast(profile == null ? "Aircraft is unavailable." : AircraftLockReason(profile) + ". " + AircraftRecoveryAction(profile));
                return;
            }

            var userProfile = LocalStore.Profile;
            var previous = new[] { userProfile.selectedAircraft, userProfile.selectedAircraftCatalogVersion,
                userProfile.selectedAircraftVisualVersion, userProfile.selectedAircraftPhysicsVersion,
                userProfile.selectedAircraftSource, userProfile.selectedAircraftUpdatedUtc };
            try
            {
                userProfile.selectedAircraft = profile.Id;
                userProfile.selectedAircraftCatalogVersion = AircraftCatalog.CatalogVersion;
                userProfile.selectedAircraftVisualVersion = profile.VisualVersion;
                userProfile.selectedAircraftPhysicsVersion = profile.PhysicsVersion;
                userProfile.selectedAircraftSource = "hangar";
                userProfile.selectedAircraftUpdatedUtc = DateTime.UtcNow.ToString("O");
                LocalStore.Save();
                ShowPage(Page.Hangar);
                ShowToast("Selected " + profile.Name + ". Saved for future flights.");
            }
            catch (Exception error)
            {
                userProfile.selectedAircraft = previous[0];
                userProfile.selectedAircraftCatalogVersion = previous[1];
                userProfile.selectedAircraftVisualVersion = previous[2];
                userProfile.selectedAircraftPhysicsVersion = previous[3];
                userProfile.selectedAircraftSource = previous[4];
                userProfile.selectedAircraftUpdatedUtc = previous[5];
                Debug.LogException(error);
                ShowToast("Aircraft selection was not saved. Check local storage and try again.");
            }
        }

        private string WorkspaceAircraftGroup() => _workspace == TrainingWorkspace.Fpv ? "FPV" :
            _workspace == TrainingWorkspace.Professional ? "Professional" : "Aerial";

        private AircraftWorkspace CurrentAircraftWorkspace() => _workspace == TrainingWorkspace.Fpv ? AircraftWorkspace.Fpv :
            _workspace == TrainingWorkspace.Professional ? AircraftWorkspace.Professional : AircraftWorkspace.Aerial;

        private static AircraftWorkspace GroupWorkspace(string group) => group == "FPV" ? AircraftWorkspace.Fpv :
            group == "Professional" ? AircraftWorkspace.Professional : AircraftWorkspace.Aerial;

        private static bool HasWorkspace(AircraftProfile profile, AircraftWorkspace workspace) =>
            profile != null && (profile.Workspaces & workspace) != 0;

        private static string AircraftGroups(AircraftProfile profile)
        {
            var groups = new List<string>();
            if (HasWorkspace(profile, AircraftWorkspace.Aerial)) groups.Add("Aerial");
            if (HasWorkspace(profile, AircraftWorkspace.Fpv)) groups.Add("FPV");
            if (HasWorkspace(profile, AircraftWorkspace.Professional)) groups.Add("Professional");
            return groups.Count == 0 ? "Unassigned" : string.Join(" / ", groups);
        }

        private bool IsRecommendedForCurrentWorkspace(AircraftProfile profile) =>
            profile != null && HasWorkspace(profile, CurrentAircraftWorkspace());

        private bool IsCompatibleForCurrentContext(AircraftProfile profile)
        {
            if (profile == null || !profile.IsSelectable || !HasWorkspace(profile, CurrentAircraftWorkspace())) return false;
            return _mission == null || AircraftCatalog.SupportsTask(profile, "inspection");
        }

        private static string AircraftPurpose(AircraftProfile profile) => profile.UseCases != null && profile.UseCases.Length > 0
            ? string.Join(" · ", profile.UseCases)
            : "General simulator training";

        private static string AircraftCapabilities(AircraftProfile profile) =>
            profile.Capabilities != null && profile.Capabilities.Length > 0 ? string.Join(" · ", profile.Capabilities) : "No verified capability tags";

        private static string AircraftLockReason(AircraftProfile profile)
        {
            if (profile.Status != AircraftCatalogStatus.Runtime) return "Runtime access is not approved";
            if (!profile.HasVisualAsset || !profile.HasThumbnail) return "Visual asset or thumbnail is missing";
            if (!profile.HasPhysicsProfile || !profile.HasCompatibilityProfile) return "Flight or compatibility profile is missing";
            if (!profile.IsDownloaded) return "Content package is not downloaded";
            return "";
        }

        private static string AircraftRecoveryAction(AircraftProfile profile)
        {
            if (profile.Status != AircraftCatalogStatus.Runtime) return "View catalog status";
            if (!profile.HasVisualAsset || !profile.HasThumbnail || !profile.HasPhysicsProfile || !profile.HasCompatibilityProfile)
                return "Review missing resources";
            if (!profile.IsDownloaded) return "Download content package";
            return "View compatible aircraft";
        }

        private static string DisplayList(IEnumerable<string> values)
        {
            var items = values == null ? new string[0] : values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            return items.Length == 0 ? "Not specified" : string.Join(" · ", items);
        }

        private static string ShortAircraftName(AircraftProfile profile) => !string.IsNullOrWhiteSpace(profile.ShortName)
            ? profile.ShortName
            : profile.Name.StartsWith("DJI ", StringComparison.Ordinal) ? profile.Name.Substring(4) : profile.Name;

        // Same runtime model as flight and replay; previews are not stock product photos.
        public RenderTexture RenderAircraftPreview(AircraftProfile profile)
        {
            const int layer = 30;
            var stage = new GameObject("Aircraft preview stage") { layer = layer }; stage.transform.position = new Vector3(8000, 0, 0);
            var aircraft = new GameObject("Preview aircraft"); aircraft.transform.SetParent(stage.transform, false);
            aircraft.AddComponent<AircraftVisualModel>().Initialize(profile);
            foreach (var node in aircraft.GetComponentsInChildren<Transform>()) node.gameObject.layer = layer;
            var lamp = new GameObject("Preview soft key"); lamp.transform.SetParent(stage.transform, false);
            var light = lamp.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.8f; light.color = new Color(1, .95f, .88f);
            light.cullingMask = 1 << layer; light.transform.rotation = Quaternion.Euler(38, -35, 0);
            var cameraHost = new GameObject("Preview camera"); cameraHost.transform.SetParent(stage.transform, false);
            var camera = cameraHost.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << layer;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.024f, .067f, .10f);
            camera.fieldOfView = 32; camera.nearClipPlane = .008f; camera.farClipPlane = 4;
            camera.transform.position = stage.transform.position + new Vector3(1.45f, 1.08f, 1.78f) * profile.VisualScale;
            camera.transform.LookAt(stage.transform.position + Vector3.up * .02f * profile.VisualScale);
            var mode = RenderSettings.ambientMode; var sky = RenderSettings.ambientSkyColor; var equator = RenderSettings.ambientEquatorColor;
            var ground = RenderSettings.ambientGroundColor; bool fog = RenderSettings.fog;
            int previewWidth = Screen.width >= 2400 ? 1536 : Screen.width >= 1400 ? 1024 : 768;
            int previewHeight = Mathf.RoundToInt(previewWidth * 9f / 16f);
            var rt = new RenderTexture(previewWidth, previewHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = previewWidth >= 1024 ? 4 : 2,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            rt.Create();
            try
            {
                RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.58f, .66f, .75f); RenderSettings.ambientEquatorColor = new Color(.25f, .29f, .34f);
                RenderSettings.ambientGroundColor = new Color(.10f, .12f, .15f); camera.targetTexture = rt; camera.Render();
            }
            finally
            {
                camera.targetTexture = null; RenderSettings.fog = fog; RenderSettings.ambientMode = mode;
                RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
                stage.SetActive(false); Destroy(stage);
            }
            return rt;
        }

        private void ReleaseHangarPreviews()
        {
            foreach (var preview in _hangarPreviews) if (preview != null) { preview.Release(); Destroy(preview); }
            _hangarPreviews.Clear();
        }
    }
}
