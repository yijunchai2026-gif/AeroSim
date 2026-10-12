using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AeroSim.Diagnostics;
using AeroSim.Domain;
using AeroSim.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace AeroSim
{
    public sealed partial class AeroSimApplication
    {
        private VisualLabProfiler _visualLabProfiler;
        private VisualLabEvidenceManifest _visualLabManifest;
        private RenderTexture _visualLabLive;
        private readonly Dictionary<string, Texture2D> _visualLabSlots = new();
        private string _visualLabShot = "A";
        private float _visualLabDistance = 15;
        private VisualLabCompareMode _visualLabCompare = VisualLabCompareMode.CandidateA;
        private string _visualLabLastExport = "Not exported";
        private string _visualLabWeather = "Clear";
        private int _visualLabQuality = AeroSimQuality.High;

        private void EnterVisualLab()
        {
            ReleaseVisualLab();
            LocalStore.Profile.settings.quality = _visualLabQuality;
            LocalStore.Save();
            var config = new FlightConfig
            {
                environment = "Pacific Coast", weather = _visualLabWeather, timeOfDay = 16.5f, windMph = 0,
                environmentSeed = 41001, title = "RD-09 Visual Lab · Fixed state", task = "gates"
            };
            config.aircraft = AircraftCatalog.Resolve(config.aircraft).DisplayName;
            _config = config;
            EnsureSimulator();
            _sim.Begin(config);
            _sim.Body.position = new Vector3(0, 8, 10);
            _sim.Body.linearVelocity = Vector3.zero;
            _sim.Pause(true);
            _page = Page.VisualLab;
            _visualLabProfiler = new VisualLabProfiler();
            _visualLabManifest = VisualLabEvidence.CreateManifest(_sim, _visualLabProfiler.Summarize(), null, null);
            BuildInterface();
            ApplySettings();
        }

        private VisualElement BuildVisualLab()
        {
            var page = new VisualElement();
            page.AddToClassList("visual-lab");

            var top = Element("visual-lab-top");
            top.Add(Text("VISUAL LAB · RD-09", "page-eyebrow"));
            top.Add(Text(VisualLabRunLine(), "visual-lab-run-line", "wrap"));
            top.Add(Text("DIRTY / NOT CAPTURED · gate NOT_RUN", "visual-lab-dirty"));
            page.Add(top);

            var body = Element("visual-lab-body");
            var controls = Element("visual-lab-controls");
            controls.Add(Text("Environment / Graphics", "panel-title"));
            controls.Add(Text("Fixed baseline: Pacific Coast · selected weather/quality · 16:30 · seed 41001", "muted", "wrap"));
            controls.Add(Text("GENERATED_UNREVIEWED / NOT 3A QA · P01-P11 runtime candidate integration; no ProductionReady claim.", "visual-lab-provenance", "wrap"));
            Choice(controls, "Weather", new[] { "Clear", "Overcast", "Rain" }, _visualLabWeather, value => RestartVisualLabVariant(value, _visualLabQuality));
            Choice(controls, "Quality", new[] { "Low", "High", "Ultra" }, AeroSimQuality.Name(_visualLabQuality), value => RestartVisualLabVariant(_visualLabWeather, AeroSimQuality.Parse(value)));
            Choice(controls, "Compare", Enum.GetNames(typeof(VisualLabCompareMode)).Select(value => value.Replace("CandidateA", "Candidate A").Replace("CandidateB", "Candidate B").Replace("QuadQuality", "Quad Quality")), CompareName(_visualLabCompare), value => { _visualLabCompare = ParseCompare(value); RefreshVisualLabViewport(); });
            Choice(controls, "Golden shot", VisualLabEvidence.ShotIds, _visualLabShot, value => _visualLabShot = value);
            Choice(controls, "Distance", VisualLabEvidence.Distances.Select(value => value.ToString("0") + " m"), _visualLabDistance.ToString("0") + " m", value => _visualLabDistance = float.Parse(value.Split(' ')[0]));
            controls.Add(ActionButton("Capture Reference", () => CaptureVisualLabSlot("Reference")));
            controls.Add(ActionButton("Capture Candidate A", () => CaptureVisualLabSlot("Candidate A")));
            controls.Add(ActionButton("Capture Candidate B", () => CaptureVisualLabSlot("Candidate B")));
            controls.Add(Text("One-variable rule: recapture slots only after changing one declared state. Slots are local, unreviewed observations.", "muted", "wrap"));
            body.Add(controls);

            var viewport = Element("visual-lab-viewport");
            viewport.name = "visual-lab-viewport";
            viewport.Add(BuildVisualLabViewport());
            body.Add(viewport);

            var performance = Element("visual-lab-performance");
            performance.Add(Text("Performance", "panel-title"));
            var metrics = Text(VisualLabMetricsText(), "visual-lab-metrics", "wrap");
            metrics.name = "visual-lab-metrics";
            performance.Add(metrics);
            performance.Add(Text("Residency trend uses linear regression over reserved memory. Target gate requires non-Development High, 120 s warm-up, 30 min fixed replay and 3 cold starts on frozen target hardware.", "muted", "wrap"));
            var env = _sim?.EnvironmentState;
            performance.Add(Text(env == null ? "PARTIAL STATE" : $"EnvironmentState\nCloud {env.cloudAmount:F2} · Fog {env.fogDensity:F4}\nWind {env.windSpeedMps:F1} m/s · Rain {env.precipitation:F2}\nWet {env.wetness:F2} · Puddle {env.puddleAmount:F2}\nWater {env.waterLevel:F2} · VFX {env.vfxDensity:F2}\nAudio {env.audioSnapshot}", "visual-lab-state", "wrap"));
            body.Add(performance);
            page.Add(body);

            var capture = Element("visual-lab-capture");
            capture.Add(ActionButton("Capture Golden Frame", CaptureVisualLabEvidence, true));
            capture.Add(ActionButton("Export Evidence", ExportVisualLabEvidence));
            capture.Add(ActionButton("Restart Fixed State", EnterVisualLab));
            capture.Add(ActionButton("Exit Lab", () => ShowPage(Page.Settings)));
            var export = Text(_visualLabLastExport, "visual-lab-export", "wrap"); export.name = "visual-lab-export"; capture.Add(export);
            page.Add(capture);

            metrics.schedule.Execute(() =>
            {
                if (_page != Page.VisualLab || _visualLabProfiler == null) return;
                _visualLabProfiler.Sample();
                metrics.text = VisualLabMetricsText();
            }).Every(500);
            return page;
        }

        private VisualElement BuildVisualLabViewport()
        {
            EnsureVisualLabLiveTexture();
            var root = Element("visual-lab-comparison");
            if (_visualLabCompare == VisualLabCompareMode.QuadQuality)
            {
                foreach (string quality in new[] { "Low", "High", "Ultra" })
                {
                    var cell = Element("visual-lab-quad-cell");
                    cell.Add(VisualLabImage(_visualLabLive));
                    cell.Add(Text(quality + (quality == AeroSimQuality.Name(LocalStore.Profile.settings.quality) ? " · LIVE" : " · NOT CAPTURED"), "visual-lab-overlay"));
                    cell.EnableInClassList("visual-lab-not-captured", quality != AeroSimQuality.Name(LocalStore.Profile.settings.quality));
                    root.Add(cell);
                }
                return root;
            }
            if (_visualLabCompare == VisualLabCompareMode.Split || _visualLabCompare == VisualLabCompareMode.Wipe)
            {
                var a = Element("visual-lab-half"); a.Add(VisualLabImage(Slot("Candidate A"))); a.Add(Text("CANDIDATE A", "visual-lab-overlay")); root.Add(a);
                var b = Element("visual-lab-half"); b.Add(VisualLabImage(Slot("Candidate B"))); b.Add(Text((_visualLabCompare == VisualLabCompareMode.Wipe ? "WIPE 50% · " : "") + "CANDIDATE B", "visual-lab-overlay")); root.Add(b);
                root.AddToClassList(_visualLabCompare == VisualLabCompareMode.Wipe ? "visual-lab-wipe" : "visual-lab-split");
                return root;
            }
            string slot = _visualLabCompare == VisualLabCompareMode.Reference ? "Reference" : _visualLabCompare == VisualLabCompareMode.CandidateB ? "Candidate B" : "Candidate A";
            root.Add(VisualLabImage(Slot(slot)));
            root.Add(Text(slot.ToUpperInvariant() + (_visualLabSlots.ContainsKey(slot) ? " · FROZEN" : " · LIVE / NOT CAPTURED"), "visual-lab-overlay"));
            return root;
        }

        private Texture Slot(string id) => _visualLabSlots.TryGetValue(id, out var texture) ? texture : _visualLabLive;
        private static Image VisualLabImage(Texture texture) { var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit }; image.AddToClassList("visual-lab-image"); return image; }

        private void EnsureVisualLabLiveTexture()
        {
            if (_sim?.View == null) return;
            if (_visualLabLive == null)
            {
                _visualLabLive = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { name = "RD-09 Visual Lab live" };
                _visualLabLive.Create();
            }
            _sim.View.targetTexture = _visualLabLive;
        }

        private void CaptureVisualLabSlot(string slot)
        {
            EnsureVisualLabLiveTexture();
            if (_visualLabLive == null) return;
            var previous = RenderTexture.active; RenderTexture.active = _visualLabLive;
            var texture = new Texture2D(_visualLabLive.width, _visualLabLive.height, TextureFormat.RGB24, false) { name = "RD-09 " + slot };
            texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply(); RenderTexture.active = previous;
            if (_visualLabSlots.TryGetValue(slot, out var old) && old != null) Destroy(old);
            _visualLabSlots[slot] = texture; RefreshVisualLabViewport(); ShowToast(slot + " captured · unreviewed.");
        }

        private void RefreshVisualLabViewport()
        {
            var viewport = _root?.Q<VisualElement>("visual-lab-viewport");
            if (viewport == null) return; viewport.Clear(); viewport.Add(BuildVisualLabViewport());
        }

        private void CaptureVisualLabEvidence()
        {
            try
            {
                string root = VisualLabPackageRoot();
                _visualLabManifest.performance = _visualLabProfiler?.Summarize();
                VisualLabEvidence.WriteSamples(_visualLabProfiler, _visualLabManifest, root);
                var artifact = VisualLabEvidence.Capture(_sim, _visualLabShot, _visualLabDistance, root, _visualLabManifest);
                VisualLabEvidence.Write(_visualLabManifest, root);
                _visualLabLastExport = artifact.relativePath + " · " + artifact.status;
                RefreshVisualLabExportLabel(); ShowToast("Golden frame captured · CAPTURED_UNREVIEWED.");
            }
            catch (Exception exception) { _visualLabLastExport = "Capture failed: " + exception.Message; RefreshVisualLabExportLabel(); }
        }

        private void ExportVisualLabEvidence()
        {
            try
            {
                _visualLabManifest.performance = _visualLabProfiler?.Summarize();
                VisualLabEvidence.WriteSamples(_visualLabProfiler, _visualLabManifest, VisualLabPackageRoot());
                string path = VisualLabEvidence.Write(_visualLabManifest, VisualLabPackageRoot());
                _visualLabLastExport = path + " · " + _visualLabManifest.status + " · gate " + _visualLabManifest.gateStatus;
                RefreshVisualLabExportLabel(); ShowToast("Evidence manifest exported; performance gate remains NOT_RUN.");
            }
            catch (Exception exception) { _visualLabLastExport = "Export failed: " + exception.Message; RefreshVisualLabExportLabel(); }
        }

        private string VisualLabPackageRoot() => Path.Combine(LocalStore.DirectoryPath, "visual-lab", _visualLabManifest.evidenceId);
        private void RefreshVisualLabExportLabel() { var label = _root?.Q<Label>("visual-lab-export"); if (label != null) label.text = _visualLabLastExport; }
        private string VisualLabRunLine()
        {
            var state = _sim?.EnvironmentState; var camera = _sim?.CameraState;
            return $"Scene {state?.environmentId ?? "—"}  |  Build NOT_BUILT  |  Pipeline Built-in  |  Quality {AeroSimQuality.Name(LocalStore.Profile.settings.quality)}  |  Weather {state?.weatherId ?? "—"}  |  Time {state?.timeOfDay:F1}  |  Seed {state?.seed}  |  Camera {camera?.Family}/{camera?.Preset}  |  vFOV {_sim?.View?.fieldOfView:F1}°";
        }
        private string VisualLabMetricsText()
        {
            var summary = _visualLabProfiler?.Summarize(); if (summary == null || summary.sampleCount == 0) return "Sampling not started · PERF NOT_RUN";
            return $"CPU ms  P50 {summary.cpuFrameMs.p50:F2}  P95 {summary.cpuFrameMs.p95:F2}  P99 {summary.cpuFrameMs.p99:F2}\n" +
                   $"GPU ms  P50 {summary.gpuFrameMs.p50:F2}  P95 {summary.gpuFrameMs.p95:F2}  P99 {summary.gpuFrameMs.p99:F2}\n" +
                   $"Memory MB  {summary.allocatedMemoryMb.p50:F0} alloc / {summary.reservedMemoryMb.p50:F0} reserved\n" +
                   $"GC KB  P50 {summary.gcAllocatedKb.p50:F1}  P95 {summary.gcAllocatedKb.p95:F1}  P99 {summary.gcAllocatedKb.p99:F1}\n" +
                   $"Renderer {summary.rendererCount.p50:F0} · Tri {summary.triangles.p50:F0} · Material {summary.materialCount.p50:F0}\n" +
                   $"Draw {summary.drawCalls.p50:F0} · SetPass {summary.setPassCalls.p50:F0} · RT {summary.renderTextureCount.p50:F0}/{summary.renderTextureMb.p50:F1} MB\n" +
                   $"Residency trend {summary.residencyTrendMbPerMinute:+0.00;-0.00;0.00} MB/min · {summary.residencyTrendStatus}\n" +
                   $"{summary.sampleCount} samples / {summary.durationSeconds:F1}s · gate {summary.gateStatus}";
        }
        private static string CompareName(VisualLabCompareMode mode) => mode.ToString().Replace("CandidateA", "Candidate A").Replace("CandidateB", "Candidate B").Replace("QuadQuality", "Quad Quality");
        private static VisualLabCompareMode ParseCompare(string value) => Enum.TryParse(value.Replace(" ", ""), out VisualLabCompareMode mode) ? mode : VisualLabCompareMode.CandidateA;

        private void RestartVisualLabVariant(string weather, int quality)
        {
            _visualLabWeather = FlightWeather.IsSupported(weather) ? weather : "Clear";
            _visualLabQuality = quality == AeroSimQuality.Low || quality == AeroSimQuality.High || quality == AeroSimQuality.Ultra ? quality : AeroSimQuality.High;
            _visualLabLastExport = "Variant " + _visualLabWeather + " / " + AeroSimQuality.Name(_visualLabQuality) + " · DIRTY / NOT CAPTURED";
            EnterVisualLab();
        }

        private void ReleaseVisualLab()
        {
            if (_sim?.View != null && _sim.View.targetTexture == _visualLabLive) _sim.View.targetTexture = null;
            if (_visualLabLive != null) { _visualLabLive.Release(); Destroy(_visualLabLive); _visualLabLive = null; }
            foreach (var texture in _visualLabSlots.Values) if (texture != null) Destroy(texture);
            _visualLabSlots.Clear(); _visualLabProfiler?.Dispose(); _visualLabProfiler = null;
        }
    }
}
