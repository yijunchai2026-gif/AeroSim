using System;
using System.Collections.Generic;
using System.Linq;
using AeroSim.Domain;
using UnityEngine;

namespace AeroSim.Simulation
{
    public enum ProductionAssetLoadState
    {
        ContentNotDelivered,
        ContractRejected,
        ProviderUnavailable,
        AssetMissing,
        ValidationFailed,
        Ready
    }

    public sealed class ProductionAssetLoadResult
    {
        public ProductionAssetLoadState State;
        public string RuntimeId;
        public string Message;
        public string PrefabAssetPath;
        public string ResourcePath;
        public string Address;
        public string ContentStatus;
        public GameObject Instance;
        public ProductionAssetBindings Bindings;
        public bool Succeeded => State == ProductionAssetLoadState.Ready && Instance != null;
    }

    public sealed class ProductionAssetBindings
    {
        public readonly Dictionary<string, Transform> Sockets = new(StringComparer.OrdinalIgnoreCase);
        public LODGroup LodGroup;
        public Collider[] Colliders = Array.Empty<Collider>();
        public Material[] Materials = Array.Empty<Material>();
    }

    /// <summary>Provider seam for a future Addressables-backed loader.</summary>
    public interface IProductionAssetProvider
    {
        bool CanLoad(string address, string resourcePath);
        GameObject Load(string address, string resourcePath, out string failureReason);
    }

    public sealed class ResourcesProductionAssetProvider : IProductionAssetProvider
    {
        public bool CanLoad(string address, string resourcePath) => !string.IsNullOrWhiteSpace(resourcePath);

        public GameObject Load(string address, string resourcePath, out string failureReason)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            failureReason = prefab == null ? "Resource prefab was not found at " + resourcePath : string.Empty;
            return prefab;
        }
    }

    /// <summary>
    /// RD-06 formal production-asset seam. This class never creates or falls back to procedural geometry.
    /// Only an RD-02 ProductionApproved + ProductionReady record is eligible for loading.
    /// </summary>
    public static class ProductionAssetLoader
    {
        public const string ContentNotDelivered = "CONTENT_NOT_DELIVERED";
        public const string PrefabRoot = "Assets/AeroSim/Resources/AircraftProduction/";
        public const string ResourceRoot = "AircraftProduction/";
        public const string AddressRoot = "aerosim/aircraft/production/";

        private static readonly List<IProductionAssetProvider> Providers = new()
        {
            new ResourcesProductionAssetProvider()
        };

        public static void RegisterProvider(IProductionAssetProvider provider, bool first = true)
        {
            if (provider == null || Providers.Contains(provider)) return;
            if (first) Providers.Insert(0, provider); else Providers.Add(provider);
        }

        public static ProductionAssetLoadResult Inspect(string runtimeId)
        {
            var result = Rd02ProductionManifestAdapter.Resolve(runtimeId);
            var asset = AircraftAssetContracts.Resolve(runtimeId);
            if (asset == null)
                return Fail(result, ProductionAssetLoadState.ContractRejected,
                    "RD-02 manifest has no record for runtimeId '" + runtimeId + "'.");

            if (!string.Equals(result.ContentStatus, "DELIVERED", StringComparison.Ordinal))
                return Fail(result, ProductionAssetLoadState.ContentNotDelivered,
                    ContentNotDelivered + " · formal source, textures, and prefab are not declared delivered.");

            if (!AircraftAssetStateMachine.CanClaimModelIdentity(asset.IdentityState, asset.QualityState))
                return Fail(result, ProductionAssetLoadState.ContentNotDelivered,
                    ContentNotDelivered + " · " + runtimeId + " is " + asset.QualityState + " / " + asset.IdentityState + ".");

            if (!AircraftAssetContracts.Validate(out string manifestReport))
                return Fail(result, ProductionAssetLoadState.ContractRejected, manifestReport);

            if (string.IsNullOrWhiteSpace(asset.sourcePath) ||
                !asset.sourcePath.StartsWith(AircraftAssetContracts.ProductionRoot, StringComparison.Ordinal) ||
                (asset.sourceFormat != "fbx" && asset.sourceFormat != "glb") ||
                string.IsNullOrWhiteSpace(asset.sourceSha256) || asset.sourceSha256.Length != 64)
                return Fail(result, ProductionAssetLoadState.ContractRejected,
                    "RD-02 formal source path, FBX/GLB format, and SHA-256 are required.");

            if (string.IsNullOrWhiteSpace(result.ResourcePath) && string.IsNullOrWhiteSpace(result.Address))
                return Fail(result, ProductionAssetLoadState.ContractRejected,
                    "No production prefab resource or address is declared/resolvable.");

            result.State = ProductionAssetLoadState.Ready;
            result.Message = "RD-06 contract eligible; provider resolution required.";
            return result;
        }

        public static ProductionAssetLoadResult TryAssemble(string runtimeId, Transform parent)
        {
            var result = Inspect(runtimeId);
            if (result.State != ProductionAssetLoadState.Ready) return result;

            var provider = Providers.FirstOrDefault(value => value.CanLoad(result.Address, result.ResourcePath));
            if (provider == null)
                return Fail(result, ProductionAssetLoadState.ProviderUnavailable,
                    ContentNotDelivered + " · no provider accepts the production address/resource.");

            var prefab = provider.Load(result.Address, result.ResourcePath, out string providerFailure);
            if (prefab == null)
                return Fail(result, ProductionAssetLoadState.AssetMissing,
                    ContentNotDelivered + " · " + providerFailure);

            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = "Production aircraft · " + runtimeId;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (!ProductionAssetAssemblyValidator.TryBind(instance, AircraftAssetContracts.Resolve(runtimeId),
                    out var bindings, out string report))
            {
                UnityEngine.Object.Destroy(instance);
                return Fail(result, ProductionAssetLoadState.ValidationFailed, report);
            }

            result.Instance = instance;
            result.Bindings = bindings;
            result.State = ProductionAssetLoadState.Ready;
            result.Message = "RD-06_READY · validated formal prefab assembled.";
            return result;
        }

        private static ProductionAssetLoadResult Fail(ProductionAssetLoadResult result,
            ProductionAssetLoadState state, string message)
        {
            result.State = state;
            result.Message = message;
            return result;
        }
    }

    /// <summary>
    /// Minimal forward-compatible reader for optional RD-06 path fields. JsonUtility ignores these fields in
    /// the existing RD-02 DTO, so no shared manifest contract or scene/camera integration must change.
    /// </summary>
    internal static class Rd02ProductionManifestAdapter
    {
        [Serializable]
        private sealed class Document { public Entry[] assets; }

        [Serializable]
        private sealed class Entry
        {
            public string runtimeId;
            public string productionContentStatus;
            public string productionPrefabPath;
            public string productionResourcePath;
            public string productionAddress;
        }

        internal static ProductionAssetLoadResult Resolve(string runtimeId)
        {
            string safeId = string.IsNullOrWhiteSpace(runtimeId) ? "missing-runtime-id" : runtimeId.Trim();
            var result = new ProductionAssetLoadResult
            {
                RuntimeId = safeId,
                ContentStatus = ProductionAssetLoader.ContentNotDelivered,
                PrefabAssetPath = ProductionAssetLoader.PrefabRoot + safeId + "/Aircraft.prefab",
                ResourcePath = ProductionAssetLoader.ResourceRoot + safeId + "/Aircraft",
                Address = ProductionAssetLoader.AddressRoot + safeId
            };

            var text = Resources.Load<TextAsset>(AircraftAssetContracts.ResourcePath);
            var document = text == null ? null : JsonUtility.FromJson<Document>(text.text);
            var entry = document?.assets?.FirstOrDefault(value =>
                string.Equals(value.runtimeId, safeId, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return result;
            if (!string.IsNullOrWhiteSpace(entry.productionContentStatus)) result.ContentStatus = entry.productionContentStatus;
            if (!string.IsNullOrWhiteSpace(entry.productionPrefabPath)) result.PrefabAssetPath = entry.productionPrefabPath;
            if (!string.IsNullOrWhiteSpace(entry.productionResourcePath)) result.ResourcePath = entry.productionResourcePath;
            if (!string.IsNullOrWhiteSpace(entry.productionAddress)) result.Address = entry.productionAddress;
            return result;
        }
    }

    public static class ProductionAssetAssemblyValidator
    {
        public static bool TryBind(GameObject root, AircraftAssetRecord asset,
            out ProductionAssetBindings bindings, out string report)
        {
            bindings = new ProductionAssetBindings();
            var errors = new List<string>();
            if (root == null) errors.Add("Prefab root is null.");
            if (asset == null) errors.Add("RD-02 manifest record is null.");
            if (errors.Count > 0) { report = Reject(errors); return false; }

            if (root.transform.localPosition != Vector3.zero ||
                root.transform.localRotation != Quaternion.identity || root.transform.localScale != Vector3.one)
                errors.Add("Root TRS must be identity after assembly.");

            foreach (var socket in asset.cameraSockets ?? Array.Empty<CameraSocketContract>())
            {
                var node = FindByName(root.transform, socket.name);
                if (node == null) errors.Add("Missing socket " + socket.id + " (" + socket.name + ").");
                else bindings.Sockets[socket.id] = node;
            }
            if (!bindings.Sockets.ContainsKey("flight-camera") || !bindings.Sockets.ContainsKey("preview-focus"))
                errors.Add("flight-camera and preview-focus bindings are mandatory.");

            bindings.LodGroup = root.GetComponentInChildren<LODGroup>(true);
            ValidateLods(bindings.LodGroup, asset, errors);
            bindings.Colliders = root.GetComponentsInChildren<Collider>(true);
            ValidateColliders(bindings.Colliders, asset, errors);
            bindings.Materials = ValidateMaterials(root, asset, errors);

            report = errors.Count == 0 ? "RD-06 assembly binding PASS." : Reject(errors);
            return errors.Count == 0;
        }

        private static void ValidateLods(LODGroup group, AircraftAssetRecord asset, List<string> errors)
        {
            if (group == null) { errors.Add("Missing LODGroup."); return; }
            var lods = group.GetLODs();
            if (lods.Length != 5) { errors.Add("LODGroup must contain LOD0/1/2/3/Cull."); return; }
            if (asset.lod?.maximumTriangles == null || asset.lod.maximumTriangles.Length != 4)
            { errors.Add("RD-02 LOD triangle budgets are incomplete."); return; }

            for (int i = 0; i < 4; i++)
            {
                int triangles = lods[i].renderers.Where(value => value != null).Sum(TriangleCount);
                if (triangles > asset.lod.maximumTriangles[i])
                    errors.Add("LOD" + i + " exceeds its RD-02 triangle budget.");
            }
            if (lods[4].renderers.Any(value => value != null)) errors.Add("Cull LOD must contain no renderers.");
        }

        private static void ValidateColliders(Collider[] colliders, AircraftAssetRecord asset, List<string> errors)
        {
            if (asset.collision == null) { errors.Add("RD-02 collision contract is missing."); return; }
            if (colliders.Length < asset.collision.minimumPrimitives || colliders.Length > asset.collision.maximumPrimitives)
                errors.Add("Collider count is outside the RD-02 range.");
            if (colliders.Any(value => !(value is BoxCollider || value is SphereCollider || value is CapsuleCollider)))
                errors.Add("Only Box/Sphere/Capsule production colliders are accepted.");
            if (colliders.Any(value => value.GetComponent<Renderer>() != null))
                errors.Add("Collider nodes must not contain visible renderers.");
        }

        private static Material[] ValidateMaterials(GameObject root, AircraftAssetRecord asset, List<string> errors)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var materials = renderers.SelectMany(value => value.sharedMaterials).Where(value => value != null).Distinct().ToArray();
            if (renderers.Length == 0 || materials.Length == 0) errors.Add("Production prefab has no bound visible materials.");
            string requiredShader = string.IsNullOrWhiteSpace(asset.materials?.shader) ? "Standard" : asset.materials.shader;
            if (renderers.Any(value => value.sharedMaterials.Length == 0 || value.sharedMaterials.Any(material => material == null)))
                errors.Add("Every renderer slot must bind a material.");
            if (materials.Any(value => value.shader == null || value.shader.name != requiredShader))
                errors.Add("All production materials must use RD-02 shader " + requiredShader + ".");
            return materials;
        }

        private static Transform FindByName(Transform root, string name)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (node.name == name) return node;
            return null;
        }

        private static int TriangleCount(Renderer renderer)
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skin
                ? skin.sharedMesh
                : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) return 0;
            int count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) count += (int)mesh.GetIndexCount(i) / 3;
            return count;
        }

        private static string Reject(List<string> errors) =>
            "RD-06_ASSEMBLY_REJECTED\n- " + string.Join("\n- ", errors);
    }
}
