using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// "Підземелля": a big PvP valley (300 m) with a northern plateau, a river and a waterfall you can hide behind,
    /// and under it all a torch-lit labyrinth of catacombs with ossuary halls. Four tunnels lead down:
    /// behind the waterfall, and portals in the west, east and south.
    /// The underground is built on a 6 m cell grid (maze cells + ramped tunnel cells); terrain holes open the tunnel mouths.
    /// </summary>
    public class CatacombsLevel : MonoBehaviour
    {
        const float S = 6f;          // cell size
        const float WallT = 0.6f;    // wall thickness
        const float CeilH = 4.5f;    // clear height
        const float SlabT = 0.6f;    // floor / ceiling slab thickness
        const float MazeY = -14f;
        const int MinI = -9, MaxI = 9, MinJ = -11, MaxJ = 4;
        const float Half = 150f;

        static readonly int[] DI = { 0, 1, 0, -1 };   // N E S W
        static readonly int[] DJ = { 1, 0, -1, 0 };

        enum CellKind { Maze, Hall, Tunnel }

        class Cell
        {
            public int I, J;
            public float YA, YB;          // floor height at the negative / positive side along the slope axis
            public bool AlongX;           // slope axis (only matters when YA != YB)
            public readonly bool[] Open = new bool[4];
            public CellKind Kind;
            public int PortalSide = -1;   // side opening to the surface
            public bool Hole;             // terrain must be cut above this cell
            public Vector3 Center => new Vector3(I * S, (YA + YB) * 0.5f, J * S);
            public float MinY => Mathf.Min(YA, YB);
            public float MaxY => Mathf.Max(YA, YB);
            public int OpenCount { get { int n = 0; foreach (var o in Open) if (o) n++; return n; } }
        }

        struct Portal { public Vector3 Mouth; public Vector3 Out; public float Y; }

        static readonly Dictionary<(int, int), Cell> cells = new Dictionary<(int, int), Cell>();
        static readonly List<Portal> portals = new List<Portal>();

        // ------------------------------------------------------------------ heights
        static float Smooth(float e0, float e1, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(e0, e1, v));
        public static float RiverX(float z) => -18f + 8f * Mathf.Sin((z - 63f) / 20f);
        public static float StreamX(float z) => -18f + 16f * Mathf.Sin((50f - z) / 30f);
        static readonly Vector2 Pool = new Vector2(-18f, 50f);
        static readonly Vector2 CampE = new Vector2(70f, -30f), CampW = new Vector2(-75f, -55f);

        static float BaseHeight(float x, float z)
        {
            float h = Mat.Fbm(x * 0.012f + 3f, z * 0.012f + 7f, 4) * 7f - 1f;
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            h += Smooth(112f, 145f, edge) * 48f * (0.7f + 0.6f * Mat.Fbm(x * 0.03f, z * 0.03f, 3));

            // Northern plateau: cliff along z~56-62, pushed back behind the waterfall block, ramp up in the east.
            float wf = Smooth(16f, 10f, Mathf.Abs(x + 18f));
            float c0 = Mathf.Lerp(54f, 66f, wf), c1 = Mathf.Lerp(62f, 70f, wf);
            float ramp = Smooth(20f, 9f, Mathf.Abs(x - 72f));
            c0 = Mathf.Lerp(c0, 18f, ramp);
            float plateau = Smooth(c0, c1, z);
            float top = 14f + Mat.Fbm(x * 0.05f, z * 0.05f + 2f, 2) * 1.5f;
            h = Mathf.Lerp(h, Mathf.Max(h, top), plateau);
            // River on the plateau flowing to the waterfall lip.
            if (z > 62f) h = Mathf.Lerp(h, 10.8f, Smooth(6.5f, 3f, Mathf.Abs(x - RiverX(z))) * plateau);

            // Plunge pool and the ledge behind the falling water.
            float pd = Vector2.Distance(new Vector2(x, z), Pool);
            h = Mathf.Lerp(h, 0.6f, Smooth(16f, 12f, pd) * (1f - plateau));
            h = Mathf.Lerp(h, -3.2f, Smooth(10f, 6f, pd));
            float ledge = Smooth(1.5f, 0f, Mathf.Max(Mathf.Abs(x + 18f) - 8f, 0f)) * Smooth(57f, 59.5f, z) * Smooth(64f, 63f, z);
            h = Mathf.Lerp(h, 0.6f, ledge);

            // Stream from the pool to the south.
            if (z < 46f) h = Mathf.Lerp(h, -1.8f, Smooth(5f, 2.5f, Mathf.Abs(x - StreamX(z))) * Smooth(46f, 40f, z));
            // Camp clearings
            h = Mathf.Lerp(h, BaseFlat(CampE), Smooth(16f, 10f, Vector2.Distance(new Vector2(x, z), CampE)));
            h = Mathf.Lerp(h, BaseFlat(CampW), Smooth(16f, 10f, Vector2.Distance(new Vector2(x, z), CampW)));
            return h;
        }

        static float BaseFlat(Vector2 p) => Mat.Fbm(p.x * 0.012f + 3f, p.y * 0.012f + 7f, 4) * 7f - 1f;

        /// <summary>Final ground height: base terrain flattened in front of every tunnel mouth.</summary>
        public static float Height(float x, float z)
        {
            float h = BaseHeight(x, z);
            foreach (var p in portals)
            {
                Vector2 rel = new Vector2(x - p.Mouth.x, z - p.Mouth.z);
                Vector2 o = new Vector2(p.Out.x, p.Out.z);
                float along = Vector2.Dot(rel, o), side = Mathf.Abs(rel.x * o.y - rel.y * o.x);
                if (along < -1f || along > 14f || side > 7f) continue;
                float k = Smooth(14f, 8f, along) * Smooth(7f, 4f, side);
                h = Mathf.Lerp(h, p.Y - 0.05f, k);
            }
            return h;
        }

        static bool IsHole(float x, float z)
        {
            int i = Mathf.RoundToInt(x / S), j = Mathf.RoundToInt(z / S);
            if (!cells.TryGetValue((i, j), out var c) || !c.Hole) return false;
            return Mathf.Abs(x - i * S) < S * 0.5f + 0.2f && Mathf.Abs(z - j * S) < S * 0.5f + 0.2f;
        }

        // ------------------------------------------------------------------ grid
        static Cell Add(int i, int j, float y, CellKind kind)
        {
            var c = new Cell { I = i, J = j, YA = y, YB = y, Kind = kind };
            cells[(i, j)] = c;
            return c;
        }

        static void Link(Cell a, int dir)
        {
            if (!cells.TryGetValue((a.I + DI[dir], a.J + DJ[dir]), out var b)) return;
            a.Open[dir] = true;
            b.Open[(dir + 2) % 4] = true;
        }

        /// <summary>Set a cell's floor so it rises from <paramref name="yFrom"/> on side 'dirFrom' to yTo on the opposite side.</summary>
        static void Slope(Cell c, int dirFrom, float yFrom, float yTo)
        {
            c.AlongX = dirFrom == 1 || dirFrom == 3;
            bool fromNegative = dirFrom == 2 || dirFrom == 3;   // S or W = negative side
            c.YA = fromNegative ? yFrom : yTo;
            c.YB = fromNegative ? yTo : yFrom;
        }

        static void GenerateMaze(System.Random rnd)
        {
            for (int i = MinI; i <= MaxI; i++)
            for (int j = MinJ; j <= MaxJ; j++)
                Add(i, j, MazeY, CellKind.Maze);

            // Recursive backtracker -> a perfect maze.
            var stack = new Stack<Cell>();
            var seen = new HashSet<Cell>();
            var start = cells[(0, 0)];
            stack.Push(start);
            seen.Add(start);
            var dirs = new List<int>(4);
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                dirs.Clear();
                for (int d = 0; d < 4; d++)
                    if (cells.TryGetValue((c.I + DI[d], c.J + DJ[d]), out var n) && n.Kind != CellKind.Tunnel && !seen.Contains(n)) dirs.Add(d);
                if (dirs.Count == 0) { stack.Pop(); continue; }
                int dir = dirs[rnd.Next(dirs.Count)];
                var next = cells[(c.I + DI[dir], c.J + DJ[dir])];
                Link(c, dir);
                seen.Add(next);
                stack.Push(next);
            }
            // Extra openings so fights have loops and flanks.
            foreach (var c in new List<Cell>(cells.Values))
                for (int d = 0; d < 2; d++)
                    if (!c.Open[d] && cells.ContainsKey((c.I + DI[d], c.J + DJ[d])) && rnd.NextDouble() < 0.16) Link(c, d);

            // Ossuary halls: open rectangles.
            Hall(-1, 1, -5, -3);
            Hall(-8, -6, 1, 3);
            Hall(5, 7, -10, -8);
            Hall(5, 7, 1, 2);
        }

        static void Hall(int i0, int i1, int j0, int j1)
        {
            for (int i = i0; i <= i1; i++)
            for (int j = j0; j <= j1; j++)
            {
                var c = cells[(i, j)];
                c.Kind = CellKind.Hall;
                if (i < i1) Link(c, 1);
                if (j < j1) Link(c, 0);
            }
        }

        /// <summary>
        /// Tunnel from a maze edge cell outward in a straight line: a few level cells, then ramps (at most
        /// <paramref name="maxRise"/> per cell) until the floor meets the ground, where the portal opens.
        /// </summary>
        static void TunnelOut(int i, int j, int dir, int level, float maxRise)
        {
            var prev = cells[(i, j)];
            float y = MazeY;
            for (int step = 1; step < 20; step++)
            {
                int ci = i + DI[dir] * step, cj = j + DJ[dir] * step;
                float surface = BaseHeight((ci + DI[dir]) * S, (cj + DJ[dir]) * S);
                var c = Add(ci, cj, y, CellKind.Tunnel);
                Link(prev, dir);
                prev = c;
                if (step <= level) continue;
                float rise = Mathf.Clamp(surface - y, 0f, maxRise);
                Slope(c, (dir + 2) % 4, y, y + rise);
                y += rise;
                if (surface - y < 0.3f)
                {
                    var portal = Add(ci + DI[dir], cj + DJ[dir], y, CellKind.Tunnel);
                    Link(prev, dir);
                    portal.PortalSide = dir;
                    AddPortal(portal, dir);
                    return;
                }
            }
        }

        static void AddPortal(Cell c, int dir)
        {
            var o = new Vector3(DI[dir], 0f, DJ[dir]);
            portals.Add(new Portal { Mouth = new Vector3(c.I * S, c.YA, c.J * S) + o * S * 0.5f, Out = o, Y = c.YA });
        }

        /// <summary>The secret way down behind the waterfall: north under the plateau, a switchback, then south into the maze.</summary>
        static void WaterfallTunnel()
        {
            var top = cells[(-1, MaxJ)];
            var prev = top;
            for (int j = MaxJ + 1; j <= 8; j++) { var c = Add(-1, j, MazeY, CellKind.Tunnel); Link(prev, 0); prev = c; }
            float y = MazeY;
            for (int j = 9; j <= 13; j++)
            {
                var c = Add(-1, j, y, CellKind.Tunnel);
                Slope(c, 2, y, y + 2f);
                y += 2f;
                Link(prev, 0);
                prev = c;
            }
            var t1 = Add(-1, 14, y, CellKind.Tunnel); Link(prev, 0);
            var t2 = Add(-2, 14, y, CellKind.Tunnel); Link(t1, 3);
            var t3 = Add(-3, 14, y, CellKind.Tunnel); Link(t2, 3);
            var a = Add(-3, 13, y, CellKind.Tunnel); Slope(a, 0, y, y + 2f); Link(t3, 2); y += 2f;
            var b = Add(-3, 12, y, CellKind.Tunnel); Slope(b, 0, y, 0.6f); Link(a, 2); y = 0.6f;
            var portal = Add(-3, 11, y, CellKind.Tunnel); Link(b, 2);
            portal.PortalSide = 2;
            // No terrain flattening needed: the ledge is part of the base terrain.
        }

        static void MarkHoles()
        {
            foreach (var c in cells.Values)
            {
                if (c.Kind != CellKind.Tunnel) continue;
                float roof = c.MaxY + CeilH + SlabT + 0.4f;
                float minGround = float.MaxValue;
                for (int a = -1; a <= 1; a++)
                for (int b = -1; b <= 1; b++)
                    minGround = Mathf.Min(minGround, Height(c.I * S + a * S * 0.55f, c.J * S + b * S * 0.55f));
                c.Hole = roof > minGround;
            }
        }

        // ------------------------------------------------------------------ geometry
        static Material wallMat, floorMat, ceilMat, stoneMat;

        static void Mats()
        {
            wallMat = TextureLibrary.Has("stone_wall") ? Mat.Pbr("stone_wall", Mat.Stone, 0.12f, null, new Color(0.8f, 0.76f, 0.7f)) : Scenery.CaveMat;
            floorMat = TextureLibrary.Has("camp_dirt") ? Mat.Pbr("camp_dirt", Mat.Sand, 0.08f, null, new Color(0.55f, 0.5f, 0.45f)) : Scenery.CaveMat;
            ceilMat = Scenery.CaveMat;
            stoneMat = Scenery.RockMat;
        }

        static void Slab(Transform t, Cell c, float yOffset, Material mat)
        {
            Vector3 dir = c.AlongX ? Vector3.right : Vector3.forward;
            float rise = c.YB - c.YA;
            float len = Mathf.Sqrt(S * S + rise * rise);
            var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(-Mathf.Atan2(rise, S) * Mathf.Rad2Deg, 0f, 0f);
            Vector3 centre = new Vector3(c.I * S, (c.YA + c.YB) * 0.5f, c.J * S) + rot * Vector3.up * yOffset;
            var go = Shapes.Box(t, centre, new Vector3(S + 0.02f, SlabT, len + 0.02f), mat, rot);
            go.name = yOffset < 0f ? "Floor" : "Ceiling";
        }

        static void BuildCell(Transform t, Cell c, System.Random rnd, HashSet<(int, int)> posts)
        {
            Slab(t, c, -SlabT * 0.5f, floorMat);
            Slab(t, c, CeilH + SlabT * 0.5f, ceilMat);
            float lo = c.MinY - SlabT, hi = c.MaxY + CeilH + SlabT;
            for (int d = 0; d < 4; d++)
            {
                if (c.Open[d] || c.PortalSide == d) continue;
                Vector3 edge = new Vector3(c.I * S + DI[d] * (S * 0.5f - WallT * 0.5f), (lo + hi) * 0.5f, c.J * S + DJ[d] * (S * 0.5f - WallT * 0.5f));
                Vector3 size = DI[d] != 0 ? new Vector3(WallT, hi - lo, S) : new Vector3(S, hi - lo, WallT);
                Shapes.Box(t, edge, size, wallMat).name = "Wall";
            }
            // Corner posts (shared corners built once) keep wall joints closed and read as masonry pilasters.
            for (int a = 0; a <= 1; a++)
            for (int b = 0; b <= 1; b++)
            {
                var key = (c.I * 2 + (a == 0 ? -1 : 1), c.J * 2 + (b == 0 ? -1 : 1));
                if (!posts.Add(key)) continue;
                Vector3 p = new Vector3(c.I * S + (a == 0 ? -1 : 1) * S * 0.5f, (lo + hi) * 0.5f, c.J * S + (b == 0 ? -1 : 1) * S * 0.5f);
                Shapes.Box(t, p, new Vector3(WallT * 1.6f, hi - lo + 0.02f, WallT * 1.6f), wallMat).name = "Post";
            }
            if (c.PortalSide >= 0) PortalFrame(t, c);
        }

        static void PortalFrame(Transform t, Cell c)
        {
            int d = c.PortalSide;
            Vector3 o = new Vector3(DI[d], 0f, DJ[d]);
            Vector3 side = new Vector3(-o.z, 0f, o.x);
            Vector3 mouth = new Vector3(c.I * S, c.YA, c.J * S) + o * (S * 0.5f + 0.4f);
            for (int s = -1; s <= 1; s += 2)
                Shapes.Box(t, mouth + side * s * 2.75f + Vector3.up * 2.4f, new Vector3(1.1f, 5.6f, 1.1f), stoneMat, Quaternion.LookRotation(o)).name = "PortalStone";
            Shapes.Box(t, mouth + Vector3.up * 5.3f, new Vector3(7.2f, 1.2f, 1.4f), stoneMat, Quaternion.LookRotation(o)).name = "Lintel";
            for (int s = -1; s <= 1; s += 2)
                Scenery.TorchPost(t, mouth + o * 1.6f + side * s * 3.6f + Vector3.up * -0.05f);
        }

        static void WallTorch(Transform t, Cell c, int side)
        {
            Vector3 n = new Vector3(DI[side], 0f, DJ[side]);
            Vector3 p = new Vector3(c.I * S, c.MaxY + 2.3f, c.J * S) + n * (S * 0.5f - WallT - 0.25f);
            var root = Shapes.Pivot(t, Vector3.zero, "WallTorch");
            root.position = p;
            root.rotation = Quaternion.LookRotation(-n);
            Shapes.Stick(root, new Vector3(0f, -0.35f, -0.25f), new Vector3(0f, 0.05f, 0.05f), 0.05f, ModelKit.For("wood_rough"));
            Shapes.Stick(root, new Vector3(0f, -0.02f, -0.02f), new Vector3(0f, 0.16f, 0.12f), 0.09f, ModelKit.For("char"));
            FireFx.Fire(root, new Vector3(0f, 0.18f, 0.12f), 0.3f, false, false, 10f, 2f, false);
        }

        static void Decorate(Transform t, Cell c, System.Random rnd)
        {
            Vector3 floor = new Vector3(c.I * S, c.MinY, c.J * S);
            if (c.Kind == CellKind.Tunnel)
            {
                if ((c.I + c.J) % 2 == 0)
                    for (int d = 0; d < 4; d++) if (!c.Open[d] && c.PortalSide != d) { WallTorch(t, c, d); break; }
                return;
            }
            bool deadEnd = c.OpenCount == 1;
            if (c.Kind == CellKind.Maze && (rnd.NextDouble() < 0.22 || deadEnd))
            {
                var closed = new List<int>();
                for (int d = 0; d < 4; d++) if (!c.Open[d]) closed.Add(d);
                if (closed.Count > 0) WallTorch(t, c, closed[rnd.Next(closed.Count)]);
            }
            if (deadEnd && Camp.Has(Camp.Kind.BonePile))
            {
                int open = System.Array.IndexOf(c.Open, true);
                Camp.Place(Camp.Kind.BonePile, t, floor + new Vector3(-DI[open], 0f, -DJ[open]) * 1.4f, rnd.Next(360), 0.75f, false);
            }
            else if (c.Kind == CellKind.Maze && rnd.NextDouble() < 0.12)
                PropLibrary.Spawn(PropLibrary.Kind.MossRock, t, floor + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * 3f, rnd.Next(360), 0.7f, rnd.Next(7), true);
            if (rnd.NextDouble() < 0.14 && PropLibrary.Has(PropLibrary.Kind.Roots))
            {
                // Roots hanging through the ceiling.
                var r = PropLibrary.Spawn(PropLibrary.Kind.Roots, t, floor + Vector3.up * (c.MaxY - c.MinY + CeilH + 0.05f), rnd.Next(360), 0.55f);
                if (r != null) r.transform.rotation = Quaternion.Euler(180f, 0f, 0f) * r.transform.rotation;
            }
        }

        static void DecorateHalls(Transform t, System.Random rnd)
        {
            // Big hall: four pillars, an altar, bone heaps and torches all round.
            foreach (var hall in new[] { new RectInt(-1, -5, 3, 3), new RectInt(-8, 1, 3, 3), new RectInt(5, -10, 3, 3), new RectInt(5, 1, 3, 2) })
            {
                Vector3 centre = new Vector3((hall.x + (hall.width - 1) * 0.5f) * S, MazeY, (hall.y + (hall.height - 1) * 0.5f) * S);
                for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                {
                    if (hall.height < 3 && b > 0) continue;
                    Vector3 p = centre + new Vector3(a * S * 0.5f, CeilH * 0.5f, b * (hall.height < 3 ? 0f : S * 0.5f));
                    Shapes.Box(t, p, new Vector3(1.3f, CeilH, 1.3f), wallMat).name = "Pillar";
                }
                Shapes.Box(t, centre + new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 1.4f), stoneMat).name = "Altar";
                if (Camp.Has(Camp.Kind.BonePile))
                    for (int k = 0; k < 3; k++)
                        Camp.Place(Camp.Kind.BonePile, t, centre + Quaternion.Euler(0, k * 120 + 30, 0) * Vector3.forward * (S * 1.15f), rnd.Next(360), 0.9f, false);
                Camp.Fire(t, centre + new Vector3(S, 0f, 0f));
            }
        }

        // ------------------------------------------------------------------ build
        public static ArenaLayout Build(Transform root)
        {
            cells.Clear();
            portals.Clear();
            var rnd = new System.Random(2024);
            var L = new ArenaLayout { Name = "Підземелля", Center = Vector3.zero };

            GenerateMaze(rnd);
            WaterfallTunnel();
            TunnelOut(MinI, -3, 3, 1, 2.4f);     // west
            TunnelOut(MaxI, -3, 1, 1, 2.4f);     // east
            TunnelOut(MaxI, -9, 1, 1, 2.4f);     // south-east
            TunnelOut(4, MaxJ, 0, 4, 2.8f);      // up under the cliff onto the plateau
            MarkHoles();

            Scenery.Lighting(root, new Color(1f, 0.9f, 0.76f), 1.15f, new Vector3(40f, -40f, 0f),
                new Color(0.5f, 0.58f, 0.66f), new Color(0.4f, 0.41f, 0.36f), new Color(0.18f, 0.16f, 0.12f),
                new Color(0.58f, 0.63f, 0.66f), 0.004f, new Color(0.97f, 1f, 0.95f), "sky_forest");

            var layers = new[] { TextureLibrary.Layer("grass", 5f), TextureLibrary.Layer("forest_ground", 6f), TextureLibrary.Layer("river_sand", 4f),
                                 TextureLibrary.Layer("rock_face", 9f), TextureLibrary.Layer("leaf_litter", 6f) };
            var terrain = TerrainBuilder.BuildTextured(root, Half * 2f, 769, Height, layers, (x, z, h, sl, w) =>
            {
                w[0] = 1f;
                w[1] = Mathf.Clamp01((Mat.Fbm(x * 0.04f + 3f, z * 0.04f, 3) - 0.45f) * 3f);
                float water = Mathf.Max(Smooth(14f, 9f, Vector2.Distance(new Vector2(x, z), Pool)),
                    z < 46f ? Smooth(6.5f, 3.5f, Mathf.Abs(x - StreamX(z))) : 0f);
                if (z > 62f) water = Mathf.Max(water, Smooth(8f, 4f, Mathf.Abs(x - RiverX(z))) * Smooth(60f, 66f, z));
                w[2] = water * 2.5f;
                foreach (var p in portals)
                    w[2] = Mathf.Max(w[2], Smooth(10f, 4f, Vector2.Distance(new Vector2(x, z), new Vector2(p.Mouth.x, p.Mouth.z))) * 2f);
                w[3] = Mathf.Clamp01((sl - 0.42f) * 4f) * 3f;
                w[4] = Mathf.Clamp01((Mat.Fbm(x * 0.07f + 11f, z * 0.07f, 2) - 0.5f) * 3f) * (1f - water);
            }, 768, IsHole);
            if (terrain == null) TerrainBuilder.Build(root, Half * 2f, 200, Height, (x, z, h, s) => TerrainBuilder.NaturalGround(x, z, h, s, 0f, 0f), 2048);
            ArenaLevels.Boundary(root, 132f);

            // Underground
            Mats();
            var under = Shapes.Pivot(root, Vector3.zero, "Catacombs");
            var posts = new HashSet<(int, int)>();
            foreach (var c in cells.Values) BuildCell(under, c, rnd, posts);
            foreach (var c in cells.Values) Decorate(under, c, rnd);
            DecorateHalls(under, rnd);

            BuildWaterfall(root);
            BuildWater(root);
            BuildSurface(root, rnd);

            // Spawns: spread over the valley, the plateau and the halls below.
            void Sp(Vector3 p, float yaw) => L.Spawns.Add(new SpawnPoint(p + Vector3.up * 0.2f, yaw));
            Vector3 G(float x, float z) => new Vector3(x, Height(x, z), z);
            Sp(G(-95f, -95f), 45f);
            Sp(G(95f, -95f), 315f);
            Sp(G(95f, 30f), 250f);
            Sp(G(-100f, 25f), 110f);
            Sp(G(45f, 100f), 200f);
            Sp(G(-70f, 105f), 150f);
            Sp(new Vector3(0f, MazeY + 0.2f, -5 * S), 0f);
            Sp(new Vector3(-7 * S, MazeY + 0.2f, 1 * S), 0f);
            Sp(new Vector3(6 * S, MazeY + 0.2f, -10 * S), 0f);

            // Weapons and supplies: basics on the surface, obsidian deep in the catacomb halls.
            Pickup.Weapon(root, G(-30f, -20f) + Vector3.up * 0.1f, "bow", 20f);
            Pickup.Weapon(root, G(40f, -60f) + Vector3.up * 0.1f, "spear", 20f);
            Pickup.Weapon(root, G(60f, 20f) + Vector3.up * 0.1f, "slingshot", 20f);
            Pickup.Weapon(root, G(-60f, 60f) + Vector3.up * 0.1f, "axe", 20f);
            Pickup.Weapon(root, G(20f, 95f) + Vector3.up * 0.1f, "bow", 20f);
            Pickup.Weapon(root, G(-90f, -20f) + Vector3.up * 0.1f, "club", 20f);
            Pickup.Weapon(root, new Vector3(-18f, 0.7f, 61f), "obsidian_knife", 25f);   // behind the waterfall
            Pickup.Weapon(root, new Vector3(0f, MazeY + 1.1f, -4 * S), "obsidian_spear", 25f);
            Pickup.Weapon(root, new Vector3(-7 * S, MazeY + 1.1f, 2 * S), "obsidian_axe", 25f);
            Pickup.Weapon(root, new Vector3(6 * S, MazeY + 1.1f, -9 * S), "obsidian_knife", 25f);
            Pickup.Weapon(root, new Vector3(6 * S, MazeY + 1.1f, 1.5f * S), "bow", 25f);
            foreach (var p in new[] { G(-20f, -40f), G(30f, 10f), G(-50f, 90f), G(80f, -80f), G(-80f, -70f) })
                Pickup.AmmoBox(root, p + Vector3.up * 0.1f, AmmoType.Arrow, 8, 20f);
            foreach (var p in new[] { G(10f, -30f), G(-40f, 20f), G(70f, 70f), G(-100f, -100f) })
                Pickup.AmmoBox(root, p + Vector3.up * 0.1f, AmmoType.Stone, 10, 20f);
            var rnd2 = new System.Random(5);
            var mazeList = new List<Cell>();
            foreach (var c in cells.Values) if (c.Kind == CellKind.Maze) mazeList.Add(c);
            for (int k = 0; k < 10; k++)
            {
                var c = mazeList[rnd2.Next(mazeList.Count)];
                Vector3 p = new Vector3(c.I * S, MazeY + 0.15f, c.J * S);
                if (k % 3 == 0) Pickup.Create(root, p, PickupKind.Health, amount: 40, respawn: 25f);
                else Pickup.AmmoBox(root, p, k % 3 == 1 ? AmmoType.Arrow : AmmoType.Stone, 6, 20f);
            }
            Pickup.Create(root, G(-18f, 58f) + new Vector3(0f, 0.1f, 2f), PickupKind.Armor, amount: 50, respawn: 40f);

            root.gameObject.AddComponent<CatacombsLevel>();
            return L;
        }

        // ------------------------------------------------------------------ waterfall
        static void BuildWaterfall(Transform root)
        {
            var rock = Scenery.CaveMat;
            var t = Shapes.Pivot(root, Vector3.zero, "WaterfallCliff");
            // Cliff block the tunnel mouth is cut into (covers the terrain hole).
            Shapes.Box(t, new Vector3(-27.5f, 7f, 70f), new Vector3(13f, 16f, 14f), rock).name = "Cliff";
            Shapes.Box(t, new Vector3(-8.5f, 7f, 70f), new Vector3(13f, 16f, 14f), rock).name = "Cliff";
            Shapes.Box(t, new Vector3(-18f, 8.85f, 70f), new Vector3(6.2f, 6.3f, 14f), rock).name = "Cliff";
            Shapes.Box(t, new Vector3(-18f, 11.9f, 62.6f), new Vector3(10.5f, 0.5f, 1.4f), rock).name = "Lip";
            if (PropLibrary.Has(PropLibrary.Kind.CliffA))
            {
                PropLibrary.Spawn(PropLibrary.Kind.CliffB, t, new Vector3(-37f, -1.5f, 64f), 170f, 2.3f, 0, true);
                PropLibrary.Spawn(PropLibrary.Kind.CliffA, t, new Vector3(1f, -1.5f, 64f), 200f, 2.3f, 0, true);
            }

            // Falling water: two scrolling streak layers, slanted so there's a dry ledge behind it.
            var tex = TextureLibrary.Albedo("waterfall");
            var sheet = Shapes.Pivot(t, new Vector3(-18f, 5.8f, 61.1f), "Waterfall");
            sheet.rotation = Quaternion.Euler(-14f, 180f, 0f);
            for (int layer = 0; layer < 2; layer++)
            {
                var m = Mat.WaterSurface(new Color(0.85f, 0.92f, 0.96f, 0.92f));
                m = new Material(m) { name = "WaterfallSheet" + layer };
                if (tex != null)
                {
                    m.mainTexture = tex;
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                    m.mainTextureScale = new Vector2(2.5f, 1.6f);
                }
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                var q = Shapes.Prim(PrimitiveType.Quad, sheet, new Vector3(0f, 0f, layer * 0.35f), new Vector3(10.5f - layer * 1.5f, 13.8f, 1f), m, null, false, "Sheet");
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var scroll = q.AddComponent<UvScroll>();
                scroll.Target = m;
                scroll.Speed = new Vector2(0f, layer == 0 ? 1.4f : 1.9f);
            }
            SightBlocker.Add(sheet.gameObject, Vector3.zero, new Vector3(10.5f, 13.8f, 1.2f));
            Mist(t, new Vector3(-18f, -0.6f, 57.5f));
            Sfx.Loop("water", t, new Vector3(-18f, 1f, 58f), 0.9f, 55f);
        }

        static void Mist(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Mist");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startColor = new Color(0.9f, 0.95f, 1f, 0.22f);
            main.maxParticles = 120;
            main.gravityModifier = 0.15f;
            var em = ps.emission; em.rateOverTime = 30f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(9f, 1f, 0.5f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FireFx.SoftAlphaMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        static void BuildWater(Transform root)
        {
            Scenery.WaterPlane(root, new Vector3(Pool.x, -0.8f, Pool.y - 1f), new Vector2(24f, 22f));
            WaterZones.Register(new Rect(Pool.x - 12f, Pool.y - 12f, 24f, 22f), -0.8f);
            for (float z = 44f; z > -140f; z -= 20f)
            {
                float a = StreamX(z), b = StreamX(z - 20f), m = StreamX(z - 10f);
                float x0 = Mathf.Min(a, Mathf.Min(b, m)) - 6f, x1 = Mathf.Max(a, Mathf.Max(b, m)) + 6f;
                Scenery.WaterPlane(root, new Vector3((x0 + x1) * 0.5f, -1.0f, z - 10f), new Vector2(x1 - x0, 20.2f));
                WaterZones.Register(new Rect(x0, z - 20f, x1 - x0, 20f), -1.0f);
            }
            Scenery.WaterPlane(root, new Vector3(-18f, 12.4f, 104f), new Vector2(44f, 84f));
            WaterZones.Register(new Rect(-40f, 62f, 44f, 84f), 12.4f);
        }

        // ------------------------------------------------------------------ surface
        static bool NearPortal(float x, float z, float r)
        {
            foreach (var p in portals)
                if (Vector2.Distance(new Vector2(x, z), new Vector2(p.Mouth.x, p.Mouth.z)) < r) return true;
            return Vector2.Distance(new Vector2(x, z), new Vector2(-18f, 63f)) < r;
        }

        /// <summary>Over or next to an open tunnel (terrain cut away).</summary>
        static bool NearHole(float x, float z)
        {
            int i = Mathf.RoundToInt(x / S), j = Mathf.RoundToInt(z / S);
            for (int a = -1; a <= 1; a++)
            for (int b = -1; b <= 1; b++)
                if (cells.TryGetValue((i + a, j + b), out var c) && c.Hole &&
                    Mathf.Abs(x - c.I * S) < S && Mathf.Abs(z - c.J * S) < S) return true;
            return false;
        }

        static bool Wet(float x, float z)
        {
            if (Vector2.Distance(new Vector2(x, z), Pool) < 14f) return true;
            if (z < 46f && Mathf.Abs(x - StreamX(z)) < 5.5f) return true;
            return z > 60f && Mathf.Abs(x - RiverX(z)) < 7f;
        }

        static void BuildSurface(Transform root, System.Random rnd)
        {
            System.Func<float, float, bool> free = (x, z) =>
                !Wet(x, z) && !NearPortal(x, z, 12f) && !NearHole(x, z) && ArenaLevels.Slope(Height, x, z) < 0.6f &&
                Vector2.Distance(new Vector2(x, z), CampE) > 14f && Vector2.Distance(new Vector2(x, z), CampW) > 14f &&
                !(Mathf.Abs(x + 18f) < 20f && z > 54f && z < 76f);
            var area = new Rect(-120, -120, 240, 240);

            var trees = Shapes.Pivot(root, Vector3.zero, "Trees");
            foreach (var p in Scenery.Scatter(rnd, 300, area, free, 9000))
            {
                var pos = new Vector3(p.x, Height(p.x, p.z) - 0.2f, p.z);
                if (rnd.NextDouble() < 0.8) Scenery.Pine(trees, pos, 10f + (float)rnd.NextDouble() * 9f, rnd.Next());
                else Scenery.BroadTree(trees, pos, 7f + (float)rnd.NextDouble() * 4f, rnd.Next());
            }
            foreach (var p in Scenery.Scatter(rnd, 40, area, free))
            {
                float s = 1f + (float)rnd.NextDouble() * 2.5f;
                Scenery.Rock(root, new Vector3(p.x, Height(p.x, p.z), p.z), new Vector3(s * 1.4f, s, s), (float)rnd.NextDouble() * 360f, false, false, rnd.Next(4));
            }
            var prnd = new System.Random(31);
            PropLibrary.Scatter(PropLibrary.Kind.Fern, root, prnd, 380, area, Height, free, 1.1f, 2f, false);
            PropLibrary.Scatter(PropLibrary.Kind.Shrub, root, prnd, 110, area, Height, free, 1.8f, 3.2f, false);
            PropLibrary.Scatter(PropLibrary.Kind.MossRock, root, prnd, 70, area, Height, free, 0.8f, 2f, true);
            PropLibrary.Scatter(PropLibrary.Kind.Branches, root, prnd, 90, area, Height, free, 1.2f, 2.2f, false);
            PropLibrary.Scatter(PropLibrary.Kind.Stump, root, prnd, 20, area, Height, free, 0.8f, 1.3f, true);
            PropLibrary.Scatter(PropLibrary.Kind.Log, root, prnd, 22, area, Height, free, 1.6f, 2.4f, true);
            PropLibrary.Scatter(PropLibrary.Kind.BigStump, root, prnd, 8, area, Height, free, 0.9f, 1.3f, true);
            PropLibrary.Scatter(PropLibrary.Kind.Rock, root, prnd, 40, area, Height, free, 3f, 7f, true, null, 0.15f);
            PropLibrary.Scatter(PropLibrary.Kind.Fern, root, prnd, 90, area, Height, (x, z) => !Wet(x, z) && (Vector2.Distance(new Vector2(x, z), Pool) < 18f || (z < 46f && Mathf.Abs(x - StreamX(z)) < 8f)), 1.2f, 2.2f, false);
            PropLibrary.CliffRing(root, prnd, 128f, 40, Height, 2.6f, 3.8f);

            GrassField.Create(root, new Rect(-118, -118, 236, 236), 0.9f, Height, (x, z) =>
            {
                if (Wet(x, z) || NearPortal(x, z, 7f) || NearHole(x, z)) return 0f;
                if (ArenaLevels.Slope(Height, x, z) > 0.55f) return 0f;
                return Mathf.Clamp01((Mat.Fbm(x * 0.08f + 7f, z * 0.08f, 2) - 0.25f) * 2.2f);
            }, new[] { new Color(0.22f, 0.32f, 0.11f), new Color(0.3f, 0.37f, 0.13f), new Color(0.17f, 0.26f, 0.09f) }, 21);

            Camp.Build(root, new Vector3(CampE.x, 0f, CampE.y), Height, 11, true, 8f);
            Camp.Build(root, new Vector3(CampW.x, 0f, CampW.y), Height, 12, false, 7f);
        }

        // ------------------------------------------------------------------ atmosphere
        Color sky, equator, ground, fog;
        float fogDensity, sunIntensity;
        float under;

        void Start()
        {
            sky = RenderSettings.ambientSkyColor;
            equator = RenderSettings.ambientEquatorColor;
            ground = RenderSettings.ambientGroundColor;
            fog = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;
            sunIntensity = RenderSettings.sun != null ? RenderSettings.sun.intensity : 1f;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // Below ground the sky light fades out: only torches light the catacombs.
            float target = Mathf.InverseLerp(-3f, -9f, cam.transform.position.y);
            under = Mathf.MoveTowards(under, target, Time.deltaTime * 0.8f);
            float k = Mathf.Lerp(1f, 0.1f, under);
            RenderSettings.ambientSkyColor = sky * k;
            RenderSettings.ambientEquatorColor = equator * k;
            RenderSettings.ambientGroundColor = ground * k;
            RenderSettings.fogColor = Color.Lerp(fog, new Color(0.03f, 0.025f, 0.02f), under);
            RenderSettings.fogDensity = Mathf.Lerp(fogDensity, 0.03f, under);
            if (RenderSettings.sun != null) RenderSettings.sun.intensity = sunIntensity * Mathf.Lerp(1f, 0.15f, under);
        }

        void OnDestroy()
        {
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;
        }
    }

    /// <summary>Scrolls a material's main texture (flowing water).</summary>
    public class UvScroll : MonoBehaviour
    {
        public Material Target;
        public Vector2 Speed;

        void Update()
        {
            if (Target == null) return;
            Vector2 o = Speed * Time.time;
            Target.mainTextureOffset = o;
            if (Target.HasProperty("_BaseMap")) Target.SetTextureOffset("_BaseMap", o);
        }
    }
}
