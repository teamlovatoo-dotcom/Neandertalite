using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>In-game HUD laid out like the concept: vitals bottom-left, ammo bottom-right, radar top-right.</summary>
    public static class Hud
    {
        struct FeedLine { public string Text; public Color Color; public float Time; }
        static readonly List<FeedLine> feed = new List<FeedLine>();
        static float hitMarkerTime = -9f;
        static bool hitWasKill;
        static float damageFlash;
        static float objectiveFlash;
        static string lastObjective;

        public static void Hook()
        {
            Inventory.Feed -= OnFeed;
            Inventory.Feed += OnFeed;
            Health.AnyDamaged -= OnAnyDamaged;
            Health.AnyDamaged += OnAnyDamaged;
        }

        static void OnFeed(string text, Color c)
        {
            feed.Add(new FeedLine { Text = text, Color = c, Time = Time.unscaledTime });
            if (feed.Count > 7) feed.RemoveAt(0);
        }

        static void OnAnyDamaged(Health h, DamageInfo info)
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            if (h == p.Health) damageFlash = Mathf.Min(1f, damageFlash + info.Amount / 30f + 0.2f);
            else if (info.Attacker == p.gameObject)
            {
                hitMarkerTime = Time.unscaledTime;
                hitWasKill = h.Current <= 0f;
                if (info.Headshot) OnFeed("У голову!", UIStyle.Gold);
            }
        }

        public static void Draw()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            float W = UIStyle.Width, H = UIStyle.Height;

            // Scope overlay
            if (p.Weapons.Scoped)
            {
                float s = H * 0.95f;
                var c = GUI.color;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(W * 0.5f - s * 0.5f, H * 0.5f - s * 0.5f, s, s), UIStyle.Vignette);
                GUI.color = c;
                UIStyle.Rect(new Rect(0, 0, W * 0.5f - s * 0.5f + 1, H), Color.black);
                UIStyle.Rect(new Rect(W * 0.5f + s * 0.5f - 1, 0, W, H), Color.black);
                UIStyle.Rect(new Rect(W * 0.5f - s * 0.5f, H * 0.5f, s, 1), new Color(0, 0, 0, 0.9f));
                UIStyle.Rect(new Rect(W * 0.5f, H * 0.5f - s * 0.5f, 1, s), new Color(0, 0, 0, 0.9f));
            }

            // Damage vignette
            damageFlash = Mathf.MoveTowards(damageFlash, 0f, Time.unscaledDeltaTime * 1.2f);
            float lowHp = p.Health.Fraction < 0.3f ? (0.3f - p.Health.Fraction) * 1.5f * (0.7f + 0.3f * Mathf.Sin(Time.time * 6f)) : 0f;
            float red = Mathf.Max(damageFlash, lowHp);
            if (red > 0.01f)
            {
                var c = GUI.color;
                GUI.color = new Color(0.6f, 0f, 0f, red * 0.8f);
                GUI.DrawTexture(new Rect(0, 0, W, H), UIStyle.Vignette);
                GUI.color = c;
            }

            if (p.Health.IsDead) return;

            DrawCrosshair(p, W, H);
            DrawVitals(p, H);
            DrawAmmo(p, W, H);
            DrawRadar(p, W);
            DrawResources(p, W);
            DrawFeed(H);
            DrawObjective(W);

            if (p.Interactor.Focus != null)
                UIStyle.Shadowed(new Rect(0, H * 0.5f + 60, W, 30), p.Interactor.Focus.Prompt, 20, UIStyle.TextColor, TextAnchor.MiddleCenter);
        }

        static void DrawCrosshair(PlayerController p, float W, float H)
        {
            if (p.Weapons.Scoped) return;
            float cx = W * 0.5f, cy = H * 0.5f;
            var w = p.Weapons.Current;
            Color c = new Color(1f, 1f, 1f, 0.85f);
            float gap = 6f + (p.Velocity.magnitude > 1f ? 4f : 0f) + (w != null && w.Aiming ? -3f : 0f);
            UIStyle.Rect(new Rect(cx - 1, cy - 1, 2, 2), c);
            UIStyle.Rect(new Rect(cx - gap - 8, cy - 1, 8, 2), c);
            UIStyle.Rect(new Rect(cx + gap, cy - 1, 8, 2), c);
            UIStyle.Rect(new Rect(cx - 1, cy + gap, 2, 8), c);
            if (w != null && w.Charge > 0.01f)
            {
                UIStyle.Bar(new Rect(cx - 40, cy + 28, 80, 4), w.Charge, w.Charge >= 1f ? UIStyle.Gold : Color.white);
            }

            float since = Time.unscaledTime - hitMarkerTime;
            if (since < 0.25f)
            {
                Color hc = hitWasKill ? UIStyle.Bad : Color.white;
                hc.a = 1f - since / 0.25f;
                float d = 10f, l = 8f;
                // Four diagonal ticks drawn as small rotated boxes.
                var m = GUI.matrix;
                for (int i = 0; i < 4; i++)
                {
                    float ang = 45f + i * 90f;
                    GUIUtility.RotateAroundPivot(ang, new Vector2(cx, cy) * UIStyle.Scale);
                    UIStyle.Rect(new Rect(cx + d, cy - 1, l, 2), hc);
                    GUI.matrix = m;
                }
            }
        }

        static void DrawVitals(PlayerController p, float H)
        {
            float x = 40, y = H - 110;
            // Health with a drawn plus sign
            UIStyle.Rect(new Rect(x + 6, y + 2, 4, 16), Color.white);
            UIStyle.Rect(new Rect(x, y + 8, 16, 4), Color.white);
            UIStyle.Shadowed(new Rect(x + 26, y - 6, 80, 30), Mathf.CeilToInt(p.Health.Current).ToString(), 26, Color.white, TextAnchor.MiddleLeft, true);
            UIStyle.Bar(new Rect(x + 90, y + 8, 180, 5), p.Health.Fraction, p.Health.Fraction < 0.3f ? UIStyle.Bad : Color.white);

            // Stamina with a lightning glyph
            float y2 = y + 36;
            UIStyle.Shadowed(new Rect(x - 2, y2 - 8, 24, 30), "ϟ", 24, Color.white, TextAnchor.MiddleCenter, true);
            UIStyle.Shadowed(new Rect(x + 26, y2 - 6, 80, 30), Mathf.FloorToInt(p.Stamina).ToString(), 26, Color.white, TextAnchor.MiddleLeft, true);
            UIStyle.Bar(new Rect(x + 90, y2 + 8, 180, 5), p.Stamina / p.MaxStamina, new Color(0.85f, 0.85f, 0.75f));

            if (p.Health.Armor > 0.5f)
            {
                float y3 = y - 36;
                UIStyle.Rect(new Rect(x + 1, y3 + 2, 14, 16), UIStyle.Accent);
                UIStyle.Shadowed(new Rect(x + 26, y3 - 6, 80, 30), Mathf.CeilToInt(p.Health.Armor).ToString(), 22, new Color(0.7f, 0.85f, 1f), TextAnchor.MiddleLeft, true);
                UIStyle.Bar(new Rect(x + 90, y3 + 8, 180, 4), p.Health.Armor / p.Health.MaxArmor, UIStyle.Accent);
            }
        }

        static void DrawAmmo(PlayerController p, float W, float H)
        {
            var w = p.Weapons.Current;
            if (w == null) return;
            float x = W - 340, y = H - 110;
            UIStyle.Shadowed(new Rect(x, y - 30, 300, 30), UIStyle.Spaced(w.Data.Name), 16, UIStyle.TextDim, TextAnchor.MiddleRight);
            string ammo = w.AmmoText;
            if (!string.IsNullOrEmpty(ammo))
            {
                string reserve = w.ReserveText;
                bool numeric = int.TryParse(reserve, out _);
                string text = numeric ? $"{ammo} <size=22><color=#9aa0a6>| {reserve}</color></size>" : $"{ammo} <size=18><color=#9aa0a6>{reserve.ToUpperInvariant()}</color></size>";
                UIStyle.Shadowed(new Rect(x, y, 300, 50), text, 40, Color.white, TextAnchor.MiddleRight, true);
            }
            // Weapon slots strip
            var list = p.Inventory.Weapons;
            float sx = W - 40;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var d = ItemDatabase.Weapon(list[i]);
                string label = d.Slot + " " + d.Name;
                float wdt = 12 + label.Length * 8.5f;
                sx -= wdt + 6;
                bool cur = list[i] == p.Weapons.CurrentId;
                UIStyle.Rect(new Rect(sx, H - 44, wdt, 24), cur ? new Color(0.2f, 0.42f, 0.75f, 0.85f) : new Color(0, 0, 0, 0.35f));
                UIStyle.Text(new Rect(sx, H - 44, wdt, 24), label, 14, cur ? Color.white : UIStyle.TextDim, TextAnchor.MiddleCenter);
            }
        }

        static void DrawRadar(PlayerController p, float W)
        {
            float size = 150f;
            Rect r = new Rect(W - size - 30, 24, size, size);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(r, UIStyle.Ring);
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(r, UIStyle.Ring);
            GUI.color = old;
            Vector2 c = r.center;
            float rad = size * 0.47f;
            float yaw = p.Yaw;
            string[] dirs = { "N", "E", "S", "W" };
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f - yaw) * Mathf.Deg2Rad;
                Vector2 pos = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * (rad - 12f);
                UIStyle.Text(new Rect(pos.x - 10, pos.y - 10, 20, 20), dirs[i], i == 0 ? 15 : 12, i == 0 ? Color.white : UIStyle.TextDim, TextAnchor.MiddleCenter, i == 0);
            }
            // Player arrow
            UIStyle.Text(new Rect(c.x - 10, c.y - 12, 20, 20), "▲", 16, Color.white, TextAnchor.MiddleCenter);

            // Objective / opponent blips
            var gm = GameManager.Instance;
            if (gm != null && gm.TryGetRadarTarget(out Vector3 target, out Color blip))
            {
                Vector3 d = target - p.transform.position;
                float dist = new Vector2(d.x, d.z).magnitude;
                float a = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - yaw) * Mathf.Deg2Rad;
                float k = Mathf.Clamp01(dist / 80f) * (rad - 14f);
                Vector2 pos = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * k;
                float pulse = 6f + Mathf.Sin(Time.unscaledTime * 5f) * 1.5f;
                UIStyle.Rect(new Rect(pos.x - pulse * 0.5f, pos.y - pulse * 0.5f, pulse, pulse), blip);
                UIStyle.Text(new Rect(r.x, r.yMax + 2, r.width, 20), $"{dist:0} м", 14, UIStyle.TextDim, TextAnchor.UpperCenter);
            }
        }

        static void DrawResources(PlayerController p, float W)
        {
            float y = 200;
            float x = W - 210;
            UIStyle.Shadowed(new Rect(x, y, 180, 24), $"<color=#ffd15a>◆</color> {Profile.Current.Coins}", 20, Color.white, TextAnchor.MiddleRight, true);
            y += 28;
            var inv = p.Inventory;
            foreach (Resource res in System.Enum.GetValues(typeof(Resource)))
            {
                int n = inv.Get(res);
                if (n <= 0) continue;
                UIStyle.Shadowed(new Rect(x, y, 180, 20), $"{ItemDatabase.ResourceName(res)}  <b>{n}</b>", 15, UIStyle.TextColor, TextAnchor.MiddleRight);
                y += 20;
            }
        }

        static void DrawFeed(float H)
        {
            float y = H * 0.55f;
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - feed[i].Time;
                if (age > 4f) { feed.RemoveAt(i); continue; }
                Color c = feed[i].Color;
                c.a = Mathf.Clamp01(4f - age);
                UIStyle.Shadowed(new Rect(40, y, 600, 24), feed[i].Text, 18, c);
                y -= 26;
            }
        }

        static void DrawObjective(float W)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            string obj = gm.ObjectiveText;
            if (string.IsNullOrEmpty(obj)) return;
            if (obj != lastObjective)
            {
                lastObjective = obj;
                objectiveFlash = 1f;
            }
            objectiveFlash = Mathf.MoveTowards(objectiveFlash, 0f, Time.unscaledDeltaTime * 0.5f);
            string chapter = gm.ObjectiveHeader;
            UIStyle.Shadowed(new Rect(40, 30, 700, 22), UIStyle.Spaced(chapter), 14, UIStyle.TextDim);
            Color c = Color.Lerp(Color.white, UIStyle.Gold, objectiveFlash);
            UIStyle.Shadowed(new Rect(40, 52, 800, 30), obj, 22, c, TextAnchor.UpperLeft, true);
            string hint = gm.ObjectiveHint;
            if (!string.IsNullOrEmpty(hint)) UIStyle.Shadowed(new Rect(40, 82, 800, 22), hint, 15, UIStyle.TextDim);
        }

        public static void DrawDuelScore(int blue, int red, float timeLeft, int target)
        {
            float W = UIStyle.Width;
            float cx = W * 0.5f;
            UIStyle.Rect(new Rect(cx - 140, 22, 60, 36), new Color(0.2f, 0.42f, 0.75f, 0.8f));
            UIStyle.Rect(new Rect(cx - 78, 22, 156, 36), new Color(0.1f, 0.12f, 0.14f, 0.85f));
            UIStyle.Rect(new Rect(cx + 80, 22, 60, 36), new Color(0.66f, 0.17f, 0.15f, 0.85f));
            UIStyle.Text(new Rect(cx - 140, 22, 60, 36), blue.ToString(), 24, Color.white, TextAnchor.MiddleCenter, true);
            int t = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            UIStyle.Text(new Rect(cx - 78, 22, 156, 36), $"{t / 60:00}:{t % 60:00}", 24, Color.white, TextAnchor.MiddleCenter, true);
            UIStyle.Text(new Rect(cx + 80, 22, 60, 36), red.ToString(), 24, Color.white, TextAnchor.MiddleCenter, true);
            UIStyle.Text(new Rect(cx - 140, 60, 280, 20), UIStyle.Spaced($"до {target} вбивств"), 12, UIStyle.TextDim, TextAnchor.MiddleCenter);
        }
    }
}
