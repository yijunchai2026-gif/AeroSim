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
        private string _hangarInspectAircraftId = "";
        private string _hangarInspectAngle = "front-right";
        private string _hangarInspectMode = "Beauty";

        private VisualElement BuildHangarPage()
        {
            var selected = AircraftCatalog.Selected;
            if (string.IsNullOrEmpty(_hangarGroup)) _hangarGroup = WorkspaceAircraftGroup();
            var inspected = AircraftCatalog.All.FirstOrDefault(profile => profile.Id == _hangarInspectAircraftId) ?? selected;
            _hangarInspectAircraftId = inspected.Id;
            var asset = inspected.AssetContract;
            var production = ProductionAssetLoader.Inspect(inspected.Id);
            bool formalReady = IsFormalAircraftReady(inspected);
            var angle = asset?.goldenAngles?.FirstOrDefault(value => value.id == _hangarInspectAngle) ?? asset?.goldenAngles?.FirstOrDefault();
            if (angle != null) _hangarInspectAngle = angle.id;

            var s = DesignPage("Aircraft Bay", "Inspect identity, quality evidence, and flight eligibility before entering pre-flight.", "01 Hangar / RD-05");
            s.AddToClassList("design-hangar-page");
            s.AddToClassList("aircraft-bay-page");
            s.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            s.verticalScrollerVisibility = ScrollerVisibility.Auto;

            var truth = Element("aircraft-bay-truth");
            truth.Add(StatusPill(AircraftIdentityLabel(inspected), formalReady ? "quality-pass" : "quality-warning"));
            truth.Add(StatusPill(AircraftContentLabel(production), formalReady ? "quality-pass" : "quality-fail"));
            truth.Add(Text(formalReady
                ? "Formal identity is allowed only because RD-02 identity and quality gates are both approved."
                : "Prototype geometry is shown honestly. It is not a delivered production aircraft and cannot enter a formal mission.", "wrap", "aircraft-bay-truth-copy"));
            s.Add(truth);

            var bay = Element("aircraft-bay-layout", "design-columns");
            var fleet = DesignPanel("Fleet", "Installed runtime and locked catalog entries", "aircraft-bay-fleet", "design-rail");
            var search = new TextField("Search") { value = _hangarSearch };
            search.AddToClassList("product-field");
            fleet.Add(search);
            var filters = Element("hangar-filter-row");
            foreach (var group in new[] { "All", "Aerial", "FPV", "Professional" })
            {
                string captured = group;
                var button = ActionButton(group, () => { _hangarGroup = captured; ShowPage(Page.Hangar); });
                button.name = "hangar-filter-" + group.ToLowerInvariant();
                button.EnableInClassList("hangar-filter-selected", group == _hangarGroup);
                filters.Add(button);
            }
            fleet.Add(filters);
            var recommended = new Toggle("Current workspace") { value = _hangarRecommendedOnly };
            recommended.AddToClassList("product-field");
            fleet.Add(recommended);
            var fleetList = Element("aircraft-bay-fleet-list");
            fleet.Add(fleetList);
            var fleetEmpty = Text("No aircraft match these filters. Clear search or choose All; the current selection is preserved.", "muted", "wrap");
            fleetEmpty.style.display = DisplayStyle.None;
            fleet.Add(fleetEmpty);

            var fleetRows = new List<KeyValuePair<AircraftProfile, VisualElement>>();
            foreach (var profile in AircraftCatalog.All.OrderByDescending(p => p.IsSelectable).ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                var row = Element("aircraft-card", "aircraft-bay-fleet-item");
                row.name = "aircraft-card-" + profile.Id;
                if (profile.Id == inspected.Id) row.AddToClassList("design-selected");
                var inspect = new Button(() => { _hangarInspectAircraftId = profile.Id; ShowPage(Page.Hangar); });
                inspect.AddToClassList("aircraft-bay-fleet-inspect");
                inspect.Add(Text(profile.DisplayShortName, "aircraft-bay-fleet-name", "wrap"));
                inspect.Add(Text(AircraftIdentityShortLabel(profile), profile.CanClaimModelIdentity ? "hangar-status-ready" : "hangar-status-warning", "wrap"));
                inspect.Add(Text(profile.Id == selected.Id ? "SELECTED FOR FLIGHT" : profile.IsSelectable ? "AVAILABLE" : "LOCKED · " + AircraftLockReason(profile), "aircraft-bay-fleet-state", "wrap"));
                row.Add(inspect);
                var selectRow = ActionButton(profile.Id == selected.Id ? "Selected" : profile.IsSelectable ? "Select" : "Locked", () => SelectHangarAircraft(profile));
                selectRow.name = "hangar-select-" + profile.Id;
                selectRow.SetEnabled(profile.IsSelectable && profile.Id != selected.Id);
                row.Add(selectRow);
                fleetRows.Add(new KeyValuePair<AircraftProfile, VisualElement>(profile, row));
                fleetList.Add(row);
            }

            var stage = DesignPanel(inspected.DisplayName, AircraftGroups(inspected) + " · " + inspected.Category, "aircraft-bay-stage", "design-column");
            if (inspected.Status == AircraftCatalogStatus.Runtime && inspected.HasVisualAsset)
            {
                var preview = RenderAircraftPreview(inspected, angle);
                _hangarPreviews.Add(preview);
                var previewImage = new Image { image = preview, scaleMode = ScaleMode.ScaleToFit };
                previewImage.AddToClassList("aircraft-preview");
                previewImage.AddToClassList("aircraft-bay-preview");
                stage.Add(previewImage);
            }
            else
            {
                var missingPreview = Element("aircraft-bay-preview", "aircraft-bay-missing-preview");
                missingPreview.Add(Text("CONTENT NOT DELIVERED", "aircraft-bay-missing-title"));
                missingPreview.Add(Text("No runtime visual is available. A different aircraft is not substituted.", "muted", "wrap"));
                stage.Add(missingPreview);
            }
            stage.Add(Text(inspected.Status == AircraftCatalogStatus.Runtime && inspected.HasVisualAsset
                ? "18% GRAY STUDIO · 5600K · PROGRAMMATIC RUNTIME PREVIEW"
                : "18% GRAY STUDIO CONTRACT · PREVIEW UNAVAILABLE", "page-eyebrow", "aircraft-bay-studio-label"));
            stage.Add(Text(asset?.disclosure ?? "No RD-02 identity record is available for this catalog entry.", "wrap", "aircraft-bay-disclosure"));

            var angleHeader = Element("aircraft-bay-section-heading");
            angleHeader.Add(Text("12 Golden Angles / Detail Inspect", "panel-title"));
            angleHeader.Add(Text("Current · " + GoldenAngleDisplayName(_hangarInspectAngle), "muted"));
            stage.Add(angleHeader);
            var angleGrid = Element("aircraft-bay-angle-grid");
            if (asset?.goldenAngles != null)
            {
                for (int i = 0; i < asset.goldenAngles.Length; i++)
                {
                    var capturedAngle = asset.goldenAngles[i];
                    var button = ActionButton("G" + (i + 1).ToString("00") + " · " + GoldenAngleDisplayName(capturedAngle.id), () =>
                    {
                        _hangarInspectAngle = capturedAngle.id;
                        ShowPage(Page.Hangar);
                    });
                    button.name = "golden-angle-" + capturedAngle.id;
                    if (capturedAngle.id == _hangarInspectAngle) button.AddToClassList("design-selected");
                    angleGrid.Add(button);
                }
            }
            else angleGrid.Add(Text("12-angle contract unavailable · inspect the RD-02 manifest.", "muted", "wrap"));
            stage.Add(angleGrid);

            var inspectModes = Element("aircraft-bay-inspect-modes");
            foreach (var mode in new[] { "Beauty", "Clay", "Wireframe", "Normal", "Roughness", "AO", "Collision", "LOD" })
            {
                string capturedMode = mode;
                bool available = mode == "Beauty" && inspected.Status == AircraftCatalogStatus.Runtime && inspected.HasVisualAsset;
                var button = ActionButton(mode, () =>
                {
                    _hangarInspectMode = capturedMode;
                    ShowToast(available ? "Beauty preview active." : capturedMode + " inspection requires a delivered formal asset and is not available for this prototype.");
                    ShowPage(Page.Hangar);
                });
                button.name = "inspect-mode-" + mode.ToLowerInvariant();
                button.EnableInClassList("design-selected", mode == _hangarInspectMode);
                button.SetEnabled(available || formalReady);
                inspectModes.Add(button);
            }
            stage.Add(inspectModes);
            stage.Add(Text(formalReady
                ? "Turntable and production debug views are eligible after provider assembly validation."
                : "Turntable / debug material views · REQUIRES ASSET. Beauty shows the current v3 programmatic model only.", "muted", "wrap"));

            var quality = DesignPanel("Quality Rail", "Manifest truth · no inferred production claims", "aircraft-bay-quality", "design-side");
            quality.Add(QualityFact("Identity", AircraftIdentityLabel(inspected), formalReady ? "PASS" : "FAIL"));
            quality.Add(QualityFact("Envelope", formalReady ? "Production contract present" : "UNVERIFIED against reference", formalReady ? "PASS" : "BLOCKED"));
            quality.Add(QualityFact("Mechanics", formalReady ? "Assembly validation required" : "Prototype articulation only", formalReady ? "VALIDATING" : "BLOCKED"));
            quality.Add(QualityFact("PBR", formalReady ? "Standard Metallic contract" : "No production texture evidence", formalReady ? "VALIDATING" : "BLOCKED"));
            quality.Add(QualityFact("LOD", asset?.lod?.names == null ? "No contract" : string.Join(" / ", asset.lod.names), formalReady ? "VALIDATING" : "CONTRACT ONLY"));
            quality.Add(QualityFact("Collision", asset?.collision == null ? "No contract" : asset.collision.minimumPrimitives + "–" + asset.collision.maximumPrimitives + " hidden primitives", formalReady ? "VALIDATING" : "CONTRACT ONLY"));
            quality.Add(QualityFact("Evidence", formalReady ? "12 captures still required" : "NOT CAPTURED", "BLOCKED"));
            quality.Add(Text("Manifest · " + (asset == null ? "not addressed" : AircraftAssetContracts.Manifest.manifestVersion) +
                "\nAsset · " + (asset?.assetId ?? "not addressed") +
                "\nVisual · " + inspected.VisualVersion +
                "\nPhysics · " + inspected.PhysicsVersion +
                "\nLoader · " + production.State +
                "\n" + production.Message, "muted", "wrap", "aircraft-bay-contract"));
            if (!formalReady)
            {
                var recovery = Element("aircraft-bay-recovery");
                recovery.Add(Text("FORMAL FLIGHT BLOCKED", "hangar-status-warning"));
                recovery.Add(Text(AircraftRecoveryMessage(inspected, production), "muted", "wrap"));
                recovery.Add(ActionButton("Open Asset Diagnostics", () => ShowToast(production.Message)));
                quality.Add(recovery);
            }

            bay.Add(fleet);
            bay.Add(stage);
            bay.Add(quality);
            s.Add(bay);

            var footer = Element("design-footer", "aircraft-bay-actions");
            footer.Add(DesignCopy(inspected.Id == selected.Id ? "Selected for next session" : "Inspection only", formalReady ? "Formal asset gate eligible" : "Prototype sandbox only · persistent PROTOTYPE ASSET label in pre-flight"));
            var select = DesignButton(inspected.Id == selected.Id ? "Selected" : "Select for Prototype", "drone", () => SelectHangarAircraft(inspected));
            select.name = "hangar-select-" + inspected.Id;
            select.SetEnabled(inspected.IsSelectable && inspected.Id != selected.Id);
            footer.Add(select);
            footer.Add(DesignButton("Pre-Flight Check", "shield", () =>
            {
                if (inspected.Id != AircraftCatalog.Selected.Id && inspected.IsSelectable) SelectHangarAircraft(inspected, false);
                _mission = null;
                _config = BuildAircraftBayTestConfig(inspected);
                ShowPage(Page.Preflight);
            }, true));
            var fly = ActionButton("Fly Selected Aircraft", () =>
            {
                _mission = null;
                _config = BuildAircraftBayTestConfig(AircraftCatalog.Selected);
                ShowPage(Page.Preflight);
            });
            fly.name = "aircraft-bay-fly-selected";
            fly.SetEnabled(AircraftCatalog.Selected.IsSelectable);
            footer.Add(fly);
            s.Add(footer);

            void RefreshFleet()
            {
                string query = (_hangarSearch ?? "").Trim();
                int visible = 0;
                foreach (var pair in fleetRows)
                {
                    bool groupMatch = _hangarGroup == "All" || HasWorkspace(pair.Key, GroupWorkspace(_hangarGroup));
                    bool recommendationMatch = !_hangarRecommendedOnly || IsRecommendedForCurrentWorkspace(pair.Key);
                    bool searchMatch = string.IsNullOrEmpty(query) || (pair.Key.DisplayName + " " + pair.Key.Family + " " + AircraftCapabilities(pair.Key)).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool show = groupMatch && recommendationMatch && searchMatch;
                    pair.Value.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                    if (show) visible++;
                }
                fleetEmpty.style.display = visible == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            search.RegisterValueChangedCallback(evt => { _hangarSearch = evt.newValue ?? ""; RefreshFleet(); });
            recommended.RegisterValueChangedCallback(evt => { _hangarRecommendedOnly = evt.newValue; RefreshFleet(); });
            RefreshFleet();
            return s;
        }

        private static VisualElement StatusPill(string value, string stateClass)
        {
            var pill = Text(value, "aircraft-bay-pill", "wrap");
            pill.AddToClassList(stateClass);
            return pill;
        }

        private static VisualElement QualityFact(string title, string detail, string state)
        {
            var row = Element("quality-rail-row");
            var copy = Element("design-copy");
            copy.Add(Text(title, "quality-rail-title"));
            copy.Add(Text(detail, "muted", "wrap"));
            row.Add(copy);
            var stateLabel = Text(state, "quality-rail-state");
            stateLabel.AddToClassList(state == "PASS" ? "quality-pass" : state == "VALIDATING" || state == "CONTRACT ONLY" ? "quality-warning" : "quality-fail");
            row.Add(stateLabel);
            return row;
        }

        private static bool IsFormalAircraftReady(AircraftProfile profile) => profile != null && profile.CanClaimModelIdentity &&
            ProductionAssetLoader.Inspect(profile.Id).State == ProductionAssetLoadState.Ready;

        private static string AircraftIdentityShortLabel(AircraftProfile profile)
        {
            var asset = profile?.AssetContract;
            if (asset == null) return "CONTENT NOT DELIVERED";
            if (asset.IdentityState == AssetIdentityState.AeroSimGeneric) return "AEROSIM GENERIC";
            return profile.CanClaimModelIdentity ? "REAL MODEL / VERIFIED" : "PROGRAMMATIC PROTOTYPE / UNVERIFIED";
        }

        private static string AircraftIdentityLabel(AircraftProfile profile)
        {
            var asset = profile?.AssetContract;
            if (asset == null) return "CONTENT NOT DELIVERED / NO IDENTITY CONTRACT";
            if (asset.IdentityState == AssetIdentityState.AeroSimGeneric)
                return "AEROSIM GENERIC / " + HumanizeAssetState(asset.QualityState.ToString()).ToUpperInvariant();
            if (!profile.CanClaimModelIdentity)
                return "PROGRAMMATIC PROTOTYPE / UNVERIFIED";
            return "REAL MODEL / VERIFIED · PRODUCTION APPROVED";
        }

        private static string AircraftContentLabel(ProductionAssetLoadResult production) =>
            production != null && production.State == ProductionAssetLoadState.Ready ? "FORMAL ASSET CONTRACT READY" : "CONTENT NOT DELIVERED";

        private static string AircraftRecoveryMessage(AircraftProfile profile, ProductionAssetLoadResult production)
        {
            if (profile == null || !profile.IsSelectable) return AircraftLockReason(profile) + ". " + AircraftRecoveryAction(profile) + ".";
            return "Continue with a clearly marked prototype test flight, choose another aircraft, or inspect the loader report. Selection, environment, and settings are preserved. " + production.Message;
        }

        private static string HumanizeAssetState(string value) => string.Concat((value ?? "").Select((character, index) => index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
        private static string GoldenAngleDisplayName(string id) => string.IsNullOrWhiteSpace(id) ? "Unavailable" : HumanizeAssetState(id.Replace('-', ' '));

        private FlightConfig BuildAircraftBayTestConfig(AircraftProfile profile) => new()
        {
            title = "Aircraft Bay · Prototype Test",
            workspace = WorkspaceName,
            aircraft = profile?.DisplayName ?? AircraftCatalog.Selected.DisplayName,
            mode = _workspace == TrainingWorkspace.Fpv ? "Manual" : "Normal",
            environment = _workspace == TrainingWorkspace.Fpv ? "Desert Range" : "Pacific Coast",
            objective = "Verify simulator handling with persistent prototype disclosure. No formal asset approval is implied."
        };

        private void SelectHangarAircraft(AircraftProfile profile, bool rebuild = true)
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
                userProfile.selectedAircraftSource = "aircraft-bay";
                userProfile.selectedAircraftUpdatedUtc = DateTime.UtcNow.ToString("O");
                LocalStore.Save();
                if (rebuild) ShowPage(Page.Hangar);
                ShowToast("Selected " + profile.DisplayName + ". Asset truth and settings remain attached to the selection.");
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
                ShowToast("Aircraft selection was not saved. Check local storage and try again; the previous selection is preserved.");
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

        private bool IsRecommendedForCurrentWorkspace(AircraftProfile profile) => profile != null && HasWorkspace(profile, CurrentAircraftWorkspace());

        private bool IsCompatibleForCurrentContext(AircraftProfile profile)
        {
            if (profile == null || !profile.IsSelectable || !HasWorkspace(profile, CurrentAircraftWorkspace())) return false;
            return _mission == null || AircraftCatalog.SupportsTask(profile, "inspection");
        }

        private static string AircraftPurpose(AircraftProfile profile) => profile?.UseCases != null && profile.UseCases.Length > 0
            ? string.Join(" · ", profile.UseCases)
            : "General simulator training";

        private static string AircraftCapabilities(AircraftProfile profile) =>
            profile?.Capabilities != null && profile.Capabilities.Length > 0 ? string.Join(" · ", profile.Capabilities) : "No verified capability tags";

        private static string AircraftLockReason(AircraftProfile profile)
        {
            if (profile == null) return "Aircraft record is missing";
            if (profile.Status != AircraftCatalogStatus.Runtime) return "Runtime access is not approved";
            if (!profile.HasVisualAsset || !profile.HasThumbnail) return "Visual asset or thumbnail is missing";
            if (!profile.HasPhysicsProfile || !profile.HasCompatibilityProfile) return "Flight or compatibility profile is missing";
            if (!profile.IsDownloaded) return "Content package is not downloaded";
            return "";
        }

        private static string AircraftRecoveryAction(AircraftProfile profile)
        {
            if (profile == null) return "Return to the fleet";
            if (profile.Status != AircraftCatalogStatus.Runtime) return "View catalog status";
            if (!profile.HasVisualAsset || !profile.HasThumbnail || !profile.HasPhysicsProfile || !profile.HasCompatibilityProfile)
                return "Review missing resources";
            if (!profile.IsDownloaded) return "Download content package";
            return "View compatible aircraft";
        }

        public RenderTexture RenderAircraftPreview(AircraftProfile profile) => RenderAircraftPreview(profile, null);

        // Same runtime model as flight and replay; previews are not stock product photos.
        private RenderTexture RenderAircraftPreview(AircraftProfile profile, GoldenAngleContract angle)
        {
            const int layer = 30;
            var stage = new GameObject("Aircraft preview stage") { layer = layer }; stage.transform.position = new Vector3(8000, 0, 0);
            var aircraft = new GameObject("Preview aircraft"); aircraft.transform.SetParent(stage.transform, false);
            aircraft.AddComponent<AircraftVisualModel>().Initialize(profile);
            if (angle != null) aircraft.transform.localRotation = Quaternion.Euler(angle.euler);
            foreach (var node in aircraft.GetComponentsInChildren<Transform>()) node.gameObject.layer = layer;
            var lamp = new GameObject("Preview soft key"); lamp.transform.SetParent(stage.transform, false);
            var light = lamp.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.8f; light.color = new Color(1, .95f, .88f);
            light.cullingMask = 1 << layer; light.transform.rotation = Quaternion.Euler(38, -35, 0);
            var cameraHost = new GameObject("Preview camera"); cameraHost.transform.SetParent(stage.transform, false);
            var camera = cameraHost.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << layer;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.18f, .18f, .18f);
            camera.fieldOfView = 32; camera.nearClipPlane = .008f; camera.farClipPlane = 4;
            float distance = angle == null ? 1 : Mathf.Max(.55f, angle.distanceMultiplier);
            camera.transform.position = stage.transform.position + new Vector3(1.45f, 1.08f, 1.78f) * profile.VisualScale * distance;
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
