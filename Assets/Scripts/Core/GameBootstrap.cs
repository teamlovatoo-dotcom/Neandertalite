using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Makes the project playable from any scene: if no GameManager exists when a scene loads, one is created
    /// and the scene's own cameras/lights are switched off (the game builds its own levels at runtime).
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.gameObject.SetActive(false);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) light.gameObject.SetActive(false);
            var go = new GameObject("Neanderthal Game");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<GameManager>();
        }
    }
}
