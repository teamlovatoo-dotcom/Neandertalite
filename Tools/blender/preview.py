"""Quick Cycles previews of generated models with the game's textures (for checking shapes, not shipped)."""
import bpy, os, math
from mathutils import Vector
import meshkit as mk

TEX = {"wood_fine": ("wood_fine", (1.3, 1.15, 1.0)), "wood_rough": ("wood_rough", (0.95, 0.75, 0.55)),
       "bark": ("bark", (1, 1, 1)), "pole_bark": ("pole_bark", (1, 1, 1)), "leather": ("leather", (1.1, 1.0, 0.95)),
       "sinew": ("hide", (0.8, 0.68, 0.5)), "stone": ("flint", (1, 1, 1)), "obsidian": ("stone_dark", (0.3, 0.3, 0.34)),
       "bone": ("bone", (1, 1, 1)), "ivory": ("bone", (1.05, 1.0, 0.9)), "char": ("bark", (0.2, 0.17, 0.15)),
       "feather": ("feather", (1, 1, 1)), "pebble": ("boulder", (1, 1, 1)), "hide": ("hide", (1, 1, 1)),
       "fur": ("hide", (0.55, 0.42, 0.3)), "thatch": ("thatch", (1.25, 1.05, 0.75)), "rock": ("boulder", (1, 1, 1))}


def setup_materials(root):
    tdir = os.path.join(root, "Assets", "Resources", "Textures")
    for m in bpy.data.materials:
        key = m.name.split(".")[0]
        if key not in TEX:
            continue
        tex, tint = TEX[key]
        m.use_nodes = True
        nt = m.node_tree
        bsdf = nt.nodes.get("Principled BSDF")
        for ext in (".jpg", ".png"):
            p = os.path.join(tdir, tex + "_diff" + ext)
            if os.path.exists(p):
                img = nt.nodes.new("ShaderNodeTexImage")
                img.image = bpy.data.images.load(p, check_existing=True)
                mix = nt.nodes.new("ShaderNodeMixRGB")
                mix.blend_type = 'MULTIPLY'
                mix.inputs[0].default_value = 1.0
                mix.inputs[2].default_value = (*tint, 1)
                nt.links.new(img.outputs[0], mix.inputs[1])
                nt.links.new(mix.outputs[0], bsdf.inputs["Base Color"])
                break
        bsdf.inputs["Roughness"].default_value = 0.6


def render_setup(res=480):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 24
    sc.cycles.use_denoising = False
    sc.render.resolution_x = res
    sc.render.resolution_y = res
    w = bpy.data.worlds.new("W")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.68, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.7
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(30))
    sc.collection.objects.link(sun)


def frame(obj, view, ortho=True, pad=1.15):
    sc = bpy.context.scene
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (lo + hi) / 2
    size = max((hi - lo).length, 0.05)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    d = Vector(view).normalized()
    cam.location = c + d * size * (3 if ortho else 1.05)
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    if ortho:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = size * pad
    else:
        cam.data.lens = 35
    cam.data.clip_end = size * 20
    sc.camera = cam


def weapons_sheet(models, out_png, root):
    tmp = os.path.splitext(out_png)[0] + "_parts"
    os.makedirs(tmp, exist_ok=True)
    for name, fn in models.items():
        mk.reset()
        b = mk.Builder()
        fn(b)
        obj = b.build(name)
        setup_materials(root)
        render_setup(420)
        frame(obj, (1, -0.55, 0.35))
        bpy.context.scene.render.filepath = os.path.join(tmp, name + ".png")
        bpy.ops.render.render(write_still=True)
    print("PREVIEW_PARTS", tmp)


def single(obj, out_png, root, view=(1, -1, 0.6), res=640, ortho=False):
    setup_materials(root)
    render_setup(res)
    frame(obj, view, ortho=ortho, pad=1.1)
    bpy.context.scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)
