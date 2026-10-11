using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>In-game HUD laid out like the concept: vitals bottom-left, ammo bottom-right, radar top-right.</summary>
    public static class Hud
    {
        struct FeedLine { public string Text; public Color Color; public float Time; }
        struct DamagePop { public Vector3 World; public float Amount; public HitZone Zone; public float Time; public bool Kill; public Vector2 Drift; }
        static readonly List<DamagePop> pops = new List<DamagePop>();
        static readonly Dictionary<Health, float> recentlyHit = new Dictionary<Health, float>();
        static Health target;
        static float targetTime = -9f;
        static bool hitWasHead;
        static Texture2D portrait;
        static bool portraitLoaded;
        struct HurtFrom { public Vector3 Pos; public float Time; }
        static readonly List<HurtFrom> hurtFrom = new List<HurtFrom>();
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
            if (h == p.Health)
            {
                damageFlash = Mathf.Min(1f, damageFlash + info.Amount / 30f + 0.2f);
                if (info.Attacker != null) hurtFrom.Add(new HurtFrom { Pos = info.Attacker.transform.position, Time = Time.unscaledTime });
                if (hurtFrom.Count > 6) hurtFrom.RemoveAt(0);
            }
            else if (info.Attacker == p.gameObject)
            {
                hitMarkerTime = Time.unscaledTime;
                hitWasKill = h.Current <= 0f;
                hitWasHead = info.Headshot;
                Vector3 at = info.Point.sqrMagnitude > 0.01f ? info.Point : h.transform.position + Vector3.up * 1.4f;
                pops.Add(new DamagePop { World = at, Amount = info.Amount, Zone = info.Zone, Time = Time.unscaledTime, Kill = hitWasKill,
                                         Drift = new Vector2(Random.Range(-30f, 30f), 0f) });
                if (pops.Count > 24) pops.RemoveAt(0);
                recentlyHit[h] = Time.unscaledTime;
                target = h;
                targetTime = Time.unscaledTime;
                Sfx.Play2D(hitWasKill ? "killmark" : info.Headshot ? "headshot" : "hitmark", hitWasKill ? 0.8f : 0.7f);
                if (info.Headshot) OnFeed(hitWasKill ? "Хедшот — вбивство!" : "У голову!", UIStyle.Gold);
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

            // Underwater
            if (p.HeadUnderwater)
            {
                UIStyle.Rect(new Rect(0, 0, W, H), new Color(0.04f, 0.2f, 0.27f, 0.45f));
                var c0 = GUI.color;
                GUI.color = new Color(0.01f, 0.06f, 0.09f, 0.7f);
                GUI.DrawTexture(new Rect(0, 0, W, H), UIStyle.Vignette);
                GUI.color = c0;
            }
            if (p.Oxygen < p.MaxOxygen - 0.01f)
            {
                UIStyle.Text(new Rect(W * 0.5f - 120, H - 150, 240, 20), UIStyle.Spaced("повітря"), 13, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleCenter);
                UIStyle.Bar(new Rect(W * 0.5f - 120, H - 128, 240, 6), p.Oxygen / p.MaxOxygen, p.Oxygen < 3f ? UIStyle.Bad : new Color(0.55f, 0.85f, 1f));
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

            DrawOverheadBars(p);
            DrawDamagePops(p);
            DrawTrajectory(p);
            DrawCrosshair(p, W, H);
            DrawTarget(p, W);
            DrawHurtDirections(p, W, H);
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
            if (w == null || w.IsMelee)
            {
                // Melee: a small dot with a faint ring.
                UIStyle.Rect(new Rect(cx - 2, cy - 2, 4, 4), c);
                var oc = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.25f);
                GUI.DrawTexture(new Rect(cx - 9, cy - 9, 18, 18), UIStyle.Ring);
                GUI.color = oc;
            }
            else
            {
                // Dynamic crosshair: the gap is the real accuracy cone projected onto the screen.
                float fov = p.Cam.fieldOfView * Mathf.Deg2Rad;
                float gap = Mathf.Tan(w.SpreadDegrees * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f) * (H * 0.5f);
                gap = Mathf.Clamp(gap, 3f, 90f);
                float len = 9f;
                Color lc = w.Charge >= 0.999f ? new Color(1f, 0.85f, 0.4f, 0.95f) : c;
                UIStyle.Rect(new Rect(cx - 1, cy - 1, 2, 2), lc);
                UIStyle.Rect(new Rect(cx - gap - len, cy - 1, len, 2), lc);
                UIStyle.Rect(new Rect(cx + gap, cy - 1, len, 2), lc);
                UIStyle.Rect(new Rect(cx - 1, cy + gap, 2, len), lc);
                UIStyle.Rect(new Rect(cx - 1, cy - gap - len, 2, len), lc);
            }
            if (w != null && w.Charge > 0.01f)
            {
                UIStyle.Bar(new Rect(cx - 40, cy + 34, 80, 4), w.Charge, w.Charge >= 1f ? UIStyle.Gold : Color.white);
            }

            float since = Time.unscaledTime - hitMarkerTime;
            float markDur = hitWasKill ? 0.45f : 0.25f;
            if (since < markDur)
            {
                Color hc = hitWasKill ? UIStyle.Bad : hitWasHead ? UIStyle.Gold : Color.white;
                hc.a = 1f - since / markDur;
                float d = hitWasKill ? 12f : 10f, l = hitWasKill || hitWasHead ? 12f : 8f;
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

        static Vector2? ToGui(PlayerController p, Vector3 world)
        {
            Vector3 sp = p.Cam.WorldToScreenPoint(world);
            if (sp.z <= 0.1f) return null;
            float s = UIStyle.Scale;
            return new Vector2(sp.x / s, (Screen.height - sp.y) / s);
        }

        /// <summary>Floating damage numbers: white body, gold head ("ГОЛОВА"), grey limbs, red on the kill.</summary>
        static void DrawDamagePops(PlayerController p)
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var d = pops[i];
                float age = Time.unscaledTime - d.Time;
                if (age > 1.1f) { pops.RemoveAt(i); continue; }
                var g = ToGui(p, d.World);
                if (!g.HasValue) continue;
                Vector2 pos = g.Value + d.Drift * age + new Vector2(0f, -70f * age);
                float a = Mathf.Clamp01(1.4f - age * 1.3f);
                bool head = d.Zone == HitZone.Head;
                bool limb = d.Zone == HitZone.Arm || d.Zone == HitZone.Leg;
                Color col = d.Kill ? new Color(1f, 0.35f, 0.3f, a) : head ? new Color(1f, 0.82f, 0.3f, a) : limb ? new Color(0.8f, 0.8f, 0.8f, a) : new Color(1f, 1f, 1f, a);
                int size = head || d.Kill ? 30 : limb ? 20 : 24;
                float pop = 1f + Mathf.Max(0f, 0.25f - age) * 2f;
                UIStyle.Shadowed(new Rect(pos.x - 80, pos.y - 20, 160, 36), Mathf.CeilToInt(d.Amount).ToString(), Mathf.RoundToInt(size * pop), col, TextAnchor.MiddleCenter, true);
                if (head || limb)
                    UIStyle.Shadowed(new Rect(pos.x - 80, pos.y + 12, 160, 20), UIStyle.Spaced(Hitbox.ZoneName(d.Zone)), 12, col, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>Small health bars over opponents you hit in the last few seconds.</summary>
        static void DrawOverheadBars(PlayerController p)
        {
            var dead = new List<Health>();
            foreach (var kv in recentlyHit)
            {
                var h = kv.Key;
                if (h == null || h.IsDead || Time.unscaledTime - kv.Value > 4f) { dead.Add(h); continue; }
                float dist = Vector3.Distance(h.transform.position, p.transform.position);
                if (dist > 70f) continue;
                var g = ToGui(p, h.transform.position + Vector3.up * 2.15f);
                if (!g.HasValue) continue;
                float a = Mathf.Clamp01(4f - (Time.unscaledTime - kv.Value));
                float w = Mathf.Lerp(70f, 40f, dist / 70f);
                Rect r = new Rect(g.Value.x - w * 0.5f, g.Value.y, w, 5);
                UIStyle.Rect(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(0f, 0f, 0f, 0.6f * a));
                UIStyle.Rect(new Rect(r.x, r.y, r.width * h.Fraction, r.height), new Color(0.9f, 0.2f, 0.15f, a));
            }
            foreach (var h in dead) recentlyHit.Remove(h);
        }

        /// <summary>Who you're aiming at / just hit: name and health, top centre.</summary>
        static void DrawTarget(PlayerController p, float W)
        {
            if (PhysicsUtil.Raycast(p.Cam.transform.position, p.Cam.transform.forward, 90f, p.transform, out var hit))
            {
                var h = hit.collider.GetComponentInParent<Health>();
                if (h != null && !h.IsDead && h != p.Health) { target = h; targetTime = Time.unscaledTime; }
            }
            if (target == null || Time.unscaledTime - targetTime > 3f) return;
            float a = Mathf.Clamp01(3f - (Time.unscaledTime - targetTime));
            var cr = target.GetComponent<Creature>();
            string name = cr != null ? (string.IsNullOrEmpty(cr.DisplayName) ? Creature.TypeName(cr.Type) : cr.DisplayName) : "Ціль";
            float cx = W * 0.5f, y = 96f;
            UIStyle.Rect(new Rect(cx - 160, y, 320, 46), new Color(0.05f, 0.05f, 0.06f, 0.55f * a));
            UIStyle.Shadowed(new Rect(cx - 150, y + 2, 300, 22), name.ToUpperInvariant(), 15, new Color(1f, 1f, 1f, a), TextAnchor.MiddleLeft, true);
            string hp = target.IsDead ? "0" : Mathf.CeilToInt(target.Current).ToString();
            UIStyle.Shadowed(new Rect(cx - 150, y + 2, 300, 22), hp, 15, new Color(1f, 0.85f, 0.8f, a), TextAnchor.MiddleRight, true);
            UIStyle.Rect(new Rect(cx - 150, y + 30, 300, 7), new Color(0f, 0f, 0f, 0.6f * a));
            UIStyle.Rect(new Rect(cx - 150, y + 30, 300 * (target.IsDead ? 0f : target.Fraction), 7), new Color(0.9f, 0.22f, 0.16f, a));
        }

        /// <summary>Throwables: dotted flight arc and a landing ring.</summary>
        static void DrawTrajectory(PlayerController p)
        {
            var w = p.Weapons.Current;
            if (w == null || !w.GetLaunch(out var pos, out var vel, out float g)) return;
            Vector3 prev = pos;
            for (int i = 1; i <= 60; i++)
            {
                float dt = 0.045f;
                vel += Vector3.down * g * dt;
                Vector3 next = prev + vel * dt;
                if (PhysicsUtil.Raycast(prev, next - prev, (next - prev).magnitude, p.transform, out var hit))
                {
                    var gp = ToGui(p, hit.point);
                    if (gp.HasValue)
                    {
                        var oc = GUI.color;
                        GUI.color = new Color(1f, 0.85f, 0.4f, 0.9f);
                        GUI.DrawTexture(new Rect(gp.Value.x - 10, gp.Value.y - 10, 20, 20), UIStyle.Ring);
                        GUI.color = oc;
                    }
                    break;
                }
                if (i % 2 == 0)
                {
                    var gp = ToGui(p, next);
                    if (gp.HasValue)
                    {
                        float s = Mathf.Lerp(5f, 2f, i / 60f);
                        UIStyle.Rect(new Rect(gp.Value.x - s * 0.5f, gp.Value.y - s * 0.5f, s, s), new Color(1f, 1f, 1f, Mathf.Lerp(0.8f, 0.25f, i / 60f)));
                    }
                }
                prev = next;
            }
        }

        /// <summary>Red markers around the crosshair pointing at whoever just hurt you.</summary>
        static void DrawHurtDirections(PlayerController p, float W, float H)
        {
            Vector2 c = new Vector2(W * 0.5f, H * 0.5f);
            var m = GUI.matrix;
            for (int i = hurtFrom.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - hurtFrom[i].Time;
                if (age > 1.6f) { hurtFrom.RemoveAt(i); continue; }
                Vector3 d = hurtFrom[i].Pos - p.transform.position;
                float ang = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - p.Yaw;
                GUIUtility.RotateAroundPivot(ang, c * UIStyle.Scale);
                UIStyle.Icon(new Rect(c.x - 22, c.y - 170, 44, 26), UIStyle.Triangle, new Color(0.9f, 0.1f, 0.08f, Mathf.Clamp01(1.6f - age) * 0.85f));
                GUI.matrix = m;
            }
        }

        static void DrawVitals(PlayerController p, float H)
        {
            // Portrait in a ring, like the concept HUD.
            if (!portraitLoaded) { portrait = Resources.Load<Texture2D>("UI/portrait"); portraitLoaded = true; }
            float px = 30, py = H - 150;
            if (portrait != null)
            {
                var oc = GUI.color;
                float hurt = Mathf.Clamp01(damageFlash);
                GUI.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f), hurt);
                GUI.DrawTexture(new Rect(px, py, 112, 112), portrait);
                GUI.color = oc;
            }
            float x = portrait != null ? 158 : 40, y = H - 110;
            // Health with a drawn plus sign
            UIStyle.Rect(new Rect(x + 6, y + 2, 4, 16), Color.white);
            UIStyle.Rect(new Rect(x, y + 8, 16, 4), Color.white);
            UIStyle.Shadowed(new Rect(x + 26, y - 6, 80, 30), Mathf.CeilToInt(p.Health.Current).ToString(), 26, Color.white, TextAnchor.MiddleLeft, true);
            UIStyle.Bar(new Rect(x + 90, y + 8, 180, 5), p.Health.Fraction, p.Health.Fraction < 0.3f ? UIStyle.Bad : Color.white);

            // Stamina with a lightning glyph
            float y2 = y + 36;
            UIStyle.Icon(new Rect(x - 1, y2 - 1, 18, 22), UIStyle.Bolt, Color.white);
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
            UIStyle.Icon(new Rect(c.x - 8, c.y - 9, 16, 16), UIStyle.Triangle, Color.white);

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
            UIStyle.Coins(new Rect(x, y, 180, 24), Profile.Current.Coins.ToString(), 20, Color.white, true, true);
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
