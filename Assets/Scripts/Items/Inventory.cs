using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>Per-run inventory of the player: weapons, ammo and crafting resources.</summary>
    public class Inventory : MonoBehaviour
    {
        public readonly List<string> Weapons = new List<string>();
        /// <summary>
        /// Hotbar order: key 1 picks Hotbar[0], key 2 Hotbar[1]... Fists are always present. A weapon that left the
        /// inventory (a thrown spear) keeps its slot as a dim "ghost" so it returns to the same key when picked up.
        /// </summary>
        public readonly List<string> Hotbar = new List<string>();
        public const int HotbarSize = 9;
        readonly Dictionary<AmmoType, int> ammo = new Dictionary<AmmoType, int>();
        readonly Dictionary<Resource, int> resources = new Dictionary<Resource, int>();

        public event Action<string> WeaponAdded;
        public event Action Changed;
        /// <summary>Short message for the HUD pickup feed.</summary>
        public static event Action<string, Color> Feed;

        public static void Notify(string msg, Color color) => Feed?.Invoke(msg, color);
        public static void Notify(string msg) => Feed?.Invoke(msg, new Color(0.9f, 0.85f, 0.7f));

        public bool HasWeapon(string id) => Weapons.Contains(id);
        /// <summary>True when the weapon in a hotbar slot can be taken in hand right now.</summary>
        public bool Holds(string id) => id == "fists" || Weapons.Contains(id);

        public void EnsureHotbar()
        {
            if (!Hotbar.Contains("fists")) Hotbar.Insert(0, "fists");
            foreach (var w in Weapons) if (!Hotbar.Contains(w)) PlaceInHotbar(w);
        }

        void PlaceInHotbar(string id)
        {
            if (Hotbar.Contains(id)) return;
            int empty = Hotbar.IndexOf("");
            if (empty >= 0) { Hotbar[empty] = id; return; }
            Hotbar.Add(id);
            // Keep the first nine keys for weapons you actually carry: drop the oldest ghost if we overflow.
            if (Hotbar.Count > HotbarSize)
            {
                int ghost = Hotbar.FindIndex(h => !Holds(h));
                if (ghost >= 0) Hotbar.RemoveAt(ghost);
            }
        }

        public void SwapHotbar(int a, int b)
        {
            EnsureHotbar();
            if (a < 0 || b < 0 || a == b) return;
            while (Hotbar.Count <= Mathf.Max(a, b)) Hotbar.Add("");
            (Hotbar[a], Hotbar[b]) = (Hotbar[b], Hotbar[a]);
            // Trailing empty cells are pointless.
            while (Hotbar.Count > 0 && Hotbar[Hotbar.Count - 1] == "") Hotbar.RemoveAt(Hotbar.Count - 1);
            Changed?.Invoke();
        }

        public void AddWeapon(string id, bool announce = true)
        {
            id = ItemDatabase.Resolve(id);
            var data = ItemDatabase.Weapon(id);
            if (data == null) return;
            if (Weapons.Contains(id))
            {
                // Duplicate weapon pickups become ammo, Half-Life style.
                if (data.Ammo != AmmoType.None) AddAmmo(data.Ammo, Mathf.Max(3, data.MagSize), announce);
                return;
            }
            Weapons.Add(id);
            EnsureHotbar();
            PlaceInHotbar(id);
            if (data.Ammo != AmmoType.None && GetAmmo(data.Ammo) == 0)
                AddAmmo(data.Ammo, data.MagSize > 0 ? data.MagSize * 2 : 8, false);
            if (announce) Notify("+ " + data.Name, new Color(1f, 0.8f, 0.4f));
            WeaponAdded?.Invoke(id);
            Changed?.Invoke();
        }

        public void RemoveWeapon(string id)
        {
            if (Weapons.Remove(id)) Changed?.Invoke();
        }

        public int GetAmmo(AmmoType t) => t == AmmoType.None ? 0 : (ammo.TryGetValue(t, out var v) ? v : 0);

        public void AddAmmo(AmmoType t, int amount, bool announce = true)
        {
            if (t == AmmoType.None || amount == 0) return;
            ammo[t] = Mathf.Max(0, GetAmmo(t) + amount);
            if (announce && amount > 0) Notify($"+{amount} {ItemDatabase.AmmoName(t)}");
            Changed?.Invoke();
        }

        public int TakeAmmo(AmmoType t, int wanted)
        {
            int have = GetAmmo(t);
            int took = Mathf.Min(have, wanted);
            ammo[t] = have - took;
            if (took > 0) Changed?.Invoke();
            return took;
        }

        public int Get(Resource r) => resources.TryGetValue(r, out var v) ? v : 0;

        public void Add(Resource r, int amount, bool announce = true)
        {
            resources[r] = Mathf.Max(0, Get(r) + amount);
            if (announce && amount > 0) Notify($"+{amount} {ItemDatabase.ResourceName(r)}");
            Changed?.Invoke();
        }

        public bool CanAfford(Dictionary<Resource, int> cost)
        {
            foreach (var kv in cost)
                if (Get(kv.Key) < kv.Value) return false;
            return true;
        }

        public void Spend(Dictionary<Resource, int> cost)
        {
            foreach (var kv in cost) resources[kv.Key] = Get(kv.Key) - kv.Value;
            Changed?.Invoke();
        }

        public void Clear()
        {
            Weapons.Clear();
            Hotbar.Clear();
            ammo.Clear();
            resources.Clear();
            Changed?.Invoke();
        }
    }
}
