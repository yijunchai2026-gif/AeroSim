// Independent fixed-USS-token gate; do not use potentially incorrect binary Text100 as baseline.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using AeroSim;
using AeroSim.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace AeroSim.Qa
{
    public static class QaBootstrap
    {
        private static bool _started;
        public static void Initialize()
        {
            if (_started || !Environment.GetCommandLineArgs().Contains("--replay-typography-qa-v2")) return;
            _started = true; var host = new GameObject("Independent Replay USS Token QA v2");
            UnityEngine.Object.DontDestroyOnLoad(host); host.AddComponent<ReplayTypographyProbeV2>();
        }
    }
    [Serializable] public class ReplayFontCheck { public string rule, stage, evidence; public bool passed; }
    [Serializable] public class ReplayFontMeasurement
    {
        public string stage, role, text, source;
        public float token, multiplier, expected, actual;
        public Rect bounds; public Vector2 contentSize, measuredText;
    }
    [Serializable] public class ReplayFontReport
    {
        public string contract = "independent-replay-uss-typography-v2", status = "NOT_RUN", version;
        public string startedUtc = DateTime.UtcNow.ToString("O"), finishedUtc, storage, fatal;
        public string limitations = "Fixed frozen USS tokens: unchanged v1 product Records->Replay pointer flow plus Space-like public BeginReplay coroutine/predecessor flow in isolated macOS native app. Synthetic inputs, not physical iOS/gamepad or 23-concept visual acceptance. Fixture is NOT pilot achievement. Linebox checks do not prove pixel parity.";
        public int width, height, runtimeErrors;
        public List<ReplayFontCheck> checks = new(); public List<ReplayFontMeasurement> measurements = new();
        public List<string> traces = new();
    }
    public sealed class ReplayTypographyProbeV2 : MonoBehaviour
    {
        private const string FixtureId = "qa-replay-token-fixture";
        private const float FontTolerance = .25f, LayoutTolerance = 3f;
        private readonly ReplayFontReport _report = new();
        private AeroSimApplication _app; private VisualElement _root; private Mouse _mouse;
        private string _directory, _frozenRecordJson, _frozenSavedRecord;
        private static string Arg(string key) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, key); return i >= 0 && i < a.Length - 1 ? a[i + 1] : null; }
        private void OnEnable() => Application.logMessageReceived += Log;
        private void OnDisable() => Application.logMessageReceived -= Log;
        private void Log(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { _report.runtimeErrors++; _report.traces.Add(m + " " + s); } }
        private void Check(string rule, bool pass, string stage, string evidence = "") => _report.checks.Add(new ReplayFontCheck { rule = rule, passed = pass, stage = stage, evidence = evidence });
        private IEnumerator SpaceSettle() { yield return new WaitForSecondsRealtime(.10f); yield return null; yield return null; }
        private IEnumerator Settle() { yield return new WaitForSecondsRealtime(.12f); yield return null; yield return null; }
        private static bool Visible(VisualElement e)
        {
            if (e?.panel == null) return false;
            for (var p = e; p != null; p = p.parent) if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden) return false;
            return e.worldBound.width > 0 && e.worldBound.height > 0;
        }
        private static string Title(Button b) => !string.IsNullOrWhiteSpace(b.text) ? b.text :
            b.Q<Label>(className: "nav-label")?.text ?? b.Q<Label>(className: "design-button-label")?.text ?? b.tooltip ?? b.name;
        private IEnumerator Click(string title, string stage)
        {
            var b = _root.Query<Button>().ToList().LastOrDefault(e => Visible(e) && e.enabledInHierarchy && Title(e) == title);
            Check("pointer.target", b != null, stage, title); if (b == null) yield break;
            var scroll = b.GetFirstAncestorOfType<ScrollView>(); scroll?.ScrollTo(b); yield return Settle();
            Rect viewport = scroll?.contentViewport.worldBound ?? _root.worldBound;
            bool contained = b.worldBound.xMin >= viewport.xMin - LayoutTolerance && b.worldBound.xMax <= viewport.xMax + LayoutTolerance && b.worldBound.yMin >= viewport.yMin - LayoutTolerance && b.worldBound.yMax <= viewport.yMax + LayoutTolerance;
            if (!contained && scroll != null) { scroll.ScrollTo(b); yield return Settle(); viewport = scroll.contentViewport.worldBound; }
            contained = b.worldBound.xMin >= viewport.xMin - LayoutTolerance && b.worldBound.xMax <= viewport.xMax + LayoutTolerance && b.worldBound.yMin >= viewport.yMin - LayoutTolerance && b.worldBound.yMax <= viewport.yMax + LayoutTolerance;
            var point = b.worldBound.center; var pick = _root.panel.Pick(point); bool hit = false;
            for (var e = pick; e != null; e = e.parent) if (e == b) hit = true;
            Check("pointer.contained", contained, stage, b.worldBound + " viewport=" + viewport);
            Check("pointer.pick-is-real-button", hit, stage, "picked=" + pick?.GetType().Name + "#" + pick?.name);
            float scale = _app.GetComponent<UIDocument>().panelSettings.scale; var p = new Vector2(point.x * scale, Screen.height - point.y * scale);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = p }); yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = p }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left)); yield return null; yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = p }); yield return Settle();
        }
        private string ProfileDisk() => File.ReadAllText(Path.Combine(LocalStore.DirectoryPath, "profile.json"));
        private string SavedRecord() => File.ReadAllText(Path.Combine(LocalStore.DirectoryPath, FixtureId + ".json"));
        private void Unchanged(string profile, string disk, string stage)
        {
            Check("profile.memory-and-disk-unchanged", profile == JsonUtility.ToJson(LocalStore.Profile) && disk == ProfileDisk(), stage);
            Check("saved-fixture-record-unchanged", _frozenSavedRecord == SavedRecord(), stage);
            if (_app.CurrentPage == Page.Replay)
                Check("replay.source-record-unchanged", _app.Simulator.Record?.id == FixtureId && _frozenRecordJson == JsonUtility.ToJson(_app.Simulator.Record), stage);
        }
        private void Measure(TextElement e, string role, float token, float multiplier, string source, string stage, bool linebox)
        {
            float actual = e.resolvedStyle.fontSize, expected = token * multiplier;
            Check("font.frozen-uss-token-times-multiplier", Mathf.Abs(actual - expected) <= FontTolerance, stage,
                role + " " + e.text + " token=" + token + " actual=" + actual + " expected=" + expected);
            var measured = e.MeasureTextSize(e.text, e.contentRect.width, e.resolvedStyle.whiteSpace == WhiteSpace.Normal ? VisualElement.MeasureMode.Exactly : VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
            if (linebox) Check("text.positive-readable-linebox", Visible(e) && e.contentRect.height + LayoutTolerance >= measured.y && e.contentRect.width + LayoutTolerance >= measured.x,
                stage, role + " " + e.text + " bounds=" + e.worldBound + " content=" + e.contentRect.size + " required=" + measured);
            _report.measurements.Add(new ReplayFontMeasurement { stage = stage, role = role, text = e.text, source = source, token = token, multiplier = multiplier, actual = actual, expected = expected,
                bounds = e.worldBound, contentSize = e.contentRect.size, measuredText = measured });
        }
        private void Audit(float multiplier, string stage, bool replay, bool linebox)
        {
            Check("page.actual-product-tree", _app.CurrentPage == (replay ? Page.Replay : Page.Home) && _root.ClassListContains("design-v1"), stage, _app.CurrentPage.ToString());
            var nav = _root.Q<ScrollView>(className: "side-nav")?.Query<Label>(className: "nav-label").ToList() ?? new List<Label>();
            Check("source-count.six-aerial-navigation-labels", nav.Count == 6 && new[] { "Home", "Academy", "Free Flight", "Challenges", "Hangar", "Flight Records" }.All(t => nav.Count(e => e.text == t) == 1), stage, "count=" + nav.Count);
            foreach (var e in nav) Measure(e, "side-nav", 17, multiplier, "Product.uss:303", stage, linebox);
            if (!replay) return;
            var actions = _root.Query<Label>(className: "design-button-label").ToList().Where(e => e.GetFirstAncestorOfType<Button>()?.ClassListContains("v1-flight-action") == true).ToList();
            Check("source-count.twelve-replay-action-child-labels", actions.Count == 12, stage, "count=" + actions.Count);
            foreach (string t in new[] { "Back", "Export CSV", "Pilot View", "Third Person", "Map", "−10s", "Play", "+10s", "Practice Again", "Add Note", "Play / Pause" })
                Check("source-count.action-semantic-present", actions.Count(e => e.text == t) == 1, stage, t);
            foreach (var e in actions) Measure(e, "replay-action-child", 16, multiplier, "FlightDesignV1.uss:11", stage, linebox);
            var disclaimer = _root.Query<Label>(className: "v1-replay-disclaimer").ToList();
            Check("source-count.exact-one-disclaimer", disclaimer.Count == 1 && disclaimer[0].text == "LOCAL RECORDED SESSION · SIMULATION — NOT FOR NAVIGATION", stage, "count=" + disclaimer.Count);
            foreach (var e in disclaimer) Measure(e, "replay-disclaimer", 12, multiplier, "FlightDesignV1.uss:129", stage, linebox);
        }
        private IEnumerator Capture(string name)
        { ScreenCapture.CaptureScreenshot(Path.Combine(_directory, name + ".png")); yield return new WaitForEndOfFrame(); yield return null; }
        private IEnumerator Run()
        {
            _directory = Arg("--replay-typography-qa-v2"); string data = Arg("--data-dir");
            if (string.IsNullOrEmpty(_directory) || string.IsNullOrEmpty(data) || !Path.IsPathRooted(_directory) || !Path.IsPathRooted(data)) throw new InvalidOperationException("Explicit isolated absolute data/results required.");
            Directory.CreateDirectory(_directory); yield return Settle();
            _app = UnityEngine.Object.FindFirstObjectByType<AeroSimApplication>(); _root = _app.GetComponent<UIDocument>().rootVisualElement;
            string actual = Path.GetFullPath(LocalStore.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar), requested = Path.GetFullPath(data).TrimEnd(Path.DirectorySeparatorChar), real = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "AeroSim")).TrimEnd(Path.DirectorySeparatorChar);
            bool isolated = actual == requested && actual != real && actual != "/" && !actual.StartsWith(real + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            Check("fixture.isolated-before-mutation", isolated, "setup", actual); if (!isolated) throw new InvalidOperationException("Refusing real profile mutation.");
            _report.storage = actual; _report.version = Application.version; _report.width = Screen.width; _report.height = Screen.height;
            _mouse = InputSystem.AddDevice<Mouse>("Independent Replay Typography QA mouse");
            LocalStore.Profile.onboarded = true; LocalStore.Profile.settings.uiScale = LocalStore.Profile.settings.hudScale = "100%"; _app.ChangeWorkspace("Aerial"); LocalStore.Save();
            var record = new SessionRecord { id = FixtureId, startedUtc = "2026-10-10T00:00:00Z", title = "QA USS token fixture — NOT pilot achievement", workspace = "Aerial", environment = "Pacific Coast", aircraft = "DJI Mini 4 Pro", mode = "Normal", duration = 20, passed = false, config = new FlightConfig() };
            record.frames.Add(new TelemetryFrame { time = 0, position = new Vector3(0, 8, 10), battery = 100 });
            record.frames.Add(new TelemetryFrame { time = 20, position = new Vector3(10, 12, 25), battery = 99 });
            record.events.Add(new FlightEvent { time = 10, kind = "qa-synthetic", message = "QA fixture event" });
            LocalStore.SaveSession(record); _frozenRecordJson = JsonUtility.ToJson(record); _frozenSavedRecord = SavedRecord();
            foreach (float multiplier in new[] { 1f, 1.25f, 1.5f, 2f })
            {
                string stage = "Text" + Mathf.RoundToInt(multiplier * 100);
                LocalStore.Profile.settings.textScale = multiplier; LocalStore.Save();
                _app.ShowPage(Page.Home); yield return Settle(); yield return Settle(); Audit(multiplier, stage + "-Home", false, true);
                string profile = JsonUtility.ToJson(LocalStore.Profile), disk = ProfileDisk();
                yield return Click("Flight Records", stage + "-Records"); yield return Click("Replay", stage + "-Replay");
                yield return Settle(); Audit(multiplier, stage + "-Replay", true, true); Unchanged(profile, disk, stage + "-Replay");
                yield return Capture(stage + "-Replay");
                for (int i = 0; i < 3; i++) _app.RefreshUiPreferences(); yield return Settle(); yield return Settle();
                Audit(multiplier, stage + "-Replay-reapply3", true, false); Unchanged(profile, disk, stage + "-Replay-reapply3");
                yield return Click("Home", stage + "-return-Home"); Audit(multiplier, stage + "-return-Home", false, true); Unchanged(profile, disk, stage + "-return-Home");
                yield return Click("Flight Records", stage + "-reopen-Records"); yield return Click("Replay", stage + "-reopen-Replay");
                yield return Settle(); Audit(multiplier, stage + "-reopen-Replay", true, true); Unchanged(profile, disk, stage + "-reopen-Replay");
            }

            // Additional public-product entry reproduces the predecessor and coroutine phase
            // of old Space V2. It does not replace any above v1 Pointer assertion.
            foreach (float multiplier in new[] { 1f, 1.25f, 1.5f, 2f })
            {
                string stage = "Text" + Mathf.RoundToInt(multiplier * 100) + "-SpaceCoroutine";
                LocalStore.Profile.settings.textScale = multiplier; LocalStore.Save();
                foreach (string state in new[] { "FPV-Tuning", "Aerial-Settings", "Aerial-Controller", "Professional-Home" })
                {
                    string workspace = state.StartsWith("FPV-") ? "FPV" : state.StartsWith("Professional-") ? "Professional" : "Aerial";
                    _app.ChangeWorkspace(workspace);
                    _app.PreparePreview(state == "FPV-Tuning" ? Page.Tuning : state == "Aerial-Settings" ? Page.Settings : state == "Aerial-Controller" ? Page.Controller : Page.Home);
                    yield return SpaceSettle(); yield return SpaceSettle();
                    Check("coroutine.predecessor-real-product-page", _app.CurrentPage == (state == "FPV-Tuning" ? Page.Tuning : state == "Aerial-Settings" ? Page.Settings : state == "Aerial-Controller" ? Page.Controller : Page.Home), stage + "-" + state, "frame=" + Time.frameCount + " page=" + _app.CurrentPage);
                }
                _app.ChangeWorkspace("Aerial");
                string profile = JsonUtility.ToJson(LocalStore.Profile), disk = ProfileDisk();
                _report.traces.Add(stage + " BeginReplay before frame=" + Time.frameCount);
                _app.BeginReplay(record); yield return SpaceSettle(); yield return SpaceSettle();
                _report.traces.Add(stage + " BeginReplay audit frame=" + Time.frameCount);
                Audit(multiplier, stage + "-Replay", true, true); Unchanged(profile, disk, stage + "-Replay");
                yield return Capture(stage + "-Replay");
                for (int i = 0; i < 3; i++) _app.RefreshUiPreferences(); yield return SpaceSettle(); yield return SpaceSettle();
                Audit(multiplier, stage + "-reapply3", true, false); Unchanged(profile, disk, stage + "-reapply3");
                _app.ShowPage(Page.Home); yield return SpaceSettle(); yield return SpaceSettle();
                Audit(multiplier, stage + "-return-Home", false, true); Unchanged(profile, disk, stage + "-return-Home");
                _app.BeginReplay(record); yield return SpaceSettle(); yield return SpaceSettle();
                Audit(multiplier, stage + "-reopen-Replay", true, true); Unchanged(profile, disk, stage + "-reopen-Replay");
            }
        }
        private IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object next = null; bool moved = false; Exception failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; } catch (Exception ex) { failure = ex; }
                if (failure != null) { _report.fatal = failure.ToString(); Check("probe.exception", false, "fatal", _report.fatal); break; }
                if (!moved) { stack.Pop(); continue; }
                if (next is IEnumerator child) stack.Push(child); else yield return next;
            }
            Check("probe.all-stages-completed", stack.Count == 0, "completion");
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            _report.finishedUtc = DateTime.UtcNow.ToString("O");
            _report.status = _report.runtimeErrors == 0 && string.IsNullOrEmpty(_report.fatal) && _report.checks.All(e => e.passed) ? "PASS_AUTOMATED_ONLY" : "FAIL";
            if (!string.IsNullOrEmpty(_directory)) File.WriteAllText(Path.Combine(_directory, "replay-typography-v2-report.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("Replay USS Token QA " + _report.status + " checks=" + _report.checks.Count + " fail=" + _report.checks.Count(e => !e.passed));
            Application.Quit(_report.status == "PASS_AUTOMATED_ONLY" ? 0 : 2);
        }
    }
}

