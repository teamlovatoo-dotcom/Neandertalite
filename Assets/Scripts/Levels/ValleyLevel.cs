using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// The big valley from the concept art: river and waterfall, pine forest, rival camp, a cave in the
    /// western cliffs (mammoth + trader + workbench) and the glowing tower in the north.
    /// Used by Survival (story/training) and Battle Royale.
    /// </summary>
    public class ValleyLevel
    {
        public const float Size = 300f;
        public const float WaterY = -1.4f;

        public Vector3 PlayerStart;
        public Vector3 CaveEntrance;
        public Vector3 TraderPos;
        public Vector3 TowerPos;
        public Vector3 CampCenter;
        public Vector3 WolfArea;
        public Rect CaveBounds;
        public readonly List<Creature> Wolves = new List<Creature>();
        public readonly List<Creature> Campers = new List<Creature>();
        public Creature Mammoth;
        public Transform Root;

        static float RiverX(float z) => 40f + 10f * Mathf.Sin(z / 30f);

        static bool InCaveNotch(float x, float z) => x < -84f && z > -31f && z < 39f;
        static float Smooth(float e0, float e1, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(e0, e1, v));

        public static float Height(float x, float z)
        {
            float h = Mat.Fbm(x * 0.012f + 10f, z * 0.012f + 4f, 4) * 4f;

            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float rough = 0.7f + 0.6f * Mat.Fbm(x * 0.03f, z * 0.03f, 3);
            float mountain = Smooth(105f, 145f, edge) * 42f * rough;
            float cliffW = Smooth(-80f, -98f, x) * 30f * rough;
            float cliffN = Smooth(100f, 114f, z) * 24f * rough;

            // Corridor up to the tower plateau.
            float corridor = Smooth(16f, 9f, Mathf.Abs(x)) * Smooth(90f, 100f, z);
            mountain *= 1f - corridor;
            cliffN *= 1f - corridor;
            float plateau = Smooth(92f, 118f, z) * 8f * corridor;

            // Flat notch for the cave structure.
            float notch = Smooth(-80f, -86f, x) * Smooth(-36f, -30f, z) * Smooth(44f, 38f, z);
            cliffW *= 1f - notch;
            mountain *= 1f - notch;

            h = h + mountain + cliffW + cliffN + plateau;
            if (notch > 0.01f) h = Mathf.Lerp(h, 0.5f, notch);

            // Rival camp clearing
            float camp = Smooth(22f, 14f, Vector2.Distance(new Vector2(x, z), new Vector2(-10f, 40f)));
            h = Mathf.Lerp(h, 1.5f, camp);

            // River carve
            if (z < 106f)
            {
                float d = Mathf.Abs(x - RiverX(z));
                float k = Smooth(10f, 5f, d);
                h = Mathf.Lerp(h, -2.4f, k);
            }
            return h;
        }

        static Color Ground(float x, float z, float h, float slope)
        {
            float path = 0f;
            // Trail from the start, past the river, to the camp and up to the tower.
            path = Mathf.Max(path, Smooth(3.5f, 1.5f, Mathf.Abs(x - Mathf.Sin(z / 25f) * 6f)) * Smooth(140f, 130f, z));
            float camp = Smooth(18f, 10f, Vector2.Distance(new Vector2(x, z), new Vector2(-10f, 40f)));
            path = Mathf.Max(path, camp * 0.8f);
            if (InCaveNotch(x, z)) path = 1f;
            float sand = z < 106f ? Smooth(12f, 8f, Mathf.Abs(x - RiverX(z))) : 0f;
            return TerrainBuilder.NaturalGround(x, z, h, slope, path, sand);
        }

        public static ValleyLevel Build(Transform root, bool spawnLife, int seed = 1)
        {
            var L = new ValleyLevel { Root = root };
            var rnd = new System.Random(seed);

            Scenery.Lighting(root, new Color(1f, 0.93f, 0.8f), 1.15f, new Vector3(38f, -35f, 0f),
                new Color(0.55f, 0.62f, 0.72f), new Color(0.42f, 0.42f, 0.4f), new Color(0.2f, 0.18f, 0.15f),
                new Color(0.62f, 0.66f, 0.7f), 0.0045f);

            var layers = new[] { TextureLibrary.Layer("grass", 6f), TextureLibrary.Layer("forest_ground", 6f), TextureLibrary.Layer("river_sand", 4f), TextureLibrary.Layer("rock_face", 10f) };
            if (TerrainBuilder.BuildTextured(root, Size, 513, Height, layers, (x, z, h, sl, w) =>
            {
                w[0] = 1f;
                w[1] = Mathf.Clamp01((Mat.Fbm(x * 0.04f + 9f, z * 0.04f, 3) - 0.45f) * 3f);
                float path = Smooth(3.5f, 1.5f, Mathf.Abs(x - Mathf.Sin(z / 25f) * 6f)) * Smooth(140f, 130f, z);
                float camp = Smooth(18f, 10f, Vector2.Distance(new Vector2(x, z), new Vector2(-10f, 40f)));
                float sand = z < 106f ? Smooth(12f, 8f, Mathf.Abs(x - RiverX(z))) : 0f;
                if (InCaveNotch(x, z)) path = 1f;
                w[2] = Mathf.Max(Mathf.Max(path * 1.5f, camp * 1.2f), sand * 2.5f);
                w[3] = Mathf.Clamp01((sl - 0.45f) * 3f) * 3f;
            }) == null)
                TerrainBuilder.Build(root, Size, 200, Height, Ground, 1024);
            Scenery.WaterPlane(root, new Vector3(30f, WaterY, -20f), new Vector2(80f, 260f));
            Boundary(root, 138f);

            L.PlayerStart = Ground3(0f, -112f);
            L.TowerPos = Ground3(0f, 128f);
            L.CampCenter = Ground3(-10f, 40f);
            L.CaveEntrance = new Vector3(-88f, Height(-88f, 0f), 0f);
            L.WolfArea = Ground3(26f, -38f);

            // Waterfall from the northern cliff into the river.
            float wx = RiverX(104f);
            float topY = Height(wx, 113f) + 0.5f;
            Scenery.Waterfall(root, new Vector3(wx, topY, 110f), topY - WaterY, 7f, Vector3.back);

            Scenery.Tower(root, L.TowerPos + Vector3.down * 0.5f, 90f);
            BuildCamp(L, root, rnd);
            BuildCave(L, root);
            ScatterNature(L, root, rnd);
            GrassField.Create(root, new Rect(-132, -132, 264, 236), 0.55f, Height, (x, z) =>
            {
                if (InCaveNotch(x, z) || x < -88f) return 0f;
                if (Mathf.Abs(x - RiverX(z)) < 9.5f) return 0f;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(-10f, 40f)) < 15f) return 0f;
                if (Mathf.Abs(x - Mathf.Sin(z / 25f) * 6f) < 2.2f && z < 130f) return 0.1f;
                float h = Height(x, z);
                if (Mathf.Abs(Height(x + 1f, z) - h) + Mathf.Abs(Height(x, z + 1f) - h) > 0.5f) return 0f;
                return Mathf.Clamp01((Mat.Fbm(x * 0.06f + 2f, z * 0.06f, 2) - 0.2f) * 2f);
            }, new[] { new Color(0.24f, 0.31f, 0.12f), new Color(0.32f, 0.36f, 0.15f), new Color(0.2f, 0.27f, 0.1f) }, 13);

            // A fallen-log bridge.
            float bz = -15f;
            float rx = RiverX(bz);
            Shapes.Stick(root, new Vector3(rx - 11f, Height(rx - 11f, bz) + 0.3f, bz), new Vector3(rx + 11f, Height(rx + 11f, bz) + 0.3f, bz), 1.1f, Mat.Get(Mat.Bark), true);

            // Starter resources near the spawn.
            for (int i = 0; i < 5; i++) Pickup.Res(root, Ground3(-6f + i * 3f, -104f + (i % 2) * 3f), Resource.Wood, 1);
            for (int i = 0; i < 3; i++) Pickup.Res(root, Ground3(5f + i * 2.5f, -98f), Resource.Stone, 1);

            // Strange crates near the tower with metal parts.
            MetalCrate(root, Ground3(14f, 116f));
            MetalCrate(root, Ground3(-15f, 120f));
            MetalCrate(root, Ground3(-18f, 47f));

            if (spawnLife) SpawnLife(L, root);
            Sfx.Loop("wind", root, Vector3.zero, 0.35f, 0f);
            return L;
        }

        public static Vector3 Ground3(float x, float z) => new Vector3(x, Height(x, z), z);

        static void Boundary(Transform root, float r)
        {
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Boundary");
                go.transform.SetParent(root, false);
                var bc = go.AddComponent<BoxCollider>();
                bool alongX = i < 2;
                float s = i % 2 == 0 ? 1f : -1f;
                go.transform.position = alongX ? new Vector3(0, 50, r * s) : new Vector3(r * s, 50, 0);
                bc.size = alongX ? new Vector3(r * 2f, 200f, 2f) : new Vector3(2f, 200f, r * 2f);
            }
        }

        static void MetalCrate(Transform root, Vector3 pos)
        {
            var c = Shapes.Box(root, pos + Vector3.up * 0.4f, new Vector3(1.2f, 0.8f, 0.8f), Mat.Get(new Color(0.2f, 0.24f, 0.26f), 0.4f, 0.6f), Quaternion.Euler(0, 25, 0));
            Shapes.Box(c.transform, new Vector3(0, 0.52f, 0), new Vector3(1.02f, 0.05f, 1.02f), Mat.Emissive(Mat.TowerGlow, 1.5f), null, false);
            Pickup.Res(root, pos + new Vector3(0.9f, 0f, 0.9f), Resource.MetalParts, 2);
        }

        static void BuildCamp(ValleyLevel L, Transform root, System.Random rnd)
        {
            Vector3 c = L.CampCenter;
            Campfire.Create(root, c);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + 0.3f;
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 11f;
                p.y = Height(p.x, p.z);
                float yaw = Mathf.Atan2(c.x - p.x, c.z - p.z) * Mathf.Rad2Deg;
                Scenery.Hut(root, p, yaw, 2.2f);
            }
            // Drying racks with hides
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = c + new Vector3(5f - i * 10f, 0, -6f);
                p.y = Height(p.x, p.z);
                var t = Shapes.Pivot(root, p, "Rack");
                Shapes.Stick(t, new Vector3(-1.2f, 0, 0), new Vector3(-1.2f, 2f, 0), 0.08f, Mat.Get(Mat.Wood), true);
                Shapes.Stick(t, new Vector3(1.2f, 0, 0), new Vector3(1.2f, 2f, 0), 0.08f, Mat.Get(Mat.Wood), true);
                Shapes.Stick(t, new Vector3(-1.3f, 1.9f, 0), new Vector3(1.3f, 1.9f, 0), 0.06f, Mat.Get(Mat.Wood));
                Shapes.Box(t, new Vector3(0, 1.3f, 0), new Vector3(1.6f, 1.1f, 0.04f), Mat.Get(Mat.Fur), null, false);
            }
            Scenery.TorchPost(root, c + new Vector3(4f, Height(c.x + 4f, c.z + 9f) - c.y, 9f));
            Scenery.TorchPost(root, c + new Vector3(-4f, Height(c.x - 4f, c.z - 12f) - c.y, -12f));
        }

        static void CaveWall(Transform root, float x0, float z0, float x1, float z1, float h, Material m)
        {
            var center = new Vector3((x0 + x1) * 0.5f, 0.5f + h * 0.5f - 1f, (z0 + z1) * 0.5f);
            Scenery.Block(root, center, new Vector3(Mathf.Abs(x1 - x0), h + 2f, Mathf.Abs(z1 - z0)), m);
        }

        static void BuildCave(ValleyLevel L, Transform root)
        {
            var m = Scenery.CaveMat;
            var cave = Shapes.Pivot(root, Vector3.zero, "Cave");
            Transform t = cave;
            // Tunnel x[-112,-88] z[-5,5]
            CaveWall(t, -112, -8, -86, -5, 8, m);
            CaveWall(t, -112, 5, -86, 8, 8, m);
            CaveWall(t, -112, -8, -86, 8, 0, m); // floor slab
            Scenery.Block(t, new Vector3(-99f, 8.5f, 0f), new Vector3(26f, 3f, 16f), m);
            // Chamber A x[-146,-112] z[-27,9], ceiling at 14 with a light hole.
            CaveWall(t, -149, -30, -146, 12, 14, m);
            CaveWall(t, -149, -30, -109, -27, 14, m);
            CaveWall(t, -112, -30, -109, -5, 14, m);
            CaveWall(t, -112, 5, -109, 12, 14, m);
            CaveWall(t, -149, 9, -134, 12, 14, m);
            CaveWall(t, -126, 9, -109, 12, 14, m);
            float cy = 15.5f;
            Scenery.Block(t, new Vector3(-138.5f, cy, -9f), new Vector3(21f, 3f, 42f), m);        // west of hole
            Scenery.Block(t, new Vector3(-117.5f, cy, -9f), new Vector3(13f, 3f, 42f), m);        // east of hole
            Scenery.Block(t, new Vector3(-126f, cy, -21f), new Vector3(4f, 3f, 18f), m);          // south of hole
            Scenery.Block(t, new Vector3(-126f, cy, 4f), new Vector3(4f, 3f, 16f), m);            // north of hole
            var shaft = new GameObject("LightShaft");
            shaft.transform.SetParent(t, false);
            shaft.transform.position = new Vector3(-126f, 13f, -9f);
            shaft.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var spot = shaft.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.55f, 0.75f, 1f);
            spot.range = 22f;
            spot.spotAngle = 45f;
            spot.intensity = 4f;
            spot.shadows = LightShadows.Soft;
            // Passage x[-134,-126] z[9,16]
            CaveWall(t, -137, 9, -134, 16, 6, m);
            CaveWall(t, -126, 9, -123, 16, 6, m);
            Scenery.Block(t, new Vector3(-130f, 7f, 12.5f), new Vector3(14f, 2f, 9f), m);
            // Chamber C x[-142,-118] z[16,36]
            CaveWall(t, -145, 16, -142, 39, 8, m);
            CaveWall(t, -118, 16, -115, 39, 8, m);
            CaveWall(t, -145, 36, -115, 39, 8, m);
            CaveWall(t, -145, 13, -134, 16, 8, m);
            CaveWall(t, -126, 13, -115, 16, 8, m);
            Scenery.Block(t, new Vector3(-130f, 9f, 26f), new Vector3(30f, 2f, 26f), m);

            // Disguise the structure as part of the mountain.
            var rr = new System.Random(5);
            for (int i = 0; i < 26; i++)
            {
                float x = -150f + (float)rr.NextDouble() * 62f;
                float z = -34f + (float)rr.NextDouble() * 76f;
                float s = 10f + (float)rr.NextDouble() * 12f;
                Scenery.Rock(root, new Vector3(x, 15f + (float)rr.NextDouble() * 6f, z), new Vector3(s, s * 0.6f, s), (float)rr.NextDouble() * 360f, false, false, i);
            }
            // Entrance boulders framing the mouth.
            Scenery.Rock(root, new Vector3(-86f, 3f, -9f), new Vector3(6f, 9f, 5f), 10f, false, false, 1);
            Scenery.Rock(root, new Vector3(-86f, 3f, 9f), new Vector3(6f, 9f, 5f), 60f, false, false, 2);
            Scenery.Rock(root, new Vector3(-87f, 9.5f, 0f), new Vector3(8f, 4f, 16f), 0f, false, false, 3);

            for (int i = 0; i < 3; i++) Scenery.TorchPost(t, new Vector3(-92f - i * 8f, 0.5f, i % 2 == 0 ? -4f : 4f));
            Scenery.TorchPost(t, new Vector3(-120f, 0.5f, 20f));
            Scenery.TorchPost(t, new Vector3(-140f, 0.5f, 34f));
            Scenery.TorchPost(t, new Vector3(-120f, 0.5f, 34f));

            L.TraderPos = new Vector3(-137f, 0.5f, 28f);
            Trader.Create(root, L.TraderPos, 90f);
            Shapes.Box(root, L.TraderPos + new Vector3(1.4f, 0.45f, 0f), new Vector3(1f, 0.9f, 2.4f), Scenery.WoodMat);
            Workbench.Create(root, new Vector3(-124f, 0.5f, 33f), 0f);
            Campfire.Create(root, new Vector3(-130f, 0.5f, 25f));
            Pickup.Res(root, new Vector3(-140f, 0.6f, 18f), Resource.MetalParts, 1);

            L.CaveBounds = new Rect(-150f, -30f, 62f, 70f);
            ZoneTrigger.Create(root, new Vector3(-100f, 1f, 0f), "cave", 7f);
            ZoneTrigger.Create(root, L.TowerPos, "tower", 14f).Once = false;
        }

        static void ScatterNature(ValleyLevel L, Transform root, System.Random rnd)
        {
            var trees = Shapes.Pivot(root, Vector3.zero, "Trees");
            System.Func<float, float, bool> okTree = (x, z) =>
            {
                if (Mathf.Abs(x) > 134f || Mathf.Abs(z) > 134f) return false;
                if (InCaveNotch(x, z) || x < -90f) return false;
                if (z < 106f && Mathf.Abs(x - RiverX(z)) < 12f) return false;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(-10f, 40f)) < 20f) return false;
                if (Mathf.Abs(x - Mathf.Sin(z / 25f) * 6f) < 5f) return false;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(0f, -112f)) < 10f) return false;
                if (Mathf.Abs(x) < 24f && z > 105f) return false;
                float h = Height(x, z);
                float s = Mathf.Abs(Height(x + 1f, z) - h) + Mathf.Abs(Height(x, z + 1f) - h);
                return s < 0.8f;
            };
            foreach (var p in Scenery.Scatter(rnd, 260, new Rect(-135, -135, 270, 270), okTree))
            {
                var pos = new Vector3(p.x, Height(p.x, p.z) - 0.2f, p.z);
                if (rnd.NextDouble() < 0.82) Scenery.Pine(trees, pos, 9f + (float)rnd.NextDouble() * 9f, rnd.Next());
                else Scenery.BroadTree(trees, pos, 7f + (float)rnd.NextDouble() * 4f, rnd.Next());
            }

            System.Func<float, float, bool> okSmall = (x, z) =>
                Mathf.Abs(x) < 132f && Mathf.Abs(z) < 132f && !InCaveNotch(x, z) && !(z < 106f && Mathf.Abs(x - RiverX(z)) < 9f);

            foreach (var p in Scenery.Scatter(rnd, 70, new Rect(-135, -135, 270, 270), okSmall))
            {
                float s = 0.8f + (float)rnd.NextDouble() * 2.2f;
                bool small = s < 1.6f;
                Scenery.Rock(root, new Vector3(p.x, Height(p.x, p.z) + s * 0.1f, p.z), new Vector3(s * 1.3f, s, s * 1.1f), (float)rnd.NextDouble() * 360f, false, small, rnd.Next(4));
            }
            // Big boulders along the cliffs.
            foreach (var p in Scenery.Scatter(rnd, 40, new Rect(-145, -145, 290, 290), (x, z) => Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) > 110f && !InCaveNotch(x, z)))
            {
                float s = 4f + (float)rnd.NextDouble() * 6f;
                Scenery.Rock(root, new Vector3(p.x, Height(p.x, p.z), p.z), new Vector3(s, s * 0.8f, s), (float)rnd.NextDouble() * 360f, false, false, rnd.Next(4));
            }
            foreach (var p in Scenery.Scatter(rnd, 45, new Rect(-130, -130, 260, 240), okTree))
                Scenery.Bush(root, new Vector3(p.x, Height(p.x, p.z), p.z), 0.8f + (float)rnd.NextDouble() * 0.6f, rnd.NextDouble() < 0.3);
            foreach (var p in Scenery.Scatter(rnd, 30, new Rect(-130, -130, 260, 240), okSmall))
                Pickup.Res(root, new Vector3(p.x, Height(p.x, p.z), p.z), Resource.Wood, 1);
            foreach (var p in Scenery.Scatter(rnd, 22, new Rect(-130, -130, 260, 240), okSmall))
                Pickup.Res(root, new Vector3(p.x, Height(p.x, p.z), p.z), Resource.Stone, 1);
        }

        static void SpawnLife(ValleyLevel L, Transform root)
        {
            for (int i = 0; i < 3; i++)
                L.Wolves.Add(CreatureFactory.Spawn(CreatureType.Wolf, root, Ground3(L.WolfArea.x + i * 2.5f, L.WolfArea.z + (i % 2) * 3f), Random.Range(0f, 360f)));
            for (int i = 0; i < 4; i++)
                CreatureFactory.Spawn(CreatureType.Deer, root, Ground3(80f + i * 3f, -70f + (i % 2) * 4f), Random.Range(0f, 360f));
            CreatureFactory.Spawn(CreatureType.Bear, root, Ground3(78f, 62f), 200f);
            for (int i = 0; i < 2; i++)
                CreatureFactory.Spawn(CreatureType.Wolf, root, Ground3(-45f + i * 3f, 72f), Random.Range(0f, 360f));
            L.Mammoth = CreatureFactory.Spawn(CreatureType.Mammoth, root, new Vector3(-132f, 0.5f, -14f), 90f);
            L.Mammoth.LeashRadius = 25f;
            L.Mammoth.SightRange = 20f;

            Vector3 c = L.CampCenter;
            for (int i = 0; i < 3; i++)
                L.Campers.Add(CreatureFactory.Spawn(CreatureType.Hunter, root, Ground3(c.x - 4f + i * 4f, c.z - 3f), 180f));
            L.Campers.Add(CreatureFactory.Spawn(CreatureType.Archer, root, Ground3(c.x - 7f, c.z + 6f), 180f));
            L.Campers.Add(CreatureFactory.Spawn(CreatureType.Archer, root, Ground3(c.x + 7f, c.z + 6f), 180f));
        }
    }
}
