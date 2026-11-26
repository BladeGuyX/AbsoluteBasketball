"""
Personagem "One" (5º personagem) — concept: art_source/concepts/one.png
-> assets/models/one.glb + art_source/blend/one.blend

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_one.py

Mesmo esqueleto e proporções chibi do Rei (1,4 m até a ponta do cabelo). Reaproveita de make_rei.py a cabeça, os rostos,
a pele, a túnica (o casaco do Rei com outra barra) e as botas; de make_ametista.py as mechas Bézier (strand) e o quadril.
- Corpo      túnica cinza-escura com barra rasgada roxa e runas roxas no peito, cinto com fivela roxa, capa preta longa e
             rasgada (forro roxo) com gola alta de vampiro e gema roxa no fecho, calça preta com faixa roxa, botas roxas
- Cabeca     cabeça, pescoço, orelhas e cabelo preto bagunçado (franja até as sobrancelhas, mechas na nuca) — osso Head
- Rosto_*    expressões (os MESMOS nomes de objeto do Rei; os rótulos da roda ficam em Scripts/Game/Characters.cs)
- MaoEsquerda / MaoDireita   esferas SEM esqueleto (a Godot move por código)
Materiais "Runa", "Gema", "OlhoBrilho" e "PupilaBrilho" ficam sem sombra na Godot (brilho), ver CharacterVisual.ApplyMaterials.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402
import ab_common as ab  # noqa: E402
import make_rig_base  # noqa: E402
import make_rei as rei  # noqa: E402
import make_ametista as ame  # noqa: E402
import make_raio as raio  # noqa: E402

# chave usada pelas funções reaproveitadas -> (nome do material no .glb, cor)
PAL = {
    "Pele": ("Pele", "#EDE3E2"),
    "Cabelo": ("Cabelo", "#1F1E26"), "CabeloEscuro": ("CabeloEscuro", "#121117"), "CabeloBrilho": ("CabeloBrilho", "#3D3A4C"),
    "Casaco": ("Casaco", "#35363E"), "Forro": ("Forro", "#3B1F5E"),
    "Roxo": ("Roxo", "#5E2E96"), "Runa": ("Runa", "#A066F5"), "Cinto": ("Cinto", "#1E1B24"),
    "Capa": ("Capa", "#151419"), "ForroCapa": ("ForroCapa", "#4A2378"), "Gema": ("Gema", "#B76BFF"),
    "Calca": ("Calca", "#1A1A20"), "Botas": ("Botas", "#5A3290"), "Sola": ("Sola", "#A47EE2"), "Cadarco": ("Cadarco", "#2A1840"),
    "Olhos": ("Olhos", "#9C6EE0"), "Pupila": ("Pupila", "#3A1A66"), "Brilho": ("Brilho", "#FFFFFF"),
    "OlhoBrilho": ("OlhoBrilho", "#C97CFF"), "PupilaBrilho": ("PupilaBrilho", "#F6E6FF"),
    "Tinta": ("Tinta", "#1A1620"), "Boca": ("Boca", "#4A1E2E"), "Dentes": ("Dentes", "#FFFFFF"),
}
EMIT = {"Runa", "Gema", "OlhoBrilho", "PupilaBrilho"}
RIG = make_rig_base.PROP
smooth, new_obj, add_mats, join, solidify, mat = rei.smooth, rei.new_obj, rei.add_mats, rei.join, rei.solidify, rei.mat
outward = raio.outward


def tri(x):
    """Onda triangular 0..1..0 com período 1."""
    f = x - math.floor(x)
    return 1 - abs(2 * f - 1)


# ---------------------------------------------------------------------------------------------------------
# Túnica = casaco do Rei fechado na frente, com a barra rasgada (pontas) um pouco mais alta
def tunic_hem(th):
    return 0.305 + 0.02 * math.cos(th) - 0.04 * tri(th * 9 / (2 * math.pi))


def tunic_front(z):
    return max(0.0, 0.40 - z) * 0.9


def tunic_back(z):
    return max(0.0, 0.38 - z) * 0.5


def make_tunic():
    old = rei.hem_z, rei.front_open, rei.back_open
    rei.hem_z, rei.front_open, rei.back_open = tunic_hem, tunic_front, tunic_back
    try:
        return rei.make_coat()
    finally:
        rei.hem_z, rei.front_open, rei.back_open = old


def coat_pt(th, z, grow=0.0):
    rx, ry = rei.coat_r(z)
    return Vector(((rx + grow) * math.sin(th), -(ry + grow) * math.cos(th), z))


def make_hem_band():
    """Faixa roxa na barra da túnica (as pontas rasgadas ficam roxas, como na concept)."""
    bm = bmesh.new()
    NR, NC = 3, rei.SEG // 2
    for side in (0, 1):
        grid = []
        for r in range(NR + 1):
            t = r / NR
            row = []
            for c in range(NC + 1):
                u = c / NC
                th = 0.0
                for _ in range(2):
                    z = tunic_hem(th) + 0.065 * (1 - t)
                    hf, hb = tunic_front(z), tunic_back(z)
                    th = hf + u * (math.pi - hb - hf) if side == 0 else math.pi + hb + u * (math.pi - hb - hf)
                z = tunic_hem(th) + 0.065 * (1 - t)
                row.append(bm.verts.new(coat_pt(th, z, 0.005)))
            grid.append(row)
        for r in range(NR):
            for c in range(NC):
                bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("barra_roxa", bm)
    outward(o)
    add_mats(o, "Roxo")
    smooth(o)
    return o


def coat_strip(pts, w, m="Runa", grow=0.004, name="runa"):
    """Linha (u = metros na horizontal a partir da frente, z) desenhada sobre a túnica."""
    bm = bmesh.new()
    left, right = [], []
    n = len(pts)
    for i, (u, z) in enumerate(pts):
        a, b = pts[max(i - 1, 0)], pts[min(i + 1, n - 1)]
        tu, tz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(tu, tz) or 1.0
        nu, nz = -tz / L * w * 0.5, tu / L * w * 0.5
        for (uu, zz), lst in (((u + nu, z + nz), left), ((u - nu, z - nz), right)):
            rx, _ = rei.coat_r(zz)
            lst.append(bm.verts.new(coat_pt(uu / rx, zz, grow)))
    for i in range(n - 1):
        bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
    o = new_obj(name, bm)
    outward(o)
    add_mats(o, m)
    smooth(o)
    return o


def make_runes():
    """Desenho de 'árvore'/circuito roxo no peito (da gola para baixo, com galhos)."""
    lines = [
        [(0.0, 0.79), (0.0, 0.70), (0.0, 0.575)],
        [(0.0, 0.752), (0.034, 0.728), (0.034, 0.688)],
        [(0.0, 0.752), (-0.034, 0.728), (-0.034, 0.688)],
        [(0.0, 0.672), (0.056, 0.64), (0.056, 0.598)],
        [(0.0, 0.672), (-0.056, 0.64), (-0.056, 0.598)],
        [(0.0, 0.625), (0.026, 0.6), (0.026, 0.58)],
        [(0.0, 0.625), (-0.026, 0.6), (-0.026, 0.58)],
    ]
    parts = [coat_strip(p, 0.0085) for p in lines]
    # pontinhos nas pontas dos galhos
    for u, z in ((0.034, 0.684), (-0.034, 0.684), (0.056, 0.594), (-0.056, 0.594), (0.0, 0.571)):
        parts.append(coat_strip([(u, z + 0.006), (u, z - 0.006)], 0.016, name="runa_ponto"))
    return parts


def make_tunic_collar():
    """Golinha em pé da túnica (por baixo da gola da capa), com friso roxo."""
    o = ame.ell_tube([(0.772, 0.106, 0.092, 0.006), (0.80, 0.098, 0.086, 0.008), (0.825, 0.092, 0.082, 0.01)], 28, "gola_tunica")
    outward(o)
    add_mats(o, "Casaco")
    smooth(o)
    solidify(o, 0.008, -1.0)
    edge = ame.ell_tube([(0.818, 0.095, 0.085, 0.01), (0.83, 0.093, 0.083, 0.01)], 28, "friso_tunica")
    outward(edge)
    add_mats(edge, "Roxo")
    smooth(edge)
    return [o, edge]


def make_belt():
    rings = []
    for z, k in ((0.452, 0.004), (0.458, 0.009), (0.486, 0.009), (0.492, 0.004)):
        rx, ry = rei.coat_r(z)
        rings.append((z, rx + k, ry + k, 0.0))
    o = ame.ell_tube(rings, 40, "cinto")
    outward(o)
    add_mats(o, "Cinto")
    smooth(o)
    rx, ry = rei.coat_r(0.472)
    buckle = rei.rounded_box((0.058, 0.014, 0.042), (0, -(ry + 0.016), 0.472), "Roxo", 0.006, 2)
    inner = rei.rounded_box((0.032, 0.01, 0.022), (0, -(ry + 0.022), 0.472), "Cinto", 0.004, 1)
    studs = []
    for x in (-0.12, -0.075, 0.075, 0.12):
        th = math.asin(max(-1.0, min(1.0, x / (rx + 0.01))))
        p = coat_pt(th, 0.472, 0.012)
        studs.append(rei.rounded_box((0.012, 0.008, 0.012), tuple(p), "Roxo", 0.004, 1))
    return [o, buckle, inner] + studs


# ---------------------------------------------------------------------------------------------------------
# Capa: preta, aberta na frente, do pescoço até perto do chão atrás, barra rasgada; forro roxo
CAPE_TOP = 0.795


def cape_r(z):
    rx, ry = rei.coat_r(max(z, 0.28))
    extra = 0.022 + max(0.0, 0.47 - z) * 0.13
    return rx + extra, ry + extra


def cape_gap(z):
    """Meia abertura da frente (rad): fechada no pescoço, abre para baixo (a capa fica nas laterais e atrás)."""
    k = min(1.0, max(0.0, (CAPE_TOP - z) / (CAPE_TOP - 0.45)))
    return 0.42 + 0.78 * (k ** 0.7) + max(0.0, 0.45 - z) * 0.25


def cape_hem(th):
    back = 0.5 - 0.5 * math.cos(th)            # 1 atrás, 0 na frente
    return 0.235 - 0.135 * back + 0.05 * tri(th * 11 / (2 * math.pi) + 0.25)


def cape_pt(th, z):
    rx, ry = cape_r(z)
    return Vector((rx * math.sin(th), 0.01 - ry * math.cos(th), z))


def make_cape():
    bm = bmesh.new()
    NR, NC = 16, 60
    grid = []
    for r in range(NR + 1):
        t = r / NR
        row = []
        for c in range(NC + 1):
            u = c / NC
            th = math.pi
            for _ in range(3):
                z = CAPE_TOP + (cape_hem(th) - CAPE_TOP) * t
                g = cape_gap(z)
                th = g + u * (2 * math.pi - 2 * g)
            z = CAPE_TOP + (cape_hem(th) - CAPE_TOP) * t
            row.append(bm.verts.new(cape_pt(th, z)))
        grid.append(row)
    for r in range(NR):
        for c in range(NC):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("capa", bm)
    outward(o)
    add_mats(o, "Capa", "ForroCapa")
    smooth(o)
    solidify(o, 0.012, -1.0, mat_off=1, rim_off=1)
    return o


def collar_pt(th, t):
    back = 0.5 - 0.5 * math.cos(th)
    ztop = 0.83 + 0.12 * back ** 1.5   # na frente fica abaixo do queixo; atrás sobe até a nuca
    z = 0.772 + (ztop - 0.772) * t
    flare = t ** 1.6
    return Vector(((0.128 + 0.07 * flare) * math.sin(th), 0.012 - (0.11 + 0.055 * flare) * math.cos(th), z))


def make_cape_collar():
    """Gola alta de vampiro, aberta na frente e virada para fora em cima; roxa por dentro, friso roxo na borda."""
    gap, n, rows = 0.5, 34, 5
    bm = bmesh.new()
    grid = [[bm.verts.new(collar_pt(gap + (2 * math.pi - 2 * gap) * c / n, r / rows)) for c in range(n + 1)] for r in range(rows + 1)]
    for r in range(rows):
        for c in range(n):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("gola_capa", bm)
    outward(o)
    add_mats(o, "Capa", "ForroCapa")
    smooth(o)
    solidify(o, 0.012, -1.0, mat_off=1, rim_off=1)
    # friso roxo na borda de cima
    bm = bmesh.new()
    r0, r1 = [], []
    for c in range(n + 1):
        th = gap + (2 * math.pi - 2 * gap) * c / n
        r0.append(bm.verts.new(collar_pt(th, 0.9) * Vector((1.03, 1.0, 1.0)) + Vector((0, 0.0, 0))))
        r1.append(bm.verts.new(collar_pt(th, 1.0) * Vector((1.03, 1.0, 1.0))))
    for c in range(n):
        bm.faces.new((r0[c], r0[c + 1], r1[c + 1], r1[c]))
    e = new_obj("friso_gola", bm)
    outward(e)
    add_mats(e, "Roxo")
    smooth(e)
    solidify(e, 0.016, 0.0)
    return [o, e]


def make_gem():
    """Gema roxa (losango) no fecho da capa, embaixo da golinha, com um encaixe escuro atrás."""
    cx, cy, cz = 0.0, -0.128, 0.79
    bm = bmesh.new()
    top = bm.verts.new((cx, cy, cz + 0.034))
    bot = bm.verts.new((cx, cy, cz - 0.034))
    front = bm.verts.new((cx, cy - 0.018, cz))
    back = bm.verts.new((cx, cy + 0.006, cz))
    l = bm.verts.new((cx - 0.021, cy - 0.004, cz))
    r = bm.verts.new((cx + 0.021, cy - 0.004, cz))
    for a, b in ((l, front), (front, r), (r, back), (back, l)):
        bm.faces.new((top, a, b))
        bm.faces.new((bot, b, a))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    g = new_obj("gema", bm)
    add_mats(g, "Gema")
    for p in g.data.polygons:
        p.use_smooth = False
    seat = rei.rounded_box((0.05, 0.012, 0.074), (cx, cy + 0.008, cz), "Cinto", 0.008, 2)
    rim = rei.rounded_box((0.056, 0.008, 0.08), (cx, cy + 0.012, cz), "Roxo", 0.008, 2)
    return [g, seat, rim]


# ---------------------------------------------------------------------------------------------------------
# Pernas: calça preta com faixa roxa reta no lado de fora
def make_leg_stripes():
    parts = []
    for s in (1, -1):
        cx = s * RIG["leg_x"]
        pts = [(0.0, 0.47 - 0.27 * i / 6) for i in range(7)]
        bm = bmesh.new()
        left, right = [], []
        w = 0.014
        for y, z in pts:
            for yy, lst in ((y + w * 0.5, left), (y - w * 0.5, right)):
                r = raio.leg_r(z)
                x = cx + s * (math.sqrt(max(r * r - yy * yy, 0.0)) + 0.003)
                lst.append(bm.verts.new((x, yy, z)))
        for i in range(len(pts) - 1):
            bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
        o = new_obj("faixa_perna", bm)
        o.data.update()
        if sum(p.normal.x * s for p in o.data.polygons) < 0:
            for p in o.data.polygons:
                p.flip()
        add_mats(o, "Roxo")
        smooth(o)
        parts.append(o)
    return parts


# ---------------------------------------------------------------------------------------------------------
# Cabelo: preto, bagunçado; franja cobrindo a testa até as sobrancelhas, mechas ao lado do rosto, nuca em camadas
HAIR_MATS = ame.HAIR_MATS


def hp(az, el, inset=0.95):
    return rei.hair_point(az, el, inset)


def make_hair():
    bpy.ops.mesh.primitive_uv_sphere_add(segments=36, ring_count=18, radius=1.0, location=(0, 0, 0))
    cap = bpy.context.active_object
    bm = bmesh.new()
    bm.from_mesh(cap.data)
    kill = [v for v in bm.verts if (v.co.z < 0.38 and v.co.y < -0.4) or v.co.z < -0.55
            or (-0.55 < v.co.z < 0.05 and abs(v.co.x) > 0.8 and abs(v.co.y) < 0.4)]
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    for v in bm.verts:
        v.co = rei.HAIR_C + Vector((v.co.x * rei.HAIR_R.x, v.co.y * rei.HAIR_R.y, v.co.z * rei.HAIR_R.z))
    bm.to_mesh(cap.data)
    bm.free()
    cap.data.materials.append(mat("Cabelo"))
    smooth(cap)
    solidify(cap, 0.01, -1.0)

    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1.0, location=(0, 0.025, 1.125))
    vol = bpy.context.active_object
    vol.scale = (0.232, 0.225, 0.15)
    ab.select_only([vol])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    vol.data.materials.append(mat("Cabelo"))
    smooth(vol)

    bm = bmesh.new()
    strand = ame.strand
    # topo: mechas bagunçadas saindo do redemoinho (para trás e para os lados, levantando um pouco)
    for i, az in enumerate(range(-165, 180, 30)):
        a = math.radians(az)
        p0, d = hp(az, 72, 0.9)
        out = Vector((math.sin(a), -math.cos(a), 0))
        lift = 0.05 if i % 2 else 0.015
        p2 = p0 + out * 0.15 + Vector((0, 0.03, lift - 0.03))
        p1 = p0 + out * 0.07 + Vector((0, 0.0, 0.05 + lift))
        strand(bm, p0, p1, p2, 0.075, d, flat=0.55, mi=0, n=8, rings=6, keep=0.06)
    # arrepiados no alto da cabeça (como na concept)
    for az, el, up, back in ((170, 82, 0.16, 0.07), (-150, 76, 0.12, 0.09), (40, 84, 0.1, -0.02)):
        p0, d = hp(az, el, 0.92)
        p1 = p0 + Vector((0.0, back * 0.3, up * 0.65))
        p2 = p0 + Vector((math.sin(math.radians(az)) * 0.05, back, up))
        strand(bm, p0, p1, p2, 0.034, d, flat=0.5, mi=0, n=6, rings=5, keep=0.05)
    # franja: mechas pontudas caindo da linha do cabelo até as sobrancelhas, de comprimentos alternados
    for i, (az, zt, sway) in enumerate(((-66, 0.99, -0.03), (-50, 1.03, -0.025), (-34, 1.045, -0.012), (-18, 1.02, 0.0),
                                        (-3, 1.05, 0.01), (12, 1.025, 0.012), (27, 1.045, 0.018), (43, 1.03, 0.025),
                                        (59, 1.0, 0.03))):
        p0, d = hp(az, 52, 0.96)
        x = p0.x * 1.08 + sway
        y = rei.head_surface_y(max(-0.2, min(0.2, x)), zt, 0.03)
        p2 = Vector((x, y, zt))
        p1 = (p0 + p2) * 0.5 + Vector((0, -0.05, 0.035))
        strand(bm, p0, p1, p2, 0.062 if i % 2 == 0 else 0.054, d, flat=0.45, mi=0 if i % 3 else 1, n=8, rings=6, keep=0.04)
    # mechas na frente das orelhas, descendo ao lado do rosto
    for s in (1, -1):
        for az, zt, r in ((70, 0.915, 0.052), (84, 0.935, 0.05)):
            p0, d = hp(s * az, 34, 0.95)
            p2 = Vector((s * 0.218, -0.075 + 0.04 * (az - 70) / 14, zt))
            p1 = (p0 + p2) * 0.5 + Vector((s * 0.035, -0.015, 0.02))
            strand(bm, p0, p1, p2, r, d, flat=0.4, mi=1 if az > 75 else 0, n=7, rings=5, keep=0.05)
    # lados (acima das orelhas): curtas para baixo/trás
    for s in (1, -1):
        for az, el in ((100, 38), (118, 20), (135, 36)):
            p0, d = hp(s * az, el, 0.93)
            p1 = p0 + d * 0.03 + Vector((0, 0.02, -0.04))
            p2 = p0 + d * 0.05 + Vector((s * 0.005, 0.04, -0.11))
            strand(bm, p0, p1, p2, 0.058, d, flat=0.4, mi=1, n=6, rings=4, keep=0.05)
    # nuca: camadas para baixo, pontas abrindo um pouco
    for az, el, L in ((180, 35, 0.17), (155, 32, 0.16), (-155, 32, 0.16), (132, 26, 0.14), (-132, 26, 0.14),
                      (168, 8, 0.15), (-168, 8, 0.15), (145, 4, 0.13), (-145, 4, 0.13), (180, -14, 0.12),
                      (158, -16, 0.11), (-158, -16, 0.11)):
        p0, d = hp(az, el, 0.93)
        p1 = p0 + d * L * 0.15 + Vector((0, 0.012, -L * 0.4))
        p2 = p0 + d * L * 0.38 + Vector((0, 0.025, -L))
        strand(bm, p0, p1, p2, 0.06, d, flat=0.35, mi=0 if el > 10 else 1, n=6, rings=4, keep=0.06)
    # brilho: algumas mechas finas mais claras no topo
    for az in (-35, 10, 150):
        p0, d = hp(az, 64, 1.0)
        a = math.radians(az)
        p2 = p0 + Vector((math.sin(a) * 0.09, -math.cos(a) * 0.09, -0.04))
        strand(bm, p0, p0 + Vector((math.sin(a) * 0.04, -math.cos(a) * 0.04, 0.02)), p2, 0.022, d, flat=0.45, mi=2, n=6, keep=0.1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    sp = new_obj("mechas", bm)
    add_mats(sp, *HAIR_MATS)
    smooth(sp)
    return [cap, vol, sp]


# ---------------------------------------------------------------------------------------------------------
# Rostos (concept: Calm, Focused, Eerie Smile, Cold Glare, Serious, Angry)
MZ = rei.MOUTH_Z


def glow_eyes(o):
    for i, m in enumerate(o.data.materials):
        if m is None:
            continue
        if m.name.startswith("Olhos"):
            o.data.materials[i] = mat("OlhoBrilho")
        elif m.name.startswith("Pupila"):
            o.data.materials[i] = mat("PupilaBrilho")
    return o


def make_faces():
    R = rei
    faces = []

    # Calmo (slot "Serio" = cara padrão): olhos meio fechados, boca pequena reta
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.005, slope=0.12)
        R.brow(f, s, z=1.056, angle=6)
    R.mouth_line(f, [(-0.013, MZ), (0.0, MZ - 0.0008), (0.013, MZ)], 0.005)
    faces.append(f.build("Rosto_Serio"))

    # Focado
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.002, slope=0.3, low=-0.017)
        R.brow(f, s, z=1.047, angle=18)
    R.mouth_line(f, [(-0.012, MZ), (0.0, MZ - 0.0012), (0.012, MZ)], 0.005)
    faces.append(f.build("Rosto_Focado"))

    # Sorriso sinistro (slot "SorrisoFrio"): olhos fechados em arco e sorriso largo e fino
    f = R.Face()
    for s in (1, -1):
        cx, cz = s * R.EYE_X, R.EYE_Z
        f.strip([(cx - 0.042, cz - 0.01), (cx - 0.022, cz + 0.008), (cx, cz + 0.013), (cx + 0.022, cz + 0.008), (cx + 0.042, cz - 0.01)],
                0.011, 0.011, "Tinta", 3)
        R.brow(f, s, z=1.06, angle=-4, arch=0.004)
    f.strip([(-0.03, MZ + 0.008), (-0.017, MZ - 0.003), (0.0, MZ - 0.006), (0.017, MZ - 0.003), (0.03, MZ + 0.008)], 0.006, 0.006, "Tinta", 2)
    faces.append(f.build("Rosto_SorrisoFrio"))

    # Olhar frio (slot "Bravo"): olhos roxos brilhando, sobrancelhas em V, boca para baixo
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.0, slope=0.42, low=-0.015, lid_w=0.011)
        R.brow(f, s, z=1.05, angle=26, w0=0.012, w1=0.005)
    f.strip([(-0.017, MZ - 0.005), (-0.006, MZ - 0.001), (0.006, MZ - 0.001), (0.017, MZ - 0.005)], 0.0055, 0.0055, "Tinta", 2)
    faces.append(glow_eyes(f.build("Rosto_Bravo")))

    # Risada sombria (slot "Feliz"): olhos estreitos, sorriso aberto de lado com dentes
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.0, slope=0.18, low=-0.012)
        R.brow(f, s, z=1.052, angle=14)
    ame.grin_mouth(f, 0.062, 0.03, tilt=0.01, cx=0.002)
    faces.append(glow_eyes(f.build("Rosto_Feliz")))

    # Surpreso (atordoado)
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, a=0.03, b=0.035, pupil=(0.009, 0.012), top_follow=True, lid_w=0.006, wing=False)
        R.brow(f, s, z=1.098, angle=-8, arch=0.006, w0=0.008)
    f.poly(R.ellipse(0.0, MZ - 0.004, 0.01, 0.014, 16), "Boca", 0)
    faces.append(f.build("Rosto_Surpreso"))
    return faces


# ---------------------------------------------------------------------------------------------------------
def cape_weights(x, y, z):
    if z >= 0.47:
        w = rei.spine_weights(z)
        if z > 0.64 and abs(x) > 0.14:
            k = rei.smoothstep(0.14, 0.24, abs(x)) * rei.smoothstep(0.64, 0.73, z) * 0.6
            w = {b: v * (1 - k) for b, v in w.items()}
            w["LeftShoulder" if x > 0 else "RightShoulder"] = k
        return w
    side = "Left" if x > 0 else "Right"
    k = rei.smoothstep(0.47, 0.15, z) * rei.smoothstep(0.06, 0.26, abs(x)) * 0.35
    return {"Hips": 1 - k, f"{side}UpperLeg": k}


def build():
    rig = make_rig_base.build()
    rei.M.clear()
    for key, (name, hexc) in PAL.items():
        rei.M[key] = ab.material(None, name, rei.srgb(hexc), roughness=0.8,
                                 emission=rei.srgb(hexc) if key in EMIT else None)

    tunic = make_tunic()
    band = make_hem_band()
    runes = make_runes()
    tcollar = make_tunic_collar()
    belt = make_belt()
    cape = make_cape()
    ccollar = make_cape_collar()
    gem = make_gem()
    pelvis = ame.make_pelvis()
    legs = rei.make_legs()
    stripes = make_leg_stripes()
    boots = rei.make_boots()

    upper = [tunic, band, cape, pelvis] + runes + tcollar + belt + ccollar + gem
    for o in upper:
        ab.finalize(o)
    for o in [tunic, band] + runes + belt:
        rei.skin(o, rig, rei.coat_weights)
    rei.skin(cape, rig, cape_weights)
    for o in tcollar + ccollar + gem:
        rei.skin(o, rig, lambda x, y, z: {"UpperChest": 0.6, "Neck": 0.4} if z > 0.8 else {"UpperChest": 1.0})
    rei.skin(pelvis, rig, lambda x, y, z: {"Hips": 1.0})
    for o in legs + stripes + boots:
        ab.finalize(o)
        rei.skin(o, rig, rei.leg_weights)
    body = join("Corpo", upper + legs + stripes + boots)

    rei.make_hair = make_hair          # a cabeça do Rei chama make_hair(): usa o cabelo deste personagem
    head = join("Cabeca", rei.make_head())
    ab.finalize(head)
    rei.skin(head, rig, rei.head_weights)

    faces = make_faces()
    for f in faces:
        ab.finalize(f)
        rei.skin(f, rig, lambda x, y, z: {"Head": 1.0})

    hands = []
    for s, nm in ((1, "MaoEsquerda"), (-1, "MaoDireita")):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=0.078, location=(s * 0.40, -0.02, 0.43))
        h = bpy.context.active_object
        h.name = nm
        h.data.name = nm
        h.data.materials.append(mat("Pele"))
        smooth(h)
        ab.finalize(h)
        hands.append(h)

    objs = [rig, body, head] + faces + hands
    tris, top = 0, 0.0
    for o in objs:
        if o.type == "MESH":
            o.data.calc_loop_triangles()
            tris += len(o.data.loop_triangles)
            top = max(top, max((o.matrix_world @ v.co).z for v in o.data.vertices))
    print(f"[AB] one: {tris} triângulos, topo {top:.3f} m")
    return objs


if __name__ == "__main__":
    objs = build()
    ab.export_glb("one", objs, animations=False)
    ab.save_blend("one")
