# Travessia nas Alturas - Equilibrista com rig e animacoes.
# Uso: blender --background --python gerar_personagem.py
#
# 1. Malha unica de manequim (corpo + bracos segurando a vara), fundida por voxels.
# 2. Esqueleto generico (Hips > Spine > Chest > ShoulderRig > bracos; Hips > pernas).
# 3. Skinning automatico (pesos por calor / ARMATURE_AUTO).
# 4. Acoes: Idle, Walk, RopeIdle, RopeWalk, Jump, Fall.
# 5. Exporta FBX (malha + armature + animacoes) e renderiza quadros de conferencia.
#
# Convencao: personagem olhando para -Y no Blender. Lado esquerdo do personagem = +X.

import os
import math
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
SKIP_MAIN = True
exec(open(os.path.join(HERE, "gerar_modelos.py"), encoding="utf-8").read())  # funcoes auxiliares

FPS = 30

# ------------------------------------------------------------------ esqueleto
# nome, cabeca, cauda, pai
SHOULDER = 0.19
ELBOW = (0.31, -0.14, 1.04)
HAND = (0.25, -0.34, 1.02)
LEG_X = 0.1


def bone_list():
    bones = [
        ("Hips", (0, 0, 0.80), (0, 0, 0.95), None),
        ("Spine", (0, 0, 0.95), (0, 0, 1.12), "Hips"),
        ("Chest", (0, 0, 1.12), (0, 0, 1.28), "Spine"),
        ("Neck", (0, 0, 1.28), (0, 0, 1.36), "Chest"),
        ("Head", (0, 0, 1.36), (0, 0, 1.66), "Neck"),
        # Pivo dos ombros: a vara e os bracos giram em torno dele no jogo.
        ("ShoulderRig", (0, 0, 1.22), (0, -0.12, 1.22), "Chest"),
    ]
    for side, sx in (("L", 1), ("R", -1)):
        bones += [
            (f"UpperArm.{side}", (sx * SHOULDER, 0, 1.22), (sx * ELBOW[0], ELBOW[1], ELBOW[2]), "ShoulderRig"),
            (f"LowerArm.{side}", (sx * ELBOW[0], ELBOW[1], ELBOW[2]), (sx * HAND[0], HAND[1], HAND[2]), f"UpperArm.{side}"),
            (f"Hand.{side}", (sx * HAND[0], HAND[1], HAND[2]), (sx * HAND[0], HAND[1] - 0.09, HAND[2]), f"LowerArm.{side}"),
            (f"UpperLeg.{side}", (sx * LEG_X, 0, 0.78), (sx * LEG_X, -0.015, 0.43), "Hips"),
            (f"LowerLeg.{side}", (sx * LEG_X, -0.015, 0.43), (sx * LEG_X, 0, 0.09), f"UpperLeg.{side}"),
            (f"Foot.{side}", (sx * LEG_X, 0, 0.09), (sx * LEG_X, -0.14, 0.03), f"LowerLeg.{side}"),
        ]
    return bones


def build_mesh():
    """Malha do manequim em pose de repouso: pernas retas, bracos segurando a vara a frente."""
    img = build_atlas("T_Equilibrista", {"pele": (0, mannequin_cell)})
    parts = []
    parts.append(sphere("Cabeca", 0.155, (0, 0, 1.49), scale=(1, 1, 1.08), seg=32))
    parts += capsule("Pescoco", (0, 0, 1.3), (0, 0, 1.38), 0.055)
    parts.append(sphere("Tronco", 0.2, (0, 0, 1.06), scale=(1.0, 0.72, 1.32), seg=32))
    parts.append(sphere("Quadril", 0.165, (0, 0, 0.8), scale=(1.05, 0.8, 0.72), seg=32))
    for sx in (-1, 1):
        shoulder = (sx * SHOULDER, 0, 1.22)
        elbow = (sx * ELBOW[0], ELBOW[1], ELBOW[2])
        hand = (sx * HAND[0], HAND[1], HAND[2])
        parts.append(sphere("Ombro", 0.066, shoulder, seg=16))
        parts += capsule("Braco", shoulder, elbow, 0.05)
        parts += capsule("Antebraco", elbow, hand, 0.043)
        parts.append(sphere("Mao", 0.055, (hand[0], hand[1] - 0.01, hand[2]), scale=(1.1, 1, 0.9), seg=16))
        hip = (sx * LEG_X, 0, 0.78)
        knee = (sx * LEG_X, -0.015, 0.43)
        ankle = (sx * LEG_X, 0, 0.09)
        parts += capsule("Coxa", hip, knee, 0.072)
        parts += capsule("Canela", knee, ankle, 0.056)
        parts.append(sphere("Pe", 0.06, (ankle[0], ankle[1] - 0.05, 0.045), scale=(1, 1.9, 0.75), seg=16))
    body = smooth_union("Equilibrista", parts, voxel=0.01, smooth_iter=6, decimate=0.3)
    return finish_custom(body, "Equilibrista", img, (0, 0, 0))


def build_armature():
    arm = bpy.data.armatures.new("Armature")
    obj = bpy.data.objects.new("Armature", arm)
    bpy.context.collection.objects.link(obj)
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, parent in bone_list():
        eb = arm.edit_bones.new(name)
        eb.head = head
        eb.tail = tail
        eb.roll = 0.0
        if parent:
            eb.parent = arm.edit_bones[parent]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in obj.pose.bones:
        pb.rotation_mode = "XYZ"
    return obj


def skin(mesh, armature):
    """Skinning automatico: Blender calcula os pesos pela distancia/calor de cada osso."""
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    empty = [g.name for g in mesh.vertex_groups
             if not any(g.index in [x.group for x in v.groups] for v in mesh.data.vertices)]
    print("Grupos sem pesos:", empty)


# ------------------------------------------------------------------ animacao
# Eixos de rotacao local (rotacao em graus):
#  pernas (osso apontando para baixo): X positivo leva a perna para tras, negativo para frente;
#  Z positivo na perna esquerda puxa o pe para dentro (para a linha da corda).
#  Hips/Spine/Chest (osso para cima): X positivo inclina para tras; Y gira o tronco.

def pose_frame(arm, frame, rot, loc=None):
    for pb in arm.pose.bones:
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
    for bone, r in rot.items():
        arm.pose.bones[bone].rotation_euler = tuple(math.radians(a) for a in r)
    for bone, l in (loc or {}).items():
        arm.pose.bones[bone].location = l
    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_euler", frame=frame)
        pb.keyframe_insert("location", frame=frame)


def make_action(arm, name, keys):
    """keys: lista de (quadro, rotacoes, locais). O ultimo quadro repete o primeiro nos loops."""
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    for frame, rot, loc in keys:
        pose_frame(arm, frame, rot, loc)
    # interpolacao suave
    for fc in getattr(act, "fcurves", []):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
    act.frame_range = (keys[0][0], keys[-1][0])
    # guarda numa faixa NLA para exportar todas as acoes
    track = arm.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, int(keys[0][0]), act)
    strip.name = name
    arm.animation_data.action = None
    return act


def legs(l_up, l_low, r_up, r_low, l_in=0, r_in=0, l_foot=0, r_foot=0):
    return {
        "UpperLeg.L": (l_up, 0, l_in), "LowerLeg.L": (l_low, 0, 0), "Foot.L": (l_foot, 0, 0),
        "UpperLeg.R": (r_up, 0, -r_in), "LowerLeg.R": (r_low, 0, 0), "Foot.R": (r_foot, 0, 0),
    }


def merge(*dicts):
    out = {}
    for d in dicts:
        out.update(d)
    return out


def bob(z):
    return {"Hips": (0, z, 0)}  # o osso Hips aponta para cima: Y local = vertical


def build_actions(arm):
    # Idle: respiracao leve
    make_action(arm, "Idle", [
        (1, {"Chest": (0, 0, 0), "Head": (0, 0, 0)}, None),
        (31, {"Chest": (-2.5, 0, 0), "Head": (2, 0, 0), "UpperArm.L": (-2, 0, 0), "UpperArm.R": (-2, 0, 0)}, bob(0.006)),
        (61, {"Chest": (0, 0, 0), "Head": (0, 0, 0)}, None),
    ])

    # Walk: ciclo de 24 quadros (contato, passagem, contato oposto, passagem oposta)
    walk = [
        (1, merge(legs(-28, 6, 26, 12, l_foot=18, r_foot=-10), {"Spine": (0, 5, 0), "Head": (0, -4, 0)}), bob(-0.025)),
        (7, merge(legs(4, 6, -12, 58, l_foot=-4, r_foot=-20), {"Spine": (0, 0, 0)}), bob(0.02)),
        (13, merge(legs(26, 12, -28, 6, l_foot=-10, r_foot=18), {"Spine": (0, -5, 0), "Head": (0, 4, 0)}), bob(-0.025)),
        (19, merge(legs(-12, 58, 4, 6, l_foot=-20, r_foot=-4), {"Spine": (0, 0, 0)}), bob(0.02)),
        (25, merge(legs(-28, 6, 26, 12, l_foot=18, r_foot=-10), {"Spine": (0, 5, 0), "Head": (0, -4, 0)}), bob(-0.025)),
    ]
    make_action(arm, "Walk", walk)

    # RopeIdle: joelhos levemente flexionados, pes para dentro, corpo atento
    rope_base = legs(-6, 14, 6, 14, l_in=6, r_in=6, l_foot=-6, r_foot=-6)
    make_action(arm, "RopeIdle", [
        (1, merge(rope_base, {"Spine": (6, 0, 0)}), bob(-0.03)),
        (31, merge(rope_base, {"Spine": (8, 0, 0), "Head": (-4, 0, 0)}), bob(-0.04)),
        (61, merge(rope_base, {"Spine": (6, 0, 0)}), bob(-0.03)),
    ])

    # RopeWalk: passos curtos e cuidadosos, um pe a frente do outro sobre a corda
    rw = [
        (1, merge(legs(-16, 12, 14, 18, l_in=7, r_in=7, l_foot=8, r_foot=-8), {"Spine": (6, 3, 0)}), bob(-0.04)),
        (11, merge(legs(0, 14, -6, 42, l_in=7, r_in=5, l_foot=-6, r_foot=-16), {"Spine": (7, 0, 0)}), bob(-0.02)),
        (21, merge(legs(14, 18, -16, 12, l_in=7, r_in=7, l_foot=-8, r_foot=8), {"Spine": (6, -3, 0)}), bob(-0.04)),
        (31, merge(legs(-6, 42, 0, 14, l_in=5, r_in=7, l_foot=-16, r_foot=-6), {"Spine": (7, 0, 0)}), bob(-0.02)),
        (41, merge(legs(-16, 12, 14, 18, l_in=7, r_in=7, l_foot=8, r_foot=-8), {"Spine": (6, 3, 0)}), bob(-0.04)),
    ]
    make_action(arm, "RopeWalk", rw)

    # Jump: pernas recolhidas
    jump = merge(legs(-50, 70, -20, 45, l_foot=-15, r_foot=-10), {"Spine": (8, 0, 0)})
    make_action(arm, "Jump", [(1, jump, None), (11, jump, None)])

    # Crouch: agachado para desviar dos passaros (quadril desce, tronco inclina a frente)
    crouch = merge(legs(-72, 112, -66, 106, l_in=5, r_in=5, l_foot=-38, r_foot=-38),
                   {"Spine": (30, 0, 0), "Chest": (14, 0, 0), "Head": (-24, 0, 0)})
    crouch_b = merge(legs(-72, 112, -66, 106, l_in=5, r_in=5, l_foot=-38, r_foot=-38),
                     {"Spine": (32, 0, 0), "Chest": (15, 0, 0), "Head": (-26, 0, 0)})
    make_action(arm, "Crouch", [(1, crouch, bob(-0.38)), (21, crouch_b, bob(-0.385)), (41, crouch, bob(-0.38))])

    # Fall: pernas pedalando e bracos se debatendo (sem soltar a vara)
    fa = merge(legs(-55, 75, -15, 30), {"Spine": (12, 0, 0), "UpperArm.L": (-25, 0, 0), "UpperArm.R": (15, 0, 0),
                                       "Head": (-12, 0, 0)})
    fb = merge(legs(-15, 30, -55, 75), {"Spine": (12, 0, 0), "UpperArm.L": (15, 0, 0), "UpperArm.R": (-25, 0, 0),
                                       "Head": (-12, 0, 0)})
    make_action(arm, "Fall", [(1, fa, None), (9, fb, None), (17, fa, None)])


# ------------------------------------------------------------------ exportacao e conferencia

def export_character(mesh, armature):
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    path = os.path.join(MODELS_DIR, "Equilibrista.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        mesh_smooth_type="FACE", add_leaf_bones=False, armature_nodetype="NULL",
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode="STRIP")
    print("Exportado:", path)


def render_frames(armature, pole):
    """Renderiza quadros das animacoes de lado e de frente para conferencia."""
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = 360
    sc.render.resolution_y = 480
    sc.render.fps = FPS
    bpy.ops.object.camera_add()
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.3
    sc.camera = cam
    views = {"lado": ((4, -0.2, 0.9), (math.radians(90), 0, math.radians(90))),
             "frente": ((0, -4, 0.9), (math.radians(90), 0, 0))}
    shots = [("Walk", 1), ("Walk", 7), ("Walk", 13), ("RopeWalk", 1), ("RopeWalk", 11), ("Jump", 1), ("Fall", 1), ("Idle", 1), ("Crouch", 1)]
    anim = armature.animation_data
    for view, (loc, rot) in views.items():
        cam.location = loc
        cam.rotation_euler = rot
        for act_name, frame in shots:
            for t in anim.nla_tracks:
                t.mute = t.name != act_name
            sc.frame_set(frame)
            sc.render.filepath = os.path.join(PREVIEW_DIR, f"anim_{view}_{act_name}_{frame:02d}.png")
            bpy.ops.render.render(write_still=True)
    for t in anim.nla_tracks:
        t.mute = False


def main_character():
    reset_scene()
    mesh = build_mesh()
    armature = build_armature()
    skin(mesh, armature)
    build_actions(armature)
    export_character(mesh, armature)

    # vara presa ao osso ShoulderRig so para conferencia visual no Blender
    pole = model_bastao()
    pole.location = (0, -0.34, 1.0)
    bpy.ops.object.select_all(action="DESELECT")
    pole.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    armature.data.bones.active = armature.data.bones["ShoulderRig"]
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    pole.parent = armature
    pole.parent_type = "BONE"
    pole.parent_bone = "ShoulderRig"
    pole.matrix_parent_inverse = (armature.matrix_world @ armature.pose.bones["ShoulderRig"].matrix).inverted()

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC_DIR, "Equilibrista_Rig.blend"))
    render_frames(armature, pole)
    print("PERSONAGEM CONCLUIDO")


main_character()
