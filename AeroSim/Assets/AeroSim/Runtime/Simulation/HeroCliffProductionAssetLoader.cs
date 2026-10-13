using System;
using System.Linq;
using AeroSim.Domain;
using UnityEngine;

namespace AeroSim.Simulation
{
    public sealed class HeroCliffLoadResult
    {
        public ProductionAssetLoadState state;
        public string message;
        public GameObject instance;
        public bool Succeeded => state == ProductionAssetLoadState.Ready && instance != null;
    }

    /// <summary>P11 fail-closed loader. It validates the authored prefab and never creates replacement geometry.</summary>
    public static class HeroCliffProductionAssetLoader
    {
        public const string ResourcePath = "CoastProduction/P11/HeroCliff";

        public static HeroCliffLoadResult TryAssemble(Transform parent, EnvironmentState state)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) return Fail(ProductionAssetLoadState.AssetMissing, "P11 prefab missing; caller may conservatively retain P01 fallback.");
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = "Hero Cliff P11 · GENERATED_UNREVIEWED";
            if (!Validate(instance, out string report))
            {
                UnityEngine.Object.Destroy(instance);
                return Fail(ProductionAssetLoadState.ValidationFailed, report);
            }
            ApplyEnvironment(instance, state);
            return new HeroCliffLoadResult { state = ProductionAssetLoadState.Ready, message = report, instance = instance };
        }

        public static bool Validate(GameObject root, out string report)
        {
            if (root == null) { report = "P11_REJECTED: root is null."; return false; }
            var errors = new System.Collections.Generic.List<string>();
            var metadata = root.GetComponent<HeroCliffP11Metadata>();
            if (metadata == null || metadata.sourceSeed != 110412) errors.Add("reproducibility metadata missing");
            var group = root.GetComponent<LODGroup>();
            if (group == null || group.GetLODs().Length != 5) errors.Add("LOD0/1/2/3/Cull contract missing");
            else if (group.GetLODs()[4].renderers.Any(r => r != null)) errors.Add("Cull must have no renderers");
            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0 || colliders.Length > 16) errors.Add("compound collision count outside 1..16");
            if (colliders.Any(c => c is MeshCollider || !(c is BoxCollider || c is SphereCollider || c is CapsuleCollider))) errors.Add("collision must use primitives; MeshCollider forbidden");
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 8) errors.Add("exactly eight cliff/talus LOD renderers required");
            if (group != null && group.GetLODs().Take(4).Any(lod => lod.renderers.Length != 2)) errors.Add("each visual LOD must pair cliff and talus renderers");
            if (renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count() != 1) errors.Add("P11 must use one shared material");
            report = errors.Count == 0 ? "P11_VALIDATED_GENERATED_UNREVIEWED" : "P11_REJECTED: " + string.Join("; ", errors);
            return errors.Count == 0;
        }

        public static void ApplyEnvironment(GameObject root, EnvironmentState state)
        {
            if (root == null || state == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                block.SetFloat("_Wetness", state.wetness); block.SetFloat("_Precipitation", state.precipitation);
                block.SetFloat("_WaterLevel", state.waterLevel);
                block.SetColor("_MaterialId", new Color(0.12f, 0.72f, 0.92f, 1f));
                Color lodColor = renderer.name.Contains("LOD0") ? new Color(0.9f, 0.18f, 0.12f, 1f) :
                    renderer.name.Contains("LOD1") ? new Color(0.95f, 0.68f, 0.12f, 1f) :
                    renderer.name.Contains("LOD2") ? new Color(0.18f, 0.82f, 0.28f, 1f) : new Color(0.16f, 0.42f, 0.95f, 1f);
                block.SetColor("_LodColor", lodColor);
                renderer.SetPropertyBlock(block);
            }
        }

        static HeroCliffLoadResult Fail(ProductionAssetLoadState state, string message) => new() { state = state, message = message };
    }
}
