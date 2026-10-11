using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Procedurally synthesised placeholder sounds so the prototype has audio feedback
    /// without any imported clips. Replace entries in <see cref="clips"/> with real AudioClips later.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioSource uiSource;
        public static float Volume = 0.8f;

        public static void Init()
        {
            if (clips.Count > 0) return;
            clips["swing"] = Make("swing", 0.25f, (t, d) => Noise() * Env(t, 0.02f, d) * 0.35f * Mathf.Sin(t / d * Mathf.PI));
            clips["hit"] = Make("hit", 0.18f, (t, d) => (Noise() * 0.5f + Mathf.Sin(t * 2 * Mathf.PI * 90f)) * Env(t, 0.002f, d) * 0.7f);
            clips["thunk"] = Make("thunk", 0.15f, (t, d) => (Noise() * 0.3f + Mathf.Sin(t * 2 * Mathf.PI * (220f - t * 600f))) * Env(t, 0.001f, d) * 0.6f);
            clips["bow"] = Make("bow", 0.35f, (t, d) => Mathf.Sin(t * 2 * Mathf.PI * (180f - t * 120f)) * Mathf.Exp(-t * 14f) * 0.6f + Noise() * Mathf.Exp(-t * 40f) * 0.2f);
            clips["draw"] = Make("draw", 0.4f, (t, d) => Noise() * 0.08f * (t / d) + Mathf.Sin(t * 2 * Mathf.PI * 60f) * 0.04f);
            clips["gun"] = Make("gun", 0.45f, (t, d) => (Noise() * Mathf.Exp(-t * 18f) + Mathf.Sin(t * 2 * Mathf.PI * 55f) * Mathf.Exp(-t * 9f)) * 0.8f);
            clips["shotgun"] = Make("shotgun", 0.6f, (t, d) => (Noise() * Mathf.Exp(-t * 10f) + Mathf.Sin(t * 2 * Mathf.PI * 45f) * Mathf.Exp(-t * 6f)) * 0.9f);
            clips["rifle"] = Make("rifle", 0.3f, (t, d) => (Noise() * Mathf.Exp(-t * 25f) + Mathf.Sin(t * 2 * Mathf.PI * 70f) * Mathf.Exp(-t * 12f)) * 0.7f);
            clips["sniper"] = Make("sniper", 0.9f, (t, d) => (Noise() * Mathf.Exp(-t * 8f) + Mathf.Sin(t * 2 * Mathf.PI * 40f) * Mathf.Exp(-t * 4f)) * 0.9f);
            clips["reload"] = Make("reload", 0.3f, (t, d) => ((t % 0.12f) < 0.01f ? Noise() * 0.6f : 0f));
            clips["pickup"] = Make("pickup", 0.15f, (t, d) => Mathf.Sin(t * 2 * Mathf.PI * (600f + t * 2000f)) * Env(t, 0.005f, d) * 0.3f);
            clips["coin"] = Make("coin", 0.3f, (t, d) => (Mathf.Sin(t * 2 * Mathf.PI * (t < 0.08f ? 1300f : 1750f))) * Mathf.Exp(-t * 10f) * 0.3f);
            clips["buy"] = Make("buy", 0.4f, (t, d) => (Mathf.Sin(t * 2 * Mathf.PI * 880f) + Mathf.Sin(t * 2 * Mathf.PI * 1320f)) * Mathf.Exp(-t * 6f) * 0.2f);
            clips["deny"] = Make("deny", 0.25f, (t, d) => Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 140f)) * Env(t, 0.005f, d) * 0.15f);
            clips["hitmark"] = Make("hitmark", 0.07f, (t, d) => (Mathf.Sin(t * 2 * Mathf.PI * 2400f) * 0.6f + Noise() * 0.4f) * Env(t, 0.001f, d) * 0.22f);
            clips["headshot"] = Make("headshot", 0.35f, (t, d) => (Mathf.Sin(t * 2 * Mathf.PI * 1850f) + 0.5f * Mathf.Sin(t * 2 * Mathf.PI * 2780f)) * Mathf.Exp(-t * 14f) * 0.22f + Noise() * Mathf.Exp(-t * 60f) * 0.25f);
            clips["killmark"] = Make("killmark", 0.45f, (t, d) => (Mathf.Sin(t * 2 * Mathf.PI * (t < 0.1f ? 900f : 1350f))) * Mathf.Exp(-t * 7f) * 0.2f);
            clips["click"] = Make("click", 0.04f, (t, d) => Noise() * Env(t, 0.001f, d) * 0.3f);
            clips["hurt"] = Make("hurt", 0.35f, (t, d) => Saw(t * (140f - t * 80f)) * Env(t, 0.02f, d) * 0.35f);
            clips["growl"] = Make("growl", 0.7f, (t, d) => Saw(t * (70f + Mathf.Sin(t * 30f) * 8f)) * (0.5f + Noise() * 0.5f) * Env(t, 0.05f, d) * 0.35f);
            clips["trumpet"] = Make("trumpet", 1.2f, (t, d) => Saw(t * (220f + Mathf.Sin(t * 5f) * 40f + t * 80f)) * Env(t, 0.1f, d) * 0.3f);
            clips["roar"] = Make("roar", 1.0f, (t, d) => (Saw(t * (90f - t * 30f)) * 0.5f + Noise() * 0.5f) * Env(t, 0.08f, d) * 0.45f);
            clips["death"] = Make("death", 0.6f, (t, d) => Saw(t * (120f - t * 90f)) * Env(t, 0.02f, d) * 0.35f);
            clips["step"] = Make("step", 0.08f, (t, d) => Noise() * Env(t, 0.002f, d) * 0.12f);
            clips["fire"] = Make("fire", 2.0f, (t, d) => Noise() * (0.04f + (Mathf.PerlinNoise(t * 20f, 0.5f) > 0.75f ? 0.25f : 0f)), true);
            clips["wind"] = Make("wind", 4.0f, (t, d) => Noise() * (0.03f + 0.03f * Mathf.Sin(t * Mathf.PI * 2f / d)), true);
            clips["water"] = Make("water", 3.0f, (t, d) => Noise() * 0.12f, true);
            clips["objective"] = Make("objective", 0.8f, (t, d) => (Mathf.Sin(t * 2 * Mathf.PI * 440f) + Mathf.Sin(t * 2 * Mathf.PI * (t < 0.3f ? 554f : 659f))) * Env(t, 0.02f, d) * 0.15f);
        }

        delegate float Wave(float t, float duration);

        static System.Random rng = new System.Random(1234);
        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
        static float Env(float t, float attack, float d) => Mathf.Clamp01(t / attack) * Mathf.Clamp01((d - t) / (d * 0.6f));

        static AudioClip Make(string name, float duration, Wave wave, bool loopSmooth = false)
        {
            int n = Mathf.CeilToInt(duration * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                data[i] = Mathf.Clamp(wave(t, duration), -1f, 1f);
            }
            if (loopSmooth)
            {
                int fade = Rate / 20;
                for (int i = 0; i < fade && i < n; i++)
                {
                    float k = (float)i / fade;
                    data[i] *= k;
                    data[n - 1 - i] *= k;
                }
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip Get(string name)
        {
            Init();
            return clips.TryGetValue(name, out var c) ? c : null;
        }

        public static void Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            var clip = Get(name);
            if (clip == null) return;
            var go = new GameObject("sfx_" + name);
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume * Volume;
            src.pitch = pitch * Random.Range(0.93f, 1.07f);
            src.spatialBlend = 1f;
            src.minDistance = 2f;
            src.maxDistance = 60f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, src.pitch) + 0.1f);
        }

        public static void Play2D(string name, float volume = 1f, float pitch = 1f)
        {
            var clip = Get(name);
            if (clip == null) return;
            if (uiSource == null)
            {
                var go = new GameObject("UI Audio");
                Object.DontDestroyOnLoad(go);
                uiSource = go.AddComponent<AudioSource>();
                uiSource.spatialBlend = 0f;
            }
            uiSource.pitch = pitch;
            uiSource.PlayOneShot(clip, volume * Volume);
        }

        public static AudioSource Loop(string name, Transform parent, Vector3 localPos, float volume, float maxDistance = 25f)
        {
            var clip = Get(name);
            var go = new GameObject("loop_" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.volume = volume * Volume;
            src.spatialBlend = maxDistance > 0 ? 1f : 0f;
            src.maxDistance = Mathf.Max(1f, maxDistance);
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
            return src;
        }
    }
}
