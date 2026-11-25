"""
Personagem "Ametista" (2º personagem; nome provisório) — concept: art_source/concepts/ametista.png
-> assets/models/ametista.glb + art_source/blend/ametista.blend

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_ametista.py

Mesmo esqueleto e mesmas proporções chibi do Rei (1,4 m até o topo do topete). Reaproveita de make_rei.py a cabeça,
o sistema de rostos (decalques) e a pele. Objetos separados (pedido do usuário):
- Corpo      casaco verde aberto (forro escuro, gola alta, lapelas), camiseta branca, calça cinza com barra dobrada, tênis
- Cabeca     cabeça, pescoço, orelhas e cabelo roxo (topete para cima e para trás, mecha na testa) — no osso Head
- (sem coroa: este personagem não usa)
- Rosto_*    expressões (os MESMOS nomes de objeto do Rei; os rótulos da roda ficam em Scripts/Game/Characters.cs)
- MaoEsquerda / MaoDireita   esferas SEM esqueleto (a Godot move por código)
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

PAL = dict(
    Pele="#D9A26E", Cabelo="#5D2B8A", CabeloEscuro="#3A1760", CabeloBrilho="#A276E3",
    Casaco="#1F4231", Forro="#132A1F", Lapela="#2A5A42", Camiseta="#F2EFE9",
    Calca="#3D3D3D", Barra="#5E5E62", Tenis="#F4F4F2", Sola="#D8D8D8", TenisVerde="#2D6A4F",
    Olhos="#F0A030", Pupila="#4A2408", Brilho="#FFFFFF", Tinta="#2A1A22", Boca="#5E2018", Dentes="#FFFFFF",
)
RIG = make_rig_base.PROP
smooth, new_obj, add_mats, join, solidify, mat = rei.smooth, rei.new_obj, rei.add_mats, rei.join, rei.solidify, rei.mat


def lerp_table(tab, z):
    if z <= tab[0][0]:
        return tab[0][1]
    for (z0, a0), (z1, a1) in zip(tab, tab[1:]):
        if z0 <= z <= z1:
            k = (z - z0) / (z1 - z0)
            k = k * k * (3 - 2 * k)
            return a0 + (a1 - a0) * k
    return tab[-1][1]


# ---------------------------------------------------------------------------------------------------------
# Casaco aberto na frente (do pescoço até a barra) — mostra a camiseta
COAT_TOP = 0.795
FRONT_OPEN = [(0.26, 0.62), (0.40, 0.47), (0.52, 0.42), (0.64, 0.43), (0.72, 0.52), (0.795, 0.78)]  # meia-abertura (rad)


def front_open(z):
    return lerp_table(FRONT_OPEN, z)


def hem_z(th):
    return 0.262 + 0.022 * math.cos(th)  # rabo do casaco um pouco mais baixo atrás


def ring_pt(th, z, grow=0.0):
    rx, ry = rei.coat_r(z)
    return Vector(((rx + grow) * math.sin(th), -(ry + grow) * math.cos(th), z))


def make_coat():
    bm = bmesh.new()
    ts = [0, 0.07, 0.15, 0.24, 0.33, 0.42, 0.5, 0.57, 0.64, 0.7, 0.76, 0.81, 0.86, 0.9, 0.935, 0.965, 1.0]
    NC = 44
    grid = []
    for t in ts:
        row = []
        for c in range(NC + 1):
            u = c / NC
            th0 = math.pi * (0.1 + 1.8 * u)
            z = hem_z(th0) + (COAT_TOP - hem_z(th0)) * t
            fo = front_open(z)
            th = fo + u * (2 * math.pi - 2 * fo)
            z = hem_z(th) + (COAT_TOP - hem_z(th)) * t
            fo = front_open(z)
            th = fo + u * (2 * math.pi - 2 * fo)
            row.append(bm.verts.new(ring_pt(th, z)))
        grid.append(row)
    for r in range(len(ts) - 1):
        for c in range(NC):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("casaco", bm)
    # normais para fora (centro do casaco está no eixo Z)
    o.data.update()
    out = sum((p.normal.dot(Vector((p.center.x, p.center.y, 0))) for p in o.data.polygons))
    if out < 0:
        for p in o.data.polygons:
            p.flip()
    add_mats(o, "Casaco", "Forro")
    smooth(o)
    solidify(o, 0.014, -1.0, mat_off=1, rim_off=1)
    return o


def make_collar():
    """Gola alta levantada, bem aberta na frente, abrindo para fora em cima; por dentro, forro escuro."""
    bm = bmesh.new()
    gap = 0.78
    n, rows = 30, 4
    grid = []
    for r in range(rows + 1):
        t = r / rows
        row = []
        for c in range(n + 1):
            th = gap + (2 * math.pi - 2 * gap) * c / n
            back = 0.5 - 0.5 * math.cos(th)            # 0 na frente, 1 atrás
            ztop = 0.875 + 0.06 * back
            z = 0.765 + (ztop - 0.765) * t
            flare = t * t
            rx = 0.118 + 0.07 * flare
            ry = 0.100 + 0.06 * flare
            row.append(bm.verts.new((rx * math.sin(th), 0.008 - ry * math.cos(th), z)))
        grid.append(row)
    for r in range(rows):
        for c in range(n):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("gola", bm)
    add_mats(o, "Casaco", "Forro")
    smooth(o)
    solidify(o, 0.012, -1.0, mat_off=1, rim_off=1)
    return o


def make_lapels():
    """Lapelas dobradas para fora ao longo da abertura, do peito até a gola."""
    parts = []
    for s in (1, -1):
        bm = bmesh.new()
        zs = [0.56 + (0.79 - 0.56) * i / 8 for i in range(9)]
        inner, outer = [], []
        for z in zs:
            fo = front_open(z)
            rx, _ = rei.coat_r(z)
            w = 0.062 * rei.smoothstep(0.55, 0.72, z) + 0.004
            th_in = fo - 0.01
            th_out = fo + w / max(rx, 0.1)
            if s < 0:
                th_in, th_out = 2 * math.pi - th_in, 2 * math.pi - th_out
            inner.append(bm.verts.new(ring_pt(th_in, z, 0.004)))
            outer.append(bm.verts.new(ring_pt(th_out, z, 0.002)))
        for i in range(len(zs) - 1):
            bm.faces.new((inner[i], outer[i], outer[i + 1], inner[i + 1]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        o = new_obj("lapela", bm)
        o.data.update()
        if sum(p.normal.dot(Vector((p.center.x, p.center.y, 0))) for p in o.data.polygons) < 0:
            for p in o.data.polygons:
                p.flip()
        add_mats(o, "Lapela")
        smooth(o)
        solidify(o, 0.006, 1.0)
        parts.append(o)
    return parts


def ell_tube(rings, seg=32, name="tubo"):
    """rings: [(z, rx, ry, cy)] de baixo para cima -> tubo elíptico aberto."""
    bm = bmesh.new()
    rows = [[bm.verts.new((rx * math.sin(2 * math.pi * j / seg), cy - ry * math.cos(2 * math.pi * j / seg), z))
             for j in range(seg)] for (z, rx, ry, cy) in rings]
    for i in range(len(rows) - 1):
        for j in range(seg):
            bm.faces.new((rows[i][j], rows[i][(j + 1) % seg], rows[i + 1][(j + 1) % seg], rows[i + 1][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_obj(name, bm)


def make_shirt():
    rings = []
    for z in (0.40, 0.45, 0.50, 0.56, 0.62, 0.68, 0.72, 0.75, 0.775):
        rx, ry = rei.coat_r(z)
        rings.append((z, rx - 0.024, ry - 0.024, 0.0))
    rings += [(0.80, 0.115, 0.098, 0.004), (0.815, 0.075, 0.07, 0.008)]
    o = ell_tube(rings, 36, "camiseta")
    add_mats(o, "Camiseta")
    smooth(o)
    # gola careca (rolinho em volta do pescoço)
    bm = rei.tube([(0, 0.008, 0.808, 0.078), (0, 0.008, 0.822, 0.072)], seg=20)
    neck = new_obj("gola_camiseta", bm)
    add_mats(neck, "Camiseta")
    smooth(neck)
    return [o, neck]


def make_pelvis():
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1.0, location=(0, 0.0, 0.445))
    o = bpy.context.active_object
    o.scale = (0.165, 0.13, 0.07)
    ab.select_only([o])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.data.materials.append(mat("Calca"))
    smooth(o)
    return o


def make_cuffs():
    parts = []
    for s in (1, -1):
        x = s * RIG["leg_x"]
        bm = rei.tube([(x, -0.004, 0.158, 0.064), (x, -0.004, 0.166, 0.075), (x, -0.004, 0.205, 0.075),
                       (x, -0.004, 0.214, 0.066)], seg=18)
        o = new_obj("barra", bm)
        add_mats(o, "Barra")
        smooth(o)
        parts.append(o)
    return parts


def make_sneakers():
    parts = []
    for s in (1, -1):
        x = s * RIG["leg_x"]
        cano = new_obj("cano", rei.tube([(x, 0.006, 0.175, 0.068), (x, 0.006, 0.13, 0.073), (x, 0.0, 0.085, 0.078)], seg=18))
        add_mats(cano, "TenisVerde")
        smooth(cano)
        pe = rei.rounded_box((0.148, 0.255, 0.095), (x, -0.048, 0.07), "Tenis", 0.042, 3)
        for v in pe.data.vertices:   # bico mais baixo e arredondado
            if v.co.y < -0.10:
                v.co.z -= (abs(v.co.y) - 0.10) * 0.35 * max(0.0, (v.co.z - 0.03) / 0.09)
        smooth(pe)
        lado = rei.rounded_box((0.154, 0.15, 0.055), (x, 0.0, 0.075), "TenisVerde", 0.025, 2)   # faixa verde dos lados/calcanhar
        smooth(lado)
        lingua = rei.rounded_box((0.085, 0.10, 0.03), (x, -0.075, 0.112), "TenisVerde", 0.012, 2)
        lingua.rotation_euler = (math.radians(-20), 0, 0)
        smooth(lingua)
        sola = rei.rounded_box((0.16, 0.27, 0.036), (x, -0.048, 0.017), "Sola", 0.013, 2)
        laces = []
        for i, y in enumerate((-0.06, -0.088, -0.116)):
            bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, 0.13 - i * 0.011))
            l = bpy.context.active_object
            l.scale = (0.07, 0.01, 0.011)
            l.rotation_euler = (math.radians(-20), 0, 0)
            l.data.materials.append(mat("Tenis"))
            laces.append(l)
        parts += [cano, pe, lado, lingua, sola] + laces
    return parts


# ---------------------------------------------------------------------------------------------------------
# Cabelo: topete roxo (sobe da testa e varre para trás), lados curtos, nuca em mechas, uma mecha caindo na testa
HAIR_MATS = ["Cabelo", "CabeloEscuro", "CabeloBrilho"]


def bez(p0, p1, p2, t):
    return p0 * (1 - t) ** 2 + p1 * 2 * (1 - t) * t + p2 * t * t


def strand(bm, p0, p1, p2, r0, hint, flat=0.5, mi=0, n=8, rings=6, keep=0.15):
    """Mecha curva (Bézier quadrática p0→p2), achatada contra 'hint' (normal da cabeça), afinando até a ponta."""
    vrows = []
    for i in range(rings):
        t = i / rings
        c = bez(p0, p1, p2, t)
        tan = (bez(p0, p1, p2, min(t + 0.02, 1)) - bez(p0, p1, p2, max(t - 0.02, 0))).normalized()
        side = tan.cross(hint)
        if side.length < 1e-4:
            side = tan.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(tan).normalized()
        r = r0 * (keep + (1 - keep) * (1 - t) ** 0.8) if i > 0 else r0
        vrows.append([bm.verts.new(c + side * math.cos(2 * math.pi * j / n) * r + up * math.sin(2 * math.pi * j / n) * r * flat)
                      for j in range(n)])
    tip = bm.verts.new(p2)
    tan0 = (p1 - p0).normalized()
    cap = bm.verts.new(p0 - tan0 * r0 * 0.4)
    fs = []
    for i in range(rings - 1):
        for j in range(n):
            k = (j + 1) % n
            fs.append(bm.faces.new((vrows[i][j], vrows[i][k], vrows[i + 1][k], vrows[i + 1][j])))
    for j in range(n):
        k = (j + 1) % n
        fs.append(bm.faces.new((vrows[-1][j], vrows[-1][k], tip)))
        fs.append(bm.faces.new((vrows[0][k], vrows[0][j], cap)))
    for f in fs:
        f.material_index = mi


def hp(az, el, inset=0.95):
    return rei.hair_point(az, el, inset)


def make_hair():
    # calota (mais testa à mostra que o Rei: o cabelo sobe), lados e nuca
    bpy.ops.mesh.primitive_uv_sphere_add(segments=36, ring_count=18, radius=1.0, location=(0, 0, 0))
    cap = bpy.context.active_object
    bm = bmesh.new()
    bm.from_mesh(cap.data)
    kill = []
    for v in bm.verts:
        d = v.co
        face = d.z < 0.42 and d.y < -0.36
        low = d.z < -0.5
        ear = -0.55 < d.z < 0.05 and abs(d.x) > 0.78 and abs(d.y) < 0.42
        if face or low or ear:
            kill.append(v)
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    for v in bm.verts:
        v.co = rei.HAIR_C + Vector((v.co.x * rei.HAIR_R.x * 0.97, v.co.y * rei.HAIR_R.y * 0.97, v.co.z * rei.HAIR_R.z * 0.97))
    bm.to_mesh(cap.data)
    bm.free()
    cap.data.materials.append(mat("Cabelo"))
    smooth(cap)
    solidify(cap, 0.01, -1.0)

    # volume do topete (por baixo das mechas, tapa os buracos)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1.0, location=(0, -0.01, 1.215))
    vol = bpy.context.active_object
    vol.scale = (0.185, 0.19, 0.13)
    vol.rotation_euler = (math.radians(-12), 0, 0)
    ab.select_only([vol])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    vol.data.materials.append(mat("Cabelo"))
    smooth(vol)

    bm = bmesh.new()
    # topete: mechas grandes da linha do cabelo, subindo um pouco para a frente e varrendo para trás
    for az in (-64, -52, -40, -28, -16, -4, 8, 20, 32, 44, 56, 68):
        a = math.radians(az)
        h = 1 - (az / 78.0) ** 2 * 0.55
        p0, d = hp(az, 30, 0.95)
        p1 = p0 + Vector((math.sin(a) * 0.03, -0.075, 0.26 * h))
        p2 = Vector((p0.x * 1.15, 0.19 + 0.03 * (1 - h), 1.17 + 0.17 * h))
        strand(bm, p0, p1, p2, 0.085 * (0.8 + 0.2 * h), d, flat=0.6, mi=0, keep=0.3)
    # meio do topo: mechas para trás (preenchem entre o topete e a nuca)
    for az in (-40, -14, 14, 40):
        p0, d = hp(az, 62, 0.9)
        p1 = p0 + Vector((0, 0.06, 0.12))
        p2 = p0 + Vector((math.sin(math.radians(az)) * 0.05, 0.25, 0.04))
        strand(bm, p0, p1, p2, 0.065, d, flat=0.55, mi=0)
    # brilho: mechas finas claras por cima do topete
    for az in (-30, -6, 18, 42):
        a = math.radians(az)
        p0, d = hp(az, 40, 1.0)
        p0 += Vector((0, -0.02, 0.04))
        p1 = p0 + Vector((math.sin(a) * 0.02, -0.06, 0.2))
        p2 = Vector((p0.x * 1.1, 0.12, 1.33 + 0.02 * math.cos(a)))
        strand(bm, p0, p1, p2, 0.028, d, flat=0.45, mi=2, n=6, keep=0.1)
    # lados: mechas curtas para trás (escuras)
    for s in (1, -1):
        for az, el in ((80, 30), (95, 12), (110, 32), (125, 10), (140, 28)):
            p0, d = hp(s * az, el, 0.93)
            p1 = p0 + Vector((s * 0.008, 0.04, 0.0))
            p2 = p0 + Vector((s * 0.012, 0.075, -0.02))
            strand(bm, p0, p1, p2, 0.05, d, flat=0.35, mi=1, n=6, rings=4)
    # nuca: mechas para baixo/trás em camadas
    for az, el, L in ((180, 25, 0.13), (155, 20, 0.12), (-155, 20, 0.12), (130, 5, 0.1), (-130, 5, 0.1), (168, -5, 0.12),
                      (-168, -5, 0.12), (180, -22, 0.1), (145, -20, 0.09), (-145, -20, 0.09), (180, 45, 0.12),
                      (150, 45, 0.11), (-150, 45, 0.11)):
        p0, d = hp(az, el, 0.93)
        p1 = p0 + d * L * 0.12 + Vector((0, 0.01, -L * 0.35))
        p2 = p0 + d * L * 0.2 + Vector((0, 0.02, -L * 0.9))
        strand(bm, p0, p1, p2, 0.055, d, flat=0.35, mi=0 if el > 10 else 1, n=6, rings=4, keep=0.1)
    # mecha caindo na testa (lado direito do personagem = −X)
    p0, d = hp(-14, 44, 0.97)
    strand(bm, p0, Vector((-0.055, -0.25, 1.15)), Vector((-0.085, -0.222, 1.055)), 0.03, d, flat=0.45, mi=0, n=6, keep=0.1)
    p0, d = hp(-2, 46, 0.97)
    strand(bm, p0, Vector((-0.02, -0.25, 1.17)), Vector((-0.04, -0.226, 1.09)), 0.022, d, flat=0.45, mi=2, n=6, keep=0.1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    sp = new_obj("mechas", bm)
    add_mats(sp, *HAIR_MATS)
    smooth(sp)
    return [cap, vol, sp]


# ---------------------------------------------------------------------------------------------------------
# Rostos (mesmos nomes de objeto do Rei; os rótulos da roda deste personagem ficam no Characters.cs)
MZ = rei.MOUTH_Z


def grin_mouth(f, w, h, tilt=0.0, teeth=True, cx=0.0):
    """Boca aberta sorrindo (meia-lua): w = largura, h = altura; tilt sobe o canto esquerdo do personagem (+X)."""
    top, bot = [], []
    for i in range(13):
        k = i / 12
        x = cx - w / 2 + w * k
        lift = 0.006 * math.sin(math.pi * k) * 0 + tilt * (k - 0.5)
        corner = 0.006 * (abs(2 * k - 1) ** 3)
        top.append((x, MZ + 0.004 + lift + corner))
        bot.append((x, MZ + 0.004 + lift + corner - h * math.sin(math.pi * k)))
    f.poly(top + bot[::-1][1:-1], "Boca", 0)
    if teeth:
        tt = [(x, z) for x, z in top[2:-2]]
        tb = [(x, z - h * 0.32 * math.sin(math.pi * (i + 2) / 12)) for i, (x, z) in enumerate(top[2:-2])]
        f.poly(tt + tb[::-1], "Dentes", 1)


def make_faces():
    faces = []
    R = rei

    # Relaxado (slot "Serio" = cara padrão): pálpebras meio baixas, meio sorriso tranquilo
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.006, slope=0.08)
        R.brow(f, s, z=1.06, angle=2, arch=0.003)
    f.strip([(-0.022, MZ + 0.004), (-0.01, MZ - 0.001), (0.006, MZ - 0.001), (0.02, MZ + 0.003), (0.027, MZ + 0.008)],
            0.0055, 0.004, "Tinta", 2)
    faces.append(f.build("Rosto_Serio"))

    # Focado
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.003, slope=0.3, low=-0.018)
        R.brow(f, s, z=1.047, angle=16)
    R.mouth_line(f, [(-0.014, MZ), (0.0, MZ - 0.001), (0.014, MZ)], 0.005)
    faces.append(f.build("Rosto_Focado"))

    # Sorriso convencido (slot "SorrisoFrio"): uma sobrancelha erguida, olhos apertados, sorriso de lado com dentes
    f = R.Face()
    R.eye(f, 1, lid=0.006, slope=0.12)
    R.eye(f, -1, lid=0.0, slope=0.22, low=-0.016)
    R.brow(f, 1, z=1.07, angle=-6, arch=0.005)
    R.brow(f, -1, z=1.05, angle=12)
    grin_mouth(f, 0.062, 0.022, tilt=0.014, cx=0.004)
    faces.append(f.build("Rosto_SorrisoFrio"))

    # Bravo: sobrancelhas em V, dentes cerrados
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.002, slope=0.55, low=-0.018, lid_w=0.011)
        R.brow(f, s, z=1.05, angle=28, w0=0.012, w1=0.005)
    w, h = 0.05, 0.026
    pts = [(w * 0.5 * math.cos(a), MZ - 0.004 + h * 0.5 * math.sin(a) * (0.75 if math.sin(a) > 0 else 1.0))
           for a in (2 * math.pi * i / 20 for i in range(20))]
    f.poly(pts, "Boca", 0)
    f.poly([(-0.019, MZ + 0.002), (0.019, MZ + 0.002), (0.019, MZ - 0.012), (-0.019, MZ - 0.012)], "Dentes", 1)
    f.strip([(-0.019, MZ - 0.005), (0.019, MZ - 0.005)], 0.0022, 0.0022, "Tinta", 2)
    faces.append(f.build("Rosto_Bravo"))

    # Rindo (slot "Feliz"): olhos fechados em arco e bocão aberto com dentes em cima
    f = R.Face()
    for s in (1, -1):
        cx = s * R.EYE_X
        arc = [(cx + 0.032 * (2 * i / 10 - 1), R.EYE_Z - 0.01 + 0.022 * (1 - (2 * i / 10 - 1) ** 2)) for i in range(11)]
        f.strip(arc, 0.01, 0.01, "Tinta", 3)
        R.brow(f, s, z=1.068, angle=-8, arch=0.005)
    grin_mouth(f, 0.066, 0.04)
    faces.append(f.build("Rosto_Feliz"))

    # Surpreso (atordoado)
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, a=0.03, b=0.035, pupil=(0.009, 0.012), top_follow=True, lid_w=0.006, wing=False)
        R.brow(f, s, z=1.098, angle=-8, arch=0.006, w0=0.008)
    f.poly(R.ellipse(0.0, MZ - 0.004, 0.011, 0.015, 16), "Boca", 0)
    faces.append(f.build("Rosto_Surpreso"))
    return faces


# ---------------------------------------------------------------------------------------------------------
def build():
    rig = make_rig_base.build()
    rei.M.clear()
    for name, hexc in PAL.items():
        c = rei.srgb(hexc)
        rei.M[name] = ab.material(None, name, c, roughness=0.8)

    coat = make_coat()
    collar = make_collar()
    lapels = make_lapels()
    shirt = make_shirt()
    pelvis = make_pelvis()
    legs = rei.make_legs()
    cuffs = make_cuffs()
    shoes = make_sneakers()

    for o in [coat, collar] + lapels + shirt + [pelvis]:
        ab.finalize(o)
    rei.skin(coat, rig, rei.coat_weights)
    for o in [collar] + lapels:
        rei.skin(o, rig, lambda x, y, z: {"UpperChest": 0.6, "Neck": 0.4} if z > 0.8 else rei.spine_weights(z))
    for o in shirt:
        rei.skin(o, rig, lambda x, y, z: rei.spine_weights(z) if z < 0.8 else {"UpperChest": 0.5, "Neck": 0.5})
    rei.skin(pelvis, rig, lambda x, y, z: {"Hips": 1.0})
    for o in legs + cuffs + shoes:
        ab.finalize(o)
        rei.skin(o, rig, rei.leg_weights)
    body = join("Corpo", [coat, collar] + lapels + shirt + [pelvis] + legs + cuffs + shoes)

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
    tris = 0
    top = 0.0
    for o in objs:
        if o.type == "MESH":
            o.data.calc_loop_triangles()
            tris += len(o.data.loop_triangles)
            top = max(top, max((o.matrix_world @ v.co).z for v in o.data.vertices))
    print(f"[AB] ametista: {tris} triângulos, topo {top:.3f} m")
    return objs


if __name__ == "__main__":
    objs = build()
    ab.export_glb("ametista", objs, animations=False)
    ab.save_blend("ametista")
