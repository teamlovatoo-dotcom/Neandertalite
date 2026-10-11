using System;
using UnityEngine;

namespace Neanderthal
{
    public enum Team { Player, Enemy, Wildlife, Neutral }

    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public GameObject Attacker;
        public bool Headshot;
        public HitZone Zone;
        public float Knockback;
        public string WeaponId;
    }

    /// <summary>Health + armor shared by the player, creatures and rival hunters.</summary>
    public class Health : MonoBehaviour
    {
        public float Max = 100f;
        public float Current = 100f;
        public float Armor;
        public float MaxArmor = 100f;
        public Team Team = Team.Neutral;
        public bool Invulnerable;

        public bool IsDead { get; private set; }
        public GameObject LastAttacker { get; private set; }

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;
        public event Action Healed;

        /// <summary>Raised for every damage dealt anywhere; the HUD uses it for hit markers.</summary>
        public static event Action<Health, DamageInfo> AnyDamaged;

        public float Fraction => Max <= 0 ? 0 : Current / Max;

        public void ResetHealth(float max)
        {
            Max = max;
            Current = max;
            IsDead = false;
        }

        public void Revive()
        {
            IsDead = false;
            Current = Max;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || Invulnerable || info.Amount <= 0f) return;

            // Friendly fire is off between members of the same team (except the neutral bucket).
            if (info.Attacker != null && Team != Team.Neutral)
            {
                var attackerHealth = info.Attacker.GetComponentInParent<Health>();
                if (attackerHealth != null && attackerHealth != this && attackerHealth.Team == Team && Team != Team.Player) return;
            }

            float dmg = info.Amount;
            // Half-Life style armor: absorbs 60% of damage, costing 1 armor point per absorbed point.
            if (Armor > 0f)
            {
                float absorbed = Mathf.Min(Armor, dmg * 0.6f);
                Armor -= absorbed;
                dmg -= absorbed;
            }

            Current = Mathf.Max(0f, Current - dmg);
            LastAttacker = info.Attacker;
            info.Amount = dmg;
            Damaged?.Invoke(info);
            AnyDamaged?.Invoke(this, info);

            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke(info);
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f || Current >= Max) return;
            Current = Mathf.Min(Max, Current + amount);
            Healed?.Invoke();
        }

        public void AddArmor(float amount)
        {
            Armor = Mathf.Min(MaxArmor, Armor + amount);
            Healed?.Invoke();
        }

        /// <summary>Resolve a collider hit into the Health it belongs to plus body-part multiplier.</summary>
        public static Health FromCollider(Collider c, out float multiplier, out bool head) => FromCollider(c, out multiplier, out head, out _);

        public static Health FromCollider(Collider c, out float multiplier, out bool head, out HitZone zone)
        {
            multiplier = 1f;
            head = false;
            zone = HitZone.Body;
            if (c == null) return null;
            var hb = c.GetComponent<Hitbox>();
            if (hb != null)
            {
                multiplier = hb.Multiplier;
                head = hb.IsHead;
                zone = head ? HitZone.Head : hb.Zone;
            }
            return c.GetComponentInParent<Health>();
        }

        /// <summary>Convenience: apply damage to whatever a raycast hit.</summary>
        public static bool Apply(RaycastHit hit, float amount, Vector3 dir, GameObject attacker, float knockback = 0f, string weaponId = null)
        {
            var h = FromCollider(hit.collider, out float mult, out bool head, out HitZone zone);
            if (h == null) return false;
            h.TakeDamage(new DamageInfo
            {
                Amount = amount * mult,
                Point = hit.point,
                Direction = dir,
                Attacker = attacker,
                Headshot = head,
                Zone = zone,
                Knockback = knockback,
                WeaponId = weaponId
            });
            return true;
        }
    }
}
