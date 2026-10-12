using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroSim.Simulation
{
    [CreateAssetMenu(menuName = "AeroSim/Coast Production Runtime Catalog")]
    public sealed class CoastProductionRuntimeCatalog : ScriptableObject
    {
        public const string ResourcePath = "CoastProduction/CoastProductionRuntimeCatalog";
        public string provenance = "CAPTURED_UNREVIEWED / NOT SCANNED";
        public string[] packageIds = { "P01", "P02", "P03", "P04", "P05", "P06", "P07", "P08", "P09", "P10", "P11" };
        public GameObject[] p01RockPrefabs;
        public GameObject p11HeroCliffPrefab;
        public Shader p11DiagnosticShader;
        public Material p01RockMaterial;
        public Texture2D intertidalWetRock;
        public Texture2D intertidalBed;
        public Texture2D gravelSurface;
        public Texture2D concreteSurface;
        public Texture2D concreteDecals;
        public Texture2D industrialMetal;
        public Texture2D vegetationAtlas;
        public Texture2D foamAtlas;
        public Texture2D weatherAtlas;
        public TextAsset[] lookDevProfiles;
        public TextAsset budgetContract;

        public static CoastProductionRuntimeCatalog Load() => Resources.Load<CoastProductionRuntimeCatalog>(ResourcePath);
        public bool HasAllVisualCandidates => intertidalWetRock != null && intertidalBed != null && gravelSurface != null &&
            concreteSurface != null && concreteDecals != null && industrialMetal != null && vegetationAtlas != null &&
            foamAtlas != null && weatherAtlas != null && p01RockPrefabs != null && p01RockPrefabs.Length > 0 && p11DiagnosticShader != null;
    }

    [Serializable]
    public sealed class CoastProductionRuntimeStatus
    {
        public string provenance = "CAPTURED_UNREVIEWED / NOT SCANNED";
        public string[] packageIds = Array.Empty<string>();
        public int boundTextures;
        public int instantiatedP01Meshes;
        public bool p11Loaded;
        public string p11LoadState = "NOT_ATTEMPTED";
        public string p11Provenance = "GENERATED_UNREVIEWED";
        public string lookDevProfile = "NOT_CONNECTED";
        public string qualityTier;
        public string weather;
        public bool mipStreamingRequested;
        public bool mipStreamingActiveActual;
        public float mipStreamingMemoryBudgetMbActual;
        public bool candidateBindingsComplete;
    }

    internal readonly struct CoastLookDevProfile
    {
        internal readonly string Id;
        internal readonly Color Sun, Sky, Equator, Ground, Fog;
        internal readonly float SunIntensity, ShadowStrength, AmbientIntensity, ReflectionIntensity, FogDensity;

        private CoastLookDevProfile(string id, Color sun, Color sky, Color equator, Color ground, Color fog,
            float sunIntensity, float shadowStrength, float ambientIntensity, float reflectionIntensity, float fogDensity)
        {
            Id = id; Sun = sun; Sky = sky; Equator = equator; Ground = ground; Fog = fog;
            SunIntensity = sunIntensity; ShadowStrength = shadowStrength; AmbientIntensity = ambientIntensity;
            ReflectionIntensity = reflectionIntensity; FogDensity = fogDensity;
        }

        internal static CoastLookDevProfile ForWeather(string weather)
        {
            if (string.Equals(weather, "Rain", StringComparison.OrdinalIgnoreCase))
                return new CoastLookDevProfile("P09-LD-03-RAIN-EXPOSURE", Hex("CAD5DA"), Hex("7E8F98"), Hex("65757A"), Hex("353D3D"), Hex("83949B"), .24f, .32f, .78f, .52f, .0058f);
            if (string.Equals(weather, "Overcast", StringComparison.OrdinalIgnoreCase))
                return new CoastLookDevProfile("P09-LD-02-OVERCAST-EXPOSURE", Hex("DCE4E7"), Hex("A6B2B7"), Hex("899598"), Hex("4D5351"), Hex("AAB7BC"), .42f, .48f, .94f, .62f, .0028f);
            return new CoastLookDevProfile("P09-LD-01-CLEAR-EXPOSURE", Hex("FFF1D6"), Hex("9FB4C1"), Hex("7F9194"), Hex("3E4340"), Hex("B8C7CD"), 1.05f, .82f, .82f, .72f, .0018f);
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }
    }

    public sealed partial class FlightSimulator
    {
        private CoastProductionRuntimeCatalog _coastProduction;
        private readonly CoastProductionRuntimeStatus _coastProductionStatus = new();
        public CoastProductionRuntimeStatus CoastProductionStatus => _coastProductionStatus;

        private void PrepareCoastProductionIntegration()
        {
            _coastProduction = CoastProductionRuntimeCatalog.Load();
            _coastProductionStatus.packageIds = _coastProduction == null ? Array.Empty<string>() : _coastProduction.packageIds;
            _coastProductionStatus.weather = _environmentState?.weatherId ?? "UNKNOWN";
            _coastProductionStatus.qualityTier = AeroSim.Domain.AeroSimQuality.Name(_environmentQuality);
            _coastProductionStatus.mipStreamingRequested = _environmentPolicy?.textureMipStreaming == true;
            _coastProductionStatus.mipStreamingActiveActual = QualitySettings.streamingMipmapsActive;
            _coastProductionStatus.mipStreamingMemoryBudgetMbActual = QualitySettings.streamingMipmapsMemoryBudget;
            _coastProductionStatus.boundTextures = 0;
            _coastProductionStatus.instantiatedP01Meshes = 0;
            _coastProductionStatus.p11Loaded = false;
            _coastProductionStatus.p11LoadState = "NOT_ATTEMPTED";
            _coastProductionStatus.lookDevProfile = "NOT_CONNECTED";
            _coastProductionStatus.candidateBindingsComplete = false;
        }

        private Texture2D CoastTexture(string semantic)
        {
            if (_coastProduction == null) return null;
            Texture2D value = semantic switch
            {
                "soil" => _coastProduction.gravelSurface,
                "coastal-rock" => _coastProduction.intertidalWetRock,
                "weathered-concrete" => _coastProduction.concreteSurface,
                "galvanized-steel" => _coastProduction.industrialMetal,
                "coastal-vegetation" => _coastProduction.vegetationAtlas,
                "safety-marking" => _coastProduction.concreteDecals,
                "tide-pool" => _coastProduction.intertidalBed,
                "intertidal-wet-band" => _coastProduction.intertidalWetRock,
                "shore-breaker-foam" => _coastProduction.foamAtlas,
                _ => null
            };
            if (value != null) _coastProductionStatus.boundTextures++;
            return value;
        }

        private void ApplyCoastLookDev(Light sun)
        {
            if (_visualEnvironment != "Pacific Coast" || _environmentState == null) return;
            var profile = CoastLookDevProfile.ForWeather(_environmentState.weatherId);
            RenderSettings.ambientSkyColor = profile.Sky;
            RenderSettings.ambientEquatorColor = profile.Equator;
            RenderSettings.ambientGroundColor = profile.Ground;
            RenderSettings.ambientIntensity = profile.AmbientIntensity;
            RenderSettings.reflectionIntensity = profile.ReflectionIntensity;
            RenderSettings.fogColor = profile.Fog;
            // EnvironmentState is authoritative for stateful values; P09 only supplies the palette/exposure response.
            RenderSettings.fogDensity = _environmentState.fogDensity;
            if (sun != null) { sun.color = profile.Sun; sun.intensity = profile.SunIntensity; sun.shadowStrength = profile.ShadowStrength; }
            _coastProductionStatus.lookDevProfile = profile.Id;
        }

        private bool AddP11HeroCliff(Transform parent)
        {
            var result = HeroCliffProductionAssetLoader.TryAssemble(parent, _environmentState);
            _coastProductionStatus.p11LoadState = result.state + " · " + result.message;
            if (!result.Succeeded) return false;
            result.instance.transform.localPosition = new Vector3(-8, -2.2f, 0);
            result.instance.transform.localRotation = Quaternion.Euler(0, 0, 0);
            result.instance.transform.localScale = Vector3.one;
            RegisterEnvironmentObject(result.instance, true);
            _coastProductionStatus.p11Loaded = true;
            return true;
        }

        private void AddP01CandidateGeometry(Transform parent)
        {
            if (_coastProduction?.p01RockPrefabs == null) return;
            Vector3[] positions = { new(-18, -2.5f, -21), new(-34, -3.1f, 18), new(-8, -1.8f, 34) };
            int budget = _environmentQuality == AeroSim.Domain.AeroSimQuality.Low ? 1 : _environmentQuality == AeroSim.Domain.AeroSimQuality.Ultra ? 3 : 2;
            for (int index = 0; index < Mathf.Min(budget, Mathf.Min(positions.Length, _coastProduction.p01RockPrefabs.Length)); index++)
            {
                GameObject prefab = _coastProduction.p01RockPrefabs[index];
                if (prefab == null) continue;
                GameObject instance = Instantiate(prefab, parent, false);
                instance.name += " · P01 CAPTURED_UNREVIEWED / NOT SCANNED";
                instance.transform.localPosition = positions[index];
                float scale = index == 0 ? 1.2f : index == 1 ? .9f : .65f;
                instance.transform.localScale = Vector3.one * scale;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (_coastProduction.p01RockMaterial != null) renderer.sharedMaterial = _coastProduction.p01RockMaterial;
                    var properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties);
                    properties.SetFloat("_Wetness", _environmentState?.wetness ?? 0);
                    renderer.SetPropertyBlock(properties);
                }
                RegisterEnvironmentObject(instance, false);
                _coastProductionStatus.instantiatedP01Meshes++;
            }
        }

        private void CompleteCoastProductionIntegration()
        {
            _coastProductionStatus.candidateBindingsComplete = _coastProduction != null && _coastProduction.HasAllVisualCandidates &&
                _coastProductionStatus.packageIds.Length == 11 && _coastProductionStatus.boundTextures >= 9 &&
                (_coastProductionStatus.p11Loaded || _coastProductionStatus.instantiatedP01Meshes > 0) && _coastProductionStatus.lookDevProfile != "NOT_CONNECTED";
        }
    }
}
