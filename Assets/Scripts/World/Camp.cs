using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Neanderthal camp pieces (Blender models from Tools/blender/make_structures.py): hide tipis, a mammoth-bone hut,
    /// a thatched lean-to, hide drying racks, wood and bone piles - and whole camps arranged around a fire.
    /// </summary>
    public static class Camp
    {
        public enum Kind { Tipi, MammothHut, LeanTo, DryingRack, WoodPile, BonePile }

        public static bool Has(Kind k) => ModelKit.Has("Models/Camp/" + k);

        /// <summary>Place a structure; its doorway / open side faces local +Z (rotate with yaw).</summary>
        public static GameObject Place(Kind k, Transform parent, Vector3 pos, float yaw, float scale = 1f, bool collide = true)
        {
            var go = ModelKit.Spawn("Models/Camp/" + k, parent, out _);
            if (go == null) return null;
            go.name = k.ToString();
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            if (collide) ModelKit.AddColliders(go);
            return go;
        }

        /// <summary>Lowest ground under a footprint so nothing floats on slopes.</summary>
        public static float GroundUnder(System.Func<float, float, float> height, float x, float z, float radius)
        {
            float y = height(x, z);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                y = Mathf.Min(y, height(x + Mathf.Cos(a) * radius, z + Mathf.Sin(a) * radius));
            }
            return y;
        }

        static float FaceYaw(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        /// <summary>
        /// A camp around a burning fire: shelters on a ring facing the fire, drying racks, wood and bones.
        /// Returns false when the models are missing (nothing is built then).
        /// </summary>
        public static bool Build(Transform parent, Vector3 center, System.Func<float, float, float> height, int seed,
            bool mammothHut = true, float radius = 7.5f, bool healingFire = false)
        {
            if (!Has(Kind.Tipi)) return false;
            var rnd = new System.Random(seed);
            var root = Shapes.Pivot(parent, Vector3.zero, "Camp");
            center.y = height(center.x, center.z);

            if (healingFire) Campfire.Create(root, center);
            else Fire(root, center);

            float a0 = (float)rnd.NextDouble() * Mathf.PI * 2f;
            Kind[] ring = mammothHut
                ? new[] { Kind.MammothHut, Kind.Tipi, Kind.LeanTo, Kind.Tipi }
                : new[] { Kind.Tipi, Kind.LeanTo, Kind.Tipi };
            for (int i = 0; i < ring.Length; i++)
            {
                float a = a0 + i * Mathf.PI * 2f / ring.Length + ((float)rnd.NextDouble() - 0.5f) * 0.3f;
                float r = radius * (ring[i] == Kind.MammothHut ? 1.15f : 1f);
                var p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                p.y = GroundUnder(height, p.x, p.z, ring[i] == Kind.MammothHut ? 2.6f : 2f) - 0.05f;
                Place(ring[i], root, p, FaceYaw(p, center), ring[i] == Kind.Tipi ? 0.9f + (float)rnd.NextDouble() * 0.2f : 1f);
            }
            // Between the shelters: drying racks, firewood and a heap of bones.
            Kind[] extras = { Kind.DryingRack, Kind.WoodPile, Kind.BonePile, Kind.DryingRack };
            for (int i = 0; i < extras.Length; i++)
            {
                float a = a0 + (i + 0.5f) * Mathf.PI * 2f / ring.Length;
                float r = extras[i] == Kind.WoodPile ? radius * 0.45f : radius * 0.85f;
                var p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                p.y = GroundUnder(height, p.x, p.z, 1f) - 0.03f;
                float yaw = extras[i] == Kind.DryingRack ? FaceYaw(p, center) + 90f : (float)rnd.NextDouble() * 360f;
                Place(extras[i], root, p, yaw, 1f, extras[i] != Kind.BonePile);
            }
            // Log seats around the fire.
            for (int i = 0; i < 3; i++)
            {
                float a = a0 + 0.4f + i * Mathf.PI * 2f / 3f;
                var p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.3f;
                p.y = height(p.x, p.z);
                PropLibrary.Spawn(PropLibrary.Kind.Log, root, p, FaceYaw(p, center) + 90f, 0.9f, 0, true);
            }
            return true;
        }

        /// <summary>Decorative fire (no healing): stone pit, logs and the particle fire with light.</summary>
        public static void Fire(Transform parent, Vector3 pos)
        {
            var t = Shapes.Pivot(parent, Vector3.zero, "Fire");
            t.position = pos;
            if (PropLibrary.Spawn(PropLibrary.Kind.FirePit, t, pos, Random.Range(0f, 360f), 0.95f) == null)
            {
                var stone = Mat.Get(Mat.Stone);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f;
                    Shapes.Ball(t, new Vector3(Mathf.Cos(a) * 0.6f, 0.08f, Mathf.Sin(a) * 0.6f), new Vector3(0.3f, 0.2f, 0.25f), stone, false);
                }
            }
            Campfire.FireWood(t);
            FireFx.Fire(t, new Vector3(0, 0.15f, 0), 1f, true, false, 12f, 2.6f, true);
            Sfx.Loop("fire", t, Vector3.up * 0.4f, 0.6f, 14f);
        }
    }
}
