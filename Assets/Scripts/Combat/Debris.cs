using UnityEngine;

namespace Neanderthal
{
    /// <summary>A small chip that flies, falls, shrinks and goes back to the effects pool.</summary>
    public class Debris : MonoBehaviour
    {
        public Vector3 Velocity;
        public float Life = 0.7f;
        float age;
        Vector3 startScale;

        public void Launch(Vector3 scale, Vector3 velocity)
        {
            startScale = scale;
            transform.localScale = scale;
            Velocity = velocity;
            age = 0f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.deltaTime;
            Velocity += Vector3.down * 12f * Time.deltaTime;
            transform.position += Velocity * Time.deltaTime;
            transform.localScale = startScale * Mathf.Clamp01(1f - age / Life);
            if (age >= Life) Effects.Release(this);
        }
    }
}
