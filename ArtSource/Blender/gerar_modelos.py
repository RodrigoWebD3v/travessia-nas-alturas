# Travessia nas Alturas - geracao dos modelos 3D no Blender.
# Uso: blender --background --python gerar_modelos.py
#
# Cada modelo e montado a partir de partes simples. Cada parte recebe UV
# (Smart UV Project ou projecao planar) remapeado para uma celula de um
# atlas de textura 512x512 pintado proceduralmente. As partes sao unidas
# em uma malha, exportadas em FBX para a Unity e o .blend e salvo como fonte.

import bpy
import bmesh
import math
import os
import numpy as np
from mathutils import Vector

PROJECT = r"C:\Users\rodri\TrabalhoKinho"
MODELS_DIR = os.path.join(PROJECT, "Assets", "Art", "Models")
TEX_DIR = os.path.join(PROJECT, "Assets", "Art", "Textures")
SRC_DIR = os.path.join(PROJECT, "ArtSource", "Blender")
PREVIEW_DIR = os.path.join(PROJECT, "ArtSource", "Previews")
for d in (MODELS_DIR, TEX_DIR, SRC_DIR, PREVIEW_DIR):
    os.makedirs(d, exist_ok=True)

ATLAS = 512
GRID = 4
CELL = ATLAS // GRID
rng = np.random.default_rng(7)


# ---------------------------------------------------------------- pintura

def hex_rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def cell_coords():
    y, x = np.mgrid[0:CELL, 0:CELL]
    return (x + 0.5) / CELL, (y + 0.5) / CELL  # 0..1, origem embaixo


def noise(amount, scale=1):
    n = rng.random((CELL // scale + 1, CELL // scale + 1))
    n = np.kron(n, np.ones((scale, scale)))[:CELL, :CELL]
    return (n - 0.5) * 2 * amount


def solid(color, amount=0.04, scale=4):
    c = np.ones((CELL, CELL, 3)) * hex_rgb(color)
    return c + noise(amount, scale)[..., None]


def stripes(c1, c2, count=6, vertical=False):
    u, v = cell_coords()
    t = u if vertical else v
    mask = (np.floor(t * count) % 2 == 0)[..., None]
    return np.where(mask, solid(c1, 0.02), solid(c2, 0.02))


def poly_mask(points):
    u, v = cell_coords()
    inside = np.zeros(u.shape, bool)
    n = len(points)
    for i in range(n):
        x1, y1 = points[i]
        x2, y2 = points[(i + 1) % n]
        cond = ((y1 > v) != (y2 > v))
        xint = (x2 - x1) * (v - y1) / (y2 - y1 + 1e-9) + x1
        inside ^= cond & (u < xint)
    return inside


def star_points(cx, cy, r_out, r_in, n=5):
    pts = []
    for i in range(n * 2):
        a = math.pi / 2 + i * math.pi / n
        r = r_out if i % 2 == 0 else r_in
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def radial(c_in, c_out):
    u, v = cell_coords()
    d = np.clip(np.sqrt((u - 0.5) ** 2 + (v - 0.5) ** 2) / 0.7, 0, 1)[..., None]
    return hex_rgb(c_in) * (1 - d) + hex_rgb(c_out) * d


def coin_face():
    u, v = cell_coords()
    img = radial("#FFE680", "#E0A800")
    d = np.sqrt((u - 0.5) ** 2 + (v - 0.5) ** 2)
    ring = ((d > 0.38) & (d < 0.44))[..., None]
    img = np.where(ring, hex_rgb("#C98A00"), img)
    star = poly_mask(star_points(0.5, 0.5, 0.3, 0.13))[..., None]
    img = np.where(star, hex_rgb("#FFF6C8"), img)
    return img


def heart_face():
    u, v = cell_coords()
    img = radial("#FF6B7A", "#B0182C")
    hl = (((u - 0.33) / 0.12) ** 2 + ((v - 0.68) / 0.07) ** 2 < 1)[..., None]
    return np.where(hl, hex_rgb("#FFD6DC"), img)


def bricks(mortar, brick, brick2, rows=6, cols=3):
    u, v = cell_coords()
    row = np.floor(v * rows)
    off = (row % 2) * 0.5
    bu = (u * cols + off) % 1.0
    bv = (v * rows) % 1.0
    is_mortar = ((bu < 0.06) | (bv < 0.1))[..., None]
    brick_id = np.floor(u * cols + off) + row * 7
    tint = (np.sin(brick_id * 12.9898) * 43758.5453 % 1.0)[..., None]
    base = hex_rgb(brick) * (1 - tint) + hex_rgb(brick2) * tint
    base = base + noise(0.04, 2)[..., None]
    return np.where(is_mortar, hex_rgb(mortar), base)


def roof_tiles():
    u, v = cell_coords()
    rows = 6
    row = np.floor(v * rows)
    off = (row % 2) * 0.5
    bu = (u * 5 + off) % 1.0
    bv = (v * rows) % 1.0
    curve = bv < 0.25 + 0.2 * np.abs(np.sin(bu * math.pi))
    img = solid("#C8463A", 0.03)
    return np.where((curve & (bv < 0.12))[..., None], hex_rgb("#7E2620"), img)


def leaves():
    img = solid("#63B75D", 0.05, 2)
    for _ in range(90):
        cx, cy = rng.random(2)
        r = rng.uniform(0.04, 0.09)
        u, v = cell_coords()
        m = (((u - cx) ** 2 + (v - cy) ** 2) < r * r)[..., None]
        col = hex_rgb("#4C9A47") if rng.random() < 0.6 else hex_rgb("#86D07A")
        img = np.where(m, col, img)
    return img


def bark():
    u, v = cell_coords()
    streak = np.sin(u * 40 + np.sin(v * 9) * 2) * 0.06
    return solid("#8C5A36", 0.03)[...] + streak[..., None]


def wood():
    u, v = cell_coords()
    grain = np.sin(v * 60 + np.sin(u * 12) * 3) * 0.05
    return solid("#B07A45", 0.02) + grain[..., None]


def clock_face():
    u, v = cell_coords()
    d = np.sqrt((u - 0.5) ** 2 + (v - 0.5) ** 2)
    img = np.where((d < 0.42)[..., None], hex_rgb("#FFFFFF"), hex_rgb("#2F7FD8"))
    ang = np.arctan2(v - 0.5, u - 0.5)
    ticks = (d > 0.33) & (d < 0.40) & (np.abs(((ang / (2 * math.pi) * 12) % 1) - 0.5) > 0.42)
    img = np.where(ticks[..., None], hex_rgb("#22324A"), img)
    hand1 = (np.abs(u - 0.5) < 0.02) & (v > 0.5) & (v < 0.8)
    hand2 = (np.abs(v - 0.5) < 0.02) & (u > 0.5) & (u < 0.72)
    hub = d < 0.04
    return np.where((hand1 | hand2 | hub)[..., None], hex_rgb("#E75050"), img)


def flag_cloth():
    u, v = cell_coords()
    img = solid("#FFFFFF", 0.02)
    star = poly_mask(star_points(0.5, 0.5, 0.32, 0.14))[..., None]
    return np.where(star, hex_rgb("#FFD447"), img * 0 + hex_rgb("#3FAE49"))


def face_cell():
    """Pele com olhos, usada pela projecao frontal da cabeca."""
    u, v = cell_coords()
    img = solid("#F7C9A0", 0.02)
    for ex in (0.36, 0.64):
        white = (((u - ex) / 0.07) ** 2 + ((v - 0.55) / 0.1) ** 2) < 1
        pupil = (((u - ex) / 0.035) ** 2 + ((v - 0.53) / 0.06) ** 2) < 1
        img = np.where(white[..., None], hex_rgb("#FFFFFF"), img)
        img = np.where(pupil[..., None], hex_rgb("#1E2A44"), img)
    smile = (np.abs(np.sqrt((u - 0.5) ** 2 + (v - 0.42) ** 2) - 0.13) < 0.015) & (v < 0.36)
    blush = ((((u - 0.25) / 0.06) ** 2 + ((v - 0.4) / 0.04) ** 2) < 1) | \
            ((((u - 0.75) / 0.06) ** 2 + ((v - 0.4) / 0.04) ** 2) < 1)
    img = np.where(blush[..., None], hex_rgb("#F29A8A"), img)
    return np.where(smile[..., None], hex_rgb("#9B3A2E"), img)


def cap_cell():
    u, v = cell_coords()
    img = solid("#D63C3C", 0.03)
    d = ((u - 0.5) / 0.22) ** 2 + ((v - 0.5) / 0.3) ** 2 < 1
    img = np.where(d[..., None], hex_rgb("#FFFFFF"), img)
    star = poly_mask(star_points(0.5, 0.5, 0.2, 0.09))[..., None]
    return np.where(star & d[..., None], hex_rgb("#FFD447"), img)


def slime_cell():
    img = solid("#8E4FD0", 0.05, 4)
    for _ in range(25):
        cx, cy = rng.random(2)
        r = rng.uniform(0.03, 0.07)
        u, v = cell_coords()
        m = (((u - cx) ** 2 + (v - cy) ** 2) < r * r)[..., None]
        img = np.where(m, hex_rgb("#C48BFF"), img)
    return img


def window_cell():
    u, v = cell_coords()
    img = bricks("#B8B0A0", "#E2D8C4", "#CFC3AA")
    arch = ((np.abs(u - 0.5) < 0.22) & (v < 0.62) & (v > 0.12)) | \
           ((((u - 0.5) / 0.22) ** 2 + ((v - 0.62) / 0.2) ** 2) < 1)
    return np.where(arch[..., None], hex_rgb("#2C3550"), img)


def build_atlas(name, cells):
    """cells: dict nome -> (indice, funcao). Salva PNG e devolve imagem Blender."""
    arr = np.zeros((ATLAS, ATLAS, 3))
    arr[...] = 0.5
    for _, (idx, painter) in cells.items():
        cx, cy = idx % GRID, idx // GRID
        arr[cy * CELL:(cy + 1) * CELL, cx * CELL:(cx + 1) * CELL] = np.clip(painter(), 0, 1)
    rgba = np.concatenate([arr, np.ones((ATLAS, ATLAS, 1))], axis=2)
    img = bpy.data.images.new(name, ATLAS, ATLAS, alpha=True)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    path = os.path.join(TEX_DIR, name + ".png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    img.pack()
    return img


def save_tile(name, painter_arr):
    """Textura 256x256 repetivel para plataformas."""
    size = painter_arr.shape[0]
    rgba = np.concatenate([np.clip(painter_arr, 0, 1), np.ones((size, size, 1))], axis=2)
    img = bpy.data.images.new(name, size, size, alpha=True)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(TEX_DIR, name + ".png")
    img.file_format = "PNG"
    img.save()


def tile_noise(size, amount, blob):
    n = rng.random((size // blob, size // blob))
    n = np.kron(n, np.ones((blob, blob)))
    return (n - 0.5) * 2 * amount


def grass_tile():
    s = 256
    base = np.ones((s, s, 3)) * hex_rgb("#63B75D")
    base += tile_noise(s, 0.05, 4)[..., None] + tile_noise(s, 0.03, 16)[..., None]
    for _ in range(500):
        x, y = rng.integers(0, s, 2)
        h = rng.integers(3, 8)
        col = hex_rgb("#86D07A") if rng.random() < 0.5 else hex_rgb("#4C9A47")
        base[y:y + h, x:x + 1] = col
    return base


def dirt_tile():
    s = 256
    base = np.ones((s, s, 3)) * hex_rgb("#8C5A36")
    base += tile_noise(s, 0.05, 4)[..., None]
    yy, xx = np.mgrid[0:s, 0:s]
    for _ in range(60):
        cx, cy = rng.integers(0, s, 2)
        r = rng.integers(3, 8)
        m = ((xx - cx) ** 2 + (yy - cy) ** 2) < r * r
        base[m] = hex_rgb("#A8784E") if rng.random() < 0.5 else hex_rgb("#6E4428")
    band = (yy > s - 26)
    base[band] = base[band] * 0.4 + hex_rgb("#63B75D") * 0.6
    return base


def stone_tile():
    s = 256
    global CELL
    old = CELL
    CELL = s
    arr = bricks("#9C9486", "#D8CFBE", "#C2B69F", rows=8, cols=4)
    CELL = old
    return arr


# ---------------------------------------------------------------- malhas

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def finish_part(obj, cell, uv="smart", bevel=0.0, smooth=True, axes=("x", "z")):
    """Aplica transformacoes, bevel, UV e remapeia o UV para a celula do atlas."""
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=mod.name)

    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")

    if uv == "smart":
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.03)
        bpy.ops.object.mode_set(mode="OBJECT")
    else:  # planar: projeta as coordenadas do objeto no plano escolhido
        ia = "xyz".index(axes[0])
        ib = "xyz".index(axes[1])
        verts = [v.co for v in me.vertices]
        amin = min(v[ia] for v in verts); amax = max(v[ia] for v in verts)
        bmin = min(v[ib] for v in verts); bmax = max(v[ib] for v in verts)
        layer = me.uv_layers.active.data
        for loop in me.loops:
            co = me.vertices[loop.vertex_index].co
            u = (co[ia] - amin) / max(amax - amin, 1e-6)
            v = (co[ib] - bmin) / max(bmax - bmin, 1e-6)
            if uv == "planar_flip":
                u = 1 - u
            layer[loop.index].uv = (u, v)

    margin = 3 / ATLAS
    cx, cy = cell % GRID, cell // GRID
    size = 1 / GRID
    for d in me.uv_layers.active.data:
        u, v = d.uv
        d.uv = (cx * size + margin + u * (size - 2 * margin),
                cy * size + margin + v * (size - 2 * margin))

    if smooth:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    else:
        bpy.ops.object.shade_flat()
    return obj


def cube(name, size, loc, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    return o


def cyl(name, r, depth, loc, rot=(0, 0, 0), verts=24, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    return o


def sphere(name, r, loc, scale=(1, 1, 1), seg=24, ico=False):
    if ico:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=r, location=loc)
    else:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=r, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    return o


def torus(name, major, minor, loc, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, location=loc, rotation=rot,
                                     major_segments=32, minor_segments=8)
    o = bpy.context.active_object
    o.name = name
    return o


def extruded_outline(name, pts2d, depth):
    """Cria um prisma a partir de um contorno 2D no plano XZ, extrudado em Y."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    front = [bm.verts.new((x, -depth / 2, z)) for x, z in pts2d]
    back = [bm.verts.new((x, depth / 2, z)) for x, z in pts2d]
    bm.faces.new(front[::-1])
    bm.faces.new(back)
    n = len(pts2d)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    return o


def make_material(name, image):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Linear"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.7
    return mat


def assemble(name, parts, image, origin="bottom"):
    activate(parts[0])
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name + "_Mesh"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # origem: base (centro inferior) ou centro do volume
    bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    minz = min(v.z for v in bb)
    cx = (min(v.x for v in bb) + max(v.x for v in bb)) / 2
    cy = (min(v.y for v in bb) + max(v.y for v in bb)) / 2
    cz = (minz + max(v.z for v in bb)) / 2
    target = Vector((cx, cy, minz if origin == "bottom" else cz))
    if origin == "center_keep":  # mantem a origem do desenho (centro da vara nas maos)
        target = Vector((0, 0, 0))
    obj.data.transform(__import__("mathutils").Matrix.Translation(-target))
    obj.location = (0, 0, 0)
    obj.data.materials.clear()
    obj.data.materials.append(make_material("M_" + name, image))
    return obj


def export(obj):
    activate(obj)
    path = os.path.join(MODELS_DIR, obj.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
        use_mesh_modifiers=True, path_mode="STRIP", add_leaf_bones=False)
    print("Exportado:", path)


# ---------------------------------------------------------------- modelos
# Convencao: frente do modelo voltada para -Y no Blender (+Z na Unity).

def capsule(name, p1, p2, r, verts=16):
    """Cilindro entre dois pontos com esferas nas pontas (membro arredondado)."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    mid = (p1 + p2) / 2
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d.length, location=mid)
    o = bpy.context.active_object
    o.name = name
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d.normalized())
    parts = [o, sphere(name + "_A", r, p1, seg=16), sphere(name + "_B", r, p2, seg=16)]
    return parts


def smooth_union(name, objs, voxel=0.011, smooth_iter=6, decimate=0.35):
    """Funde as partes num corpo unico e liso (remesh por voxels + suavizacao)."""
    activate(objs[0])
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    rem = obj.modifiers.new("Remesh", "REMESH")
    rem.mode = "VOXEL"
    rem.voxel_size = voxel
    bpy.ops.object.modifier_apply(modifier=rem.name)
    sm = obj.modifiers.new("Smooth", "SMOOTH")
    sm.factor = 0.8
    sm.iterations = smooth_iter
    bpy.ops.object.modifier_apply(modifier=sm.name)
    dec = obj.modifiers.new("Decimate", "DECIMATE")
    dec.ratio = decimate
    bpy.ops.object.modifier_apply(modifier=dec.name)
    return obj


def mannequin_cell():
    u, v = cell_coords()
    shade = 0.03 * np.sin(u * 6.0) * np.sin(v * 5.0)
    return solid("#F4F4F2", 0.012, 8) + shade[..., None]


def finish_custom(obj, name, image, pivot, cell=0):
    """UV, material e origem num ponto escolhido (pivo de rotacao)."""
    finish_part(obj, cell)
    obj.data.transform(__import__("mathutils").Matrix.Translation(-Vector(pivot)))
    obj.location = (0, 0, 0)
    obj.name = name
    obj.data.name = name + "_Mesh"
    obj.data.materials.clear()
    obj.data.materials.append(make_material("M_" + name, image))
    return obj


# Pose do manequim (frente para -Y). Pivo dos bracos no centro dos ombros.
SHOULDER_PIVOT = (0.0, 0.0, 1.25)
POLE_CENTER = (0.0, -0.34, 1.0)


def model_equilibrista():
    """Corpo de manequim liso: cabeca, tronco, quadril e pernas em passada sobre a corda."""
    img = build_atlas("T_Equilibrista", {"pele": (0, mannequin_cell)})
    parts = []
    parts.append(sphere("Cabeca", 0.155, (0, 0, 1.49), scale=(1, 1, 1.08), seg=32))
    parts += capsule("Pescoco", (0, 0, 1.3), (0, 0, 1.38), 0.055)
    parts.append(sphere("Tronco", 0.2, (0, 0, 1.06), scale=(1.0, 0.72, 1.32), seg=32))
    parts.append(sphere("Quadril", 0.16, (0, 0, 0.8), scale=(1.05, 0.8, 0.72), seg=32))
    for sx in (-1, 1):
        parts.append(sphere("Ombro", 0.068, (sx * 0.19, 0, 1.22), seg=16))
    # passada: perna esquerda a frente, direita atras, pes quase alinhados na corda
    for sx, step in ((-1, -1), (1, 1)):
        hip = (sx * 0.085, 0, 0.78)
        knee = (sx * 0.065, step * 0.07, 0.43)
        ankle = (sx * 0.045, step * 0.13, 0.08)
        parts += capsule("Coxa", hip, knee, 0.072)
        parts += capsule("Canela", knee, ankle, 0.056)
        parts.append(sphere("Pe", 0.06, (ankle[0], ankle[1] - 0.05, 0.045), scale=(1, 1.9, 0.75), seg=16))
    body = smooth_union("Equilibrista", parts)
    return finish_custom(body, "Equilibrista", img, (0, 0, 0))


def model_bracos():
    """Bracos segurando a vara; giram em torno do pivo dos ombros junto com ela."""
    img = build_atlas("T_Bracos", {"pele": (0, mannequin_cell)})
    parts = []
    for sx in (-1, 1):
        shoulder = (sx * 0.19, 0, 1.22)
        elbow = (sx * 0.27, -0.13, 1.04)
        hand = (sx * 0.24, -0.33, 1.02)
        parts.append(sphere("Ombro", 0.058, shoulder, seg=16))
        parts += capsule("Braco", shoulder, elbow, 0.05)
        parts += capsule("Antebraco", elbow, hand, 0.043)
        parts.append(sphere("Mao", 0.055, (hand[0], hand[1] - 0.01, hand[2]), scale=(1.1, 1, 0.9), seg=16))
    arms = smooth_union("Equilibrista_Bracos", parts, voxel=0.009, smooth_iter=5, decimate=0.4)
    return finish_custom(arms, "Equilibrista_Bracos", img, SHOULDER_PIVOT)


def model_equilibrista_antigo():
    cells = {
        "pele": (0, lambda: solid("#F7C9A0", 0.02)),
        "rosto": (1, face_cell),
        "camisa": (2, lambda: stripes("#FFFFFF", "#E75050", 7)),
        "calca": (3, lambda: solid("#2F5BD3", 0.04)),
        "sapato": (4, lambda: solid("#6B3E22", 0.03)),
        "cinto": (5, lambda: stripes("#3A2A1E", "#FFD447", 2, vertical=True)),
        "bone": (6, cap_cell),
        "cabelo": (7, lambda: solid("#5A3520", 0.05, 2)),
        "luva": (8, lambda: solid("#FFFFFF", 0.02)),
    }
    img = build_atlas("T_Equilibrista", cells)
    parts = []
    for sx in (-1, 1):
        parts.append(finish_part(cyl("Perna", 0.085, 0.5, (sx * 0.11, 0, 0.3)), 3))
        parts.append(finish_part(cube("Sapato", (0.15, 0.26, 0.11), (sx * 0.11, -0.04, 0.055)), 4, bevel=0.03))
        # bracos estendidos para frente segurando o bastao
        arm = cyl("Braco", 0.065, 0.34, (sx * 0.26, -0.15, 0.88), rot=(math.radians(80), 0, 0))
        parts.append(finish_part(arm, 2))
        parts.append(finish_part(sphere("Mao", 0.075, (sx * 0.26, -0.33, 0.85)), 8))
        parts.append(finish_part(sphere("Ombro", 0.09, (sx * 0.24, 0, 0.9)), 2))
        parts.append(finish_part(sphere("Orelha", 0.04, (sx * 0.2, 0.02, 1.17)), 0))
    torso = cyl("Tronco", 0.22, 0.46, (0, 0, 0.77), r2=0.2)
    parts.append(finish_part(torso, 2))
    parts.append(finish_part(cyl("Cinto", 0.225, 0.07, (0, 0, 0.56)), 5))
    parts.append(finish_part(cyl("Quadril", 0.21, 0.12, (0, 0, 0.5)), 3))
    parts.append(finish_part(cyl("Pescoco", 0.08, 0.1, (0, 0, 1.02)), 0))
    head = sphere("Cabeca", 0.2, (0, 0, 1.18), seg=32)
    parts.append(finish_part(head, 1, uv="planar_flip", axes=("x", "z")))
    parts.append(finish_part(sphere("Nariz", 0.045, (0, -0.2, 1.16)), 0))
    parts.append(finish_part(sphere("Cabelo", 0.205, (0, 0.03, 1.21), scale=(1, 1, 0.9)), 7))
    cap = sphere("Bone", 0.215, (0, 0.0, 1.27), scale=(1, 1.02, 0.62), seg=32)
    parts.append(finish_part(cap, 6, uv="planar_flip", axes=("x", "z")))
    brim = cyl("Aba", 0.16, 0.03, (0, -0.19, 1.25), rot=(math.radians(-8), 0, 0), r2=0.16)
    brim.scale = (1.2, 1, 1)
    parts.append(finish_part(brim, 6))
    return assemble("Equilibrista", parts, img)


POLE_LENGTH = 4.2
POLE_DROOP = 0.16


def pole_droop(x):
    """Curvatura da vara: as pontas pesadas caem um pouco."""
    return -POLE_DROOP * (x / (POLE_LENGTH / 2)) ** 2


def model_bastao():
    """Vara longa e laranja, levemente curvada, com pesos nas pontas."""
    cells = {"vara": (0, lambda: solid("#F28C28", 0.02, 8)),
             "peso": (1, lambda: solid("#B85A12", 0.03, 4))}
    img = build_atlas("T_Bastao", cells)
    rings, sides, r = 60, 10, 0.022
    me = bpy.data.meshes.new("Vara")
    bm = bmesh.new()
    grid = []
    for i in range(rings + 1):
        x = -POLE_LENGTH / 2 + POLE_LENGTH * i / rings
        row = []
        for j in range(sides):
            a = 2 * math.pi * j / sides
            row.append(bm.verts.new((x, math.cos(a) * r, pole_droop(x) + math.sin(a) * r)))
        grid.append(row)
    for i in range(rings):
        for j in range(sides):
            k = (j + 1) % sides
            bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][k], grid[i][k]))
    bm.to_mesh(me)
    bm.free()
    vara = bpy.data.objects.new("Vara", me)
    bpy.context.collection.objects.link(vara)
    parts = [finish_part(vara, 0)]
    for sx in (-1, 1):
        x = sx * (POLE_LENGTH / 2 + 0.02)
        parts.append(finish_part(sphere("Peso", 0.055, (x, 0, pole_droop(x)), scale=(1.4, 1, 1)), 1))
    return assemble("Bastao", parts, img, origin="center_keep")


def model_bastao_antigo():
    cells = {"madeira": (0, wood), "ponta": (1, lambda: solid("#E75050", 0.03)),
             "faixa": (2, lambda: stripes("#FFD447", "#E75050", 4, vertical=True))}
    img = build_atlas("T_Bastao", cells)
    parts = [finish_part(cyl("Haste", 0.022, 2.4, (0, 0, 0), rot=(0, math.radians(90), 0), verts=12), 0)]
    for sx in (-1, 1):
        parts.append(finish_part(sphere("Peso", 0.07, (sx * 1.22, 0, 0)), 1))
        parts.append(finish_part(cyl("Faixa", 0.03, 0.12, (sx * 0.95, 0, 0), rot=(0, math.radians(90), 0), verts=12), 2))
    return assemble("Bastao", parts, img, origin="center")


def model_moeda():
    cells = {"face": (0, coin_face), "borda": (1, lambda: solid("#D9A400", 0.03))}
    img = build_atlas("T_Moeda", cells)
    body = cyl("Corpo", 0.33, 0.08, (0, 0, 0), rot=(math.radians(90), 0, 0), verts=32)
    parts = [finish_part(body, 0, uv="planar", axes=("x", "z")),
             finish_part(torus("Aro", 0.33, 0.045, (0, 0, 0), rot=(math.radians(90), 0, 0)), 1)]
    return assemble("Moeda", parts, img, origin="center")


def heart_outline(scale=0.022, steps=48):
    pts = []
    for i in range(steps):
        t = i / steps * 2 * math.pi
        x = 16 * math.sin(t) ** 3
        z = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((x * scale, z * scale))
    return pts


def model_coracao():
    cells = {"face": (0, heart_face), "lado": (1, lambda: solid("#C92A3C", 0.03))}
    img = build_atlas("T_Coracao", cells)
    h = extruded_outline("Coracao", heart_outline(), 0.2)
    activate(h)
    mod = h.modifiers.new("Bevel", "BEVEL")
    mod.width = 0.05
    mod.segments = 3
    bpy.ops.object.modifier_apply(modifier=mod.name)
    parts = [finish_part(h, 0, uv="planar", axes=("x", "z"))]
    return assemble("Coracao", parts, img, origin="center")


def model_bota():
    cells = {"couro": (0, lambda: solid("#2F7FD8", 0.04)), "sola": (1, lambda: solid("#3A2A1E", 0.03)),
             "punho": (2, lambda: stripes("#FFFFFF", "#FFD447", 3)), "asa": (3, lambda: solid("#FFFFFF", 0.02)),
             "estrela": (4, lambda: np.where(poly_mask(star_points(0.5, 0.5, 0.4, 0.17))[..., None],
                                            hex_rgb("#FFD447"), solid("#2F7FD8", 0.03)))}
    img = build_atlas("T_Bota", cells)
    parts = [
        finish_part(cube("Cano", (0.24, 0.24, 0.36), (0, 0.04, 0.34)), 0, bevel=0.04),
        finish_part(cube("Pe", (0.24, 0.46, 0.17), (0, -0.07, 0.12)), 0, bevel=0.06),
        finish_part(cube("Sola", (0.27, 0.5, 0.05), (0, -0.07, 0.025)), 1, bevel=0.02),
        finish_part(cube("Punho", (0.29, 0.29, 0.09), (0, 0.04, 0.55)), 2, bevel=0.03),
        finish_part(cube("Emblema", (0.02, 0.16, 0.16), (0.125, 0.03, 0.33)), 4, uv="planar", axes=("y", "z")),
    ]
    for sx in (-1, 1):
        wing = cube("Asa", (0.03, 0.22, 0.12), (sx * 0.15, 0.12, 0.5), rot=(math.radians(-25), 0, 0))
        parts.append(finish_part(wing, 3, bevel=0.012))
        wing2 = cube("Asa2", (0.03, 0.16, 0.09), (sx * 0.15, 0.14, 0.4), rot=(math.radians(-15), 0, 0))
        parts.append(finish_part(wing2, 3, bevel=0.012))
    return assemble("Bota", parts, img)


def model_arvore():
    cells = {"casca": (0, bark), "folhas": (1, leaves), "folhas2": (2, lambda: solid("#4C9A47", 0.05, 2))}
    img = build_atlas("T_Arvore", cells)
    parts = [finish_part(cyl("Tronco", 0.2, 1.3, (0, 0, 0.65), r2=0.12, verts=12), 0)]
    parts.append(finish_part(cyl("Raiz", 0.3, 0.15, (0, 0, 0.07), r2=0.2, verts=12), 0))
    for r, z, idx, off in ((0.8, 1.55, 1, (0, 0)), (0.6, 2.15, 1, (0.1, -0.05)), (0.45, 2.6, 2, (-0.05, 0.05)),
                           (0.45, 1.5, 2, (0.55, 0.1)), (0.42, 1.6, 2, (-0.5, -0.1))):
        parts.append(finish_part(sphere("Copa", r, (off[0], off[1], z), ico=True), idx))
    return assemble("Arvore", parts, img)


def model_torre():
    cells = {"pedra": (0, lambda: bricks("#9C9486", "#D8CFBE", "#C2B69F", rows=10, cols=4)),
             "telhado": (1, roof_tiles), "janela": (2, window_cell),
             "madeira": (3, wood), "bandeira": (4, lambda: solid("#E75050", 0.02)),
             "faixa": (5, lambda: bricks("#7E7568", "#B9AE98", "#A79A82", rows=3, cols=6))}
    img = build_atlas("T_Torre", cells)
    parts = [finish_part(cyl("Corpo", 1.0, 4.0, (0, 0, 2.0), verts=24, r2=0.9), 0)]
    parts.append(finish_part(cyl("Base", 1.15, 0.4, (0, 0, 0.2), verts=24), 5))
    parts.append(finish_part(cyl("Anel", 1.1, 0.45, (0, 0, 4.2), verts=24), 5))
    for i in range(10):
        a = i / 10 * 2 * math.pi
        parts.append(finish_part(cube("Ameia", (0.32, 0.25, 0.35), (math.cos(a) * 1.0, math.sin(a) * 1.0, 4.58),
                                      rot=(0, 0, a)), 5, bevel=0.02))
    parts.append(finish_part(cyl("Telhado", 1.05, 1.6, (0, 0, 5.2), r2=0.0, verts=24), 1))
    # janelas em arco e porta, ligeiramente para fora da parede
    for z, a in ((2.6, -90), (1.4, 30), (3.2, 150)):
        ar = math.radians(a)
        parts.append(finish_part(cube("Janela", (0.4, 0.06, 0.6), (math.cos(ar) * 0.97, math.sin(ar) * 0.97, z),
                                      rot=(0, 0, ar + math.pi / 2)), 2, uv="planar", axes=("x", "z")))
    parts.append(finish_part(cube("Porta", (0.6, 0.08, 0.9), (0, -1.08, 0.75)), 3, bevel=0.02))
    parts.append(finish_part(cyl("Mastro", 0.03, 0.9, (0, 0, 6.3), verts=8), 3))
    parts.append(finish_part(cube("Flamula", (0.02, 0.45, 0.25), (0, 0.23, 6.6)), 4))
    return assemble("Torre", parts, img)


# ------------------------------------------------- objetos de apoio da fase

def model_relogio():
    cells = {"mostrador": (0, clock_face), "aro": (1, lambda: solid("#2F7FD8", 0.03)),
             "sino": (2, lambda: solid("#FFD447", 0.03))}
    img = build_atlas("T_Relogio", cells)
    parts = [finish_part(cyl("Corpo", 0.3, 0.14, (0, 0, 0), rot=(math.radians(90), 0, 0), verts=32), 0,
                         uv="planar_flip", axes=("x", "z")),
             finish_part(torus("Aro", 0.3, 0.04, (0, 0, 0), rot=(math.radians(90), 0, 0)), 1)]
    for sx in (-1, 1):
        parts.append(finish_part(sphere("Sino", 0.1, (sx * 0.2, 0, 0.3), scale=(1, 1, 0.7)), 2))
    parts.append(finish_part(cyl("Pe", 0.03, 0.12, (0, 0, -0.33), verts=8), 1))
    return assemble("Relogio", parts, img, origin="center")


def model_gosma():
    cells = {"gosma": (0, slime_cell), "olho": (1, lambda: solid("#FFFFFF", 0.01)),
             "pupila": (2, lambda: solid("#1E2A44", 0.01))}
    img = build_atlas("T_Gosma", cells)
    parts = [finish_part(sphere("Corpo", 0.4, (0, 0, 0.22), scale=(1.1, 1.1, 0.55), seg=32), 0)]
    for sx, sy, r in ((0.35, 0.2, 0.15), (-0.3, 0.25, 0.12), (0.05, 0.38, 0.13)):
        parts.append(finish_part(sphere("Pingo", r, (sx, sy, 0.06), scale=(1, 1, 0.4)), 0))
    for sx in (-1, 1):
        parts.append(finish_part(sphere("Olho", 0.08, (sx * 0.12, -0.33, 0.35)), 1))
        parts.append(finish_part(sphere("Pupila", 0.04, (sx * 0.12, -0.4, 0.35)), 2))
    return assemble("Gosma", parts, img)


def model_bandeira():
    cells = {"mastro": (0, lambda: solid("#E8EEF2", 0.02)), "pano": (1, flag_cloth),
             "base": (2, lambda: bricks("#7E7568", "#B9AE98", "#A79A82", rows=2, cols=3)),
             "topo": (3, lambda: solid("#FFD447", 0.03))}
    img = build_atlas("T_Bandeira", cells)
    parts = [finish_part(cube("Base", (0.6, 0.6, 0.3), (0, 0, 0.15)), 2, bevel=0.03),
             finish_part(cyl("Mastro", 0.05, 3.0, (0, 0, 1.8), verts=12), 0),
             finish_part(sphere("Topo", 0.1, (0, 0, 3.35)), 3)]
    cloth = cube("Pano", (0.9, 0.04, 0.6), (0.5, 0, 2.9))
    parts.append(finish_part(cloth, 1, uv="planar", axes=("x", "z")))
    return assemble("Bandeira", parts, img)


# ---------------------------------------------------------------- preview

def render_preview(objs):
    """Renderiza todos os modelos lado a lado para conferencia."""
    x = 0.0
    for o in objs:
        dims = o.dimensions
        o.location = (x + dims.x / 2, 0, 0 if o.name not in ("Moeda", "Coracao", "Bastao", "Relogio") else dims.z / 2)
        if o.name == "Equilibrista_Bracos":
            o.location = (objs[0].location.x, 0, SHOULDER_PIVOT[2])
            continue
        x += max(dims.x, 0.6) + 0.5
    bpy.ops.object.camera_add(location=(x / 2, -14, 4.0), rotation=(math.radians(80), 0, 0))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = x + 1
    bpy.context.scene.camera = cam
    bpy.ops.object.light_add(type="SUN", location=(0, -5, 10), rotation=(math.radians(50), 0, math.radians(-30)))
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = 2000
    sc.render.resolution_y = 700
    sc.render.filepath = os.path.join(PREVIEW_DIR, "modelos.png")
    bpy.ops.render.render(write_still=True)


def main():
    reset_scene()
    save_tile("T_Grama", grass_tile())
    save_tile("T_Terra", dirt_tile())
    save_tile("T_Pedra", stone_tile())

    builders = [model_bastao, model_moeda, model_coracao, model_bota,
                model_arvore, model_torre, model_relogio, model_gosma, model_bandeira]
    objs = []
    for b in builders:
        o = b()
        export(o)
        o.hide_set(False)
        objs.append(o)
        # move para longe para nao interferir no proximo modelo
        o.location.x += 100 + len(objs) * 10
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC_DIR, "TravessiaModelos.blend"))
    render_preview(objs)
    print("CONCLUIDO")


def render_character(objs):
    """Monta corpo + bracos + vara (vara levantada a esquerda) para conferir a pose."""
    by_name = {o.name: o for o in objs}
    for o in objs:
        o.hide_render = o.name not in ("Equilibrista", "Equilibrista_Bracos", "Bastao")
    body, arms, pole = by_name["Equilibrista"], by_name["Equilibrista_Bracos"], by_name["Bastao"]
    body.location = (0, 0, 0)
    arms.location = SHOULDER_PIVOT
    pole.parent = arms
    pole.location = (POLE_CENTER[0] - SHOULDER_PIVOT[0], POLE_CENTER[1] - SHOULDER_PIVOT[1],
                     POLE_CENTER[2] - SHOULDER_PIVOT[2])
    arms.rotation_euler = (0, math.radians(-18), 0)  # gira em torno do eixo frente-tras
    cam = bpy.context.scene.camera
    cam.data.type = "PERSP"
    cam.data.lens = 40
    cam.location = (2.2, -4.2, 1.6)
    cam.rotation_euler = (math.radians(85), 0, math.radians(28))
    sc = bpy.context.scene
    sc.render.resolution_x = 900
    sc.render.resolution_y = 900
    sc.render.filepath = os.path.join(PREVIEW_DIR, "personagem.png")
    bpy.ops.render.render(write_still=True)


# O personagem com rig fica em gerar_personagem.py, que reaproveita estas funcoes.
if not globals().get("SKIP_MAIN"):
    main()