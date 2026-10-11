using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Half-Life style first-person controller: Quake-like ground/air acceleration, sprint with stamina,
    /// crouch, jump, fall damage, recoil and screen shake.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        public float WalkSpeed = 5.2f;
        public float SprintSpeed = 8.2f;
        public float CrouchSpeed = 2.6f;
        public float GroundAccel = 11f;
        public float AirAccel = 2.2f;
        public float Friction = 7f;
        public float JumpSpeed = 5.4f;
        public float Gravity = 18f;
        public float StandHeight = 1.8f;
        public float CrouchHeight = 1.1f;

        [Header("Stamina")]
        public float MaxStamina = 100f;
        public float Stamina = 100f;
        public float StaminaRegen = 18f;
        public float SprintCost = 16f;
        public float JumpCost = 10f;

        public Camera Cam { get; private set; }
        public CharacterController Controller { get; private set; }
        public Health Health { get; private set; }
        public Inventory Inventory { get; private set; }
        public WeaponHolder Weapons { get; private set; }
        public PlayerInteractor Interactor { get; private set; }

        public Vector3 Velocity => velocity;
        public bool Grounded { get; private set; }
        public bool Swimming { get; private set; }
        public bool HeadUnderwater { get; private set; }
        public bool Rolling => rollT >= 0f;
        public float Oxygen = 12f;
        public float MaxOxygen = 12f;
        public bool IsSprinting { get; private set; }
        public bool Crouching { get; private set; }
        /// <summary>True while a menu (shop, crafting, pause) owns the mouse.</summary>
        public bool InputLocked => UIManager.AnyMenuOpen || UIManager.JustChanged || Health.IsDead;
        public float Yaw => yaw;

        Vector3 velocity;
        float yaw, pitch;
        float recoilPitch, recoilYaw;
        float shake;
        float staminaDelay;
        float eyeHeight;
        float stepTimer;
        float airTime;
        float fallStartY;
        bool wasGrounded;

        // Movement extras
        const float RollTime = 0.55f;
        float coyote, jumpBuffer, rollT = -1f, slideT, mantleT = -1f, landRollWindow, drownTick, swimSound;
        Vector3 rollDir, slideDir, mantleFrom, mantleTo;
        bool wasInWater;

        public static PlayerController Spawn(Transform parent, Vector3 pos, float yawDeg)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            var pc = go.AddComponent<PlayerController>();
            pc.yaw = yawDeg;
            return pc;
        }

        void Awake()
        {
            Instance = this;
            Controller = GetComponent<CharacterController>();
            Health = gameObject.AddComponent<Health>();
            Health.Team = Team.Player;
            Health.ResetHealth(100f);
            Inventory = gameObject.AddComponent<Inventory>();

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            eyeHeight = 1.65f;
            camGo.transform.localPosition = new Vector3(0, eyeHeight, 0);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 900f;
            Cam.fieldOfView = Profile.Current.Fov;
            camGo.AddComponent<AudioListener>();
            Neanderthal.Rendering.PostFx.SetupCamera(Cam);

            Weapons = gameObject.AddComponent<WeaponHolder>();
            Weapons.Init(this);
            Interactor = gameObject.AddComponent<PlayerInteractor>();

            Health.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnDamaged(DamageInfo info)
        {
            Sfx.Play2D("hurt", 0.8f, Random.Range(0.9f, 1.1f));
            Shake(Mathf.Clamp(info.Amount / 40f, 0.1f, 0.6f));
            if (info.Knockback > 0f)
            {
                Vector3 push = info.Direction; push.y = 0f;
                velocity += push.normalized * info.Knockback * 0.6f + Vector3.up * info.Knockback * 0.15f;
            }
            AddRecoil(Random.Range(1f, 3f), Random.Range(-2f, 2f));
        }

        public bool UseStamina(float amount)
        {
            staminaDelay = 1f;
            if (Stamina < amount) { Stamina = 0f; return false; }
            Stamina -= amount;
            return true;
        }

        public void AddRecoil(float up, float side)
        {
            recoilPitch += up;
            recoilYaw += side;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        public void Teleport(Vector3 pos, float yawDeg)
        {
            Controller.enabled = false;
            transform.position = pos;
            Controller.enabled = true;
            velocity = Vector3.zero;
            yaw = yawDeg;
            pitch = 0f;
            fallStartY = pos.y;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (Health.IsDead)
            {
                // Slump to the ground.
                Cam.transform.localPosition = Vector3.Lerp(Cam.transform.localPosition, new Vector3(0, 0.35f, 0), dt * 3f);
                Cam.transform.localRotation = Quaternion.Slerp(Cam.transform.localRotation, Quaternion.Euler(pitch, 0, 70f), dt * 3f);
                return;
            }

            Look(dt);
            Move(dt);
            UpdateStamina(dt);
        }

        void Look(float dt)
        {
            if (!InputLocked)
            {
                Vector2 look = GameInput.Look;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -88f, 88f);
            }
            // Recoil: snap up, recover smoothly.
            float rp = recoilPitch * Mathf.Min(1f, dt * 20f);
            float ry = recoilYaw * Mathf.Min(1f, dt * 20f);
            recoilPitch -= rp;
            recoilYaw -= ry;
            pitch = Mathf.Clamp(pitch - rp, -88f, 88f);
            yaw += ry;

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
            Vector3 shakeRot = shake > 0f ? new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * shake * 3f : Vector3.zero;
            float tilt = -GameInput.Move.x * 1.2f * (Grounded ? 1f : 0.5f);
            float flip = rollT >= 0f ? 360f * Mathf.SmoothStep(0f, 1f, rollT / RollTime) : 0f;
            Cam.transform.localRotation = Quaternion.Euler(pitch + shakeRot.x + flip, shakeRot.y, tilt + shakeRot.z);
        }

        void Move(float dt)
        {
            Vector2 input = InputLocked ? Vector2.zero : GameInput.Move;
            Vector3 wishDir = (transform.right * input.x + transform.forward * input.y);
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            // ---- Water: wading slows you, deep water switches to swimming.
            bool inWater = WaterZones.Surface(transform.position, out float surface);
            float depth = inWater ? surface - transform.position.y : 0f;
            if (inWater && !wasInWater && velocity.y < -3f) Sfx.Play("swing", transform.position, 1f, 0.45f);
            wasInWater = inWater;
            Swimming = inWater && depth > 1.2f && mantleT < 0f;
            HeadUnderwater = inWater && Cam.transform.position.y < surface - 0.05f;
            UpdateOxygen(dt);

            if (mantleT >= 0f) { DoMantle(dt); return; }
            if (Swimming) { Swim(dt, input, surface); return; }

            bool locked = InputLocked;
            if (!locked && GameInput.JumpDown) jumpBuffer = 0.15f; else jumpBuffer -= dt;
            if (Grounded) coyote = 0.12f; else coyote -= dt;
            if (!locked && (GameInput.CrouchDown || GameInput.RollDown)) landRollWindow = 0.35f; else landRollWindow -= dt;

            // ---- Dodge roll (V / Alt)
            if (!locked && GameInput.RollDown && Grounded && rollT < 0f && Stamina >= 15f)
            {
                UseStamina(18f);
                rollT = 0f;
                rollDir = wishDir.sqrMagnitude > 0.01f ? wishDir.normalized : transform.forward;
                Sfx.Play("swing", transform.position, 0.8f, 0.6f);
            }

            // ---- Crouch (forced low during rolls and slides; stays crouched under ceilings)
            bool wantCrouch = !locked && GameInput.CrouchHeld || rollT >= 0f || slideT > 0f;
            if (!wantCrouch && Crouching)
            {
                bool blocked = Physics.SphereCast(transform.position + Vector3.up * CrouchHeight * 0.5f, Controller.radius * 0.9f, Vector3.up,
                    out _, StandHeight - CrouchHeight * 0.5f, ~0, QueryTriggerInteraction.Ignore);
                if (blocked) wantCrouch = true;
            }
            Crouching = wantCrouch;
            float targetH = Crouching ? CrouchHeight : StandHeight;
            Controller.height = Mathf.MoveTowards(Controller.height, targetH, dt * 8f);
            Controller.center = new Vector3(0f, Controller.height * 0.5f, 0f);
            float targetEye = rollT >= 0f ? 0.7f : (Crouching ? CrouchHeight - 0.15f : 1.65f);
            eyeHeight = Mathf.MoveTowards(eyeHeight, targetEye, dt * (rollT >= 0f ? 9f : 5f));
            Cam.transform.localPosition = new Vector3(0f, eyeHeight, 0f);

            bool movingForward = input.y > 0.1f;
            bool wantSprint = !locked && GameInput.SprintHeld && movingForward && !Crouching && Stamina > 1f && !(Weapons.Current != null && Weapons.Current.Aiming);
            IsSprinting = wantSprint && Grounded;
            float maxSpeed = Crouching ? CrouchSpeed : (wantSprint ? SprintSpeed : WalkSpeed);
            if (inWater) maxSpeed *= Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(depth / 1.2f));

            // ---- Slide: crouch while sprinting keeps your momentum for a moment
            Vector3 hvNow = new Vector3(velocity.x, 0f, velocity.z);
            if (!locked && GameInput.CrouchDown && Grounded && hvNow.magnitude > 6.5f && slideT <= 0f && rollT < 0f)
            {
                slideT = 0.85f;
                slideDir = hvNow.normalized;
                velocity += slideDir * 2f;
                Sfx.Play("step", transform.position, 1f, 0.5f);
            }

            if (rollT >= 0f)
            {
                rollT += dt;
                float k = rollT / RollTime;
                Vector3 rv = rollDir * Mathf.Lerp(10.5f, 4f, k);
                velocity.x = rv.x;
                velocity.z = rv.z;
                if (Grounded) velocity.y = -2f; else velocity.y -= Gravity * dt;
                if (k >= 1f) rollT = -1f;
            }
            else if (Grounded)
            {
                Vector3 hv = new Vector3(velocity.x, 0f, velocity.z);
                float speed = hv.magnitude;
                if (slideT > 0f)
                {
                    slideT -= dt;
                    float drop = 3f * dt;
                    hv = speed > drop ? hv * ((speed - drop) / speed) : Vector3.zero;
                    hv += Vector3.Project(wishDir, Vector3.Cross(Vector3.up, slideDir)) * 2f * dt; // slight steering
                    if (speed < 3.5f || (!locked && !GameInput.CrouchHeld)) slideT = 0f;
                }
                else
                {
                    if (speed > 0.001f)
                    {
                        float drop = Mathf.Max(speed, 1.5f) * Friction * dt;
                        hv *= Mathf.Max(0f, speed - drop) / speed;
                    }
                    hv = Accelerate(hv, wishDir, maxSpeed, GroundAccel, dt);
                }
                velocity.x = hv.x;
                velocity.z = hv.z;
                velocity.y = -2f;

                if (IsSprinting) UseStaminaContinuous(SprintCost * dt);

                stepTimer -= dt * speed;
                if (stepTimer <= 0f && speed > 1f && slideT <= 0f)
                {
                    stepTimer = 2.2f;
                    Sfx.Play("step", transform.position, Crouching ? 0.3f : 0.7f, Random.Range(0.8f, 1.2f));
                }
            }
            else
            {
                Vector3 hv = new Vector3(velocity.x, 0f, velocity.z);
                hv = Accelerate(hv, wishDir, maxSpeed, AirAccel, dt);
                velocity.x = hv.x;
                velocity.z = hv.z;
                // Snappier arc: heavier when falling, short hop when Space is released early.
                bool holding = !locked && GameInput.JumpHeld;
                float g = velocity.y < 0f ? 1.45f : (holding ? 1f : 1.9f);
                velocity.y -= Gravity * g * dt;

                // Ledge climb: jump into a ledge while holding forward.
                if (!locked && holding && input.y > 0.1f && velocity.y < 2.5f && TryMantle()) return;
            }

            // ---- Jump with coyote time and input buffering (can also jump out of a roll or slide)
            if (jumpBuffer > 0f && coyote > 0f && Stamina >= JumpCost * 0.5f && !locked)
            {
                UseStamina(JumpCost);
                velocity.y = JumpSpeed * (slideT > 0f ? 1.05f : 1f);
                jumpBuffer = 0f;
                coyote = 0f;
                rollT = -1f;
                slideT = 0f;
                Grounded = false;
                // Grounded jump into a high ledge mantles straight away.
                if (input.y > 0.1f && TryMantle()) return;
            }

            var flags = Controller.Move(velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

            wasGrounded = Grounded;
            Grounded = Controller.isGrounded;
            if (!Grounded)
            {
                if (wasGrounded) fallStartY = transform.position.y;
                fallStartY = Mathf.Max(fallStartY, transform.position.y);
                airTime += dt;
            }
            else if (!wasGrounded)
            {
                // Landed. Pressing crouch/roll just before touching down turns a hard fall into a roll.
                float fall = fallStartY - transform.position.y;
                bool landingRoll = landRollWindow > 0f && fall > 2.5f;
                if (fall > 5f && !inWater)
                {
                    float dmg = (fall - 5f) * 9f * (landingRoll ? 0.3f : 1f);
                    Health.TakeDamage(new DamageInfo { Amount = dmg, Point = transform.position, Direction = Vector3.down });
                }
                if (landingRoll && rollT < 0f)
                {
                    rollT = 0f;
                    Vector3 hv = new Vector3(velocity.x, 0f, velocity.z);
                    rollDir = hv.sqrMagnitude > 0.5f ? hv.normalized : transform.forward;
                }
                if (airTime > 0.3f) Sfx.Play("step", transform.position, 1f, 0.7f);
                airTime = 0f;
                fallStartY = transform.position.y;
            }

            // Fell out of the world
            if (transform.position.y < -60f)
                Health.TakeDamage(new DamageInfo { Amount = 9999f, Direction = Vector3.down });
        }

        void Swim(float dt, Vector2 input, float surface)
        {
            rollT = -1f;
            slideT = 0f;
            Crouching = false;
            Controller.height = Mathf.MoveTowards(Controller.height, StandHeight, dt * 6f);
            Controller.center = new Vector3(0f, Controller.height * 0.5f, 0f);
            eyeHeight = Mathf.MoveTowards(eyeHeight, 1.65f, dt * 5f);
            Cam.transform.localPosition = new Vector3(0f, eyeHeight, 0f);

            bool locked = InputLocked;
            var cam = Cam.transform;
            Vector3 wish = cam.forward * input.y + cam.right * input.x;
            if (!locked && GameInput.JumpHeld) wish += Vector3.up;
            if (!locked && GameInput.CrouchHeld) wish += Vector3.down;
            wish = Vector3.ClampMagnitude(wish, 1f);
            bool fast = !locked && GameInput.SprintHeld && Stamina > 1f && wish.sqrMagnitude > 0.1f;
            if (fast) UseStaminaContinuous(10f * dt);
            IsSprinting = false;
            Vector3 target = wish * (fast ? 5.2f : 3.4f);

            // Float with the head just above the surface unless diving.
            float floatFeet = surface - 1.45f;
            if (wish.y <= 0.05f && !(!locked && GameInput.CrouchHeld))
                target.y += Mathf.Clamp((floatFeet - transform.position.y) * 2.5f, -1.5f, 1.5f);
            velocity = Vector3.Lerp(velocity, target, dt * 3f);
            // Can't fly out of the water; climb out at the shore instead.
            if (transform.position.y > surface - 1.25f && velocity.y > 0f) velocity.y = 0f;

            Controller.Move(velocity * dt);
            Grounded = false;
            wasGrounded = false;
            fallStartY = transform.position.y;
            airTime = 0f;

            swimSound -= dt * velocity.magnitude;
            if (swimSound <= 0f && velocity.magnitude > 1f)
            {
                swimSound = 3f;
                Sfx.Play("swing", transform.position, 0.5f, 0.55f);
            }
            // Pull yourself onto a ledge from the water.
            if (!locked && input.y > 0.1f && GameInput.JumpHeld) TryMantle();
        }

        void UpdateOxygen(float dt)
        {
            if (HeadUnderwater)
            {
                Oxygen = Mathf.Max(0f, Oxygen - dt);
                if (Oxygen <= 0f)
                {
                    drownTick -= dt;
                    if (drownTick <= 0f)
                    {
                        drownTick = 1f;
                        Health.TakeDamage(new DamageInfo { Amount = 10f, Point = transform.position, Direction = Vector3.up });
                    }
                }
            }
            else
            {
                Oxygen = Mathf.Min(MaxOxygen, Oxygen + dt * 4f);
                drownTick = 0f;
            }
        }

        /// <summary>Climb onto a ledge 0.6-2.1 m above the feet if there is room to stand.</summary>
        bool TryMantle()
        {
            if (mantleT >= 0f) return true;
            Vector3 fwd = transform.forward;
            Vector3 feet = transform.position;
            float r = Controller.radius;
            if (!PhysicsUtil.Raycast(feet + Vector3.up * 0.9f, fwd, r + 0.6f, transform, out _)) return false;
            Vector3 probe = feet + fwd * (r + 0.5f) + Vector3.up * 2.4f;
            if (!PhysicsUtil.Raycast(probe, Vector3.down, 2.3f, transform, out var top)) return false;
            float rise = top.point.y - feet.y;
            if (rise < 0.6f || rise > 2.1f || top.normal.y < 0.7f) return false;
            Vector3 stand = top.point + Vector3.up * 0.05f;
            var hits = Physics.OverlapCapsule(stand + Vector3.up * (r + 0.05f), stand + Vector3.up * (StandHeight - r), r * 0.9f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
                if (!h.transform.IsChildOf(transform)) return false;
            mantleFrom = feet;
            mantleTo = stand;
            mantleT = 0f;
            velocity = Vector3.zero;
            UseStamina(8f);
            Sfx.Play("step", feet, 1f, 0.8f);
            return true;
        }

        void DoMantle(float dt)
        {
            mantleT += dt / 0.42f;
            float k = Mathf.Clamp01(mantleT);
            Vector3 up = new Vector3(mantleFrom.x, mantleTo.y + 0.1f, mantleFrom.z);
            Vector3 p = k < 0.6f ? Vector3.Lerp(mantleFrom, up, Mathf.SmoothStep(0f, 1f, k / 0.6f)) : Vector3.Lerp(up, mantleTo, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));
            Controller.enabled = false;
            transform.position = p;
            Controller.enabled = true;
            eyeHeight = Mathf.MoveTowards(eyeHeight, 1.65f, dt * 5f);
            Cam.transform.localPosition = new Vector3(0f, eyeHeight - Mathf.Sin(k * Mathf.PI) * 0.25f, 0f);
            if (mantleT >= 1f)
            {
                mantleT = -1f;
                Grounded = true;
                wasGrounded = true;
                fallStartY = transform.position.y;
                airTime = 0f;
            }
        }

        static Vector3 Accelerate(Vector3 vel, Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            if (wishDir.sqrMagnitude < 0.0001f) return vel;
            float current = Vector3.Dot(vel, wishDir);
            float add = wishSpeed - current;
            if (add <= 0f) return vel;
            float accelSpeed = Mathf.Min(accel * wishSpeed * dt, add);
            return vel + wishDir * accelSpeed;
        }

        void UseStaminaContinuous(float amount)
        {
            Stamina = Mathf.Max(0f, Stamina - amount);
            staminaDelay = 0.6f;
        }

        void UpdateStamina(float dt)
        {
            staminaDelay -= dt;
            if (staminaDelay <= 0f) Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegen * dt);
        }

        public void ResetForRespawn()
        {
            Health.Revive();
            Stamina = MaxStamina;
            Oxygen = MaxOxygen;
            rollT = -1f;
            slideT = 0f;
            mantleT = -1f;
            velocity = Vector3.zero;
            pitch = 0f;
            Cam.transform.localPosition = new Vector3(0, eyeHeight, 0);
        }
    }
}
