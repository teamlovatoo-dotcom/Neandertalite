using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    public enum Tier { Primitive, Improved, Modern }
    public enum WeaponKind { Melee, Spear, Bow, Firearm }
    public enum AmmoType { None, Arrow, Bolt, PistolRound, RifleRound, Buckshot }
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
        public static readonly string[] ShopTabs = { "BLUEPRINTS", "TOOLS", "WEAPONS", "GEAR", "MISC", "SELL" };

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
                case AmmoType.Arrow: return "Arrows";
                case AmmoType.Bolt: return "Bolts";
                case AmmoType.PistolRound: return "Pistol Rounds";
                case AmmoType.RifleRound: return "Rifle Rounds";
                case AmmoType.Buckshot: return "Buckshot";
                default: return "";
            }
        }

        public static string ResourceName(Resource r) => r == Resource.MetalParts ? "Metal Parts" : r.ToString();

        public static void Build()
        {
            if (built) return;
            built = true;

            // ---------- PRIMITIVE ----------
            Add(new WeaponData { Id = "fists", Name = "Fists", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 8, Range = 1.6f, Cooldown = 0.45f, StaminaCost = 4, HarvestPower = 0.25f, Description = "Better than nothing." });
            Add(new WeaponData { Id = "knife", Name = "Stone Knife", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 24, Range = 1.8f, Cooldown = 0.38f, StaminaCost = 5, HarvestPower = 0.5f, Description = "Fast flint blade. Skins game for extra hide." });
            Add(new WeaponData { Id = "bone_dagger", Name = "Bone Dagger", Kind = WeaponKind.Melee, Tier = Tier.Improved, Slot = 1, Damage = 34, Range = 1.9f, Cooldown = 0.35f, StaminaCost = 5, HarvestPower = 0.5f, Description = "Sharpened mammoth bone." });
            Add(new WeaponData { Id = "club", Name = "Club", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 40, Range = 2.1f, Cooldown = 0.85f, StaminaCost = 12, HarvestPower = 1f, Description = "Heavy, slow, stuns." });
            Add(new WeaponData { Id = "axe", Name = "Stone Axe", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 38, Range = 2.0f, Cooldown = 0.7f, StaminaCost = 10, HarvestPower = 2.5f, Description = "Chops trees fast. Cracks skulls." });
            Add(new WeaponData { Id = "torch", Name = "Torch", Kind = WeaponKind.Melee, Tier = Tier.Primitive, Slot = 1, Damage = 15, Range = 1.9f, Cooldown = 0.6f, StaminaCost = 6, HarvestPower = 0.3f, LightRange = 14f, Description = "Light for the caves. Animals fear it." });
            Add(new WeaponData { Id = "spear", Name = "Spear", Kind = WeaponKind.Spear, Tier = Tier.Primitive, Slot = 2, Damage = 32, Range = 2.7f, Cooldown = 0.65f, StaminaCost = 8, ProjectileSpeed = 28f, HarvestPower = 0.4f, Description = "LMB: thrust. RMB: hold to throw (70 dmg). Pick it back up!" });
            Add(new WeaponData { Id = "bow", Name = "Bow", Kind = WeaponKind.Bow, Tier = Tier.Primitive, Slot = 3, Damage = 55, Cooldown = 0.3f, Ammo = AmmoType.Arrow, ProjectileSpeed = 55f, ChargeTime = 0.9f, Sound = "bow", Description = "Hold LMB to draw, release to shoot. RMB to steady aim." });

            // ---------- IMPROVED / MODERN (blueprints) ----------
            Add(new WeaponData { Id = "compound_bow", Name = "Bow (Modern)", Kind = WeaponKind.Bow, Tier = Tier.Improved, Slot = 3, Damage = 85, Cooldown = 0.25f, Ammo = AmmoType.Arrow, ProjectileSpeed = 80f, ChargeTime = 0.55f, ZoomFov = 50f, Sound = "bow", Description = "Watertight laminated bow with pulleys. Faster draw, flatter arc." });
            Add(new WeaponData { Id = "crossbow", Name = "Crossbow", Kind = WeaponKind.Firearm, Tier = Tier.Improved, Slot = 3, Damage = 95, Cooldown = 0.2f, Ammo = AmmoType.Bolt, MagSize = 1, ReloadTime = 1.4f, ProjectileSpeed = 90f, Spread = 0.2f, ZoomFov = 40f, Sound = "bow", Description = "One bolt, one kill. RMB to aim." });
            Add(new WeaponData { Id = "pistol", Name = "Pistol", Kind = WeaponKind.Firearm, Tier = Tier.Modern, Slot = 4, Damage = 26, Cooldown = 0.22f, Ammo = AmmoType.PistolRound, MagSize = 8, ReloadTime = 1.3f, Spread = 1.2f, Sound = "gun", Description = "Reliable sidearm from the strange tower." });
            Add(new WeaponData { Id = "shotgun", Name = "Shotgun", Kind = WeaponKind.Firearm, Tier = Tier.Modern, Slot = 5, Damage = 13, Pellets = 9, Cooldown = 0.9f, Ammo = AmmoType.Buckshot, MagSize = 6, ReloadTime = 2.2f, Spread = 6f, Sound = "shotgun", Description = "Clears caves of bears." });
            Add(new WeaponData { Id = "rifle", Name = "Assault Rifle", Kind = WeaponKind.Firearm, Tier = Tier.Modern, Slot = 5, Damage = 22, Cooldown = 0.1f, Automatic = true, Ammo = AmmoType.RifleRound, MagSize = 30, ReloadTime = 1.9f, Spread = 1.6f, ZoomFov = 55f, Sound = "rifle", Description = "From stone to steel." });
            Add(new WeaponData { Id = "sniper", Name = "Sniper Rifle", Kind = WeaponKind.Firearm, Tier = Tier.Modern, Slot = 5, Damage = 130, Cooldown = 1.2f, Ammo = AmmoType.RifleRound, MagSize = 5, ReloadTime = 2.5f, Spread = 0.05f, ZoomFov = 15f, Sound = "sniper", Description = "RMB: scope. Mammoths fear it." });

            // ---------- CRAFTING (Tab anywhere / workbench) ----------
            R("r_knife", "Stone Knife", "Stone Tools", weapon: "knife", cost: (Resource.Stone, 2), cost2: (Resource.Wood, 1));
            R("r_axe", "Stone Axe", "Stone Tools", weapon: "axe", cost: (Resource.Stone, 2), cost2: (Resource.Wood, 2));
            R("r_spear", "Spear", "Stone Tools", weapon: "spear", cost: (Resource.Wood, 2), cost2: (Resource.Stone, 1));
            R("r_club", "Club", "Bone Weapons", weapon: "club", cost: (Resource.Wood, 2), cost2: (Resource.Bone, 1));
            R("r_dagger", "Bone Dagger", "Bone Weapons", weapon: "bone_dagger", cost: (Resource.Bone, 3), cost2: (Resource.Hide, 1));
            R("r_bow", "Bow", "Bone Weapons", weapon: "bow", cost: (Resource.Wood, 3), cost2: (Resource.Hide, 1));
            R("r_arrows", "Arrows x5", "Bone Weapons", ammo: AmmoType.Arrow, ammoAmount: 5, cost: (Resource.Wood, 1), cost2: (Resource.Stone, 1));
            R("r_bone_arrows", "Bone Arrows x8", "Bone Weapons", ammo: AmmoType.Arrow, ammoAmount: 8, cost: (Resource.Wood, 1), cost2: (Resource.Bone, 1));
            R("r_torch", "Torch", "Fire", weapon: "torch", cost: (Resource.Wood, 1), cost2: (Resource.Hide, 1));
            R("r_campfire", "Campfire", "Fire", places: "campfire", cost: (Resource.Wood, 4), cost2: (Resource.Stone, 3), desc: "Heals you while you stand close. Sets your respawn point.");
            R("r_clothing", "Fur Clothing", "Clothing", armor: 25, cost: (Resource.Hide, 3), desc: "+25 armor.");
            R("r_shelter", "Shelter", "Shelter", places: "shelter", cost: (Resource.Wood, 6), cost2: (Resource.Hide, 2), desc: "A lean-to. Respawn point and +50 health when you rest.");

            W("w_compound_bow", "Bow (Modern)", "compound_bow", "bp_compound_bow", (Resource.MetalParts, 2), (Resource.Wood, 2));
            W("w_crossbow", "Crossbow", "crossbow", "bp_crossbow", (Resource.MetalParts, 2), (Resource.Wood, 3));
            W("w_pistol", "Pistol", "pistol", "bp_pistol", (Resource.MetalParts, 4), (Resource.Wood, 1));
            W("w_shotgun", "Shotgun", "shotgun", "bp_shotgun", (Resource.MetalParts, 6), (Resource.Wood, 2));
            W("w_rifle", "Assault Rifle", "rifle", "bp_rifle", (Resource.MetalParts, 10), (Resource.Wood, 1));
            W("w_sniper", "Sniper Rifle", "sniper", "bp_sniper", (Resource.MetalParts, 12), (Resource.Wood, 2));
            Recipes.Add(new Recipe { Id = "w_vest", Name = "Armor Vest", Category = "Workbench", NeedsWorkbench = true, RequiredBlueprint = "bp_vest", GivesArmor = 100, Cost = { { Resource.MetalParts, 5 }, { Resource.Hide, 2 } }, Description = "+100 armor." });
            Recipes.Add(new Recipe { Id = "w_bolts", Name = "Bolts x5", Category = "Workbench", NeedsWorkbench = true, RequiredBlueprint = "bp_crossbow", GivesAmmo = AmmoType.Bolt, AmmoAmount = 5, Cost = { { Resource.MetalParts, 1 }, { Resource.Wood, 1 } } });
            Recipes.Add(new Recipe { Id = "w_pistol_ammo", Name = "Pistol Rounds x16", Category = "Workbench", NeedsWorkbench = true, RequiredBlueprint = "bp_pistol", GivesAmmo = AmmoType.PistolRound, AmmoAmount = 16, Cost = { { Resource.MetalParts, 1 } } });
            Recipes.Add(new Recipe { Id = "w_buckshot", Name = "Buckshot x8", Category = "Workbench", NeedsWorkbench = true, RequiredBlueprint = "bp_shotgun", GivesAmmo = AmmoType.Buckshot, AmmoAmount = 8, Cost = { { Resource.MetalParts, 1 } } });
            Recipes.Add(new Recipe { Id = "w_rifle_ammo", Name = "Rifle Rounds x30", Category = "Workbench", NeedsWorkbench = true, RequiredBlueprint = "bp_rifle", GivesAmmo = AmmoType.RifleRound, AmmoAmount = 30, Cost = { { Resource.MetalParts, 2 } } });

            // ---------- SHOP (the trader in the cave) ----------
            BP("bp_workbench", "Workbench", 300, "Lets you build modern gear at a workbench.");
            BP("bp_compound_bow", "Bow (Modern)", 500, "Blueprint: laminated pulley bow.");
            BP("bp_crossbow", "Crossbow", 650, "Blueprint: crossbow and bolts.");
            BP("bp_pistol", "Pistol", 750, "Blueprint: pistol and rounds.");
            BP("bp_shotgun", "Shotgun", 900, "Blueprint: shotgun and buckshot.");
            BP("bp_rifle", "Rifle", 1200, "Blueprint: assault rifle and rounds.");
            BP("bp_sniper", "Sniper Rifle", 1500, "Blueprint: long-range rifle.");
            BP("bp_vest", "Armor Vest", 600, "Blueprint: plated vest, +100 armor.");

            Shop.Add(new ShopEntry { Id = "s_axe", Name = "Stone Axe", Tab = "TOOLS", Kind = ShopKind.Weapon, WeaponId = "axe", Price = 60 });
            Shop.Add(new ShopEntry { Id = "s_torch", Name = "Torch", Tab = "TOOLS", Kind = ShopKind.Weapon, WeaponId = "torch", Price = 30 });
            Shop.Add(new ShopEntry { Id = "s_knife", Name = "Stone Knife", Tab = "WEAPONS", Kind = ShopKind.Weapon, WeaponId = "knife", Price = 40 });
            Shop.Add(new ShopEntry { Id = "s_spear", Name = "Spear", Tab = "WEAPONS", Kind = ShopKind.Weapon, WeaponId = "spear", Price = 70 });
            Shop.Add(new ShopEntry { Id = "s_bow", Name = "Bow", Tab = "WEAPONS", Kind = ShopKind.Weapon, WeaponId = "bow", Price = 120 });
            Shop.Add(new ShopEntry { Id = "s_arrows", Name = "Arrows x10", Tab = "WEAPONS", Kind = ShopKind.Ammo, Ammo = AmmoType.Arrow, Amount = 10, Price = 40 });
            Shop.Add(new ShopEntry { Id = "s_clothing", Name = "Fur Clothing (+25 armor)", Tab = "GEAR", Kind = ShopKind.Armor, Amount = 25, Price = 90 });
            Shop.Add(new ShopEntry { Id = "s_herbs", Name = "Healing Herbs x2", Tab = "GEAR", Kind = ShopKind.Resource, Resource = Resource.Herbs, Amount = 2, Price = 50, Description = "Press H to heal 35." });
            Shop.Add(new ShopEntry { Id = "s_metal", Name = "Metal Parts x2", Tab = "MISC", Kind = ShopKind.Resource, Resource = Resource.MetalParts, Amount = 2, Price = 200 });
            Shop.Add(new ShopEntry { Id = "s_bolts", Name = "Bolts x5", Tab = "MISC", Kind = ShopKind.Ammo, Ammo = AmmoType.Bolt, Amount = 5, Price = 60 });
            Shop.Add(new ShopEntry { Id = "s_pistol_ammo", Name = "Pistol Rounds x16", Tab = "MISC", Kind = ShopKind.Ammo, Ammo = AmmoType.PistolRound, Amount = 16, Price = 80 });
            Shop.Add(new ShopEntry { Id = "s_rifle_ammo", Name = "Rifle Rounds x30", Tab = "MISC", Kind = ShopKind.Ammo, Ammo = AmmoType.RifleRound, Amount = 30, Price = 140 });
            Shop.Add(new ShopEntry { Id = "s_buckshot", Name = "Buckshot x8", Tab = "MISC", Kind = ShopKind.Ammo, Ammo = AmmoType.Buckshot, Amount = 8, Price = 90 });
            Shop.Add(new ShopEntry { Id = "sell_hide", Name = "Sell Hide", Tab = "SELL", Kind = ShopKind.SellResource, Resource = Resource.Hide, Price = 25 });
            Shop.Add(new ShopEntry { Id = "sell_bone", Name = "Sell Bone", Tab = "SELL", Kind = ShopKind.SellResource, Resource = Resource.Bone, Price = 15 });
            Shop.Add(new ShopEntry { Id = "sell_wood", Name = "Sell Wood", Tab = "SELL", Kind = ShopKind.SellResource, Resource = Resource.Wood, Price = 3 });
            Shop.Add(new ShopEntry { Id = "sell_stone", Name = "Sell Stone", Tab = "SELL", Kind = ShopKind.SellResource, Resource = Resource.Stone, Price = 3 });
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
            var r = new Recipe { Id = id, Name = name, Category = "Workbench", NeedsWorkbench = true, RequiredBlueprint = bp, GivesWeapon = weapon, Description = Weapons[weapon].Description };
            r.Cost[c1.Item1] = c1.Item2;
            r.Cost[c2.Item1] = c2.Item2;
            Recipes.Add(r);
        }

        static void BP(string id, string name, int price, string desc)
            => Shop.Add(new ShopEntry { Id = id, Name = name, Tab = "BLUEPRINTS", Kind = ShopKind.Blueprint, Price = price, Description = desc });
    }
}
