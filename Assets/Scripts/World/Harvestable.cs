using UnityEngine;

namespace Neanderthal
{
    /// <summary>Trees, boulders and bushes: hit them with tools to gather resources straight into the inventory.</summary>
    public class Harvestable : MonoBehaviour
    {
        public Resource Yield = Resource.Wood;
        public int Remaining = 6;
        public float HitsPerUnit = 1f;
        public Color ChipColor = Mat.Bark;
        public bool FallsWhenDepleted;

        float progress;
        bool depleted;
        float fallT;
        Vector3 fallAxis;

        public void Hit(float power, Vector3 point, GameObject attacker)
        {
            if (depleted) return;
            Effects.Chips(point, (attacker != null ? attacker.transform.position - point : Vector3.up).normalized, ChipColor);
            Sfx.Play(Yield == Resource.Stone ? "stone" : Yield == Resource.Wood ? "chop" : "thunk", point, 0.8f, Yield == Resource.Stone ? 1.1f : 0.95f);
            progress += power;
            int gained = 0;
            while (progress >= HitsPerUnit && Remaining > 0)
            {
                progress -= HitsPerUnit;
                Remaining--;
                gained++;
            }
            if (gained > 0)
            {
                var player = attacker != null ? attacker.GetComponent<PlayerController>() : null;
                if (player != null) player.Inventory.Add(Yield, gained);
            }
            if (Remaining <= 0) Deplete(attacker);
        }

        void Deplete(GameObject attacker)
        {
            depleted = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (FallsWhenDepleted)
            {
                Vector3 away = attacker != null ? transform.position - attacker.transform.position : Vector3.forward;
                away.y = 0f;
                fallAxis = Vector3.Cross(Vector3.up, away.normalized);
                Sfx.Play("thunk", transform.position + Vector3.up * 3f, 1f, 0.5f);
            }
            else
            {
                Destroy(gameObject, 0.05f);
            }
        }

        void Update()
        {
            if (!depleted || !FallsWhenDepleted) return;
            fallT += Time.deltaTime;
            if (fallT < 1.4f)
                transform.RotateAround(transform.position, fallAxis, Mathf.Lerp(10f, 120f, fallT / 1.4f) * Time.deltaTime);
            else if (fallT < 4f)
                transform.position += Vector3.down * Time.deltaTime * 0.8f;
            else
                Destroy(gameObject);
        }
    }
}
