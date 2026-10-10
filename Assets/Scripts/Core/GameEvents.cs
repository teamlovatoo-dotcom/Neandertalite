using System;

namespace Neanderthal
{
    /// <summary>Global gameplay events. Objectives, HUD and modes listen here instead of polling.</summary>
    public static class GameEvents
    {
        public static event Action<Pickup> PickedUp;
        public static event Action<Creature, DamageInfo> CreatureKilled;
        public static event Action<Recipe> Crafted;
        public static event Action<ShopEntry> Purchased;
        public static event Action<string> ZoneEntered;
        public static event Action<string> Interacted;

        public static void RaisePickup(Pickup p) => PickedUp?.Invoke(p);
        public static void RaiseKill(Creature c, DamageInfo info) => CreatureKilled?.Invoke(c, info);
        public static void RaiseCraft(Recipe r) => Crafted?.Invoke(r);
        public static void RaisePurchase(ShopEntry e) => Purchased?.Invoke(e);
        public static void RaiseZone(string id) => ZoneEntered?.Invoke(id);
        public static void RaiseInteract(string id) => Interacted?.Invoke(id);

        public static void ClearAll()
        {
            PickedUp = null;
            CreatureKilled = null;
            Crafted = null;
            Purchased = null;
            ZoneEntered = null;
            Interacted = null;
        }
    }
}
