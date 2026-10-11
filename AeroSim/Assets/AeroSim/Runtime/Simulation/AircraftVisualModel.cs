using System.Collections.Generic;
using AeroSim.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroSim.Simulation
{
    // Shared by hangar, gameplay and replay. Geometry is visual only; flight physics remains profile-driven.
    public sealed class AircraftVisualModel : MonoBehaviour
    {
        public struct LodQualityReport
        {
            public int Level, Renderers, RenderedVertices, RenderedTriangles, SharedMaterials;
            public float ScreenRelativeTransitionHeight;
        }

        public struct ModelQualityReport
        {
            public string RuntimeArchetype, RuntimeVisualVersion, GoldenSampleId;
            public int Renderers, RenderedVertices, RenderedTriangles, UniqueMeshes, SharedMaterials, RuntimeMeshes;
            public int ReadableMeshes, ReadableRuntimeMeshes, Textures, ReadableTextures;
            public int Colliders, EnabledColliders, PrimitiveColliders, MeshColliders, CollisionRenderers, CompoundAttachedColliders;
            public int Rotors, MotionDiscs, Sensors, Fasteners, MechanicalParts;
            public LodQualityReport[] Lods;
            public bool IsGoldenSample, HasExpectedRotorCount, HasRuntimeLodGroup, HasValidCompoundCollision, IsInsideMaterialBudget;
        }

        public const int SharedMaterialBudget = 6;
        public const int MinimumCompoundColliders = 6;
        public const int MaximumCompoundColliders = 16;
        public const int LodLevelCount = 5;
        public const string RuntimeVisualVersion = "aerosim-procedural-aircraft/3.0.0";
        public const string FoldingGoldenRuntimeId = "mavic3p";
        public const string DuctedGoldenRuntimeId = "avata2";
        public const string OpenFrameGoldenRuntimeId = "racer";
        public AircraftProfile Profile { get; private set; }
        public string RuntimeArchetype { get; private set; }
        public string GoldenSampleId { get; private set; }
        public Transform CollisionRoot { get; private set; }
        public bool IsGoldenSample { get { return Profile != null && Profile.Id == GoldenSampleId; } }
        public readonly List<Transform> Rotors = new();
        public readonly List<GameObject> MotionDiscs = new();
        private readonly List<Material> _materials = new();
        private readonly List<Mesh> _meshes = new();
        private readonly List<Renderer[]> _lodRenderers = new();
        private Mesh _rounded, _ring, _screw, _disc;
        private Material _shell, _dark, _metal, _glass, _carbon, _blur;
        private LODGroup _lodGroup;
        private int _sensorCount, _fastenerCount, _mechanicalPartCount;

        public int DistinctSharedMaterialCount
        {
            get
            {
                var materials = new HashSet<Material>();
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.sharedMaterial != null) materials.Add(renderer.sharedMaterial);
                }
                return materials.Count;
            }
        }

        // Runtime diagnostics use shared assets only; they never instantiate materials or read mesh payloads.
        public ModelQualityReport GetQualityReport()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            int vertices, triangles, readableMeshes;
            var uniqueMeshes = new HashSet<Mesh>();
            AccumulateGeometry(renderers, uniqueMeshes, out vertices, out triangles, out readableMeshes);
            var colliders = CollisionRoot != null ? CollisionRoot.GetComponentsInChildren<Collider>(true) : new Collider[0];
            int enabledColliders = 0, primitiveColliders = 0, meshColliders = 0, attachedColliders = 0;
            var parentBody = GetComponentInParent<Rigidbody>();
            foreach (var collider in colliders)
            {
                if (collider.enabled) enabledColliders++;
                if (collider is MeshCollider) meshColliders++;
                if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider) primitiveColliders++;
                if (parentBody != null && collider.attachedRigidbody == parentBody) attachedColliders++;
            }
            int collisionRenderers = CollisionRoot != null ? CollisionRoot.GetComponentsInChildren<Renderer>(true).Length : 0;
            int readableRuntimeMeshes = 0;
            foreach (var mesh in _meshes) if (mesh != null && mesh.isReadable) readableRuntimeMeshes++;
            var textures = new HashSet<Texture2D>();
            foreach (var material in _materials)
            {
                if (material == null || !material.HasProperty("_MainTex")) continue;
                var texture = material.mainTexture as Texture2D;
                if (texture != null) textures.Add(texture);
            }
            int readableTextures = 0;
            foreach (var texture in textures) if (texture.isReadable) readableTextures++;
            var lodReports = new LodQualityReport[_lodRenderers.Count];
            for (int level = 0; level < _lodRenderers.Count; level++)
                lodReports[level] = BuildLodReport(level, _lodRenderers[level]);
            return new ModelQualityReport
            {
                RuntimeArchetype = RuntimeArchetype,
                RuntimeVisualVersion = RuntimeVisualVersion,
                GoldenSampleId = GoldenSampleId,
                IsGoldenSample = IsGoldenSample,
                Renderers = renderers.Length,
                RenderedVertices = vertices,
                RenderedTriangles = triangles,
                UniqueMeshes = uniqueMeshes.Count,
                SharedMaterials = DistinctSharedMaterialCount,
                RuntimeMeshes = _meshes.Count,
                ReadableMeshes = readableMeshes,
                ReadableRuntimeMeshes = readableRuntimeMeshes,
                Textures = textures.Count,
                ReadableTextures = readableTextures,
                Colliders = colliders.Length,
                EnabledColliders = enabledColliders,
                PrimitiveColliders = primitiveColliders,
                MeshColliders = meshColliders,
                CollisionRenderers = collisionRenderers,
                CompoundAttachedColliders = attachedColliders,
                Rotors = Rotors.Count,
                MotionDiscs = MotionDiscs.Count,
                Sensors = _sensorCount,
                Fasteners = _fastenerCount,
                MechanicalParts = _mechanicalPartCount,
                Lods = lodReports,
                HasExpectedRotorCount = Rotors.Count == 4 && MotionDiscs.Count == 4,
                HasRuntimeLodGroup = _lodGroup != null && _lodGroup.enabled && _lodGroup.lodCount == LodLevelCount,
                HasValidCompoundCollision = colliders.Length >= MinimumCompoundColliders && colliders.Length <= MaximumCompoundColliders
                    && enabledColliders == colliders.Length && primitiveColliders == colliders.Length && meshColliders == 0
                    && collisionRenderers == 0 && (parentBody == null || attachedColliders == colliders.Length),
                IsInsideMaterialBudget = DistinctSharedMaterialCount <= SharedMaterialBudget
            };
        }

        private static void AccumulateGeometry(Renderer[] renderers, HashSet<Mesh> uniqueMeshes,
            out int vertices, out int triangles, out int readableMeshes)
        {
            vertices = 0;
            triangles = 0;
            readableMeshes = 0;
            var readable = new HashSet<Mesh>();
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null) continue;
                uniqueMeshes.Add(mesh);
                if (mesh.isReadable) readable.Add(mesh);
                vertices += mesh.vertexCount;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    triangles += (int)mesh.GetIndexCount(sub) / 3;
            }
            readableMeshes = readable.Count;
        }

        private LodQualityReport BuildLodReport(int level, Renderer[] renderers)
        {
            int vertices, triangles, readable;
            var meshes = new HashSet<Mesh>();
            AccumulateGeometry(renderers, meshes, out vertices, out triangles, out readable);
            var materials = new HashSet<Material>();
            foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials)
                    if (material != null) materials.Add(material);
            float transition = 0;
            if (_lodGroup != null)
            {
                var lods = _lodGroup.GetLODs();
                if (level < lods.Length) transition = lods[level].screenRelativeTransitionHeight;
            }
            return new LodQualityReport
            {
                Level = level,
                Renderers = renderers.Length,
                RenderedVertices = vertices,
                RenderedTriangles = triangles,
                SharedMaterials = materials.Count,
                ScreenRelativeTransitionHeight = transition
            };
        }

        public bool ValidateVisualBudget(out string reason)
        {
            var report = GetQualityReport();
            if (!report.HasExpectedRotorCount)
            {
                reason = "Expected four rotors and four motion discs.";
                return false;
            }
            if (!report.HasRuntimeLodGroup || report.Lods == null || report.Lods.Length != LodLevelCount)
            {
                reason = "Runtime LODGroup must expose LOD0, LOD1, LOD2, LOD3 and Cull.";
                return false;
            }
            if (!report.IsInsideMaterialBudget)
            {
                reason = "Shared material budget exceeded: " + report.SharedMaterials + "/" + SharedMaterialBudget + ".";
                return false;
            }
            if (report.RuntimeMeshes > 4 || report.ReadableRuntimeMeshes != 0)
            {
                reason = "Runtime mesh budget/read-write gate failed: " + report.RuntimeMeshes + " meshes, "
                    + report.ReadableRuntimeMeshes + " readable.";
                return false;
            }
            if (!report.HasValidCompoundCollision)
            {
                reason = "Compound collision gate failed: " + report.Colliders + " total, "
                    + report.PrimitiveColliders + " primitive, " + report.MeshColliders + " mesh, "
                    + report.CompoundAttachedColliders + " attached to parent body, "
                    + report.CollisionRenderers + " renderers.";
                return false;
            }
            for (int level = 0; level < 4; level++)
            {
                var lod = report.Lods[level];
                if (lod.SharedMaterials > SharedMaterialBudget || lod.Renderers > RendererBudget(level)
                    || lod.RenderedTriangles > TriangleBudget(level))
                {
                    reason = "LOD" + level + " budget failed: " + lod.Renderers + " renderers, "
                        + lod.RenderedTriangles + " triangles, " + lod.SharedMaterials + " materials.";
                    return false;
                }
                if (level > 0 && (lod.Renderers > report.Lods[level - 1].Renderers
                    || lod.RenderedTriangles > report.Lods[level - 1].RenderedTriangles))
                {
                    reason = "LOD complexity must decrease monotonically at LOD" + level + ".";
                    return false;
                }
            }
            if (report.Lods[4].Renderers != 0 || report.Lods[4].RenderedTriangles != 0)
            {
                reason = "Cull LOD must contain no renderers.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private int TriangleBudget(int level)
        {
            if (RuntimeArchetype == "guarded-fpv")
                return level == 0 ? 100000 : level == 1 ? 45000 : level == 2 ? 15000 : 4000;
            if (RuntimeArchetype == "open-5inch-fpv")
                return level == 0 ? 140000 : level == 1 ? 65000 : level == 2 ? 22000 : 5000;
            return level == 0 ? 120000 : level == 1 ? 55000 : level == 2 ? 18000 : 4000;
        }

        private static int RendererBudget(int level)
        {
            return level == 0 ? 180 : level == 1 ? 110 : level == 2 ? 64 : 20;
        }

        public void Initialize(AircraftProfile profile)
        {
            Profile = profile;
            ConfigureRuntimeIdentity(profile);
            name = "Aircraft visual · " + profile.Id;
            _rounded = RoundedMesh();
            _ring = DuctMesh(48, .31f, .27f, .05f);
            _screw = LowCylinderMesh(8);
            _disc = DiscMesh(48, 1f);

            Color shellColor;
            switch (profile.Shape)
            {
                case AirframeShape.Mini: shellColor = new Color(.91f, .93f, .92f); break;
                case AirframeShape.Air: shellColor = new Color(.72f, .76f, .78f); break;
                case AirframeShape.Mavic: shellColor = new Color(.34f, .36f, .35f); break;
                case AirframeShape.Ducted: shellColor = new Color(.33f, .35f, .34f); break;
                case AirframeShape.Micro: shellColor = new Color(.98f, .43f, .075f); break;
                default: shellColor = new Color(.82f, .08f, .035f); break;
            }
            // Six shared materials total. Fine color changes use property blocks, never per-part instances.
            _shell = Surface(shellColor, profile.Texture, .40f, .02f, 0, .32f, .075f, .32f, new Color(.30f, .34f, .36f));
            _dark = Surface(new Color(.025f, .031f, .036f), null, .24f, .02f, 0, .22f, .14f, .42f, new Color(.18f, .23f, .27f));
            _metal = Surface(new Color(.31f, .33f, .35f), null, .68f, .78f, 1, .38f, .07f, .18f, new Color(.30f, .34f, .38f));
            _glass = Surface(new Color(.012f, .052f, .092f), null, .96f, .04f, 3, 0, .30f, .08f, new Color(.16f, .45f, .66f));
            _carbon = Surface(new Color(.63f, .65f, .66f), "carbon-weave-v3", .74f, .01f, 2, .52f, .18f, .30f, new Color(.23f, .30f, .34f));
            _carbon.mainTextureScale = new Vector2(4, 4);
            _blur = new Material(Resources.Load<Shader>("RotorBlur")) { enableInstancing = true };
            _materials.Add(_blur);

            if (profile.Shape == AirframeShape.Ducted || profile.Shape == AirframeShape.Micro)
                BuildDucted(profile.Shape == AirframeShape.Micro);
            else if (profile.Shape == AirframeShape.Racer)
                BuildRacer();
            else
                BuildCameraDrone();
            BuildCompoundCollision();
            ConfigureRuntimeLods();
            UploadRuntimeMeshes();
            transform.localScale = Vector3.one * profile.VisualScale;
        }

        private void ConfigureRuntimeIdentity(AircraftProfile profile)
        {
            if (profile.Shape == AirframeShape.Ducted || profile.Shape == AirframeShape.Micro)
            {
                RuntimeArchetype = "guarded-fpv";
                GoldenSampleId = DuctedGoldenRuntimeId;
            }
            else if (profile.Shape == AirframeShape.Racer)
            {
                RuntimeArchetype = "open-5inch-fpv";
                GoldenSampleId = OpenFrameGoldenRuntimeId;
            }
            else
            {
                RuntimeArchetype = "folding-camera";
                GoldenSampleId = FoldingGoldenRuntimeId;
            }
        }

        private void BuildCompoundCollision()
        {
            CollisionRoot = Pivot("Collision", Vector3.zero);
            // FlightSimulator historically adds one fallback root box. Disable and retire it once the
            // model-owned compound is available; child colliders attach to the same parent Rigidbody.
            var body = GetComponentInParent<Rigidbody>();
            if (body != null)
            {
                var legacy = body.GetComponent<BoxCollider>();
                if (legacy != null) { legacy.enabled = false; Destroy(legacy); }
            }
            if (RuntimeArchetype == "guarded-fpv") BuildGuardedCollision();
            else if (RuntimeArchetype == "open-5inch-fpv") BuildOpenFrameCollision();
            else BuildFoldingCollision();
        }

        private void BuildFoldingCollision()
        {
            AddBoxCollider("Body hull", new Vector3(0, 0, 0), new Vector3(.42f, .20f, .62f));
            AddBoxCollider("Battery hull", new Vector3(0, .075f, -.16f), new Vector3(.29f, .10f, .29f));
            AddSphereCollider("Gimbal hull", new Vector3(0, -.12f, .25f), .105f);
            float span = Profile.Shape == AirframeShape.Mini ? .405f : Profile.Shape == AirframeShape.Mavic ? .58f : .54f;
            float front = Profile.Shape == AirframeShape.Mini ? .34f : Profile.Shape == AirframeShape.Mavic ? .50f : .43f;
            float rear = Profile.Shape == AirframeShape.Mini ? -.38f : Profile.Shape == AirframeShape.Mavic ? -.55f : -.46f;
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                AddCapsuleCollider("Arm hull " + i, Vector3.zero,
                    new Vector3(side * span, 0, i < 2 ? front : rear), .055f);
            }
        }

        private void BuildGuardedCollision()
        {
            float s = Profile.Shape == AirframeShape.Micro ? .72f : 1f;
            AddBoxCollider("Canopy hull", new Vector3(0, .025f, -.015f) * s, new Vector3(.34f, .18f, .56f) * s);
            AddBoxCollider("Battery hull", new Vector3(0, .13f, -.105f) * s, new Vector3(.25f, .10f, .34f) * s);
            AddBoxCollider("Camera hull", new Vector3(0, .008f, .34f) * s, new Vector3(.18f, .14f, .13f) * s);
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                AddSphereCollider("Guard hull " + i,
                    new Vector3(side * .30f, -.018f, i < 2 ? .27f : -.29f) * s, .31f * s);
            }
        }

        private void BuildOpenFrameCollision()
        {
            AddBoxCollider("Frame hull", new Vector3(0, .03f, -.02f), new Vector3(.32f, .17f, .41f));
            AddBoxCollider("Battery hull", new Vector3(0, .19f, -.055f), new Vector3(.21f, .14f, .33f));
            AddBoxCollider("Camera hull", new Vector3(0, .055f, .22f), new Vector3(.19f, .16f, .18f));
            for (int i = 0; i < 4; i++)
            {
                var end = new Vector3(i % 2 == 0 ? -.40f : .40f, 0, i < 2 ? .40f : -.40f);
                AddCapsuleCollider("Arm hull " + i, Vector3.zero, end, .065f);
            }
        }

        private void AddBoxCollider(string label, Vector3 center, Vector3 size)
        {
            var node = Pivot(label, Vector3.zero, CollisionRoot);
            var collider = node.gameObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private void AddSphereCollider(string label, Vector3 center, float radius)
        {
            var node = Pivot(label, center, CollisionRoot);
            node.gameObject.AddComponent<SphereCollider>().radius = radius;
        }

        private void AddCapsuleCollider(string label, Vector3 start, Vector3 end, float radius)
        {
            var node = Pivot(label, (start + end) * .5f, CollisionRoot);
            node.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            var collider = node.gameObject.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = radius;
            collider.height = Vector3.Distance(start, end) + radius * 2f;
        }

        private void ConfigureRuntimeLods()
        {
            var lod0 = new List<Renderer>();
            var lod1 = new List<Renderer>();
            var lod2 = new List<Renderer>();
            var lod3 = new List<Renderer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                lod0.Add(renderer);
                if (!IsLod0Detail(renderer.name)) lod1.Add(renderer);
                if (IsLod2Silhouette(renderer.name)) lod2.Add(renderer);
                if (IsLod3Silhouette(renderer.name)) lod3.Add(renderer);
            }
            // LOD1/2/3 deliberately reuse stable renderers; LODGroup performs actual runtime enable/disable.
            _lodRenderers.Clear();
            _lodRenderers.Add(lod0.ToArray());
            _lodRenderers.Add(lod1.ToArray());
            _lodRenderers.Add(lod2.ToArray());
            _lodRenderers.Add(lod3.ToArray());
            _lodRenderers.Add(new Renderer[0]);
            _lodGroup = gameObject.AddComponent<LODGroup>();
            _lodGroup.fadeMode = LODFadeMode.None;
            _lodGroup.animateCrossFading = false;
            _lodGroup.SetLODs(new[]
            {
                new LOD(.35f, _lodRenderers[0]),
                new LOD(.15f, _lodRenderers[1]),
                new LOD(.05f, _lodRenderers[2]),
                new LOD(.015f, _lodRenderers[3]),
                new LOD(.001f, _lodRenderers[4])
            });
            _lodGroup.RecalculateBounds();
        }

        private static bool IsLod0Detail(string partName)
        {
            string key = partName.ToLowerInvariant();
            return key.Contains("screw") || key.Contains("fastener") || key.Contains("vent")
                || key.Contains("receiver") || key.Contains("aperture") || key.Contains("split line")
                || key.Contains("phase wire") || key.Contains("terminal") || key.Contains("latch")
                || key.Contains("bezel") || key.Contains("wear rail") || key.Contains("status indicator")
                || key.Contains("navigation led") || key.Contains("battery lead");
        }

        private bool IsLod2Silhouette(string partName)
        {
            string key = partName.ToLowerInvariant();
            return key.Contains("shell") || key.Contains("chassis") || key.Contains("canopy")
                || key.Contains("carbon plate") || key.Contains("carbon x arm") || key.Contains("folding arm")
                || key.Contains("prop guard") || key.Contains("guard bridge") || key.Contains("motor cross brace")
                || key.Contains("battery") || key.Contains("camera body") || key.Contains("camera housing")
                || key.Contains("optical glass") || key.Contains("motor bell") || key.Contains("motion blur disc")
                || (RuntimeArchetype != "guarded-fpv" && key.Contains("landing")) || key.Contains("antenna");
        }

        private bool IsLod3Silhouette(string partName)
        {
            string key = partName.ToLowerInvariant();
            if (key.Contains("motion blur disc")) return true;
            if (RuntimeArchetype == "guarded-fpv")
                return key.Contains("canopy") || key.Contains("prop guard");
            if (RuntimeArchetype == "open-5inch-fpv")
                return key.Contains("carbon plate") || key.Contains("carbon x arm");
            return key.Contains("sculpted folding shell") || key.Contains("lower magnesium chassis");
        }

        private void UploadRuntimeMeshes()
        {
            foreach (var mesh in _meshes)
                if (mesh != null && mesh.isReadable) mesh.UploadMeshData(true);
        }

        private Material Surface(Color color, string texture, float smooth, float metal, float materialType,
            float micro, float fresnel, float cavity, Color fresnelColor)
        {
            var m = new Material(Resources.Load<Shader>("TrainingSurface")) { color = color, enableInstancing = true };
            _materials.Add(m);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_MaterialType", materialType);
            m.SetFloat("_MicroDetail", micro);
            m.SetFloat("_FresnelStrength", fresnel);
            m.SetFloat("_CavityStrength", cavity);
            m.SetColor("_FresnelColor", fresnelColor);
            if (texture != null) m.mainTexture = Resources.Load<Texture2D>("Art/AircraftV3/" + texture);
            return m;
        }

        private Transform Pivot(string label, Vector3 position, Transform parent = null)
        {
            var pivot = new GameObject(label).transform;
            pivot.SetParent(parent != null ? parent : transform, false);
            pivot.localPosition = position;
            return pivot;
        }

        private GameObject Part(string label, PrimitiveType type, Vector3 p, Vector3 scale, Material material, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = label;
            g.transform.SetParent(parent != null ? parent : transform, false);
            g.transform.localPosition = p;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
            var collider = g.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            _mechanicalPartCount++;
            return g;
        }

        private GameObject ColoredPart(string label, PrimitiveType type, Vector3 p, Vector3 scale, Material material,
            Color color, Color emission, Transform parent = null)
        {
            var part = Part(label, type, p, scale, material, parent);
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            properties.SetColor("_EmissionColor", emission);
            part.GetComponent<Renderer>().SetPropertyBlock(properties);
            return part;
        }

        private GameObject Shape(string label, Mesh mesh, Vector3 p, Vector3 scale, Material material, Transform parent = null)
        {
            var g = new GameObject(label);
            g.transform.SetParent(parent != null ? parent : transform, false);
            g.transform.localPosition = p;
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = material;
            _mechanicalPartCount++;
            return g;
        }

        private void Rod(string label, Vector3 a, Vector3 b, float radius, Material material, Transform parent = null)
        {
            var g = Part(label, PrimitiveType.Capsule, (a + b) * .5f, new Vector3(radius, Vector3.Distance(a, b) * .5f, radius), material, parent);
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }

        private void Screw(string label, Vector3 p, float radius, Transform parent = null)
        {
            Shape(label, _screw, p, new Vector3(radius, .005f, radius), _metal, parent);
            _fastenerCount++;
        }

        private void SensorWindow(string label, Vector3 p, float diameter, float depth, Transform parent = null)
        {
            var surround = Part(label + " surround", PrimitiveType.Cylinder, p, new Vector3(diameter * 1.12f, depth, diameter * 1.12f), _dark, parent);
            surround.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var glass = Part(label + " glass", PrimitiveType.Cylinder, p + Vector3.forward * depth * 1.9f,
                new Vector3(diameter, depth * .35f, diameter), _glass, parent);
            glass.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Part(label + " receiver", PrimitiveType.Sphere, p + Vector3.forward * depth * 2.35f,
                new Vector3(diameter * .40f, diameter * .40f, depth * .25f), _dark, parent);
            _sensorCount++;
        }

        private void Lens(string label, Vector3 p, float diameter, float length, Transform parent = null)
        {
            var barrel = Part(label + " barrel", PrimitiveType.Cylinder, p, new Vector3(diameter, length, diameter), _dark, parent);
            barrel.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var ring = Part(label + " anodized bezel", PrimitiveType.Cylinder, p + Vector3.forward * length * .94f,
                new Vector3(diameter * 1.04f, .004f, diameter * 1.04f), _metal, parent);
            ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var front = Part(label + " optical glass", PrimitiveType.Cylinder, p + Vector3.forward * (length + .003f),
                new Vector3(diameter * .80f, .003f, diameter * .80f), _glass, parent);
            front.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var aperture = Part(label + " inner aperture", PrimitiveType.Cylinder, p + Vector3.forward * (length + .006f),
                new Vector3(diameter * .34f, .002f, diameter * .34f), _dark, parent);
            aperture.transform.localRotation = Quaternion.Euler(90, 0, 0);
            _sensorCount++;
        }

        private Transform BuildThreeAxisGimbal(Vector3 mount)
        {
            var yaw = Pivot("Gimbal yaw pivot", mount);
            Part("Yaw motor", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.045f, .024f, .045f), _metal, yaw);
            var roll = Pivot("Gimbal roll pivot", new Vector3(0, -.055f, .012f), yaw);
            var rollMotor = Part("Roll motor", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.037f, .020f, .037f), _metal, roll);
            rollMotor.transform.localRotation = Quaternion.Euler(0, 0, 90);
            Rod("Gimbal fork left", new Vector3(-.07f, 0, 0), new Vector3(-.07f, -.07f, .04f), .012f, _metal, roll);
            Rod("Gimbal fork right", new Vector3(.07f, 0, 0), new Vector3(.07f, -.07f, .04f), .012f, _metal, roll);
            var pitch = Pivot("Gimbal pitch pivot", new Vector3(0, -.07f, .04f), roll);
            var pitchMotor = Part("Pitch motor", PrimitiveType.Cylinder, new Vector3(-.072f, 0, 0), new Vector3(.033f, .018f, .033f), _metal, pitch);
            pitchMotor.transform.localRotation = Quaternion.Euler(0, 0, 90);
            return pitch;
        }

        private void BuildCameraDrone()
        {
            bool mini = Profile.Shape == AirframeShape.Mini;
            bool mavic = Profile.Shape == AirframeShape.Mavic;
            var size = mini ? new Vector3(.34f, .14f, .54f) : mavic ? new Vector3(.45f, .19f, .78f) : new Vector3(.52f, .17f, .64f);
            Shape("Sculpted folding shell", _rounded, new Vector3(0, .035f, 0), size, _shell);
            Shape("Lower magnesium chassis", _rounded, new Vector3(0, -.042f, -.02f), Vector3.Scale(size, new Vector3(.91f, .55f, .90f)), _dark);
            if (!mini)
            {
                Shape(mavic ? "Long shoulder fairing" : "Wide shoulder fairing", _rounded,
                    new Vector3(0, .065f, mavic ? -.12f : .02f),
                    new Vector3(size.x * (mavic ? .82f : .94f), size.y * .52f, size.z * .48f), _shell);
            }
            Shape("Battery panel shadow gap", _rounded, new Vector3(0, size.y * .52f - .001f, -size.z * .17f), new Vector3(size.x * .76f, .009f, size.z * .40f), _dark);
            Shape("Rear removable battery", _rounded, new Vector3(0, size.y * .52f + .003f, -size.z * .17f), new Vector3(size.x * .73f, .010f, size.z * .37f), _shell);
            Part("Battery latch", PrimitiveType.Cube, new Vector3(0, size.y * .53f + .007f, -size.z * .29f), new Vector3(.045f, .008f, .023f), _dark);
            Part("Nose shell split line", PrimitiveType.Cube, new Vector3(0, size.y * .37f, size.z * .372f), new Vector3(size.x * .50f, .006f, .007f), _dark);
            Shape("Vented underside sensor deck", _rounded, new Vector3(0, -.088f, .015f), new Vector3(size.x * .54f, .025f, size.z * .34f), _dark);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int j = 0; j < (mini ? 3 : 5); j++)
                    Part("Side cooling vent", PrimitiveType.Cube, new Vector3(side * size.x * .474f, .015f, -.105f + j * .038f), new Vector3(.009f, .036f, .010f), _dark);
                var eye = new Vector3(side * size.x * .28f, .035f + size.y * .22f, size.z * .455f);
                Shape("Raised front sensor eyebrow", _rounded, eye, new Vector3(.080f, .054f, .050f), _shell);
                SensorWindow("Front obstacle sensor", eye + Vector3.forward * .026f, .032f, .005f);
                SensorWindow("Rear obstacle sensor", new Vector3(side * size.x * .29f, .025f, -size.z * .45f), .025f, .004f);
                SensorWindow("Downward vision sensor", new Vector3(side * size.x * .17f, -.103f, .015f), .023f, .004f);
                Screw("Battery cover screw", new Vector3(side * size.x * .25f, size.y * .53f + .010f, -size.z * .22f), .012f);
            }
            if (!mini) Shape("Forward binocular sensor bridge", _rounded, new Vector3(0, .08f, size.z * .475f), new Vector3(.12f, .030f, .020f), _glass);

            var pitch = BuildThreeAxisGimbal(new Vector3(0, -.075f, size.z * .32f));
            if (mini)
            {
                Shape("Compact camera body", _rounded, Vector3.zero, new Vector3(.135f, .11f, .12f), _metal, pitch);
                Lens("Main camera", new Vector3(0, 0, .068f), .087f, .025f, pitch);
            }
            else if (mavic)
            {
                Shape("Tri-camera camera body", _rounded, Vector3.zero, new Vector3(.235f, .18f, .15f), _metal, pitch);
                Lens("Wide camera", new Vector3(-.047f, -.030f, .082f), .10f, .027f, pitch);
                Lens("Medium tele camera", new Vector3(.066f, -.040f, .084f), .060f, .026f, pitch);
                Lens("Tele camera", new Vector3(.05f, .055f, .084f), .069f, .026f, pitch);
            }
            else
            {
                Shape("Stacked dual-camera body", _rounded, Vector3.zero, new Vector3(.165f, .20f, .145f), _metal, pitch);
                Lens("Wide camera", new Vector3(0, -.048f, .078f), .094f, .028f, pitch);
                Lens("Medium tele camera", new Vector3(0, .050f, .078f), .067f, .026f, pitch);
            }

            float span = mini ? .405f : mavic ? .58f : .54f;
            float front = mini ? .34f : mavic ? .50f : .43f;
            float rear = mini ? -.38f : mavic ? -.55f : -.46f;
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var motor = new Vector3(side * span, i < 2 ? .002f : .027f, i < 2 ? front : rear);
                var hinge = new Vector3(side * size.x * .36f, -.013f, i < 2 ? size.z * .25f : -size.z * .31f);
                var armPivot = Pivot((i < 2 ? "Front" : "Rear") + " folding arm hinge", hinge);
                Rod("Tapered folding arm", Vector3.zero, motor - hinge, mini ? .050f : .068f, _shell, armPivot);
                Part("Arm hinge barrel", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.082f, .025f, .082f), _metal, armPivot);
                Screw("Arm hinge screw", Vector3.up * .028f, .024f, armPivot);
                Rotor(motor, mini ? .29f : .34f, 2, i, true);
                Rod("Landing foot", motor - Vector3.up * .036f, motor - Vector3.up * .13f, .028f, _dark);
                ColoredPart("Navigation LED", PrimitiveType.Sphere, motor - new Vector3(0, .032f, .01f), new Vector3(.037f, .018f, .022f), _glass,
                    i < 2 ? new Color(.76f, .035f, .012f) : new Color(.025f, .48f, .11f),
                    i < 2 ? new Color(.28f, .008f, .002f) : new Color(.004f, .19f, .025f));
            }
        }

        private void BuildDucted(bool micro)
        {
            float s = micro ? .72f : 1f;
            Shape(micro ? "Trainer canopy" : "Low cinewhoop canopy", _rounded, new Vector3(0, .05f * s, -.015f), new Vector3(.34f, .15f, .62f) * s, _shell);
            Shape("Raised battery pack", _rounded, new Vector3(0, .135f * s, -.105f), new Vector3(.25f, .09f, .36f) * s, _dark);
            Shape("Lower carbon structure", _rounded, new Vector3(0, -.052f * s, -.025f), new Vector3(.36f, .06f, .62f) * s, _carbon);
            for (int side = -1; side <= 1; side += 2) Screw("Canopy screw", new Vector3(side * .115f, .135f, .035f) * s, .013f * s);
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var p = new Vector3(side * .30f, -.018f, i < 2 ? .27f : -.29f) * s;
                Shape("Integrated prop guard", _ring, p, Vector3.one * s, _shell);
                Shape("TPU guard wear rail", _ring, p - Vector3.up * .035f * s, new Vector3(1.025f, .45f, 1.025f) * s, _dark);
                Rod("Guard bridge", new Vector3(side * .10f, -.02f, p.z * .70f), p, .045f * s, _carbon);
                Rod("Motor cross brace", p - new Vector3(.24f, .006f, 0) * s, p + new Vector3(.24f, -.006f, 0) * s, .021f * s, _carbon);
                Rotor(p, .248f * s, micro ? 4 : 3, i, false);
                Part("Soft landing bumper", PrimitiveType.Sphere, p - Vector3.up * .075f * s, new Vector3(.085f, .045f, .085f) * s, _dark);
            }
            var pitch = Pivot("FPV camera pitch pivot", new Vector3(0, .008f, .29f) * s);
            var sideMotor = Part("Camera pitch motor", PrimitiveType.Cylinder, new Vector3(-.085f, 0, 0) * s, new Vector3(.028f, .015f, .028f) * s, _metal, pitch);
            sideMotor.transform.localRotation = Quaternion.Euler(0, 0, 90);
            Shape("Forward FPV camera housing", _rounded, new Vector3(0, 0, .03f) * s, new Vector3(.18f, .135f, .13f) * s, _metal, pitch);
            Lens("Wide FPV camera", new Vector3(0, 0, .102f) * s, .12f * s, .021f * s, pitch);
            for (int side = -1; side <= 1; side += 2)
                SensorWindow("Downward positioning sensor", new Vector3(side * .091f, -.070f, .15f) * s, .021f * s, .003f * s);
            ColoredPart("Tail status indicator", PrimitiveType.Sphere, new Vector3(0, .06f, -.31f) * s, new Vector3(.045f, .016f, .02f) * s, _glass,
                new Color(.025f, .48f, .11f), new Color(.004f, .19f, .025f));
        }

        private void BuildRacer()
        {
            // Open 5-inch X frame: plates, standoffs, stack and terminated harnesses remain visibly separate.
            Part("Bottom carbon plate", PrimitiveType.Cube, new Vector3(0, -.045f, -.02f), new Vector3(.33f, .018f, .42f), _carbon);
            Part("Top carbon plate", PrimitiveType.Cube, new Vector3(0, .105f, -.02f), new Vector3(.31f, .018f, .40f), _carbon);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Rod("Anodized stack standoff", new Vector3(x * .135f, -.035f, z * .175f - .02f), new Vector3(x * .135f, .105f, z * .175f - .02f), .014f, _metal);
                Screw("Frame stack screw", new Vector3(x * .135f, .119f, z * .175f - .02f), .014f);
            }
            for (int layer = 0; layer < 3; layer++)
                Part(layer == 0 ? "Four-in-one ESC" : layer == 1 ? "Flight controller" : "Video transmitter",
                    PrimitiveType.Cube, new Vector3(0, -.005f + layer * .040f, -.02f), new Vector3(.22f, .014f, .22f), layer == 1 ? _metal : _dark);
            Shape("LiPo pack", _rounded, new Vector3(0, .19f, -.055f), new Vector3(.20f, .13f, .32f), _dark);
            ColoredPart("Battery strap", PrimitiveType.Cube, new Vector3(0, .255f, -.025f), new Vector3(.23f, .015f, .075f), _shell,
                new Color(.76f, .035f, .012f), new Color(.06f, .002f, 0));
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(i % 2 == 0 ? -.40f : .40f, 0, i < 2 ? .40f : -.40f);
                Rod("Carbon X arm", Vector3.zero, p, .064f, _carbon);
                Rotor(p, .30f, 3, i, false);
                var escEnd = Vector3.Lerp(Vector3.zero, p, .46f);
                Rod("Motor phase wire A", escEnd + Vector3.left * .012f, p + Vector3.left * .018f - Vector3.up * .025f, .006f, _shell);
                Rod("Motor phase wire B", escEnd, p - Vector3.up * .025f, .006f, _metal);
                Rod("Motor phase wire C", escEnd + Vector3.right * .012f, p + Vector3.right * .018f - Vector3.up * .025f, .006f, _dark);
                Part("Motor wire terminal", PrimitiveType.Sphere, p - Vector3.up * .025f, new Vector3(.018f, .010f, .018f), _metal);
            }
            var cage = Shape("FPV camera side cage", _rounded, new Vector3(0, .055f, .195f), new Vector3(.18f, .15f, .14f), _shell);
            var cageProperties = new MaterialPropertyBlock();
            cageProperties.SetColor("_Color", new Color(.76f, .035f, .012f));
            cageProperties.SetColor("_EmissionColor", new Color(.06f, .002f, 0));
            cage.GetComponent<Renderer>().SetPropertyBlock(cageProperties);
            Lens("FPV lens", new Vector3(0, .055f, .276f), .105f, .026f);
            Rod("Rear antenna left", new Vector3(-.045f, .09f, -.22f), new Vector3(-.10f, .28f, -.31f), .018f, _dark);
            Rod("Rear antenna right", new Vector3(.045f, .09f, -.22f), new Vector3(.10f, .28f, -.31f), .018f, _dark);
            Part("XT60 connector", PrimitiveType.Cube, new Vector3(.13f, .17f, -.20f), new Vector3(.055f, .035f, .075f), _shell);
            Rod("Battery lead", new Vector3(.09f, .12f, -.13f), new Vector3(.13f, .17f, -.20f), .012f, _dark);
        }

        private void Rotor(Vector3 p, float radius, int blades, int index, bool folding)
        {
            bool clockwise = index == 0 || index == 3;
            // Two-piece bell reads as an outrunner instead of a sealed generic cylinder.
            Part("Brushless motor stator", PrimitiveType.Cylinder, p - Vector3.up * .012f, new Vector3(.108f, .027f, .108f), _metal);
            Part("Outrunner motor bell", PrimitiveType.Cylinder, p + Vector3.up * .022f, new Vector3(.118f, .030f, .118f), _dark);
            Part("Machined motor cap", PrimitiveType.Cylinder, p + Vector3.up * .053f, new Vector3(.097f, .008f, .097f), _metal);
            for (int vent = 0; vent < 4; vent++)
            {
                float angle = vent * Mathf.PI * .5f;
                Part("Motor bell vent", PrimitiveType.Cube, p + Vector3.up * .041f + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .071f,
                    new Vector3(.020f, .009f, .010f), _metal);
            }
            var root = Pivot((clockwise ? "CW" : "CCW") + " propeller rotor " + index, p + Vector3.up * .066f);
            root.localRotation = Quaternion.Euler(0, index * 33, 0);
            for (int i = 0; i < blades; i++)
            {
                float a = i / (float)blades * Mathf.PI * 2;
                float pitch = clockwise ? 7f : -7f;
                var bladeRoot = Pivot(folding ? "Folding blade hinge" : "Fixed blade root", Vector3.zero, root);
                bladeRoot.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
                var blade = Shape(clockwise ? "CW pitched airfoil blade" : "CCW pitched airfoil blade", _rounded,
                    new Vector3(radius * .54f, 0, 0), new Vector3(radius * .90f, .014f, .045f), _dark, bladeRoot);
                blade.transform.localRotation = Quaternion.Euler(pitch, 0, pitch * .35f);
                if (folding) Screw("Folding prop root pin", new Vector3(.052f, .006f, 0), .013f, bladeRoot);
            }
            Shape("Hub fastener", _screw, Vector3.zero, new Vector3(.046f, .012f, .046f), _metal, root);
            Rotors.Add(root);
            var disc = Shape(clockwise ? "CW motion blur disc" : "CCW motion blur disc", _disc, p + Vector3.up * .068f, new Vector3(radius, 1f, radius), _blur);
            disc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            disc.SetActive(false);
            MotionDiscs.Add(disc);
        }

        public void SetSpinning(bool spinning)
        {
            foreach (var disc in MotionDiscs) disc.SetActive(spinning);
        }

        private Mesh Keep(Mesh m)
        {
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            _meshes.Add(m);
            return m;
        }

        private Mesh RoundedMesh()
        {
            const int seg = 32, rings = 20;
            var v = new Vector3[(rings + 1) * (seg + 1)];
            var uv = new Vector2[v.Length];
            var t = new List<int>();
            float Signed(float x) { return Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), .55f); }
            for (int j = 0; j <= rings; j++)
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2, b = j / (float)rings * Mathf.PI;
                int k = j * (seg + 1) + i;
                var p = new Vector3(Signed(Mathf.Cos(a)) * Signed(Mathf.Sin(b)), Signed(Mathf.Cos(b)), Signed(Mathf.Sin(a)) * Signed(Mathf.Sin(b))) * .5f;
                p.x *= .92f - .15f * p.z;
                p.y += p.z * .025f;
                v[k] = p;
                uv[k] = new Vector2(i / (float)seg, j / (float)rings);
                if (j < rings && i < seg)
                {
                    t.Add(k); t.Add(k + 1); t.Add(k + seg + 1);
                    t.Add(k + 1); t.Add(k + seg + 2); t.Add(k + seg + 1);
                }
            }
            return Keep(new Mesh { name = "Sculpted shell", vertices = v, uv = uv, triangles = t.ToArray() });
        }

        private Mesh DuctMesh(int seg, float outer, float inner, float halfHeight)
        {
            var v = new Vector3[(seg + 1) * 4];
            var uv = new Vector2[v.Length];
            var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v[i * 4] = d * outer + Vector3.up * halfHeight;
                v[i * 4 + 1] = d * inner + Vector3.up * halfHeight;
                v[i * 4 + 2] = d * outer - Vector3.up * halfHeight;
                v[i * 4 + 3] = d * inner - Vector3.up * halfHeight;
                for (int j = 0; j < 4; j++) uv[i * 4 + j] = new Vector2(i / (float)seg, j / 3f);
                if (i == seg) continue;
                int k = i * 4, n = k + 4;
                void Quad(int a0, int b, int c, int d0) { t.Add(a0); t.Add(c); t.Add(b); t.Add(a0); t.Add(d0); t.Add(c); }
                Quad(k, n, n + 1, k + 1);
                Quad(k + 2, k + 3, n + 3, n + 2);
                Quad(k, k + 2, n + 2, n);
                Quad(k + 1, n + 1, n + 3, k + 3);
            }
            return Keep(new Mesh { name = "Integrated propeller duct", vertices = v, uv = uv, triangles = t.ToArray() });
        }

        private Mesh LowCylinderMesh(int seg)
        {
            var v = new Vector3[seg * 2 + 2];
            var uv = new Vector2[v.Length];
            var t = new List<int>();
            v[seg * 2] = Vector3.up * .5f;
            v[seg * 2 + 1] = Vector3.down * .5f;
            uv[seg * 2] = uv[seg * 2 + 1] = new Vector2(.5f, .5f);
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                float x = Mathf.Cos(a) * .5f, z = Mathf.Sin(a) * .5f;
                int n = (i + 1) % seg;
                v[i] = new Vector3(x, .5f, z);
                v[i + seg] = new Vector3(x, -.5f, z);
                uv[i] = uv[i + seg] = new Vector2(x + .5f, z + .5f);
                t.Add(seg * 2); t.Add(i); t.Add(n);
                t.Add(seg * 2 + 1); t.Add(n + seg); t.Add(i + seg);
                t.Add(i); t.Add(i + seg); t.Add(n + seg);
                t.Add(i); t.Add(n + seg); t.Add(n);
            }
            return Keep(new Mesh { name = "Low-poly fastener", vertices = v, uv = uv, triangles = t.ToArray() });
        }

        private Mesh DiscMesh(int seg, float radius)
        {
            var v = new Vector3[seg + 2];
            var uv = new Vector2[v.Length];
            var t = new int[seg * 3];
            uv[0] = new Vector2(.5f, .5f);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                v[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                uv[i + 1] = new Vector2(.5f + Mathf.Cos(a) * .5f, .5f + Mathf.Sin(a) * .5f);
                if (i < seg) { t[i * 3] = 0; t[i * 3 + 1] = i + 2; t[i * 3 + 2] = i + 1; }
            }
            return Keep(new Mesh { name = "Rotor disc", vertices = v, uv = uv, triangles = t });
        }

        private void OnDestroy()
        {
            foreach (var material in _materials) if (material != null) Destroy(material);
            foreach (var mesh in _meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
