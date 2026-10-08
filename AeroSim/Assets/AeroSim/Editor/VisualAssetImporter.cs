using UnityEditor;
using UnityEngine;

namespace AeroSim.Editor
{
    public sealed class VisualAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if ((!assetPath.Contains("/Art/MaterialsV2/") && !assetPath.Contains("/Art/AircraftV3/")) || !assetPath.EndsWith(".png")) return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;
            bool foliage=assetPath.Contains("evergreen-needles");
            importer.sRGBTexture=true; importer.alphaSource=foliage?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
            importer.alphaIsTransparency=foliage;
            importer.mipmapEnabled=true; importer.wrapMode=foliage?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
            importer.mipMapsPreserveCoverage=foliage; importer.alphaTestReferenceValue=.42f;
            importer.filterMode=FilterMode.Trilinear; importer.anisoLevel=8;
            importer.npotScale=TextureImporterNPOTScale.ToNearest; importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name="iPhone",overridden=true,maxTextureSize=1024,
                format=TextureImporterFormat.ASTC_6x6,compressionQuality=70
            });
        }
    }
}
