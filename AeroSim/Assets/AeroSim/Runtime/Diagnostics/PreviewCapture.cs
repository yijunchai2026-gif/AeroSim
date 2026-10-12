using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AeroSim.Domain;
using AeroSim.Input;
using AeroSim.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace AeroSim.Diagnostics
{
    public static class PreviewCapture
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--capture-preview") && !args.Contains("--capture-all") && !args.Contains("--capture-graphics") && !args.Contains("--capture-aircraft") && !args.Contains("--capture-visual-lab") && !args.Contains("--capture-coast-matrix") && !args.Contains("--capture-hero-cliff") && !args.Contains("--smoke-test") && !args.Contains("--export-evidence-manifest")) return;
            var host = new GameObject("AeroSim Diagnostics"); UnityEngine.Object.DontDestroyOnLoad(host); host.AddComponent<Runner>();
        }
        private sealed class Runner : MonoBehaviour
        {
            private AeroSimApplication _app;
            private Keyboard _keyboard;
            private Mouse _mouse;
            private readonly List<string> _results = new();
            private int _failures;
            private int _runtimeErrors;
            private int _particleVelocityWarnings;
            private void OnEnable()=>Application.logMessageReceived+=TrackError;
            private void OnDisable()=>Application.logMessageReceived-=TrackError;
            private void TrackError(string condition,string trace,LogType type)
            {
                if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)_runtimeErrors++;
                if(type==LogType.Warning && condition.Contains("Particle Velocity curves must all be in the same mode")) _particleVelocityWarnings++;
            }
            private string Argument(string key)
            {
                var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key); return i >= 0 && i < args.Length - 1 ? args[i + 1] : null;
            }
            private void Check(string name, bool passed)
            {
                var result = (passed ? "PASS " : "FAIL ") + name; _results.Add(result); Debug.Log(result); if (!passed) _failures++;
            }
            private void RecordCoastCapture(string name, VisualLabEvidenceManifest manifest, VisualLabShotArtifact artifact, bool runtimeBindingsMatch, string absolutePath)
            {
                bool captured = artifact != null && artifact.captureStatus == "CAPTURED" && File.Exists(absolutePath);
                bool guarded = artifact?.compositionGuard != null && artifact.compositionGuard.Passed;
                bool evidenceIncomplete = manifest == null || manifest.status == "INCOMPLETE_EVIDENCE" || manifest.gateStatus != "GATE_PASS";
                string state = captured ? "CAPTURED" : "CAPTURE_FAILED";
                state += guarded ? " · COMPOSITION_GUARD_OK" : " · COMPOSITION_GUARD_FAILED";
                state += evidenceIncomplete ? " · INCOMPLETE_EVIDENCE · GATE_NOT_RUN" : " · " + manifest.gateStatus;
                state += runtimeBindingsMatch ? " · RUNTIME_BINDINGS_OK" : " · RUNTIME_BINDINGS_FAILED";
                _results.Add(state + " " + name); Debug.Log(state + " " + name);
                if (!captured || !guarded || !runtimeBindingsMatch) _failures++;
            }
            private static bool IsVisible(VisualElement element)
            {
                if (element == null || element.worldBound.width <= 0 || element.worldBound.height <= 0) return false;
                for (var node = element; node != null; node = node.parent)
                    if (node.resolvedStyle.display == DisplayStyle.None || node.resolvedStyle.visibility == Visibility.Hidden) return false;
                return true;
            }
            private static bool Inside(Rect inner, Rect outer, float tolerance = 1) => inner.xMin >= outer.xMin - tolerance &&
                inner.xMax <= outer.xMax + tolerance && inner.yMin >= outer.yMin - tolerance && inner.yMax <= outer.yMax + tolerance;
            private static bool Overlaps(Rect a, Rect b) => Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)>1 &&
                Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin)>1;
            private void AuditFlightHudGeometry(string label)
            {
                var root = _app.GetComponent<UIDocument>().rootVisualElement;
                var hud = root.Q<VisualElement>(className:"flight-hud");
                var state = _app.FlightHudLayoutState;
                if (hud == null || state == null) { Check(label+" HUD geometry state available",false); return; }
                var comfort = new Rect(hud.worldBound.x + state.comfortRect.x, hud.worldBound.y + state.comfortRect.y,
                    state.comfortRect.width, state.comfortRect.height);
                var blocks = new List<VisualElement>();
                foreach (var className in new[] { "v1-camera-dock", "v1-camera-hud-status", "v1-flight-map", "touch-stick", "v1-flight-telemetry", "flight-guidance", "v1-camera-tools", "v1-objective-scroll", "v1-custom-osd-chip" })
                    blocks.AddRange(root.Query<VisualElement>(className:className).ToList().Where(IsVisible));
                Debug.Log($"HUD_LAYOUT {label} status={state.status} strategy={state.radarStrategy} content={state.contentRect} comfort={comfort}");
                foreach (var block in blocks)
                {
                    string target = string.IsNullOrEmpty(block.name) ? string.Join(".",block.GetClasses()) : block.name;
                    Debug.Log($"HUD_RECT {label} {target}={block.worldBound}");
                    Check(label+" "+target+" inside ComfortRect",Inside(block.worldBound,comfort));
                }
                var guidance = root.Q<VisualElement>(className:"flight-guidance");
                var telemetry = root.Q<VisualElement>(className:"v1-flight-telemetry");
                bool shortWide = state.contentRect.height < 520 || state.contentRect.width / Mathf.Max(1,state.contentRect.height) > 1.9f;
                Check(label+" short-wide guidance is hidden or separated",!shortWide || !IsVisible(guidance) || !IsVisible(telemetry) || !Overlaps(guidance.worldBound,telemetry.worldBound));
                var texts = new HashSet<TextElement>();
                foreach (var block in blocks) foreach (var text in block.Query<TextElement>().ToList())
                    if (IsVisible(text) && !string.IsNullOrWhiteSpace(text.text)) texts.Add(text);
                var textList = texts.ToList();
                for (int i=0;i<textList.Count;i++) for (int j=i+1;j<textList.Count;j++)
                    Check(label+" no text overlap: "+textList[i].text+" / "+textList[j].text,!Overlaps(textList[i].worldBound,textList[j].worldBound));
                for (int i=0;i<blocks.Count;i++) for (int j=i+1;j<blocks.Count;j++)
                {
                    string a = string.IsNullOrEmpty(blocks[i].name) ? string.Join(".",blocks[i].GetClasses()) : blocks[i].name;
                    string b = string.IsNullOrEmpty(blocks[j].name) ? string.Join(".",blocks[j].GetClasses()) : blocks[j].name;
                    Check(label+" no HUD overlap: "+a+" / "+b,!Overlaps(blocks[i].worldBound,blocks[j].worldBound));
                }
            }
            private IEnumerator Start()
            {
                yield return null; yield return null;
                _app = FindFirstObjectByType<AeroSimApplication>();
                string evidenceManifestPath = Argument("--export-evidence-manifest");
                if (evidenceManifestPath != null)
                {
                    Rd01Manifest.WriteRuntimeSnapshot(evidenceManifestPath, Argument("--build-id"), Argument("--source-commit"));
                    Check("RD-01 runtime evidence manifest exported", File.Exists(evidenceManifestPath));
                }
                if (Environment.GetCommandLineArgs().Contains("--smoke-test")) yield return SmokeTest();
                if (Environment.GetCommandLineArgs().Contains("--capture-hero-cliff"))
                {
                    string heroCliffDirectory = Argument("--capture-hero-cliff");
                    if (string.IsNullOrWhiteSpace(heroCliffDirectory)) Check("--capture-hero-cliff requires an output directory", false);
                    else yield return CaptureHeroCliff(heroCliffDirectory);
                }
                if (Argument("--capture-coast-matrix") != null) yield return CaptureCoastMatrix(Argument("--capture-coast-matrix"));
                if (Argument("--capture-visual-lab") != null) yield return CaptureVisualLab(Argument("--capture-visual-lab"));
                if (Argument("--capture-graphics") != null) yield return CaptureGraphics(Argument("--capture-graphics"));
                if (Argument("--capture-aircraft") != null) yield return CaptureAircraft(Argument("--capture-aircraft"));
                string dir = Argument("--capture-all");
                if (dir != null) yield return CaptureAll(dir);
                else if (Argument("--capture-preview") != null)
                {
                    string page = Argument("--page"); if (Enum.TryParse<Page>(page, out var destination)) _app.PreparePreview(destination);
                    if (Argument("--workspace") != null) { _app.ChangeWorkspace(Argument("--workspace")); if (page != null && Enum.TryParse<Page>(page, out destination)) _app.PreparePreview(destination); }
                    yield return null; yield return null; yield return Capture(Argument("--capture-preview"));
                }
                yield return null;yield return null;
                Check("No runtime errors or exceptions",_runtimeErrors==0);
                if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
                if (_mouse != null) InputSystem.RemoveDevice(_mouse);
                var resultFile = Argument("--test-results");
                if (resultFile != null) { Directory.CreateDirectory(Path.GetDirectoryName(resultFile)); File.WriteAllLines(resultFile, _results.Concat(new[] { "Failures: " + _failures })); }
                Application.Quit(_failures == 0 ? 0 : 2);
            }
            private IEnumerator Capture(string path)
            {
                yield return new WaitForEndOfFrame();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var texture = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(path, texture.EncodeToPNG()); Destroy(texture);
                Debug.Log("Captured: " + path);
            }
            private IEnumerator Click(string selector)
            {
                if (_mouse == null) _mouse=InputSystem.AddDevice<Mouse>("AeroSimCaptureMouse");
                var root = _app.GetComponent<UIDocument>().rootVisualElement;
                var buttons = root.Query<Button>().ToList();
                var button = buttons.Find(b =>
                    b.name == selector || b.text == selector ||
                    (selector == "Start Session" && b.name == "preflight-start-session") ||
                    (selector == "hangar-filter-all" && (b.text == "All" || b.text == "✓ All")) ||
                    b.Q<Label>(className: "design-button-label")?.text == selector);
                if (button == null && selector.StartsWith("hangar-select-", StringComparison.Ordinal))
                {
                    string profileId = selector.Substring("hangar-select-".Length);
                    var card = root.Q<VisualElement>("aircraft-card-" + profileId);
                    button = card?.Query<Button>().ToList().FirstOrDefault();
                }
                if (button == null) { Check("Button exists: " + selector, false); yield break; }
                var scroll = button.GetFirstAncestorOfType<ScrollView>();
                scroll?.ScrollTo(button); yield return null; yield return null;

                // v4 pages can place actions in a ScrollView or modal ScrollView. Click the
                // visible, pickable part of the stable target instead of a stale world-bound
                // centre that may now sit behind a viewport clip after responsive relayout.
                Rect visible = button.worldBound;
                for (VisualElement ancestor = button.parent; ancestor != null; ancestor = ancestor.parent)
                {
                    if (ancestor is ScrollView ancestorScroll)
                        visible = Intersect(visible, ancestorScroll.contentViewport.worldBound);
                }
                visible = Intersect(visible, root.worldBound);
                if (visible.width <= 0 || visible.height <= 0)
                {
                    Check("Button is visible: " + selector, false);
                    yield break;
                }

                var point = visible.center;
                var picked = root.panel?.Pick(point);
                if (picked == null || (picked != button && !button.Contains(picked)))
                {
                    Check("Button is pointer-pickable: " + selector, false);
                    yield break;
                }
                var rootBound = root.worldBound;
                var position = new Vector2(
                    (point.x - rootBound.xMin) * Screen.width / Mathf.Max(1, rootBound.width),
                    Screen.height - (point.y - rootBound.yMin) * Screen.height / Mathf.Max(1, rootBound.height));
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }); yield return null;
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = position, buttons = 1 }); yield return null; yield return null;
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }); yield return null; yield return null;
            }
            private static Rect Intersect(Rect left, Rect right)
            {
                float xMin = Mathf.Max(left.xMin, right.xMin), yMin = Mathf.Max(left.yMin, right.yMin);
                float xMax = Mathf.Min(left.xMax, right.xMax), yMax = Mathf.Min(left.yMax, right.yMax);
                return Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
            }
            private IEnumerator SmokeTest()
            {
                var rd01 = Rd01Manifest.Current;
                var rd01Errors = Rd01Manifest.Validate(rd01);
                Check("RD-01 manifest schema and non-claim states are valid", rd01Errors.Count == 0);
                Check("RD-01 freezes one replay plus G1/G2/G3 case matrices",
                    Rd01Manifest.Cases(rd01).Count() == 35 &&
                    rd01.fixedReplay.caseId == "RPL-MAIN-001" &&
                    rd01.goldenShotCases.Select(value => value.caseId).SequenceEqual(new[] { "G3-GOLDEN-A", "G3-GOLDEN-B", "G3-GOLDEN-C", "G3-GOLDEN-D", "G3-GOLDEN-E", "G3-GOLDEN-F", "G3-GOLDEN-G", "G3-GOLDEN-H" }));
                Check("RD-01 ADR keeps mainline Built-in with migration not approved",
                    rd01.renderPipelineAdr.mainlinePipeline == "Built-in" && rd01.renderPipelineAdr.migrationApproval == "NOT_APPROVED" &&
                    rd01.renderPipelineAdr.spikes.Length == 3 && rd01.renderPipelineAdr.spikes.All(value => value.status == "NOT_RUN") &&
                    GraphicsSettings.currentRenderPipeline == null);
                Check("RD-01 declares candidate hardware without performance claim",
                    rd01.targetHardware.status == "CANDIDATE_NOT_BENCHMARKED" && rd01.targetHardware.performanceEvidence == "NOT_RUN" &&
                    rd01.targetHardware.resolution == "2560x1440 native" && rd01.targetHardware.quality == "High" && rd01.targetHardware.targetFps == 60);
                Check("RD-02 versioned manifest validates", AircraftAssetContracts.Validate(out _));
                Check("Four DJI-reference visuals are Programmatic Prototype / Unverified",
                    new[] { "mini4", "air3s", "mavic3p", "avata2" }.All(id =>
                    {
                        var asset = AircraftAssetContracts.Resolve(id);
                        return asset != null && asset.IdentityState == AssetIdentityState.Unverified && asset.QualityState == AssetQualityState.ProgrammaticPrototype;
                    }));
                Check("Racer and Micro identities are fixed to AeroSim Generic",
                    new[] { "racer", "micro" }.All(id => AircraftAssetContracts.Resolve(id)?.IdentityState == AssetIdentityState.AeroSimGeneric));
                Check("RD-02 state machines reject direct production promotion",
                    !AircraftAssetStateMachine.CanTransition(AssetIdentityState.Unverified, AssetIdentityState.ProductionApproved) &&
                    !AircraftAssetStateMachine.CanTransition(AssetQualityState.ProgrammaticPrototype, AssetQualityState.ProductionReady));
                Check("RD-05 consumes RD-06 truth without promoting missing production content",
                    AircraftCatalog.All.Where(profile => profile.Status == AircraftCatalogStatus.Runtime).All(profile =>
                    {
                        var load = ProductionAssetLoader.Inspect(profile.Id);
                        return load.State == ProductionAssetLoadState.ContentNotDelivered &&
                            load.Message.Contains(ProductionAssetLoader.ContentNotDelivered) && !profile.CanClaimModelIdentity;
                    }));
                Check("RD-05 exposes exactly 12 manifest-backed inspection entries per runtime aircraft",
                    AircraftCatalog.All.Where(profile => profile.Status == AircraftCatalogStatus.Runtime).All(profile =>
                    {
                        var package = GoldenAngleCaptureManifest.Build(profile.Id);
                        return GoldenAngleCaptureManifest.Validate(package, out _) && package.captureCases.Length == 12 &&
                            package.captureCases.All(capture => capture.captureStatus == GoldenAngleCaptureManifest.NotCaptured);
                    }));
                Check("Diagnostics uses isolated storage", Argument("--data-dir") != null);
                var candidateIds = new[] { "dji-mavic-4-pro", "dji-matrice-350-rtk", "dji-agras-t50", "dji-inspire-3" };
                Check("Aircraft catalog contains ten entries with six runtime aircraft",
                    AircraftCatalog.All.Length == 10 &&
                    AircraftCatalog.All.Select(profile => profile.CatalogId).Distinct().Count() == 10 &&
                    AircraftCatalog.All.Count(profile => profile.Status == AircraftCatalogStatus.Runtime) == 6);
                foreach (string candidateId in candidateIds)
                {
                    var candidate = AircraftCatalog.All.FirstOrDefault(profile => profile.Id == candidateId);
                    Check("Candidate aircraft is locked from flight: " + candidateId,
                        candidate != null &&
                        (candidate.Status == AircraftCatalogStatus.Candidate || candidate.Status == AircraftCatalogStatus.Locked) &&
                        !candidate.IsSelectable && !string.IsNullOrWhiteSpace(candidate.IncompatibleReasonKey));
                }
                LocalStore.Profile.selectedAircraft = "removed-smoke-aircraft";
                LocalStore.Save();
                Check("Unavailable selected aircraft falls back with migration evidence",
                    AircraftCatalog.Selected.Id == "mini4" && LocalStore.SelectedAircraftNeedsConfirmation &&
                    LocalStore.Profile.selectedAircraftFallbackFrom == "removed-smoke-aircraft");
                LocalStore.Profile.selectedAircraft = "air3s";
                LocalStore.Save(); LocalStore.Initialize();
                Check("Selected aircraft and version metadata persist across reload",
                    LocalStore.Profile.selectedAircraft == "dji-air-3s" && AircraftCatalog.Selected.Id == "air3s" &&
                    !LocalStore.SelectedAircraftNeedsConfirmation &&
                    !string.IsNullOrEmpty(LocalStore.Profile.selectedAircraftCatalogVersion) &&
                    !string.IsNullOrEmpty(LocalStore.Profile.selectedAircraftVisualVersion) &&
                    !string.IsNullOrEmpty(LocalStore.Profile.selectedAircraftPhysicsVersion));

                var definitions = FovViewDefinitions.All;
                var expectedFovContracts = new Dictionary<string, Vector3>
                {
                    { "fpv", new Vector3(45,95,60) }, { "cockpit", new Vector3(35,85,55) },
                    { "gimbal", new Vector3(30,65,45) }, { "near-follow", new Vector3(50,100,70) },
                    { "far-follow", new Vector3(45,95,65) }, { "external", new Vector3(35,100,60) }
                };
                var expectedFovPresets = new Dictionary<string, float[]>
                {
                    { "fpv", new[] { 50f,60f,75f } }, { "cockpit", new[] { 45f,55f,70f } },
                    { "gimbal", new[] { 35f,45f,55f } }, { "near-follow", new[] { 60f,70f,85f } },
                    { "far-follow", new[] { 55f,65f,80f } }, { "external", new[] { 45f,60f,80f } }
                };
                Check("FOV has six versioned view definitions with fixed PRD boundaries",
                    definitions.Length == 6 && definitions.All(value => value.schemaVersion == FovViewDefinitions.CurrentVersion) &&
                    definitions.Select(value => value.viewId).Distinct().Count() == 6 &&
                    definitions.All(value => expectedFovContracts.ContainsKey(value.viewId) &&
                        Mathf.Approximately(value.minimumDeg, expectedFovContracts[value.viewId].x) &&
                        Mathf.Approximately(value.maximumDeg, expectedFovContracts[value.viewId].y) &&
                        Mathf.Approximately(value.defaultDeg, expectedFovContracts[value.viewId].z) &&
                        value.presetsDeg.SequenceEqual(expectedFovPresets[value.viewId])));
                bool legacyCameraMigration = expectedFovContracts.Keys.All(viewId =>
                    CameraStateCatalog.TryFromLegacyViewId(viewId, CameraContext.LiveFlight, out var state) &&
                    CameraStateCatalog.LegacyViewId(state) == viewId);
                CameraStateCatalog.TryFromLegacyViewId("chase", CameraContext.LiveFlight, out var legacyChase);
                Check("RD-03 camera catalog merges legacy six-view migration and capability semantics",
                    CameraStateCatalog.SchemaVersion == 1 && CameraStateCatalog.All.Count == 12 && legacyCameraMigration &&
                    legacyChase.Family == CameraFamily.Follow && legacyChase.Preset == CameraPreset.Near &&
                    CameraStateCatalog.Get(CameraFamily.VideoFpv, CameraPreset.Video).Capability == CameraCapability.Locked &&
                    CameraStateCatalog.Get(CameraFamily.NosePilot, CameraPreset.Nose).Binding == CameraBinding.AircraftSocket &&
                    CameraStateCatalog.Get(CameraFamily.Follow, CameraPreset.Near).Binding == CameraBinding.TargetRelative &&
                    CameraStateCatalog.Get(CameraFamily.Observer, CameraPreset.Fixed).Binding == CameraBinding.World);
                bool formulaValid = true;
                foreach (float vertical in new[] { 30f, 35f, 45f, 60f, 80f, 100f })
                    foreach (float aspect in new[] { 16f / 9f, 21f / 9f, 32f / 9f })
                    {
                        float horizontal = FlightSimulator.VerticalToHorizontalFov(vertical, aspect);
                        float restored = 2f * Mathf.Atan(Mathf.Tan(horizontal * Mathf.Deg2Rad * .5f) / aspect) * Mathf.Rad2Deg;
                        formulaValid &= Mathf.Abs(restored - vertical) <= .1f;
                    }
                Check("FOV vertical-horizontal formula round-trips across supported aspects", formulaValid);

                var originalFov = definitions.ToDictionary(value => value.viewId, value => LocalStore.GetVerticalFov(value.viewId));
                for (int i = 0; i < definitions.Length; i++)
                    LocalStore.SetVerticalFov(definitions[i].viewId, definitions[i].presetsDeg[i % 3], "preset", 16f / 9f);
                LocalStore.Save();
                var isolatedFov = definitions.ToDictionary(value => value.viewId, value => LocalStore.GetVerticalFov(value.viewId));
                float untouchedCockpit = isolatedFov["cockpit"];
                LocalStore.SetVerticalFov("fpv", definitions.First(value => value.viewId == "fpv").minimumDeg, "custom", 16f / 9f);
                Check("Six FOV views persist independently without cross-talk",
                    definitions.All(value => Mathf.Approximately(isolatedFov[value.viewId], value.presetsDeg[Array.IndexOf(definitions, value) % 3])) &&
                    Mathf.Approximately(LocalStore.GetVerticalFov("cockpit"), untouchedCockpit));

                LocalStore.Profile.schemaVersion = 3;
                LocalStore.Profile.settings.cameraFov.Clear();
                LocalStore.Profile.settings.horizontalFov = 90;
                LocalStore.Profile.settings.fovAspect = 16f / 9f;
                LocalStore.Save();
                float expectedMigrated = 2f * Mathf.Atan(Mathf.Tan(45f * Mathf.Deg2Rad) / (16f / 9f)) * Mathf.Rad2Deg;
                Check("Legacy horizontal FOV migrates once into six vertical records",
                    LocalStore.Profile.schemaVersion == 4 && LocalStore.Profile.settings.cameraFov.Count == 6 &&
                    definitions.All(value => Mathf.Abs(LocalStore.GetVerticalFov(value.viewId) - expectedMigrated) <= .1f) &&
                    LocalStore.Profile.settings.horizontalFov == 0 && LocalStore.Profile.settings.fovAspect == 0);

                int diagnosticsBeforeInvalid = LocalStore.FovDiagnostics.Count;
                LocalStore.Profile.settings.cameraFov.Find(value => value.viewId == "fpv").valueDeg = float.NaN;
                LocalStore.Profile.settings.cameraFov.Find(value => value.viewId == "near-follow").valueDeg = 1000;
                LocalStore.Save();
                Check("Illegal FOV values recover per-view defaults with runtime diagnostics",
                    Mathf.Approximately(LocalStore.GetVerticalFov("fpv"), LocalStore.DefaultVerticalFov("fpv")) &&
                    Mathf.Approximately(LocalStore.GetVerticalFov("near-follow"), LocalStore.DefaultVerticalFov("near-follow")) &&
                    LocalStore.FovDiagnostics.Count >= diagnosticsBeforeInvalid + 2);

                foreach (var definition in definitions) LocalStore.SetVerticalFov(definition.viewId, originalFov[definition.viewId], "custom", 16f / 9f);
                LocalStore.Save();
                _app.DiagnosticBeginFovEdit();
                var familyFovPanel = _app.DiagnosticBuildFovPanel();
                var familySections = familyFovPanel.Query<VisualElement>(className: "fov-view-section").ToList();
                Check("RD-04 FOV transaction exposes six explicit family/preset profiles and unavailable Video FPV truth",
                    familySections.Count == 6 && familySections.All(section => section.userData is CameraState) &&
                    familyFovPanel.Query<Label>(className: "camera-capability-unavailable").ToList().Any(label => label.text.Contains("VIDEO FPV") && label.text.Contains("UNAVAILABLE")));
                _app.DiagnosticCancelFovEdit();
                _app.DiagnosticBeginFovEdit(); _app.DiagnosticSetFovDraft("cockpit", definitions.First(value => value.viewId == "cockpit").presetsDeg[2]); _app.DiagnosticCancelFovEdit();
                Check("FOV Cancel restores the six-view opening snapshot", definitions.All(value => Mathf.Approximately(LocalStore.GetVerticalFov(value.viewId), originalFov[value.viewId])));
                _app.DiagnosticBeginFovEdit();
                foreach (var definition in definitions) _app.DiagnosticSetFovDraft(definition.viewId, definition.presetsDeg[2]);
                _app.DiagnosticApplyFovEdit();
                bool appliedAll = definitions.All(value => Mathf.Approximately(LocalStore.GetVerticalFov(value.viewId), value.presetsDeg[2]));
                bool undoSucceeded = _app.UndoLastFovApply();
                Check("FOV Apply persists six values and 10-second Undo restores all", appliedAll && undoSucceeded && definitions.All(value => Mathf.Approximately(LocalStore.GetVerticalFov(value.viewId), originalFov[value.viewId])));
                _app.DiagnosticBeginFovEdit(); _app.DiagnosticSetFovDraft("external", definitions.First(value => value.viewId == "external").presetsDeg[0]); _app.DiagnosticApplyFovEdit(); _app.DiagnosticExpireFovUndo();
                Check("FOV Undo expires after its 10-second window", !_app.UndoLastFovApply());
                foreach (var definition in definitions) LocalStore.SetVerticalFov(definition.viewId, originalFov[definition.viewId], "custom", 16f / 9f);
                LocalStore.Save();

                var xrProbe = AeroSimApplication.FovXrActiveProbe;
                AeroSimApplication.FovXrActiveProbe = () => true;
                var xrPanel = _app.DiagnosticBuildFovPanel();
                Check("Active XR runtime disables FOV controls and displays ownership reason",
                    xrPanel.Q<Label>(className: "fov-xr-reason") != null &&
                    xrPanel.Query<VisualElement>(className: "fov-view-section").ToList().All(section => !section.enabledInHierarchy) &&
                    xrPanel.Q<Button>("action-apply-fov") != null && !xrPanel.Q<Button>("action-apply-fov").enabledInHierarchy);
                float xrBefore = LocalStore.GetVerticalFov("fpv");
                _app.DiagnosticBeginFovEdit(); _app.DiagnosticSetFovDraft("fpv", definitions.First(value => value.viewId == "fpv").presetsDeg[2]); _app.DiagnosticApplyFovEdit();
                Check("Active XR runtime blocks desktop FOV Apply", AeroSimApplication.IsFovXrControlled && Mathf.Approximately(LocalStore.GetVerticalFov("fpv"), xrBefore));
                _app.DiagnosticCancelFovEdit(); AeroSimApplication.FovXrActiveProbe = xrProbe;

                foreach(var name in new[]{"meadow-grass-v2","coastal-rock-v2","aircraft-shell-v2","evergreen-needles-v2"})
                    Check("Art texture bundled: "+name, Resources.Load<Texture2D>("Art/MaterialsV2/"+name)!=null);
                foreach(var name in new[]{"TrainingSurface","LandscapeSurface","Atmosphere","OceanSurface","RotorBlur","NeedleFoliage","IndustrialSurfaceV1","RiverSurfaceV1","RainStreak","HeroSurfaceV1","CoastalWaterDetail"})
                {
                    var shader=Resources.Load<Shader>(name);Check("Art shader supported: "+name,shader!=null&&shader.isSupported);
                }
                var p11CaptureCatalog = CoastProductionRuntimeCatalog.Load();
                Check("P11 capture CLI contract exposes fixed 5x8 route and supported replacement shader",
                    HeroCliffCaptureRoute.ValidateContract(p11CaptureCatalog?.p11DiagnosticShader, null, out _) &&
                    HeroCliffCaptureRoute.Distances.Length == 5 && HeroCliffCaptureRoute.ChannelNames.Length == 8);

                var seedConfig = new FlightConfig { environment="Pacific Coast", weather="Rain", timeOfDay=16.5f, windMph=8 };
                var deterministicStateA = EnvironmentState.Create(seedConfig);
                var deterministicStateB = EnvironmentState.Create(JsonUtility.FromJson<FlightConfig>(JsonUtility.ToJson(seedConfig)));
                var changedSeedState = EnvironmentState.Create(new FlightConfig { environment="Warehouse", weather="Rain", timeOfDay=16.5f, windMph=8 });
                Check("EnvironmentState v1 is valid and deterministic", deterministicStateA.Validate().Count==0 && deterministicStateA.seed==deterministicStateB.seed && deterministicStateA.seed!=changedSeedState.seed);
                var sectorContracts=EnvironmentSectorContract.CreateGrid(240);
                Check("Sector contract v1 partitions a global sector and deterministic 3x3 grid", sectorContracts.Count==10 && sectorContracts.Select(value=>value.id).Distinct().Count()==10);
                Check("RD-08 slice matrix covers Clear/Overcast/Rain at Low/High/Ultra without claiming scanned assets",
                    FlightWeather.Names.All(weather => new[] { AeroSimQuality.Low, AeroSimQuality.High, AeroSimQuality.Ultra }.All(quality => FlightSimulator.SupportsProceduralSliceVariant(weather, quality))));
                Check("All quality levels expose valid LOD/streaming and Low/Off fallbacks", Enumerable.Range(AeroSimQuality.Low,AeroSimQuality.Names.Length).All(value=>
                {
                    var policy=EnvironmentStreamingPolicy.ForQuality(value);
                    return policy.Validate().Count==0 && policy.unloadDistance>policy.loadDistance &&
                        (value!=AeroSimQuality.Low || policy.vfx==EnvironmentFeatureMode.Off);
                }));

                _app.ShowPage(Page.FreeFlight); yield return null; yield return null;
                var weatherChoice = _app.GetComponent<UIDocument>().rootVisualElement.Query<DropdownField>().ToList().FirstOrDefault(field => field.label == "Weather");
                Check("Free Flight weather UI exposes Clear, Overcast and Rain", weatherChoice != null && FlightWeather.Names.All(weatherChoice.choices.Contains));
                Check("Weather contract contains exactly three deterministic states", FlightWeather.Names.SequenceEqual(new[] { "Clear", "Overcast", "Rain" }));

                int originalQuality = LocalStore.Profile.settings.quality;
                bool fourQualityContracts = AeroSimQuality.Names.SequenceEqual(new[] { "Low", "Medium", "High", "Ultra" });
                for (int quality=0; quality<AeroSimQuality.Names.Length; quality++)
                {
                    LocalStore.Profile.settings.quality=quality; LocalStore.Save(); LocalStore.Initialize();
                    fourQualityContracts &= LocalStore.Profile.settings.quality==quality && AeroSimQuality.Name(quality)==AeroSimQuality.Names[quality];
                    _app.StartFlight(new FlightConfig { environment="Pacific Coast",weather="Rain",windMph=8,title="QA · Rain · "+AeroSimQuality.Name(quality) });
                    yield return null; yield return null;
                    var qualitySim=_app.Simulator;
                    var environmentDiagnostics=qualitySim.EnvironmentDiagnostics;
                    bool environmentContracts=environmentDiagnostics!=null && environmentDiagnostics.IsValid &&
                        environmentDiagnostics.stateSchemaVersion==EnvironmentState.CurrentSchemaVersion && environmentDiagnostics.seed!=0 &&
                        environmentDiagnostics.sectorContracts==10 && environmentDiagnostics.assignedObjects>0 && environmentDiagnostics.materialContracts>0 &&
                        environmentDiagnostics.features.Count==6 && qualitySim.EnvironmentState.seed==qualitySim.Config.environmentSeed &&
                        qualitySim.Record.environmentState.seed==qualitySim.Config.environmentSeed && qualitySim.ActiveEnvironmentAudioSnapshot==qualitySim.EnvironmentState.audioSnapshot;
                    Check("Environment production contracts: "+AeroSimQuality.Name(quality),environmentContracts);
                    var slice=qualitySim.VerticalSliceDiagnostics;
                    bool sliceContracts=slice!=null && slice.IsValid && slice.label==ProceduralVerticalSliceDiagnostics.RequiredLabel &&
                        slice.heroZones==3 && slice.goldenViewStates==15 && slice.lodBands==5 && slice.physicalColliders>=12 && slice.readableLandmarks>=3 &&
                        slice.materialSemantics.Distinct().Count()>=9 &&
                        qualitySim.EnvironmentGoldenViews.All(value=>!string.IsNullOrWhiteSpace(value.reviewBand)) &&
                        qualitySim.EnvironmentGoldenViews.Select(value=>value.distanceMeters).Distinct().OrderBy(value=>value).SequenceEqual(new[]{1f,5f,15f,40f,80f});
                    Check("RD-08 procedural hero zones and golden distances: "+AeroSimQuality.Name(quality),sliceContracts);
                    bool highDetail=qualitySim.EnvironmentKeywordActive("AEROSIM_LANDSCAPE_DETAIL") && qualitySim.EnvironmentKeywordActive("AEROSIM_WATER_HIGH");
                    bool ultraDetail=qualitySim.EnvironmentKeywordActive("AEROSIM_LANDSCAPE_ULTRA") && qualitySim.EnvironmentKeywordActive("AEROSIM_WATER_ULTRA") && qualitySim.EnvironmentKeywordActive("AEROSIM_SKY_ULTRA");
                    bool levelContract=qualitySim.EnvironmentQuality==quality && qualitySim.Config.weather=="Rain" && qualitySim.Record.config.weather=="Rain" &&
                        qualitySim.RainVisualActive==(quality>AeroSimQuality.Low) && highDetail==(quality>=AeroSimQuality.High) && ultraDetail==(quality==AeroSimQuality.Ultra) &&
                        (quality!=AeroSimQuality.Low || !qualitySim.View.allowMSAA) && (quality!=AeroSimQuality.Ultra || qualitySim.View.farClipPlane>=1750);
                    Check("Rain rendering contract: "+AeroSimQuality.Name(quality),levelContract);
                    if (quality == AeroSimQuality.Low || quality == AeroSimQuality.High || quality == AeroSimQuality.Ultra)
                    {
                        var coast = qualitySim.CoastProductionStatus;
                        Check("P01-P11 candidate assets bound to runtime: "+AeroSimQuality.Name(quality), coast != null && coast.candidateBindingsComplete &&
                            coast.provenance == "CAPTURED_UNREVIEWED / NOT SCANNED" && coast.packageIds.SequenceEqual(new[] { "P01", "P02", "P03", "P04", "P05", "P06", "P07", "P08", "P09", "P10", "P11" }) &&
                            coast.boundTextures >= 9 && (coast.p11Loaded || coast.instantiatedP01Meshes >= 1) && coast.lookDevProfile == "P09-LD-03-RAIN-EXPOSURE");
                        Check("P11 Hero Cliff validated or conservative P01 fallback active: "+AeroSimQuality.Name(quality), coast != null &&
                            ((coast.p11Loaded && coast.p11LoadState.Contains("P11_VALIDATED_GENERATED_UNREVIEWED")) || (!coast.p11Loaded && coast.instantiatedP01Meshes >= 1)));
                        var captureTarget = FindFirstObjectByType<HeroCliffP11Metadata>();
                        Check("P11 capture target and material diagnostic contract: "+AeroSimQuality.Name(quality),
                            coast != null && coast.p11Loaded && captureTarget != null &&
                            HeroCliffCaptureRoute.ValidateContract(p11CaptureCatalog?.p11DiagnosticShader, captureTarget, out _));
                    }
                    if (quality>AeroSimQuality.Low)
                    {
                        Check("Rain particles play: "+AeroSimQuality.Name(quality),qualitySim.RainParticlesPlaying);
                        Check("Rain velocity curves share mode and preserve wind/downfall direction: "+AeroSimQuality.Name(quality),
                            qualitySim.RainVelocityCurvesConsistent && qualitySim.RainVelocityDirectionValid);
                    }
                    fourQualityContracts &= levelContract && environmentContracts;
                    if (quality==AeroSimQuality.Ultra)
                    {
                        var rainRecord=qualitySim.Record;
                        rainRecord.frames.Add(new TelemetryFrame { time=0,position=qualitySim.Position,rotation=qualitySim.Body.rotation.eulerAngles,battery=qualitySim.Battery });
                        _app.BeginReplay(rainRecord); yield return null;
                        bool replayWeather=_app.Simulator.ReplayMode && _app.Simulator.Config.weather=="Rain" && _app.Simulator.Record.config.weather=="Rain";
                        Check("Rain config and record survive replay reconstruction",replayWeather); fourQualityContracts &= replayWeather;
                    }
                }
                Check("Particle velocity mode warning is absent", _particleVelocityWarnings==0);
                LocalStore.Profile.settings.quality=99; LocalStore.Save(); fourQualityContracts &= LocalStore.Profile.settings.quality==AeroSimQuality.Ultra;
                LocalStore.Profile.settings.quality=-7; LocalStore.Save(); fourQualityContracts &= LocalStore.Profile.settings.quality==AeroSimQuality.Low;
                Check("Four quality levels persist and drive distinct rendering contracts", fourQualityContracts);
                Check("RD-09 fixed shot matrix exposes A-H and 1/5/15/40/80m",
                    VisualLabEvidence.ShotIds.SequenceEqual(new[] { "A", "B", "C", "D", "E", "F", "G", "H" }) &&
                    VisualLabEvidence.Distances.SequenceEqual(new[] { 1f, 5f, 15f, 40f, 80f }));
                var rd09Draft = VisualLabEvidence.CreateManifest(null, new VisualLabPerformanceSummary { status = "NOT_RUN" }, null, null);
                Check("RD-09 draft links RD-01 and never claims an unrun performance PASS",
                    rd09Draft.rd01ManifestId == rd01.manifestId && rd09Draft.rd01SourceCommit == rd01.sourceBaseline.headCommit &&
                    rd09Draft.status == "INCOMPLETE_EVIDENCE" && rd09Draft.gateStatus == "NOT_RUN" && rd09Draft.performance.gateStatus == "NOT_RUN");
                LocalStore.Profile.settings.quality=originalQuality; LocalStore.Save(); LocalStore.Initialize(); _app.ShowPage(Page.Home); yield return null;

                _keyboard = InputSystem.AddDevice<Keyboard>("AeroSimTestKeyboard"); _mouse = InputSystem.AddDevice<Mouse>("AeroSimTestMouse");
                foreach (Page page in Enum.GetValues(typeof(Page)))
                {
                    if (page == Page.Flight || page == Page.Replay || page == Page.Report || page == Page.Results) continue;
                    _app.PreparePreview(page); yield return null;
                    Check("Route renders: " + page, _app.GetComponent<UIDocument>().rootVisualElement.Query<Button>().ToList().Count > 0);
                }
                _app.ShowPage(Page.Hangar); yield return null; yield return null;
                var bayRoot = _app.GetComponent<UIDocument>().rootVisualElement;
                Check("RD-05 Aircraft Bay exposes honest identity and missing-content state",
                    bayRoot.Query<Label>().ToList().Any(label => label.text == "PROGRAMMATIC PROTOTYPE / UNVERIFIED") &&
                    bayRoot.Query<Label>().ToList().Any(label => label.text == "CONTENT NOT DELIVERED"));
                Check("RD-05 Aircraft Bay exposes Quality Rail and 12 golden-angle entries",
                    bayRoot.Query<Label>().ToList().Any(label => label.text == "Quality Rail") &&
                    bayRoot.Query<Button>().ToList().Count(button => button.name != null && button.name.StartsWith("golden-angle-", StringComparison.Ordinal)) == 12);
                Check("RD-05 production-only inspection modes stay disabled without formal content",
                    new[] { "clay", "wireframe", "normal", "roughness", "ao", "collision", "lod" }.All(mode =>
                    {
                        var button = bayRoot.Q<Button>("inspect-mode-" + mode);
                        return button != null && !button.enabledInHierarchy;
                    }));
                _app.ShowPage(Page.FreeFlight); yield return null; yield return null;
                yield return Click("action-launch-free-flight"); Check("Pointer click launches real flight", _app.CurrentPage == Page.Flight);
                if (_app.CurrentPage != Page.Flight) _app.StartFlight(new FlightConfig());
                yield return null; yield return null; AuditFlightHudGeometry("Smoke Flight");
                var flightRoot = _app.GetComponent<UIDocument>().rootVisualElement;
                Check("Flight exposes left and right touch controls",
                    flightRoot.Q<VisualElement>(className: "stick-left") != null &&
                    flightRoot.Q<VisualElement>(className: "stick-right") != null &&
                    flightRoot.Query<VisualElement>(className: "touch-stick").ToList().Count >= 2);
                Check("Touch pointer owner contract rejects third and cross-stick pointers", _app.RunTouchPointerContractDiagnostic());
                var sim = _app.Simulator;
                var persistedBeforeCameraPreview = definitions.ToDictionary(value => value.viewId, value => LocalStore.GetVerticalFov(value.viewId));
                var cameraPositions = new List<Vector3>();
                var cameraRotations = new List<Quaternion>();
                bool sixViewsSwitchInOneFrame = true;
                foreach (var definition in definitions)
                {
                    float previewFov = definition.presetsDeg[2];
                    bool switched = sim.PreviewCameraView(definition.viewId, previewFov);
                    yield return null;
                    bool distinctPose = true;
                    for (int i = 0; i < cameraPositions.Count; i++)
                        distinctPose &= Vector3.Distance(sim.View.transform.position, cameraPositions[i]) > .001f ||
                            Quaternion.Angle(sim.View.transform.rotation, cameraRotations[i]) > .01f;
                    sixViewsSwitchInOneFrame &= switched && sim.ActiveFovViewId == definition.viewId &&
                        Mathf.Approximately(sim.View.fieldOfView, previewFov) && distinctPose;
                    cameraPositions.Add(sim.View.transform.position); cameraRotations.Add(sim.View.transform.rotation);
                }
                bool previewDidNotCrossWrite = definitions.All(value => Mathf.Approximately(LocalStore.GetVerticalFov(value.viewId), persistedBeforeCameraPreview[value.viewId]));
                Check("All six camera views switch pose/viewId/FOV within one frame without persistence cross-talk", sixViewsSwitchInOneFrame && previewDidNotCrossWrite);

                bool noseSelected = sim.SetCameraState(CameraFamily.NosePilot, CameraPreset.Nose, CameraContext.LiveFlight);
                var aircraftModel = sim.Body.GetComponentInChildren<AircraftVisualModel>();
                var noseSocket = aircraftModel != null && aircraftModel.CameraSockets.TryGetValue("flight-camera", out var socket) ? socket : null;
                Check("RD-03 Nose is a true aircraft-socket first-person camera",
                    noseSelected && sim.CameraUsesAircraftSocket && noseSocket != null &&
                    Vector3.Distance(sim.View.transform.position, noseSocket.position) < .001f &&
                    Quaternion.Angle(sim.View.transform.rotation, noseSocket.rotation) < .01f);
                Check("RD-03 unavailable Video FPV remains explicit and cannot masquerade as Nose",
                    !sim.SetCameraState(CameraFamily.VideoFpv, CameraPreset.Video, CameraContext.LiveFlight) &&
                    sim.CameraState.Family == CameraFamily.NosePilot &&
                    CameraStateCatalog.Get(CameraFamily.VideoFpv, CameraPreset.Video).UnavailableReason.Contains("not implemented"));
                FlightInput.TouchLeft = new Vector2(.8f, .6f); FlightInput.TouchRight = new Vector2(-.7f, .5f);
                bool mountSelected = sim.SetCameraState(CameraFamily.Mount, CameraPreset.HighMount, CameraContext.LiveFlight);
                Check("RD-04 camera transition atomically clears four axes and requires neutral",
                    mountSelected && sim.CurrentInput == Vector4.zero && FlightInput.TouchLeft == Vector2.zero &&
                    FlightInput.TouchRight == Vector2.zero && FlightInput.WaitingForNeutral);
                sim.SetCameraState(CameraFamily.NosePilot, CameraPreset.Nose, CameraContext.LiveFlight);
                yield return Click("camera-cycle");
                var cameraDock = _app.GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("camera-dock");
                var cameraDockState = cameraDock?.Q<Label>(className: "v1-camera-dock-state");
                var cameraDockFov = cameraDock?.Q<Label>(className: "v1-camera-dock-fov");
                string expectedDockFov = sim.VerticalFieldOfView.ToString("F1") + "° v  ·  " + sim.HorizontalFieldOfView.ToString("F1") + "° h";
                Check("RD-04 Camera Dock exposes family/preset and live v/hFOV",
                    sim.CameraState.Family == CameraFamily.Mount && sim.CameraState.Preset == CameraPreset.HighMount &&
                    cameraDockState?.text == "MOUNT · HIGH" && cameraDockFov?.text == expectedDockFov);

                sim.SetCameraState(CameraFamily.Follow, CameraPreset.Near, CameraContext.LiveFlight);
                Vector3 unobstructedFollow = sim.View.transform.position;
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.name = "RD-03 camera collision probe";
                blocker.transform.position = Vector3.Lerp(sim.Position + Vector3.up * .1f, unobstructedFollow, .55f);
                blocker.transform.localScale = Vector3.one * 1.2f;
                Physics.SyncTransforms(); sim.ApplyCameraViewPose(true);
                Check("RD-03 Follow collision pulls the camera in and reports occlusion",
                    sim.CameraObstructed && sim.CameraVisibleFraction < 1 &&
                    Vector3.Distance(sim.View.transform.position, sim.Position) < Vector3.Distance(unobstructedFollow, sim.Position));
                Destroy(blocker); yield return null; Physics.SyncTransforms();

                sim.Pause(true);
                sim.SetCameraState(CameraFamily.Observer, CameraPreset.Fixed, CameraContext.LiveFlight);
                Vector3 observerBefore = sim.View.transform.position;
                sim.MoveObserver(new Vector3(2, 1, -1));
                Vector3 observerPaused = sim.View.transform.position;
                sim.Pause(false); sim.MoveObserver(Vector3.one);
                Check("RD-03 Observer is world-bound and only owns movement while paused or replaying",
                    sim.CameraIsWorldObserver && Vector3.Distance(observerPaused, observerBefore + new Vector3(2, 1, -1)) < .001f &&
                    Vector3.Distance(sim.View.transform.position, observerPaused) < .001f);
                sim.SetCameraState(CameraFamily.Follow, CameraPreset.Near, CameraContext.LiveFlight);

                sim.SetCameraView("near-follow");
                Vector3 openingCameraPosition = sim.View.transform.position; Quaternion openingCameraRotation = sim.View.transform.rotation;
                _app.DiagnosticBeginFovEdit(); sim.PreviewCameraView("external", definitions.First(value => value.viewId == "external").presetsDeg[0]);
                _app.DiagnosticCancelFovEdit(); yield return null;
                Check("FOV Cancel restores opening camera view and pose snapshot",
                    sim.ActiveFovViewId == "near-follow" && Vector3.Distance(sim.View.transform.position, openingCameraPosition) < .001f &&
                    Quaternion.Angle(sim.View.transform.rotation, openingCameraRotation) < .01f &&
                    Mathf.Approximately(sim.View.fieldOfView, persistedBeforeCameraPreview["near-follow"]));
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.R)); yield return new WaitForSeconds(1.5f);
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); yield return null;
                Check("Keyboard throttle moves aircraft", sim.Position.y > 4);
                sim.Pause(true); Vector3 frozen = sim.Position; float time = sim.Elapsed; yield return new WaitForSeconds(.4f);
                Check("Pause freezes physics and clock", Vector3.Distance(sim.Position, frozen) < .001f && sim.Elapsed == time); sim.Pause(false);
                yield return Click("action-pause"); Check("Pause button freezes session", sim.Paused);
                yield return Click("action-resume-flight"); Check("Resume button unfreezes session", !sim.Paused);
                var gamepad = InputSystem.AddDevice<Gamepad>("AeroSimTestGamepad");
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.7f, .9f), rightStick = new Vector2(-.6f, .8f) }); yield return null; yield return null;
                var input = FlightInput.Raw(); Check("Gamepad four-channel input", input.x > .5f && input.y > .4f && input.z > .5f && input.w < -.3f);
                InputSystem.RemoveDevice(gamepad); Check("Controller disconnect pauses flight", sim.Paused);
                FlightInput.TouchLeft = new Vector2(.5f, .8f); FlightInput.TouchRight = new Vector2(-.5f, .7f);
                input = FlightInput.Raw(); Check("Touch four-channel input", input.x > .7f && input.y > .4f && input.z > .6f && input.w < -.4f);
                FlightInput.TouchLeft = FlightInput.TouchRight = Vector2.zero;
                _app.StartFlight(new FlightConfig {
                    task = "gates", courseId = "test-gates", title = "QA · Gate circuit",
                    aircraft = LocalStore.Profile.selectedAircraft
                }); yield return null;
                sim = _app.Simulator;
                var gates = new[] { new Vector3(0,8,25), new Vector3(20,10,50), new Vector3(30,12,85), new Vector3(0,10,110), new Vector3(-25,9,80), new Vector3(-15,8,40) };
                foreach (var position in gates) { sim.Body.position = position; sim.Body.linearVelocity = Vector3.zero; yield return new WaitForSeconds(.15f); }
                Check("Ordered gate objective completes", sim.GateIndex == 6 && sim.ObjectiveComplete);
                sim.Collision(8); Check("Collision updates score evidence", sim.Record.collisions == 1);
                sim.Finish(); yield return null;
                var r = LocalStore.Sessions().FirstOrDefault();
                Check("Session persists with score and telemetry", r != null && r.score == 92 && r.passed && r.frames.Count > 0);
                Check("Session persists an aircraft identity and version snapshot",
                    r?.aircraftSnapshot != null && r.aircraftSnapshot.catalogId == "dji-air-3s" &&
                    r.aircraftSnapshot.displayName == AircraftCatalog.Resolve("air3s").DisplayName && r.aircraftSnapshot.availableAtCapture &&
                    r.aircraftSnapshot.assetManifestVersion == AircraftAssetContracts.Manifest.manifestVersion &&
                    r.aircraftSnapshot.assetIdentityState == "Unverified" && r.aircraftSnapshot.assetQualityState == "ProgrammaticPrototype" &&
                    !string.IsNullOrEmpty(r.aircraftSnapshot.catalogVersion) &&
                    !string.IsNullOrEmpty(r.aircraftSnapshot.visualVersion) &&
                    !string.IsNullOrEmpty(r.aircraftSnapshot.physicsVersion) && r.aircraftSnapshot.capabilities.Count > 0);
                Check("Result screen is shown", _app.CurrentPage == Page.Results);
                if (r != null)
                {
                    _app.BeginReplay(r); sim = _app.Simulator; sim.Seek(r.duration * .5f); yield return null;
                    Check("Replay seeks into actual telemetry", sim.ReplayMode && Mathf.Abs(sim.ReplayTime - r.duration * .5f) < .01f);
                    bool recordedCameraAvailable = sim.SetReplayCameraSource(ReplayCameraSource.Recorded);
                    var replayFrame = r.frames.LastOrDefault(frame => frame.time <= sim.ReplayTime && frame.cameraSnapshot?.IsValid == true);
                    Check("RD-03 Replay distinguishes and restores Recorded Camera snapshots",
                        recordedCameraAvailable && sim.ReplayCamera == ReplayCameraSource.Recorded && replayFrame != null &&
                        sim.CameraState.Context == CameraContext.Replay &&
                        Vector3.Distance(sim.View.transform.position, replayFrame.cameraSnapshot.Position) < .001f);
                    sim.Pause(true); sim.Pause(false);
                    Check("RD-03 replay remains kinematic across pause/resume", sim.Body.isKinematic);
                    Check("RD-03 Analysis Camera selection is explicit", sim.SetReplayCameraSource(ReplayCameraSource.Analysis) && sim.ReplayCamera == ReplayCameraSource.Analysis);
                    sim.ReplayPlaying = true; float previous = sim.ReplayTime; yield return new WaitForSeconds(.2f); Check("Replay advances", sim.ReplayTime > previous);
                    var csv = LocalStore.ExportCsv(r); var html = LocalStore.ExportReport(r);
                    Check("CSV and printable report export", File.Exists(csv) && File.ReadAllLines(csv).Length > 1 && File.ReadAllText(html).Contains("92/100"));
                }
                _app.StartFlight(new FlightConfig { task = "rth", courseId = "test-rth", title = "QA · RTH", windMph = 0, rthAltitude = 8 }); yield return null;
                sim = _app.Simulator; sim.Body.position = new Vector3(22, 9, 0); sim.Body.linearVelocity = Vector3.zero; yield return new WaitForSeconds(.2f); sim.StartRth();
                for (int i = 0; i < 120 && !sim.ObjectiveComplete; i++) yield return new WaitForSeconds(.15f);
                Check("RTH returns and lands", sim.ObjectiveComplete && sim.Position.y < 1 && new Vector2(sim.Position.x, sim.Position.z).magnitude < 3);
                _app.StartFlight(new FlightConfig { professional = true, task = "inspection", workspace = "Professional", title = "QA · Inspection", mission = new MissionDefinition(), windMph = 0 }); yield return null;
                sim = _app.Simulator;
                for (int i = 0; i < 3; i++) { sim.Body.position = sim.Config.mission.targets[i] + new Vector3(0, 0, -8); sim.Body.linearVelocity = Vector3.zero; yield return new WaitForSeconds(.1f); sim.CapturePhoto(); yield return new WaitForSeconds(.12f); }
                Check("Inspection capture order completes", sim.ObjectiveComplete && sim.Record.photos == 3);
                Check("Photo evidence saved", Directory.GetFiles(LocalStore.DirectoryPath, "*-photo-*.png").Length >= 3);
                var evidence = sim.Record.photoEvidence;
                Check("Photo evidence metadata matches three real targets", evidence.Count == 3 && evidence.All(p => p.status == "saved" && p.width == 1280 && p.height == 720) && evidence.Select(p => p.targetIndex).SequenceEqual(new[] { 0, 1, 2 }));
                Check("RD-03 PhotoCapture stores complete immutable camera snapshots",
                    evidence.All(p => p.cameraSnapshot?.IsValid == true && p.cameraSnapshot.SessionId == sim.Record.id &&
                        p.cameraSnapshot.Context == CameraContext.PhotoCapture.ToString() && p.cameraSnapshot.Source == "pilot" &&
                        p.cameraSnapshot.Projection == "Perspective" && !string.IsNullOrEmpty(p.cameraSnapshot.Build)));
                Check("Photo evidence identities and files are unique", evidence.Select(p => p.id).Distinct().Count() == 3 && evidence.Select(p => p.fileName).Distinct().Count() == 3 && evidence.All(p => File.Exists(Path.Combine(LocalStore.DirectoryPath, p.fileName))));
                sim.Finish(); yield return null;
                LocalStore.Profile.settings.units = "Metric"; LocalStore.Save(); LocalStore.Initialize();
                Check("Profile reload preserves settings and sessions", LocalStore.Profile.settings.units == "Metric" && LocalStore.Sessions().Count == 2);
                _app.StartFlight(new FlightConfig { title = "QA · Rapid capture" }); yield return null;
                sim = _app.Simulator; sim.Body.position = new Vector3(0, 8, 0); sim.Body.linearVelocity = Vector3.zero;
                var cameraTarget = sim.View.targetTexture; var activeTarget = RenderTexture.active;
                sim.CapturePhoto(); sim.CapturePhoto(); sim.CapturePhoto();
                var rapidRecord = sim.Record;
                Check("Same-frame rapid captures save distinct evidence", rapidRecord.photos == 3 && rapidRecord.photoEvidence.Count == 3 && rapidRecord.photoEvidence.All(p => p.status == "saved") && rapidRecord.photoEvidence.Select(p => p.fileName).Distinct().Count() == 3);
                Check("Evidence rendering restores camera and active targets", sim.View.targetTexture == cameraTarget && RenderTexture.active == activeTarget);
                sim.Finish(); _app.ShowPage(Page.Home); yield return null;
                Check("Immediate finish retains the captured session identity", LocalStore.Sessions().Any(r => r.id == rapidRecord.id && r.photos == 3 && r.photoEvidence.All(p => File.Exists(Path.Combine(LocalStore.DirectoryPath, p.fileName)))));
                _app.StartFlight(new FlightConfig { title = "QA · Capture rejection" }); yield return null;
                sim = _app.Simulator; sim.Body.position = new Vector3(0, 8, 0); sim.Pause(true);
                Check("Paused capture does not create evidence or count", !sim.CapturePhoto() && sim.Record.photos == 0 && sim.Record.photoEvidence.Count == 0);
                _app.BeginReplay(rapidRecord); yield return null;
                Check("Replay capture cannot alter saved evidence", !_app.Simulator.CapturePhoto() && rapidRecord.photos == 3 && rapidRecord.photoEvidence.Count == 3);
                _app.StartFlight(new FlightConfig { professional = true, task = "inspection", mission = new MissionDefinition(), title = "QA · Rejected evidence write" }); yield return null;
                sim = _app.Simulator; sim.Body.position = sim.Config.mission.targets[0] + new Vector3(0, 0, -8);
                string validSessionId = sim.Record.id; sim.Record.id = "../invalid-qa-session";
                cameraTarget = sim.View.targetTexture; activeTarget = RenderTexture.active;
                bool invalidCapture = sim.CapturePhoto(); sim.Record.id = validSessionId;
                Check("Invalid evidence output fails without advancing target", !invalidCapture && sim.Record.photos == 0 && sim.GateIndex == 0 && sim.Record.photoEvidence.Single().status == "failed");
                Check("Rejected evidence write restores render targets", sim.View.targetTexture == cameraTarget && RenderTexture.active == activeTarget);
                _app.ShowPage(Page.Home); yield return null;
                var isolatedHost = new GameObject("QA photo writer failure probe");
                var isolatedSim = isolatedHost.AddComponent<FlightSimulator>();
                isolatedSim.Begin(new FlightConfig { professional = true, mission = new MissionDefinition(), task = "inspection" });
                isolatedSim.Body.position = isolatedSim.Config.mission.targets[0] + new Vector3(0, 0, -8);
                Check("Missing photo writer records explicit failure", !isolatedSim.CapturePhoto() && isolatedSim.Record.photos == 0 && isolatedSim.GateIndex == 0 && isolatedSim.Record.photoEvidence.Single().status == "failed" && isolatedSim.Record.photoEvidence.Single().failureReason.Contains("No photo evidence writer"));
                isolatedSim.Stop(); Destroy(isolatedHost); yield return null;
                var invalidRecord = new SessionRecord { id = "../invalid-qa-export" };
                bool csvRejected = false, reportRejected = false, saveRejected = false;
                try { LocalStore.ExportCsv(invalidRecord); } catch (ArgumentException) { csvRejected = true; }
                try { LocalStore.ExportReport(invalidRecord); } catch (ArgumentException) { reportRejected = true; }
                try { LocalStore.SaveSession(invalidRecord); } catch (ArgumentException) { saveRejected = true; }
                Check("Session traversal is rejected by all export and save paths", csvRejected && reportRejected && saveRejected);
                var priorAttempts = LocalStore.Profile.courses.Find(c => c.id == r.courseId)?.attempts ?? 0;
                LocalStore.SaveNotes(r);
                Check("Saving notes does not increment course attempts", (LocalStore.Profile.courses.Find(c => c.id == r.courseId)?.attempts ?? 0) == priorAttempts);
                var validPhoto = rapidRecord.photoEvidence[0];
                var unrelatedPhoto = JsonUtility.FromJson<PhotoEvidence>(JsonUtility.ToJson(validPhoto)); unrelatedPhoto.sessionId = "other-qa-session";
                var traversalPhoto = JsonUtility.FromJson<PhotoEvidence>(JsonUtility.ToJson(validPhoto)); traversalPhoto.fileName = "../other-qa-photo.png";
                Check("Photo resolver accepts actual linked PNG and rejects cross-session/traversal", EvidencePaths.ResolvePhoto(rapidRecord, validPhoto) != null && EvidencePaths.ResolvePhoto(rapidRecord, unrelatedPhoto) == null && EvidencePaths.ResolvePhoto(rapidRecord, traversalPhoto) == null);
                bool decodedCaptures = true;
                foreach (var photo in rapidRecord.photoEvidence)
                {
                    var image = new Texture2D(2, 2);
                    try { decodedCaptures &= image.LoadImage(File.ReadAllBytes(EvidencePaths.ResolvePhoto(rapidRecord, photo))) && image.width == 1280 && image.height == 720; }
                    finally { Destroy(image); }
                }
                Check("Saved evidence PNGs actually decode at 1280 by 720", decodedCaptures);
                string legacyId = "legacy-qa-session";
                File.WriteAllText(EvidencePaths.ResolveOutput(legacyId + ".json"), "{\"id\":\"legacy-qa-session\",\"photos\":5,\"frames\":null,\"events\":null}");
                LocalStore.Profile.sessions.Add(legacyId);
                var legacy = LocalStore.Sessions().Find(record => record.id == legacyId);
                Check("Legacy record recovers without fabricated photo metadata", legacy != null && legacy.photos == 5 && legacy.photoEvidence.Count == 0 && legacy.frames != null && legacy.events != null && File.ReadAllText(LocalStore.ExportReport(legacy)).Contains("legacy record has no linked photo metadata"));
                LocalStore.Profile.settings.units = "U.S. Customary"; LocalStore.Save(); _app.ShowPage(Page.Home);
            }
            private IEnumerator CaptureHeroCliff(string dir)
            {
                Directory.CreateDirectory(dir);
                LocalStore.Profile.settings.quality = AeroSimQuality.High; LocalStore.Save();
                _app.StartFlight(new FlightConfig
                {
                    environment = "Pacific Coast", weather = Argument("--hero-cliff-weather") ?? "Clear",
                    timeOfDay = 16.5f, windMph = 8, environmentSeed = 41001,
                    title = "P11 Hero Cliff CLI capture", task = "gates"
                });
                var sim = _app.Simulator;
                sim.Body.position = new Vector3(0, 8, 10); sim.Body.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(.5f); sim.Pause(true); yield return null;

                var metadata = FindFirstObjectByType<HeroCliffP11Metadata>();
                var catalog = CoastProductionRuntimeCatalog.Load();
                Check("P11 CLI locates instantiated HeroCliffP11Metadata", metadata != null && sim.CoastProductionStatus?.p11Loaded == true);
                if (metadata == null)
                {
                    _results.Add("CAPTURE_FAILED P11 target missing"); _failures++; yield break;
                }

                var route = gameObject.AddComponent<HeroCliffCaptureRoute>();
                route.captureCamera = sim.View; route.target = metadata; route.diagnosticShader = catalog?.p11DiagnosticShader;
                bool finished = false; string failure = null; HeroCliffCapturePackage package = null;
                yield return route.CaptureToDirectory(dir, value => { package = value; finished = true; }, value => { failure = value; finished = true; });
                Check("P11 CLI capture coroutine completed", finished && failure == null && package != null);
                if (package != null)
                {
                    bool stateValid = package.status == HeroCliffCaptureRoute.Captured &&
                        package.reviewStatus == HeroCliffCaptureRoute.NotReviewed && package.gateStatus == HeroCliffCaptureRoute.GateNotRun;
                    bool matrixValid = package.frames.Count == HeroCliffCaptureRoute.Distances.Length * HeroCliffCaptureRoute.ChannelNames.Length &&
                        package.frames.All(frame => frame.status == HeroCliffCaptureRoute.Captured && File.Exists(Path.Combine(dir, frame.file)) &&
                            !string.IsNullOrWhiteSpace(frame.sha256) && frame.distinctFromFinalColor) &&
                        HeroCliffCaptureRoute.Distances.All(distance => package.frames.Where(frame => Mathf.Approximately(frame.distanceMeters, distance)).Select(frame => frame.sha256).Distinct().Count() == HeroCliffCaptureRoute.ChannelNames.Length);
                    bool pathsValid = package.frames.Where(frame => frame.channel != "FinalColor").All(frame => frame.renderPath.StartsWith("camera.RenderWithShader/", StringComparison.Ordinal)) &&
                        package.frames.Where(frame => frame.channel == "FinalColor").All(frame => frame.renderPath == "camera.Render/final-material");
                    Check("P11 CLI manifest truth states are CAPTURED/NOT_REVIEWED/GATE_NOT_RUN", stateValid);
                    Check("P11 CLI writes 5x8 distinct verified PNG matrix", matrixValid);
                    Check("P11 CLI diagnostics use replacement shader rather than FinalColor", pathsValid);
                    Check("P11 CLI frame manifest written", File.Exists(Path.Combine(dir, "manifest.json")));
                }
                else { _results.Add("CAPTURE_FAILED " + failure); }
                Destroy(route); _app.ShowPage(Page.Home); yield return null;
            }

            private IEnumerator CaptureCoastMatrix(string dir)
            {
                Directory.CreateDirectory(dir);
                bool minimal = Environment.GetCommandLineArgs().Contains("--coast-matrix-minimal");
                foreach (string weather in minimal ? new[] { "Clear" } : new[] { "Clear", "Overcast", "Rain" })
                    foreach (int quality in minimal ? new[] { AeroSimQuality.Low } : new[] { AeroSimQuality.Low, AeroSimQuality.High, AeroSimQuality.Ultra })
                    {
                        LocalStore.Profile.settings.quality = quality; LocalStore.Save();
                        _app.StartFlight(new FlightConfig
                        {
                            environment = "Pacific Coast", weather = weather, timeOfDay = 16.5f, windMph = 8,
                            environmentSeed = 41001, title = "Coast P01-P10 matrix · " + weather + " · " + AeroSimQuality.Name(quality), task = "gates"
                        });
                        var sim = _app.Simulator; sim.Body.position = new Vector3(0, 8, 10); sim.Body.linearVelocity = Vector3.zero;
                        yield return new WaitForSeconds(.35f); sim.Pause(true); yield return null;
                        string variant = weather.ToLowerInvariant() + "-" + AeroSimQuality.Name(quality).ToLowerInvariant();
                        string root = Path.Combine(dir, variant);
                        var manifest = VisualLabEvidence.CreateManifest(sim, new VisualLabPerformanceSummary { status = "NOT_RUN" }, Argument("--build-id"), Argument("--source-commit"));
                        var artifact = VisualLabEvidence.Capture(sim, "A", 15, root, manifest);
                        VisualLabEvidence.Write(manifest, root);
                        var coast = sim.CoastProductionStatus;
                        bool runtimeBindingsMatch = coast != null && coast.candidateBindingsComplete && coast.weather == weather &&
                            coast.qualityTier == AeroSimQuality.Name(quality) && coast.mipStreamingActiveActual;
                        RecordCoastCapture("Coast matrix " + weather + " / " + AeroSimQuality.Name(quality), manifest, artifact,
                            runtimeBindingsMatch, Path.Combine(root, artifact.relativePath));
                        yield return null;
                    }
                _app.ShowPage(Page.Home);
            }

            private IEnumerator CaptureVisualLab(string dir)
            {
                int quality = AeroSimQuality.Parse(Argument("--visual-lab-quality") ?? "High");
                int seconds = 10;
                if (int.TryParse(Argument("--visual-lab-seconds"), out int requested)) seconds = Mathf.Max(1, requested);
                int warmupSeconds = 2;
                if (int.TryParse(Argument("--visual-lab-warmup-seconds"), out int requestedWarmup)) warmupSeconds = Mathf.Max(0, requestedWarmup);
                LocalStore.Profile.settings.quality = quality; LocalStore.Save();
                _app.StartFlight(new FlightConfig
                {
                    environment = "Pacific Coast", weather = Argument("--visual-lab-weather") ?? "Clear",
                    timeOfDay = 16.5f, windMph = 0, environmentSeed = 41001,
                    title = "RD-09 Visual Lab batch", task = "gates"
                });
                var sim = _app.Simulator; sim.Body.position = new Vector3(0, 8, 10); sim.Body.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(warmupSeconds);
                using var profiler = new VisualLabProfiler();
                float started = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - started < seconds) { profiler.Sample(); yield return null; }
                sim.Pause(true); yield return null;
                var manifest = VisualLabEvidence.CreateManifest(sim, profiler.Summarize(), Argument("--build-id"), Argument("--source-commit"));
                manifest.warmupSeconds = warmupSeconds;
                manifest.targetHardwareProfileDeclared = Argument("--target-hardware-profile") == manifest.targetHardwareProfileId;
                foreach (string shot in VisualLabEvidence.ShotIds)
                    foreach (float distance in VisualLabEvidence.Distances)
                    {
                        var artifact = VisualLabEvidence.Capture(sim, shot, distance, dir, manifest);
                        bool validArtifact = artifact.captureStatus == "CAPTURED" && artifact.compositionGuard != null && artifact.compositionGuard.Passed &&
                            artifact.provenanceLabel == ProceduralVerticalSliceDiagnostics.RequiredLabel + " / NOT SCANNED" &&
                            !string.IsNullOrWhiteSpace(artifact.heroZoneId) && !string.IsNullOrWhiteSpace(artifact.reviewBand) &&
                            File.Exists(Path.Combine(dir, artifact.relativePath));
                        string captureResult = (validArtifact ? "CAPTURED · COMPOSITION_GUARD_OK " : "CAPTURED · COMPOSITION_GUARD_FAILED ") + "Visual Lab " + shot + " / " + distance + "m";
                        _results.Add(captureResult); Debug.Log(captureResult); if (!validArtifact) _failures++;
                        yield return null;
                    }
                string samplesPath = VisualLabEvidence.WriteSamples(profiler, manifest, dir);
                bool samplesCaptured = File.Exists(samplesPath) && !string.IsNullOrWhiteSpace(manifest.performanceSamplesSha256);
                _results.Add((samplesCaptured ? "CAPTURED " : "CAPTURE_FAILED ") + "RD-09 raw performance samples"); if (!samplesCaptured) _failures++;
                string path = VisualLabEvidence.Write(manifest, dir);
                bool manifestWritten = File.Exists(path);
                _results.Add((manifestWritten ? manifest.status : "CAPTURE_FAILED") + " RD-09 evidence manifest"); if (!manifestWritten) _failures++;
                bool gateHeld = manifest.status == "INCOMPLETE_EVIDENCE" && manifest.gateStatus == "NOT_RUN" && manifest.performance.gateStatus == "NOT_RUN";
                _results.Add((gateHeld ? "GATE_NOT_RUN " : "GATE_STATE_INVALID ") + "RD-09 unreviewed evidence"); if (!gateHeld) _failures++;
                _app.ShowPage(Page.Home);
            }

            private IEnumerator CaptureAll(string dir)
            {
                Directory.CreateDirectory(dir);
                var pages = new[] { Page.Onboarding, Page.Home, Page.Controller, Page.Academy, Page.Brief, Page.Preflight, Page.FreeFlight, Page.Challenges, Page.Hangar, Page.Readiness, Page.Settings, Page.Records, Page.Results, Page.Report };
                string target=Argument("--workspace");
                if(target!=null) { _app.ChangeWorkspace(target); _app.ShowPage(Page.FreeFlight); }
                _app.ChangeWorkspace("Aerial");
                foreach (var page in pages)
                {
                    _app.PreparePreview(page); yield return null; yield return null;
                    if(page==Page.Hangar)
                    {
                        var root=_app.GetComponent<UIDocument>().rootVisualElement;
                        var main=root.Q<VisualElement>(className:"main-content");
                        var cards=root.Query<VisualElement>(className:"aircraft-card").ToList();
                        var previews=root.Query<Image>(className:"aircraft-preview").ToList();
                        Check("Wide layout caps primary content width",root.resolvedStyle.width<2200||(main!=null&&main.resolvedStyle.width<=1921));
                        Check("Wide Hangar caps aircraft card width",root.resolvedStyle.width<2200||(cards.Count>0&&cards.All(card=>card.resolvedStyle.width<=461)));
                        Check("Wide Hangar uses high-density preview textures",root.resolvedStyle.width<2200||(previews.Count>0&&previews.All(image=>image.image is RenderTexture texture&&texture.width>=1536)));
                    }
                    yield return Capture(Path.Combine(dir, "aerial-" + page + ".png"));
                    if(page==Page.Onboarding)
                    {
                        yield return Click("Continue"); yield return Capture(Path.Combine(dir,"aerial-Onboarding-2.png"));
                        yield return Click("Continue"); yield return Capture(Path.Combine(dir,"aerial-Onboarding-3.png"));
                    }
                    if(page==Page.FreeFlight || page==Page.Controller || page==Page.Settings)
                    {
                        var scroll=_app.GetComponent<UIDocument>().rootVisualElement.Q<ScrollView>(className:"product-scroll");
                        scroll.scrollOffset=new Vector2(0,10000); yield return null; yield return null; yield return Capture(Path.Combine(dir,"aerial-"+page+"-detail.png"));
                    }
                }
                _app.ChangeWorkspace("FPV");
                foreach (var page in new[] { Page.Home, Page.Academy, Page.Tuning, Page.OsdEditor, Page.Challenges }) { _app.PreparePreview(page); yield return null; yield return null; yield return Capture(Path.Combine(dir, "fpv-" + page + ".png")); }
                _app.ChangeWorkspace("Professional");
                foreach (var page in new[] { Page.Home, Page.Catalog, Page.MissionEditor, Page.Instructor, Page.Brief, Page.Preflight }) { _app.PreparePreview(page); yield return null; yield return null; yield return Capture(Path.Combine(dir, "professional-" + page + ".png")); }
                _app.StartFlight(new FlightConfig { title="Pacific Coast Practice", objective="Pass through the six highlighted route gates in order.", task = "gates", environment = "Pacific Coast" });
                _app.Simulator.Body.position = new Vector3(0, 8, 10); _app.Simulator.Pause(true); yield return new WaitForSeconds(.4f); AuditFlightHudGeometry("Aerial Flight"); yield return Capture(Path.Combine(dir, "aerial-Flight.png"));
                _app.Simulator.Pause(false); yield return Click("action-pause"); yield return Capture(Path.Combine(dir,"aerial-Pause.png"));
                _app.StartFlight(new FlightConfig { workspace="FPV", task="gates", mode="Manual", environment="Desert Range", aircraft="5-inch Racer" });
                _app.Simulator.Body.position=new Vector3(0,8,10); _app.Simulator.Pause(true); yield return new WaitForSeconds(.4f); AuditFlightHudGeometry("FPV Flight"); yield return Capture(Path.Combine(dir,"fpv-Flight.png"));
                _app.StartFlight(new FlightConfig { workspace="Professional", professional=true, task="inspection", environment="City Park", mission=new MissionDefinition() });
                _app.Simulator.Body.position=new Vector3(0,12,15); _app.Simulator.Pause(true); yield return new WaitForSeconds(.4f); AuditFlightHudGeometry("Professional Flight"); yield return Capture(Path.Combine(dir,"professional-Flight.png"));
                var record = LocalStore.Sessions().FirstOrDefault();
                if (record != null) { _app.BeginReplay(record); _app.Simulator.Seek(record.duration * .5f); yield return new WaitForSeconds(.3f); yield return Capture(Path.Combine(dir, "aerial-Replay.png")); }
                _app.ShowPage(Page.Home);
            }

            private IEnumerator CaptureAircraft(string dir)
            {
                Directory.CreateDirectory(dir);
                Check("Legacy Camera Drone resolves to Mini",AircraftCatalog.Resolve("Camera Drone").Id=="mini4");
                Check("Unknown aircraft has safe fallback",AircraftCatalog.Resolve("missing-id").Id=="mini4");
                foreach(var name in new[]{"mini-shell-v3","air-shell-v3","mavic-shell-v3","avata-shell-v3","carbon-weave-v3"})
                    Check("Aircraft V3 texture bundled: "+name,Resources.Load<Texture2D>("Art/AircraftV3/"+name)!=null);
                foreach(var path in new[]{"Art/DesignV1/CoastHeroV1","Art/DesignV1/ControllerCutout","Art/DesignV1/FpvIndustrialSunset"})
                {
                    var texture=Resources.Load<Texture2D>(path);
                    Check("High-density UI art bundled: "+path,texture!=null&&texture.width>=1600&&texture.height>=700);
                }
                _app.ChangeWorkspace("Aerial");
                var runtimeProfiles = AircraftCatalog.All.Where(profile =>
                    profile.Status == AircraftCatalogStatus.Runtime && profile.IsSelectable).ToArray();
                Check("CaptureAircraft exercises six runtime aircraft",runtimeProfiles.Length==6);
                var initiallySelectedId = AircraftCatalog.Selected.Id;
                var profiles = runtimeProfiles.Where(profile => profile.Id != initiallySelectedId)
                    .Concat(runtimeProfiles.Where(profile => profile.Id == initiallySelectedId)).ToArray();
                foreach(var profile in profiles)
                {
                    _app.ShowPage(Page.Hangar);yield return null;yield return null;
                    yield return Click("hangar-filter-all");yield return null;yield return null;
                    yield return Click("hangar-select-"+profile.Id);
                    Check("Hangar selection: "+profile.Id,AircraftCatalog.Selected.Id==profile.Id);
                    LocalStore.Initialize();
                    Check("Aircraft selection reload: "+profile.Id,AircraftCatalog.Selected.Id==profile.Id);
                    var preview=_app.RenderAircraftPreview(profile);
                    var active=RenderTexture.active;RenderTexture.active=preview;
                    var image=new Texture2D(preview.width,preview.height,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,preview.width,preview.height),0,0);image.Apply();
                    File.WriteAllBytes(Path.Combine(dir,profile.Id+"-preview.png"),image.EncodeToPNG());
                    RenderTexture.active=active;preview.Release();Destroy(preview);Destroy(image);
                    yield return Click("Fly Selected Aircraft");yield return null;
                    var root=_app.GetComponent<UIDocument>().rootVisualElement;
                    var aircraftField=root.Query<DropdownField>().ToList().FirstOrDefault(field=>field.label=="Aircraft");
                    Check("Free Flight uses hangar aircraft: "+profile.Id,
                        aircraftField!=null&&aircraftField.value==profile.DisplayName&&
                        aircraftField.choices.SequenceEqual(runtimeProfiles.Select(entry=>entry.DisplayName)));
                    if(profile.Id=="mavic3p")yield return Capture(Path.Combine(dir,"selected-free-flight.png"));
                    yield return Click("action-launch-free-flight");yield return null;
                    var sim=_app.Simulator;var model=sim.Body.GetComponentInChildren<AircraftVisualModel>();
                    Check("Flight builds selected model with 4 rotors: "+profile.Id,model!=null&&model.Profile.Id==profile.Id&&model.Rotors.Count==4);
                    Check("Aircraft exposes manifest camera sockets: "+profile.Id,
                        model!=null&&model.CameraSockets.ContainsKey("flight-camera")&&model.CameraSockets.ContainsKey("preview-focus"));
                    Check("Programmatic aircraft does not claim a golden production model: "+profile.Id,
                        profile.AssetContract?.QualityState!=AssetQualityState.ProgrammaticPrototype||!model.IsGoldenSample);
                    var nodes=model.GetComponentsInChildren<Transform>();
                    int lenses=nodes.Count(n=>n.name.EndsWith("optical glass"));
                    int expected=profile.Shape==AirframeShape.Mavic?3:profile.Shape==AirframeShape.Air?2:1;
                    Check("Distinct camera layout: "+profile.Id,lenses==expected);
                    if(profile.Shape==AirframeShape.Ducted||profile.Shape==AirframeShape.Micro)
                        Check("Four integrated prop guards: "+profile.Id,nodes.Count(n=>n.name=="Integrated prop guard")==4);
                    Check("No unsupported aircraft materials: "+profile.Id,model.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.isSupported));
                    Check("Aircraft shared material budget is six or fewer: "+profile.Id,model.DistinctSharedMaterialCount<=6);
                    string visualBudgetReason;
                    var visualBudgetPass=model.ValidateVisualBudget(out visualBudgetReason);
                    var quality=model.GetQualityReport();
                    Check("Aircraft formal visual budget: "+profile.Id+
                        (visualBudgetPass?"":" · "+visualBudgetReason),visualBudgetPass);
                    Check("Aircraft has LOD0/1/2/3/Cull: "+profile.Id,
                        quality.HasRuntimeLodGroup&&quality.Lods!=null&&quality.Lods.Length==AircraftVisualModel.LodLevelCount&&
                        quality.Lods[4].Renderers==0&&quality.Lods[4].RenderedTriangles==0);
                    Check("Aircraft LOD complexity and materials decrease within budget: "+profile.Id,
                        quality.Lods.Take(4).All(l=>l.SharedMaterials<=AircraftVisualModel.SharedMaterialBudget)&&
                        Enumerable.Range(1,3).All(i=>quality.Lods[i].Renderers<=quality.Lods[i-1].Renderers&&
                            quality.Lods[i].RenderedTriangles<=quality.Lods[i-1].RenderedTriangles));
                    Check("Aircraft runtime meshes release CPU ReadWrite data: "+profile.Id,
                        quality.RuntimeMeshes<=4&&quality.ReadableRuntimeMeshes==0);
                    Check("Aircraft uses 6-16 non-rendering primitive compound colliders: "+profile.Id,
                        quality.HasValidCompoundCollision&&quality.Colliders>=AircraftVisualModel.MinimumCompoundColliders&&
                        quality.Colliders<=AircraftVisualModel.MaximumCompoundColliders&&
                        quality.PrimitiveColliders==quality.Colliders&&quality.MeshColliders==0&&
                        quality.CollisionRenderers==0&&quality.CompoundAttachedColliders==quality.Colliders);
                    Check("Aircraft compound replaces legacy root BoxCollider: "+profile.Id,
                        sim.Body.GetComponent<BoxCollider>()==null);
                    sim.Body.position=new Vector3(0,8,10);sim.Pause(true);sim.Body.transform.position=sim.Body.position;yield return new WaitForSeconds(.3f);
                    yield return SceneImage(Path.Combine(dir,profile.Id+"-detail.png"),sim.Position+new Vector3(1.70f,.85f,1.95f)*profile.VisualScale,sim.Position,38);
                    yield return Capture(Path.Combine(dir,profile.Id+"-flight.png"));
                    Check("Course launch keeps selected aircraft: "+profile.Id,CourseCatalog.Find("a10").Config().aircraft==profile.DisplayName);
                    sim.Pause(false);yield return new WaitForSeconds(.2f);
                    var completedRecord=sim.Record;
                    sim.Finish();yield return null;
                    Check("Flight finishes to Results with saved aircraft: "+profile.Id,
                        _app.CurrentPage==Page.Results&&LocalStore.Sessions().Any(saved=>saved.id==completedRecord.id&&saved.aircraftSnapshot?.catalogId==profile.CatalogId));
                    _app.ShowPage(Page.Records);yield return null;
                    _app.BeginReplay(completedRecord);yield return null;
                    Check("Records replay retains completed aircraft: "+profile.Id,_app.Simulator.Body.GetComponentInChildren<AircraftVisualModel>().Profile.Id==profile.Id);
                    _app.ChangeWorkspace("FPV");_app.ShowPage(Page.FreeFlight);yield return null;
                    Check("Leaving replay stops simulation cleanly: "+profile.Id,!sim.Running&&!sim.ReplayMode&&sim.Body==null);
                    aircraftField=root.Query<DropdownField>().ToList().FirstOrDefault(f=>f.label=="Aircraft");
                    Check("Workspace change preserves aircraft: "+profile.Id,aircraftField!=null&&aircraftField.value==profile.DisplayName);
                    _app.ChangeWorkspace("Aerial");
                }
                var candidateIds = new[] { "dji-mavic-4-pro", "dji-matrice-350-rtk", "dji-agras-t50", "dji-inspire-3" };
                foreach(var candidateId in candidateIds)
                {
                    var candidate = AircraftCatalog.All.FirstOrDefault(profile => profile.Id == candidateId);
                    string selectedBefore = AircraftCatalog.Selected.Id;
                    _app.ShowPage(Page.Hangar);yield return null;yield return null;
                    yield return Click("hangar-filter-all");yield return null;yield return null;
                    var root = _app.GetComponent<UIDocument>().rootVisualElement;
                    var search = root.Query<TextField>().ToList().FirstOrDefault(field=>field.label=="Search");
                    if(search!=null)search.value=candidate.Name;
                    yield return null;
                    var card = root.Q<VisualElement>("aircraft-card-"+candidateId);
                    var select = root.Q<Button>("hangar-select-"+candidateId);
                    bool lockReasonVisible = card != null && card.Query<Label>().ToList().Any(label =>
                        label.text != null && label.text.Contains("LOCKED") && label.text.Contains("Runtime access is not approved"));
                    Check("Candidate search and identity remain browsable: "+candidateId,
                        search!=null&&card!=null&&card.resolvedStyle.display!=DisplayStyle.None&&
                        card.Query<Button>(className:"aircraft-bay-fleet-inspect").ToList().Count==1);
                    Check("Candidate is visible and locked in Aircraft Bay: "+candidateId,
                        candidate!=null&&card!=null&&card.resolvedStyle.display!=DisplayStyle.None&&
                        select!=null&&!select.enabledInHierarchy&&lockReasonVisible);
                    Check("Disabled candidate does not change selected aircraft: "+candidateId,
                        AircraftCatalog.Selected.Id==selectedBefore);
                    yield return Capture(Path.Combine(dir,candidateId+"-locked.png"));
                }
                {
                    var root=_app.GetComponent<UIDocument>().rootVisualElement;
                    var search=root.Query<TextField>().ToList().FirstOrDefault(field=>field.label=="Search");
                    string selectedBefore=AircraftCatalog.Selected.Id;
                    if(search!=null)search.value="no-aircraft-qa-empty-state";
                    yield return null;
                    bool noCards=root.Query<VisualElement>(className:"aircraft-card").ToList().All(card=>card.resolvedStyle.display==DisplayStyle.None);
                    bool emptyCopy=root.Query<Label>().ToList().Any(label=>label.text!=null&&label.text.Contains("No aircraft match these filters"));
                    Check("Hangar no-result empty state preserves selection",search!=null&&noCards&&emptyCopy&&AircraftCatalog.Selected.Id==selectedBefore);
                    if(search!=null)search.value="";
                }
                _app.ShowPage(Page.Home);yield return null;yield return Resources.UnloadUnusedAssets();
                int baselineMaterials=Resources.FindObjectsOfTypeAll<Material>().Length;
                int baselineRenderTextures=Resources.FindObjectsOfTypeAll<RenderTexture>().Length;
                for(int cycle=0;cycle<20;cycle++)
                {
                    _app.ShowPage(Page.Hangar);yield return null;
                    _app.ShowPage(Page.Home);yield return null;
                }
                yield return Resources.UnloadUnusedAssets();
                int finalMaterials=Resources.FindObjectsOfTypeAll<Material>().Length;
                int finalRenderTextures=Resources.FindObjectsOfTypeAll<RenderTexture>().Length;
                Check("Twenty Hangar cycles do not grow Material instances",finalMaterials<=baselineMaterials+2);
                Check("Twenty Hangar cycles do not grow RenderTexture instances",finalRenderTextures<=baselineRenderTextures);
                if(AircraftCatalog.Selected.Id!="avata2")
                {
                    _app.ShowPage(Page.Hangar);yield return null;yield return null;
                    yield return Click("hangar-filter-all");yield return null;yield return null;
                    yield return Click("hangar-select-avata2");yield return null;
                }
                Check("Hangar UI selects Avata 2 for cross-workspace checks",AircraftCatalog.Selected.Id=="avata2");
                _app.ShowPage(Page.Hangar);yield return null;yield return null;
                yield return Capture(Path.Combine(dir,"hangar-top.png"));
                var scroll=_app.GetComponent<UIDocument>().rootVisualElement.Q<ScrollView>(className:"product-scroll");
                scroll.scrollOffset=new Vector2(0,530);yield return null;yield return null;
                yield return Capture(Path.Combine(dir,"hangar-cameras-fpv.png"));
                scroll.scrollOffset=new Vector2(0,10000);yield return null;yield return null;
                yield return Capture(Path.Combine(dir,"hangar-trainers.png"));
                _app.ChangeWorkspace("Professional");
                _app.PreparePreview(Page.Preflight);yield return null;yield return null;
                var professionalRoot = _app.GetComponent<UIDocument>().rootVisualElement;
                foreach(var toggle in professionalRoot.Query<Toggle>().ToList())toggle.value=true;
                var formalStart = professionalRoot.Q<Button>("preflight-start-session");
                Check("Professional mission is blocked by undelivered prototype asset",
                    _app.CurrentPage==Page.Preflight&&formalStart!=null&&!formalStart.enabledInHierarchy&&
                    professionalRoot.Query<Label>().ToList().Any(label=>label.text!=null&&label.text.Contains("CONTENT NOT DELIVERED")));
                yield return Click("Open Prototype Sandbox");yield return null;yield return null;
                foreach(var toggle in _app.GetComponent<UIDocument>().rootVisualElement.Query<Toggle>().ToList())toggle.value=true;
                yield return Click("Start Session");yield return null;
                Check("Prototype recovery preserves selected aircraft and launches marked sandbox",_app.CurrentPage==Page.Flight&&_app.Simulator.Config.aircraft==AircraftCatalog.Resolve("avata2").DisplayName);
                _app.ShowPage(Page.Challenges);yield return null;yield return null;
                yield return Click("Start Challenge");yield return null;
                Check("Challenge uses selected aircraft",_app.CurrentPage==Page.Flight&&_app.Simulator.Config.aircraft==AircraftCatalog.Resolve("avata2").DisplayName);
                _app.ShowPage(Page.Hangar);yield return null;yield return null;
                yield return Click("hangar-filter-all");yield return null;yield return null;
                yield return Click("hangar-select-mini4");yield return null;
                Check("Professional mission blocks incompatible Mini 4 Pro selection",AircraftCatalog.Selected.Id=="avata2");
            }

            private IEnumerator CaptureGraphics(string dir)
            {
                Directory.CreateDirectory(dir);
                foreach(var environment in new[]{"Pacific Coast","Desert Range","City Park","Warehouse"})
                {
                    bool desert=environment=="Desert Range",professional=environment=="City Park";
                    _app.StartFlight(new FlightConfig {
                        environment=environment,workspace=desert?"FPV":professional?"Professional":"Aerial",
                        title=environment+" · Art V2",task=professional?"inspection":"gates",professional=professional,
                        mission=professional?new MissionDefinition():null,windMph=0,timeOfDay=16.5f
                    });
                    var sim=_app.Simulator;sim.Body.position=new Vector3(0,8,10);sim.Pause(true);
                    sim.Body.transform.position=sim.Body.position;
                    yield return new WaitForSeconds(.7f);
                    var name=environment.ToLowerInvariant().Replace(' ','-');
                    yield return Capture(Path.Combine(dir,name+"-flight.png"));
                    yield return SceneImage(Path.Combine(dir,name+"-scene.png"),new Vector3(desert?-37:-34,24,-28),new Vector3(8,7,80),64);
                    if(environment=="Pacific Coast")
                    {
                        yield return SceneImage(Path.Combine(dir,"drone-detail.png"),sim.Position+new Vector3(1.05f,.6f,1.25f),sim.Position+new Vector3(0,-.02f,0),44);
                        yield return SceneImage(Path.Combine(dir,"coast-panorama.png"),new Vector3(-38,32,-55),new Vector3(-95,3,95),65);
                        sim.Pause(false);
                        var started=Time.realtimeSinceStartup;for(int i=0;i<120;i++)yield return null;
                        float elapsed=Time.realtimeSinceStartup-started;
                        _results.Add("OBSERVED desktop coast 120 frames: "+(120/elapsed).ToString("F1")+" FPS at "+Screen.width+"x"+Screen.height+"; not a mobile benchmark");
                        sim.Pause(true);
                    }
                }
                _app.StartFlight(new FlightConfig{environment="Pacific Coast",windMph=0,timeOfDay=19,title="Golden hour · Art V2"});
                _app.Simulator.Body.position=new Vector3(0,8,10);_app.Simulator.Pause(true);_app.Simulator.Body.transform.position=_app.Simulator.Body.position;yield return new WaitForSeconds(.5f);
                yield return SceneImage(Path.Combine(dir,"coast-evening.png"),new Vector3(-30,24,-40),new Vector3(-90,2,80),65);
                _app.StartFlight(new FlightConfig{environment="Pacific Coast",windMph=0,weather="Overcast",title="Overcast · Art V2"});
                _app.Simulator.Body.position=new Vector3(0,8,10);_app.Simulator.Pause(true);_app.Simulator.Body.transform.position=_app.Simulator.Body.position;yield return new WaitForSeconds(.5f);
                yield return SceneImage(Path.Combine(dir,"coast-overcast.png"),new Vector3(-30,24,-40),new Vector3(-90,2,80),65);
                _app.ShowPage(Page.Home);
            }

            private IEnumerator SceneImage(string path,Vector3 position,Vector3 target,float fov)
            {
                yield return new WaitForEndOfFrame();
                // A separate actual Unity camera, not concept art and not a changed pilot camera.
                var host=new GameObject("Art QA camera");var camera=host.AddComponent<Camera>();camera.CopyFrom(_app.Simulator.View);
                camera.enabled=false;camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=fov;
                var rt=new RenderTexture(1680,945,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;rt.Create();camera.targetTexture=rt;camera.Render();
                var previous=RenderTexture.active;RenderTexture.active=rt;
                var texture=new Texture2D(1680,945,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1680,945),0,0);texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;
                rt.Release();Destroy(rt);Destroy(texture);Destroy(host);Debug.Log("Captured rendered scene: "+path);
            }
        }
    }
}
