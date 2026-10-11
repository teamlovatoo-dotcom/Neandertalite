"""Stone-age weapon models (knapped flint/obsidian, wood, leather, sinew, bone) -> Assets/Resources/Models/Weapons.
Run: blender -b -P Tools/blender/make_weapons.py -- [preview.png]
Build space: Z = weapon length (or 'up' for bow/slingshot), markers 'Fwd' and 'Up' (1 m from origin) tell the game how
to orient each model, so the FBX axis conversion never matters. Grip sits at the origin."""
import bpy, sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mathutils import Vector, Matrix
import meshkit as mk

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models", "Weapons")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = args[0] if args else None

KNIFE = mk.normalized_shape(lambda v: (0.62 + 1.5 * v) * (1 - v) ** 0.85)
LEAF = mk.normalized_shape(lambda v: (0.45 + 2.2 * v) * (1 - v) ** 1.05)
def _axe(v):
    w = 0.62 + 0.38 * v
    if v > 0.82:
        w *= math.sqrt(max(0.0, 1 - ((v - 0.82) / 0.18) ** 2))
    return w
AXE = mk.normalized_shape(_axe)
ROT_Z90 = Matrix.Rotation(math.radians(90), 4, 'Z')


def markers(fwd=(0, 0, 1), up=(0, 1, 0), **extra):
    mk.empty("Fwd", fwd)
    mk.empty("Up", up)
    for k, v in extra.items():
        mk.empty(k, v)


def grip_wrap(b, r, z0, z1, seed):
    mk.helix(b, "leather", r, z0, z1, 0.0118, 0.0165, 0.0013, seed=seed, tex=0.12, per_turn=16)


def knife(b):
    mk.tube(b, "wood_fine", mk.wobble_path(-0.078, 0.036, 12, 0.0012, 3, 1), [0.0145 + 0.0015 * (1 - i / 12) for i in range(13)],
            segs=12, tex=0.12, rough=0.04, seed=1, ellipse=[(1, 0.82)] * 13)
    mk.lumpy_sphere(b, "wood_fine", (0, 0, -0.078), 0.0152, (1, 0.82, 0.55), sub=2, amp=0.08, seed=2, tex=0.1)
    grip_wrap(b, 0.0158, -0.064, 0.014, 0.3)
    mk.biface(b, "stone", 0.165, 0.021, 0.0055, KNIFE, thick_prof=lambda v: 1 - 0.35 * v, scars=34, seed=11,
              xf=Matrix.Translation((0, 0, 0.014)) @ ROT_Z90)
    mk.helix(b, "sinew", 0.0142, 0.02, 0.052, 0.0032, 0, 0.0011, seed=1, flat=False, wobble=0.0006, tex=0.05)
    markers(Tip=(0, 0, 0.179))


def bone_dagger(b):
    zs = [-0.088, -0.075, -0.055, -0.03, 0.0, 0.05, 0.1, 0.15, 0.2, 0.24, 0.27, 0.285]
    rs = [0.017, 0.024, 0.021, 0.0165, 0.015, 0.0145, 0.0135, 0.0115, 0.009, 0.006, 0.0035, 0.0012]
    el = [(1, 0.85)] * 5 + [(1, 0.8), (1, 0.72), (1, 0.62), (1, 0.52), (1, 0.45), (1, 0.4), (1, 0.4)]
    mk.tube(b, "bone", [Vector((0.002 * math.sin(z * 9), 0, z)) for z in zs], rs, segs=14, tex=0.2, rough=0.05, seed=3, ellipse=el)
    mk.lumpy_sphere(b, "bone", (0.009, 0, -0.084), 0.017, (1, 0.9, 0.8), sub=2, amp=0.12, seed=4, tex=0.15)
    mk.lumpy_sphere(b, "bone", (-0.01, 0.003, -0.082), 0.015, (1, 0.9, 0.8), sub=2, amp=0.12, seed=5, tex=0.15)
    grip_wrap(b, 0.0168, -0.05, 0.04, 1.2)
    mk.helix(b, "sinew", 0.0158, 0.043, 0.056, 0.003, 0, 0.0011, seed=2, flat=False, tex=0.05)
    markers(Tip=(0, 0, 0.285))


def stick(b):
    path = mk.wobble_path(-0.22, 0.86, 22, 0.01, 1.6, 7)
    mk.tube(b, "bark", path, [0.021 - 0.005 * i / 22 for i in range(23)], segs=12, tex=0.35, rough=0.07, seed=7)
    for z, ang, ln in ((0.18, 40, 0.035), (0.47, 200, 0.05), (0.7, 110, 0.03)):
        p0 = path[int((z + 0.22) / 1.08 * 22)]
        d = Vector((math.cos(math.radians(ang)), math.sin(math.radians(ang)), 0.9)).normalized()
        mk.tube(b, "bark", [p0, p0 + d * ln * 0.5, p0 + d * ln], [0.0085, 0.0075, 0.007], segs=7, tex=0.2)
    markers(Tip=(0, 0, 0.86))


def club(b):
    zs = [-0.14 + i * 0.026 for i in range(31)]
    def r(z):
        if z < 0.06: return 0.0205
        t = (z - 0.06) / 0.56
        return 0.0205 + 0.04 * t ** 1.3 - (0.03 * ((z - 0.58) / 0.04) ** 2 if z > 0.58 else 0)
    path = [Vector((0.006 * mk.smooth_noise(z * 2, 3), 0.006 * mk.smooth_noise(z * 2 + 9, 3), z)) for z in zs]
    mk.tube(b, "wood_rough", path, [max(0.012, r(z)) for z in zs], segs=16, tex=0.4, rough=0.22, seed=3)
    rnd = random.Random(5)
    for k in range(7):
        z = rnd.uniform(0.33, 0.58); a = rnd.uniform(0, 6.28)
        rr = r(z) * 0.85
        mk.lumpy_sphere(b, "wood_rough", (math.cos(a) * rr, math.sin(a) * rr, z), rnd.uniform(0.018, 0.028), (1, 1, 1.3), sub=2,
                        amp=0.25, seed=k, tex=0.3)
    for k, (z, a) in enumerate(((0.47, 0.4), (0.53, 2.5), (0.42, 4.4))):
        rr = r(z) * 0.75
        out = Vector((math.cos(a), math.sin(a), 0))
        # blade length along 'out', flat faces facing along the shaft
        m = Matrix((
            (-out.y, 0, out.x, 0),
            (out.x, 0, out.y, 0),
            (0, 1, 0, 0),
            (0, 0, 0, 1)))
        mk.biface(b, "stone", 0.045, 0.013, 0.004, LEAF, scars=10, seed=20 + k, nu=8, nv=12,
                  xf=Matrix.Translation(out * rr + Vector((0, 0, z))) @ m)
    grip_wrap(b, 0.0215, -0.12, 0.05, 2.1)
    markers(Tip=(0, 0, 0.63))


def axe(b):
    path = mk.wobble_path(-0.13, 0.5, 18, 0.004, 1.5, 4)
    mk.tube(b, "wood_rough", path, [0.0165 + 0.003 * i / 18 for i in range(19)], segs=12, tex=0.35, rough=0.06, seed=4)
    grip_wrap(b, 0.0172, -0.115, 0.04, 3.3)
    # head: length out along +Y (up), width along the haft (Z), thickness sideways (X)
    m = Matrix(((0, 1, 0, 0), (0, 0, 1, 0), (1, 0, 0, 0), (0, 0, 0, 1)))
    mk.biface(b, "stone", 0.135, 0.036, 0.0115, AXE, thick_prof=lambda v: 1 - 0.72 * v, scars=40, seed=31, nu=16, nv=34,
              xf=Matrix.Translation((0, -0.04, 0.425)) @ m)
    mk.helix(b, "sinew", 0.0205, 0.385, 0.403, 0.0028, 0, 0.0013, seed=4, flat=False, tex=0.05)
    mk.helix(b, "sinew", 0.0205, 0.447, 0.465, 0.0028, 0, 0.0013, seed=5, flat=False, tex=0.05)
    for k in range(5):
        tilt = math.radians(38 if k % 2 == 0 else -38)
        rot = Matrix.Rotation(tilt, 4, 'Y')
        pts = []
        for i in range(25):
            a = i / 24 * 2 * math.pi
            p = Vector((math.cos(a) * 0.0215, math.sin(a) * 0.0235, 0))
            pts.append(rot @ p + Vector((0, 0.003 * (k - 2) * 0.3, 0.425 + 0.004 * (k - 2))))
        mk.tube(b, "sinew", pts, 0.0014, segs=5, tex=0.05, caps=False)
    markers(Tip=(0, 0.095, 0.425))


def torch(b):
    path = mk.wobble_path(-0.13, 0.36, 14, 0.004, 2, 9)
    mk.tube(b, "bark", path, 0.0185, segs=12, tex=0.3, rough=0.08, seed=9)
    rnd = random.Random(9)
    for k in range(16):
        a = k / 16 * 6.283 + rnd.uniform(-0.1, 0.1)
        r0, r1 = 0.016, rnd.uniform(0.026, 0.036)
        z0, z1 = 0.3, rnd.uniform(0.47, 0.52)
        pts = [Vector((math.cos(a) * (r0 + (r1 - r0) * math.sin(t * 3.0) ), math.sin(a) * (r0 + (r1 - r0) * math.sin(t * 3.0)), z0 + (z1 - z0) * t))
               for t in [i / 6 for i in range(7)]]
        mk.tube(b, "char", pts, [0.006, 0.0075, 0.0075, 0.007, 0.006, 0.0045, 0.002], segs=6, tex=0.15)
    mk.lumpy_sphere(b, "char", (0, 0, 0.43), 0.03, (1, 1, 2.2), sub=2, amp=0.2, seed=3, tex=0.15)
    mk.helix(b, "leather", 0.0225, 0.315, 0.335, 0.0095, 0.011, 0.002, seed=1, tex=0.12)
    mk.helix(b, "leather", 0.034, 0.41, 0.425, 0.0095, 0.011, 0.002, seed=2, tex=0.12)
    markers(Flame=(0, 0, 0.5), Tip=(0, 0, 0.52))


def spear(b):
    zs = [-0.93 + i * 0.06 for i in range(31)] + [0.87]
    rs = []
    for z in zs:
        r = 0.0165 - 0.0018 * (z + 0.93) / 1.8
        if z < -0.86: r *= 0.55 + 0.45 * (z + 0.93) / 0.07
        rs.append(r)
    path = [Vector((0.004 * mk.smooth_noise(z * 1.3, 5), 0.004 * mk.smooth_noise(z * 1.3 + 4, 5), z)) for z in zs]
    path[-1] = Vector((path[-2].x, path[-2].y, 0.87))
    mk.tube(b, "wood_rough", path, rs, segs=12, tex=0.5, rough=0.04, seed=5)
    tip = path[-1]
    mk.biface(b, "stone", 0.215, 0.027, 0.0075, LEAF, thick_prof=lambda v: 1 - 0.3 * v, scars=40, seed=41,
              xf=Matrix.Translation((tip.x, tip.y, 0.845)) @ ROT_Z90)
    mk.helix(b, "sinew", 0.016, 0.83, 0.895, 0.003, 0, 0.0012, seed=3, flat=False, wobble=0.0007, tex=0.05,
             center=lambda z: Vector((tip.x, tip.y, 0)))
    # hide tassel tied under the binding
    for k, a in enumerate((0.3, 1.1)):
        top = Vector((tip.x + 0.017 * math.cos(a), tip.y + 0.017 * math.sin(a), 0.82))
        pts = [top + Vector((0.004 * k, 0.012 * math.sin(i * 0.7), -0.022 * i)) for i in range(7)]
        mk.tube(b, "leather", pts, [0.0022] * 7, segs=4, tex=0.1, side=lambda i: Vector((1, 0, 0)), ellipse=[(0.45, 1.6)] * 7)
    markers(Tip=(tip.x, tip.y, 1.06))


def bow_limb_point(s):
    a = abs(s)
    y = -0.14 * (a / 0.6) ** 1.8 + 0.03 * max(0.0, (a - 0.5) / 0.12) ** 2
    return Vector((0, y, s * 0.92))


def bow(b):
    n = 48
    ss = [-0.62 + 1.24 * i / n for i in range(n + 1)]
    path = [bow_limb_point(s) for s in ss]
    def half_w(s):
        a = abs(s)
        return 0.017 if a < 0.08 else 0.017 - 0.0105 * ((a - 0.08) / 0.54) ** 0.9
    def half_t(s):
        a = abs(s)
        return 0.0125 if a < 0.08 else 0.0125 - 0.0065 * ((a - 0.08) / 0.54)
    el = [(half_w(s), half_t(s)) for s in ss]
    mk.tube(b, "wood_fine", path, [1.0] * len(path), segs=14, tex=0.45, side=lambda i: Vector((1, 0, 0)), ellipse=el)
    gi = [i for i, s in enumerate(ss) if abs(s) <= 0.085]
    mk.tube(b, "leather", [path[i] for i in gi], [1.0] * len(gi), segs=14, tex=0.12, side=lambda i: Vector((1, 0, 0)),
            ellipse=[(el[i][0] * 1.13, el[i][1] * 1.2) for i in gi])
    for sgn in (1, -1):
        sn = [sgn * (0.55 + 0.035 * k / 4) for k in range(5)]
        mk.tube(b, "sinew", [bow_limb_point(s) for s in sn], [1.0] * 5, segs=10, tex=0.05, side=lambda i: Vector((1, 0, 0)),
                ellipse=[(half_w(s) * 1.3, half_t(s) * 1.45) for s in sn])
    a, c = bow_limb_point(0.6), bow_limb_point(-0.6)
    markers(fwd=(0, 1, 0), up=(0, 0, 1), NockA=tuple(a), NockB=tuple(c))


def slingshot(b):
    mk.tube(b, "wood_fine", mk.wobble_path(-0.115, 0.045, 10, 0.0015, 3, 2), [0.0172 - 0.0012 * i / 10 for i in range(11)],
            segs=12, tex=0.15, rough=0.06, seed=2)
    mk.lumpy_sphere(b, "wood_fine", (0, 0, -0.115), 0.0168, (1, 1, 0.6), sub=2, amp=0.06, seed=1, tex=0.1)
    for sgn in (1, -1):
        pts = []
        for i in range(13):
            t = i / 12
            p0, p1, p2 = Vector((0, 0, 0.035)), Vector((sgn * 0.006, 0, 0.09)), Vector((sgn * 0.05, 0.004, 0.158))
            pts.append((1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2)
        mk.tube(b, "wood_fine", pts, [0.0145 - 0.004 * i / 12 for i in range(13)], segs=10, tex=0.15, rough=0.05, seed=sgn + 4)
        mk.helix(b, "sinew", 0.0118, 0.0, 0.016, 0.0028, 0, 0.0011, seed=sgn, flat=False, tex=0.05,
                 center=lambda z, sgn=sgn: Vector((sgn * 0.0485, 0.004, 0.13)))
    mk.lumpy_sphere(b, "wood_fine", (0, 0, 0.047), 0.0175, (1.35, 0.9, 1.1), sub=2, amp=0.08, seed=6, tex=0.1)
    grip_wrap(b, 0.0175, -0.1, 0.025, 0.7)
    markers(fwd=(0, 1, 0), up=(0, 0, 1), NockA=(0.0485, 0.004, 0.137), NockB=(-0.0485, 0.004, 0.137))


def arrow(b):
    mk.tube(b, "wood_fine", [Vector((0, 0, -0.75)), Vector((0, 0, -0.38)), Vector((0, 0, -0.004))], 0.0042, segs=7, tex=0.4)
    mk.biface(b, "stone", 0.07, 0.0095, 0.0028, LEAF, scars=12, seed=51, nu=8, nv=14, xf=Matrix.Translation((0, 0, -0.012)))
    mk.helix(b, "sinew", 0.0049, -0.024, 0.004, 0.0024, 0, 0.0008, seed=1, flat=False, tex=0.03, per_turn=7)
    mk.helix(b, "sinew", 0.0049, -0.748, -0.733, 0.0024, 0, 0.0008, seed=2, flat=False, tex=0.03, per_turn=7)
    for k in range(3):
        a = k * 2 * math.pi / 3 + 0.3
        rad = Vector((math.cos(a), math.sin(a), 0))
        verts, faces, uvs = [], [], []
        n = 10
        for i in range(n + 1):
            t = i / n
            z = -0.725 + 0.13 * t
            h = 0.015 * (math.sin(math.pi * min(1, t * 1.25)) ** 0.6) * (1 - 0.35 * t)
            verts.append(rad * 0.0043 + Vector((0, 0, z)))
            verts.append(rad * (0.0043 + h) + Vector((0, 0, z - 0.01)))
        for i in range(n):
            f = [2 * i, 2 * i + 2, 2 * i + 3, 2 * i + 1]
            for ff in (f, f[::-1]):
                faces.append(ff)
                uvs.append([((verts[q].z + 0.725) / 0.13, (verts[q] - Vector((0, 0, verts[q].z))).length / 0.02) for q in ff])
        b.add("feather", verts, faces, uvs)
    markers(Tip=(0, 0, 0.058))


def stone(b):
    mk.lumpy_sphere(b, "pebble", (0, 0, 0), 0.033, (1.18, 0.86, 1.0), sub=3, amp=0.1, seed=12, tex=0.12)
    markers()


MODELS = {"knife": knife, "bone_dagger": bone_dagger, "stick": stick, "club": club, "axe": axe, "torch": torch,
          "spear": spear, "bow": bow, "slingshot": slingshot, "arrow": arrow, "stone": stone}

os.makedirs(OUT, exist_ok=True)
for name, fn in MODELS.items():
    mk.reset()
    b = mk.Builder()
    fn(b)
    b.build(name)
    print("MODEL", name, mk.tri_count(), "tris")
    mk.export(os.path.join(OUT, name + ".fbx"))

if PREVIEW:
    import preview
    preview.weapons_sheet(MODELS, PREVIEW, ROOT)
