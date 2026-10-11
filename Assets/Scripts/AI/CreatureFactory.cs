using UnityEngine;

namespace Neanderthal
{
    /// <summary>Spawns fully configured creatures. Tune stats here.</summary>
    public static class CreatureFactory
    {
        static int seedCounter = 17;

        public static Creature Spawn(CreatureType type, Transform parent, Vector3 pos, float yaw = 0f, Color? team = null)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var health = go.AddComponent<Health>();
            var c = go.AddComponent<Creature>();
            c.Type = type;
            c.Health = health;
            int seed = seedCounter++;

            switch (type)
            {
                case CreatureType.Wolf:
                    c.Body = CreatureBodies.Quadruped(go.transform, CreatureBodies.Beast.Wolf, seed);
                    Setup(c, Team.Wildlife, 55f, walk: 1.8f, run: 7.2f, dmg: 11f, range: 1.9f, cd: 1.0f, windup: 0.3f);
                    c.FearsFire = true;
                    c.CoinReward = 15;
                    c.Loot.Add((Resource.Hide, 1));
                    c.Loot.Add((Resource.Bone, 1));
                    break;
                case CreatureType.Deer:
                    c.Body = CreatureBodies.Quadruped(go.transform, CreatureBodies.Beast.Deer, seed);
                    Setup(c, Team.Wildlife, 40f, walk: 1.5f, run: 8.5f, dmg: 0f, range: 0f, cd: 1f, windup: 0.3f);
                    c.Aggressive = false;
                    c.Skittish = true;
                    c.CoinReward = 5;
                    c.Loot.Add((Resource.Hide, 2));
                    c.Loot.Add((Resource.Bone, 1));
                    break;
                case CreatureType.Bear:
                    c.Body = CreatureBodies.Quadruped(go.transform, CreatureBodies.Beast.Bear, seed);
                    Setup(c, Team.Wildlife, 220f, walk: 1.4f, run: 5.6f, dmg: 30f, range: 2.4f, cd: 1.6f, windup: 0.5f);
                    c.FearsFire = true;
                    c.Territorial = true;
                    c.Knockback = 7f;
                    c.CoinReward = 60;
                    c.Loot.Add((Resource.Hide, 3));
                    c.Loot.Add((Resource.Bone, 2));
                    break;
                case CreatureType.Mammoth:
                    c.Body = CreatureBodies.Quadruped(go.transform, CreatureBodies.Beast.Mammoth, seed);
                    Setup(c, Team.Wildlife, 650f, walk: 1.2f, run: 5f, dmg: 45f, range: 4.6f, cd: 2.4f, windup: 0.8f);
                    c.Territorial = true;
                    c.Knockback = 14f;
                    c.TurnSpeed = 90f;
                    c.CoinReward = 250;
                    c.Loot.Add((Resource.Hide, 5));
                    c.Loot.Add((Resource.Bone, 6));
                    break;
                case CreatureType.Hunter:
                    c.Body = CreatureBodies.Neanderthal(go.transform, Random.value < 0.5f ? "spear" : "club", team, seed);
                    Setup(c, Team.Enemy, 80f, walk: 1.6f, run: 5.4f, dmg: 16f, range: 2.4f, cd: 1.3f, windup: 0.45f);
                    c.CoinReward = 30;
                    c.Loot.Add((Resource.Wood, 1));
                    break;
                case CreatureType.Archer:
                    c.Body = CreatureBodies.Neanderthal(go.transform, "bow", team, seed);
                    Setup(c, Team.Enemy, 60f, walk: 1.6f, run: 4.8f, dmg: 14f, range: 2f, cd: 2.2f, windup: 0.45f);
                    c.Ranged = true;
                    c.ProjectileSpeed = 42f;
                    c.Accuracy = 3f;
                    c.SightRange = 40f;
                    c.CoinReward = 30;
                    c.Loot.Add((Resource.Stone, 1));
                    break;
                case CreatureType.Duelist:
                {
                    bool sling = Random.value < 0.35f;
                    c.RangedWeapon = sling ? "slingshot" : "bow";
                    c.Body = CreatureBodies.Neanderthal(go.transform, c.RangedWeapon, team ?? Mat.TeamRed, seed);
                    Setup(c, Team.Enemy, 100f, walk: 2.2f, run: 5.6f, dmg: sling ? 17f : 22f, range: 2.4f, cd: sling ? 1.1f : 1.5f, windup: 0.4f);
                    c.Ranged = true;
                    c.ProjectileSpeed = sling ? 44f : 55f;
                    c.Accuracy = 2.2f;
                    c.SightRange = 80f;
                    c.LeashRadius = 999f;
                    c.CoinReward = 0;
                    c.Respawns = true;
                    break;
                }
            }

            health.ResetHealth(health.Max);
            c.Init();
            return c;
        }

        static void Setup(Creature c, Team team, float hp, float walk, float run, float dmg, float range, float cd, float windup)
        {
            c.Health.Team = team;
            c.Health.Max = hp;
            c.WalkSpeed = walk;
            c.RunSpeed = run;
            c.AttackDamage = dmg;
            c.AttackRange = range;
            c.AttackCooldown = cd;
            c.AttackWindup = windup;
        }
    }
}
