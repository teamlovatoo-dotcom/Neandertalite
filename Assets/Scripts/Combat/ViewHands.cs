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

        /// <summary>
        /// Grip target for the first-person arms (FirstPersonArms): +Z runs along the handle out of the thumb side,
        /// +Y points from the handle towards the wrist.
        /// </summary>
        static Transform Target(Transform parent, string name, Vector3 pos, Vector3 along, Vector3 toWrist)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.LookRotation(along, toWrist);
            return t;
        }

        /// <summary>Grip targets for the player's rigged arms. Returns false for weapons that have none.</summary>
        static bool AttachTargets(string id, Transform model, BowString str)
        {
            Vector3 wristR = new Vector3(0.75f, 0.65f, 0f), wristL = new Vector3(-0.75f, 0.65f, 0f);
            switch (id)
            {
                case "fists":
                    Target(model, "GripR", new Vector3(0f, 0f, 0.05f), new Vector3(0f, 1f, 0.2f), new Vector3(0.3f, -0.5f, -0.8f));
                    Target(model, "GripL", new Vector3(-0.46f, 0.02f, 0.08f), new Vector3(0f, 1f, 0.2f), new Vector3(-0.3f, -0.5f, -0.8f));
                    return true;
                case "spear":
                case "obsidian_spear":
                    Target(model, "GripR", Vector3.zero, Vector3.forward, wristR);
                    Target(model, "GripL", new Vector3(0f, 0f, 0.42f), Vector3.forward, wristL);
                    return true;
                case "bow":
                case "compound_bow":
                case "slingshot":
                    bool sling = id == "slingshot";
                    Target(model, "GripL", new Vector3(0f, sling ? -0.045f : -0.01f, 0.005f), Vector3.up, new Vector3(-0.3f, 0f, -1f));
                    if (str != null) str.DrawHand = Target(model, "GripR", Vector3.zero, Vector3.up, new Vector3(0.4f, -0.2f, -1f));
                    return true;
                case "stone":
                    Target(model, "GripR", new Vector3(0.006f, -0.012f, -0.008f), Vector3.forward, new Vector3(0.3f, -0.6f, -0.5f));
                    return true;
                default:
                    if (WeaponModels.ModelName(id) == null) return false;
                    Target(model, "GripR", Vector3.zero, Vector3.forward, wristR);
                    return true;
            }
        }

        /// <summary>Adds hands to a freshly built first-person weapon model. Returns false for weapons without them.</summary>
        public static bool Attach(string id, Transform model, BowString str)
        {
            if (model == null) return false;
            if (FirstPersonArms.Available) return AttachTargets(id, model, str);
            if (!Available) return false;
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
