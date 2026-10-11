using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// F3 diagnostics panel. Shows the numbers the author can screenshot when something feels wrong: frame rate,
    /// lights, garbage collections, the input backend, what the crosshair really points at, and a log of recent hits
    /// (who hit whom, where, from how far, and whether a wall was in between). Off by default; costs nothing when off.
    /// </summary>
    public static class DebugOverlay
    {
        public static bool Visible;

        struct HitLine { public string Text; public Color Color; public float Time; }
        static readonly List<HitLine> hits = new List<HitLine>();
        static readonly float[] frameTimes = new float[120];
        static int frameIndex;
        static float sampleTimer;
        static int pointLights, spotLights, shadowLights, creatures;
        static int gcStart = -1;
        static float gcStartTime;
        static bool hooked;

        public static void Tick()
        {
            if (!hooked) { Health.AnyDamaged += OnDamaged; hooked = true; }
            if (GameInput.DebugDown) Visible = !Visible;
            if (!Visible) return;

            frameTimes[frameIndex] = Time.unscaledDeltaTime;
            frameIndex = (frameIndex + 1) % frameTimes.Length;
            if (gcStart < 0) { gcStart = System.GC.CollectionCount(0); gcStartTime = Time.unscaledTime; }

            sampleTimer -= Time.unscaledDeltaTime;
            if (sampleTimer <= 0f)
            {
                sampleTimer = 1f;
                pointLights = spotLights = shadowLights = 0;
                foreach (var l in Light.GetLights(LightType.Point, 0)) { if (!l.isActiveAndEnabled) continue; pointLights++; if (l.shadows != LightShadows.None) shadowLights++; }
                foreach (var l in Light.GetLights(LightType.Spot, 0)) { if (!l.isActiveAndEnabled) continue; spotLights++; if (l.shadows != LightShadows.None) shadowLights++; }
                creatures = 0;
                foreach (var c in Creature.All) if (c != null && !c.IsDead) creatures++;
            }
        }

        static void OnDamaged(Health h, DamageInfo info)
        {
            if (!Visible) return;
            var p = PlayerController.Instance;
            string victim = Name(h != null ? h.gameObject : null, p);
            string attacker = Name(info.Attacker, p);
            float dist = info.Attacker != null && h != null ? Vector3.Distance(info.Attacker.transform.position, h.transform.position) : 0f;
            bool wall = false;
            if (info.Attacker != null && h != null)
                wall = PhysicsUtil.LineBlocked(info.Attacker.transform.position + Vector3.up * 1.5f, h.transform.position + Vector3.up * 1.2f, info.Attacker.transform, h.transform);
            string zone = Hitbox.ZoneName(info.Zone);
            string text = $"{attacker} → {victim}: {info.Amount:0} ({zone}{(string.IsNullOrEmpty(info.WeaponId) ? "" : ", " + info.WeaponId)}) {dist:0.0} м{(wall ? "  ⚠ КРІЗЬ ПЕРЕШКОДУ" : "")}";
            Color c = wall ? UIStyle.Bad : h == (p != null ? p.Health : null) ? new Color(1f, 0.75f, 0.6f) : Color.white;
            hits.Add(new HitLine { Text = text, Color = c, Time = Time.unscaledTime });
            if (hits.Count > 10) hits.RemoveAt(0);
            Debug.Log("[F3] " + text);
        }

        static string Name(GameObject go, PlayerController p)
        {
            if (go == null) return "світ";
            if (p != null && go.transform.IsChildOf(p.transform)) return "Гравець";
            var cr = go.GetComponentInParent<Creature>();
            if (cr != null) return string.IsNullOrEmpty(cr.DisplayName) ? Creature.TypeName(cr.Type) : cr.DisplayName;
            return go.name;
        }

        public static void Draw()
        {
            if (!Visible) return;
            float avg = 0f, worst = 0f;
            int n = 0;
            foreach (var t in frameTimes) { if (t <= 0f) continue; avg += t; worst = Mathf.Max(worst, t); n++; }
            avg = n > 0 ? avg / n : 0f;
            int gcs = gcStart >= 0 ? System.GC.CollectionCount(0) - gcStart : 0;
            float minutes = Mathf.Max(0.05f, (Time.unscaledTime - gcStartTime) / 60f);

            var lines = new List<string>
            {
                $"FPS {(avg > 0 ? 1f / avg : 0):0}   кадр {avg * 1000f:0.0} мс   найгірший {worst * 1000f:0.0} мс",
                $"Лампи: точкові {pointLights}, прожектори {spotLights}, з тінями {shadowLights}   Боти/звірі: {creatures}",
                $"Збирання сміття (GC): {gcs} за {minutes:0.0} хв   Пам'ять C#: {System.GC.GetTotalMemory(false) / (1024f * 1024f):0} МБ",
                $"Ввід: {GameInput.Backend}   миша за кадр: {GameInput.Look.x:0.00}, {GameInput.Look.y:0.00}   чутливість {GameInput.MouseSensitivity:0.0}",
            };
            var p = PlayerController.Instance;
            if (p != null)
            {
                Vector3 pos = p.transform.position;
                lines.Add($"Позиція: {pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}   FOV {p.Cam.fieldOfView:0}   швидкість {new Vector2(p.Velocity.x, p.Velocity.z).magnitude:0.0} м/с");
                if (PhysicsUtil.Raycast(p.Cam.transform.position, p.Cam.transform.forward, 200f, p.transform, out var hit))
                {
                    var hb = hit.collider.GetComponent<Hitbox>();
                    string what = hb != null ? $"{Name(hit.collider.gameObject, p)} / {Hitbox.ZoneName(hb.Zone)}" : hit.collider.name;
                    var w = p.Weapons.Current;
                    string reach = w != null && w.IsMelee && w.Data.Range > 0f ? (hit.distance <= w.Data.Range ? "  [дістає]" : "  [задалеко]") : "";
                    lines.Add($"Приціл: {what}, {hit.distance:0.00} м{reach}");
                }
                else lines.Add("Приціл: небо / нічого");
            }

            float W = UIStyle.Width;
            float h = 24f * lines.Count + 24f * hits.Count + 24f;
            Rect r = new Rect(W * 0.5f - 470, 130, 940, h);
            UIStyle.Rect(r, new Color(0f, 0f, 0f, 0.62f));
            float y = r.y + 8;
            foreach (var l in lines) { UIStyle.Text(new Rect(r.x + 12, y, r.width - 24, 24), l, 16, Color.white); y += 24; }
            for (int i = hits.Count - 1; i >= 0; i--)
            {
                Color c = hits[i].Color;
                c.a = Mathf.Clamp01(12f - (Time.unscaledTime - hits[i].Time));
                UIStyle.Text(new Rect(r.x + 12, y, r.width - 24, 24), hits[i].Text, 15, c);
                y += 24;
            }
        }
    }
}
