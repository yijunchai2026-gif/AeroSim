using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using AeroSim.CoastP01;

namespace AeroSim.Editor
{
    /// <summary>PROCEDURAL / GENERATED SOURCE — NOT A SCAN.</summary>
    public static class CoastP01Generator
    {
        private const string Root = "Assets/AeroSim/Art/CoastProduction/P01";
        private const string ShaderPath = Root + "/Shaders/CoastRockP01.shader";
        private const string TexturePath = Root + "/Source/generated_image_035637.png";
        private const string MaterialPath = Root + "/Materials/CoastRockP01.mat";

        private sealed class Spec
        {
            public string Id, Slug;
            public Vector3 Size;
            public int Rings;
            public float Taper, Lean, Erosion;
            public bool CollisionProxy;
            public Spec(string id, string slug, Vector3 size, int rings, float taper, float lean, float erosion, bool collisionProxy = false)
            { Id=id; Slug=slug; Size=size; Rings=rings; Taper=taper; Lean=lean; Erosion=erosion; CollisionProxy=collisionProxy; }
        }

        private static readonly Spec[] Specs = {
            new Spec("P01-CLIFF-01", "macro-cliff", new Vector3(96,54,30), 14, .28f, .10f, .16f),
            new Spec("P01-STRATA-01", "strata-wall", new Vector3(58,34,12), 18, .10f, .02f, .08f),
            new Spec("P01-OVERHANG-01", "overhang", new Vector3(32,27,20), 12, -.14f, .30f, .18f),
            new Spec("P01-TALUS-01", "talus-blocks", new Vector3(38,10,24), 7, .52f, .04f, .31f),
            new Spec("P01-STACK-A", "sea-stack-a", new Vector3(18,46,17), 15, .44f, .13f, .25f),
            new Spec("P01-STACK-B", "sea-stack-b", new Vector3(24,61,20), 17, .55f, -.11f, .21f),
            new Spec("P01-FRACTURE-01", "fractured-edge", new Vector3(44,29,18), 13, .24f, -.08f, .38f),
            new Spec("P01-FOOT-01", "cliff-foot", new Vector3(72,12,34), 8, .47f, .00f, .24f),
            new Spec("P01-DISTANT-01", "distant-silhouette", new Vector3(210,86,24), 11, .61f, .06f, .12f),
            new Spec("P01-COL-01", "collision-proxy", new Vector3(94,52,28), 1, 0, 0, 0, true)
        };

        [MenuItem("AeroSim/Coast P01/Generate Asset Library")]
        public static void Generate()
        {
            EnsureFolder(Root + "/Materials"); EnsureFolder(Root + "/Meshes"); EnsureFolder(Root + "/Prefabs");
            Material material = BuildMaterial();
            foreach (Spec spec in Specs) BuildAsset(spec, material);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("Coast P01 generated 10 asset entries. PROCEDURAL / GENERATED SOURCE — NOT A SCAN.");
        }

        private static Material BuildMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (shader == null || texture == null) throw new InvalidOperationException("P01 shader or generated source texture is missing.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
            material.name = "CoastRockP01_PROCEDURAL"; material.shader = shader;
            material.SetTexture("_BaseMap", texture); material.SetColor("_Tint", new Color(.72f,.75f,.76f,1));
            material.SetFloat("_MetersPerTile", 4f); material.SetFloat("_StrataScale", .34f);
            material.SetFloat("_StrataStrength", .42f); material.SetFloat("_MacroStrength", .18f);
            material.SetFloat("_Roughness", .84f); material.SetFloat("_Wetness", .08f); material.SetFloat("_EdgeLight", .12f);
            EditorUtility.SetDirty(material); return material;
        }

        private static void BuildAsset(Spec spec, Material material)
        {
            string meshPath = Root + "/Meshes/" + spec.Slug + ".asset";
            string prefabPath = Root + "/Prefabs/" + spec.Slug + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) AssetDatabase.DeleteAsset(prefabPath);
            GameObject root = new GameObject(spec.Id + "_PROCEDURAL_GENERATED_SOURCE");
            try
            {
                root.tag = "Untagged";
                if (spec.CollisionProxy)
                {
                    BuildCollisionProxy(root, spec.Size);
                }
                else
                {
                    Mesh mesh = BuildRockMesh(spec);
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    MeshFilter filter = root.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                    MeshRenderer renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    root.AddComponent<CoastP01AssetMarker>().assetId = spec.Id;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Mesh BuildRockMesh(Spec spec)
        {
            int ringCount = Math.Max(spec.Rings, 2); const int sides = 8;
            var vertices = new List<Vector3>(ringCount*sides + 2);
            var triangles = new List<int>((ringCount-1)*sides*6 + sides*6);
            var uv = new List<Vector2>(ringCount*sides + 2);
            uint state = StableSeed(spec.Id);
            for (int ring=0; ring<ringCount; ring++)
            {
                float t = ring/(float)(ringCount-1); float y = t*spec.Size.y;
                float profile = Mathf.Lerp(1f, Mathf.Max(.16f,1f-spec.Taper), t);
                float shelf = 1f + Mathf.Sin(t*Mathf.PI*6f + Next01(ref state)*1.7f)*spec.Erosion;
                float centerX = (t-.5f)*spec.Lean*spec.Size.x;
                for (int side=0; side<sides; side++)
                {
                    float a = side*Mathf.PI*2f/sides;
                    float jitter = Mathf.Lerp(.88f,1.12f,Next01(ref state));
                    float rx = spec.Size.x*.5f*profile*shelf*jitter;
                    float rz = spec.Size.z*.5f*profile*Mathf.Lerp(.86f,1.14f,Next01(ref state));
                    vertices.Add(new Vector3(centerX + Mathf.Cos(a)*rx, y, Mathf.Sin(a)*rz));
                    uv.Add(new Vector2(side/(float)sides,t));
                }
            }
            for (int ring=0; ring<ringCount-1; ring++) for (int side=0; side<sides; side++)
            {
                int a=ring*sides+side, b=ring*sides+(side+1)%sides, c=(ring+1)*sides+side, d=(ring+1)*sides+(side+1)%sides;
                triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
            int bottom=vertices.Count; vertices.Add(Vector3.zero); uv.Add(new Vector2(.5f,.5f));
            int top=vertices.Count; vertices.Add(new Vector3(spec.Lean*spec.Size.x*.5f,spec.Size.y,0)); uv.Add(new Vector2(.5f,.5f));
            for (int side=0; side<sides; side++)
            {
                int next=(side+1)%sides;
                triangles.Add(bottom); triangles.Add(next); triangles.Add(side);
                int last=(ringCount-1)*sides; triangles.Add(top); triangles.Add(last+side); triangles.Add(last+next);
            }
            Mesh mesh = new Mesh { name=spec.Id+"_PROCEDURAL" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.SetUVs(0,uv);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }

        private static void BuildCollisionProxy(GameObject root, Vector3 size)
        {
            Vector3[] centers = { new Vector3(-size.x*.27f,size.y*.24f,0), new Vector3(0,size.y*.5f,0), new Vector3(size.x*.28f,size.y*.32f,0) };
            Vector3[] scales = { new Vector3(size.x*.42f,size.y*.48f,size.z), new Vector3(size.x*.36f,size.y,size.z*.82f), new Vector3(size.x*.38f,size.y*.64f,size.z*.9f) };
            for (int i=0;i<3;i++) { BoxCollider c=root.AddComponent<BoxCollider>(); c.center=centers[i]; c.size=scales[i]; }
            root.AddComponent<CoastP01AssetMarker>().assetId = "P01-COL-01";
        }

        private static uint StableSeed(string text) { uint h=2166136261; foreach(char c in text){h^=c;h*=16777619;} return h; }
        private static float Next01(ref uint s) { s ^= s<<13; s ^= s>>17; s ^= s<<5; return (s&0x00ffffff)/16777215f; }
        private static void EnsureFolder(string path)
        {
            string parent=Path.GetDirectoryName(path).Replace('\\','/'), name=Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent,name);
        }
    }
}
