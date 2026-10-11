using UnityEngine;

namespace Neanderthal
{
    public enum HitZone { Body, Head, Chest, Stomach, Arm, Leg }

    /// <summary>Marks a collider as a body part: the zone decides the damage multiplier (head x2.5, limbs less).</summary>
    public class Hitbox : MonoBehaviour
    {
        public float Multiplier = 1f;
        public bool IsHead;
        public HitZone Zone = HitZone.Body;

        public static string ZoneName(HitZone z)
        {
            switch (z)
            {
                case HitZone.Head: return "голова";
                case HitZone.Chest: return "груди";
                case HitZone.Stomach: return "живіт";
                case HitZone.Arm: return "рука";
                case HitZone.Leg: return "нога";
                default: return "тіло";
            }
        }
    }
}
