"""
Builds a stylized, rigged Neanderthal for the game and exports Assets/Resources/Models/Neanderthal.fbx.
Run headless:  blender -b -P Tools/blender/make_neanderthal.py -- <repo_root> [preview.png]
Body = Skin modifier over a stocky skeleton + subdivision; fur tunic, hair and beard are separate
parts joined into one skinned mesh with materials named Skin / Fur / Hair / Eyes.
"""
import bpy, bmesh, math, random, sys, os
from mathutils import Vector, noise

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = argv[0] if argv else os.getcwd()
PREVIEW = argv[1] if len(argv) > 1 else None
PORTRAIT = argv[2] if len(argv) > 2 else None   # optional: square head shot for the HUD
random.seed(7)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

def mat(name, color, rough=0.8):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*color, 1)
    return m

M_SKIN = mat("Skin", (0.2, 0.1, 0.055), 0.6)
M_FUR = mat("Fur", (0.085, 0.05, 0.026), 0.95)
M_HAIR = mat("Hair", (0.022, 0.013, 0.008), 0.85)
M_EYES = mat("Eyes", (0.02, 0.015, 0.01), 0.2)

def link(obj):
    bpy.context.collection.objects.link(obj)
    return obj

def apply_all(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    obj.modifiers.clear()
    old = obj.data
    obj.data = me
    bpy.data.meshes.remove(old)

# ------------------------------------------------------------------ body (skin modifier)
# (name, position, radius_x, radius_y) - character faces -Y, left side is +X.
P = {}
def node(name, x, y, z, rx, ry=None):
    P[name] = (Vector((x, y, z)), rx, ry if ry is not None else rx)

node("pelvis", 0, 0.0, 0.95, 0.18, 0.14)
node("belly", 0, -0.015, 1.08, 0.2, 0.17)
node("chest", 0, 0.0, 1.26, 0.26, 0.19)
node("upchest", 0, 0.01, 1.39, 0.24, 0.16)
node("neck", 0, 0.015, 1.5, 0.095, 0.09)
node("neck2", 0, 0.02, 1.56, 0.085, 0.085)
for s, sg in (("L", 1), ("R", -1)):
    node("trap" + s, 0.12 * sg, 0.02, 1.47, 0.08, 0.07)
    node("shoulder" + s, 0.23 * sg, 0.01, 1.42, 0.12, 0.11)
    node("bicep" + s, 0.37 * sg, 0.0, 1.42, 0.1, 0.095)
    node("elbow" + s, 0.53 * sg, 0.0, 1.42, 0.07)
    node("forearm" + s, 0.63 * sg, 0.0, 1.42, 0.085, 0.075)
    node("wrist" + s, 0.77 * sg, 0.0, 1.42, 0.055, 0.045)
    node("hand" + s, 0.86 * sg, -0.005, 1.415, 0.065, 0.035)
    node("fingers" + s, 0.95 * sg, -0.005, 1.41, 0.055, 0.028)
    node("hip" + s, 0.12 * sg, 0.0, 0.88, 0.13, 0.13)
    node("thigh" + s, 0.13 * sg, 0.0, 0.7, 0.13, 0.125)
    node("knee" + s, 0.135 * sg, -0.01, 0.5, 0.08)
    node("calf" + s, 0.135 * sg, 0.015, 0.33, 0.095, 0.09)
    node("ankle" + s, 0.135 * sg, 0.0, 0.09, 0.058)
    node("heel" + s, 0.135 * sg, 0.025, 0.04, 0.05)
    node("toe" + s, 0.135 * sg, -0.14, 0.035, 0.06, 0.038)

E = [("pelvis", "belly"), ("belly", "chest"), ("chest", "upchest"), ("upchest", "neck"), ("neck", "neck2")]
for s in ("L", "R"):
    E += [("upchest", "trap" + s), ("trap" + s, "shoulder" + s), ("shoulder" + s, "bicep" + s), ("bicep" + s, "elbow" + s),
          ("elbow" + s, "forearm" + s), ("forearm" + s, "wrist" + s), ("wrist" + s, "hand" + s),
          ("hand" + s, "fingers" + s), ("pelvis", "hip" + s), ("hip" + s, "thigh" + s), ("thigh" + s, "knee" + s),
          ("knee" + s, "calf" + s), ("calf" + s, "ankle" + s), ("ankle" + s, "heel" + s), ("ankle" + s, "toe" + s)]

names = list(P.keys())
me = bpy.data.meshes.new("BodySkel")
me.from_pydata([P[n][0] for n in names], [(names.index(a), names.index(b)) for a, b in E], [])
body = link(bpy.data.objects.new("Body", me))
skin = body.modifiers.new("Skin", "SKIN")
for i, n in enumerate(names):
    sv = me.skin_vertices[0].data[i]
    sv.radius = (P[n][1], P[n][2])
me.skin_vertices[0].data[names.index("pelvis")].use_root = True
body.modifiers.new("Sub", "SUBSURF").levels = 2
apply_all(body)
# Organic surface noise
for v in body.data.vertices:
    n = noise.noise(v.co * 14.0) * 0.004
    v.co += v.normal * n
body.data.materials.append(M_SKIN)
for p in body.data.polygons:
    p.use_smooth = True

# ------------------------------------------------------------------ head (deformed sphere, Neanderthal skull)
HC = Vector((0, 0.015, 1.67))
bpy.ops.mesh.primitive_uv_sphere_add(radius=1.0, location=(0, 0, 0), segments=40, ring_count=28)
head = bpy.context.active_object
head.name = "Head"
def g(d, w):
    return math.exp(-(d * d) / (w * w))
for v in head.data.vertices:
    x, y, z = v.co
    # base: long low skull, wide face
    px, py, pz = x * 0.122, y * 0.142, z * 0.138
    if py > 0: py *= 1.18                       # occipital bun
    if pz > 0: pz *= 0.9                        # low, flat crown
    front = max(0.0, -y)                        # 0 back .. 1 face
    # brow ridge: a continuous bar above the eyes
    pz_n = z
    py -= 0.045 * g(pz_n - 0.3, 0.13) * front ** 3
    # eye sockets
    for ex in (0.36, -0.36):
        py += 0.018 * g(x - ex, 0.17) * g(pz_n - 0.12, 0.12) * front ** 4
    # big broad nose
    py -= 0.05 * g(x, 0.16) * g(pz_n + 0.1, 0.16) * front ** 6
    px *= 1 + 0.25 * g(pz_n + 0.12, 0.14) * front ** 4 * g(x, 0.3)
    # projecting mid-face, receding chin
    py -= 0.018 * g(pz_n + 0.3, 0.25) * front ** 2
    py += 0.02 * g(pz_n + 0.75, 0.2) * front ** 2
    # cheekbones wide
    px *= 1 + 0.08 * g(pz_n + 0.05, 0.25)
    v.co = Vector((px, py, pz)) + HC
for p in head.data.polygons:
    p.use_smooth = True
head.data.materials.append(M_SKIN)
for v in head.data.vertices:
    v.co += v.normal * noise.noise(v.co * 30.0) * 0.002
# Ears
for sx in (1, -1):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1.0, location=(0.118 * sx, 0.025, 1.66), segments=12, ring_count=8)
    ear = bpy.context.active_object
    ear.scale = (0.012, 0.03, 0.04)
    bpy.ops.object.transform_apply(scale=True)
    ear.data.materials.append(M_SKIN)
    ear.name = "Ear"

# Eyes
for sx in (0.036, -0.036):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.015, location=(sx * 1.12, -0.113, 1.692), segments=12, ring_count=8)
    e = bpy.context.active_object
    e.name = "Eye"
    e.data.materials.append(M_EYES)

# ------------------------------------------------------------------ fur tunic
def ring_band(name, z0, z1, rings, seg, radius_fn, jag_bottom=0.0, jag_top=0.0, angle_mask=None):
    bm = bmesh.new()
    rows = []
    for r in range(rings + 1):
        t = r / rings
        z = z0 + (z1 - z0) * t
        row = []
        for s in range(seg):
            a = 2 * math.pi * s / seg
            rx, ry = radius_fn(t)
            jz = 0.0
            if r == 0 and jag_bottom: jz = random.uniform(-jag_bottom, jag_bottom * 0.3)
            if r == rings and jag_top: jz = random.uniform(-jag_top * 0.3, jag_top)
            row.append(bm.verts.new((math.cos(a) * rx, math.sin(a) * ry, z + jz)))
        rows.append(row)
    for r in range(rings):
        for s in range(seg):
            if angle_mask and not angle_mask(2 * math.pi * (s + 0.5) / seg): continue
            bm.faces.new((rows[r][s], rows[r][(s + 1) % seg], rows[r + 1][(s + 1) % seg], rows[r + 1][s]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = link(bpy.data.objects.new(name, me))
    obj.data.materials.append(M_FUR)
    sol = obj.modifiers.new("Solid", "SOLIDIFY")
    sol.thickness = 0.018
    sol.offset = 1
    obj.modifiers.new("Sub", "SUBSURF").levels = 1
    apply_all(obj)
    for v in obj.data.vertices:
        v.co += v.normal * noise.noise(v.co * 22.0) * 0.012
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj

skirt = ring_band("Skirt", 0.58, 1.06, 6, 32,
                  lambda t: (0.3 - 0.08 * t, 0.25 - 0.06 * t), jag_bottom=0.09)
# Diagonal fur wrap: over the left shoulder, across the chest to the right hip, ragged edges.
bm = bmesh.new()
seg = 36
tilt = math.radians(38)
rows = []
for r, off in enumerate((-0.12, -0.04, 0.04, 0.12)):
    row = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        # ellipse in a plane tilted about Y (left shoulder high, right hip low)
        lx, ly = math.cos(a) * 0.34, math.sin(a) * 0.24
        x = lx * math.cos(tilt)
        z = 1.2 + lx * math.sin(tilt) + off
        if r in (0, 3): z += random.uniform(-0.035, 0.035)
        row.append(bm.verts.new((x, ly, z)))
    rows.append(row)
for r in range(3):
    for i in range(seg):
        bm.faces.new((rows[r][i], rows[r][(i + 1) % seg], rows[r + 1][(i + 1) % seg], rows[r + 1][i]))
me = bpy.data.meshes.new("Wrap"); bm.to_mesh(me); bm.free()
vest = link(bpy.data.objects.new("Wrap", me))
vest.data.materials.append(M_FUR)
vest.modifiers.new("Sub", "SUBSURF").levels = 2
sw = vest.modifiers.new("Wrap", "SHRINKWRAP"); sw.target = body; sw.offset = 0.022; sw.wrap_method = 'NEAREST_SURFACEPOINT'
sol = vest.modifiers.new("Solid", "SOLIDIFY"); sol.thickness = 0.02; sol.offset = 1
apply_all(vest)
for v in vest.data.vertices:
    v.co += v.normal * noise.noise(v.co * 22.0) * 0.01
for p in vest.data.polygons: p.use_smooth = True
strap = vest
# Rope belt
bpy.ops.mesh.primitive_torus_add(major_radius=0.205, minor_radius=0.014, location=(0, 0, 1.0))
belt = bpy.context.active_object; belt.name = "Belt"; belt.scale = (1.0, 0.82, 1.0)
bpy.ops.object.transform_apply(scale=True)
belt.data.materials.append(M_SKIN if False else mat("Rope", (0.42, 0.32, 0.18), 0.9))

# ------------------------------------------------------------------ hair and beard (metaballs)
hair_parts = []
def blob(x, y, z, r, sx=1, sy=1, sz=1):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1.0, location=(x, y, z), segments=16, ring_count=10)
    o = bpy.context.active_object
    o.scale = (r * sx, r * sy, r * sz)
    hair_parts.append(o)
S = 1.12  # head scale relative to the first draft
def hb(x, y, z, r, sx=1, sy=1, sz=1):
    blob(x * S, (y - 0.0) * S + 0.005, 1.67 + (z - 1.65) * S, r * S, sx, sy, sz)
hb(0, 0.03, 1.73, 0.13, 1.0, 1.15, 0.62)            # skull cap
hb(0, 0.09, 1.67, 0.12, 1.0, 0.9, 0.9)              # back of the head
for i in range(11):                                 # long mane down to the shoulders
    a = math.radians(-125 + i * 25)
    hb(math.sin(a) * 0.11, 0.05 + math.cos(a) * 0.07, 1.55 - 0.012 * abs(i - 5), 0.062, 0.75, 0.75, 1.9)
for i in range(9):
    hb(random.uniform(-0.14, 0.14), random.uniform(0.06, 0.12), random.uniform(1.4, 1.52), 0.055, 0.9, 0.7, 1.6)
hb(0.07, -0.05, 1.71, 0.04, 1.2, 1.0, 0.7)          # fringe locks
hb(-0.07, -0.05, 1.71, 0.04, 1.2, 1.0, 0.7)
hb(0, -0.095, 1.535, 0.07, 1.25, 0.85, 1.25)        # full beard
hb(0.065, -0.07, 1.575, 0.05, 1, 1, 1.5)
hb(-0.065, -0.07, 1.575, 0.05, 1, 1, 1.5)
hb(0, -0.1, 1.47, 0.055, 1.15, 0.75, 1.3)
hb(0.085, -0.035, 1.62, 0.03, 0.9, 1.4, 1.7)        # sideburns
hb(-0.085, -0.035, 1.62, 0.03, 0.9, 1.4, 1.7)
hb(0, -0.135, 1.6, 0.026, 1.9, 0.7, 0.55)           # moustache
bpy.ops.object.select_all(action='DESELECT')
for o in hair_parts: o.select_set(True)
bpy.context.view_layer.objects.active = hair_parts[0]
bpy.ops.object.transform_apply(scale=True)
bpy.ops.object.join()
hair = bpy.context.active_object
hair.name = "Hair"
rm = hair.modifiers.new("Remesh", "REMESH"); rm.mode = 'VOXEL'; rm.voxel_size = 0.009
hair.modifiers.new("Smooth", "SMOOTH").iterations = 6
apply_all(hair)
dec = hair.modifiers.new("Dec", "DECIMATE"); dec.ratio = 0.35
apply_all(hair)
for v in hair.data.vertices:
    v.co += v.normal * (noise.noise(v.co * 40.0) * 0.01 + noise.noise(Vector((v.co.x * 90, v.co.y * 90, v.co.z * 12))) * 0.007)
hair.data.materials.append(M_HAIR)
for p in hair.data.polygons: p.use_smooth = True

# ------------------------------------------------------------------ armature
arm_data = bpy.data.armatures.new("Rig")
rig = link(bpy.data.objects.new("Rig", arm_data))
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
def bone(name, head, tail, parent=None):
    b = eb.new(name); b.head = head; b.tail = tail
    if parent: b.parent = eb[parent]; b.use_connect = False
    return b
bone("Hips", (0, 0, 0.92), (0, 0, 1.08))
bone("Spine", (0, 0, 1.08), (0, 0, 1.26), "Hips")
bone("Chest", (0, 0, 1.26), (0, 0, 1.46), "Spine")
bone("Neck", (0, 0.01, 1.46), (0, 0.01, 1.56), "Chest")
bone("Head", (0, 0.01, 1.56), (0, 0.01, 1.8), "Neck")
bone("Facing", (0, -0.05, 1.66), (0, -0.2, 1.66), "Head")   # marker: points where the face looks
for s, sg in (("L", 1), ("R", -1)):
    bone("Shoulder." + s, (0.05 * sg, 0, 1.43), (0.21 * sg, 0, 1.42), "Chest")
    bone("UpperArm." + s, (0.21 * sg, 0, 1.42), (0.53 * sg, 0, 1.42), "Shoulder." + s)
    bone("LowerArm." + s, (0.53 * sg, 0, 1.42), (0.77 * sg, 0, 1.42), "UpperArm." + s)
    bone("Hand." + s, (0.77 * sg, 0, 1.42), (0.97 * sg, 0, 1.41), "LowerArm." + s)
    bone("UpperLeg." + s, (0.11 * sg, 0, 0.9), (0.13 * sg, 0, 0.5), "Hips")
    bone("LowerLeg." + s, (0.13 * sg, 0, 0.5), (0.13 * sg, 0, 0.09), "UpperLeg." + s)
    bone("Foot." + s, (0.13 * sg, 0, 0.09), (0.13 * sg, -0.13, 0.03), "LowerLeg." + s)
bpy.ops.object.mode_set(mode='OBJECT')

def select_only(objs, active):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = active

# Automatic weights for body and clothes
for obj in (body, skirt, vest, belt):
    select_only([obj, rig], rig)
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
# Rigid parts follow the head
for obj in [o for o in bpy.data.objects if o.name.startswith(("Eye", "Ear"))] + [hair, head]:
    vg = obj.vertex_groups.new(name="Head")
    vg.add([v.index for v in obj.data.vertices], 1.0, 'REPLACE')
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig

# Join everything into one skinned mesh
parts = [o for o in bpy.data.objects if o.type == 'MESH']
select_only(parts, body)
bpy.ops.object.join()
body.name = "Neanderthal"
tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
print("NEANDERTHAL_TRIS", tris, "VERTS", len(body.data.vertices))

# ------------------------------------------------------------------ export
out_dir = os.path.join(ROOT, "Assets", "Resources", "Models")
os.makedirs(out_dir, exist_ok=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, "Neanderthal.fbx"), use_selection=False,
                         object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         mesh_smooth_type='FACE', use_armature_deform_only=False)
print("EXPORTED", os.path.join(out_dir, "Neanderthal.fbx"))

# ------------------------------------------------------------------ preview render
if PREVIEW:
    # Pose for the preview: arms down a bit
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    for s, sg in (("L", 1), ("R", -1)):
        pb = rig.pose.bones["UpperArm." + s]
        pb.rotation_mode = 'XYZ'
    bpy.ops.object.mode_set(mode='OBJECT')
    cam_data = bpy.data.cameras.new("Cam"); cam = link(bpy.data.objects.new("Cam", cam_data))
    cam.location = (1.5, -2.9, 1.1); cam.rotation_euler = (math.radians(88), 0, math.radians(27))
    cam_data.lens = 50
    scene.camera = cam
    for loc, energy, col in (((2, -2, 3), 220, (1, 0.9, 0.8)), ((-2.5, -1, 2), 70, (0.6, 0.7, 1)), ((0, 2.5, 2.5), 160, (1, 1, 1))):
        ld = bpy.data.lights.new("L", 'AREA'); ld.energy = energy; ld.color = col; ld.size = 2
        lo = link(bpy.data.objects.new("L", ld)); lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 1.2)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    world = bpy.data.worlds.new("W"); scene.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.12, 0.13, 0.15, 1)
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 24; scene.cycles.device = 'CPU'; scene.cycles.use_denoising = False
    scene.render.resolution_x = 720; scene.render.resolution_y = 900
    scene.render.filepath = PREVIEW
    bpy.ops.render.render(write_still=True)
    print("PREVIEW", PREVIEW)

# ------------------------------------------------------------------ HUD portrait (head close-up, transparent background)
if PORTRAIT:
    for o in [o for o in bpy.data.objects if o.type in ('CAMERA', 'LIGHT')]:
        bpy.data.objects.remove(o)
    cam_data = bpy.data.cameras.new("PCam"); cam = link(bpy.data.objects.new("PCam", cam_data))
    target = Vector((0, 0, 1.62))
    cam.location = (0.38, -1.15, 1.66)
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam_data.lens = 85
    scene.camera = cam
    for loc, energy, col, size in (((1.3, -0.9, 2.0), 55, (1, 0.72, 0.45), 0.6), ((-1.3, -0.8, 1.7), 8, (0.5, 0.62, 1), 1.5),
                                   ((-0.6, 1.2, 2.1), 110, (0.75, 0.85, 1), 0.5), ((0.9, 1.0, 1.9), 60, (1, 0.6, 0.3), 0.4)):
        ld = bpy.data.lights.new("PL", 'AREA'); ld.energy = energy; ld.color = col; ld.size = size
        lo = link(bpy.data.objects.new("PL", ld)); lo.location = loc
        lo.rotation_euler = (target - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    if scene.world is None:
        world = bpy.data.worlds.new("W"); scene.world = world; world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.02, 0.022, 0.025, 1)
    scene.view_settings.look = 'AgX - Punchy' if 'AgX - Punchy' in [i.identifier for i in scene.view_settings.bl_rna.properties['look'].enum_items] else 'None'

    scene.render.film_transparent = True
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.device = 'CPU'; scene.cycles.use_denoising = False
    scene.render.resolution_x = 512; scene.render.resolution_y = 512
    scene.render.filepath = PORTRAIT
    bpy.ops.render.render(write_still=True)
    print("PORTRAIT", PORTRAIT)
