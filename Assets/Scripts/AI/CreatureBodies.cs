using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>References to the animated parts of a procedurally built body.</summary>
    public class CreatureBody
    {
        public Transform Root;          // child of the creature, rotated on death
        public readonly List<Transform> Legs = new List<Transform>();
        public readonly List<float> LegPhase = new List<float>();
        public Transform ArmL, ArmR, Head, Neck;
        public Transform HeldWeapon;
        public BowString Bow;
        public float Height = 1.8f;
        public float Radius = 0.4f;
        public float EyeHeight = 1.6f;
        public float StrideAmp = 30f;
        public bool Biped;

        /// <summary>True when the body is the imported skinned model (bones with rest rotations).</summary>
        public bool Skinned;
        public readonly Dictionary<Transform, Quaternion> Rest = new Dictionary<Transform, Quaternion>();
        readonly Dictionary<Transform, Vector3> current = new Dictionary<Transform, Vector3>();

        /// <summary>
        /// Rotate a limb by euler angles expressed in the body's own axes (x = pitch forward/back,
        /// y = turn, z = roll sideways), relative to its rest pose. Works for primitives and bones alike.
        /// </summary>
        public void Pose(Transform bone, Vector3 euler)
        {
            if (bone == null) return;
            current[bone] = euler;
            if (!Skinned || !Rest.TryGetValue(bone, out var rest))
            {
                bone.localRotation = Quaternion.Euler(euler);
                return;
            }
            var p = bone.parent;
            Vector3 right = p.InverseTransformDirection(Root.right);
            Vector3 up = p.InverseTransformDirection(Root.up);
            Vector3 fwd = p.InverseTransformDirection(Root.forward);
            bone.localRotation = Quaternion.AngleAxis(euler.y, up) * Quaternion.AngleAxis(euler.x, right) * Quaternion.AngleAxis(euler.z, fwd) * rest;
        }

        public void PoseSmooth(Transform bone, Vector3 target, float t)
        {
            if (bone == null) return;
            current.TryGetValue(bone, out var cur);
            Pose(bone, Vector3.Lerp(cur, target, Mathf.Clamp01(t)));
        }
    }

    /// <summary>Imported art loaded from Resources. Missing files simply fall back to primitives.</summary>
    public static class ModelLibrary
    {
        static GameObject neanderthal;
        static bool tried;

        public static GameObject Neanderthal
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    neanderthal = Resources.Load<GameObject>("Models/Neanderthal");
                }
                return neanderthal;
            }
        }
    }

    /// <summary>Builds blockout bodies for every creature from primitives.</summary>
    public static class CreatureBodies
    {
        public static CreatureBody Neanderthal(Transform parent, string weaponId, Color? teamColor, int seed)
        {
            if (ModelLibrary.Neanderthal != null)
            {
                var skinned = NeanderthalModel(parent, weaponId, teamColor, seed);
                if (skinned != null) return skinned;
            }
            var rnd = new System.Random(seed);
            float build = 0.95f + (float)rnd.NextDouble() * 0.12f;
            var b = new CreatureBody { Biped = true, Height = 1.75f * build, Radius = 0.38f, EyeHeight = 1.62f * build, StrideAmp = 35f };
            var root = Shapes.Pivot(parent, Vector3.zero, "Body");
            root.localScale = Vector3.one * build;
            b.Root = root;

            var skin = Mat.Get(Color.Lerp(Mat.Skin, new Color(0.38f, 0.25f, 0.18f), (float)rnd.NextDouble()));
            var fur = Mat.Get((float)rnd.NextDouble() > 0.5f ? Mat.Fur : Mat.FurDark);
            var hair = Mat.Get(Mat.Hair);

            // Legs
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = Shapes.Pivot(root, new Vector3(0.14f * side, 0.92f, 0f), side < 0 ? "LegL" : "LegR");
                Hitboxed(Shapes.Caps(hip, new Vector3(0, -0.44f, 0), new Vector3(0.2f, 0.46f, 0.2f), skin), 0.8f);
                Shapes.Box(hip, new Vector3(0, -0.88f, 0.06f), new Vector3(0.16f, 0.08f, 0.26f), Mat.Get(Mat.Leather), null, false);
                Shapes.Box(hip, new Vector3(0, -0.12f, 0), new Vector3(0.24f, 0.28f, 0.26f), fur, null, false);
                b.Legs.Add(hip);
                b.LegPhase.Add(side < 0 ? 0f : Mathf.PI);
            }

            // Torso: barrel chest, fur wrap and cloak
            Hitboxed(Shapes.Caps(root, new Vector3(0, 1.28f, 0), new Vector3(0.58f, 0.36f, 0.4f), skin), 1f);
            Shapes.Box(root, new Vector3(0, 1.0f, 0), new Vector3(0.52f, 0.3f, 0.38f), fur, null, false);
            Shapes.Box(root, new Vector3(0, 1.38f, -0.12f), new Vector3(0.62f, 0.5f, 0.2f), fur, Quaternion.Euler(-6, 0, 0), false);
            Shapes.Stick(root, new Vector3(-0.3f, 1.55f, 0.05f), new Vector3(0.25f, 1.0f, 0.2f), 0.06f, Mat.Get(Mat.Leather));

            // Head
            b.Neck = Shapes.Pivot(root, new Vector3(0, 1.55f, 0.02f), "Neck");
            b.Head = b.Neck;
            var head = Shapes.Ball(b.Neck, new Vector3(0, 0.13f, 0.02f), new Vector3(0.26f, 0.29f, 0.28f), skin);
            Hitboxed(head, 2f, true);
            Shapes.Box(b.Neck, new Vector3(0, 0.19f, 0.13f), new Vector3(0.24f, 0.05f, 0.07f), skin, null, false);         // brow ridge
            Shapes.Ball(b.Neck, new Vector3(0, 0.2f, -0.03f), new Vector3(0.31f, 0.27f, 0.32f), hair, false);               // hair
            Shapes.Ball(b.Neck, new Vector3(0, 0.02f, 0.1f), new Vector3(0.24f, 0.22f, 0.16f), hair, false);                // beard
            Shapes.Ball(b.Neck, new Vector3(0, 0.1f, 0.16f), new Vector3(0.08f, 0.07f, 0.08f), skin, false);                // nose
            var eyeMat = Mat.Get(new Color(0.05f, 0.04f, 0.03f), 0.8f);
            Shapes.Ball(b.Neck, new Vector3(-0.055f, 0.15f, 0.13f), Vector3.one * 0.035f, eyeMat, false);
            Shapes.Ball(b.Neck, new Vector3(0.055f, 0.15f, 0.13f), Vector3.one * 0.035f, eyeMat, false);

            // Arms
            for (int side = -1; side <= 1; side += 2)
            {
                var sh = Shapes.Pivot(root, new Vector3(0.36f * side, 1.48f, 0f), side < 0 ? "ArmL" : "ArmR");
                Shapes.Caps(sh, new Vector3(0, -0.32f, 0), new Vector3(0.17f, 0.36f, 0.17f), skin, null, false);
                Shapes.Ball(sh, new Vector3(0, -0.68f, 0.02f), Vector3.one * 0.13f, skin, false);
                if (teamColor.HasValue)
                    Shapes.Cyl(sh, new Vector3(0, -0.15f, 0), new Vector3(0.19f, 0.04f, 0.19f), Mat.Get(teamColor.Value), null, false);
                if (side < 0) b.ArmL = sh; else b.ArmR = sh;
            }

            if (!string.IsNullOrEmpty(weaponId))
            {
                var hand = Shapes.Pivot(b.ArmR, new Vector3(0, -0.68f, 0.04f), "Hand");
                var model = WeaponModels.Build(weaponId, hand, out var bs).transform;
                Shapes.SetShadows(model.gameObject, true);
                var data = ItemDatabase.Weapon(weaponId);
                if (data != null && data.Kind == WeaponKind.Bow)
                {
                    // Bow is held in the left hand, upright.
                    model.SetParent(Shapes.Pivot(b.ArmL, new Vector3(0, -0.68f, 0.06f), "HandL"), false);
                    model.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    b.Bow = bs;
                    if (bs != null) bs.ShowArrow = false;
                }
                else
                {
                    model.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                }
                b.HeldWeapon = model;
            }
            return b;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>
        /// Builds a body from the rigged Neanderthal.fbx (made by Tools/blender/make_neanderthal.py).
        /// Orientation, scale and the arms-down rest pose are derived from the bones, so export axis settings don't matter.
        /// </summary>
        static CreatureBody NeanderthalModel(Transform parent, string weaponId, Color? teamColor, int seed)
        {
            var rnd = new System.Random(seed);
            float build = 0.97f + (float)rnd.NextDouble() * 0.1f;
            var root = Shapes.Pivot(parent, Vector3.zero, "Body");
            var model = Object.Instantiate(ModelLibrary.Neanderthal, root, false);
            model.name = "Model";
            var t = model.transform;
            Transform hips = FindDeep(t, "Hips"), head = FindDeep(t, "Head"), facing = FindDeep(t, "Facing");
            Transform armL = FindDeep(t, "UpperArm.L"), armR = FindDeep(t, "UpperArm.R");
            Transform foreL = FindDeep(t, "LowerArm.L"), foreR = FindDeep(t, "LowerArm.R");
            Transform legL = FindDeep(t, "UpperLeg.L"), legR = FindDeep(t, "UpperLeg.R");
            Transform neck = FindDeep(t, "Neck"), handL = FindDeep(t, "Hand.L"), handR = FindDeep(t, "Hand.R");
            if (hips == null || head == null || facing == null || armL == null || armR == null || legL == null || legR == null || neck == null)
            {
                Object.Destroy(root.gameObject);
                return null;
            }

            // Stand upright and face +Z of the body root.
            Vector3 upDir = root.InverseTransformDirection(head.position - hips.position);
            t.localRotation = Quaternion.FromToRotation(upDir, Vector3.up) * t.localRotation;
            Vector3 face = root.InverseTransformDirection(facing.position - head.position);
            face.y = 0f;
            if (face.sqrMagnitude > 1e-6f) t.localRotation = Quaternion.FromToRotation(face.normalized, Vector3.forward) * t.localRotation;
            // Scale so the hips sit at a Neanderthal-ish height, feet on the ground.
            float hipsY = root.InverseTransformPoint(hips.position).y;
            if (hipsY > 0.01f) t.localScale *= 0.9f * build / hipsY;
            float footY = Mathf.Min(root.InverseTransformPoint(FindDeep(t, "Foot.L") != null ? FindDeep(t, "Foot.L").position : t.position).y, 0.09f * build);
            t.localPosition += Vector3.down * (footY - 0.08f * build);

            // T-pose -> arms hanging slightly outwards.
            foreach (var (upper, lower) in new[] { (armL, foreL), (armR, foreR) })
            {
                if (lower == null) continue;
                Vector3 dir = lower.position - upper.position;
                float side = Mathf.Sign(root.InverseTransformDirection(dir).x);
                Vector3 target = root.TransformDirection(new Vector3(side * 0.22f, -1f, 0.05f).normalized);
                upper.rotation = Quaternion.FromToRotation(dir, target) * upper.rotation;
            }

            var b = new CreatureBody { Biped = true, Skinned = true, Root = root, Height = 1.75f * build, Radius = 0.38f, EyeHeight = 1.62f * build, StrideAmp = 32f };
            b.Legs.Add(legL); b.LegPhase.Add(0f);
            b.Legs.Add(legR); b.LegPhase.Add(Mathf.PI);
            b.ArmL = armL; b.ArmR = armR; b.Neck = neck; b.Head = head;
            foreach (var bone in new[] { legL, legR, armL, armR, neck }) b.Rest[bone] = bone.localRotation;

            // Materials: skin tone varies per hunter.
            var skinTone = Color.Lerp(new Color(0.42f, 0.26f, 0.17f), new Color(0.3f, 0.18f, 0.11f), (float)rnd.NextDouble());
            var skin = Mat.Textured("np_skin_" + ColorUtility.ToHtmlStringRGB(skinTone),
                Mat.NoiseTexture(128, skinTone * 1.08f, skinTone * 0.82f, 9f, 21), 0.35f);
            var fur = Mat.Textured("np_fur", Mat.NoiseTexture(128, new Color(0.36f, 0.25f, 0.15f), new Color(0.16f, 0.1f, 0.06f), 26f, 4), 0.05f, new Vector2(3, 3));
            var hair = Mat.Textured("np_hair", Mat.NoiseTexture(128, new Color(0.11f, 0.075f, 0.05f), new Color(0.03f, 0.02f, 0.015f), 40f, 8), 0.25f, new Vector2(2, 6));
            var rope = Mat.Get(new Color(0.45f, 0.35f, 0.22f), 0.1f);
            var eyes = Mat.Get(new Color(0.03f, 0.02f, 0.015f), 0.85f);
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name : "";
                    mats[i] = n.Contains("Fur") ? fur : n.Contains("Hair") ? hair : n.Contains("Eye") ? eyes : n.Contains("Rope") ? rope : skin;
                }
                smr.sharedMaterials = mats;
            }

            // Hit volumes: body capsule up to the shoulders, separate head sphere for headshots.
            var bodyCol = root.gameObject.AddComponent<CapsuleCollider>();
            bodyCol.center = new Vector3(0f, 0.76f * build, 0f);
            bodyCol.height = 1.52f * build;
            bodyCol.radius = 0.27f;
            root.gameObject.AddComponent<Hitbox>().Multiplier = 1f;
            var headCol = new GameObject("HeadHitbox");
            headCol.transform.SetParent(head, false);
            headCol.transform.position = Vector3.Lerp(head.position, facing.position, 0.5f) + root.up * 0.03f;
            var sc = headCol.AddComponent<SphereCollider>();
            sc.radius = 0.14f / Mathf.Max(0.001f, headCol.transform.lossyScale.x);
            var hb = headCol.AddComponent<Hitbox>();
            hb.Multiplier = 2f;
            hb.IsHead = true;

            if (teamColor.HasValue)
            {
                var foreArm = foreL != null ? foreL : armL;
                var band = Shapes.Pivot(armL, Vector3.zero, "TeamBand");
                band.position = Vector3.Lerp(armL.position, foreArm.position, 0.35f);
                band.rotation = Quaternion.FromToRotation(Vector3.up, foreArm.position - armL.position);
                band.localScale = Vector3.one / Mathf.Max(0.001f, armL.lossyScale.x);
                Shapes.Cyl(band, Vector3.zero, new Vector3(0.21f, 0.03f, 0.21f), Mat.Get(teamColor.Value), null, false);
            }

            if (!string.IsNullOrEmpty(weaponId))
            {
                var data = ItemDatabase.Weapon(weaponId);
                bool bow = data != null && data.Kind == WeaponKind.Bow;
                var hand = bow ? (handL != null ? handL : armL) : (handR != null ? handR : armR);
                var grip = new GameObject(bow ? "HandL" : "Hand").transform;
                grip.SetParent(hand, false);
                grip.position = hand.position + root.forward * 0.03f;
                grip.rotation = root.rotation;
                grip.localScale = Vector3.one / Mathf.Max(0.001f, hand.lossyScale.x);
                var weaponModel = WeaponModels.Build(weaponId, grip, out var bs).transform;
                weaponModel.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                Shapes.SetShadows(weaponModel.gameObject, true);
                if (bow) { b.Bow = bs; if (bs != null) bs.ShowArrow = false; }
                b.HeldWeapon = weaponModel;
            }
            return b;
        }

        public enum Beast { Wolf, Deer, Bear, Mammoth }

        public static CreatureBody Quadruped(Transform parent, Beast kind, int seed)
        {
            float len, legLen, thick, headSize;
            Color furC;
            switch (kind)
            {
                case Beast.Wolf: len = 1.1f; legLen = 0.55f; thick = 0.42f; headSize = 0.3f; furC = new Color(0.36f, 0.33f, 0.3f); break;
                case Beast.Deer: len = 1.2f; legLen = 0.85f; thick = 0.45f; headSize = 0.28f; furC = new Color(0.52f, 0.36f, 0.22f); break;
                case Beast.Bear: len = 1.7f; legLen = 0.7f; thick = 0.95f; headSize = 0.5f; furC = new Color(0.24f, 0.16f, 0.1f); break;
                default: len = 3.6f; legLen = 1.6f; thick = 2.4f; headSize = 1.2f; furC = new Color(0.3f, 0.2f, 0.13f); break;
            }
            var rnd = new System.Random(seed);
            furC *= 0.9f + (float)rnd.NextDouble() * 0.2f;
            furC.a = 1f;
            var fur = Mat.Get(furC);
            var dark = Mat.Get(furC * 0.6f);

            var b = new CreatureBody
            {
                Biped = false,
                Radius = Mathf.Max(0.35f, thick * 0.55f),
                Height = legLen + thick,
                EyeHeight = legLen + thick * 0.8f,
                StrideAmp = kind == Beast.Mammoth ? 18f : 32f
            };
            var root = Shapes.Pivot(parent, Vector3.zero, "Body");
            b.Root = root;
            float bodyY = legLen + thick * 0.35f;

            Hitboxed(Shapes.Caps(root, new Vector3(0, bodyY, 0), new Vector3(thick, len * 0.5f + thick * 0.25f, thick * 0.95f), fur, Quaternion.Euler(90, 0, 0)), 1f);
            if (kind == Beast.Mammoth)
            {
                Shapes.Ball(root, new Vector3(0, bodyY + thick * 0.45f, len * 0.25f), new Vector3(thick * 0.8f, thick * 0.6f, thick * 0.8f), fur, false); // hump
                Shapes.Box(root, new Vector3(0, bodyY - thick * 0.45f, 0), new Vector3(thick * 0.85f, thick * 0.25f, len * 0.9f), dark, null, false);   // shaggy belly fur
            }
            if (kind == Beast.Bear)
                Shapes.Ball(root, new Vector3(0, bodyY + thick * 0.3f, len * 0.2f), new Vector3(thick * 0.8f, thick * 0.6f, thick * 0.7f), fur, false);

            // Legs: front pair and back pair, diagonal gait
            float lx = thick * 0.3f, lz = len * 0.38f;
            float legThick = kind == Beast.Mammoth ? 0.75f : (kind == Beast.Bear ? 0.3f : (kind == Beast.Deer ? 0.1f : 0.13f));
            Vector3[] hips = { new Vector3(-lx, legLen + 0.05f, lz), new Vector3(lx, legLen + 0.05f, lz), new Vector3(-lx, legLen + 0.05f, -lz), new Vector3(lx, legLen + 0.05f, -lz) };
            float[] phase = { 0f, Mathf.PI, Mathf.PI, 0f };
            for (int i = 0; i < 4; i++)
            {
                var hip = Shapes.Pivot(root, hips[i], "Leg" + i);
                Hitboxed(Shapes.Caps(hip, new Vector3(0, -legLen * 0.5f, 0), new Vector3(legThick, legLen * 0.55f, legThick), i < 2 ? fur : dark), 0.7f);
                b.Legs.Add(hip);
                b.LegPhase.Add(phase[i]);
            }

            // Neck + head
            b.Neck = Shapes.Pivot(root, new Vector3(0, bodyY + thick * 0.2f, len * 0.5f + thick * 0.15f), "Neck");
            b.Head = b.Neck;
            Vector3 headPos;
            switch (kind)
            {
                case Beast.Deer: headPos = new Vector3(0, 0.45f, 0.2f); Shapes.Stick(b.Neck, Vector3.zero, headPos, 0.16f, fur); break;
                case Beast.Mammoth: headPos = new Vector3(0, 0.3f, 0.5f); break;
                default: headPos = new Vector3(0, 0.08f, headSize * 0.6f); break;
            }
            var head = Shapes.Ball(b.Neck, headPos, new Vector3(headSize, headSize * 0.9f, headSize * 1.1f), fur);
            Hitboxed(head, 2f, true);
            var eye = Mat.Get(new Color(0.05f, 0.04f, 0.03f), 0.8f);
            Shapes.Ball(b.Neck, headPos + new Vector3(-headSize * 0.32f, headSize * 0.15f, headSize * 0.38f), Vector3.one * headSize * 0.12f, eye, false);
            Shapes.Ball(b.Neck, headPos + new Vector3(headSize * 0.32f, headSize * 0.15f, headSize * 0.38f), Vector3.one * headSize * 0.12f, eye, false);

            switch (kind)
            {
                case Beast.Wolf:
                    Shapes.Box(b.Neck, headPos + new Vector3(0, -0.04f, 0.2f), new Vector3(0.13f, 0.12f, 0.26f), fur, null, false);
                    Shapes.Ball(b.Neck, headPos + new Vector3(0, -0.01f, 0.33f), Vector3.one * 0.06f, eye, false);
                    Shapes.Cone(b.Neck, headPos + new Vector3(-0.08f, 0.1f, -0.02f), new Vector3(0.08f, 0.14f, 0.05f), dark, null, 4);
                    Shapes.Cone(b.Neck, headPos + new Vector3(0.08f, 0.1f, -0.02f), new Vector3(0.08f, 0.14f, 0.05f), dark, null, 4);
                    Shapes.Stick(root, new Vector3(0, bodyY + 0.05f, -len * 0.55f), new Vector3(0, bodyY - 0.25f, -len * 0.85f), 0.1f, dark);
                    break;
                case Beast.Deer:
                    Shapes.Box(b.Neck, headPos + new Vector3(0, -0.03f, 0.18f), new Vector3(0.12f, 0.12f, 0.2f), fur, null, false);
                    var antler = Mat.Get(Mat.Bone);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 a0 = headPos + new Vector3(0.06f * s, 0.12f, 0f);
                        Vector3 a1 = a0 + new Vector3(0.15f * s, 0.3f, -0.05f);
                        Shapes.Stick(b.Neck, a0, a1, 0.025f, antler);
                        Shapes.Stick(b.Neck, a1, a1 + new Vector3(0.1f * s, 0.18f, 0.08f), 0.02f, antler);
                        Shapes.Stick(b.Neck, a1, a1 + new Vector3(-0.02f * s, 0.2f, -0.1f), 0.02f, antler);
                    }
                    Shapes.Ball(root, new Vector3(0, bodyY + 0.05f, -len * 0.58f), Vector3.one * 0.14f, Mat.Get(new Color(0.9f, 0.88f, 0.82f)), false);
                    break;
                case Beast.Bear:
                    Shapes.Box(b.Neck, headPos + new Vector3(0, -0.06f, 0.28f), new Vector3(0.24f, 0.2f, 0.22f), dark, null, false);
                    Shapes.Ball(b.Neck, headPos + new Vector3(-0.17f, 0.2f, -0.05f), Vector3.one * 0.13f, fur, false);
                    Shapes.Ball(b.Neck, headPos + new Vector3(0.17f, 0.2f, -0.05f), Vector3.one * 0.13f, fur, false);
                    break;
                case Beast.Mammoth:
                {
                    var tusk = Mat.Get(Mat.Bone, 0.3f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 t0 = headPos + new Vector3(0.3f * s, -0.4f, 0.4f);
                        Vector3 t1 = t0 + new Vector3(0.2f * s, -0.6f, 0.7f);
                        Vector3 t2 = t1 + new Vector3(-0.15f * s, 0.25f, 0.6f);
                        Vector3 t3 = t2 + new Vector3(-0.25f * s, 0.45f, 0.1f);
                        Shapes.Stick(b.Neck, t0, t1, 0.16f, tusk);
                        Shapes.Stick(b.Neck, t1, t2, 0.13f, tusk);
                        Shapes.Stick(b.Neck, t2, t3, 0.09f, tusk);
                    }
                    // Trunk hangs down in segments
                    Vector3 p = headPos + new Vector3(0, -0.2f, 0.5f);
                    float w = 0.35f;
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 q = p + new Vector3(0, -0.42f, 0.06f - i * 0.03f);
                        Shapes.Stick(b.Neck, p, q, w, dark);
                        p = q;
                        w *= 0.82f;
                    }
                    Shapes.Ball(b.Neck, headPos + new Vector3(-0.65f, 0f, -0.1f), new Vector3(0.25f, 0.8f, 0.6f), dark, false);
                    Shapes.Ball(b.Neck, headPos + new Vector3(0.65f, 0f, -0.1f), new Vector3(0.25f, 0.8f, 0.6f), dark, false);
                    Shapes.Stick(root, new Vector3(0, bodyY, -len * 0.6f), new Vector3(0, bodyY - 0.8f, -len * 0.7f), 0.1f, dark);
                    break;
                }
            }
            return b;
        }

        static GameObject Hitboxed(GameObject go, float mult, bool head = false)
        {
            var hb = go.AddComponent<Hitbox>();
            hb.Multiplier = mult;
            hb.IsHead = head;
            return go;
        }
    }
}
