"""Neanderthal camp structures -> Assets/Resources/Models/Camp: hide tipi, mammoth-bone hut, thatched lean-to,
hide drying rack, wood pile, bone pile. Z is up, the doorway faces -Y (marker 'Fwd').
Run: blender -b -P Tools/blender/make_structures.py -- [preview_dir]"""
import bpy, sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mathutils import Vector, Matrix
import meshkit as mk

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models", "Camp")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = args[0] if args else None
DOOR = -math.pi / 2   # doorway faces -Y


def angdiff(a, b):
    return (a - b + math.pi) % (2 * math.pi) - math.pi


def pole(b, p0, p1, r0, r1, seed, mat="pole_bark", n=10, amp=0.025):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    side = d.cross(Vector((0, 0, 1)))
    if side.length < 1e-4:
        side = Vector((1, 0, 0))
    side.normalize()
    up = side.cross(d).normalized()
    pts = [p0 + d * (i / n) + side * amp * mk.smooth_noise(i * 0.7, seed) + up * amp * mk.smooth_noise(i * 0.7 + 5, seed)
           for i in range(n + 1)]
    mk.tube(b, mat, pts, [r0 + (r1 - r0) * i / n for i in range(n + 1)], segs=9, tex=0.9, rough=0.06, seed=seed,
            cap_mat="wood_fine")


def lash(b, center, radius, seed, turns=5, axis=Vector((0, 0, 1))):
    """A few loops of sinew cord around a joint."""
    axis = Vector(axis).normalized()
    ref = Vector((1, 0, 0)) if abs(axis.x) < 0.9 else Vector((0, 1, 0))
    u = axis.cross(ref).normalized(); v = axis.cross(u)
    rnd = random.Random(seed)
    for k in range(turns):
        tilt = rnd.uniform(-0.6, 0.6)
        pts = []
        for i in range(17):
            a = i / 16 * 2 * math.pi
            pts.append(Vector(center) + (u * math.cos(a) + v * math.sin(a)) * radius + axis * (math.cos(a) * tilt * radius + (k - turns / 2) * 0.012))
        mk.tube(b, "sinew", pts, 0.006, segs=5, tex=0.1, caps=False)


# ------------------------------------------------------------------ hide tipi
def tipi(_):
    R, H = 2.1, 3.45
    npoles = 11
    rnd = random.Random(3)
    poles = [DOOR + math.pi / npoles + 2 * math.pi * k / npoles + rnd.uniform(-0.05, 0.05) for k in range(npoles)]
    b = mk.Builder()
    for k, a in enumerate(poles):
        base = Vector((math.cos(a) * (R - 0.12), math.sin(a) * (R - 0.12), 0))
        cross = Vector((math.cos(a + 2.2) * 0.07, math.sin(a + 2.2) * 0.07, H - 0.12))
        top = cross + (cross - base).normalized() * rnd.uniform(0.7, 1.0)
        pole(b, base, top, 0.045, 0.026, k)
    lash(b, (0, 0, H - 0.12), 0.11, 1, turns=6)
    # lacing pins above the door
    for i in range(8):
        z = 1.55 + i * 0.2
        r = R * (1 - z / (H + 0.05)) + 0.03
        c = Vector((math.cos(DOOR) * r, math.sin(DOOR) * r, z))
        mk.tube(b, "wood_fine", [c + Vector((-0.09, 0.02, 0)), c + Vector((0.09, 0.02, 0.01))], 0.012, segs=6, tex=0.2)
    # stones holding the hem
    for k in range(18):
        a = 2 * math.pi * k / 18 + rnd.uniform(-0.08, 0.08)
        if abs(angdiff(a, DOOR)) < 0.36:
            continue
        rr = rnd.uniform(0.11, 0.18)
        mk.lumpy_sphere(b, "rock", (math.cos(a) * (R + 0.05), math.sin(a) * (R + 0.05), rr * 0.35), rr, (1.2, 1, 0.75),
                        sub=2, amp=0.25, seed=k, tex=0.5)
    b.build("frame")

    c = mk.Builder()
    def cover(u, v):
        a = DOOR + u * 2 * math.pi
        h = v * (H - 0.32)
        r = R * (1 - h / (H + 0.05))
        # sag between poles, strongest mid-height
        fr = min(abs(angdiff(a, p)) for p in poles) / (math.pi / npoles)
        sag = 0.07 * (1 - h / H) ** 0.5 * fr ** 1.6
        wr = 0.025 * mk.smooth_noise(a * 3 + h * 2.3, 2) + 0.012 * mk.smooth_noise(a * 11 + h * 7, 4)
        flare = 0.06 * max(0.0, 1 - h / 0.25) ** 2
        rr = r * (1 - sag) + wr + flare
        z = h + (0.03 * mk.smooth_noise(a * 4, 6) if v == 0 else 0)
        return (math.cos(a) * rr, math.sin(a) * rr, z), (u * 2 * math.pi * R * 0.7, h)
    def door(u, v):
        a = DOOR + u * 2 * math.pi
        da = abs(angdiff(a, DOOR))
        h = v * (H - 0.32)
        return da < 0.33 and h < 1.45 * max(0.0, 1 - (da / 0.33) ** 2) ** 0.35
    mk.sheet(c, "hide", cover, 96, 30, tex=1.4, skip=door)
    # door flap folded to the side
    def flap(u, v):
        a = DOOR - 0.36 - u * 0.5
        h = v * 1.45
        r = R * (1 - h / (H + 0.05)) + 0.06 + 0.04 * math.sin(u * math.pi)
        return (math.cos(a) * r, math.sin(a) * r, h + 0.02), (u * 1.2, h)
    mk.sheet(c, "hide", flap, 8, 10, tex=1.4)
    obj = c.build("cover")
    mk.solidify(obj, 0.018)
    mk.join_all("Tipi")
    mk.empty("Fwd", (0, -1, 0)); mk.empty("Up", (0, 0, 1))


# ------------------------------------------------------------------ mammoth bone hut
def mammoth_hut(_):
    R, H = 2.7, 2.45
    ribs = 10
    rnd = random.Random(8)
    b = mk.Builder()

    def dome_z(rho):
        return H * max(0.0, 1 - (rho / R) ** 2) ** 0.62

    c = mk.Builder()
    def cover(u, v):
        a = DOOR + u * 2 * math.pi
        rho = R * (1 - v) + 0.0001
        fr = min(abs(angdiff(a, DOOR + math.pi / ribs + 2 * math.pi * k / ribs)) for k in range(ribs)) / (math.pi / ribs)
        bulge = 0.05 * (1 - fr) ** 3 - 0.06 * fr ** 2 * (1 - v)
        n = 0.03 * mk.smooth_noise(a * 3 + v * 4, 1) + 0.012 * mk.smooth_noise(a * 13 + v * 11, 2)
        rr = rho + bulge + n
        z = dome_z(rho) + (bulge + n) * 0.6
        return (math.cos(a) * rr, math.sin(a) * rr, z), (u * 2 * math.pi * R * 0.75, v * H * 1.6)
    def door(u, v):
        a = DOOR + u * 2 * math.pi
        da = abs(angdiff(a, DOOR))
        rho = R * (1 - v)
        return da < 0.3 and dome_z(rho) < 1.5 * max(0.0, 1 - (da / 0.3) ** 2) ** 0.4
    mk.sheet(c, "hide", cover, 90, 26, tex=1.5, skip=door)
    obj = c.build("cover")
    mk.solidify(obj, 0.02)

    # mandibles stacked around the base, chin up, alternating lean
    n = 30
    for k in range(n):
        a = DOOR + 2 * math.pi * (k + 0.5) / n
        if abs(angdiff(a, DOOR)) < 0.34:
            continue
        out = Vector((math.cos(a), math.sin(a), 0))
        tang = Vector((-math.sin(a), math.cos(a), 0))
        base = out * (R + 0.12)
        lean = 0.25 if k % 2 else -0.25
        pts = []
        for i in range(9):
            t = i / 8
            w = math.sin(t * math.pi)
            pts.append(base + tang * (t - 0.5) * 0.55 + Vector((0, 0, 0.08 + 0.42 * w)) + out * (0.05 * w + lean * 0.1))
        mk.tube(b, "bone", pts, [0.06, 0.07, 0.08, 0.09, 0.1, 0.09, 0.08, 0.07, 0.06], segs=9, tex=0.6, rough=0.15, seed=k)
        mk.lumpy_sphere(b, "bone", pts[4] + Vector((0, 0, 0.05)), 0.12, (1.3, 1, 0.9), sub=2, amp=0.25, seed=k + 50, tex=0.6)
    # long bones standing between them
    for k in range(14):
        a = DOOR + 2 * math.pi * (k + 0.2) / 14
        if abs(angdiff(a, DOOR)) < 0.42:
            continue
        out = Vector((math.cos(a), math.sin(a), 0))
        p0 = out * (R + 0.2)
        p1 = out * (R - 0.15) + Vector((0, 0, 1.15))
        mk.tube(b, "bone", [p0.lerp(p1, i / 6) for i in range(7)], [0.07, 0.06, 0.05, 0.048, 0.05, 0.06, 0.07], segs=9,
                tex=0.6, rough=0.1, seed=k + 9)
        mk.lumpy_sphere(b, "bone", p0 + Vector((0, 0, 0.05)), 0.11, (1, 1, 0.8), sub=2, amp=0.25, seed=k + 80, tex=0.6)
        mk.lumpy_sphere(b, "bone", p1, 0.1, (1.2, 1, 0.8), sub=2, amp=0.25, seed=k + 90, tex=0.6)
    # tusk arch over the doorway
    for sgn in (1, -1):
        d = Vector((math.cos(DOOR), math.sin(DOOR), 0))
        side = Vector((-d.y, d.x, 0)) * sgn
        base = d * (R + 0.15) + side * 0.95
        pts = []
        for i in range(17):
            t = i / 16
            pts.append(base - side * (0.93 * t ** 1.6) + Vector((0, 0, 2.05 * math.sin(t * math.pi * 0.55))) + d * (0.25 * math.sin(t * math.pi)))
        mk.tube(b, "ivory", pts, [0.11 - 0.075 * i / 16 for i in range(17)], segs=12, tex=0.8, rough=0.03, seed=sgn + 3)
    lash(b, d * (R + 0.4) + Vector((0, 0, 1.97)), 0.07, 4, turns=5, axis=Vector((1, 0, 0.3)))
    # skull on the roof
    mk.lumpy_sphere(b, "bone", (0, 0.4, H + 0.05), 0.42, (1.1, 0.95, 0.75), sub=3, amp=0.22, seed=21, tex=0.7)
    for sgn in (1, -1):
        pts = [Vector((sgn * (0.18 + 0.5 * t), 0.15 - 0.6 * t, H - 0.1 + 0.25 * math.sin(t * 2.4))) for t in [i / 10 for i in range(11)]]
        mk.tube(b, "ivory", pts, [0.07 - 0.05 * i / 10 for i in range(11)], segs=9, tex=0.6)
    b.build("bones")
    mk.join_all("MammothHut")
    mk.empty("Fwd", (0, -1, 0)); mk.empty("Up", (0, 0, 1))


# ------------------------------------------------------------------ lean-to
def lean_to(_):
    b = mk.Builder()
    W, HR, D = 1.8, 1.75, 2.2
    for sgn in (1, -1):
        pole(b, (sgn * 1.55, -0.2, -0.1), (sgn * 1.55, -0.2, HR - 0.05), 0.065, 0.055, sgn + 2)
        for fs in (1, -1):
            pole(b, (sgn * 1.55, -0.2, HR - 0.15), (sgn * 1.55 + fs * 0.14, -0.2, HR + 0.18), 0.04, 0.03, sgn * 3 + fs, n=4, amp=0.01)
        lash(b, (sgn * 1.55, -0.2, HR - 0.05), 0.075, sgn + 7, turns=4, axis=Vector((0, 0, 1)))
    pole(b, (-2.05, -0.2, HR + 0.02), (2.05, -0.2, HR), 0.06, 0.05, 11, n=14)
    rnd = random.Random(2)
    for i in range(11):
        x = -W + 2 * W * i / 10
        pole(b, (x, -0.35, HR + 0.18), (x + rnd.uniform(-0.1, 0.1), D, -0.05), 0.04, 0.032, 20 + i)
    # bed of ferns / hide on the floor
    mk.sheet(b, "fur", lambda u, v: ((-0.9 + 1.8 * u, 0.3 + 1.2 * v, 0.03 + 0.025 * mk.smooth_noise(u * 5 + v * 3, 3)), (u * 1.8, v * 1.2)),
             10, 8, tex=1.2)
    for i in range(7):
        x = rnd.uniform(-1.5, 1.5)
        t0 = rnd.uniform(0.0, 0.5)
        p0 = Vector((x, -0.2 + (D + 0.2) * t0, HR * (1 - t0) + 0.25))
        p1 = p0 + Vector((rnd.uniform(-0.6, 0.6), 0.9, -0.75))
        pole(b, p0, p1, 0.025, 0.012, 40 + i, mat="bark", n=6)
    b.build("frame")
    c = mk.Builder()
    def roof(u, v):
        x = -W - 0.15 + (2 * W + 0.3) * u
        y = -0.42 + (D + 0.45) * v
        z = (HR + 0.25) * (1 - v) - 0.02 + 0.06 * mk.smooth_noise(x * 2 + v * 5, 7) + 0.03 * mk.smooth_noise(x * 9, 8)
        return (x, y, z), (x, math.hypot(y, z))
    mk.sheet(c, "thatch", roof, 40, 24, tex=1.3)
    mk.solidify(c.build("roof"), 0.12)
    mk.join_all("LeanTo")
    mk.empty("Fwd", (0, -1, 0)); mk.empty("Up", (0, 0, 1))


# ------------------------------------------------------------------ drying rack
def drying_rack(_):
    b = mk.Builder()
    for sgn in (1, -1):
        x = sgn * 1.25
        for fy in (1, -1):
            pole(b, (x, fy * 0.5, -0.05), (x, -fy * 0.06, 1.9), 0.04, 0.032, int(sgn * 5 + fy), n=8)
        lash(b, (x, 0, 1.78), 0.06, sgn + 3, turns=4, axis=Vector((0, 1, 0)))
    pole(b, (-1.45, 0, 1.79), (1.45, 0, 1.79), 0.04, 0.035, 9, n=12)
    # hide stretched in a stick frame
    fx, fz0, fz1 = 0.62, 0.35, 1.55
    for p0, p1 in (((-fx, -0.05, fz0), (fx, -0.05, fz0)), ((-fx, -0.05, fz1), (fx, -0.05, fz1)),
                   ((-fx, -0.05, fz0 - 0.08), (-fx, -0.05, fz1 + 0.12)), ((fx, -0.05, fz0 - 0.08), (fx, -0.05, fz1 + 0.12))):
        pole(b, p0, p1, 0.022, 0.02, int(p0[0] * 10 + p0[2] * 7), mat="wood_rough", n=5, amp=0.008)
    for sgn in (1, -1):
        mk.tube(b, "sinew", [Vector((sgn * fx, -0.05, fz1 + 0.1)), Vector((sgn * fx * 0.5, -0.02, 1.79))], 0.006, segs=4, tex=0.1)
    def hide_outline(u, v):
        x = (u - 0.5) * 2; z = (v - 0.5) * 2
        k = 0.82 + 0.1 * mk.smooth_noise(u * 4 + v * 3, 5)
        return x * fx * k * (1 - 0.18 * z * z), fz0 + (fz1 - fz0) * (0.5 + 0.5 * z * k * (1 - 0.15 * x * x))
    def stretched(u, v):
        x, z = hide_outline(u, v)
        return (x, -0.05 + 0.02 * mk.smooth_noise(u * 6 + v * 5, 9), z), (x, z)
    mk.sheet(b, "hide", stretched, 16, 18, tex=1.2)
    for i in range(12):
        t = i / 12
        if i % 3 == 0:
            u, v = t * 3 % 1, 0.0 if i < 6 else 1.0
        else:
            u, v = (0.0 if i % 2 else 1.0), t
        x, z = hide_outline(u, v)
        frame = Vector((max(-fx, min(fx, x * 1.25)), -0.05, max(fz0, min(fz1, (z - 0.95) * 1.3 + 0.95))))
        mk.tube(b, "sinew", [Vector((x, -0.05, z)), frame], 0.004, segs=4, tex=0.1)
    # pelts draped over the bar
    for k, x0 in enumerate((-0.95, 0.85)):
        def pelt(u, v, x0=x0, k=k):
            s = (v - 0.5) * 2.2
            w = 0.26 * (1 - 0.3 * abs(s)) * (1 + 0.15 * mk.smooth_noise(v * 6, k + 2))
            x = x0 + (u - 0.5) * 2 * w
            hang = max(0.0, abs(s) - 0.05)
            y = math.copysign(0.05 + 0.06 * math.sin(min(1, abs(s) * 8) * math.pi / 2), s) + 0.04 * mk.smooth_noise(u * 3 + v * 8, k)
            z = 1.84 - hang * 0.8 - 0.06 * min(1, abs(s) * 6) ** 2
            return (x, y, z), (x * 1.0, s)
        c = mk.Builder()
        mk.sheet(c, "fur", pelt, 10, 26, tex=1.0)
        mk.solidify(c.build("pelt%d" % k), 0.015)
    b.build("rack")
    mk.join_all("DryingRack")
    mk.empty("Fwd", (0, -1, 0)); mk.empty("Up", (0, 0, 1))


# ------------------------------------------------------------------ wood pile
def wood_pile(_):
    b = mk.Builder()
    rnd = random.Random(4)
    rows = [5, 4, 3, 2]
    for r, cnt in enumerate(rows):
        for i in range(cnt):
            rad = rnd.uniform(0.085, 0.11)
            y = (i - (cnt - 1) / 2) * 0.205
            z = 0.1 + r * 0.175
            x0, x1 = -0.6 + rnd.uniform(-0.08, 0.08), 0.6 + rnd.uniform(-0.08, 0.08)
            mk.tube(b, "pole_bark", [Vector((x0, y, z)), Vector((0, y + rnd.uniform(-0.01, 0.01), z)), Vector((x1, y, z))],
                    [rad, rad * 0.97, rad * 0.95], segs=12, tex=0.8, rough=0.08, seed=r * 10 + i, cap_mat="wood_fine")
    pole(b, (0.35, 0.7, 0.05), (0.85, 0.2, 0.06), 0.06, 0.05, 3)
    b.build("WoodPile")
    mk.empty("Fwd", (0, -1, 0)); mk.empty("Up", (0, 0, 1))


# ------------------------------------------------------------------ bone pile
def bone_pile(_):
    b = mk.Builder()
    rnd = random.Random(6)
    for k in range(6):
        a = rnd.uniform(0, 6.28)
        c = Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.5, 0.5), 0.07))
        d = Vector((math.cos(a), math.sin(a), 0)) * rnd.uniform(0.35, 0.55)
        p0, p1 = c - d, c + d
        mk.tube(b, "bone", [p0.lerp(p1, i / 6) + Vector((0, 0, 0.02 * math.sin(i))) for i in range(7)],
                [0.05, 0.04, 0.035, 0.033, 0.035, 0.04, 0.05], segs=8, tex=0.5, seed=k)
        mk.lumpy_sphere(b, "bone", p0, 0.07, (1.2, 1, 0.8), sub=2, amp=0.25, seed=k, tex=0.4)
        mk.lumpy_sphere(b, "bone", p1, 0.065, (1.2, 1, 0.8), sub=2, amp=0.25, seed=k + 20, tex=0.4)
    for k in range(5):
        x = -0.4 + k * 0.18
        pts = [Vector((x + 0.05 * math.sin(t * 3), -0.6 + 0.9 * t, 0.05 + 0.35 * math.sin(t * math.pi))) for t in [i / 10 for i in range(11)]]
        mk.tube(b, "bone", pts, 0.025, segs=6, tex=0.4)
    pts = [Vector((0.9 - 1.4 * t, 0.6 - 0.2 * t, 0.12 + 0.5 * math.sin(t * 2.2) * t)) for t in [i / 14 for i in range(15)]]
    mk.tube(b, "ivory", pts, [0.12 - 0.09 * i / 14 for i in range(15)], segs=12, tex=0.7)
    mk.lumpy_sphere(b, "bone", (-0.75, -0.2, 0.25), 0.33, (1.2, 0.9, 0.8), sub=3, amp=0.25, seed=33, tex=0.6)
    b.build("BonePile")
    mk.empty("Fwd", (0, -1, 0)); mk.empty("Up", (0, 0, 1))


MODELS = {"Tipi": tipi, "MammothHut": mammoth_hut, "LeanTo": lean_to, "DryingRack": drying_rack, "WoodPile": wood_pile,
          "BonePile": bone_pile}
os.makedirs(OUT, exist_ok=True)
for name, fn in MODELS.items():
    mk.reset()
    fn(None)
    print("MODEL", name, mk.tri_count(), "tris")
    mk.export(os.path.join(OUT, name + ".fbx"))
    if PREVIEW:
        import preview
        os.makedirs(PREVIEW, exist_ok=True)
        obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
        preview.single(obj, os.path.join(PREVIEW, name + ".png"), ROOT, view=(0.9, -1.4, 0.7), res=520)
