using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// CC0 photo textures from Poly Haven stored in Resources/Textures as name_diff.jpg / name_nor_gl.jpg.
    /// Everything degrades gracefully to procedural colours when a file is missing.
    /// </summary>
    public static class TextureLibrary
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Albedo(string name) => Load(name + "_diff");
        public static Texture2D Normal(string name) => Load(name + "_nor_gl");
        public static bool Has(string name) => Albedo(name) != null;

        static Texture2D Load(string file)
        {
            if (cache.TryGetValue(file, out var t)) return t;
            t = Resources.Load<Texture2D>("Textures/" + file);
            if (t != null)
            {
                t.wrapMode = TextureWrapMode.Repeat;
                t.anisoLevel = 8;
            }
            cache[file] = t;
            return t;
        }

        /// <summary>Terrain layer from a texture set; tileSize in metres.</summary>
        public static TerrainLayer Layer(string name, float tileSize, float smoothness = 0.05f)
        {
            var albedo = Albedo(name);
            if (albedo == null) return null;
            var layer = new TerrainLayer
            {
                name = name,
                diffuseTexture = albedo,
                normalMapTexture = Normal(name),
                tileSize = new Vector2(tileSize, tileSize),
                normalScale = 1f,
                smoothness = smoothness,
                metallic = 0f
            };
            return layer;
        }
    }
}
