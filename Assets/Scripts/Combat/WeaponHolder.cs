using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Owns the first-person viewmodel: weapon switching (hotbar keys 1-9 / scroll), view bob, sway and FOV zoom.
    /// Lives on the player; the actual weapon behaviours are plain classes in Weapons.cs.
    /// </summary>
    public class WeaponHolder : MonoBehaviour
    {
        public PlayerController Player;
        public Transform ViewRoot;      // child of camera, animated
        [System.NonSerialized] public Weapon Current;
        public string CurrentId => Current?.Data.Id;

        // Rounds currently loaded in each weapon's magazine, kept while holstered.
        readonly Dictionary<string, int> loaded = new Dictionary<string, int>();

        Vector3 swayPos;
        float bobPhase;
        float sprintBlend, landKick, airTime;
        bool wasGrounded = true;
        Vector3 tiltRot;
        float switchAnim;               // 1 = fully lowered
        string pendingSwitch;
        float baseFov = 80f;
        float hitStopT;

        /// <summary>Freeze the weapon's swing for a moment when it connects.</summary>
        public void HitStop(float seconds) => hitStopT = Mathf.Max(hitStopT, seconds);
        public const float ViewScale = 0.3f;

        public void Init(PlayerController player)
        {
            Player = player;
            // The viewmodel is shrunk around the camera: it looks exactly the same on screen (uniform scaling about the
            // eye doesn't change the perspective image) but now fits inside the player's body, so it never pokes
            // through walls, trees or rocks.
            var shrink = new GameObject("ViewModelScale").transform;
            shrink.SetParent(player.Cam.transform, false);
            shrink.localScale = Vector3.one * ViewScale;
            ViewRoot = new GameObject("ViewModel").transform;
            ViewRoot.SetParent(shrink, false);
            // The player's own rigged arms reach for the weapon's grips (falls back to the old hand meshes if missing).
            FirstPersonArms.Create(shrink, this);
            player.Inventory.WeaponAdded += OnWeaponAdded;
        }

        void OnWeaponAdded(string id)
        {
            // Auto-equip newly found weapons (except while busy).
            if (Current == null || Current.Data.Id == "fists" || Current.CanHolster) SwitchTo(id);
        }

        public int GetLoaded(string id, int fallback)
        {
            if (!loaded.TryGetValue(id, out int v)) { v = fallback; loaded[id] = v; }
            return v;
        }

        public void SetLoaded(string id, int v) => loaded[id] = v;

        public void ClearLoaded() => loaded.Clear();

        public void SwitchTo(string id)
        {
            if (!Player.Inventory.HasWeapon(id) && id != "fists") return;
            if (Current != null && Current.Data.Id == id) return;
            pendingSwitch = id;
        }

        public void ForceEquip(string id)
        {
            pendingSwitch = null;
            switchAnim = 0f;
            Equip(id);
        }

        void Equip(string id)
        {
            Current?.Holster();
            foreach (Transform c in ViewRoot) Destroy(c.gameObject);
            var data = ItemDatabase.Weapon(id) ?? ItemDatabase.Weapon("fists");
            Current = Weapon.Create(data, this);
            Current.Equip();
        }

        /// <summary>Called by a weapon that consumed itself (thrown spear).</summary>
        public void OnWeaponConsumed()
        {
            string next = BestWeapon();
            ForceEquip(next);
            switchAnim = 1f;
        }

        public string BestWeapon()
        {
            var inv = Player.Inventory.Weapons;
            for (int i = inv.Count - 1; i >= 0; i--)
                if (inv[i] != "fists") return inv[i];
            return "fists";
        }

        /// <summary>Keys 1-9 pick that hotbar cell; the mouse wheel steps through the weapons you carry.</summary>
        void HandleSwitchInput()
        {
            var inv = Player.Inventory;
            inv.EnsureHotbar();
            var bar = inv.Hotbar;
            string cur = pendingSwitch ?? CurrentId;
            for (int slot = 1; slot <= Inventory.HotbarSize; slot++)
            {
                if (!GameInput.SlotDown(slot)) continue;
                int i = slot - 1;
                if (i >= bar.Count || !inv.Holds(bar[i])) { Sfx.Play2D("deny", 0.4f); return; }
                if (bar[i] != cur) { SwitchTo(bar[i]); Sfx.Play2D("click"); }
                return;
            }
            float scroll = GameInput.Scroll;
            if (scroll != 0f)
            {
                int n = bar.Count;
                int idx = Mathf.Max(0, bar.IndexOf(cur));
                for (int step = 1; step <= n; step++)
                {
                    int j = ((idx - (int)scroll * step) % n + n) % n;
                    if (!inv.Holds(bar[j]) || bar[j] == cur) continue;
                    SwitchTo(bar[j]);
                    Sfx.Play2D("click");
                    break;
                }
            }
        }

        void Update()
        {
            if (Player == null || Player.Health.IsDead)
            {
                if (ViewRoot != null) ViewRoot.gameObject.SetActive(false);
                return;
            }
            ViewRoot.gameObject.SetActive(true);
            float dt = Time.deltaTime;

            if (Current == null) Equip(Player.Inventory.Weapons.Count > 0 ? BestWeapon() : "fists");

            bool inputOk = !Player.InputLocked;
            if (inputOk) HandleSwitchInput();

            // Lower the current weapon, swap, raise the new one.
            if (pendingSwitch != null && Current != null && Current.CanHolster)
            {
                switchAnim = Mathf.MoveTowards(switchAnim, 1f, dt * 6f);
                if (switchAnim >= 1f)
                {
                    Equip(pendingSwitch);
                    pendingSwitch = null;
                }
            }
            else
            {
                switchAnim = Mathf.MoveTowards(switchAnim, 0f, dt * 5f);
            }

            float tickDt = dt;
            if (hitStopT > 0f) { hitStopT -= dt; tickDt = 0f; }
            if (Current != null && switchAnim < 0.3f) Current.Tick(tickDt, inputOk);

            Animate(dt);
            UpdateFov(dt);
        }

        void Animate(float dt)
        {
            Vector3 hv = Player.Velocity; hv.y = 0f;
            float speed = hv.magnitude;
            float bobAmount = Player.Grounded ? Mathf.Clamp01(speed / 8f) : 0f;
            bobPhase += dt * (Player.IsSprinting ? 13f : 9f) * Mathf.Clamp01(speed / 2f);
            Vector3 bob = new Vector3(Mathf.Cos(bobPhase) * 0.012f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.016f, 0f) * bobAmount;

            Vector2 look = Player.InputLocked ? Vector2.zero : GameInput.Look;
            Vector3 targetSway = new Vector3(-look.x, -look.y, 0f) * 0.004f;
            swayPos = Vector3.Lerp(swayPos, Vector3.ClampMagnitude(targetSway, 0.04f), dt * 8f);

            Vector3 basePos = Current != null ? Current.HipPosition : new Vector3(0.25f, -0.25f, 0.5f);
            Quaternion baseRot = Current != null ? Quaternion.Euler(Current.HipEuler) : Quaternion.identity;
            if (Current != null && Current.Aiming) bob *= 0.2f;

            // Sprint pose: weapon swung down and across, like modern shooters.
            bool sprintPose = Player.IsSprinting && Current != null && !Current.Busy && !Current.Aiming && speed > 3f;
            sprintBlend = Mathf.MoveTowards(sprintBlend, sprintPose ? 1f : 0f, dt * 5f);
            float sb = Mathf.SmoothStep(0f, 1f, sprintBlend);
            Vector3 sprintPos = new Vector3(-0.04f, -0.07f, -0.04f) * sb;
            Vector3 sprintRot = new Vector3(14f, -26f, 10f) * sb;

            // Landing dip and a little lift while in the air.
            if (!Player.Grounded) airTime += dt;
            if (Player.Grounded && !wasGrounded && airTime > 0.15f) landKick = Mathf.Clamp01(airTime * 1.4f);
            if (Player.Grounded) airTime = 0f;
            wasGrounded = Player.Grounded;
            landKick = Mathf.MoveTowards(landKick, 0f, dt * 3.5f);
            float land = Mathf.Sin(landKick * Mathf.PI) * 0.06f;
            Vector3 airPos = new Vector3(0f, Player.Grounded ? 0f : 0.015f, 0f) - new Vector3(0f, land, 0f);

            // Breathing idle and strafe roll.
            float t = Time.time;
            Vector3 breathe = new Vector3(Mathf.Sin(t * 0.9f) * 0.002f, Mathf.Sin(t * 1.7f) * 0.004f, 0f) * (Current != null && Current.Aiming ? 0.3f : 1f);
            Vector3 local = Player.transform.InverseTransformDirection(hv);
            tiltRot = Vector3.Lerp(tiltRot, new Vector3(local.z * 0.25f, 0f, -local.x * 0.9f), dt * 6f);

            Vector3 lowered = new Vector3(0f, -0.45f * switchAnim, 0f);
            Vector3 animPos = Current != null ? Current.AnimPos : Vector3.zero;
            Vector3 animRot = Current != null ? Current.AnimEuler : Vector3.zero;
            Vector3 crouch = Player.Crouching ? new Vector3(0.01f, -0.015f, 0f) : Vector3.zero;

            ViewRoot.localPosition = basePos + bob + swayPos + sprintPos + airPos + breathe + crouch + lowered + animPos;
            ViewRoot.localRotation = baseRot * Quaternion.Euler(animRot + sprintRot + tiltRot + new Vector3(switchAnim * 40f + land * 60f, 0f, 0f));
        }

        void UpdateFov(float dt)
        {
            baseFov = Profile.Current.Fov;
            float target = baseFov;
            if (Current != null)
            {
                if (Current.Aiming && Current.Data.ZoomFov > 0) target = Current.Data.ZoomFov;
                else target = baseFov - Current.ZoomDegrees;
            }
            if (Player.IsSprinting) target += 5f;
            Player.Cam.fieldOfView = Mathf.Lerp(Player.Cam.fieldOfView, target, dt * 10f);
            // Scale the mouse by how much the view is magnified (ratio of tangents), so a target under the crosshair
            // moves across the screen at the same speed with and without zoom.
            float zoomRatio = Mathf.Tan(Player.Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(Mathf.Max(1f, baseFov) * 0.5f * Mathf.Deg2Rad);
            Player.LookScale = Mathf.Clamp(zoomRatio, 0.15f, 1f);
            // Hide the viewmodel through a scope.
            bool scoped = Current != null && Current.Aiming && Current.Data.ZoomFov > 0 && Current.Data.ZoomFov < 30f;
            foreach (Transform c in ViewRoot) c.gameObject.SetActive(!scoped);
        }

        public bool Scoped => Current != null && Current.Aiming && Current.Data.ZoomFov > 0 && Current.Data.ZoomFov < 30f;
    }
}
