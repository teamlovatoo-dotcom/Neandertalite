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
            Cam.transform.localRotation = Quaternion.Euler(pitch + shakeRot.x, shakeRot.y, tilt + shakeRot.z);
        }

        void Move(float dt)
        {
            Vector2 input = InputLocked ? Vector2.zero : GameInput.Move;
            Vector3 wishDir = (transform.right * input.x + transform.forward * input.y);
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            // Crouch (shrinks the capsule; stays crouched under ceilings).
            bool wantCrouch = !InputLocked && GameInput.CrouchHeld;
            if (!wantCrouch && Crouching)
            {
                bool blocked = Physics.SphereCast(transform.position + Vector3.up * CrouchHeight * 0.5f, Controller.radius * 0.9f, Vector3.up,
                    out _, StandHeight - CrouchHeight * 0.5f, ~0, QueryTriggerInteraction.Ignore);
                if (blocked) wantCrouch = true;
            }
            Crouching = wantCrouch;
            float targetH = Crouching ? CrouchHeight : StandHeight;
            Controller.height = Mathf.MoveTowards(Controller.height, targetH, dt * 6f);
            Controller.center = new Vector3(0f, Controller.height * 0.5f, 0f);
            float targetEye = Crouching ? CrouchHeight - 0.15f : 1.65f;
            eyeHeight = Mathf.MoveTowards(eyeHeight, targetEye, dt * 5f);
            Cam.transform.localPosition = new Vector3(0f, eyeHeight, 0f);

            bool movingForward = input.y > 0.1f;
            bool wantSprint = !InputLocked && GameInput.SprintHeld && movingForward && !Crouching && Stamina > 1f && !(Weapons.Current != null && Weapons.Current.Aiming);
            IsSprinting = wantSprint && Grounded;
            float maxSpeed = Crouching ? CrouchSpeed : (wantSprint ? SprintSpeed : WalkSpeed);

            if (Grounded)
            {
                // Friction
                Vector3 hv = new Vector3(velocity.x, 0f, velocity.z);
                float speed = hv.magnitude;
                if (speed > 0.001f)
                {
                    float drop = Mathf.Max(speed, 1.5f) * Friction * dt;
                    hv *= Mathf.Max(0f, speed - drop) / speed;
                }
                hv = Accelerate(hv, wishDir, maxSpeed, GroundAccel, dt);
                velocity.x = hv.x;
                velocity.z = hv.z;
                velocity.y = -2f;

                if (!InputLocked && GameInput.JumpDown && Stamina >= JumpCost * 0.5f)
                {
                    UseStamina(JumpCost);
                    velocity.y = JumpSpeed;
                    Grounded = false;
                }

                if (IsSprinting) UseStaminaContinuous(SprintCost * dt);

                stepTimer -= dt * speed;
                if (stepTimer <= 0f && speed > 1f)
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
                velocity.y -= Gravity * dt;
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
                // Landed
                float fall = fallStartY - transform.position.y;
                if (fall > 5f)
                {
                    float dmg = (fall - 5f) * 9f;
                    Health.TakeDamage(new DamageInfo { Amount = dmg, Point = transform.position, Direction = Vector3.down });
                }
                if (airTime > 0.3f) Sfx.Play("step", transform.position, 1f, 0.7f);
                airTime = 0f;
                fallStartY = transform.position.y;
            }

            // Fell out of the world
            if (transform.position.y < -60f)
                Health.TakeDamage(new DamageInfo { Amount = 9999f, Direction = Vector3.down });
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
            velocity = Vector3.zero;
            pitch = 0f;
            Cam.transform.localPosition = new Vector3(0, eyeHeight, 0);
        }
    }
}
