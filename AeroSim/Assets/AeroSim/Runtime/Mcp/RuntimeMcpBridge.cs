using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroSim.Runtime.SceneEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace AeroSim.Runtime.Mcp
{
    internal static class RuntimeMcpProtocol
    {
        public const string Version = "aerosim.runtime-review-mcp/v1";
        public const string DraftStatus = "DRAFT_NOT_USER_CONFIRMED";
        public static readonly HashSet<string> Tools = new(StringComparer.Ordinal)
        {
            "inspect_scene", "capture_game_view", "get_console_logs", "get_performance_metrics", "validate_scene"
        };
    }

    [Serializable]
    internal sealed class RuntimeMcpRequest
    {
        public string protocol;
        public string id;
        public string tool;
        public RuntimeCaptureOptions capture;
    }

    [Serializable]
    internal sealed class RuntimeCaptureOptions
    {
        public int width = 1280;
        public int height = 720;
        public bool includeUi = true;
    }

    [Serializable]
    internal sealed class RuntimeMcpResponse
    {
        public string protocol = RuntimeMcpProtocol.Version;
        public string requestId;
        public string tool;
        public bool success;
        public string status;
        public string message;
        public string runId;
        public string productionStatus = RuntimeMcpProtocol.DraftStatus;
        public string scene;
        public RuntimeObjectSnapshot[] objects = Array.Empty<RuntimeObjectSnapshot>();
        public RuntimeFinding[] findings = Array.Empty<RuntimeFinding>();
        public RuntimeLogEntry[] logs = Array.Empty<RuntimeLogEntry>();
        public RuntimePerformanceSnapshot performance;
        public string[] artifacts = Array.Empty<string>();
        public string errorType;
        public string dataScope;
        public float sampledRealtime;
        public float sampleAgeMs;
        public bool cached;
        public bool truncated;
        public int objectLimit;
    }

    [Serializable]
    internal sealed class RuntimeObjectSnapshot
    {
        public string runtimeId;
        public string hierarchyPath;
        public string name;
        public bool active;
        public bool draft;
        public string draftStatus;
        public Vector3 position;
        public Vector3 rotationEuler;
        public Vector3 scale;
        public bool hasRenderer;
        public bool hasCollider;
    }

    [Serializable]
    internal sealed class RuntimeFinding
    {
        public string id;
        public string gate;
        public string severity;
        public string category;
        public string target;
        public string evidence;
        public string expected;
        public string actual;
        public string recommendation;
        public float confidence;
    }

    [Serializable]
    internal sealed class RuntimeLogEntry
    {
        public string utc;
        public string type;
        public string message;
        public string stackTrace;
    }

    [Serializable]
    internal sealed class RuntimePerformanceSnapshot
    {
        public float realtimeSinceStartup;
        public float unscaledDeltaTimeMs;
        public float smoothedFps;
        public long monoUsedBytes;
        public long allocatedMemoryBytes;
        public int activeGameObjects;
        public int rendererCount;
        public int colliderCount;
    }

    [DisallowMultipleComponent]
    public sealed class RuntimeMcpBridge : MonoBehaviour
    {
        private readonly struct InboxCandidate
        {
            public readonly string Path;
            public readonly long TimestampTicks;
            public readonly string Name;

            public InboxCandidate(string path, long timestampTicks)
            {
                Path = path;
                TimestampTicks = timestampTicks;
                Name = System.IO.Path.GetFileName(path);
            }
        }

        private sealed class InboxCandidateComparer : IComparer<InboxCandidate>
        {
            public int Compare(InboxCandidate left, InboxCandidate right)
            {
                int byTime = left.TimestampTicks.CompareTo(right.TimestampTicks);
                return byTime != 0 ? byTime : string.CompareOrdinal(left.Name, right.Name);
            }
        }

        private const int MaxRequestBytes = 64 * 1024;
        private const int MaxQueueDepth = 32;
        private const int MaxLogs = 200;
        private const int MaxRequestsPerSecond = 4;
        private const int MaxInboxEntriesPerPoll = 32;
        private const int MaxSceneObjects = 4096;
        private const float SceneCacheSeconds = 1f;
        private const float PerformanceCacheSeconds = 1f;
        private readonly Queue<RuntimeLogEntry> logs = new();
        private readonly Queue<float> requestTimes = new();
        private readonly SortedSet<InboxCandidate> inboxCandidates = new(new InboxCandidateComparer());
        private IEnumerator<string> inboxEnumeration;
        private RuntimeObjectSnapshot[] sceneCache = Array.Empty<RuntimeObjectSnapshot>();
        private string sceneCacheName = string.Empty;
        private float sceneSampledRealtime = float.NegativeInfinity;
        private bool sceneCacheTruncated;
        private RuntimePerformanceSnapshot performanceCache;
        private float performanceSampledRealtime = float.NegativeInfinity;
        private string root;
        private string inbox;
        private string outbox;
        private string runs;
        private bool processing;
        private bool inboxScanCompleted;
        private float nextPoll;
        private float smoothedFps;

        private void Awake()
        {
            root = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "AeroSimReview"));
            inbox = Child(root, "inbox");
            outbox = Child(root, "outbox");
            runs = Child(root, "runs");
            EnsureNotLink(root);
            EnsureNotLink(inbox);
            EnsureNotLink(outbox);
            EnsureNotLink(runs);
            Directory.CreateDirectory(inbox);
            Directory.CreateDirectory(outbox);
            Directory.CreateDirectory(runs);
            Application.logMessageReceived += OnLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            inboxEnumeration?.Dispose();
            inboxEnumeration = null;
        }

        private void Update()
        {
            float instantaneous = Time.unscaledDeltaTime > 0 ? 1f / Time.unscaledDeltaTime : 0;
            smoothedFps = smoothedFps <= 0 ? instantaneous : Mathf.Lerp(smoothedFps, instantaneous, .05f);
            if (processing || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .1f;
            ProcessNext();
        }

        private void ProcessNext()
        {
            PumpInboxEnumeration();
            if (!inboxScanCompleted) return;
            if (inboxCandidates.Count == 0)
            {
                inboxScanCompleted = false;
                return;
            }
            var now = Time.unscaledTime;
            while (requestTimes.Count > 0 && now - requestTimes.Peek() >= 1f) requestTimes.Dequeue();
            if (requestTimes.Count >= MaxRequestsPerSecond) return;
            requestTimes.Enqueue(now);
            var request = inboxCandidates.Min;
            inboxCandidates.Remove(request);
            if (inboxCandidates.Count == 0) inboxScanCompleted = false;
            ProcessFile(request.Path);
        }

        private void PumpInboxEnumeration()
        {
            if (inboxScanCompleted) return;
            if (inboxEnumeration == null)
            {
                try
                {
                    inboxEnumeration = Directory.EnumerateFiles(inbox, "*.json", SearchOption.TopDirectoryOnly).GetEnumerator();
                    inboxScanCompleted = false;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Runtime MCP inbox unavailable: " + exception.Message);
                    return;
                }
            }
            try
            {
                int visited = 0;
                while (visited < MaxInboxEntriesPerPoll)
                {
                    if (!inboxEnumeration.MoveNext())
                    {
                        CompleteInboxScan();
                        return;
                    }
                    visited++;
                    var path = inboxEnumeration.Current;
                    if (!File.Exists(path)) continue;
                    InboxCandidate candidate;
                    try { candidate = new InboxCandidate(path, File.GetLastWriteTimeUtc(path).Ticks); }
                    catch (IOException) { continue; }
                    catch (UnauthorizedAccessException) { continue; }
                    inboxCandidates.Add(candidate);
                    if (inboxCandidates.Count <= MaxQueueDepth) continue;
                    var rejected = inboxCandidates.Max;
                    inboxCandidates.Remove(rejected);
                    RejectWithoutReading(rejected.Path, "QUEUE_FULL", "Runtime MCP queue depth exceeded.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Runtime MCP inbox enumeration failed: " + exception.Message);
                CompleteInboxScan();
            }
        }

        private void CompleteInboxScan()
        {
            inboxEnumeration?.Dispose();
            inboxEnumeration = null;
            inboxScanCompleted = true;
        }

        private void ProcessFile(string requestPath)
        {
            RuntimeMcpRequest request = null;
            try
            {
                EnsureInside(requestPath, inbox);
                var info = new FileInfo(requestPath);
                if (info.Length <= 0 || info.Length > MaxRequestBytes) throw new InvalidDataException("Request must be between 1 and 65536 bytes.");
                var json = File.ReadAllText(requestPath);
                File.Delete(requestPath);
                request = JsonUtility.FromJson<RuntimeMcpRequest>(json);
                ValidateRequest(request);
                var response = CreateBaseResponse(request);
                switch (request.tool)
                {
                    case "inspect_scene": InspectScene(response); Complete(response); break;
                    case "get_console_logs": GetLogs(response); Complete(response); break;
                    case "get_performance_metrics": GetPerformance(response); Complete(response); break;
                    case "validate_scene": ValidateScene(response); Complete(response); break;
                    case "capture_game_view":
                        ValidateCaptureRequest(request);
                        processing = true;
                        StartCoroutine(CaptureGameView(request, response));
                        break;
                    default: throw new InvalidDataException("Runtime MCP exposes read-only tools only.");
                }
            }
            catch (Exception exception)
            {
                try { if (File.Exists(requestPath)) File.Delete(requestPath); } catch { }
                CompleteFailure(request, exception);
            }
        }

        private static void ValidateCaptureRequest(RuntimeMcpRequest request)
        {
            var options = request.capture ?? new RuntimeCaptureOptions();
            if (options.width < 1 || options.width > 4096 || options.height < 1 || options.height > 4096)
                throw new InvalidDataException("Capture dimensions must be within 1..4096.");
            if (!options.includeUi)
                throw new NotSupportedException("capture_game_view requires includeUi=true; no artifact was generated.");
        }

        private IEnumerator CaptureGameView(RuntimeMcpRequest request, RuntimeMcpResponse response)
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            try
            {
                var options = request.capture ?? new RuntimeCaptureOptions();
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null) throw new InvalidOperationException("Game view capture returned no texture.");
                if (texture.width != options.width || texture.height != options.height)
                {
                    var scaled = new Texture2D(options.width, options.height, TextureFormat.RGB24, false);
                    var temporary = RenderTexture.GetTemporary(options.width, options.height, 0, RenderTextureFormat.ARGB32);
                    var previous = RenderTexture.active;
                    Graphics.Blit(texture, temporary);
                    RenderTexture.active = temporary;
                    scaled.ReadPixels(new Rect(0, 0, options.width, options.height), 0, 0);
                    scaled.Apply(false, false);
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(temporary);
                    Destroy(texture);
                    texture = scaled;
                }
                var runDirectory = RunDirectory(response.runId);
                var path = Child(runDirectory, "game-view.png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                response.artifacts = new[] { "runs/" + response.runId + "/game-view.png" };
                response.message = $"Captured the runtime game view including UI at {options.width}x{options.height}.";
                response.success = true;
                response.status = "OK";
                Complete(response);
            }
            catch (Exception exception) { CompleteFailure(request, exception, response.runId); }
            finally
            {
                if (texture != null) Destroy(texture);
                processing = false;
            }
        }

        private void InspectScene(RuntimeMcpResponse response)
        {
            float now = Time.realtimeSinceStartup;
            bool useCache = now - sceneSampledRealtime < SceneCacheSeconds;
            if (!useCache) RebuildSceneCache(now);
            response.scene = sceneCacheName;
            response.objects = sceneCache;
            response.success = true;
            response.status = "OK";
            response.dataScope = "Loaded scene roots plus the persistent AeroSim application root; depth-first, capped.";
            response.sampledRealtime = sceneSampledRealtime;
            response.sampleAgeMs = Mathf.Max(0, (now - sceneSampledRealtime) * 1000f);
            response.cached = useCache;
            response.truncated = sceneCacheTruncated;
            response.objectLimit = MaxSceneObjects;
            response.message = useCache
                ? "Returned the most recent bounded runtime scene sample (cache TTL 1 second)."
                : "Runtime scene inspected without mutation; object traversal is capped at 4096 entries.";
        }

        private void RebuildSceneCache(float sampledAt)
        {
            var snapshots = new List<RuntimeObjectSnapshot>(Mathf.Min(256, MaxSceneObjects));
            var roots = new List<GameObject>();
            var visitedRoots = new HashSet<int>();
            bool truncated = false;
            var sceneNames = new List<string>();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount && !truncated; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                sceneNames.Add(scene.name);
                roots.Clear();
                scene.GetRootGameObjects(roots);
                foreach (var rootObject in roots)
                {
                    if (!visitedRoots.Add(rootObject.GetInstanceID())) continue;
                    if (!AddSnapshots(rootObject.transform, snapshots)) { truncated = true; break; }
                }
            }
            var persistentRoot = transform.root;
            if (!truncated && persistentRoot != null && visitedRoots.Add(persistentRoot.gameObject.GetInstanceID()))
                truncated = !AddSnapshots(persistentRoot, snapshots);
            sceneCache = snapshots.ToArray();
            sceneCacheName = string.Join(",", sceneNames);
            sceneCacheTruncated = truncated;
            sceneSampledRealtime = sampledAt;
        }

        private static bool AddSnapshots(Transform transform, List<RuntimeObjectSnapshot> snapshots)
        {
            if (snapshots.Count >= MaxSceneObjects) return false;
            var marker = transform.GetComponent<RuntimeSceneObject>();
            snapshots.Add(new RuntimeObjectSnapshot
            {
                runtimeId = marker == null ? string.Empty : marker.RuntimeId,
                hierarchyPath = HierarchyPath(transform),
                name = transform.name,
                active = transform.gameObject.activeInHierarchy,
                draft = marker != null && marker.IsDraft,
                draftStatus = marker == null ? string.Empty : marker.Status,
                position = transform.position,
                rotationEuler = transform.eulerAngles,
                scale = transform.localScale,
                hasRenderer = transform.TryGetComponent<Renderer>(out _),
                hasCollider = transform.TryGetComponent<Collider>(out _)
            });
            for (int i = 0; i < transform.childCount; i++)
                if (!AddSnapshots(transform.GetChild(i), snapshots)) return false;
            return true;
        }

        private void GetLogs(RuntimeMcpResponse response)
        {
            response.logs = logs.ToArray();
            response.success = true;
            response.status = "OK";
            response.message = "Returned bounded in-memory logs captured since Runtime MCP startup.";
        }

        private void GetPerformance(RuntimeMcpResponse response)
        {
            float now = Time.realtimeSinceStartup;
            bool useCache = now - performanceSampledRealtime < PerformanceCacheSeconds;
            if (!useCache) RebuildPerformanceCache(now);
            response.performance = performanceCache;
            response.success = true;
            response.status = "OK";
            response.dataScope = "Bounded loaded-scene hierarchy sample; active object/component counts are capped with the scene sample.";
            response.sampledRealtime = performanceSampledRealtime;
            response.sampleAgeMs = Mathf.Max(0, (now - performanceSampledRealtime) * 1000f);
            response.cached = useCache;
            response.truncated = sceneCacheTruncated;
            response.objectLimit = MaxSceneObjects;
            response.message = useCache
                ? "Returned the most recent performance sample (cache TTL 1 second)."
                : "Performance and bounded hierarchy counts sampled on the Unity main thread.";
        }

        private void RebuildPerformanceCache(float sampledAt)
        {
            if (sampledAt - sceneSampledRealtime >= SceneCacheSeconds) RebuildSceneCache(sampledAt);
            int active = 0;
            int renderers = 0;
            int colliders = 0;
            foreach (var item in sceneCache)
            {
                if (!item.active) continue;
                active++;
                if (item.hasRenderer) renderers++;
                if (item.hasCollider) colliders++;
            }
            performanceCache = new RuntimePerformanceSnapshot
            {
                realtimeSinceStartup = sampledAt,
                unscaledDeltaTimeMs = Time.unscaledDeltaTime * 1000f,
                smoothedFps = smoothedFps,
                monoUsedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),
                allocatedMemoryBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                activeGameObjects = active,
                rendererCount = renderers,
                colliderCount = colliders
            };
            performanceSampledRealtime = sampledAt;
        }

        private static void ValidateScene(RuntimeMcpResponse response)
        {
            var findings = new List<RuntimeFinding>();
            var drafts = FindObjectsByType<RuntimeSceneObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var draft in drafts)
            {
                if (!draft.IsDraft) continue;
                var state = new RuntimeTransformState(draft.transform);
                if (!state.IsFinite())
                    findings.Add(Finding("G1", "P0", "NON_FINITE_TRANSFORM", draft, "Transform contains NaN or Infinity.", "Finite position, rotation and scale.", draft.transform.position + " / " + draft.transform.localScale, "Reset the draft transform to finite values.", 1f));
                var scale = draft.transform.lossyScale;
                if (!RuntimeSceneEditorService.IsScaleInsideEnvelope(scale))
                    findings.Add(Finding("G1", "P0", "ABNORMAL_SCALE", draft, "Scale exceeds the conservative sandbox envelope.", "Each world axis 0.02..50 and anisotropy <= 100.", scale.ToString("F3"), "Use a plausible draft scale and review from the normal game camera.", .98f));
                if (!RuntimeSceneEditorService.IsInsideBoundary(draft.transform.position))
                    findings.Add(Finding("G0", "P0", "OUTSIDE_SANDBOX_BOUNDARY", draft, "Draft origin is outside the isolated sandbox.", "|x| and |z| <= 100; -2 <= y <= 80.", draft.transform.position.ToString("F2"), "Move the draft inside the sandbox boundary.", 1f));
                var colliders = draft.GetComponentsInChildren<Collider>();
                if (colliders.Length == 0)
                    findings.Add(Finding("G1", "P0", "MISSING_COLLIDER", draft, "No collider is present on the draft hierarchy.", "At least one collider for support and interaction checks.", "0 colliders", "Add a collider through an approved prefab/primitive path.", 1f));
                var renderers = draft.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    findings.Add(Finding("G2", "P0", "MISSING_RENDERER", draft, "No renderer is present on the draft hierarchy.", "At least one renderer visible from normal game views.", "0 renderers", "Use an approved visible primitive or prefab.", 1f));
                ValidateSupport(draft, colliders, findings);
            }
            response.findings = findings.ToArray();
            response.success = true;
            response.status = findings.Count == 0 ? "PASS_BASIC_G0_G1_G2" : "FINDINGS";
            response.message = "Basic automated G0/G1/G2 checks only: support raycast, finite transform, scale, sandbox boundary, collider and renderer. This is not a geological, continuity, route, composition, or human visual judgment.";
        }

        private static void ValidateSupport(RuntimeSceneObject draft, Collider[] ownColliders, List<RuntimeFinding> findings)
        {
            var renderers = draft.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(draft.transform.position, Vector3.zero);
            bool initialized = false;
            foreach (var renderer in renderers)
            {
                if (!initialized) { bounds = renderer.bounds; initialized = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!initialized) return;
            var origin = new Vector3(bounds.center.x, bounds.min.y + .05f, bounds.center.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
            float supportDistance = float.PositiveInfinity;
            foreach (var hit in hits)
            {
                bool own = false;
                foreach (var collider in ownColliders) if (hit.collider == collider) { own = true; break; }
                if (!own) supportDistance = Mathf.Min(supportDistance, hit.distance);
            }
            if (float.IsPositiveInfinity(supportDistance) || supportDistance > .2f)
                findings.Add(Finding("G1", "P0", "POSSIBLE_FLOATING_DRAFT", draft,
                    "A downward support ray from the rendered lower center found no external support within 0.2 m.",
                    "External support surface within 0.2 m below the lower center.",
                    float.IsPositiveInfinity(supportDistance) ? "No support hit within 5 m" : supportDistance.ToString("F3") + " m",
                    "Place the draft on a support surface, then confirm contact visually from normal gameplay views.", .78f));
        }

        private static RuntimeFinding Finding(string gate, string severity, string category, RuntimeSceneObject target, string evidence, string expected, string actual, string recommendation, float confidence)
        {
            return new RuntimeFinding
            {
                id = Guid.NewGuid().ToString("N"), gate = gate, severity = severity, category = category,
                target = target.RuntimeId, evidence = evidence, expected = expected, actual = actual,
                recommendation = recommendation, confidence = confidence
            };
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            logs.Enqueue(new RuntimeLogEntry
            {
                utc = DateTime.UtcNow.ToString("O"), type = type.ToString(),
                message = Truncate(condition, 4096), stackTrace = Truncate(stackTrace, 8192)
            });
            while (logs.Count > MaxLogs) logs.Dequeue();
        }

        private static string Truncate(string value, int max) => string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max);

        private RuntimeMcpResponse CreateBaseResponse(RuntimeMcpRequest request)
        {
            return new RuntimeMcpResponse
            {
                requestId = request.id, tool = request.tool,
                runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)
            };
        }

        private static void ValidateRequest(RuntimeMcpRequest request)
        {
            if (request == null) throw new InvalidDataException("Request JSON is invalid.");
            if (!string.Equals(request.protocol, RuntimeMcpProtocol.Version, StringComparison.Ordinal)) throw new InvalidDataException("Unsupported runtime MCP protocol.");
            if (string.IsNullOrEmpty(request.id) || request.id.Length > 64 || !SafeIdentifier(request.id)) throw new InvalidDataException("Request id must be 1..64 ASCII letters, digits, '-' or '_'.");
            if (!RuntimeMcpProtocol.Tools.Contains(request.tool ?? string.Empty)) throw new InvalidDataException("Unknown or non-read-only runtime MCP tool: " + request.tool);
        }

        private void CompleteFailure(RuntimeMcpRequest request, Exception exception, string existingRunId = null)
        {
            var response = new RuntimeMcpResponse
            {
                requestId = request?.id ?? "invalid-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                tool = request?.tool ?? string.Empty,
                runId = existingRunId ?? DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-error",
                success = false, status = "ERROR", message = exception.Message, errorType = exception.GetType().Name
            };
            Complete(response);
        }

        private void RejectWithoutReading(string requestPath, string status, string message)
        {
            string requestId;
            try
            {
                EnsureInside(requestPath, inbox);
                requestId = Path.GetFileNameWithoutExtension(requestPath);
                if (!SafeIdentifier(requestId)) requestId = "rejected-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                File.Delete(requestPath);
            }
            catch { return; }
            Complete(new RuntimeMcpResponse
            {
                requestId = requestId,
                success = false, status = status, message = message, errorType = "RateLimitException",
                runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-rejected"
            });
        }

        private void Complete(RuntimeMcpResponse response)
        {
            try
            {
                Directory.CreateDirectory(RunDirectory(response.runId));
                var safeId = SafeIdentifier(response.requestId) ? response.requestId : "invalid-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var target = Child(outbox, safeId + "-" + response.runId + ".json");
                var temporary = target + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(response, true));
                if (File.Exists(target)) File.Delete(target);
                File.Move(temporary, target);
            }
            catch (Exception exception) { Debug.LogError("Runtime MCP response write failed: " + exception.Message); }
        }

        private string RunDirectory(string runId)
        {
            if (!SafeIdentifier(runId)) throw new InvalidDataException("Unsafe run id.");
            var path = Child(runs, runId);
            Directory.CreateDirectory(path);
            return path;
        }

        private static string Child(string parent, string leaf)
        {
            if (string.IsNullOrEmpty(leaf) || leaf.IndexOfAny(new[] { '/', '\\' }) >= 0 || leaf == "." || leaf == "..")
                throw new InvalidDataException("Unsafe path segment.");
            var result = Path.GetFullPath(Path.Combine(parent, leaf));
            EnsureInside(result, parent);
            return result;
        }

        private static void EnsureNotLink(string path)
        {
            if (!Directory.Exists(path) && !File.Exists(path)) return;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Runtime MCP queue directories cannot be symbolic links or reparse points.");
        }

        private static void EnsureInside(string child, string parent)
        {
            var fullChild = Path.GetFullPath(child);
            var fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullChild.StartsWith(fullParent, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Runtime MCP path escaped its isolated root.");
        }

        private static bool SafeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 96) return false;
            foreach (var c in value)
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') return false;
            return true;
        }

        private static string HierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            while (transform != null) { names.Push(transform.name); transform = transform.parent; }
            return string.Join("/", names);
        }
    }
}
