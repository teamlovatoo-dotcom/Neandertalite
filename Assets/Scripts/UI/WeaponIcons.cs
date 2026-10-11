using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Hotbar / inventory icons rendered from the real weapon models, so every weapon (and any future one) gets an
    /// icon without hand-drawn art. Icons are made lazily, one per frame, by a hidden orthographic camera far below the
    /// map. If anything fails the HUD simply shows the weapon's name instead.
    /// </summary>
    public static class WeaponIcons
    {
        const int Size = 160;
        const int IconLayer = 31;                       // unused by the game; only the icon camera looks at it
        static readonly Vector3 Studio = new Vector3(0f, -3000f, 0f);
        // Same tone as the HUD cells: if the pipeline drops alpha, the icon still blends into its cell.
        static readonly Color Backdrop = new Color(0.075f, 0.08f, 0.09f, 0f);

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly List<string> queue = new List<string>();
        static readonly HashSet<string> failed = new HashSet<string>();
        static Camera cam;
        static Light keyLight, fillLight;
        static RenderTexture rt;

        /// <summary>The icon, or null while it is still queued (draw the name meanwhile).</summary>
        public static Texture2D Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var tex) && tex != null) return tex;
            if (!failed.Contains(id) && !queue.Contains(id)) queue.Add(id);
            return null;
        }

        /// <summary>Called once per frame (outside OnGUI); renders at most one pending icon.</summary>
        public static void Pump()
        {
            if (queue.Count == 0) return;
            string id = queue[0];
            queue.RemoveAt(0);
            try
            {
                var tex = Render(id);
                if (tex != null) cache[id] = tex; else failed.Add(id);
            }
            catch (System.Exception e)
            {
                failed.Add(id);
                Debug.LogWarning($"[WeaponIcons] {id}: {e.Message}");
            }
        }

        static void Setup()
        {
            if (cam != null) return;
            var go = new GameObject("WeaponIconCamera");
            Object.DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Backdrop;
            cam.cullingMask = 1 << IconLayer;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            go.transform.position = Studio + new Vector3(0f, 0f, -5f);
            go.transform.rotation = Quaternion.identity;

            keyLight = MakeLight("IconKey", Quaternion.Euler(35f, -40f, 0f), 1.25f, new Color(1f, 0.95f, 0.88f));
            fillLight = MakeLight("IconFill", Quaternion.Euler(-20f, 150f, 0f), 0.45f, new Color(0.7f, 0.8f, 1f));

            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "WeaponIconRT" };
            rt.Create();
        }

        static Light MakeLight(string name, Quaternion rot, float intensity, Color color)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            go.transform.rotation = rot;
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
            l.cullingMask = 1 << IconLayer;
            l.enabled = false;
            return l;
        }

        static Texture2D Render(string id)
        {
            if (ItemDatabase.Weapon(id) == null) return null;
            Setup();
            var model = WeaponModels.Build(id, null, out _);
            if (model == null) return null;
            try
            {
                // Lay the weapon's longest axis along the screen diagonal, like an inventory icon.
                model.transform.SetPositionAndRotation(Studio, Quaternion.identity);
                Bounds local = WorldBounds(model);
                Vector3 e = local.extents;
                Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
                Quaternion rot = Quaternion.FromToRotation(axis, new Vector3(1f, 1f, 0f).normalized);
                rot = Quaternion.AngleAxis(-25f, new Vector3(1f, 1f, 0f).normalized) * rot;   // show a bit of depth
                model.transform.rotation = rot;
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IconLayer;
                foreach (var l in model.GetComponentsInChildren<Light>(true)) l.enabled = false;
                foreach (var ps in model.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);

                Bounds b = WorldBounds(model);
                float half = Mathf.Max(b.extents.x, b.extents.y) * 1.12f + 0.01f;
                cam.orthographicSize = half;
                cam.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - Mathf.Max(2f, b.extents.z + 1f));
                cam.targetTexture = rt;

                var oldAmbient = RenderSettings.ambientLight;
                var oldMode = RenderSettings.ambientMode;
                var oldFog = RenderSettings.fog;
                var oldProbe = RenderSettings.ambientProbe;   // restored exactly, so the level's lighting is untouched
                RenderSettings.fog = false;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.45f);
                keyLight.enabled = fillLight.enabled = true;
                try { cam.Render(); }
                finally
                {
                    keyLight.enabled = fillLight.enabled = false;
                    RenderSettings.fog = oldFog;
                    RenderSettings.ambientMode = oldMode;
                    RenderSettings.ambientLight = oldAmbient;
                    RenderSettings.ambientProbe = oldProbe;
                }

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Icon_" + id, wrapMode = TextureWrapMode.Clamp };
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                return tex;
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.2f);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
