# Travessia nas Alturas - passaro (perigo das cordas).
# Uso: blender --background --python gerar_passaro.py
#
# Corpo e asas sao objetos separados no mesmo FBX. Cada asa tem a origem no ombro,
# para a Unity bater as asas girando esses objetos.
# Convencao: bico para -Y no Blender. Asa esquerda do passaro = +X.

import os
import math
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
SKIP_MAIN = True
exec(open(os.path.join(HERE, "gerar_modelos.py"), encoding="utf-8").read())  # funcoes auxiliares


def wing_cell():
    u, v = cell_coords()
    img = solid("#C93C3C", 0.03)
    feathers = (np.floor(u * 5) % 2 == 0) & (v < 0.45)
    img = np.where(feathers[..., None], hex_rgb("#8E2424"), img)
    tip = v < 0.12
    return np.where(tip[..., None], hex_rgb("#3A1414"), img)


def model_passaro():
    cells = {"corpo": (0, lambda: solid("#E75050", 0.04, 4)),
             "barriga": (1, lambda: solid("#FFF1D6", 0.02)),
             "bico": (2, lambda: solid("#FFC23A", 0.03)),
             "olho": (3, lambda: solid("#FFFFFF", 0.01)),
             "pupila": (4, lambda: solid("#1E2A44", 0.01)),
             "asa": (5, wing_cell),
             "cauda": (6, lambda: stripes("#8E2424", "#E75050", 4, vertical=True))}
    img = build_atlas("T_Passaro", cells)

    body_parts = [
        finish_part(sphere("Corpo", 0.2, (0, 0, 0), scale=(0.9, 1.2, 0.85), seg=24), 0),
        finish_part(sphere("Barriga", 0.15, (0, -0.08, -0.06), scale=(0.9, 1.1, 0.8), seg=20), 1),
        finish_part(sphere("Cabeca", 0.13, (0, -0.22, 0.1), seg=24), 0),
        finish_part(cyl("Bico", 0.045, 0.13, (0, -0.37, 0.08), rot=(math.radians(90), 0, 0), r2=0.0, verts=12), 2),
        finish_part(cube("Cauda", (0.16, 0.2, 0.03), (0, 0.27, 0.06), rot=(math.radians(-20), 0, 0)), 6, bevel=0.01),
    ]
    for sx in (-1, 1):
        body_parts.append(finish_part(sphere("Olho", 0.036, (sx * 0.075, -0.3, 0.15), seg=12), 3))
        body_parts.append(finish_part(sphere("Pupila", 0.019, (sx * 0.08, -0.328, 0.155), seg=10), 4))
    body = assemble("Passaro_Corpo", body_parts, img, origin="center_keep")

    wings = []
    for sx, name in ((1, "Passaro_AsaEsq"), (-1, "Passaro_AsaDir")):
        shoulder = (sx * 0.14, -0.02, 0.07)
        w = sphere(name, 0.2, (sx * 0.32, 0.02, 0.07), scale=(1.5, 0.75, 0.12), seg=20)
        w = finish_custom(w, name, img, shoulder, cell=5)
        w.location = shoulder
        wings.append(w)
    return body, wings


def export_bird(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    path = os.path.join(MODELS_DIR, "Passaro.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
        add_leaf_bones=False, path_mode="STRIP")
    print("Exportado:", path)


def render_bird(objs):
    for o in objs:
        o.hide_render = False
    wings = [o for o in objs if "Asa" in o.name]
    wings[0].rotation_euler = (0, math.radians(-25), 0)
    wings[1].rotation_euler = (0, math.radians(25), 0)
    bpy.ops.object.camera_add(location=(1.2, -1.6, 0.7))
    cam = bpy.context.active_object
    cam.rotation_euler = (math.radians(72), 0, math.radians(37))
    sc = bpy.context.scene
    sc.camera = cam
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = 600
    sc.render.resolution_y = 500
    sc.render.filepath = os.path.join(PREVIEW_DIR, "passaro.png")
    bpy.ops.render.render(write_still=True)
    for w in wings:
        w.rotation_euler = (0, 0, 0)


reset_scene()
body, wings = model_passaro()
objs = [body] + wings
export_bird(objs)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC_DIR, "Passaro.blend"))
render_bird(objs)
print("PASSARO CONCLUIDO")
