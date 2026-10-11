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

        public string Prompt => IsShelter ? "[E] Відпочити в притулку (точка відродження)" : "[E] Відпочити біля вогню (точка відродження)";
        public bool CanInteract => true;

        public void Interact(PlayerController player)
        {
            if (GameManager.Instance != null) GameManager.Instance.SetRespawn(transform.position + transform.forward * 1.8f + Vector3.up * 0.2f);
            if (IsShelter && restCooldown <= 0f)
            {
                player.Health.Heal(50f);
                restCooldown = 60f;
                Inventory.Notify("Відпочинок: +50 здоров'я", new Color(0.5f, 1f, 0.5f));
            }
            else Inventory.Notify("Точку відродження встановлено", new Color(1f, 0.8f, 0.5f));
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
            if (PropLibrary.Has(PropLibrary.Kind.FirePit))
            {
                PropLibrary.Spawn(PropLibrary.Kind.FirePit, t, pos, Random.Range(0f, 360f), 0.95f);
            }
            else
            {
                var stone = Mat.Get(Mat.Stone);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f;
                    Shapes.Ball(t, new Vector3(Mathf.Cos(a) * 0.6f, 0.08f, Mathf.Sin(a) * 0.6f), new Vector3(0.3f, 0.2f, 0.25f), stone, false);
                }
            }
            var wood = Mat.Get(Mat.WoodDark);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                Shapes.Stick(t, new Vector3(Mathf.Cos(a) * 0.45f, 0.05f, Mathf.Sin(a) * 0.45f), new Vector3(0, 0.45f, 0), 0.08f, wood);
            }
            FireFx.Fire(t, new Vector3(0, 0.15f, 0), 1f, true, false, 12f, 2.6f, true);
            // glowing coals
            var coals = Shapes.Ball(t, new Vector3(0, 0.06f, 0), new Vector3(0.6f, 0.12f, 0.6f), Mat.Emissive(new Color(1f, 0.35f, 0.08f), 2.5f), false);
            Shapes.SetShadows(coals, false);
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
