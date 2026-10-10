using UnityEngine;

namespace Neanderthal
{
    /// <summary>Marks a collider as a body part. Head hitboxes deal extra damage.</summary>
    public class Hitbox : MonoBehaviour
    {
        public float Multiplier = 1f;
        public bool IsHead;
    }
}
