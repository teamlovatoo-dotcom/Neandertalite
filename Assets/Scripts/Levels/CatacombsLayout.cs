using System.Collections.Generic;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Pure-data layout of the "Підземелля" map: ground height, terrain holes, every box of the underground, water
    /// surfaces, torches, decorations, spawns and pickups. No GameObjects here (only Vector/Mathf maths and
    /// System.Random), so the same code is run outside Unity by Tools/levelcheck to verify and render the map.
    /// CatacombsLevel turns it into the scene.
    /// </summary>
    public class CatacombsLayout
    {
        // ---------------------------------------------------------------- constants
        public const float S = 6f;          // cell size
        public const float WallT = 0.6f;    // wall thickness
        public const float CeilH = 4.5f;    // clear height
        public const float SlabT = 0.6f;    // floor / ceiling slab thickness
        public const float MazeY = -14f;
        public const int MinI = -6, MaxI = 6, MinJ = -7, MaxJ = 2;
        public const float Half = 150f;
        public const float StreamWater = -1.0f, PoolWater = -1.0f, RiverWater = 12.4f, RiverBed = 10.6f;

        static readonly int[] DI = { 0, 1, 0, -1 };   // N E S W
        static readonly int[] DJ = { 1, 0, -1, 0 };

        public enum Part { Floor, Ceiling, Wall, Retain, Face, Post, Pillar, Altar, Cliff, Lip, Menhir, Lintel }
        public enum CellKind { Maze, Hall, Tunnel }

        public struct Box
        {
            public Vector3 Center, Size;   // size in the box's own axes
            public float Yaw, Pitch;       // rotation = Euler(0, Yaw, 0) * Euler(Pitch, 0, 0)
            public Part Part;
        }

        public struct Water { public Vector3 Center; public Vector2 Size; }

        /// <summary>A placed thing: torch, decoration, spawn or pickup. Kind is a short tag ("torch", "bones", "weapon:bow"...).</summary>
        public struct Spot { public Vector3 Pos; public float Yaw; public float Scale; public string Kind; }

        public class Cell
        {
            public int I, J;
            public float YA, YB;           // floor at the negative / positive side along the slope axis
            public bool AlongX;
            public readonly bool[] Open = new bool[4];
            public CellKind Kind;
            public int PortalSide = -1;    // side opening onto the surface
            public bool OpenAir;           // sunken stairway: no roof, retaining walls, terrain cut away
            public bool Secret;            // the waterfall cave mouth (inside the cliff block)
            public int Axis = -1;          // tunnel direction (0..3) for open-air hole shaping
            public float MinY => Mathf.Min(YA, YB);
            public float MaxY => Mathf.Max(YA, YB);
            public Vector3 Floor => new Vector3(I * S, MinY, J * S);
            public int OpenCount { get { int n = 0; foreach (var o in Open) if (o) n++; return n; } }
            /// <summary>Floor height at the middle of side d.</summary>
            public float EdgeY(int d)
            {
                if (YA == YB) return YA;
                if (AlongX) return d == 1 ? YB : d == 3 ? YA : (YA + YB) * 0.5f;
                return d == 0 ? YB : d == 2 ? YA : (YA + YB) * 0.5f;
            }
        }

        public struct Portal { public Vector3 Mouth; public Vector3 Out; public float Y; }

        public readonly Dictionary<(int, int), Cell> Cells = new Dictionary<(int, int), Cell>();
        public readonly List<Portal> Portals = new List<Portal>();
        public readonly List<Box> Boxes = new List<Box>();
        public readonly List<Water> Waters = new List<Water>();
        public readonly List<(Rect area, float y)> WaterZones = new List<(Rect, float)>();
        public readonly List<Rect> Holes = new List<Rect>();
        public readonly List<Spot> Torches = new List<Spot>();       // wall torches (Yaw = facing)
        public readonly List<Spot> TorchPosts = new List<Spot>();
        public readonly List<Spot> Decor = new List<Spot>();         // "bones", "mossrock", "roots", "fire"
        public readonly List<Spot> Spawns = new List<Spot>();
        public readonly List<Spot> Pickups = new List<Spot>();       // "weapon:id", "ammo:Arrow:8", "health", "armor"
        public readonly List<(int i0, int i1, int j0, int j1)> HallRects = new List<(int, int, int, int)>();

        public static readonly Vector2 Pool = new Vector2(-18f, 50f);
        public static readonly Vector2 CampE = new Vector2(76f, -82f), CampW = new Vector2(-78f, -50f);
        public static readonly Vector3 WaterfallTop = new Vector3(-18f, RiverWater + 0.2f, 62.6f);

        // ---------------------------------------------------------------- heights
        static float Smooth(float e0, float e1, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(e0, e1, v));
        /// <summary>River on the plateau: dead straight through the stone slot above the waterfall, then meandering.</summary>
        public static float RiverX(float z) => -18f + 8f * Mathf.Sin((z - 78f) / 20f) * Smooth(78f, 96f, z);
        public static float StreamX(float z) => -18f + 16f * Mathf.Sin((50f - z) / 30f);
        static float Lowland(float x, float z) => 0.6f + Noise.Fbm(x * 0.012f + 3f, z * 0.012f + 7f, 4) * 6f;

        public float BaseHeight(float x, float z)
        {
            float h = Lowland(x, z);
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            h += Smooth(112f, 145f, edge) * 48f * (0.7f + 0.6f * Noise.Fbm(x * 0.03f, z * 0.03f, 3));

            // Northern plateau. Behind the waterfall the cliff steps back (the cliff block stands in front of it);
            // in the east a long ramp leads up.
            float wf = Smooth(16f, 10f, Mathf.Abs(x + 18f));
            float c0 = Mathf.Lerp(54f, 66f, wf), c1 = Mathf.Lerp(62f, 70f, wf);
            float ramp = Smooth(20f, 9f, Mathf.Abs(x - 72f));
            c0 = Mathf.Lerp(c0, 18f, ramp);
            float plateau = Smooth(c0, c1, z);
            float top = 14f + Noise.Fbm(x * 0.05f, z * 0.05f + 2f, 2) * 1.5f;
            h = Mathf.Lerp(h, Mathf.Max(h, top), plateau);
            // (it rises from a spring at the foot of the northern mountains)
            if (z > 62f) h = Mathf.Lerp(h, RiverBed, Smooth(6.5f, 3f, Mathf.Abs(x - RiverX(z))) * plateau * Smooth(132f, 120f, z));

            // Plunge pool with a flat rim, and the dry ledge behind the falling water.
            float pd = Vector2.Distance(new Vector2(x, z), Pool);
            h = Mathf.Lerp(h, 0.9f, Smooth(17f, 12f, pd) * (1f - plateau));
            h = Mathf.Lerp(h, -3.4f, Smooth(10f, 6f, pd));
            float ledge = Smooth(1.5f, 0f, Mathf.Max(Mathf.Abs(x + 18f) - 8f, 0f)) * Smooth(57.5f, 59.5f, z) * Smooth(64f, 63f, z);
            h = Mathf.Lerp(h, 0.9f, ledge);

            // Stream from the pool to the south, in a proper channel.
            // (it seeps away into a marsh before the southern mountains)
            if (z < 46f) h = Mathf.Lerp(h, -2.2f, Smooth(5.5f, 2.5f, Mathf.Abs(x - StreamX(z))) * Smooth(46f, 40f, z) * Smooth(-124f, -110f, z));

            // Camp clearings.
            h = Mathf.Lerp(h, Lowland(CampE.x, CampE.y), Smooth(16f, 10f, Vector2.Distance(new Vector2(x, z), CampE)));
            h = Mathf.Lerp(h, Lowland(CampW.x, CampW.y), Smooth(16f, 10f, Vector2.Distance(new Vector2(x, z), CampW)));
            return h;
        }

        /// <summary>Final ground height: base terrain flattened in front of every stairway mouth.</summary>
        public float Height(float x, float z)
        {
            float h = BaseHeight(x, z);
            foreach (var p in Portals)
            {
                Vector2 rel = new Vector2(x - p.Mouth.x, z - p.Mouth.z);
                Vector2 o = new Vector2(p.Out.x, p.Out.z);
                float along = Vector2.Dot(rel, o), side = Mathf.Abs(rel.x * o.y - rel.y * o.x);
                if (along < -0.5f || along > 14f || side > 8f) continue;
                float k = Smooth(14f, 7f, along) * Smooth(8f, 4.5f, side);
                h = Mathf.Lerp(h, p.Y - 0.03f, k);
            }
            return h;
        }

        public bool IsHole(float x, float z)
        {
            foreach (var r in Holes) if (r.Contains(new Vector2(x, z))) return true;
            return false;
        }

        /// <summary>True inside a covered catacomb cell (under a ceiling), where daylight must not reach.</summary>
        public bool Underground(Vector3 p)
        {
            if (!Cells.TryGetValue((Mathf.RoundToInt(p.x / S), Mathf.RoundToInt(p.z / S)), out var c) || c.OpenAir) return false;
            return p.y > c.MinY - 1f && p.y < c.MaxY + CeilH;
        }

        public bool NearHole(float x, float z, float margin)
        {
            foreach (var r in Holes)
                if (x > r.xMin - margin && x < r.xMax + margin && z > r.yMin - margin && z < r.yMax + margin) return true;
            return false;
        }

        public bool NearPortal(float x, float z, float r)
        {
            foreach (var p in Portals)
                if (Vector2.Distance(new Vector2(x, z), new Vector2(p.Mouth.x, p.Mouth.z)) < r) return true;
            return Vector2.Distance(new Vector2(x, z), new Vector2(-18f, 62f)) < r;
        }

        public bool Wet(float x, float z)
        {
            if (Vector2.Distance(new Vector2(x, z), Pool) < 13f) return true;
            if (z < 46f && Mathf.Abs(x - StreamX(z)) < 5.5f) return true;
            return z > 60f && Mathf.Abs(x - RiverX(z)) < 7f;
        }

        // ---------------------------------------------------------------- grid
        Cell Add(int i, int j, float y, CellKind kind)
        {
            var c = new Cell { I = i, J = j, YA = y, YB = y, Kind = kind };
            Cells[(i, j)] = c;
            return c;
        }

        void Link(Cell a, int dir)
        {
            if (!Cells.TryGetValue((a.I + DI[dir], a.J + DJ[dir]), out var b)) return;
            a.Open[dir] = true;
            b.Open[(dir + 2) % 4] = true;
        }

        static void Slope(Cell c, int dirFrom, float yFrom, float yTo)
        {
            c.AlongX = dirFrom == 1 || dirFrom == 3;
            bool fromNegative = dirFrom == 2 || dirFrom == 3;
            c.YA = fromNegative ? yFrom : yTo;
            c.YB = fromNegative ? yTo : yFrom;
        }

        void GenerateMaze(System.Random rnd)
        {
            for (int i = MinI; i <= MaxI; i++)
            for (int j = MinJ; j <= MaxJ; j++)
                Add(i, j, MazeY, CellKind.Maze);

            var stack = new Stack<Cell>();
            var seen = new HashSet<Cell>();
            var start = Cells[(0, 0)];
            stack.Push(start);
            seen.Add(start);
            var dirs = new List<int>(4);
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                dirs.Clear();
                for (int d = 0; d < 4; d++)
                    if (Cells.TryGetValue((c.I + DI[d], c.J + DJ[d]), out var n) && !seen.Contains(n)) dirs.Add(d);
                if (dirs.Count == 0) { stack.Pop(); continue; }
                int dir = dirs[rnd.Next(dirs.Count)];
                Link(c, dir);
                var next = Cells[(c.I + DI[dir], c.J + DJ[dir])];
                seen.Add(next);
                stack.Push(next);
            }
            // Loops so nobody gets trapped in long dead ends.
            foreach (var c in new List<Cell>(Cells.Values))
                for (int d = 0; d < 2; d++)
                    if (!c.Open[d] && Cells.ContainsKey((c.I + DI[d], c.J + DJ[d])) && rnd.NextDouble() < 0.25) Link(c, d);

            Hall(-1, 1, -4, -2);    // central ossuary
            Hall(-6, -5, 0, 2);     // north-west
            Hall(4, 6, -7, -6);     // south-east
            Hall(4, 5, 1, 2);       // north-east, under the waterfall side
        }

        void Hall(int i0, int i1, int j0, int j1)
        {
            HallRects.Add((i0, i1, j0, j1));
            for (int i = i0; i <= i1; i++)
            for (int j = j0; j <= j1; j++)
            {
                var c = Cells[(i, j)];
                c.Kind = CellKind.Hall;
                if (i < i1) Link(c, 1);
                if (j < j1) Link(c, 0);
            }
        }

        /// <summary>Straight tunnel from a maze edge: level cells, then ramps (≤ maxRise per cell) until it meets the ground.</summary>
        void TunnelOut(int i, int j, int dir, int level, float maxRise)
        {
            var prev = Cells[(i, j)];
            float y = MazeY;
            for (int step = 1; step < 20; step++)
            {
                int ci = i + DI[dir] * step, cj = j + DJ[dir] * step;
                float surface = BaseHeight((ci + DI[dir]) * S, (cj + DJ[dir]) * S);
                var c = Add(ci, cj, y, CellKind.Tunnel);
                c.Axis = dir;
                Link(prev, dir);
                prev = c;
                if (step <= level) continue;
                float rise = Mathf.Clamp(surface - y, 0f, maxRise);
                Slope(c, (dir + 2) % 4, y, y + rise);
                y += rise;
                if (surface - y < 0.3f)
                {
                    var portal = Add(ci + DI[dir], cj + DJ[dir], y, CellKind.Tunnel);
                    portal.Axis = dir;
                    Link(prev, dir);
                    portal.PortalSide = dir;
                    var o = new Vector3(DI[dir], 0f, DJ[dir]);
                    Portals.Add(new Portal { Mouth = new Vector3(portal.I * S, y, portal.J * S) + o * S * 0.5f, Out = o, Y = y });
                    return;
                }
            }
        }

        /// <summary>The secret way down behind the waterfall: north under the plateau, a switchback, then south into the maze.</summary>
        void WaterfallTunnel()
        {
            var prev = Cells[(-1, MaxJ)];
            for (int j = MaxJ + 1; j <= 8; j++) { var c = Add(-1, j, MazeY, CellKind.Tunnel); c.Axis = 0; Link(prev, 0); prev = c; }
            float y = MazeY;
            for (int j = 9; j <= 13; j++)
            {
                var c = Add(-1, j, y, CellKind.Tunnel);
                c.Axis = 0;
                Slope(c, 2, y, y + 2f);
                y += 2f;
                Link(prev, 0);
                prev = c;
            }
            var t1 = Add(-1, 14, y, CellKind.Tunnel); Link(prev, 0);
            var t2 = Add(-2, 14, y, CellKind.Tunnel); Link(t1, 3);
            var t3 = Add(-3, 14, y, CellKind.Tunnel); Link(t2, 3);
            var a = Add(-3, 13, y, CellKind.Tunnel); Slope(a, 0, y, y + 2f); Link(t3, 2); y += 2f;
            var b = Add(-3, 12, y, CellKind.Tunnel); Slope(b, 0, y, 0.9f); Link(a, 2);
            var mouth = Add(-3, 11, 0.9f, CellKind.Tunnel); Link(b, 2);
            mouth.PortalSide = 2;
            mouth.Secret = true;
        }

        /// <summary>Tunnel cells whose roof would poke out of the ground become open-air sunken stairways.</summary>
        void ClassifyOpenAir()
        {
            foreach (var c in Cells.Values)
            {
                if (c.Kind != CellKind.Tunnel || c.Secret) continue;
                float roof = c.MaxY + CeilH + SlabT;
                float minGround = float.MaxValue;
                for (int a = -1; a <= 1; a++)
                for (int b = -1; b <= 1; b++)
                    minGround = Mathf.Min(minGround, Height(c.I * S + a * S * 0.5f, c.J * S + b * S * 0.5f));
                c.OpenAir = roof + 0.6f > minGround;
            }
            // Terrain cut-outs: across the stairway just inside the retaining walls, along it up to the neighbours.
            foreach (var c in Cells.Values)
            {
                if (c.Secret)
                {
                    // Under the cliff block: the terrain cliff would cut through the cave mouth.
                    Holes.Add(Rect.MinMaxRect(-21.05f, c.J * S - S * 0.5f - 0.3f, -14.95f, CliffBack - 0.2f));
                    continue;
                }
                if (!c.OpenAir) continue;
                bool alongX = c.Axis == 1 || c.Axis == 3;
                float across = S * 0.5f - WallT * 0.5f;
                float lo = -S * 0.5f, hi = S * 0.5f;
                int negSide = alongX ? 3 : 2, posSide = alongX ? 1 : 0;
                // Into the next open stairway cell: overlap. At the mouth: stop just short of the floor's end.
                lo += Extends(c, negSide) ? (c.PortalSide == negSide ? 0.15f : -0.3f) : 0f;
                hi += Extends(c, posSide) ? (c.PortalSide == posSide ? -0.15f : 0.3f) : 0f;
                Rect r = alongX
                    ? Rect.MinMaxRect(c.I * S + lo, c.J * S - across, c.I * S + hi, c.J * S + across)
                    : Rect.MinMaxRect(c.I * S - across, c.J * S + lo, c.I * S + across, c.J * S + hi);
                Holes.Add(r);
            }
        }

        bool Extends(Cell c, int side)
        {
            if (c.PortalSide == side) return true;
            if (!c.Open[side]) return false;
            return Cells.TryGetValue((c.I + DI[side], c.J + DJ[side]), out var n) && n.OpenAir;
        }

        // ---------------------------------------------------------------- geometry
        void AddBox(Vector3 c, Vector3 size, Part part, float yaw = 0f, float pitch = 0f)
            => Boxes.Add(new Box { Center = c, Size = size, Yaw = yaw, Pitch = pitch, Part = part });

        /// <summary>Floor (offset &lt; 0) or ceiling slab following the cell's slope.</summary>
        void Slab(Cell c, float offset, Part part)
        {
            float rise = c.YB - c.YA;
            float a = Mathf.Atan2(rise, S);
            float len = Mathf.Sqrt(S * S + rise * rise);
            Vector3 up = c.AlongX ? new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f) : new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a));
            Vector3 centre = new Vector3(c.I * S, (c.YA + c.YB) * 0.5f, c.J * S) + up * offset;
            AddBox(centre, new Vector3(S + 0.02f, SlabT, len + 0.02f), part, c.AlongX ? 90f : 0f, -a * Mathf.Rad2Deg);
        }

        float GroundMaxAlong(Vector3 a, Vector3 b)
        {
            float m = float.MinValue;
            for (int k = 0; k <= 6; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, k / 6f);
                m = Mathf.Max(m, Height(p.x, p.z));
            }
            return m;
        }

        void BuildCell(Cell c, HashSet<(int, int)> posts)
        {
            Slab(c, -SlabT * 0.5f, Part.Floor);
            if (!c.OpenAir) Slab(c, CeilH + SlabT * 0.5f, Part.Ceiling);
            float lo = c.MinY - SlabT;
            float hiCovered = c.MaxY + CeilH + SlabT;
            for (int d = 0; d < 4; d++)
            {
                if (c.Open[d] || c.PortalSide == d) continue;
                Vector3 mid = new Vector3(c.I * S + DI[d] * (S * 0.5f - WallT * 0.5f), 0f, c.J * S + DJ[d] * (S * 0.5f - WallT * 0.5f));
                Vector3 along = DI[d] != 0 ? new Vector3(0f, 0f, S * 0.5f) : new Vector3(S * 0.5f, 0f, 0f);
                float hi = hiCovered;
                Part part = Part.Wall;
                if (c.OpenAir)
                {
                    // Retaining wall: just above the ground outside it, so the cut edge of the terrain is hidden.
                    Vector3 outside = new Vector3(DI[d], 0f, DJ[d]) * 0.5f;
                    hi = Mathf.Max(c.MaxY + 1.1f, GroundMaxAlong(mid - along + outside, mid + along + outside) + 0.3f);
                    part = Part.Retain;
                }
                Vector3 size = DI[d] != 0 ? new Vector3(WallT, hi - lo, S) : new Vector3(S, hi - lo, WallT);
                AddBox(new Vector3(mid.x, (lo + hi) * 0.5f, mid.z), size, part);
            }
            // Where a covered tunnel meets an open stairway, a stone face closes the gap up to the ground.
            for (int d = 0; d < 4; d++)
            {
                if (c.OpenAir || !c.Open[d]) continue;
                if (!Cells.TryGetValue((c.I + DI[d], c.J + DJ[d]), out var n) || !n.OpenAir) continue;
                float roof = c.EdgeY(d) + CeilH + SlabT;
                Vector3 mid = new Vector3(c.I * S + DI[d] * (S * 0.5f - WallT * 0.5f), 0f, c.J * S + DJ[d] * (S * 0.5f - WallT * 0.5f));
                Vector3 along = DI[d] != 0 ? new Vector3(0f, 0f, S * 0.5f) : new Vector3(S * 0.5f, 0f, 0f);
                float top = Mathf.Max(roof + 0.8f, GroundMaxAlong(mid - along, mid + along) + 0.35f);
                Vector3 size = DI[d] != 0 ? new Vector3(WallT * 1.4f, top - (roof - SlabT), S + WallT * 2f) : new Vector3(S + WallT * 2f, top - (roof - SlabT), WallT * 1.4f);
                AddBox(new Vector3(mid.x, (top + roof - SlabT) * 0.5f, mid.z), size, Part.Face);
                // Door frame stones and torches on the stairway side.
                Vector3 o = new Vector3(DI[d], 0f, DJ[d]);
                Vector3 side = new Vector3(-o.z, 0f, o.x);
                Vector3 door = new Vector3(c.I * S, c.EdgeY(d), c.J * S) + o * (S * 0.5f + 0.2f);
                for (int s = -1; s <= 1; s += 2)
                {
                    AddBox(door + side * s * 2.35f + Vector3.up * (CeilH * 0.5f), new Vector3(0.9f, CeilH, 0.9f), Part.Menhir, Mathf.Atan2(o.x, o.z) * Mathf.Rad2Deg);
                    Torches.Add(new Spot { Pos = door + o * 0.9f + side * s * 2.2f + Vector3.up * 2.4f, Yaw = Mathf.Atan2(o.x, o.z) * Mathf.Rad2Deg, Kind = "torch" });
                }
            }
            // Corner posts shared between cells (only for covered cells: open stairways keep clean walls).
            if (c.OpenAir) return;
            for (int a = 0; a <= 1; a++)
            for (int b = 0; b <= 1; b++)
            {
                var key = (c.I * 2 + (a == 0 ? -1 : 1), c.J * 2 + (b == 0 ? -1 : 1));
                if (!posts.Add(key)) continue;
                Vector3 p = new Vector3(c.I * S + (a == 0 ? -1 : 1) * S * 0.5f, (lo + hiCovered) * 0.5f, c.J * S + (b == 0 ? -1 : 1) * S * 0.5f);
                AddBox(p, new Vector3(WallT * 1.6f, hiCovered - lo, WallT * 1.6f), Part.Post);
            }
        }

        void PortalMouth(Cell c)
        {
            int d = c.PortalSide;
            Vector3 o = new Vector3(DI[d], 0f, DJ[d]);
            Vector3 side = new Vector3(-o.z, 0f, o.x);
            Vector3 mouth = new Vector3(c.I * S, c.EdgeY(d), c.J * S) + o * (S * 0.5f + 1.2f);
            float yaw = Mathf.Atan2(o.x, o.z) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
            {
                AddBox(mouth + side * s * 3.9f + Vector3.up * 1.6f, new Vector3(1.2f, 4.2f, 1.0f), Part.Menhir, yaw + s * 6f);
                TorchPosts.Add(new Spot { Pos = mouth + o * 1.2f + side * s * 2.6f, Kind = "post" });
            }
        }

        void Decorate(System.Random rnd)
        {
            foreach (var c in Cells.Values)
            {
                if (c.PortalSide >= 0 && !c.Secret) PortalMouth(c);
                if (c.OpenAir) continue;
                Vector3 floor = c.Floor;
                var closed = new List<int>();
                for (int d = 0; d < 4; d++) if (!c.Open[d] && c.PortalSide != d) closed.Add(d);
                if (c.Kind == CellKind.Tunnel)
                {
                    if ((c.I + c.J) % 2 == 0 && closed.Count > 0) WallTorch(c, closed[0]);
                    continue;
                }
                bool deadEnd = c.OpenCount == 1;
                if (c.Kind == CellKind.Maze && closed.Count > 0 && (deadEnd || rnd.NextDouble() < 0.3)) WallTorch(c, closed[rnd.Next(closed.Count)]);
                if (deadEnd)
                {
                    int open = System.Array.IndexOf(c.Open, true);
                    Decor.Add(new Spot { Pos = floor + new Vector3(-DI[open], 0f, -DJ[open]) * 1.5f, Yaw = rnd.Next(360), Scale = 0.75f, Kind = "bones" });
                }
                else if (c.Kind == CellKind.Maze && rnd.NextDouble() < 0.15)
                    Decor.Add(new Spot { Pos = floor + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * 2.4f, Yaw = rnd.Next(360), Scale = 0.6f, Kind = "mossrock" });
                if (rnd.NextDouble() < 0.15)
                    Decor.Add(new Spot { Pos = floor + Vector3.up * CeilH, Yaw = rnd.Next(360), Scale = 0.5f, Kind = "roots" });
            }
            for (int k = 0; k < HallRects.Count; k++)
            {
                var h = HallRects[k];
                Vector3 anchor = HallAnchor(k);
                AddBox(anchor + new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 1.4f), Part.Altar);
                Vector3 fire = anchor + new Vector3(S, 0f, 0f);
                Decor.Add(new Spot { Pos = fire, Kind = "fire" });
                for (int i = h.i0; i <= h.i1; i++)
                for (int j = h.j0; j <= h.j1; j++)
                {
                    Vector3 cc = new Vector3(i * S, MazeY, j * S);
                    if ((cc - anchor).sqrMagnitude < 1f || (cc - fire).sqrMagnitude < 1f || rnd.NextDouble() > 0.5) continue;
                    Decor.Add(new Spot { Pos = cc + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * 2f, Yaw = rnd.Next(360), Scale = 0.85f, Kind = "bones" });
                }
            }
        }

        /// <summary>Centre of the cell holding a hall's altar (always a cell centre, never a pillar corner).</summary>
        public Vector3 HallAnchor(int k)
        {
            var h = HallRects[k];
            int i = h.i0 + (h.i1 - h.i0) / 2, j = h.j0 + (h.j1 - h.j0) / 2;
            return new Vector3(i * S, MazeY, j * S);
        }

        void WallTorch(Cell c, int side)
        {
            Vector3 n = new Vector3(DI[side], 0f, DJ[side]);
            Vector3 p = new Vector3(c.I * S, c.MaxY + 2.3f, c.J * S) + n * (S * 0.5f - WallT - 0.25f);
            Torches.Add(new Spot { Pos = p, Yaw = Mathf.Atan2(-n.x, -n.z) * Mathf.Rad2Deg, Kind = "torch" });
        }

        public const float CliffFront = 62.4f, CliffBack = 78f;

        void WaterfallCliff()
        {
            // The block the secret cave is cut into. Its middle part is the river bed of the stone slot the water
            // spills from, and it hides the terrain hole above the cave mouth.
            float zc = (CliffFront + CliffBack) * 0.5f, zs = CliffBack - CliffFront;
            AddBox(new Vector3(-27.6f, 7f, zc), new Vector3(13f, 16f, zs), Part.Cliff);
            AddBox(new Vector3(-8.4f, 7f, zc), new Vector3(13f, 16f, zs), Part.Cliff);
            float roof = 0.9f + CeilH + SlabT;
            AddBox(new Vector3(-18f, (roof + RiverBed) * 0.5f, zc), new Vector3(6.4f, RiverBed - roof, zs), Part.Cliff);
        }

        void BuildWater()
        {
            // Pool and stream share one level and their planes only touch (no overlapping, flickering water).
            Waters.Add(new Water { Center = new Vector3(Pool.x, PoolWater, 50f), Size = new Vector2(24f, 20f) });
            WaterZones.Add((new Rect(Pool.x - 12f, 40f, 24f, 20f), PoolWater));   // (bottoms: see CatacombsLevel)
            Strips(40f, -118f, 20f, StreamX, 6.5f, StreamWater);
            // River: the stone slot above the waterfall, then the meandering channel up to its spring.
            Waters.Add(new Water { Center = new Vector3(-18f, RiverWater, (CliffFront + CliffBack) * 0.5f), Size = new Vector2(6.2f, CliffBack - CliffFront) });
            WaterZones.Add((Rect.MinMaxRect(-21.1f, CliffFront, -14.9f, CliffBack), RiverWater));
            Strips(CliffBack, 126f, 16f, RiverX, 7f, RiverWater);
        }

        /// <summary>Water planes along a winding channel, cut in strips that just touch (never overlap).</summary>
        void Strips(float from, float to, float len, System.Func<float, float> centre, float margin, float y)
        {
            float dir = to > from ? 1f : -1f;
            for (float z = from; dir * (to - z) > 0.01f; z += dir * len)
            {
                float z2 = dir > 0 ? Mathf.Min(z + len, to) : Mathf.Max(z - len, to);
                float x0 = float.MaxValue, x1 = float.MinValue;
                for (int k = 0; k <= 10; k++)
                {
                    float cx = centre(Mathf.Lerp(z, z2, k / 10f));
                    x0 = Mathf.Min(x0, cx - margin);
                    x1 = Mathf.Max(x1, cx + margin);
                }
                float zl = Mathf.Min(z, z2), zh = Mathf.Max(z, z2);
                Waters.Add(new Water { Center = new Vector3((x0 + x1) * 0.5f, y, (zl + zh) * 0.5f), Size = new Vector2(x1 - x0, zh - zl) });
                WaterZones.Add((Rect.MinMaxRect(x0, zl, x1, zh), y));
            }
        }

        // ---------------------------------------------------------------- spawns & items
        void Items()
        {
            Vector3 G(float x, float z) => new Vector3(x, Height(x, z), z);
            Vector3 Hall(int k, float dz) => HallAnchor(k) + new Vector3(0f, 0f, dz);
            void Sp(Vector3 p, float yaw) => Spawns.Add(new Spot { Pos = p + Vector3.up * 0.2f, Yaw = yaw, Kind = "spawn" });
            void It(Vector3 p, string kind) => Pickups.Add(new Spot { Pos = p + Vector3.up * 0.12f, Kind = kind });

            Sp(G(-95f, -95f), 45f);
            Sp(G(95f, -95f), 315f);
            Sp(G(98f, 25f), 250f);
            Sp(G(-100f, 25f), 110f);
            Sp(G(45f, 100f), 200f);
            Sp(G(-72f, 100f), 150f);
            Sp(Hall(0, -S * 0.9f), 0f);
            Sp(Hall(1, -S * 0.9f), 0f);
            Sp(Hall(2, S * 0.9f), 180f);

            It(G(-35f, -15f), "weapon:bow");
            It(G(40f, -70f), "weapon:spear");
            It(G(55f, 20f), "weapon:slingshot");
            It(G(-60f, 60f), "weapon:axe");
            It(G(34f, 95f), "weapon:bow");
            It(G(-90f, -20f), "weapon:club");
            It(new Vector3(-20f, 0.9f, 61.4f), "weapon:obsidian_knife");          // behind the waterfall
            It(Hall(0, 0f) + Vector3.up * 1f, "weapon:obsidian_spear");             // on the altars
            It(Hall(1, 0f) + Vector3.up * 1f, "weapon:obsidian_axe");
            It(Hall(2, 0f) + Vector3.up * 1f, "weapon:obsidian_knife");
            It(Hall(3, 0f) + Vector3.up * 1f, "weapon:bow");
            foreach (var p in new[] { G(-20f, -40f), G(30f, 10f), G(-50f, 90f), G(80f, -80f), G(-80f, -75f) }) It(p, "ammo:Arrow:8");
            foreach (var p in new[] { G(10f, -30f), G(-40f, 20f), G(70f, 70f), G(-100f, -100f) }) It(p, "ammo:Stone:10");
            It(new Vector3(-15.5f, 0.9f, 61.4f), "armor");
            var rnd = new System.Random(5);
            var maze = new List<Cell>();
            foreach (var c in Cells.Values) if (c.Kind == CellKind.Maze && c.OpenCount >= 2) maze.Add(c);
            for (int k = 0; k < 9 && maze.Count > 0; k++)
            {
                var c = maze[rnd.Next(maze.Count)];
                maze.Remove(c);
                It(c.Floor, k % 3 == 0 ? "health" : k % 3 == 1 ? "ammo:Arrow:6" : "ammo:Stone:6");
            }
        }

        // ---------------------------------------------------------------- generate
        public static CatacombsLayout Generate(int seed = 2024)
        {
            var L = new CatacombsLayout();
            var rnd = new System.Random(seed);
            L.GenerateMaze(rnd);
            L.WaterfallTunnel();
            L.TunnelOut(MinI, -2, 3, 1, 2.4f);     // west
            L.TunnelOut(MaxI, -2, 1, 1, 2.4f);     // east
            L.TunnelOut(MaxI, -6, 1, 1, 2.4f);     // south-east
            L.TunnelOut(3, MaxJ, 0, 5, 2.8f);      // under the cliff, up onto the plateau
            L.ClassifyOpenAir();
            var posts = new HashSet<(int, int)>();
            foreach (var c in L.Cells.Values) L.BuildCell(c, posts);
            L.WaterfallCliff();
            L.Decorate(rnd);
            L.BuildWater();
            L.Items();
            return L;
        }
    }
}
