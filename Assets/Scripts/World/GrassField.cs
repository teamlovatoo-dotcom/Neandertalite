using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Neanderthal
{
    /// <summary>
    /// Dense GPU-instanced grass: thousands of blade clumps scattered over the terrain, drawn in chunks
    /// near the camera, with a gentle wind sway on the closest ones. No textures or packages needed.
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        class Chunk
        {
            public Vector3 Center;
            public readonly List<Matrix4x4>[] Matrices = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
            public readonly List<Vector4>[] Seeds = { new List<Vector4>(), new List<Vector4>(), new List<Vector4>() }; // pos.xyz + yaw
            public readonly List<Vector2>[] Shape = { new List<Vector2>(), new List<Vector2>(), new List<Vector2>() }; // scale, lean
        }

        const float ChunkSize = 16f;
        const int MaxPerCall = 1023;

        public float DrawDistance = 75f;
        public float WindDistance = 35f;

        readonly List<Chunk> chunks = new List<Chunk>();
        Mesh mesh;
        Material[] materials;
        readonly List<Matrix4x4> batch = new List<Matrix4x4>(MaxPerCall);

        /// <param name="density">Clumps per square metre where grass is allowed.</param>
        /// <param name="weight">0..1 how much grass grows at (x,z); 0 = none.</param>
        public static GrassField Create(Transform parent, Rect area, float density, Func<float, float, float> height,
            Func<float, float, float> weight, Color[] colors, int seed, float scale = 1f)
        {
            var go = new GameObject("Grass");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<GrassField>();
            g.mesh = BuildClumpMesh(seed);
            g.materials = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                var m = Mat.Get(colors[i % colors.Length], 0.05f);
                m.enableInstancing = true;
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                g.materials[i] = m;
            }

            var rnd = new System.Random(seed);
            int cx = Mathf.CeilToInt(area.width / ChunkSize), cz = Mathf.CeilToInt(area.height / ChunkSize);
            for (int ix = 0; ix < cx; ix++)
            for (int iz = 0; iz < cz; iz++)
            {
                var chunk = new Chunk();
                float x0 = area.x + ix * ChunkSize, z0 = area.y + iz * ChunkSize;
                int count = Mathf.RoundToInt(ChunkSize * ChunkSize * density);
                for (int n = 0; n < count; n++)
                {
                    float x = x0 + (float)rnd.NextDouble() * ChunkSize;
                    float z = z0 + (float)rnd.NextDouble() * ChunkSize;
                    float w = weight(x, z);
                    if (w <= 0f || rnd.NextDouble() > w) continue;
                    float y = height(x, z);
                    int c = rnd.Next(3);
                    float s = scale * (0.7f + (float)rnd.NextDouble() * 0.7f) * Mathf.Lerp(0.6f, 1f, w);
                    float yaw = (float)rnd.NextDouble() * 360f;
                    float lean = ((float)rnd.NextDouble() - 0.5f) * 14f;
                    var list = chunk.Matrices[c];
                    if (list.Count >= MaxPerCall * 4) continue;
                    list.Add(Matrix4x4.TRS(new Vector3(x, y - 0.02f, z), Quaternion.Euler(lean, yaw, 0f), Vector3.one * s));
                    chunk.Seeds[c].Add(new Vector4(x, y - 0.02f, z, yaw));
                    chunk.Shape[c].Add(new Vector2(s, lean));
                }
                chunk.Center = new Vector3(x0 + ChunkSize * 0.5f, height(x0 + ChunkSize * 0.5f, z0 + ChunkSize * 0.5f), z0 + ChunkSize * 0.5f);
                if (chunk.Matrices[0].Count + chunk.Matrices[1].Count + chunk.Matrices[2].Count > 0) g.chunks.Add(chunk);
            }
            return g;
        }

        /// <summary>A clump of ~9 tapered, slightly curved blades. Normals point up for soft, even lighting.</summary>
        static Mesh BuildClumpMesh(int seed)
        {
            var rnd = new System.Random(seed * 7 + 1);
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            int blades = 9;
            for (int b = 0; b < blades; b++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = (float)rnd.NextDouble() * 0.12f;
                Vector3 root = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float h = 0.28f + (float)rnd.NextDouble() * 0.32f;
                float w = 0.025f + (float)rnd.NextDouble() * 0.02f;
                float face = (float)rnd.NextDouble() * Mathf.PI * 2f;
                Vector3 side = new Vector3(Mathf.Cos(face), 0f, Mathf.Sin(face));
                Vector3 bend = new Vector3(-side.z, 0f, side.x) * (0.05f + (float)rnd.NextDouble() * 0.12f);
                int segs = 3;
                int start = verts.Count;
                for (int s = 0; s <= segs; s++)
                {
                    float t = (float)s / segs;
                    float width = w * (1f - t * 0.92f);
                    Vector3 c = root + Vector3.up * h * t + bend * t * t;
                    verts.Add(c - side * width);
                    verts.Add(c + side * width);
                    normals.Add(Vector3.up);
                    normals.Add(Vector3.up);
                }
                for (int s = 0; s < segs; s++)
                {
                    int i0 = start + s * 2;
                    // both windings so blades are visible from either side
                    tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0 + 1, i0 + 2, i0 + 3 });
                    tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0 + 1, i0 + 3, i0 + 2 });
                }
            }
            var mesh = new Mesh { name = "GrassClump" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || mesh == null) return;
            Vector3 cp = cam.transform.position;
            float t = Time.time;
            float maxSq = DrawDistance * DrawDistance, windSq = WindDistance * WindDistance;
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var size = new Vector3(ChunkSize, 6f, ChunkSize);

            foreach (var chunk in chunks)
            {
                float d = (chunk.Center - cp).sqrMagnitude;
                if (d > maxSq) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(chunk.Center, size))) continue;
                bool wind = d < windSq;
                for (int c = 0; c < 3; c++)
                {
                    var list = chunk.Matrices[c];
                    if (list.Count == 0) continue;
                    if (wind)
                    {
                        // Recompute sway for nearby clumps only.
                        var seeds = chunk.Seeds[c];
                        var shape = chunk.Shape[c];
                        for (int i = 0; i < list.Count; i++)
                        {
                            Vector4 sd = seeds[i];
                            float sway = Mathf.Sin(t * 1.7f + sd.x * 0.35f + sd.z * 0.27f) * 7f + Mathf.Sin(t * 3.1f + sd.x) * 2f;
                            list[i] = Matrix4x4.TRS(new Vector3(sd.x, sd.y, sd.z), Quaternion.Euler(shape[i].y + sway, sd.w, sway * 0.4f), Vector3.one * shape[i].x);
                        }
                    }
                    for (int start = 0; start < list.Count; start += MaxPerCall)
                    {
                        int n = Mathf.Min(MaxPerCall, list.Count - start);
                        batch.Clear();
                        for (int i = 0; i < n; i++) batch.Add(list[start + i]);
                        Graphics.DrawMeshInstanced(mesh, 0, materials[c], batch, null, ShadowCastingMode.Off, true);
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
