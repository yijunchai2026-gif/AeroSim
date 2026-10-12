using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AeroSim.Domain;
using AeroSim.Simulation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace AeroSim.Diagnostics
{
    public enum VisualLabCompareMode { Reference, CandidateA, CandidateB, Split, Wipe, QuadQuality }

    [Serializable]
    public sealed class VisualLabSample
    {
        public double elapsedSeconds;
        public double cpuFrameMs;
        public double gpuFrameMs;
        public long allocatedMemoryBytes;
        public long reservedMemoryBytes;
        public long monoMemoryBytes;
        public long gcAllocatedBytes;
        public long drawCalls;
        public long setPassCalls;
        public long triangles;
        public int rendererCount;
        public int materialCount;
        public int renderTextureCount;
        public long renderTextureBytes;
    }

    [Serializable]
    public sealed class VisualLabPercentiles
    {
        public double p50;
        public double p95;
        public double p99;
    }

    [Serializable]
    public sealed class VisualLabPerformanceSummary
    {
        public string status = "CAPTURED_UNREVIEWED";
        public int sampleCount;
        public double durationSeconds;
        public VisualLabPercentiles cpuFrameMs;
        public VisualLabPercentiles gpuFrameMs;
        public VisualLabPercentiles allocatedMemoryMb;
        public VisualLabPercentiles reservedMemoryMb;
        public VisualLabPercentiles gcAllocatedKb;
        public VisualLabPercentiles drawCalls;
        public VisualLabPercentiles setPassCalls;
        public VisualLabPercentiles triangles;
        public VisualLabPercentiles rendererCount;
        public VisualLabPercentiles materialCount;
        public VisualLabPercentiles renderTextureCount;
        public VisualLabPercentiles renderTextureMb;
        public double residencyTrendMbPerMinute;
        public string residencyTrendStatus;
        public string gateStatus = "NOT_RUN";
        public string note = "Raw runtime observations only. A short or non-target-hardware sample cannot pass REQ-PERF-001.";
    }

    [Serializable]
    public sealed class VisualLabCompositionGuard
    {
        public string status = "FAIL";
        public string method = "renderer-bounds-ray-grid/v1";
        public bool cameraOutsideRendererBounds;
        public bool cameraOutsideColliderBounds;
        public bool heroZoneInFrustum;
        public float skyVisibleRatio;
        public float oceanVisibleRatio;
        public float groundVisibleRatio;
        public float minimumSkyRatio = .12f;
        public float minimumOceanRatio = .015f;
        public float minimumGroundRatio = .18f;
        public List<string> failures = new();
        public bool Passed => status == "PASS" && failures.Count == 0;
    }

    [Serializable]
    public sealed class VisualLabShotArtifact
    {
        public string shotId;
        public string rd01CaseId;
        public string relativePath;
        public string sha256;
        public long bytes;
        public string status;
        public string captureStatus = "NOT_CAPTURED";
        public string reviewStatus = "NOT_REVIEWED";
        public string gateStatus = "NOT_RUN";
        public int pixelWidth;
        public int pixelHeight;
        public float distanceMeters;
        public string heroZoneId;
        public string reviewBand;
        public string provenanceLabel;
        public Vector3 cameraPosition;
        public Vector3 cameraEuler;
        public float verticalFov;
        public float horizontalFov;
        public VisualLabCompositionGuard compositionGuard;
    }

    [Serializable]
    public sealed class VisualLabEnvironmentSnapshot
    {
        public string environmentId;
        public string weather;
        public float timeOfDay;
        public int seed;
        public float cloud;
        public float fog;
        public float windMps;
        public float precipitation;
        public float wetness;
        public float puddle;
        public float water;
        public float vfx;
        public string audio;
        public string stateStatus;
    }

    [Serializable]
    public sealed class VisualLabEvidenceManifest
    {
        public string schemaVersion = "aerosim.visual-lab-evidence/v2";
        public string workPackage = "RD-09";
        public string prdVersion = "4.0";
        public string evidenceId;
        public string createdAtUtc;
        public string status = "INCOMPLETE_EVIDENCE";
        public string gateStatus = "NOT_RUN";
        public string rd01ManifestId;
        public string rd01SchemaVersion;
        public string rd01SourceCommit;
        public string rd01BuildId;
        public string buildId;
        public string sourceCommit;
        public string unityVersion;
        public string productVersion;
        public string platform;
        public string operatingSystem;
        public string cpu;
        public string gpu;
        public int systemMemoryMb;
        public string pipeline;
        public bool nonDevelopmentPlayer;
        public string targetHardwareProfileId;
        public bool targetHardwareProfileDeclared;
        public int warmupSeconds;
        public int coldStartRuns;
        public int windowWidth;
        public int windowHeight;
        public string quality;
        public string camera;
        public string provenance = "CAPTURED_UNREVIEWED / NOT SCANNED";
        public CoastProductionRuntimeStatus coastProduction;
        public string performanceSamplesPath;
        public string performanceSamplesSha256;
        public VisualLabEnvironmentSnapshot environment;
        public VisualLabPerformanceSummary performance;
        public List<VisualLabShotArtifact> screenshots = new();
        public List<string> missingEvidence = new();
        public string[] supportedComparisons = { "Reference", "Candidate A", "Candidate B", "A/B Split", "Wipe", "Quad Quality" };
        public string[] goldenShots = { "A", "B", "C", "D", "E", "F", "G", "H" };
        public float[] reviewDistancesMeters = { 1, 5, 15, 40, 80 };
    }

    public sealed class VisualLabProfiler : IDisposable
    {
        private readonly List<VisualLabSample> _samples = new();
        private readonly double _started = Time.realtimeSinceStartupAsDouble;
        private ProfilerRecorder _gc, _draw, _setPass, _triangles;
        private bool _recordersReady;

        public IReadOnlyList<VisualLabSample> Samples => _samples;

        public VisualLabProfiler()
        {
            try
            {
                _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                _draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                _recordersReady = true;
            }
            catch (Exception exception) { Debug.LogWarning("Visual Lab profiler counters unavailable: " + exception.Message); }
        }

        public VisualLabSample Sample()
        {
            FrameTimingManager.CaptureFrameTimings();
            var timings = new FrameTiming[1];
            uint count = FrameTimingManager.GetLatestTimings(1, timings);
            double fallback = Time.unscaledDeltaTime * 1000.0;
            var renderers = Resources.FindObjectsOfTypeAll<Renderer>().Where(value => value != null && value.gameObject.scene.IsValid() && value.enabled && value.gameObject.activeInHierarchy).ToArray();
            var materials = new HashSet<int>();
            long meshTriangles = 0;
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials) if (material != null) materials.Add(material.GetInstanceID());
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) meshTriangles += TriangleCount(filter.sharedMesh);
                var skinned = renderer as SkinnedMeshRenderer;
                if (skinned != null && skinned.sharedMesh != null) meshTriangles += TriangleCount(skinned.sharedMesh);
            }
            var renderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>().Where(value => value != null && value.IsCreated()).ToArray();
            long rtBytes = renderTextures.Sum(EstimateRenderTextureBytes);
            var sample = new VisualLabSample
            {
                elapsedSeconds = Time.realtimeSinceStartupAsDouble - _started,
                cpuFrameMs = count > 0 && timings[0].cpuFrameTime > 0 ? timings[0].cpuFrameTime : fallback,
                gpuFrameMs = count > 0 && timings[0].gpuFrameTime > 0 ? timings[0].gpuFrameTime : 0,
                allocatedMemoryBytes = Profiler.GetTotalAllocatedMemoryLong(),
                reservedMemoryBytes = Profiler.GetTotalReservedMemoryLong(),
                monoMemoryBytes = Profiler.GetMonoUsedSizeLong(),
                gcAllocatedBytes = Counter(_gc), drawCalls = Counter(_draw), setPassCalls = Counter(_setPass),
                triangles = Math.Max(meshTriangles, Counter(_triangles)), rendererCount = renderers.Length,
                materialCount = materials.Count, renderTextureCount = renderTextures.Length, renderTextureBytes = rtBytes
            };
            _samples.Add(sample);
            return sample;
        }

        public VisualLabPerformanceSummary Summarize()
        {
            var result = new VisualLabPerformanceSummary { sampleCount = _samples.Count };
            if (_samples.Count == 0) { result.status = "NOT_RUN"; result.residencyTrendStatus = "NOT_RUN"; return result; }
            result.durationSeconds = _samples.Last().elapsedSeconds;
            result.cpuFrameMs = Percentiles(_samples.Select(x => x.cpuFrameMs));
            result.gpuFrameMs = Percentiles(_samples.Select(x => x.gpuFrameMs));
            result.allocatedMemoryMb = Percentiles(_samples.Select(x => x.allocatedMemoryBytes / 1048576.0));
            result.reservedMemoryMb = Percentiles(_samples.Select(x => x.reservedMemoryBytes / 1048576.0));
            result.gcAllocatedKb = Percentiles(_samples.Select(x => x.gcAllocatedBytes / 1024.0));
            result.drawCalls = Percentiles(_samples.Select(x => (double)x.drawCalls));
            result.setPassCalls = Percentiles(_samples.Select(x => (double)x.setPassCalls));
            result.triangles = Percentiles(_samples.Select(x => (double)x.triangles));
            result.rendererCount = Percentiles(_samples.Select(x => (double)x.rendererCount));
            result.materialCount = Percentiles(_samples.Select(x => (double)x.materialCount));
            result.renderTextureCount = Percentiles(_samples.Select(x => (double)x.renderTextureCount));
            result.renderTextureMb = Percentiles(_samples.Select(x => x.renderTextureBytes / 1048576.0));
            result.residencyTrendMbPerMinute = Trend(_samples.Select(x => x.elapsedSeconds).ToArray(), _samples.Select(x => x.reservedMemoryBytes / 1048576.0).ToArray()) * 60;
            result.residencyTrendStatus = result.durationSeconds < 1800 ? "OBSERVED_SHORT_RUN" : "CAPTURED_UNREVIEWED";
            return result;
        }

        public void Dispose()
        {
            if (!_recordersReady) return;
            _gc.Dispose(); _draw.Dispose(); _setPass.Dispose(); _triangles.Dispose();
        }

        private static long Counter(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue : 0;
        private static long TriangleCount(Mesh mesh)
        {
            long triangles = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++) triangles += (long)mesh.GetIndexCount(subMesh) / 3;
            return triangles;
        }
        private static long EstimateRenderTextureBytes(RenderTexture value)
        {
            int depthBytes = value.depth > 0 ? 4 : 0;
            int colorBytes = value.format == RenderTextureFormat.ARGBHalf ? 8 : value.format == RenderTextureFormat.ARGBFloat ? 16 : 4;
            int samples = Mathf.Max(1, value.antiAliasing);
            return (long)value.width * value.height * (colorBytes + depthBytes) * samples;
        }
        private static VisualLabPercentiles Percentiles(IEnumerable<double> source)
        {
            var values = source.Where(value => !double.IsNaN(value) && !double.IsInfinity(value)).OrderBy(value => value).ToArray();
            return new VisualLabPercentiles { p50 = Pick(values, .50), p95 = Pick(values, .95), p99 = Pick(values, .99) };
        }
        private static double Pick(double[] values, double percentile)
        {
            if (values.Length == 0) return 0;
            double index = (values.Length - 1) * percentile; int low = (int)Math.Floor(index), high = (int)Math.Ceiling(index);
            return low == high ? values[low] : values[low] + (values[high] - values[low]) * (index - low);
        }
        private static double Trend(double[] x, double[] y)
        {
            if (x.Length < 2 || x.Length != y.Length) return 0;
            double xMean = x.Average(), yMean = y.Average(), numerator = 0, denominator = 0;
            for (int i = 0; i < x.Length; i++) { numerator += (x[i] - xMean) * (y[i] - yMean); denominator += (x[i] - xMean) * (x[i] - xMean); }
            return denominator <= double.Epsilon ? 0 : numerator / denominator;
        }
    }

    public static class VisualLabEvidence
    {
        public static readonly string[] ShotIds = { "A", "B", "C", "D", "E", "F", "G", "H" };
        public static readonly float[] Distances = { 1, 5, 15, 40, 80 };

        public static VisualLabEvidenceManifest CreateManifest(FlightSimulator simulator, VisualLabPerformanceSummary performance, string buildId, string sourceCommit)
        {
            var rd01 = Rd01Manifest.Current;
            var state = simulator?.EnvironmentState;
            var diagnostics = simulator?.EnvironmentDiagnostics;
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var manifest = new VisualLabEvidenceManifest
            {
                evidenceId = "rd09-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"), createdAtUtc = DateTime.UtcNow.ToString("O"),
                rd01ManifestId = rd01.manifestId, rd01SchemaVersion = rd01.schemaVersion, rd01SourceCommit = rd01.sourceBaseline.headCommit,
                rd01BuildId = rd01.build.buildId, buildId = string.IsNullOrWhiteSpace(buildId) ? "NOT_BUILT" : buildId,
                sourceCommit = string.IsNullOrWhiteSpace(sourceCommit) ? "UNCOMMITTED_WORKTREE" : sourceCommit,
                unityVersion = Application.unityVersion, productVersion = Application.version, platform = Application.platform.ToString(),
                operatingSystem = SystemInfo.operatingSystem, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                systemMemoryMb = SystemInfo.systemMemorySize, pipeline = pipeline == null ? "Built-in" : pipeline.GetType().FullName,
                nonDevelopmentPlayer = !Debug.isDebugBuild && !Application.isEditor, targetHardwareProfileId = rd01.targetHardware.profileId,
                targetHardwareProfileDeclared = false, warmupSeconds = 0, coldStartRuns = 1,
                windowWidth = Screen.width, windowHeight = Screen.height, quality = AeroSimQuality.Name(LocalStore.Profile.settings.quality),
                camera = simulator?.CameraDiagnostic ?? "NOT_AVAILABLE", coastProduction = simulator?.CoastProductionStatus,
                performance = performance ?? new VisualLabPerformanceSummary { status = "NOT_RUN" },
                environment = state == null ? null : new VisualLabEnvironmentSnapshot
                {
                    environmentId = state.environmentId, weather = state.weatherId, timeOfDay = state.timeOfDay, seed = state.seed,
                    cloud = state.cloudAmount, fog = state.fogDensity, windMps = state.windSpeedMps, precipitation = state.precipitation,
                    wetness = state.wetness, puddle = state.puddleAmount, water = state.waterLevel, vfx = state.vfxDensity,
                    audio = state.audioSnapshot, stateStatus = diagnostics != null && diagnostics.IsValid ? "COMPLETE" : "PARTIAL_STATE"
                }
            };
            RefreshStatus(manifest);
            return manifest;
        }

        public static VisualLabShotArtifact Capture(FlightSimulator simulator, string shotId, float distanceMeters, string packageRoot, VisualLabEvidenceManifest manifest)
        {
            if (simulator?.View == null) throw new InvalidOperationException("Visual Lab requires a running simulator camera.");
            if (!ShotIds.Contains(shotId)) throw new ArgumentException("Shot must be A-H.", nameof(shotId));
            if (!Distances.Contains(distanceMeters)) throw new ArgumentException("Distance must be 1, 5, 15, 40, or 80 metres.", nameof(distanceMeters));
            string root = Path.GetFullPath(packageRoot); Directory.CreateDirectory(root); string imageDirectory = Path.Combine(root, "screenshots"); Directory.CreateDirectory(imageDirectory);
            var views = simulator.EnvironmentGoldenViews;
            if (views == null || views.Count == 0) throw new InvalidOperationException("RD-08 golden environment views are unavailable.");
            int shotIndex = Array.IndexOf(ShotIds, shotId);
            var distanceViews = views.Where(value => Mathf.Approximately(value.distanceMeters, distanceMeters)).ToArray();
            if (distanceViews.Length == 0) throw new InvalidOperationException("RD-08 has no golden view for " + distanceMeters + "m.");
            var baseView = distanceViews[shotIndex % distanceViews.Length];
            float orbitDegrees = shotIndex * 45f;
            Vector3 radial = baseView.position - baseView.target;
            Vector3 cameraPosition = baseView.target + Quaternion.AngleAxis(orbitDegrees, Vector3.up) * radial;
            cameraPosition = ResolveSafeCameraPosition(cameraPosition, baseView.target, distanceMeters);
            Vector3 aimTarget = baseView.target + Vector3.up * (distanceMeters >= 15 ? 4f : 0f);
            string fileName = $"G3-GOLDEN-{shotId}-{distanceMeters:0}m-{AeroSimQuality.Name(LocalStore.Profile.settings.quality).ToLowerInvariant()}.png";
            string path = Path.Combine(imageDirectory, fileName);
            var camera = simulator.View; var oldPosition = camera.transform.position; var oldRotation = camera.transform.rotation; float oldFov = camera.fieldOfView; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            RenderTexture renderTexture = null; Texture2D texture = null;
            VisualLabCompositionGuard compositionGuard = null;
            try
            {
                renderTexture = new RenderTexture(2560, 1440, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, name = "RD-09 Evidence " + shotId };
                renderTexture.Create(); camera.transform.position = cameraPosition; camera.transform.LookAt(aimTarget); camera.fieldOfView = baseView.verticalFov; camera.targetTexture = renderTexture; camera.Render();
                compositionGuard = EvaluateCompositionGuard(camera, baseView.heroZoneId);
                RenderTexture.active = renderTexture; texture = new Texture2D(2560, 1440, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 2560, 1440), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; camera.transform.SetPositionAndRotation(oldPosition, oldRotation); camera.fieldOfView = oldFov; RenderTexture.active = oldActive;
                if (renderTexture != null) { renderTexture.Release(); UnityEngine.Object.Destroy(renderTexture); }
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
            var artifact = new VisualLabShotArtifact
            {
                shotId = shotId, rd01CaseId = "G3-GOLDEN-" + shotId, relativePath = "screenshots/" + fileName,
                sha256 = Sha256(path), bytes = new FileInfo(path).Length,
                status = compositionGuard != null && compositionGuard.Passed ? "CAPTURED" : "CAPTURED_GUARD_FAILED",
                captureStatus = "CAPTURED", reviewStatus = "NOT_REVIEWED", gateStatus = "NOT_RUN",
                pixelWidth = texture?.width ?? 2560, pixelHeight = texture?.height ?? 1440, distanceMeters = distanceMeters,
                heroZoneId = baseView.heroZoneId, reviewBand = baseView.reviewBand,
                provenanceLabel = ProceduralVerticalSliceDiagnostics.RequiredLabel + " / NOT SCANNED",
                cameraPosition = cameraPosition, cameraEuler = Quaternion.LookRotation(aimTarget - cameraPosition).eulerAngles,
                verticalFov = baseView.verticalFov, horizontalFov = FlightSimulator.VerticalToHorizontalFov(baseView.verticalFov, 16f / 9f),
                compositionGuard = compositionGuard
            };
            manifest.screenshots.RemoveAll(value => value.shotId == shotId && Mathf.Approximately(value.distanceMeters, distanceMeters));
            manifest.screenshots.Add(artifact); RefreshStatus(manifest); return artifact;
        }

        private static Vector3 ResolveSafeCameraPosition(Vector3 candidate, Vector3 target, float distance)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(candidate - target, Vector3.up).normalized;
            if (horizontal.sqrMagnitude < .9f) horizontal = Vector3.back;
            var renderers = Resources.FindObjectsOfTypeAll<Renderer>()
                .Where(value => value != null && value.enabled && value.gameObject.activeInHierarchy && value.gameObject.scene.IsValid()).ToArray();
            var colliders = Resources.FindObjectsOfTypeAll<Collider>()
                .Where(value => value != null && value.enabled && value.gameObject.activeInHierarchy && value.gameObject.scene.IsValid()).ToArray();
            foreach (float elevation in new[] { 24f, 35f, 45f, 55f, 65f })
                foreach (float yaw in new[] { 0f, -15f, 15f, -30f, 30f })
                {
                    Vector3 orbit = Quaternion.AngleAxis(yaw, Vector3.up) * horizontal;
                    float radians = elevation * Mathf.Deg2Rad;
                    Vector3 position = target + orbit * (Mathf.Cos(radians) * distance) + Vector3.up * (Mathf.Sin(radians) * distance);
                    bool insideRenderer = renderers.Any(value => ContainsWithMargin(value.bounds, position, .05f));
                    bool insideCollider = colliders.Any(value => ContainsWithMargin(value.bounds, position, .05f));
                    if (!insideRenderer && !insideCollider) return position;
                }
            return candidate;
        }

        private static VisualLabCompositionGuard EvaluateCompositionGuard(Camera camera, string heroZoneId)
        {
            var result = new VisualLabCompositionGuard();
            Vector3 position = camera.transform.position;
            var renderers = Resources.FindObjectsOfTypeAll<Renderer>()
                .Where(value => value != null && value.enabled && value.gameObject.activeInHierarchy && value.gameObject.scene.IsValid()).ToArray();
            var colliders = Resources.FindObjectsOfTypeAll<Collider>()
                .Where(value => value != null && value.enabled && value.gameObject.activeInHierarchy && value.gameObject.scene.IsValid()).ToArray();

            var containingRenderers = renderers.Where(value => ContainsWithMargin(value.bounds, position, .05f)).ToArray();
            var containingColliders = colliders.Where(value => ContainsWithMargin(value.bounds, position, .05f)).ToArray();
            result.cameraOutsideRendererBounds = containingRenderers.Length == 0;
            result.cameraOutsideColliderBounds = containingColliders.Length == 0;
            var hero = Resources.FindObjectsOfTypeAll<EnvironmentHeroZoneMarker>()
                .FirstOrDefault(value => value != null && value.gameObject.scene.IsValid() && value.heroZoneId == heroZoneId);
            result.heroZoneInFrustum = hero != null && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), hero.reviewBounds);

            var waterRenderers = renderers.Where(IsWaterRenderer).ToArray();
            const int columns = 24, rows = 14;
            int sky = 0, ocean = 0, ground = 0, total = columns * rows;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    Ray ray = camera.ViewportPointToRay(new Vector3((x + .5f) / columns, (y + .5f) / rows, 0));
                    RaycastHit hit = Physics.RaycastAll(ray, camera.farClipPlane, ~0, QueryTriggerInteraction.Ignore)
                        .Where(value => value.collider != null && value.collider.name.IndexOf("Flight boundary", StringComparison.OrdinalIgnoreCase) < 0)
                        .OrderBy(value => value.distance).FirstOrDefault();
                    bool hitGround = hit.collider != null;
                    float waterDistance = float.PositiveInfinity;
                    foreach (Renderer renderer in waterRenderers)
                        if (renderer.bounds.IntersectRay(ray, out float distance) && distance >= camera.nearClipPlane && distance < waterDistance)
                            waterDistance = distance;
                    if (waterDistance < float.PositiveInfinity && (!hitGround || waterDistance < hit.distance)) ocean++;
                    else if (hitGround) ground++;
                    else sky++;
                }
            result.skyVisibleRatio = (float)sky / total;
            result.oceanVisibleRatio = (float)ocean / total;
            result.groundVisibleRatio = (float)ground / total;
            if (!result.cameraOutsideRendererBounds) result.failures.Add("camera is inside or touching Renderer bounds: " + string.Join(", ", containingRenderers.Take(4).Select(value => value.name)));
            if (!result.cameraOutsideColliderBounds) result.failures.Add("camera is inside or touching Collider bounds: " + string.Join(", ", containingColliders.Take(4).Select(value => value.name)));
            if (!result.heroZoneInFrustum) result.failures.Add("hero zone review bounds are outside the camera frustum");
            if (result.skyVisibleRatio < result.minimumSkyRatio) result.failures.Add("sky visible ratio below minimum");
            if (result.oceanVisibleRatio < result.minimumOceanRatio) result.failures.Add("ocean visible ratio below minimum");
            if (result.groundVisibleRatio < result.minimumGroundRatio) result.failures.Add("ground visible ratio below minimum");
            result.status = result.failures.Count == 0 ? "PASS" : "FAIL";
            return result;
        }

        private static bool ContainsWithMargin(Bounds bounds, Vector3 position, float margin)
        {
            bounds.Expand(margin * 2); return bounds.Contains(position);
        }

        private static bool IsWaterRenderer(Renderer renderer)
        {
            for (Transform node = renderer.transform; node != null; node = node.parent)
            {
                if (node.name.IndexOf("ocean", StringComparison.OrdinalIgnoreCase) >= 0 || node.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    node.name.IndexOf("tide", StringComparison.OrdinalIgnoreCase) >= 0 || node.name.IndexOf("foam", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                var marker = node.GetComponent<EnvironmentCompositionMarker>();
                if (marker != null && marker.layer == EnvironmentCompositionLayer.Water) return true;
            }
            return false;
        }

        public static string WriteSamples(VisualLabProfiler profiler, VisualLabEvidenceManifest manifest, string packageRoot)
        {
            if (profiler == null) throw new ArgumentNullException(nameof(profiler));
            string root = Path.GetFullPath(packageRoot); Directory.CreateDirectory(root);
            string path = Path.Combine(root, "performance-samples.csv");
            var lines = new List<string> { "elapsed_s,cpu_ms,gpu_ms,allocated_bytes,reserved_bytes,mono_bytes,gc_bytes,draw_calls,setpass_calls,triangles,renderers,materials,rt_count,rt_bytes" };
            lines.AddRange(profiler.Samples.Select(value => string.Join(",", new[]
            {
                value.elapsedSeconds.ToString("F6", System.Globalization.CultureInfo.InvariantCulture), value.cpuFrameMs.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                value.gpuFrameMs.ToString("F6", System.Globalization.CultureInfo.InvariantCulture), value.allocatedMemoryBytes.ToString(), value.reservedMemoryBytes.ToString(),
                value.monoMemoryBytes.ToString(), value.gcAllocatedBytes.ToString(), value.drawCalls.ToString(), value.setPassCalls.ToString(), value.triangles.ToString(),
                value.rendererCount.ToString(), value.materialCount.ToString(), value.renderTextureCount.ToString(), value.renderTextureBytes.ToString()
            })));
            File.WriteAllLines(path, lines); manifest.performanceSamplesPath = "performance-samples.csv"; manifest.performanceSamplesSha256 = Sha256(path); RefreshStatus(manifest); return path;
        }

        public static string Write(VisualLabEvidenceManifest manifest, string packageRoot)
        {
            RefreshStatus(manifest); string root = Path.GetFullPath(packageRoot); Directory.CreateDirectory(root);
            string path = Path.Combine(root, "manifest.json"); File.WriteAllText(path, JsonUtility.ToJson(manifest, true)); return path;
        }

        private static void RefreshStatus(VisualLabEvidenceManifest manifest)
        {
            manifest.missingEvidence.Clear();
            if (manifest.environment == null || manifest.environment.stateStatus != "COMPLETE") manifest.missingEvidence.Add("complete EnvironmentState / RD-07 adapter diagnostics");
            if (manifest.coastProduction == null || !manifest.coastProduction.candidateBindingsComplete) manifest.missingEvidence.Add("P01-P10 runtime candidate bindings");
            if (manifest.performance == null || manifest.performance.status == "NOT_RUN") manifest.missingEvidence.Add("runtime performance samples");
            if (string.IsNullOrWhiteSpace(manifest.performanceSamplesPath) || string.IsNullOrWhiteSpace(manifest.performanceSamplesSha256)) manifest.missingEvidence.Add("hashed raw performance sample file");
            if (manifest.performance == null || manifest.performance.durationSeconds < 1800) manifest.missingEvidence.Add("30-minute High target-hardware run");
            if (!manifest.nonDevelopmentPlayer) manifest.missingEvidence.Add("non-Development Player run");
            if (!manifest.targetHardwareProfileDeclared) manifest.missingEvidence.Add("frozen target-hardware profile declaration and review");
            if (manifest.warmupSeconds < 120) manifest.missingEvidence.Add("120-second warm-up");
            if (manifest.coldStartRuns < 3) manifest.missingEvidence.Add("three cold-start runs");
            if (manifest.quality != "High") manifest.missingEvidence.Add("High quality ART TARGET run");
            int matrixCells = manifest.screenshots.Select(value => value.shotId + "@" + value.distanceMeters.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Distinct(StringComparer.Ordinal).Count();
            if (matrixCells < ShotIds.Length * Distances.Length) manifest.missingEvidence.Add("A-H x 1/5/15/40/80m screenshot matrix");
            if (manifest.screenshots.Any(value => value.captureStatus != "CAPTURED")) manifest.missingEvidence.Add("successfully captured screenshot artifacts");
            if (manifest.screenshots.Any(value => value.compositionGuard == null || !value.compositionGuard.Passed)) manifest.missingEvidence.Add("all screenshot composition guards passing");
            if (manifest.screenshots.Any(value => value.reviewStatus != "VISUALLY_REVIEWED")) manifest.missingEvidence.Add("human visual review decisions");
            if (manifest.buildId == "NOT_BUILT" || manifest.sourceCommit == "UNCOMMITTED_WORKTREE") manifest.missingEvidence.Add("immutable build ID and source commit");
            manifest.status = manifest.missingEvidence.Count == 0 ? "VISUALLY_REVIEWED" : "INCOMPLETE_EVIDENCE";
            manifest.gateStatus = manifest.status == "VISUALLY_REVIEWED" && manifest.screenshots.All(value => value.gateStatus == "GATE_PASS") ? "GATE_PASS" : "NOT_RUN";
            if (manifest.performance != null) manifest.performance.gateStatus = "NOT_RUN";
        }

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path); using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
    }
}
