// Generates the catacombs layout outside Unity and dumps it for checks / rendering (see check.py, render_blender.py).
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Neanderthal;

class Program
{
    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : ".";
        var L = CatacombsLayout.Generate();
        var sb = new StringBuilder();
        sb.Append("{\"boxes\":[");
        bool first = true;
        foreach (var b in L.Boxes)
        {
            if (!first) sb.Append(','); first = false;
            sb.Append($"[{F(b.Center.x)},{F(b.Center.y)},{F(b.Center.z)},{F(b.Size.x)},{F(b.Size.y)},{F(b.Size.z)},{F(b.Yaw)},{F(b.Pitch)},\"{b.Part}\"]");
        }
        sb.Append("],\"water\":[");
        first = true;
        foreach (var w in L.Waters) { if (!first) sb.Append(','); first = false; sb.Append($"[{F(w.Center.x)},{F(w.Center.y)},{F(w.Center.z)},{F(w.Size.x)},{F(w.Size.y)}]"); }
        sb.Append("],\"holes\":[");
        first = true;
        foreach (var r in L.Holes) { if (!first) sb.Append(','); first = false; sb.Append($"[{F(r.xMin)},{F(r.yMin)},{F(r.xMax)},{F(r.yMax)}]"); }
        void Spots(string name, System.Collections.Generic.List<CatacombsLayout.Spot> list)
        {
            sb.Append($"],\"{name}\":[");
            bool f2 = true;
            foreach (var s in list) { if (!f2) sb.Append(','); f2 = false; sb.Append($"[{F(s.Pos.x)},{F(s.Pos.y)},{F(s.Pos.z)},{F(s.Yaw)},{F(s.Scale)},\"{s.Kind}\"]"); }
        }
        Spots("torches", L.Torches);
        Spots("posts", L.TorchPosts);
        Spots("decor", L.Decor);
        Spots("spawns", L.Spawns);
        Spots("pickups", L.Pickups);
        sb.Append("],\"cells\":[");
        first = true;
        foreach (var c in L.Cells.Values)
        {
            if (!first) sb.Append(','); first = false;
            sb.Append($"[{c.I},{c.J},{F(c.YA)},{F(c.YB)},{(c.AlongX ? 1 : 0)},\"{c.Kind}\",{(c.OpenAir ? 1 : 0)},{c.PortalSide},{(c.Open[0] ? 1 : 0)},{(c.Open[1] ? 1 : 0)},{(c.Open[2] ? 1 : 0)},{(c.Open[3] ? 1 : 0)}]");
        }
        sb.Append("],\"portals\":[");
        first = true;
        foreach (var p in L.Portals) { if (!first) sb.Append(','); first = false; sb.Append($"[{F(p.Mouth.x)},{F(p.Mouth.y)},{F(p.Mouth.z)},{F(p.Out.x)},{F(p.Out.z)}]"); }
        sb.Append("]}");
        File.WriteAllText(Path.Combine(outDir, "layout.json"), sb.ToString());

        // Ground height (+ hole mask, wet mask) on a 0.5 m grid.
        int n = 601; float step = 0.5f;
        using (var bw = new BinaryWriter(File.Create(Path.Combine(outDir, "height.bin"))))
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = -150f + i * step, z = -150f + j * step;
                bw.Write(L.Height(x, z));
                bw.Write(L.IsHole(x, z) ? 1f : 0f);
                bw.Write(L.Wet(x, z) ? 1f : 0f);
            }
        Console.WriteLine($"boxes {L.Boxes.Count} cells {L.Cells.Count} portals {L.Portals.Count} holes {L.Holes.Count} torches {L.Torches.Count}");
    }
}
