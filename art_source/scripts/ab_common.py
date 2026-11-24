"""
Absolute Basketball â€” funÃ§Ãµes comuns para os scripts de modelo/animaÃ§Ã£o no Blender (Etapa 7).

Uso dentro de um script de modelo (rodado com tools\\blender.ps1):

    import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import ab_common as ab
    ab.reset_scene()
    obj = ...                      # modela por cÃ³digo
    ab.material(obj, "Couro", (0.9, 0.45, 0.1))
    ab.finalize(obj)               # aplica escala/rotaÃ§Ã£o, UV se faltar
    ab.export_glb("ball", [obj])   # -> assets/models/ball.glb (+Y Up)
    ab.save_blend("ball")          # -> art_source/blend/ball.blend (fonte editÃ¡vel)

Regras (CLAUDE.md "Regras de arte 3D" / "Regras de animaÃ§Ã£o"):
- 1 unidade = 1 metro, Z para cima no Blender, exporta glTF com +Y Up.
- Personagem olha para -Y no Blender (vista frontal = numpad 1). A Godot gira o modelo 180Â° no encaixe.
- Origem: nos pÃ©s (personagens / objetos apoiados no chÃ£o) ou no centro de massa (bola).
- Ctrl+A (escala/rotaÃ§Ã£o) aplicado antes de exportar; UV unwrap; materiais Principled BSDF simples.
- AnimaÃ§Ãµes: 30 fps, in place, uma Action por animaÃ§Ã£o (snake_case; loops terminam em "-loop"), Fake User.
"""
import math
import os
import sys

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
MODELS_DIR = os.path.join(ROOT, "assets", "models")
BLEND_DIR = os.path.join(ROOT, "art_source", "blend")
PREVIEW_DIR = os.path.join(ROOT, "art_source", "previews")
FPS = 30


def script_args():
    """Argumentos depois de '--' na linha de comando do Blender."""
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def reset_scene():
    """Cena vazia, mÃ©trica, 30 fps."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    sc.unit_settings.scale_length = 1.0
    sc.render.fps = FPS
    sc.render.fps_base = 1.0
    return sc


def material(obj, name, color, roughness=0.6, metallic=0.0, emission=None):
    """Material Principled BSDF simples (cor base RGB 0..1). Reaproveita se jÃ¡ existir com o mesmo nome."""
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if emission is not None:
            bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
            bsdf.inputs["Emission Strength"].default_value = 1.0
    if obj is not None:
        obj.data.materials.append(mat)
    return mat


def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    if objs:
        bpy.context.view_layer.objects.active = objs[0]


def ensure_uv(obj, angle_deg=66.0, margin=0.02):
    """Smart UV Project se a malha ainda nÃ£o tiver UV."""
    if obj.type != "MESH" or obj.data.uv_layers:
        return
    select_only([obj])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle_deg), island_margin=margin)
    bpy.ops.object.mode_set(mode="OBJECT")


def finalize(*objs):
    """Aplica rotaÃ§Ã£o/escala (Ctrl+A), garante UV e sombreamento suave por Ã¢ngulo."""
    objs = [o for o in objs if o is not None]
    meshes = [o for o in objs if o.type == "MESH"]
    select_only(objs)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for o in meshes:
        ensure_uv(o)
        if hasattr(bpy.ops.object, "shade_auto_smooth"):
            select_only([o])
            try:
                bpy.ops.object.shade_auto_smooth()
            except RuntimeError:
                pass


def set_origin_to_feet(obj):
    """Origem no ponto mais baixo, centralizado em X/Y (personagens e objetos apoiados no chÃ£o)."""
    from mathutils import Vector
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    minz = min(p.z for p in pts)
    cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
    cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
    bpy.context.scene.cursor.location = Vector((cx, cy, minz))
    select_only([obj])
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    obj.location = (0, 0, 0)


def export_glb(name, objs=None, animations=False):
    """Exporta para assets/models/<name>.glb (+Y Up, modificadores aplicados). objs=None exporta a cena toda."""
    os.makedirs(MODELS_DIR, exist_ok=True)
    path = os.path.join(MODELS_DIR, name + ".glb")
    kw = dict(
        filepath=path,
        export_format="GLB",
        export_yup=True,
        export_apply=True,
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
        export_animations=animations,
    )
    if animations:
        kw.update(export_animation_mode="ACTIONS", export_force_sampling=False, export_frame_range=False)
    if objs is not None:
        select_only(objs)
        kw["use_selection"] = True
    # as opÃ§Ãµes do exportador mudam entre versÃµes do Blender: manda sÃ³ as que existem nesta
    known = set(bpy.ops.export_scene.gltf.get_rna_type().properties.keys())
    skipped = [k for k in kw if k not in known]
    if skipped:
        print(f"[AB] opÃ§Ãµes do glTF ignoradas nesta versÃ£o: {', '.join(skipped)}")
    bpy.ops.export_scene.gltf(**{k: v for k, v in kw.items() if k in known})
    print(f"[AB] exportado: {path}")
    return path


def save_blend(name):
    """Salva a fonte editÃ¡vel em art_source/blend/<name>.blend."""
    os.makedirs(BLEND_DIR, exist_ok=True)
    path = os.path.join(BLEND_DIR, name + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print(f"[AB] salvo: {path}")
    return path


def new_action(armature, name, loop=False):
    """Cria uma Action com Fake User e liga ao armature. loop=True acrescenta '-loop' ao nome."""
    full = name + ("-loop" if loop and not name.endswith("-loop") else "")
    act = bpy.data.actions.new(full)
    act.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = act
    return act


def key_bezier(act):
    """InterpolaÃ§Ã£o BÃ©zier com ease (auto clamped) em todas as chaves da Action (nada de linear/robÃ³tico)."""
    curves = []
    if hasattr(act, "fcurves") and act.fcurves is not None:
        try:
            curves = list(act.fcurves)
        except TypeError:
            curves = []
    if not curves and hasattr(act, "layers"):  # Actions em camadas (Blender 4.4+)
        for layer in act.layers:
            for strip in layer.strips:
                for bag in getattr(strip, "channelbags", []):
                    curves.extend(bag.fcurves)
    for fc in curves:
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = "AUTO_CLAMPED"
            kp.handle_right_type = "AUTO_CLAMPED"
