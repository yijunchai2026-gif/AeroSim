#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AeroSim.Editor
{
    /// <summary>
    /// P10 static-only Coast art budget gate. It inspects importers and asset files and never opens,
    /// saves, or mutates a scene. Use ValidateFromCommandLine with -executeMethod in CI.
    /// </summary>
    public static class CoastArtBudgetValidator
    {
        internal const string ManifestPath = "Assets/AeroSim/Art/CoastProduction/P10/coast-art-budget-v1.json";
        private const string RuntimeCatalogPath = "Assets/AeroSim/Resources/CoastProduction/CoastProductionRuntimeCatalog.asset";
        private static readonly string[] ContractCategories =
        {
            "textureResolution", "textureMemory", "mipStreaming", "compression", "materialCount", "shaderKeyword",
            "drawCall", "lod", "billboard", "collision", "qualityDowngrade"
        };

        [MenuItem("AeroSim/P10/Validate Coast Art Budget (Static Only)")]
        public static void ValidateMenu()
        {
            CoastArtBudgetReport report = Validate();
            if (!report.Passed) throw new BuildFailedException(report.Format());
            Debug.Log(report.Format());
        }

        public static void ValidateFromCommandLine()
        {
            CoastArtBudgetReport report = Validate();
            Debug.Log(report.Format());
            if (!report.Passed) throw new BuildFailedException(report.Format());
        }

        internal static CoastArtBudgetReport Validate()
        {
            var report = new CoastArtBudgetReport();
            CoastArtBudgetManifest manifest = LoadManifest(report);
            if (manifest == null) return report;

            ValidateManifest(manifest, report);
            string[] assetPaths = ResolveAuditPaths(manifest, report);
            ValidateTextures(manifest, assetPaths, report);
            ValidateShaders(manifest, assetPaths, report);
            ValidateGameObjectAssets(manifest, assetPaths, report);
            ValidateGeneratedSources(manifest, assetPaths, report);
            ValidateQualityDowngrade(manifest, report);
            return report;
        }

        private static CoastArtBudgetManifest LoadManifest(CoastArtBudgetReport report)
        {
            if (!File.Exists(ManifestPath))
            {
                report.Error("P10-MANIFEST-MISSING", ManifestPath);
                return null;
            }
            try
            {
                var manifest = JsonUtility.FromJson<CoastArtBudgetManifest>(File.ReadAllText(ManifestPath));
                if (manifest == null) report.Error("P10-MANIFEST-INVALID", "Manifest deserialized to null.");
                return manifest;
            }
            catch (Exception exception)
            {
                report.Error("P10-MANIFEST-INVALID", exception.Message);
                return null;
            }
        }

        private static void ValidateManifest(CoastArtBudgetManifest manifest, CoastArtBudgetReport report)
        {
            if (manifest.schemaVersion != "aerosim.coast-art-budget/v1") report.Error("P10-SCHEMA", "Unsupported schemaVersion.");
            if (manifest.workPackage != "P10") report.Error("P10-WORK-PACKAGE", "workPackage must be P10.");
            if (manifest.validationMode != "STATIC_ASSET_ONLY") report.Error("P10-MODE", "validationMode must remain STATIC_ASSET_ONLY.");
            if (manifest.manifestStatus != "CAPTURED_UNREVIEWED" || manifest.defaultGeneratedSourceStatus != "CAPTURED_UNREVIEWED")
                report.Error("P10-REVIEW-STATE", "Manifest and generated-source default must be CAPTURED_UNREVIEWED.");
            if (manifest.automaticProductionReady) report.Error("P10-AUTO-PRODUCTION", "automaticProductionReady must be false.");
            if (manifest.forbiddenAutomaticStates == null || !manifest.forbiddenAutomaticStates.Contains("ProductionReady"))
                report.Error("P10-AUTO-PRODUCTION", "ProductionReady must be an explicitly forbidden automatic state.");

            string[] packageIds = manifest.workPackages == null ? Array.Empty<string>() : manifest.workPackages.Select(value => value.id).ToArray();
            string[] expectedPackages = Enumerable.Range(1, 9).Select(value => "P" + value.ToString("00")).Concat(new[] { "P11" }).ToArray();
            if (!packageIds.OrderBy(value => value).SequenceEqual(expectedPackages.OrderBy(value => value)))
                report.Error("P10-P01-P09-P11-SCOPE", "Exactly one audit declaration is required for P01-P09 and P11.");

            string[] categories = manifest.contracts == null ? Array.Empty<string>() : manifest.contracts.Select(value => value.category).ToArray();
            if (categories.Length != 11 || categories.Distinct(StringComparer.Ordinal).Count() != 11 || ContractCategories.Any(value => !categories.Contains(value)))
                report.Error("P10-CONTRACT-SET", "The manifest must define the eleven required budget contracts exactly once.");
            if (manifest.contracts != null && manifest.contracts.Any(value => !value.required || string.IsNullOrWhiteSpace(value.id)))
                report.Error("P10-CONTRACT-REQUIRED", "Every contract requires an id and required=true.");
            if (manifest.contracts != null && manifest.contracts.Where(value => value.category == "mipStreaming" || value.category == "compression").Any(value => value.runtimeUsedGeneratedSourceExempt))
                report.Error("P10-RUNTIME-SOURCE-EXEMPT", "Runtime-used generated source textures cannot be exempt from mip, streaming, or compression checks.");
        }

        private static string[] ResolveAuditPaths(CoastArtBudgetManifest manifest, CoastArtBudgetReport report)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoastWorkPackage package in manifest.workPackages ?? Array.Empty<CoastWorkPackage>())
            {
                if (package.assetRoots == null || package.assetRoots.Length == 0)
                {
                    report.Error("P10-EMPTY-SCOPE", package.id + " has no asset roots.");
                    continue;
                }
                foreach (string root in package.assetRoots)
                {
                    if (File.Exists(root)) paths.Add(root.Replace('\\', '/'));
                    else if (Directory.Exists(root))
                    {
                        foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { root }))
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guid);
                            if (!AssetDatabase.IsValidFolder(path)) paths.Add(path);
                        }
                    }
                    else report.Error("P10-MISSING-ROOT", package.id + ": " + root);
                }
            }
            report.auditedAssets = paths.Count;
            return paths.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static void ValidateTextures(CoastArtBudgetManifest manifest, IEnumerable<string> paths, CoastArtBudgetReport report)
        {
            CoastBudgetContract resolution = Contract(manifest, "textureResolution");
            CoastBudgetContract memory = Contract(manifest, "textureMemory");
            CoastBudgetContract mip = Contract(manifest, "mipStreaming");
            CoastBudgetContract compression = Contract(manifest, "compression");
            double p11EstimatedTextureMb = 0;
            var runtimeDependencies = new HashSet<string>(AssetDatabase.GetDependencies(RuntimeCatalogPath, true), StringComparer.Ordinal);
            foreach (string path in paths.Where(IsTexture))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { report.Error("CAB-01", path + " has no TextureImporter."); continue; }
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                if (path.Contains("/P11/")) p11EstimatedTextureMb += width * (double)height * 4.0 / 3.0 / (1024.0 * 1024.0); // conservative BC7/ASTC 8bpp plus full mip chain
                if (Mathf.Max(width, height) > resolution.maxSourceDimension) report.Error("CAB-01", path + " source exceeds " + resolution.maxSourceDimension + ".");
                TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings("iPhone");
                if (standalone.overridden && standalone.maxTextureSize > resolution.maxStandaloneDimension) report.Error("CAB-01", path + " Standalone max size exceeds contract.");
                if (ios.overridden && ios.maxTextureSize > resolution.maxMobileDimension) report.Error("CAB-01", path + " iOS max size exceeds contract.");

                bool generated = IsUnderAny(path, manifest.generatedSourceRoots);
                bool runtimeUsed = runtimeDependencies.Contains(path);
                bool mipExempt = generated && !runtimeUsed && mip.generatedSourceExempt;
                bool compressionExempt = generated && !runtimeUsed && compression.generatedSourceExempt;
                if (!mipExempt)
                {
                    if (mip.requireMipmaps && !importer.mipmapEnabled) report.Error("CAB-02", path + " requires mipmaps" + (runtimeUsed ? " (runtime catalog dependency)." : "."));
                    if (mip.requireStreaming && !importer.streamingMipmaps) report.Error("CAB-02", path + " requires streaming mipmaps" + (runtimeUsed ? " (runtime catalog dependency)." : "."));
                }
                if (!compressionExempt && compression.requireCompressed && importer.textureCompression == TextureImporterCompression.Uncompressed)
                    report.Error("CAB-03", path + " is imported uncompressed" + (runtimeUsed ? " (runtime catalog dependency)." : "."));
            }
            if (p11EstimatedTextureMb > memory.maxTextureMemoryMbPerAsset)
                report.Error("CAB-01B", "P11 estimated compressed texture memory " + p11EstimatedTextureMb.ToString("F1") + " MiB exceeds " + memory.maxTextureMemoryMbPerAsset + " MiB.");
        }

        private static void ValidateShaders(CoastArtBudgetManifest manifest, IEnumerable<string> paths, CoastArtBudgetReport report)
        {
            CoastBudgetContract budget = Contract(manifest, "shaderKeyword");
            foreach (string path in paths.Where(value => value.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)))
            {
                int keywords = 0;
                long variants = 1;
                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (!line.StartsWith("#pragma shader_feature", StringComparison.Ordinal) && !line.StartsWith("#pragma multi_compile", StringComparison.Ordinal)) continue;
                    if (line.StartsWith("#pragma multi_compile_fog", StringComparison.Ordinal) || line.StartsWith("#pragma multi_compile_instancing", StringComparison.Ordinal)) continue;
                    string[] tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Skip(2).Where(value => value != "__").ToArray();
                    keywords += tokens.Length;
                    variants *= Math.Max(2, tokens.Length + (line.Contains("shader_feature") ? 1 : 0));
                }
                if (keywords > budget.maxDeclaredKeywords) report.Error("CAB-05", path + " declares " + keywords + " keywords.");
                if (variants > budget.maxEstimatedVariants) report.Error("CAB-05", path + " estimates " + variants + " explicit variants.");
            }
        }

        private static void ValidateGameObjectAssets(CoastArtBudgetManifest manifest, IEnumerable<string> paths, CoastArtBudgetReport report)
        {
            CoastBudgetContract materials = Contract(manifest, "materialCount");
            CoastBudgetContract draw = Contract(manifest, "drawCall");
            CoastBudgetContract lod = Contract(manifest, "lod");
            CoastBudgetContract billboard = Contract(manifest, "billboard");
            CoastBudgetContract collision = Contract(manifest, "collision");
            foreach (string path in paths.Where(IsGameObjectAsset))
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
                    .Where(value => !value.name.StartsWith("HC_COL_", StringComparison.Ordinal)).ToArray();
                int materialSlots = renderers.Sum(value => value.sharedMaterials.Count(material => material != null));
                if (renderers.Any(value => value.sharedMaterials.Count(material => material != null) > materials.maxMaterialsPerRenderer)) report.Error("CAB-04", path + " exceeds per-renderer material budget.");
                if (renderers.SelectMany(value => value.sharedMaterials).Where(value => value != null).Distinct().Count() > materials.maxMaterialsPerAsset) report.Error("CAB-04", path + " exceeds per-asset material budget.");
                if (renderers.Length > draw.maxRenderersPerAsset || materialSlots > draw.maxEstimatedDrawCallsPerAsset) report.Error("CAB-06", path + " exceeds the static draw-call estimate.");

                foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true)) ValidateLod(path, group, lod, report);
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                if (colliders.Length > collision.maxCollidersPerAsset) report.Error("CAB-09", path + " exceeds collider budget.");
                if (collision.forbidMeshCollider && colliders.Any(value => value is MeshCollider)) report.Error("CAB-09", path + " contains a forbidden MeshCollider.");

                if (path.IndexOf("vegetation", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    BillboardRenderer[] billboards = root.GetComponentsInChildren<BillboardRenderer>(true);
                    if (billboards.Any(value => value.sharedMaterial != null) && billboards.Select(value => value.sharedMaterial).Where(value => value != null).Distinct().Count() > billboard.maxBillboardMaterials)
                        report.Error("CAB-08", path + " exceeds billboard material budget.");
                }
            }
        }

        private static void ValidateLod(string path, LODGroup group, CoastBudgetContract budget, CoastArtBudgetReport report)
        {
            LOD[] levels = group.GetLODs();
            if (levels.Length < budget.minimumLodLevels) { report.Error("CAB-07", path + " has fewer than " + budget.minimumLodLevels + " LOD levels."); return; }
            long[] triangles = levels.Select(level => level.renderers.Where(value => value != null).Sum(TriangleCount)).ToArray();
            if (triangles[0] > budget.maxLod0Triangles) report.Error("CAB-07", path + " LOD0 exceeds triangle budget.");
            if (triangles.Length > 1 && triangles[1] > triangles[0] * budget.maxLod1Ratio) report.Error("CAB-07", path + " LOD1 reduction is insufficient.");
            if (triangles.Length > 2 && triangles[2] > triangles[0] * budget.maxLod2Ratio) report.Error("CAB-07", path + " LOD2 reduction is insufficient.");
            for (int index = 1; index < triangles.Length; index++) if (triangles[index] > triangles[index - 1]) report.Error("CAB-07", path + " LOD triangle counts are not monotonic.");
        }

        private static void ValidateGeneratedSources(CoastArtBudgetManifest manifest, IEnumerable<string> paths, CoastArtBudgetReport report)
        {
            foreach (string path in paths.Where(value => IsUnderAny(value, manifest.generatedSourceRoots)))
            {
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    string text = File.ReadAllText(path);
                    if (text.IndexOf("ProductionReady", StringComparison.OrdinalIgnoreCase) >= 0) report.Error("P10-AUTO-PRODUCTION", path + " contains forbidden generated-source state ProductionReady.");
                }
            }
        }

        private static void ValidateQualityDowngrade(CoastArtBudgetManifest manifest, CoastArtBudgetReport report)
        {
            CoastQualityTier[] tiers = (manifest.qualityTiers ?? Array.Empty<CoastQualityTier>()).OrderBy(value => value.rank).ToArray();
            if (tiers.Length != 4 || !tiers.Select(value => value.name).SequenceEqual(new[] { "Low", "Medium", "High", "Ultra" }))
            { report.Error("CAB-10", "Quality tiers must be Low, Medium, High, Ultra in ascending rank."); return; }
            for (int index = 1; index < tiers.Length; index++)
            {
                CoastQualityTier lower = tiers[index - 1], higher = tiers[index];
                if (lower.textureLimit > higher.textureLimit || lower.lodBias > higher.lodBias || lower.vegetationDensity > higher.vegetationDensity)
                    report.Error("CAB-10", lower.name + " exceeds " + higher.name + " in a monotonic budget field.");
                if (ModeRank(lower.waterMode) > ModeRank(higher.waterMode) || ModeRank(lower.vfxMode) > ModeRank(higher.vfxMode))
                    report.Error("CAB-10", lower.name + " enables a feature above " + higher.name + ".");
            }
        }

        private static CoastBudgetContract Contract(CoastArtBudgetManifest manifest, string category) => manifest.contracts.First(value => value.category == category);
        private static int ModeRank(string value) => value == "Full" ? 2 : value == "Low" ? 1 : 0;
        private static bool IsUnderAny(string path, string[] roots) => roots != null && roots.Any(root => path.Equals(root, StringComparison.Ordinal) || path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal));
        private static bool IsTexture(string path) => new[] { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr" }.Contains(Path.GetExtension(path).ToLowerInvariant());
        private static bool IsGameObjectAsset(string path) => new[] { ".prefab", ".fbx", ".glb", ".obj" }.Contains(Path.GetExtension(path).ToLowerInvariant());
        private static long TriangleCount(Renderer renderer)
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) return 0;
            long count = 0; for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++) count += (long)mesh.GetIndexCount(subMesh) / 3; return count;
        }
    }

    internal sealed class CoastArtBudgetReport
    {
        private readonly List<string> _errors = new();
        internal int auditedAssets;
        internal bool Passed => _errors.Count == 0;
        internal void Error(string id, string message) => _errors.Add(id + ": " + message);
        internal string Format() => Passed
            ? "P10 CoastArtBudget PASS · static assets " + auditedAssets + " · shared scenes untouched · generated sources remain CAPTURED_UNREVIEWED."
            : "P10 CoastArtBudget FAIL (" + _errors.Count + ")\n- " + string.Join("\n- ", _errors);
    }

    [Serializable] internal sealed class CoastArtBudgetManifest
    {
        public string schemaVersion, workPackage, environmentId, validationMode, manifestStatus, defaultGeneratedSourceStatus, notes;
        public bool automaticProductionReady;
        public string[] forbiddenAutomaticStates, generatedSourceRoots;
        public CoastWorkPackage[] workPackages;
        public CoastBudgetContract[] contracts;
        public CoastQualityTier[] qualityTiers;
    }
    [Serializable] internal sealed class CoastWorkPackage { public string id, name; public string[] assetRoots; }
    [Serializable] internal sealed class CoastBudgetContract
    {
        public string id, category, description;
        public bool required, requireMipmaps, requireStreaming, requireCompressed, generatedSourceExempt, runtimeUsedGeneratedSourceExempt, forbidMeshCollider;
        public int maxSourceDimension, maxStandaloneDimension, maxMobileDimension, maxTextureMemoryMbPerAsset, maxMaterialsPerRenderer, maxMaterialsPerAsset;
        public int maxDeclaredKeywords, maxEstimatedVariants, maxEstimatedDrawCallsPerAsset, maxRenderersPerAsset;
        public int minimumLodLevels, maxLod0Triangles, maxBillboardMaterials, maxCollidersPerAsset;
        public float maxLod1Ratio, maxLod2Ratio, billboardStartDistance, billboardCullDistance;
    }
    [Serializable] internal sealed class CoastQualityTier
    {
        public string name, waterMode, vfxMode;
        public int rank, textureLimit;
        public float lodBias, vegetationDensity;
        public bool billboards;
    }
}
#endif
