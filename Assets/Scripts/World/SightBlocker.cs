using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// A see-through-but-not-really volume (a waterfall curtain): arrows and players pass through it,
    /// but bots can't spot you behind it and name tags are hidden.
    /// </summary>
    public class SightBlocker : MonoBehaviour
    {
        static readonly List<SightBlocker> all = new List<SightBlocker>();
        Collider col;

        void OnEnable() { col = GetComponent<Collider>(); all.Add(this); }
        void OnDisable() => all.Remove(this);

        public static bool Blocks(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return false;
            var ray = new Ray(from, d / len);
            foreach (var b in all)
                if (b != null && b.col != null && b.col.Raycast(ray, out _, len)) return true;
            return false;
        }

        public static void Add(GameObject go, Vector3 center, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = center;
            box.size = size;
            go.AddComponent<SightBlocker>();
        }
    }
}
