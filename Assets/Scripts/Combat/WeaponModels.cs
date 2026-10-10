using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Procedural blockout models for every weapon and projectile. Forward is +Z, the grip sits at the origin.
    /// Replace with imported meshes by returning your prefab instance from <see cref="Build"/>.
    /// </summary>
    public static class WeaponModels
    {
        static readonly Quaternion ToForward = Quaternion.Euler(90f, 0f, 0f);

        public static GameObject Build(string id, Transform parent, out BowString bowString)
        {
            bowString = null;
            var root = new GameObject("Model_" + id);
            root.transform.SetParent(parent, false);
            var t = root.transform;

            var wood = Mat.Get(Mat.Wood);
            var woodDark = Mat.Get(Mat.WoodDark);
            var flint = Mat.Get(Mat.Flint, 0.45f);
            var leather = Mat.Get(Mat.Leather);
            var bone = Mat.Get(Mat.Bone, 0.2f);
            var metal = Mat.Get(Mat.Metal, 0.55f, 0.7f);
            var metalLight = Mat.Get(Mat.MetalLight, 0.6f, 0.8f);
            var sinew = Mat.Get(Mat.Sinew);

            switch (id)
            {
                case "fists":
                    Shapes.Box(t, new Vector3(0f, 0f, 0.05f), new Vector3(0.09f, 0.09f, 0.11f), Mat.Get(Mat.Skin), null, false);
                    break;

                case "knife":
                    Shapes.Stick(t, new Vector3(0, 0, -0.06f), new Vector3(0, 0, 0.06f), 0.032f, woodDark);
                    Shapes.Cone(t, new Vector3(0, 0, 0.06f), new Vector3(0.045f, 0.2f, 0.015f), flint, ToForward, 4);
                    Shapes.Stick(t, new Vector3(0, 0, 0.045f), new Vector3(0, 0, 0.075f), 0.04f, leather);
                    break;

                case "bone_dagger":
                    Shapes.Stick(t, new Vector3(0, 0, -0.07f), new Vector3(0, 0, 0.06f), 0.035f, leather);
                    Shapes.Cone(t, new Vector3(0, 0, 0.06f), new Vector3(0.05f, 0.26f, 0.02f), bone, ToForward, 4);
                    Shapes.Ball(t, new Vector3(0, 0, -0.075f), Vector3.one * 0.045f, bone, false);
                    break;

                case "club":
                    Shapes.Stick(t, new Vector3(0, 0, -0.1f), new Vector3(0, 0, 0.3f), 0.05f, wood);
                    Shapes.Stick(t, new Vector3(0, 0, 0.3f), new Vector3(0, 0, 0.55f), 0.1f, woodDark);
                    Shapes.Ball(t, new Vector3(0, 0, 0.56f), new Vector3(0.12f, 0.12f, 0.14f), woodDark, false);
                    Shapes.Ball(t, new Vector3(0.04f, 0.03f, 0.45f), Vector3.one * 0.05f, bone, false);
                    Shapes.Ball(t, new Vector3(-0.04f, -0.02f, 0.5f), Vector3.one * 0.04f, bone, false);
                    break;

                case "axe":
                    Shapes.Stick(t, new Vector3(0, 0, -0.1f), new Vector3(0, 0, 0.45f), 0.04f, wood);
                    Shapes.Box(t, new Vector3(0, 0.07f, 0.4f), new Vector3(0.05f, 0.18f, 0.12f), flint, Quaternion.Euler(0, 0, 0), false);
                    Shapes.Cone(t, new Vector3(0, 0.15f, 0.4f), new Vector3(0.12f, 0.08f, 0.05f), flint, null, 4);
                    Shapes.Stick(t, new Vector3(0, -0.02f, 0.36f), new Vector3(0, -0.02f, 0.44f), 0.055f, leather);
                    break;

                case "torch":
                    Shapes.Stick(t, new Vector3(0, 0, -0.1f), new Vector3(0, 0, 0.42f), 0.04f, wood);
                    Shapes.Stick(t, new Vector3(0, 0, 0.36f), new Vector3(0, 0, 0.48f), 0.07f, leather);
                    var flame = Shapes.Ball(t, new Vector3(0, 0.03f, 0.53f), new Vector3(0.09f, 0.14f, 0.09f), Mat.Emissive(Mat.Fire, 3f), false);
                    flame.AddComponent<Flicker>();
                    Shapes.SetShadows(flame, false);
                    break;

                case "spear":
                    Shapes.Stick(t, new Vector3(0, 0, -0.9f), new Vector3(0, 0, 0.9f), 0.035f, wood);
                    Shapes.Cone(t, new Vector3(0, 0, 0.88f), new Vector3(0.06f, 0.24f, 0.025f), flint, ToForward, 4);
                    Shapes.Stick(t, new Vector3(0, 0, 0.84f), new Vector3(0, 0, 0.93f), 0.045f, sinew);
                    break;

                case "bow":
                case "compound_bow":
                {
                    bool modern = id == "compound_bow";
                    var limb = modern ? metal : wood;
                    // Limbs: three segments per side curving towards the archer (-Z).
                    Vector3 grip = Vector3.zero;
                    Vector3 u1 = new Vector3(0, 0.22f, -0.02f), u2 = new Vector3(0, 0.42f, -0.07f), u3 = new Vector3(0, 0.56f, -0.14f);
                    Vector3 d1 = new Vector3(0, -0.22f, -0.02f), d2 = new Vector3(0, -0.42f, -0.07f), d3 = new Vector3(0, -0.56f, -0.14f);
                    Shapes.Stick(t, grip, u1, 0.035f, limb);
                    Shapes.Stick(t, u1, u2, 0.03f, limb);
                    Shapes.Stick(t, u2, u3, 0.024f, limb);
                    Shapes.Stick(t, grip, d1, 0.035f, limb);
                    Shapes.Stick(t, d1, d2, 0.03f, limb);
                    Shapes.Stick(t, d2, d3, 0.024f, limb);
                    Shapes.Stick(t, new Vector3(0, -0.07f, 0), new Vector3(0, 0.07f, 0), 0.05f, leather);
                    if (modern)
                    {
                        Shapes.Cyl(t, u3, new Vector3(0.06f, 0.01f, 0.06f), metalLight, Quaternion.Euler(0, 0, 90), false);
                        Shapes.Cyl(t, d3, new Vector3(0.06f, 0.01f, 0.06f), metalLight, Quaternion.Euler(0, 0, 90), false);
                        Shapes.Box(t, new Vector3(0, 0.12f, 0.06f), new Vector3(0.02f, 0.02f, 0.12f), metalLight, null, false);
                    }
                    bowString = BowString.Create(t, u3, d3, modern ? metalLight : sinew, id == "compound_bow" ? 0.75f : 0.62f);
                    break;
                }

                case "crossbow":
                {
                    Shapes.Box(t, new Vector3(0, 0, 0.15f), new Vector3(0.05f, 0.06f, 0.6f), woodDark, null, false);
                    Shapes.Box(t, new Vector3(0, -0.07f, -0.05f), new Vector3(0.04f, 0.12f, 0.05f), woodDark, Quaternion.Euler(-15, 0, 0), false);
                    Vector3 l = new Vector3(-0.3f, 0, 0.36f), r = new Vector3(0.3f, 0, 0.36f), c = new Vector3(0, 0, 0.44f);
                    Shapes.Stick(t, c, l, 0.03f, metal);
                    Shapes.Stick(t, c, r, 0.03f, metal);
                    bowString = BowString.Create(t, l, r, sinew, 0.3f, horizontal: true);
                    Shapes.Cyl(t, new Vector3(0, 0.05f, 0.05f), new Vector3(0.035f, 0.08f, 0.035f), metalLight, ToForward, false);
                    break;
                }

                case "pistol":
                    Shapes.Box(t, new Vector3(0, -0.05f, -0.01f), new Vector3(0.035f, 0.11f, 0.05f), metal, Quaternion.Euler(-12, 0, 0), false);
                    Shapes.Box(t, new Vector3(0, 0.02f, 0.06f), new Vector3(0.04f, 0.045f, 0.2f), metalLight, null, false);
                    Shapes.Box(t, new Vector3(0, -0.015f, 0.03f), new Vector3(0.012f, 0.03f, 0.04f), metal, null, false);
                    break;

                case "shotgun":
                    Shapes.Cyl(t, new Vector3(0, 0.03f, 0.35f), new Vector3(0.035f, 0.36f, 0.035f), metal, ToForward, false);
                    Shapes.Cyl(t, new Vector3(0, -0.005f, 0.32f), new Vector3(0.05f, 0.1f, 0.05f), woodDark, ToForward, false);
                    Shapes.Box(t, new Vector3(0, 0.01f, 0.02f), new Vector3(0.05f, 0.07f, 0.22f), metal, null, false);
                    Shapes.Box(t, new Vector3(0, -0.04f, -0.18f), new Vector3(0.045f, 0.09f, 0.26f), wood, Quaternion.Euler(8, 0, 0), false);
                    break;

                case "rifle":
                    Shapes.Box(t, new Vector3(0, 0.02f, 0.08f), new Vector3(0.05f, 0.08f, 0.36f), metal, null, false);
                    Shapes.Cyl(t, new Vector3(0, 0.03f, 0.42f), new Vector3(0.025f, 0.16f, 0.025f), metal, ToForward, false);
                    Shapes.Box(t, new Vector3(0, -0.08f, 0.12f), new Vector3(0.035f, 0.13f, 0.06f), metal, Quaternion.Euler(12, 0, 0), false);
                    Shapes.Box(t, new Vector3(0, -0.05f, -0.03f), new Vector3(0.035f, 0.09f, 0.04f), metal, Quaternion.Euler(-12, 0, 0), false);
                    Shapes.Box(t, new Vector3(0, 0, -0.2f), new Vector3(0.04f, 0.07f, 0.18f), metalLight, null, false);
                    Shapes.Box(t, new Vector3(0, 0.08f, 0.08f), new Vector3(0.02f, 0.025f, 0.1f), metalLight, null, false);
                    break;

                case "sniper":
                    Shapes.Box(t, new Vector3(0, 0.0f, 0.05f), new Vector3(0.05f, 0.07f, 0.4f), woodDark, null, false);
                    Shapes.Cyl(t, new Vector3(0, 0.02f, 0.55f), new Vector3(0.022f, 0.32f, 0.022f), metal, ToForward, false);
                    Shapes.Cyl(t, new Vector3(0, 0.09f, 0.1f), new Vector3(0.04f, 0.14f, 0.04f), metal, ToForward, false);
                    Shapes.Box(t, new Vector3(0, -0.04f, -0.25f), new Vector3(0.045f, 0.1f, 0.24f), woodDark, Quaternion.Euler(6, 0, 0), false);
                    Shapes.Box(t, new Vector3(0, -0.07f, 0.0f), new Vector3(0.03f, 0.08f, 0.04f), metal, Quaternion.Euler(-12, 0, 0), false);
                    break;

                default:
                    Shapes.Box(t, Vector3.zero, Vector3.one * 0.1f, wood, null, false);
                    break;
            }
            return root;
        }

        public static GameObject Arrow(Transform parent, bool bolt)
        {
            var root = new GameObject(bolt ? "Bolt" : "Arrow");
            root.transform.SetParent(parent, false);
            var t = root.transform;
            float len = bolt ? 0.42f : 0.75f;
            Shapes.Stick(t, new Vector3(0, 0, -len), Vector3.zero, 0.012f, Mat.Get(bolt ? Mat.Metal : Mat.Wood));
            Shapes.Cone(t, Vector3.zero, new Vector3(0.03f, 0.07f, 0.012f), Mat.Get(bolt ? Mat.MetalLight : Mat.Flint, 0.4f), ToForward, 4);
            var feather = Mat.Get(bolt ? Mat.TeamRed : new Color(0.75f, 0.72f, 0.65f));
            Shapes.Box(t, new Vector3(0, 0.012f, -len + 0.06f), new Vector3(0.002f, 0.025f, 0.1f), feather, null, false);
            Shapes.Box(t, new Vector3(0.011f, -0.006f, -len + 0.06f), new Vector3(0.025f, 0.002f, 0.1f), feather, Quaternion.Euler(0, 0, 30), false);
            Shapes.Box(t, new Vector3(-0.011f, -0.006f, -len + 0.06f), new Vector3(0.025f, 0.002f, 0.1f), feather, Quaternion.Euler(0, 0, -30), false);
            return root;
        }
    }
}
