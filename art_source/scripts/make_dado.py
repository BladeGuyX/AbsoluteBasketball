"""
Absolute Basketball — dado do Luck (cena da ultimate "Sorte Infinita", v0.36).

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_dado.py
Saída:  assets/models/dado.glb  +  art_source/blend/dado.blend

Forma: cubo de 0,26 m de lado (estilizado, bem maior que um dado de verdade para ler na tela), cantos arredondados (bevel),
origem no centro. Pontos (bolinhas achatadas, levemente afundadas) em verde-escuro, faces opostas somam 7.
Na Godot (+Y Up): CIMA (+Y) = 6, BAIXO = 1, FRENTE (+Z da Godot = −Y do Blender) = 2, TRÁS = 5, DIREITA (+X) = 3, ESQUERDA = 4.
Materiais: "Dado" (branco marfim) e "Ponto" (verde do Luck, escuro — o toon clareia).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import ab_common as ab  # noqa: E402

S = 0.26          # lado
BEVEL = 0.045     # raio dos cantos
PIP_R = 0.025     # raio dos pontos
PIP_OFF = 0.06    # distância do centro da face até os pontos dos cantos

# posições (u, v) dos pontos de cada número na face, em múltiplos de PIP_OFF
LAYOUT = {
    1: [(0, 0)],
    2: [(-1, -1), (1, 1)],
    3: [(-1, -1), (0, 0), (1, 1)],
    4: [(-1, -1), (1, -1), (-1, 1), (1, 1)],
    5: [(-1, -1), (1, -1), (0, 0), (-1, 1), (1, 1)],
    6: [(-1, -1), (-1, 0), (-1, 1), (1, -1), (1, 0), (1, 1)],
}

# face -> (normal no Blender, eixo u, eixo v, número)
FACES = [
    (Vector((0, 0, 1)), Vector((1, 0, 0)), Vector((0, 1, 0)), 6),    # cima
    (Vector((0, 0, -1)), Vector((1, 0, 0)), Vector((0, 1, 0)), 1),   # baixo
    (Vector((0, -1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1)), 2),   # frente (+Z na Godot)
    (Vector((0, 1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1)), 5),    # trás
    (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)), 3),    # direita
    (Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)), 4),   # esquerda
]


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def main():
    ab.reset_scene()
    bpy.ops.mesh.primitive_cube_add(size=S)
    cube = bpy.context.active_object
    cube.name = "Dado"
    bev = cube.modifiers.new("Bevel", "BEVEL")
    bev.width = BEVEL
    bev.segments = 4
    bev.limit_method = "NONE"
    ab.material(cube, "Dado", srgb("#EDE6D6"), roughness=0.4)

    pips = []
    for n, u, v, num in FACES:
        for (a, b) in LAYOUT[num]:
            c = n * (S / 2 - 0.001) + u * (a * PIP_OFF) + v * (b * PIP_OFF)
            bpy.ops.mesh.primitive_uv_sphere_add(radius=PIP_R, segments=12, ring_count=6, location=c)
            p = bpy.context.active_object
            # achata na direção da normal
            p.scale = Vector((1, 1, 1)) - Vector((abs(n.x), abs(n.y), abs(n.z))) * 0.6
            pips.append(p)
    ab.finalize(*pips)
    ab.select_only(pips)
    bpy.context.view_layer.objects.active = pips[0]
    bpy.ops.object.join()
    pip = bpy.context.active_object
    pip.name = "Pontos"
    pip.data.materials.clear()
    ab.material(pip, "Ponto", srgb("#14552B"), roughness=0.5)
    ab.select_only([pip])
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    ab.finalize(cube, pip)
    ab.export_glb("dado")
    ab.save_blend("dado")


main()
