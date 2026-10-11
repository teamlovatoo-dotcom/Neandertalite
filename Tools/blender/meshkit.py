"""Tiny procedural modelling kit for Blender (bmesh-free): tubes swept along paths, knapped stone bifaces,
helical wraps, lofted sheets. Every generator writes its own UVs in metres so tiling textures line up.
Shared by make_weapons.py and make_structures.py."""
import bpy, math, random
from mathutils import Vector, Matrix, noise


class Builder:
    """Accumulates geometry per material and turns it into one multi-material mesh object."""

    def __init__(self):
        self.parts = {}   # mat -> [verts, faces, uvs(per face list of uv tuples)]

    def part(self, mat):
        if mat not in self.parts:
            self.parts[mat] = [[], [], []]
        return self.parts[mat]

    def add(self, mat, verts, faces, uvs):
        p = self.part(mat)
        off = len(p[0])
        p[0].extend(verts)
        for f, uv in zip(faces, uvs):
            p[1].append([i + off for i in f])
            p[2].append(uv)

    def build(self, name, smooth=True):
        verts, faces, uvs, mats = [], [], [], []
        mat_names = list(self.parts.keys())
        for mi, m in enumerate(mat_names):
            v, f, uv = self.parts[m]
            off = len(verts)
            verts.extend([tuple(x) for x in v])
            for face, fuv in zip(f, uv):
                faces.append([i + off for i in face])
                uvs.append(fuv)
                mats.append(mi)
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(verts, [], faces)
        mesh.validate(clean_customdata=False)
        uvl = mesh.uv_layers.new(name="UVMap")
        for poly, fuv, mi in zip(mesh.polygons, uvs, mats):
            poly.material_index = mi
            poly.use_smooth = smooth
            for li, uv in zip(poly.loop_indices, fuv):
                uvl.data[li].uv = uv
        for m in mat_names:
            mat = bpy.data.materials.get(m) or bpy.data.materials.new(m)
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        # merge seams of degenerate tips etc.
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.remove_doubles(threshold=0.00002)
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        obj.select_set(False)
        return obj


def frames(path):
    """Parallel-transport frames along a polyline: list of (tangent, normal, binormal)."""
    out = []
    t0 = (path[1] - path[0]).normalized()
    ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
    n = t0.cross(ref).normalized()
    for i in range(len(path)):
        if i == 0:
            t = t0
        elif i == len(path) - 1:
            t = (path[i] - path[i - 1]).normalized()
        else:
            t = (path[i + 1] - path[i - 1]).normalized()
        n = (n - t * n.dot(t)).normalized()
        out.append((t, n, t.cross(n)))
    return out


def tube(b, mat, path, radii, segs=10, tex=0.5, caps=True, rough=0.0, seed=0, side=None, ellipse=None, cap_mat=None):
    """Sweep a (possibly elliptical) ring along path. radii: list or float. side(i)->Vector forces the ring's
    first axis (used for flat wraps). ellipse: list of (sx, sy) multipliers per point."""
    path = [Vector(p) for p in path]
    if not isinstance(radii, (list, tuple)):
        radii = [radii] * len(path)
    fr = frames(path)
    verts, faces, uvs = [], [], []
    L = 0.0
    lengths = [0.0]
    for i in range(1, len(path)):
        L += (path[i] - path[i - 1]).length
        lengths.append(L)
    circ = 2 * math.pi * max(radii)
    for i, p in enumerate(path):
        t, n, bn = fr[i]
        if side is not None:
            a = side(i)
            a = (a - t * a.dot(t)).normalized()
            n, bn = a, t.cross(a)
        sx, sy = ellipse[i] if ellipse else (1.0, 1.0)
        for j in range(segs):
            ang = 2 * math.pi * j / segs
            r = radii[i]
            if rough > 0:
                r *= 1 + rough * noise.noise(Vector((math.cos(ang) * 2.1 + seed, math.sin(ang) * 2.1, lengths[i] * 9.0)))
            verts.append(p + n * math.cos(ang) * r * sx + bn * math.sin(ang) * r * sy)
    for i in range(len(path) - 1):
        for j in range(segs):
            a, bq = i * segs + j, i * segs + (j + 1) % segs
            c, d = (i + 1) * segs + (j + 1) % segs, (i + 1) * segs + j
            faces.append([a, bq, c, d])
            u0, u1 = j / segs * circ / tex, (j + 1) / segs * circ / tex
            v0, v1 = lengths[i] / tex, lengths[i + 1] / tex
            uvs.append([(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
    cap_faces, cap_uvs = [], []
    if caps:
        for end, idx in ((0, 0), (1, len(path) - 1)):
            ring = [idx * segs + j for j in range(segs)]
            if end == 0:
                ring = ring[::-1]
            c = path[idx]
            cap_faces.append(ring)
            cap_uvs.append([((verts[k] - c).dot(fr[idx][1]) / tex + 0.5, (verts[k] - c).dot(fr[idx][2]) / tex + 0.5) for k in ring])
    if cap_mat is None:
        b.add(mat, verts, faces + cap_faces, uvs + cap_uvs)
    else:
        b.add(mat, verts, faces, uvs)
        if cap_faces:
            used = sorted({k for f in cap_faces for k in f})
            remap = {k: i for i, k in enumerate(used)}
            b.add(cap_mat, [verts[k] for k in used], [[remap[k] for k in f] for f in cap_faces], cap_uvs)


def smooth_noise(x, seed):
    return noise.noise(Vector((x, seed * 1.37, 0.5)))


def wobble_path(z0, z1, n, amp, freq, seed, axis=2):
    pts = []
    for i in range(n + 1):
        z = z0 + (z1 - z0) * i / n
        dx = amp * smooth_noise(z * freq, seed)
        dy = amp * smooth_noise(z * freq + 17.3, seed)
        pts.append(Vector((dx, dy, z)))
    return pts


def helix(b, mat, radius, z0, z1, pitch, width, thick, seed=0, per_turn=20, wobble=0.0, tex=0.1, center=None, flat=True):
    """Leather strip (flat=True) or sinew cord wrapped around the Z axis."""
    c = center or (lambda z: Vector((0, 0, 0)))
    turns = (z1 - z0) / pitch
    n = max(4, int(turns * per_turn))
    pts, sides = [], []
    for i in range(n + 1):
        f = i / n
        ang = f * turns * 2 * math.pi + seed
        z = z0 + (z1 - z0) * f
        r = radius + wobble * smooth_noise(f * 30, seed)
        radial = Vector((math.cos(ang), math.sin(ang), 0))
        pts.append(c(z) + radial * r + Vector((0, 0, z)))
        sides.append(radial)
    if flat:
        tube(b, mat, pts, [1.0] * len(pts), segs=8, tex=tex, caps=True, side=lambda i: sides[i],
             ellipse=[(thick, width * 0.5)] * len(pts))
    else:
        tube(b, mat, pts, thick, segs=4 if per_turn < 10 else 5, tex=tex, caps=True)


def biface(b, mat, L, W, T, shape, thick_prof=None, nu=18, nv=40, scars=28, seed=0, xf=None, tex=0.08, edge_jitter=0.06):
    """Knapped stone blade lying in the XZ plane (length +Z, thickness Y) with flake scars on both faces."""
    rnd = random.Random(seed)
    thick_prof = thick_prof or (lambda v: 1.0)
    xf = xf or Matrix.Identity(4)
    sc = []
    for s in range(scars):
        side = 1 if s % 2 == 0 else -1
        v = rnd.uniform(0.05, 0.97)
        edge = rnd.choice((-1, 1))
        u = edge * rnd.uniform(0.35, 0.95)
        r = W * rnd.uniform(0.35, 0.75) * max(0.35, shape(v))
        d = T * rnd.uniform(0.25, 0.5) * thick_prof(v)
        sc.append((side, u, v, r, d))
    jit = [1 + edge_jitter * (rnd.random() - 0.5) * 2 for _ in range(nv + 1)]
    verts, faces, uvs = [], [], []
    idx = {}

    def vid(side, i, j):
        key = (0 if j in (0, nu) else side, i, j)
        if key in idx:
            return idx[key]
        v = i / nv
        u = j / nu * 2 - 1
        w = W * shape(v) * jit[i]
        x = u * w
        z = v * L
        y = side * T * (max(0.0, 1 - u * u) ** 0.55) * thick_prof(v)
        if j not in (0, nu):
            for (ss, su, sv, r, d) in sc:
                if ss != side:
                    continue
                dx, dz = x - su * W * shape(sv), z - sv * L
                dd = math.sqrt(dx * dx + dz * dz)
                if dd < r:
                    y -= side * d * (1 - (dd / r) ** 2) ** 2
            if side * y < T * 0.08 * (1 - u * u):
                y = side * T * 0.08 * (1 - u * u)
        p = xf @ Vector((x, y, z))
        idx[key] = len(verts)
        verts.append(p)
        return idx[key]

    for side in (1, -1):
        for i in range(nv):
            for j in range(nu):
                a, bq, c, d = vid(side, i, j), vid(side, i, j + 1), vid(side, i + 1, j + 1), vid(side, i + 1, j)
                f = [a, bq, c, d] if side == 1 else [d, c, bq, a]
                faces.append(f)
                uvs.append([((verts[k].x) / tex, verts[k].z / tex) for k in f])
    # base cap
    ring = [vid(1, 0, j) for j in range(nu + 1)] + [vid(-1, 0, j) for j in range(nu - 1, 0, -1)]
    faces.append(ring[::-1])
    uvs.append([(verts[k].x / tex, verts[k].y / tex) for k in ring[::-1]])
    b.add(mat, verts, faces, uvs)


def normalized_shape(f, samples=200):
    m = max(f(i / samples) for i in range(samples + 1))
    return lambda v: max(0.0, f(v) / m)


def lumpy_sphere(b, mat, center, radius, scale=(1, 1, 1), sub=3, amp=0.18, seed=0, tex=0.3):
    """Icosphere displaced by noise (pebbles, knobs, skull bumps)."""
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    verts = []
    for v in bm.verts:
        p = v.co.copy()
        k = 1 + amp * noise.noise(p * 1.7 + Vector((seed, seed * 0.3, 0)))
        verts.append(Vector(center) + Vector((p.x * scale[0], p.y * scale[1], p.z * scale[2])) * radius * k)
    faces = [[v.index for v in f.verts] for f in bm.faces]
    uvs = []
    for f in faces:
        uvs.append([((verts[k].x + verts[k].y * 0.7) / tex, verts[k].z / tex) for k in f])
    bm.free()
    b.add(mat, verts, faces, uvs)


def empty(name, loc):
    e = bpy.data.objects.new(name, None)
    e.location = loc
    bpy.context.scene.collection.objects.link(e)
    return e


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export(path, extra_types=('EMPTY',)):
    bpy.ops.export_scene.fbx(filepath=path, use_selection=False, object_types={'MESH', *extra_types},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False, add_leaf_bones=False)


def tri_count():
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')


def sheet(b, mat, fn, nu, nv, tex=1.0, skip=None):
    """Grid surface: fn(u, v) -> (Vector position, (uv_u, uv_v) in metres). skip(u, v) drops a face."""
    verts, uvm = [], []
    for j in range(nv + 1):
        for i in range(nu + 1):
            p, uv = fn(i / nu, j / nv)
            verts.append(Vector(p))
            uvm.append(uv)
    faces, uvs = [], []
    for j in range(nv):
        for i in range(nu):
            if skip and skip((i + 0.5) / nu, (j + 0.5) / nv):
                continue
            f = [j * (nu + 1) + i, j * (nu + 1) + i + 1, (j + 1) * (nu + 1) + i + 1, (j + 1) * (nu + 1) + i]
            faces.append(f)
            uvs.append([(uvm[k][0] / tex, uvm[k][1] / tex) for k in f])
    # drop unused verts is unnecessary: remove_doubles + validate keep them harmless
    b.add(mat, verts, faces, uvs)


def solidify(obj, thickness):
    bpy.context.view_layer.objects.active = obj
    m = obj.modifiers.new("Solid", "SOLIDIFY")
    m.thickness = thickness
    m.offset = 0
    bpy.ops.object.modifier_apply(modifier=m.name)


def join_all(name):
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    # strip loose verts left by skipped sheet faces
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.mesh.select_loose()
    bpy.ops.mesh.delete(type='VERT')
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)
    return obj
