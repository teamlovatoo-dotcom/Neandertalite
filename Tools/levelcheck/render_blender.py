"""Renders the generated catacombs layout in Blender for visual review.
blender -b -P render_blender.py -- <dir with layout.json/height.bin> <out prefix>"""
import bpy, sys, json, math
import numpy as np
from mathutils import Vector, Euler, Matrix
args = sys.argv[sys.argv.index("--") + 1:]
D, OUT = args[0], args[1]
L = json.load(open(D + "/layout.json"))
raw = np.fromfile(D + "/height.bin", dtype=np.float32).reshape(601, 601, 3)
H, HOLE, WET = raw[..., 0], raw[..., 1] > 0.5, raw[..., 2] > 0.5
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

def mat(name, col, alpha=1.0, rough=0.8, emit=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*col, 1)
    b.inputs["Roughness"].default_value = rough
    if alpha < 1:
        b.inputs["Alpha"].default_value = alpha
        m.blend_method = 'BLEND'
    if emit > 0:
        b.inputs["Emission Color"].default_value = (*col, 1)
        b.inputs["Emission Strength"].default_value = emit
    return m

M = {"ground": mat("ground", (0.22, 0.3, 0.12)), "rock": mat("rock", (0.4, 0.38, 0.35)), "wall": mat("wall", (0.55, 0.5, 0.45)),
     "floor": mat("floor", (0.35, 0.28, 0.2)), "retain": mat("retain", (0.75, 0.68, 0.55)), "face": mat("face", (0.8, 0.5, 0.3)),
     "cliff": mat("cliff", (0.3, 0.28, 0.27)), "menhir": mat("menhir", (0.5, 0.5, 0.55)), "water": mat("water", (0.1, 0.35, 0.6), 0.75, 0.05),
     "torch": mat("torch", (1, 0.5, 0.1), emit=8), "decor": mat("decor", (0.9, 0.9, 0.8)), "spawn": mat("spawn", (0.1, 0.4, 1), emit=3),
     "pickup": mat("pickup", (1, 0.9, 0.1), emit=3), "sand": mat("sand", (0.55, 0.5, 0.38))}

# terrain
n = 601
xs = -150 + np.arange(n) * 0.5
verts = [(xs[i], xs[j], float(H[j, i])) for j in range(n) for i in range(n)]   # Blender: x, y(=Unity z), z(=Unity y)
faces, fm = [], []
for j in range(n - 1):
    for i in range(n - 1):
        if HOLE[j, i] or HOLE[j, i + 1] or HOLE[j + 1, i] or HOLE[j + 1, i + 1]:
            continue
        faces.append((j * n + i, j * n + i + 1, (j + 1) * n + i + 1, (j + 1) * n + i))
        fm.append(1 if WET[j, i] else 0)
me = bpy.data.meshes.new("Terrain"); me.from_pydata(verts, [], faces)
me.materials.append(M["ground"]); me.materials.append(M["sand"])
for p, k in zip(me.polygons, fm): p.material_index = k
terrain = bpy.data.objects.new("Terrain", me); sc.collection.objects.link(terrain)

coll_under = bpy.data.collections.new("Under"); sc.collection.children.link(coll_under)
def cube(c, s, yaw, pitch, m, coll=None):
    bpy.ops.mesh.primitive_cube_add(size=1)
    o = bpy.context.active_object
    o.scale = (s[0], s[2], s[1])
    # Unity (x,y,z) -> Blender (x,z,y); rotation Euler(0,yaw,0)*Euler(pitch,0,0)
    Ry = Matrix.Rotation(-math.radians(yaw), 4, 'Z')
    Rx = Matrix.Rotation(-math.radians(pitch), 4, 'X')
    o.matrix_world = Matrix.Translation((c[0], c[2], c[1])) @ Ry @ Rx @ Matrix.Diagonal((s[0], s[2], s[1], 1))
    o.data.materials.append(m)
    if coll is not None:
        for cc in o.users_collection: cc.objects.unlink(o)
        coll.objects.link(o)
    return o
pm = {"Floor": "floor", "Ceiling": "rock", "Wall": "wall", "Post": "wall", "Retain": "retain", "Face": "face", "Pillar": "wall",
      "Altar": "menhir", "Cliff": "cliff", "Lip": "cliff", "Menhir": "menhir", "Lintel": "menhir"}
for b in L["boxes"]:
    cube(b[0:3], b[3:6], b[6], b[7], M[pm[b[8]]], coll_under if b[8] in ("Floor", "Ceiling", "Wall", "Post", "Pillar", "Altar") else None)
for w in L["water"]:
    bpy.ops.mesh.primitive_plane_add(size=1, location=(w[0], w[2], w[1]))
    o = bpy.context.active_object; o.scale = (w[3], w[4], 1); o.data.materials.append(M["water"])
def dots(lst, m, r):
    for s in lst:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=(s[0], s[2], s[1]), segments=8, ring_count=6)
        bpy.context.active_object.data.materials.append(m)
dots(L["torches"], M["torch"], 0.25); dots(L["posts"], M["torch"], 0.3)
dots(L["decor"], M["decor"], 0.4); dots(L["spawns"], M["spawn"], 0.6); dots(L["pickups"], M["pickup"], 0.35)

w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.6, 0.7, 0.85, 1)
w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); sun.data.energy = 3
sun.rotation_euler = (math.radians(45), 0, math.radians(30)); sc.collection.objects.link(sun)
sc.render.engine = 'CYCLES'; sc.cycles.samples = 12; sc.cycles.use_denoising = False
sc.render.resolution_x, sc.render.resolution_y = 1100, 700
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam

def shot(name, loc, target, lens=24, hide_terrain=False, ortho=0):
    cam.location = Vector(loc)
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    cam.data.type = 'ORTHO' if ortho else 'PERSP'
    if ortho: cam.data.ortho_scale = ortho
    terrain.hide_render = hide_terrain
    sc.render.filepath = f"{OUT}_{name}.png"
    bpy.ops.render.render(write_still=True)
    print("SHOT", name)

views = sys.argv[sys.argv.index("--") + 3:] if len(args) > 2 else ["all"]
want = lambda v: "all" in views or v in views
# Blender coords: (x, z_unity, y_unity)
if want("overview"): shot("overview", (60, -230, 190), (0, 10, 0), 24)
if want("waterfall"): shot("waterfall", (-8, 30, 8), (-18, 62, 5), 24)
if want("west"): shot("west", (-115, -30, 14), (-96, -12, 2), 24)
if want("east"): shot("east", (110, -55, 16), (95, -30, 2), 26)
if want("plateau"): shot("plateau", (42, 82, 26), (18, 104, 13), 26)
if want("under"): shot("under", (0, -110, 120), (0, -15, -14), 30, hide_terrain=True)
if want("top"): shot("top", (0, 0, 300), (0, 0.01, 0), 30, ortho=300)
