using UnityEngine;

namespace Neanderthal
{
    /// <summary>Anything the player can use with [E]: traders, workbenches, campfires, shelters.</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract { get; }
        void Interact(PlayerController player);
    }

    /// <summary>Looks for interactables under the crosshair; also handles the heal (H) and crafting (Tab) keys.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public float Reach = 3f;
        public IInteractable Focus { get; private set; }
        PlayerController player;

        void Start() => player = GetComponent<PlayerController>();

        void Update()
        {
            Focus = null;
            if (player == null || player.InputLocked)
            {
                return;
            }

            var cam = player.Cam.transform;
            if (PhysicsUtil.SphereCast(cam.position, 0.15f, cam.forward, Reach, transform, out var hit))
            {
                foreach (var mb in hit.collider.GetComponentsInParent<MonoBehaviour>())
                {
                    if (mb is IInteractable it && it.CanInteract)
                    {
                        Focus = it;
                        break;
                    }
                }
            }

            if (Focus != null && GameInput.InteractDown) Focus.Interact(player);

            if (GameInput.HealDown) UseHerbs();
            if (GameInput.CraftingDown && GameManager.Instance != null && GameManager.Instance.Mode == GameMode.Survival)
                UIManager.Instance.OpenCrafting(false);
        }

        void UseHerbs()
        {
            var inv = player.Inventory;
            if (inv.Get(Resource.Herbs) <= 0)
            {
                Inventory.Notify("No healing herbs (buy from the trader or find red-berry bushes)", new Color(1f, 0.5f, 0.4f));
                Sfx.Play2D("deny", 0.5f);
                return;
            }
            if (player.Health.Current >= player.Health.Max) return;
            inv.Add(Resource.Herbs, -1, false);
            player.Health.Heal(35f);
            Sfx.Play2D("pickup", 0.7f, 0.7f);
            Inventory.Notify("+35 health", new Color(0.5f, 1f, 0.5f));
        }
    }
}
