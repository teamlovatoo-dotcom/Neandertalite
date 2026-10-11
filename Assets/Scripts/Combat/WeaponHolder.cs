using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Owns the first-person viewmodel: weapon switching (1-5 / scroll), view bob, sway and FOV zoom.
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

        public void Init(PlayerController player)
        {
            Player = player;
            ViewRoot = new GameObject("ViewModel").transform;
            ViewRoot.SetParent(player.Cam.transform, false);
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

        List<string> Ordered()
        {
            var list = new List<string>(Player.Inventory.Weapons);
            if (!list.Contains("fists")) list.Insert(0, "fists");
            list.Sort((a, b) =>
            {
                var da = ItemDatabase.Weapon(a); var db = ItemDatabase.Weapon(b);
                int c = da.Slot.CompareTo(db.Slot);
                return c != 0 ? c : string.CompareOrdinal(a, b);
            });
            return list;
        }

        void HandleSwitchInput()
        {
            var list = Ordered();
            if (list.Count == 0) return;
            string cur = pendingSwitch ?? CurrentId;
            for (int slot = 1; slot <= 5; slot++)
            {
                if (!GameInput.SlotDown(slot)) continue;
                var inSlot = list.FindAll(w => ItemDatabase.Weapon(w).Slot == slot);
                if (inSlot.Count == 0) { Sfx.Play2D("deny", 0.4f); return; }
                int idx = inSlot.IndexOf(cur);
                SwitchTo(inSlot[(idx + 1) % inSlot.Count]);
                Sfx.Play2D("click");
                return;
            }
            float scroll = GameInput.Scroll;
            if (scroll != 0f)
            {
                int idx = Mathf.Max(0, list.IndexOf(cur));
                idx = (idx - (int)scroll + list.Count) % list.Count;
                SwitchTo(list[idx]);
                Sfx.Play2D("click");
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

            if (Current != null && switchAnim < 0.3f) Current.Tick(dt, inputOk);

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
            Player.LookScale = Mathf.Clamp(Player.Cam.fieldOfView / Mathf.Max(1f, baseFov), 0.2f, 1f);
            // Hide the viewmodel through a scope.
            bool scoped = Current != null && Current.Aiming && Current.Data.ZoomFov > 0 && Current.Data.ZoomFov < 30f;
            foreach (Transform c in ViewRoot) c.gameObject.SetActive(!scoped);
        }

        public bool Scoped => Current != null && Current.Aiming && Current.Data.ZoomFov > 0 && Current.Data.ZoomFov < 30f;
    }
}
