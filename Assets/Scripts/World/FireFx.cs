using UnityEngine;

namespace Neanderthal
{
    /// <summary>Particle fire (flames, embers, smoke) and ambient floating motes. Works on URP and Built-in.</summary>
    public static class FireFx
    {
        static Texture2D soft;
        static Material additive, alphaBlend;

        static Texture2D Soft
        {
            get
            {
                if (soft != null) return soft;
                int s = 64;
                soft = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    soft.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                soft.Apply();
                return soft;
            }
        }

        static Material ParticleMat(bool add)
        {
            if (add && additive != null) return additive;
            if (!add && alphaBlend != null) return alphaBlend;
            bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            Shader sh = urp ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : null;
            if (sh == null) sh = Shader.Find(add ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = add ? "FxAdditive" : "FxAlpha", mainTexture = Soft };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Soft);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", add ? 2f : 0f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)(add ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)(add ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            if (add) additive = m; else alphaBlend = m;
            return m;
        }

        static ParticleSystem System(Transform parent, Vector3 localPos, string name, bool add, bool local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMat(add);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static Gradient Grad(params (float t, Color c)[] keys)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[keys.Length];
            var ak = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                ck[i] = new GradientColorKey(keys[i].c, keys[i].t);
                ak[i] = new GradientAlphaKey(keys[i].c.a, keys[i].t);
            }
            g.SetKeys(ck, ak);
            return g;
        }

        /// <summary>Flames + embers (+ smoke). Scale 1 = campfire, ~0.35 = torch.</summary>
        public static Transform Fire(Transform parent, Vector3 localPos, float scale, bool smoke, bool localSpace = false,
            float lightRange = 0f, float lightIntensity = 0f, bool lightShadows = false)
        {
            var root = new GameObject("Fire").transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            if (lightRange > 0f) Shapes.PointLight(root, Vector3.up * 0.5f * scale, Mat.Fire, lightRange, lightIntensity, lightShadows);

            var flames = System(root, Vector3.zero, "Flames", true, localSpace);
            var main = flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.5f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.7f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.45f, 0.12f));
            main.maxParticles = 120;
            var em = flames.emission; em.rateOverTime = 45f;
            var sh = flames.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = 0.22f * scale;
            var col = flames.colorOverLifetime; col.enabled = true;
            col.color = Grad((0f, new Color(1f, 0.95f, 0.6f, 0f)), (0.15f, new Color(1f, 0.8f, 0.35f, 0.9f)), (0.6f, new Color(1f, 0.35f, 0.08f, 0.6f)), (1f, new Color(0.4f, 0.05f, 0.02f, 0f)));
            var sz = flames.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));
            var noise = flames.noise; noise.enabled = true; noise.strength = 0.35f * scale; noise.frequency = 2f;
            flames.Play();

            var embers = System(root, Vector3.up * 0.1f * scale, "Embers", true, localSpace);
            var em2 = embers.main;
            em2.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            em2.startSpeed = new ParticleSystem.MinMaxCurve(1f * scale, 2.6f * scale);
            em2.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            em2.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.85f, 0.4f));
            em2.maxParticles = 60;
            var e2 = embers.emission; e2.rateOverTime = 10f * Mathf.Max(0.4f, scale);
            var s2 = embers.shape; s2.shapeType = ParticleSystemShapeType.Cone; s2.angle = 18f; s2.radius = 0.2f * scale;
            var n2 = embers.noise; n2.enabled = true; n2.strength = 0.9f; n2.frequency = 1.2f;
            var c2 = embers.colorOverLifetime; c2.enabled = true;
            c2.color = Grad((0f, new Color(1f, 0.8f, 0.4f, 1f)), (1f, new Color(1f, 0.3f, 0.1f, 0f)));
            embers.Play();

            if (smoke)
            {
                var sm = System(root, Vector3.up * 0.6f * scale, "Smoke", false, localSpace);
                var m3 = sm.main;
                m3.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
                m3.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
                m3.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.1f * scale);
                m3.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m3.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.29f, 0.28f, 0.35f));
                m3.maxParticles = 40;
                var e3 = sm.emission; e3.rateOverTime = 5f;
                var s3 = sm.shape; s3.shapeType = ParticleSystemShapeType.Cone; s3.angle = 10f; s3.radius = 0.2f * scale;
                var c3 = sm.colorOverLifetime; c3.enabled = true;
                c3.color = Grad((0f, new Color(0.35f, 0.33f, 0.3f, 0f)), (0.2f, new Color(0.3f, 0.29f, 0.28f, 0.35f)), (1f, new Color(0.5f, 0.5f, 0.5f, 0f)));
                var z3 = sm.sizeOverLifetime; z3.enabled = true;
                z3.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 3f)));
                var n3 = sm.noise; n3.enabled = true; n3.strength = 0.5f; n3.frequency = 0.5f;
                sm.Play();
            }
            new GameObject("Flicker").transform.SetParent(root, false);
            root.Find("Flicker").gameObject.AddComponent<Flicker>();
            return root;
        }

        /// <summary>Slow, sunlit dust motes / pollen around a moving transform (the player camera).</summary>
        public static void Motes(Transform follow, Color color)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(follow, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
            main.startColor = color;
            main.maxParticles = 250;
            var em = ps.emission; em.rateOverTime = 30f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(24f, 8f, 24f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.15f; noise.frequency = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad((0f, new Color(1f, 1f, 1f, 0f)), (0.3f, new Color(1f, 1f, 1f, 1f)), (0.7f, new Color(1f, 1f, 1f, 1f)), (1f, new Color(1f, 1f, 1f, 0f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMat(true);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }
    }
}
