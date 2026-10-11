using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Cheap code-only effects: dust puffs, blood droplets, chips, tracers, muzzle flashes.
    /// Puffs and droplets are emitted from a few shared world-space particle systems, and solid chips come from a
    /// reusable pool, so a long fight creates no new objects (no garbage-collection hitches).
    /// </summary>
    public static class Effects
    {
        static Transform fxRoot;
        static ParticleSystem puffs, drops;
        static readonly Stack<Debris> pool = new Stack<Debris>();

        /// <summary>Moderate blood: a few droplets and a faint mist, no gore.</summary>
        public static void Blood(Vector3 pos, Vector3 normal)
        {
            Color c = new Color(0.42f, 0.03f, 0.02f);
            Emit(Drops, pos, normal, 9, 2.2f, 0.03f, 0.065f, 0.35f, 0.6f, c);
            Emit(Puffs, pos, normal, 2, 0.4f, 0.12f, 0.22f, 0.25f, 0.4f, new Color(0.35f, 0.02f, 0.02f, 0.45f));
        }

        public static void Dust(Vector3 pos, Vector3 normal)
        {
            Emit(Puffs, pos, normal, 4, 0.7f, 0.18f, 0.45f, 0.6f, 1.0f, new Color(0.5f, 0.45f, 0.38f, 0.55f));
            Burst(pos, normal, new Color(0.42f, 0.38f, 0.32f), 3, 1.8f, 0.035f);
        }

        public static void Chips(Vector3 pos, Vector3 normal, Color c)
        {
            Burst(pos, normal, c, 6, 3f, 0.06f);
            Emit(Puffs, pos, normal, 2, 0.5f, 0.15f, 0.3f, 0.5f, 0.8f, new Color(c.r, c.g, c.b, 0.4f));
        }

        /// <summary>Landing / footstep dust at the feet.</summary>
        public static void FootDust(Vector3 pos, float amount)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(amount * 6f), 1, 8);
            Emit(Puffs, pos + Vector3.up * 0.05f, Vector3.up, n, 0.9f * amount, 0.2f, 0.5f, 0.5f, 0.9f, new Color(0.48f, 0.43f, 0.36f, 0.4f));
        }

        static Transform Root
        {
            get
            {
                if (fxRoot == null)
                {
                    fxRoot = new GameObject("Fx Pool").transform;
                    Object.DontDestroyOnLoad(fxRoot.gameObject);
                }
                return fxRoot;
            }
        }

        static ParticleSystem Puffs => puffs != null ? puffs : (puffs = MakeSystem("Puffs", -0.15f, true));
        static ParticleSystem Drops => drops != null ? drops : (drops = MakeSystem("Drops", 1.1f, false));

        static ParticleSystem MakeSystem(string name, float gravity, bool grow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            if (grow)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FireFx.SoftAlphaMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 normal, int count, float speed, float minSize, float maxSize, float minLife, float maxLife, Color color)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.05f;
                ep.velocity = (normal + Random.insideUnitSphere * 0.8f).normalized * speed * Random.Range(0.4f, 1.1f);
                ep.startSize = Random.Range(minSize, maxSize);
                ep.startLifetime = Random.Range(minLife, maxLife);
                ep.startColor = color;
                ep.rotation = Random.Range(0f, 360f);
                ps.Emit(ep, 1);
            }
        }

        /// <summary>Solid little chips (stone, wood) flying off a hit. Pooled.</summary>
        public static void Burst(Vector3 pos, Vector3 normal, Color color, int count, float speed, float size)
        {
            var mat = Mat.Get(color);
            for (int i = 0; i < count; i++)
            {
                Debris d;
                if (pool.Count > 0 && pool.Peek() != null) d = pool.Pop();
                else
                {
                    if (pool.Count > 0) pool.Pop();   // destroyed with a level: forget it
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.Destroy(go.GetComponent<Collider>());
                    go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    go.transform.SetParent(Root, false);
                    d = go.AddComponent<Debris>();
                }
                d.GetComponent<Renderer>().sharedMaterial = mat;
                d.transform.position = pos;
                d.transform.rotation = Random.rotation;
                d.Launch(Vector3.one * size * Random.Range(0.6f, 1.3f),
                         (normal + Random.insideUnitSphere * 0.9f).normalized * speed * Random.Range(0.5f, 1.2f));
            }
        }

        public static void Release(Debris d)
        {
            d.gameObject.SetActive(false);
            if (pool.Count < 200) pool.Push(d);
            else Object.Destroy(d.gameObject);
        }

        public static void Tracer(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.02f;
            lr.endWidth = 0.01f;
            lr.sharedMaterial = Mat.Flat(color);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(go, 0.05f);
        }

        public static void MuzzleFlash(Transform parent, Vector3 localPos)
        {
            var flash = Shapes.Ball(parent, localPos, Vector3.one * 0.12f, Mat.Emissive(new Color(1f, 0.8f, 0.4f), 4f), false);
            flash.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = Shapes.PointLight(flash.transform, Vector3.zero, new Color(1f, 0.75f, 0.4f), 6f, 3f);
            l.transform.SetParent(parent, true);
            Object.Destroy(flash, 0.05f);
            Object.Destroy(l.gameObject, 0.05f);
        }
    }
}
