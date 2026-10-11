using UnityEditor;

namespace Neanderthal.EditorTools
{
    /// <summary>Imports *_nor_gl textures as normal maps and keeps photo textures repeatable and crisp.</summary>
    public class TextureImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath.Contains("/Resources/Sky/"))
            {
                // Equirectangular HDR skies: no mips (they seam at the wrap), full resolution.
                var sky = (TextureImporter)assetImporter;
                sky.mipmapEnabled = false;
                sky.wrapModeU = UnityEngine.TextureWrapMode.Repeat;
                sky.wrapModeV = UnityEngine.TextureWrapMode.Clamp;
                sky.maxTextureSize = 4096;
                sky.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }
            if (!assetPath.Contains("/Resources/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            ti.anisoLevel = 8;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 2048;
            if (assetPath.Contains("/fx_"))
            {
                // Particle flipbooks / sprites.
                ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                ti.alphaIsTransparency = true;
                ti.anisoLevel = 1;
                return;
            }
            if (assetPath.EndsWith("_diff.png"))
            {
                ti.alphaIsTransparency = true;
                ti.mipMapsPreserveCoverage = true;
                ti.alphaTestReferenceValue = 0.42f;
            }
            if (assetPath.Contains("_nor_gl"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
            }
        }
    }
}
