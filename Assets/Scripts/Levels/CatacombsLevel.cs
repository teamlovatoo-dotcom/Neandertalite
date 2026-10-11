using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Builds the "Підземелля" map from <see cref="CatacombsLayout"/> (which is also verified and rendered outside
    /// Unity by Tools/levelcheck): terrain with cut-outs for the sunken stairways, the catacomb boxes, water,
    /// the waterfall with its hiding ledge, torches, decoration, the surface forest, spawns and pickups.
    /// While the camera is underground the daylight fades out so only torches light the tunnels.
    /// </summary>
    public class CatacombsLevel : MonoBehaviour
    {
        static CatacombsLayout L;
        /// <summary>Height of the terrain actually built (what props must stand on).</summary>
        static System.Func<float, float, float> Ground;
        static Material wallMat, floorMat, ceilMat, stoneMat, cliffMat;

        public static float Height(float x, float z) => L != null ? L.Height(x, z) : 0f;

        static void Mats()
        {
            wallMat = TextureLibrary.Has("stone_wall") ? Mat.Pbr("stone_wall", Mat.Stone, 0.12f, null, new Color(0.82f, 0.78f, 0.72f)) : Scenery.CaveMat;
            floorMat = TextureLibrary.Has("camp_dirt") ? Mat.Pbr("camp_dirt", Mat.Sand, 0.08f, null, new Color(0.55f, 0.5f, 0.45f)) : Scenery.CaveMat;
            ceilMat = Scenery.CaveMat;
            stoneMat = Scenery.RockMat;
            cliffMat = Scenery.CaveMat;
        }

        static Material For(CatacombsLayout.Part p)
        {
            switch (p)
            {
                case CatacombsLayout.Part.Floor: return floorMat;
                case CatacombsLayout.Part.Ceiling: return ceilMat;
                case CatacombsLayout.Part.Menhir:
                case CatacombsLayout.Part.Altar:
                case CatacombsLayout.Part.Lintel: return stoneMat;
                case CatacombsLayout.Part.Cliff:
                case CatacombsLayout.Part.Lip: return cliffMat;
                default: return wallMat;
            }
        }

        public static ArenaLayout Build(Transform root)
        {
            L = CatacombsLayout.Generate();
            var A = new ArenaLayout { Name = "Підземелля", Center = Vector3.zero };

            Scenery.Lighting(root, new Color(1f, 0.9f, 0.76f), 1.15f, new Vector3(40f, -40f, 0f),
                new Color(0.5f, 0.58f, 0.66f), new Color(0.4f, 0.41f, 0.36f), new Color(0.18f, 0.16f, 0.12f),
                new Color(0.58f, 0.63f, 0.66f), 0.004f, new Color(0.97f, 1f, 0.95f), "sky_forest");

            var terrain = BuildTerrain(root);
            if (terrain != null)
            {
                var tp = terrain.transform.position;
                Ground = (x, z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + tp.y;
                SelfCheck();
            }
            else Ground = L.Height;
            ArenaLevels.Boundary(root, 132f);

            Mats();
            var under = Shapes.Pivot(root, Vector3.zero, "Catacombs");
            foreach (var b in L.Boxes)
            {
                var rot = Quaternion.Euler(0f, b.Yaw, 0f) * Quaternion.Euler(b.Pitch, 0f, 0f);
                Shapes.Box(under, b.Center, b.Size, For(b.Part), rot).name = b.Part.ToString();
            }
            int torchIndex = 0;
            foreach (var t in L.Torches) WallTorch(under, t.Pos, t.Yaw, torchIndex++ % 2 == 0);
            foreach (var t in L.TorchPosts) Scenery.TorchPost(root, new Vector3(t.Pos.x, Ground(t.Pos.x, t.Pos.z), t.Pos.z));
            foreach (var d in L.Decor) Decor(under, d);

            BuildWaterfall(root);
            foreach (var w in L.Waters) Scenery.WaterPlane(root, w.Center, w.Size);
            foreach (var z in L.WaterZones) WaterZones.Register(z.area, z.y, z.y - 3.6f);   // dry tunnels below
            BuildSurface(root);

            foreach (var s in L.Spawns) A.Spawns.Add(new SpawnPoint(Snap(s.Pos, 0.2f), s.Yaw));
            foreach (var p in L.Pickups) { var q = p; q.Pos = Snap(p.Pos, 0.12f); Pickup(root, q); }

            root.gameObject.AddComponent<CatacombsLevel>();
            return A;
        }

        /// <summary>Surface positions are put back exactly on the built terrain; underground ones are left alone.</summary>
        static Vector3 Snap(Vector3 p, float lift)
        {
            if (p.y < -6f || L.IsHole(p.x, p.z) || L.Underground(p)) return p;
            float g = Ground(p.x, p.z);
            return Mathf.Abs(p.y - lift - L.Height(p.x, p.z)) < 0.3f ? new Vector3(p.x, g + lift, p.z) : p;
        }

        /// <summary>Logs if the built terrain differs from the layout (props would float or sink).</summary>
        static void SelfCheck()
        {
            float worst = 0f;
            Vector2 at = Vector2.zero;
            for (float x = -140f; x <= 140f; x += 7f)
            for (float z = -140f; z <= 140f; z += 7f)
            {
                if (L.NearHole(x, z, 1f)) continue;
                float d = Mathf.Abs(Ground(x, z) - L.Height(x, z));
                if (d > worst) { worst = d; at = new Vector2(x, z); }
            }
            if (worst > 0.35f) Debug.LogWarning($"Підземелля: terrain differs from layout by {worst:0.00} m at {at}");
            else Debug.Log($"Підземелля: terrain matches layout (max {worst:0.00} m)");
        }

        static Terrain BuildTerrain(Transform root)
        {
            var layers = new[] { TextureLibrary.Layer("grass", 5f), TextureLibrary.Layer("forest_ground", 6f), TextureLibrary.Layer("river_sand", 4f),
                                 TextureLibrary.Layer("rock_face", 9f), TextureLibrary.Layer("leaf_litter", 6f) };
            var terrain = TerrainBuilder.BuildTextured(root, CatacombsLayout.Half * 2f, 1025, L.Height, layers, (x, z, h, sl, w) =>
            {
                w[0] = 1f;
                w[1] = Mathf.Clamp01((Mat.Fbm(x * 0.04f + 3f, z * 0.04f, 3) - 0.45f) * 3f);
                // Sand only right at the water and around the stairway mouths.
                float sand = 0f;
                if (Vector2.Distance(new Vector2(x, z), CatacombsLayout.Pool) < 14f) sand = 1f;
                if (z < 46f && Mathf.Abs(x - CatacombsLayout.StreamX(z)) < 6f && h < 0.4f) sand = 1f;
                if (z > 60f && Mathf.Abs(x - CatacombsLayout.RiverX(z)) < 7f && h < 13.4f) sand = 1f;
                foreach (var p in L.Portals)
                    sand = Mathf.Max(sand, Mathf.Clamp01((10f - Vector2.Distance(new Vector2(x, z), new Vector2(p.Mouth.x, p.Mouth.z))) / 5f) * 0.8f);
                w[2] = sand * 2.5f;
                w[3] = Mathf.Clamp01((sl - 0.42f) * 4f) * 3f;
                w[4] = Mathf.Clamp01((Mat.Fbm(x * 0.07f + 11f, z * 0.07f, 2) - 0.5f) * 3f) * (1f - sand);
            }, 512, L.IsHole);
            if (terrain == null) TerrainBuilder.Build(root, CatacombsLayout.Half * 2f, 200, L.Height, (x, z, h, s) => TerrainBuilder.NaturalGround(x, z, h, s, 0f, 0f), 2048);
            return terrain;
        }

        static void WallTorch(Transform t, Vector3 pos, float yaw, bool motes)
        {
            var root = Shapes.Pivot(t, Vector3.zero, "WallTorch");
            root.position = pos;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            Shapes.Stick(root, new Vector3(0f, -0.35f, -0.25f), new Vector3(0f, 0.05f, 0.05f), 0.05f, ModelKit.For("wood_rough"));
            Shapes.Stick(root, new Vector3(0f, -0.02f, -0.02f), new Vector3(0f, 0.16f, 0.12f), 0.09f, ModelKit.For("char"));
            FireFx.Fire(root, new Vector3(0f, 0.18f, 0.12f), 0.3f, false, false, 10f, 2f, false);
            if (motes) DustMotes(root, new Vector3(0f, -0.2f, 1.2f));
        }

        /// <summary>A few warm specks of dust drifting in the torchlight: the air in the tunnels feels thick and old.</summary>
        static void DustMotes(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("DustMotes");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new Color(1f, 0.78f, 0.5f, 0.55f);
            main.maxParticles = 14;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.002f;
            var em = ps.emission; em.rateOverTime = 2.2f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(2.4f, 1.8f, 2f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.06f; noise.frequency = 0.3f; noise.quality = ParticleSystemNoiseQuality.Low;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FireFx.SoftAlphaMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }

        static void Decor(Transform t, CatacombsLayout.Spot d)
        {
            switch (d.Kind)
            {
                case "bones":
                    if (Camp.Has(Camp.Kind.BonePile)) Camp.Place(Camp.Kind.BonePile, t, d.Pos, d.Yaw, d.Scale, false);
                    break;
                case "mossrock":
                    PropLibrary.Spawn(PropLibrary.Kind.MossRock, t, d.Pos, d.Yaw, d.Scale, (int)d.Yaw, true);
                    break;
                case "roots":
                    var r = PropLibrary.Spawn(PropLibrary.Kind.Roots, t, d.Pos + Vector3.up * 0.02f, d.Yaw, d.Scale);
                    if (r != null) r.transform.rotation = Quaternion.Euler(180f, 0f, 0f) * r.transform.rotation;
                    break;
                case "fire":
                    Camp.Fire(t, d.Pos);
                    break;
            }
        }

        static void Pickup(Transform root, CatacombsLayout.Spot p)
        {
            var parts = p.Kind.Split(':');
            switch (parts[0])
            {
                case "weapon": Neanderthal.Pickup.Weapon(root, p.Pos, parts[1], 22f); break;
                case "ammo":
                    var type = (AmmoType)System.Enum.Parse(typeof(AmmoType), parts[1]);
                    Neanderthal.Pickup.AmmoBox(root, p.Pos, type, int.Parse(parts[2]), 20f);
                    break;
                case "health": Neanderthal.Pickup.Create(root, p.Pos, PickupKind.Health, amount: 40, respawn: 25f); break;
                case "armor": Neanderthal.Pickup.Create(root, p.Pos, PickupKind.Armor, amount: 50, respawn: 40f); break;
            }
        }

        // ------------------------------------------------------------------ waterfall
        static void BuildWaterfall(Transform root)
        {
            var t = Shapes.Pivot(root, Vector3.zero, "Waterfall");
            // Rock faces softening the cliff block's edges.
            if (PropLibrary.Has(PropLibrary.Kind.CliffA))
            {
                PropLibrary.Spawn(PropLibrary.Kind.CliffB, t, new Vector3(-38f, Ground(-38f, 63f) - 2f, 63f), 160f, 2.2f, 0, true);
                PropLibrary.Spawn(PropLibrary.Kind.CliffA, t, new Vector3(2f, Ground(2f, 63f) - 2f, 63f), 205f, 2.2f, 0, true);
            }
            for (int k = 0; k < 6; k++)
            {
                float x = k < 3 ? -33f + k * 4f : -3f - (k - 3) * 4f;
                Scenery.Rock(t, new Vector3(x, 15.2f, 64f + (k % 2) * 6f), new Vector3(4.5f, 2.2f, 4f), k * 47f, false, false, k % 4);
            }

            // Falling water: two scrolling streak layers, slanted out from the lip so there's a dry ledge behind.
            var tex = TextureLibrary.Albedo("waterfall");
            float top = CatacombsLayout.RiverWater + 0.1f, bottom = CatacombsLayout.PoolWater - 0.2f;
            float zTop = CatacombsLayout.CliffFront + 0.1f, zBottom = 57.6f;
            var sheet = Shapes.Pivot(t, new Vector3(-18f, (top + bottom) * 0.5f, (zTop + zBottom) * 0.5f), "Curtain");
            float tilt = Mathf.Atan2(zTop - zBottom, top - bottom) * Mathf.Rad2Deg;
            sheet.rotation = Quaternion.Euler(-tilt, 180f, 0f);
            float len = Mathf.Sqrt((top - bottom) * (top - bottom) + (zTop - zBottom) * (zTop - zBottom));
            for (int layer = 0; layer < 2; layer++)
            {
                var m = new Material(Mat.WaterSurface(new Color(0.86f, 0.93f, 0.97f, 0.92f))) { name = "WaterfallSheet" + layer };
                if (tex != null)
                {
                    m.mainTexture = tex;
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                    m.mainTextureScale = new Vector2(2.5f, 1.6f);
                }
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                var q = Shapes.Prim(PrimitiveType.Quad, sheet, new Vector3(0f, 0f, layer * 0.3f), new Vector3(6.2f - layer * 0.8f, len, 1f), m, null, false, "Sheet");
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var scroll = q.AddComponent<UvScroll>();
                scroll.Target = m;
                scroll.Speed = new Vector2(0f, layer == 0 ? 1.4f : 1.9f);
            }
            SightBlocker.Add(sheet.gameObject, Vector3.zero, new Vector3(6.4f, len, 1.2f));
            Mist(t, new Vector3(-18f, CatacombsLayout.PoolWater + 0.1f, 57.4f));
            Sfx.Loop("water", t, new Vector3(-18f, 1f, 59f), 0.9f, 55f);
        }

        static void Mist(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Mist");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startColor = new Color(0.9f, 0.95f, 1f, 0.2f);
            main.maxParticles = 100;
            main.gravityModifier = 0.15f;
            var em = ps.emission; em.rateOverTime = 26f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(6f, 1f, 0.5f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FireFx.SoftAlphaMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        // ------------------------------------------------------------------ surface
        static bool Free(float x, float z, float margin)
        {
            if (L.Wet(x, z) || L.NearPortal(x, z, 10f + margin) || L.NearHole(x, z, 2.5f + margin)) return false;
            if (x > -40f && x < 4f && z > 54f && z < 82f) return false;               // waterfall cliff & pool rim
            if (ArenaLevels.Slope(Ground, x, z) > 0.6f) return false;
            if (Vector2.Distance(new Vector2(x, z), CatacombsLayout.CampE) < 14f + margin) return false;
            if (Vector2.Distance(new Vector2(x, z), CatacombsLayout.CampW) < 14f + margin) return false;
            foreach (var s in L.Spawns) if (s.Pos.y > -6f && Vector2.Distance(new Vector2(x, z), new Vector2(s.Pos.x, s.Pos.z)) < 3f) return false;
            return true;
        }

        static void BuildSurface(Transform root)
        {
            var rnd = new System.Random(77);
            System.Func<float, float, bool> free = (x, z) => Free(x, z, 0f);
            System.Func<float, float, bool> wide = (x, z) => Free(x, z, 2f);
            var area = new Rect(-120, -120, 240, 240);

            var trees = Shapes.Pivot(root, Vector3.zero, "Trees");
            foreach (var p in Scenery.Scatter(rnd, 300, area, wide, 9000))
            {
                var pos = new Vector3(p.x, Ground(p.x, p.z) - 0.2f, p.z);
                if (rnd.NextDouble() < 0.8) Scenery.Pine(trees, pos, 10f + (float)rnd.NextDouble() * 9f, rnd.Next());
                else Scenery.BroadTree(trees, pos, 7f + (float)rnd.NextDouble() * 4f, rnd.Next());
            }
            foreach (var p in Scenery.Scatter(rnd, 40, area, wide))
            {
                float s = 1f + (float)rnd.NextDouble() * 2.5f;
                Scenery.Rock(root, new Vector3(p.x, Ground(p.x, p.z), p.z), new Vector3(s * 1.4f, s, s), (float)rnd.NextDouble() * 360f, false, false, rnd.Next(4));
            }
            var prnd = new System.Random(31);
            PropLibrary.Scatter(PropLibrary.Kind.Fern, root, prnd, 380, area, Ground, free, 1.1f, 2f, false);
            PropLibrary.Scatter(PropLibrary.Kind.Shrub, root, prnd, 110, area, Ground, free, 1.8f, 3.2f, false);
            PropLibrary.Scatter(PropLibrary.Kind.MossRock, root, prnd, 70, area, Ground, free, 0.8f, 2f, true);
            PropLibrary.Scatter(PropLibrary.Kind.Branches, root, prnd, 90, area, Ground, free, 1.2f, 2.2f, false);
            PropLibrary.Scatter(PropLibrary.Kind.Stump, root, prnd, 20, area, Ground, free, 0.8f, 1.3f, true);
            PropLibrary.Scatter(PropLibrary.Kind.Log, root, prnd, 22, area, Ground, wide, 1.6f, 2.4f, true);
            PropLibrary.Scatter(PropLibrary.Kind.BigStump, root, prnd, 8, area, Ground, wide, 0.9f, 1.3f, true);
            PropLibrary.Scatter(PropLibrary.Kind.Rock, root, prnd, 40, area, Ground, free, 3f, 7f, true, null, 0.15f);
            // Ferns on the banks (dry ground only).
            PropLibrary.Scatter(PropLibrary.Kind.Fern, root, prnd, 90, area, Ground, (x, z) =>
                !L.Wet(x, z) && Ground(x, z) > 0.4f && Free(x, z, -4f) &&
                (Vector2.Distance(new Vector2(x, z), CatacombsLayout.Pool) < 18f || (z < 46f && Mathf.Abs(x - CatacombsLayout.StreamX(z)) < 8.5f)), 1.2f, 2.2f, false);
            PropLibrary.CliffRing(root, prnd, 128f, 40, Ground, 2.6f, 3.8f);

            GrassField.Create(root, new Rect(-118, -118, 236, 236), 0.9f, Ground, (x, z) =>
            {
                if (L.Wet(x, z) || Ground(x, z) < 0.3f || L.NearPortal(x, z, 8f) || L.NearHole(x, z, 1.5f)) return 0f;
                if (x > -40f && x < 4f && z > 56f && z < 80f) return 0f;
                if (ArenaLevels.Slope(Ground, x, z) > 0.55f) return 0f;
                return Mathf.Clamp01((Mat.Fbm(x * 0.08f + 7f, z * 0.08f, 2) - 0.25f) * 2.2f);
            }, new[] { new Color(0.22f, 0.32f, 0.11f), new Color(0.3f, 0.37f, 0.13f), new Color(0.17f, 0.26f, 0.09f) }, 21);

            Camp.Build(root, new Vector3(CatacombsLayout.CampE.x, 0f, CatacombsLayout.CampE.y), Ground, 11, true, 8f);
            Camp.Build(root, new Vector3(CatacombsLayout.CampW.x, 0f, CatacombsLayout.CampW.y), Ground, 12, false, 7f);
        }

        // ------------------------------------------------------------------ atmosphere
        Color sky, equator, ground, fog;
        float fogDensity, sunIntensity;
        float under;

        void Start()
        {
            sky = RenderSettings.ambientSkyColor;
            equator = RenderSettings.ambientEquatorColor;
            ground = RenderSettings.ambientGroundColor;
            fog = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;
            sunIntensity = RenderSettings.sun != null ? RenderSettings.sun.intensity : 1f;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || L == null) return;
            // Below ground the daylight goes away completely (no sun leaking through wall joints):
            // only the torches and fires light the catacombs.
            Vector3 p = cam.transform.position;
            float target = L.Underground(p) ? 1f : 0f;
            under = Mathf.MoveTowards(under, target, Time.deltaTime * 1.2f);
            Sfx.Underground = under > 0.5f;
            float k = Mathf.Lerp(1f, 0.06f, under);
            RenderSettings.ambientSkyColor = sky * k;
            RenderSettings.ambientEquatorColor = equator * k;
            RenderSettings.ambientGroundColor = ground * k;
            RenderSettings.fogColor = Color.Lerp(fog, new Color(0.025f, 0.02f, 0.016f), under);
            RenderSettings.fogDensity = Mathf.Lerp(fogDensity, 0.035f, under);
            if (RenderSettings.sun != null)
            {
                RenderSettings.sun.intensity = sunIntensity * (1f - under);
                RenderSettings.sun.enabled = under < 0.99f;
            }
        }

        void OnDestroy()
        {
            Sfx.Underground = false;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;
            if (RenderSettings.sun != null) { RenderSettings.sun.intensity = sunIntensity; RenderSettings.sun.enabled = true; }
        }
    }

    /// <summary>Scrolls a material's main texture (flowing water).</summary>
    public class UvScroll : MonoBehaviour
    {
        public Material Target;
        public Vector2 Speed;

        void Update()
        {
            if (Target == null) return;
            Vector2 o = Speed * Time.time;
            Target.mainTextureOffset = o;
            if (Target.HasProperty("_BaseMap")) Target.SetTextureOffset("_BaseMap", o);
        }
    }
}
