using UnityEngine;

namespace Neanderthal
{
    /// <summary>Cheap code-only effects: debris bursts, tracers, muzzle flashes.</summary>
    public static class Effects
    {
        public static void Blood(Vector3 pos, Vector3 normal) => Burst(pos, normal, new Color(0.45f, 0.03f, 0.02f), 7, 2.5f, 0.05f);
        public static void Dust(Vector3 pos, Vector3 normal) => Burst(pos, normal, new Color(0.45f, 0.4f, 0.33f), 5, 1.8f, 0.04f);
        public static void Chips(Vector3 pos, Vector3 normal, Color c) => Burst(pos, normal, c, 6, 3f, 0.06f);

        public static void Burst(Vector3 pos, Vector3 normal, Color color, int count, float speed, float size)
        {
            var mat = Mat.Get(color);
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = mat;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * size * Random.Range(0.6f, 1.3f);
                go.transform.rotation = Random.rotation;
                var d = go.AddComponent<Debris>();
                d.Velocity = (normal + Random.insideUnitSphere * 0.9f).normalized * speed * Random.Range(0.5f, 1.2f);
            }
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
