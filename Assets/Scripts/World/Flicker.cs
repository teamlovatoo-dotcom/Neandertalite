using UnityEngine;

namespace Neanderthal
{
    /// <summary>Makes a flame mesh and any light under the same parent flicker.</summary>
    public class Flicker : MonoBehaviour
    {
        Vector3 baseScale;
        Light lightSource;
        float baseIntensity;
        float seed;

        void Start()
        {
            baseScale = transform.localScale;
            lightSource = transform.parent != null ? transform.parent.GetComponentInChildren<Light>() : null;
            if (lightSource != null) baseIntensity = lightSource.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(Time.time * 8f, seed);
            transform.localScale = new Vector3(baseScale.x * (0.85f + n * 0.3f), baseScale.y * (0.8f + n * 0.45f), baseScale.z * (0.85f + n * 0.3f));
            if (lightSource != null) lightSource.intensity = baseIntensity * (0.75f + n * 0.5f);
        }
    }
}
