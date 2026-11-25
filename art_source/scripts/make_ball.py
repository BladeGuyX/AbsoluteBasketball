"""
Absolute Basketball — bola de basquete (Etapa 7). Concept: art_source/concepts/ball.png

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_ball.py
Saída:  assets/models/ball.glb  +  art_source/blend/ball.blend

Forma:
- Esfera de raio 0,22 m (Tuning.BallRadius), origem no centro.
- 4 costuras como faixas levemente em relevo (um objeto só, material "Costura"):
  * 2 circunferências máximas: equador (plano Z=0) e meridiano (plano X=0) -> a "cruz" vista de frente (-Y);
  * 2 costuras curvas em volta dos polos ±X: a distância angular ao polo ondula (theta0 ± amp·cos 2φ),
    ficando mais perto do centro da vista frontal no equador -> o formato ") | (" da concept.
- Cores da concept (sRGB): laranja #E8742A, costura #2B2B2B. Sombra/brilho/textura de gomos pintados são ignorados
  (o jogo usa toon com contorno).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import ab_common as ab  # noqa: E402

R = 0.22               # raio (m) = Tuning.BallRadius
SPHERE_SEG = 32        # segmentos da esfera (meridianos)
SPHERE_RINGS = 16      # anéis da esfera
SEAM_WIDTH = 0.016     # largura da costura (m) ~3,5% do diâmetro, como na concept
SEAM_RAISE = 0.0025    # relevo do meio da faixa acima da superfície
SEAM_SINK = 0.0003     # bordas da faixa levemente abaixo da superfície (sem fresta)
GREAT_SEGS = 48        # segmentos de cada circunferência máxima
CURVE_SEGS = 72        # segmentos de cada costura curva
CURVE_THETA0 = math.radians(58)  # distância angular média da costura curva ao polo ±X
CURVE_AMP = math.radians(16)     # ondulação (no equador fica a theta0+amp do polo; em cima/baixo, theta0-amp)


def srgb(h):
    """'#E8742A' -> cor linear (o Principled BSDF e o glTF usam cor linear)."""
    h = h.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return tuple(out)


ORANGE = srgb("#E8742A")
SEAM = srgb("#2B2B2B")


def great_circle(axis_a, axis_b):
    a, b = Vector(axis_a), Vector(axis_b)
    return [(a * math.cos(t) + b * math.sin(t)).normalized()
            for t in (2 * math.pi * i / GREAT_SEGS for i in range(GREAT_SEGS))]


def curved_seam(sign):
    """Costura curva em volta do polo sign·X. φ = ângulo em volta do eixo X (0 = frente -Y, 90° = topo +Z)."""
    pts = []
    for i in range(CURVE_SEGS):
        phi = 2 * math.pi * i / CURVE_SEGS
        th = CURVE_THETA0 + CURVE_AMP * math.cos(2 * phi)
        x = sign * math.cos(th)
        s = math.sin(th)
        pts.append(Vector((x, -s * math.cos(phi), s * math.sin(phi))).normalized())
    return pts


def add_ribbon(bm, uv, pts, mat_index):
    """Faixa fechada sobre a esfera seguindo pts (direções unitárias): 3 fileiras (borda, meio elevado, borda)."""
    n = len(pts)
    rows = []
    for i, p in enumerate(pts):
        t = (pts[(i + 1) % n] - pts[i - 1]).normalized()
        side = t.cross(p).normalized() * (SEAM_WIDTH / 2) / R
        rows.append((
            bm.verts.new((p + side).normalized() * (R - SEAM_SINK)),
            bm.verts.new(p * (R + SEAM_RAISE)),
            bm.verts.new((p - side).normalized() * (R - SEAM_SINK)),
        ))
    for i in range(n):
        a, b = rows[i], rows[(i + 1) % n]
        for k in range(2):
            f = bm.faces.new((a[k], b[k], b[k + 1], a[k + 1]))
            f.material_index = mat_index
            f.smooth = True
            for loop, (u, v) in zip(f.loops, ((i, k), (i + 1, k), (i + 1, k + 1), (i, k + 1))):
                loop[uv].uv = (u / n, 0.5 + v * 0.02)  # tira fina no meio do UV (cor lisa)


ab.reset_scene()
me = bpy.data.meshes.new("Bola")
obj = bpy.data.objects.new("Bola", me)
bpy.context.collection.objects.link(obj)
ab.material(obj, "Borracha", ORANGE, roughness=0.75)
ab.material(obj, "Costura", SEAM, roughness=0.9)

bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
bmesh.ops.create_uvsphere(bm, u_segments=SPHERE_SEG, v_segments=SPHERE_RINGS, radius=R, calc_uvs=True)
for f in bm.faces:
    f.material_index = 0
    f.smooth = True
# UV da esfera ocupa a metade de baixo; as costuras usam uma faixa fina em cima (sem sobrepor)
for f in bm.faces:
    for loop in f.loops:
        u, v = loop[uv].uv
        loop[uv].uv = (u, v * 0.48)

add_ribbon(bm, uv, great_circle((1, 0, 0), (0, 1, 0)), 1)   # equador (Z=0)
add_ribbon(bm, uv, great_circle((0, 1, 0), (0, 0, 1)), 1)   # meridiano (X=0)
add_ribbon(bm, uv, curved_seam(+1), 1)
add_ribbon(bm, uv, curved_seam(-1), 1)
bm.normal_update()
bm.to_mesh(me)
bm.free()

ab.finalize(obj)
tris = sum(len(p.vertices) - 2 for p in me.polygons)
print(f"[AB] Bola: {len(me.vertices)} vértices, {tris} triângulos")
ab.export_glb("ball", [obj])
ab.save_blend("ball")
