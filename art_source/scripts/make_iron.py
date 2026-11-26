"""
Personagem "Iron" (4º personagem) — concept: art_source/concepts/iron.png
-> assets/models/iron.glb + art_source/blend/iron.blend

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_iron.py

Mesmo esqueleto e proporções chibi do Rei. Reaproveita de make_rei.py a cabeça, os rostos, a pele e as botas; de
make_ametista.py o quadril da calça e o tubo elíptico.
Objetos separados (pedido do usuário):
- Corpo      peitoral de aço (faixa clara no meio), ombreiras com 2 lâminas, gola alta de couro, gibão acolchoado
             marrom (losangos) aparecendo embaixo do peitoral, cinto com fivela, escarcelas (placas nas coxas),
             calça cinza-escura, joelheiras de aço, botas de couro com dobra, biqueira de aço e sola cinza
- Cabeca     cabeça, pescoço, orelhas, cabelo raspado e a cicatriz na sobrancelha direita — no osso Head
- Rosto_*    expressões (os MESMOS nomes de objeto do Rei; rótulos da roda em Scripts/Game/Characters.cs)
- MaoEsquerda / MaoDireita   manoplas de aço (esfera + nós dos dedos + punho de couro) SEM esqueleto
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
import ab_common as ab  # noqa: E402
import make_rig_base  # noqa: E402
import make_rei as rei  # noqa: E402
import make_ametista as ame  # noqa: E402

# chave usada pelas funções -> (nome do material no .glb, cor)
PAL = {
    "Pele": ("Pele", "#A26E4E"),
    "Cabelo": ("Cabelo", "#2B221E"),
    "Aco": ("Aco", "#9EA4AC"), "AcoClaro": ("AcoClaro", "#E4E8EC"), "AcoEscuro": ("AcoEscuro", "#62676F"),
    "Couro": ("Couro", "#6E3F22"), "CouroEscuro": ("CouroEscuro", "#3C2416"),
    "Gibao": ("Gibao", "#4A2E1E"), "GibaoLinha": ("GibaoLinha", "#2A1910"),
    "Calca": ("Calca", "#34363B"),
    "Botas": ("Botas", "#764628"), "Sola": ("Sola", "#4A4B50"), "Cadarco": ("BotaDobra", "#8E5A34"),
    "Cicatriz": ("Cicatriz", "#D39A80"),
    "Olhos": ("Olhos", "#8C98A6"), "Pupila": ("Pupila", "#262C34"), "Brilho": ("Brilho", "#FFFFFF"),
    "Tinta": ("Tinta", "#1A1414"), "Boca": ("Boca", "#561A14"), "Dentes": ("Dentes", "#FFFFFF"),
}
RIG = make_rig_base.PROP
smooth, new_obj, add_mats, join, solidify, mat = rei.smooth, rei.new_obj, rei.add_mats, rei.join, rei.solidify, rei.mat


def outward(o):
    o.data.update()
    if sum(p.normal.dot(Vector((p.center.x, p.center.y, 0))) for p in o.data.polygons) < 0:
        for p in o.data.polygons:
            p.flip()


def table3(tab, z):
    """Perfil (z, rx, ry) interpolado suave."""
    if z <= tab[0][0]:
        return tab[0][1], tab[0][2]
    for (z0, x0, y0), (z1, x1, y1) in zip(tab, tab[1:]):
        if z0 <= z <= z1:
            k = (z - z0) / (z1 - z0)
            k = k * k * (3 - 2 * k)
            return x0 + (x1 - x0) * k, y0 + (y1 - y0) * k
    return tab[-1][1], tab[-1][2]


# ---------------------------------------------------------------------------------------------------------
# Gibão acolchoado (por baixo de tudo): do quadril ao pescoço, com uma sainha curta
GAMB = [(0.315, 0.228, 0.192), (0.36, 0.218, 0.182), (0.42, 0.205, 0.168), (0.50, 0.203, 0.163), (0.60, 0.208, 0.165),
        (0.70, 0.214, 0.158), (0.75, 0.205, 0.145), (0.78, 0.17, 0.122), (0.80, 0.112, 0.095)]


def gamb_pt(th, z, grow=0.0):
    rx, ry = table3(GAMB, z)
    return Vector(((rx + grow) * math.sin(th), -(ry + grow) * math.cos(th), z))


def make_gambeson():
    bm = bmesh.new()
    zs = [0.315, 0.33, 0.36, 0.40, 0.44, 0.48, 0.52, 0.57, 0.62, 0.67, 0.71, 0.745, 0.77, 0.79, 0.80]
    N = 48
    rows = [[bm.verts.new(gamb_pt(2 * math.pi * j / N, z)) for j in range(N)] for z in zs]
    for i in range(len(rows) - 1):
        for j in range(N):
            bm.faces.new((rows[i][j], rows[i][(j + 1) % N], rows[i + 1][(j + 1) % N], rows[i + 1][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("gibao", bm)
    outward(o)
    add_mats(o, "Gibao", "GibaoLinha")
    smooth(o)
    solidify(o, 0.012, -1.0, mat_off=1, rim_off=1)
    return o


def make_quilting():
    """Losangos do acolchoado: linhas finas diagonais na faixa entre o peitoral e o cinto e na sainha."""
    bm = bmesh.new()
    w = 0.0045
    step = 2 * math.pi / 22

    def line(z0, z1, th0, dth):
        n = 8
        left, right = [], []
        for i in range(n + 1):
            k = i / n
            z = z0 + (z1 - z0) * k
            th = th0 + dth * k
            rx, _ = table3(GAMB, z)
            dz = (z1 - z0)
            du = dth * rx
            L = math.hypot(dz, du) or 1.0
            nz, nu = du / L * w * 0.5, -dz / L * w * 0.5
            for (zz, uu), lst in (((z + nz, th + nu / rx), left), ((z - nz, th - nu / rx), right)):
                lst.append(bm.verts.new(gamb_pt(uu, zz, 0.0035)))
        for i in range(n):
            bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))

    for band in ((0.445, 0.495), (0.318, 0.40)):
        z0, z1 = band
        for j in range(22):
            th = j * step
            line(z0, z1, th, step)
            line(z0, z1, th, -step)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("losangos", bm)
    outward(o)
    add_mats(o, "GibaoLinha")
    smooth(o)
    return o


# ---------------------------------------------------------------------------------------------------------
# Peitoral de aço (frente + costas numa peça, abertura do pescoço em cima, borda virada embaixo)
PLATE = [(0.488, 0.240, 0.200), (0.50, 0.233, 0.193), (0.56, 0.231, 0.193), (0.63, 0.238, 0.197), (0.70, 0.244, 0.188),
         (0.745, 0.234, 0.170), (0.772, 0.192, 0.142), (0.788, 0.152, 0.120)]
PLATE_CY = -0.006


def plate_pt(th, z, grow=0.0):
    rx, ry = table3(PLATE, z)
    # frente um pouco mais estufada (peito)
    front = max(0.0, -math.cos(th))
    bulge = 0.012 * front * math.sin(math.pi * min(1.0, max(0.0, (z - 0.52) / 0.24)))
    return Vector(((rx + grow) * math.sin(th), PLATE_CY - (ry + grow + bulge) * math.cos(th), z))


def make_breastplate():
    bm = bmesh.new()
    zs = [0.488, 0.497, 0.51, 0.54, 0.58, 0.62, 0.66, 0.70, 0.73, 0.75, 0.766, 0.778, 0.788]
    N = 56
    rows = [[bm.verts.new(plate_pt(2 * math.pi * j / N, z)) for j in range(N)] for z in zs]
    for i in range(len(rows) - 1):
        for j in range(N):
            bm.faces.new((rows[i][j], rows[i][(j + 1) % N], rows[i + 1][(j + 1) % N], rows[i + 1][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("peitoral", bm)
    outward(o)
    add_mats(o, "Aco", "AcoClaro")
    smooth(o)
    solidify(o, 0.016, -1.0, mat_off=0, rim_off=1)   # bordas claras (brilho do metal)
    return o


def plate_strip(th0, half_w, z0, z1, m, grow=0.004, name="faixa"):
    """Faixa vertical sobre o peitoral (u = largura em metros)."""
    bm = bmesh.new()
    zs = [z0 + (z1 - z0) * i / 10 for i in range(11)]
    cols = 4
    grid = []
    for z in zs:
        rx, _ = table3(PLATE, z)
        grid.append([bm.verts.new(plate_pt(th0 + (-half_w + 2 * half_w * c / cols) / rx, z, grow)) for c in range(cols + 1)])
    for r in range(len(zs) - 1):
        for c in range(cols):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    o = new_obj(name, bm)
    outward(o)
    add_mats(o, m)
    smooth(o)
    return o


def make_plate_details():
    parts = [
        plate_strip(0.0, 0.034, 0.505, 0.772, "AcoClaro"),            # faixa clara no meio da frente
        plate_strip(math.pi, 0.022, 0.51, 0.765, "AcoClaro"),         # e nas costas (mais fina)
    ]
    # rebites nas laterais
    for s in (1, -1):
        for z in (0.53, 0.60, 0.67):
            p = plate_pt(s * 1.25, z, 0.006)
            bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=0.009, location=p)
            r = bpy.context.active_object
            r.data.materials.append(mat("AcoClaro"))
            parts.append(r)
    return parts


def make_gorget():
    """Gola alta de couro (aparece por cima do peitoral, em volta do pescoço)."""
    bm = bmesh.new()
    n, rows = 32, 4
    grid = []
    for r in range(rows + 1):
        t = r / rows
        row = []
        for c in range(n):
            th = 2 * math.pi * c / n
            back = 0.5 - 0.5 * math.cos(th)
            ztop = 0.865 + 0.03 * back
            z = 0.765 + (ztop - 0.765) * t
            row.append(bm.verts.new(((0.128 + 0.01 * t) * math.sin(th), 0.008 - (0.112 + 0.008 * t) * math.cos(th), z)))
        grid.append(row)
    for r in range(rows):
        for c in range(n):
            bm.faces.new((grid[r][c], grid[r][(c + 1) % n], grid[r + 1][(c + 1) % n], grid[r + 1][c]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("gola", bm)
    outward(o)
    add_mats(o, "CouroEscuro", "Couro")
    smooth(o)
    solidify(o, 0.014, -1.0, mat_off=1, rim_off=1)
    return o


# ---------------------------------------------------------------------------------------------------------
# Ombreiras: cúpula grande + 2 lâminas embaixo, inclinadas para fora; correia de couro presa no peitoral
def dome(center, size, tilt_deg, s, keep=-0.25, m="Aco", rim="AcoClaro", thick=0.013, name="cupula"):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=14, radius=1.0, location=(0, 0, 0))
    o = bpy.context.active_object
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < keep], context="VERTS")
    rot = Matrix.Rotation(math.radians(-s * tilt_deg), 3, "Y")
    for v in bm.verts:
        c = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        v.co = rot @ c + Vector(center)
    bm.to_mesh(o.data)
    bm.free()
    o.name = name
    o.data.materials.clear()
    add_mats(o, m, rim)
    smooth(o)
    solidify(o, thick, -1.0, mat_off=0, rim_off=1)
    return o


def make_pauldrons():
    out = []
    for s in (1, -1):
        parts = [
            dome((s * 0.238, 0.0, 0.708), (0.118, 0.142, 0.105), 24, s, keep=-0.3),
            dome((s * 0.282, 0.0, 0.648), (0.092, 0.122, 0.07), 38, s, keep=-0.15, thick=0.011),
            dome((s * 0.305, 0.0, 0.602), (0.076, 0.104, 0.058), 46, s, keep=-0.1, thick=0.01),
        ]
        # rebite no alto da cúpula
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=0.012, location=(s * 0.262, -0.075, 0.776))
        r = bpy.context.active_object
        r.data.materials.append(mat("AcoClaro"))
        parts.append(r)
        # correia de couro descendo para o peito
        strap = rei.rounded_box((0.03, 0.012, 0.075), (s * 0.168, -0.183, 0.722), "Couro", 0.004, 2)
        strap.rotation_euler = (math.radians(-16), 0, math.radians(s * 12))
        ab.select_only([strap])
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        parts.append(strap)
        o = join("ombreira", parts)
        out.append((s, o))
    return out


# ---------------------------------------------------------------------------------------------------------
# Cinto com fivela; escarcelas (placas que caem do cinto em cima das coxas)
def make_belt():
    rings = []
    for z, g in ((0.398, 0.006), (0.404, 0.012), (0.446, 0.012), (0.452, 0.006)):
        rx, ry = table3(GAMB, z)
        rings.append((z, rx + g, ry + g, 0.0))
    o = ame.ell_tube(rings, 48, "cinto")
    outward(o)
    add_mats(o, "Couro")
    smooth(o)
    solidify(o, 0.008, -1.0)
    rx, ry = table3(GAMB, 0.425)
    y = -(ry + 0.02)
    frame = rei.rounded_box((0.074, 0.012, 0.058), (0, y, 0.425), "Aco", 0.006, 2)
    inner = rei.rounded_box((0.044, 0.008, 0.032), (0, y - 0.005, 0.425), "CouroEscuro", 0.004, 1)
    pin = rei.rounded_box((0.008, 0.008, 0.04), (0.004, y - 0.009, 0.425), "AcoClaro", 0.002, 1)
    return [o, frame, inner, pin]


def make_tassets():
    parts = []
    for s in (1, -1):
        for th_c, half, z0, z1 in ((0.52, 0.30, 0.305, 0.40), (1.25, 0.22, 0.335, 0.40)):
            bm = bmesh.new()
            rows = 5
            cols = 8
            grid = []
            for r in range(rows + 1):
                t = r / rows
                z = z1 - (z1 - z0) * t
                row = []
                for c in range(cols + 1):
                    th = s * (th_c - half + 2 * half * c / cols)
                    rx, ry = table3(GAMB, max(z, 0.315))
                    grow = 0.022 + 0.05 * t           # abre para fora embaixo
                    row.append(bm.verts.new(((rx + grow) * math.sin(th), -(ry + grow) * math.cos(th), z)))
                grid.append(row)
            for r in range(rows):
                for c in range(cols):
                    bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
            o = new_obj("escarcela", bm)
            outward(o)
            add_mats(o, "Aco", "AcoClaro")
            smooth(o)
            solidify(o, 0.011, -1.0, mat_off=0, rim_off=1)
            parts.append(o)
    return parts


def tasset_weights(x, y, z):
    side = "Left" if x > 0 else "Right"
    k = rei.smoothstep(0.42, 0.30, z) * 0.6
    return {"Hips": 1 - k, f"{side}UpperLeg": k}


# ---------------------------------------------------------------------------------------------------------
# Joelheiras e botas
def make_kneepads():
    parts = []
    for s in (1, -1):
        x = s * RIG["leg_x"]
        d = dome((x, -0.052, RIG["knee_z"] + 0.005), (0.062, 0.05, 0.07), 0, 1, keep=-0.2, thick=0.01, name="joelheira")
        # a cúpula aponta para cima: deita para a frente (-Y)
        bm = bmesh.new()
        bm.from_mesh(d.data)
        piv = Vector((x, -0.052, RIG["knee_z"] + 0.005))
        rot = Matrix.Rotation(math.radians(90), 3, "X")
        for v in bm.verts:
            v.co = rot @ (v.co - piv) + piv
        bm.to_mesh(d.data)
        bm.free()
        parts.append(d)
        band = rei.tube([(x, 0.0, RIG["knee_z"] - 0.012, 0.074), (x, 0.0, RIG["knee_z"] + 0.016, 0.076)], seg=18)
        b = new_obj("tira_joelho", band)
        add_mats(b, "CouroEscuro")
        smooth(b)
        parts.append(b)
    return parts


def make_boots():
    parts = rei.make_boots()   # cano, pé, sola e "cadarços" (viram as tiras da dobra)
    for s in (1, -1):
        x = s * RIG["leg_x"]
        # dobra de couro no alto do cano
        cuff = rei.tube([(x, 0.005, 0.192, 0.08), (x, 0.005, 0.2, 0.086), (x, 0.005, 0.232, 0.086), (x, 0.005, 0.24, 0.078)], seg=18)
        c = new_obj("dobra", cuff)
        add_mats(c, "Cadarco")
        smooth(c)
        parts.append(c)
        # biqueira de aço: casca em cima do bico da bota, deitada para a frente
        piv = Vector((x, -0.125, 0.058))
        cap = dome(tuple(piv), (0.07, 0.05, 0.062), 0, 1, keep=0.0, thick=0.008, name="biqueira")
        bm = bmesh.new()
        bm.from_mesh(cap.data)
        rot = Matrix.Rotation(math.radians(58), 3, "X")
        for v in bm.verts:
            v.co = rot @ (v.co - piv) + piv
        bm.to_mesh(cap.data)
        bm.free()
        parts.append(cap)
    return parts


# ---------------------------------------------------------------------------------------------------------
# Cabeça: cabelo raspado (calota fina) e cicatriz
# linha do cabelo: elevação mínima (graus) por azimute (0 = frente, 90 = lado esquerdo, 180 = nuca)
HAIRLINE = [(0, 40), (35, 36), (62, 24), (80, 20), (95, 22), (112, 8), (140, -14), (180, -26)]


def hairline(az):
    a = abs(((az + 180) % 360) - 180)
    for (a0, e0), (a1, e1) in zip(HAIRLINE, HAIRLINE[1:]):
        if a0 <= a <= a1:
            k = (a - a0) / (a1 - a0)
            k = k * k * (3 - 2 * k)
            return e0 + (e1 - e0) * k
    return HAIRLINE[-1][1]


def make_hair():
    """Cabelo raspado: calota fina com a linha do cabelo lisa (grade em azimute × elevação)."""
    c = Vector((0.0, 0.004, 1.008))
    r = Vector((0.236, 0.216, 0.222))
    bm = bmesh.new()
    NA, NE = 64, 12
    rows = []
    for i in range(NE + 1):
        row = []
        for j in range(NA):
            az = 360.0 * j / NA
            e0 = hairline(az)
            el = math.radians(e0 + (88.0 - e0) * i / NE)
            a = math.radians(az)
            d = Vector((math.sin(a) * math.cos(el), -math.cos(a) * math.cos(el), math.sin(el)))
            row.append(bm.verts.new(c + Vector((d.x * r.x, d.y * r.y, d.z * r.z))))
        rows.append(row)
    for i in range(NE):
        for j in range(NA):
            bm.faces.new((rows[i][j], rows[i][(j + 1) % NA], rows[i + 1][(j + 1) % NA], rows[i + 1][j]))
    top = bm.verts.new(c + Vector((0, 0, r.z)))
    for j in range(NA):
        bm.faces.new((rows[NE][j], rows[NE][(j + 1) % NA], top))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    cap = new_obj("cabelo", bm)
    outward(cap)
    add_mats(cap, "Cabelo")
    smooth(cap)
    solidify(cap, 0.007, -1.0)
    return [cap]


def make_scar():
    """Cicatriz cortando a sobrancelha direita (lado −X), decalque por cima do rosto."""
    f = rei.Face()
    f.strip([(-0.13, 1.062), (-0.113, 1.03), (-0.097, 0.998)], 0.009, 0.006, "Cicatriz", 5)
    f.strip([(-0.092, 1.056), (-0.08, 1.03)], 0.0075, 0.005, "Cicatriz", 5)
    return f.build("cicatriz")


# ---------------------------------------------------------------------------------------------------------
# Rostos (concept: Stern, Focused, Approving Half-Smile, Angry, Battle Shout, Serious)
MZ = rei.MOUTH_Z


def make_faces():
    R = rei
    faces = []

    # Severo (slot "Serio"): sobrancelhas grossas e baixas, boca reta caída nos cantos
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.005, slope=0.3, low=-0.022)
        R.brow(f, s, z=1.046, angle=14, w0=0.0135, w1=0.0075)
    R.mouth_line(f, [(-0.019, MZ - 0.003), (-0.007, MZ), (0.007, MZ), (0.019, MZ - 0.003)], 0.006)
    faces.append(f.build("Rosto_Serio"))

    # Focado
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.002, slope=0.36, low=-0.016)
        R.brow(f, s, z=1.04, angle=19, w0=0.0135, w1=0.0075)
    R.mouth_line(f, [(-0.013, MZ), (0.0, MZ - 0.0015), (0.013, MZ)], 0.0055)
    faces.append(f.build("Rosto_Focado"))

    # Meio sorriso (slot "SorrisoFrio"): olhar tranquilo, canto da boca subindo de um lado
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.006, slope=0.2, low=-0.02)
        R.brow(f, s, z=1.052, angle=8, w0=0.013, w1=0.007)
    f.strip([(-0.02, MZ + 0.001), (-0.007, MZ - 0.002), (0.007, MZ - 0.0015), (0.018, MZ + 0.004), (0.025, MZ + 0.01)],
            0.0055, 0.004, "Tinta", 2)
    faces.append(f.build("Rosto_SorrisoFrio"))

    # Bravo: sobrancelhas em V, olhos apertados, dentes cerrados
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.001, slope=0.55, low=-0.018, lid_w=0.011)
        R.brow(f, s, z=1.048, angle=30, w0=0.015, w1=0.007)
    w, h = 0.05, 0.026
    pts = [(w * 0.5 * math.cos(2 * math.pi * i / 20), MZ - 0.004 + h * 0.5 * math.sin(2 * math.pi * i / 20) *
            (0.75 if math.sin(2 * math.pi * i / 20) > 0 else 1.0)) for i in range(20)]
    f.poly(pts, "Boca", 0)
    f.poly([(-0.019, MZ + 0.002), (0.019, MZ + 0.002), (0.019, MZ - 0.012), (-0.019, MZ - 0.012)], "Dentes", 1)
    f.strip([(-0.019, MZ - 0.005), (0.019, MZ - 0.005)], 0.0022, 0.0022, "Tinta", 2)
    faces.append(f.build("Rosto_Bravo"))

    # Grito de guerra (slot "Feliz" = comemoração): bocão aberto, sobrancelhas em V, olhos abertos
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.012, slope=0.35, low=-0.02, lid_w=0.011)
        R.brow(f, s, z=1.056, angle=24, w0=0.015, w1=0.007)
    pts = [(0.03 * math.cos(2 * math.pi * i / 24), MZ - 0.012 + 0.024 * math.sin(2 * math.pi * i / 24)) for i in range(24)]
    f.poly(pts, "Boca", 0)
    f.poly([(-0.02, MZ + 0.009), (0.02, MZ + 0.009), (0.017, MZ + 0.002), (-0.017, MZ + 0.002)], "Dentes", 1)
    f.poly([(-0.016, MZ - 0.03), (0.016, MZ - 0.03), (0.012, MZ - 0.025), (-0.012, MZ - 0.025)], "Dentes", 1)
    faces.append(f.build("Rosto_Feliz"))

    # Surpreso (atordoado)
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, a=0.03, b=0.035, pupil=(0.009, 0.012), top_follow=True, lid_w=0.006, wing=False)
        R.brow(f, s, z=1.094, angle=-8, arch=0.006, w0=0.011)
    f.poly(R.ellipse(0.0, MZ - 0.004, 0.011, 0.015, 16), "Boca", 0)
    faces.append(f.build("Rosto_Surpreso"))
    return faces


# ---------------------------------------------------------------------------------------------------------
# Manoplas: esfera de aço, nós dos dedos e punho de couro virado para o corpo (mãos sem osso)
def make_gauntlet(s, name):
    hr = 0.078
    parts = []
    bpy.ops.mesh.primitive_uv_sphere_add(segments=22, ring_count=12, radius=hr, location=(0, 0, 0))
    ball = bpy.context.active_object
    ball.data.materials.append(mat("Aco"))
    smooth(ball)
    parts.append(ball)
    # nós dos dedos: faixa clara na frente (no jogo a frente do Root é +Y do Blender)
    knuckles = rei.tube([(0, 0, -0.03, hr * 0.97), (0, 0, 0.0, hr * 1.04), (0, 0, 0.03, hr * 0.97)], seg=22)
    k = new_obj("nos", knuckles)
    add_mats(k, "AcoClaro")
    smooth(k)
    # a faixa gira em volta do eixo para o corpo (+X na mão esquerda do jogo = +X do Blender aqui)
    k.rotation_euler = (0, math.radians(90), 0)
    ab.select_only([k])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    parts.append(k)
    # punho de couro (cano curto) saindo para o lado do corpo e um pouco para cima
    d = Vector((s, 0, 1.0)).normalized()
    cuff = rei.tube([(0, 0, 0.05, 0.05), (0, 0, 0.07, 0.056), (0, 0, 0.088, 0.054)], seg=18)
    c = new_obj("punho", cuff)
    add_mats(c, "Couro")
    smooth(c)
    solidify(c, 0.008, 1.0)
    q = Vector((0, 0, 1)).rotation_difference(d)
    c.rotation_mode = "QUATERNION"
    c.rotation_quaternion = q
    ab.select_only([c])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    parts.append(c)
    h = join(name, parts)
    h.location = (s * 0.40, -0.02, 0.43)
    return h


# ---------------------------------------------------------------------------------------------------------
def build():
    rig = make_rig_base.build()
    rei.M.clear()
    for key, (name, hexc) in PAL.items():
        metal = name in ("Aco", "AcoClaro", "AcoEscuro")
        rei.M[key] = ab.material(None, name, rei.srgb(hexc), roughness=0.45 if metal else 0.8, metallic=0.4 if metal else 0.0)

    gamb = make_gambeson()
    quilt = make_quilting()
    plate = make_breastplate()
    details = make_plate_details()
    gorget = make_gorget()
    pauld = make_pauldrons()
    belt = make_belt()
    tassets = make_tassets()
    pelvis = ame.make_pelvis()
    legs = rei.make_legs()
    knees = make_kneepads()
    boots = make_boots()

    torso = [gamb, quilt, plate, gorget] + details + belt + [pelvis]
    for o in torso:
        ab.finalize(o)
    for o in [gamb, quilt]:
        rei.skin(o, rig, lambda x, y, z: rei.spine_weights(z) if z >= 0.42 else tasset_weights(x, y, z) if z < 0.4 else {"Hips": 1.0})
    for o in [plate] + details:
        rei.skin(o, rig, lambda x, y, z: rei.spine_weights(z))
    rei.skin(gorget, rig, lambda x, y, z: {"UpperChest": 0.5, "Neck": 0.5} if z > 0.8 else {"UpperChest": 1.0})
    for o in belt + [pelvis]:
        rei.skin(o, rig, lambda x, y, z: {"Hips": 1.0})
    for o in tassets:
        ab.finalize(o)
        rei.skin(o, rig, tasset_weights)
    shoulders = []
    for s, o in pauld:
        ab.finalize(o)
        bone = "LeftShoulder" if s > 0 else "RightShoulder"
        rei.skin(o, rig, lambda x, y, z, b=bone: {b: 0.7, "UpperChest": 0.3})
        shoulders.append(o)
    for o in legs + knees + boots:
        ab.finalize(o)
        rei.skin(o, rig, rei.leg_weights)
    body = join("Corpo", torso + tassets + shoulders + legs + knees + boots)

    rei.make_hair = make_hair          # a cabeça do Rei chama make_hair(): cabelo raspado do Iron
    head = join("Cabeca", rei.make_head() + [make_scar()])
    ab.finalize(head)
    rei.skin(head, rig, rei.head_weights)

    faces = make_faces()
    for f in faces:
        ab.finalize(f)
        rei.skin(f, rig, lambda x, y, z: {"Head": 1.0})

    hands = []
    for s, nm in ((1, "MaoEsquerda"), (-1, "MaoDireita")):
        h = make_gauntlet(s, nm)
        ab.finalize(h)
        hands.append(h)

    objs = [rig, body, head] + faces + hands
    tris, top = 0, 0.0
    for o in objs:
        if o.type == "MESH":
            o.data.calc_loop_triangles()
            tris += len(o.data.loop_triangles)
            top = max(top, max((o.matrix_world @ v.co).z for v in o.data.vertices))
    print(f"[AB] iron: {tris} triângulos, topo {top:.3f} m")
    return objs


if __name__ == "__main__":
    objs = build()
    ab.export_glb("iron", objs, animations=False)
    ab.save_blend("iron")
