using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// First-person hands (Tools/blender/make_hands.py): a fist closed round the grip with the forearm in a fur
    /// bracer. Placed per weapon: one hand on knives and clubs, two on the spear, the bow hand on the grip and the
    /// drawing hand riding the string.
    /// </summary>
    public static class ViewHands
    {
        const string Path = "Models/Weapons/hand";

        public static bool Available => ModelKit.Has(Path);

        static Transform Hand(Transform parent, Vector3 pos, Quaternion rot, bool left)
        {
            var go = ModelKit.Spawn(Path, parent, out _, false, false);
            if (go == null) return null;
            go.name = left ? "HandL" : "HandR";
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            if (left) go.transform.localScale = new Vector3(-1f, 1f, 1f);
            return go.transform;
        }

        /// <summary>Adds hands to a freshly built first-person weapon model. Returns false for weapons without them.</summary>
        public static bool Attach(string id, Transform model, BowString str)
        {
            if (!Available || model == null) return false;
            var vertical = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            switch (id)
            {
                case "fists":
                    Hand(model, new Vector3(0f, 0f, 0.05f), Quaternion.Euler(-20f, 0f, 0f), false);
                    Hand(model, new Vector3(-0.46f, 0.02f, 0.08f), Quaternion.Euler(-20f, 0f, 0f), true);
                    return true;
                case "spear":
                case "obsidian_spear":
                    Hand(model, Vector3.zero, Quaternion.identity, false);
                    Hand(model, new Vector3(0f, 0f, 0.42f), Quaternion.Euler(0f, 0f, 25f), true);
                    return true;
                case "bow":
                case "compound_bow":
                    Hand(model, new Vector3(0f, -0.01f, 0.005f), vertical, true);
                    if (str != null) str.DrawHand = Hand(model, Vector3.zero, vertical, false);
                    return true;
                case "slingshot":
                    Hand(model, new Vector3(0f, -0.045f, 0f), vertical, true);
                    if (str != null) str.DrawHand = Hand(model, Vector3.zero, Quaternion.LookRotation(Vector3.left, Vector3.forward), false);
                    return true;
                case "stone":
                    Hand(model, new Vector3(0.006f, -0.012f, -0.008f), Quaternion.identity, false);
                    return true;
                default:
                    if (WeaponModels.ModelName(id) == null) return false;
                    Hand(model, Vector3.zero, Quaternion.identity, false);
                    return true;
            }
        }
    }
}
