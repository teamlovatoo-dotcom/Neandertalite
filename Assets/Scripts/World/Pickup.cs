using UnityEngine;

namespace Neanderthal
{
    public enum PickupKind { Weapon, Ammo, Resource, Coins, Armor, Health }

    /// <summary>Walk-over pickup (Half-Life style). Optionally respawns, which duel weapon spawns use.</summary>
    public class Pickup : MonoBehaviour
    {
        public PickupKind Kind;
        public string WeaponId;
        public AmmoType Ammo;
        public Resource Resource;
        public int Amount = 1;
        public float Respawn;           // seconds, 0 = one-shot
        public bool Animate = true;
        public float Radius = 1.4f;

        Transform visual;
        Vector3 basePos;
        float hiddenUntil = -1f;
        float spawnTime;

        public static Pickup Create(Transform parent, Vector3 pos, PickupKind kind, string weaponId = null,
            AmmoType ammo = AmmoType.None, Resource res = Resource.Wood, int amount = 1, float respawn = 0f)
        {
            var go = new GameObject("Pickup_" + kind);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var p = go.AddComponent<Pickup>();
            p.Kind = kind;
            p.WeaponId = weaponId;
            p.Ammo = ammo;
            p.Resource = res;
            p.Amount = amount;
            p.Respawn = respawn;
            p.visual = BuildVisual(go.transform, p);
            return p;
        }

        public static Pickup Weapon(Transform parent, Vector3 pos, string id, float respawn = 0f)
            => Create(parent, pos, PickupKind.Weapon, weaponId: ItemDatabase.Resolve(id), respawn: respawn);

        public static Pickup AmmoBox(Transform parent, Vector3 pos, AmmoType type, int amount, float respawn = 0f)
            => Create(parent, pos, PickupKind.Ammo, ammo: AmmoType.Arrow, amount: amount, respawn: respawn);   // arrows are the only ammo now

        public static Pickup Res(Transform parent, Vector3 pos, Resource r, int amount)
            => Create(parent, pos, PickupKind.Resource, res: r, amount: amount);

        public static Pickup Coins(Transform parent, Vector3 pos, int amount)
            => Create(parent, pos, PickupKind.Coins, amount: amount);

        /// <summary>Turn an arrow/spear stuck in the world into something the player can collect.</summary>
        public static void MakeRecoverable(GameObject go, string weaponId, AmmoType ammo, int amount)
        {
            var p = go.AddComponent<Pickup>();
            p.Kind = weaponId != null ? PickupKind.Weapon : PickupKind.Ammo;
            p.WeaponId = weaponId;
            p.Ammo = ammo;
            p.Amount = amount;
            p.Animate = false;
            p.Radius = 1.6f;
            p.visual = go.transform;
            p.spawnTime = Time.time;
        }

        void Start()
        {
            basePos = visual != null ? visual.localPosition : Vector3.zero;
            if (spawnTime == 0f) spawnTime = Time.time;
        }

        void Update()
        {
            if (hiddenUntil > 0f)
            {
                if (Time.time >= hiddenUntil)
                {
                    hiddenUntil = -1f;
                    if (visual != null) visual.gameObject.SetActive(true);
                }
                return;
            }

            if (Animate && visual != null)
            {
                visual.localPosition = basePos + Vector3.up * (0.12f + Mathf.Sin(Time.time * 2.2f + transform.position.x) * 0.06f);
                visual.Rotate(0f, (Kind == PickupKind.Health ? 35f : 70f) * Time.deltaTime, 0f, Space.World);
            }

            var player = PlayerController.Instance;
            if (player == null || player.Health.IsDead) return;
            // Freshly thrown spears shouldn't be re-collected instantly.
            if (!Animate && Time.time - spawnTime < 0.6f) return;
            Vector3 d = player.transform.position - transform.position;
            float dy = d.y;
            d.y = 0f;
            if (d.magnitude < Radius && dy > -2.2f && dy < 1.5f) Collect(player);
        }

        void Collect(PlayerController player)
        {
            var inv = player.Inventory;
            switch (Kind)
            {
                case PickupKind.Weapon:
                    inv.AddWeapon(WeaponId);
                    Sfx.Play2D("pickup", 0.8f, 0.8f);
                    break;
                case PickupKind.Ammo:
                    inv.AddAmmo(Ammo, Amount);
                    Sfx.Play2D("pickup", 0.6f);
                    break;
                case PickupKind.Resource:
                    inv.Add(Resource, Amount);
                    Sfx.Play2D("pickup", 0.6f, 1.2f);
                    break;
                case PickupKind.Coins:
                    Profile.Current.Earn(Amount);
                    Inventory.Notify($"+{Amount} монет", new Color(1f, 0.85f, 0.3f));
                    Sfx.Play2D("coin", 0.8f);
                    break;
                case PickupKind.Health:
                    if (player.Health.Current >= player.Health.Max) return;
                    player.Health.Heal(Amount);
                    Inventory.Notify($"+{Amount} здоров'я", new Color(0.5f, 1f, 0.5f));
                    Sfx.Play2D("pickup", 0.8f, 0.7f);
                    break;
                case PickupKind.Armor:
                    if (player.Health.Armor >= player.Health.MaxArmor) return;
                    player.Health.AddArmor(Amount);
                    Inventory.Notify($"+{Amount} броні", new Color(0.5f, 0.75f, 1f));
                    Sfx.Play2D("pickup", 0.8f, 0.6f);
                    break;
            }
            GameEvents.RaisePickup(this);

            if (Respawn > 0f)
            {
                hiddenUntil = Time.time + Respawn;
                if (visual != null) visual.gameObject.SetActive(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        static Transform BuildVisual(Transform parent, Pickup p)
        {
            var root = new GameObject("Visual").transform;
            root.SetParent(parent, false);
            switch (p.Kind)
            {
                case PickupKind.Weapon:
                {
                    var m = WeaponModels.Build(p.WeaponId, root, out var bs).transform;
                    if (bs != null) bs.ShowArrow = false;
                    var data = ItemDatabase.Weapon(p.WeaponId);
                    float len = data != null && (data.Kind == WeaponKind.Spear) ? 0.6f : 1f;
                    m.localScale = Vector3.one * len;
                    m.localRotation = Quaternion.Euler(0f, 0f, 70f);
                    m.localPosition = new Vector3(0f, 0.25f, 0f);
                    break;
                }
                case PickupKind.Ammo:
                {
                    if (p.Ammo == AmmoType.Stone)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            var st = WeaponModels.Stone(root).transform;
                            st.localPosition = new Vector3(Mathf.Cos(i * 1.3f) * 0.08f * (i % 3), 0.04f + (i == 4 ? 0.05f : 0f), Mathf.Sin(i * 1.3f) * 0.08f * (i % 3));
                            st.localRotation = Quaternion.Euler(i * 37f, i * 71f, 0f);
                        }
                    }
                    else if (p.Ammo == AmmoType.Arrow || p.Ammo == AmmoType.Bolt)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var a = WeaponModels.Arrow(root, p.Ammo == AmmoType.Bolt).transform;
                            a.localPosition = new Vector3((i - 1.5f) * 0.05f, 0.1f, 0.3f);
                            a.localRotation = Quaternion.Euler(0f, (i - 1.5f) * 6f, 0f);
                        }
                        Shapes.Stick(root, new Vector3(0, 0.1f, -0.25f), new Vector3(0, 0.1f, -0.05f), 0.16f, Mat.Get(Mat.Leather));
                    }
                    else
                    {
                        Shapes.Box(root, new Vector3(0, 0.15f, 0), new Vector3(0.35f, 0.22f, 0.22f), Mat.Get(new Color(0.25f, 0.3f, 0.2f)), null, false);
                        Shapes.Box(root, new Vector3(0, 0.27f, 0), new Vector3(0.36f, 0.03f, 0.23f), Mat.Get(Mat.MetalLight, 0.5f, 0.6f), null, false);
                    }
                    break;
                }
                case PickupKind.Resource:
                    BuildResource(root, p.Resource);
                    break;
                case PickupKind.Coins:
                    Shapes.Ball(root, new Vector3(0, 0.15f, 0), new Vector3(0.25f, 0.22f, 0.25f), Mat.Get(Mat.Leather), false);
                    for (int i = 0; i < 4; i++)
                        Shapes.Cyl(root, new Vector3(Mathf.Cos(i * 1.6f) * 0.12f, 0.28f + i * 0.02f, Mathf.Sin(i * 1.6f) * 0.12f),
                            new Vector3(0.09f, 0.008f, 0.09f), Mat.Get(new Color(0.95f, 0.75f, 0.25f), 0.7f, 0.9f), Quaternion.Euler(20f * i, 0, 15f), false);
                    break;
                case PickupKind.Health:
                    if (ModelKit.Spawn("Models/Props/medkit", root, out _) != null) break;   // the author's 30 cm medkit
                    BuildResource(root, Resource.Herbs);
                    Shapes.Box(root, new Vector3(0, 0.55f, 0), new Vector3(0.28f, 0.08f, 0.08f), Mat.Emissive(new Color(0.4f, 1f, 0.4f), 1.5f), null, false);
                    Shapes.Box(root, new Vector3(0, 0.55f, 0), new Vector3(0.08f, 0.28f, 0.08f), Mat.Emissive(new Color(0.4f, 1f, 0.4f), 1.5f), null, false);
                    break;
                case PickupKind.Armor:
                    Shapes.Box(root, new Vector3(0, 0.3f, 0), new Vector3(0.4f, 0.5f, 0.15f), Mat.Get(new Color(0.2f, 0.25f, 0.3f), 0.3f, 0.4f), null, false);
                    break;
            }
            return root;
        }

        public static void BuildResource(Transform root, Resource r)
        {
            switch (r)
            {
                case Resource.Wood:
                    for (int i = 0; i < 3; i++)
                        Shapes.Stick(root, new Vector3(-0.35f, 0.06f + i * 0.05f, (i - 1) * 0.07f), new Vector3(0.35f, 0.06f + i * 0.03f, (i - 1) * 0.09f), 0.07f, Mat.Get(Mat.Bark));
                    break;
                case Resource.Stone:
                    Shapes.Ball(root, new Vector3(0, 0.12f, 0), new Vector3(0.3f, 0.2f, 0.25f), Mat.Get(Mat.Flint, 0.3f), false);
                    Shapes.Ball(root, new Vector3(0.15f, 0.08f, 0.1f), new Vector3(0.16f, 0.12f, 0.14f), Mat.Get(Mat.Stone), false);
                    break;
                case Resource.Bone:
                    Shapes.Stick(root, new Vector3(-0.25f, 0.06f, 0), new Vector3(0.25f, 0.06f, 0.05f), 0.05f, Mat.Get(Mat.Bone));
                    Shapes.Ball(root, new Vector3(-0.27f, 0.06f, 0), Vector3.one * 0.09f, Mat.Get(Mat.Bone), false);
                    Shapes.Ball(root, new Vector3(0.27f, 0.06f, 0.05f), Vector3.one * 0.09f, Mat.Get(Mat.Bone), false);
                    break;
                case Resource.Hide:
                    Shapes.Box(root, new Vector3(0, 0.05f, 0), new Vector3(0.6f, 0.05f, 0.45f), Mat.Get(Mat.Fur), Quaternion.Euler(0, 15, 3), false);
                    Shapes.Box(root, new Vector3(0, 0.09f, 0), new Vector3(0.4f, 0.05f, 0.3f), Mat.Get(Mat.FurDark), Quaternion.Euler(0, -10, -2), false);
                    break;
                case Resource.MetalParts:
                    Shapes.Box(root, new Vector3(0, 0.1f, 0), new Vector3(0.3f, 0.12f, 0.2f), Mat.Get(Mat.Metal, 0.5f, 0.8f), Quaternion.Euler(0, 20, 0), false);
                    Shapes.Cyl(root, new Vector3(0.1f, 0.2f, 0), new Vector3(0.08f, 0.06f, 0.08f), Mat.Emissive(Mat.TowerGlow, 1.5f), null, false);
                    break;
                case Resource.Herbs:
                    Shapes.Ball(root, new Vector3(0, 0.15f, 0), new Vector3(0.35f, 0.25f, 0.35f), Mat.Get(Mat.Leaves), false);
                    for (int i = 0; i < 5; i++)
                        Shapes.Ball(root, new Vector3(Mathf.Cos(i * 1.3f) * 0.14f, 0.22f, Mathf.Sin(i * 1.3f) * 0.14f), Vector3.one * 0.06f, Mat.Get(new Color(0.75f, 0.08f, 0.1f), 0.6f), false);
                    break;
            }
        }
    }
}
