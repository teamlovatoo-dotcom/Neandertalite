using UnityEngine;

namespace Neanderthal
{
    /// <summary>Where blueprints become steel. Needs the Workbench blueprint from the trader.</summary>
    public class Workbench : MonoBehaviour, IInteractable
    {
        public string Prompt => Profile.Current.HasBlueprint("bp_workbench")
            ? "[E] Скористатися верстаком"
            : "Верстак (купи креслення верстака в торговця)";
        public bool CanInteract => true;

        public void Interact(PlayerController player)
        {
            if (!Profile.Current.HasBlueprint("bp_workbench"))
            {
                Inventory.Notify("Потрібне креслення верстака (Торговець » КРЕСЛЕННЯ)", new Color(1f, 0.55f, 0.4f));
                Sfx.Play2D("deny", 0.6f);
                return;
            }
            GameEvents.RaiseInteract("workbench");
            UIManager.Instance.OpenCrafting(true);
        }

        public static Workbench Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Workbench");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var t = go.transform;
            var wood = Mat.Get(Mat.WoodDark);
            var metal = Mat.Get(Mat.Metal, 0.5f, 0.7f);
            Shapes.Box(t, new Vector3(0, 0.9f, 0), new Vector3(2.4f, 0.1f, 1f), wood);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Shapes.Box(t, new Vector3(1.1f * x, 0.45f, 0.42f * z), new Vector3(0.1f, 0.9f, 0.1f), metal, null, false);
            Shapes.Box(t, new Vector3(0, 0.25f, 0), new Vector3(2.2f, 0.05f, 0.9f), wood, null, false);
            // Vice, tools, glowing tablet from the tower, blueprint board.
            Shapes.Box(t, new Vector3(-0.8f, 1.05f, 0.2f), new Vector3(0.25f, 0.2f, 0.2f), metal, null, false);
            Shapes.Box(t, new Vector3(0.6f, 0.97f, 0.1f), new Vector3(0.5f, 0.03f, 0.35f), Mat.Emissive(Mat.TowerGlow, 1.2f), Quaternion.Euler(0, 10, 0), false);
            Shapes.Stick(t, new Vector3(0.1f, 0.97f, -0.2f), new Vector3(0.4f, 0.97f, -0.3f), 0.03f, Mat.Get(Mat.Wood));
            Shapes.Box(t, new Vector3(0, 1.9f, 0.55f), new Vector3(2.4f, 1.3f, 0.05f), Mat.Get(new Color(0.12f, 0.22f, 0.35f)), null, false);
            var lines = Mat.Emissive(new Color(0.6f, 0.8f, 1f), 0.8f);
            Shapes.Box(t, new Vector3(-0.5f, 2.0f, 0.52f), new Vector3(0.9f, 0.04f, 0.01f), lines, null, false);
            Shapes.Box(t, new Vector3(-0.3f, 1.85f, 0.52f), new Vector3(0.5f, 0.12f, 0.01f), lines, null, false);
            Shapes.Box(t, new Vector3(0.6f, 2.1f, 0.52f), new Vector3(0.6f, 0.03f, 0.01f), lines, null, false);
            Shapes.Box(t, new Vector3(0.6f, 1.7f, 0.52f), new Vector3(0.7f, 0.03f, 0.01f), lines, null, false);
            Shapes.PointLight(t, new Vector3(0, 2.8f, -0.5f), new Color(0.75f, 0.85f, 1f), 7f, 1.6f, true);
            return go.AddComponent<Workbench>();
        }
    }
}
