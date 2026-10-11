using UnityEngine;

namespace Neanderthal
{
    /// <summary>Base class for game modes (Arena PvP, Survival). Lives on the level root.</summary>
    public abstract class ModeBase : MonoBehaviour
    {
        public virtual string ObjectiveHeader => "";
        public virtual string ObjectiveText => "";
        public virtual string ObjectiveHint => "";
        public virtual bool TryGetRadarTarget(out Vector3 pos, out Color color)
        {
            pos = default;
            color = default;
            return false;
        }
        public virtual void DrawHud() { }

        protected float deadTimer = -1f;

        /// <summary>Common "you died" overlay with a respawn countdown.</summary>
        protected void DrawDeathOverlay(string killer)
        {
            if (deadTimer < 0f) return;
            float W = UIStyle.Width, H = UIStyle.Height;
            UIStyle.Rect(new Rect(0, 0, W, H), new Color(0.15f, 0f, 0f, 0.35f));
            UIStyle.Shadowed(new Rect(0, H * 0.38f, W, 80), "ТИ ЗАГИНУВ", 64, new Color(0.9f, 0.3f, 0.25f), TextAnchor.MiddleCenter, true);
            if (!string.IsNullOrEmpty(killer))
                UIStyle.Shadowed(new Rect(0, H * 0.38f + 80, W, 30), "тебе вбив: " + killer, 20, UIStyle.TextColor, TextAnchor.MiddleCenter);
            UIStyle.Shadowed(new Rect(0, H * 0.38f + 120, W, 30), $"відродження через {Mathf.CeilToInt(deadTimer)}", 18, UIStyle.TextDim, TextAnchor.MiddleCenter);
        }

        protected static string NameOf(GameObject attacker)
        {
            if (attacker == null) return "падіння";
            var c = attacker.GetComponentInParent<Creature>();
            if (c != null) return string.IsNullOrEmpty(c.DisplayName) ? Creature.TypeName(c.Type) : c.DisplayName;
            if (attacker.GetComponentInParent<PlayerController>() != null) return "ти сам";
            return attacker.name;
        }
    }
}
