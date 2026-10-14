using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Loads the procedural Blender models (Tools/blender/make_weapons.py, make_structures.py) from Resources.
    /// Each FBX carries empties 'Fwd' and 'Up' one metre from the origin; from them the model is rotated to face +Z
    /// with +Y up and scaled to metres, so FBX axis conversion never matters. Other empties (NockA, Flame, Tip...)
    /// are returned as named points in the normalised space. Materials are swapped by name for photo-textured ones.
    /// </summary>
    public static class ModelKit
    {
        public class Info
        {
            public GameObject Prefab;
            public Quaternion Rotation;
            public float Scale;
            public readonly Dictionary<string, Vector3> Points = new Dictionary<string, Vector3>();
            public Vector3 Point(string name, Vector3 fallback) => Points.TryGetValue(name, out var p) ? p : fallback;
        }

        static readonly Dictionary<string, Info> infos = new Dictionary<string, Info>();
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public static bool Has(string path) => GetInfo(path) != null;

        public static Info GetInfo(string path)
        {
            if (infos.TryGetValue(path, out var info)) return info;
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) { infos[path] = null; return null; }
            info = new Info { Prefab = prefab, Rotation = Quaternion.identity, Scale = 1f };
            var tmp = Object.Instantiate(prefab);
            tmp.SetActive(false);
            var raw = new Dictionary<string, Vector3>();
            foreach (var t in tmp.GetComponentsInChildren<Transform>(true))
                if (t != tmp.transform && t.GetComponent<Renderer>() == null) raw[t.name] = tmp.transform.InverseTransformPoint(t.position);
            Object.Destroy(tmp);
            if (raw.TryGetValue("Fwd", out var fwd) && raw.TryGetValue("Up", out var up) && fwd.sqrMagnitude > 1e-6f && up.sqrMagnitude > 1e-6f)
            {
                info.Scale = 1f / up.magnitude;
                info.Rotation = Quaternion.Inverse(Quaternion.LookRotation(fwd, up));
            }
            foreach (var kv in raw)
                info.Points[kv.Key] = info.Rotation * (kv.Value * info.Scale);
            infos[path] = info;
            return info;
        }

        /// <summary>Instantiate a normalised copy under parent (origin = model origin). Returns null if missing.</summary>
        public static GameObject Spawn(string path, Transform parent, out Info info, bool obsidian = false, bool shadows = true)
        {
            info = GetInfo(path);
            if (info == null) return null;
            var root = new GameObject(System.IO.Path.GetFileName(path));
            root.transform.SetParent(parent, false);
            var inst = Object.Instantiate(info.Prefab, root.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = info.Rotation;
            inst.transform.localScale = Vector3.one * info.Scale;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var sm = r.sharedMaterials;
                for (int i = 0; i < sm.Length; i++) sm[i] = For(sm[i] != null ? sm[i].name : "", obsidian);
                r.sharedMaterials = sm;
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return root;
        }

        public static void AddColliders(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        /// <summary>Photo-textured material for a Blender material name (wood_fine, leather, stone, hide...).</summary>
        public static Material For(string name, bool obsidian = false)
        {
            string key = name;
            int cut = key.IndexOfAny(new[] { '.', ' ' });
            if (cut > 0) key = key.Substring(0, cut);
            key = key.ToLowerInvariant();
            if (key == "stone" && obsidian) key = "obsidian";
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            switch (key)
            {
                case "wood_fine": m = Mat.Pbr("wood_fine", Mat.Wood, 0.35f, null, new Color(1.25f, 1.1f, 0.95f)); break;
                case "wood_rough": m = Mat.Pbr("wood_rough", Mat.Wood, 0.15f, null, new Color(0.95f, 0.76f, 0.56f)); break;
                case "bark": m = Mat.Pbr("bark", Mat.Bark, 0.08f); break;
                case "pole_bark": m = Mat.Pbr("pole_bark", Mat.Bark, 0.08f, null, new Color(0.85f, 0.8f, 0.75f)); break;
                case "leather": m = Mat.Pbr("leather", Mat.Leather, 0.32f, null, new Color(1.1f, 1f, 0.95f)); break;
                case "sinew": m = Mat.Pbr("hide", Mat.Sinew, 0.35f, null, new Color(0.85f, 0.72f, 0.52f)); break;
                case "hide": m = Mat.Pbr("hide", Mat.Leather, 0.18f); break;
                case "fur": m = TextureLibrary.Has("fur") ? Mat.Pbr("fur", Mat.Fur, 0.08f) : Mat.Pbr("hide", Mat.Fur, 0.1f, null, new Color(0.55f, 0.42f, 0.3f)); break;
                case "skin": m = Mat.Pbr("hide", Mat.Skin, 0.32f, null, new Color(0.95f, 0.68f, 0.52f)); break;
                case "nail": m = Mat.Pbr("bone", Mat.Bone, 0.5f, null, new Color(0.9f, 0.75f, 0.65f)); break;
                case "stone": m = Mat.Pbr("flint", Mat.Flint, 0.55f); break;
                case "obsidian": m = Mat.Pbr("stone_dark", Mat.Flint, 0.93f, null, new Color(0.16f, 0.16f, 0.19f)); break;
                case "bone": m = Mat.Pbr("bone", Mat.Bone, 0.25f); break;
                case "ivory": m = Mat.Pbr("bone", Mat.Bone, 0.45f, null, new Color(1.05f, 1f, 0.9f)); break;
                case "char": m = Mat.Pbr("bark", Mat.WoodDark, 0.05f, null, new Color(0.22f, 0.18f, 0.16f)); break;
                case "feather": m = Mat.Pbr("feather", Mat.Bone, 0.1f); break;
                case "pebble": m = Mat.Pbr("boulder", Mat.Stone, 0.25f, null, new Color(0.72f, 0.7f, 0.66f)); break;
                case "rock": m = Scenery.RockMat; break;
                case "thatch": m = Mat.Pbr("thatch", Mat.Wood, 0.05f, null, new Color(1.35f, 1.12f, 0.78f)); break;
                default:
                    // Imported models (import_weapon.py) bring their own baked texture: Textures/<material>_diff.
                    m = TextureLibrary.Has(key) ? Mat.Pbr(key, Mat.Wood, 0.25f) : Mat.Get(Mat.Wood);
                    break;
            }
            mats[key] = m;
            return m;
        }
    }
}
