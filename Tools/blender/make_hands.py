"""First-person Neanderthal hand: a fist closed around a handle (axis +Z through the origin), forearm going down and
back with a fur bracer. Markers Fwd/Up like the weapons. -> Assets/Resources/Models/Weapons/hand.fbx
Run: blender -b -P Tools/blender/make_hands.py -- [preview.png]"""
import bpy, sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mathutils import Vector, Matrix
import meshkit as mk

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models", "Weapons")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = args[0] if args else None
RG = 0.021   # handle radius the fingers close around


def arc_point(ang_deg, r, z):
    a = math.radians(ang_deg)
    return Vector((math.cos(a) * r, math.sin(a) * r, z))


def hand(b):
    rnd = random.Random(3)
    # Four fingers: from the knuckles (upper right) over the top of the handle, round the left, back under it.
    for k, z in enumerate((0.03, 0.009, -0.012, -0.031)):
        fr = 0.0105 - 0.0007 * k
        pts = [arc_point(30, 0.05, z - 0.004), arc_point(55, 0.038, z)]
        for a in range(80, 255, 18):
            pts.append(arc_point(a, RG + fr * 0.95, z + 0.002 * math.sin(a * 0.05)))
        radii = [fr * 1.15] + [fr * (1.0 - 0.25 * i / (len(pts) - 1)) for i in range(1, len(pts))]
        mk.tube(b, "skin", pts, radii, segs=9, tex=0.05)
        # knuckle bump and a fingernail
        mk.lumpy_sphere(b, "skin", arc_point(42, 0.047, z), fr * 1.25, (1, 1, 1), sub=2, amp=0.08, seed=k, tex=0.05)
        mk.lumpy_sphere(b, "nail", arc_point(248, RG + fr * 1.55, z), fr * 0.62, (1.0, 0.45, 0.9), sub=1, amp=0.0, seed=k, tex=0.05)
    # Back of the hand / metacarpals: from the knuckles down to the wrist on the right side.
    mk.lumpy_sphere(b, "skin", (0.043, -0.004, -0.0), 0.034, (0.75, 1.45, 1.3), sub=3, amp=0.07, seed=7, tex=0.06)
    # Palm heel under the handle.
    mk.lumpy_sphere(b, "skin", (0.012, -0.038, -0.004), 0.03, (1.25, 0.85, 1.35), sub=3, amp=0.07, seed=8, tex=0.06)
    # Thumb: from the base of the palm round the left side, its tip pressing on the index finger.
    tpts = [Vector((0.02, -0.055, -0.03)), Vector((-0.012, -0.045, -0.035)), Vector((-0.033, -0.018, -0.02)),
            Vector((-0.036, 0.008, 0.0)), Vector((-0.026, 0.024, 0.018)), Vector((-0.012, 0.032, 0.031))]
    mk.tube(b, "skin", tpts, [0.017, 0.015, 0.0135, 0.012, 0.011, 0.0095], segs=10, tex=0.05)
    mk.lumpy_sphere(b, "nail", (-0.008, 0.042, 0.033), 0.0065, (1, 0.45, 1), sub=1, amp=0, seed=1, tex=0.05)
    # Wrist and forearm going down and back towards the camera, wrapped in a fur bracer.
    wrist = Vector((0.03, -0.075, -0.012))
    elbow = Vector((0.085, -0.36, -0.2))
    arm = [wrist.lerp(elbow, i / 10) for i in range(11)]
    mk.tube(b, "skin", arm, [0.031 + 0.017 * (i / 10) ** 0.8 for i in range(11)], segs=14, tex=0.12, rough=0.05, seed=2)
    fur = [wrist.lerp(elbow, 0.25 + 0.6 * i / 8) for i in range(9)]
    mk.tube(b, "fur", fur, [0.043 + 0.016 * i / 8 for i in range(9)], segs=16, tex=0.15, rough=0.18, seed=5)
    for t in (0.27, 0.82):
        c = wrist.lerp(elbow, t)
        d = (elbow - wrist).normalized()
        ring = []
        ref = d.cross(Vector((1, 0, 0))).normalized()
        ref2 = d.cross(ref)
        r = 0.047 + 0.016 * (t - 0.25) / 0.6
        for i in range(17):
            a = i / 16 * 2 * math.pi
            ring.append(c + (ref * math.cos(a) + ref2 * math.sin(a)) * r)
        mk.tube(b, "leather", ring, 0.0045, segs=5, tex=0.1, caps=False)


mk.reset()
b = mk.Builder()
hand(b)
obj = b.build("Hand")
mk.empty("Fwd", (0, 0, 1)); mk.empty("Up", (0, 1, 0))
print("MODEL hand", mk.tri_count(), "tris")
os.makedirs(OUT, exist_ok=True)
mk.export(os.path.join(OUT, "hand.fbx"))
if PREVIEW:
    import preview
    preview.TEX["skin"] = ("hide", (0.95, 0.68, 0.52))
    preview.TEX["nail"] = ("bone", (0.9, 0.75, 0.65))
    # a handle to show the grip
    hb = mk.Builder()
    mk.tube(hb, "wood_fine", [Vector((0, 0, -0.06)), Vector((0, 0, 0.12))], RG - 0.001, segs=12, tex=0.1)
    hb.build("Handle")
    preview.single(obj, PREVIEW, ROOT, view=(-0.35, 0.45, -1.0), res=520)
