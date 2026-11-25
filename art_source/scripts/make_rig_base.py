"""
Esqueleto base compartilhado por TODOS os personagens -> art_source/blend/rig_base.blend

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_rig_base.py

- Nomes de ossos = perfil humanoide da Godot (SkeletonProfileHumanoid): a Godot reconhece sozinha e dá para
  reaproveitar animações entre personagens (retarget).
- v0.21: proporções do 1º personagem (o Rei, chibi de ~1,4 m na concept; a Godot escala o visual por
  Tuning.CharacterScale). Outro personagem pode passar as próprias proporções em build(prop) — mantendo os NOMES.
- Os ossos dos braços existem (perfil humanoide), mas o Rei não tem braços: as mãos flutuam e são do código.
- Pose de descanso: T-pose, olhando para -Y, pés na origem.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import ab_common as ab  # noqa: E402

# alturas (m) e larguras — ajustar à concept art
PROP = dict(
    hips=0.45, spine=0.53, chest=0.62, upper_chest=0.71, neck=0.79, head=0.86, head_top=1.25,
    shoulder_x=0.05, upper_arm_x=0.17, elbow_x=0.27, wrist_x=0.37, hand_tip_x=0.43, arm_z=0.73,
    leg_x=0.105, knee_z=0.26, ankle_z=0.075, toe_y=-0.085, toe_tip_y=-0.16,
)


def build(prop=None):
    ab.reset_scene()
    p = dict(PROP, **(prop or {}))
    arm = bpy.data.armatures.new("rig_base")
    obj = bpy.data.objects.new("rig_base", arm)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.show_in_front = True
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.edit_bones

    def bone(name, head, tail, parent=None, connect=False):
        b = eb.new(name)
        b.head, b.tail = head, tail
        if parent:
            b.parent = eb[parent]
            b.use_connect = connect
        return b

    bone("Root", (0, 0, 0), (0, 0.25, 0))
    bone("Hips", (0, 0, p["hips"]), (0, 0, p["spine"]), "Root")
    bone("Spine", (0, 0, p["spine"]), (0, 0, p["chest"]), "Hips", True)
    bone("Chest", (0, 0, p["chest"]), (0, 0, p["upper_chest"]), "Spine", True)
    bone("UpperChest", (0, 0, p["upper_chest"]), (0, 0, p["neck"]), "Chest", True)
    bone("Neck", (0, 0, p["neck"]), (0, 0, p["head"]), "UpperChest", True)
    bone("Head", (0, 0, p["head"]), (0, 0, p["head_top"]), "Neck", True)
    for side, s in (("Left", 1), ("Right", -1)):  # olhando para -Y, a esquerda do personagem é +X
        z = p["arm_z"]
        bone(f"{side}Shoulder", (s * p["shoulder_x"], 0, z), (s * p["upper_arm_x"], 0, z), "UpperChest")
        bone(f"{side}UpperArm", (s * p["upper_arm_x"], 0, z), (s * p["elbow_x"], 0.01, z), f"{side}Shoulder", True)
        bone(f"{side}LowerArm", (s * p["elbow_x"], 0.01, z), (s * p["wrist_x"], 0, z), f"{side}UpperArm", True)
        bone(f"{side}Hand", (s * p["wrist_x"], 0, z), (s * p["hand_tip_x"], 0, z), f"{side}LowerArm", True)
        x = s * p["leg_x"]
        bone(f"{side}UpperLeg", (x, 0, p["hips"]), (x, -0.02, p["knee_z"]), "Hips")
        bone(f"{side}LowerLeg", (x, -0.02, p["knee_z"]), (x, 0, p["ankle_z"]), f"{side}UpperLeg", True)
        bone(f"{side}Foot", (x, 0, p["ankle_z"]), (x, p["toe_y"], 0.02), f"{side}LowerLeg", True)
        bone(f"{side}Toes", (x, p["toe_y"], 0.02), (x, p["toe_tip_y"], 0.02), f"{side}Foot", True)
    # roll coerente (eixo Z do osso para cima/frente) — bom para espelhar poses
    bpy.ops.armature.select_all(action="SELECT")
    bpy.ops.armature.calculate_roll(type="GLOBAL_POS_Z")
    bpy.ops.object.mode_set(mode="OBJECT")
    arm.display_type = "STICK"
    return obj


if __name__ == "__main__":
    rig = build()
    print(f"[AB] ossos: {len(rig.data.bones)} -> {', '.join(b.name for b in rig.data.bones)}")
    ab.save_blend("rig_base")
