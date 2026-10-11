using UnityEngine;

namespace Neanderthal
{
    /// <summary>Two-segment bow string whose nock point slides back as the bow is drawn.</summary>
    public class BowString : MonoBehaviour
    {
        Transform segA, segB, nockedArrow;
        Vector3 tipA, tipB;
        float maxPull;
        bool horizontal;
        public float Draw;           // 0..1
        public bool ShowArrow = true;
        public bool IsBolt;
        /// <summary>First-person drawing hand that follows the nock point.</summary>
        public Transform DrawHand;
        bool pouch;
        Transform pouchPad;

        /// <param name="pouch">Slingshot: a leather band with a stone in a pouch instead of an arrow.</param>
        public static BowString Create(Transform parent, Vector3 tipA, Vector3 tipB, Material mat, float maxPull, bool horizontal = false, bool pouch = false)
        {
            var bs = parent.gameObject.AddComponent<BowString>();
            bs.tipA = tipA;
            bs.tipB = tipB;
            bs.maxPull = maxPull;
            bs.horizontal = horizontal;
            bs.segA = Shapes.Cyl(parent, Vector3.zero, Vector3.one, mat, null, false).transform;
            bs.segB = Shapes.Cyl(parent, Vector3.zero, Vector3.one, mat, null, false).transform;
            bs.IsBolt = horizontal;
            bs.pouch = pouch;
            if (pouch)
            {
                bs.nockedArrow = WeaponModels.Stone(parent).transform;
                bs.nockedArrow.localScale *= 0.8f;
                bs.pouchPad = Shapes.Box(parent, Vector3.zero, new Vector3(0.05f, 0.032f, 0.012f), mat, null, false).transform;
            }
            else bs.nockedArrow = WeaponModels.Arrow(parent, horizontal).transform;
            bs.Refresh();
            return bs;
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            Vector3 mid = (tipA + tipB) * 0.5f;
            float rest = pouch ? 0.03f : horizontal ? 0.12f : 0.0f;
            Vector3 nock = new Vector3(mid.x, mid.y, mid.z - rest - Draw * maxPull);
            Place(segA, tipA, nock, pouch ? 0.009f : 0.006f);
            Place(segB, tipB, nock, pouch ? 0.009f : 0.006f);
            if (DrawHand != null) DrawHand.localPosition = nock + new Vector3(pouch ? 0f : 0.004f, pouch ? -0.012f : -0.004f, -0.035f);
            if (pouch)
            {
                nockedArrow.gameObject.SetActive(ShowArrow);
                nockedArrow.localPosition = nock + new Vector3(0f, 0f, 0.012f);
                pouchPad.localPosition = nock;
                pouchPad.localRotation = Quaternion.identity;
                return;
            }
            if (nockedArrow != null)
            {
                nockedArrow.gameObject.SetActive(ShowArrow);
                float len = IsBolt ? 0.42f : 0.75f;
                // Arrow rests on the bow/stock with its nock on the string.
                nockedArrow.localPosition = new Vector3(horizontal ? 0f : 0.02f, horizontal ? 0.035f : 0f, nock.z + len);
                nockedArrow.localRotation = Quaternion.identity;
            }
        }

        static void Place(Transform seg, Vector3 a, Vector3 b, float thick)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            seg.localPosition = (a + b) * 0.5f;
            seg.localRotation = len > 0.0001f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
            seg.localScale = new Vector3(thick, len * 0.5f, thick);
        }
    }
}
