#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AeroSim.Editor.ReviewMcp
{
    [InitializeOnLoad]
    internal static class AeroSimReviewInspection
    {
        internal sealed class Result
        {
            internal bool Success;
            internal string Status;
            internal string Message;
            internal readonly List<string> Artifacts = new List<string>();
            internal ReviewFinding[] Findings = Array.Empty<ReviewFinding>();
        }

        [Serializable]
        private sealed class InspectionDocument
        {
            public string schema = "aerosim.review-inspection/v1";
            public string generatedUtc;
            public string kind;
            public string target;
            public string scene;
            public string details;
            public string reviewStatus = "NOT_REVIEWED";
            public string gateStatus = "GATE_NOT_RUN";
            public SceneObjectSnapshot[] objects = Array.Empty<SceneObjectSnapshot>();
            public LogRecord[] logs = Array.Empty<LogRecord>();
            public MetricRecord[] metrics = Array.Empty<MetricRecord>();
        }

        [Serializable]
        private sealed class LogRecord
        {
            public string timestampUtc;
            public string type;
            public string message;
            public string stackTrace;
        }

        [Serializable]
        private sealed class MetricRecord
        {
            public string name;
            public string value;
            public string unit;
            public string source;
        }

        [Serializable]
        private sealed class SceneContext
        {
            public string activeScene;
            public string[] loadedScenes;
            public bool isPlaying;
            public bool isPaused;
            public int rootObjectCount;
            public int totalObjectCount;
            public int cameraCount;
            public int lightCount;
            public int rendererCount;
            public string renderPipeline;
            public Color ambientLight;
            public string reviewStatus = "NOT_REVIEWED";
            public string gateStatus = "GATE_NOT_RUN";
        }

        private const int MaxBufferedLogs = 2000;
        private static readonly object LogLock = new object();
        private static readonly Queue<LogRecord> Logs = new Queue<LogRecord>();

        static AeroSimReviewInspection()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
        }

        internal static Result Execute(ReviewRequest request, string runDirectory)
        {
            switch (request.tool)
            {
                case "inspect_scene": return InspectScene(runDirectory);
                case "inspect_object": return InspectObject(request.target, runDirectory);
                case "inspect_material": return InspectMaterial(request, runDirectory);
                case "inspect_lod_group": return InspectLodGroup(request.target, runDirectory);
                case "inspect_colliders": return InspectColliders(request.target, runDirectory);
                case "inspect_prefab": return InspectPrefab(request, runDirectory);
                case "get_console_logs": return ExportLogs(runDirectory, false);
                case "get_shader_errors": return ExportLogs(runDirectory, true);
                case "get_performance_metrics": return ExportPerformance(runDirectory);
                case "raycast_from_view": return RaycastFromView(runDirectory);
                case "list_visible_objects": return ListVisibleObjects(request, runDirectory);
                case "run_validator": return NotSupported("NOT_SUPPORTED: Phase 1 has no frozen validator or gate contract; no validation was run.");
                default: return NotSupported("NOT_SUPPORTED: unknown read tool.");
            }
        }

        internal static Result CaptureDiagnosticBundle(ReviewRequest request, string runDirectory)
        {
            Result aggregate = new Result { Success = true, Status = "OK", Message = "Diagnostic evidence bundle generated. Evidence remains NOT_REVIEWED; no gate was run." };
            Append(aggregate, InspectScene(runDirectory));
            Append(aggregate, ListVisibleObjects(request, runDirectory));
            Append(aggregate, ExportLogs(runDirectory, false));
            Append(aggregate, ExportLogs(runDirectory, true));
            Append(aggregate, ExportPerformance(runDirectory));

            ReviewRequest captureRequest = JsonUtility.FromJson<ReviewRequest>(JsonUtility.ToJson(request));
            captureRequest.tool = "capture_game_view";
            if (AeroSimReviewCapture.TryCapture(captureRequest, runDirectory, out string image, out string captureMessage))
            {
                aggregate.Artifacts.Add(image);
            }
            else
            {
                aggregate.Message += " Camera image omitted: " + captureMessage;
            }
            return aggregate;
        }

        internal static Camera ResolveCamera(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return Camera.main != null ? Camera.main : AeroSimReviewCapture.FindEnabledCamera();
            }
            GameObject gameObject = ResolveGameObject(target);
            if (gameObject != null)
            {
                return gameObject.GetComponent<Camera>();
            }
            foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (camera.name == target && camera.gameObject.scene.IsValid())
                {
                    return camera;
                }
            }
            return null;
        }

        private static Result InspectScene(string runDirectory)
        {
            GameObject[] objects = SceneObjects().ToArray();
            Scene active = SceneManager.GetActiveScene();
            SceneContext context = new SceneContext
            {
                activeScene = active.IsValid() ? active.path : string.Empty,
                loadedScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(index => SceneManager.GetSceneAt(index).path).ToArray(),
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                rootObjectCount = active.IsValid() ? active.rootCount : 0,
                totalObjectCount = objects.Length,
                cameraCount = objects.Count(item => item.GetComponent<Camera>() != null),
                lightCount = objects.Count(item => item.GetComponent<Light>() != null),
                rendererCount = objects.Sum(item => item.GetComponents<Renderer>().Length),
                renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                    ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().FullName
                    : "Built-in Render Pipeline",
                ambientLight = RenderSettings.ambientLight
            };
            WriteJson(runDirectory, "scene-context.json", context);

            SceneSnapshot snapshot = new SceneSnapshot
            {
                scene = active.IsValid() ? active.path : string.Empty,
                generatedUtc = UtcNow(),
                objects = objects.Select(Snapshot).ToArray()
            };
            WriteJson(runDirectory, "scene-snapshot.json", snapshot);
            return Ok("Scene context and hierarchy snapshot exported.", "scene-context.json", "scene-snapshot.json");
        }

        private static Result InspectObject(string target, string runDirectory)
        {
            GameObject gameObject = ResolveGameObject(target);
            if (gameObject == null)
            {
                return Failed("TARGET_NOT_FOUND", "Object target was not found by GlobalObjectId, hierarchy path, or unique name.");
            }
            InspectionDocument document = Document("object", target);
            document.objects = new[] { Snapshot(gameObject) };
            document.details = EditorJsonUtility.ToJson(gameObject, true);
            WriteJson(runDirectory, "object-inspection.json", document);
            return Ok("Object inspection exported.", "object-inspection.json");
        }

        private static Result InspectMaterial(ReviewRequest request, string runDirectory)
        {
            Material material = null;
            string path = SafeAssetPath(string.IsNullOrEmpty(request.assetPath) ? request.target : request.assetPath);
            if (!string.IsNullOrEmpty(path))
            {
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            if (material == null)
            {
                GameObject gameObject = ResolveGameObject(request.target);
                Renderer renderer = gameObject != null ? gameObject.GetComponent<Renderer>() : null;
                material = renderer != null ? renderer.sharedMaterials.FirstOrDefault(item => item != null) : null;
            }
            if (material == null)
            {
                return Failed("TARGET_NOT_FOUND", "Material was not found below Assets/AeroSim or on the target Renderer.");
            }
            InspectionDocument document = Document("material", request.target);
            document.details = EditorJsonUtility.ToJson(material, true);
            document.target = AssetDatabase.GetAssetPath(material);
            WriteJson(runDirectory, "material-inspection.json", document);
            return Ok("Material inspection exported.", "material-inspection.json");
        }

        private static Result InspectLodGroup(string target, string runDirectory)
        {
            GameObject gameObject = ResolveGameObject(target);
            LODGroup group = gameObject != null ? gameObject.GetComponent<LODGroup>() : null;
            if (group == null)
            {
                return Failed("TARGET_NOT_FOUND", "Target has no LODGroup component.");
            }
            InspectionDocument document = Document("lod-group", target);
            document.objects = new[] { Snapshot(gameObject) };
            document.details = EditorJsonUtility.ToJson(group, true);
            WriteJson(runDirectory, "lod-group-inspection.json", document);
            return Ok("LODGroup inspection exported.", "lod-group-inspection.json");
        }

        private static Result InspectColliders(string target, string runDirectory)
        {
            GameObject gameObject = ResolveGameObject(target);
            if (gameObject == null)
            {
                return Failed("TARGET_NOT_FOUND", "Collider target object was not found.");
            }
            Collider[] colliders = gameObject.GetComponentsInChildren<Collider>(true);
            InspectionDocument document = Document("colliders", target);
            document.objects = colliders.Select(item => Snapshot(item.gameObject)).Distinct(new SnapshotPathComparer()).ToArray();
            document.details = string.Join("\n", colliders.Select(item => EditorJsonUtility.ToJson(item, false)));
            WriteJson(runDirectory, "collider-inspection.json", document);
            return Ok("Collider inspection exported for " + colliders.Length + " collider(s).", "collider-inspection.json");
        }

        private static Result InspectPrefab(ReviewRequest request, string runDirectory)
        {
            UnityEngine.Object target = null;
            string assetPath = SafeAssetPath(request.assetPath);
            if (!string.IsNullOrEmpty(assetPath))
            {
                target = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
            if (target == null)
            {
                target = ResolveGameObject(request.target);
            }
            if (target == null)
            {
                return Failed("TARGET_NOT_FOUND", "Prefab asset or instance was not found.");
            }
            InspectionDocument document = Document("prefab", request.target);
            document.details = EditorJsonUtility.ToJson(target, true) + "\nassetPath=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
            GameObject gameObject = target as GameObject;
            if (gameObject != null)
            {
                document.objects = new[] { Snapshot(gameObject) };
            }
            WriteJson(runDirectory, "prefab-inspection.json", document);
            return Ok("Prefab inspection exported.", "prefab-inspection.json");
        }

        private static Result ExportLogs(string runDirectory, bool shaderOnly)
        {
            LogRecord[] records;
            lock (LogLock)
            {
                records = Logs.Where(item => !shaderOnly || IsShaderMessage(item.message)).ToArray();
            }
            if (shaderOnly)
            {
                List<LogRecord> combined = records.ToList();
                foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (material.shader != null && !material.shader.isSupported)
                    {
                        combined.Add(new LogRecord
                        {
                            timestampUtc = UtcNow(),
                            type = "ShaderUnsupported",
                            message = material.name + " uses unsupported shader " + material.shader.name,
                            stackTrace = string.Empty
                        });
                    }
                }
                records = combined.ToArray();
            }
            InspectionDocument document = Document(shaderOnly ? "shader-errors" : "console-errors", string.Empty);
            document.logs = records.Where(item => shaderOnly || item.type == LogType.Error.ToString() || item.type == LogType.Exception.ToString() || item.type == LogType.Assert.ToString()).ToArray();
            document.details = shaderOnly
                ? "Buffered shader-related log messages since editor domain load plus loaded materials whose Shader.isSupported is false. This is not a forced shader compilation pass."
                : "Buffered errors, exceptions, and asserts since editor domain load; historical Console entries before domain load may be absent.";
            string fileName = shaderOnly ? "shader-errors.json" : "console-errors.json";
            WriteJson(runDirectory, fileName, document);
            return Ok(document.details, fileName);
        }

        private static Result ExportPerformance(string runDirectory)
        {
            long managed = GC.GetTotalMemory(false);
            InspectionDocument document = Document("performance-baseline", string.Empty);
            document.details = "Point-in-time editor baseline only; this is not a Profiler capture or runtime benchmark.";
            document.metrics = new[]
            {
                Metric("managedMemory", managed.ToString(), "bytes", "GC.GetTotalMemory(false)"),
                Metric("systemMemory", SystemInfo.systemMemorySize.ToString(), "MB", "SystemInfo.systemMemorySize"),
                Metric("graphicsMemory", SystemInfo.graphicsMemorySize.ToString(), "MB", "SystemInfo.graphicsMemorySize"),
                Metric("processorCount", SystemInfo.processorCount.ToString(), "count", "SystemInfo.processorCount"),
                Metric("targetFrameRate", Application.targetFrameRate.ToString(), "fps", "Application.targetFrameRate"),
                Metric("isPlaying", EditorApplication.isPlaying.ToString(), "bool", "EditorApplication.isPlaying"),
                Metric("timeSinceStartup", EditorApplication.timeSinceStartup.ToString("F3"), "seconds", "EditorApplication.timeSinceStartup")
            };
            WriteJson(runDirectory, "performance-metrics.json", document);
            return Ok(document.details, "performance-metrics.json");
        }

        private static Result RaycastFromView(string runDirectory)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
            {
                return Failed("VIEW_NOT_AVAILABLE", "No active SceneView camera is available for raycasting.");
            }
            Ray ray = new Ray(view.camera.transform.position, view.camera.transform.forward);
            bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, view.camera.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            InspectionDocument document = Document("scene-view-raycast", string.Empty);
            document.details = hit
                ? "hit=true; object=" + HierarchyPath(hitInfo.collider.gameObject) + "; point=" + hitInfo.point + "; normal=" + hitInfo.normal + "; distance=" + hitInfo.distance
                : "hit=false; origin=" + ray.origin + "; direction=" + ray.direction;
            document.objects = hit ? new[] { Snapshot(hitInfo.collider.gameObject) } : Array.Empty<SceneObjectSnapshot>();
            WriteJson(runDirectory, "scene-view-raycast.json", document);
            return Ok(hit ? "SceneView center ray hit exported." : "SceneView center ray completed with no hit.", "scene-view-raycast.json");
        }

        private static Result ListVisibleObjects(ReviewRequest request, string runDirectory)
        {
            Camera camera = ResolveCamera(request.capture != null ? request.capture.cameraPath : null);
            if (camera == null && SceneView.lastActiveSceneView != null)
            {
                camera = SceneView.lastActiveSceneView.camera;
            }
            if (camera == null)
            {
                return Failed("CAMERA_NOT_AVAILABLE", "No camera is available for frustum visibility inspection.");
            }
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            SceneObjectSnapshot[] visible = SceneObjects()
                .Where(item => item.activeInHierarchy)
                .Where(item => item.GetComponents<Renderer>().Any(renderer => renderer.enabled && GeometryUtility.TestPlanesAABB(planes, renderer.bounds)))
                .Select(Snapshot)
                .ToArray();
            InspectionDocument document = Document("visible-objects", HierarchyPath(camera.gameObject));
            document.details = "Frustum intersection of enabled Renderer bounds. Occlusion and per-pixel visibility are not evaluated.";
            document.objects = visible;
            WriteJson(runDirectory, "visible-objects.json", document);
            return Ok("Visible object approximation exported for " + visible.Length + " object(s).", "visible-objects.json");
        }

        private static IEnumerable<GameObject> SceneObjects()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item != null && item.scene.IsValid() && item.scene.isLoaded && (item.hideFlags & HideFlags.HideAndDontSave) == 0)
                .OrderBy(HierarchyPath, StringComparer.Ordinal);
        }

        private static GameObject ResolveGameObject(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return null;
            }
            if (GlobalObjectId.TryParse(target, out GlobalObjectId id))
            {
                GameObject byId = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
                if (byId != null)
                {
                    return byId;
                }
            }
            GameObject[] exactPath = SceneObjects().Where(item => string.Equals(HierarchyPath(item), target, StringComparison.Ordinal)).ToArray();
            if (exactPath.Length == 1)
            {
                return exactPath[0];
            }
            GameObject[] exactName = SceneObjects().Where(item => string.Equals(item.name, target, StringComparison.Ordinal)).ToArray();
            return exactName.Length == 1 ? exactName[0] : null;
        }

        private static SceneObjectSnapshot Snapshot(GameObject gameObject)
        {
            Renderer[] renderers = gameObject.GetComponents<Renderer>();
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return new SceneObjectSnapshot
            {
                globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString(),
                hierarchyPath = HierarchyPath(gameObject),
                name = gameObject.name,
                tag = SafeTag(gameObject),
                layer = gameObject.layer,
                active = gameObject.activeInHierarchy,
                position = gameObject.transform.position,
                rotationEuler = gameObject.transform.eulerAngles,
                scale = gameObject.transform.lossyScale,
                components = gameObject.GetComponents<Component>().Where(item => item != null).Select(item => item.GetType().FullName).ToArray(),
                materials = renderers.SelectMany(item => item.sharedMaterials).Where(item => item != null).Select(item => AssetDatabase.GetAssetPath(item)).Distinct().ToArray(),
                bounds = bounds
            };
        }

        private static string HierarchyPath(GameObject gameObject)
        {
            List<string> parts = new List<string>();
            Transform current = gameObject.transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return gameObject.scene.name + ":/" + string.Join("/", parts);
        }

        private static string SafeTag(GameObject gameObject)
        {
            try { return gameObject.tag; }
            catch (UnityException) { return "<invalid-tag>"; }
        }

        private static string SafeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            string normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/AeroSim/", StringComparison.Ordinal) || normalized.Contains("../") || Path.IsPathRooted(normalized))
            {
                return null;
            }
            return normalized;
        }

        private static InspectionDocument Document(string kind, string target)
        {
            return new InspectionDocument
            {
                generatedUtc = UtcNow(),
                kind = kind,
                target = target ?? string.Empty,
                scene = SceneManager.GetActiveScene().path
            };
        }

        private static MetricRecord Metric(string name, string value, string unit, string source)
        {
            return new MetricRecord { name = name, value = value, unit = unit, source = source };
        }

        private static void WriteJson(string directory, string fileName, object value)
        {
            string json = JsonUtility.ToJson(value, true);
            AeroSimReviewBridge.WriteTextAtomically(Path.Combine(directory, fileName), json);
        }

        private static Result Ok(string message, params string[] artifacts)
        {
            Result result = new Result { Success = true, Status = "OK", Message = message };
            result.Artifacts.AddRange(artifacts);
            return result;
        }

        private static Result Failed(string status, string message)
        {
            return new Result { Success = false, Status = status, Message = message };
        }

        private static Result NotSupported(string message)
        {
            return Failed("NOT_SUPPORTED", message);
        }

        private static void Append(Result aggregate, Result child)
        {
            if (child.Success)
            {
                aggregate.Artifacts.AddRange(child.Artifacts);
            }
            else
            {
                aggregate.Message += " Partial export issue: " + child.Message;
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            lock (LogLock)
            {
                Logs.Enqueue(new LogRecord { timestampUtc = UtcNow(), type = type.ToString(), message = condition, stackTrace = stackTrace });
                while (Logs.Count > MaxBufferedLogs)
                {
                    Logs.Dequeue();
                }
            }
        }

        private static bool IsShaderMessage(string message)
        {
            return !string.IsNullOrEmpty(message) &&
                   (message.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string UtcNow()
        {
            return DateTime.UtcNow.ToString("o");
        }

        private sealed class SnapshotPathComparer : IEqualityComparer<SceneObjectSnapshot>
        {
            public bool Equals(SceneObjectSnapshot x, SceneObjectSnapshot y)
            {
                return ReferenceEquals(x, y) || (x != null && y != null && x.globalObjectId == y.globalObjectId);
            }

            public int GetHashCode(SceneObjectSnapshot obj)
            {
                return obj != null && obj.globalObjectId != null ? obj.globalObjectId.GetHashCode() : 0;
            }
        }
    }
}
#endif
