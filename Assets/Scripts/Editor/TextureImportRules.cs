using UnityEditor;

namespace Neanderthal.EditorTools
{
    /// <summary>Imports *_nor_gl textures as normal maps and keeps photo textures repeatable and crisp.</summary>
    public class TextureImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            ti.anisoLevel = 8;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 1024;
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
