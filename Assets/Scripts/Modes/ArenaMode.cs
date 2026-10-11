using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// PvP arena: 1v1 duel or free-for-all deathmatch, first to N kills or highest score when time runs out.
    /// Opponents are bots for now; the scoring/respawn rules are written per-combatant so a networked
    /// version can plug real players in place of <see cref="Creature"/> bots.
    /// </summary>
    public class ArenaMode : ModeBase
    {
        class Combatant
        {
            public string Name;
            public Color Color;
            public Health Health;
            public Creature Bot;      // null for the local player
            public int Kills;
            public int Deaths;
            public float RespawnAt = -1f;
        }

        static readonly string[] BotNames = { "Грок", "Угг", "Тхак", "Борр", "Мог" };
        static readonly Color[] BotColors =
        {
            new Color(0.72f, 0.18f, 0.16f), new Color(0.85f, 0.5f, 0.15f), new Color(0.55f, 0.3f, 0.7f),
            new Color(0.3f, 0.6f, 0.3f), new Color(0.85f, 0.75f, 0.2f)
        };

        ArenaSettings settings;
        ArenaLayout layout;
        PlayerController player;
        readonly List<Combatant> combatants = new List<Combatant>();
        Combatant me;
        float timeLeft = 300f;
        bool finished;
        string lastKiller;

        bool IsDuel => settings.Opponents == 1;

        public override string ObjectiveHeader => layout != null ? layout.Name : "";
        public override string ObjectiveText => "";

        public void Begin(ArenaSettings s)
        {
            settings = s;
            layout = ArenaLevels.Build(s.Map, transform);
            var spawn = layout.Spawns[0];
            player = PlayerController.Spawn(transform, spawn.Position, spawn.Yaw);
            me = new Combatant { Name = "Ти", Color = Mat.TeamBlue, Health = player.Health };
            combatants.Add(me);
            GiveLoadout();
            player.Health.Died += info => OnDied(me, info);

            for (int i = 0; i < s.Opponents; i++)
            {
                var sp = layout.Spawns[(i + 1) % layout.Spawns.Count];
                var bot = CreatureFactory.Spawn(CreatureType.Duelist, transform, sp.Position, sp.Yaw, BotColors[i % BotColors.Length]);
                bot.Health.Team = Team.Neutral; // free-for-all: everyone can hurt everyone
                bot.DisplayName = BotNames[i % BotNames.Length];
                bot.TargetSelector = PickTarget;
                switch (s.Difficulty)
                {
                    case 0: bot.Accuracy = 5f; bot.Health.ResetHealth(80f); bot.AttackCooldown = 2.2f; bot.RunSpeed = 4.8f; break;
                    case 2: bot.Accuracy = 1.2f; bot.Health.ResetHealth(120f); bot.AttackCooldown = 1.1f; bot.RunSpeed = 6.2f; break;
                    default: bot.Accuracy = 2.5f; bot.Health.ResetHealth(100f); bot.AttackCooldown = 1.5f; break;
                }
                var c = new Combatant { Name = bot.DisplayName, Color = BotColors[i % BotColors.Length], Health = bot.Health, Bot = bot };
                combatants.Add(c);
                bot.Health.Died += info => OnDied(c, info);
            }
            Inventory.Notify(IsDuel ? $"ДУЕЛЬ — до {s.ScoreToWin} вбивств" : $"УСІ ПРОТИ ВСІХ — до {s.ScoreToWin} вбивств", UIStyle.Gold);
        }

        void GiveLoadout()
        {
            var inv = player.Inventory;
            inv.Clear();
            player.Weapons.ClearLoaded();
            foreach (var id in new[] { settings.Melee, settings.Secondary, settings.Primary })
            {
                var d = ItemDatabase.Weapon(id);
                if (d == null) continue;
                inv.AddWeapon(id, false);
                if (d.Ammo != AmmoType.None)
                {
                    int want = d.Ammo == AmmoType.Arrow ? 20 : (d.MagSize > 0 ? d.MagSize * 3 : 10);
                    if (inv.GetAmmo(d.Ammo) < want) inv.AddAmmo(d.Ammo, want - inv.GetAmmo(d.Ammo), false);
                }
            }
            player.Health.Armor = 0f;
            player.Weapons.ForceEquip(settings.Primary);
        }

        /// <summary>Bots go after the nearest living opponent (player or other bots).</summary>
        Health PickTarget(Creature self)
        {
            Health best = null;
            float bestD = float.MaxValue;
            foreach (var c in combatants)
            {
                if (c.Health == null || c.Health == self.Health || c.Health.IsDead) continue;
                float d = (c.Health.transform.position - self.transform.position).sqrMagnitude;
                // Slight preference for the human so FFA doesn't ignore you.
                if (c == me) d *= 0.6f;
                if (d < bestD) { bestD = d; best = c.Health; }
            }
            return best;
        }

        Combatant FromAttacker(GameObject attacker)
        {
            if (attacker == null) return null;
            foreach (var c in combatants)
                if (c.Health != null && attacker.transform.IsChildOf(c.Health.transform)) return c;
            return null;
        }

        void OnDied(Combatant victim, DamageInfo info)
        {
            if (finished) return;
            victim.Deaths++;
            victim.RespawnAt = Time.time + 3f;
            var killer = FromAttacker(info.Attacker);
            if (killer != null && killer != victim)
            {
                killer.Kills++;
                if (killer == me) Profile.Current.Earn(10);
                string how = info.WeaponId != null && ItemDatabase.Weapon(info.WeaponId) != null ? ItemDatabase.Weapon(info.WeaponId).Name.ToLowerInvariant() : "";
                string head = info.Headshot ? "  ·  в голову" : "";
                Inventory.Notify($"{killer.Name}  »  {victim.Name}{(how.Length > 0 ? "  (" + how + ")" : "")}{head}", killer == me ? UIStyle.Gold : UIStyle.TextColor);
            }
            else
            {
                Inventory.Notify($"{victim.Name} загинув", UIStyle.TextDim);
            }
            if (victim == me)
            {
                lastKiller = killer != null && killer != me ? killer.Name : "падіння";
                deadTimer = 3f;
            }
            if (killer != null && killer.Kills >= settings.ScoreToWin) Finish();
        }

        SpawnPoint SafestSpawn()
        {
            SpawnPoint best = layout.Spawns[0];
            float bestScore = -1f;
            foreach (var sp in layout.Spawns)
            {
                float nearest = float.MaxValue;
                foreach (var c in combatants)
                    if (c.Health != null && !c.Health.IsDead)
                        nearest = Mathf.Min(nearest, Vector3.Distance(sp.Position, c.Health.transform.position));
                float score = nearest + Random.value * 5f;
                if (score > bestScore) { bestScore = score; best = sp; }
            }
            return best;
        }

        void Update()
        {
            if (finished || layout == null) return;
            timeLeft -= Time.deltaTime;
            if (deadTimer >= 0f) deadTimer -= Time.deltaTime;

            foreach (var c in combatants)
            {
                if (c.RespawnAt < 0f || Time.time < c.RespawnAt) continue;
                c.RespawnAt = -1f;
                var sp = SafestSpawn();
                if (c == me)
                {
                    deadTimer = -1f;
                    player.ResetForRespawn();
                    player.Teleport(sp.Position, sp.Yaw);
                    GiveLoadout();
                }
                else
                {
                    c.Bot.Revive(sp.Position, sp.Yaw);
                }
            }

            if (timeLeft <= 0f) Finish();
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            var sorted = new List<Combatant>(combatants);
            sorted.Sort((a, b) => b.Kills.CompareTo(a.Kills));
            bool draw = sorted.Count > 1 && sorted[0].Kills == sorted[1].Kills;
            bool won = !draw && sorted[0] == me;
            var p = Profile.Current;
            if (won) { p.DuelWins++; p.Earn(150); }
            else { if (!draw) p.DuelLosses++; p.Earn(40); }

            var body = new System.Text.StringBuilder();
            foreach (var c in sorted) body.AppendLine($"{c.Name}   вбивств: {c.Kills}   смертей: {c.Deaths}");
            body.AppendLine();
            body.Append(won ? "+150 монет" : "+40 монет");
            Sfx.Play2D(won ? "objective" : "deny", 0.8f);
            UIManager.Instance.ShowResult(draw ? "НІЧИЯ" : (won ? "ПЕРЕМОГА" : $"ПЕРЕМІГ {sorted[0].Name.ToUpperInvariant()}"), body.ToString());
        }

        public override bool TryGetRadarTarget(out Vector3 pos, out Color color)
        {
            pos = default;
            color = UIStyle.AccentRed;
            if (player == null) return false;
            float best = float.MaxValue;
            bool found = false;
            foreach (var c in combatants)
            {
                if (c == me || c.Health == null || c.Health.IsDead) continue;
                float d = (c.Health.transform.position - player.transform.position).sqrMagnitude;
                if (d < best) { best = d; pos = c.Health.transform.position; color = c.Color; found = true; }
            }
            return found;
        }

        public override void DrawHud()
        {
            if (IsDuel && combatants.Count > 1)
            {
                Hud.DrawDuelScore(me.Kills, combatants[1].Kills, timeLeft, settings.ScoreToWin);
            }
            else
            {
                float W = UIStyle.Width;
                int t = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
                UIStyle.Rect(new Rect(W * 0.5f - 80, 22, 160, 36), new Color(0.1f, 0.12f, 0.14f, 0.85f));
                UIStyle.Text(new Rect(W * 0.5f - 80, 22, 160, 36), $"{t / 60:00}:{t % 60:00}", 24, Color.white, TextAnchor.MiddleCenter, true);
                var sorted = new List<Combatant>(combatants);
                sorted.Sort((a, b) => b.Kills.CompareTo(a.Kills));
                float y = 66;
                foreach (var c in sorted)
                {
                    UIStyle.Rect(new Rect(W * 0.5f - 110, y, 6, 22), c.Color);
                    UIStyle.Rect(new Rect(W * 0.5f - 104, y, 214, 22), new Color(0, 0, 0, c == me ? 0.55f : 0.35f));
                    UIStyle.Text(new Rect(W * 0.5f - 96, y, 150, 22), c.Name, 15, c == me ? Color.white : UIStyle.TextColor, TextAnchor.MiddleLeft, c == me);
                    UIStyle.Text(new Rect(W * 0.5f + 40, y, 60, 22), c.Kills.ToString(), 15, Color.white, TextAnchor.MiddleRight, true);
                    y += 24;
                }
            }
            DrawDeathOverlay(lastKiller);
        }
    }
}
