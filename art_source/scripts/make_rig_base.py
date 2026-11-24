"""
Esqueleto base compartilhado por TODOS os personagens -> art_source/blend/rig_base.blend

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_rig_base.py

- Nomes de ossos = perfil humanoide da Godot (SkeletonProfileHumanoid): a Godot reconhece sozinha e dá para
  reaproveitar animações entre personagens (retarget).
- Altura total ~1,92 m (o boneco placeholder do jogo e a cápsula de colisão de 1,8 m). Ajuste as proporções
  em PROP abaixo quando a concept art do personagem chegar — mantendo os NOMES dos ossos.
- Pose de descanso: T-pose, olhando para -Y, pés na origem.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import ab_common as ab  # noqa: E402

# alturas (m) e larguras — ajustar à concept art
PROP = dict(
    hips=0.98, spine=1.10, chest=1.25, upper_chest=1.40, neck=1.52, head=1.62, head_top=1.92,
    shoulder_x=0.05, upper_arm_x=0.20, elbow_x=0.48, wrist_x=0.74, hand_tip_x=0.86, arm_z=1.48,
    leg_x=0.11, knee_z=0.53, ankle_z=0.10, toe_y=-0.12, toe_tip_y=-0.21,
)


def build():
    ab.reset_scene()
    p = PROP
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
