using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Runtime material factory. Works with the Built-in pipeline (Standard shader) and URP (Lit).
    /// Materials are cached by key so hundreds of rocks/trees share a handful of materials.
    /// </summary>
    public static class Mat
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Shader litShader;
        static Shader unlitShader;

        // Palette inspired by the concept art: dusty earth, moss, warm firelight, cold blue tower.
        public static readonly Color Skin = new Color(0.47f, 0.32f, 0.23f);
        public static readonly Color Hair = new Color(0.12f, 0.08f, 0.06f);
        public static readonly Color Fur = new Color(0.30f, 0.21f, 0.13f);
        public static readonly Color FurDark = new Color(0.18f, 0.13f, 0.09f);
        public static readonly Color Wood = new Color(0.40f, 0.27f, 0.15f);
        public static readonly Color WoodDark = new Color(0.25f, 0.17f, 0.10f);
        public static readonly Color Stone = new Color(0.45f, 0.44f, 0.42f);
        public static readonly Color Flint = new Color(0.33f, 0.33f, 0.36f);
        public static readonly Color Bone = new Color(0.86f, 0.82f, 0.70f);
        public static readonly Color Leather = new Color(0.36f, 0.24f, 0.14f);
        public static readonly Color Sinew = new Color(0.75f, 0.68f, 0.52f);
        public static readonly Color Metal = new Color(0.22f, 0.23f, 0.25f);
        public static readonly Color MetalLight = new Color(0.55f, 0.56f, 0.58f);
        public static readonly Color Leaves = new Color(0.16f, 0.27f, 0.12f);
        public static readonly Color LeavesDark = new Color(0.10f, 0.19f, 0.09f);
        public static readonly Color Bark = new Color(0.27f, 0.20f, 0.14f);
        public static readonly Color Rock = new Color(0.38f, 0.36f, 0.33f);
        public static readonly Color RockRed = new Color(0.55f, 0.30f, 0.20f);
        public static readonly Color Sand = new Color(0.66f, 0.52f, 0.38f);
        public static readonly Color Concrete = new Color(0.50f, 0.48f, 0.45f);
        public static readonly Color Water = new Color(0.10f, 0.22f, 0.26f);
        public static readonly Color TeamBlue = new Color(0.20f, 0.42f, 0.75f);
        public static readonly Color TeamRed = new Color(0.72f, 0.18f, 0.16f);
        public static readonly Color Fire = new Color(1.0f, 0.55f, 0.15f);
        public static readonly Color TowerGlow = new Color(0.45f, 0.75f, 1.0f);

        static Shader Lit
        {
            get
            {
                if (litShader == null)
                {
                    litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (litShader == null) litShader = Shader.Find("Standard");
                    if (litShader == null) litShader = Shader.Find("Legacy Shaders/Diffuse");
                }
                return litShader;
            }
        }

        static Shader Unlit
        {
            get
            {
                if (unlitShader == null)
                {
                    unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
                    if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
                }
                return unlitShader;
            }
        }

        public static Material Get(Color color, float smoothness = 0.1f, float metallic = 0f)
        {
            string key = $"lit_{ColorUtility.ToHtmlStringRGBA(color)}_{smoothness:0.00}_{metallic:0.00}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Lit) { name = key, color = color, enableInstancing = true };
            SetFloat(m, "_Glossiness", smoothness);
            SetFloat(m, "_Smoothness", smoothness);
            SetFloat(m, "_Metallic", metallic);
            cache[key] = m;
            return m;
        }

        public static Material Emissive(Color color, float intensity = 2f)
        {
            string key = $"emit_{ColorUtility.ToHtmlStringRGBA(color)}_{intensity:0.0}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Lit) { name = key, color = color };
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * intensity);
            cache[key] = m;
            return m;
        }

        public static Material Flat(Color color)
        {
            string key = $"unlit_{ColorUtility.ToHtmlStringRGBA(color)}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Unlit) { name = key, color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            cache[key] = m;
            return m;
        }

        public static Material Textured(string key, Texture2D tex, float smoothness = 0.05f, Vector2? tiling = null)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Lit) { name = key, color = Color.white, mainTexture = tex, enableInstancing = true };
            if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            SetFloat(m, "_Glossiness", smoothness);
            SetFloat(m, "_Smoothness", smoothness);
            cache[key] = m;
            return m;
        }

        static void SetFloat(Material m, string prop, float v)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        /// <summary>Procedural noisy texture used for rock, bark and ground details.</summary>
        public static Texture2D NoiseTexture(int size, Color a, Color b, float scale, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[size * size];
            float ox = seed * 13.37f, oy = seed * 7.77f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                // Tileable fbm: sample on a torus-ish wrap by blending offsets.
                float n = Fbm(u * scale + ox, v * scale + oy, 4);
                float grain = Random01(x, y, seed) * 0.12f;
                px[y * size + x] = Color.Lerp(a, b, Mathf.Clamp01(n + grain - 0.06f));
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        public static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum / 0.9375f;
        }

        public static float Random01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
