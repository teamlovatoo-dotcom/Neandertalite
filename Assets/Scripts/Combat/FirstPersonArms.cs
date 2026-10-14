using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// The player's own arms in first person: the rigged "fps arms" model (CC0, Tools/blender/make_fp_arms.py) with the
    /// fingers already closed in a grip. Every frame, after the weapon has been animated, two-bone IK brings each hand
    /// to its grip target on the weapon (ViewHands creates "GripR" / "GripL" under the weapon model), so swings, thrusts,
    /// the bow draw and throws move real arms. An arm with nothing to hold drops out of view.
    /// Lives under the same shrunk view-model pivot as the weapon so it never pokes through walls.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after BowString / WeaponHolder have placed this frame's targets
    public class FirstPersonArms : MonoBehaviour
    {
        const string ModelPath = "Models/FPArms";
        // Where the rig's origin (between the shoulders) sits relative to the eye.
        static readonly Vector3 ShoulderOffset = new Vector3(0f, -0.24f, -0.06f);
        static readonly Color SkinTint = new Color(0.78f, 0.62f, 0.5f);

        class Arm
        {
            public Transform Upper, Fore, Hand, Grip;
            public Quaternion UpperRest, ForeRest, HandRest;
            public Quaternion GripRelRot;      // grip orientation in hand space
            public Vector3 GripRelPos;         // grip offset in hand space, in units of the rig's scale
            public float Side;                 // +1 right, -1 left
            public float Weight;               // blends towards the dropped pose when there's nothing to hold
        }

        Arm right, left;
        Transform pivot;                       // the shrunk view-model pivot (child of the camera)
        WeaponHolder holder;
        Weapon cachedWeapon;
        Transform targetR, targetL;
        Renderer[] renderers;

        public static bool Available => Resources.Load<GameObject>(ModelPath) != null;

        public static FirstPersonArms Create(Transform viewPivot, WeaponHolder holder)
        {
            var info = ModelKit.GetInfo(ModelPath);
            if (info == null) return null;
            var root = new GameObject("FirstPersonArms");
            root.transform.SetParent(viewPivot, false);
            root.transform.localPosition = ShoulderOffset;
            var inst = Instantiate(info.Prefab, root.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = info.Rotation;
            inst.transform.localScale = Vector3.one * info.Scale;
            var arms = root.AddComponent<FirstPersonArms>();
            arms.pivot = viewPivot;
            arms.holder = holder;
            if (!arms.Setup(inst.transform)) { Destroy(root); return null; }
            return arms;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = Find(c, name);
                if (r != null) return r;
            }
            return null;
        }

        bool Setup(Transform model)
        {
            right = MakeArm(model, "R", 1f);
            left = MakeArm(model, "L", -1f);
            if (right == null || left == null) return false;
            var skin = Mat.Pbr("fparms", Mat.Skin, 0.28f, null, SkinTint);
            renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = skin;
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            return true;
        }

        static Arm MakeArm(Transform model, string s, float side)
        {
            var a = new Arm
            {
                Upper = Find(model, "upper_arm." + s), Fore = Find(model, "forearm." + s),
                Hand = Find(model, "hand." + s), Grip = Find(model, "Grip." + s), Side = side
            };
            if (a.Upper == null || a.Fore == null || a.Hand == null || a.Grip == null) return null;
            a.UpperRest = a.Upper.localRotation;
            a.ForeRest = a.Fore.localRotation;
            a.HandRest = a.Hand.localRotation;
            a.GripRelRot = Quaternion.Inverse(a.Hand.rotation) * a.Grip.rotation;
            a.GripRelPos = Quaternion.Inverse(a.Hand.rotation) * (a.Grip.position - a.Hand.position) / Mathf.Max(1e-6f, a.Hand.lossyScale.x);
            return a;
        }

        void FindTargets()
        {
            cachedWeapon = holder.Current;
            targetR = targetL = null;
            if (holder.ViewRoot == null) return;
            foreach (var t in holder.ViewRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "GripR") targetR = t;
                else if (t.name == "GripL") targetL = t;
            }
        }

        void LateUpdate()
        {
            bool show = holder != null && holder.ViewRoot != null && holder.ViewRoot.gameObject.activeInHierarchy && !holder.Scoped;
            foreach (var r in renderers) r.enabled = show;
            if (!show) return;
            if (holder.Current != cachedWeapon || (targetR == null && targetL == null)) FindTargets();
            float dt = Time.deltaTime;
            Solve(right, targetR, dt);
            Solve(left, targetL, dt);
        }

        /// <summary>Two-bone IK: elbow bends outwards and down, the hand takes the grip's orientation.</summary>
        void Solve(Arm a, Transform target, float dt)
        {
            a.Upper.localRotation = a.UpperRest;
            a.Fore.localRotation = a.ForeRest;
            a.Hand.localRotation = a.HandRest;

            bool active = target != null && target.gameObject.activeInHierarchy;
            a.Weight = Mathf.MoveTowards(a.Weight, active ? 1f : 0f, dt * 6f);

            // Target: the grip, or a dropped-down pose below the screen edge when the hand is free.
            Vector3 dropPos = pivot.TransformPoint(new Vector3(0.34f * a.Side, -0.95f, 0.05f));
            Quaternion dropRot = pivot.rotation * Quaternion.LookRotation(Vector3.down, Vector3.forward);
            Vector3 gripPos = active ? target.position : dropPos;
            Quaternion gripRot = active ? target.rotation : dropRot;
            gripPos = Vector3.Lerp(dropPos, gripPos, a.Weight);
            gripRot = Quaternion.Slerp(dropRot, gripRot, a.Weight);

            float scale = a.Hand.lossyScale.x;
            Quaternion handRot = gripRot * Quaternion.Inverse(a.GripRelRot);
            Vector3 handPos = gripPos - handRot * (a.GripRelPos * scale);

            Vector3 A = a.Upper.position, B = a.Fore.position, C = a.Hand.position;
            float l1 = (B - A).magnitude, l2 = (C - B).magnitude;
            Vector3 toT = handPos - A;
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
            Vector3 dir = toT.sqrMagnitude > 1e-8f ? toT.normalized : pivot.forward;
            // Elbow hint: out to the side, down and a little forward (camera space, like the pose checked in Blender).
            Vector3 pole = pivot.TransformPoint(new Vector3(0.6f * a.Side, -0.7f, 0.1f)) - A;
            Vector3 bend = pole - dir * Vector3.Dot(pole, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = -pivot.up;
            bend.Normalize();
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            Vector3 elbow = A + dir * (cosA * l1) + bend * (Mathf.Sqrt(1f - cosA * cosA) * l1);

            a.Upper.rotation = Quaternion.FromToRotation(B - A, elbow - A) * a.Upper.rotation;
            elbow = a.Fore.position;
            C = a.Hand.position;
            Vector3 wrist = A + dir * d;
            a.Fore.rotation = Quaternion.FromToRotation(C - elbow, wrist - elbow) * a.Fore.rotation;
            a.Hand.rotation = handRot;
        }
    }
}
