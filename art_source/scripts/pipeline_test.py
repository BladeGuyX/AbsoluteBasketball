"""
Teste do pipeline de arte (não é arte do jogo): rig_base + boneco de cilindros preso aos ossos + 2 animações
(idle-loop e jump) -> assets/models/_pipeline_test.glb. Serve de EXEMPLO de como os scripts de personagem/animação
devem ser escritos (malha ligada ao armature, uma Action por animação, Bézier, Fake User, loop igual no 1º e último frame).

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\pipeline_test.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import ab_common as ab  # noqa: E402
import make_rig_base  # noqa: E402

rig = make_rig_base.build()
skin = ab.material(None, "Pele", (0.95, 0.8, 0.65))
shirt = ab.material(None, "Camisa", (0.15, 0.45, 0.95))

# uma peça por osso (cilindro do head ao tail), pesada 100% nesse osso
parts = []
for b in rig.data.bones:
    if b.name == "Root" or b.length < 0.05:
        continue
    head = rig.matrix_world @ b.head_local
    tail = rig.matrix_world @ b.tail_local
    vec = tail - head
    r = 0.16 if b.name in ("Hips", "Spine", "Chest", "UpperChest") else 0.13 if b.name == "Head" else 0.05
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=vec.length, vertices=10, location=(head + tail) / 2)
    o = bpy.context.active_object
    o.rotation_euler = vec.to_track_quat("Z", "Y").to_euler()
    o.data.materials.append(shirt if b.name in ("Hips", "Spine", "Chest", "UpperChest") else skin)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    vg = o.vertex_groups.new(name=b.name)
    vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    parts.append(o)
ab.select_only(parts)
bpy.ops.object.join()
body = bpy.context.active_object
body.name = "corpo"
ab.finalize(body)
body.parent = rig
mod = body.modifiers.new("Armature", "ARMATURE")
mod.object = rig

# animações
def pose(bone, rot_x=0.0, rot_y=0.0, rot_z=0.0, loc_z=0.0):
    pb = rig.pose.bones[bone]
    pb.rotation_mode = "XYZ"
    pb.rotation_euler = (rot_x, rot_y, rot_z)
    pb.location = (0, 0, 0) if bone != "Hips" else (0, loc_z, 0)  # Y local do Hips aponta para cima

def key(frame, bones):
    for b in bones:
        pb = rig.pose.bones[b]
        pb.keyframe_insert("rotation_euler", frame=frame)
        if b == "Hips":
            pb.keyframe_insert("location", frame=frame)

bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE")
B = ["Hips", "Spine", "LeftUpperArm", "RightUpperArm", "LeftUpperLeg", "RightUpperLeg"]

act = ab.new_action(rig, "idle", loop=True)       # idle-loop: 1º e último frame iguais
for f, k in ((1, 0.0), (16, 1.0), (31, 0.0)):
    pose("Hips", loc_z=-0.03 * k); pose("Spine", rot_x=0.05 * k)
    pose("LeftUpperArm", rot_y=-1.2); pose("RightUpperArm", rot_y=1.2)
    pose("LeftUpperLeg"); pose("RightUpperLeg")
    key(f, B)
ab.key_bezier(act)

act = ab.new_action(rig, "jump")                    # não é loop
for f, (crouch, legs, arms) in ((1, (0, 0, -1.2)), (6, (-0.18, 0.5, -0.6)), (12, (0.05, -0.1, 0.4)), (20, (0, 0, -1.2))):
    pose("Hips", loc_z=crouch); pose("Spine", rot_x=0.2 * (crouch < 0))
    pose("LeftUpperArm", rot_y=arms); pose("RightUpperArm", rot_y=-arms)
    pose("LeftUpperLeg", rot_x=legs); pose("RightUpperLeg", rot_x=legs)
    key(f, B)
ab.key_bezier(act)
rig.animation_data.action = None
bpy.ops.object.mode_set(mode="OBJECT")

ab.export_glb("_pipeline_test", None, animations=True)
ab.save_blend("_pipeline_test")
