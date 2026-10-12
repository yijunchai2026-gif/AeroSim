#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AeroSim.Editor.ReviewMcp
{
    /// <summary>
    /// Deliberately small, allow-listed mutation surface for the review bridge.  No request value is
    /// ever treated as a type name, method name, command line, or script.
    /// </summary>
    internal static class AeroSimReviewMutations
    {
        private const string AssetRoot = "Assets/AeroSim/";
        private static readonly object Sync = new object();
        private static readonly List<AppliedChange> Applied = new List<AppliedChange>();

        private sealed class AppliedChange
        {
            public ChangeRecord record;
            public AssetBackup assetBackup;
        }

        private sealed class AssetBackup
        {
            public string assetPath;
            public byte[] assetBytes;
            public byte[] metaBytes;
        }

        [Serializable]
        private sealed class ObjectState
        {
            public string globalObjectId;
            public string hierarchyPath;
            public string assetPath;
            public string type;
            public string json;
            public string[] componentJson = Array.Empty<string>();
            public string dependencyHash;
        }

        internal static ReviewResponse Execute(ReviewRequest request, string runDirectory)
        {
            var response = NewResponse(request);
            if (request == null)
                return Fail(response, "INVALID_REQUEST", "Request is required.");
            if (!string.Equals(request.protocol, AeroSimReviewProtocol.Version, StringComparison.Ordinal))
                return Fail(response, "INVALID_REQUEST", "Unsupported protocol version.");
            if (!AeroSimReviewProtocol.MutationTools.Contains(request.tool ?? string.Empty))
                return Fail(response, "NOT_SUPPORTED", "The requested mutation tool is not supported.");

            try
            {
                switch (request.tool)
                {
                    case "undo_change": return UndoChange(request, response, runDirectory);
                    case "rollback_transaction": return RollbackTransaction(request, response, runDirectory);
                    case "run_asset_gate": return RunAssetGate(request, response);
                    case "save_scene": return SaveScene(request, response);
                    case "save_prefab": return SavePrefab(request, response);
                    default: return ExecuteUndoable(request, response, runDirectory);
                }
            }
            catch (Exception ex)
            {
                response.success = false;
                response.status = "FAILED";
                response.message = ex.Message;
                response.errorType = ex.GetType().FullName;
                response.stackTrace = ex.StackTrace;
                return response;
            }
        }

        private static ReviewResponse ExecuteUndoable(ReviewRequest request, ReviewResponse response, string runDirectory)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("AeroSim Review MCP: " + request.tool);
            Object target = null;
            string before = string.Empty;
            AssetBackup assetBackup = null;
            try
            {
                assetBackup = CaptureAssetBackup(request);
                target = ResolveMutationTarget(request);
                before = Snapshot(target);
                Dispatch(request, target);
                string after = Snapshot(ResolveMutationTarget(request) ?? target);

                Undo.CollapseUndoOperations(group);
                Undo.IncrementCurrentGroup();
                var change = new ChangeRecord
                {
                    transactionId = EffectiveTransactionId(request),
                    requestId = request.id,
                    tool = request.tool,
                    target = request.target,
                    generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    beforeJson = before,
                    afterJson = after,
                    undoGroup = group,
                    verified = true,
                    rolledBack = false
                };
                PersistChange(change, runDirectory);
                lock (Sync) Applied.Add(new AppliedChange { record = change, assetBackup = assetBackup });
                response.success = true;
                response.status = "OK";
                response.message = "Mutation applied and registered with Unity Undo.";
                response.transactionId = change.transactionId;
                return response;
            }
            catch (MutationException ex)
            {
                Undo.RevertAllDownToGroup(group);
                RestoreAssetBackup(assetBackup);
                return Fail(response, ex.Status, ex.Message);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                RestoreAssetBackup(assetBackup);
                throw;
            }
        }

        private static void Dispatch(ReviewRequest request, Object target)
        {
            switch (request.tool)
            {
                case "set_transform": SetTransform(RequireGameObject(target).transform, request); break;
                case "set_camera": SetCamera(RequireComponent<Camera>(target), request); break;
                case "set_light_property": SetLight(RequireComponent<Light>(target), request); break;
                case "set_material_property": SetMaterialProperty(ResolveMaterial(request, target), request); break;
                case "set_lod_thresholds": SetLodThresholds(RequireComponent<LODGroup>(target), request); break;
                case "add_or_update_collider": AddOrUpdateCollider(RequireGameObject(target), request); break;
                case "toggle_object": ToggleObject(target, request); break;
                case "replace_material": ReplaceMaterial(RequireComponent<Renderer>(target), request); break;
                case "replace_mesh": ReplaceMesh(target, request); break;
                case "add_component": AddComponent(RequireGameObject(target), request); break;
                case "update_component": UpdateComponent(RequireGameObject(target), request); break;
                case "edit_prefab": EditPrefab(request); break;
                case "update_model_importer": UpdateModelImporter(request); break;
                case "reimport_asset": ReimportAsset(request); break;
                default: throw NotSupported("Tool has no safe implementation.");
            }
        }

        private static Object ResolveMutationTarget(ReviewRequest request)
        {
            if (request.tool == "update_model_importer")
            {
                string path = RequireAllowedAssetPath(request.assetPath, true);
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (importer == null) throw NotFound("Asset importer was not found.");
                return importer;
            }
            if (request.tool == "reimport_asset" || request.tool == "edit_prefab")
                return LoadAllowedAsset(request.assetPath, typeof(Object), true);
            if (!string.IsNullOrWhiteSpace(request.target))
            {
                Object target = ResolveObject(request.target);
                return request.tool == "set_material_property" ? ResolveMaterial(request, target) : target;
            }
            if (!string.IsNullOrWhiteSpace(request.assetPath))
            {
                Object target = LoadAllowedAsset(request.assetPath, typeof(Object), true);
                return request.tool == "set_material_property" ? ResolveMaterial(request, target) : target;
            }
            throw Invalid("A target GlobalObjectId, hierarchy path, or allowed asset path is required.");
        }

        private static Object ResolveObject(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0) throw Invalid("Target is empty.");

            GlobalObjectId id;
            if (GlobalObjectId.TryParse(value, out id))
            {
                Object found = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                if (found == null) throw NotFound("GlobalObjectId did not resolve to a loaded object.");
                EnsureObjectAllowed(found);
                return found;
            }

            if (value.StartsWith("Assets/", StringComparison.Ordinal))
                return LoadAllowedAsset(value, typeof(Object), true);

            GameObject match = null;
            string normalized = value.Trim('/');
            foreach (GameObject root in LoadedSceneRoots())
            {
                string rootPath = HierarchyPath(root.transform);
                Transform candidate = string.Equals(rootPath, normalized, StringComparison.Ordinal)
                    ? root.transform
                    : FindByPath(root.transform, normalized);
                if (candidate == null) continue;
                if (match != null && match != candidate.gameObject)
                    throw Invalid("Hierarchy path is ambiguous; use GlobalObjectId.");
                match = candidate.gameObject;
            }
            if (match == null) throw NotFound("Hierarchy path did not resolve to a loaded scene object.");
            return match;
        }

        private static IEnumerable<GameObject> LoadedSceneRoots()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects()) yield return root;
            }
        }

        private static Transform FindByPath(Transform root, string path)
        {
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int start = parts.Length > 0 && string.Equals(parts[0], root.name, StringComparison.Ordinal) ? 1 : 0;
            Transform current = root;
            for (int i = start; i < parts.Length; i++)
            {
                Transform next = null;
                for (int child = 0; child < current.childCount; child++)
                {
                    Transform item = current.GetChild(child);
                    if (!string.Equals(item.name, parts[i], StringComparison.Ordinal)) continue;
                    if (next != null) throw Invalid("Hierarchy path contains duplicate sibling names; use GlobalObjectId.");
                    next = item;
                }
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        private static string HierarchyPath(Transform transform)
        {
            var names = new List<string>();
            while (transform != null) { names.Add(transform.name); transform = transform.parent; }
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static void SetTransform(Transform transform, ReviewRequest request)
        {
            Undo.RecordObject(transform, "Set transform");
            switch (request.property ?? string.Empty)
            {
                case "position": transform.position = request.vectorValue; break;
                case "localPosition": transform.localPosition = request.vectorValue; break;
                case "rotationEuler": transform.eulerAngles = request.rotationEuler; break;
                case "localRotationEuler": transform.localEulerAngles = request.rotationEuler; break;
                case "localScale": transform.localScale = request.vectorValue; break;
                case "allLocal":
                    RequireFloatCount(request.floatValues, 3, "allLocal scale");
                    transform.localPosition = request.vectorValue;
                    transform.localEulerAngles = request.rotationEuler;
                    transform.localScale = new Vector3(request.floatValues[0], request.floatValues[1], request.floatValues[2]);
                    break;
                default: throw NotSupported("Transform property is not allow-listed.");
            }
            EditorUtility.SetDirty(transform);
        }

        private static void SetCamera(Camera camera, ReviewRequest request)
        {
            Undo.RecordObject(camera, "Set camera");
            switch (request.property ?? string.Empty)
            {
                case "fieldOfView": camera.fieldOfView = InRange(request.floatValue, 1f, 179f, "fieldOfView"); break;
                case "nearClipPlane": camera.nearClipPlane = Positive(request.floatValue, "nearClipPlane"); break;
                case "farClipPlane": camera.farClipPlane = Positive(request.floatValue, "farClipPlane"); break;
                case "orthographic": camera.orthographic = request.boolValue; break;
                case "orthographicSize": camera.orthographicSize = Positive(request.floatValue, "orthographicSize"); break;
                case "backgroundColor": camera.backgroundColor = request.colorValue; break;
                case "cullingMask": camera.cullingMask = request.intValue; break;
                case "clearFlags": camera.clearFlags = EnumValue<CameraClearFlags>(request.intValue, "clearFlags"); break;
                case "enabled": camera.enabled = request.boolValue; break;
                default: throw NotSupported("Camera property is not allow-listed.");
            }
            if (camera.farClipPlane <= camera.nearClipPlane) throw Invalid("farClipPlane must exceed nearClipPlane.");
            EditorUtility.SetDirty(camera);
        }

        private static void SetLight(Light light, ReviewRequest request)
        {
            Undo.RecordObject(light, "Set light");
            switch (request.property ?? string.Empty)
            {
                case "intensity": light.intensity = NonNegative(request.floatValue, "intensity"); break;
                case "range": light.range = Positive(request.floatValue, "range"); break;
                case "spotAngle": light.spotAngle = InRange(request.floatValue, 1f, 179f, "spotAngle"); break;
                case "innerSpotAngle": light.innerSpotAngle = InRange(request.floatValue, 0f, 179f, "innerSpotAngle"); break;
                case "color": light.color = request.colorValue; break;
                case "type": light.type = EnumValue<LightType>(request.intValue, "type"); break;
                case "shadows": light.shadows = EnumValue<LightShadows>(request.intValue, "shadows"); break;
                case "enabled": light.enabled = request.boolValue; break;
                default: throw NotSupported("Light property is not allow-listed.");
            }
            EditorUtility.SetDirty(light);
        }

        private static Material ResolveMaterial(ReviewRequest request, Object target)
        {
            if (target is Material material) { EnsureObjectAllowed(material); return material; }
            Renderer renderer = ComponentFrom<Renderer>(target);
            if (renderer == null) throw Invalid("Target must be a Material or Renderer.");
            Material[] materials = renderer.sharedMaterials;
            int index = request.intValue;
            if (index < 0 || index >= materials.Length || materials[index] == null)
                throw Invalid("Material slot is out of range or empty.");
            EnsureObjectAllowed(materials[index]);
            return materials[index];
        }

        private static void SetMaterialProperty(Material material, ReviewRequest request)
        {
            string property = request.property ?? string.Empty;
            var colorProperties = new HashSet<string>(StringComparer.Ordinal) { "_BaseColor", "_Color", "_EmissionColor", "_SpecColor" };
            var floatProperties = new HashSet<string>(StringComparer.Ordinal) { "_Metallic", "_Smoothness", "_Glossiness", "_Cutoff", "_BumpScale", "_OcclusionStrength" };
            var textureProperties = new HashSet<string>(StringComparer.Ordinal) { "_BaseMap", "_MainTex", "_BumpMap", "_EmissionMap", "_OcclusionMap", "_MetallicGlossMap" };
            if (!material.HasProperty(property)) throw Invalid("Material does not expose the requested property.");
            Undo.RecordObject(material, "Set material property");
            if (colorProperties.Contains(property)) material.SetColor(property, request.colorValue);
            else if (floatProperties.Contains(property)) material.SetFloat(property, request.floatValue);
            else if (textureProperties.Contains(property))
                material.SetTexture(property, string.IsNullOrWhiteSpace(request.assetPath) ? null : LoadAllowedAsset<Texture>(request.assetPath));
            else throw NotSupported("Material property is not allow-listed.");
            EditorUtility.SetDirty(material);
        }

        private static void SetLodThresholds(LODGroup group, ReviewRequest request)
        {
            LOD[] lods = group.GetLODs();
            if (request.floatValues == null || request.floatValues.Length != lods.Length)
                throw Invalid("One threshold is required for each LOD level.");
            float previous = 1f;
            for (int i = 0; i < lods.Length; i++)
            {
                float value = InRange(request.floatValues[i], 0f, 1f, "LOD threshold");
                if (value >= previous) throw Invalid("LOD thresholds must be strictly descending.");
                lods[i].screenRelativeTransitionHeight = value;
                previous = value;
            }
            Undo.RecordObject(group, "Set LOD thresholds");
            group.SetLODs(lods);
            group.RecalculateBounds();
            EditorUtility.SetDirty(group);
        }

        private static void AddOrUpdateCollider(GameObject gameObject, ReviewRequest request)
        {
            string type = request.componentType ?? string.Empty;
            Collider collider;
            switch (type)
            {
                case "BoxCollider": collider = GetOrAdd<BoxCollider>(gameObject); break;
                case "SphereCollider": collider = GetOrAdd<SphereCollider>(gameObject); break;
                case "CapsuleCollider": collider = GetOrAdd<CapsuleCollider>(gameObject); break;
                case "MeshCollider": collider = GetOrAdd<MeshCollider>(gameObject); break;
                default: throw NotSupported("Collider type is not allow-listed.");
            }
            Undo.RecordObject(collider, "Update collider");
            ApplyColliderProperty(collider, request);
            EditorUtility.SetDirty(collider);
        }

        private static void ApplyColliderProperty(Collider collider, ReviewRequest request)
        {
            switch (request.property ?? string.Empty)
            {
                case "enabled": collider.enabled = request.boolValue; break;
                case "isTrigger": collider.isTrigger = request.boolValue; break;
                case "center":
                    if (collider is BoxCollider boxCenter) boxCenter.center = request.vectorValue;
                    else if (collider is SphereCollider sphereCenter) sphereCenter.center = request.vectorValue;
                    else if (collider is CapsuleCollider capsuleCenter) capsuleCenter.center = request.vectorValue;
                    else throw NotSupported("MeshCollider has no center property.");
                    break;
                case "size":
                    if (!(collider is BoxCollider box)) throw NotSupported("size is only valid for BoxCollider.");
                    box.size = PositiveVector(request.vectorValue, "size");
                    break;
                case "radius":
                    if (collider is SphereCollider sphere) sphere.radius = Positive(request.floatValue, "radius");
                    else if (collider is CapsuleCollider capsule) capsule.radius = Positive(request.floatValue, "radius");
                    else throw NotSupported("radius is only valid for sphere or capsule colliders.");
                    break;
                case "height":
                    if (!(collider is CapsuleCollider heightCapsule)) throw NotSupported("height is only valid for CapsuleCollider.");
                    heightCapsule.height = Positive(request.floatValue, "height");
                    break;
                case "direction":
                    if (!(collider is CapsuleCollider directionCapsule) || request.intValue < 0 || request.intValue > 2)
                        throw Invalid("Capsule direction must be 0, 1, or 2.");
                    directionCapsule.direction = request.intValue;
                    break;
                case "convex":
                    if (!(collider is MeshCollider mesh)) throw NotSupported("convex is only valid for MeshCollider.");
                    mesh.convex = request.boolValue;
                    break;
                case "sharedMesh":
                    if (!(collider is MeshCollider meshCollider)) throw NotSupported("sharedMesh is only valid for MeshCollider.");
                    meshCollider.sharedMesh = LoadAllowedAsset<Mesh>(request.assetPath);
                    break;
                default: throw NotSupported("Collider property is not allow-listed.");
            }
        }

        private static void ToggleObject(Object target, ReviewRequest request)
        {
            GameObject gameObject = target as GameObject;
            if (gameObject != null)
            {
                Undo.RecordObject(gameObject, "Toggle object");
                gameObject.SetActive(request.boolValue);
                EditorUtility.SetDirty(gameObject);
                return;
            }
            Component component = target as Component;
            if (component is Camera camera) { Undo.RecordObject(camera, "Toggle camera"); camera.enabled = request.boolValue; }
            else if (component is Light light) { Undo.RecordObject(light, "Toggle light"); light.enabled = request.boolValue; }
            else if (component is AudioSource audioSource) { Undo.RecordObject(audioSource, "Toggle audio source"); audioSource.enabled = request.boolValue; }
            else if (component is Renderer renderer) { Undo.RecordObject(renderer, "Toggle renderer"); renderer.enabled = request.boolValue; }
            else if (component is Collider collider) { Undo.RecordObject(collider, "Toggle collider"); collider.enabled = request.boolValue; }
            else throw NotSupported("Component type is not allow-listed for toggling.");
            EditorUtility.SetDirty(component);
        }

        private static void ReplaceMaterial(Renderer renderer, ReviewRequest request)
        {
            Material replacement = LoadAllowedAsset<Material>(request.assetPath);
            Material[] materials = renderer.sharedMaterials;
            int index = request.intValue;
            if (index < 0 || index >= materials.Length) throw Invalid("Material slot is out of range.");
            Undo.RecordObject(renderer, "Replace material");
            materials[index] = replacement;
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }

        private static void ReplaceMesh(Object target, ReviewRequest request)
        {
            Mesh mesh = LoadAllowedAsset<Mesh>(request.assetPath);
            MeshFilter filter = ComponentFrom<MeshFilter>(target);
            SkinnedMeshRenderer skinned = ComponentFrom<SkinnedMeshRenderer>(target);
            if (filter != null)
            {
                Undo.RecordObject(filter, "Replace mesh");
                filter.sharedMesh = mesh;
                EditorUtility.SetDirty(filter);
            }
            else if (skinned != null)
            {
                Undo.RecordObject(skinned, "Replace mesh");
                skinned.sharedMesh = mesh;
                EditorUtility.SetDirty(skinned);
            }
            else throw Invalid("Target has neither MeshFilter nor SkinnedMeshRenderer.");
        }

        private static void AddComponent(GameObject gameObject, ReviewRequest request)
        {
            if (GetAllowedComponent(gameObject, request.componentType) != null)
                throw Invalid("The object already has that component; use update_component.");
            switch (request.componentType ?? string.Empty)
            {
                case "Camera": Undo.AddComponent<Camera>(gameObject); break;
                case "Light": Undo.AddComponent<Light>(gameObject); break;
                case "LODGroup": Undo.AddComponent<LODGroup>(gameObject); break;
                case "BoxCollider": Undo.AddComponent<BoxCollider>(gameObject); break;
                case "SphereCollider": Undo.AddComponent<SphereCollider>(gameObject); break;
                case "CapsuleCollider": Undo.AddComponent<CapsuleCollider>(gameObject); break;
                case "MeshCollider": Undo.AddComponent<MeshCollider>(gameObject); break;
                case "MeshFilter": Undo.AddComponent<MeshFilter>(gameObject); break;
                case "MeshRenderer": Undo.AddComponent<MeshRenderer>(gameObject); break;
                case "Rigidbody": Undo.AddComponent<Rigidbody>(gameObject); break;
                case "AudioSource": Undo.AddComponent<AudioSource>(gameObject); break;
                default: throw NotSupported("Component type is not allow-listed.");
            }
        }

        private static void UpdateComponent(GameObject gameObject, ReviewRequest request)
        {
            Component component = GetAllowedComponent(gameObject, request.componentType);
            if (component == null) throw NotFound("Allow-listed component was not found on target.");
            Undo.RecordObject(component, "Update component");
            switch (request.componentType ?? string.Empty)
            {
                case "Camera": SetCamera((Camera)component, request); return;
                case "Light": SetLight((Light)component, request); return;
                case "BoxCollider": case "SphereCollider": case "CapsuleCollider": case "MeshCollider":
                    ApplyColliderProperty((Collider)component, request); break;
                case "MeshFilter":
                    if (request.property != "sharedMesh") throw NotSupported("MeshFilter property is not allow-listed.");
                    ((MeshFilter)component).sharedMesh = LoadAllowedAsset<Mesh>(request.assetPath); break;
                case "MeshRenderer":
                    if (request.property != "enabled") throw NotSupported("MeshRenderer property is not allow-listed.");
                    ((MeshRenderer)component).enabled = request.boolValue; break;
                case "Rigidbody": ApplyRigidbody((Rigidbody)component, request); break;
                case "AudioSource": ApplyAudioSource((AudioSource)component, request); break;
                case "LODGroup":
                    if (request.property != "thresholds") throw NotSupported("LODGroup property is not allow-listed.");
                    SetLodThresholds((LODGroup)component, request); return;
                default: throw NotSupported("Component cannot be updated safely.");
            }
            EditorUtility.SetDirty(component);
        }

        private static void ApplyRigidbody(Rigidbody body, ReviewRequest request)
        {
            switch (request.property ?? string.Empty)
            {
                case "mass": body.mass = Positive(request.floatValue, "mass"); break;
                case "drag": body.linearDamping = NonNegative(request.floatValue, "drag"); break;
                case "angularDrag": body.angularDamping = NonNegative(request.floatValue, "angularDrag"); break;
                case "useGravity": body.useGravity = request.boolValue; break;
                case "isKinematic": body.isKinematic = request.boolValue; break;
                case "constraints": body.constraints = EnumValue<RigidbodyConstraints>(request.intValue, "constraints"); break;
                case "collisionDetectionMode": body.collisionDetectionMode = EnumValue<CollisionDetectionMode>(request.intValue, "collisionDetectionMode"); break;
                default: throw NotSupported("Rigidbody property is not allow-listed.");
            }
        }

        private static void ApplyAudioSource(AudioSource source, ReviewRequest request)
        {
            switch (request.property ?? string.Empty)
            {
                case "enabled": source.enabled = request.boolValue; break;
                case "playOnAwake": source.playOnAwake = request.boolValue; break;
                case "loop": source.loop = request.boolValue; break;
                case "volume": source.volume = InRange(request.floatValue, 0f, 1f, "volume"); break;
                case "pitch": source.pitch = InRange(request.floatValue, -3f, 3f, "pitch"); break;
                case "spatialBlend": source.spatialBlend = InRange(request.floatValue, 0f, 1f, "spatialBlend"); break;
                case "clip": source.clip = string.IsNullOrWhiteSpace(request.assetPath) ? null : LoadAllowedAsset<AudioClip>(request.assetPath); break;
                default: throw NotSupported("AudioSource property is not allow-listed.");
            }
        }

        private static Component GetAllowedComponent(GameObject gameObject, string type)
        {
            switch (type ?? string.Empty)
            {
                case "Camera": return gameObject.GetComponent<Camera>();
                case "Light": return gameObject.GetComponent<Light>();
                case "LODGroup": return gameObject.GetComponent<LODGroup>();
                case "BoxCollider": return gameObject.GetComponent<BoxCollider>();
                case "SphereCollider": return gameObject.GetComponent<SphereCollider>();
                case "CapsuleCollider": return gameObject.GetComponent<CapsuleCollider>();
                case "MeshCollider": return gameObject.GetComponent<MeshCollider>();
                case "MeshFilter": return gameObject.GetComponent<MeshFilter>();
                case "MeshRenderer": return gameObject.GetComponent<MeshRenderer>();
                case "Rigidbody": return gameObject.GetComponent<Rigidbody>();
                case "AudioSource": return gameObject.GetComponent<AudioSource>();
                default: throw NotSupported("Component type is not allow-listed.");
            }
        }

        private static void EditPrefab(ReviewRequest request)
        {
            string path = RequireAllowedAssetPath(request.assetPath, true);
            if (AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(GameObject)) throw Invalid("assetPath is not a prefab.");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) throw NotFound("Prefab could not be loaded.");
            try
            {
                GameObject target = ResolvePrefabChild(root, request.target);
                if (request.componentType == "Transform") SetTransform(target.transform, request);
                else if (request.stringValue == "add_component") AddComponent(target, request);
                else UpdateComponent(target, request);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject ResolvePrefabChild(GameObject root, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "." || path == root.name) return root;
            Transform found = FindByPath(root.transform, path.Trim('/'));
            if (found == null) throw NotFound("Prefab child path was not found.");
            return found.gameObject;
        }

        private static void UpdateModelImporter(ReviewRequest request)
        {
            string path = RequireAllowedAssetPath(request.assetPath, true);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw Invalid("Asset does not use ModelImporter.");
            switch (request.property ?? string.Empty)
            {
                case "globalScale": importer.globalScale = Positive(request.floatValue, "globalScale"); break;
                case "useFileScale": importer.useFileScale = request.boolValue; break;
                case "importBlendShapes": importer.importBlendShapes = request.boolValue; break;
                case "importVisibility": importer.importVisibility = request.boolValue; break;
                case "importCameras": importer.importCameras = request.boolValue; break;
                case "importLights": importer.importLights = request.boolValue; break;
                case "isReadable": importer.isReadable = request.boolValue; break;
                case "optimizeMeshPolygons": importer.optimizeMeshPolygons = request.boolValue; break;
                case "optimizeMeshVertices": importer.optimizeMeshVertices = request.boolValue; break;
                case "meshCompression": importer.meshCompression = EnumValue<ModelImporterMeshCompression>(request.intValue, "meshCompression"); break;
                case "importAnimation": importer.importAnimation = request.boolValue; break;
                case "materialImportMode": importer.materialImportMode = EnumValue<ModelImporterMaterialImportMode>(request.intValue, "materialImportMode"); break;
                default: throw NotSupported("ModelImporter property is not allow-listed.");
            }
            EditorUtility.SetDirty(importer);
            AssetDatabase.WriteImportSettingsIfDirty(path);
        }

        private static void ReimportAsset(ReviewRequest request)
        {
            string path = RequireAllowedAssetPath(request.assetPath, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static ReviewResponse RunAssetGate(ReviewRequest request, ReviewResponse response)
        {
            try
            {
                string gate = request.stringValue ?? string.Empty;
                if (gate == "aircraft-production")
                {
                    global::AeroSim.Editor.AircraftProductionValidator.ValidateOrThrow();
                    response.success = true;
                    response.status = "OK";
                    response.message = "Aircraft production gate passed.";
                    return response;
                }
                if (gate == "coast-art-budget")
                {
                    global::AeroSim.Editor.CoastArtBudgetReport report = global::AeroSim.Editor.CoastArtBudgetValidator.Validate();
                    if (!report.Passed) return Fail(response, "GATE_FAILED", report.Format());
                    response.success = true;
                    response.status = "OK";
                    response.message = report.Format();
                    return response;
                }
                if (gate.Length != 0) return Fail(response, "NOT_SUPPORTED", "Asset gate is not allow-listed.");

                string path = RequireAllowedAssetPath(request.assetPath, true);
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) return Fail(response, "NOT_FOUND", "Asset was not found.");
                string[] labels = AssetDatabase.GetLabels(asset);
                if (labels.Any(label => string.Equals(label, "AeroSimRejected", StringComparison.OrdinalIgnoreCase)))
                    return Fail(response, "GATE_FAILED", "Asset is marked AeroSimRejected.");
                response.success = true;
                response.status = "OK";
                response.message = "Asset gate passed: path, existence, and rejection-label checks succeeded.";
                return response;
            }
            catch (MutationException ex) { return Fail(response, ex.Status, ex.Message); }
            catch (Exception ex) { return Fail(response, "GATE_FAILED", ex.Message); }
        }

        private static ReviewResponse SaveScene(ReviewRequest request, ReviewResponse response)
        {
            Scene scene;
            if (!string.IsNullOrWhiteSpace(request.assetPath))
            {
                string path = RequireAllowedAssetPath(request.assetPath, true);
                scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) return Fail(response, "NOT_FOUND", "Allowed scene is not loaded.");
            }
            else scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return Fail(response, "NOT_FOUND", "No loaded scene is selected.");
            if (string.IsNullOrEmpty(scene.path)) return Fail(response, "NOT_SUPPORTED", "Untitled scenes cannot be saved by this endpoint.");
            try { RequireAllowedAssetPath(scene.path, true); }
            catch (MutationException ex) { return Fail(response, ex.Status, ex.Message); }
            if (!EditorSceneManager.SaveScene(scene)) return Fail(response, "FAILED", "Unity did not save the scene.");
            response.success = true; response.status = "OK"; response.message = "Scene saved."; return response;
        }

        private static ReviewResponse SavePrefab(ReviewRequest request, ReviewResponse response)
        {
            try
            {
                string path = RequireAllowedAssetPath(request.assetPath, true);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
                    return Fail(response, "INVALID_REQUEST", "assetPath is not a prefab.");
                AssetDatabase.SaveAssetIfDirty(prefab);
                response.success = true; response.status = "OK"; response.message = "Prefab asset saved."; return response;
            }
            catch (MutationException ex) { return Fail(response, ex.Status, ex.Message); }
        }

        private static ReviewResponse UndoChange(ReviewRequest request, ReviewResponse response, string runDirectory)
        {
            lock (Sync)
            {
                if (Applied.Count == 0) return Fail(response, "NOT_FOUND", "There is no AeroSim mutation to undo in this editor session.");
                AppliedChange latest = Applied[Applied.Count - 1];
                if (!string.IsNullOrWhiteSpace(request.transactionId) &&
                    !string.Equals(request.transactionId, latest.record.transactionId, StringComparison.Ordinal))
                    return Fail(response, "NOT_SUPPORTED", "Only the most recent change can be undone safely.");
                if (latest.assetBackup != null) RestoreAssetBackup(latest.assetBackup);
                else
                {
                    if (Undo.GetCurrentGroup() != latest.record.undoGroup + 1)
                        return Fail(response, "UNDO_CONFLICT", "Unity Undo history changed after this AeroSim mutation; refusing to undo unrelated editor work.");
                    Undo.PerformUndo();
                }
                latest.record.rolledBack = true;
                latest.record.verified = true;
                PersistChange(latest.record, runDirectory);
                Applied.RemoveAt(Applied.Count - 1);
                response.success = true; response.status = "OK"; response.message = "Most recent AeroSim change undone.";
                response.transactionId = latest.record.transactionId;
                return response;
            }
        }

        private static ReviewResponse RollbackTransaction(ReviewRequest request, ReviewResponse response, string runDirectory)
        {
            if (string.IsNullOrWhiteSpace(request.transactionId)) return Fail(response, "INVALID_REQUEST", "transactionId is required.");
            lock (Sync)
            {
                if (Applied.Count == 0 || !string.Equals(Applied[Applied.Count - 1].record.transactionId, request.transactionId, StringComparison.Ordinal))
                    return Fail(response, "NOT_SUPPORTED", "Only the latest contiguous transaction can be rolled back safely.");
                AppliedChange latestTransactionChange = Applied[Applied.Count - 1];
                if (latestTransactionChange.assetBackup == null && Undo.GetCurrentGroup() != latestTransactionChange.record.undoGroup + 1)
                    return Fail(response, "UNDO_CONFLICT", "Unity Undo history changed after this AeroSim transaction; refusing to roll back unrelated editor work.");
                int count = 0;
                while (Applied.Count > 0)
                {
                    AppliedChange latest = Applied[Applied.Count - 1];
                    if (!string.Equals(latest.record.transactionId, request.transactionId, StringComparison.Ordinal)) break;
                    if (latest.assetBackup != null) RestoreAssetBackup(latest.assetBackup);
                    else Undo.PerformUndo();
                    latest.record.rolledBack = true;
                    latest.record.verified = true;
                    PersistChange(latest.record, runDirectory);
                    Applied.RemoveAt(Applied.Count - 1);
                    count++;
                }
                response.success = true; response.status = "OK";
                response.message = "Rolled back " + count.ToString(CultureInfo.InvariantCulture) + " change(s).";
                response.transactionId = request.transactionId;
                return response;
            }
        }

        private static AssetBackup CaptureAssetBackup(ReviewRequest request)
        {
            if (request.tool != "edit_prefab" && request.tool != "update_model_importer" && request.tool != "reimport_asset")
                return null;
            string assetPath = RequireAllowedAssetPath(request.assetPath, true);
            string absolute = AbsoluteAssetPath(assetPath);
            if (!File.Exists(absolute)) throw NotSupported("Folder mutation is not supported.");
            string meta = absolute + ".meta";
            return new AssetBackup
            {
                assetPath = assetPath,
                assetBytes = File.ReadAllBytes(absolute),
                metaBytes = File.Exists(meta) ? File.ReadAllBytes(meta) : null
            };
        }

        private static void RestoreAssetBackup(AssetBackup backup)
        {
            if (backup == null) return;
            string absolute = AbsoluteAssetPath(backup.assetPath);
            File.WriteAllBytes(absolute, backup.assetBytes);
            if (backup.metaBytes != null) File.WriteAllBytes(absolute + ".meta", backup.metaBytes);
            AssetDatabase.ImportAsset(backup.assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string AbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolute = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string allowedRoot = Path.GetFullPath(Path.Combine(projectRoot, AssetRoot));
            if (!IsUnderDirectory(absolute, allowedRoot) && !string.Equals(absolute, allowedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
                throw Forbidden("Resolved asset path escaped Assets/AeroSim.");
            return absolute;
        }

        private static string Snapshot(Object target)
        {
            if (target == null) return string.Empty;
            string assetPath = AssetDatabase.GetAssetPath(target);
            var state = new ObjectState
            {
                globalObjectId = TryGlobalId(target),
                hierarchyPath = target is GameObject go ? HierarchyPath(go.transform) : target is Component component ? HierarchyPath(component.transform) : string.Empty,
                assetPath = assetPath,
                type = target.GetType().FullName,
                json = EditorJsonUtility.ToJson(target, false),
                componentJson = SnapshotComponents(target),
                dependencyHash = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.GetAssetDependencyHash(assetPath).ToString()
            };
            return JsonUtility.ToJson(state, false);
        }

        private static string[] SnapshotComponents(Object target)
        {
            GameObject gameObject = target as GameObject;
            if (gameObject == null && target is Component component) gameObject = component.gameObject;
            if (gameObject == null) return Array.Empty<string>();
            return gameObject.GetComponents<Component>()
                .Where(value => value != null)
                .Select(value => value.GetType().FullName + ":" + EditorJsonUtility.ToJson(value, false))
                .ToArray();
        }

        private static string TryGlobalId(Object target)
        {
            try { return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString(); }
            catch { return string.Empty; }
        }

        private static void PersistChange(ChangeRecord change, string runDirectory)
        {
            if (string.IsNullOrWhiteSpace(runDirectory)) return;
            string root = Path.GetFullPath(runDirectory);
            Directory.CreateDirectory(root);
            string changes = Path.Combine(root, "changes");
            Directory.CreateDirectory(changes);
            string id = SafeFileName(string.IsNullOrWhiteSpace(change.requestId) ? Guid.NewGuid().ToString("N") : change.requestId);
            string destination = Path.GetFullPath(Path.Combine(changes, id + ".json"));
            if (!IsUnderDirectory(destination, root)) throw Invalid("Unsafe runDirectory or request id.");
            string temporary = destination + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(change, true));
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }

        private static string SafeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = value.Where(c => !invalid.Contains(c) && c != '/' && c != '\\').Take(96).ToArray();
            return chars.Length == 0 ? "change" : new string(chars);
        }

        private static bool IsUnderDirectory(string path, string root)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.Ordinal);
        }

        private static string RequireAllowedAssetPath(string path, bool mustExist)
        {
            path = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (!path.StartsWith(AssetRoot, StringComparison.Ordinal) || path.Contains("/../") || path.EndsWith("/..", StringComparison.Ordinal))
                throw Forbidden("Only assets below Assets/AeroSim are allowed.");
            if (mustExist && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))) throw NotFound("Allowed asset path does not exist.");
            return path;
        }

        private static Object LoadAllowedAsset(string path, Type type, bool mustExist)
        {
            path = RequireAllowedAssetPath(path, mustExist);
            Object asset = AssetDatabase.LoadAssetAtPath(path, type);
            if (asset == null && mustExist) throw NotFound("Asset is absent or has the wrong type.");
            return asset;
        }

        private static T LoadAllowedAsset<T>(string path) where T : Object
        {
            return (T)LoadAllowedAsset(path, typeof(T), true);
        }

        private static void EnsureObjectAllowed(Object value)
        {
            string path = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(path))
            {
                RequireAllowedAssetPath(path, true);
                return;
            }
            Component component = value as Component;
            GameObject gameObject = value as GameObject ?? (component != null ? component.gameObject : null);
            if (gameObject == null) return;
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
                throw Forbidden("Object is neither in a loaded scene nor an allowed asset.");
            RequireAllowedAssetPath(gameObject.scene.path, true);
        }

        private static GameObject RequireGameObject(Object target)
        {
            if (target is GameObject gameObject) return gameObject;
            if (target is Component component) return component.gameObject;
            throw Invalid("Target must be a GameObject or Component.");
        }

        private static T RequireComponent<T>(Object target) where T : Component
        {
            T component = ComponentFrom<T>(target);
            if (component == null) throw NotFound(typeof(T).Name + " was not found on target.");
            return component;
        }

        private static T ComponentFrom<T>(Object target) where T : Component
        {
            if (target is T direct) return direct;
            if (target is GameObject gameObject) return gameObject.GetComponent<T>();
            if (target is Component component) return component.GetComponent<T>();
            return null;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(gameObject);
        }

        private static float Positive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) throw Invalid(name + " must be finite and positive.");
            return value;
        }

        private static float NonNegative(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) throw Invalid(name + " must be finite and non-negative.");
            return value;
        }

        private static float InRange(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw Invalid(name + " must be between " + min + " and " + max + ".");
            return value;
        }

        private static Vector3 PositiveVector(Vector3 value, string name)
        {
            if (value.x <= 0f || value.y <= 0f || value.z <= 0f || float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z))
                throw Invalid(name + " components must be positive and finite.");
            return value;
        }

        private static T EnumValue<T>(int value, string name) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value)) throw Invalid(name + " enum value is invalid.");
            return (T)Enum.ToObject(typeof(T), value);
        }

        private static void RequireFloatCount(float[] values, int count, string name)
        {
            if (values == null || values.Length != count) throw Invalid(name + " requires exactly " + count + " values.");
        }

        private static string EffectiveTransactionId(ReviewRequest request)
        {
            return string.IsNullOrWhiteSpace(request.transactionId) ? "request-" + (request.id ?? Guid.NewGuid().ToString("N")) : request.transactionId;
        }

        private static ReviewResponse NewResponse(ReviewRequest request)
        {
            return new ReviewResponse
            {
                requestId = request != null ? request.id : null,
                tool = request != null ? request.tool : null,
                transactionId = request != null ? request.transactionId : null,
                success = false,
                status = "FAILED",
                message = string.Empty
            };
        }

        private static ReviewResponse Fail(ReviewResponse response, string status, string message)
        {
            response.success = false; response.status = status; response.message = message; return response;
        }

        private sealed class MutationException : Exception
        {
            public readonly string Status;
            public MutationException(string status, string message) : base(message) { Status = status; }
        }

        private static MutationException Invalid(string message) { return new MutationException("INVALID_REQUEST", message); }
        private static MutationException Forbidden(string message) { return new MutationException("FORBIDDEN", message); }
        private static MutationException NotFound(string message) { return new MutationException("NOT_FOUND", message); }
        private static MutationException NotSupported(string message) { return new MutationException("NOT_SUPPORTED", message); }
    }
}
#endif
