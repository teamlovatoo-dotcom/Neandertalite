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

        static Material flameMat, emberMat, smokeMat;

        static Material MakeMat(string name, Texture tex, bool add, Color tint)
        {
            bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            Shader sh = urp ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : null;
            if (sh == null) sh = Shader.Find(add ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = name, mainTexture = tex };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, 0.5f));
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", add ? 2f : 0f);
            // Additive still weighs by alpha (SrcAlpha, One): soft sprites fade out instead of drawing as squares.
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)(add ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }

        static Material ParticleMat(bool add)
        {
            if (add && additive != null) return additive;
            if (!add && alphaBlend != null) return alphaBlend;
            var m = MakeMat(add ? "FxAdditive" : "FxAlpha", Soft, add, Color.white);
            if (add) additive = m; else alphaBlend = m;
            return m;
        }

        static Texture2D FireSheet => Resources.Load<Texture2D>("Textures/fx_fire");
        static Texture2D SmokeSheet => Resources.Load<Texture2D>("Textures/fx_smoke");

        /// <summary>Flipbook flame material (premultiplied, additive, HDR-bright core for bloom).</summary>
        static Material FlameMat
        {
            get
            {
                if (flameMat != null) return flameMat;
                var tex = FireSheet;
                flameMat = tex != null ? MakeMat("FxFlame", tex, true, new Color(2.2f, 2f, 1.8f)) : ParticleMat(true);
                return flameMat;
            }
        }

        static Material EmberMat => emberMat != null ? emberMat : (emberMat = MakeMat("FxEmber", Soft, true, new Color(4f, 2.4f, 1.2f)));

        static Material SmokeMat
        {
            get
            {
                if (smokeMat != null) return smokeMat;
                var tex = SmokeSheet;
                smokeMat = tex != null ? MakeMat("FxSmoke", tex, false, Color.white) : ParticleMat(false);
                return smokeMat;
            }
        }

        static ParticleSystem System(Transform parent, Vector3 localPos, string name, bool add, bool local)
            => System(parent, localPos, name, ParticleMat(add), local);

        static ParticleSystem System(Transform parent, Vector3 localPos, string name, Material mat, bool local)
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
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void Sheet(ParticleSystem ps, int x, int y, bool animate)
        {
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = x;
            tsa.numTilesY = y;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.frameOverTime = animate
                ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f))
                : new ParticleSystem.MinMaxCurve(0f, 0.99f);
            tsa.cycleCount = 1;
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

            bool sheet = FireSheet != null;
            // Flipbook flame tongues: a few large, slowly rising billboards, each playing the whole loop.
            var flames = System(root, Vector3.zero, "Flames", FlameMat, localSpace);
            var main = flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f * scale, 0.3f * scale);
            main.startSize = sheet ? new ParticleSystem.MinMaxCurve(1.0f * scale, 1.45f * scale) : new ParticleSystem.MinMaxCurve(0.35f * scale, 0.7f * scale);
            main.startRotation = sheet ? new ParticleSystem.MinMaxCurve(-0.18f, 0.18f) : new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.maxParticles = 40;
            var em = flames.emission; em.rateOverTime = sheet ? 11f : 45f;
            var sh = flames.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.2f * scale;
            var col = flames.colorOverLifetime; col.enabled = true;
            col.color = sheet
                ? Grad((0f, new Color(0f, 0f, 0f, 1f)), (0.15f, new Color(1f, 1f, 1f, 1f)), (0.75f, new Color(0.9f, 0.85f, 0.8f, 1f)), (1f, new Color(0f, 0f, 0f, 1f)))
                : Grad((0f, new Color(1f, 0.95f, 0.6f, 0f)), (0.15f, new Color(1f, 0.8f, 0.35f, 0.9f)), (0.6f, new Color(1f, 0.35f, 0.08f, 0.6f)), (1f, new Color(0.4f, 0.05f, 0.02f, 0f)));
            if (sheet)
            {
                Sheet(flames, 8, 8, true);
                var fr = flames.GetComponent<ParticleSystemRenderer>();
                fr.pivot = new Vector3(0f, 0.38f, 0f);
                fr.flip = new Vector3(0.5f, 0f, 0f);
                fr.sortingFudge = -1f;
            }
            else
            {
                var sz = flames.sizeOverLifetime; sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));
            }
            var noise = flames.noise; noise.enabled = true; noise.strength = 0.12f * scale; noise.frequency = 1.5f;
            flames.Play();

            // Warm glow pooled at the base.
            var glow = System(root, Vector3.up * 0.1f * scale, "Glow", ParticleMat(true), localSpace);
            var gm = glow.main;
            gm.startLifetime = 0.5f;
            gm.startSpeed = 0f;
            gm.startSize = new ParticleSystem.MinMaxCurve(1.4f * scale, 1.9f * scale);
            gm.startColor = new Color(1f, 0.45f, 0.12f, 0.22f);
            gm.maxParticles = 8;
            var ge = glow.emission; ge.rateOverTime = 10f;
            var gs = glow.shape; gs.enabled = false;
            var gc = glow.colorOverLifetime; gc.enabled = true;
            gc.color = Grad((0f, new Color(1f, 1f, 1f, 0f)), (0.5f, new Color(1f, 1f, 1f, 1f)), (1f, new Color(1f, 1f, 1f, 0f)));
            glow.Play();

            // Sparks: hot, stretched streaks that drift up and cool down.
            var embers = System(root, Vector3.up * 0.15f * scale, "Embers", EmberMat, localSpace);
            var em2 = embers.main;
            em2.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 2.2f);
            em2.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * scale, 3f * scale);
            em2.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.04f);
            em2.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.8f, 0.45f));
            em2.gravityModifier = -0.05f;
            em2.maxParticles = 80;
            var e2 = embers.emission; e2.rateOverTime = 12f * Mathf.Max(0.4f, scale);
            e2.SetBursts(new[] { new ParticleSystem.Burst(0.5f, 3, 8, 0, 1.7f) });
            var s2 = embers.shape; s2.shapeType = ParticleSystemShapeType.Cone; s2.angle = 20f; s2.radius = 0.22f * scale;
            var n2 = embers.noise; n2.enabled = true; n2.strength = 1.1f; n2.frequency = 1.4f; n2.scrollSpeed = 0.6f;
            var c2 = embers.colorOverLifetime; c2.enabled = true;
            c2.color = Grad((0f, new Color(1f, 0.85f, 0.5f, 1f)), (0.6f, new Color(1f, 0.4f, 0.1f, 0.9f)), (1f, new Color(0.6f, 0.1f, 0.02f, 0f)));
            var er = embers.GetComponent<ParticleSystemRenderer>();
            er.renderMode = ParticleSystemRenderMode.Stretch;
            er.velocityScale = 0.035f;
            er.lengthScale = 1.2f;
            embers.Play();

            if (smoke)
            {
                bool smokeSheet = SmokeSheet != null;
                var sm = System(root, Vector3.up * 0.9f * scale, "Smoke", SmokeMat, localSpace);
                var m3 = sm.main;
                m3.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6.5f);
                m3.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
                m3.startSize = new ParticleSystem.MinMaxCurve(0.7f * scale, 1.2f * scale);
                m3.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m3.startColor = new ParticleSystem.MinMaxGradient(new Color(0.32f, 0.3f, 0.29f, 0.5f), new Color(0.45f, 0.43f, 0.41f, 0.4f));
                m3.maxParticles = 50;
                var e3 = sm.emission; e3.rateOverTime = 6f;
                var s3 = sm.shape; s3.shapeType = ParticleSystemShapeType.Cone; s3.angle = 8f; s3.radius = 0.2f * scale;
                var c3 = sm.colorOverLifetime; c3.enabled = true;
                c3.color = Grad((0f, new Color(1f, 1f, 1f, 0f)), (0.15f, new Color(1f, 1f, 1f, 0.7f)), (0.6f, new Color(1f, 1f, 1f, 0.35f)), (1f, new Color(1f, 1f, 1f, 0f)));
                var z3 = sm.sizeOverLifetime; z3.enabled = true;
                z3.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 3.5f)));
                var r3 = sm.rotationOverLifetime; r3.enabled = true; r3.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                var n3 = sm.noise; n3.enabled = true; n3.strength = 0.4f; n3.frequency = 0.35f;
                var v3 = sm.velocityOverLifetime; v3.enabled = true; v3.space = ParticleSystemSimulationSpace.World;
                v3.x = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); v3.y = new ParticleSystem.MinMaxCurve(0f, 0f); v3.z = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                if (smokeSheet) Sheet(sm, 2, 2, false);
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
