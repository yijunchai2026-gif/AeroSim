#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AeroSim.Domain;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AeroSim.Editor
{
    public sealed class AircraftDccImportGate : AssetPostprocessor
    {
        private bool IsProductionAsset => assetPath.StartsWith(AircraftAssetContracts.ProductionRoot, StringComparison.Ordinal);

        private void OnPreprocessModel()
        {
            if (!IsProductionAsset) return;
            var asset = RequireRecord(assetPath);
            string extension = Path.GetExtension(assetPath).TrimStart('.').ToLowerInvariant();
            if (extension != "fbx" && extension != "glb") throw new InvalidDataException("RD-02 only accepts FBX or GLB production aircraft: " + assetPath);
            if (!string.Equals(extension, asset.sourceFormat, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("RD-02 source format does not match manifest: " + assetPath);
            if (asset.QualityState == AssetQualityState.ProgrammaticPrototype) throw new InvalidDataException("RD-02 programmatic prototypes cannot enter the DCC gate: " + assetPath);
            VerifySha(assetPath, asset.sourceSha256);

            if (assetImporter is ModelImporter importer)
            {
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsProductionAsset) return;
            var asset = RequireRecord(assetPath);
            AircraftProductionValidator.MaterializeCollisionProxies(root);
            AircraftProductionValidator.ValidateImportedModel(root, asset, assetPath);
        }

        private void OnPreprocessTexture()
        {
            if (!IsProductionAsset || !(assetImporter is TextureImporter importer)) return;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (name.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else if (name.EndsWith("_MetallicAO", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_Mask", StringComparison.OrdinalIgnoreCase)) importer.sRGBTexture = false;
            else if (name.EndsWith("_BaseColor", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_Emission", StringComparison.OrdinalIgnoreCase)) importer.sRGBTexture = true;
        }

        internal static AircraftAssetRecord RequireRecord(string path)
        {
            var asset = AircraftAssetContracts.Manifest?.assets?.FirstOrDefault(x => string.Equals(x.sourcePath, path, StringComparison.Ordinal));
            if (asset == null) throw new InvalidDataException("RD-02 production asset is not explicitly addressed by the manifest: " + path);
            return asset;
        }

        internal static void VerifySha(string path, string expected)
        {
            if (string.IsNullOrWhiteSpace(expected) || expected.Length != 64) throw new InvalidDataException("RD-02 requires a 64-character SHA-256 for " + path);
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            string actual = string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("RD-02 SHA-256 mismatch for " + path);
        }
    }

    public static class AircraftProductionValidator
    {
        [MenuItem("AeroSim/RD-02/Validate Asset Manifest and Production Imports")]
        public static void ValidateMenu()
        {
            ValidateOrThrow();
            Debug.Log(AircraftAssetContracts.Validate(out string report) ? report : "RD-02 validation failed unexpectedly.");
        }

        public static void ValidateOrThrow()
        {
            if (!AircraftAssetContracts.Validate(out string report)) throw new BuildFailedException(report);
            foreach (var asset in AircraftAssetContracts.Manifest.assets.Where(x => x.QualityState != AssetQualityState.ProgrammaticPrototype))
            {
                if (!File.Exists(asset.sourcePath)) throw new BuildFailedException("RD-02 production source is missing: " + asset.sourcePath);
                AircraftDccImportGate.VerifySha(asset.sourcePath, asset.sourceSha256);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(asset.sourcePath);
                if (root == null) throw new BuildFailedException("RD-02 production source could not be loaded as a model: " + asset.sourcePath);
                ValidateImportedModel(root, asset, asset.sourcePath);
            }
        }

        internal static void MaterializeCollisionProxies(GameObject root)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("COL_", StringComparison.Ordinal)))
            {
                var filter = node.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || node.GetComponent<Collider>() != null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                if (node.name.StartsWith("COL_BOX_", StringComparison.Ordinal))
                {
                    var collider = node.gameObject.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                }
                else if (node.name.StartsWith("COL_SPHERE_", StringComparison.Ordinal))
                {
                    var collider = node.gameObject.AddComponent<SphereCollider>(); collider.center = bounds.center; collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                }
                else if (node.name.StartsWith("COL_CAPSULE_", StringComparison.Ordinal))
                {
                    var collider = node.gameObject.AddComponent<CapsuleCollider>(); collider.center = bounds.center; collider.direction = 1;
                    collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.z); collider.height = Mathf.Max(collider.radius * 2f, bounds.size.y);
                }
                var renderer = node.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
            }
        }

        internal static void ValidateImportedModel(GameObject root, AircraftAssetRecord asset, string path)
        {
            var errors = new List<string>();
            if (root.transform.localPosition != Vector3.zero || root.transform.localRotation != Quaternion.identity || root.transform.localScale != Vector3.one) errors.Add("root TRS must be identity");
            foreach (var socket in asset.cameraSockets ?? Array.Empty<CameraSocketContract>()) if (Find(root.transform, socket.name) == null) errors.Add("missing camera socket " + socket.name);
            var lodGroup = root.GetComponentInChildren<LODGroup>(true);
            if (lodGroup == null || lodGroup.GetLODs().Length != 5) errors.Add("LODGroup must contain LOD0/LOD1/LOD2/LOD3/Cull");
            else
            {
                var lods = lodGroup.GetLODs();
                for (int i = 0; i < 4; i++)
                {
                    int triangles = lods[i].renderers.Where(r => r != null).Sum(r => TriangleCount(r));
                    if (triangles > asset.lod.maximumTriangles[i]) errors.Add($"LOD{i} exceeds triangle budget");
                }
                if (lods[4].renderers.Any(r => r != null)) errors.Add("Cull LOD must not contain renderers");
            }
            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length < asset.collision.minimumPrimitives || colliders.Length > asset.collision.maximumPrimitives) errors.Add("collision primitive count is outside manifest range");
            if (colliders.Any(x => x is MeshCollider)) errors.Add("MeshCollider is forbidden");
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials.Where(x => x != null))
                    if (material.shader == null || material.shader.name != "Standard") errors.Add("all visible materials must use Built-in Standard");
            if (errors.Count > 0) throw new InvalidDataException("RD-02 import gate rejected " + path + ":\n- " + string.Join("\n- ", errors));
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }

        private static int TriangleCount(Renderer renderer)
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            return mesh == null ? 0 : mesh.triangles.Length / 3;
        }
    }

    public sealed class AircraftProductionBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -200;
        public void OnPreprocessBuild(BuildReport report) => AircraftProductionValidator.ValidateOrThrow();
    }
}
#endif
