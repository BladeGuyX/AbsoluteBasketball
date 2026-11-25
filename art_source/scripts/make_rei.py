"""
Personagem "Rei" (concept: art_source/concepts/rei.png) -> assets/models/rei.glb + art_source/blend/rei.blend

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_rei.py

Chibi de ~1,4 m (topo da coroa), como na concept. A Godot escala o visual por Tuning.CharacterScale.
Objetos separados (pedido do usuário):
- Corpo      casaco (com forro vermelho), calça, botas, botões — pele no rig_base
- Cabeca     cabeça, pescoço, orelhas e cabelo — 100% no osso Head (pescoço no Neck)
- Coroa      coroa com bordas e gema brilhando — 100% no osso Head
- Rosto_*    expressões (decalques finos sobre a cara), uma por objeto; a Godot mostra só uma
- MaoEsquerda / MaoDireita   esferas SEM esqueleto (flutuam; a Godot move por código)
Sem animações aqui: as animações são feitas na Godot (Scripts/Game/CharacterVisual.cs).
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


def srgb(h):
    h = h.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return tuple(out)


# paleta da concept (preto levemente levantado para o toon não virar uma mancha só)
PAL = dict(
    Pele="#F5E6D3", Cabelo="#1E1E24", Casaco="#202026", Forro="#8B0000", Metal="#7A7A80",
    Calca="#2A2A30", Botas="#17171B", Sola="#6A1515", Cadarco="#5A5A60",
    Coroa="#3A2E31", CoroaBrilho="#FF3A22", Gema="#FF1010",
    Olhos="#E3121C", Pupila="#3A0406", Brilho="#FFFFFF", Tinta="#141416", Boca="#5C1014", Dentes="#FFFFFF",
)
EMIT = {"CoroaBrilho", "Gema"}
M = {}

# proporções do esqueleto (chibi) — ver make_rig_base.PROP
RIG = make_rig_base.PROP
SEG = 48  # divisões em volta do casaco


def mat(name):
    return M[name]


def new_obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def add_mats(o, *names):
    for n in names:
        o.data.materials.append(mat(n))


def apply_mods(o):
    ab.select_only([o])
    for m in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def solidify(o, thick, offset=-1.0, mat_off=0, rim_off=0):
    s = o.modifiers.new("solid", "SOLIDIFY")
    s.thickness = thick
    s.offset = offset
    s.use_rim = True
    s.material_offset = mat_off
    s.material_offset_rim = rim_off
    apply_mods(o)


def join(name, objs):
    objs = [o for o in objs if o is not None]
    ab.select_only(objs)
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    return o


def smooth(o, on=True):
    for p in o.data.polygons:
        p.use_smooth = on


# ---------------------------------------------------------------------------------------------------------
# Casaco: perfil (z, meia-largura X, meia-profundidade Y) interpolado
COAT = [
    (0.20, 0.320, 0.258), (0.31, 0.292, 0.234), (0.38, 0.262, 0.208), (0.45, 0.224, 0.176), (0.50, 0.204, 0.162),
    (0.57, 0.204, 0.162), (0.64, 0.212, 0.164), (0.70, 0.226, 0.158), (0.745, 0.226, 0.146), (0.775, 0.188, 0.122),
    (0.795, 0.112, 0.093),
]


def coat_r(z):
    if z <= COAT[0][0]:
        return COAT[0][1], COAT[0][2]
    for (z0, x0, y0), (z1, x1, y1) in zip(COAT, COAT[1:]):
        if z0 <= z <= z1:
            k = (z - z0) / (z1 - z0)
            k = k * k * (3 - 2 * k)
            return x0 + (x1 - x0) * k, y0 + (y1 - y0) * k
    return COAT[-1][1], COAT[-1][2]


def ring_pt(th, z, cy=0.0):
    rx, ry = coat_r(z)
    return Vector((rx * math.sin(th), cy - ry * math.cos(th), z))


WAIST = 0.47


def hem_z(th):
    return 0.285 + 0.03 * math.cos(th)  # frente mais alta, rabo do casaco mais baixo atrás


def front_open(z):
    return max(0.0, WAIST - z) * 1.55   # abertura em V na frente (rad, metade)


def back_open(z):
    return max(0.0, 0.43 - z) * 0.55    # fenda atrás


def make_coat():
    bm = bmesh.new()
    # parte de cima (fechada), do quadril ao pescoço
    zs = [WAIST, 0.50, 0.535, 0.57, 0.605, 0.64, 0.67, 0.70, 0.725, 0.745, 0.762, 0.778, 0.795]
    rows = [[bm.verts.new(ring_pt(2 * math.pi * j / SEG, z)) for j in range(SEG)] for z in zs]
    for i in range(len(rows) - 1):
        for j in range(SEG):
            bm.faces.new((rows[i][j], rows[i][(j + 1) % SEG], rows[i + 1][(j + 1) % SEG], rows[i + 1][j]))
    # saia: dois painéis (esquerdo e direito) com V na frente e fenda atrás
    NR, NC = 8, SEG // 2
    for side in (0, 1):
        grid = []
        for r in range(NR + 1):
            t = r / NR
            row = []
            for c in range(NC + 1):
                u = c / NC
                # ângulo provisório só para achar a altura da barra
                th0 = (u * math.pi) if side == 0 else (math.pi + u * math.pi)
                z = WAIST + (hem_z(th0) - WAIST) * t
                hf, hb = front_open(z), back_open(z)
                if side == 0:
                    th = hf + u * (math.pi - hb - hf)
                else:
                    th = math.pi + hb + u * (math.pi - hb - hf)
                z = WAIST + (hem_z(th) - WAIST) * t
                row.append(bm.verts.new(ring_pt(th, z)))
            grid.append(row)
        for r in range(NR):
            for c in range(NC):
                # de cima para baixo: inverte a ordem para a normal apontar para fora
                bm.faces.new((grid[r + 1][c], grid[r + 1][c + 1], grid[r][c + 1], grid[r][c]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("casaco", bm)
    add_mats(o, "Casaco", "Forro")
    smooth(o)
    solidify(o, 0.014, -1.0, mat_off=1, rim_off=1)
    return o


def make_collar():
    """Gola alta, aberta na frente; por dentro vermelha."""
    bm = bmesh.new()
    gap = 0.55
    n, rows = 28, 3
    grid = []
    for r in range(rows + 1):
        t = r / rows
        row = []
        for c in range(n + 1):
            th = gap + (2 * math.pi - 2 * gap) * c / n
            ztop = 0.872 + 0.03 * (-math.cos(th))  # mais alta atrás
            z = 0.772 + (ztop - 0.772) * t
            rx = 0.112 + 0.03 * t
            ry = 0.096 + 0.026 * t
            row.append(bm.verts.new((rx * math.sin(th), 0.012 - ry * math.cos(th), z)))
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


def surface_front(x, z):
    """Ponto na frente do casaco (para botões)."""
    rx, ry = coat_r(z)
    k = max(0.0, 1 - (x / rx) ** 2)
    return Vector((x, -ry * math.sqrt(k), z))


def make_buttons():
    parts = []
    for z in (0.665, 0.598, 0.531):
        for x in (-0.066, 0.066):
            p = surface_front(x, z) + Vector((0, -0.006, 0))
            bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=0.019, location=p)
            b = bpy.context.active_object
            b.scale = (1, 0.5, 1)
            parts.append(b)
    # meia-cinta nas costas com dois botões
    rx, ry = coat_r(0.5)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, ry + 0.006, 0.505))
    belt = bpy.context.active_object
    belt.scale = (0.17, 0.012, 0.036)
    for x in (-0.06, 0.06):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=0.015, location=(x, ry + 0.014, 0.505))
        b = bpy.context.active_object
        b.scale = (1, 0.5, 1)
        parts.append(b)
    for p in parts:
        p.data.materials.append(mat("Metal"))
    belt.data.materials.append(mat("Casaco"))
    o = join("botoes", parts + [belt])
    ab.select_only([o])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    smooth(o)
    return o


def tube(rings, seg=14):
    """rings: [(x, y, z, r)] -> tubo aberto."""
    bm = bmesh.new()
    rows = []
    for (x, y, z, r) in rings:
        rows.append([bm.verts.new((x + r * math.sin(2 * math.pi * j / seg), y - r * math.cos(2 * math.pi * j / seg), z))
                     for j in range(seg)])
    for i in range(len(rows) - 1):
        for j in range(seg):
            a, b = rows[i][j], rows[i][(j + 1) % seg]
            c, d = rows[i + 1][(j + 1) % seg], rows[i + 1][j]
            if rings[i][2] > rings[i + 1][2]:
                bm.faces.new((d, c, b, a))
            else:
                bm.faces.new((a, b, c, d))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def make_legs():
    parts = []
    for s in (1, -1):
        x = s * RIG["leg_x"]
        bm = tube([(x, 0, 0.50, 0.086), (x, 0, 0.42, 0.082), (x, -0.004, 0.33, 0.072), (x, -0.008, 0.27, 0.067),
                   (x, -0.004, 0.21, 0.062), (x, 0, 0.15, 0.058)])
        o = new_obj("perna", bm)
        add_mats(o, "Calca")
        smooth(o)
        parts.append(o)
    return parts


def rounded_box(size, loc, m, bevel, segs=3):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.scale = size
    ab.select_only([o])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    b = o.modifiers.new("bevel", "BEVEL")
    b.width = bevel
    b.segments = segs
    apply_mods(o)
    o.data.materials.append(mat(m))
    return o


def make_boots():
    parts = []
    for s in (1, -1):
        x = s * RIG["leg_x"]
        bm = tube([(x, 0.005, 0.205, 0.072), (x, 0.005, 0.15, 0.074), (x, 0.0, 0.09, 0.08)], seg=16)
        cano = new_obj("cano", bm)
        add_mats(cano, "Botas")
        smooth(cano)
        pe = rounded_box((0.145, 0.25, 0.10), (x, -0.045, 0.072), "Botas", 0.04, 3)
        # bico um pouco mais baixo e arredondado
        for v in pe.data.vertices:
            if v.co.y < -0.10:
                v.co.z -= (abs(v.co.y) - 0.10) * 0.35 * max(0.0, (v.co.z - 0.03) / 0.09)
        smooth(pe)
        sola = rounded_box((0.155, 0.265, 0.032), (x, -0.045, 0.016), "Sola", 0.012, 2)
        laces = []
        for i, y in enumerate((-0.075, -0.105, -0.135)):
            bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, 0.118 - i * 0.012))
            l = bpy.context.active_object
            l.scale = (0.075, 0.011, 0.012)
            l.rotation_euler = (math.radians(-18), 0, 0)
            l.data.materials.append(mat("Cadarco"))
            laces.append(l)
        parts += [cano, pe, sola] + laces
    return parts


# ---------------------------------------------------------------------------------------------------------
# Cabeça (elipsoide afinando no queixo) — o rosto é projetado nesta mesma superfície
HC = Vector((0.0, 0.0, 1.0))      # centro
HR = Vector((0.228, 0.208, 0.212))   # raios


def taper(z):
    k = max(0.0, (HC.z - z) / HR.z)
    return 1.0 - 0.13 * k * k


def head_surface_y(x, z, off=0.0):
    """y da superfície da frente da cabeça no ponto (x, z), afastado 'off' para fora."""
    rx = HR.x * taper(z)
    u = (x / rx) ** 2 + ((z - HC.z) / HR.z) ** 2
    return HC.y - HR.y * math.sqrt(max(0.0, 1 - u)) - off


def make_head():
    bpy.ops.mesh.primitive_uv_sphere_add(segments=36, ring_count=18, radius=1.0, location=(0, 0, 0))
    h = bpy.context.active_object
    for v in h.data.vertices:
        z = HC.z + v.co.z * HR.z
        v.co = Vector((v.co.x * HR.x * taper(z), HC.y + v.co.y * HR.y, z))
    h.data.materials.append(mat("Pele"))
    smooth(h)
    ears = []
    for s in (1, -1):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=0.045, location=(s * 0.214, 0.012, 0.975))
        e = bpy.context.active_object
        e.scale = (0.45, 0.72, 1.0)
        e.data.materials.append(mat("Pele"))
        smooth(e)
        ears.append(e)
    bm = tube([(0, 0.01, 0.75, 0.058), (0, 0.01, 0.88, 0.06)], seg=14)
    neck = new_obj("pescoco", bm)
    add_mats(neck, "Pele")
    smooth(neck)
    return [h, neck] + ears + make_hair()


HAIR_C = Vector((0.0, 0.014, 1.02))
HAIR_R = Vector((0.242, 0.226, 0.228))


def hair_point(az_deg, el_deg, inset=0.9):
    """Ponto na calota do cabelo. az: 0 = frente (-Y), 90 = esquerda (+X); el: 0 = equador, 90 = topo."""
    az, el = math.radians(az_deg), math.radians(el_deg)
    d = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))
    p = HAIR_C + Vector((d.x * HAIR_R.x, d.y * HAIR_R.y, d.z * HAIR_R.z)) * inset
    return p, d


def spike(bm, base, axis, length, r, flat=0.55, curl=None):
    """Mecha: cone achatado, da base 'base' na direção 'axis' (curva para 'curl' na ponta)."""
    axis = axis.normalized()
    side = axis.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize()
    up = side.cross(axis).normalized()
    n = 6
    ring = []
    for i in range(n):
        a = 2 * math.pi * i / n
        ring.append(bm.verts.new(base + side * math.cos(a) * r + up * math.sin(a) * r * flat))
    mid_c = base + axis * length * 0.55
    if curl is not None:
        mid_c += curl * length * 0.12
    mid = [bm.verts.new(mid_c + side * math.cos(2 * math.pi * i / n) * r * 0.55 + up * math.sin(2 * math.pi * i / n) * r * 0.55 * flat)
           for i in range(n)]
    tip_p = base + axis * length + (curl * length * 0.35 if curl is not None else Vector())
    tip = bm.verts.new(tip_p)
    cap = bm.verts.new(base - axis * r * 0.3)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ring[i], ring[j], mid[j], mid[i]))
        bm.faces.new((mid[i], mid[j], tip))
        bm.faces.new((ring[j], ring[i], cap))


def make_hair():
    # calota sem a região do rosto e sem a parte de baixo dos lados (orelhas aparecem)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=36, ring_count=18, radius=1.0, location=(0, 0, 0))
    cap = bpy.context.active_object
    bm = bmesh.new()
    bm.from_mesh(cap.data)
    kill = []
    for v in bm.verts:
        d = v.co.copy()
        face = d.z < 0.36 and d.y < -0.42
        low = d.z < -0.55
        ear = -0.5 < d.z < 0.02 and abs(d.x) > 0.8 and abs(d.y) < 0.4
        if face or low or ear:
            kill.append(v)
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    for v in bm.verts:
        v.co = HAIR_C + Vector((v.co.x * HAIR_R.x, v.co.y * HAIR_R.y, v.co.z * HAIR_R.z))
    bm.to_mesh(cap.data)
    bm.free()
    cap.data.materials.append(mat("Cabelo"))
    smooth(cap)
    solidify(cap, 0.01, -1.0)

    bm = bmesh.new()
    down = Vector((0, 0, -1))

    def along(d, k_out, dirv):
        t = dirv - d * dirv.dot(d)
        if t.length < 1e-4:
            t = Vector((0, 1, 0))
        return (d * k_out + t.normalized()).normalized()

    # franja: mechas descendo sobre a testa (pontas param acima das sobrancelhas)
    for az, el, L, r in ((-48, 30, 0.105, 0.04), (-30, 36, 0.115, 0.045), (-12, 40, 0.12, 0.048), (6, 40, 0.125, 0.048),
                         (22, 37, 0.115, 0.046), (40, 32, 0.105, 0.042), (56, 24, 0.09, 0.038), (-62, 22, 0.09, 0.036)):
        p, d = hair_point(az, el, 0.93)
        side = Vector((math.sin(math.radians(az)), 0, 0)) * 0.35
        spike(bm, p, along(d, 0.25, down + side), L, r, 0.5, curl=Vector((0, -1, 0)))
    # topo: mechas espetadas para trás/para cima
    for az, el, L, r in ((0, 70, 0.11, 0.05), (35, 66, 0.10, 0.048), (-35, 66, 0.10, 0.048), (70, 58, 0.09, 0.045),
                         (-70, 58, 0.09, 0.045), (120, 62, 0.10, 0.05), (-120, 62, 0.10, 0.05), (180, 66, 0.11, 0.05),
                         (155, 45, 0.11, 0.05), (-155, 45, 0.11, 0.05)):
        p, d = hair_point(az, el, 0.9)
        back = Vector((0, 1, 0.35))
        spike(bm, p, along(d, 0.9, back), L, r, 0.6, curl=Vector((0, 0.3, 1)))
    # lados e nuca: mechas para fora e para baixo
    for az, el, L, r in ((92, 22, 0.095, 0.042), (-92, 22, 0.095, 0.042), (110, 0, 0.09, 0.04), (-110, 0, 0.09, 0.04),
                         (140, 15, 0.11, 0.046), (-140, 15, 0.11, 0.046), (165, -5, 0.11, 0.046), (-165, -5, 0.11, 0.046),
                         (180, -25, 0.10, 0.045), (130, -25, 0.09, 0.04), (-130, -25, 0.09, 0.04), (75, 8, 0.08, 0.036),
                         (-75, 8, 0.08, 0.036)):
        p, d = hair_point(az, el, 0.92)
        spike(bm, p, along(d, 0.55, down), L, r, 0.55, curl=d)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    sp = new_obj("mechas", bm)
    add_mats(sp, "Cabelo")
    return [cap, sp]


# ---------------------------------------------------------------------------------------------------------
CROWN_Z = 1.15
CROWN_PEAKS = [0.235, 0.15, 0.185, 0.145, 0.175, 0.145, 0.185, 0.15]  # 8 pontas, começando pela frente
CROWN_VALLEY = 0.085


def crown_height(th):
    n = len(CROWN_PEAKS)
    step = 2 * math.pi / n
    k = th / step
    i = int(math.floor(k)) % n
    f = k - math.floor(k)
    # pico em f=0 (ponta i) e f=1 (ponta i+1); vale no meio
    if f < 0.5:
        return CROWN_VALLEY + (CROWN_PEAKS[i] - CROWN_VALLEY) * (1 - 2 * f)
    return CROWN_VALLEY + (CROWN_PEAKS[(i + 1) % n] - CROWN_VALLEY) * (2 * f - 1)


def crown_r(h):
    return 0.198 + 0.12 * h, 0.19 + 0.12 * h  # abre para cima


def make_crown():
    bm = bmesh.new()
    N = 96
    rows = []
    for r_i, frac in enumerate((0.0, 0.33, 0.7, 1.0)):
        row = []
        for j in range(N):
            th = 2 * math.pi * j / N
            h = crown_height(th)
            z = CROWN_Z + (min(h, CROWN_VALLEY * 0.85) if r_i < 3 else h) * (frac if r_i < 3 else 1.0)
            if r_i == 2:
                z = CROWN_Z + CROWN_VALLEY * 0.85 + (h - CROWN_VALLEY * 0.85) * 0.35
            rx, ry = crown_r(z - CROWN_Z)
            row.append(bm.verts.new((rx * math.sin(th), 0.01 - ry * math.cos(th), z)))
        rows.append(row)
    for i in range(len(rows) - 1):
        for j in range(N):
            bm.faces.new((rows[i][j], rows[i][(j + 1) % N], rows[i + 1][(j + 1) % N], rows[i + 1][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    c = new_obj("coroa", bm)
    add_mats(c, "Coroa", "CoroaBrilho")
    solidify(c, 0.022, -1.0, mat_off=0, rim_off=1)   # borda (pontas) brilhando
    # faixa brilhante perto da base
    bm = bmesh.new()
    zs = (CROWN_Z + 0.03, CROWN_Z + 0.044)
    rows = []
    for z in zs:
        rx, ry = crown_r(z - CROWN_Z)
        rows.append([bm.verts.new(((rx + 0.004) * math.sin(2 * math.pi * j / 64), 0.01 - (ry + 0.004) * math.cos(2 * math.pi * j / 64), z))
                     for j in range(64)])
    for j in range(64):
        bm.faces.new((rows[0][j], rows[0][(j + 1) % 64], rows[1][(j + 1) % 64], rows[1][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    band = new_obj("faixa", bm)
    add_mats(band, "CoroaBrilho")
    solidify(band, 0.004, 1.0)
    # gema (losango) na ponta da frente
    gz = CROWN_Z + 0.105
    rx, ry = crown_r(gz - CROWN_Z)
    bm = bmesh.new()
    cy = 0.01 - ry - 0.012
    top = bm.verts.new((0, cy, gz + 0.036))
    bot = bm.verts.new((0, cy, gz - 0.036))
    l = bm.verts.new((0.022, cy, gz))
    r = bm.verts.new((-0.022, cy, gz))
    f = bm.verts.new((0, cy - 0.016, gz))
    b = bm.verts.new((0, cy + 0.01, gz))
    for tri in ((top, l, f), (top, f, r), (bot, f, l), (bot, r, f), (top, b, l), (top, r, b), (bot, l, b), (bot, b, r)):
        bm.faces.new(tri)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    gem = new_obj("gema", bm)
    add_mats(gem, "Gema")
    o = join("Coroa", [c, band, gem])
    return o


# ---------------------------------------------------------------------------------------------------------
# Rosto: decalques projetados na cara. Cada expressão é um objeto "Rosto_<Nome>".
FACE_OFF = 0.0035
LAYER = 0.0014


class Face:
    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def _mi(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def _v(self, x, z, layer):
        return self.bm.verts.new((x, head_surface_y(x, z, FACE_OFF + LAYER * layer), z))

    @staticmethod
    def _mouth(pts):
        """Boca maior que no desenho original (lida melhor de longe): escala em volta do centro da boca."""
        if not pts or max(p[1] for p in pts) > 0.9:
            return pts, 1.0
        return [(x * MOUTH_K, MOUTH_Z + (z - MOUTH_Z) * MOUTH_K) for x, z in pts], MOUTH_K * 0.85

    def poly(self, pts, m, layer):
        pts, _ = self._mouth(pts)
        if len(pts) < 3:
            return
        cx = sum(p[0] for p in pts) / len(pts)
        cz = sum(p[1] for p in pts) / len(pts)
        c = self._v(cx, cz, layer)
        vs = [self._v(x, z, layer) for x, z in pts]
        mi = self._mi(m)
        for i in range(len(vs)):
            f = self.bm.faces.new((c, vs[(i + 1) % len(vs)], vs[i]))
            f.material_index = mi

    def strip(self, pts, w0, w1, m, layer):
        """Linha grossa (polilinha) com espessura de w0 (início) a w1 (fim)."""
        pts, kw = self._mouth(pts)
        w0, w1 = w0 * kw, w1 * kw
        n = len(pts)
        left, right = [], []
        for i, (x, z) in enumerate(pts):
            a = pts[max(i - 1, 0)]
            b = pts[min(i + 1, n - 1)]
            tx, tz = b[0] - a[0], b[1] - a[1]
            L = math.hypot(tx, tz) or 1.0
            nx, nz = -tz / L, tx / L
            w = (w0 + (w1 - w0) * i / max(n - 1, 1)) * 0.5
            left.append(self._v(x + nx * w, z + nz * w, layer))
            right.append(self._v(x - nx * w, z - nz * w, layer))
        mi = self._mi(m)
        for i in range(n - 1):
            f = self.bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
            f.material_index = mi

    def build(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        # normais para a frente (-Y)
        for f in self.bm.faces:
            f.normal_update()
            if f.normal.y > 0:
                f.normal_flip()
        o = new_obj(name, self.bm)
        for m in self.mats:
            o.data.materials.append(mat(m))
        return o


def ellipse(cx, cz, a, b, n=24):
    return [(cx + a * math.cos(2 * math.pi * i / n), cz + b * math.sin(2 * math.pi * i / n)) for i in range(n)]


EYE_X, EYE_Z = 0.086, 0.962


def eye(face, s, lid=0.010, slope=0.25, a=0.031, b=0.029, low=None, pupil=(0.013, 0.018), lid_w=0.009, wing=True,
        top_follow=False):
    k = 1.18  # olhos maiores (cabeça maior que na 1ª versão)
    lid, a, b, lid_w = lid * k, a * k, b * k, lid_w * 1.1
    low = None if low is None else low * k
    pupil = None if not pupil else (pupil[0] * k, pupil[1] * k)
    """Olho do lado s (+1 = esquerdo do personagem, +X). lid = altura da pálpebra no centro (acima de EYE_Z);
    slope > 0 = pálpebra desce para o lado de dentro (cara séria/brava)."""
    cx, cz = s * EYE_X, EYE_Z

    def lid_z(x):
        return cz + lid + slope * (x - cx) * s

    def top(x):
        k = 1 - ((x - cx) / a) ** 2
        return cz + b * math.sqrt(max(0.0, k))

    def clip(pts):
        out = []
        for x, z in pts:
            if not top_follow:
                z = min(z, lid_z(x))
            if low is not None:
                z = max(z, cz + low)
            out.append((x, z))
        return out

    face.poly(clip(ellipse(cx, cz, a, b)), "Olhos", 0)
    if pupil:
        pa, pb = pupil
        face.poly(clip(ellipse(cx - s * 0.002, cz - 0.004, pa, pb, 16)), "Pupila", 1)
        hx, hz = cx + s * 0.011, cz + 0.006
        if top_follow or hz < lid_z(hx) - 0.008:
            face.poly(ellipse(hx, hz, 0.0062, 0.0062, 10), "Brilho", 2)
    # pálpebra de cima (traço grosso) + "asinha" para fora
    xs = [cx - a * 1.02 + 2.04 * a * i / 12 for i in range(13)]
    line = [(x, (top(x) if top_follow else min(lid_z(x), top(x))) + 0.0015) for x in xs]
    if s < 0:
        line = line[::-1]
    if wing:
        ox = cx + s * a * 1.02
        line_out = (ox + s * 0.012, cz + 0.004 + (0 if top_follow else min(lid, 0.012) * 0.3))
        line = line + [line_out] if s > 0 else [line_out] + line
    face.strip(line, lid_w, lid_w * 0.55, "Tinta", 3)


def brow(face, s, z=1.055, angle=12.0, length=0.052, w0=0.0095, w1=0.0045, x=EYE_X, arch=0.0):
    """Sobrancelha: angle > 0 = lado de dentro mais baixo (bravo/sério). z é relativo ao olho antigo (0,985)."""
    z = z - 0.985 + EYE_Z + 0.006
    length *= 1.15
    xi, xo = s * (x - length * 0.5), s * (x + length * 0.5)
    dz = math.tan(math.radians(angle)) * length * 0.5
    pts = []
    for i in range(7):
        k = i / 6
        px = xi + (xo - xi) * k
        pz = z - dz + 2 * dz * k + arch * math.sin(math.pi * k)
        pts.append((px, pz))
    face.strip(pts, w0, w1, "Tinta", 3)


MOUTH_Z = 0.862
MOUTH_K = 1.6


def mouth_line(face, pts, w=0.0055):
    face.strip(pts, w, w, "Tinta", 2)


def make_faces():
    faces = []

    # Sério (padrão): pálpebra reta pesada, boca pequena
    f = Face()
    for s in (1, -1):
        eye(f, s, lid=0.009, slope=0.22)
        brow(f, s, z=1.054, angle=10)
    mouth_line(f, [(-0.019, MOUTH_Z - 0.001), (-0.006, MOUTH_Z + 0.001), (0.006, MOUTH_Z + 0.001), (0.019, MOUTH_Z - 0.001)])
    faces.append(f.build("Rosto_Serio"))

    # Focado: olhos mais fechados, sobrancelhas mais baixas, boca apertada
    f = Face()
    for s in (1, -1):
        eye(f, s, lid=0.004, slope=0.32, low=-0.02)
        brow(f, s, z=1.046, angle=17)
    mouth_line(f, [(-0.013, MOUTH_Z), (0.0, MOUTH_Z - 0.0015), (0.013, MOUTH_Z)], 0.005)
    faces.append(f.build("Rosto_Focado"))

    # Sorriso frio: meio sorriso de um lado, uma sobrancelha levantada
    f = Face()
    eye(f, 1, lid=0.006, slope=0.18)
    eye(f, -1, lid=0.003, slope=0.28)
    brow(f, 1, z=1.062, angle=-4, arch=0.004)
    brow(f, -1, z=1.05, angle=14)
    f.strip([(-0.021, MOUTH_Z + 0.0), (-0.008, MOUTH_Z - 0.002), (0.006, MOUTH_Z - 0.001), (0.018, MOUTH_Z + 0.004),
             (0.026, MOUTH_Z + 0.011)], 0.005, 0.0035, "Tinta", 2)
    faces.append(f.build("Rosto_SorrisoFrio"))

    # Bravo: sobrancelhas em V, olhos apertados, dentes cerrados
    f = Face()
    for s in (1, -1):
        eye(f, s, lid=0.002, slope=0.55, low=-0.018, lid_w=0.011)
        brow(f, s, z=1.041, angle=30, w0=0.012, w1=0.005)
    w, h = 0.05, 0.026
    pts = []
    for i in range(20):
        a = 2 * math.pi * i / 20
        pts.append((w * 0.5 * math.cos(a), MOUTH_Z - 0.004 + h * 0.5 * math.sin(a) * (0.75 if math.sin(a) > 0 else 1.0)))
    f.poly(pts, "Boca", 0)
    f.poly([(-0.019, MOUTH_Z + 0.002), (0.019, MOUTH_Z + 0.002), (0.019, MOUTH_Z - 0.012), (-0.019, MOUTH_Z - 0.012)], "Dentes", 1)
    f.strip([(-0.019, MOUTH_Z - 0.005), (0.019, MOUTH_Z - 0.005)], 0.0022, 0.0022, "Tinta", 2)
    faces.append(f.build("Rosto_Bravo"))

    # Feliz: olhos fechados em arco (^ ^) e boca aberta sorrindo
    f = Face()
    for s in (1, -1):
        cx = s * EYE_X
        arc = [(cx + 0.03 * (2 * i / 10 - 1), EYE_Z - 0.008 + 0.02 * (1 - (2 * i / 10 - 1) ** 2)) for i in range(11)]
        f.strip(arc, 0.009, 0.009, "Tinta", 3)
        brow(f, s, z=1.064, angle=-6, arch=0.004)
    pts = [(-0.026, MOUTH_Z + 0.004)]
    for i in range(1, 12):
        k = i / 12
        pts.append((-0.026 + 0.052 * k, MOUTH_Z + 0.004 - 0.024 * math.sin(math.pi * k)))
    pts.append((0.026, MOUTH_Z + 0.004))
    f.poly(pts, "Boca", 0)
    faces.append(f.build("Rosto_Feliz"))

    # Surpreso: olhos arregalados, sobrancelhas no alto, boca em "o"
    f = Face()
    for s in (1, -1):
        eye(f, s, a=0.03, b=0.035, pupil=(0.009, 0.012), top_follow=True, lid_w=0.006, wing=False)
        brow(f, s, z=1.074, angle=-8, arch=0.006, w0=0.008)
    f.poly(ellipse(0.0, MOUTH_Z - 0.004, 0.011, 0.015, 16), "Boca", 0)
    faces.append(f.build("Rosto_Surpreso"))
    return faces


# ---------------------------------------------------------------------------------------------------------
# Pesos de pele
def smoothstep(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def spine_weights(z):
    """Peso ao longo da coluna (quadril → pescoço), com transições suaves."""
    chain = [("Hips", RIG["hips"]), ("Spine", RIG["spine"]), ("Chest", RIG["chest"]), ("UpperChest", RIG["upper_chest"]),
             ("Neck", RIG["neck"])]
    if z <= chain[0][1] + 0.03:
        return {"Hips": 1.0}
    for (b0, z0), (b1, z1) in zip(chain, chain[1:]):
        if z <= z1 + 0.03:
            k = smoothstep(z1 - 0.035, z1 + 0.035, z)
            return {b0: 1 - k, b1: k}
    return {"Neck": 1.0}


def leg_weights(x, y, z):
    side = "Left" if x > 0 else "Right"
    if z < RIG["ankle_z"] + 0.05:
        k = smoothstep(RIG["toe_y"] + 0.02, RIG["toe_y"] - 0.03, y)  # bico da bota
        return {f"{side}Foot": 1 - k, f"{side}Toes": k} if k > 0 else {f"{side}Foot": 1.0}
    if z < RIG["ankle_z"] + 0.11:
        k = smoothstep(RIG["ankle_z"] + 0.05, RIG["ankle_z"] + 0.11, z)
        return {f"{side}Foot": 1 - k, f"{side}LowerLeg": k}
    k = smoothstep(RIG["knee_z"] - 0.035, RIG["knee_z"] + 0.035, z)
    w = {f"{side}LowerLeg": 1 - k, f"{side}UpperLeg": k}
    if z > RIG["hips"] - 0.02:  # topo da coxa, escondido no casaco
        h = smoothstep(RIG["hips"] - 0.02, RIG["hips"] + 0.05, z)
        w = {b: v * (1 - h) for b, v in w.items()}
        w["Hips"] = h
    return w


def coat_weights(x, y, z):
    if z >= WAIST - 0.02:
        w = spine_weights(z)
        # ombros: um pouco no osso do ombro (dá para "encolher os ombros")
        if z > 0.64 and abs(x) > 0.14:
            k = smoothstep(0.14, 0.23, abs(x)) * smoothstep(0.64, 0.72, z) * 0.7
            w = {b: v * (1 - k) for b, v in w.items()}
            w["LeftShoulder" if x > 0 else "RightShoulder"] = k
        return w
    # saia do casaco: quadril + um pouco da coxa do lado (balança com as pernas)
    side = "Left" if x > 0 else "Right"
    k = smoothstep(WAIST, 0.27, z) * smoothstep(0.02, 0.14, abs(x)) * 0.55
    return {"Hips": 1 - k, f"{side}UpperLeg": k}


def skin(o, rig, fn):
    """Cria grupos de vértices pelo ponto (fn(x,y,z) -> {osso: peso}) e liga ao armature."""
    groups = {}
    for v in o.data.vertices:
        p = o.matrix_world @ v.co
        for b, wv in fn(p.x, p.y, p.z).items():
            if wv <= 1e-4:
                continue
            if b not in groups:
                groups[b] = o.vertex_groups.get(b) or o.vertex_groups.new(name=b)
            groups[b].add([v.index], wv, "REPLACE")
    o.parent = rig
    o.matrix_parent_inverse = rig.matrix_world.inverted()
    mod = o.modifiers.new("Armature", "ARMATURE")
    mod.object = rig


def head_weights(x, y, z):
    if z < RIG["head"] - 0.02:
        k = smoothstep(RIG["neck"], RIG["head"] - 0.02, z)
        return {"Neck": 1 - k, "Head": k} if z > RIG["neck"] else {"Neck": 0.5, "UpperChest": 0.5}
    return {"Head": 1.0}


# ---------------------------------------------------------------------------------------------------------
def build():
    rig = make_rig_base.build()
    for name, hexc in PAL.items():
        c = srgb(hexc)
        M[name] = ab.material(None, name, c, roughness=0.55 if name in ("Metal", "Coroa") else 0.8,
                              metallic=0.3 if name in ("Metal", "Coroa") else 0.0, emission=c if name in EMIT else None)

    coat = make_coat()
    collar = make_collar()
    buttons = make_buttons()
    legs = make_legs()
    boots = make_boots()

    # corpo: um objeto (pele calculada por peça antes de juntar)
    for o in [coat, collar, buttons]:
        ab.finalize(o)
    skin(coat, rig, coat_weights)
    skin(collar, rig, lambda x, y, z: {"UpperChest": 0.6, "Neck": 0.4} if z > 0.8 else {"UpperChest": 1.0})
    skin(buttons, rig, lambda x, y, z: spine_weights(z))
    for o in legs + boots:
        ab.finalize(o)
        skin(o, rig, leg_weights)
    body = join("Corpo", [coat, collar, buttons] + legs + boots)

    head = join("Cabeca", make_head())
    ab.finalize(head)
    skin(head, rig, head_weights)

    crown = make_crown()
    ab.finalize(crown)
    for p in crown.data.polygons:
        p.use_smooth = False
    skin(crown, rig, lambda x, y, z: {"Head": 1.0})

    faces = make_faces()
    for f in faces:
        ab.finalize(f)
        skin(f, rig, lambda x, y, z: {"Head": 1.0})

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

    objs = [rig, body, head, crown] + faces + hands
    tris = 0
    for o in objs:
        if o.type == "MESH":
            o.data.calc_loop_triangles()
            tris += len(o.data.loop_triangles)
    print(f"[AB] rei: {tris} triângulos")
    return objs


if __name__ == "__main__":
    objs = build()
    ab.export_glb("rei", objs, animations=False)
    ab.save_blend("rei")
