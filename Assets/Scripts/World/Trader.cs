using UnityEngine;

namespace Neanderthal
{
    /// <summary>The old one in the cave who trades blueprints for coins. Opens the shop UI.</summary>
    public class Trader : MonoBehaviour, IInteractable
    {
        public string Prompt => "[E] Trade with the Old One";
        public bool CanInteract => true;
        CreatureBody body;
        float t;

        public static Trader Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Trader");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var tr = go.AddComponent<Trader>();
            tr.body = CreatureBodies.Neanderthal(go.transform, null, new Color(0.8f, 0.7f, 0.3f), 99);
            // Grey beard of wisdom.
            Shapes.Ball(tr.body.Neck, new Vector3(0, 0.0f, 0.12f), new Vector3(0.26f, 0.3f, 0.16f), Mat.Get(new Color(0.6f, 0.58f, 0.55f)), false);
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = 0.45f;
            return tr;
        }

        public void Interact(PlayerController player)
        {
            GameEvents.RaiseInteract("trader");
            UIManager.Instance.OpenShop();
        }

        void Update()
        {
            if (body == null) return;
            t += Time.deltaTime;
            // Idle: breathe and glance at the player.
            body.Root.localPosition = new Vector3(0f, Mathf.Sin(t * 1.5f) * 0.01f, 0f);
            body.ArmR.localRotation = Quaternion.Euler(-30f + Mathf.Sin(t) * 8f, 0f, 0f);
            body.ArmL.localRotation = Quaternion.Euler(-35f + Mathf.Sin(t * 0.8f) * 6f, 0f, 0f);
            var p = PlayerController.Instance;
            if (p != null)
            {
                Vector3 d = p.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude < 8f && d.sqrMagnitude > 0.01f)
                {
                    var look = Quaternion.LookRotation(transform.InverseTransformDirection(d.normalized));
                    body.Neck.localRotation = Quaternion.Slerp(body.Neck.localRotation, Quaternion.Euler(0f, Mathf.Clamp(Mathf.DeltaAngle(0f, look.eulerAngles.y), -60f, 60f), 0f), Time.deltaTime * 4f);
                }
            }
        }
    }
}
