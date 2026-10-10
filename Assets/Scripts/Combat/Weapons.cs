using UnityEngine;

namespace Neanderthal
{
    /// <summary>Base class for first-person weapon behaviours (plain C#, driven by WeaponHolder).</summary>
    public abstract class Weapon
    {
        public WeaponData Data;
        protected WeaponHolder Holder;
        protected PlayerController Player => Holder.Player;
        protected Transform Model;
        protected float Cooldown;

        // Outputs read by WeaponHolder / HUD.
        public Vector3 AnimPos;
        public Vector3 AnimEuler;
        public float Charge;                // bows & throws: 0..1 for the HUD
        public bool Aiming;
        public virtual bool Busy => Cooldown > 0f;
        public virtual bool CanHolster => true;
        public virtual Vector3 HipPosition => new Vector3(0.28f, -0.27f, 0.45f);
        public virtual Vector3 HipEuler => new Vector3(0f, -6f, 0f);
        public virtual string AmmoText => "";
        public virtual string ReserveText => "";

        public static Weapon Create(WeaponData data, WeaponHolder holder)
        {
            Weapon w;
            switch (data.Kind)
            {
                case WeaponKind.Spear: w = new SpearWeapon(); break;
                case WeaponKind.Bow: w = new BowWeapon(); break;
                case WeaponKind.Firearm: w = new GunWeapon(); break;
                default: w = new MeleeWeapon(); break;
            }
            w.Data = data;
            w.Holder = holder;
            return w;
        }

        public virtual void Equip()
        {
            Model = WeaponModels.Build(Data.Id, Holder.ViewRoot, out var bs).transform;
            OnModelBuilt(bs);
            Shapes.SetShadows(Model.gameObject, false);
            // A rough hand on the grip so it reads as "held".
            if (Data.Id != "fists")
                Shapes.Box(Model, new Vector3(0f, -0.01f, 0f), new Vector3(0.085f, 0.09f, 0.1f), Mat.Get(Mat.Skin), null, false);
            if (Data.LightRange > 0f)
                Shapes.PointLight(Model, new Vector3(0f, 0.1f, 0.55f), Mat.Fire, Data.LightRange, 2.2f, true);
        }

        protected virtual void OnModelBuilt(BowString bowString) { }
        public virtual void Holster() { }
        public abstract void Tick(float dt, bool inputOk);

        protected Vector3 EyePos => Player.Cam.transform.position;
        protected Vector3 Aim => Player.Cam.transform.forward;

        protected Vector3 Spread(Vector3 dir, float degrees)
        {
            if (degrees <= 0f) return dir;
            var cam = Player.Cam.transform;
            Vector2 r = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return (dir + cam.right * r.x + cam.up * r.y).normalized;
        }

        /// <summary>Shared melee hit: sphere-cast from the eye; damages creatures, harvests trees/rocks.</summary>
        protected void MeleeStrike(float damage, float range, float knockback)
        {
            var root = Player.transform;
            if (PhysicsUtil.SphereCast(EyePos, 0.22f, Aim, range, root, out var hit))
            {
                if (Health.Apply(hit, damage, Aim, Player.gameObject, knockback, Data.Id))
                {
                    Sfx.Play("hit", hit.point);
                    Effects.Blood(hit.point, -Aim);
                    Player.Shake(0.15f);
                    return;
                }
                var harvest = hit.collider.GetComponentInParent<Harvestable>();
                if (harvest != null)
                {
                    harvest.Hit(Data.HarvestPower, hit.point, Player.gameObject);
                    Player.Shake(0.1f);
                    return;
                }
                Sfx.Play("thunk", hit.point, 0.7f);
                Effects.Dust(hit.point, hit.normal);
            }
        }
    }

    public class MeleeWeapon : Weapon
    {
        float swingT = -1f;        // -1 idle, otherwise seconds into the swing
        bool struck;
        float power;
        bool alt;

        float Duration => Mathf.Max(0.3f, Data.Cooldown);

        public override bool CanHolster => swingT < 0f;

        public override void Tick(float dt, bool inputOk)
        {
            Cooldown -= dt;
            if (swingT < 0f && inputOk && Cooldown <= 0f && (GameInput.FireHeld || GameInput.AltDown))
            {
                alt = GameInput.AltDown && !GameInput.FireHeld;
                // Out of stamina = feeble swing.
                power = Player.UseStamina(Data.StaminaCost * (alt ? 1.6f : 1f)) ? 1f : 0.5f;
                swingT = 0f;
                struck = false;
                Sfx.Play("swing", EyePos, 0.6f, Data.Cooldown > 0.6f ? 0.8f : 1.1f);
            }

            if (swingT >= 0f)
            {
                swingT += dt;
                float k = swingT / Duration;
                // Wind-up (0-0.3), strike (0.3-0.5), recover (0.5-1).
                if (!struck && k >= 0.35f)
                {
                    struck = true;
                    float dmg = Data.Damage * power * (alt ? 1.5f : 1f);
                    MeleeStrike(dmg, Data.Range, Data.Damage * 0.08f * (alt ? 2f : 1f));
                }
                if (k < 0.3f)
                {
                    float w = k / 0.3f;
                    AnimEuler = alt ? new Vector3(-50f * w, 0f, 0f) : new Vector3(-25f * w, -20f * w, 25f * w);
                    AnimPos = new Vector3(0.08f * w, 0.1f * w, -0.05f * w);
                }
                else if (k < 0.5f)
                {
                    float s = (k - 0.3f) / 0.2f;
                    AnimEuler = alt ? Vector3.Lerp(new Vector3(-50, 0, 0), new Vector3(60, 0, 0), s)
                                    : Vector3.Lerp(new Vector3(-25, -20, 25), new Vector3(40, 50, -30), s);
                    AnimPos = Vector3.Lerp(new Vector3(0.08f, 0.1f, -0.05f), new Vector3(-0.25f, -0.05f, 0.15f), s);
                }
                else
                {
                    float r = (k - 0.5f) / 0.5f;
                    AnimEuler = Vector3.Lerp(alt ? new Vector3(60, 0, 0) : new Vector3(40, 50, -30), Vector3.zero, r);
                    AnimPos = Vector3.Lerp(new Vector3(-0.25f, -0.05f, 0.15f), Vector3.zero, r);
                }
                if (k >= 1f)
                {
                    swingT = -1f;
                    Cooldown = 0.05f;
                    AnimEuler = Vector3.zero;
                    AnimPos = Vector3.zero;
                }
            }
        }
    }

    public class SpearWeapon : Weapon
    {
        float thrustT = -1f;
        bool struck;
        float throwCharge;
        bool raising;

        public override bool CanHolster => thrustT < 0f && !raising;
        public override Vector3 HipPosition => new Vector3(0.22f, -0.22f, 0.1f);
        public override Vector3 HipEuler => new Vector3(-4f, -3f, 0f);

        public override void Tick(float dt, bool inputOk)
        {
            Cooldown -= dt;

            // RMB: raise and throw.
            if (inputOk && GameInput.AltHeld && thrustT < 0f && Cooldown <= 0f)
            {
                raising = true;
                throwCharge = Mathf.MoveTowards(throwCharge, 1f, dt / 0.45f);
            }
            if (raising && (!GameInput.AltHeld || !inputOk))
            {
                raising = false;
                if (throwCharge > 0.3f && inputOk)
                {
                    Throw();
                    return;
                }
                throwCharge = 0f;
            }
            Charge = throwCharge;
            Aiming = raising;

            if (!raising && thrustT < 0f && inputOk && Cooldown <= 0f && GameInput.FireHeld)
            {
                float power = Player.UseStamina(Data.StaminaCost) ? 1f : 0.5f;
                thrustT = 0f;
                struck = false;
                Sfx.Play("swing", EyePos, 0.5f, 1.2f);
                Cooldown = Data.Cooldown;
                pendingPower = power;
            }

            if (thrustT >= 0f)
            {
                thrustT += dt;
                float k = thrustT / Data.Cooldown;
                if (!struck && k > 0.3f)
                {
                    struck = true;
                    MeleeStrike(Data.Damage * pendingPower, Data.Range, 3f);
                }
                float push = k < 0.3f ? -k / 0.3f * 0.1f : (k < 0.45f ? Mathf.Lerp(-0.1f, 0.45f, (k - 0.3f) / 0.15f) : Mathf.Lerp(0.45f, 0f, (k - 0.45f) / 0.55f));
                AnimPos = new Vector3(-0.08f * Mathf.Clamp01(push * 3f), 0.04f * Mathf.Clamp01(push * 3f), push);
                AnimEuler = Vector3.zero;
                if (k >= 1f) { thrustT = -1f; AnimPos = Vector3.zero; }
            }
            else
            {
                // Overhand throw pose.
                AnimPos = new Vector3(-0.02f, 0.22f, -0.15f) * throwCharge;
                AnimEuler = new Vector3(-8f, 2f, 0f) * throwCharge;
            }
        }

        float pendingPower = 1f;

        void Throw()
        {
            Player.UseStamina(Data.StaminaCost * 1.5f);
            var visual = WeaponModels.Build("spear", null, out _);
            Vector3 dir = Aim;
            Vector3 pos = EyePos + dir * 0.6f + Player.Cam.transform.right * 0.15f;
            var p = Projectile.Launch(visual, pos, dir * Data.ProjectileSpeed * Mathf.Lerp(0.6f, 1f, throwCharge) + Player.Velocity * 0.5f, 70f * throwCharge, Player.gameObject, 7f);
            p.RecoverWeaponId = Data.Id;
            p.Knockback = 5f;
            Sfx.Play("swing", EyePos, 0.9f, 0.7f);
            throwCharge = 0f;
            Charge = 0f;
            Aiming = false;
            Player.Inventory.RemoveWeapon(Data.Id);
            Holder.OnWeaponConsumed();
        }
    }

    public class BowWeapon : Weapon
    {
        BowString str;
        float draw;
        float heldFull;
        bool drawing;

        public override bool CanHolster => !drawing;
        public override bool Busy => drawing;
        public override Vector3 HipPosition => Aiming ? new Vector3(-0.03f, -0.07f, 0.5f) : new Vector3(-0.1f, -0.1f, 0.5f);
        public override Vector3 HipEuler => new Vector3(0f, 3f, -24f);
        public override string AmmoText => Player.Inventory.GetAmmo(Data.Ammo).ToString();
        public override string ReserveText => ItemDatabase.AmmoName(Data.Ammo);

        protected override void OnModelBuilt(BowString bowString) => str = bowString;

        public override void Tick(float dt, bool inputOk)
        {
            Cooldown -= dt;
            int arrows = Player.Inventory.GetAmmo(Data.Ammo);
            Aiming = inputOk && GameInput.AltHeld;

            if (!drawing && inputOk && Cooldown <= 0f && GameInput.FireHeld && arrows > 0)
            {
                drawing = true;
                draw = 0f;
                heldFull = 0f;
                Sfx.Play("draw", EyePos, 0.5f);
            }

            if (drawing)
            {
                draw = Mathf.MoveTowards(draw, 1f, dt / Data.ChargeTime);
                if (draw >= 1f)
                {
                    heldFull += dt;
                    // Holding a full draw tires the arms.
                    if (heldFull > 2f && !Player.UseStamina(8f * dt)) heldFull += dt * 2f;
                }
                if (!GameInput.FireHeld || !inputOk)
                {
                    drawing = false;
                    if (draw > 0.15f && inputOk) Release();
                    draw = 0f;
                }
            }

            Charge = draw;
            if (str != null)
            {
                str.Draw = draw;
                str.ShowArrow = arrows > 0 && Cooldown <= 0.05f;
            }
            AnimPos = new Vector3(-0.02f * draw, 0.03f * draw, -0.06f * draw);
            AnimEuler = new Vector3(0f, 0f, 6f * draw);
        }

        void Release()
        {
            if (Player.Inventory.TakeAmmo(Data.Ammo, 1) == 0) return;
            float shake = heldFull > 2.5f ? Mathf.Min(4f, (heldFull - 2.5f) * 2f) : 0f;
            float spread = (Aiming ? 0.1f : 0.6f) + (1f - draw) * 1.5f + shake;
            Vector3 dir = Spread(Aim, spread);
            var visual = WeaponModels.Arrow(null, false);
            Vector3 pos = EyePos + dir * 0.4f;
            var p = Projectile.Launch(visual, pos, dir * Data.ProjectileSpeed * Mathf.Lerp(0.35f, 1f, draw), Data.Damage * Mathf.Lerp(0.25f, 1f, draw * draw), Player.gameObject);
            p.RecoverAmmo = AmmoType.Arrow;
            Sfx.Play("bow", EyePos, 0.8f);
            Cooldown = Data.Cooldown;
            Player.AddRecoil(0.6f, Random.Range(-0.2f, 0.2f));
        }
    }

    public class GunWeapon : Weapon
    {
        BowString str;
        int mag;
        float reloadT = -1f;
        float recoilAnim;

        public override bool CanHolster => true;
        public override bool Busy => reloadT >= 0f;
        public override Vector3 HipPosition => Aiming ? new Vector3(0f, -0.1f, 0.35f) : new Vector3(0.22f, -0.2f, 0.4f);
        public override Vector3 HipEuler => Vector3.zero;
        public override string AmmoText => mag.ToString();
        public override string ReserveText => Player.Inventory.GetAmmo(Data.Ammo).ToString();

        protected override void OnModelBuilt(BowString bowString) => str = bowString;

        public override void Equip()
        {
            base.Equip();
            mag = Holder.GetLoaded(Data.Id, 0);
            if (mag == 0) mag = Player.Inventory.TakeAmmo(Data.Ammo, Data.MagSize);
            Holder.SetLoaded(Data.Id, mag);
        }

        public override void Holster()
        {
            reloadT = -1f;
            Holder.SetLoaded(Data.Id, mag);
        }

        public override void Tick(float dt, bool inputOk)
        {
            Cooldown -= dt;
            Aiming = inputOk && GameInput.AltHeld && reloadT < 0f && Data.ZoomFov > 0f;

            if (reloadT >= 0f)
            {
                reloadT += dt;
                float k = reloadT / Data.ReloadTime;
                AnimPos = new Vector3(0f, -0.12f * Mathf.Sin(k * Mathf.PI), 0f);
                AnimEuler = new Vector3(25f * Mathf.Sin(k * Mathf.PI), 0f, -20f * Mathf.Sin(k * Mathf.PI));
                if (str != null) str.Draw = Mathf.Clamp01(k * 1.3f);
                if (k >= 1f)
                {
                    int need = Data.MagSize - mag;
                    mag += Player.Inventory.TakeAmmo(Data.Ammo, need);
                    Holder.SetLoaded(Data.Id, mag);
                    reloadT = -1f;
                    AnimPos = Vector3.zero;
                    AnimEuler = Vector3.zero;
                }
                return;
            }

            if (str != null)
            {
                str.Draw = mag > 0 ? 1f : 0f;
                str.ShowArrow = mag > 0;
            }

            bool wantsReload = inputOk && GameInput.ReloadDown && mag < Data.MagSize;
            bool trigger = inputOk && (Data.Automatic ? GameInput.FireHeld : GameInput.FireDown);
            if (trigger && mag <= 0)
            {
                if (Player.Inventory.GetAmmo(Data.Ammo) > 0) wantsReload = true;
                else if (GameInput.FireDown) Sfx.Play2D("click", 0.8f);
            }
            if (wantsReload && Player.Inventory.GetAmmo(Data.Ammo) > 0)
            {
                reloadT = 0f;
                Sfx.Play("reload", EyePos, 0.6f);
                return;
            }

            if (trigger && mag > 0 && Cooldown <= 0f) Fire();

            recoilAnim = Mathf.MoveTowards(recoilAnim, 0f, dt * 8f);
            AnimPos = new Vector3(0f, 0.01f, -0.06f) * recoilAnim;
            AnimEuler = new Vector3(-8f, 0f, 0f) * recoilAnim;
        }

        void Fire()
        {
            mag--;
            Holder.SetLoaded(Data.Id, mag);
            Cooldown = Data.Cooldown;
            recoilAnim = 1f;
            Sfx.Play(Data.Sound, EyePos, 0.9f);

            float spread = Data.Spread * (Aiming ? 0.35f : 1f) * (Player.Crouching ? 0.7f : 1f) * (Player.Grounded ? 1f : 2f);
            spread += Player.Velocity.magnitude > 3f ? Data.Spread * 0.5f : 0f;

            if (Data.ProjectileSpeed > 0f)
            {
                Vector3 dir = Spread(Aim, spread);
                var visual = WeaponModels.Arrow(null, true);
                var p = Projectile.Launch(visual, EyePos + dir * 0.4f, dir * Data.ProjectileSpeed, Data.Damage, Player.gameObject, 4f);
                p.RecoverAmmo = AmmoType.Bolt;
                Player.AddRecoil(1f, 0f);
                return;
            }

            Effects.MuzzleFlash(Model, new Vector3(0f, 0.03f, 0.6f));
            Vector3 muzzle = Model.TransformPoint(new Vector3(0f, 0.03f, 0.6f));
            for (int i = 0; i < Mathf.Max(1, Data.Pellets); i++)
            {
                Vector3 dir = Spread(Aim, spread);
                Vector3 end = EyePos + dir * 300f;
                if (PhysicsUtil.Raycast(EyePos, dir, 300f, Player.transform, out var hit))
                {
                    end = hit.point;
                    float falloff = Data.Pellets > 1 ? Mathf.Clamp01(1.4f - hit.distance / 25f) : 1f;
                    if (Health.Apply(hit, Data.Damage * falloff, dir, Player.gameObject, Data.Damage * 0.05f, Data.Id))
                    {
                        Effects.Blood(hit.point, -dir);
                    }
                    else
                    {
                        Effects.Dust(hit.point, hit.normal);
                        var harvest = hit.collider.GetComponentInParent<Harvestable>();
                        if (harvest != null) harvest.Hit(0.2f, hit.point, Player.gameObject);
                    }
                }
                if (i < 3) Effects.Tracer(muzzle, end, new Color(1f, 0.85f, 0.5f));
            }
            float kick = Mathf.Clamp(Data.Damage * Mathf.Max(1, Data.Pellets) / 25f, 0.6f, 6f);
            Player.AddRecoil(kick * (Aiming ? 0.6f : 1f), Random.Range(-0.4f, 0.4f) * kick);
            Player.Shake(Mathf.Min(0.4f, kick * 0.05f));
        }
    }
}
