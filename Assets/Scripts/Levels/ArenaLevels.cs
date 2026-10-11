using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    public struct SpawnPoint
    {
        public Vector3 Position;
        public float Yaw;
        public SpawnPoint(Vector3 p, float yaw) { Position = p; Yaw = yaw; }
    }

    /// <summary>Spawn layout returned by arena builders.</summary>
    public class ArenaLayout
    {
        public string Name;
        public readonly List<SpawnPoint> Spawns = new List<SpawnPoint>();
        public Vector3 Center;
    }

    /// <summary>The two PvP maps from the concept boards: Cave Forest and Canyon Fortress.</summary>
    public static class ArenaLevels
    {
        public static ArenaLayout Build(ArenaMap map, Transform root)
        {
            return map == ArenaMap.CanyonFortress ? CanyonFortress(root) : CaveForest(root);
        }

        // ------------------------------------------------------------------ CAVE FOREST
        static float StreamZ(float x) => -12f + 5f * Mathf.Sin(x / 12f);
        static float Smooth(float e0, float e1, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(e0, e1, v));

        static float ForestHeight(float x, float z)
        {
            float h = Mat.Fbm(x * 0.03f + 2f, z * 0.03f + 5f, 3) * 2.5f;
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            h += Smooth(44f, 58f, edge) * 18f * (0.7f + 0.6f * Mat.Fbm(x * 0.05f, z * 0.05f, 2));
            // Central hill the cave tunnel runs through (tunnel corridor flattened).
            float hill = Smooth(22f, 8f, Vector2.Distance(new Vector2(x, z), new Vector2(0f, 14f))) * 8f;
            float tunnel = Smooth(5.5f, 4f, Mathf.Abs(z - 14f)) * Smooth(26f, 20f, Mathf.Abs(x));
            hill *= 1f - tunnel;
            h += hill;
            // Stream and pond
            float d = Mathf.Abs(z - StreamZ(x));
            h = Mathf.Lerp(h, -1.8f, Smooth(5f, 2.2f, d));
            float pond = Smooth(9f, 5f, Vector2.Distance(new Vector2(x, z), new Vector2(26f, -14f)));
            h = Mathf.Lerp(h, -2f, pond);
            return h;
        }

        static ArenaLayout CaveForest(Transform root)
        {
            var L = new ArenaLayout { Name = "Печерний ліс", Center = new Vector3(0, 0, 0) };
            Scenery.Lighting(root, new Color(1f, 0.9f, 0.75f), 1.1f, new Vector3(32f, -60f, 0f),
                new Color(0.5f, 0.58f, 0.66f), new Color(0.38f, 0.4f, 0.35f), new Color(0.18f, 0.16f, 0.12f),
                new Color(0.55f, 0.6f, 0.58f), 0.012f);
            TerrainBuilder.Build(root, 130f, 130, ForestHeight, (x, z, h, s) =>
            {
                float path = Smooth(3f, 1f, Mathf.Abs(new Vector2(x, z).magnitude - 30f));
                float sand = Smooth(6f, 3.5f, Mathf.Abs(z - StreamZ(x)));
                return TerrainBuilder.NaturalGround(x, z, h, s, path, sand);
            }, 512);
            Scenery.WaterPlane(root, new Vector3(0f, -1.1f, -12f), new Vector2(120f, 40f));
            Boundary(root, 49f);

            // Cave tunnel through the hill: walls + roof, rock on top.
            var rock = Scenery.RockMat;
            float tz = 14f;
            for (int s = -1; s <= 1; s += 2)
                Scenery.Block(root, new Vector3(0f, 2f, tz + s * 5f), new Vector3(30f, 7f, 2f), rock);
            Scenery.Block(root, new Vector3(0f, 6.2f, tz), new Vector3(30f, 1.6f, 12f), rock);
            Scenery.Rock(root, new Vector3(0f, 8f, tz), new Vector3(22f, 6f, 16f), 0f, false, false, 1);
            Scenery.Rock(root, new Vector3(-15f, 3f, tz - 6f), new Vector3(5f, 7f, 4f), 20f, false, false, 2);
            Scenery.Rock(root, new Vector3(15f, 3f, tz + 6f), new Vector3(5f, 7f, 4f), 200f, false, false, 3);
            Shapes.PointLight(root, new Vector3(0f, 4f, tz), new Color(0.5f, 0.7f, 1f), 12f, 1.2f);
            Scenery.TorchPost(root, new Vector3(-8f, 0.2f, tz + 3.5f));
            Scenery.TorchPost(root, new Vector3(8f, 0.2f, tz - 3.5f));

            // Bridges over the stream
            foreach (float bx in new[] { -22f, 18f })
            {
                float sz = StreamZ(bx);
                var deck = Shapes.Box(root, new Vector3(bx, 0.35f, sz), new Vector3(3.2f, 0.3f, 11f), Mat.Get(Mat.WoodDark));
                deck.name = "Bridge";
                for (int i = -1; i <= 1; i += 2)
                    Shapes.Box(root, new Vector3(bx + i * 1.5f, 1f, sz), new Vector3(0.12f, 0.12f, 11f), Mat.Get(Mat.Wood));
            }

            // Ramps onto a rock shelf in the west.
            Scenery.Block(root, new Vector3(-32f, 1.5f, 22f), new Vector3(10f, 5f, 8f), rock);
            Scenery.Ramp(root, new Vector3(-32f, ForestHeight(-32f, 10f), 10f), Vector3.forward, 8f, 4f - ForestHeight(-32f, 10f), 3f, Mat.Get(Mat.WoodDark));
            Scenery.Waterfall(root, new Vector3(-34f, 4.2f, 18f), 5.5f, 3f, Vector3.right);

            // Trees and cover
            var rnd = new System.Random(42);
            var trees = Shapes.Pivot(root, Vector3.zero, "Trees");
            foreach (var p in Scenery.Scatter(rnd, 90, new Rect(-46, -46, 92, 92), (x, z) =>
                         Mathf.Abs(z - StreamZ(x)) > 6f && Vector2.Distance(new Vector2(x, z), new Vector2(0, 14)) > 16f &&
                         Mathf.Abs(new Vector2(x, z).magnitude - 30f) > 3f && Vector2.Distance(new Vector2(x, z), new Vector2(26, -14)) > 10f))
            {
                var pos = new Vector3(p.x, ForestHeight(p.x, p.z) - 0.2f, p.z);
                if (rnd.NextDouble() < 0.85) Scenery.Pine(trees, pos, 10f + (float)rnd.NextDouble() * 8f, rnd.Next());
                else Scenery.BroadTree(trees, pos, 7f + (float)rnd.NextDouble() * 3f, rnd.Next());
            }
            foreach (var p in Scenery.Scatter(rnd, 30, new Rect(-44, -44, 88, 88), (x, z) => Mathf.Abs(z - StreamZ(x)) > 4f))
            {
                float s = 1f + (float)rnd.NextDouble() * 2f;
                Scenery.Rock(root, new Vector3(p.x, ForestHeight(p.x, p.z), p.z), new Vector3(s * 1.4f, s, s), (float)rnd.NextDouble() * 360f, false, false, rnd.Next(4));
            }
            // Fallen logs as cover
            Shapes.Stick(root, new Vector3(-12f, ForestHeight(-12f, -30f) + 0.4f, -30f), new Vector3(-4f, ForestHeight(-4f, -33f) + 0.4f, -33f), 0.9f, Mat.Get(Mat.Bark), true);
            Shapes.Stick(root, new Vector3(10f, ForestHeight(10f, 34f) + 0.4f, 34f), new Vector3(18f, ForestHeight(18f, 31f) + 0.4f, 31f), 0.9f, Mat.Get(Mat.Bark), true);

            // Spawns: blue in the south-west, red in the north-east (+ alternates).
            L.Spawns.Add(Spawn(ForestHeight, -36f, -36f, 45f));
            L.Spawns.Add(Spawn(ForestHeight, 36f, 38f, 225f));
            L.Spawns.Add(Spawn(ForestHeight, 38f, -36f, 315f));
            L.Spawns.Add(Spawn(ForestHeight, -38f, 36f, 135f));
            L.Spawns.Add(Spawn(ForestHeight, 0f, -40f, 0f));
            L.Spawns.Add(Spawn(ForestHeight, 0f, 40f, 180f));

            // Weapon spawns (respawn every 20s)
            WeaponSpawn(root, ForestHeight, 0f, 14f, "bow");
            WeaponSpawn(root, ForestHeight, -22f, -28f, "spear");
            WeaponSpawn(root, ForestHeight, 24f, 26f, "spear");
            WeaponSpawn(root, ForestHeight, -32f, 22f, "axe", 4f);
            WeaponSpawn(root, ForestHeight, 30f, 2f, "club");
            WeaponSpawn(root, ForestHeight, -30f, 0f, "knife");
            AmmoSpawn(root, ForestHeight, -10f, 14f, AmmoType.Arrow, 8);
            AmmoSpawn(root, ForestHeight, 10f, 14f, AmmoType.Arrow, 8);
            AmmoSpawn(root, ForestHeight, -18f, -38f, AmmoType.Arrow, 6);
            AmmoSpawn(root, ForestHeight, 20f, 40f, AmmoType.Arrow, 6);
            HealthSpawn(root, ForestHeight, 26f, -24f);
            HealthSpawn(root, ForestHeight, -26f, 34f);
            ArmorSpawn(root, ForestHeight, 0f, -26f);
            return L;
        }

        // ------------------------------------------------------------------ CANYON FORTRESS
        static float CanyonHeight(float x, float z)
        {
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float h = Mat.Fbm(x * 0.04f, z * 0.04f, 2) * 0.6f * Smooth(24f, 30f, edge);
            h += Smooth(48f, 60f, edge) * 30f * (0.6f + 0.8f * Mat.Fbm(x * 0.06f + 9f, z * 0.06f, 3));
            return h;
        }

        static ArenaLayout CanyonFortress(Transform root)
        {
            var L = new ArenaLayout { Name = "Фортеця в каньйоні", Center = Vector3.zero };
            Scenery.Lighting(root, new Color(1f, 0.88f, 0.72f), 1.35f, new Vector3(42f, 140f, 0f),
                new Color(0.55f, 0.65f, 0.8f), new Color(0.6f, 0.45f, 0.35f), new Color(0.35f, 0.22f, 0.15f),
                new Color(0.8f, 0.68f, 0.58f), 0.006f);
            TerrainBuilder.Build(root, 140f, 120, CanyonHeight, (x, z, h, s) =>
            {
                float n = Mat.Fbm(x * 0.08f, z * 0.08f, 3);
                Color sand = Color.Lerp(new Color(0.62f, 0.47f, 0.33f), new Color(0.52f, 0.38f, 0.26f), n);
                Color rock = Color.Lerp(new Color(0.6f, 0.32f, 0.2f), new Color(0.42f, 0.22f, 0.14f), n);
                return Color.Lerp(sand, rock, Mathf.Clamp01((s - 0.3f) * 2f + h / 25f));
            }, 512);
            Boundary(root, 50f);

            // Red rock spires outside the fortress
            var rnd = new System.Random(7);
            foreach (var p in Scenery.Scatter(rnd, 26, new Rect(-58, -58, 116, 116), (x, z) => Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) > 44f))
            {
                float s = 4f + (float)rnd.NextDouble() * 5f;
                Scenery.Rock(root, new Vector3(p.x, CanyonHeight(p.x, p.z) - 2f, p.z), new Vector3(s, s * (2.5f + (float)rnd.NextDouble() * 3f), s), (float)rnd.NextDouble() * 360f, true, false, rnd.Next(4));
            }
            foreach (var p in Scenery.Scatter(rnd, 14, new Rect(-42, -42, 84, 84), (x, z) => Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) > 25f))
            {
                float s = 1.2f + (float)rnd.NextDouble() * 1.5f;
                Scenery.Rock(root, new Vector3(p.x, 0f, p.z), new Vector3(s * 1.3f, s, s), (float)rnd.NextDouble() * 360f, true, false, rnd.Next(4));
            }

            var wall = Scenery.WallMat;
            var rail = Mat.Get(new Color(0.6f, 0.12f, 0.1f), 0.3f);
            float half = 20f, wallH = 6f, thick = 3f;
            // Outer walls with a gate (tunnel) in the middle of each side.
            for (int side = 0; side < 4; side++)
            {
                var rot = Quaternion.Euler(0f, side * 90f, 0f);
                for (int seg = -1; seg <= 1; seg += 2)
                {
                    Vector3 c = rot * new Vector3(seg * 11.5f, wallH * 0.5f, half);
                    Shapes.Box(root, c, new Vector3(17f, wallH, thick), wall, rot);
                    // Red railing on the walkway
                    Vector3 railPos = rot * new Vector3(seg * 11.5f, wallH + 0.55f, half - thick * 0.5f + 0.1f);
                    Shapes.Box(root, railPos, new Vector3(17f, 0.08f, 0.08f), rail, rot, false);
                    for (int post = -2; post <= 2; post++)
                        Shapes.Box(root, rot * new Vector3(seg * 11.5f + post * 4f, wallH + 0.3f, half - thick * 0.5f + 0.1f), new Vector3(0.08f, 0.6f, 0.08f), rail, rot, false);
                }
                // Gate lintel = short tunnel
                Vector3 lintel = rot * new Vector3(0f, wallH - 1f, half);
                Shapes.Box(root, lintel, new Vector3(6f, 2f, thick + 2f), wall, rot);
                // Corner towers
                Vector3 corner = rot * new Vector3(half, wallH * 0.5f + 1f, half);
                Shapes.Box(root, corner, new Vector3(5f, wallH + 2f, 5f), wall);
            }

            // Inner ramps from the courtyard up to the walkways (two corners).
            Scenery.Ramp(root, new Vector3(-10f, 0f, 9.5f), Vector3.left, 8.6f, wallH, 3f, wall);
            Scenery.Ramp(root, new Vector3(10f, 0f, -9.5f), Vector3.right, 8.6f, wallH, 3f, wall);
            // Outer ramps
            Scenery.Ramp(root, new Vector3(12f, 0f, 30f), Vector3.back, 8.5f, wallH, 3f, wall);
            Scenery.Ramp(root, new Vector3(-12f, 0f, -30f), Vector3.forward, 8.5f, wallH, 3f, wall);

            // Central duel platform with ramps and a tunnel underneath.
            Shapes.Box(root, new Vector3(-3.5f, 1.75f, 0f), new Vector3(3f, 3.5f, 8f), wall);
            Shapes.Box(root, new Vector3(3.5f, 1.75f, 0f), new Vector3(3f, 3.5f, 8f), wall);
            Shapes.Box(root, new Vector3(0f, 3.75f, 0f), new Vector3(10f, 0.5f, 8f), wall);
            Scenery.Ramp(root, new Vector3(-13f, 0f, 0f), Vector3.right, 8f, 4f, 3f, wall);
            Scenery.Ramp(root, new Vector3(13f, 0f, 0f), Vector3.left, 8f, 4f, 3f, wall);
            Shapes.PointLight(root, new Vector3(0f, 1.8f, 0f), new Color(1f, 0.75f, 0.5f), 8f, 1.2f);

            // Low cover walls in the courtyard
            Shapes.Box(root, new Vector3(-12f, 0.6f, -4f), new Vector3(4f, 1.2f, 0.8f), wall);
            Shapes.Box(root, new Vector3(12f, 0.6f, 4f), new Vector3(4f, 1.2f, 0.8f), wall);
            Shapes.Box(root, new Vector3(-6f, 0.6f, 13f), new Vector3(0.8f, 1.2f, 4f), wall);
            Shapes.Box(root, new Vector3(6f, 0.6f, -13f), new Vector3(0.8f, 1.2f, 4f), wall);
            // Crates outside
            foreach (var cp in new[] { new Vector3(-28f, 0, 6f), new Vector3(28f, 0, -6f), new Vector3(8f, 0, 30f), new Vector3(-8f, 0, -30f) })
            {
                Shapes.Box(root, cp + Vector3.up * 0.6f, new Vector3(1.2f, 1.2f, 1.2f), Mat.Get(Mat.WoodDark), Quaternion.Euler(0, 15, 0));
                Shapes.Box(root, cp + new Vector3(1.3f, 0.5f, 0.3f), new Vector3(1f, 1f, 1f), Mat.Get(Mat.Wood), Quaternion.Euler(0, -10, 0));
            }

            L.Spawns.Add(Spawn(CanyonHeight, -36f, -36f, 45f));
            L.Spawns.Add(Spawn(CanyonHeight, 36f, 36f, 225f));
            L.Spawns.Add(Spawn(CanyonHeight, 36f, -36f, 315f));
            L.Spawns.Add(Spawn(CanyonHeight, -36f, 36f, 135f));
            L.Spawns.Add(Spawn(CanyonHeight, -14f, -14f, 45f));
            L.Spawns.Add(Spawn(CanyonHeight, 14f, 14f, 225f));

            WeaponSpawn(root, CanyonHeight, 0f, 0f, "crossbow", 4.1f);
            WeaponSpawn(root, CanyonHeight, -14f, 14f, "bow");
            WeaponSpawn(root, CanyonHeight, 14f, -14f, "spear");
            WeaponSpawn(root, CanyonHeight, 0f, 0f, "axe");
            WeaponSpawn(root, CanyonHeight, -30f, 30f, "club");
            WeaponSpawn(root, CanyonHeight, 30f, -30f, "knife");
            AmmoSpawn(root, CanyonHeight, 0f, -20f, AmmoType.Arrow, 8, wallH);
            AmmoSpawn(root, CanyonHeight, 0f, 20f, AmmoType.Arrow, 8, wallH);
            AmmoSpawn(root, CanyonHeight, 20f, 0f, AmmoType.Bolt, 4, wallH);
            AmmoSpawn(root, CanyonHeight, -20f, 0f, AmmoType.Bolt, 4, wallH);
            HealthSpawn(root, CanyonHeight, -26f, -6f);
            HealthSpawn(root, CanyonHeight, 26f, 6f);
            ArmorSpawn(root, CanyonHeight, 0f, -16f);
            return L;
        }

        // ------------------------------------------------------------------ helpers
        static SpawnPoint Spawn(System.Func<float, float, float> h, float x, float z, float yaw)
            => new SpawnPoint(new Vector3(x, h(x, z) + 0.2f, z), yaw);

        static Vector3 At(System.Func<float, float, float> h, float x, float z, float y)
            => new Vector3(x, (h != null ? h(x, z) : 0f) + y + 0.1f, z);

        static void WeaponSpawn(Transform root, System.Func<float, float, float> h, float x, float z, string id, float y = 0f)
        {
            Pad(root, At(h, x, z, y));
            Pickup.Weapon(root, At(h, x, z, y), id, 20f);
        }

        static void AmmoSpawn(Transform root, System.Func<float, float, float> h, float x, float z, AmmoType t, int n, float y = 0f)
            => Pickup.AmmoBox(root, At(h, x, z, y), t, n, 20f);

        static void HealthSpawn(Transform root, System.Func<float, float, float> h, float x, float z)
            => Pickup.Create(root, At(h, x, z, 0f), PickupKind.Health, amount: 40, respawn: 25f);

        static void ArmorSpawn(Transform root, System.Func<float, float, float> h, float x, float z)
            => Pickup.Create(root, At(h, x, z, 0f), PickupKind.Armor, amount: 50, respawn: 40f);

        static void Pad(Transform root, Vector3 pos)
        {
            var pad = Shapes.Cyl(root, pos + Vector3.up * 0.02f, new Vector3(1.4f, 0.03f, 1.4f), Mat.Get(new Color(0.15f, 0.15f, 0.16f)), null, false);
            Shapes.Cyl(root, pos + Vector3.up * 0.05f, new Vector3(1.0f, 0.02f, 1.0f), Mat.Emissive(new Color(0.9f, 0.6f, 0.25f), 1.2f), null, false);
        }

        static void Boundary(Transform root, float r)
        {
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Boundary");
                go.transform.SetParent(root, false);
                var bc = go.AddComponent<BoxCollider>();
                bool alongX = i < 2;
                float s = i % 2 == 0 ? 1f : -1f;
                go.transform.position = alongX ? new Vector3(0, 40, r * s) : new Vector3(r * s, 40, 0);
                bc.size = alongX ? new Vector3(r * 2f, 120f, 2f) : new Vector3(2f, 120f, r * 2f);
            }
        }
    }
}
