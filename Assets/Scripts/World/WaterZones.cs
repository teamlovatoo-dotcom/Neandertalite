using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>Registry of water surfaces (rectangles in XZ at a given height) for swimming and wading.</summary>
    public static class WaterZones
    {
        struct Zone { public Rect Area; public float Surface, Bottom; }
        static readonly List<Zone> zones = new List<Zone>();

        /// <param name="bottomY">Below this there is no water (tunnels and caves under a river stay dry).</param>
        public static void Register(Rect area, float surfaceY, float bottomY = float.NaN)
            => zones.Add(new Zone { Area = area, Surface = surfaceY, Bottom = float.IsNaN(bottomY) ? surfaceY - 8f : bottomY });
        public static void Clear() => zones.Clear();

        /// <summary>True when <paramref name="p"/> is inside a zone and below its surface.</summary>
        public static bool Surface(Vector3 p, out float surfaceY)
        {
            foreach (var z in zones)
            {
                if (z.Area.Contains(new Vector2(p.x, p.z)) && p.y < z.Surface && p.y > z.Bottom)
                {
                    surfaceY = z.Surface;
                    return true;
                }
            }
            surfaceY = 0f;
            return false;
        }
    }
}
