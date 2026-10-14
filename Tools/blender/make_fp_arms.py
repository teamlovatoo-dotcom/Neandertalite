"""First-person arms for the game from "fps arms (rigged only)" by para, CC0
(https://opengameart.org/content/fps-arms-rigged-only, MakeHuman-based mesh, IK + finger-curl rig).
The fingers are closed with the rig's own controls (first finger bone rotated, the rest copy it), the pose is baked
as the new rest pose, and Grip.L / Grip.R empties under the hands mark the handle centre (+Z along the handle, out of
the thumb side). Exports Assets/Resources/Models/FPArms.fbx and Textures/fparms_diff.png.
Run: blender -b "<unpacked>/FPS ARMS RIG 1.blend" -P Tools/blender/make_fp_arms.py -- "<unpacked>/new_diff.png" """
import bpy, sys, os, math, shutil
import numpy as np
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TEX = args[0]
OUT = os.path.join(ROOT, "Assets", "Resources", "Models", "FPArms.fbx")
CURL = {"index": 52, "middle": 55, "ring": 57, "pinky": 60}     # degrees per joint (the rig copies .01 to .02/.03)
THUMB = 28

rig = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
mesh = [o for o in bpy.data.objects if o.type == 'MESH'][0]
for o in list(bpy.data.objects):
    if o not in (rig, mesh):
        bpy.data.objects.remove(o)
if rig.animation_data:
    rig.animation_data.action = None
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
for pb in rig.pose.bones:
    pb.rotation_mode = 'XYZ'
    pb.rotation_euler = (0, 0, 0); pb.location = (0, 0, 0)
for s in ("L", "R"):
    for f, a in CURL.items():
        rig.pose.bones[f"f_{f}.01.{s}"].rotation_euler = (math.radians(a), 0, 0)
    rig.pose.bones[f"thumb.02.{s}"].rotation_euler = (math.radians(THUMB), 0, 0)
bpy.context.view_layer.update()


def wpos(name):
    return np.array(rig.matrix_world @ rig.pose.bones[name].head)


# Grip centre: the curled joints of each finger lie on a circle round the handle.
grips = {}
for s in ("L", "R"):
    cs, ns, rs = [], [], []
    for f in ("index", "middle", "ring", "pinky"):
        a, b, d = (wpos(f"f_{f}.0{k}.{s}") for k in (1, 2, 3))
        ab, ad = b - a, d - a
        nn = np.cross(ab, ad)
        c = a + (np.cross(nn, ab) * np.dot(ad, ad) + np.cross(ad, nn) * np.dot(ab, ab)) / (2 * np.dot(nn, nn))
        cs.append(c); ns.append(nn / np.linalg.norm(nn)); rs.append(np.linalg.norm(c - a))
    side = wpos(f"f_index.01.{s}") - wpos(f"f_pinky.01.{s}")
    axis = np.mean([x if np.dot(x, side) > 0 else -x for x in ns], axis=0); axis /= np.linalg.norm(axis)
    centre = np.mean(cs, axis=0)
    back = wpos(f"hand.{s}") - centre
    grips[s] = (centre, axis, back, float(np.mean(rs)))

# Bake: the mesh takes the posed shape, the pose becomes the rest pose, rig constraints go away.
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.objects.active = mesh
for m in list(mesh.modifiers):
    if m.type == 'ARMATURE':
        bpy.ops.object.modifier_apply(modifier=m.name)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.select_all(action='SELECT')
bpy.ops.pose.armature_apply(selected=False)
for pb in rig.pose.bones:
    for c in list(pb.constraints):
        pb.constraints.remove(c)
bpy.ops.object.mode_set(mode='EDIT')
# For the game the hand hangs off the forearm (the IK control bone is Blender-only).
for s in ("L", "R"):
    eb = rig.data.edit_bones
    eb[f"hand.{s}"].parent = eb[f"forearm.{s}"]
    eb.remove(eb[f"hand.{s}.control"])
bpy.ops.object.mode_set(mode='OBJECT')
am = mesh.modifiers.new("Armature", 'ARMATURE'); am.object = rig

for s, (c, axis, back, r) in grips.items():
    z = Vector(axis); y = Vector(back - axis * np.dot(back, axis)).normalized(); x = y.cross(z)
    e = bpy.data.objects.new(f"Grip.{s}", None)
    bpy.context.scene.collection.objects.link(e)
    m = Matrix((x, y, z)).transposed().to_4x4(); m.translation = Vector(c)
    e.parent = rig; e.parent_type = 'BONE'; e.parent_bone = f"hand.{s}"
    e.matrix_world = m

# Markers like the other models: forward = where the hands are, up; Up's length sets the scale (upper arm ~0.31 m).
bu = rig.data.bones["upper_arm.R"]
ua = (rig.matrix_world @ bu.tail_local - rig.matrix_world @ bu.head_local).length
units_per_m = ua / 0.31
fwd = Vector(grips["R"][0] + grips["L"][0]) * 0.5; fwd.z = 0; fwd.normalize()
for name, d in (("Fwd", fwd), ("Up", Vector((0, 0, 1)))):
    e = bpy.data.objects.new(name, None); e.location = d * units_per_m
    bpy.context.scene.collection.objects.link(e)
mesh.data.materials[0].name = "FPSkin"
print("FPARMS units/m", round(units_per_m, 3), "grip radius (m)", round(grips["R"][3] / units_per_m, 4))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=False, object_types={'ARMATURE', 'MESH', 'EMPTY'}, add_leaf_bones=False,
                         bake_anim=False, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         mesh_smooth_type='FACE', use_armature_deform_only=True, path_mode='STRIP')
shutil.copyfile(TEX, os.path.join(ROOT, "Assets", "Resources", "Textures", "fparms_diff.png"))
print("EXPORTED", OUT)
