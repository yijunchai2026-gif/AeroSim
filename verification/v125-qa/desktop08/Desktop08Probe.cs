// Independent supplemental desktop gate. Frozen Space/Pause/Replay probes remain unchanged.
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
            if (_started || !Environment.GetCommandLineArgs().Contains("--desktop08-qa")) return;
            _started = true; var host = new GameObject("Independent Desktop08 QA");
            UnityEngine.Object.DontDestroyOnLoad(host); host.AddComponent<Desktop08Probe>();
        }
    }
    [Serializable] public class Desktop08Check { public string rule, stage, evidence; public bool passed; }
    [Serializable] public class Desktop08Measurement
    {
        public string stage, role, text; public float font, expectedFont;
        public Rect bounds; public Vector2 content, required;
    }
    [Serializable] public class Desktop08Report
    {
        public string contract = "independent-desktop08-v1", status = "NOT_RUN", version, storage, fatal;
        public string startedUtc = DateTime.UtcNow.ToString("O"), finishedUtc;
        public string limitations = "Supplemental Aerial keyboard focus, Professional hero linebox/nonintersection, and Replay split-unit semantics in isolated macOS native logical viewports. Synthetic InputSystem keyboard only; public replay fixture setup is not user/device input. No 23-concept parity, physical controller, iPhone/iPad or final user acceptance proof. Existing failures and UNRENDERED remain separate.";
        public int width, height, runtimeErrors; public float panelScale;
        public List<Desktop08Check> checks = new(); public List<Desktop08Measurement> measurements = new(); public List<string> traces = new();
    }
    public sealed class Desktop08Probe : MonoBehaviour
    {
        private const float FontTolerance = .25f, LayoutTolerance = 3f;
        private readonly Desktop08Report _report = new();
        private AeroSimApplication _app; private VisualElement _root; private Keyboard _keyboard;
        private string _directory; private int _submits;
        private static string Arg(string key) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, key); return i >= 0 && i < a.Length - 1 ? a[i + 1] : null; }
        private void OnEnable() => Application.logMessageReceived += Log;
        private void OnDisable() => Application.logMessageReceived -= Log;
        private void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { _report.runtimeErrors++; _report.traces.Add(message + " " + trace); } }
        private void Check(string rule, bool pass, string stage, string evidence = "") => _report.checks.Add(new Desktop08Check { rule = rule, passed = pass, stage = stage, evidence = evidence });
        private IEnumerator Settle() { yield return new WaitForSecondsRealtime(.12f); yield return null; yield return null; }
        private static bool Displayed(VisualElement element)
        {
            if (element?.panel == null) return false;
            for (var p = element; p != null; p = p.parent) if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden) return false;
            return true;
        }
        private static bool Visible(VisualElement element) => Displayed(element) && element.worldBound.width > 0 && element.worldBound.height > 0;
        private static string Title(Button b) => b == null ? "none" : !string.IsNullOrWhiteSpace(b.text) ? b.text : b.Q<Label>(className: "design-button-label")?.text ?? b.tooltip ?? b.name;
        private VisualElement Focus() => _root.panel?.focusController.focusedElement as VisualElement;
        private List<VisualElement> Modals() => _root.Query<VisualElement>(className: "modal-overlay").ToList().Where(Visible).ToList();
        private static bool HasText(VisualElement scope, string text) => scope != null && scope.Query<TextElement>().ToList().Any(e => Displayed(e) && e.text == text);
        private string State() => "page=" + _app.CurrentPage + " paused=" + _app.Simulator.Paused + " modalCount=" + Modals().Count + " focus=" + Focus()?.GetType().Name + "#" + Focus()?.name + " title=" + Title(Focus() as Button) + " submits=" + _submits;
        private IEnumerator Key(Key key, bool shift = false)
        {
            InputSystem.QueueStateEvent(_keyboard, shift ? new KeyboardState(key, UnityEngine.InputSystem.Key.LeftShift) : new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); yield return Settle();
        }
        private IEnumerator Capture(string name)
        { ScreenCapture.CaptureScreenshot(Path.Combine(_directory, name + ".png")); yield return new WaitForEndOfFrame(); yield return null; }
        private string ProfileDisk() => File.ReadAllText(Path.Combine(LocalStore.DirectoryPath, "profile.json"));
        private void ProfileUnchanged(string memory, string disk, string stage) => Check("data.profile-memory-disk-unchanged", memory == JsonUtility.ToJson(LocalStore.Profile) && disk == ProfileDisk(), stage);
        private IEnumerator Fresh(float text, string stage)
        {
            LocalStore.Profile.settings.uiScale = LocalStore.Profile.settings.hudScale = "100%"; LocalStore.Profile.settings.textScale = text; LocalStore.Save();
            _app.ChangeWorkspace("Aerial"); _app.StartFlight(new FlightConfig { workspace = "Aerial", title = "QA desktop08 focus fixture — NOT pilot achievement" });
            _app.Simulator.Body.position = new Vector3(0, 15, 20); yield return Settle(); yield return Settle();
            Check("flight.fresh-running-empty-focus", _app.CurrentPage == Page.Flight && _app.Simulator.Running && !_app.Simulator.Paused && Modals().Count == 0 && Focus() == null, stage, State());
        }
        private bool PauseIsReal(string stage)
        {
            var modals = Modals(); var pause = modals.FirstOrDefault(m => HasText(m, "Flight paused"));
            bool real = _app.CurrentPage == Page.Flight && _app.Simulator.Paused && modals.Count == 1 && pause != null && HasText(pause, "Resume Flight") && !modals.Any(m => HasText(m, "Session objective"));
            Check("pause.actual-overlay-not-objective", real, stage, State() + " modalTexts=" + string.Join("|", modals.SelectMany(m => m.Query<TextElement>().ToList()).Select(t => t.text)));
            Check("pause.resume-focused-in-pause", pause != null && pause.Contains(Focus()) && Title(Focus() as Button) == "Resume Flight", stage, State());
            return real;
        }
        private IEnumerator EmptySpace(string stage)
        {
            var record = _app.Simulator.Record; bool paused = _app.Simulator.Paused, rth = _app.Simulator.ReturningHome; int photos = record.photos;
            Check("flight.empty-focus-before-space", Focus() == null, stage, State());
            string before = State(); yield return Key(UnityEngine.InputSystem.Key.Space);
            Check("flight.empty-space-does-not-act", _app.CurrentPage == Page.Flight && _app.Simulator.Running && ReferenceEquals(record, _app.Simulator.Record) && _app.Simulator.Paused == paused && _app.Simulator.ReturningHome == rth && record.photos == photos && Modals().Count == 0 && Focus() == null, stage, before + " -> " + State());
        }
        private IEnumerator CycleModal(VisualElement modal, int expectedButtons, string stage)
        {
            var buttons = modal?.Query<Button>().ToList().Where(b => Visible(b) && b.enabledInHierarchy).ToList() ?? new List<Button>();
            Check("modal.expected-keyboard-target-count", buttons.Count == expectedButtons, stage, "count=" + buttons.Count);
            var initial = Focus();
            foreach (bool shift in new[] { false, true })
            {
                for (int i = 0; i < expectedButtons + 1; i++)
                {
                    yield return Key(UnityEngine.InputSystem.Key.Tab, shift);
                    Check("modal.tab-focus-contained", modal != null && modal.Contains(Focus()) && Focus() is Button && Visible(Focus()), stage, "shift=" + shift + " step=" + i + " " + State());
                }
            }
            Check("modal.forward-reverse-cycle-restores-focus", ReferenceEquals(initial, Focus()), stage, State());
        }
        private IEnumerator FocusCase(float text)
        {
            string stage = "Aerial-Text" + Mathf.RoundToInt(text * 100);
            yield return Fresh(text, stage + "-first-Tab");
            yield return Key(UnityEngine.InputSystem.Key.Tab);
            Check("flight.first-intentional-tab-is-pause", Focus() is Button b && Visible(b) && b.name == "action-pause" && Title(b) == "Pause", stage, State());
            int submits = _submits; yield return Key(UnityEngine.InputSystem.Key.Space);
            Check("flight.intentional-space-one-submit", _submits == submits + 1, stage, State() + " delta=" + (_submits - submits));
            PauseIsReal(stage + "-first-Tab-Space"); yield return Capture(stage + "-first-Tab-Space");

            // Independent setup avoids a failed first route hiding modal coverage.
            yield return Fresh(text, stage + "-pause-modal"); yield return Key(UnityEngine.InputSystem.Key.Escape);
            bool realPause = PauseIsReal(stage + "-Escape-Pause");
            if (realPause)
            {
                var pause = Modals()[0]; var record = _app.Simulator.Record; string frozen = JsonUtility.ToJson(record), profile = JsonUtility.ToJson(LocalStore.Profile), disk = ProfileDisk();
                yield return CycleModal(pause, 7, stage + "-Pause");
                Check("modal.pause-tab-does-not-act", ReferenceEquals(record, _app.Simulator.Record) && frozen == JsonUtility.ToJson(record) && _app.Simulator.Paused && Modals().Count == 1, stage, State());
                ProfileUnchanged(profile, disk, stage + "-Pause-tabs");
                yield return Key(UnityEngine.InputSystem.Key.Enter);
                Check("pause.enter-resume-clears-modal-and-focus", !_app.Simulator.Paused && Modals().Count == 0 && Focus() == null, stage, State());
                yield return EmptySpace(stage + "-after-resume");
                foreach (var arrow in new[] { UnityEngine.InputSystem.Key.UpArrow, UnityEngine.InputSystem.Key.DownArrow, UnityEngine.InputSystem.Key.LeftArrow, UnityEngine.InputSystem.Key.RightArrow })
                { yield return Key(arrow); yield return EmptySpace(stage + "-" + arrow + "-then-Space"); }
            }
            yield return Fresh(text, stage + "-Objective"); yield return Key(UnityEngine.InputSystem.Key.Tab);
            var objective = _root.Q<Button>("action-objective"); bool hudObjective = Visible(objective);
            _report.traces.Add(stage + " HUD Objective visible=" + hudObjective);
            if (hudObjective)
            {
                for (int i = 0; i < 16 && !ReferenceEquals(Focus(), objective); i++) yield return Key(UnityEngine.InputSystem.Key.Tab);
                Check("objective.visible-hud-keyboard-reachable", ReferenceEquals(Focus(), objective), stage, State());
                if (ReferenceEquals(Focus(), objective)) yield return Key(UnityEngine.InputSystem.Key.Enter);
            }
            else
            {
                Check("objective.hidden-hud-first-pause-target", Title(Focus() as Button) == "Pause", stage, State());
                yield return Key(UnityEngine.InputSystem.Key.Enter); PauseIsReal(stage + "-Objective-via-Pause");
                yield return Key(UnityEngine.InputSystem.Key.Tab);
                Check("objective.pause-session-objective-keyboard-reachable", Title(Focus() as Button) == "Session Objective", stage, State());
                if (Title(Focus() as Button) == "Session Objective") yield return Key(UnityEngine.InputSystem.Key.Enter);
            }
            var modals = Modals(); var top = modals.LastOrDefault(); bool actualObjective = top != null && HasText(top, "Session objective") && HasText(top, "Back to Flight");
            Check("objective.actual-keyboard-opened-overlay", actualObjective && _app.CurrentPage == Page.Flight && _app.Simulator.Paused && top.Contains(Focus()), stage, State());
            if (actualObjective)
            {
                int countBefore = modals.Count; var record = _app.Simulator.Record; string frozen = JsonUtility.ToJson(record), profile = JsonUtility.ToJson(LocalStore.Profile), disk = ProfileDisk();
                yield return CycleModal(top, 1, stage + "-Objective");
                Check("modal.objective-tabs-do-not-act", Modals().Count == countBefore && _app.Simulator.Paused && frozen == JsonUtility.ToJson(record), stage, State());
                ProfileUnchanged(profile, disk, stage + "-Objective-tabs");
                yield return Key(UnityEngine.InputSystem.Key.Enter);
                Check("objective.close-removes-only-top", !Modals().Contains(top) && Modals().Count == countBefore - 1 && ReferenceEquals(record, _app.Simulator.Record), stage, State());
                if (countBefore == 2) { PauseIsReal(stage + "-Objective-close-to-Pause"); yield return Key(UnityEngine.InputSystem.Key.Escape); }
                Check("objective.close-resumes-empty-focus", !_app.Simulator.Paused && Modals().Count == 0 && Focus() == null, stage, State());
                yield return EmptySpace(stage + "-after-Objective-close");
            }
        }
        private void Measure(TextElement text, string role, float token, float multiplier, string stage)
        {
            Check("hero.target-present", text != null, stage, role); if (text == null) return;
            var content = text.contentRect; var required = text.MeasureTextSize(text.text, content.width, text.resolvedStyle.whiteSpace == WhiteSpace.Normal ? VisualElement.MeasureMode.Exactly : VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
            float actual = text.resolvedStyle.fontSize;
            string evidence = role + " text=" + text.text + " content=" + content + " required=" + required + " font=" + actual + " bounds=" + text.worldBound;
            Check("hero.positive-final-geometry", Displayed(text) && content.width > 0 && content.height > 0 && actual > 0, stage, evidence);
            Check("hero.final-width-linebox-height", content.height + LayoutTolerance >= required.y, stage, evidence);
            if (token > 0) Check("hero.fixed-uss-token-times-multiplier", Mathf.Abs(actual - token * multiplier) <= FontTolerance, stage, evidence + " expected=" + token * multiplier);
            _report.measurements.Add(new Desktop08Measurement { stage = stage, role = role, text = text.text, font = actual, expectedFont = token * multiplier, bounds = text.worldBound, content = content.size, required = required });
        }
        private IEnumerator Hero(float text)
        {
            string stage = "Professional-Home-Text" + Mathf.RoundToInt(text * 100);
            LocalStore.Profile.settings.textScale = text; LocalStore.Save(); _app.ChangeWorkspace("Professional"); _app.PreparePreview(Page.Home); yield return Settle(); yield return Settle();
            var hero = _root.Q<VisualElement>(className: "design-dashboard-hero");
            Check("hero.actual-professional-product-home", _app.CurrentPage == Page.Home && LocalStore.Profile.workspace == "Professional" && hero != null, stage);
            var title = hero?.Q<Label>(className: "panel-title"); var subtitle = hero?.Query<Label>().ToList().FirstOrDefault(e => e.text == "NEXT TRAINING TASK");
            var description = hero?.Q<Label>(className: "design-assignment-description"); var button = hero?.Query<Button>().ToList().FirstOrDefault(b => Title(b) == "Continue Assignment");
            Check("hero.stable-fixture-semantic-texts", title?.text == "Bridge Inspection Fundamentals" && description?.text == "Plan the mission, capture each target and review your recorded evidence.", stage, title?.text + " | " + description?.text);
            Measure(title, "title", 36, text, stage); Measure(subtitle, "subtitle", 0, text, stage); Measure(description, "description", 16, text, stage);
            Check("hero.continue-assignment-present", button != null && Visible(button), stage);
            var elements = new VisualElement[] { title, subtitle, description, button };
            for (int i = 0; i < elements.Length; i++) for (int j = i + 1; j < elements.Length; j++)
                Check("hero.final-bounds-do-not-intersect", elements[i] != null && elements[j] != null && !elements[i].worldBound.Overlaps(elements[j].worldBound), stage, "pair=" + i + "," + j + " a=" + elements[i]?.worldBound + " b=" + elements[j]?.worldBound);
            if (hero != null) _report.traces.Add(stage + " hero=" + hero.worldBound + " content=" + hero.contentRect + " parent=" + hero.parent?.worldBound + " root=" + _root.worldBound);
            yield return Capture(stage);
        }
        private IEnumerator Replay(float text, string units)
        {
            string stage = "Replay-" + units + "-Text" + Mathf.RoundToInt(text * 100);
            LocalStore.Profile.settings.textScale = text; LocalStore.Profile.settings.units = units; _app.ChangeWorkspace("Aerial"); LocalStore.Save();
            var record = new SessionRecord { id = "qa-desktop08-unit-fixture", title = "QA split-unit fixture — NOT pilot achievement", workspace = "Aerial", environment = "Pacific Coast", aircraft = "DJI Mini 4 Pro", duration = 20, passed = false, config = new FlightConfig() };
            record.frames.Add(new TelemetryFrame { time = 0, position = new Vector3(0, 8, 10), speed = 0, battery = 100 });
            record.frames.Add(new TelemetryFrame { time = 10, position = new Vector3(10, 12, 25), speed = 4, battery = 92 });
            record.frames.Add(new TelemetryFrame { time = 20, position = new Vector3(15, 16, 30), speed = 6, battery = 88 });
            string frozen = JsonUtility.ToJson(record), profile = JsonUtility.ToJson(LocalStore.Profile), disk = ProfileDisk();
            _app.BeginReplay(record); _app.Simulator.Seek(10); yield return Settle(); yield return Settle();
            Check("replay.actual-paused-product-tree", _app.CurrentPage == Page.Replay && !_app.Simulator.ReplayPlaying && Mathf.Abs(_app.Simulator.ReplayTime - 10) < .001f && _root.ClassListContains("v1-replay"), stage);
            var headings = _root.Query<VisualElement>(className: "v1-replay-chart-heading").ToList();
            Check("replay.exact-three-chart-headings", headings.Count == 3, stage, "count=" + headings.Count);
            string[] names = { "Altitude", "Ground Speed", "Battery" }, suffixes = { units == "Metric" ? "m" : "ft", units == "Metric" ? "km/h" : "mph", "%" };
            string[] expected = { (12f * (units == "Metric" ? 1 : 3.28084f)).ToString("F0"), (4f * (units == "Metric" ? 3.6f : 2.23694f)).ToString("F1"), "92" };
            for (int i = 0; i < names.Length; i++)
            {
                var matches = headings.Where(h => HasText(h, names[i])).ToList();
                Check("replay.semantic-heading-unique", matches.Count == 1, stage, names[i]);
                var heading = matches.FirstOrDefault(); var value = heading?.Query<Label>(className: "v1-replay-chart-value").ToList() ?? new List<Label>(); var unit = heading?.Query<Label>(className: "v1-replay-chart-unit").ToList() ?? new List<Label>();
                string evidence = names[i] + " value=" + string.Join("|", value.Select(v => v.text)) + " unit=" + string.Join("|", unit.Select(u => u.text)) + " expected=" + expected[i] + " " + suffixes[i];
                Check("replay.one-value-one-unit-element", value.Count == 1 && unit.Count == 1 && value.All(Visible) && unit.All(Visible), stage, evidence);
                Check("replay.numeric-value-without-unit-suffix", value.Count == 1 && value[0].text == expected[i], stage, evidence);
                Check("replay.single-correct-unit-label", unit.Count == 1 && unit[0].text == suffixes[i], stage, evidence);
                if (heading != null) _report.traces.Add(stage + " " + evidence + " renderedTexts=" + string.Join("|", heading.Query<TextElement>().ToList().Select(e => e.text)));
            }
            Check("data.replay-record-reference-json-unchanged", ReferenceEquals(record, _app.Simulator.Record) && frozen == JsonUtility.ToJson(record), stage);
            ProfileUnchanged(profile, disk, stage); yield return Capture(stage);
        }
        private IEnumerator Run()
        {
            _directory = Arg("--desktop08-qa"); string data = Arg("--data-dir");
            if (string.IsNullOrEmpty(_directory) || string.IsNullOrEmpty(data) || !Path.IsPathRooted(_directory) || !Path.IsPathRooted(data)) throw new InvalidOperationException("Explicit isolated absolute data/results required.");
            Directory.CreateDirectory(_directory); yield return Settle(); _app = UnityEngine.Object.FindFirstObjectByType<AeroSimApplication>();
            if (_app == null) throw new InvalidOperationException("Product application not found."); _root = _app.GetComponent<UIDocument>().rootVisualElement;
            string actual = Path.GetFullPath(LocalStore.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar), requested = Path.GetFullPath(data).TrimEnd(Path.DirectorySeparatorChar), real = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "AeroSim")).TrimEnd(Path.DirectorySeparatorChar);
            bool isolated = actual == requested && actual != real && actual != "/" && !actual.StartsWith(real + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            Check("fixture.isolated-before-mutation", isolated, "setup", actual); if (!isolated) throw new InvalidOperationException("Refusing real profile mutation.");
            _report.storage = actual; _report.version = Application.version; _report.width = Screen.width; _report.height = Screen.height; _report.panelScale = _app.GetComponent<UIDocument>().panelSettings.scale;
            Check("viewport.in-supplemental-matrix", Screen.width == 1440 && Screen.height == 900 || Screen.width == 1920 && Screen.height == 1080, "setup", Screen.width + "x" + Screen.height);
            _root.RegisterCallback<NavigationSubmitEvent>(e => _submits++, TrickleDown.TrickleDown); _keyboard = InputSystem.AddDevice<Keyboard>("Independent desktop08 synthetic keyboard");
            LocalStore.Profile.onboarded = true; LocalStore.Profile.settings.uiScale = LocalStore.Profile.settings.hudScale = "100%"; LocalStore.Save();
            foreach (float text in new[] { 1f, 2f }) { yield return FocusCase(text); yield return Hero(text); foreach (string units in new[] { "Imperial", "Metric" }) yield return Replay(text, units); }
        }
        private IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object next = null; bool moved = false; Exception failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; } catch (Exception ex) { failure = ex; }
                if (failure != null) { _report.fatal = failure.ToString(); Check("probe.exception", false, "fatal", _report.fatal); break; }
                if (!moved) { stack.Pop(); continue; } if (next is IEnumerator child) stack.Push(child); else yield return next;
            }
            Check("probe.all-stages-completed", stack.Count == 0, "completion"); if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _report.finishedUtc = DateTime.UtcNow.ToString("O"); _report.status = _report.runtimeErrors == 0 && string.IsNullOrEmpty(_report.fatal) && _report.checks.All(c => c.passed) ? "PASS_AUTOMATED_ONLY" : "FAIL";
            if (!string.IsNullOrEmpty(_directory)) File.WriteAllText(Path.Combine(_directory, "desktop08-report.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("Independent desktop08 " + _report.status + " checks=" + _report.checks.Count + " fail=" + _report.checks.Count(c => !c.passed)); Application.Quit(_report.status == "PASS_AUTOMATED_ONLY" ? 0 : 2);
        }
    }
}
