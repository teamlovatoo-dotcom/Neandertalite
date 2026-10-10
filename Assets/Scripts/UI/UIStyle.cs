using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Shared IMGUI styling matching the concept: dark slate panels, thin outlines, spaced uppercase labels,
    /// steel-blue highlight. All UI is drawn on a virtual 1920x1080 canvas scaled to the screen.
    /// </summary>
    public static class UIStyle
    {
        public static readonly Color Panel = new Color(0.07f, 0.08f, 0.09f, 0.92f);
        public static readonly Color PanelLight = new Color(0.12f, 0.13f, 0.15f, 0.95f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color Accent = new Color(0.2f, 0.42f, 0.75f, 1f);
        public static readonly Color AccentRed = new Color(0.66f, 0.17f, 0.15f, 1f);
        public static readonly Color TextColor = new Color(0.88f, 0.88f, 0.86f);
        public static readonly Color TextDim = new Color(0.6f, 0.62f, 0.63f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        public static readonly Color Good = new Color(0.55f, 0.9f, 0.55f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.4f);

        public const float VirtualHeight = 1080f;
        public static float Scale => Screen.height / VirtualHeight;
        public static float Width => Screen.width / Scale;
        public static float Height => VirtualHeight;

        static Texture2D white;
        static Texture2D ring;
        static Texture2D vignette;
        static readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();

        public static Texture2D White
        {
            get
            {
                if (white == null)
                {
                    white = new Texture2D(1, 1);
                    white.SetPixel(0, 0, Color.white);
                    white.Apply();
                }
                return white;
            }
        }

        public static Texture2D Ring
        {
            get
            {
                if (ring == null) ring = MakeRing(128, 0.47f, 0.012f);
                return ring;
            }
        }

        public static Texture2D Vignette
        {
            get
            {
                if (vignette == null)
                {
                    int s = 128;
                    vignette = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                        vignette.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.6f))));
                    }
                    vignette.Apply();
                }
                return vignette;
            }
        }

        static Texture2D MakeRing(int s, float radius, float thickness)
        {
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / thickness);
                float fill = d < radius ? 0.35f : 0f;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Max(a, fill * 0.5f)));
            }
            tex.Apply();
            return tex;
        }

        public static void BeginCanvas()
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1f));
        }

        public static GUIStyle Label(int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false)
        {
            string key = $"{size}_{ColorUtility.ToHtmlStringRGBA(color)}_{anchor}_{bold}";
            if (!styles.TryGetValue(key, out var st))
            {
                st = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    alignment = anchor,
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                    wordWrap = false,
                    richText = true,
                    clipping = TextClipping.Overflow
                };
                st.normal.textColor = color;
                styles[key] = st;
            }
            return st;
        }

        public static GUIStyle Wrap(int size, Color color)
        {
            string key = $"wrap_{size}_{ColorUtility.ToHtmlStringRGBA(color)}";
            if (!styles.TryGetValue(key, out var st))
            {
                st = new GUIStyle(Label(size, color)) { wordWrap = true, clipping = TextClipping.Clip };
                styles[key] = st;
            }
            return st;
        }

        /// <summary>Spaced uppercase heading like "S U R V I V A L".</summary>
        public static string Spaced(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.ToUpperInvariant())
            {
                sb.Append(c);
                sb.Append(c == ' ' ? "  " : " ");
            }
            return sb.ToString().TrimEnd();
        }

        public static void Rect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        public static void Outline(Rect r, Color c, float w = 1f)
        {
            Rect(new Rect(r.x, r.y, r.width, w), c);
            Rect(new Rect(r.x, r.yMax - w, r.width, w), c);
            Rect(new Rect(r.x, r.y, w, r.height), c);
            Rect(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        public static void PanelBox(Rect r, bool light = false)
        {
            Rect(r, light ? PanelLight : Panel);
            Outline(r, Line);
        }

        public static void Text(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, bool bold = false)
            => GUI.Label(r, s, Label(size, c, a, bold));

        public static void Shadowed(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, bool bold = false)
        {
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, Label(size, new Color(0, 0, 0, 0.7f * c.a), a, bold));
            GUI.Label(r, s, Label(size, c, a, bold));
        }

        /// <summary>Flat button with hover highlight. Returns true on click.</summary>
        public static bool Button(Rect r, string label, bool selected = false, bool enabled = true, int size = 18)
        {
            bool hover = r.Contains(Event.current.mousePosition) && enabled;
            Color bg = selected ? Accent : (hover ? new Color(1f, 1f, 1f, 0.09f) : new Color(1f, 1f, 1f, 0.03f));
            Rect(r, bg);
            Outline(r, selected ? new Color(0.5f, 0.7f, 1f, 0.8f) : Line);
            Text(r, label, size, enabled ? (selected ? Color.white : TextColor) : TextDim, TextAnchor.MiddleCenter);
            if (enabled && Event.current.type == EventType.MouseDown && Event.current.button == 0 && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                Sfx.Play2D("click");
                return true;
            }
            return false;
        }

        public static void Bar(Rect r, float fraction, Color fill)
        {
            Rect(r, new Color(1f, 1f, 1f, 0.12f));
            Rect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), fill);
        }

        public static float Slider(Rect r, string label, float value, float min, float max, string format)
        {
            Text(new Rect(r.x, r.y, r.width * 0.4f, r.height), label, 18, TextColor, TextAnchor.MiddleLeft);
            Rect track = new Rect(r.x + r.width * 0.42f, r.y + r.height * 0.5f - 3, r.width * 0.43f, 6);
            Rect(track, new Color(1, 1, 1, 0.15f));
            float k = Mathf.InverseLerp(min, max, value);
            Rect(new Rect(track.x, track.y, track.width * k, track.height), Accent);
            Rect(new Rect(track.x + track.width * k - 6, track.y - 7, 12, 20), Color.white);
            Text(new Rect(r.x + r.width * 0.88f, r.y, r.width * 0.12f, r.height), value.ToString(format), 18, TextDim, TextAnchor.MiddleRight);
            var e = Event.current;
            Rect hit = new Rect(track.x - 8, r.y, track.width + 16, r.height);
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && hit.Contains(e.mousePosition))
            {
                value = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                e.Use();
                GUI.changed = true;
            }
            return value;
        }
    }
}
