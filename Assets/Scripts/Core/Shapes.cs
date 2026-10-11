using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Helpers for blocking out geometry from primitives. Everything in the prototype is built
    /// from these so the project runs without any imported art. Swap for real models later.
    /// </summary>
    public static class Shapes
    {
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale,
            Material mat, Quaternion? localRot = null, bool collider = true, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null, bool collider = true)
        {
            var go = Prim(PrimitiveType.Cube, parent, pos, scale, mat, rot, collider);
            // Photo-textured boxes get UVs in metres so a 17 m wall tiles instead of stretching one image.
            if (mat != null && mat.mainTexture != null && scale.sqrMagnitude > 1.5f)
                go.GetComponent<MeshFilter>().sharedMesh = MetricBox(scale, 2.5f);
            return go;
        }

        static readonly Dictionary<Vector3Int, Mesh> metricBoxes = new Dictionary<Vector3Int, Mesh>();

        /// <summary>Unit cube whose UVs repeat every <paramref name="tile"/> metres once scaled by <paramref name="size"/>.</summary>
        public static Mesh MetricBox(Vector3 size, float tile)
        {
            var key = new Vector3Int(Mathf.RoundToInt(size.x * 10f), Mathf.RoundToInt(size.y * 10f), Mathf.RoundToInt(size.z * 10f));
            if (metricBoxes.TryGetValue(key, out var cached) && cached != null) return cached;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            void Face(Vector3 n, Vector3 u, Vector3 v, float su, float sv)
            {
                int s = verts.Count;
                Vector3 c = n * 0.5f;
                verts.Add(c - u * 0.5f - v * 0.5f); verts.Add(c + u * 0.5f - v * 0.5f);
                verts.Add(c + u * 0.5f + v * 0.5f); verts.Add(c - u * 0.5f + v * 0.5f);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(su, 0)); uvs.Add(new Vector2(su, sv)); uvs.Add(new Vector2(0, sv));
                for (int i = 0; i < 4; i++) normals.Add(n);
                tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
            float X = size.x / tile, Y = size.y / tile, Z = size.z / tile;
            Face(Vector3.forward, Vector3.left, Vector3.up, X, Y);
            Face(Vector3.back, Vector3.right, Vector3.up, X, Y);
            Face(Vector3.right, Vector3.forward, Vector3.up, Z, Y);
            Face(Vector3.left, Vector3.back, Vector3.up, Z, Y);
            Face(Vector3.up, Vector3.right, Vector3.forward, X, Z);
            Face(Vector3.down, Vector3.right, Vector3.back, X, Z);
            var mesh = new Mesh { name = "MetricBox" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            metricBoxes[key] = mesh;
            return mesh;
        }

        public static GameObject Ball(Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = true)
            => Prim(PrimitiveType.Sphere, parent, pos, scale, mat, null, collider);

        public static GameObject Cyl(Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null, bool collider = true)
            => Prim(PrimitiveType.Cylinder, parent, pos, scale, mat, rot, collider);

        public static GameObject Caps(Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null, bool collider = true)
            => Prim(PrimitiveType.Capsule, parent, pos, scale, mat, rot, collider);

        public static Transform Pivot(Transform parent, Vector3 localPos, string name = "Pivot")
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        /// <summary>A cylinder stretched between two local points (great for limbs, sticks, shafts).</summary>
        public static GameObject Stick(Transform parent, Vector3 a, Vector3 b, float thickness, Material mat, bool collider = false)
        {
            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = b - a;
            float len = dir.magnitude;
            var rot = len > 0.0001f ? Quaternion.FromToRotation(Vector3.up, dir / len) : Quaternion.identity;
            return Cyl(parent, mid, new Vector3(thickness, len * 0.5f, thickness), mat, rot, collider);
        }

        /// <summary>A four-sided pointed cone mesh, used for spear tips, roofs and arrowheads.</summary>
        public static GameObject Cone(Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null, int sides = 6)
        {
            var go = new GameObject("Cone");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh(sides);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static readonly Dictionary<int, Mesh> coneMeshes = new Dictionary<int, Mesh>();

        /// <summary>Unit cone: base radius 0.5 at y=0, apex at y=1.</summary>
        public static Mesh ConeMesh(int sides)
        {
            if (coneMeshes.TryGetValue(sides, out var cached) && cached != null) return cached;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                int s = verts.Count;
                verts.Add(p0); verts.Add(Vector3.up); verts.Add(p1);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                s = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(Vector3.zero);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            }
            var mesh = new Mesh { name = "Cone" + sides };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            coneMeshes[sides] = mesh;
            return mesh;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        public static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        }

        public static void SetShadows(GameObject go, bool cast)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static Light PointLight(Transform parent, Vector3 pos, Color color, float range, float intensity, bool shadows = false)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }
    }

    public static class PhysicsUtil
    {
        static readonly RaycastHit[] hits = new RaycastHit[32];

        /// <summary>Raycast that skips trigger colliders and anything under <paramref name="ignoreRoot"/>.</summary>
        public static bool Raycast(Vector3 origin, Vector3 dir, float dist, Transform ignoreRoot, out RaycastHit best)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, hits, dist, ~0, QueryTriggerInteraction.Ignore);
            return Closest(n, ignoreRoot, out best);
        }

        /// <summary>Raycast against the world only: skips triggers, creatures and players.</summary>
        public static bool RaycastStatic(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, hits, dist, ~0, QueryTriggerInteraction.Ignore);
            best = default;
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.GetComponentInParent<Health>() != null || h.collider is CharacterController) continue;
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }

        public static bool SphereCast(Vector3 origin, float radius, Vector3 dir, float dist, Transform ignoreRoot, out RaycastHit best)
        {
            int n = Physics.SphereCastNonAlloc(origin, radius, dir, hits, dist, ~0, QueryTriggerInteraction.Ignore);
            return Closest(n, ignoreRoot, out best);
        }

        static bool Closest(int n, Transform ignoreRoot, out RaycastHit best)
        {
            best = default;
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (ignoreRoot != null && h.transform.IsChildOf(ignoreRoot)) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue; // started inside collider
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }
    }
}
