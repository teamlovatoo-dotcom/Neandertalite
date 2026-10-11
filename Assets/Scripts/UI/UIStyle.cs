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

        static Font fontRegular, fontBold, fontSemiBold, fontLight;
        public static Font FontRegular => fontRegular != null ? fontRegular : (fontRegular = Resources.Load<Font>("Fonts/Oswald-Regular"));
        public static Font FontBold => fontBold != null ? fontBold : (fontBold = Resources.Load<Font>("Fonts/Oswald-Bold"));
        public static Font FontSemiBold => fontSemiBold != null ? fontSemiBold : (fontSemiBold = Resources.Load<Font>("Fonts/Oswald-SemiBold"));
        public static Font FontLight => fontLight != null ? fontLight : (fontLight = Resources.Load<Font>("Fonts/Oswald-Light"));

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

        static Texture2D fadeRight;

        /// <summary>Horizontal gradient, opaque on the left fading to transparent on the right.</summary>
        public static Texture2D FadeRight
        {
            get
            {
                if (fadeRight == null)
                {
                    fadeRight = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                    for (int x = 0; x < 256; x++)
                    {
                        float k = x / 255f;
                        fadeRight.SetPixel(x, 0, new Color(1, 1, 1, 1f - k * k * (3f - 2f * k)));
                    }
                    fadeRight.Apply();
                }
                return fadeRight;
            }
        }

        static Texture2D fadeUp;

        /// <summary>Vertical gradient, opaque at the bottom fading to transparent at the top.</summary>
        public static Texture2D FadeUp
        {
            get
            {
                if (fadeUp == null)
                {
                    fadeUp = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                    for (int y = 0; y < 256; y++)
                    {
                        float k = y / 255f;
                        fadeUp.SetPixel(0, y, new Color(1, 1, 1, 1f - k * k * (3f - 2f * k)));
                    }
                    fadeUp.Apply();
                }
                return fadeUp;
            }
        }

        /// <summary>Draw a texture upside down (e.g. FadeUp as a top band).</summary>
        public static void IconFlipped(Rect r, Texture2D tex, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTextureWithTexCoords(r, tex, new Rect(0, 1, 1, -1));
            GUI.color = old;
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

        static Texture2D coin, triangle, bolt, claw;

        /// <summary>Procedural icon from a signed shape function (inside > 0), anti-aliased.</summary>
        static Texture2D Shape(int w, int h, System.Func<float, float, float> inside)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = 0f;
                for (int s = 0; s < 4; s++)
                {
                    float u = (x + 0.25f + (s % 2) * 0.5f) / w, v = (y + 0.25f + (s / 2) * 0.5f) / h;
                    a += inside(u, v) > 0f ? 0.25f : 0f;
                }
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            return tex;
        }

        static bool InPoly(float x, float y, Vector2[] p)
        {
            bool c = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if (((p[i].y > y) != (p[j].y > y)) && (x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)) c = !c;
            return c;
        }

        public static Texture2D Coin => coin != null ? coin : (coin = Shape(32, 32, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
            return d < 0.46f && !(d > 0.3f && d < 0.36f) ? 1f : -1f;
        }));

        public static Texture2D Triangle => triangle != null ? triangle : (triangle = Shape(32, 32, (u, v) =>
            InPoly(u, v, new[] { new Vector2(0.5f, 0.95f), new Vector2(0.9f, 0.08f), new Vector2(0.5f, 0.28f), new Vector2(0.1f, 0.08f) }) ? 1f : -1f));

        public static Texture2D Bolt => bolt != null ? bolt : (bolt = Shape(32, 32, (u, v) =>
            InPoly(u, v, new[] { new Vector2(0.62f, 0.98f), new Vector2(0.18f, 0.44f), new Vector2(0.46f, 0.44f), new Vector2(0.34f, 0.02f), new Vector2(0.84f, 0.6f), new Vector2(0.55f, 0.6f) }) ? 1f : -1f));

        /// <summary>A single tapered, slightly ragged claw scratch (vertical; rotate when drawing).</summary>
        public static Texture2D Claw => claw != null ? claw : (claw = Shape(48, 320, (u, v) =>
        {
            float taper = Mathf.Sin(Mathf.Clamp01(v) * Mathf.PI);
            float half = 0.42f * Mathf.Pow(taper, 0.8f);
            float rag = (Mathf.PerlinNoise(v * 18f, 3.3f) - 0.5f) * 0.18f;
            float center = 0.5f + (v - 0.5f) * 0.12f;
            return half - Mathf.Abs(u - center - rag) ;
        }));

        public static void Icon(Rect r, Texture2D tex, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, tex);
            GUI.color = old;
        }

        /// <summary>Gold coin icon followed by an amount. Anchor Left or Right.</summary>
        public static void Coins(Rect r, string amount, int size, Color textColor, bool alignRight = false, bool shadow = false)
        {
            var st = Label(size, textColor, TextAnchor.MiddleLeft, FontSemiBold);
            float tw = st.CalcSize(new GUIContent(amount)).x;
            float icon = size * 0.8f;
            float total = icon + 6f + tw;
            float x = alignRight ? r.xMax - total : r.x;
            Icon(new Rect(x, r.center.y - icon * 0.5f, icon, icon), Coin, Gold);
            var tr = new Rect(x + icon + 6f, r.y, tw + 4f, r.height);
            if (shadow) GUI.Label(new Rect(tr.x + 2, tr.y + 2, tr.width, tr.height), amount, Label(size, new Color(0, 0, 0, 0.7f), TextAnchor.MiddleLeft, FontSemiBold));
            GUI.Label(tr, amount, st);
        }

        public static void BeginCanvas()
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1f));
        }

        public static GUIStyle Label(int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false)
            => Label(size, color, anchor, bold ? FontSemiBold : FontRegular, bold);

        public static GUIStyle Label(int size, Color color, TextAnchor anchor, Font font, bool fallbackBold = false)
        {
            string key = $"{size}_{ColorUtility.ToHtmlStringRGBA(color)}_{anchor}_{(font != null ? font.name : "default")}_{fallbackBold}";
            if (!styles.TryGetValue(key, out var st))
            {
                st = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = size,
                    alignment = anchor,
                    // Oswald carries its own weight; only fake bold when the font file is missing.
                    fontStyle = font == null && fallbackBold ? FontStyle.Bold : FontStyle.Normal,
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
            GUI.Label(r, label, Label(size, enabled ? (selected ? Color.white : TextColor) : TextDim, TextAnchor.MiddleCenter, FontSemiBold));
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
