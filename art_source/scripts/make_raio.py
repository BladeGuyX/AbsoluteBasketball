"""
Personagem "Raio" (3º personagem; nome provisório) — concept: art_source/concepts/raio.png
-> assets/models/raio.glb + art_source/blend/raio.blend

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_raio.py

Mesmo esqueleto e proporções chibi do Rei (1,4 m até a ponta do cabelo). Reaproveita de make_rei.py a cabeça, os rostos
e a pele; de make_ametista.py as mechas Bézier (strand), a camiseta, o quadril da calça, a barra e o tênis.
Objetos separados (pedido do usuário):
- Corpo      jaqueta bomber preta (gola alta, punho na cintura, raios neon no peito, costas e lados), camiseta cinza,
             calça jogger com faixa em zigue-zague neon, barra presa, tênis pretos com sola neon e raio na lateral
- Cabeca     cabeça, pescoço, orelhas e cabelo ciano espetado — no osso Head
- Rosto_*    expressões (os MESMOS nomes de objeto do Rei; os rótulos da roda ficam em Scripts/Game/Characters.cs)
- MaoEsquerda / MaoDireita   esferas SEM esqueleto (a Godot move por código)
Materiais "Neon" e "SolaNeon" ficam sem sombra na Godot (brilho), ver CharacterVisual.ApplyMaterials.
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

# nome do material no .glb (chave = nome usado pelas funções reaproveitadas)
PAL = {
    "Pele": ("Pele", "#E6B690"),
    "Cabelo": ("Cabelo", "#1CB9CC"), "CabeloEscuro": ("CabeloEscuro", "#11808F"), "CabeloBrilho": ("CabeloBrilho", "#86EEF4"),
    "Casaco": ("Casaco", "#1B1D22"), "Forro": ("Forro", "#2C3138"), "Punho": ("Punho", "#121317"),
    "Camiseta": ("Camiseta", "#3E444C"), "Calca": ("Calca", "#1E2026"), "Barra": ("Barra", "#15161A"),
    "Tenis": ("Tenis", "#202329"), "TenisVerde": ("TenisDetalhe", "#383E46"), "Sola": ("SolaNeon", "#9EF6FA"),
    "Neon": ("Neon", "#2EE6F2"),
    "Olhos": ("Olhos", "#46D9EC"), "Pupila": ("Pupila", "#0B4450"), "Brilho": ("Brilho", "#FFFFFF"),
    "Tinta": ("Tinta", "#1A1A20"), "Boca": ("Boca", "#5E2018"), "Dentes": ("Dentes", "#FFFFFF"),
}
RIG = make_rig_base.PROP
smooth, new_obj, add_mats, join, solidify, mat = rei.smooth, rei.new_obj, rei.add_mats, rei.join, rei.solidify, rei.mat

# ---------------------------------------------------------------------------------------------------------
# Jaqueta bomber: fechada (zíper), um pouco estufada, termina num punho na cintura; abre em V só perto da gola
J_HEM, J_TOP = 0.425, 0.795
J_OPEN = [(0.40, 0.035), (0.62, 0.035), (0.70, 0.13), (0.76, 0.34), (0.795, 0.62)]


def jr(z):
    rx, ry = rei.coat_r(z)
    b = 0.014 * math.sin(math.pi * min(1.0, max(0.0, (z - 0.40) / 0.36)))
    return rx + b, ry + b


def jpt(th, z, grow=0.0):
    rx, ry = jr(z)
    return Vector(((rx + grow) * math.sin(th), -(ry + grow) * math.cos(th), z))


def outward(o):
    o.data.update()
    if sum(p.normal.dot(Vector((p.center.x, p.center.y, 0))) for p in o.data.polygons) < 0:
        for p in o.data.polygons:
            p.flip()


def make_jacket():
    bm = bmesh.new()
    zs = [J_HEM + (J_TOP - J_HEM) * t for t in (0, 0.08, 0.17, 0.27, 0.38, 0.49, 0.6, 0.7, 0.78, 0.85, 0.91, 0.96, 1.0)]
    NC = 48
    grid = []
    for z in zs:
        fo = ame.lerp_table(J_OPEN, z)
        grid.append([bm.verts.new(jpt(fo + (c / NC) * (2 * math.pi - 2 * fo), z)) for c in range(NC + 1)])
    for r in range(len(zs) - 1):
        for c in range(NC):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("jaqueta", bm)
    outward(o)
    add_mats(o, "Casaco", "Forro")
    smooth(o)
    solidify(o, 0.014, -1.0, mat_off=1, rim_off=1)
    return o


def make_waistband():
    rings = []
    for z, k in ((0.392, -0.012), (0.398, 0.0), (0.438, 0.0), (0.446, -0.01)):
        rx, ry = jr(max(z, 0.43))
        rings.append((z, rx + 0.004 + k, ry + 0.004 + k, 0.0))
    o = ame.ell_tube(rings, 40, "punho")
    outward(o)
    add_mats(o, "Punho")
    smooth(o)
    solidify(o, 0.01, -1.0)
    return o


def make_collar():
    """Gola alta em pé, aberta só na frente (zíper), forro cinza por dentro."""
    bm = bmesh.new()
    gap = 0.5
    n, rows = 30, 4
    grid = []
    for r in range(rows + 1):
        t = r / rows
        row = []
        for c in range(n + 1):
            th = gap + (2 * math.pi - 2 * gap) * c / n
            back = 0.5 - 0.5 * math.cos(th)
            ztop = 0.885 + 0.04 * back
            z = 0.765 + (ztop - 0.765) * t
            flare = t * t
            row.append(bm.verts.new(((0.12 + 0.035 * flare) * math.sin(th), 0.008 - (0.103 + 0.03 * flare) * math.cos(th), z)))
        grid.append(row)
    for r in range(rows):
        for c in range(n):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj("gola", bm)
    outward(o)
    add_mats(o, "Casaco", "Forro")
    smooth(o)
    solidify(o, 0.012, -1.0, mat_off=1, rim_off=1)
    return o


# --- decalques neon na jaqueta (coordenadas locais em metros: u na horizontal, v na vertical) -----------------
BOLT = [(0.10, 0.5), (-0.28, -0.04), (-0.03, -0.04), (-0.13, -0.5), (0.29, 0.08), (0.04, 0.08)]


def jacket_poly(pts, th0, z0, size, grow=0.004, name="raio"):
    bm = bmesh.new()
    vs = []
    for u, v in pts:
        z = z0 + v * size
        rx, _ = jr(z)
        vs.append(bm.verts.new(jpt(th0 + u * size / rx, z, grow)))
    bm.faces.new(vs)
    o = new_obj(name, bm)
    outward(o)
    add_mats(o, "Neon")
    return o


def jacket_strip(pts, th0, w, grow=0.004):
    """Linha (u, z) sobre a jaqueta, centrada no ângulo th0."""
    bm = bmesh.new()
    left, right = [], []
    n = len(pts)
    for i, (u, z) in enumerate(pts):
        a, b = pts[max(i - 1, 0)], pts[min(i + 1, n - 1)]
        tu, tz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(tu, tz) or 1.0
        nu, nz = -tz / L * w * 0.5, tu / L * w * 0.5
        for (uu, zz), lst in (((u + nu, z + nz), left), ((u - nu, z - nz), right)):
            rx, _ = jr(zz)
            lst.append(bm.verts.new(jpt(th0 + uu / rx, zz, grow)))
    for i in range(n - 1):
        bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
    o = new_obj("faixa", bm)
    outward(o)
    add_mats(o, "Neon")
    return o


def zigzag(z0, z1, amp, n, u0=0.0):
    return [(u0 + (amp if i % 2 else -amp) * (0 if i in (0, n) else 1), z0 + (z1 - z0) * i / n) for i in range(n + 1)]


def make_neon_jacket():
    parts = [
        jacket_poly(BOLT, 0.48, 0.64, 0.085),                     # raio no peito (lado esquerdo do personagem)
        jacket_poly(BOLT, math.pi, 0.60, 0.19),                   # raio grande nas costas
    ]
    for s in (1, -1):
        parts.append(jacket_strip(zigzag(0.765, 0.455, 0.022, 5), s * math.pi / 2, 0.013))      # lados (costura)
        parts.append(jacket_strip(zigzag(0.6, 0.455, 0.014, 3), s * 0.30, 0.009))               # frente, perto do zíper
    # gola: friso neon na borda de cima
    bm = bmesh.new()
    gap, n = 0.5, 30
    row0, row1 = [], []
    for c in range(n + 1):
        th = gap + (2 * math.pi - 2 * gap) * c / n
        back = 0.5 - 0.5 * math.cos(th)
        ztop = 0.885 + 0.04 * back
        for z, row in ((ztop - 0.016, row0), (ztop - 0.006, row1)):
            k = ((z - 0.765) / (ztop - 0.765)) ** 2
            row.append(bm.verts.new(((0.123 + 0.035 * k) * math.sin(th), 0.008 - (0.106 + 0.03 * k) * math.cos(th), z)))
    for c in range(n):
        bm.faces.new((row0[c], row0[c + 1], row1[c + 1], row1[c]))
    g = new_obj("friso", bm)
    outward(g)
    add_mats(g, "Neon")
    parts.append(g)
    for p in parts:
        smooth(p)
    return parts


# --- calça: faixa em zigue-zague no lado de fora de cada perna ------------------------------------------------
LEG = [(0.50, 0.086), (0.42, 0.082), (0.33, 0.072), (0.27, 0.067), (0.21, 0.062), (0.15, 0.058)]


def leg_r(z):
    for (z0, r0), (z1, r1) in zip(LEG, LEG[1:]):
        if z1 <= z <= z0:
            return r1 + (r0 - r1) * (z - z1) / (z0 - z1)
    return LEG[0][1] if z > LEG[0][0] else LEG[-1][1]


def make_leg_stripes():
    parts = []
    for s in (1, -1):
        cx = s * RIG["leg_x"]
        pts = [(y, z) for y, z in zigzag(0.47, 0.235, 0.022, 6)]
        bm = bmesh.new()
        left, right = [], []
        w = 0.012
        for i, (y, z) in enumerate(pts):
            a, b = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
            ty, tz = b[0] - a[0], b[1] - a[1]
            L = math.hypot(ty, tz) or 1.0
            ny, nz = -tz / L * w * 0.5, ty / L * w * 0.5
            for (yy, zz), lst in (((y + ny, z + nz), left), ((y - ny, z - nz), right)):
                r = leg_r(zz)
                x = cx + s * (math.sqrt(max(r * r - yy * yy, 0.0)) + 0.003)
                lst.append(bm.verts.new((x, yy, zz)))
        for i in range(len(pts) - 1):
            bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
        o = new_obj("faixa_perna", bm)
        o.data.update()
        if sum(p.normal.x * s for p in o.data.polygons) < 0:
            for p in o.data.polygons:
                p.flip()
        add_mats(o, "Neon")
        smooth(o)
        parts.append(o)
    return parts


def make_shoe_bolts():
    """Raio em relevo na lateral de fora de cada tênis (a ponta sai para trás, como na concept)."""
    parts = []
    shape = [(0.075, 0.5), (-0.30, 0.02), (-0.05, 0.02), (-0.20, -0.5), (0.32, 0.1), (0.07, 0.1)]
    for s in (1, -1):
        cx = s * (RIG["leg_x"] + 0.079)
        bm = bmesh.new()
        # u = para trás (+Y), v = para cima
        vs = [bm.verts.new((cx, 0.0 + u * 0.15, 0.085 + v * 0.075)) for u, v in shape]
        bm.faces.new(vs)
        o = new_obj("raio_tenis", bm)
        o.data.update()
        if sum(p.normal.x * s for p in o.data.polygons) < 0:
            for p in o.data.polygons:
                p.flip()
        add_mats(o, "Neon")
        solidify(o, 0.008, 0.0)
        parts.append(o)
    return parts


# ---------------------------------------------------------------------------------------------------------
# Cabelo: ciano, bem espetado para cima e para os lados; duas mechas caindo na testa
def hair_spike(bm, az, el, L, r, up, out, back, mi=0, inset=0.92, side=0.0, flat=0.55):
    p0, d = rei.hair_point(az, el, inset)
    dirv = (d * out + Vector((side, back, up))).normalized()
    p1 = p0 + (d * 0.5 + dirv * 0.5).normalized() * L * 0.45
    p2 = p0 + dirv * L
    ame.strand(bm, p0, p1, p2, r, d, flat=flat, mi=mi, n=8, rings=6, keep=0.05)


def make_hair():
    # calota (rosto e orelhas livres)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=36, ring_count=18, radius=1.0, location=(0, 0, 0))
    cap = bpy.context.active_object
    bm = bmesh.new()
    bm.from_mesh(cap.data)
    kill = [v for v in bm.verts if (v.co.z < 0.4 and v.co.y < -0.38) or v.co.z < -0.5
            or (-0.55 < v.co.z < 0.05 and abs(v.co.x) > 0.78 and abs(v.co.y) < 0.42)]
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    for v in bm.verts:
        v.co = rei.HAIR_C + Vector((v.co.x * rei.HAIR_R.x, v.co.y * rei.HAIR_R.y, v.co.z * rei.HAIR_R.z))
    bm.to_mesh(cap.data)
    bm.free()
    cap.data.materials.append(mat("Cabelo"))
    smooth(cap)
    solidify(cap, 0.01, -1.0)

    # volume (o cabelo é cheio, sobe da cabeça)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1.0, location=(0, 0.02, 1.12))
    vol = bpy.context.active_object
    vol.scale = (0.225, 0.215, 0.155)
    ab.select_only([vol])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    vol.data.materials.append(mat("Cabelo"))
    smooth(vol)

    bm = bmesh.new()
    # topo: espetos grandes para cima
    for az, el, L in ((0, 82, 0.21), (60, 74, 0.2), (-60, 74, 0.2), (130, 72, 0.19), (-130, 72, 0.19), (180, 70, 0.19)):
        hair_spike(bm, az, el, L, 0.08, up=1.0, out=0.35, back=0.15)
    # anel de cima: para cima e para fora (a frente sobe da testa)
    for az in (-150, -110, -70, -30, 10, 50, 90, 130, 170):
        a = math.radians(az)
        front = max(0.0, math.cos(a))
        hair_spike(bm, az, 52, 0.19 - 0.02 * front, 0.078, up=0.9 + 0.3 * front, out=0.75, back=0.15 + 0.2 * front)
    # anel do meio: para os lados e para trás (silhueta larga), sem cobrir o rosto
    for az in (-165, -135, -105, -78, 78, 105, 135, 165):
        hair_spike(bm, az, 24, 0.16, 0.07, up=0.45, out=1.0, back=0.35, mi=0)
    # nuca: para baixo/trás, mais escuras
    for az, el in ((180, 2), (150, -4), (-150, -4), (120, -10), (-120, -10), (180, -24), (155, -26), (-155, -26)):
        hair_spike(bm, az, el, 0.12, 0.062, up=-0.45, out=0.9, back=0.5, mi=1)
    # costeletas (na frente da orelha)
    for s in (1, -1):
        hair_spike(bm, s * 68, 14, 0.09, 0.045, up=-1.0, out=0.45, back=-0.1, mi=1, flat=0.45)
    # franja: mechas espetadas na testa (as pontas param acima das sobrancelhas)
    for az, L, sd in ((-34, 0.11, -0.35), (-12, 0.125, -0.12), (14, 0.115, 0.15), (38, 0.1, 0.4)):
        hair_spike(bm, az, 44, L, 0.052, up=-1.0, out=0.22, back=-0.08, side=sd, flat=0.45, inset=0.97)
    # brilho: mechas finas claras por cima dos espetos da frente
    for az in (-30, 10, 50):
        p0, d = rei.hair_point(az, 58, 1.0)
        a = math.radians(az)
        p2 = p0 + Vector((math.sin(a) * 0.06, -0.02, 0.15))
        ame.strand(bm, p0, p0 + d * 0.05 + Vector((0, 0, 0.06)), p2, 0.026, d, flat=0.45, mi=2, n=6, keep=0.05)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    sp = new_obj("mechas", bm)
    add_mats(sp, *ame.HAIR_MATS)
    smooth(sp)
    return [cap, vol, sp]


# ---------------------------------------------------------------------------------------------------------
# Rostos (concept: Confident Grin, Focused, Excited, Annoyed)
MZ = rei.MOUTH_Z


def make_faces():
    R = rei
    faces = []

    # Tranquilo (slot "Serio" = cara padrão): olhar firme, canto da boca levemente para cima
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.007, slope=0.2)
        R.brow(f, s, z=1.056, angle=12)
    f.strip([(-0.018, MZ), (-0.004, MZ - 0.0015), (0.01, MZ - 0.0005), (0.022, MZ + 0.004)], 0.0055, 0.004, "Tinta", 2)
    faces.append(f.build("Rosto_Serio"))

    # Focado
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.003, slope=0.32, low=-0.018)
        R.brow(f, s, z=1.046, angle=18)
    R.mouth_line(f, [(-0.014, MZ), (0.0, MZ - 0.001), (0.014, MZ)], 0.005)
    faces.append(f.build("Rosto_Focado"))

    # Sorriso confiante (slot "SorrisoFrio"): sobrancelhas baixas, sorrisão de lado com dentes
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.004, slope=0.3, low=-0.014)
        R.brow(f, s, z=1.05, angle=18)
    ame.grin_mouth(f, 0.07, 0.024, tilt=0.012, cx=0.003)
    faces.append(f.build("Rosto_SorrisoFrio"))

    # Irritado (slot "Bravo"): sobrancelhas em V, olhar torto, boca para baixo
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, lid=0.002, slope=0.5, low=-0.016, lid_w=0.011)
        R.brow(f, s, z=1.05, angle=26, w0=0.012, w1=0.005)
    f.strip([(-0.02, MZ - 0.007), (-0.008, MZ - 0.001), (0.008, MZ - 0.001), (0.02, MZ - 0.007)], 0.0055, 0.0055, "Tinta", 2)
    faces.append(f.build("Rosto_Bravo"))

    # Empolgado (slot "Feliz"): olhos bem abertos, sobrancelhas no alto, bocão aberto com dentes
    f = R.Face()
    for s in (1, -1):
        R.eye(f, s, a=0.031, b=0.032, top_follow=True, lid_w=0.008)
        R.brow(f, s, z=1.085, angle=-6, arch=0.005)
    ame.grin_mouth(f, 0.07, 0.045)
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
    for key, (name, hexc) in PAL.items():
        rei.M[key] = ab.material(None, name, rei.srgb(hexc), roughness=0.8,
                                 emission=rei.srgb(hexc) if name in ("Neon", "SolaNeon") else None)

    jacket = make_jacket()
    band = make_waistband()
    collar = make_collar()
    neon = make_neon_jacket()
    shirt = ame.make_shirt()
    pelvis = ame.make_pelvis()
    legs = rei.make_legs()
    stripes = make_leg_stripes()
    cuffs = ame.make_cuffs()
    shoes = ame.make_sneakers()
    bolts = make_shoe_bolts()

    upper = [jacket, band, collar] + neon + shirt + [pelvis]
    for o in upper:
        ab.finalize(o)
    for o in [jacket, band] + neon:
        rei.skin(o, rig, rei.coat_weights)
    rei.skin(collar, rig, lambda x, y, z: {"UpperChest": 0.6, "Neck": 0.4} if z > 0.8 else rei.spine_weights(z))
    for o in shirt:
        rei.skin(o, rig, lambda x, y, z: rei.spine_weights(z) if z < 0.8 else {"UpperChest": 0.5, "Neck": 0.5})
    rei.skin(pelvis, rig, lambda x, y, z: {"Hips": 1.0})
    for o in legs + stripes + cuffs + shoes + bolts:
        ab.finalize(o)
        rei.skin(o, rig, rei.leg_weights)
    body = join("Corpo", upper + legs + stripes + cuffs + shoes + bolts)

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
    print(f"[AB] raio: {tris} triângulos, topo {top:.3f} m")
    return objs


if __name__ == "__main__":
    objs = build()
    ab.export_glb("raio", objs, animations=False)
    ab.save_blend("raio")
