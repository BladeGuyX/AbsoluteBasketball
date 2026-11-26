"""
Absolute Basketball — bigorna da cena da ultimate do Iron ("Indestrutível", v0.37): cai na cabeça dele e quica.

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_bigorna.py
Saída:  assets/models/bigorna.glb  +  art_source/blend/bigorna.blend

Bigorna de desenho animado, ~0,62 m de comprimento (bem maior que a cabeça para ler na tela), origem no CENTRO da base
(apoiada no chão). Ponta (chifre) para +X. Materiais: "Ferro" (cinza-escuro) e "FerroClaro" (face de cima gasta).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
import ab_common as ab  # noqa: E402


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def box(size, loc, bevel=0.012):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.scale = size
    ab.select_only([o])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    b = o.modifiers.new("bevel", "BEVEL")
    b.width = bevel
    b.segments = 2
    ab.select_only([o])
    bpy.ops.object.modifier_apply(modifier=b.name)
    return o


def horn():
    """Chifre: cone deitado saindo da frente do topo, afinando e curvando um pouco para baixo."""
    bm = bmesh.new()
    seg, rows = 16, 8
    ring = []
    for i in range(rows + 1):
        t = i / rows
        x = 0.17 + 0.2 * t
        z = 0.355 - 0.03 * t * t
        r = 0.075 * (1 - t) + 0.008
        ring.append([bm.verts.new((x, r * math.cos(2 * math.pi * j / seg) * 1.1, z + r * math.sin(2 * math.pi * j / seg)))
                     for j in range(seg)])
    for i in range(rows):
        for j in range(seg):
            bm.faces.new((ring[i][j], ring[i][(j + 1) % seg], ring[i + 1][(j + 1) % seg], ring[i + 1][j]))
    tip = bm.verts.new((0.385, 0, 0.322))
    for j in range(seg):
        bm.faces.new((ring[rows][j], ring[rows][(j + 1) % seg], tip))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("chifre")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("chifre", me)
    bpy.context.scene.collection.objects.link(o)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def main():
    ab.reset_scene()
    ferro = ab.material(None, "Ferro", srgb("#4B5058"), roughness=0.5, metallic=0.4)
    claro = ab.material(None, "FerroClaro", srgb("#9AA1AA"), roughness=0.35, metallic=0.5)
    parts = [
        box((0.36, 0.26, 0.07), (0, 0, 0.035)),          # pé
        box((0.28, 0.2, 0.05), (0, 0, 0.095)),           # degrau
        box((0.16, 0.13, 0.16), (0, 0, 0.2)),            # cintura
        box((0.36, 0.2, 0.11), (-0.01, 0, 0.335)),       # corpo de cima
        horn(),
    ]
    for p in parts:
        p.data.materials.append(ferro)
    face = box((0.34, 0.18, 0.012), (-0.01, 0, 0.392), 0.004)   # face de cima gasta
    face.data.materials.append(claro)
    ab.select_only(parts + [face])
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = "Bigorna"
    o.data.name = "Bigorna"
    ab.finalize(o)
    ab.export_glb("bigorna", [o])
    ab.save_blend("bigorna")


main()
