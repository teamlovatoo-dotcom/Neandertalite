using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    public enum CreatureType { Wolf, Deer, Bear, Mammoth, Hunter, Archer, Duelist }

    /// <summary>
    /// Lightweight creature AI: perception, wander, chase, melee / ranged attacks, flee, pack alerts.
    /// Movement is kinematic (ground snapping + obstacle probing) so no NavMesh bake is required.
    /// </summary>
    public class Creature : MonoBehaviour
    {
        enum State { Idle, Wander, Chase, Attack, Ranged, Flee, Wary, Dead }

        public static readonly List<Creature> All = new List<Creature>();

        public CreatureType Type;
        public Health Health;
        [System.NonSerialized] public CreatureBody Body;
        public float WalkSpeed = 1.6f;
        public float RunSpeed = 5f;
        public float TurnSpeed = 360f;
        public float SightRange = 30f;
        public float AttackRange = 2f;
        public float AttackDamage = 15f;
        public float AttackCooldown = 1.2f;
        public float AttackWindup = 0.35f;
        public float Knockback = 3f;
        public bool Aggressive = true;
        public bool Territorial;         // only attacks when you come close or hurt it
        public bool Skittish;            // flees (deer)
        public bool FearsFire;
        public bool Ranged;
        public float ProjectileSpeed = 45f;
        public float Accuracy = 2.5f;    // spread in degrees
        public float LeashRadius = 60f;
        public int CoinReward = 10;
        [System.NonSerialized] public List<(Resource, int)> Loot = new List<(Resource, int)>();
        public string RangedWeapon = "bow";
        public string MeleeWeapon = "spear";
        public bool Respawns;            // duel bot: handled externally

        public Health Target;
        public string DisplayName = "";
        /// <summary>Optional custom targeting (arena bots pick the nearest opponent, players included).</summary>
        public System.Func<Creature, Health> TargetSelector;
        public Vector3 Home;
        public System.Action<Creature> OnDeathCallback;

        State state = State.Idle;
        float stateTimer;
        float attackTimer;
        float windup = -1f;
        bool rangedWindup;
        Vector3 wanderTarget;
        Vector3 lastKnownPos;
        float lastSeenTime = -99f;
        float lastHurtTime = -99f;
        float gaitPhase;
        float currentSpeed;
        float verticalVel;
        float deathTime;
        float strafeDir = 1f;
        float strafeTimer;
        float soundTimer;
        Vector3 knock;
        float retarget;

        public bool IsDead => state == State.Dead;

        public static string TypeName(CreatureType t)
        {
            switch (t)
            {
                case CreatureType.Wolf: return "Вовк";
                case CreatureType.Deer: return "Олень";
                case CreatureType.Bear: return "Ведмідь";
                case CreatureType.Mammoth: return "Мамонт";
                case CreatureType.Hunter: return "Мисливець";
                case CreatureType.Archer: return "Лучник";
                default: return "Суперник";
            }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Init()
        {
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
            Home = transform.position;
            wanderTarget = Home;
            soundTimer = Random.Range(3f, 10f);
            attackTimer = Random.Range(0.5f, 1.5f);
        }

        void OnDamaged(DamageInfo info)
        {
            lastHurtTime = Time.time;
            Effects.Blood(info.Point == Vector3.zero ? transform.position + Vector3.up : info.Point, -info.Direction);
            Vector3 k = info.Direction; k.y = 0f;
            knock += k.normalized * info.Knockback * (Type == CreatureType.Mammoth ? 0.1f : 1f);
            if (Random.value < 0.5f) Sfx.Play(Voice(), transform.position, 0.6f, PitchForType());

            if (info.Attacker != null)
            {
                var h = info.Attacker.GetComponentInParent<Health>();
                if (h != null && h != Health) { Target = h; lastKnownPos = h.transform.position; lastSeenTime = Time.time; }
            }
            if (Skittish) { SetState(State.Flee); return; }
            if (state == State.Idle || state == State.Wander || state == State.Wary) SetState(State.Chase);
            AlertPack();
        }

        void AlertPack()
        {
            foreach (var c in All)
            {
                if (c == this || c.IsDead || c.Type != Type) continue;
                if ((c.transform.position - transform.position).sqrMagnitude > 20f * 20f) continue;
                if (c.Target == null) c.Target = Target;
                c.lastKnownPos = lastKnownPos;
                c.lastSeenTime = Time.time;
                if (c.Skittish) c.SetState(State.Flee);
                else if (c.state != State.Chase && c.state != State.Attack && c.state != State.Ranged) c.SetState(State.Chase);
            }
        }

        void OnDied(DamageInfo info)
        {
            SetState(State.Dead);
            deathTime = Time.time;
            Sfx.Play("death", transform.position, 0.9f, PitchForType());
            if (Body.Bow != null) Body.Bow.Draw = 0f;

            // Loot drops in a little ring.
            var parent = transform.parent;
            int i = 0;
            bool skinned = info.WeaponId == "knife" || info.WeaponId == "bone_dagger";
            foreach (var (res, amount) in Loot)
            {
                int amt = amount + (skinned && res == Resource.Hide ? 1 : 0);
                Vector3 p = transform.position + Quaternion.Euler(0, i * 72f, 0) * Vector3.forward * (Body.Radius + 0.4f) + Vector3.up * 0.2f;
                Pickup.Res(parent, p, res, amt);
                i++;
            }
            if (CoinReward > 0)
                Pickup.Coins(parent, transform.position + Vector3.up * 0.3f, CoinReward);
            if (Type == CreatureType.Archer && Random.value < 0.6f)
                Pickup.AmmoBox(parent, transform.position + transform.right * 0.8f + Vector3.up * 0.2f, AmmoType.Arrow, Random.Range(2, 5));
            if (Type == CreatureType.Hunter && Random.value < 0.3f)
                Pickup.Weapon(parent, transform.position - transform.right * 0.8f + Vector3.up * 0.2f, "spear");

            GameEvents.RaiseKill(this, info);
            OnDeathCallback?.Invoke(this);
        }

        string Voice()
        {
            switch (Type)
            {
                case CreatureType.Mammoth: return "trumpet";
                case CreatureType.Bear: return "roar";
                case CreatureType.Wolf: return "growl";
                case CreatureType.Deer: return "hurt";
                default: return "hurt";
            }
        }

        float PitchForType()
        {
            switch (Type)
            {
                case CreatureType.Wolf: return 1.4f;
                case CreatureType.Deer: return 1.8f;
                case CreatureType.Mammoth: return 0.8f;
                case CreatureType.Bear: return 0.9f;
                default: return 0.75f;
            }
        }

        void SetState(State s)
        {
            state = s;
            stateTimer = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            stateTimer += dt;

            if (state == State.Dead)
            {
                DeathAnim(dt);
                return;
            }

            retarget -= dt;
            if (Target == null || Target.IsDead || (TargetSelector != null && retarget <= 0f && windup < 0f))
            {
                retarget = 1.5f;
                AcquireTarget();
            }
            Perceive();
            Think(dt);
            ApplyKnockback(dt);
            Animate(dt);

            soundTimer -= dt;
            if (soundTimer <= 0f)
            {
                soundTimer = Random.Range(6f, 14f);
                if (state == State.Chase || (Type == CreatureType.Mammoth && Random.value < 0.3f))
                    Sfx.Play(Voice(), transform.position, 0.5f, PitchForType());
            }
        }

        void AcquireTarget()
        {
            Target = null;
            if (TargetSelector != null)
            {
                Target = TargetSelector(this);
                return;
            }
            var p = PlayerController.Instance;
            if (p != null && !p.Health.IsDead) Target = p.Health;
        }

        bool CanSee(out float dist)
        {
            dist = float.MaxValue;
            if (Target == null || Target.IsDead) return false;
            Vector3 eye = transform.position + Vector3.up * Body.EyeHeight;
            Vector3 tp = Target.transform.position + Vector3.up * 1.3f;
            Vector3 d = tp - eye;
            dist = d.magnitude;
            if (dist > SightRange) return false;
            // Forward-ish field of view unless very close.
            if (dist > 6f && Vector3.Angle(transform.forward, d) > 110f) return false;
            if (SightBlocker.Blocks(eye, tp)) return false;
            if (PhysicsUtil.Raycast(eye, d / dist, dist, transform, out var hit))
                return hit.transform.IsChildOf(Target.transform);
            return true;
        }

        void Perceive()
        {
            if (CanSee(out float dist))
            {
                lastSeenTime = Time.time;
                lastKnownPos = Target.transform.position;
                if (state == State.Idle || state == State.Wander)
                {
                    if (Skittish && dist < 18f) SetState(State.Flee);
                    else if (Aggressive && (!Territorial || dist < 12f)) SetState(State.Chase);
                }
            }
            else if (Target != null && !Target.IsDead)
            {
                // Hear footsteps of a running player nearby.
                float d = Vector3.Distance(Target.transform.position, transform.position);
                var pc = PlayerController.Instance;
                bool loud = pc != null && pc.Health == Target && pc.IsSprinting;
                if (d < (loud ? 14f : 5f) && (state == State.Idle || state == State.Wander))
                {
                    lastKnownPos = Target.transform.position;
                    lastSeenTime = Time.time;
                    if (Skittish) SetState(State.Flee);
                    else if (Aggressive && !Territorial) SetState(State.Chase);
                }
            }
        }

        bool TargetHasTorch()
        {
            var pc = PlayerController.Instance;
            return pc != null && Target == pc.Health && pc.Weapons.CurrentId == "torch";
        }

        void Think(float dt)
        {
            attackTimer -= dt;
            float dist = Target != null ? HorizontalDistance(Target.transform.position) : 999f;
            bool seen = Time.time - lastSeenTime < 0.3f;

            if (windup >= 0f)
            {
                UpdateWindup(dt, dist);
                return;
            }

            switch (state)
            {
                case State.Idle:
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, dt * 6f);
                    if (stateTimer > Random.Range(2f, 6f)) PickWander();
                    break;

                case State.Wander:
                    if (MoveTo(wanderTarget, WalkSpeed, dt) < 1.2f || stateTimer > 12f) SetState(State.Idle);
                    break;

                case State.Flee:
                {
                    Vector3 away = transform.position - (Target != null ? Target.transform.position : transform.position - transform.forward);
                    away.y = 0f;
                    MoveTo(transform.position + away.normalized * 10f, RunSpeed, dt);
                    if (stateTimer > 6f && dist > 25f) SetState(State.Idle);
                    break;
                }

                case State.Wary:
                {
                    // Circle at a distance from the torch, snarling.
                    FaceTowards(Target.transform.position, dt);
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, dt * 6f);
                    if (dist < 4.5f) MoveTo(transform.position + (transform.position - Target.transform.position).normalized * 3f, WalkSpeed * 1.5f, dt, false);
                    if (!TargetHasTorch() || Time.time - lastHurtTime < 5f) SetState(State.Chase);
                    if (dist > SightRange) SetState(State.Idle);
                    break;
                }

                case State.Chase:
                {
                    if (Target == null) { SetState(State.Idle); break; }
                    if (FearsFire && TargetHasTorch() && Time.time - lastHurtTime > 5f && dist < 9f)
                    {
                        SetState(State.Wary);
                        Sfx.Play("growl", transform.position, 0.7f, PitchForType());
                        break;
                    }
                    if (Time.time - lastSeenTime > 8f || HorizontalDistance(Home) > LeashRadius)
                    {
                        Target = null;
                        wanderTarget = Home;
                        SetState(State.Wander);
                        break;
                    }
                    if (Ranged && seen && dist > 6f && dist < SightRange * 0.9f)
                    {
                        SetState(State.Ranged);
                        break;
                    }
                    Vector3 goal = seen ? Target.transform.position : lastKnownPos;
                    MoveTo(goal, RunSpeed, dt);
                    if (dist < AttackRange && attackTimer <= 0f && seen) StartMelee();
                    break;
                }

                case State.Ranged:
                {
                    if (Target == null || !seen && stateTimer > 1.5f || dist < 5f) { SetState(State.Chase); break; }
                    FaceTowards(Target.transform.position, dt);
                    // Duelists strafe while shooting; archers hold position.
                    strafeTimer -= dt;
                    if (strafeTimer <= 0f) { strafeTimer = Random.Range(0.8f, 2f); strafeDir = Random.value < 0.5f ? -1f : 1f; }
                    if (Type == CreatureType.Duelist)
                    {
                        Vector3 side = transform.right * strafeDir;
                        Vector3 approach = dist > 20f ? transform.forward : (dist < 9f ? -transform.forward : Vector3.zero);
                        MoveTo(transform.position + (side + approach * 0.7f) * 3f, WalkSpeed * 1.8f, dt, false);
                    }
                    else currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, dt * 6f);
                    if (attackTimer <= 0f && seen) StartRanged();
                    break;
                }
            }
        }

        void PickWander()
        {
            Vector2 r = Random.insideUnitCircle * 12f;
            wanderTarget = Home + new Vector3(r.x, 0f, r.y);
            SetState(State.Wander);
        }

        void StartMelee()
        {
            windup = 0f;
            rangedWindup = false;
            if (Type == CreatureType.Wolf || Type == CreatureType.Bear) Sfx.Play("growl", transform.position, 0.6f, PitchForType());
            if (Type == CreatureType.Mammoth) Sfx.Play("trumpet", transform.position, 0.9f);
        }

        void StartRanged()
        {
            windup = 0f;
            rangedWindup = true;
            Sfx.Play("draw", transform.position, 0.5f);
        }

        void UpdateWindup(float dt, float dist)
        {
            windup += dt;
            if (Target != null) FaceTowards(Target.transform.position, dt);
            float total = rangedWindup ? 1.0f : AttackWindup;
            if (Body.Bow != null && rangedWindup) Body.Bow.Draw = Mathf.Clamp01(windup / total);
            if (!rangedWindup && Type == CreatureType.Mammoth && Target != null)
                MoveTo(Target.transform.position, RunSpeed * 1.3f, dt, true); // charge

            if (windup < total) return;
            windup = -1f;
            attackTimer = AttackCooldown * Random.Range(0.85f, 1.25f);
            if (Body.Bow != null) Body.Bow.Draw = 0f;

            if (Target == null || Target.IsDead) return;
            if (rangedWindup) FireProjectile();
            else if (dist < AttackRange * 1.35f)
            {
                Vector3 dir = (Target.transform.position - transform.position).normalized;
                Target.TakeDamage(new DamageInfo
                {
                    Amount = AttackDamage,
                    Point = Target.transform.position + Vector3.up,
                    Direction = dir,
                    Attacker = gameObject,
                    Knockback = Knockback
                });
                Sfx.Play("hit", Target.transform.position + Vector3.up, 0.8f, 0.8f);
            }
            else Sfx.Play("swing", transform.position + Vector3.up, 0.5f);
        }

        void FireProjectile()
        {
            Vector3 from = transform.position + Vector3.up * (Body.EyeHeight - 0.1f) + transform.forward * 0.6f;
            Vector3 tp = Target.transform.position + Vector3.up * 1.1f;
            // Lead the target and compensate for drop.
            var pc = Target.GetComponent<PlayerController>();
            Vector3 tv = pc != null ? pc.Velocity : Vector3.zero;
            float t = Vector3.Distance(from, tp) / ProjectileSpeed;
            Vector3 aimPoint = tp + tv * t * 0.8f + Vector3.up * (0.5f * 9.81f * t * t);
            Vector3 dir = (aimPoint - from).normalized;
            Vector2 r = Random.insideUnitCircle * Mathf.Tan(Accuracy * Mathf.Deg2Rad);
            dir = (dir + transform.right * r.x + Vector3.up * r.y).normalized;
            bool sling = RangedWeapon == "slingshot";
            var visual = sling ? WeaponModels.Stone(null) : WeaponModels.Arrow(null, false);
            var p = Projectile.Launch(visual, from, dir * ProjectileSpeed, AttackDamage, gameObject);
            p.WeaponId = RangedWeapon;
            p.RecoverAmmo = sling ? AmmoType.Stone : AmmoType.Arrow;
            p.Blunt = sling;
            Sfx.Play("bow", from, 0.8f, sling ? 1.6f : 1f);
        }

        float HorizontalDistance(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void FaceTowards(Vector3 p, float dt)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return;
            var target = Quaternion.LookRotation(d);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, TurnSpeed * dt);
        }

        readonly UnityEngine.AI.NavMeshPath navPath = new UnityEngine.AI.NavMeshPath();
        Vector3[] navCorners;
        int navCorner;
        float navRepath;
        Vector3 navDest;

        /// <summary>With a baked NavMesh, the next corner of the path to dest (tunnels, mazes); otherwise dest itself.</summary>
        Vector3 NavSteer(Vector3 dest, float dt)
        {
            if (!NavBaker.Ready) return dest;
            navRepath -= dt;
            if (navRepath <= 0f || (dest - navDest).sqrMagnitude > 4f)
            {
                navRepath = 0.6f + Random.value * 0.3f;
                navDest = dest;
                navCorners = NavBaker.Path(transform.position, dest, navPath) ? navPath.corners : null;
                navCorner = 1;
            }
            if (navCorners == null) return dest;
            while (navCorner < navCorners.Length - 1)
            {
                Vector3 c = navCorners[navCorner] - transform.position;
                c.y = 0f;
                if (c.sqrMagnitude > 0.8f * 0.8f) break;
                navCorner++;
            }
            return navCorner < navCorners.Length ? navCorners[navCorner] : dest;
        }

        /// <summary>Steer towards a point with simple obstacle avoidance. Returns remaining distance.</summary>
        float MoveTo(Vector3 dest, float speed, float dt, bool face = true)
        {
            Vector3 to = dest - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.3f)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, dt * 8f);
                return dist;
            }
            if (dist > 3f)
            {
                Vector3 steer = NavSteer(dest, dt) - transform.position;
                steer.y = 0f;
                if (steer.sqrMagnitude > 0.01f) to = steer;
            }
            Vector3 dir = to.normalized;
            Vector3 chosen = Vector3.zero;
            float[] angles = { 0f, 30f, -30f, 60f, -60f, 95f, -95f };
            foreach (float a in angles)
            {
                Vector3 cand = Quaternion.Euler(0f, a, 0f) * dir;
                if (Clear(cand, Mathf.Max(1.2f, Body.Radius * 2.5f)))
                {
                    chosen = cand;
                    break;
                }
            }
            if (chosen == Vector3.zero)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, dt * 8f);
                if (face) FaceTowards(transform.position + dir, dt);
                return dist;
            }

            // Separation from other creatures
            foreach (var c in All)
            {
                if (c == this || c.IsDead) continue;
                Vector3 d = transform.position - c.transform.position;
                d.y = 0f;
                float min = Body.Radius + c.Body.Radius + 0.2f;
                if (d.sqrMagnitude < min * min && d.sqrMagnitude > 0.0001f)
                    chosen += d.normalized * 0.6f;
            }
            chosen.y = 0f;
            chosen.Normalize();

            currentSpeed = Mathf.MoveTowards(currentSpeed, speed, dt * speed * 3f);
            if (face) FaceTowards(transform.position + chosen, dt);
            float step = Mathf.Min(currentSpeed * dt, dist);
            Translate(chosen * step);
            return dist;
        }

        bool Clear(Vector3 dir, float lookAhead)
        {
            Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.6f, Body.Height * 0.45f);
            float radius = Mathf.Min(Body.Radius * 0.8f, 0.8f);
            if (PhysicsUtil.SphereCast(origin, radius, dir, lookAhead, transform, out var hit))
            {
                // The target itself never blocks; gentle slopes are fine.
                if (Target != null && hit.transform.IsChildOf(Target.transform)) return true;
                if (hit.normal.y > 0.6f) return true;
                return false;
            }
            // Don't walk off cliffs.
            Vector3 ahead = transform.position + dir * lookAhead;
            if (PhysicsUtil.RaycastStatic(ahead + Vector3.up * 2f, Vector3.down, 8f, out var ground))
                return ground.point.y > transform.position.y - 3f;
            return false;
        }

        void Translate(Vector3 delta)
        {
            Vector3 next = transform.position + delta;
            if (PhysicsUtil.RaycastStatic(next + Vector3.up * 1.5f, Vector3.down, 6f, out var hit) && hit.normal.y > 0.45f)
            {
                if (hit.point.y - transform.position.y > 0.9f) return; // too steep to climb
                next.y = hit.point.y;
                verticalVel = 0f;
            }
            transform.position = next;
        }

        void ApplyKnockback(float dt)
        {
            if (knock.sqrMagnitude > 0.01f)
            {
                Translate(knock * dt);
                knock = Vector3.MoveTowards(knock, Vector3.zero, dt * 15f);
            }
            // Gravity when nothing below.
            if (!PhysicsUtil.RaycastStatic(transform.position + Vector3.up * 0.3f, Vector3.down, 0.6f, out _))
            {
                verticalVel -= 18f * dt;
                transform.position += Vector3.up * verticalVel * dt;
                if (transform.position.y < -50f) Health.TakeDamage(new DamageInfo { Amount = 9999f });
            }
        }

        void Animate(float dt)
        {
            float speedNorm = Mathf.Clamp01(currentSpeed / Mathf.Max(0.1f, RunSpeed));
            gaitPhase += dt * (3f + currentSpeed * (Body.Biped ? 2.2f : 2.6f)) * (Type == CreatureType.Mammoth ? 0.5f : 1f);
            float amp = Body.StrideAmp * Mathf.Clamp01(currentSpeed / 1.5f);
            for (int i = 0; i < Body.Legs.Count; i++)
                Body.Pose(Body.Legs[i], new Vector3(Mathf.Sin(gaitPhase + Body.LegPhase[i]) * amp, 0f, 0f));

            float attackPose = 0f;
            if (windup >= 0f)
            {
                float total = rangedWindup ? 1.0f : AttackWindup;
                attackPose = Mathf.Clamp01(windup / total);
            }

            if (Body.Biped)
            {
                if (rangedWindup && windup >= 0f || state == State.Ranged)
                {
                    // Bow raised: left arm forward, right arm draws.
                    Body.PoseSmooth(Body.ArmL, new Vector3(-85f, 0f, 0f), dt * 10f);
                    Body.PoseSmooth(Body.ArmR, new Vector3(-85f + attackPose * 10f, -35f * attackPose, 0f), dt * 10f);
                }
                else if (windup >= 0f)
                {
                    // Raise then stab.
                    float a = attackPose < 0.7f ? Mathf.Lerp(0f, -150f, attackPose / 0.7f) : Mathf.Lerp(-150f, -60f, (attackPose - 0.7f) / 0.3f);
                    Body.Pose(Body.ArmR, new Vector3(a, 0f, 0f));
                }
                else
                {
                    float swing = Mathf.Sin(gaitPhase) * amp * 0.6f;
                    Body.PoseSmooth(Body.ArmL, new Vector3(swing, 0f, -5f), dt * 8f);
                    Body.PoseSmooth(Body.ArmR, new Vector3(-swing - 20f, 0f, 5f), dt * 8f);
                }
                Body.Root.localRotation = Quaternion.Euler(speedNorm * 8f, 0f, 0f);
            }
            else
            {
                // Head bob / lunge
                float lunge = windup >= 0f ? Mathf.Sin(attackPose * Mathf.PI) : 0f;
                float headPitch = Type == CreatureType.Mammoth ? -lunge * 25f : lunge * 25f;
                Body.Pose(Body.Neck, new Vector3(headPitch + Mathf.Sin(gaitPhase * 2f) * 3f * speedNorm, 0f, 0f));
                Body.Root.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(gaitPhase)) * 0.04f * speedNorm, lunge * Body.Radius * 0.5f);
            }
        }

        void DeathAnim(float dt)
        {
            float t = Time.time - deathTime;
            // Topple sideways, then sink into the ground.
            Body.Root.localRotation = Quaternion.Slerp(Body.Root.localRotation, Quaternion.Euler(0f, 0f, 88f), dt * 5f);
            Body.Root.localPosition = Vector3.Lerp(Body.Root.localPosition, new Vector3(0f, Body.Radius * 0.6f, 0f), dt * 5f);
            if (t > 12f && !Respawns)
            {
                Body.Root.localPosition += Vector3.down * dt * 0.5f;
                if (t > 16f) Destroy(gameObject);
            }
        }

        /// <summary>Used by duels to bring the bot back.</summary>
        public void Revive(Vector3 pos, float yaw)
        {
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Body.Root.localRotation = Quaternion.identity;
            Body.Root.localPosition = Vector3.zero;
            Health.Revive();
            knock = Vector3.zero;
            windup = -1f;
            Home = pos;
            SetState(State.Chase);
        }
    }
}
