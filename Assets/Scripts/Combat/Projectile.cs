using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Ballistic projectile (arrows, bolts, thrown spears) simulated with segment raycasts so fast
    /// projectiles never tunnel. Sticks into what it hits; arrows and spears in the world can be picked back up.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public float Damage;
        public float Gravity = 9.81f;
        public GameObject Owner;
        public string RecoverWeaponId;            // thrown spear -> becomes a spear pickup
        public AmmoType RecoverAmmo = AmmoType.None;
        public float Knockback = 2f;
        /// <summary>Stones: tumble in flight, bounce off instead of sticking, and always land on the ground.</summary>
        public bool Blunt;

        Vector3 velocity;
        bool stuck;
        float life;

        public static Projectile Launch(GameObject visual, Vector3 pos, Vector3 velocity, float damage, GameObject owner, float gravity = 9.81f)
        {
            var p = visual.AddComponent<Projectile>();
            visual.transform.SetParent(null, true);
            visual.transform.position = pos;
            visual.transform.rotation = Quaternion.LookRotation(velocity);
            Shapes.SetShadows(visual, true);
            p.velocity = velocity;
            p.Damage = damage;
            p.Owner = owner;
            p.Gravity = gravity;
            return p;
        }

        void Update()
        {
            life += Time.deltaTime;
            if (stuck)
            {
                if (life > 40f) Destroy(gameObject);
                return;
            }
            if (life > 8f)
            {
                Destroy(gameObject);
                return;
            }

            float dt = Time.deltaTime;
            Vector3 start = transform.position;
            velocity += Vector3.down * Gravity * dt;
            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (dist < 0.0001f) return;

            if (PhysicsUtil.Raycast(start, step / dist, dist, Owner != null ? Owner.transform : null, out var hit))
            {
                OnImpact(hit);
                return;
            }
            transform.position = start + step;
            if (Blunt) transform.Rotate(new Vector3(720f, 300f, 0f) * dt, Space.Self);
            else transform.rotation = Quaternion.LookRotation(velocity);
        }

        void OnImpact(RaycastHit hit)
        {
            stuck = true;
            life = 0f;
            Vector3 dir = velocity.normalized;
            float speedFactor = Mathf.Clamp(velocity.magnitude / 40f, 0.5f, 1.2f);
            bool hitCreature = Health.Apply(hit, Damage * speedFactor, dir, Owner, Knockback);

            if (hitCreature)
            {
                Sfx.Play("hit", hit.point, 0.9f);
                Effects.Blood(hit.point, -dir);
            }
            else
            {
                Sfx.Play("thunk", hit.point, 0.8f);
                Effects.Dust(hit.point, hit.normal);
                var harvest = hit.collider.GetComponentInParent<Harvestable>();
                if (harvest != null && RecoverWeaponId != null) harvest.Hit(0.3f, hit.point, Owner);
            }

            if (Blunt)
            {
                // Stones drop where they hit and can be picked up again.
                Vector3 rest = hit.point + hit.normal * 0.04f;
                if (PhysicsUtil.RaycastStatic(hit.point + Vector3.up * 0.5f - dir * 0.3f, Vector3.down, 8f, out var floor)) rest = floor.point + Vector3.up * 0.035f;
                transform.position = rest;
                if (RecoverAmmo != AmmoType.None) Pickup.MakeRecoverable(gameObject, null, RecoverAmmo, 1);
                else Destroy(gameObject, 10f);
                return;
            }

            // Embed slightly. Creatures carry the arrow along (parented to their unscaled root).
            transform.position = hit.point - dir * 0.05f;
            if (hitCreature)
            {
                var victim = hit.collider.GetComponentInParent<Health>();
                if (victim != null) transform.SetParent(victim.transform, true);
            }

            if (!hitCreature)
            {
                if (RecoverWeaponId != null) Pickup.MakeRecoverable(gameObject, RecoverWeaponId, AmmoType.None, 0);
                else if (RecoverAmmo != AmmoType.None && Random.value < 0.75f) Pickup.MakeRecoverable(gameObject, null, RecoverAmmo, 1);
            }
            else if (RecoverWeaponId != null)
            {
                // A spear that hits a creature drops at its feet instead of vanishing.
                transform.SetParent(null, true);
                Pickup.MakeRecoverable(gameObject, RecoverWeaponId, AmmoType.None, 0);
                if (PhysicsUtil.Raycast(hit.point + Vector3.up, Vector3.down, 6f, hit.collider.transform.root, out var ground))
                {
                    transform.position = ground.point + Vector3.up * 0.1f;
                    transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(dir, Vector3.up).normalized + Vector3.down * 0.1f);
                }
            }
        }
    }
}
