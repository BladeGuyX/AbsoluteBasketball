"""
Absolute Basketball — cesta completa (Etapa 7). Concept: art_source/concepts/hoop.png

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_hoop.py
Saída:  assets/models/hoop_azul.glb e hoop_laranja.glb  +  art_source/blend/hoop_azul.blend / hoop_laranja.blend

Duas versões com a MESMA forma e os MESMOS nomes de objeto/material; muda só a paleta:
- azul    = cesta defendida pelo time AZUL (cesta 0, em -X na Godot)
- laranja = cesta defendida pelo time LARANJA (cesta 1, em +X)

Objetos separados (o jogo posiciona cada um pelo tuning e anima Aro/Rede):
  Aro       aro + suporte + ganchinhos da rede   origem = centro do aro (0, 0, 4,3)
  Rede      cordões em losango (low-poly)        origem = centro do aro (balança por shader na Godot)
  Tabela    vidro + moldura + acolchoado de baixo origem = centro da tabela
  Quadrado  marcação branca na frente do vidro   origem = centro do quadrado
  Braco     braço horizontal + diagonal + placa  origem = eixo do poste, no chão
  Poste     coluna                               origem = eixo do poste, no chão
  Base      base acolchoada com faixa, estrela e rodinhas   origem = eixo do poste, no chão

Medidas = padrões do Tuning (docs/ARTE.md): aro raio 0,55 a 4,3 m; tabela 3,3 x 1,85 x 0,08 com a frente a 0,80 m
atrás do centro do aro e base a 4,025 m; poste a 2,6 m atrás do aro; quadrado = zona da tabelinha (CourtGeo.Bank*).
Frente (lado do aro) = -Y. Origem da cesta = chão logo abaixo do centro do aro.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import ab_common as ab  # noqa: E402

# ---- medidas (m) -------------------------------------------------------------------------------------------
RIM_R = 0.55            # Tuning.RimRadius (raio do eixo do tubo)
RIM_TUBE = 0.035        # raio do tubo do aro
HOOP_H = 4.3            # Tuning.HoopHeight
BOARD_W, BOARD_H, BOARD_T = 3.3, 1.85, 0.08
BOARD_FRONT = RIM_R + 0.25                       # 0,80
BOARD_Y = BOARD_FRONT + BOARD_T / 2              # centro da tabela em Y
BOARD_BOTTOM = HOOP_H - RIM_R * 0.5              # 4,025
BOARD_Z = BOARD_BOTTOM + BOARD_H / 2
POLE_Y = 2.6                                     # 0,8 + HoopInset 1,8
POLE_TOP = BOARD_BOTTOM + BOARD_H - 0.35         # coluna termina um pouco abaixo do topo da tabela
POLE_SIZE = 0.30
SQ_W = RIM_R * 1.6 * 2                           # CourtGeo.BankHalfWidth * 2
SQ_Z0, SQ_Z1 = HOOP_H + 0.05, min(HOOP_H + RIM_R * 2.4, BOARD_BOTTOM + BOARD_H - 0.05)
SQ_LINE = 0.07
NET_LEN = RIM_R * 1.7                            # ~0,94
NET_BOTTOM_R = RIM_R * 0.62                      # ~0,34
NET_COLS = 12                                    # ganchos no aro
NET_ROWS = 5                                     # fileiras de nós abaixo do aro
NET_STRAND = 0.009                               # meia-espessura do cordão


def srgb(h):
    h = h.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return tuple(out)


# Paletas (concept: #1F2546 escuro, #3B4789 azul, #EE5236 aro, branco, #A9DDEE vidro)
PALETTES = {
    "azul": dict(Escuro="#1F2546", Acolchoado="#3B4789", Aro="#EE5236", Branco="#F4F6FA",
                 Faixa="#DCE0F2", Vidro="#9AD5EC", Rede="#FFFFFF", Borracha="#2A2C33"),
    "laranja": dict(Escuro="#5E2216", Acolchoado="#D9542C", Aro="#EE5236", Branco="#FFF7F2",
                    Faixa="#FFE1D2", Vidro="#FFBE9C", Rede="#FFFFFF", Borracha="#2A2C33"),
}

MATS = {}


def mat(name):
    return MATS[name]


# ---- utilitários de malha ----------------------------------------------------------------------------------
def apply_mods(o):
    ab.select_only([o])
    for m in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def box(size, loc, m, bevel=0.0, segs=2, rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.scale = size
    if rot is not None:
        o.rotation_mode = "QUATERNION"
        o.rotation_quaternion = rot
    ab.select_only([o])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        b = o.modifiers.new("bevel", "BEVEL")
        b.width = bevel
        b.segments = segs
        b.limit_method = "ANGLE"
        apply_mods(o)
    o.data.materials.append(m)
    return o


def beam(p0, p1, thick, m, bevel=0.0):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    q = d.normalized().to_track_quat("X", "Z")
    return box((d.length, thick[0], thick[1]), (p0 + p1) / 2, m, bevel, 2, q)


def cylinder(r, depth, loc, m, verts=12, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.data.materials.append(m)
    return o


def join(name, objs, origin):
    """Junta as peças num objeto só com a origem no ponto dado (location fica = origin)."""
    ab.select_only(objs)
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    bpy.context.scene.cursor.location = Vector(origin)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    for p in o.data.polygons:
        p.use_smooth = False
    return o


def star(cx, y, cz, r_out, r_in, depth, m):
    """Estrela de 5 pontas no plano XZ (face para -Y), com espessura."""
    me = bpy.data.meshes.new("estrela")
    bm = bmesh.new()
    front, back = [], []
    for i in range(10):
        a = math.pi / 2 + i * math.pi / 5
        r = r_out if i % 2 == 0 else r_in
        x, z = cx + math.cos(a) * r, cz + math.sin(a) * r
        front.append(bm.verts.new((x, y, z)))
        back.append(bm.verts.new((x, y + depth, z)))
    bm.faces.new(front[::-1])
    bm.faces.new(back)
    for i in range(10):
        j = (i + 1) % 10
        bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("estrela", me)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(m)
    return o


# ---- peças -------------------------------------------------------------------------------------------------
def make_rim():
    parts = []
    bpy.ops.mesh.primitive_torus_add(major_radius=RIM_R, minor_radius=RIM_TUBE, major_segments=32, minor_segments=8,
                                     location=(0, 0, HOOP_H))
    t = bpy.context.active_object
    t.data.materials.append(mat("Aro"))
    parts.append(t)
    # suporte: pescoço do aro até a placa na tabela (como na concept, um bloco vermelho)
    neck_y0 = RIM_R - 0.01
    parts.append(box((0.22, BOARD_FRONT - neck_y0, 0.06), (0, (neck_y0 + BOARD_FRONT) / 2, HOOP_H), mat("Aro"), 0.01))
    parts.append(box((0.10, 0.14, 0.16), (0, BOARD_FRONT - 0.07, HOOP_H - 0.08), mat("Aro"), 0.015))   # mão-francesa
    parts.append(box((0.36, 0.035, 0.30), (0, BOARD_FRONT - 0.0175, HOOP_H - 0.06), mat("Aro"), 0.012))  # placa
    # ganchinhos da rede embaixo do aro
    for i in range(NET_COLS):
        a = 2 * math.pi * i / NET_COLS
        p = Vector((math.sin(a) * RIM_R, -math.cos(a) * RIM_R, HOOP_H - RIM_TUBE - 0.018))
        q = Vector((math.sin(a), -math.cos(a), 0)).to_track_quat("X", "Z")
        parts.append(box((0.025, 0.018, 0.04), p, mat("Aro"), 0, 1, q))
    return join("Aro", parts, (0, 0, HOOP_H))


def net_node(i, k):
    """Posição do nó i da fileira k (k = 0 é o topo, preso no aro). Fileiras ímpares deslocadas meio passo."""
    t = k / NET_ROWS
    shape = 1 - (1 - t) ** 1.6                       # afunila rápido no topo, quase reto embaixo (como a concept)
    r = RIM_R + (NET_BOTTOM_R - RIM_R) * shape
    z = HOOP_H - RIM_TUBE - 0.03 - (NET_LEN - RIM_TUBE - 0.03) * t
    a = 2 * math.pi * (i + 0.5 * (k % 2)) / NET_COLS
    return Vector((math.sin(a) * r, -math.cos(a) * r, z))


def make_net():
    """Rede: cordões finos (tubos de 4 lados) ligando os nós em losango + anel de baixo.
    UV: u = volta (0..1), v = profundidade (0 no aro, 1 embaixo) — o shader da Godot usa a altura do vértice."""
    me = bpy.data.meshes.new("Rede")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    def strand(a, b):
        d = (b - a).normalized()
        side = d.cross(Vector((0, 0, 1)))
        if side.length < 1e-4:
            side = Vector((1, 0, 0))
        side.normalize()
        up = side.cross(d).normalized()
        offs = [side * NET_STRAND, up * NET_STRAND, -side * NET_STRAND, -up * NET_STRAND]
        va = [bm.verts.new(a + o) for o in offs]
        vb = [bm.verts.new(b + o) for o in offs]
        ua = (HOOP_H - a.z) / NET_LEN
        ub = (HOOP_H - b.z) / NET_LEN
        uu = (math.atan2(a.x, -a.y) / (2 * math.pi)) % 1.0
        for s in range(4):
            n = (s + 1) % 4
            f = bm.faces.new((va[s], va[n], vb[n], vb[s]))
            for loop, (u, v) in zip(f.loops, ((uu, ua), (uu, ua), (uu, ub), (uu, ub))):
                loop[uv].uv = (u, v)

    for k in range(NET_ROWS):
        for i in range(NET_COLS):
            p = net_node(i, k)
            if k % 2 == 0:   # nó par em i -> nós ímpares i (à direita) e i-1 (à esquerda)
                strand(p, net_node(i, k + 1))
                strand(p, net_node((i - 1) % NET_COLS, k + 1))
            else:            # nó ímpar (i + 0,5) -> nós pares i e i+1
                strand(p, net_node(i, k + 1))
                strand(p, net_node((i + 1) % NET_COLS, k + 1))
    for i in range(NET_COLS):  # anel de baixo
        strand(net_node(i, NET_ROWS), net_node((i + 1) % NET_COLS, NET_ROWS))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("Rede", me)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(mat("Rede"))
    bpy.context.scene.cursor.location = Vector((0, 0, HOOP_H))
    ab.select_only([o])
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    return o


def make_board():
    parts = []
    frame = 0.06                         # borda escura em volta do vidro
    pad_h = 0.20                         # acolchoado de baixo (faixa grossa da concept)
    ear_h = 0.55                         # "orelhas" do acolchoado subindo pelas laterais
    ear_w = 0.13
    glass_h = BOARD_H - frame - pad_h
    glass_w = BOARD_W - 2 * frame
    # vidro (um pouco mais fino que a tabela; a moldura fecha a espessura)
    parts.append(box((glass_w, BOARD_T * 0.7, glass_h), (0, BOARD_Y, BOARD_BOTTOM + pad_h + glass_h / 2), mat("Vidro")))
    # moldura: topo e laterais
    top_z = BOARD_BOTTOM + BOARD_H - frame / 2
    parts.append(box((BOARD_W, BOARD_T, frame), (0, BOARD_Y, top_z), mat("Escuro"), 0.015))
    for sx in (-1, 1):
        parts.append(box((frame, BOARD_T, BOARD_H - pad_h), (sx * (BOARD_W - frame) / 2, BOARD_Y, BOARD_BOTTOM + pad_h + (BOARD_H - pad_h) / 2),
                         mat("Escuro"), 0.015))
        # orelhas do acolchoado (mais grossas que a moldura, para frente)
        parts.append(box((ear_w, BOARD_T + 0.04, ear_h), (sx * (BOARD_W - ear_w) / 2, BOARD_Y + 0.02, BOARD_BOTTOM + ear_h / 2),
                         mat("Escuro"), 0.03, 3))
    # acolchoado de baixo (frente rente à face da tabela: a bola não atravessa nada visível)
    parts.append(box((BOARD_W, BOARD_T + 0.04, pad_h), (0, BOARD_Y + 0.02, BOARD_BOTTOM + pad_h / 2), mat("Escuro"), 0.03, 3))
    # filete branco entre o vidro e a moldura (a "linha clara" da concept)
    inner = 0.025
    gz0, gz1 = BOARD_BOTTOM + pad_h, BOARD_BOTTOM + BOARD_H - frame
    y = BOARD_FRONT - 0.002
    parts.append(box((glass_w, 0.006, inner), (0, y, gz1 - inner / 2), mat("Branco")))
    for sx in (-1, 1):
        parts.append(box((inner, 0.006, gz1 - gz0 - ear_h + pad_h), (sx * (glass_w - inner) / 2, y, (gz1 + BOARD_BOTTOM + ear_h) / 2), mat("Branco")))
    return join("Tabela", parts, (0, BOARD_Y, BOARD_Z))


def make_square():
    parts = []
    y = BOARD_FRONT - 0.006
    h = SQ_Z1 - SQ_Z0
    zc = (SQ_Z0 + SQ_Z1) / 2
    parts.append(box((SQ_W, 0.008, SQ_LINE), (0, y, SQ_Z1 - SQ_LINE / 2), mat("Branco")))
    parts.append(box((SQ_W, 0.008, SQ_LINE), (0, y, SQ_Z0 + SQ_LINE / 2), mat("Branco")))
    for sx in (-1, 1):
        parts.append(box((SQ_LINE, 0.008, h - 2 * SQ_LINE), (sx * (SQ_W - SQ_LINE) / 2, y, zc), mat("Branco")))
    return join("Quadrado", parts, (0, BOARD_FRONT, zc))


def make_arm():
    parts = []
    back = BOARD_FRONT + BOARD_T
    th = (0.14, 0.14)
    z_top = POLE_TOP - 0.12
    # braço horizontal do topo do poste até as costas da tabela
    parts.append(beam((0, POLE_Y, z_top), (0, back + 0.03, z_top), th, mat("Escuro"), 0.02))
    # diagonal: do poste (embaixo) até a tabela (em cima), como na vista de costas
    parts.append(beam((0, POLE_Y - 0.05, HOOP_H - 0.55), (0, back + 0.05, BOARD_Z - 0.05), th, mat("Escuro"), 0.02))
    # placa nas costas da tabela
    parts.append(box((0.62, 0.05, 0.70), (0, back + 0.025, BOARD_Z + 0.05), mat("Escuro"), 0.015))
    # abraçadeiras no poste
    for z in (z_top, HOOP_H - 0.55):
        parts.append(box((POLE_SIZE + 0.08, POLE_SIZE + 0.08, 0.16), (0, POLE_Y, z), mat("Escuro"), 0.02))
    return join("Braco", parts, (0, POLE_Y, 0))


def make_pole():
    o = box((POLE_SIZE, POLE_SIZE, POLE_TOP - 1.0), (0, POLE_Y, 1.0 + (POLE_TOP - 1.0) / 2), mat("Acolchoado"), 0.05, 3)
    cap = box((POLE_SIZE + 0.04, POLE_SIZE + 0.04, 0.06), (0, POLE_Y, POLE_TOP + 0.03), mat("Escuro"), 0.015)
    return join("Poste", [o, cap], (0, POLE_Y, 0))


def make_base():
    parts = []
    w, d = 0.95, 1.05                    # largura (através da quadra) x profundidade
    band_h = 0.30
    pad_h = 1.10
    # acolchoado de cima (topo bem arredondado, como a "cúpula" da vista frontal)
    pad = box((w, d, pad_h), (0, POLE_Y, band_h + pad_h / 2 - 0.02), mat("Acolchoado"), 0.30, 4)
    parts.append(pad)
    # faixa clara de baixo (um pouco mais larga)
    parts.append(box((w + 0.05, d + 0.05, band_h), (0, POLE_Y, 0.05 + band_h / 2), mat("Faixa"), 0.06, 3))
    # estrela na frente da faixa
    parts.append(star(0, POLE_Y - (d + 0.05) / 2 - 0.012, 0.05 + band_h / 2, 0.085, 0.036, 0.02, mat("Acolchoado")))
    # rodinhas
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(cylinder(0.05, 0.05, (sx * (w / 2 - 0.12), POLE_Y + sy * (d / 2 - 0.14), 0.05), mat("Borracha"), 10,
                                  (0, math.pi / 2, 0)))
    return join("Base", parts, (0, POLE_Y, 0))


def build(variant):
    ab.reset_scene()
    MATS.clear()
    pal = PALETTES[variant]
    for name, hexc in pal.items():
        rough = 0.15 if name == "Vidro" else 0.7
        MATS[name] = ab.material(None, name, srgb(hexc), roughness=rough)
    objs = [make_rim(), make_net(), make_board(), make_square(), make_arm(), make_pole(), make_base()]
    ab.finalize(*objs)
    total = 0
    for o in objs:
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        total += tris
        print(f"[AB] {variant}/{o.name}: {tris} triângulos, origem {tuple(round(c, 3) for c in o.location)}")
    print(f"[AB] {variant}: total {total} triângulos")
    ab.export_glb(f"hoop_{variant}", objs)
    ab.save_blend(f"hoop_{variant}")


for v in ("azul", "laranja"):
    build(v)
