using System.Collections.Generic;
using AeroSim.Domain;
using UnityEngine;

namespace AeroSim.Simulation
{
    // Static, original 3D set dressing. The 0.4 collider layout and mission volumes remain authoritative.
    public sealed partial class FlightSimulator
    {
        private Material EnvironmentSurfaceV1(Color tint, int kind, string texture = null, float tile = 3)
        {
            var shader = Resources.Load<Shader>("IndustrialSurfaceV1");
            if (shader == null) return Landscape(tint, "coastal-rock-v2", tile, .06f);
            var material = new Material(shader) { color = tint, enableInstancing = true };
            _materials.Add(material);
            material.SetFloat("_Kind", kind);
            material.SetFloat("_Scale", tile);
            material.SetFloat("_Metallic", kind == 1 ? .52f : 0);
            material.SetFloat("_Smoothness", kind == 1 ? .3f : .18f);
            material.SetFloat("_Wetness", _rainAmount);
            material.SetFloat("_Detail", _environmentQuality >= AeroSimQuality.Ultra ? 1 : _environmentQuality >= AeroSimQuality.High ? .72f : 0);
            // New tileable albedo may be supplied by the separate material artist. No menu painting is used.
            var albedo = texture == null ? null : Resources.Load<Texture2D>(texture);
            material.SetFloat("_UseTexture", albedo != null ? 1 : 0);
            if (albedo != null) material.SetTexture("_MainTex", albedo);
            RegisterSurface(material, "surface.environment." + kind + "." + (texture ?? "procedural"), kind == 1 ? EnvironmentSurfaceKind.Metal : EnvironmentSurfaceKind.Concrete, tile, true, true);
            return material;
        }

        private Material EnvironmentGlowV1(Color tint, float emission)
        {
            var material = EnvironmentSurfaceV1(tint, 1, null, 1);
            if (material.HasProperty("_Emission")) material.SetFloat("_Emission", emission);
            return material;
        }

        // Six independent faces keep crisp architectural normals; batching avoids hundreds of new renderers.
        private sealed class EnvironmentBatchV1
        {
            private readonly List<Vector3> _vertices = new();
            private readonly List<Vector2> _uv = new();
            private readonly List<int> _indices = new();

            public void Box(Vector3 center, Vector3 size, Quaternion? rotation = null)
            {
                var half = size * .5f;
                var rotate = rotation ?? Quaternion.identity;
                void Face(Vector3 normal, Vector3 right, Vector3 up, float plane, float width, float height)
                {
                    int first = _vertices.Count;
                    var origin = normal * plane;
                    _vertices.Add(center + rotate * (origin - right * width - up * height));
                    _vertices.Add(center + rotate * (origin + right * width - up * height));
                    _vertices.Add(center + rotate * (origin + right * width + up * height));
                    _vertices.Add(center + rotate * (origin - right * width + up * height));
                    _uv.Add(Vector2.zero); _uv.Add(Vector2.right); _uv.Add(Vector2.one); _uv.Add(Vector2.up);
                    _indices.Add(first); _indices.Add(first + 1); _indices.Add(first + 2);
                    _indices.Add(first); _indices.Add(first + 2); _indices.Add(first + 3);
                }
                Face(Vector3.forward, Vector3.right, Vector3.up, half.z, half.x, half.y);
                Face(Vector3.back, Vector3.left, Vector3.up, half.z, half.x, half.y);
                Face(Vector3.right, Vector3.back, Vector3.up, half.x, half.z, half.y);
                Face(Vector3.left, Vector3.forward, Vector3.up, half.x, half.z, half.y);
                Face(Vector3.up, Vector3.right, Vector3.back, half.y, half.x, half.z);
                Face(Vector3.down, Vector3.right, Vector3.forward, half.y, half.x, half.z);
            }

            public void Beam(Vector3 a, Vector3 b, float width, float depth = 0)
            {
                var delta = b - a;
                Box((a + b) * .5f, new Vector3(width, delta.magnitude, depth > 0 ? depth : width),
                    Quaternion.FromToRotation(Vector3.up, delta.normalized));
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (_vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(_vertices); mesh.SetUVs(0, _uv); mesh.SetTriangles(_indices, 0);
                return mesh;
            }
        }

        private void PlaceEnvironmentBatchV1(string name, EnvironmentBatchV1 batch, Material material)
        {
            MeshObject(name, KeepMesh(batch.Mesh(name)), Vector3.zero, Vector3.one, material);
        }

        private void BuildIndustrialDetailsV1(Material concrete, Material steel)
        {
            var concreteDetails = new EnvironmentBatchV1();
            var metal = new EnvironmentBatchV1();
            var rust = new EnvironmentBatchV1();
            var lights = new EnvironmentBatchV1();
            var markings = new EnvironmentBatchV1();
            for (int bay = 0; bay < 8; bay++)
            {
                float z = bay * 20 - 10;
                // All added posts are outside the six existing gate approach volumes.
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 39;
                    concreteDetails.Box(new Vector3(x, 13.5f, z), new Vector3(1.6f, 27, 1.6f));
                    concreteDetails.Box(new Vector3(x, .28f, z), new Vector3(3.1f, .55f, 3.1f));
                    metal.Box(new Vector3(x, 25.5f, z), new Vector3(1.9f, .18f, 1.9f));
                    metal.Beam(new Vector3(x, 23, z), new Vector3(x - side * 8, 28, z), .25f);
                    metal.Box(new Vector3(side * 41.5f, 21.5f, z), new Vector3(5, .3f, 20));
                    metal.Box(new Vector3(side * 38.8f, 22.9f, z), new Vector3(.1f, .1f, 20));
                    metal.Box(new Vector3(side * 38.8f, 22.1f, z), new Vector3(.1f, .1f, 20));
                    for (int rail = 0; rail < 5; rail++)
                        metal.Box(new Vector3(side * 38.8f, 22.25f, z - 8 + rail * 4), new Vector3(.12f, 1.5f, .12f));
                    // Vertical side-wall panels, clerestory glazing frames and rusty pipework.
                    metal.Box(new Vector3(side * 44.45f, 16, z), new Vector3(.14f, .1f, 19));
                    metal.Box(new Vector3(side * 44.45f, 6, z), new Vector3(.14f, .15f, 19));
                    rust.Box(new Vector3(side * 43.65f, 9, z), new Vector3(.25f, 18, .25f));
                    lights.Box(new Vector3(side * 37.95f, 18, z + 3), new Vector3(.16f, .7f, .45f));
                }
                // Roof lattice stays above the highest gate (top = 16 m), and RTH remains unchanged.
                metal.Box(new Vector3(0, 27.35f, z), new Vector3(78, .15f, .55f));
                metal.Box(new Vector3(0, 29.1f, z), new Vector3(78, .15f, .55f));
                for (int section = 0; section < 8; section++)
                {
                    float x = -38 + section * 9.5f;
                    metal.Beam(new Vector3(x, 27.45f, z), new Vector3(x + 4.75f, 29, z), .14f);
                    metal.Beam(new Vector3(x + 4.75f, 29, z), new Vector3(x + 9.5f, 27.45f, z), .14f);
                }
                markings.Box(new Vector3(-9, .115f, z), new Vector3(.12f, .015f, 14));
                markings.Box(new Vector3(9, .115f, z), new Vector3(.12f, .015f, 14));
            }
            // Catwalk at the far end is visible through successive race gates, not across the start pad.
            metal.Box(new Vector3(0, 22, 128), new Vector3(78, .35f, 3.8f));
            for (int side = -1; side <= 1; side += 2)
            {
                metal.Box(new Vector3(0, 23.35f, 128 + side * 1.8f), new Vector3(78, .1f, .1f));
                for (int i = 0; i < 22; i++) metal.Box(new Vector3(-37 + i * 3.5f, 22.65f, 128 + side * 1.8f), new Vector3(.1f, 1.4f, .1f));
            }
            // Rail ladders are placed alongside existing solid perimeter walls.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int rung = 0; rung < 40; rung++) rust.Box(new Vector3(side * 43, 1 + rung * .5f, 2), new Vector3(1, .08f, .12f));
                rust.Box(new Vector3(side * 43 - .5f, 11, 2), new Vector3(.08f, 21, .12f));
                rust.Box(new Vector3(side * 43 + .5f, 11, 2), new Vector3(.08f, 21, .12f));
            }
            // Cosmetic glowing strips belong to the actual collider gates and disappear when those gates do.
            if (Config.race || Config.task == "gates")
            {
                // Gates are built after this environment; attach once via a small lifecycle component.
                var glow = EnvironmentGlowV1(new Color(1, .35f, .055f), 2);
                _world.AddComponent<GateLightAttachV1>().Initialize(_world.transform, glow);
            }
            PlaceEnvironmentBatchV1("Industrial concrete supports · visual", concreteDetails, concrete);
            PlaceEnvironmentBatchV1("Industrial trusses and catwalks · visual", metal, steel);
            PlaceEnvironmentBatchV1("Aged service pipes and ladders · visual", rust, EnvironmentSurfaceV1(new Color(.44f,.18f,.075f), 1));
            PlaceEnvironmentBatchV1("Warm industrial luminaires", lights, EnvironmentGlowV1(new Color(1,.58f,.20f), 1.3f));
            PlaceEnvironmentBatchV1("Training corridor paint", markings, EnvironmentSurfaceV1(new Color(.82f,.66f,.26f), 0));
        }

        private void BuildBridgeDetailsV1(Material concrete, Material steel, Vector3 sunlight, bool evening)
        {
            var beams = new EnvironmentBatchV1();
            var stone = new EnvironmentBatchV1();
            var seams = new EnvironmentBatchV1();
            for (int pier = 0; pier < 4; pier++)
            {
                float x = pier * 27 - 40;
                stone.Box(new Vector3(x, .6f, 55), new Vector3(8.3f, 1.15f, 9.4f));
                stone.Box(new Vector3(x, 20.4f, 55), new Vector3(6.3f, .7f, 7.3f));
                for (int row = 1; row < 9; row++)
                    for (int side = -1; side <= 1; side += 2)
                        seams.Box(new Vector3(x, row * 2.35f, 55 + side * 3.008f), new Vector3(4.98f, .025f, .025f));
                for (int side = -1; side <= 1; side += 2)
                {
                    beams.Box(new Vector3(x, 22.68f, 55 + side * 3.9f), new Vector3(8.1f, .16f, 1.1f));
                    // Rust-coloured waterline is a physical, narrow material strip, not a billboard.
                    seams.Box(new Vector3(x, .46f, 55 + side * 4.715f), new Vector3(8.25f, .22f, .02f));
                }
            }
            for (int girder = 0; girder < 6; girder++)
                beams.Box(new Vector3(0, 22.63f, 49.5f + girder * 2.2f), new Vector3(109.8f, .7f, .36f));
            for (int joint = 0; joint < 15; joint++)
            {
                float x = -53 + joint * 7.5f;
                seams.Box(new Vector3(x, 25.57f, 55), new Vector3(.06f, .012f, 12.35f));
                beams.Box(new Vector3(x, 22.1f, 55), new Vector3(.24f, .45f, 12.5f));
            }
            PlaceEnvironmentBatchV1("Bridge footings and bearing plinths · visual", stone, concrete);
            PlaceEnvironmentBatchV1("Bridge underside girders and bearings · visual", beams, steel);
            PlaceEnvironmentBatchV1("Bridge construction joints and tidal wear · visual", seams, EnvironmentSurfaceV1(new Color(.20f,.22f,.21f), 0));

            var waterShader = Resources.Load<Shader>("RiverSurfaceV1");
            if (waterShader != null && _waterMode != EnvironmentFeatureMode.Off)
            {
                var water = new Material(waterShader); _materials.Add(water);
                RegisterSurface(water, "surface.water.river", EnvironmentSurfaceKind.Water, 2, true, true, "probe-normal");
                water.SetVector("_SunDirection", sunlight);
                water.SetColor("_Color", evening ? new Color(.055f,.18f,.23f) : new Color(.04f,.23f,.34f));
                water.SetColor("_SkyColor",Color.Lerp(RenderSettings.fogColor,RenderSettings.ambientSkyColor,.55f));
                water.SetFloat("_Rain",_rainAmount); water.SetFloat("_Wind",Mathf.Clamp01(_environmentState.windSpeedMps/9f));
                if(_environmentQuality>=AeroSimQuality.High)water.EnableKeyword("AEROSIM_WATER_HIGH");
                if(_environmentQuality>=AeroSimQuality.Ultra)water.EnableKeyword("AEROSIM_WATER_ULTRA");
                int waterCells=_environmentQuality==AeroSimQuality.Low?22:_environmentQuality==AeroSimQuality.Medium?34:_environmentQuality==AeroSimQuality.High?45:58;
                // This is the simulator's shallow lake surface; original ground collision remains at y=0.
                MeshObject("City training lake · visual water / unchanged ground physics", FlatMesh(waterCells, 340, 154), new Vector3(0,.135f,95), Vector3.one, water);
            }
            var promenade = new EnvironmentBatchV1();
            var parkMetal = new EnvironmentBatchV1();
            promenade.Box(new Vector3(0,.16f,15.5f), new Vector3(340,.16f,3));
            for (int i = 0; i < 29; i++)
            {
                float x = -162 + i * 11.5f;
                if (Mathf.Abs(x) < 48) continue; // no railing or furniture in the training approach corridor
                parkMetal.Box(new Vector3(x, .75f, 17.05f), new Vector3(.1f, 1.25f, .1f));
                if (i % 3 == 0)
                {
                    promenade.Box(new Vector3(x,.65f,11), new Vector3(3,.12f,.55f));
                    parkMetal.Box(new Vector3(x-.95f,.35f,11), new Vector3(.12f,.6f,.4f));
                    parkMetal.Box(new Vector3(x+.95f,.35f,11), new Vector3(.12f,.6f,.4f));
                }
            }
            for (int side = -1; side <= 1; side += 2)
                parkMetal.Box(new Vector3(side * 108,.95f,17.05f), new Vector3(120,.09f,.09f));
            PlaceEnvironmentBatchV1("City park waterfront promenade · visual", promenade, concrete);
            PlaceEnvironmentBatchV1("Waterfront railings and furniture · visual", parkMetal, steel);
        }

        private void BuildCoastalDetailsV1(Material concrete, Material steel)
        {
            var boardwalk = new EnvironmentBatchV1();
            var rails = new EnvironmentBatchV1();
            for (int i = 0; i < 60; i++)
            {
                float z = -45 + i * 3;
                float y = GroundHeight(-66,z) + .12f;
                boardwalk.Box(new Vector3(-66,y,z), new Vector3(5,.13f,2.93f));
                if (i % 3 == 0)
                {
                    rails.Box(new Vector3(-68.3f,y+.75f,z), new Vector3(.12f,1.4f,.12f));
                    rails.Box(new Vector3(-68.3f,y+1.1f,z+3), new Vector3(.1f,.1f,6));
                }
            }
            PlaceEnvironmentBatchV1("Coastal observation boardwalk · visual", boardwalk, EnvironmentSurfaceV1(new Color(.50f,.42f,.30f), 0, null, 2));
            PlaceEnvironmentBatchV1("Coastal safety railing · visual", rails, steel);
            var stone = new EnvironmentBatchV1();
            for (int i = 0; i < 30; i++)
            {
                float z = -140 + i * 10;
                stone.Box(new Vector3(-77,.03f,z), new Vector3(2.5f,.15f,4.5f), Quaternion.Euler(0,i*43,0));
            }
            PlaceEnvironmentBatchV1("Shoreline gravel ledges · visual", stone, concrete);
        }
    }

    // Single deferred attachment ensures cosmetic illumination follows actual gate lifetime and selection colour.
    internal sealed class GateLightAttachV1 : MonoBehaviour
    {
        private Transform _root;
        private Material _glow;
        public void Initialize(Transform root, Material glow) { _root=root; _glow=glow; }
        private void Start()
        {
            foreach (Transform child in _root)
            {
                if (child.name != "Race gate") continue;
                AddStrip(child, new Vector3(-4,0,-.27f), new Vector3(.16f,7.8f,.04f));
                AddStrip(child, new Vector3(4,0,-.27f), new Vector3(.16f,7.8f,.04f));
                AddStrip(child, new Vector3(0,4,-.27f), new Vector3(8,.16f,.04f));
            }
            Destroy(this);
        }
        private void AddStrip(Transform gate, Vector3 position, Vector3 scale)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name="Race gate luminous trim · cosmetic";
            strip.transform.SetParent(gate,false); strip.transform.localPosition=position; strip.transform.localScale=scale;
            // The existing orange/cyan frame is the collider and active-state indicator.
            Object.Destroy(strip.GetComponent<Collider>());
            strip.GetComponent<Renderer>().sharedMaterial=_glow;
        }
    }
}
