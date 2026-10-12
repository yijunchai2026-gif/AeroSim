using System;
using System.Collections.Generic;
using AeroSim.Domain;
using UnityEngine;

namespace AeroSim.Domain
{
    [Serializable]
    public sealed class EnvironmentState
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public int seed;
        public string environmentId = "";
        public string weatherId = "Clear";
        public float timeOfDay;
        public Vector3 sunDirection;
        public float cloudAmount;
        public float fogDensity;
        public Vector3 windDirection;
        public float windSpeedMps;
        public float precipitation;
        public float wetness;
        public float puddleAmount;
        public float waterLevel;
        public float vfxDensity;
        public string audioSnapshot = "open-clear";

        public static EnvironmentState Create(FlightConfig config)
        {
            config ??= new FlightConfig();
            string weather = FlightWeather.IsSupported(config.weather) ? config.weather : "Clear";
            bool rain = weather.Equals("Rain", StringComparison.OrdinalIgnoreCase);
            bool overcast = rain || weather.Equals("Overcast", StringComparison.OrdinalIgnoreCase);
            float cloud = rain ? 1f : overcast ? .86f : .26f;
            float sunElevation = config.timeOfDay > 18 ? 13 : 36;
            Quaternion sunRotation = Quaternion.Euler(sunElevation, -38, 0);
            Vector3 wind = new Vector3(1, 0, -.35f).normalized;
            return new EnvironmentState
            {
                seed = config.environmentSeed != 0 ? config.environmentSeed : StableSeed(config.environment, weather, config.timeOfDay, config.windMph),
                environmentId = string.IsNullOrWhiteSpace(config.environment) ? "Pacific Coast" : config.environment,
                weatherId = weather,
                timeOfDay = config.timeOfDay,
                sunDirection = -(sunRotation * Vector3.forward),
                cloudAmount = cloud,
                fogDensity = rain ? .0046f : overcast ? .0031f : .00115f,
                windDirection = wind,
                windSpeedMps = Mathf.Max(0, config.windMph) * .44704f,
                precipitation = rain ? 1 : 0,
                wetness = rain ? 1 : overcast ? .12f : 0,
                puddleAmount = rain ? .65f : 0,
                waterLevel = 1,
                vfxDensity = rain ? 1 : 0,
                audioSnapshot = rain ? "open-rain" : overcast ? "open-overcast" : "open-clear"
            };
        }

        public EnvironmentState Copy() => JsonUtility.FromJson<EnvironmentState>(JsonUtility.ToJson(this));

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (schemaVersion != CurrentSchemaVersion) errors.Add("Unsupported EnvironmentState schema " + schemaVersion + ".");
            if (string.IsNullOrWhiteSpace(environmentId)) errors.Add("EnvironmentState.environmentId is required.");
            if (!FlightWeather.IsSupported(weatherId)) errors.Add("EnvironmentState.weatherId is unsupported.");
            Check01(cloudAmount, nameof(cloudAmount), errors); Check01(precipitation, nameof(precipitation), errors);
            Check01(wetness, nameof(wetness), errors); Check01(puddleAmount, nameof(puddleAmount), errors);
            Check01(waterLevel, nameof(waterLevel), errors); Check01(vfxDensity, nameof(vfxDensity), errors);
            if (!Finite(fogDensity) || fogDensity < 0) errors.Add("EnvironmentState.fogDensity must be finite and non-negative.");
            if (!Finite(windSpeedMps) || windSpeedMps < 0) errors.Add("EnvironmentState.windSpeedMps must be finite and non-negative.");
            if (sunDirection.sqrMagnitude < .9f || windDirection.sqrMagnitude < .9f) errors.Add("EnvironmentState directions must be normalized.");
            return errors;
        }

        public static int StableSeed(string environment, string weather, float timeOfDay, float windMph)
        {
            unchecked
            {
                uint hash = 2166136261;
                string value = (environment ?? "") + "|" + (weather ?? "") + "|" + Mathf.RoundToInt(timeOfDay * 100) + "|" + Mathf.RoundToInt(windMph * 100);
                foreach (char c in value) { hash ^= c; hash *= 16777619; }
                int result = (int)(hash & 0x7fffffff);
                return result == 0 ? 1 : result;
            }
        }

        private static void Check01(float value, string name, List<string> errors)
        {
            if (!Finite(value) || value < 0 || value > 1) errors.Add("EnvironmentState." + name + " must be within [0,1].");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

namespace AeroSim.Simulation
{
    public enum EnvironmentFeatureMode { Off = 0, Low = 1, Full = 2 }
    public enum EnvironmentSurfaceKind { Terrain, Rock, Vegetation, Concrete, Metal, Water, Marking, Generic }

    [Serializable]
    public sealed class EnvironmentStreamingPolicy
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public int quality;
        public float sectorSize = 240;
        public float loadDistance;
        public float unloadDistance;
        public float lod0Distance;
        public float lod1Distance;
        public float lod2Distance;
        public float vegetationDensity;
        public bool textureMipStreaming;
        public EnvironmentFeatureMode reflections;
        public EnvironmentFeatureMode probes;
        public EnvironmentFeatureMode water;
        public EnvironmentFeatureMode vegetation;
        public EnvironmentFeatureMode vfx;
        public EnvironmentFeatureMode audio;

        public static EnvironmentStreamingPolicy ForQuality(int quality)
        {
            int q = AeroSimQuality.Normalize(quality);
            return new EnvironmentStreamingPolicy
            {
                quality = q,
                loadDistance = q == AeroSimQuality.Low ? 360 : q == AeroSimQuality.Medium ? 480 : 720,
                unloadDistance = q == AeroSimQuality.Low ? 480 : q == AeroSimQuality.Medium ? 600 : 900,
                lod0Distance = q == AeroSimQuality.Low ? 10 : q == AeroSimQuality.Medium ? 15 : 22,
                lod1Distance = q == AeroSimQuality.Low ? 55 : q == AeroSimQuality.Medium ? 80 : 110,
                lod2Distance = q == AeroSimQuality.Low ? 180 : q == AeroSimQuality.Medium ? 260 : 420,
                vegetationDensity = q == AeroSimQuality.Low ? .48f : q == AeroSimQuality.Medium ? .72f : q == AeroSimQuality.High ? 1 : 1.2f,
                textureMipStreaming = true,
                reflections = q == AeroSimQuality.Low ? EnvironmentFeatureMode.Low : q >= AeroSimQuality.High ? EnvironmentFeatureMode.Full : EnvironmentFeatureMode.Low,
                probes = q >= AeroSimQuality.High ? EnvironmentFeatureMode.Full : EnvironmentFeatureMode.Low,
                water = q >= AeroSimQuality.High ? EnvironmentFeatureMode.Full : EnvironmentFeatureMode.Low,
                vegetation = q >= AeroSimQuality.High ? EnvironmentFeatureMode.Full : EnvironmentFeatureMode.Low,
                vfx = q == AeroSimQuality.Low ? EnvironmentFeatureMode.Off : q >= AeroSimQuality.High ? EnvironmentFeatureMode.Full : EnvironmentFeatureMode.Low,
                audio = q == AeroSimQuality.Low ? EnvironmentFeatureMode.Low : q >= AeroSimQuality.High ? EnvironmentFeatureMode.Full : EnvironmentFeatureMode.Low
            };
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (version != CurrentVersion) errors.Add("Unsupported streaming policy version.");
            if (sectorSize <= 0 || loadDistance <= 0 || unloadDistance <= loadDistance) errors.Add("Sector load/unload distances are invalid.");
            if (!(lod0Distance > 0 && lod1Distance > lod0Distance && lod2Distance > lod1Distance)) errors.Add("LOD distances must be strictly increasing.");
            if (vegetationDensity < 0) errors.Add("Vegetation density cannot be negative.");
            return errors;
        }
    }

    [Serializable]
    public sealed class EnvironmentSectorContract
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public string id;
        public Vector2Int coordinate;
        public Bounds bounds;
        public bool global;
        public bool safetyCritical;

        public static List<EnvironmentSectorContract> CreateGrid(float size)
        {
            var sectors = new List<EnvironmentSectorContract>
            {
                new() { id = "global", global = true, safetyCritical = true, bounds = new Bounds(Vector3.zero, new Vector3(4096, 2048, 4096)) }
            };
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                sectors.Add(new EnvironmentSectorContract
                {
                    id = "sector." + x + "." + z,
                    coordinate = new Vector2Int(x, z),
                    bounds = new Bounds(new Vector3(x * size, 128, z * size), new Vector3(size, 512, size)),
                    safetyCritical = x == 0 && z == 0
                });
            return sectors;
        }
    }

    [Serializable]
    public sealed class SurfaceMaterialContract
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public string id;
        public EnvironmentSurfaceKind kind;
        public float coverageMeters = 1;
        public bool baseColor = true;
        public bool normal;
        public bool mask;
        public bool supportsWetness;
        public bool receivesReflection;
        public string fallback = "constant-pbr";

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (version != CurrentVersion) errors.Add("Unsupported surface contract version.");
            if (string.IsNullOrWhiteSpace(id)) errors.Add("Surface material id is required.");
            if (coverageMeters <= 0 || float.IsNaN(coverageMeters) || float.IsInfinity(coverageMeters)) errors.Add("Surface coverage must be finite and positive.");
            if (string.IsNullOrWhiteSpace(fallback)) errors.Add("Surface fallback is required.");
            return errors;
        }
    }

    public interface IEnvironmentStateAdapter { void ApplyEnvironmentState(EnvironmentState state); }
    public interface IReflectionEnvironmentAdapter { EnvironmentFeatureMode ApplyReflectionPolicy(EnvironmentFeatureMode requested); }
    public interface IProbeEnvironmentAdapter { EnvironmentFeatureMode ApplyProbePolicy(EnvironmentFeatureMode requested); }
    public interface IWaterEnvironmentAdapter { EnvironmentFeatureMode ApplyWaterPolicy(EnvironmentFeatureMode requested); }
    public interface IVegetationEnvironmentAdapter { EnvironmentFeatureMode ApplyVegetationPolicy(EnvironmentFeatureMode requested); }
    public interface IVfxEnvironmentAdapter { EnvironmentFeatureMode ApplyVfxPolicy(EnvironmentFeatureMode requested); }
    public interface IEnvironmentAudioAdapter { EnvironmentFeatureMode ApplyEnvironmentAudioPolicy(EnvironmentFeatureMode requested); }

    [Serializable]
    public sealed class EnvironmentFeatureStatus
    {
        public string feature;
        public EnvironmentFeatureMode requested;
        public EnvironmentFeatureMode effective;
        public string fallback;
    }

    [Serializable]
    public sealed class EnvironmentDiagnostics
    {
        public int contractVersion = 1;
        public int stateSchemaVersion;
        public int seed;
        public int quality;
        public int sectorContracts;
        public int assignedObjects;
        public int materialContracts;
        public List<EnvironmentFeatureStatus> features = new();
        public List<string> errors = new();
        public bool IsValid => errors.Count == 0;
    }

    public sealed class EnvironmentSectorTag : MonoBehaviour
    {
        public int contractVersion = EnvironmentSectorContract.CurrentVersion;
        public string sectorId;
        public bool safetyCritical;
    }

    public sealed class EnvironmentProductionContext
    {
        private readonly Dictionary<Material, SurfaceMaterialContract> _materials = new();
        private readonly List<EnvironmentSectorContract> _sectors;
        private IEnvironmentStateAdapter _state;
        private IReflectionEnvironmentAdapter _reflection;
        private IProbeEnvironmentAdapter _probe;
        private IWaterEnvironmentAdapter _water;
        private IVegetationEnvironmentAdapter _vegetation;
        private IVfxEnvironmentAdapter _vfx;
        private IEnvironmentAudioAdapter _audio;
        private int _assignedObjects;

        public EnvironmentState State { get; }
        public EnvironmentStreamingPolicy Policy { get; }
        public IReadOnlyList<EnvironmentSectorContract> Sectors => _sectors;
        public EnvironmentDiagnostics Diagnostics { get; } = new();

        public EnvironmentProductionContext(EnvironmentState state, EnvironmentStreamingPolicy policy)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _sectors = EnvironmentSectorContract.CreateGrid(policy.sectorSize);
            Diagnostics.stateSchemaVersion = state.schemaVersion; Diagnostics.seed = state.seed; Diagnostics.quality = policy.quality;
            Diagnostics.sectorContracts = _sectors.Count;
            Diagnostics.errors.AddRange(state.Validate()); Diagnostics.errors.AddRange(policy.Validate());
        }

        public void Connect(object adapter)
        {
            _state = adapter as IEnvironmentStateAdapter; _reflection = adapter as IReflectionEnvironmentAdapter;
            _probe = adapter as IProbeEnvironmentAdapter; _water = adapter as IWaterEnvironmentAdapter;
            _vegetation = adapter as IVegetationEnvironmentAdapter; _vfx = adapter as IVfxEnvironmentAdapter; _audio = adapter as IEnvironmentAudioAdapter;
            if (_state == null) Diagnostics.errors.Add("Environment state adapter is missing.");
            if (_reflection == null) Diagnostics.errors.Add("Reflection adapter is missing.");
            if (_probe == null) Diagnostics.errors.Add("Probe adapter is missing.");
            if (_water == null) Diagnostics.errors.Add("Water adapter is missing.");
            if (_vegetation == null) Diagnostics.errors.Add("Vegetation adapter is missing.");
            if (_vfx == null) Diagnostics.errors.Add("VFX adapter is missing.");
            if (_audio == null) Diagnostics.errors.Add("Environment audio adapter is missing.");
        }

        public void Apply()
        {
            _state?.ApplyEnvironmentState(State.Copy());
            ApplyFeature("reflection", Policy.reflections, _reflection == null ? null : new Func<EnvironmentFeatureMode, EnvironmentFeatureMode>(_reflection.ApplyReflectionPolicy));
            ApplyFeature("probe", Policy.probes, _probe == null ? null : new Func<EnvironmentFeatureMode, EnvironmentFeatureMode>(_probe.ApplyProbePolicy));
            ApplyFeature("water", Policy.water, _water == null ? null : new Func<EnvironmentFeatureMode, EnvironmentFeatureMode>(_water.ApplyWaterPolicy));
            ApplyFeature("vegetation", Policy.vegetation, _vegetation == null ? null : new Func<EnvironmentFeatureMode, EnvironmentFeatureMode>(_vegetation.ApplyVegetationPolicy));
            ApplyFeature("vfx", Policy.vfx, _vfx == null ? null : new Func<EnvironmentFeatureMode, EnvironmentFeatureMode>(_vfx.ApplyVfxPolicy));
            ApplyFeature("audio", Policy.audio, _audio == null ? null : new Func<EnvironmentFeatureMode, EnvironmentFeatureMode>(_audio.ApplyEnvironmentAudioPolicy));
        }

        public void RegisterMaterial(Material material, SurfaceMaterialContract contract)
        {
            if (material == null || contract == null) { Diagnostics.errors.Add("Material registration requires material and contract."); return; }
            if (_materials.TryGetValue(material, out var existing) && existing.id != contract.id) Diagnostics.errors.Add("Material registered with conflicting surface contracts.");
            _materials[material] = contract;
            foreach (string error in contract.Validate()) Diagnostics.errors.Add(contract.id + ": " + error);
            Diagnostics.materialContracts = _materials.Count;
        }

        public void RegisterObject(GameObject value, bool safetyCritical)
        {
            if (value == null) return;
            Vector3 position = value.transform.position;
            Renderer renderer = value.GetComponent<Renderer>();
            bool global = renderer != null && Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z) >= Policy.sectorSize;
            int x = Mathf.Clamp(Mathf.FloorToInt((position.x + Policy.sectorSize * .5f) / Policy.sectorSize), -1, 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((position.z + Policy.sectorSize * .5f) / Policy.sectorSize), -1, 1);
            string id = global ? "global" : "sector." + x + "." + z;
            var tag = value.GetComponent<EnvironmentSectorTag>() ?? value.AddComponent<EnvironmentSectorTag>();
            tag.sectorId = id; tag.safetyCritical = safetyCritical;
            _assignedObjects++; Diagnostics.assignedObjects = _assignedObjects;
        }

        public void CompleteValidation()
        {
            if (_assignedObjects == 0) Diagnostics.errors.Add("No environment objects were assigned to sectors.");
            if (_materials.Count == 0) Diagnostics.errors.Add("No surface materials were registered.");
            foreach (var sector in _sectors)
            {
                if (sector.version != EnvironmentSectorContract.CurrentVersion || string.IsNullOrWhiteSpace(sector.id)) Diagnostics.errors.Add("Invalid sector contract.");
                if (sector.bounds.size.x <= 0 || sector.bounds.size.z <= 0) Diagnostics.errors.Add("Sector bounds must be positive.");
            }
        }

        private void ApplyFeature(string feature, EnvironmentFeatureMode requested, Func<EnvironmentFeatureMode, EnvironmentFeatureMode> apply)
        {
            EnvironmentFeatureMode effective = apply == null ? EnvironmentFeatureMode.Off : apply(requested);
            if (effective > requested) { Diagnostics.errors.Add(feature + " adapter exceeded requested policy."); effective = requested; }
            Diagnostics.features.Add(new EnvironmentFeatureStatus
            {
                feature = feature, requested = requested, effective = effective,
                fallback = effective == requested ? "none" : effective == EnvironmentFeatureMode.Off ? "off" : "low"
            });
        }
    }

    public sealed partial class FlightSimulator : IEnvironmentStateAdapter, IReflectionEnvironmentAdapter, IProbeEnvironmentAdapter,
        IWaterEnvironmentAdapter, IVegetationEnvironmentAdapter, IVfxEnvironmentAdapter, IEnvironmentAudioAdapter
    {
        private EnvironmentProductionContext _environmentContext;
        private EnvironmentState _environmentState;
        private EnvironmentStreamingPolicy _environmentPolicy;
        private bool _buildingEnvironment;
        private EnvironmentFeatureMode _waterMode, _vegetationMode, _vfxMode, _environmentAudioMode;
        public string ActiveEnvironmentAudioSnapshot { get; private set; } = "off";

        public EnvironmentState EnvironmentState => _environmentState?.Copy();
        public EnvironmentStreamingPolicy EnvironmentPolicy => _environmentPolicy;
        public IReadOnlyList<EnvironmentSectorContract> EnvironmentSectorContracts => _environmentContext?.Sectors;
        public EnvironmentDiagnostics EnvironmentDiagnostics => _environmentContext?.Diagnostics;

        public void ApplyEnvironmentState(EnvironmentState state)
        {
            _environmentState = state;
            _rainAmount = state.precipitation;
            _cloudAmount = state.cloudAmount;
        }
        public EnvironmentFeatureMode ApplyReflectionPolicy(EnvironmentFeatureMode requested) => EnvironmentFeatureMode.Off;
        public EnvironmentFeatureMode ApplyProbePolicy(EnvironmentFeatureMode requested) => EnvironmentFeatureMode.Off;
        public EnvironmentFeatureMode ApplyWaterPolicy(EnvironmentFeatureMode requested) => _waterMode = requested;
        public EnvironmentFeatureMode ApplyVegetationPolicy(EnvironmentFeatureMode requested) => _vegetationMode = requested;
        public EnvironmentFeatureMode ApplyVfxPolicy(EnvironmentFeatureMode requested) => _vfxMode = requested;
        public EnvironmentFeatureMode ApplyEnvironmentAudioPolicy(EnvironmentFeatureMode requested)
        {
            _environmentAudioMode = requested;
            ActiveEnvironmentAudioSnapshot = requested == EnvironmentFeatureMode.Off ? "off" : _environmentState.audioSnapshot;
            return requested;
        }

        private void PrepareEnvironmentProduction(FlightConfig config)
        {
            _environmentState = EnvironmentState.Create(config);
            config.environmentSeed = _environmentState.seed;
            _environmentPolicy = EnvironmentStreamingPolicy.ForQuality(LocalStore.Profile.settings.quality);
            _environmentContext = new EnvironmentProductionContext(_environmentState, _environmentPolicy);
            _environmentContext.Connect(this);
            _environmentContext.Apply();
        }

        private void RegisterEnvironmentObject(GameObject value, bool safetyCritical)
        {
            if (_buildingEnvironment) _environmentContext?.RegisterObject(value, safetyCritical);
        }

        private void RegisterSurface(Material material, string id, EnvironmentSurfaceKind kind, float coverageMeters, bool wetness, bool reflection, string fallback = "constant-pbr")
        {
            if (!_buildingEnvironment) return;
            _environmentContext?.RegisterMaterial(material, new SurfaceMaterialContract
            {
                id = id, kind = kind, coverageMeters = coverageMeters, supportsWetness = wetness,
                receivesReflection = reflection, fallback = fallback
            });
        }

        private void CompleteEnvironmentProduction()
        {
            _environmentContext?.CompleteValidation();
            if (Record != null)
            {
                Record.environmentState = _environmentState?.Copy();
                if (Record.config != null) Record.config.environmentSeed = _environmentState?.seed ?? 0;
            }
        }
    }
}
