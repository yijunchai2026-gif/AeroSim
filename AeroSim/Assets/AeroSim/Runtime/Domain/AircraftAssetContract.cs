using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AeroSim.Domain
{
    public enum AssetIdentityState { Unverified, ReferenceLocked, GeometryVerified, ProductionApproved, AeroSimGeneric, Retired }
    public enum AssetQualityState { ProgrammaticPrototype, DccCandidate, ImportValidated, ProductionReady, Rejected, Retired }

    public static class AircraftAssetStateMachine
    {
        public static bool CanTransition(AssetIdentityState from, AssetIdentityState to)
        {
            if (from == to) return true;
            if (from == AssetIdentityState.Retired) return false;
            if (to == AssetIdentityState.Retired) return true;
            return (from == AssetIdentityState.Unverified && (to == AssetIdentityState.ReferenceLocked || to == AssetIdentityState.AeroSimGeneric)) ||
                   (from == AssetIdentityState.ReferenceLocked && to == AssetIdentityState.GeometryVerified) ||
                   (from == AssetIdentityState.GeometryVerified && to == AssetIdentityState.ProductionApproved);
        }

        public static bool CanTransition(AssetQualityState from, AssetQualityState to)
        {
            if (from == to) return true;
            if (from == AssetQualityState.Retired) return false;
            if (to == AssetQualityState.Retired || to == AssetQualityState.Rejected) return true;
            return (from == AssetQualityState.ProgrammaticPrototype && to == AssetQualityState.DccCandidate) ||
                   (from == AssetQualityState.DccCandidate && to == AssetQualityState.ImportValidated) ||
                   (from == AssetQualityState.ImportValidated && to == AssetQualityState.ProductionReady);
        }

        public static bool CanClaimModelIdentity(AssetIdentityState identity, AssetQualityState quality) =>
            identity == AssetIdentityState.ProductionApproved && quality == AssetQualityState.ProductionReady;
    }

    [Serializable] public sealed class AircraftAssetManifestDocument
    {
        public string schema, manifestVersion, contractVersion, coordinateSystem, generatedBy;
        public AircraftAssetRecord[] assets;
    }

    [Serializable] public sealed class AircraftAssetRecord
    {
        public string assetId, runtimeId, catalogId, displayName, shortName, brand, referenceModel;
        public string identityState, qualityState, disclosure, sourcePath, sourceFormat, sourceSha256;
        public CameraSocketContract[] cameraSockets;
        public LodContract lod;
        public CollisionContract collision;
        public MaterialContract materials;
        public GoldenAngleContract[] goldenAngles;
        public AssetIdentityState IdentityState => Parse(identityState, AssetIdentityState.Unverified);
        public AssetQualityState QualityState => Parse(qualityState, AssetQualityState.ProgrammaticPrototype);
        private static T Parse<T>(string value, T fallback) where T : struct => Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
    }

    [Serializable] public sealed class CameraSocketContract { public string id, name, purpose; public Vector3 position, rotationEuler; }
    [Serializable] public sealed class LodContract { public string[] names; public float[] screenRelativeHeights; public int[] maximumTriangles; public float cullScreenRelativeHeight; }
    [Serializable] public sealed class CollisionContract { public int minimumPrimitives, maximumPrimitives; public string[] allowedPrimitiveTypes; public bool meshColliderAllowed, visibleGeometryAllowed; }
    [Serializable] public sealed class MaterialContract { public string shader, workflow; public string[] requiredTextureSuffixes; public bool normalMapsLinear, metallicAoLinear, baseColorSrgb, emissionSrgb; }
    [Serializable] public sealed class GoldenAngleContract { public string id; public Vector3 euler; public float distanceMultiplier; }

    public static class AircraftAssetContracts
    {
        public const string ResourcePath = "AircraftAssets/asset-manifest-v1";
        public const string ProductionRoot = "Assets/AeroSim/Art/AircraftProduction/";
        private static AircraftAssetManifestDocument _manifest;
        public static AircraftAssetManifestDocument Manifest => _manifest ??= Load();

        public static AircraftAssetRecord Resolve(string runtimeId) =>
            Manifest?.assets?.FirstOrDefault(x => string.Equals(x.runtimeId, runtimeId, StringComparison.OrdinalIgnoreCase));

        public static string StatusLabel(AircraftAssetRecord asset)
        {
            if (asset == null) return "No RD-02 asset contract";
            return asset.IdentityState == AssetIdentityState.AeroSimGeneric
                ? "AeroSim Generic · " + Humanize(asset.QualityState.ToString())
                : Humanize(asset.QualityState.ToString()) + " / " + Humanize(asset.IdentityState.ToString());
        }

        public static bool Validate(out string report)
        {
            var errors = new List<string>();
            var manifest = Manifest;
            if (manifest == null) errors.Add("Manifest resource is missing or invalid JSON.");
            else
            {
                if (manifest.schema != "aerosim.aircraft-asset-manifest/v1") errors.Add("Unsupported manifest schema.");
                if (string.IsNullOrWhiteSpace(manifest.manifestVersion)) errors.Add("Manifest version is required.");
                if (manifest.coordinateSystem != "Unity:Y-up,+Z-forward,+X-right,meters") errors.Add("Coordinate contract must be Unity Y-up, +Z forward, +X right, meters.");
                if (manifest.assets == null || manifest.assets.Length == 0) errors.Add("Manifest has no assets.");
                else
                {
                    foreach (var group in manifest.assets.GroupBy(x => x.runtimeId, StringComparer.OrdinalIgnoreCase))
                        if (group.Count() > 1) errors.Add("Duplicate runtimeId: " + group.Key);
                    foreach (var asset in manifest.assets) Validate(asset, errors);
                }
            }
            report = errors.Count == 0 ? $"RD-02 PASS · manifest {manifest.manifestVersion} · {manifest.assets.Length} asset contracts." : "RD-02 FAIL\n- " + string.Join("\n- ", errors);
            return errors.Count == 0;
        }

        private static AircraftAssetManifestDocument Load()
        {
            var text = Resources.Load<TextAsset>(ResourcePath);
            return text == null ? null : JsonUtility.FromJson<AircraftAssetManifestDocument>(text.text);
        }

        private static void Validate(AircraftAssetRecord asset, List<string> errors)
        {
            string id = string.IsNullOrWhiteSpace(asset?.runtimeId) ? "<missing-runtime-id>" : asset.runtimeId;
            if (asset == null) { errors.Add("Null asset record."); return; }
            if (string.IsNullOrWhiteSpace(asset.assetId) || string.IsNullOrWhiteSpace(asset.displayName)) errors.Add(id + ": assetId and displayName are required.");
            if (!Enum.TryParse(asset.identityState, true, out AssetIdentityState identity)) errors.Add(id + ": invalid identityState.");
            if (!Enum.TryParse(asset.qualityState, true, out AssetQualityState quality)) errors.Add(id + ": invalid qualityState.");
            if (quality == AssetQualityState.ProgrammaticPrototype && !string.IsNullOrWhiteSpace(asset.sourcePath)) errors.Add(id + ": programmatic prototypes cannot claim a DCC source path.");
            if (quality != AssetQualityState.ProgrammaticPrototype)
            {
                if (string.IsNullOrWhiteSpace(asset.sourcePath) || !asset.sourcePath.StartsWith(ProductionRoot, StringComparison.Ordinal)) errors.Add(id + ": production source must live below the approved root.");
                if (asset.sourceFormat != "fbx" && asset.sourceFormat != "glb") errors.Add(id + ": sourceFormat must be fbx or glb.");
                if (string.IsNullOrWhiteSpace(asset.sourceSha256) || asset.sourceSha256.Length != 64) errors.Add(id + ": SHA-256 is required for DCC assets.");
            }
            if (identity == AssetIdentityState.ProductionApproved && quality != AssetQualityState.ProductionReady) errors.Add(id + ": ProductionApproved requires ProductionReady quality.");
            if (asset.cameraSockets == null || !asset.cameraSockets.Any(x => x.id == "flight-camera") || !asset.cameraSockets.Any(x => x.id == "preview-focus")) errors.Add(id + ": flight-camera and preview-focus sockets are required.");
            if (asset.lod?.names == null || !new[] { "LOD0", "LOD1", "LOD2", "LOD3", "Cull" }.SequenceEqual(asset.lod.names)) errors.Add(id + ": LOD chain must be LOD0/1/2/3/Cull.");
            if (asset.lod == null || asset.lod.cullScreenRelativeHeight <= 0 || asset.lod.screenRelativeHeights?.Length != 4 || asset.lod.maximumTriangles?.Length != 4) errors.Add(id + ": LOD thresholds and budgets are incomplete.");
            if (asset.collision == null || asset.collision.minimumPrimitives < 6 || asset.collision.maximumPrimitives > 16 || asset.collision.meshColliderAllowed || asset.collision.visibleGeometryAllowed) errors.Add(id + ": collision must use 6-16 hidden primitives and no MeshCollider.");
            if (asset.materials == null || asset.materials.shader != "Standard" || asset.materials.workflow != "Metallic" || !asset.materials.normalMapsLinear || !asset.materials.metallicAoLinear || !asset.materials.baseColorSrgb || !asset.materials.emissionSrgb) errors.Add(id + ": Built-in Standard metallic PBR color-space contract is required.");
            if (asset.goldenAngles == null || asset.goldenAngles.Length != 12 || asset.goldenAngles.Select(x => x.id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 12) errors.Add(id + ": exactly 12 unique golden angles are required.");
        }

        private static string Humanize(string value) => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
    }
}
