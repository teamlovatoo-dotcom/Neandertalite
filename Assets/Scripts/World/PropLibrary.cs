using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Photo-scanned nature props from Poly Haven (CC0), decimated in Blender (Tools/blender) and stored in
    /// Resources/Models/Props. Each prop gets a photo-textured material; plants use alpha cutout.
    /// </summary>
    public static class PropLibrary
    {
        public enum Kind { Fern, Shrub, MossRock, Branches, FirePit, Stump, Log, DesertBoulder, CliffA, CliffB, BigStump, Roots, Rock }

        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        static string Texture(Kind k)
        {
            switch (k)
            {
                case Kind.Fern: return "fern";
                case Kind.Shrub: return "shrub";
                case Kind.MossRock: return "mossrock";
                case Kind.Branches: return "branches";
                case Kind.FirePit: return "firepit";
                case Kind.Stump: return "stump";
                case Kind.Log: return "log";
                case Kind.CliffA: return "cliffa";
                case Kind.CliffB: return "cliffb";
                case Kind.BigStump: return "bigstump";
                case Kind.Roots: return "roots";
                case Kind.Rock: return "rock07";
                default: return "desertboulder";
            }
        }

        static int Variants(Kind k) => k == Kind.Fern ? 4 : k == Kind.MossRock ? 7 : k == Kind.Branches ? 3 : 1;

        static GameObject Prefab(Kind k, int variant)
        {
            string name = Variants(k) > 1 ? $"{k}_{variant % Variants(k)}" : k == Kind.Rock ? "Rock07" : k.ToString();
            if (!prefabs.TryGetValue(name, out var go))
            {
                go = Resources.Load<GameObject>("Models/Props/" + name);
                prefabs[name] = go;
            }
            return go;
        }

        public static bool Has(Kind k) => Prefab(k, 0) != null && TextureLibrary.Has(Texture(k));

        static Material Material(Kind k, Color? tint)
        {
            string key = k + (tint.HasValue ? ColorUtility.ToHtmlStringRGB(tint.Value) : "");
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            bool plant = k == Kind.Fern || k == Kind.Shrub;
            m = Mat.Pbr(Texture(k), plant ? Mat.Leaves : Mat.Rock, plant ? 0.12f : 0.2f, null, tint ?? (plant ? new Color(0.95f, 1f, 0.9f) : (Color?)null));
            if (plant) Mat.MakeCutout(m, 0.4f);
            materials[key] = m;
            return m;
        }

        /// <summary>Instantiate a prop standing on the ground at pos. Returns null if the asset is missing.</summary>
        public static GameObject Spawn(Kind k, Transform parent, Vector3 pos, float yaw, float scale, int variant = 0, bool collider = false, Color? tint = null)
        {
            var prefab = Prefab(k, variant);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = k.ToString();
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;
            go.transform.localScale = go.transform.localScale * scale;
            var mat = Material(k, tint);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (collider)
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            return go;
        }

        /// <summary>
        /// Photo-scanned cliff faces around the edge of a square arena (half-size <paramref name="half"/>): a natural,
        /// solid backdrop. Faces turn inward; they are sunk into the rising terrain.
        /// </summary>
        public static void CliffRing(Transform parent, System.Random rnd, float half, int count, System.Func<float, float, float> height,
            float minScale, float maxScale, Color? tint = null, System.Func<float, float, bool> accept = null)
        {
            if (!Has(Kind.CliffA)) return;
            var root = Shapes.Pivot(parent, Vector3.zero, "Cliffs");
            for (int i = 0; i < count; i++)
            {
                float t = (i + (float)rnd.NextDouble() * 0.6f) / count * 4f;
                int side = (int)t;
                float f = (t - side) * 2f - 1f;
                float off = half + (float)rnd.NextDouble() * 6f;
                Vector2 p = side == 0 ? new Vector2(f * half, off) : side == 1 ? new Vector2(off, -f * half) : side == 2 ? new Vector2(-f * half, -off) : new Vector2(-off, f * half);
                if (accept != null && !accept(p.x, p.y)) continue;
                float yawIn = Mathf.Atan2(-p.x, -p.y) * Mathf.Rad2Deg + ((float)rnd.NextDouble() - 0.5f) * 60f;
                float s = Mathf.Lerp(minScale, maxScale, (float)rnd.NextDouble());
                Spawn(rnd.NextDouble() < 0.5 ? Kind.CliffA : Kind.CliffB, root, new Vector3(p.x, height(p.x, p.y) - 1.2f * s, p.y), yawIn, s, 0, true, tint);
            }
        }

        /// <summary>Scatter props over an area using a placement test; heights come from the level's height function.</summary>
        public static void Scatter(Kind k, Transform parent, System.Random rnd, int count, Rect area,
            System.Func<float, float, float> height, System.Func<float, float, bool> accept, float minScale, float maxScale, bool collider, Color? tint = null, float sink = 0.03f)
        {
            if (!Has(k)) return;
            var root = Shapes.Pivot(parent, Vector3.zero, k + "s");
            int placed = 0, tries = 0;
            while (placed < count && tries++ < count * 30)
            {
                float x = area.x + (float)rnd.NextDouble() * area.width;
                float z = area.y + (float)rnd.NextDouble() * area.height;
                if (!accept(x, z)) continue;
                float s = Mathf.Lerp(minScale, maxScale, (float)rnd.NextDouble());
                Spawn(k, root, new Vector3(x, height(x, z) - sink * s, z), (float)rnd.NextDouble() * 360f, s, rnd.Next(16), collider, tint);
                placed++;
            }
        }
    }
}

