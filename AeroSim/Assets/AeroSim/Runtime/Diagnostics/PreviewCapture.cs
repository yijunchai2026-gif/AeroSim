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
using UnityEngine.UIElements;

namespace AeroSim.Diagnostics
{
    public static class PreviewCapture
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--capture-preview") && !args.Contains("--capture-all") && !args.Contains("--capture-graphics") && !args.Contains("--capture-aircraft") && !args.Contains("--smoke-test")) return;
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
            private void OnEnable()=>Application.logMessageReceived+=TrackError;
            private void OnDisable()=>Application.logMessageReceived-=TrackError;
            private void TrackError(string condition,string trace,LogType type)
            {
                if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)_runtimeErrors++;
            }
            private string Argument(string key)
            {
                var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key); return i >= 0 && i < args.Length - 1 ? args[i + 1] : null;
            }
            private void Check(string name, bool passed)
            {
                var result = (passed ? "PASS " : "FAIL ") + name; _results.Add(result); Debug.Log(result); if (!passed) _failures++;
            }
            private IEnumerator Start()
            {
                yield return null; yield return null;
                _app = FindFirstObjectByType<AeroSimApplication>();
                if (Environment.GetCommandLineArgs().Contains("--smoke-test")) yield return SmokeTest();
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
            private IEnumerator Click(string title)
            {
                if (_mouse == null) _mouse=InputSystem.AddDevice<Mouse>("AeroSimCaptureMouse");
                var root = _app.GetComponent<UIDocument>().rootVisualElement;
                var buttons = root.Query<Button>().ToList();
                var button = buttons.Find(b =>
                    b.name == title || b.text == title ||
                    (title == "hangar-filter-all" && (b.text == "All" || b.text == "✓ All")) ||
                    b.Q<Label>(className: "design-button-label")?.text == title);
                if (button == null && title.StartsWith("hangar-select-", StringComparison.Ordinal))
                {
                    string profileId = title.Substring("hangar-select-".Length);
                    var card = root.Q<VisualElement>("aircraft-card-" + profileId);
                    button = card?.Query<Button>().ToList().FirstOrDefault();
                }
                if (button == null) { Check("Button exists: " + title, false); yield break; }
                var scroll = button.GetFirstAncestorOfType<ScrollView>();
                scroll?.ScrollTo(button); yield return null; yield return null;
                var bound = button.worldBound; var point = bound.center;
                var position = new Vector2(point.x * Screen.width / root.resolvedStyle.width, Screen.height - point.y * Screen.height / root.resolvedStyle.height);
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }); yield return null;
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = position, buttons = 1 }); yield return null; yield return null;
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }); yield return null; yield return null;
            }
            private IEnumerator SmokeTest()
            {
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
                foreach(var name in new[]{"meadow-grass-v2","coastal-rock-v2","aircraft-shell-v2","evergreen-needles-v2"})
                    Check("Art texture bundled: "+name, Resources.Load<Texture2D>("Art/MaterialsV2/"+name)!=null);
                foreach(var name in new[]{"TrainingSurface","LandscapeSurface","Atmosphere","OceanSurface","RotorBlur","NeedleFoliage","IndustrialSurfaceV1","RiverSurfaceV1"})
                {
                    var shader=Resources.Load<Shader>(name);Check("Art shader supported: "+name,shader!=null&&shader.isSupported);
                }
                _keyboard = InputSystem.AddDevice<Keyboard>("AeroSimTestKeyboard"); _mouse = InputSystem.AddDevice<Mouse>("AeroSimTestMouse");
                foreach (Page page in Enum.GetValues(typeof(Page)))
                {
                    if (page == Page.Flight || page == Page.Replay || page == Page.Report || page == Page.Results) continue;
                    _app.PreparePreview(page); yield return null;
                    Check("Route renders: " + page, _app.GetComponent<UIDocument>().rootVisualElement.Query<Button>().ToList().Count > 0);
                }
                _app.ShowPage(Page.FreeFlight); yield return null; yield return null;
                yield return Click("Launch Free Flight"); Check("Pointer click launches real flight", _app.CurrentPage == Page.Flight);
                if (_app.CurrentPage != Page.Flight) _app.StartFlight(new FlightConfig());
                var flightRoot = _app.GetComponent<UIDocument>().rootVisualElement;
                Check("Flight exposes left and right touch controls",
                    flightRoot.Q<VisualElement>(className: "stick-left") != null &&
                    flightRoot.Q<VisualElement>(className: "stick-right") != null &&
                    flightRoot.Query<VisualElement>(className: "touch-stick").ToList().Count >= 2);
                Check("Touch pointer owner contract rejects third and cross-stick pointers", _app.RunTouchPointerContractDiagnostic());
                var sim = _app.Simulator;
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.R)); yield return new WaitForSeconds(1.5f);
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); yield return null;
                Check("Keyboard throttle moves aircraft", sim.Position.y > 4);
                sim.Pause(true); Vector3 frozen = sim.Position; float time = sim.Elapsed; yield return new WaitForSeconds(.4f);
                Check("Pause freezes physics and clock", Vector3.Distance(sim.Position, frozen) < .001f && sim.Elapsed == time); sim.Pause(false);
                yield return Click("Pause"); Check("Pause button freezes session", sim.Paused);
                yield return Click("Resume Flight"); Check("Resume button unfreezes session", !sim.Paused);
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
                    r.aircraftSnapshot.displayName == "DJI Air 3S" && r.aircraftSnapshot.availableAtCapture &&
                    !string.IsNullOrEmpty(r.aircraftSnapshot.catalogVersion) &&
                    !string.IsNullOrEmpty(r.aircraftSnapshot.visualVersion) &&
                    !string.IsNullOrEmpty(r.aircraftSnapshot.physicsVersion) && r.aircraftSnapshot.capabilities.Count > 0);
                Check("Result screen is shown", _app.CurrentPage == Page.Results);
                if (r != null)
                {
                    _app.BeginReplay(r); sim = _app.Simulator; sim.Seek(r.duration * .5f); yield return null;
                    Check("Replay seeks into actual telemetry", sim.ReplayMode && Mathf.Abs(sim.ReplayTime - r.duration * .5f) < .01f);
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
                        Check("Wide layout caps primary content width",root.resolvedStyle.width<2200||(main!=null&&main.resolvedStyle.width<=1921));
                        Check("Wide Hangar caps aircraft card width",root.resolvedStyle.width<2200||(cards.Count>0&&cards.All(card=>card.resolvedStyle.width<=461)));
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
                _app.Simulator.Body.position = new Vector3(0, 8, 10); _app.Simulator.Pause(true); yield return new WaitForSeconds(.4f); yield return Capture(Path.Combine(dir, "aerial-Flight.png"));
                _app.Simulator.Pause(false); yield return Click("Pause"); yield return Capture(Path.Combine(dir,"aerial-Pause.png"));
                _app.StartFlight(new FlightConfig { workspace="FPV", task="gates", mode="Manual", environment="Desert Range", aircraft="5-inch Racer" });
                _app.Simulator.Body.position=new Vector3(0,8,10); _app.Simulator.Pause(true); yield return new WaitForSeconds(.4f); yield return Capture(Path.Combine(dir,"fpv-Flight.png"));
                _app.StartFlight(new FlightConfig { workspace="Professional", professional=true, task="inspection", environment="City Park", mission=new MissionDefinition() });
                _app.Simulator.Body.position=new Vector3(0,12,15); _app.Simulator.Pause(true); yield return new WaitForSeconds(.4f); yield return Capture(Path.Combine(dir,"professional-Flight.png"));
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
                        aircraftField!=null&&aircraftField.value==profile.Name&&
                        aircraftField.choices.SequenceEqual(runtimeProfiles.Select(entry=>entry.Name)));
                    if(profile.Id=="mavic3p")yield return Capture(Path.Combine(dir,"selected-free-flight.png"));
                    yield return Click("Launch Free Flight");yield return null;
                    var sim=_app.Simulator;var model=sim.Body.GetComponentInChildren<AircraftVisualModel>();
                    Check("Flight builds selected model with 4 rotors: "+profile.Id,model!=null&&model.Profile.Id==profile.Id&&model.Rotors.Count==4);
                    var nodes=model.GetComponentsInChildren<Transform>();
                    int lenses=nodes.Count(n=>n.name.EndsWith("optical glass"));
                    int expected=profile.Shape==AirframeShape.Mavic?3:profile.Shape==AirframeShape.Air?2:1;
                    Check("Distinct camera layout: "+profile.Id,lenses==expected);
                    if(profile.Shape==AirframeShape.Ducted||profile.Shape==AirframeShape.Micro)
                        Check("Four integrated prop guards: "+profile.Id,nodes.Count(n=>n.name=="Integrated prop guard")==4);
                    Check("No unsupported aircraft materials: "+profile.Id,model.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.isSupported));
                    Check("Aircraft shared material budget is six or fewer: "+profile.Id,model.DistinctSharedMaterialCount<=6);
                    sim.Body.position=new Vector3(0,8,10);sim.Pause(true);sim.Body.transform.position=sim.Body.position;yield return new WaitForSeconds(.3f);
                    yield return SceneImage(Path.Combine(dir,profile.Id+"-detail.png"),sim.Position+new Vector3(1.70f,.85f,1.95f)*profile.VisualScale,sim.Position,38);
                    yield return Capture(Path.Combine(dir,profile.Id+"-flight.png"));
                    Check("Course launch keeps selected aircraft: "+profile.Id,CourseCatalog.Find("a10").Config().aircraft==profile.Name);
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
                    Check("Workspace change preserves aircraft: "+profile.Id,aircraftField!=null&&aircraftField.value==profile.Name);
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
                    var details=card?.Q<Foldout>();if(details!=null)details.value=true;
                    bool lockReasonVisible = card != null && card.Query<Label>().ToList().Any(label =>
                        label.text != null && label.text.Contains("LOCKED") && label.text.Contains("Runtime access is not approved"));
                    Check("Candidate search and details remain browsable: "+candidateId,
                        search!=null&&card!=null&&card.resolvedStyle.display!=DisplayStyle.None&&details!=null&&details.value);
                    Check("Candidate is visible and locked in Hangar: "+candidateId,
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
                foreach(var toggle in _app.GetComponent<UIDocument>().rootVisualElement.Query<Toggle>().ToList())toggle.value=true;
                yield return Click("Start Session");yield return null;
                Check("Professional mission uses selected aircraft",_app.CurrentPage==Page.Flight&&_app.Simulator.Config.aircraft=="DJI Avata 2");
                _app.ShowPage(Page.Challenges);yield return null;yield return null;
                yield return Click("Start Challenge");yield return null;
                Check("Challenge uses selected aircraft",_app.CurrentPage==Page.Flight&&_app.Simulator.Config.aircraft=="DJI Avata 2");
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
