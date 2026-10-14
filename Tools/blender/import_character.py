"""
Turns an AI-generated character (TRELLIS / Tripo GLB, millions of noisy triangles, holes, broken hair)
into the game's rigged Neanderthal:

  1. drop floating fragments, stand the model on the ground at 1.8 m, facing -Y;
  2. patch untextured (black) texels in the source texture;
  3. voxel-remesh (closes holes in the pelt and hair) and decimate to a game budget;
  4. new UV layout, bake albedo + tangent normal map from the original onto it;
  5. add full, shoulder-length hair (metaball locks fitted to the head);
  6. build the skeleton CreatureBodies expects (Hips/Spine/Chest/Neck/Head/Facing/UpperArm.L ...) and skin it;
  7. export Assets/Resources/Models/Neanderthal.fbx and Resources/Textures/neanderthal_diff/_nor_gl.png.

Run headless:
  blender -b --factory-startup -P Tools/blender/import_character.py -- <input.glb> <repo_root>
          [--tris 30000] [--check <dir>] [--preview out.png] [--portrait out.png] [--blend out.blend]
"""
import bpy
import bmesh
import math
import os
import random
import sys
import numpy as np
from mathutils import Matrix, Vector, Quaternion, Euler

sys.stdout.reconfigure(line_buffering=True)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def take_flag(name, default=None):
    if name in argv:
        i = argv.index(name)
        value = argv[i + 1]
        del argv[i:i + 2]
        return value
    return default


TARGET_TRIS = int(take_flag("--tris", "30000"))
CHECK_DIR = take_flag("--check")
PREVIEW = take_flag("--preview")
PORTRAIT = take_flag("--portrait")
BLEND = take_flag("--blend")
SRC, ROOT = argv[0], argv[1]

HEIGHT = 1.80
VOXEL = 0.006
TEX_SIZE = 2048
VIS = 0.574  # metaball visible radius / influence radius at threshold 0.6
HAIR_LUM = 0.16  # baked sRGB brightness below which head polygons count as hair/beard
random.seed(11)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


# ---------------------------------------------------------------- utilities
def activate(*objs):
    for o in bpy.context.view_layer.objects:
        if o is not None:
            o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[-1]


def apply_modifiers(obj):
    for m in list(obj.modifiers):
        with bpy.context.temp_override(object=obj, active_object=obj):
            bpy.ops.object.modifier_apply(modifier=m.name)


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def world_points(obj):
    co = np.empty(len(obj.data.vertices) * 3)
    obj.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def drop_small_islands(obj, keep_fraction, weld=0.0):
    """Deletes connected pieces smaller than keep_fraction of the largest one.
    weld > 0 first merges coincident vertices (AI meshes are split along every UV seam)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    if weld > 0:
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=weld)
    bm.verts.ensure_lookup_table()
    seen, islands = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, island = [v], []
        seen.add(v.index)
        while stack:
            cur = stack.pop()
            island.append(cur)
            for e in cur.link_edges:
                o = e.other_vert(cur)
                if o.index not in seen:
                    seen.add(o.index)
                    stack.append(o)
        islands.append(island)
    biggest = max(len(i) for i in islands)
    doomed = [v for i in islands if len(i) < biggest * keep_fraction for v in i]
    bmesh.ops.delete(bm, geom=doomed, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    print(f"islands: {len(islands)}, removed {len(doomed)} verts in small pieces")


def shift_sum(a):
    """3x3 box sum with edge clamping."""
    p = np.pad(a, ((1, 1), (1, 1)) + ((0, 0),) * (a.ndim - 2), mode="edge")
    return sum(p[1 + dy:p.shape[0] - 1 + dy, 1 + dx:p.shape[1] - 1 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1))


def box_mean(a, r):
    """Mean over a (2r+1)^2 window via an integral image."""
    c = np.pad(a, ((r + 1, r), (r + 1, r)), mode="edge").cumsum(0).cumsum(1)
    k = 2 * r + 1
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


def fill_dark_texels(img, threshold=0.03, iterations=60, relative=0.0):
    """Replaces holes by diffusing colour in from their neighbours. A texel is a hole when it is near-black,
    or (relative > 0) when it is much darker than its surroundings: AI textures are full of unbaked specks."""
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    rgb = px[:, :, :3]
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    hole = lum < threshold
    if relative > 0:
        hole |= (lum < 0.12) & (lum < relative * box_mean(lum, 10))
    valid = (~hole).astype(np.float32)
    print(f"{img.name}: patching {int((1 - valid).sum())} dark texels")
    for _ in range(iterations):
        if valid.min() > 0:
            break
        s = shift_sum(rgb * valid[:, :, None])
        n = shift_sum(valid)
        grow = (valid == 0) & (n > 0)
        rgb[grow] = s[grow] / n[grow][:, None]
        valid[grow] = 1.0
    px[:, :, :3] = rgb
    px[:, :, 3] = 1.0
    img.pixels.foreach_set(px.ravel())
    img.update()


def new_material(name, color, roughness):
    m = bpy.data.materials.new(name)
    if m.node_tree is None:
        m.use_nodes = True
    m.diffuse_color = (*color, 1)
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = roughness
    return m


def setup_cycles(samples):
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
        print("GPU unavailable, using CPU:", e)


# ------------------------------------------------------------ 1. import
bpy.ops.import_scene.gltf(filepath=SRC)
src = [o for o in bpy.data.objects if o.type == "MESH"]
activate(*src)
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
if len(src) > 1:
    bpy.ops.object.join()
high = bpy.context.view_layer.objects.active
for o in [o for o in bpy.data.objects if o.type != "MESH"]:
    bpy.data.objects.remove(o)
bpy.context.view_layer.update()
activate(high)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
high.name = "High"
drop_small_islands(high, 0.01, weld=0.0005)

pts = world_points(high)
lo, hi = pts.min(0), pts.max(0)
scale = HEIGHT / (hi[2] - lo[2])
torso = pts[(pts[:, 2] > lo[2] + 0.4 * (hi[2] - lo[2])) & (pts[:, 2] < lo[2] + 0.7 * (hi[2] - lo[2]))]
cx, cy = (torso[:, 0].min() + torso[:, 0].max()) / 2, (torso[:, 1].min() + torso[:, 1].max()) / 2
high.data.transform(Matrix.Translation((-cx * scale, -cy * scale, -lo[2] * scale)) @ Matrix.Scale(scale, 4))
high.data.update()
print(f"scaled x{scale:.3f}, source tris {tri_count(high)}")

src_mat = high.data.materials[0]
src_img = next(n.image for n in src_mat.node_tree.nodes
               if n.type == "TEX_IMAGE" and n.outputs[0].links and n.outputs[0].links[0].to_socket.name == "Base Color")
fill_dark_texels(src_img, relative=0.5, iterations=80)

# --------------------------------------------------------- 2. game mesh
bpy.ops.object.select_all(action="DESELECT")
low = high.copy()
low.data = high.data.copy()
low.name = "Neanderthal"
scene.collection.objects.link(low)
low.data.materials.clear()
# AI meshes are open shells (pelt, hair strands): give them thickness so the voxel remesh keeps them.
sol = low.modifiers.new("Solidify", "SOLIDIFY")
sol.thickness = 0.012
sol.offset = 0.0
rm = low.modifiers.new("Remesh", "REMESH")
rm.mode = "VOXEL"
rm.voxel_size = VOXEL
rm.adaptivity = 0.0
apply_modifiers(low)
drop_small_islands(low, 0.02)
sm = low.modifiers.new("Smooth", "CORRECTIVE_SMOOTH")
sm.iterations = 3
sm.use_only_smooth = True
apply_modifiers(low)
dec = low.modifiers.new("Decimate", "DECIMATE")
dec.ratio = TARGET_TRIS / max(1, tri_count(low))
apply_modifiers(low)
low.data.polygons.foreach_set("use_smooth", [True] * len(low.data.polygons))
print(f"game mesh: {tri_count(low)} tris")

activate(low)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004, area_weight=0.0, scale_to_bounds=False)
bpy.ops.uv.pack_islands(margin=0.003)
bpy.ops.object.mode_set(mode="OBJECT")

# ----------------------------------------------------------- 3. bake
skin = new_material("Skin", (0.25, 0.15, 0.1), 0.6)
low.data.materials.append(skin)
nt = skin.node_tree
bsdf = nt.nodes["Principled BSDF"]
albedo = bpy.data.images.new("neanderthal_diff", TEX_SIZE, TEX_SIZE, alpha=False)
normal = bpy.data.images.new("neanderthal_nor_gl", TEX_SIZE, TEX_SIZE, alpha=False, is_data=True)
normal.colorspace_settings.name = "Non-Color"
tex_albedo = nt.nodes.new("ShaderNodeTexImage")
tex_albedo.image = albedo
tex_normal = nt.nodes.new("ShaderNodeTexImage")
tex_normal.image = normal

setup_cycles(16)
bake = scene.render.bake
bake.use_selected_to_active = True
bake.cage_extrusion = 0.02
bake.max_ray_distance = 0.06
bake.margin = 12
activate(high, low)

nt.nodes.active = tex_albedo
bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_selected_to_active=True,
                    cage_extrusion=0.02, max_ray_distance=0.06, margin=12)
fill_dark_texels(albedo, threshold=0.02, iterations=400, relative=0.45)
nt.nodes.active = tex_normal
bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", use_selected_to_active=True,
                    cage_extrusion=0.02, max_ray_distance=0.06, margin=12)
# Rays that hit the back of thin pelt/hair shells bake normals pointing into the surface,
# which render as black patches: mirror them to the outside (keeps the relief).
npx = np.empty(TEX_SIZE * TEX_SIZE * 4, dtype=np.float32)
normal.pixels.foreach_get(npx)
npx = npx.reshape(-1, 4)
inward = npx[:, 2] < 0.5
npx[:, 2] = 0.5 + np.abs(npx[:, 2] - 0.5)
normal.pixels.foreach_set(npx.ravel())
normal.update()
print(f"baked albedo + normal ({int(inward.sum())} inward normals flipped)")

nt.links.new(tex_albedo.outputs["Color"], bsdf.inputs["Base Color"])
nmap = nt.nodes.new("ShaderNodeNormalMap")
nt.links.new(tex_normal.outputs["Color"], nmap.inputs["Color"])
nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
bsdf.inputs["Roughness"].default_value = 0.62

tex_dir = os.path.join(ROOT, "Assets", "Resources", "Textures")
os.makedirs(tex_dir, exist_ok=True)
for img in (albedo, normal):
    img.filepath_raw = os.path.join(tex_dir, img.name + ".png")
    img.file_format = "PNG"
    img.save()
    print("SAVED", img.filepath_raw)
bpy.data.objects.remove(high)

# ------------------------------------------------------- 4. landmarks
P = world_points(low)
X, Y, Z = P[:, 0], P[:, 1], P[:, 2]
hand_tip = np.abs(X).max()
arm = P[np.abs(X) > hand_tip * 0.55]
arm_z, arm_y = float(np.median(arm[:, 2])), float(np.median(arm[:, 1]))
chest = P[(Z > arm_z - 0.30) & (Z < arm_z - 0.18) & (np.abs(X) < hand_tip * 0.4)]
torso_w = float(np.percentile(np.abs(chest[:, 0]), 98))
sh_x = torso_w - 0.03
arm_len = hand_tip - sh_x
elbow_x, wrist_x = sh_x + 0.43 * arm_len, sh_x + 0.77 * arm_len
shin = P[(Z > 0.20 * HEIGHT) & (Z < 0.30 * HEIGHT)]
leg_x = float(np.median(np.abs(shin[:, 0])))
leg_y = float(np.median(shin[:, 1]))
top = float(Z.max())
head_pts = P[Z > top - 0.16]
head_y = float(np.median(head_pts[:, 1]))
head_half_w = float(np.percentile(np.abs(head_pts[:, 0]), 97))
head_front = float(np.percentile(head_pts[:, 1], 2))
head_back = float(np.percentile(head_pts[:, 1], 98))
feet = P[Z < 0.05]
toe_y = float(np.percentile(feet[:, 1], 1))
hips_z, knee_z, ankle_z = 0.52 * HEIGHT, 0.285 * HEIGHT, 0.055 * HEIGHT
neck_z, head_z = arm_z + 0.02, arm_z + 0.13
print(f"landmarks: arm_z {arm_z:.3f} hand {hand_tip:.3f} shoulder {sh_x:.3f} legs {leg_x:.3f} top {top:.3f} "
      f"head y {head_y:.3f} w {head_half_w:.3f} front {head_front:.3f} back {head_back:.3f}")

# --------------------------------------------------------------- 5. hair
class MB:
    def __init__(self, name, res):
        self.data = bpy.data.metaballs.new(name)
        self.data.resolution = self.data.render_resolution = res
        self.obj = bpy.data.objects.new(name, self.data)
        scene.collection.objects.link(self.obj)

    def ellipsoid(self, co, semi):
        m = max(semi)
        e = self.data.elements.new(type="ELLIPSOID")
        e.co, e.radius = co, m / VIS
        e.size_x, e.size_y, e.size_z = (s / m for s in semi)

    def capsule(self, a, b, r):
        a, b = Vector(a), Vector(b)
        d = b - a
        e = self.data.elements.new(type="CAPSULE")
        e.co, e.radius, e.size_x = (a + b) / 2, r / VIS, d.length / 2
        e.rotation = Vector((1, 0, 0)).rotation_difference(d.normalized())


hair_mat = new_material("Hair", (0.016, 0.011, 0.008), 0.85)
hair_mat.node_tree.nodes["Principled BSDF"].inputs["Specular IOR Level"].default_value = 0.25

# The source already has the right hairstyle and beard silhouette, only badly textured.
# Every head/neck polygon whose baked colour is dark (hair, beard, brows) gets the Hair material.
low.data.materials.append(hair_mat)
w, h = albedo.size
px = np.empty(w * h * 4, dtype=np.float32)
albedo.pixels.foreach_get(px)
lum_img = px.reshape(h, w, 4)[:, :, :3] @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
me = low.data
uv = np.empty(len(me.loops) * 2, dtype=np.float32)
me.uv_layers.active.data.foreach_get("uv", uv)
uv = uv.reshape(-1, 2)
ls = np.empty(len(me.polygons), dtype=np.int64)
lt = np.empty(len(me.polygons), dtype=np.int64)
me.polygons.foreach_get("loop_start", ls)
me.polygons.foreach_get("loop_total", lt)
puv = np.add.reduceat(uv, ls) / lt[:, None]
cen = np.empty(len(me.polygons) * 3, dtype=np.float32)
me.polygons.foreach_get("center", cen)
cen = cen.reshape(-1, 3)
pl = lum_img[np.clip((puv[:, 1] * h).astype(int), 0, h - 1), np.clip((puv[:, 0] * w).astype(int), 0, w - 1)]
on_head = (cen[:, 2] > neck_z + 0.03) & (np.abs(cen[:, 0]) < head_half_w + 0.02)   # not the shoulders
print("head polygon brightness percentiles", np.percentile(pl[on_head], [10, 25, 50, 75, 90]).round(3).tolist())
scalp = on_head & ((cen[:, 2] > top - 0.055) | ((cen[:, 1] > head_y + 0.01) & (cen[:, 2] > neck_z + 0.09)))
eye_band = ((cen[:, 1] < head_y - 0.02) & (np.abs(cen[:, 0]) < 0.075)
            & (cen[:, 2] > top - 0.15) & (cen[:, 2] < top - 0.085))    # dark eye sockets are not hair
is_hair = (on_head & (pl < HAIR_LUM) | scalp) & ~eye_band
# Majority filter over neighbouring polygons: removes speckles and smooths the hairline.
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
neighbours = [[f2.index for e in f.edges for f2 in e.link_faces if f2 is not f] for f in bm.faces]
bm.free()
for _ in range(3):
    ratio = np.array([is_hair[n].mean() if n else 0.0 for n in neighbours])
    is_hair = on_head & ~eye_band & np.where(is_hair, ratio > 0.3, ratio > 0.6)
mi = np.zeros(len(me.polygons), dtype=np.int32)
mi[is_hair] = 1
me.polygons.foreach_set("material_index", mi)
print(f"hair/beard polygons from the source: {int(is_hair.sum())} of {int(on_head.sum())} on the head")

# Long hair down the back to the shoulders (the source stops at the nape).
head_depth = (head_back - head_front) / 2
hc = Vector((0, (head_front + head_back) / 2, top - 0.10))
mb = MB("HairMB", 0.006)
for i in range(90):
    phi = math.radians(random.uniform(85, 275))                       # 0 = face; only sides and back
    side, back = math.sin(phi), -math.cos(phi)
    start = hc + Vector((side * head_half_w * 0.75, back * head_depth * 0.75, random.uniform(-0.02, 0.06)))
    end_z = random.uniform(arm_z - 0.10, arm_z + 0.03) if back > 0.3 else random.uniform(arm_z + 0.02, arm_z + 0.10)
    mid = Vector((side * (head_half_w * 0.9 + random.uniform(0.0, 0.03)), hc.y + back * (head_depth * 0.9 + 0.02),
                  (start.z + end_z) / 2 + 0.02))
    end = Vector((side * random.uniform(head_half_w * 0.5, head_half_w * 1.05), hc.y + back * (head_depth + random.uniform(0.02, 0.06)), end_z))
    r = random.uniform(0.009, 0.015)
    mb.capsule(start, mid, r)
    mb.capsule(mid, end, r * random.uniform(0.4, 0.7))
dg = bpy.context.evaluated_depsgraph_get()
hair_me = bpy.data.meshes.new_from_object(mb.obj.evaluated_get(dg))
bpy.data.objects.remove(mb.obj)
hair = bpy.data.objects.new("Hair", hair_me)
scene.collection.objects.link(hair)
tex = bpy.data.textures.new("HairNoise", "CLOUDS")
tex.noise_scale = 0.012
disp = hair.modifiers.new("Displace", "DISPLACE")
disp.texture, disp.strength, disp.texture_coords = tex, 0.008, "GLOBAL"
apply_modifiers(hair)
dec = hair.modifiers.new("Decimate", "DECIMATE")
dec.ratio = 5000 / max(1, tri_count(hair))
apply_modifiers(hair)
hair.data.polygons.foreach_set("use_smooth", [True] * len(hair.data.polygons))
hair.data.materials.append(hair_mat)
print(f"hair: {tri_count(hair)} tris")

# ----------------------------------------------------------- 6. skeleton
BONES = [
    ("Hips", (0, leg_y, hips_z), (0, leg_y, hips_z + 0.12), None),
    ("Spine", (0, leg_y, hips_z + 0.12), (0, leg_y, hips_z + 0.32), "Hips"),
    ("Chest", (0, leg_y, hips_z + 0.32), (0, arm_y, neck_z), "Spine"),
    ("Neck", (0, arm_y, neck_z), (0, head_y, head_z), "Chest"),
    ("Head", (0, head_y, head_z), (0, head_y, top), "Neck"),
    ("Facing", (0, head_front + 0.04, top - 0.12), (0, head_front - 0.1, top - 0.12), "Head"),
]
for s, sg in (("L", 1), ("R", -1)):
    BONES += [
        (f"Shoulder.{s}", (0.03 * sg, arm_y, arm_z - 0.01), (sh_x * sg, arm_y, arm_z), "Chest"),
        (f"UpperArm.{s}", (sh_x * sg, arm_y, arm_z), (elbow_x * sg, arm_y, arm_z), f"Shoulder.{s}"),
        (f"LowerArm.{s}", (elbow_x * sg, arm_y, arm_z), (wrist_x * sg, arm_y, arm_z), f"UpperArm.{s}"),
        (f"Hand.{s}", (wrist_x * sg, arm_y, arm_z), (hand_tip * sg, arm_y, arm_z), f"LowerArm.{s}"),
        (f"UpperLeg.{s}", (leg_x * 0.9 * sg, leg_y, hips_z - 0.02), (leg_x * sg, leg_y - 0.01, knee_z), "Hips"),
        (f"LowerLeg.{s}", (leg_x * sg, leg_y - 0.01, knee_z), (leg_x * sg, leg_y + 0.01, ankle_z), f"UpperLeg.{s}"),
        (f"Foot.{s}", (leg_x * sg, leg_y + 0.01, ankle_z), (leg_x * sg, toe_y + 0.06, 0.025), f"LowerLeg.{s}"),
        (f"Toes.{s}", (leg_x * sg, toe_y + 0.06, 0.025), (leg_x * sg, toe_y, 0.02), f"Foot.{s}"),
    ]

arm_data = bpy.data.armatures.new("Rig")
rig = bpy.data.objects.new("Rig", arm_data)
scene.collection.objects.link(rig)
activate(rig)
bpy.ops.object.mode_set(mode="EDIT")
for name, h, t, parent in BONES:
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = h, t
    if parent:
        b.parent = arm_data.edit_bones[parent]
    b.use_deform = name != "Facing"
bpy.ops.object.mode_set(mode="OBJECT")

activate(low, rig)
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
assert len(low.vertex_groups) > 0, "automatic weights failed"

for g in low.vertex_groups:
    hair.vertex_groups.new(name=g.name)
dt = hair.modifiers.new("WeightTransfer", "DATA_TRANSFER")
dt.object, dt.use_vert_data = low, True
dt.data_types_verts = {"VGROUP_WEIGHTS"}
dt.vert_mapping = "POLYINTERP_NEAREST"
apply_modifiers(hair)
for g in list(hair.vertex_groups):
    if "Arm" in g.name or "Hand" in g.name:
        hair.vertex_groups.remove(g)
activate(hair, low)
bpy.ops.object.join()
low = bpy.context.view_layer.objects.active
print("NEANDERTHAL_TRIS", tri_count(low), "VERTS", len(low.data.vertices))
mi = np.empty(len(low.data.polygons), dtype=np.int32)
low.data.polygons.foreach_get("material_index", mi)
print("materials", [(m.name, int((mi == i).sum())) for i, m in enumerate(low.data.materials)])

# ------------------------------------------------------------ 7. export
out_dir = os.path.join(ROOT, "Assets", "Resources", "Models")
activate(low, rig)
bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, "Neanderthal.fbx"), use_selection=True,
                         object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, bake_anim=False,
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="FACE", use_armature_deform_only=False, path_mode="STRIP")
print("EXPORTED", os.path.join(out_dir, "Neanderthal.fbx"))
if BLEND:
    os.makedirs(os.path.dirname(os.path.abspath(BLEND)), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(BLEND))


# ------------------------------------------------------------ renders
def area_light(loc, power, size, color, target=(0, 0, 1.2)):
    li = bpy.data.lights.new("Light", "AREA")
    li.energy, li.size, li.color = power, size, color
    o = bpy.data.objects.new("Light", li)
    scene.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()


def render(path, loc, target, lens, res, transparent=False):
    scene.render.film_transparent = transparent
    cd = bpy.data.cameras.new("Cam")
    cd.lens = lens
    cam = bpy.data.objects.new("Cam", cd)
    scene.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = path
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    print("RENDERED", path)


def rotate_world(bone, axis, deg):
    pb = rig.pose.bones[bone]
    local = (pb.bone.matrix_local.to_3x3().inverted() @ Vector(axis)).normalized()
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = Quaternion(local, math.radians(deg)) @ pb.rotation_quaternion


def rest_pose():
    for pb in rig.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)


if CHECK_DIR or PREVIEW or PORTRAIT:
    setup_cycles(64)
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world = bpy.data.worlds.new("World")
    if scene.world.node_tree is None:
        scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.3, 0.33, 0.36, 1)
    bg.inputs["Strength"].default_value = 0.5
    area_light((-2.2, -3.0, 3.2), 600, 2.0, (1.0, 0.93, 0.85))
    area_light((3.0, -2.5, 1.6), 200, 3.0, (0.8, 0.88, 1.0))
    area_light((0.6, 3.0, 2.8), 700, 1.5, (0.85, 0.92, 1.0))

if CHECK_DIR:
    render(os.path.join(CHECK_DIR, "front.png"), (0, -6.0, 0.95), (0, 0, 0.92), 62, (900, 1000))
    render(os.path.join(CHECK_DIR, "back.png"), (0, 6.0, 0.95), (0, 0, 0.92), 62, (900, 1000))
    render(os.path.join(CHECK_DIR, "side.png"), (6.0, 0, 0.95), (0, 0, 0.92), 62, (900, 1000))
    render(os.path.join(CHECK_DIR, "head.png"), (-0.35, -0.95, top - 0.08), (0, head_y, top - 0.13), 85, (800, 800))
    rotate_world("UpperArm.L", (0, 1, 0), 62)
    rotate_world("UpperArm.R", (0, 1, 0), -62)
    rotate_world("LowerArm.R", (0, 0, 1), 60)
    rotate_world("UpperLeg.L", (1, 0, 0), -35)
    rotate_world("LowerLeg.L", (1, 0, 0), 40)
    rotate_world("Head", (0, 0, 1), 25)
    render(os.path.join(CHECK_DIR, "pose_test.png"), (-3.4, -4.6, 1.2), (0, 0, 0.9), 62, (900, 1000))
    rest_pose()

if PREVIEW:
    rotate_world("UpperArm.L", (0, 1, 0), 68)
    rotate_world("UpperArm.R", (0, 1, 0), -68)
    render(PREVIEW, (1.6, -3.1, 1.15), (0, 0, 0.9), 50, (720, 900))
    rest_pose()

if PORTRAIT:
    render(PORTRAIT, (0.38, -1.15, top - 0.12), (0, head_y, top - 0.16), 85, (512, 512), transparent=True)
