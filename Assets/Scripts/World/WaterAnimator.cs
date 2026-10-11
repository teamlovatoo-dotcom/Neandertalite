using UnityEngine;

namespace Neanderthal
{
    /// <summary>Scrolls a procedural ripple normal map over the water material so the surface moves and sparkles.</summary>
    public class WaterAnimator : MonoBehaviour
    {
        static Texture2D ripples;
        public Material Target;
        public Vector2 Flow = new Vector2(0.015f, 0.04f);

        public static Texture2D Ripples
        {
            get
            {
                if (ripples != null) return ripples;
                int s = 256;
                var h = new float[s, s];
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (float)x / s * Mathf.PI * 2f, v = (float)y / s * Mathf.PI * 2f;
                    // tileable sum of waves
                    h[x, y] = Mathf.Sin(u * 3 + v * 2) * 0.5f + Mathf.Sin(u * 5 - v * 4 + 1.3f) * 0.3f + Mathf.Sin(-u * 9 + v * 7 + 2.1f) * 0.15f + Mathf.Sin(u * 13 + v * 11) * 0.08f;
                }
                ripples = new Texture2D(s, s, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat };
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = h[(x + 1) % s, y] - h[(x - 1 + s) % s, y];
                    float dy = h[x, (y + 1) % s] - h[x, (y - 1 + s) % s];
                    var n = new Vector3(-dx * 1.6f, -dy * 1.6f, 1f).normalized;
                    ripples.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
                ripples.Apply(true);
                return ripples;
            }
        }

        public static void Attach(GameObject water, Material mat, float tiling)
        {
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", Ripples);
                mat.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.6f);
            }
            water.AddComponent<WaterAnimator>().Target = mat;
        }

        void Update()
        {
            if (Target != null && Target.HasProperty("_BumpMap"))
                Target.SetTextureOffset("_BumpMap", Flow * Time.time);
        }
    }
}
