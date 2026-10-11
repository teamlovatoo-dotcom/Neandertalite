using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Game-ready conifers: a tapered bark trunk plus whorls of alpha-tested twig cards (fir twig atlas from
    /// Poly Haven, CC0). Four mesh variants are built once and shared by every tree.
    /// </summary>
    public static class TreeFactory
    {
        const float RefHeight = 12f;
        static Mesh[] variants;
        static Material barkMat, needleMat;
        static bool checkedTextures, available;

        public static bool Available
        {
            get
            {
                if (!checkedTextures)
                {
                    checkedTextures = true;
                    available = TextureLibrary.Has("fir_twig");
                }
                return available;
            }
        }

        static Material NeedleMat()
        {
            if (needleMat != null) return needleMat;
            needleMat = Mat.Pbr("fir_twig", Mat.Leaves, 0.15f, null, new Color(1f, 1.08f, 0.95f));
            Mat.MakeCutout(needleMat, 0.42f);
            return needleMat;
        }

        static Material BarkMat()
        {
            if (barkMat == null) barkMat = Mat.Pbr("bark", Mat.Bark, 0.05f);
            return barkMat;
        }

        static Mesh[] broadVariants;
        static Material leafMat;

        public static bool BroadleafAvailable => TextureLibrary.Has("leaves") && TextureLibrary.Has("bark");

        static Material LeafMat()
        {
            if (leafMat != null) return leafMat;
            leafMat = Mat.Pbr("leaves", Mat.Leaves, 0.2f, null, new Color(1f, 1.05f, 0.95f));
            Mat.MakeCutout(leafMat, 0.45f);
            return leafMat;
        }

        /// <summary>Broadleaf tree: bark trunk with a few limbs and a rounded crown of crossed leaf cards.</summary>
        public static GameObject Broadleaf(Transform parent, Vector3 pos, float height, int seed)
        {
            if (broadVariants == null)
            {
                broadVariants = new Mesh[3];
                for (int i = 0; i < broadVariants.Length; i++) broadVariants[i] = BuildBroadleaf(2000 + i * 53);
            }
            var go = new GameObject("Tree");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var rnd = new System.Random(seed);
            go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            float s = height / 10f;
            go.transform.localScale = Vector3.one * s;
            go.AddComponent<MeshFilter>().sharedMesh = broadVariants[rnd.Next(broadVariants.Length)];
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { BarkMat(), LeafMat() };
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.4f;
            col.height = 5f;
            col.center = new Vector3(0f, 2.5f, 0f);
            return go;
        }

        static void Limb(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris, Vector3 a, Vector3 b, float ra, float rb, int sides)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 side2 = Vector3.Cross(axis, side);
            int start = v.Count;
            float len = (b - a).magnitude;
            for (int j = 0; j <= 1; j++)
            for (int i = 0; i <= sides; i++)
            {
                float ang = i * Mathf.PI * 2f / sides;
                Vector3 dir = side * Mathf.Cos(ang) + side2 * Mathf.Sin(ang);
                v.Add((j == 0 ? a : b) + dir * (j == 0 ? ra : rb));
                n.Add(dir);
                uv.Add(new Vector2(i / (float)sides, j * len / 2.5f));
            }
            for (int i = 0; i < sides; i++)
            {
                int i0 = start + i, i1 = i0 + 1, i2 = start + sides + 1 + i, i3 = i2 + 1;
                tris.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
            }
        }

        static Mesh BuildBroadleaf(int seed)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var bark = new List<int>(); var leaves = new List<int>();
            float H = 10f;
            Vector3 top = new Vector3((R() - 0.5f) * 0.4f, H * 0.5f, (R() - 0.5f) * 0.4f);
            Limb(v, n, uv, bark, Vector3.down * 0.2f, new Vector3(0, H * 0.25f, 0), 0.42f, 0.32f, 8);
            Limb(v, n, uv, bark, new Vector3(0, H * 0.25f, 0), top, 0.32f, 0.24f, 8);
            Vector3 crown = new Vector3(top.x, H * 0.68f, top.z);
            int limbs = 4 + rnd.Next(2);
            for (int i = 0; i < limbs; i++)
            {
                float a = i * Mathf.PI * 2f / limbs + R() * 0.6f;
                Vector3 end = crown + new Vector3(Mathf.Cos(a) * H * 0.22f, (R() - 0.2f) * H * 0.15f, Mathf.Sin(a) * H * 0.22f);
                Limb(v, n, uv, bark, top, end, 0.18f, 0.05f, 6);
            }
            // Crown of leaf clumps (3 crossed cards each), normals radiate from the crown centre.
            Vector3 radii = new Vector3(H * 0.33f, H * 0.24f, H * 0.33f);
            int clumps = 38;
            for (int c = 0; c < clumps; c++)
            {
                Vector3 dir = new Vector3(R() * 2f - 1f, R() * 2f - 1f, R() * 2f - 1f).normalized;
                float r = Mathf.Lerp(0.55f, 1f, Mathf.Sqrt(R()));
                Vector3 p = crown + Vector3.Scale(dir, radii) * r;
                Vector3 normal = (Vector3.Scale(p - crown, new Vector3(1f / radii.x, 1f / radii.y, 1f / radii.z)).normalized + Vector3.up * 0.4f).normalized;
                float size = H * (0.16f + R() * 0.06f);
                int cell = rnd.Next(2);
                for (int k = 0; k < 3; k++)
                {
                    var rot = Quaternion.Euler(R() * 360f, R() * 360f, R() * 360f);
                    Vector3 up = rot * Vector3.up, right = rot * Vector3.right;
                    AddLeafCard(v, n, uv, leaves, p - up * size * 0.5f, up, right, size, size * 0.55f, cell, normal);
                }
            }
            var mesh = new Mesh { name = "Broadleaf_" + seed };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(bark, 0);
            mesh.SetTriangles(leaves, 1);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddLeafCard(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris,
            Vector3 basePos, Vector3 dir, Vector3 side, float length, float width, int cell, Vector3 normal)
        {
            float u0 = cell * 0.5f;
            Vector3 tip = basePos + dir * length;
            for (int face = 0; face < 2; face++)
            {
                int s = v.Count;
                v.Add(basePos - side * width * 0.5f); v.Add(basePos + side * width * 0.5f);
                v.Add(tip + side * width * 0.5f); v.Add(tip - side * width * 0.5f);
                uv.Add(new Vector2(u0, 0f)); uv.Add(new Vector2(u0 + 0.5f, 0f));
                uv.Add(new Vector2(u0 + 0.5f, 1f)); uv.Add(new Vector2(u0, 1f));
                for (int i = 0; i < 4; i++) n.Add(normal);
                if (face == 0) tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
                else tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
        }

        public static GameObject Pine(Transform parent, Vector3 pos, float height, int seed)
        {
            if (variants == null)
            {
                variants = new Mesh[4];
                for (int i = 0; i < variants.Length; i++) variants[i] = BuildPine(1000 + i * 37);
            }
            var go = new GameObject("Pine");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var rnd = new System.Random(seed);
            go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            float s = height / RefHeight;
            go.transform.localScale = new Vector3(s * (0.9f + (float)rnd.NextDouble() * 0.2f), s, s * (0.9f + (float)rnd.NextDouble() * 0.2f));
            go.AddComponent<MeshFilter>().sharedMesh = variants[rnd.Next(variants.Length)];
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { BarkMat(), NeedleMat() };
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = RefHeight * 0.03f;
            col.height = RefHeight;
            col.center = new Vector3(0f, RefHeight * 0.5f, 0f);
            return go;
        }

        static Mesh BuildPine(int seed)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var trunk = new List<int>();
            var needles = new List<int>();
            float H = RefHeight;
            float r0 = H * 0.028f;

            // Trunk: 8-sided tapered cylinder, UVs in metres for the bark texture.
            int sides = 8, rings = 6;
            for (int j = 0; j <= rings; j++)
            {
                float t = (float)j / rings;
                float r = Mathf.Lerp(r0, r0 * 0.12f, t) * (j == 0 ? 1.3f : 1f);
                float y = t * H;
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(dir * r + Vector3.up * y);
                    n.Add(dir);
                    uv.Add(new Vector2(i / (float)sides * 1.2f, y / 2.5f));
                }
            }
            for (int j = 0; j < rings; j++)
            for (int i = 0; i < sides; i++)
            {
                int a = j * (sides + 1) + i, b = a + 1, c = a + sides + 1, d = c + 1;
                trunk.AddRange(new[] { a, c, b, b, c, d });
            }

            // Branch whorls
            int whorls = 9 + rnd.Next(4);
            for (int w = 0; w < whorls; w++)
            {
                float t = Mathf.Lerp(0.26f, 0.95f, w / (float)(whorls - 1)) + (R() - 0.5f) * 0.02f;
                float y = t * H;
                float len = Mathf.Lerp(H * 0.34f, H * 0.07f, Mathf.Pow(t, 1.15f)) * (0.85f + R() * 0.3f);
                float droop = Mathf.Lerp(-28f, 8f, t);
                int count = 5 + rnd.Next(3);
                float offset = R() * 360f;
                for (int b = 0; b < count; b++)
                {
                    float yaw = offset + b * 360f / count + (R() - 0.5f) * 20f;
                    var dir = Quaternion.Euler(0f, yaw, 0f) * (Quaternion.Euler(-droop + (R() - 0.5f) * 10f, 0f, 0f) * Vector3.forward);
                    var basePos = new Vector3(0f, y, 0f) + new Vector3(dir.x, 0f, dir.z).normalized * r0 * (1f - t) * 0.9f;
                    float width = len * (0.75f + R() * 0.25f);
                    var side = Vector3.Cross(Vector3.up, dir).normalized;
                    var up = Vector3.Cross(dir, side).normalized;
                    int cell = rnd.Next(4);
                    AddCard(v, n, uv, needles, basePos, dir, side, len, width, cell);
                    // Second card, rolled ~70 degrees, gives the branch volume.
                    var side2 = (side * 0.35f + up * 0.94f).normalized;
                    AddCard(v, n, uv, needles, basePos + Vector3.up * 0.05f, dir, side2, len * 0.9f, width * 0.85f, (cell + 1) % 4);
                }
            }
            // Crown
            for (int c = 0; c < 3; c++)
            {
                float yaw = c * 60f;
                var dir = (Vector3.up * 3f + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 0.3f).normalized;
                var side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                AddCard(v, n, uv, needles, new Vector3(0f, H * 0.9f, 0f), dir, side, H * 0.16f, H * 0.1f, rnd.Next(4));
            }

            var mesh = new Mesh { name = "Pine_" + seed };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trunk, 0);
            mesh.SetTriangles(needles, 1);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Double-sided card from base along dir; UV v runs from stem (0) to tip (1) inside an atlas cell.</summary>
        static void AddCard(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris,
            Vector3 basePos, Vector3 dir, Vector3 side, float length, float width, int cell)
        {
            float u0 = (cell % 2) * 0.5f, v0 = cell < 2 ? 0.5f : 0f;
            Vector3 tip = basePos + dir * length;
            // Foliage normals point outwards and up: soft, volumetric lighting on both faces.
            Vector3 outward = new Vector3(dir.x, 0f, dir.z);
            Vector3 normal = (outward.normalized * 0.55f + Vector3.up * 0.85f).normalized;
            for (int face = 0; face < 2; face++)
            {
                int s = v.Count;
                v.Add(basePos - side * width * 0.5f);
                v.Add(basePos + side * width * 0.5f);
                v.Add(tip + side * width * 0.5f);
                v.Add(tip - side * width * 0.5f);
                uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u0 + 0.5f, v0));
                uv.Add(new Vector2(u0 + 0.5f, v0 + 0.5f)); uv.Add(new Vector2(u0, v0 + 0.5f));
                for (int i = 0; i < 4; i++) n.Add(normal);
                if (face == 0) tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
                else tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
        }
    }
}
