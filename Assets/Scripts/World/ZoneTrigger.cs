using UnityEngine;

namespace Neanderthal
{
    /// <summary>Fires a GameEvents zone event the first time the player walks into a radius.</summary>
    public class ZoneTrigger : MonoBehaviour
    {
        public string Id;
        public float Radius = 6f;
        public bool Once = true;
        bool inside;

        public static ZoneTrigger Create(Transform parent, Vector3 pos, string id, float radius)
        {
            var go = new GameObject("Zone_" + id);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var z = go.AddComponent<ZoneTrigger>();
            z.Id = id;
            z.Radius = radius;
            return z;
        }

        void Update()
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            bool now = (p.transform.position - transform.position).sqrMagnitude < Radius * Radius;
            if (now && !inside)
            {
                GameEvents.RaiseZone(Id);
                if (Once) { Destroy(gameObject); return; }
            }
            inside = now;
        }
    }
}
