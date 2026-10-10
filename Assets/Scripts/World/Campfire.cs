using UnityEngine;

namespace Neanderthal
{
    /// <summary>Warmth heals nearby players. Interact to rest and set your respawn point.</summary>
    public class Campfire : MonoBehaviour, IInteractable
    {
        public float HealPerSecond = 4f;
        public float Radius = 3.5f;
        public bool IsShelter;
        float restCooldown;

        public string Prompt => IsShelter ? "[E] Rest in shelter (respawn point)" : "[E] Rest by the fire (respawn point)";
        public bool CanInteract => true;

        public void Interact(PlayerController player)
        {
            if (GameManager.Instance != null) GameManager.Instance.SetRespawn(transform.position + transform.forward * 1.8f + Vector3.up * 0.2f);
            if (IsShelter && restCooldown <= 0f)
            {
                player.Health.Heal(50f);
                restCooldown = 60f;
                Inventory.Notify("Rested: +50 health", new Color(0.5f, 1f, 0.5f));
            }
            else Inventory.Notify("Respawn point set", new Color(1f, 0.8f, 0.5f));
            Sfx.Play2D("objective", 0.5f);
        }

        void Update()
        {
            restCooldown -= Time.deltaTime;
            if (IsShelter) return;
            var p = PlayerController.Instance;
            if (p == null) return;
            if ((p.transform.position - transform.position).sqrMagnitude < Radius * Radius)
                p.Health.Heal(HealPerSecond * Time.deltaTime);
        }

        public static Campfire Create(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Campfire");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var t = go.transform;
            var stone = Mat.Get(Mat.Stone);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Shapes.Ball(t, new Vector3(Mathf.Cos(a) * 0.6f, 0.08f, Mathf.Sin(a) * 0.6f), new Vector3(0.3f, 0.2f, 0.25f), stone, false);
            }
            var wood = Mat.Get(Mat.WoodDark);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                Shapes.Stick(t, new Vector3(Mathf.Cos(a) * 0.45f, 0.05f, Mathf.Sin(a) * 0.45f), new Vector3(0, 0.45f, 0), 0.08f, wood);
            }
            Shapes.PointLight(t, new Vector3(0, 0.9f, 0), Mat.Fire, 11f, 2.4f, true);
            var flame = Shapes.Ball(t, new Vector3(0, 0.35f, 0), new Vector3(0.45f, 0.65f, 0.45f), Mat.Emissive(Mat.Fire, 3f), false);
            Shapes.SetShadows(flame, false);
            flame.AddComponent<Flicker>();
            var inner = Shapes.Ball(t, new Vector3(0, 0.3f, 0), new Vector3(0.25f, 0.4f, 0.25f), Mat.Emissive(new Color(1f, 0.9f, 0.5f), 4f), false);
            Shapes.SetShadows(inner, false);
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.7f;
            col.center = new Vector3(0, 0.3f, 0);
            Sfx.Loop("fire", t, Vector3.up * 0.4f, 0.6f, 14f);
            return go.AddComponent<Campfire>();
        }

        public static Campfire CreateShelter(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Shelter");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var t = go.transform;
            var wood = Mat.Get(Mat.Wood);
            var hide = Mat.Get(Mat.Fur);
            // Lean-to: ridge pole on two forks, hides draped as the roof.
            Shapes.Stick(t, new Vector3(-1.4f, 0f, 0.6f), new Vector3(-1.4f, 1.8f, 0.6f), 0.1f, wood, true);
            Shapes.Stick(t, new Vector3(1.4f, 0f, 0.6f), new Vector3(1.4f, 1.8f, 0.6f), 0.1f, wood, true);
            Shapes.Stick(t, new Vector3(-1.6f, 1.8f, 0.6f), new Vector3(1.6f, 1.8f, 0.6f), 0.09f, wood);
            Shapes.Box(t, new Vector3(0, 0.95f, -0.3f), new Vector3(3f, 0.06f, 2.1f), hide, Quaternion.Euler(-58f, 0, 0));
            Shapes.Box(t, new Vector3(0, 0.03f, -0.2f), new Vector3(1.6f, 0.05f, 1.2f), Mat.Get(Mat.FurDark), null, false);
            var c = go.AddComponent<Campfire>();
            c.IsShelter = true;
            return c;
        }
    }
}
