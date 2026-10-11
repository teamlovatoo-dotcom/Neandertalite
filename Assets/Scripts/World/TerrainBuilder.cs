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

        /// <summary>Per-point layer weights (any scale, normalised internally).</summary>
        public delegate void WeightFunc(float x, float z, float height, float slope, float[] weights);

        /// <summary>
        /// Real Unity Terrain with photo-textured layers blended by <paramref name="weights"/>.
        /// Returns null if a texture set is missing so the caller can fall back to <see cref="Build"/>.
        /// </summary>
        public static Terrain BuildTextured(Transform parent, float size, int heightRes, Func<float, float, float> height,
            TerrainLayer[] layers, WeightFunc weights, int alphaRes = 512, Func<float, float, bool> hole = null)
        {
            foreach (var l in layers) if (l == null) return null;
            float half = size * 0.5f;
            int n = heightRes;
            var hs = new float[n, n];
            float minH = float.MaxValue, maxH = float.MinValue;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float h = height(-half + size * i / (n - 1), -half + size * j / (n - 1));
                hs[j, i] = h;
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
            }
            float range = Mathf.Max(1f, maxH - minH);
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                hs[j, i] = (hs[j, i] - minH) / range;

            var td = new TerrainData { heightmapResolution = n };
            td.size = new Vector3(size, range, size);
            td.SetHeights(0, 0, hs);
            if (hole != null)
            {
                // Openings (e.g. tunnel mouths into the catacombs): false = no terrain there.
                int hr = td.holesResolution;
                var solid = new bool[hr, hr];
                for (int j = 0; j < hr; j++)
                for (int i = 0; i < hr; i++)
                    solid[j, i] = !hole(-half + size * (i + 0.5f) / hr, -half + size * (j + 0.5f) / hr);
                td.SetHoles(0, 0, solid);
            }
            td.terrainLayers = layers;
            td.alphamapResolution = alphaRes;
            int L = layers.Length;
            var alpha = new float[alphaRes, alphaRes, L];
            var w = new float[L];
            for (int j = 0; j < alphaRes; j++)
            for (int i = 0; i < alphaRes; i++)
            {
                float x = -half + size * (i + 0.5f) / alphaRes, z = -half + size * (j + 0.5f) / alphaRes;
                float h = height(x, z);
                float hx = height(x + 1f, z) - h, hz = height(x, z + 1f) - h;
                System.Array.Clear(w, 0, L);
                weights(x, z, h, Mathf.Sqrt(hx * hx + hz * hz), w);
                float sum = 0f;
                for (int k = 0; k < L; k++) { w[k] = Mathf.Max(0f, w[k]); sum += w[k]; }
                if (sum <= 0f) { w[0] = 1f; sum = 1f; }
                for (int k = 0; k < L; k++) alpha[j, i, k] = w[k] / sum;
            }
            td.SetAlphamaps(0, 0, alpha);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-half, minH, -half);
            var t = go.GetComponent<Terrain>();
            t.drawInstanced = true;
            t.heightmapPixelError = 4f;
            t.basemapDistance = 160f;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Terrain/Lit")
                : Shader.Find("Nature/Terrain/Standard");
            if (shader != null) t.materialTemplate = new Material(shader) { name = "TerrainMat" };

            var owned = go.AddComponent<OwnedAssets>();
            owned.Assets.Add(td);
            foreach (var l in layers) owned.Assets.Add(l);
            if (t.materialTemplate != null) owned.Assets.Add(t.materialTemplate);
            return t;
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
