"""First-person preview: renders what the player sees (vertical FOV 80, 16:9) with the weapon and hand FBX files
placed exactly as the game places them (WeaponHolder hip pose + ViewHands offsets, Unity axes converted).
Not shipped; used to judge viewmodels without opening Unity.
Run: blender -b -P Tools/blender/fp_preview.py -- out.png weapon [weapon ...]"""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mathutils import Vector, Matrix
import meshkit as mk
import preview

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
WDIR = os.path.join(ROOT, "Assets", "Resources", "Models", "Weapons")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else "/tmp/fp.png"
WEAPONS = args[1:] or ["knife"]
HAND_FILE = os.environ.get("FP_HAND", "hand")       # lets a new hand model be compared with the old one

# Unity-side placement (keep in sync with Weapons.cs HipPosition/HipEuler and ViewHands.Attach).
VERT = "vertical"
HIP = {"default": ((0.28, -0.27, 0.45), (0, -6, 0)), "spear": ((0.22, -0.22, 0.1), (-4, -3, 0)),
       "bow": ((-0.1, -0.1, 0.5), (0, 3, -24)), "slingshot": ((0.06, -0.14, 0.42), (-4, 0, -8)),
       "stone": ((0.24, -0.22, 0.38), (10, -10, 0))}
HANDS = {"fists": [((0, 0, 0.05), (-20, 0, 0), False), ((-0.46, 0.02, 0.08), (-20, 0, 0), True)],
         "spear": [((0, 0, 0), (0, 0, 0), False), ((0, 0, 0.42), (0, 0, 25), True)],
         "bow": [((0, -0.01, 0.005), VERT, True), ((0, 0, -0.12), VERT, False)],
         "slingshot": [((0, -0.045, 0), VERT, True)],
         "stone": [((0.006, -0.012, -0.008), (0, 0, 0), False)]}
HAND_MODEL = os.environ.get("FP_HAND_LAYOUT", "")


def euler_u(e):
    """Unity Quaternion.Euler(x, y, z) as a 3x3 matrix acting on (right, up, forward) triples: Ry * Rx * Rz."""
    x, y, z = (math.radians(a) for a in e)
    rx = Matrix(((1, 0, 0), (0, math.cos(x), -math.sin(x)), (0, math.sin(x), math.cos(x))))
    ry = Matrix(((math.cos(y), 0, math.sin(y)), (0, 1, 0), (-math.sin(y), 0, math.cos(y))))
    rz = Matrix(((math.cos(z), -math.sin(z), 0), (math.sin(z), math.cos(z), 0), (0, 0, 1)))
    return ry @ rx @ rz


def rot_u(r):
    if r == VERT:   # Quaternion.LookRotation(Vector3.up, Vector3.forward)
        return Matrix(((-1, 0, 0), (0, 0, 1), (0, 1, 0)))
    return euler_u(r)


def trs(pos, rot3, scale=(1, 1, 1)):
    m = rot3.to_4x4() @ Matrix.Diagonal((*scale, 1))
    m.translation = Vector(pos)
    return m


# Unity camera space (right, up, forward) -> Blender world with the camera at the origin looking along +Y.
C = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def import_model(name):
    """Import an FBX; return (objects, model->unity-local matrix) using its Fwd/Up empties like ModelKit does."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(WDIR, name + ".fbx"))
    objs = [o for o in bpy.data.objects if o not in before]
    bpy.context.view_layer.update()
    pts = {o.name.split(".")[0]: o.matrix_world.translation.copy() for o in objs if o.type == 'EMPTY'}
    f, u = pts["Fwd"], pts["Up"]
    scale = 1.0 / u.length
    fn, un = f.normalized(), u.normalized()
    r = fn.cross(un)
    m = Matrix((r, un, fn)).transposed().to_4x4()      # columns: right, up, forward (blender coords)
    to_unity = Matrix.Diagonal((scale, scale, scale, 1)) @ m.inverted()
    meshes = [o for o in objs if o.type == 'MESH']
    for o in objs:
        if o.type != 'MESH':
            bpy.data.objects.remove(o)
    return meshes, to_unity


def place(meshes, to_unity, unity_world):
    for o in meshes:
        o.matrix_world = C @ unity_world @ to_unity @ o.matrix_world


def scene_backdrop():
    # ground and a far wall so the viewmodel has something behind it
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 20, -1.6))
    g = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.18, 0.2, 0.1, 1)
    g.data.materials.append(gm)


def render(weapon, out):
    mk.reset()
    w_name = {"obsidian_knife": "knife", "obsidian_spear": "spear", "obsidian_axe": "axe"}.get(weapon, weapon)
    key = "spear" if w_name == "spear" else "bow" if w_name == "bow" else w_name if w_name in HIP else "default"
    hip_pos, hip_eul = HIP[key]
    root = trs(hip_pos, euler_u(hip_eul))
    if weapon != "fists":
        meshes, tu = import_model(w_name)
        place(meshes, tu, root)
    for pos, r, left in HANDS.get(w_name, [((0, 0, 0), (0, 0, 0), False)]):
        meshes, tu = import_model(HAND_FILE)
        place(meshes, tu, root @ trs(pos, rot_u(r), (-1, 1, 1) if left else (1, 1, 1)))
    scene_backdrop()
    preview.TEX["skin"] = ("skin" if os.path.exists(os.path.join(ROOT, "Assets/Resources/Textures/skin_diff.png")) else "hide", (0.95, 0.72, 0.58))
    preview.TEX["nail"] = ("bone", (0.9, 0.75, 0.65))
    preview.setup_materials(ROOT)
    preview.render_setup(640)
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.angle_y = math.radians(80)
    cam.data.clip_start = 0.01
    cam.rotation_euler = (math.radians(90), 0, 0)      # look along +Y, +Z up
    sc.camera = cam
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)


for i, w in enumerate(WEAPONS):
    render(w, OUT if len(WEAPONS) == 1 else OUT.replace(".png", f"_{w}.png"))
