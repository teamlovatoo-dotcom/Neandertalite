using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    public enum Tier { Primitive, Improved, Modern }
    public enum WeaponKind { Melee, Spear, Bow, Firearm, Throw }
    public enum AmmoType { None, Arrow, Bolt, PistolRound, RifleRound, Buckshot, Stone }
    public enum Resource { Wood, Stone, Bone, Hide, MetalParts, Herbs }

    /// <summary>Static description of a weapon. Plain data so it can later move to ScriptableObjects.</summary>
    public class WeaponData
    {
        public string Id;
        public string Name;
        public WeaponKind Kind;
        public Tier Tier;
        public int Slot;                 // 1 melee, 2 spear/primitive ranged, 3 bows, 4 pistol, 5 rifles
        public float Damage;
        public float Range = 2f;         // melee reach
        public float Cooldown = 0.5f;    // seconds between attacks/shots
        public float StaminaCost;        // melee swings cost stamina, like the concept HUD shows
        public AmmoType Ammo;
        public int MagSize;              // 0 = fed directly from reserve (bows)
        public float ReloadTime;
        public float ProjectileSpeed;    // 0 = hitscan
        public float Spread;             // degrees
        public int Pellets = 1;
        public bool Automatic;
        public float ChargeTime;         // bows: time to full draw
        public float ZoomFov;            // 0 = no zoom
        public float HarvestPower = 1f;  // multiplier when chopping trees / breaking rocks
        public float LightRange;         // torches
        public string Sound = "swing";
        public string Description;
    }

    public class Recipe
    {
        public string Id;
        public string Name;
        public string Category;          // Stone Tools, Bone Weapons, Fire, Clothing, Shelter, Workbench
        public bool NeedsWorkbench;
        public string RequiredBlueprint; // null when always known
        public Dictionary<Resource, int> Cost = new Dictionary<Resource, int>();
        // Result: exactly one of these is used.
        public string GivesWeapon;
        public AmmoType GivesAmmo;
        public int AmmoAmount;
        public int GivesArmor;
        public string Places;            // "campfire" or "shelter"
        public string Description;
    }

    public enum ShopKind { Blueprint, Weapon, Ammo, Resource, Armor, SellResource }

    public class ShopEntry
    {
        public string Id;
        public string Name;
        public string Tab;               // BLUEPRINTS, TOOLS, WEAPONS, GEAR, MISC, SELL
        public ShopKind Kind;
        public int Price;
        public string WeaponId;
        public AmmoType Ammo;
        public Resource Resource;
        public int Amount = 1;
        public string Description;
    }

    public static class ItemDatabase
    {
        public static readonly Dictionary<string, WeaponData> Weapons = new Dictionary<string, WeaponData>();
        public static readonly List<Recipe> Recipes = new List<Recipe>();
        public static readonly List<ShopEntry> Shop = new List<ShopEntry>();
        /// <summary>
        /// Modern weapons (blueprints, workbench, guns) are fully implemented but stay locked for now:
        /// the game currently plays in the stone age only. Flip to true to open them.
        /// </summary>
        public static readonly bool ModernWeapons = false;

        public static readonly string[] ShopTabs = ModernWeapons
            ? new[] { "КРЕСЛЕННЯ", "ІНСТРУМЕНТИ", "ЗБРОЯ", "СПОРЯДЖЕННЯ", "РІЗНЕ", "ПРОДАЖ" }
            : new[] { "ЗБРОЯ", "ПОКРАЩЕННЯ", "ІНСТРУМЕНТИ", "СПОРЯДЖЕННЯ", "ПРОДАЖ" };

        static bool built;

        public static WeaponData Weapon(string id)
        {
            Build();
            return Weapons.TryGetValue(id, out var w) ? w : null;
        }

        public static string AmmoName(AmmoType a)
        {
            switch (a)
            {
                case AmmoType.Arrow: return "Стріли";
                case AmmoType.Bolt: return "Болти";
                case AmmoType.PistolRound: return "Пістолетні набої";
                case AmmoType.RifleRound: return "Гвинтівкові набої";
                case AmmoType.Buckshot: return "Шріт";
                case AmmoType.Stone: return "Камені";
                default: return "";
            }
        }

        public static string ResourceName(Resource r)
        {
            switch (r)
            {
                case Resource.Wood: return "Деревина";
                case Resource.Stone: return "Камінь";
                case Resource.Bone: return "Кістка";
                case Resource.Hide: return "Шкура";
                case Resource.MetalParts: return "Металеві деталі";
                default: return "Цілющі трави";
            }
        }

        public static void Build()
        {
            if (built) return;
            built = true;

            // ---------- PRIMITIVE ----------
            Add(new WeaponData { Id = "fists", Name = "Кулаки", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 8, Range = 1.6f, Cooldown = 0.45f, StaminaCost = 4, HarvestPower = 0.25f, Description = "Краще, ніж нічого." });
            Add(new WeaponData { Id = "stick", Name = "Палка", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 15, Range = 2.1f, Cooldown = 0.5f, StaminaCost = 5, HarvestPower = 0.4f, Description = "Перша зброя неандертальця. Довга, легка, б'є недалеко від кийка." });
            Add(new WeaponData { Id = "stone", Name = "Камінь", Kind = WeaponKind.Throw, Tier = Tier.Primitive, Slot = 2, Damage = 24, Cooldown = 0.35f, StaminaCost = 3, Ammo = AmmoType.Stone, ProjectileSpeed = 26f, ChargeTime = 0.45f, Description = "Затисни ЛКМ — замах, відпусти — кидок. Кинуті камені можна підібрати." });
            Add(new WeaponData { Id = "slingshot", Name = "Рогатка", Kind = WeaponKind.Bow, Tier = Tier.Primitive, Slot = 3, Damage = 34, Cooldown = 0.25f, Ammo = AmmoType.Stone, ProjectileSpeed = 44f, ChargeTime = 0.5f, Sound = "bow", Description = "Стріляє камінцями: далі й точніше за кидок рукою. Затисни ЛКМ — натягнути, відпусти — постріл." });
            Add(new WeaponData { Id = "knife", Name = "Кам'яний ніж", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 24, Range = 1.8f, Cooldown = 0.38f, StaminaCost = 5, HarvestPower = 0.5f, Description = "Швидке кремінне лезо. Знімає з дичини додаткову шкуру." });
            Add(new WeaponData { Id = "obsidian_knife", Name = "Обсидіановий ніж", Kind = WeaponKind.Melee, Tier = Tier.Improved, Slot = 1, Damage = 36, Range = 1.85f, Cooldown = 0.34f, StaminaCost = 5, HarvestPower = 0.6f, Description = "Лезо з чорного вулканічного скла — гостріше за будь-який кремінь." });
            Add(new WeaponData { Id = "bone_dagger", Name = "Кістяний кинджал", Kind = WeaponKind.Melee, Tier = Tier.Improved, Slot = 1, Damage = 34, Range = 1.9f, Cooldown = 0.35f, StaminaCost = 5, HarvestPower = 0.5f, Description = "Загострена кістка мамонта." });
            Add(new WeaponData { Id = "club", Name = "Кийок", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 40, Range = 2.1f, Cooldown = 0.85f, StaminaCost = 12, HarvestPower = 1f, Description = "Важкий, повільний, приголомшує." });
            Add(new WeaponData { Id = "axe", Name = "Кам'яна сокира", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 38, Range = 2.0f, Cooldown = 0.7f, StaminaCost = 10, HarvestPower = 2.5f, Description = "Швидко рубає дерева. Розколює черепи." });
            Add(new WeaponData { Id = "obsidian_axe", Name = "Обсидіанова сокира", Kind = WeaponKind.Melee, Tier = Tier.Improved, Slot = 1, Damage = 50, Range = 2.05f, Cooldown = 0.65f, StaminaCost = 10, HarvestPower = 3.2f, Description = "Важке обсидіанове лезо. Рубає швидше й глибше." });
            Add(new WeaponData { Id = "torch", Name = "Смолоскип", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 15, Range = 1.9f, Cooldown = 0.6f, StaminaCost = 6, HarvestPower = 0.3f, LightRange = 14f, Description = "Світло для печер. Звірі його бояться." });
            Add(new WeaponData { Id = "spear", Name = "Спис", Kind = WeaponKind.Spear, Tier = Tier.Primitive, Slot = 2, Damage = 32, Range = 2.7f, Cooldown = 0.65f, StaminaCost = 8, ProjectileSpeed = 28f, HarvestPower = 0.4f, Description = "ЛКМ — удар. ПКМ — затисни й відпусти, щоб кинути (70 шкоди). Потім підбери!" });
            Add(new WeaponData { Id = "obsidian_spear", Name = "Обсидіановий спис", Kind = WeaponKind.Spear, Tier = Tier.Improved, Slot = 2, Damage = 44, Range = 2.75f, Cooldown = 0.6f, StaminaCost = 8, ProjectileSpeed = 32f, HarvestPower = 0.5f, Description = "ЛКМ — удар. ПКМ — кидок (до 95 шкоди). Чорний наконечник пробиває шкуру мамонта." });
            Add(new WeaponData { Id = "bow", Name = "Лук", Kind = WeaponKind.Bow, Tier = Tier.Primitive, Slot = 3, Damage = 55, Cooldown = 0.3f, Ammo = AmmoType.Arrow, ProjectileSpeed = 55f, ChargeTime = 0.9f, Sound = "bow", Description = "Затисни ЛКМ — натягнути, відпусти — постріл. ПКМ — точне прицілювання." });

            // ---------- IMPROVED / MODERN (blueprints) ----------
            Add(new WeaponData { Id = "compound_bow", Name = "Сучасний лук", Kind = WeaponKind.Bow, Tier = Tier.Improved, Slot = 3, Damage = 85, Cooldown = 0.25f, Ammo = AmmoType.Arrow, ProjectileSpeed = 80f, ChargeTime = 0.55f, ZoomFov = 50f, Sound = "bow", Description = "Водостійкий блочний лук. Швидший натяг, пряміша траєкторія." });
            Add(new WeaponData { Id = "crossbow", Name = "Арбалет", Kind = WeaponKind.Firearm, Tier = Tier.Improved, Slot = 3, Damage = 95, Cooldown = 0.2f, Ammo = AmmoType.Bolt, MagSize = 1, ReloadTime = 1.4f, ProjectileSpeed = 90f, Spread = 0.2f, ZoomFov = 40f, Sound = "bow", Description = "Один болт — одне вбивство. ПКМ — приціл." });
            Add(new WeaponData { Id = "pistol", Name = "Пістолет", Kind = WeaponKind.Firearm, Tier = Tier.Modern, Slot = 4, Damage = 26, Cooldown = 0.22f, Ammo = AmmoType.PistolRound, MagSize = 8, ReloadTime = 1.3f, Spread = 1.2f, Sound = "gun", Description = "Надійна зброя з дивної вежі." });
            Add(new WeaponData { Id = "shotgun", Name = "Дробовик", Kind = WeaponKind.Firearm, Tier = Tier.Modern, Slot = 5, Damage = 13, Pellets = 9, Cooldown = 0.9f, Ammo = AmmoType.Buckshot, MagSize = 6, ReloadTime = 2.2f, Spread = 6f, Sound = "shotgun", Description = "Очищає печери від ведмедів." });
            Add(new WeaponData { Id = "rifle", Name = "Автомат", Kind = WeaponKind.Firearm, Tier = Tier.Modern, Slot = 5, Damage = 22, Cooldown = 0.1f, Automatic = true, Ammo = AmmoType.RifleRound, MagSize = 30, ReloadTime = 1.9f, Spread = 1.6f, ZoomFov = 55f, Sound = "rifle", Description = "Від каменю до сталі." });
            Add(new WeaponData { Id = "sniper", Name = "Снайперська гвинтівка", Kind = WeaponKind.Firearm, Tier = Tier.Modern, Slot = 5, Damage = 130, Cooldown = 1.2f, Ammo = AmmoType.RifleRound, MagSize = 5, ReloadTime = 2.5f, Spread = 0.05f, ZoomFov = 15f, Sound = "sniper", Description = "ПКМ — оптичний приціл. Мамонти її бояться." });

            // ---------- CRAFTING (Tab anywhere / workbench) ----------
            R("r_stick", "Палка", "Кам'яні знаряддя", weapon: "stick", cost: (Resource.Wood, 1));
            R("r_stones", "Камені ×6", "Кам'яні знаряддя", ammo: AmmoType.Stone, ammoAmount: 6, cost: (Resource.Stone, 1), desc: "Для кидка рукою і рогатки.");
            R("r_slingshot", "Рогатка", "Кам'яні знаряддя", weapon: "slingshot", cost: (Resource.Wood, 2), cost2: (Resource.Hide, 1));
            R("r_knife", "Кам'яний ніж", "Кам'яні знаряддя", weapon: "knife", cost: (Resource.Stone, 2), cost2: (Resource.Wood, 1));
            R("r_axe", "Кам'яна сокира", "Кам'яні знаряддя", weapon: "axe", cost: (Resource.Stone, 2), cost2: (Resource.Wood, 2));
            R("r_spear", "Спис", "Кам'яні знаряддя", weapon: "spear", cost: (Resource.Wood, 2), cost2: (Resource.Stone, 1));
            R("r_club", "Кийок", "Кістяна зброя", weapon: "club", cost: (Resource.Wood, 2), cost2: (Resource.Bone, 1));
            R("r_dagger", "Кістяний кинджал", "Кістяна зброя", weapon: "bone_dagger", cost: (Resource.Bone, 3), cost2: (Resource.Hide, 1));
            R("r_bow", "Лук", "Кістяна зброя", weapon: "bow", cost: (Resource.Wood, 3), cost2: (Resource.Hide, 1));
            R("r_arrows", "Стріли ×5", "Кістяна зброя", ammo: AmmoType.Arrow, ammoAmount: 5, cost: (Resource.Wood, 1), cost2: (Resource.Stone, 1));
            R("r_bone_arrows", "Кістяні стріли ×8", "Кістяна зброя", ammo: AmmoType.Arrow, ammoAmount: 8, cost: (Resource.Wood, 1), cost2: (Resource.Bone, 1));
            R("r_obs_knife", "Обсидіановий ніж", "Покращення", weapon: "obsidian_knife", cost: (Resource.Stone, 4), cost2: (Resource.Bone, 1));
            R("r_obs_spear", "Обсидіановий спис", "Покращення", weapon: "obsidian_spear", cost: (Resource.Stone, 4), cost2: (Resource.Wood, 2));
            R("r_obs_axe", "Обсидіанова сокира", "Покращення", weapon: "obsidian_axe", cost: (Resource.Stone, 5), cost2: (Resource.Wood, 2));
            R("r_torch", "Смолоскип", "Вогонь", weapon: "torch", cost: (Resource.Wood, 1), cost2: (Resource.Hide, 1));
            R("r_campfire", "Вогнище", "Вогонь", places: "campfire", cost: (Resource.Wood, 4), cost2: (Resource.Stone, 3), desc: "Лікує, поки стоїш поруч. Стає точкою відродження.");
            R("r_clothing", "Хутряний одяг", "Одяг", armor: 25, cost: (Resource.Hide, 3), desc: "+25 броні.");
            R("r_shelter", "Притулок", "Притулок", places: "shelter", cost: (Resource.Wood, 6), cost2: (Resource.Hide, 2), desc: "Курінь. Точка відродження і +50 здоров'я під час відпочинку.");

            if (ModernWeapons) BuildModern();

            // ---------- SHOP (the trader in the cave) ----------
            Shop.Add(new ShopEntry { Id = "s_stick", Name = "Палка", Tab = "ЗБРОЯ", Kind = ShopKind.Weapon, WeaponId = "stick", Price = 10 });
            Shop.Add(new ShopEntry { Id = "s_stones", Name = "Камені ×10", Tab = "ЗБРОЯ", Kind = ShopKind.Ammo, Ammo = AmmoType.Stone, Amount = 10, Price = 10 });
            Shop.Add(new ShopEntry { Id = "s_slingshot", Name = "Рогатка", Tab = "ЗБРОЯ", Kind = ShopKind.Weapon, WeaponId = "slingshot", Price = 60 });
            Shop.Add(new ShopEntry { Id = "s_obs_knife", Name = "Обсидіановий ніж", Tab = "ПОКРАЩЕННЯ", Kind = ShopKind.Weapon, WeaponId = "obsidian_knife", Price = 160 });
            Shop.Add(new ShopEntry { Id = "s_obs_spear", Name = "Обсидіановий спис", Tab = "ПОКРАЩЕННЯ", Kind = ShopKind.Weapon, WeaponId = "obsidian_spear", Price = 220 });
            Shop.Add(new ShopEntry { Id = "s_obs_axe", Name = "Обсидіанова сокира", Tab = "ПОКРАЩЕННЯ", Kind = ShopKind.Weapon, WeaponId = "obsidian_axe", Price = 200 });
            Shop.Add(new ShopEntry { Id = "s_bone_dagger", Name = "Кістяний кинджал", Tab = "ПОКРАЩЕННЯ", Kind = ShopKind.Weapon, WeaponId = "bone_dagger", Price = 110 });
            Shop.Add(new ShopEntry { Id = "s_club", Name = "Кийок", Tab = "ІНСТРУМЕНТИ", Kind = ShopKind.Weapon, WeaponId = "club", Price = 50 });
            BuildCommonShop();
        }

        static void BuildModern()
        {
            W("w_compound_bow", "Сучасний лук", "compound_bow", "bp_compound_bow", (Resource.MetalParts, 2), (Resource.Wood, 2));
            W("w_crossbow", "Арбалет", "crossbow", "bp_crossbow", (Resource.MetalParts, 2), (Resource.Wood, 3));
            W("w_pistol", "Пістолет", "pistol", "bp_pistol", (Resource.MetalParts, 4), (Resource.Wood, 1));
            W("w_shotgun", "Дробовик", "shotgun", "bp_shotgun", (Resource.MetalParts, 6), (Resource.Wood, 2));
            W("w_rifle", "Автомат", "rifle", "bp_rifle", (Resource.MetalParts, 10), (Resource.Wood, 1));
            W("w_sniper", "Снайперська гвинтівка", "sniper", "bp_sniper", (Resource.MetalParts, 12), (Resource.Wood, 2));
            Recipes.Add(new Recipe { Id = "w_vest", Name = "Бронежилет", Category = "Верстак", NeedsWorkbench = true, RequiredBlueprint = "bp_vest", GivesArmor = 100, Cost = { { Resource.MetalParts, 5 }, { Resource.Hide, 2 } }, Description = "+100 броні." });
            Recipes.Add(new Recipe { Id = "w_bolts", Name = "Болти ×5", Category = "Верстак", NeedsWorkbench = true, RequiredBlueprint = "bp_crossbow", GivesAmmo = AmmoType.Bolt, AmmoAmount = 5, Cost = { { Resource.MetalParts, 1 }, { Resource.Wood, 1 } } });
            Recipes.Add(new Recipe { Id = "w_pistol_ammo", Name = "Пістолетні набої ×16", Category = "Верстак", NeedsWorkbench = true, RequiredBlueprint = "bp_pistol", GivesAmmo = AmmoType.PistolRound, AmmoAmount = 16, Cost = { { Resource.MetalParts, 1 } } });
            Recipes.Add(new Recipe { Id = "w_buckshot", Name = "Шріт ×8", Category = "Верстак", NeedsWorkbench = true, RequiredBlueprint = "bp_shotgun", GivesAmmo = AmmoType.Buckshot, AmmoAmount = 8, Cost = { { Resource.MetalParts, 1 } } });
            Recipes.Add(new Recipe { Id = "w_rifle_ammo", Name = "Гвинтівкові набої ×30", Category = "Верстак", NeedsWorkbench = true, RequiredBlueprint = "bp_rifle", GivesAmmo = AmmoType.RifleRound, AmmoAmount = 30, Cost = { { Resource.MetalParts, 2 } } });

            BP("bp_workbench", "Верстак", 300, "Дозволяє виготовляти сучасне спорядження на верстаку.");
            BP("bp_compound_bow", "Сучасний лук", 500, "Креслення: блочний лук.");
            BP("bp_crossbow", "Арбалет", 650, "Креслення: арбалет і болти.");
            BP("bp_pistol", "Пістолет", 750, "Креслення: пістолет і набої.");
            BP("bp_shotgun", "Дробовик", 900, "Креслення: дробовик і шріт.");
            BP("bp_rifle", "Автомат", 1200, "Креслення: автомат і набої.");
            BP("bp_sniper", "Снайперська гвинтівка", 1500, "Креслення: далекобійна гвинтівка.");
            BP("bp_vest", "Бронежилет", 600, "Креслення: бронежилет, +100 броні.");
            Shop.Add(new ShopEntry { Id = "s_metal", Name = "Металеві деталі ×2", Tab = "РІЗНЕ", Kind = ShopKind.Resource, Resource = Resource.MetalParts, Amount = 2, Price = 200 });
            Shop.Add(new ShopEntry { Id = "s_bolts", Name = "Болти ×5", Tab = "РІЗНЕ", Kind = ShopKind.Ammo, Ammo = AmmoType.Bolt, Amount = 5, Price = 60 });
            Shop.Add(new ShopEntry { Id = "s_pistol_ammo", Name = "Пістолетні набої ×16", Tab = "РІЗНЕ", Kind = ShopKind.Ammo, Ammo = AmmoType.PistolRound, Amount = 16, Price = 80 });
            Shop.Add(new ShopEntry { Id = "s_rifle_ammo", Name = "Гвинтівкові набої ×30", Tab = "РІЗНЕ", Kind = ShopKind.Ammo, Ammo = AmmoType.RifleRound, Amount = 30, Price = 140 });
            Shop.Add(new ShopEntry { Id = "s_buckshot", Name = "Шріт ×8", Tab = "РІЗНЕ", Kind = ShopKind.Ammo, Ammo = AmmoType.Buckshot, Amount = 8, Price = 90 });
        }

        static void BuildCommonShop()
        {
            Shop.Add(new ShopEntry { Id = "s_axe", Name = "Кам'яна сокира", Tab = "ІНСТРУМЕНТИ", Kind = ShopKind.Weapon, WeaponId = "axe", Price = 60 });
            Shop.Add(new ShopEntry { Id = "s_torch", Name = "Смолоскип", Tab = "ІНСТРУМЕНТИ", Kind = ShopKind.Weapon, WeaponId = "torch", Price = 30 });
            Shop.Add(new ShopEntry { Id = "s_knife", Name = "Кам'яний ніж", Tab = "ЗБРОЯ", Kind = ShopKind.Weapon, WeaponId = "knife", Price = 40 });
            Shop.Add(new ShopEntry { Id = "s_spear", Name = "Спис", Tab = "ЗБРОЯ", Kind = ShopKind.Weapon, WeaponId = "spear", Price = 70 });
            Shop.Add(new ShopEntry { Id = "s_bow", Name = "Лук", Tab = "ЗБРОЯ", Kind = ShopKind.Weapon, WeaponId = "bow", Price = 120 });
            Shop.Add(new ShopEntry { Id = "s_arrows", Name = "Стріли ×10", Tab = "ЗБРОЯ", Kind = ShopKind.Ammo, Ammo = AmmoType.Arrow, Amount = 10, Price = 40 });
            Shop.Add(new ShopEntry { Id = "s_clothing", Name = "Хутряний одяг (+25 броні)", Tab = "СПОРЯДЖЕННЯ", Kind = ShopKind.Armor, Amount = 25, Price = 90 });
            Shop.Add(new ShopEntry { Id = "s_herbs", Name = "Цілющі трави ×2", Tab = "СПОРЯДЖЕННЯ", Kind = ShopKind.Resource, Resource = Resource.Herbs, Amount = 2, Price = 50, Description = "Натисни H, щоб відновити 35 здоров'я." });
            Shop.Add(new ShopEntry { Id = "sell_hide", Name = "Продати шкуру", Tab = "ПРОДАЖ", Kind = ShopKind.SellResource, Resource = Resource.Hide, Price = 25 });
            Shop.Add(new ShopEntry { Id = "sell_bone", Name = "Продати кістку", Tab = "ПРОДАЖ", Kind = ShopKind.SellResource, Resource = Resource.Bone, Price = 15 });
            Shop.Add(new ShopEntry { Id = "sell_wood", Name = "Продати деревину", Tab = "ПРОДАЖ", Kind = ShopKind.SellResource, Resource = Resource.Wood, Price = 3 });
            Shop.Add(new ShopEntry { Id = "sell_stone", Name = "Продати камінь", Tab = "ПРОДАЖ", Kind = ShopKind.SellResource, Resource = Resource.Stone, Price = 3 });
        }

        public static ShopEntry Blueprint(string id) => Shop.Find(s => s.Kind == ShopKind.Blueprint && s.Id == id);

        /// <summary>The blueprint that unlocks a weapon (null for primitive weapons).</summary>
        public static string BlueprintForWeapon(string weaponId)
        {
            Build();
            foreach (var r in Recipes)
                if (r.GivesWeapon == weaponId && r.RequiredBlueprint != null) return r.RequiredBlueprint;
            return null;
        }

        static void Add(WeaponData w) => Weapons[w.Id] = w;

        static void R(string id, string name, string cat, (Resource, int) cost, (Resource, int)? cost2 = null,
            string weapon = null, AmmoType ammo = AmmoType.None, int ammoAmount = 0, int armor = 0, string places = null, string desc = null)
        {
            var r = new Recipe { Id = id, Name = name, Category = cat, GivesWeapon = weapon, GivesAmmo = ammo, AmmoAmount = ammoAmount, GivesArmor = armor, Places = places, Description = desc };
            r.Cost[cost.Item1] = cost.Item2;
            if (cost2.HasValue) r.Cost[cost2.Value.Item1] = cost2.Value.Item2;
            if (weapon != null && r.Description == null) r.Description = Weapons[weapon].Description;
            Recipes.Add(r);
        }

        static void W(string id, string name, string weapon, string bp, (Resource, int) c1, (Resource, int) c2)
        {
            var r = new Recipe { Id = id, Name = name, Category = "Верстак", NeedsWorkbench = true, RequiredBlueprint = bp, GivesWeapon = weapon, Description = Weapons[weapon].Description };
            r.Cost[c1.Item1] = c1.Item2;
            r.Cost[c2.Item1] = c2.Item2;
            Recipes.Add(r);
        }

        static void BP(string id, string name, int price, string desc)
            => Shop.Add(new ShopEntry { Id = id, Name = name, Tab = "КРЕСЛЕННЯ", Kind = ShopKind.Blueprint, Price = price, Description = desc });
    }
}
