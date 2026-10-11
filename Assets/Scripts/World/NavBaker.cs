using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Neanderthal
{
    /// <summary>
    /// Bakes a runtime NavMesh over the level's colliders (terrain, caves, catacomb mazes) in the background,
    /// so bots can find their way through tunnels and labyrinths. Until it's ready they steer directly.
    /// </summary>
    public static class NavBaker
    {
        static NavMeshDataInstance instance;
        static NavMeshData data;
        static AsyncOperation op;

        public static bool Ready => data != null && (op == null || op.isDone);

        public static void Bake(Bounds bounds)
        {
            Clear();
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            // Creatures, the player, pickups and triggers are not floor.
            sources.RemoveAll(s =>
            {
                var c = s.component;
                if (c == null) return false;
                var col = c as Collider;
                if (col != null && col.isTrigger) return true;
                return c.GetComponentInParent<Health>() != null || c.GetComponentInParent<Pickup>() != null || c is CharacterController;
            });
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.75f;
            settings.agentSlope = 46f;
            settings.overrideVoxelSize = true;      // coarser voxels: big maps bake in seconds
            settings.voxelSize = 0.2f;
            data = new NavMeshData(settings.agentTypeID);
            instance = NavMesh.AddNavMeshData(data);
            op = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, bounds);
        }

        public static void Clear()
        {
            if (instance.valid) instance.Remove();
            instance = default;
            data = null;
            op = null;
        }

        /// <summary>Next point to walk towards on the way to <paramref name="dest"/>; false if no path is known.</summary>
        public static bool Path(Vector3 from, Vector3 dest, NavMeshPath path)
        {
            if (!Ready) return false;
            if (!NavMesh.SamplePosition(from, out var a, 2.5f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(dest, out var b, 4f, NavMesh.AllAreas)) return false;
            return NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status != NavMeshPathStatus.PathInvalid && path.corners.Length >= 2;
        }
    }
}
