using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>Reusable scenery pieces: lighting, trees, boulders, huts, the tower, waterfalls, walls.</summary>
    public static class Scenery
    {
        static Mesh[] rockMeshes;
        static Material rockMat, rockRedMat, barkMat, concreteMat, caveMat, woodMat;

        public static void Lighting(Transform parent, Color sun, float sunIntensity, Vector3 sunEuler, Color sky, Color equator, Color ground, Color fog, float fogDensity, Color? grade = null)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(sunEuler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = sun;
            l.intensity = sunIntensity;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
            RenderSettings.sun = l;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var skyMat = new Material(skyShader);
                if (skyMat.HasProperty("_SkyTint")) skyMat.SetColor("_SkyTint", Color.Lerp(sky, Color.gray, 0.4f));
                if (skyMat.HasProperty("_GroundColor")) skyMat.SetColor("_GroundColor", ground);
                if (skyMat.HasProperty("_AtmosphereThickness")) skyMat.SetFloat("_AtmosphereThickness", 1.15f);
                if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", 1.1f);
                if (skyMat.HasProperty("_SunSize")) skyMat.SetFloat("_SunSize", 0.035f);
                RenderSettings.skybox = skyMat;
            }
            DynamicGI.UpdateEnvironment();
            Neanderthal.Rendering.PostFx.CreateVolume(parent, grade ?? new Color(1f, 0.97f, 0.92f));
        }

        static void EnsureMats()
        {
            if (rockMat == null) rockMat = TextureLibrary.Has("boulder")
                ? Mat.Pbr("boulder", Mat.Rock, 0.15f, new Vector2(2, 2), new Color(0.78f, 0.76f, 0.72f))
                : Mat.Textured("rock", Mat.NoiseTexture(128, new Color(0.47f, 0.45f, 0.42f), new Color(0.27f, 0.26f, 0.24f), 6f, 3), 0.1f, new Vector2(2, 2));
            if (rockRedMat == null) rockRedMat = TextureLibrary.Has("red_rock")
                ? Mat.Pbr("red_rock", Mat.RockRed, 0.1f, new Vector2(2, 3))
                : Mat.Textured("rock_red", Mat.NoiseTexture(128, new Color(0.66f, 0.38f, 0.26f), new Color(0.42f, 0.22f, 0.15f), 5f, 9), 0.08f, new Vector2(2, 2));
            if (barkMat == null) barkMat = TextureLibrary.Has("bark")
                ? Mat.Pbr("bark", Mat.Bark, 0.05f, new Vector2(1, 3))
                : Mat.Textured("bark", Mat.NoiseTexture(64, new Color(0.3f, 0.22f, 0.15f), new Color(0.18f, 0.13f, 0.09f), 10f, 5), 0.05f, new Vector2(1, 3));
            if (concreteMat == null) concreteMat = TextureLibrary.Has("fortress_wall")
                ? Mat.Pbr("fortress_wall", Mat.Concrete, 0.08f)
                : Mat.Textured("stonewall", Mat.NoiseTexture(128, new Color(0.56f, 0.53f, 0.49f), new Color(0.4f, 0.38f, 0.35f), 8f, 11), 0.05f, new Vector2(1, 1));
            if (caveMat == null) caveMat = TextureLibrary.Has("rock_face") ? Mat.Pbr("rock_face", Mat.Rock, 0.12f) : rockMat;
            if (woodMat == null) woodMat = TextureLibrary.Has("planks") ? Mat.Pbr("planks", Mat.WoodDark, 0.1f) : Mat.Get(Mat.WoodDark);
        }

        public static Material RockMat { get { EnsureMats(); return rockMat; } }
        public static Material RockRedMat { get { EnsureMats(); return rockRedMat; } }
        public static Material WallMat { get { EnsureMats(); return concreteMat; } }
        /// <summary>Rock face for cave walls and blocks (tiles in metres on boxes).</summary>
        public static Material CaveMat { get { EnsureMats(); return caveMat; } }
        /// <summary>Weathered planks for bridges, crates and decks.</summary>
        public static Material WoodMat { get { EnsureMats(); return woodMat; } }

        /// <summary>Lumpy boulder meshes made by displacing a sphere with 3D-ish noise.</summary>
        static Mesh RockMesh(int variant)
        {
            if (rockMeshes == null)
            {
                rockMeshes = new Mesh[4];
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var src = tmp.GetComponent<MeshFilter>().sharedMesh;
                for (int v = 0; v < rockMeshes.Length; v++)
                {
                    var verts = src.vertices;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 p = verts[i];
                        float s = v * 10.7f;
                        float n = Mathf.PerlinNoise(p.x * 2.1f + s, p.y * 2.1f + s) * 0.5f
                                + Mathf.PerlinNoise(p.y * 2.3f + s, p.z * 2.3f) * 0.5f
                                + Mathf.PerlinNoise(p.z * 4.5f, p.x * 4.5f + s) * 0.25f;
                        // Flatten the bottom and facet the top a bit.
                        Vector3 q = p * (0.75f + n * 0.5f);
                        if (q.y < -0.2f) q.y = -0.2f + (q.y + 0.2f) * 0.3f;
                        verts[i] = q;
                    }
                    var m = new Mesh { name = "Rock" + v, vertices = verts, uv = src.uv, triangles = src.triangles };
                    m.RecalculateNormals();
                    m.RecalculateBounds();
                    rockMeshes[v] = m;
                }
                Object.Destroy(tmp);
            }
            return rockMeshes[variant % rockMeshes.Length];
        }

        public static GameObject Rock(Transform parent, Vector3 pos, Vector3 scale, float yaw, bool red = false, bool harvestable = false, int variant = 0)
        {
            EnsureMats();
            var go = new GameObject("Rock");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), yaw, Random.Range(-8f, 8f));
            go.transform.localScale = scale;
            var mesh = RockMesh(variant);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = red ? rockRedMat : rockMat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            if (harvestable)
            {
                var h = go.AddComponent<Harvestable>();
                h.Yield = Resource.Stone;
                h.Remaining = 4;
                h.HitsPerUnit = 1.2f;
                h.ChipColor = Mat.Stone;
            }
            return go;
        }

        public static GameObject Pine(Transform parent, Vector3 pos, float height, int seed)
        {
            EnsureMats();
            if (TreeFactory.Available)
            {
                var tree = TreeFactory.Pine(parent, pos, height, seed);
                var th = tree.AddComponent<Harvestable>();
                th.Yield = Resource.Wood;
                th.Remaining = Mathf.RoundToInt(height * 0.5f);
                th.HitsPerUnit = 1f;
                th.FallsWhenDepleted = true;
                return tree;
            }
            var rnd = new System.Random(seed);
            var go = new GameObject("Pine");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            var t = go.transform;
            float trunkR = height * 0.035f;
            var trunk = Shapes.Cyl(t, new Vector3(0, height * 0.5f, 0), new Vector3(trunkR * 2f, height * 0.5f, trunkR * 2f), barkMat, null, false);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = trunkR * 1.2f;
            col.height = height;
            col.center = new Vector3(0, height * 0.5f, 0);
            var leaves = Mat.Get(Color.Lerp(Mat.LeavesDark, Mat.Leaves, (float)rnd.NextDouble()));
            int layers = 4;
            for (int i = 0; i < layers; i++)
            {
                float k = (float)i / layers;
                float w = height * Mathf.Lerp(0.42f, 0.14f, k);
                float y = height * Mathf.Lerp(0.22f, 0.72f, k);
                Shapes.Cone(t, new Vector3(0, y, 0), new Vector3(w, height * 0.38f, w), leaves, null, 7);
            }
            var h = go.AddComponent<Harvestable>();
            h.Yield = Resource.Wood;
            h.Remaining = Mathf.RoundToInt(height * 0.5f);
            h.HitsPerUnit = 1f;
            h.FallsWhenDepleted = true;
            trunk.name = "Trunk";
            return go;
        }

        public static GameObject BroadTree(Transform parent, Vector3 pos, float height, int seed)
        {
            EnsureMats();
            var rnd = new System.Random(seed);
            var go = new GameObject("Tree");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var t = go.transform;
            float r = height * 0.05f;
            Shapes.Cyl(t, new Vector3(0, height * 0.35f, 0), new Vector3(r * 2, height * 0.35f, r * 2), barkMat, null, false);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = r * 1.2f;
            col.height = height * 0.7f;
            col.center = new Vector3(0, height * 0.35f, 0);
            var leaves = Mat.Get(Color.Lerp(Mat.Leaves, new Color(0.3f, 0.35f, 0.14f), (float)rnd.NextDouble()));
            for (int i = 0; i < 5; i++)
            {
                Vector3 o = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() * 0.5f, (float)rnd.NextDouble() - 0.5f) * height * 0.45f;
                float s = height * (0.32f + (float)rnd.NextDouble() * 0.18f);
                Shapes.Ball(t, new Vector3(0, height * 0.75f, 0) + o, new Vector3(s, s * 0.8f, s), leaves, false);
            }
            var h = go.AddComponent<Harvestable>();
            h.Yield = Resource.Wood;
            h.Remaining = Mathf.RoundToInt(height * 0.6f);
            h.FallsWhenDepleted = true;
            return go;
        }

        public static GameObject Bush(Transform parent, Vector3 pos, float size, bool berries)
        {
            var go = new GameObject("Bush");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var leaves = Mat.Get(Mat.Leaves);
            for (int i = 0; i < 3; i++)
                Shapes.Ball(go.transform, new Vector3((i - 1) * size * 0.35f, size * 0.3f, (i % 2) * size * 0.2f), Vector3.one * size * (0.6f + i * 0.1f), leaves, false);
            if (berries) Pickup.Res(go.transform, pos + Vector3.up * 0.1f, Resource.Herbs, 1);
            return go;
        }

        public static GameObject Hut(Transform parent, Vector3 pos, float yaw, float radius)
        {
            EnsureMats();
            var go = new GameObject("Hut");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var t = go.transform;
            var hide = Mat.Get(Mat.Fur);
            var wood = Mat.Get(Mat.Wood);
            // Conical frame of poles with hide covering, opening facing +Z.
            int poles = 10;
            for (int i = 0; i < poles; i++)
            {
                float a = i * Mathf.PI * 2f / poles;
                Vector3 b = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                Shapes.Stick(t, b, new Vector3(0, radius * 2.1f, 0) + b * -0.15f, 0.08f, wood);
            }
            var cover = Shapes.Cone(t, Vector3.zero, new Vector3(radius * 2f, radius * 1.9f, radius * 2f), hide, null, 10);
            cover.AddComponent<MeshCollider>().sharedMesh = cover.GetComponent<MeshFilter>().sharedMesh;
            // Door: dark flap on the front.
            Shapes.Box(t, new Vector3(0, 0.7f, radius * 0.78f), new Vector3(0.9f, 1.4f, 0.1f), Mat.Get(new Color(0.08f, 0.06f, 0.05f)), Quaternion.Euler(-20, 0, 0), false);
            return go;
        }

        public static GameObject Tower(Transform parent, Vector3 pos, float height)
        {
            var go = new GameObject("Tower");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var t = go.transform;
            var dark = Mat.Get(new Color(0.12f, 0.13f, 0.16f), 0.6f, 0.6f);
            var glow = Mat.Emissive(Mat.TowerGlow, 3f);
            int segs = 7;
            float y = 0f;
            for (int i = 0; i < segs; i++)
            {
                float k = (float)i / segs;
                float w = Mathf.Lerp(16f, 2.5f, Mathf.Pow(k, 0.8f));
                float h = height / segs;
                var seg = Shapes.Cyl(t, new Vector3(0, y + h * 0.5f, 0), new Vector3(w, h * 0.5f, w), dark, null, i < 2);
                Shapes.Cyl(t, new Vector3(0, y + h, 0), new Vector3(w * 1.04f, 0.15f, w * 1.04f), glow, null, false);
                // Buttress fins
                for (int f = 0; f < 4; f++)
                {
                    var fin = Shapes.Box(t, new Vector3(0, y + h * 0.5f, 0), new Vector3(0.6f, h, w * 1.25f), dark, Quaternion.Euler(0, f * 45f, 0), false);
                    fin.transform.localPosition = new Vector3(0, y + h * 0.5f, 0);
                }
                y += h;
                seg.name = "TowerSeg" + i;
            }
            // Vertical glowing seams and a beam into the sky.
            for (int f = 0; f < 4; f++)
                Shapes.Box(t, new Vector3(0, height * 0.3f, 0), new Vector3(0.25f, height * 0.6f, 16.5f), glow, Quaternion.Euler(0, f * 45f + 22.5f, 0), false);
            var beam = Shapes.Cyl(t, new Vector3(0, height + 150f, 0), new Vector3(1.2f, 150f, 1.2f), Mat.Emissive(new Color(0.6f, 0.85f, 1f), 4f), null, false);
            Shapes.SetShadows(beam, false);
            Shapes.PointLight(t, new Vector3(0, 6f, 12f), Mat.TowerGlow, 30f, 3f);
            // A doorway at the base (sealed, for Chapter 2).
            Shapes.Box(t, new Vector3(0, 3f, 7.6f), new Vector3(3.2f, 6f, 1.2f), Mat.Emissive(new Color(0.2f, 0.45f, 0.8f), 1.5f));
            return go;
        }

        public static void Waterfall(Transform parent, Vector3 top, float height, float width, Vector3 facing)
        {
            var go = new GameObject("Waterfall");
            go.transform.SetParent(parent, false);
            go.transform.position = top;
            go.transform.rotation = Quaternion.LookRotation(facing);
            var t = go.transform;
            var water = Mat.Emissive(new Color(0.65f, 0.78f, 0.82f), 0.35f);
            Shapes.Box(t, new Vector3(0, -height * 0.5f, 0.4f), new Vector3(width, height, 0.4f), water, null, false);
            Shapes.Box(t, new Vector3(0, -height * 0.5f, 0.7f), new Vector3(width * 0.6f, height, 0.3f), Mat.Get(new Color(0.85f, 0.9f, 0.92f), 0.8f), null, false);
            var foam = Mat.Get(new Color(0.9f, 0.93f, 0.95f), 0.4f);
            for (int i = 0; i < 6; i++)
                Shapes.Ball(t, new Vector3((i - 2.5f) * width * 0.2f, -height, 1.5f), new Vector3(width * 0.35f, 1.2f, 2.2f), foam, false);
            Sfx.Loop("water", t, new Vector3(0, -height, 2f), 0.8f, 45f);
        }

        public static GameObject WaterPlane(Transform parent, Vector3 center, Vector2 size)
        {
            var mat = Mat.WaterSurface(new Color(0.12f, 0.27f, 0.3f, 0.72f));
            var go = Shapes.Prim(PrimitiveType.Plane, parent, center, new Vector3(size.x / 10f, 1f, size.y / 10f), mat, null, false, "Water");
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Underside, so the surface is visible while swimming below it.
            var under = Shapes.Prim(PrimitiveType.Plane, go.transform, Vector3.zero, Vector3.one, mat, Quaternion.Euler(180f, 0f, 0f), false, "WaterUnderside");
            under.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Thick rock-textured box used for cave walls and fortress blocks.</summary>
        public static GameObject Block(Transform parent, Vector3 center, Vector3 size, Material mat, float yaw = 0f)
        {
            var go = Shapes.Box(parent, center, size, mat, Quaternion.Euler(0, yaw, 0));
            go.name = "Block";
            return go;
        }

        /// <summary>A ramp from <paramref name="bottom"/> rising towards <paramref name="dir"/> by <paramref name="rise"/> over <paramref name="run"/>.</summary>
        public static GameObject Ramp(Transform parent, Vector3 bottom, Vector3 dir, float run, float rise, float width, Material mat)
        {
            dir.y = 0f;
            dir.Normalize();
            float len = Mathf.Sqrt(run * run + rise * rise);
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            Vector3 center = bottom + dir * run * 0.5f + Vector3.up * rise * 0.5f;
            var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(-angle, 0, 0);
            var go = Shapes.Box(parent, center - rot * Vector3.up * 0.15f, new Vector3(width, 0.3f, len), mat, rot);
            go.name = "Ramp";
            return go;
        }

        public static void TorchPost(Transform parent, Vector3 pos)
        {
            var t = Shapes.Pivot(parent, pos, "TorchPost");
            Shapes.Stick(t, Vector3.zero, new Vector3(0, 1.6f, 0), 0.07f, Mat.Get(Mat.WoodDark), true);
            Shapes.PointLight(t, new Vector3(0, 1.9f, 0), Mat.Fire, 10f, 1.8f, false);
            var flame = Shapes.Ball(t, new Vector3(0, 1.75f, 0), new Vector3(0.18f, 0.3f, 0.18f), Mat.Emissive(Mat.Fire, 3f), false);
            Shapes.SetShadows(flame, false);
            flame.AddComponent<Flicker>();
        }

        /// <summary>Random point helper that rejects positions failing a predicate.</summary>
        public static List<Vector3> Scatter(System.Random rnd, int count, Rect area, System.Func<float, float, bool> accept, int maxTries = 4000)
        {
            var list = new List<Vector3>();
            int tries = 0;
            while (list.Count < count && tries++ < maxTries)
            {
                float x = area.x + (float)rnd.NextDouble() * area.width;
                float z = area.y + (float)rnd.NextDouble() * area.height;
                if (accept(x, z)) list.Add(new Vector3(x, 0, z));
            }
            return list;
        }
    }
}
