using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AeroSim.Domain;
using AeroSim.Input;
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
            if (!args.Contains("--capture-preview") && !args.Contains("--capture-all") && !args.Contains("--capture-graphics") && !args.Contains("--smoke-test")) return;
            var host = new GameObject("AeroSim Diagnostics"); UnityEngine.Object.DontDestroyOnLoad(host); host.AddComponent<Runner>();
        }
        private sealed class Runner : MonoBehaviour
        {
            private AeroSimApplication _app;
            private Keyboard _keyboard;
            private Mouse _mouse;
            private readonly List<string> _results = new();
            private int _failures;
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
                string dir = Argument("--capture-all");
                if (dir != null) yield return CaptureAll(dir);
                else if (Argument("--capture-preview") != null)
                {
                    string page = Argument("--page"); if (Enum.TryParse<Page>(page, out var destination)) _app.PreparePreview(destination);
                    if (Argument("--workspace") != null) { _app.ChangeWorkspace(Argument("--workspace")); if (page != null && Enum.TryParse<Page>(page, out destination)) _app.PreparePreview(destination); }
                    yield return null; yield return null; yield return Capture(Argument("--capture-preview"));
                }
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
                var button = root.Query<Button>().ToList().Find(b => b.text == title);
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
                foreach(var name in new[]{"meadow-grass-v2","coastal-rock-v2","aircraft-shell-v2","evergreen-needles-v2"})
                    Check("Art texture bundled: "+name, Resources.Load<Texture2D>("Art/MaterialsV2/"+name)!=null);
                foreach(var name in new[]{"TrainingSurface","LandscapeSurface","Atmosphere","OceanSurface","RotorBlur","NeedleFoliage"})
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
                _app.StartFlight(new FlightConfig { task = "gates", courseId = "test-gates", title = "QA · Gate circuit" }); yield return null;
                sim = _app.Simulator;
                var gates = new[] { new Vector3(0,8,25), new Vector3(20,10,50), new Vector3(30,12,85), new Vector3(0,10,110), new Vector3(-25,9,80), new Vector3(-15,8,40) };
                foreach (var position in gates) { sim.Body.position = position; sim.Body.linearVelocity = Vector3.zero; yield return new WaitForSeconds(.15f); }
                Check("Ordered gate objective completes", sim.GateIndex == 6 && sim.ObjectiveComplete);
                sim.Collision(8); Check("Collision updates score evidence", sim.Record.collisions == 1);
                sim.Finish(); yield return null;
                var r = LocalStore.Sessions().FirstOrDefault();
                Check("Session persists with score and telemetry", r != null && r.score == 92 && r.passed && r.frames.Count > 0);
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
                sim.Finish(); yield return null;
                LocalStore.Profile.settings.units = "Metric"; LocalStore.Save(); LocalStore.Initialize();
                Check("Profile reload preserves settings and sessions", LocalStore.Profile.settings.units == "Metric" && LocalStore.Sessions().Count == 2);
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
                    _app.PreparePreview(page); yield return null; yield return null; yield return Capture(Path.Combine(dir, "aerial-" + page + ".png"));
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
