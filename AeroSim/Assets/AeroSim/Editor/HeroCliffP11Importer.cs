#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AeroSim.Simulation;
using UnityEditor;
using UnityEngine;

namespace AeroSim.Editor
{
    public sealed class HeroCliffP11Importer : AssetPostprocessor
    {
        internal const string Root = "Assets/AeroSim/Art/CoastProduction/P11/HeroCliff";
        internal const string Model = Root + "/Models/HeroCliff_P11.fbx";
        internal const string Prefab = "Assets/AeroSim/Resources/CoastProduction/P11/HeroCliff.prefab";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "/Textures/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            bool baseColor = assetPath.Contains("BaseColor");
            importer.textureType = assetPath.Contains("Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = baseColor;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 4096;
            Set(importer, "Standalone", 4096, TextureImporterFormat.BC7);
            Set(importer, "Android", 2048, TextureImporterFormat.ASTC_6x6);
            Set(importer, "iPhone", 2048, TextureImporterFormat.ASTC_6x6);
        }

        void OnPreprocessModel()
        {
            if (assetPath != Model) return;
            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importBlendShapes = false;
            importer.generateSecondaryUV = false; // P11 carries a non-overlapping baked UV0 atlas; no generated lightmap UV required.
            importer.importNormals = ModelImporterNormals.Import;
            importer.normalCalculationMode = ModelImporterNormalCalculationMode.AngleWeighted;
        }

        static void Set(TextureImporter importer, string platform, int size, TextureImporterFormat format)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true; settings.maxTextureSize = size; settings.format = format;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(settings);
        }
    }

    [InitializeOnLoad]
    public static class HeroCliffP11Builder
    {
        static HeroCliffP11Builder() => EditorApplication.delayCall += BuildIfPossible;

        [MenuItem("AeroSim/Coast Production/Rebuild P11 Hero Cliff")]
        public static void BuildIfPossible()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HeroCliffP11Importer.Model);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(HeroCliffP11Importer.Root + "/Shaders/HeroCliffP11.shader");
            if (model == null || shader == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(HeroCliffP11Importer.Prefab));
            const string materialPath = "Assets/AeroSim/Resources/CoastProduction/P11/HeroCliffP11.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader) { name = "Hero Cliff P11" }; AssetDatabase.CreateAsset(material, materialPath); }
            material.shader = shader; material.enableInstancing = true; material.EnableKeyword("HERO_CLIFF_MICRO");
            Bind(material, "_BaseColor", "BaseColor"); Bind(material, "_NormalMap", "Normal");
            Bind(material, "_RoughnessMap", "Roughness"); Bind(material, "_AOMap", "AO");
            Bind(material, "_HeightMap", "Height"); Bind(material, "_CurvatureMap", "Curvature");
            Bind(material, "_WetCavityMask", "WetnessCavityMask"); EditorUtility.SetDirty(material);

            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name = "HeroCliff_P11_GENERATED_UNREVIEWED";
            var lodRenderers = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                Transform node = Find(root.transform, "HC_LOD" + i);
                if (node == null) { UnityEngine.Object.DestroyImmediate(root); throw new InvalidDataException("P11 missing HC_LOD" + i); }
                var renderer = node.GetComponent<Renderer>(); renderer.sharedMaterial = material; lodRenderers[i] = renderer;
                var properties = new MaterialPropertyBlock(); properties.SetColor("_LodColor", new[] { Color.red, Color.yellow, Color.green, Color.blue }[i]); properties.SetColor("_MaterialId", new Color(.18f,.72f,.36f,1)); renderer.SetPropertyBlock(properties);
            }
            foreach (var node in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("HC_COL_BOX_", StringComparison.Ordinal)).ToArray())
            {
                var filter = node.GetComponent<MeshFilter>(); var bounds = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                var collider = node.gameObject.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                foreach (var renderer in node.GetComponents<Renderer>()) UnityEngine.Object.DestroyImmediate(renderer);
                if (filter != null) UnityEngine.Object.DestroyImmediate(filter);
                node.gameObject.layer = 0;
            }
            var group = root.GetComponent<LODGroup>() ?? root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(.58f, new[]{lodRenderers[0]}), new LOD(.28f,new[]{lodRenderers[1]}), new LOD(.12f,new[]{lodRenderers[2]}), new LOD(.035f,new[]{lodRenderers[3]}), new LOD(.005f,Array.Empty<Renderer>()) });
            group.fadeMode = LODFadeMode.CrossFade; group.animateCrossFading = true; group.RecalculateBounds();
            var metadata = root.AddComponent<HeroCliffP11Metadata>(); metadata.productionReady = false; metadata.provenance = "GENERATED_UNREVIEWED / UNITY DEVICE QA NOT RUN"; metadata.sourceSeed = 110412;
            PrefabUtility.SaveAsPrefabAsset(root, HeroCliffP11Importer.Prefab); UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            CoastProductionCatalogBuilder.EnsureCatalog();
        }

        static void Bind(Material material, string property, string suffix) => material.SetTexture(property,
            AssetDatabase.LoadAssetAtPath<Texture2D>(HeroCliffP11Importer.Root + "/Textures/T_HeroCliff_P11_" + suffix + ".png"));
        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }
}
#endif
