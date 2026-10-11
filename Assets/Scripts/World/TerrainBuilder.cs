using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Neanderthal
{
    /// <summary>
    /// Builds a heightfield mesh + collider from an analytic height function, with a baked color texture.
    /// Using a plain mesh (instead of Unity Terrain) keeps it render-pipeline agnostic.
    /// </summary>
    public static class TerrainBuilder
    {
        static int terrainCounter;

        public delegate Color ColorFunc(float x, float z, float height, float slope);

        public static GameObject Build(Transform parent, float size, int resolution, Func<float, float, float> height, ColorFunc color, int texSize = 1024)
        {
            var go = new GameObject("Terrain");
            go.transform.SetParent(parent, false);
            int n = resolution + 1;
            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            float half = size * 0.5f;
            float step = size / resolution;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = -half + i * step, z = -half + j * step;
                verts[j * n + i] = new Vector3(x, height(x, z), z);
                uvs[j * n + i] = new Vector2((float)i / resolution, (float)j / resolution);
            }
            var tris = new int[resolution * resolution * 6];
            int t = 0;
            for (int j = 0; j < resolution; j++)
            for (int i = 0; i < resolution; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
            var mesh = new Mesh { name = "TerrainMesh" };
            if (verts.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var tex = BakeTexture(size, texSize, height, color);
            mr.sharedMaterial = Mat.Textured("terrain_" + (++terrainCounter), tex, 0.05f);
            var owned = go.AddComponent<OwnedAssets>();
            owned.Assets.Add(tex);
            owned.Assets.Add(mesh);
            owned.Assets.Add(mr.sharedMaterial);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static Texture2D BakeTexture(float size, int texSize, Func<float, float, float> height, ColorFunc color)
        {
            var tex = new Texture2D(texSize, texSize, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var px = new Color[texSize * texSize];
            float half = size * 0.5f;
            float d = size / texSize;
            for (int j = 0; j < texSize; j++)
            for (int i = 0; i < texSize; i++)
            {
                float x = -half + (i + 0.5f) * d, z = -half + (j + 0.5f) * d;
                float h = height(x, z);
                float hx = height(x + 1f, z) - h;
                float hz = height(x, z + 1f) - h;
                float slope = Mathf.Sqrt(hx * hx + hz * hz);
                Color c = color(x, z, h, slope);
                // Per-pixel grain so it doesn't look airbrushed.
                float g = (Mat.Random01(i, j, 7) - 0.5f) * 0.06f;
                px[j * texSize + i] = new Color(c.r + g, c.g + g, c.b + g);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Shared natural-ground palette used by the forest maps.</summary>
        public static Color NaturalGround(float x, float z, float h, float slope, float pathMask, float sandMask)
        {
            float n = Mat.Fbm(x * 0.05f + 3.1f, z * 0.05f + 1.7f, 3);
            float n2 = Mat.Fbm(x * 0.4f, z * 0.4f, 2);
            Color grass = Color.Lerp(new Color(0.24f, 0.30f, 0.13f), new Color(0.36f, 0.36f, 0.17f), n);
            grass = Color.Lerp(grass, new Color(0.18f, 0.24f, 0.11f), n2 * 0.5f);
            Color dirt = Color.Lerp(new Color(0.36f, 0.29f, 0.2f), new Color(0.28f, 0.22f, 0.15f), n2);
            Color rock = Color.Lerp(new Color(0.42f, 0.4f, 0.37f), new Color(0.3f, 0.29f, 0.27f), n2);
            Color sand = new Color(0.5f, 0.45f, 0.35f);
            Color c = Color.Lerp(grass, dirt, Mathf.Clamp01(pathMask));
            c = Color.Lerp(c, sand, Mathf.Clamp01(sandMask));
            c = Color.Lerp(c, rock, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((slope - 0.45f) * 2.5f)));
            return c;
        }
    }
}
