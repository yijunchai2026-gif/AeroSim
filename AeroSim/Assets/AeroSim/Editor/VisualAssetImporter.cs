using UnityEditor;
using UnityEngine;

namespace AeroSim.Editor
{
    public sealed class VisualAssetImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath.Contains("/Art/DesignV1/") && assetPath.EndsWith(".png"))
            {
                var ui = (TextureImporter)assetImporter;
                ui.textureType = TextureImporterType.Default;
                ui.sRGBTexture = true; ui.alphaSource = TextureImporterAlphaSource.FromInput;
                ui.alphaIsTransparency = true; ui.mipmapEnabled = false; ui.wrapMode = TextureWrapMode.Clamp;
                ui.filterMode = FilterMode.Bilinear; ui.npotScale = TextureImporterNPOTScale.None;
                ui.maxTextureSize = 2048; ui.textureCompression = TextureImporterCompression.Uncompressed;
                ui.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                    name = "iPhone", overridden = true, maxTextureSize = 2048,
                    format = TextureImporterFormat.ASTC_6x6, compressionQuality = 80
                });
                return;
            }
            if (assetPath.Contains("/Art/CoastProduction/") && assetPath.EndsWith(".png"))
            {
                var coast = (TextureImporter)assetImporter;
                bool atlas = assetPath.Contains("vegetation-atlas") || assetPath.Contains("foam-atlas") ||
                    assetPath.Contains("weather-atlas") || assetPath.Contains("decal_atlas");
                coast.textureType = TextureImporterType.Default;
                coast.sRGBTexture = true;
                coast.alphaSource = TextureImporterAlphaSource.FromInput;
                coast.alphaIsTransparency = false;
                coast.mipmapEnabled = true;
                coast.streamingMipmaps = true;
                coast.wrapMode = atlas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                coast.filterMode = FilterMode.Trilinear;
                coast.anisoLevel = 8;
                coast.npotScale = TextureImporterNPOTScale.ToNearest;
                coast.maxTextureSize = 4096;
                coast.textureCompression = TextureImporterCompression.CompressedHQ;
                coast.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                    name = "Standalone", overridden = true, maxTextureSize = 4096,
                    format = TextureImporterFormat.Automatic, compressionQuality = 80
                });
                coast.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                    name = "iPhone", overridden = true, maxTextureSize = 2048,
                    format = TextureImporterFormat.ASTC_6x6, compressionQuality = 70
                });
                return;
            }
            if ((!assetPath.Contains("/Art/MaterialsV2/") && !assetPath.Contains("/Art/AircraftV3/")) || !assetPath.EndsWith(".png")) return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;
            bool foliage=assetPath.Contains("evergreen-needles");
            importer.sRGBTexture=true; importer.alphaSource=foliage?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
            importer.alphaIsTransparency=foliage;
            importer.mipmapEnabled=true; importer.streamingMipmaps=true; importer.wrapMode=foliage?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
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
