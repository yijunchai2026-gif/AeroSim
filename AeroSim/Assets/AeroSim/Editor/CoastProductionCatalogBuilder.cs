#if UNITY_EDITOR
using System.IO;
using AeroSim.Simulation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AeroSim.Editor
{
    [InitializeOnLoad]
    public sealed class CoastProductionCatalogBuilder : IPreprocessBuildWithReport
    {
        private const string DirectoryPath = "Assets/AeroSim/Resources/CoastProduction";
        private const string AssetPath = DirectoryPath + "/CoastProductionRuntimeCatalog.asset";
        public int callbackOrder => -1000;

        static CoastProductionCatalogBuilder() => EditorApplication.delayCall += EnsureCatalog;
        public void OnPreprocessBuild(BuildReport report) => EnsureCatalog();

        [MenuItem("AeroSim/Coast Production/Rebuild Runtime Catalog")]
        public static void EnsureCatalog()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            Directory.CreateDirectory(DirectoryPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CoastProductionRuntimeCatalog>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CoastProductionRuntimeCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }
            catalog.provenance = "CAPTURED_UNREVIEWED / NOT SCANNED";
            catalog.packageIds = new[] { "P01", "P02", "P03", "P04", "P05", "P06", "P07", "P08", "P09", "P10", "P11" };
            catalog.p01RockPrefabs = new[]
            {
                Load<GameObject>("P01/Prefabs/sea-stack-a.prefab"), Load<GameObject>("P01/Prefabs/sea-stack-b.prefab"),
                Load<GameObject>("P01/Prefabs/fractured-edge.prefab")
            };
            catalog.p01RockMaterial = Load<Material>("P01/Materials/CoastRockP01.mat");
            catalog.p11HeroCliffPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AeroSim/Resources/CoastProduction/P11/HeroCliff.prefab");
            catalog.p11DiagnosticShader = Load<Shader>("P11/HeroCliff/Shaders/HeroCliffDiagnostics.shader");
            catalog.intertidalWetRock = Load<Texture2D>("P02/source/wet-rock-reference.png");
            catalog.intertidalBed = Load<Texture2D>("P02/source/tidepool-bed-reference.png");
            catalog.gravelSurface = Load<Texture2D>("P03/source/coastal-aggregate-reference.png");
            catalog.concreteSurface = Load<Texture2D>("P04/sources/P04_concrete_source.png");
            catalog.concreteDecals = Load<Texture2D>("P04/sources/P04_decal_atlas_source.png");
            catalog.industrialMetal = Load<Texture2D>("P05/source/generated_image_035559.png");
            catalog.vegetationAtlas = Load<Texture2D>("P06/source/coastal-vegetation-atlas.png");
            catalog.foamAtlas = Load<Texture2D>("P07/source/foam-atlas-reference.png");
            catalog.weatherAtlas = Load<Texture2D>("P08/source/weather-atlas-reference.png");
            catalog.lookDevProfiles = new[]
            {
                Load<TextAsset>("P09/profiles/01-clear-exposure.json"), Load<TextAsset>("P09/profiles/02-overcast-exposure.json"),
                Load<TextAsset>("P09/profiles/03-rain-exposure.json")
            };
            catalog.budgetContract = Load<TextAsset>("P10/coast-art-budget-v1.json");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static T Load<T>(string relativePath) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>("Assets/AeroSim/Art/CoastProduction/" + relativePath);
    }
}
#endif
