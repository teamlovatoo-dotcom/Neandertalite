"""
Turns an AI-generated weapon GLB (TRELLIS / Tripo: ~200k triangles, longest side along X) into a game weapon model:
Assets/Resources/Models/Weapons/<id>.fbx plus Resources/Textures/w_<id>_diff.png / w_<id>_nor_gl.png.

Same conventions as make_weapons.py: build space Z = weapon length (bow: Z = limbs, string towards -Y),
grip at the origin, empties 'Fwd'/'Up' one metre out so the game can orient the model, plus Tip / NockA / NockB.
The source is expected with the stone end (blade, axe head, arrowhead) at -X; the bow with its string at +Z.

Run headless:
  blender -b --factory-startup -P Tools/blender/import_weapon.py -- <input.glb> <repo_root> <knife|axe|bow|arrow|medkit> [--check dir]
"""
import bpy
import bmesh
import math
import os
import sys
import numpy as np
from mathutils import Matrix, Vector

sys.stdout.reconfigure(line_buffering=True)
argv = sys.argv[sys.argv.index("--") + 1:]
CHECK = None
if "--check" in argv:
    i = argv.index("--check")
    CHECK = argv[i + 1]
    del argv[i:i + 2]
SRC, ROOT, WID = argv[0], argv[1], argv[2]

#           length (m)   grip (fraction of length from the butt)   triangle budget
SPEC = {
    "knife": dict(length=0.27, grip=0.30, tris=2500),
    "axe": dict(length=0.62, grip=0.20, tris=3000),
    "arrow": dict(length=0.81, grip=None, tris=1200),
    "bow": dict(length=1.14, grip=0.5, tris=3000),
    "medkit": dict(length=0.30, grip=None, tris=3000),   # upright prop: length = height, origin at the bottom
}[WID]
UPRIGHT = WID == "medkit"
OUT_DIR = "Props" if UPRIGHT else "Weapons"
TEX = 1024

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


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


def points(obj):
    co = np.empty(len(obj.data.vertices) * 3)
    obj.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def clean(obj):
    """Welds seam-split vertices and drops floating fragments."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0004)
    bm.verts.ensure_lookup_table()
    seen, islands = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, isl = [v], []
        seen.add(v.index)
        while stack:
            cur = stack.pop()
            isl.append(cur)
            for e in cur.link_edges:
                o = e.other_vert(cur)
                if o.index not in seen:
                    seen.add(o.index)
                    stack.append(o)
        islands.append(isl)
    big = max(len(i) for i in islands)
    bmesh.ops.delete(bm, geom=[v for i in islands if len(i) < big * 0.01 for v in i], context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()


def box_mean(a, r):
    c = np.pad(a, ((r + 1, r), (r + 1, r)), mode="edge").cumsum(0).cumsum(1)
    k = 2 * r + 1
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


def fill_holes(img):
    """AI textures are full of unbaked near-black specks: diffuse colour in from the neighbours."""
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    rgb = px[:, :, :3]
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    valid = (~((lum < 0.025) | ((lum < 0.1) & (lum < 0.45 * box_mean(lum, 8))))).astype(np.float32)
    for _ in range(60):
        if valid.min() > 0:
            break
        p = lambda a: np.pad(a, ((1, 1), (1, 1)) + ((0, 0),) * (a.ndim - 2), mode="edge")
        sv, vv = p(rgb * valid[:, :, None]), p(valid)
        s = sum(sv[1 + dy:h + 1 + dy, 1 + dx:w + 1 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1))
        n = sum(vv[1 + dy:h + 1 + dy, 1 + dx:w + 1 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1))
        grow = (valid == 0) & (n > 0)
        rgb[grow] = s[grow] / n[grow][:, None]
        valid[grow] = 1
    px[:, :, :3] = rgb
    px[:, :, 3] = 1
    img.pixels.foreach_set(px.ravel())
    img.update()


# ------------------------------------------------------------------ import
bpy.ops.import_scene.gltf(filepath=SRC)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
activate(*meshes)
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
if len(meshes) > 1:
    bpy.ops.object.join()
high = bpy.context.view_layer.objects.active
for o in [o for o in bpy.data.objects if o.type != "MESH"]:
    bpy.data.objects.remove(o)
bpy.context.view_layer.update()
activate(high)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
clean(high)

# source -> build space (both rotations, no mirroring)
if UPRIGHT:
    R = Matrix.Identity(4)
elif WID == "bow":
    R = Matrix(((0, -1, 0, 0), (0, 0, -1, 0), (1, 0, 0, 0), (0, 0, 0, 1)))   # X->Z limbs, +Z(string)->-Y
else:
    R = Matrix(((0, -1, 0, 0), (0, 0, 1, 0), (-1, 0, 0, 0), (0, 0, 0, 1)))   # -X(stone end)->+Z
high.data.transform(R)
P = points(high)
L = P[:, 2].max() - P[:, 2].min()
s = SPEC["length"] / L
high.data.transform(Matrix.Scale(s, 4))
P = points(high)
zmin, zmax = P[:, 2].min(), P[:, 2].max()
if UPRIGHT:
    oz = zmin
    ring = P
elif WID == "arrow":
    oz = zmax - 0.058                        # origin at the base of the arrowhead, like make_weapons.arrow
    ring = P[np.abs(P[:, 2] - (oz - 0.15)) < 0.02]
elif WID == "bow":
    oz = 0.5 * (zmin + zmax)
    ring = P[np.abs(P[:, 2] - oz) < 0.03]
else:
    oz = zmin + SPEC["grip"] * (zmax - zmin)
    ring = P[np.abs(P[:, 2] - oz) < 0.015]
ox = 0.5 * (ring[:, 0].min() + ring[:, 0].max())
oy = 0.5 * (ring[:, 1].min() + ring[:, 1].max())
high.data.transform(Matrix.Translation((-ox, -oy, -oz)))
high.data.update()
P = points(high)
print(f"{WID}: {tri_count(high)} source tris, scaled x{s:.3f}, z {P[:, 2].min():.3f}..{P[:, 2].max():.3f}")

# ------------------------------------------------------------ texture
mat = high.data.materials[0]
src_img = next(n.image for n in mat.node_tree.nodes
               if n.type == "TEX_IMAGE" and n.outputs[0].links and n.outputs[0].links[0].to_socket.name == "Base Color")
fill_holes(src_img)
tex_dir = os.path.join(ROOT, "Assets", "Resources", "Textures")

markers = {}
if WID == "bow":
    # The game draws its own animated string between NockA and NockB, so the modelled string goes.
    # It is fused to the limbs, so find it geometrically: cut the bow into thin slices along the limbs;
    # in each slice the points form two clusters, the thick limb and the thin string.
    P = points(high)
    half = 0.5 * (P[:, 2].max() - P[:, 2].min())
    is_string = np.zeros(len(P), dtype=bool)
    centres = []
    for z0 in np.arange(-0.78 * half, 0.78 * half, 0.01):
        idx = np.where((P[:, 2] >= z0) & (P[:, 2] < z0 + 0.01))[0]
        if len(idx) < 20:
            continue
        q = P[idx, :2]
        a = q[np.argmax(np.linalg.norm(q - q.mean(0), axis=1))]           # two-means, seeded far apart
        b = q[np.argmax(np.linalg.norm(q - a, axis=1))]
        for _ in range(8):
            lab = np.linalg.norm(q - a, axis=1) > np.linalg.norm(q - b, axis=1)
            if lab.all() or (~lab).all():
                break
            a, b = q[~lab].mean(0), q[lab].mean(0)
        if np.linalg.norm(a - b) < 0.02:
            continue                                                       # string and limb touch here
        spread_a = np.linalg.norm(q[~lab] - a, axis=1).mean()
        spread_b = np.linalg.norm(q[lab] - b, axis=1).mean()
        thin = lab if spread_b < spread_a else ~lab
        is_string[idx[thin]] = True
        centres.append((z0 + 0.005, *(b if spread_b < spread_a else a)))
    bm = bmesh.new()
    bm.from_mesh(high.data)
    bm.verts.ensure_lookup_table()
    doomed = [f for f in bm.faces if all(is_string[v.index] for v in f.verts)]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(high.data)
    bm.free()
    clean(high)
    P = points(high)
    nz = half - 0.03
    for name, sg in (("NockA", 1), ("NockB", -1)):                        # string ties on at the limb tips
        tip = P[sg * P[:, 2] > nz - 0.02]
        markers[name] = (float(tip[:, 0].mean()), float(tip[:, 1].min()), float(sg * nz))
    markers["Fwd"], markers["Up"] = (0, 1, 0), (0, 0, 1)
    print(f"bow: removed {len(doomed)} string faces from {len(centres)} slices, nocks {markers['NockA']} / {markers['NockB']}")

# ---------------------------------------------------------- game mesh
# AI meshes are split along every UV seam, so a plain decimate stalls: rebuild the surface
# (thin parts get a little thickness first), decimate, unwrap and bake colour + relief from the source.
voxel = max(SPEC["length"] / 350, 0.0006)
low = high.copy()
low.data = high.data.copy()
low.name = WID
scene.collection.objects.link(low)
low.data.materials.clear()
sol = low.modifiers.new("Solidify", "SOLIDIFY")
sol.thickness, sol.offset = voxel * 1.5, 0.0
rm = low.modifiers.new("Remesh", "REMESH")
rm.mode, rm.voxel_size, rm.adaptivity = "VOXEL", voxel, 0.0
apply_modifiers(low)
dec = low.modifiers.new("Decimate", "DECIMATE")
dec.ratio = SPEC["tris"] / max(1, tri_count(low))
apply_modifiers(low)

if UPRIGHT:
    markers["Fwd"], markers["Up"] = (0, -1, 0), (0, 0, 1)
elif WID != "bow":
    P = points(low)
    markers["Fwd"], markers["Up"] = (0, 0, 1), (0, 1, 0)
    if WID == "axe":
        head = P[P[:, 2] > P[:, 2].max() - 0.12]
        markers["Tip"] = (0, float(head[:, 1].max()) * 0.8, float(np.median(head[:, 2])))
    else:
        markers["Tip"] = (0, 0, float(P[:, 2].max()))

activate(low)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.006, area_weight=0.0, scale_to_bounds=False)
bpy.ops.uv.pack_islands(margin=0.004)
bpy.ops.object.mode_set(mode="OBJECT")

lm = bpy.data.materials.new(f"w_{WID}")
low.data.materials.append(lm)
nt = lm.node_tree
bsdf = nt.nodes["Principled BSDF"]
aimg = bpy.data.images.new(f"w_{WID}_diff", TEX, TEX, alpha=False)
nimg = bpy.data.images.new(f"w_{WID}_nor_gl", TEX, TEX, alpha=False, is_data=True)
nimg.colorspace_settings.name = "Non-Color"
t_alb = nt.nodes.new("ShaderNodeTexImage")
t_alb.image = aimg
t_nor = nt.nodes.new("ShaderNodeTexImage")
t_nor.image = nimg
scene.render.engine = "CYCLES"
scene.cycles.samples = 4
activate(high, low)
reach = dict(use_selected_to_active=True, cage_extrusion=voxel * 4, max_ray_distance=voxel * 10, margin=8)
nt.nodes.active = t_alb
bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, **reach)
fill_holes(aimg)
nt.nodes.active = t_nor
bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", **reach)
npx = np.empty(TEX * TEX * 4, dtype=np.float32)
nimg.pixels.foreach_get(npx)
npx = npx.reshape(-1, 4)
npx[:, 2] = 0.5 + np.abs(npx[:, 2] - 0.5)   # normals baked from the back of thin shells point inwards
nimg.pixels.foreach_set(npx.ravel())
for img in (aimg, nimg):
    img.filepath_raw = os.path.join(tex_dir, img.name + ".png")
    img.file_format = "PNG"
    img.save()
nt.links.new(t_alb.outputs["Color"], bsdf.inputs["Base Color"])
nmap = nt.nodes.new("ShaderNodeNormalMap")
nt.links.new(t_nor.outputs["Color"], nmap.inputs["Color"])
nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
bpy.data.objects.remove(high)
low.data.polygons.foreach_set("use_smooth", [True] * len(low.data.polygons))

for name, co in markers.items():
    e = bpy.data.objects.new(name, None)
    e.location = co
    scene.collection.objects.link(e)
print(f"{WID}: {tri_count(low)} tris, markers {markers}")

out = os.path.join(ROOT, "Assets", "Resources", "Models", OUT_DIR, WID + ".fbx")
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, object_types={"MESH", "EMPTY"},
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, add_leaf_bones=False)
print("EXPORTED", out)

if CHECK:
    # side and front views with the grip (red) and markers (green) shown as dots
    scene.render.engine = "BLENDER_EEVEE"
    scene.world = bpy.data.worlds.new("W")
    if scene.world.node_tree is None:
        scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.55, 0.57, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = 1.6

    def dot(co, color):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.006 + SPEC["length"] * 0.006, location=co)
        m = bpy.data.materials.new("dot")
        m.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = (*color, 1)
        m.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 5
        bpy.context.active_object.data.materials.append(m)

    dot((0, 0, 0), (1, 0, 0))
    for n, co in markers.items():
        if n not in ("Fwd", "Up"):
            dot(co, (0, 1, 0))
    os.makedirs(CHECK, exist_ok=True)
    size = SPEC["length"] * 1.15
    for name, d in (("side", (1, 0, 0)), ("front", (0, -1, 0))):
        cd = bpy.data.cameras.new(name)
        cd.type, cd.ortho_scale = "ORTHO", size
        cam = bpy.data.objects.new(name, cd)
        scene.collection.objects.link(cam)
        center = Vector((0, 0, 0.5 * (zmin + zmax) - oz))
        cam.location = center + Vector(d) * 3
        cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.camera = cam
        scene.render.resolution_x, scene.render.resolution_y = 500, 900
        scene.render.filepath = os.path.join(CHECK, f"{WID}_{name}.png")
        bpy.ops.render.render(write_still=True)
