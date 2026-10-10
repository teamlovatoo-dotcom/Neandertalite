using UnityEngine;

namespace Neanderthal
{
    /// <summary>A tiny chunk that flies, falls and shrinks away.</summary>
    public class Debris : MonoBehaviour
    {
        public Vector3 Velocity;
        public float Life = 0.7f;
        float age;
        Vector3 startScale;

        void Start() => startScale = transform.localScale;

        void Update()
        {
            age += Time.deltaTime;
            Velocity += Vector3.down * 12f * Time.deltaTime;
            transform.position += Velocity * Time.deltaTime;
            transform.localScale = startScale * Mathf.Clamp01(1f - age / Life);
            if (age >= Life) Destroy(gameObject);
        }
    }
}
