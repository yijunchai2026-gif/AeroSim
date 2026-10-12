using System;
using System.Collections.Generic;
using System.Linq;
using AeroSim.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroSim.Simulation
{
    public enum EnvironmentHeroZoneKind { CoastalTakeoff, CliffRoute, IndustrialInspection }
    public enum EnvironmentCompositionLayer { Surface, Prop, Vegetation, Water, Vfx, Audio }

    [Serializable]
    public sealed class GoldenViewState
    {
        public string id;
        public string heroZoneId;
        public float distanceMeters;
        public Vector3 position;
        public Vector3 target;
        public float verticalFov = 55;
        public string reviewBand;
    }

    public sealed class EnvironmentHeroZoneMarker : MonoBehaviour
    {
        public const int CurrentVersion = 1;
        public int contractVersion = CurrentVersion;
        public string sliceLabel = "Procedural Vertical Slice";
        public string heroZoneId;
        public EnvironmentHeroZoneKind kind;
        public string sectorId;
        public Bounds reviewBounds;
    }

    public sealed class EnvironmentCompositionMarker : MonoBehaviour
    {
        public string heroZoneId;
        public EnvironmentCompositionLayer layer;
        public string semantic;
    }

    public sealed class EnvironmentGoldenViewMarker : MonoBehaviour
    {
        public string stateId;
        public string heroZoneId;
        public float distanceMeters;
        public Vector3 target;
        public float verticalFov;
    }

    [Serializable]
    public sealed class ProceduralVerticalSliceDiagnostics
    {
        public const string RequiredLabel = "Procedural Vertical Slice";
        public string label = RequiredLabel;
        public string weather;
        public string quality;
        public int heroZones;
        public int compositionLayers;
        public int goldenViewStates;
        public int audioLayers;
        public int vfxSystems;
        public int physicalColliders;
        public int readableLandmarks;
        public int lodBands;
        public List<string> materialSemantics = new();
        public List<string> errors = new();
        public bool IsValid => errors.Count == 0;
    }

    public sealed partial class FlightSimulator
    {
        private static readonly float[] GoldenDistances = { 1, 5, 15, 40, 80 };
        private readonly List<GoldenViewState> _environmentGoldenViews = new();
        private readonly List<AudioClip> _sliceAudioClips = new();
        private ProceduralVerticalSliceDiagnostics _verticalSliceDiagnostics;

        public ProceduralVerticalSliceDiagnostics VerticalSliceDiagnostics => _verticalSliceDiagnostics;
        public IReadOnlyList<GoldenViewState> EnvironmentGoldenViews => _environmentGoldenViews;
        public static bool SupportsProceduralSliceVariant(string weather, int quality) =>
            new[] { "Clear", "Overcast", "Rain" }.Contains(weather) &&
            (quality == AeroSimQuality.Low || quality == AeroSimQuality.High || quality == AeroSimQuality.Ultra);

        private void BuildProceduralVerticalSlice(Material oceanMaterial)
        {
            _verticalSliceDiagnostics = new ProceduralVerticalSliceDiagnostics
            {
                weather = _environmentState.weatherId,
                quality = AeroSimQuality.Name(_environmentQuality)
            };
            _environmentGoldenViews.Clear();
            if (_visualEnvironment != "Pacific Coast")
            {
                _verticalSliceDiagnostics.errors.Add("RD-08 vertical slice is authored only for Pacific Coast.");
                return;
            }

            var root = new GameObject("PROCEDURAL VERTICAL SLICE · RD-08 · NOT SCANNED").transform;
            root.SetParent(_world.transform, false);

            var soil = HeroSurface(new Color(.32f, .29f, .20f), 0, 2.2f, .08f, "soil");
            var rock = HeroSurface(new Color(.31f, .34f, .33f), 1, 3.6f, .12f, "coastal-rock");
            var concrete = HeroSurface(new Color(.52f, .53f, .49f), 2, 2.8f, .18f, "weathered-concrete");
            var steel = HeroSurface(new Color(.19f, .22f, .22f), 3, 1.2f, .32f, "galvanized-steel");
            var vegetation = HeroSurface(new Color(.25f, .36f, .20f), 4, .7f, .05f, "coastal-vegetation");
            var marking = HeroSurface(new Color(.86f, .64f, .16f), 5, 1, .23f, "safety-marking");
            var tidePool = CoastalDetailSurface(0, "tide-pool");
            var wetBand = CoastalDetailSurface(1, "intertidal-wet-band");
            var breakerFoam = CoastalDetailSurface(2, "shore-breaker-foam");

            BuildCoastalTakeoffHero(root, soil, concrete, steel, vegetation, marking);
            BuildCliffRouteHero(root, rock, steel, vegetation, oceanMaterial, tidePool, wetBand, breakerFoam);
            BuildIndustrialInspectionHero(root, concrete, steel, marking, vegetation);
            BuildFlightSafetyEnvelope(root, steel, marking);
            BuildSliceAudio(root);
            CompleteCoastProductionIntegration();
            ValidateVerticalSlice(root);
        }

        private Material HeroSurface(Color color, int semantic, float scale, float smoothness, string id)
        {
            var shader = Resources.Load<Shader>("HeroSurfaceV1");
            var material = shader == null ? EnvironmentSurfaceV1(color, semantic == 3 ? 1 : 0, null, scale) : new Material(shader);
            if (shader != null)
            {
                material.color = color;
                material.enableInstancing = true;
                material.SetFloat("_Semantic", semantic);
                material.SetFloat("_Scale", scale);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Wetness", _environmentState.wetness);
                material.SetFloat("_Puddle", _environmentState.puddleAmount);
                material.SetFloat("_CloudCover", _environmentState.cloudAmount);
                material.SetFloat("_Quality", _environmentQuality == AeroSimQuality.Low ? 0 : _environmentQuality == AeroSimQuality.Ultra ? 1 : .68f);
                var candidateTexture = CoastTexture(id);
                if (candidateTexture != null)
                {
                    material.SetTexture("_BaseMap", candidateTexture);
                    material.SetFloat("_TextureStrength", semantic == 4 ? .82f : .68f);
                }
                if (_environmentQuality >= AeroSimQuality.High) material.EnableKeyword("AEROSIM_HERO_HIGH");
                if (_environmentQuality >= AeroSimQuality.Ultra) material.EnableKeyword("AEROSIM_HERO_ULTRA");
                _materials.Add(material);
                RegisterSurface(material, "surface.hero." + id,
                    semantic == 0 ? EnvironmentSurfaceKind.Terrain : semantic == 1 ? EnvironmentSurfaceKind.Rock :
                    semantic == 3 ? EnvironmentSurfaceKind.Metal : semantic == 4 ? EnvironmentSurfaceKind.Vegetation :
                    semantic == 5 ? EnvironmentSurfaceKind.Marking : EnvironmentSurfaceKind.Concrete,
                    scale, true, semantic != 4, "constant-pbr-procedural");
            }
            _verticalSliceDiagnostics.materialSemantics.Add(id);
            return material;
        }

        private Material CoastalDetailSurface(int mode, string id)
        {
            var shader = Resources.Load<Shader>("CoastalWaterDetail");
            var material = shader == null ? HeroSurface(new Color(.08f, .27f, .28f), 1, 1.4f, .62f, id + "-fallback") : new Material(shader);
            if (shader != null)
            {
                material.enableInstancing = true;
                material.SetFloat("_Mode", mode);
                material.SetFloat("_Wetness", _environmentState.wetness);
                material.SetFloat("_Wind", Mathf.Clamp01(_environmentState.windSpeedMps / 9f));
                material.SetFloat("_Quality", _environmentQuality == AeroSimQuality.Low ? .25f : _environmentQuality == AeroSimQuality.Ultra ? 1 : .68f);
                var detailAtlas = CoastTexture(id);
                if (detailAtlas != null) material.SetTexture("_DetailAtlas", detailAtlas);
                if (_environmentQuality >= AeroSimQuality.High) material.EnableKeyword("AEROSIM_COAST_HIGH");
                if (_environmentQuality >= AeroSimQuality.Ultra) material.EnableKeyword("AEROSIM_COAST_ULTRA");
                _materials.Add(material);
                RegisterSurface(material, "surface.coast." + id, EnvironmentSurfaceKind.Water, 1.2f, true, true, "procedural-coastal-detail");
                _verticalSliceDiagnostics.materialSemantics.Add(id);
            }
            return material;
        }

        private Transform HeroZone(Transform parent, string id, EnvironmentHeroZoneKind kind, Vector3 center, Vector3 size)
        {
            var zone = new GameObject("HERO ZONE · " + id).transform;
            zone.SetParent(parent, false);
            zone.localPosition = center;
            var marker = zone.gameObject.AddComponent<EnvironmentHeroZoneMarker>();
            marker.heroZoneId = id;
            marker.kind = kind;
            marker.sectorId = SectorId(center);
            marker.reviewBounds = new Bounds(center, size);
            RegisterEnvironmentObject(zone.gameObject, false);
            _verticalSliceDiagnostics.heroZones++;
            AddGoldenViews(zone, id, center + Vector3.up * Mathf.Max(1.2f, size.y * .3f));
            return zone;
        }

        private Transform Layer(Transform zone, EnvironmentCompositionLayer layer, string semantic)
        {
            var value = new GameObject(layer.ToString().ToUpperInvariant() + " · " + semantic).transform;
            value.SetParent(zone, false);
            var marker = value.gameObject.AddComponent<EnvironmentCompositionMarker>();
            marker.heroZoneId = zone.GetComponent<EnvironmentHeroZoneMarker>().heroZoneId;
            marker.layer = layer;
            marker.semantic = semantic;
            _verticalSliceDiagnostics.compositionLayers++;
            return value;
        }

        private void BuildCoastalTakeoffHero(Transform parent, Material soil, Material concrete, Material steel, Material vegetation, Material marking)
        {
            var zone = HeroZone(parent, "coastal-takeoff", EnvironmentHeroZoneKind.CoastalTakeoff, Vector3.zero, new Vector3(54, 12, 58));
            var surface = Layer(zone, EnvironmentCompositionLayer.Surface, "raised anti-slip flight deck / compacted aggregate transition");
            var ground = new EnvironmentBatchV1();
            ground.Box(new Vector3(0, .035f, 0), new Vector3(29, .07f, 29));
            ground.Box(new Vector3(0, .22f, 13), new Vector3(18, .44f, 12));
            ground.Box(new Vector3(0, .45f, 13), new Vector3(16, .16f, 10));
            PlaceHeroBatch("Certified takeoff apron and raised deck", ground, concrete, surface, true);

            var props = Layer(zone, EnvironmentCompositionLayer.Prop, "perimeter frame / tie-down eyes / edge lights / safety markings");
            var frame = new EnvironmentBatchV1();
            frame.Box(new Vector3(0, .36f, 7.9f), new Vector3(18.8f, .42f, .24f));
            frame.Box(new Vector3(0, .36f, 18.1f), new Vector3(18.8f, .42f, .24f));
            frame.Box(new Vector3(-9.3f, .36f, 13), new Vector3(.24f, .42f, 10.4f));
            frame.Box(new Vector3(9.3f, .36f, 13), new Vector3(.24f, .42f, 10.4f));
            for (int i = -3; i <= 3; i++) frame.Box(new Vector3(i * 2.25f, .56f, 13), new Vector3(.08f, .025f, 9.6f));
            for (int side = -1; side <= 1; side += 2)
            {
                frame.Box(new Vector3(side * 8, .75f, 13), new Vector3(.18f, .36f, .18f));
                frame.Box(new Vector3(side * 10.5f, .62f, -7.5f), new Vector3(.18f, 1.25f, .18f));
                frame.Box(new Vector3(side * 10.5f, 1.2f, -7.5f), new Vector3(1.9f, .1f, .1f));
            }
            PlaceHeroBatch("Takeoff deck frame grating and hardware", frame, steel, props, true);

            var stripes = new EnvironmentBatchV1();
            const int ringSegments = 24;
            for (int i = 0; i < ringSegments; i++)
            {
                float a = i * Mathf.PI * 2 / ringSegments;
                stripes.Box(new Vector3(Mathf.Cos(a) * 3.8f, .555f, 13 + Mathf.Sin(a) * 3.8f), new Vector3(.85f, .018f, .22f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
            }
            stripes.Box(new Vector3(-1.25f, .558f, 13), new Vector3(.42f, .02f, 4.2f));
            stripes.Box(new Vector3(1.25f, .558f, 13), new Vector3(.42f, .02f, 4.2f));
            stripes.Box(new Vector3(0, .558f, 13), new Vector3(2.6f, .02f, .42f));
            for (int i = -4; i <= 4; i++) stripes.Box(new Vector3(i * 2.05f, .57f, 7.55f), new Vector3(1.02f, .02f, .72f), Quaternion.Euler(0, -32, 0));
            PlaceHeroBatch("Takeoff H ring and exclusion chevrons", stripes, marking, props);

            var drainage = Layer(zone, EnvironmentCompositionLayer.Water, "perimeter drainage / deterministic rain puddle response");
            var channels = new EnvironmentBatchV1();
            for (int side = -1; side <= 1; side += 2) channels.Box(new Vector3(side * 13.2f, .075f, 0), new Vector3(.65f, .02f, 26));
            PlaceHeroBatch("Takeoff drainage channels", channels, steel, drainage, true);
            BuildHeroVegetation(zone, vegetation, new Vector3(15, 0, 4), _environmentQuality == AeroSimQuality.Low ? 14 : 26, 13);
            BuildHeroMist(zone, "salt spray at takeoff edge", new Vector3(-20, 1.2f, 5), new Vector3(9, 1, 16), .25f);
            Layer(zone, EnvironmentCompositionLayer.Audio, "coastal wind / raised deck resonance");
            _verticalSliceDiagnostics.readableLandmarks++;
        }

        private void BuildCliffRouteHero(Transform parent, Material rock, Material steel, Material vegetation, Material water, Material tidePool, Material wetBand, Material breakerFoam)
        {
            var center = new Vector3(-72, GroundHeight(-72, 112), 112);
            var zone = HeroZone(parent, "cliff-route", EnvironmentHeroZoneKind.CliffRoute, center, new Vector3(72, 38, 120));
            var surface = Layer(zone, EnvironmentCompositionLayer.Surface, "fractured strata / undercut shelves / talus / sea stacks");
            var strata = new EnvironmentBatchV1();
            int bands = _environmentQuality == AeroSimQuality.Low ? 6 : _environmentQuality == AeroSimQuality.Ultra ? 14 : 10;
            for (int i = 0; i < bands; i++)
            {
                float stagger = Mathf.Sin(i * 2.17f) * 2.1f;
                strata.Box(new Vector3(-5 - i * .82f + stagger, i * 1.48f - 2, i * 7 - 34), new Vector3(18 + i * 1.75f, 1.1f + (i % 3) * .18f, 10.5f + (i % 2) * 3.2f), Quaternion.Euler(i * .8f, -12 + i * 4.7f, (i % 2 == 0 ? -1 : 1) * 2.2f));
                if (i % 2 == 0) strata.Box(new Vector3(-17 - i * .28f, i * 1.48f - 2.65f, i * 7 - 31), new Vector3(5.8f, .42f, 7.5f), Quaternion.Euler(0, i * 7, 4));
            }
            int talus = _environmentQuality == AeroSimQuality.Low ? 8 : 18;
            var random = new System.Random(_environmentState.seed ^ 711);
            for (int i = 0; i < talus; i++)
            {
                float s = .55f + (float)random.NextDouble() * 1.7f;
                strata.Box(new Vector3(-12 - (float)random.NextDouble() * 13, -2 + (float)random.NextDouble() * 3, -38 + (float)random.NextDouble() * 76), new Vector3(s * 2.2f, s, s * 1.6f), Quaternion.Euler(random.Next(-18, 19), random.Next(360), random.Next(-15, 16)));
            }
            PlaceHeroBatch("Cliff fractured geological strata and talus", strata, rock, surface, true);
            // P11 is preferred and validated fail-closed. P01 remains the conservative visual fallback.
            if (!AddP11HeroCliff(surface)) AddP01CandidateGeometry(surface);
            BuildErodedSeaStack(zone, surface, rock, new Vector3(-30, -2.4f, -19), 7.5f, 19, 733);
            BuildErodedSeaStack(zone, surface, rock, new Vector3(-37, -3.1f, 19), 5.2f, 13, 761);

            var props = Layer(zone, EnvironmentCompositionLayer.Prop, "cantilever inspection walk / guardrail / route beacon");
            var route = new EnvironmentBatchV1();
            for (int i = 0; i < 9; i++)
            {
                float z = -38 + i * 10;
                float y = 2 + i * .18f;
                route.Box(new Vector3(8, y, z), new Vector3(3.3f, .22f, 8.8f));
                route.Box(new Vector3(6.5f, y + 1.05f, z), new Vector3(.12f, 2.1f, .12f));
                route.Box(new Vector3(9.5f, y + 1.05f, z), new Vector3(.12f, 2.1f, .12f));
                route.Box(new Vector3(6.5f, y + 1.72f, z), new Vector3(.1f, .1f, 9.6f));
                route.Box(new Vector3(9.5f, y + 1.72f, z), new Vector3(.1f, .1f, 9.6f));
            }
            route.Box(new Vector3(8, 5.2f, 44), new Vector3(.32f, 6.4f, .32f));
            route.Box(new Vector3(8, 8.25f, 44), new Vector3(2.2f, .22f, 2.2f));
            PlaceHeroBatch("Cliff safety walk guardrails and navigation beacon", route, steel, props, true);
            _verticalSliceDiagnostics.readableLandmarks++;

            BuildHeroVegetation(zone, vegetation, new Vector3(13, 1, 5), _environmentQuality == AeroSimQuality.Low ? 14 : _environmentQuality == AeroSimQuality.Ultra ? 42 : 30, 29);
            var waterLayer = Layer(zone, EnvironmentCompositionLayer.Water, "tide pools / glossy intertidal band / breaker foam / shallow transition");
            var pools = new EnvironmentBatchV1();
            var wet = new EnvironmentBatchV1();
            var foam = new EnvironmentBatchV1();
            int poolCount = _environmentQuality == AeroSimQuality.Low ? 5 : 10;
            for (int i = 0; i < poolCount; i++)
            {
                float z = -38 + i * (76f / Mathf.Max(1, poolCount - 1));
                pools.Box(new Vector3(-18 - (i % 3) * 2.2f, -2.05f + (i % 2) * .08f, z), new Vector3(4.8f + (i % 2) * 2.1f, .035f, 5.2f + (i % 3)));
                wet.Box(new Vector3(-22.5f, -2.18f, z), new Vector3(9.5f, .028f, 6.8f));
                foam.Box(new Vector3(-28.5f - (i % 2) * 1.8f, -1.92f, z), new Vector3(3.8f, .032f, 6.3f), Quaternion.Euler(0, -8 + i * 5, 0));
            }
            PlaceHeroBatch("Reflective intertidal tide pools", pools, tidePool ?? water ?? rock, waterLayer);
            PlaceHeroBatch("Dark algae-rich wet rock band", wet, wetBand ?? rock, waterLayer);
            PlaceHeroBatch("Animated shore-breaker foam ribbons", foam, breakerFoam ?? water ?? rock, waterLayer);
            BuildHeroMist(zone, "breaker foam and cliff mist", new Vector3(-25, -.5f, 3), new Vector3(12, 2.5f, 74), .75f);
            Layer(zone, EnvironmentCompositionLayer.Audio, "shore breakers / cable wind / cliff occlusion");
        }

        private void BuildIndustrialInspectionHero(Transform parent, Material concrete, Material steel, Material marking, Material vegetation)
        {
            var center = new Vector3(122, GroundHeight(122, 122), 122);
            var zone = HeroZone(parent, "industrial-inspection", EnvironmentHeroZoneKind.IndustrialInspection, center, new Vector3(74, 32, 82));
            var surface = Layer(zone, EnvironmentCompositionLayer.Surface, "weathered service slab / drainage / oil-darkened joints");
            var slab = new EnvironmentBatchV1();
            slab.Box(new Vector3(0, .08f, 0), new Vector3(58, .16f, 64));
            for (int i = -3; i <= 3; i++) slab.Box(new Vector3(i * 8, .17f, 0), new Vector3(.06f, .025f, 62));
            PlaceHeroBatch("Inspection yard slab and joints", slab, concrete, surface, true);

            var props = Layer(zone, EnvironmentCompositionLayer.Prop, "low-profile instrument shelter / pipe rack / access walk / guardrail / beacon");
            var plant = new EnvironmentBatchV1();
            plant.Box(new Vector3(-10, 3.1f, 8), new Vector3(12, 6.2f, 9));
            plant.Box(new Vector3(-10, 6.45f, 8), new Vector3(12.8f, .5f, 9.8f));
            for (int leg = -1; leg <= 1; leg += 2)
            {
                plant.Box(new Vector3(leg * 14, 4.5f, -8), new Vector3(.5f, 9, .5f));
                plant.Box(new Vector3(leg * 14, 9.1f, -8), new Vector3(3.4f, .45f, 3.4f));
            }
            plant.Box(new Vector3(0, 8.4f, -8), new Vector3(28, .45f, 3.2f));
            plant.Box(new Vector3(0, 9.25f, -9.45f), new Vector3(28, .1f, .1f));
            for (int i = 0; i < 8; i++) plant.Box(new Vector3(-14 + i * 4, 8.95f, -9.45f), new Vector3(.1f, 1.5f, .1f));
            plant.Box(new Vector3(18, 5.5f, 8), new Vector3(.38f, 11, .38f));
            plant.Box(new Vector3(18, 11.2f, 8), new Vector3(2.6f, .32f, 2.6f));
            for (int i = 0; i < 6; i++) plant.Box(new Vector3(-14 + i * 5.6f, 4.4f, -8), new Vector3(.22f, 7.8f, .22f));
            PlaceHeroBatch("Coastal inspection station and guarded access walk", plant, steel, props, true);
            _verticalSliceDiagnostics.readableLandmarks++;

            var markings = new EnvironmentBatchV1();
            for (int i = 0; i < 12; i++) markings.Box(new Vector3(-23 + i * 4.2f, .19f, -23), new Vector3(2.1f, .018f, .7f), Quaternion.Euler(0, -35, 0));
            markings.Box(new Vector3(18, 11.39f, 8), new Vector3(.75f, .04f, .75f));
            PlaceHeroBatch("Inspection exclusion hatching and beacon", markings, marking, props);
            var drainage = Layer(zone, EnvironmentCompositionLayer.Water, "runoff channel / rain-state puddles");
            var channels = new EnvironmentBatchV1();
            channels.Box(new Vector3(24, .185f, 0), new Vector3(1.1f, .025f, 58));
            for (int i = -4; i <= 4; i++) channels.Box(new Vector3(24, .205f, i * 6.3f), new Vector3(1.05f, .018f, .12f));
            PlaceHeroBatch("Industrial runoff drain", channels, steel, drainage, true);
            BuildHeroVegetation(zone, vegetation, new Vector3(25, 0, 14), _environmentQuality == AeroSimQuality.Low ? 7 : 16, 41);
            BuildHeroMist(zone, "pipe vent and rain backscatter", new Vector3(18, 10.5f, 8), new Vector3(3, 3, 3), .34f);
            Layer(zone, EnvironmentCompositionLayer.Audio, "industrial hum / metal resonance / rain impacts");
        }

        private void BuildHeroVegetation(Transform zone, Material material, Vector3 offset, int count, int seed)
        {
            var layer = Layer(zone, EnvironmentCompositionLayer.Vegetation, "deterministic salt-grass / heather / wind-pruned scrub communities");
            var grass = new EnvironmentBatchV1();
            var scrub = new EnvironmentBatchV1();
            var random = new System.Random(_environmentState.seed ^ seed);
            for (int i = 0; i < count; i++)
            {
                float clusterAngle = (i % 5) * Mathf.PI * .4f;
                float clusterRadius = 2.2f + (i % 7) * .7f;
                float x = offset.x + Mathf.Cos(clusterAngle) * clusterRadius + ((float)random.NextDouble() - .5f) * 8;
                float z = offset.z + Mathf.Sin(clusterAngle) * clusterRadius * 1.6f + ((float)random.NextDouble() - .5f) * 17;
                float h = .22f + (float)random.NextDouble() * .78f;
                var target = i % 6 == 0 ? scrub : grass;
                target.Box(new Vector3(x, offset.y + h * .5f, z), new Vector3(.07f + h * .1f, h, .07f), Quaternion.Euler(0, random.Next(360), random.Next(-10, 11)));
                if (i % 6 == 0)
                {
                    target.Box(new Vector3(x + .18f, offset.y + h * .42f, z - .1f), new Vector3(.38f, h * .48f, .28f), Quaternion.Euler(random.Next(-8, 9), random.Next(360), random.Next(-8, 9)));
                    target.Box(new Vector3(x - .14f, offset.y + h * .36f, z + .16f), new Vector3(.32f, h * .42f, .36f), Quaternion.Euler(random.Next(-8, 9), random.Next(360), random.Next(-8, 9)));
                }
            }
            PlaceHeroBatch("Salt grass community", grass, material, layer);
            PlaceHeroBatch("Wind-pruned coastal scrub community", scrub, material, layer);
        }

        private void BuildErodedSeaStack(Transform zone, Transform layer, Material material, Vector3 position, float radius, float height, int salt)
        {
            int sides = _environmentQuality == AeroSimQuality.Low ? 7 : _environmentQuality == AeroSimQuality.Ultra ? 15 : 11;
            const int rings = 6;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var random = new System.Random(_environmentState.seed ^ salt);
            for (int ring = 0; ring < rings; ring++)
            {
                float y = height * ring / (rings - 1f);
                float normalized = ring / (rings - 1f);
                float profile = Mathf.Lerp(1.08f, .48f, normalized) * (ring == 1 ? .76f : ring == 2 ? 1.02f : 1);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    float fracture = .78f + (float)random.NextDouble() * .38f;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius * profile * fracture, y + ((side + ring) % 3) * .08f, Mathf.Sin(angle) * radius * profile * fracture));
                }
            }
            for (int ring = 0; ring < rings - 1; ring++) for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int a = ring * sides + side, b = ring * sides + next, c = (ring + 1) * sides + side, d = (ring + 1) * sides + next;
                triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
            var mesh = KeepMesh(new Mesh { name = "Eroded sea stack silhouette", vertices = vertices.ToArray(), triangles = triangles.ToArray() });
            MeshObject("Asymmetric undercut sea stack", mesh, position, Vector3.one, material, layer, true);
            _verticalSliceDiagnostics.physicalColliders++;
        }

        private void BuildFlightSafetyEnvelope(Transform root, Material steel, Material marking)
        {
            var boundary = new GameObject("PHYSICAL FLIGHT BOUNDARY · safety critical · 640x640x180m").transform;
            boundary.SetParent(root, false);
            var walls = new[]
            {
                new Bounds(new Vector3(-320, 90, 0), new Vector3(2, 180, 640)),
                new Bounds(new Vector3(320, 90, 0), new Vector3(2, 180, 640)),
                new Bounds(new Vector3(0, 90, -320), new Vector3(640, 180, 2)),
                new Bounds(new Vector3(0, 90, 320), new Vector3(640, 180, 2)),
                new Bounds(new Vector3(0, 181, 0), new Vector3(640, 2, 640))
            };
            foreach (var wall in walls)
            {
                var value = new GameObject("Flight boundary collider");
                value.transform.SetParent(boundary, false);
                value.transform.localPosition = wall.center;
                var collider = value.AddComponent<BoxCollider>();
                collider.size = wall.size;
                RegisterEnvironmentObject(value, true);
                _verticalSliceDiagnostics.physicalColliders++;
            }
            _verticalSliceDiagnostics.lodBands = GoldenDistances.Length;
        }

        private void BuildHeroMist(Transform zone, string semantic, Vector3 position, Vector3 volume, float intensity)
        {
            var layer = Layer(zone, EnvironmentCompositionLayer.Vfx, semantic);
            if (_vfxMode == EnvironmentFeatureMode.Off) return;
            var go = new GameObject("VFX · " + semantic);
            go.transform.SetParent(layer, false);
            go.transform.localPosition = position;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true; main.duration = 3; main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.15f, .7f); main.startSize = new ParticleSystem.MinMaxCurve(.05f, .18f);
            main.maxParticles = _environmentQuality >= AeroSimQuality.Ultra ? 260 : _environmentQuality >= AeroSimQuality.High ? 150 : 55;
            var emission = particles.emission; emission.rateOverTime = intensity * main.maxParticles * (.35f + _environmentState.vfxDensity * .65f);
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = volume;
            var velocity = particles.velocityOverLifetime; velocity.enabled = true;
            float windX = _environmentState.windDirection.x * .6f;
            float windZ = _environmentState.windDirection.z * .6f;
            velocity.x = new ParticleSystem.MinMaxCurve(windX, windX);
            velocity.y = new ParticleSystem.MinMaxCurve(.08f, .35f);
            velocity.z = new ParticleSystem.MinMaxCurve(windZ, windZ);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = .4f; renderer.shadowCastingMode = ShadowCastingMode.Off;
            var shader = Resources.Load<Shader>("RainStreak");
            if (shader != null) { var material = new Material(shader); material.SetFloat("_Opacity", .08f + _environmentState.precipitation * .12f); _materials.Add(material); renderer.sharedMaterial = material; }
            particles.randomSeed = unchecked((uint)EnvironmentState.StableSeed(semantic, _environmentState.weatherId, _environmentState.timeOfDay, _environmentState.windSpeedMps)); particles.useAutoRandomSeed = false; particles.Play();
            _verticalSliceDiagnostics.vfxSystems++;
        }

        private void BuildSliceAudio(Transform root)
        {
            var layer = new GameObject("AUDIO · deterministic layered soundscape").transform;
            layer.SetParent(root, false);
            var marker = layer.gameObject.AddComponent<EnvironmentCompositionMarker>();
            marker.heroZoneId = "global-slice"; marker.layer = EnvironmentCompositionLayer.Audio; marker.semantic = _environmentState.audioSnapshot;
            _verticalSliceDiagnostics.compositionLayers++;
            if (_environmentAudioMode == EnvironmentFeatureMode.Off || LocalStore.Profile.settings.volume <= 0) return;
            AddAudioLayer(layer, "shore-breakers", new Vector3(-72, 1, 112), .11f, 83);
            AddAudioLayer(layer, "coastal-wind", new Vector3(0, 5, 20), .055f, 127);
            AddAudioLayer(layer, "industrial-hum", new Vector3(122, 8, 122), .045f, 191);
            if (_environmentQuality >= AeroSimQuality.High)
            {
                AddAudioLayer(layer, "cable-and-grass-wind", new Vector3(-58, 8, 112), .035f, 239);
                AddAudioLayer(layer, "inspection-metal-resonance", new Vector3(122, 15, 108), .028f, 277);
            }
            if (_environmentQuality >= AeroSimQuality.Ultra) AddAudioLayer(layer, "distant-surf-tail", new Vector3(-110, 1, 20), .024f, 313);
        }

        private void AddAudioLayer(Transform parent, string name, Vector3 position, float gain, int salt)
        {
            const int sampleRate = 8000, sampleCount = 8000;
            var samples = new float[sampleCount];
            var random = new System.Random(_environmentState.seed ^ salt); float low = 0, phase = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                float white = (float)random.NextDouble() * 2 - 1;
                low = Mathf.Lerp(low, white, name.Contains("hum") || name.Contains("metal") ? .012f : .065f);
                phase += name.Contains("hum") ? .031f : .009f;
                samples[i] = (low * .7f + Mathf.Sin(phase) * (name.Contains("hum") ? .22f : .04f)) * (.55f + _environmentState.precipitation * .25f);
            }
            var clip = AudioClip.Create("RD08 " + name + " · procedural", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0); _sliceAudioClips.Add(clip);
            var go = new GameObject("Audio layer · " + name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var source = go.AddComponent<AudioSource>(); source.clip = clip; source.loop = true; source.spatialBlend = .82f;
            source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 6; source.maxDistance = 95;
            source.volume = LocalStore.Profile.settings.volume * gain * (_environmentState.precipitation > .5f && name.Contains("shore") ? 1.2f : 1); source.Play();
            _verticalSliceDiagnostics.audioLayers++;
        }

        private void AddGoldenViews(Transform zone, string zoneId, Vector3 target)
        {
            foreach (float distance in GoldenDistances)
            {
                // Golden cameras look down into the zone from a safe elevated orbit. The previous positive-Y
                // direction put the camera below the target (A/15m was effectively at ground level).
                Vector3 direction = zoneId == "cliff-route" ? new Vector3(.68f, -.34f, -.65f).normalized : new Vector3(-.55f, -.35f, -.76f).normalized;
                var state = new GoldenViewState
                {
                    id = zoneId + "." + distance.ToString("0") + "m",
                    heroZoneId = zoneId,
                    distanceMeters = distance,
                    target = target,
                    position = target - direction * distance,
                    verticalFov = distance <= 5 ? 50 : distance >= 40 ? 60 : 55,
                    reviewBand = distance <= 1 ? "contact-material" : distance <= 5 ? "near-silhouette" : distance <= 15 ? "hero-composition" : distance <= 40 ? "navigation-landmark" : "sector-readability"
                };
                _environmentGoldenViews.Add(state);
                var markerObject = new GameObject("GOLDEN VIEW · " + state.id);
                markerObject.transform.SetParent(zone, true); markerObject.transform.position = state.position;
                markerObject.transform.rotation = Quaternion.LookRotation(state.target - state.position, Vector3.up);
                var marker = markerObject.AddComponent<EnvironmentGoldenViewMarker>();
                marker.stateId = state.id; marker.heroZoneId = zoneId; marker.distanceMeters = distance; marker.target = target; marker.verticalFov = state.verticalFov;
            }
        }

        private void PlaceHeroBatch(string name, EnvironmentBatchV1 batch, Material material, Transform parent, bool collision = false)
        {
            MeshObject(name, KeepMesh(batch.Mesh(name)), Vector3.zero, Vector3.one, material, parent, collision);
            if (collision) _verticalSliceDiagnostics.physicalColliders++;
        }

        private string SectorId(Vector3 position)
        {
            float size = _environmentPolicy.sectorSize;
            int x = Mathf.Clamp(Mathf.FloorToInt((position.x + size * .5f) / size), -1, 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((position.z + size * .5f) / size), -1, 1);
            return "sector." + x + "." + z;
        }

        private void ValidateVerticalSlice(Transform root)
        {
            _verticalSliceDiagnostics.goldenViewStates = _environmentGoldenViews.Count;
            if (_verticalSliceDiagnostics.label != ProceduralVerticalSliceDiagnostics.RequiredLabel) _verticalSliceDiagnostics.errors.Add("Honest procedural slice label is missing.");
            if (_verticalSliceDiagnostics.heroZones != 3) _verticalSliceDiagnostics.errors.Add("Exactly three hero zones are required.");
            if (_environmentGoldenViews.Count != 15 || _environmentGoldenViews.Select(v => v.distanceMeters).Distinct().Count() != 5) _verticalSliceDiagnostics.errors.Add("Each hero zone requires 1/5/15/40/80 m golden states.");
            foreach (var zone in root.GetComponentsInChildren<EnvironmentHeroZoneMarker>())
            {
                var layers = zone.GetComponentsInChildren<EnvironmentCompositionMarker>().Select(value => value.layer).Distinct().ToList();
                foreach (EnvironmentCompositionLayer required in Enum.GetValues(typeof(EnvironmentCompositionLayer)))
                    if (!layers.Contains(required)) _verticalSliceDiagnostics.errors.Add(zone.heroZoneId + " is missing " + required + ".");
                if (zone.sliceLabel != ProceduralVerticalSliceDiagnostics.RequiredLabel) _verticalSliceDiagnostics.errors.Add(zone.heroZoneId + " has an invalid provenance label.");
            }
            if (!new[] { "Clear", "Overcast", "Rain" }.Contains(_environmentState.weatherId)) _verticalSliceDiagnostics.errors.Add("Weather is outside the RD-08 matrix.");
            if (_environmentQuality != AeroSimQuality.Low && _environmentQuality != AeroSimQuality.Medium && _environmentQuality != AeroSimQuality.High && _environmentQuality != AeroSimQuality.Ultra) _verticalSliceDiagnostics.errors.Add("Quality is outside the supported matrix.");
            if (_verticalSliceDiagnostics.materialSemantics.Distinct().Count() < 9) _verticalSliceDiagnostics.errors.Add("Material semantics are incomplete.");
            if (_verticalSliceDiagnostics.compositionLayers < 19) _verticalSliceDiagnostics.errors.Add("Hierarchical surface/prop/vegetation/water/VFX/audio composition is incomplete.");
            if (_verticalSliceDiagnostics.physicalColliders < 12) _verticalSliceDiagnostics.errors.Add("Physical collision and flight boundary coverage is incomplete.");
            if (_verticalSliceDiagnostics.readableLandmarks < 3) _verticalSliceDiagnostics.errors.Add("Each hero zone requires a readable navigation landmark.");
            if (_verticalSliceDiagnostics.lodBands != GoldenDistances.Length) _verticalSliceDiagnostics.errors.Add("1/5/15/40/80 m detail review bands are incomplete.");
            if (_environmentAudioMode != EnvironmentFeatureMode.Off && LocalStore.Profile.settings.volume > 0)
            {
                int expectedAudio = _environmentQuality >= AeroSimQuality.Ultra ? 6 : _environmentQuality >= AeroSimQuality.High ? 5 : 3;
                if (_verticalSliceDiagnostics.audioLayers != expectedAudio) _verticalSliceDiagnostics.errors.Add("Quality audio layer budget is incomplete.");
            }
            int expectedVfx = _vfxMode == EnvironmentFeatureMode.Off ? 0 : 3;
            if (_verticalSliceDiagnostics.vfxSystems != expectedVfx) _verticalSliceDiagnostics.errors.Add("Quality VFX layer budget is incomplete.");
        }

        private void ReleaseVerticalSliceResources()
        {
            foreach (var clip in _sliceAudioClips) if (clip != null) Destroy(clip);
            _sliceAudioClips.Clear();
            _environmentGoldenViews.Clear();
            _verticalSliceDiagnostics = null;
        }
    }
}
