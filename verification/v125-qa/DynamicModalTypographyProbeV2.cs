// Independent native typography v2: flush rapid pointer-up through UITK event dispatch.
// Frozen v1 remains unchanged; every original assertion and product entry path is retained.
// Product dialogs are reached only through queued InputSystem pointer events.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
            if (_started || !Environment.GetCommandLineArgs().Contains("--dynamic-modal-typography-qa-v2")) return;
            _started = true;
            var host = new GameObject("Independent Dynamic Modal Typography QA");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<DynamicModalTypographyProbeV2>();
        }
    }

    [Serializable] public class TypographyCheck { public string rule, stage, evidence; public bool passed; }
    [Serializable] public class TypographyTrace { public string stage, evidence; }
    [Serializable] public class TypographyBaseline { public string dialog, key, text, type; public float font; }
    [Serializable] public class TypographyMeasurement
    {
        public string dialog, stage, key, text, type;
        public float multiplier, baselineFont, actualFont, expectedFont;
        public bool fontMatches, visible, scrollReadable;
        public Rect bounds, viewport; public Vector2 contentSize, measuredText;
    }
    [Serializable] public class TypographyReport
    {
        public string contract = "independent-dynamic-modal-typography-v2", status = "NOT_RUN", version;
        public string startedUtc = DateTime.UtcNow.ToString("O"), finishedUtc, storage, fatal;
        public string limitations = "Automated macOS native logical viewports; synthetic InputSystem pointer/key, not physical touch/gamepad/iOS/safeArea. Resolved font/scroll checks are not 23-concept visual acceptance. Fixture preferences intentionally change only inside isolated storage. No human model approval.";
        public int initialWidth, initialHeight, finalWidth, finalHeight, runtimeErrors;
        public List<TypographyBaseline> baselines = new();
        public List<TypographyCheck> checks = new();
        public List<TypographyTrace> traces = new();
        public List<TypographyMeasurement> measurements = new();
    }

    public sealed class DynamicModalTypographyProbeV2 : MonoBehaviour
    {
        private const float FontTolerance = .25f, LayoutTolerance = 3f;
        private AeroSimApplication _app;
        private VisualElement _root;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private string _directory;
        private readonly TypographyReport _report = new();
        private readonly Dictionary<string, Dictionary<string, float>> _baselines = new();
        private int _initialWidth, _initialHeight;
        private bool _finished;
        private sealed class Frozen
        {
            public SessionRecord record;
            public string recordJson, profileJson, disk;
        }
        private static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i < args.Length - 1 ? args[i + 1] : null;
        }
        private void OnEnable() => Application.logMessageReceived += Log;
        private void OnDisable() => Application.logMessageReceived -= Log;
        private void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            { _report.runtimeErrors++; Trace("runtime-error", message + " " + trace); }
        }
        private void Check(string rule, bool passed, string stage, string evidence = "") =>
            _report.checks.Add(new TypographyCheck { rule = rule, passed = passed, stage = stage, evidence = evidence });
        private void Trace(string stage, string evidence) => _report.traces.Add(new TypographyTrace { stage = stage, evidence = evidence });
        private IEnumerator Settle()
        { yield return new WaitForSecondsRealtime(.16f); yield return null; yield return null; }
        private static bool Visible(VisualElement e)
        {
            if (e == null || e.panel == null) return false;
            for (var p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden) return false;
            return e.worldBound.width > 0 && e.worldBound.height > 0;
        }
        private static string Title(Button b) => !string.IsNullOrWhiteSpace(b.text) ? b.text :
            b.Q<Label>(className: "design-button-label")?.text ?? b.tooltip ?? b.name;
        private Button FindButton(string title) => _root.Query<Button>().ToList().LastOrDefault(b =>
            Visible(b) && b.enabledInHierarchy && Title(b) == title);
        private bool HasDialog() => typeof(AeroSimApplication).GetField("_flightDialogClose", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_app) != null;
        private int ModalCount() => _root.Query<VisualElement>(className: "modal-overlay").ToList().Count(Visible);
        private VisualElement Overlay() => _root.Query<VisualElement>(className: "modal-overlay").ToList().LastOrDefault(Visible);
        private string State() => "page=" + _app.CurrentPage + " paused=" + _app.Simulator.Paused + " dialog=" + HasDialog() +
            " modalCount=" + ModalCount() + " width=" + Screen.width + " height=" + Screen.height;
        private static string KeyFor(VisualElement element, VisualElement overlay)
        {
            var parts = new List<string>();
            for (var e = element; e != null && e != overlay; e = e.parent)
                parts.Add(e.GetType().Name + "[" + (e.parent == null ? 0 : e.parent.IndexOf(e)) + "]#" + e.name);
            parts.Reverse(); return string.Join("/", parts);
        }
        // Include all nonempty TextElements, even if hidden/clipped, so a missing
        // child label cannot quietly disappear from the font/readability contract.
        private static List<TextElement> Texts(VisualElement overlay) => overlay == null ? new() :
            overlay.Query<TextElement>().ToList().Where(t => !string.IsNullOrWhiteSpace(t.text)).ToList();
        private IEnumerator Click(string title, string stage, bool settleAfter = true)
        {
            var button = FindButton(title);
            Check("pointer.target", button != null, stage, title + " " + State());
            if (button == null) yield break;
            var scroll = button.GetFirstAncestorOfType<ScrollView>(); scroll?.ScrollTo(button);
            yield return Settle();
            var point = button.worldBound.center;
            var viewport = scroll?.contentViewport.worldBound ?? _root.worldBound;
            var picked = _root.panel.Pick(point); bool hit = false; string path = "";
            for (var e = picked; e != null; e = e.parent)
            { path += e.GetType().Name + "#" + e.name + " > "; if (e == button) hit = true; }
            bool contained = button.worldBound.xMin >= viewport.xMin - LayoutTolerance && button.worldBound.xMax <= viewport.xMax + LayoutTolerance &&
                button.worldBound.yMin >= viewport.yMin - LayoutTolerance && button.worldBound.yMax <= viewport.yMax + LayoutTolerance;
            Check("pointer.scrolled-button-contained", contained, stage, button.worldBound + " viewport=" + viewport);
            Check("pointer.pick-is-target-button", hit, stage, path);
            Trace(stage + " before-pointer", title + " " + State() + " pick=" + path);
            float scale = _app.GetComponent<UIDocument>().panelSettings.scale;
            var position = new Vector2(point.x * scale, Screen.height - point.y * scale);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }); yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left)); yield return null; yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            if (settleAfter) yield return Settle();
            else
            {
                // A coroutine resumes before UI Toolkit processes this frame's
                // queued pointer-up. Flush that queue; do not add the .16s settle.
                yield return null; yield return new WaitForEndOfFrame(); yield return null;
            }
            Trace(stage + " after-pointer", State());
        }
        private IEnumerator Escape()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); yield return Settle();
        }
        private string OpenTitle(string dialog) => dialog == "confirm" ? "Exit Without Saving" : dialog == "objective" ? "Session Objective" : "Flight Telemetry";
        private string CloseTitle(string dialog) => dialog == "confirm" ? "Keep Flying" : "Back to Flight";
        private IEnumerator Open(string dialog, string stage, bool settleAfter = true)
        {
            yield return Click(OpenTitle(dialog), stage + " open", settleAfter);
            Check("modal.opens-nested-paused", _app.CurrentPage == Page.Flight && _app.Simulator.Paused && HasDialog() && ModalCount() == 2,
                stage, State());
        }
        private IEnumerator Close(string dialog, string stage, Frozen frozen, bool escape = false, bool settleAfter = true)
        {
            if (escape) yield return Escape(); else yield return Click(CloseTitle(dialog), stage + " close", settleAfter);
            Check("modal.cancel-restores-pause", _app.CurrentPage == Page.Flight && _app.Simulator.Paused && !HasDialog() && ModalCount() == 1,
                stage, State());
            Unchanged(frozen, stage);
        }
        private Frozen Snapshot() => new Frozen { record = _app.Simulator.Record, recordJson = JsonUtility.ToJson(_app.Simulator.Record),
            profileJson = JsonUtility.ToJson(LocalStore.Profile), disk = File.ReadAllText(Path.Combine(LocalStore.DirectoryPath, "profile.json")) };
        private void Unchanged(Frozen frozen, string stage)
        {
            Check("cancel.record-reference-and-json-unchanged", ReferenceEquals(frozen.record, _app.Simulator.Record) &&
                frozen.recordJson == JsonUtility.ToJson(_app.Simulator.Record), stage);
            Check("cancel.profile-memory-and-disk-unchanged", frozen.profileJson == JsonUtility.ToJson(LocalStore.Profile) &&
                frozen.disk == File.ReadAllText(Path.Combine(LocalStore.DirectoryPath, "profile.json")), stage);
        }
        private IEnumerator Fresh(float multiplier, string stage)
        {
            var settings = LocalStore.Profile.settings; LocalStore.Profile.onboarded = true;
            settings.uiScale = settings.hudScale = "100%"; settings.textScale = multiplier;
            settings.osdSpeed = settings.osdAltitude = settings.osdBattery = settings.osdTimer = true;
            LocalStore.Save();
            _app.ChangeWorkspace("Aerial");
            _app.StartFlight(new FlightConfig { workspace = "Aerial", title = "QA dynamic typography fixture — NOT pilot achievement" });
            _app.Simulator.Body.position = new Vector3(10, 15, 20);
            yield return Settle(); yield return Settle();
            yield return Click("Pause", stage + " pause");
            Check("pause.opens-before-modal", _app.Simulator.Paused && !HasDialog() && ModalCount() == 1, stage, State());
        }
        private void CaptureBaseline(string dialog)
        {
            var overlay = Overlay(); var texts = Texts(overlay); var fonts = new Dictionary<string, float>();
            Check("baseline.nonempty-text-tree", overlay != null && texts.Count >= (dialog == "confirm" ? 4 : dialog == "objective" ? 3 : 9), dialog, "count=" + texts.Count);
            foreach (var text in texts)
            {
                string key = KeyFor(text, overlay); float font = text.resolvedStyle.fontSize;
                Check("baseline.unique-key-positive-resolved-font", !fonts.ContainsKey(key) && font > 0 && !float.IsNaN(font) && !float.IsInfinity(font), dialog, key + " font=" + font);
                fonts[key] = font;
                _report.baselines.Add(new TypographyBaseline { dialog = dialog, key = key, text = text.text, type = text.GetType().Name, font = font });
            }
            _baselines[dialog] = fonts;
        }
        private IEnumerator Audit(string dialog, float multiplier, string stage, bool readable = true)
        {
            var overlay = Overlay(); var texts = Texts(overlay); var fonts = _baselines[dialog];
            var keys = texts.Select(t => KeyFor(t, overlay)).ToList();
            Check("text-tree.same-keys-as-independent-100-baseline", keys.Count == fonts.Count && keys.Distinct().Count() == keys.Count && keys.All(fonts.ContainsKey),
                stage, "baseline=" + fonts.Count + " current=" + keys.Count);
            string[] semantic = dialog == "confirm" ? new[] { "Leave this flight?", "The current session will not be saved.", "Keep Flying", "Confirm Exit" } :
                dialog == "objective" ? new[] { "Session objective", "Back to Flight" } :
                new[] { "Flight Telemetry", "Actual simulator values", "Altitude   ", "Distance from home   ", "Horizontal speed   ", "Vertical speed   ", "Battery   ", "Elapsed   ", "Back to Flight" };
            foreach (string role in semantic) Check("text-tree.semantic-role-present", texts.Any(t => t.text.Contains(role)), stage, role);
            foreach (var text in texts)
            {
                string key = KeyFor(text, overlay); bool known = fonts.TryGetValue(key, out float baseline);
                float actual = text.resolvedStyle.fontSize, expected = baseline * multiplier;
                bool fontMatches = known && Mathf.Abs(actual - expected) <= FontTolerance;
                Check("font.independent-baseline-times-multiplier", fontMatches, stage,
                    key + " text=" + text.text + " base=" + baseline + " actual=" + actual + " expected=" + expected);
                var scroll = text.GetFirstAncestorOfType<ScrollView>();
                if (readable) { scroll?.ScrollTo(text); yield return Settle(); }
                var viewport = scroll?.contentViewport.worldBound ?? _root.worldBound;
                var measured = text.resolvedStyle.whiteSpace == WhiteSpace.NoWrap ?
                    text.MeasureTextSize(text.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined) :
                    text.MeasureTextSize(text.text, text.contentRect.width, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                bool visible = Visible(text);
                bool fits = visible && text.contentRect.height + LayoutTolerance >= measured.y &&
                    text.contentRect.width + LayoutTolerance >= measured.x && text.worldBound.xMin >= viewport.xMin - LayoutTolerance &&
                    text.worldBound.xMax <= viewport.xMax + LayoutTolerance && text.worldBound.yMin >= viewport.yMin - LayoutTolerance &&
                    text.worldBound.yMax <= viewport.yMax + LayoutTolerance;
                if (readable) Check("text.scroll-readable-without-linebox-clipping", fits, stage,
                    key + " bounds=" + text.worldBound + " viewport=" + viewport + " content=" + text.contentRect.size + " measured=" + measured);
                _report.measurements.Add(new TypographyMeasurement { dialog = dialog, stage = stage, key = key, text = text.text,
                    type = text.GetType().Name, multiplier = multiplier, baselineFont = baseline, actualFont = actual, expectedFont = expected,
                    fontMatches = fontMatches, visible = visible, scrollReadable = fits, bounds = text.worldBound, viewport = viewport,
                    contentSize = text.contentRect.size, measuredText = measured });
            }
        }
        private IEnumerator Capture(string name)
        {
            var overlay = Overlay(); var scroll = overlay?.Q<ScrollView>();
            if (scroll != null) scroll.scrollOffset = Vector2.zero;
            yield return Settle();
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory, name + ".png"));
            yield return new WaitForEndOfFrame(); yield return Settle();
        }
        private IEnumerator ChangeText(float multiplier)
        {
            LocalStore.Profile.settings.textScale = multiplier; LocalStore.Save(); _app.RefreshUiPreferences(); yield return Settle(); yield return Settle();
        }
        private IEnumerator Resize(int width, int height, string stage)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            float until = Time.realtimeSinceStartup + 4f;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < until) yield return null;
            yield return Settle(); yield return Settle();
            Check("viewport.resize-actually-applied", Screen.width == width && Screen.height == height, stage,
                "requested=" + width + "x" + height + " actual=" + Screen.width + "x" + Screen.height);
            Trace(stage, State());
        }
        private IEnumerator Case(string dialog, float multiplier)
        {
            string stage = dialog + "-Text" + Mathf.RoundToInt(multiplier * 100);
            yield return Fresh(multiplier, stage); var frozen = Snapshot();
            yield return Open(dialog, stage);
            yield return Audit(dialog, multiplier, stage + "-initial"); yield return Capture(stage + "-initial");
            for (int i = 0; i < 3; i++) _app.RefreshUiPreferences();
            yield return Settle(); yield return Settle();
            yield return Audit(dialog, multiplier, stage + "-reapply3", false); Unchanged(frozen, stage + "-reapply3");
            int otherWidth = _initialWidth < 1200 ? 1280 : 1024, otherHeight = _initialWidth < 1200 ? 720 : 768;
            yield return Resize(otherWidth, otherHeight, stage + "-resize-other");
            yield return Audit(dialog, multiplier, stage + "-resize-other"); Unchanged(frozen, stage + "-resize-other");
            yield return Resize(_initialWidth, _initialHeight, stage + "-resize-back");
            yield return Audit(dialog, multiplier, stage + "-resize-back", false); Unchanged(frozen, stage + "-resize-back");
            if (Mathf.Approximately(multiplier, 1))
            {
                yield return ChangeText(2); frozen = Snapshot();
                yield return Audit(dialog, 2, stage + "-live100to200"); yield return Capture(stage + "-live100to200");
                for (int i = 0; i < 3; i++) _app.RefreshUiPreferences(); yield return Settle(); yield return Settle();
                yield return Audit(dialog, 2, stage + "-live200-reapply3", false);
                yield return ChangeText(1); frozen = Snapshot();
                yield return Audit(dialog, 1, stage + "-live200to100"); yield return Capture(stage + "-live200to100");
            }
            yield return Close(dialog, stage, frozen);
            yield return Open(dialog, stage + "-reopen");
            yield return Audit(dialog, multiplier, stage + "-reopen");
            yield return Close(dialog, stage + "-reopen-Escape", frozen, true);
            // Actual repeated pointer opens/closes; no reflection Invoke callback.
            for (int i = 0; i < 2; i++)
            {
                yield return Open(dialog, stage + "-quick" + i, false);
                yield return Close(dialog, stage + "-quick" + i, frozen, false, false);
            }
            yield return Open(dialog, stage + "-after-quick");
            yield return Audit(dialog, multiplier, stage + "-after-quick", false);
            yield return Close(dialog, stage + "-after-quick", frozen);
        }
        private IEnumerator Lifecycle(string dialog)
        {
            string stage = dialog + "-component-lifecycle";
            yield return Fresh(1, stage); yield return Open(dialog, stage);
            LocalStore.Profile.settings.textScale = 2; LocalStore.Save();
            _app.RefreshUiPreferences(); // Schedule, then disable before the next frame.
            var frozen = Snapshot(); _app.enabled = false; yield return Settle();
            Check("lifecycle.component-disabled", !_app.isActiveAndEnabled, stage);
            Unchanged(frozen, stage + "-disabled");
            _app.enabled = true; yield return Settle(); yield return Settle();
            _root = _app.GetComponent<UIDocument>().rootVisualElement;
            Check("lifecycle.component-enabled-attached", _app.isActiveAndEnabled && _root.panel != null, stage, State());
            // OnEnable intentionally rebuilds Home. Test new pointer-reached flight
            // rather than incorrectly requiring the old dialog tree to survive.
            yield return Fresh(2, stage + "-new-flight"); frozen = Snapshot();
            yield return Open(dialog, stage + "-after-enable");
            yield return Audit(dialog, 2, stage + "-after-enable200"); yield return Capture(stage + "-after-enable200");
            yield return ChangeText(1); frozen = Snapshot(); yield return Audit(dialog, 1, stage + "-after-enable100");
            yield return ChangeText(2); frozen = Snapshot(); yield return Audit(dialog, 2, stage + "-after-enable200-again", false);
            yield return ChangeText(1); frozen = Snapshot(); yield return Audit(dialog, 1, stage + "-after-enable100-again", false);
            yield return Close(dialog, stage + "-after-enable-cancel", frozen);
        }
        private IEnumerator Run()
        {
            _directory = Arg("--dynamic-modal-typography-qa-v2"); string data = Arg("--data-dir");
            if (string.IsNullOrEmpty(_directory) || string.IsNullOrEmpty(data) || !Path.IsPathRooted(data) || !Path.IsPathRooted(_directory))
                throw new InvalidOperationException("Explicit absolute isolated --data-dir and --dynamic-modal-typography-qa-v2 output are required.");
            Directory.CreateDirectory(_directory); yield return Settle();
            _app = UnityEngine.Object.FindFirstObjectByType<AeroSimApplication>();
            if (_app == null) throw new InvalidOperationException("Native application was not found.");
            _root = _app.GetComponent<UIDocument>().rootVisualElement;
            string actualData = Path.GetFullPath(LocalStore.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar);
            string requestedData = Path.GetFullPath(data).TrimEnd(Path.DirectorySeparatorChar);
            string realData = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "AeroSim")).TrimEnd(Path.DirectorySeparatorChar);
            bool isolated = actualData == requestedData && actualData != realData && actualData != "/" &&
                !actualData.StartsWith(realData + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            Check("fixture.storage-is-isolated-before-mutation", isolated, "setup", actualData);
            if (!isolated) throw new InvalidOperationException("Refusing to mutate the real profile directory.");
            _report.storage = actualData; _report.version = Application.version;
            _initialWidth = _report.initialWidth = Screen.width; _initialHeight = _report.initialHeight = Screen.height;
            _keyboard = InputSystem.AddDevice<Keyboard>("Independent Typography QA keyboard");
            _mouse = InputSystem.AddDevice<Mouse>("Independent Typography QA mouse");
            foreach (string dialog in new[] { "confirm", "objective", "telemetry" })
            {
                yield return Fresh(1, dialog + "-baseline100"); var frozen = Snapshot();
                yield return Open(dialog, dialog + "-baseline100"); CaptureBaseline(dialog);
                yield return Audit(dialog, 1, dialog + "-baseline100"); yield return Capture(dialog + "-baseline100");
                yield return Close(dialog, dialog + "-baseline100", frozen);
                foreach (float multiplier in new[] { 1f, 1.25f, 1.5f, 2f }) yield return Case(dialog, multiplier);
                yield return Lifecycle(dialog);
            }
        }
        private IEnumerator Start()
        {
            // Flatten nested iterators so exceptions in any test become a report,
            // rather than an abandoned coroutine with no evidence file.
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object next = null; bool moved = false; Exception failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception ex) { failure = ex; }
                if (failure != null) { _report.fatal = failure.ToString(); Check("probe.completed-without-exception", false, "fatal", _report.fatal); break; }
                if (!moved) { stack.Pop(); continue; }
                if (next is IEnumerator child) stack.Push(child); else yield return next;
            }
            Check("probe.completed-all-stages", stack.Count == 0, "completion", "remaining iterators=" + stack.Count);
            Finish();
        }
        private void Finish()
        {
            if (_finished) return; _finished = true;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            _report.finalWidth = Screen.width; _report.finalHeight = Screen.height;
            _report.finishedUtc = DateTime.UtcNow.ToString("O");
            _report.status = _report.runtimeErrors == 0 && string.IsNullOrEmpty(_report.fatal) && _report.checks.Count > 0 && _report.checks.All(c => c.passed) ? "PASS_AUTOMATED_ONLY" : "FAIL";
            if (!string.IsNullOrEmpty(_directory))
                File.WriteAllText(Path.Combine(_directory, "dynamic-modal-typography-report.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("Independent Dynamic Modal Typography " + _report.status + " checks=" + _report.checks.Count + " fail=" + _report.checks.Count(c => !c.passed));
            Application.Quit(_report.status == "PASS_AUTOMATED_ONLY" ? 0 : 2);
        }
    }
}
