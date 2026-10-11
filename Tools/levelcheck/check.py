"""Automatic checks of the generated catacombs layout (run Program.cs first). Usage: python3 check.py <dir>"""
import json, sys, math
import numpy as np
d = sys.argv[1]
L = json.load(open(d + "/layout.json"))
raw = np.fromfile(d + "/height.bin", dtype=np.float32).reshape(601, 601, 3)
H, HOLE, WET = raw[..., 0], raw[..., 1] > 0.5, raw[..., 2] > 0.5

def g(x, z):
    fx = (x + 150) / 0.5; fz = (z + 150) / 0.5
    i0 = int(min(599, max(0, math.floor(fx)))); j0 = int(min(599, max(0, math.floor(fz))))
    tx = min(1, max(0, fx - i0)); tz = min(1, max(0, fz - j0))
    h = H[j0, i0] * (1 - tx) * (1 - tz) + H[j0, i0 + 1] * tx * (1 - tz) + H[j0 + 1, i0] * (1 - tx) * tz + H[j0 + 1, i0 + 1] * tx * tz
    return float(h)
def hole(x, z):
    i = int(round((x + 150) / 0.5)); j = int(round((z + 150) / 0.5))
    if 0 <= i < 601 and 0 <= j < 601: return bool(HOLE[j, i])
    return False

def rot(yaw, pitch):
    y, p = math.radians(yaw), math.radians(pitch)
    Ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    Rx = np.array([[1, 0, 0], [0, math.cos(p), -math.sin(p)], [0, math.sin(p), math.cos(p)]])
    return Ry @ Rx

boxes = []
for b in L["boxes"]:
    c = np.array(b[0:3]); s = np.array(b[3:6]); R = rot(b[6], b[7])
    boxes.append(dict(c=c, s=s, R=R, part=b[8], Rinv=R.T))
def inside_xz(bx, x, z, pad=0.0):
    # vertical column test against an (assumed nearly upright) box footprint
    p = bx["Rinv"] @ (np.array([x, bx["c"][1], z]) - bx["c"])
    return abs(p[0]) <= bx["s"][0] / 2 + pad and abs(p[2]) <= bx["s"][2] / 2 + pad
def top_at(bx, x, z):
    # height of the top face above (x,z) for slabs/walls: sample by projecting
    R = bx["R"]; up = R[:, 1]; c = bx["c"]; hs = bx["s"] / 2
    # top plane: point c + up*hs.y, normal up
    n = up; p0 = c + up * hs[1]
    if abs(n[1]) < 1e-3: return c[1] + hs[1]
    return p0[1] - (n[0] * (x - p0[0]) + n[2] * (z - p0[2])) / n[1]

problems = {}
def report(kind, msg):
    problems.setdefault(kind, []).append(msg)

def under_wall(x, z, y):
    for bx in boxes:
        if bx["part"] in ("Retain", "Wall", "Face", "Post") and inside_xz(bx, x, z, 0.02) and top_at(bx, x, z) >= y: return True
    return False

def in_cliff(p):
    for bx in boxes:
        if bx["part"] != "Cliff": continue
        q = bx["Rinv"] @ (p - bx["c"])
        if all(abs(q[i]) <= bx["s"][i] / 2 + 0.05 for i in range(3)): return True
    return False

# 1. underground parts poking out of the ground (where the ground isn't cut away)
under_parts = {"Floor", "Ceiling", "Wall", "Post", "Altar", "Pillar"}
for k, bx in enumerate(boxes):
    if bx["part"] not in under_parts: continue
    hs = bx["s"] / 2
    for a in (-1, -0.5, 0, 0.5, 1):
        for b in (-1, -0.5, 0, 0.5, 1):
            p = bx["c"] + bx["R"] @ np.array([a * hs[0], hs[1], b * hs[2]])
            x, z = p[0], p[2]
            if hole(x, z) or in_cliff(p) or under_wall(x, z, p[1]): continue
            gr = g(x, z)
            if p[1] > gr - (0.15 if bx["part"] != "Floor" else -0.2):
                report("pokes_out", f"{bx['part']} at ({x:.1f},{p[1]:.1f},{z:.1f}) ground {gr:.1f}")
# 2. every hole sample must have a floor under it
floors = [bx for bx in boxes if bx["part"] == "Floor"]
hy, hx = np.nonzero(HOLE)
void = 0
for j, i in zip(hy[::3], hx[::3]):
    x, z = -150 + i * 0.5, -150 + j * 0.5
    if not any(inside_xz(f, x, z, 0.05) for f in floors):
        void += 1
        if void < 6: report("hole_without_floor", f"({x:.1f},{z:.1f})")
# 3. hole edges must be hidden by a wall / face / cliff or be at floor level (stairway mouth)
edge_parts = [bx for bx in boxes if bx["part"] in ("Retain", "Face", "Cliff", "Wall", "Post", "Lip", "Menhir")]
for r in L["holes"]:
    x0, z0, x1, z1 = r
    pts = []
    for t in np.linspace(0, 1, 13):
        pts += [(x0 + (x1 - x0) * t, z0 - 0.2), (x0 + (x1 - x0) * t, z1 + 0.2), (x0 - 0.2, z0 + (z1 - z0) * t), (x1 + 0.2, z0 + (z1 - z0) * t)]
    for (x, z) in pts:
        if hole(x, z): continue
        gr = g(x, z)
        covered = any(inside_xz(bx, x, z, 0.05) and top_at(bx, x, z) >= gr - 0.05 for bx in edge_parts)
        flat = any(inside_xz(f, x, z, 0.6) and abs(top_at(f, x, z) - gr) < 0.35 for f in floors)
        if not covered and not flat:
            report("open_hole_edge", f"({x:.1f},{z:.1f}) ground {gr:.2f}")
# 4. water: z-fighting with the banks and edges hanging in the air
cliffs = [bx for bx in boxes if bx["part"] in ("Cliff", "Lip")]
planes = L["water"]
def under_other_plane(x, z, me):
    for k, w in enumerate(planes):
        if k == me: continue
        if abs(x - w[0]) <= w[3] / 2 and abs(z - w[2]) <= w[4] / 2 and abs(w[1] - planes[me][1]) < 0.01: return True
    return False
for k, w in enumerate(planes):
    cx, y, cz, sx, sz = w
    zf = vis = 0
    for x in np.arange(cx - sx / 2, cx + sx / 2, 0.5):
        for z in np.arange(cz - sz / 2, cz + sz / 2, 0.5):
            gr = g(x, z)
            if gr < y:
                vis += 1
                if y - gr < 0.12: zf += 1
    if vis and zf / vis > 0.04: report("water_zfight", f"plane {k} y={y} {zf}/{vis} samples within 12 cm of the bank")
    hang = 0
    for t in np.linspace(0, 1, 40):
        for (x, z) in ((cx - sx / 2 + sx * t, cz - sz / 2), (cx - sx / 2 + sx * t, cz + sz / 2), (cx - sx / 2, cz - sz / 2 + sz * t), (cx + sx / 2, cz - sz / 2 + sz * t)):
            if g(x, z) < y - 0.02 and not under_other_plane(x, z, k) and not any(inside_xz(c, x, z, 0.0) for c in cliffs):
                hang += 1
    if hang: report("water_edge_hanging", f"plane {k} y={y} at {hang} border samples")
    # holes under water
    for x in np.arange(cx - sx / 2, cx + sx / 2, 1.0):
        for z in np.arange(cz - sz / 2, cz + sz / 2, 1.0):
            if hole(x, z) and not any(inside_xz(c, x, z, 0.0) for c in cliffs): report("hole_under_water", f"plane {k} ({x:.1f},{z:.1f})"); break
# 5. spawns / pickups / decor on the ground and not inside walls
cells = {(c[0], c[1]): c for c in L["cells"]}
walls = [bx for bx in boxes if bx["part"] in ("Wall", "Post", "Retain", "Face", "Menhir", "Cliff", "Altar", "Pillar")]
for name in ("spawns", "pickups", "decor", "posts"):
    for s in L[name]:
        x, y, z = s[0], s[1], s[2]
        if y > -6:
            gr = g(x, z)
            if hole(x, z):
                fl = [top_at(f, x, z) for f in floors if inside_xz(f, x, z)]
                gr = max(fl) if fl else gr
            if abs(y - gr) > 1.3 and not (name == "pickups" and abs(y - gr - 1.0) < 0.3):
                report(name + "_height", f"{s[5]} at ({x:.1f},{y:.1f},{z:.1f}) ground {gr:.1f}")
        for bx in walls:
            if bx["part"] == "Altar" and name == "pickups": continue
            if inside_xz(bx, x, z, -0.05) and bx["c"][1] - bx["s"][1] / 2 - 0.1 < y < bx["c"][1] + bx["s"][1] / 2:
                report(name + "_in_wall", f"{s[5]} at ({x:.1f},{y:.1f},{z:.1f}) inside {bx['part']}"); break
# 6. torches must sit in front of a wall, inside a cell
for t in L["torches"]:
    x, y, z = t[0], t[1], t[2]
    for bx in walls:
        if inside_xz(bx, x, z, -0.02) and bx["c"][1] - bx["s"][1] / 2 < y < bx["c"][1] + bx["s"][1] / 2:
            report("torch_in_wall", f"({x:.1f},{y:.1f},{z:.1f}) in {bx['part']}"); break

total = 0
for k, v in problems.items():
    print(f"== {k}: {len(v)}")
    for m in v[:int(sys.argv[2]) if len(sys.argv) > 2 else 8]: print("   ", m)
    total += len(v)
print("TOTAL PROBLEMS", total)
