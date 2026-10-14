"""
Builds the rigged Neanderthal for the game and exports Assets/Resources/Models/Neanderthal.fbx.

Run headless:
  blender -b --factory-startup -P Tools/blender/make_neanderthal.py -- <repo_root> [preview.png] [portrait.png]
          [--blend out.blend] [--check <dir>]

Body, hair and beard are metaball sculpts (muscles, heavy brow ridge, broad nose, long low skull)
converted to meshes; the pelt, skirt and rope belt are fitted to the body surface. Everything is
skinned to one armature and joined into one mesh with materials named Skin / Fur / Hair / Eyes / Rope
(CreatureBodies.NeanderthalModel swaps them for in-game materials by name).
--check renders front / head / pose-test images to verify shape and skinning.
"""
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Vector, Quaternion, Euler
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def take_flag(name):
    if name in argv:
        i = argv.index(name)
        value = argv[i + 1]
        del argv[i:i + 2]
        return value
    return None


BLEND = take_flag("--blend")
CHECK_DIR = take_flag("--check")
ROOT = argv[0] if argv else os.getcwd()
PREVIEW = argv[1] if len(argv) > 1 else None
PORTRAIT = argv[2] if len(argv) > 2 else None

random.seed(7)
VIS = 0.574  # visible surface radius / metaball radius at threshold 0.6, stiffness 2
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


# ---------------------------------------------------------------- utilities
def link(obj):
    scene.collection.objects.link(obj)
    return obj


def activate(obj):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def apply_modifiers(obj):
    for m in list(obj.modifiers):
        with bpy.context.temp_override(object=obj, active_object=obj):
            bpy.ops.object.modifier_apply(modifier=m.name)


def shade_smooth(obj):
    obj.data.polygons.foreach_set("use_smooth", [True] * len(obj.data.polygons))


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def decimate_to(obj, target_tris):
    tris = tri_count(obj)
    if tris > target_tris:
        obj.modifiers.new("Decimate", "DECIMATE").ratio = target_tris / tris
        apply_modifiers(obj)


def noise_displace(obj, scale, strength, name):
    tex = bpy.data.textures.new(name, "CLOUDS")
    tex.noise_scale = scale
    tex.noise_depth = 2
    m = obj.modifiers.new("Displace", "DISPLACE")
    m.texture = tex
    m.strength = strength
    m.texture_coords = "GLOBAL"


def metaball_to_mesh(mb_obj, name):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(mb_obj.evaluated_get(dg))
    me.name = name
    obj = link(bpy.data.objects.new(name, me))
    bpy.data.objects.remove(mb_obj)
    return obj


def mirror(fn):
    """Call fn(side) for the character's left (+X) and right (-X) side. The character faces -Y."""
    for side in (1, -1):
        fn(side)


# --------------------------------------------------------- metaball helpers
class MB:
    def __init__(self, name, resolution):
        self.data = bpy.data.metaballs.new(name)
        self.data.resolution = resolution
        self.data.render_resolution = resolution
        self.data.threshold = 0.6
        self.obj = link(bpy.data.objects.new(name, self.data))

    def ball(self, co, r, negative=False):
        e = self.data.elements.new(type="BALL")
        e.co = co
        e.radius = r / VIS
        e.use_negative = negative

    def ellipsoid(self, co, semi, rot=(0, 0, 0)):
        m = max(semi)
        e = self.data.elements.new(type="ELLIPSOID")
        e.co = co
        e.radius = m / VIS
        e.size_x, e.size_y, e.size_z = (s / m for s in semi)
        e.rotation = Euler([math.radians(a) for a in rot]).to_quaternion()

    def capsule(self, a, b, r):
        a, b = Vector(a), Vector(b)
        d = b - a
        e = self.data.elements.new(type="CAPSULE")
        e.co = (a + b) / 2
        e.radius = r / VIS
        e.size_x = d.length / 2
        e.rotation = Vector((1, 0, 0)).rotation_difference(d.normalized())

    def tapered(self, a, b, r0, r1, segments=3):
        """Chain of capsules approximating a cone from a (radius r0) to b (radius r1)."""
        a, b = Vector(a), Vector(b)
        for i in range(segments):
            t0, t1 = i / segments, (i + 1) / segments
            self.capsule(a.lerp(b, t0), a.lerp(b, t1), r0 + (r1 - r0) * (t0 + t1) / 2)


# ---------------------------------------------------------------- materials
def material(name, color, roughness, sss=0.0, bump=None, streaks=None):
    """Flat base colour (survives FBX export) plus procedural bump for Blender renders."""
    m = bpy.data.materials.new(name)
    if m.node_tree is None:  # Blender < 5 creates materials without nodes
        m.use_nodes = True
    m.diffuse_color = (*color, 1.0)
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    if sss:
        bsdf.inputs["Subsurface Weight"].default_value = sss
        bsdf.inputs["Subsurface Radius"].default_value = (0.9, 0.35, 0.2)
        bsdf.inputs["Subsurface Scale"].default_value = 0.01
    height = None
    if bump:
        noise = nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = bump[0]
        noise.inputs["Detail"].default_value = 8.0
        height = noise.outputs["Fac"]
    if streaks:
        wave = nt.nodes.new("ShaderNodeTexWave")
        wave.wave_type = "BANDS"
        wave.bands_direction = "X"
        wave.inputs["Scale"].default_value = streaks
        wave.inputs["Distortion"].default_value = 6.0
        wave.inputs["Detail"].default_value = 4.0
        if height is not None:
            add = nt.nodes.new("ShaderNodeMath")
            nt.links.new(height, add.inputs[0])
            nt.links.new(wave.outputs["Fac"], add.inputs[1])
            height = add.outputs[0]
        else:
            height = wave.outputs["Fac"]
    if height is not None:
        b = nt.nodes.new("ShaderNodeBump")
        b.inputs["Strength"].default_value = bump[1] if bump else 0.4
        b.inputs["Distance"].default_value = 0.002
        nt.links.new(height, b.inputs["Height"])
        nt.links.new(b.outputs["Normal"], bsdf.inputs["Normal"])
    return m


MATS = {
    "skin": material("Skin", (0.20, 0.105, 0.062), 0.5, sss=0.12, bump=(180, 0.12)),
    "hair": material("Hair", (0.024, 0.016, 0.011), 0.72, bump=(60, 0.3), streaks=90),
    "fur": material("Fur", (0.12, 0.075, 0.042), 0.92, bump=(70, 0.6), streaks=40),
    "rope": material("Rope", (0.17, 0.115, 0.06), 0.9, bump=(150, 0.5)),
    "eye": material("EyeWhite", (0.17, 0.12, 0.085), 0.2),
    "iris": material("EyeIris", (0.018, 0.011, 0.007), 0.08),
}


# ------------------------------------------------------------------- body
def build_body():
    mb = MB("BodyMB", 0.007)

    # torso: barrel chest, thick neck, heavy traps
    mb.ellipsoid((0, 0.01, 0.95), (0.16, 0.115, 0.10))                                   # pelvis
    mirror(lambda s: mb.ellipsoid((s * 0.075, 0.065, 0.90), (0.085, 0.075, 0.085)))      # glutes
    mb.ellipsoid((0, -0.005, 1.10), (0.15, 0.12, 0.12))                                  # belly
    mb.ellipsoid((0, 0.005, 1.29), (0.185, 0.13, 0.16))                                  # rib cage
    mirror(lambda s: mb.ellipsoid((s * 0.085, -0.075, 1.33), (0.085, 0.045, 0.065), (0, s * 10, 0)))  # pecs
    mb.ellipsoid((0, 0.06, 1.38), (0.14, 0.07, 0.09))                                    # upper back
    mirror(lambda s: mb.ellipsoid((s * 0.10, 0.025, 1.455), (0.085, 0.05, 0.035), (0, s * -18, 0)))  # traps
    mirror(lambda s: mb.ellipsoid((s * 0.12, -0.04, 1.14), (0.05, 0.06, 0.12)))          # obliques
    mb.capsule((0, 0.012, 1.42), (0, 0.0, 1.57), 0.058)                                  # neck

    # arms (T-pose along X, palms down, thumbs forward)
    def arm(s):
        mb.ellipsoid((s * 0.21, 0.0, 1.425), (0.085, 0.08, 0.078))                       # deltoid
        mb.tapered((s * 0.22, 0, 1.425), (s * 0.47, 0, 1.43), 0.058, 0.047)
        mb.ellipsoid((s * 0.34, -0.024, 1.414), (0.09, 0.05, 0.05))                      # biceps
        mb.ellipsoid((s * 0.33, 0.026, 1.43), (0.095, 0.048, 0.05))                      # triceps
        mb.tapered((s * 0.47, 0, 1.43), (s * 0.73, 0, 1.43), 0.048, 0.031)
        mb.ellipsoid((s * 0.545, 0.0, 1.436), (0.075, 0.052, 0.048))                     # forearm flexors
        mb.ellipsoid((s * 0.79, 0.0, 1.428), (0.055, 0.048, 0.021))                      # palm
        for i, y in enumerate((-0.03, -0.01, 0.011, 0.031)):
            length = (0.075, 0.082, 0.078, 0.065)[i]
            mb.tapered((s * 0.835, y, 1.428), (s * (0.835 + length), y + (i - 1.5) * 0.012, 1.42),
                       0.012, 0.009, 2)
        mb.tapered((s * 0.765, -0.038, 1.422), (s * 0.815, -0.09, 1.415), 0.014, 0.01, 2)  # thumb
    mirror(arm)

    # legs: short heavy shins, wide bare feet
    def leg(s):
        mb.tapered((s * 0.10, 0.0, 0.92), (s * 0.115, -0.01, 0.52), 0.095, 0.062)
        mb.ellipsoid((s * 0.11, -0.03, 0.72), (0.08, 0.075, 0.16))                       # quads
        mb.ellipsoid((s * 0.115, 0.03, 0.74), (0.075, 0.065, 0.14))                      # hamstrings
        mb.ball((s * 0.115, -0.022, 0.50), 0.056)                                        # knee
        mb.tapered((s * 0.115, -0.005, 0.48), (s * 0.12, 0.01, 0.10), 0.055, 0.037)
        mb.ellipsoid((s * 0.12, 0.035, 0.37), (0.062, 0.06, 0.11))                       # calf
        mb.ball((s * 0.12, 0.012, 0.085), 0.04)                                          # ankle
        mb.ellipsoid((s * 0.12, -0.045, 0.036), (0.054, 0.11, 0.034))                    # foot
        mb.ball((s * 0.12, 0.038, 0.042), 0.04)                                          # heel
        for i in range(5):                                                               # i = 0 big toe
            mb.ball((s * (0.095 + i * 0.015), -0.15 + i * 0.007, 0.019), 0.017 if i == 0 else 0.013)
    mirror(leg)

    # head: long low skull, heavy brow ridge, broad nose, no chin
    mb.ellipsoid((0, 0.03, 1.68), (0.083, 0.108, 0.088))                                 # cranium
    mb.ellipsoid((0, -0.035, 1.605), (0.068, 0.07, 0.072))                               # midface
    mb.ellipsoid((0, -0.045, 1.555), (0.062, 0.06, 0.032))                               # jaw
    mb.ellipsoid((0, -0.088, 1.69), (0.078, 0.028, 0.023))                               # brow ridge
    mb.ellipsoid((0, -0.104, 1.632), (0.022, 0.022, 0.034), (12, 0, 0))                  # nose bridge
    mb.ellipsoid((0, -0.118, 1.608), (0.025, 0.022, 0.018))                              # nose tip
    mirror(lambda s: mb.ball((s * 0.021, -0.108, 1.603), 0.014))                         # nostril wings
    mirror(lambda s: mb.ellipsoid((s * 0.052, -0.068, 1.635), (0.028, 0.022, 0.018)))    # cheekbones
    mirror(lambda s: mb.ellipsoid((s * 0.083, 0.012, 1.638), (0.01, 0.022, 0.032)))      # ears
    mb.ellipsoid((0, -0.108, 1.58), (0.03, 0.012, 0.01))                                 # upper lip
    mirror(lambda s: mb.ball((s * 0.034, -0.09, 1.658), 0.022, negative=True))            # eye sockets

    body = metaball_to_mesh(mb.obj, "Body")
    m = body.modifiers.new("Smooth", "CORRECTIVE_SMOOTH")
    m.iterations = 4
    m.use_only_smooth = True
    apply_modifiers(body)
    decimate_to(body, 15000)
    shade_smooth(body)
    body.data.materials.append(MATS["skin"])
    return body


def build_eyes():
    parts = []
    for s in (1, -1):
        c = Vector((s * 0.034, -0.072, 1.656))
        bpy.ops.mesh.primitive_uv_sphere_add(segments=14, ring_count=8, radius=0.0115, location=c)
        ball = bpy.context.active_object
        ball.data.materials.append(MATS["eye"])
        bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=0.005,
                                             location=c + Vector((0, -0.008, 0)))
        iris = bpy.context.active_object
        iris.data.materials.append(MATS["iris"])
        parts += [ball, iris]
    activate(parts[0])
    for o in parts:
        o.select_set(True)
    bpy.ops.object.join()
    eyes = bpy.context.active_object
    eyes.name = "Eyes"
    shade_smooth(eyes)
    return eyes


# ------------------------------------------------------------ hair & beard
def build_hair():
    mb = MB("HairMB", 0.0065)
    mb.ellipsoid((0, 0.045, 1.708), (0.09, 0.108, 0.088))                                # scalp cap (round crown)
    for i in range(10):                                                                  # swept back from hairline
        x = random.uniform(-0.06, 0.06)
        mb.tapered((x, -0.05, 1.745), (x * 1.3, 0.1, 1.75 + random.uniform(-0.015, 0.01)),
                   0.012, 0.016, 2)
    for i in range(70):
        phi = math.radians(random.uniform(62, 298))                                      # 0 = face
        side, back = math.sin(phi), -math.cos(phi)
        start = Vector((side * 0.088, 0.03 + back * 0.10, random.uniform(1.64, 1.73)))
        end_z = random.uniform(1.42, 1.50) if back < 0.0 else random.uniform(1.36, 1.45)
        mid = Vector((side * random.uniform(0.11, 0.13), start.y + back * 0.03, (start.z + end_z) / 2 + 0.03))
        end = Vector((side * random.uniform(0.10, 0.17), 0.04 + back * random.uniform(0.09, 0.13), end_z))
        r = random.uniform(0.016, 0.024)
        mb.capsule(start, mid, r)
        mb.capsule(mid, end, r * 0.6)
    hair = metaball_to_mesh(mb.obj, "Hair")
    noise_displace(hair, 0.012, 0.012, "HairNoise")
    apply_modifiers(hair)
    decimate_to(hair, 5500)
    shade_smooth(hair)
    hair.data.materials.append(MATS["hair"])
    return hair


def build_beard():
    mb = MB("BeardMB", 0.0055)
    mb.ellipsoid((0, -0.088, 1.525), (0.058, 0.04, 0.05))                                # chin mass
    for s in (1, -1):
        for i in range(9):                                                               # jaw line, ear -> chin
            t = i / 8
            p = Vector((s * 0.078 * (1 - t), 0.002 - 0.098 * t, 1.618 - 0.075 * t))
            p += Vector((s * 0.008, -0.004, 0)) * (1 - t)
            mb.ball(p, 0.024 + 0.006 * t)
        mb.ball((s * 0.058, -0.078, 1.585), 0.02)                                         # cheeks
        mb.tapered((s * 0.005, -0.117, 1.592), (s * 0.04, -0.106, 1.585), 0.011, 0.01, 2)  # moustache
        mb.tapered((s * 0.04, -0.106, 1.585), (s * 0.046, -0.1, 1.55), 0.01, 0.007, 2)
        mb.tapered((s * 0.012, -0.1, 1.7), (s * 0.062, -0.09, 1.703), 0.0075, 0.0055, 2)  # eyebrows
    for i in range(18):                                                                  # hanging locks
        x = random.uniform(-0.055, 0.055)
        top = Vector((x, -0.09 - random.uniform(0, 0.01), random.uniform(1.52, 1.56)))
        bottom = Vector((x * 1.2 + random.uniform(-0.01, 0.01), -0.1 - random.uniform(0, 0.015),
                         random.uniform(1.43, 1.47)))
        mb.tapered(top, bottom, 0.017, 0.006, 3)
    beard = metaball_to_mesh(mb.obj, "Beard")
    noise_displace(beard, 0.01, 0.008, "BeardNoise")
    apply_modifiers(beard)
    decimate_to(beard, 3500)
    shade_smooth(beard)
    beard.data.materials.append(MATS["hair"])
    return beard


# ------------------------------------------------------------------ clothes
def tube_mesh(name, rings, n_around, ring_fn):
    """Open tube of quads: ring_fn(row, angle) -> Vector."""
    bm = bmesh.new()
    rows = [[bm.verts.new(ring_fn(r, 2 * math.pi * k / n_around)) for k in range(n_around)]
            for r in range(rings)]
    for r in range(rings - 1):
        for k in range(n_around):
            k2 = (k + 1) % n_around
            bm.faces.new((rows[r][k], rows[r][k2], rows[r + 1][k2], rows[r + 1][k]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return link(bpy.data.objects.new(name, me))


def conform(obj, bvh, offset, mode, iterations=3):
    """'hug': snap onto the body surface + offset; 'push': only push out where closer than offset."""
    me = obj.data
    for it in range(iterations):
        for v in me.vertices:
            loc, normal, _, dist = bvh.find_nearest(v.co)
            if loc is None:
                continue
            if mode == "hug" or (v.co - loc).dot(normal) < 0 or dist < offset:
                v.co = loc + normal * offset
        if it < iterations - 1:
            bm = bmesh.new()
            bm.from_mesh(me)
            bmesh.ops.smooth_vert(bm, verts=[v for v in bm.verts if not v.is_boundary], factor=0.6,
                                  use_axis_x=True, use_axis_y=True, use_axis_z=True)
            bm.to_mesh(me)
            bm.free()


def finish_fur(obj, thickness, noise_strength, target_tris):
    noise_displace(obj, 0.025, noise_strength, obj.name + "Noise")
    m = obj.modifiers.new("Solidify", "SOLIDIFY")
    m.thickness = thickness
    m.offset = 1.0
    apply_modifiers(obj)
    decimate_to(obj, target_tris)
    shade_smooth(obj)
    obj.data.materials.append(MATS["fur"])


def build_pelt(bvh):
    """Diagonal pelt over the right shoulder, leaving the left pec bare."""
    rings, n, z0, z1 = 26, 60, 0.95, 1.53

    def ring(r, a):
        return Vector((math.sin(a) * 0.34, -math.cos(a) * 0.26, z0 + (z1 - z0) * r / (rings - 1)))

    obj = tube_mesh("Pelt", rings, n, ring)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    kill = []
    for v in bm.verts:
        x, z = v.co.x, v.co.z
        edge = 1.37 - 0.75 * x + 0.025 * math.sin(v.co.y * 60) + random.uniform(-0.012, 0.012)
        if z > edge or (abs(x) < 0.11 and z > 1.43) or (x > 0.0 and z > 1.42):
            kill.append(v)
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    for v in bm.verts:  # ragged top edge
        if v.is_boundary and v.co.z > z0 + 0.05:
            v.co.z -= random.uniform(0.0, 0.035)
    bm.to_mesh(obj.data)
    bm.free()
    conform(obj, bvh, 0.014, "hug")
    finish_fur(obj, 0.012, 0.01, 3000)
    return obj


def build_skirt(bvh):
    rings, n, z0, z1 = 16, 64, 1.04, 0.62
    hem = [0.04 * abs(math.sin(k * 0.95)) + 0.035 * abs(math.sin(k * 0.37 + 1)) + random.uniform(0, 0.03)
           for k in range(n)]

    def ring(r, a):
        t = r / (rings - 1)
        z = z0 + (z1 - z0) * t - hem[round(a / (2 * math.pi) * n) % n] * t ** 2
        rad = 0.19 + (z0 - z) * 0.22
        return Vector((math.sin(a) * rad, -math.cos(a) * rad * 0.82 + 0.01, z))

    obj = tube_mesh("Skirt", rings, n, ring)
    conform(obj, bvh, 0.016, "push")
    finish_fur(obj, 0.014, 0.012, 3500)
    return obj


def build_belt(skirt_bvh):
    pts = []
    for k in range(48):
        a = 2 * math.pi * k / 48
        loc, _, _, _ = skirt_bvh.find_nearest(Vector((math.sin(a) * 0.6, -math.cos(a) * 0.6, 1.0)))
        p = loc + Vector((loc.x, loc.y, 0)).normalized() * 0.012
        p.z = 1.0 + 0.012 * math.sin(a * 3)
        pts.append(p)
    cu = bpy.data.curves.new("Belt", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = 0.011
    cu.bevel_resolution = 1
    sp = cu.splines.new("POLY")
    sp.points.add(len(pts) - 1)
    for sp_point, p in zip(sp.points, pts):
        sp_point.co = (*p, 1.0)
    sp.use_cyclic_u = True
    obj = link(bpy.data.objects.new("Belt", cu))
    activate(obj)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.active_object
    # knot with two hanging ends at the front-left hip
    knot_pos = pts[6] + Vector((0, -0.006, 0))
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=0.022, location=knot_pos)
    knot = bpy.context.active_object
    knot.scale = (1.0, 0.7, 0.8)
    parts = [knot]
    for dx, length in ((-0.012, 0.13), (0.014, 0.10)):
        top = knot_pos + Vector((dx, -0.005, -0.01))
        bot = top + Vector((dx * 0.8, -0.01, -length))
        bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.008, depth=length, location=(top + bot) / 2)
        end = bpy.context.active_object
        end.rotation_euler = Vector((0, 0, 1)).rotation_difference((top - bot).normalized()).to_euler()
        parts.append(end)
    activate(obj)
    for p in parts:
        p.select_set(True)
    bpy.ops.object.join()
    obj = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    shade_smooth(obj)
    obj.data.materials.append(MATS["rope"])
    return obj


# ----------------------------------------------------------------- skeleton
# Names are what CreatureBodies.NeanderthalModel looks up. "Facing" marks where the face looks.
BONES = [
    ("Hips", (0, 0.0, 0.93), (0, 0.0, 1.06), None),
    ("Spine", (0, 0.0, 1.06), (0, 0.0, 1.26), "Hips"),
    ("Chest", (0, 0.0, 1.26), (0, 0.01, 1.44), "Spine"),
    ("Neck", (0, 0.01, 1.44), (0, 0.0, 1.56), "Chest"),
    ("Head", (0, 0.0, 1.56), (0, 0.0, 1.8), "Neck"),
    ("Facing", (0, -0.05, 1.66), (0, -0.2, 1.66), "Head"),
]
for s, sg in (("L", 1), ("R", -1)):
    BONES += [
        (f"Shoulder.{s}", (0.03 * sg, 0.0, 1.42), (0.17 * sg, 0.0, 1.43), "Chest"),
        (f"UpperArm.{s}", (0.17 * sg, 0.0, 1.43), (0.47 * sg, 0.0, 1.43), f"Shoulder.{s}"),
        (f"LowerArm.{s}", (0.47 * sg, 0.0, 1.43), (0.74 * sg, 0.0, 1.43), f"UpperArm.{s}"),
        (f"Hand.{s}", (0.74 * sg, 0.0, 1.43), (0.86 * sg, 0.0, 1.425), f"LowerArm.{s}"),
        (f"UpperLeg.{s}", (0.10 * sg, 0.0, 0.92), (0.115 * sg, -0.01, 0.50), "Hips"),
        (f"LowerLeg.{s}", (0.115 * sg, -0.01, 0.50), (0.12 * sg, 0.01, 0.09), f"UpperLeg.{s}"),
        (f"Foot.{s}", (0.12 * sg, 0.01, 0.09), (0.12 * sg, -0.10, 0.025), f"LowerLeg.{s}"),
        (f"Toes.{s}", (0.12 * sg, -0.10, 0.025), (0.12 * sg, -0.17, 0.02), f"Foot.{s}"),
    ]


def build_armature():
    arm = bpy.data.armatures.new("Rig")
    rig = link(bpy.data.objects.new("Rig", arm))
    activate(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, parent in BONES:
        b = arm.edit_bones.new(name)
        b.head, b.tail = head, tail
        if parent:
            b.parent = arm.edit_bones[parent]
            b.use_connect = (Vector(arm.edit_bones[parent].tail) - Vector(head)).length < 1e-4
        b.use_deform = name != "Facing"
    bpy.ops.object.mode_set(mode="OBJECT")
    arm.display_type = "STICK"
    rig.show_in_front = True
    return rig


def skin(rig, body, from_body, head_only):
    activate(body)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    assert len(body.vertex_groups) > 0, "automatic weights failed"

    for obj in from_body:  # clothes and hair copy weights from the nearest body surface
        for g in body.vertex_groups:
            obj.vertex_groups.new(name=g.name)
        dt = obj.modifiers.new("WeightTransfer", "DATA_TRANSFER")
        dt.object = body
        dt.use_vert_data = True
        dt.data_types_verts = {"VGROUP_WEIGHTS"}
        dt.vert_mapping = "POLYINTERP_NEAREST"
        apply_modifiers(obj)
        if obj.name == "Hair":  # long hair must not follow the arms
            for g in list(obj.vertex_groups):
                if "Arm" in g.name or "Hand" in g.name:
                    obj.vertex_groups.remove(g)

    for obj in head_only:
        obj.vertex_groups.new(name="Head").add(range(len(obj.data.vertices)), 1.0, "REPLACE")

    for obj in from_body + head_only:
        obj.parent = rig
        obj.modifiers.new("Armature", "ARMATURE").object = rig


# ------------------------------------------------------------------- render
def setup_render(samples, world_color, world_strength):
    scene.render.engine = "CYCLES"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "METAL"
        prefs.get_devices()
        for d in prefs.devices:
            d.use = True
        scene.cycles.device = "GPU"
    except Exception as e:
        print("GPU unavailable, rendering on CPU:", e)
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    if scene.world.node_tree is None:
        scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (*world_color, 1)
    bg.inputs["Strength"].default_value = world_strength


def clear_rig_and_lights():
    for o in [o for o in bpy.data.objects if o.type in ("CAMERA", "LIGHT") or o.name == "Floor"]:
        bpy.data.objects.remove(o)


def area_light(loc, power, size, color, target=(0, 0, 1.2)):
    li = bpy.data.lights.new("Light", "AREA")
    li.energy, li.size, li.color = power, size, color
    o = link(bpy.data.objects.new("Light", li))
    o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()


def studio():
    clear_rig_and_lights()
    setup_render(80, (0.30, 0.36, 0.40), 0.45)
    scene.render.film_transparent = False
    bpy.ops.mesh.primitive_plane_add(size=14)
    floor = bpy.context.active_object
    floor.name = "Floor"
    floor.data.materials.append(material("Stone", (0.2, 0.18, 0.15), 0.75, bump=(12, 0.3)))
    area_light((-2.2, -3.0, 3.2), 520, 2.0, (1.0, 0.92, 0.82))
    area_light((3.0, -2.5, 1.6), 140, 3.0, (0.75, 0.85, 1.0))
    area_light((0.6, 3.0, 2.8), 650, 1.5, (0.85, 0.92, 1.0))


def render(path, loc, target, lens, res):
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = lens
    cam = link(bpy.data.objects.new("Cam", cam_data))
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = path
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    print("RENDERED", path)


def rotate_world(rig, bone, axis, deg):
    pb = rig.pose.bones[bone]
    local_axis = (pb.bone.matrix_local.to_3x3().inverted() @ Vector(axis)).normalized()
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = Quaternion(local_axis, math.radians(deg)) @ pb.rotation_quaternion


def relaxed_pose(rig):
    rotate_world(rig, "UpperArm.L", (0, 1, 0), 68)
    rotate_world(rig, "UpperArm.R", (0, 1, 0), -68)
    rotate_world(rig, "LowerArm.L", (0, 0, 1), -12)
    rotate_world(rig, "LowerArm.R", (0, 0, 1), 12)


def rest_pose(rig):
    for pb in rig.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)


# --------------------------------------------------------------------- main
body = build_body()
body_bvh = BVHTree.FromObject(body, bpy.context.evaluated_depsgraph_get())
eyes = build_eyes()
hair = build_hair()
beard = build_beard()
pelt = build_pelt(body_bvh)
skirt = build_skirt(body_bvh)
belt = build_belt(BVHTree.FromObject(skirt, bpy.context.evaluated_depsgraph_get()))

rig = build_armature()
skin(rig, body, from_body=[hair, pelt, skirt, belt], head_only=[eyes, beard])

for o in (body, eyes, hair, beard, pelt, skirt, belt):
    print(f"  {o.name:8s} {tri_count(o):6d} tris")

# Join everything into one skinned mesh
activate(body)
for o in (eyes, hair, beard, pelt, skirt, belt):
    o.select_set(True)
bpy.ops.object.join()
body.name = "Neanderthal"
print("NEANDERTHAL_TRIS", tri_count(body), "VERTS", len(body.data.vertices))

# ------------------------------------------------------------------ export
out_dir = os.path.join(ROOT, "Assets", "Resources", "Models")
os.makedirs(out_dir, exist_ok=True)
activate(rig)
body.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, "Neanderthal.fbx"), use_selection=True,
                         object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         mesh_smooth_type='FACE', use_armature_deform_only=False)
print("EXPORTED", os.path.join(out_dir, "Neanderthal.fbx"))
if BLEND:
    os.makedirs(os.path.dirname(os.path.abspath(BLEND)), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(BLEND))
    print("SAVED", BLEND)

# ------------------------------------------------------------------ renders
if CHECK_DIR:
    studio()
    render(os.path.join(CHECK_DIR, "front.png"), (0, -6.0, 0.95), (0, 0, 0.92), 65, (1000, 1000))
    render(os.path.join(CHECK_DIR, "head.png"), (-0.32, -0.85, 1.68), (0, -0.02, 1.64), 85, (800, 800))
    rotate_world(rig, "UpperArm.L", (0, 1, 0), 62)
    rotate_world(rig, "UpperArm.R", (0, 1, 0), -62)
    rotate_world(rig, "LowerArm.R", (0, 0, 1), 60)
    rotate_world(rig, "UpperLeg.L", (1, 0, 0), -35)
    rotate_world(rig, "LowerLeg.L", (1, 0, 0), 40)
    rotate_world(rig, "Head", (0, 0, 1), 25)
    render(os.path.join(CHECK_DIR, "pose_test.png"), (-3.4, -4.6, 1.2), (0, 0, 0.9), 65, (1000, 1000))
    rest_pose(rig)

if PREVIEW:
    studio()
    relaxed_pose(rig)
    render(PREVIEW, (1.6, -3.1, 1.15), (0, 0, 0.9), 50, (720, 900))
    rest_pose(rig)

if PORTRAIT:  # HUD head shot on a transparent background
    clear_rig_and_lights()
    setup_render(64, (0.02, 0.022, 0.025), 1.0)
    scene.render.film_transparent = True
    target = (0, 0, 1.62)
    for loc, energy, col, size in (((1.3, -0.9, 2.0), 55, (1, 0.72, 0.45), 0.6),
                                   ((-1.3, -0.8, 1.7), 8, (0.5, 0.62, 1), 1.5),
                                   ((-0.6, 1.2, 2.1), 110, (0.75, 0.85, 1), 0.5),
                                   ((0.9, 1.0, 1.9), 60, (1, 0.6, 0.3), 0.4)):
        area_light(loc, energy, size, col, target)
    render(PORTRAIT, (0.38, -1.15, 1.66), target, 85, (512, 512))
