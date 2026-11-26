"""
Absolute Basketball — cenário em volta da quadra de praia (v0.32).

Rodar:  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\make_scenery.py
Saída (assets/models/):
  palmeira_a.glb, palmeira_b.glb   palmeiras (tronco curvo com anéis, folhas caídas, cocos)
  arquibancada.glb                 módulo de 8 m com 4 degraus, estrutura de metal, escada e guarda-corpo
                                   (material "Assento": a Godot pinta com a cor do time)
  guarda_sol.glb                   guarda-sol listrado (materiais "LonaA"/"LonaB")
  espreguicadeira.glb              espreguiçadeira de madeira com toalha
  torre_salva_vidas.glb            torre de salva-vidas (pernas, plataforma, cabine, telhado vermelho, escada, bandeira)
  poste_luz.glb                    poste de iluminação da quadra (luminárias emissivas "Lampada")
  pedra_a.glb, pedra_b.glb         pedras arredondadas
  arbusto.glb                      moita
  prancha.glb                      prancha de surfe fincada na areia

Convenções: 1 unidade = 1 m, Z para cima, origem no chão (base do objeto). A "frente" dos objetos com frente
(arquibancada, espreguiçadeira, torre) é −Y no Blender (vira +Z na Godot). Cores um pouco mais escuras que o desejado
(o toon da Godot clareia). Tudo low-poly; cada modelo é UM objeto com vários materiais (nomes estáveis).
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
import ab_common as ab  # noqa: E402

COLORS = {
    "Tronco": (0.45, 0.30, 0.17), "TroncoAnel": (0.33, 0.21, 0.12), "Folha": (0.13, 0.55, 0.22), "FolhaEscura": (0.08, 0.40, 0.17),
    "Coco": (0.36, 0.24, 0.10), "Estrutura": (0.50, 0.55, 0.62), "Assento": (0.20, 0.45, 0.85), "Degrau": (0.78, 0.78, 0.80),
    "LonaA": (0.90, 0.20, 0.18), "LonaB": (0.95, 0.93, 0.88), "Haste": (0.85, 0.85, 0.85), "Madeira": (0.62, 0.42, 0.24),
    "Toalha": (0.15, 0.62, 0.80), "MadeiraBranca": (0.90, 0.90, 0.88), "Telhado": (0.80, 0.14, 0.12), "Bandeira": (0.95, 0.75, 0.10),
    "Metal": (0.35, 0.38, 0.44), "Lampada": (1.0, 0.97, 0.85), "Pedra": (0.55, 0.53, 0.50), "PedraEscura": (0.42, 0.40, 0.38),
    "Prancha": (0.98, 0.55, 0.15), "PranchaFaixa": (0.95, 0.95, 0.95),
}


class Model:
    """Uma malha com vários materiais, montada com bmesh."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _set(self, faces, mat):
        idx = self.mi(mat)
        for f in faces:
            f.material_index = idx

    def box(self, center, size, mat, rot_z=0.0, rot_x=0.0):
        m = Matrix.Translation(Vector(center)) @ Matrix.Rotation(rot_z, 4, "Z") @ Matrix.Rotation(rot_x, 4, "X") @ Matrix.Diagonal((*size, 1))
        r = bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)
        self._set({f for v in r["verts"] for f in v.link_faces}, mat)

    def tube(self, pts, radii, mat, segs=10, cap=True, ring_mat=None, ring_every=0):
        """Tubo passando por pts (lista de Vector) com raio em cada ponto."""
        rings = []
        for i, p in enumerate(pts):
            d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            a = d.cross(Vector((1, 0, 0)) if abs(d.x) < 0.9 else Vector((0, 1, 0))).normalized()
            b = d.cross(a).normalized()
            ring = [self.bm.verts.new(p + (a * math.cos(k / segs * math.tau) + b * math.sin(k / segs * math.tau)) * radii[i]) for k in range(segs)]
            rings.append(ring)
        for i in range(len(rings) - 1):
            m = ring_mat if (ring_mat and ring_every and i % ring_every == ring_every - 1) else mat
            faces = [self.bm.faces.new((rings[i][k], rings[i][(k + 1) % segs], rings[i + 1][(k + 1) % segs], rings[i + 1][k])) for k in range(segs)]
            self._set(faces, m)
        if cap:
            self._set([self.bm.faces.new(list(reversed(rings[0]))), self.bm.faces.new(rings[-1])], mat)

    def cyl(self, base, top, r0, r1, mat, segs=10):
        self.tube([Vector(base), Vector(top)], [r0, r1], mat, segs)

    def ico(self, center, radius, mat, subdiv=2, scale=(1, 1, 1), jitter=0.0, seed=0):
        rnd = random.Random(seed)
        m = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((*scale, 1))
        r = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius, matrix=m)
        if jitter:
            c = Vector(center)
            for v in r["verts"]:
                v.co = c + (v.co - c) * (1 + rnd.uniform(-jitter, jitter))
        self._set({f for v in r["verts"] for f in v.link_faces}, mat)

    def frond(self, base, yaw, pitch, length, width, droop, mat, segs=7):
        """Folha de palmeira: tira com nervura (formato de V raso), caindo na ponta."""
        rows = []
        for i in range(segs + 1):
            t = i / segs
            w = width * (math.sin(math.pi * min(t * 1.1, 1.0)) ** 0.6) * (1 - 0.6 * t)
            x = length * t
            z = math.sin(pitch) * x - droop * t * t
            rows.append([(x, -w, z - w * 0.25), (x, 0, z + 0.03), (x, w, z - w * 0.25)])
        rot = Matrix.Rotation(yaw, 3, "Z")
        verts = [[self.bm.verts.new(Vector(base) + rot @ Vector(p)) for p in row] for row in rows]
        faces = []
        for i in range(segs):
            for j in range(2):
                faces.append(self.bm.faces.new((verts[i][j], verts[i][j + 1], verts[i + 1][j + 1], verts[i + 1][j])))
        self._set(faces, mat)

    def build(self):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(obj)
        for m in self.mats:
            ab.material(obj, m, COLORS[m], roughness=0.7, emission=(COLORS[m] if m == "Lampada" else None))
        ab.finalize(obj)
        return obj


def export(model):
    obj = model.build()
    ab.export_glb(model.name, [obj])
    return obj


# ---------------------------------------------------------------------------------------------------------------
def palm(name, height, bend, lean_dir, n_fronds, seed):
    rnd = random.Random(seed)
    m = Model(name)
    pts, radii = [], []
    n = 16
    for i in range(n + 1):
        t = i / n
        off = bend * t * t
        pts.append(Vector((math.cos(lean_dir) * off, math.sin(lean_dir) * off, height * t)))
        radii.append(0.24 * (1 - 0.45 * t) * (1.08 if i % 2 else 1.0))
    m.tube(pts, radii, "Tronco", segs=9, ring_mat="TroncoAnel", ring_every=2)
    top = pts[-1]
    for k in range(n_fronds):
        yaw = k / n_fronds * math.tau + rnd.uniform(-0.2, 0.2)
        m.frond(top + Vector((0, 0, 0.05)), yaw, math.radians(rnd.uniform(15, 35)), rnd.uniform(2.4, 3.1), rnd.uniform(0.38, 0.5),
                rnd.uniform(1.4, 2.0), "Folha" if k % 2 else "FolhaEscura")
    for k in range(3):  # brotos de cima
        m.frond(top + Vector((0, 0, 0.1)), k * 2.1 + 0.4, math.radians(60), 1.2, 0.25, 0.3, "Folha")
    for k in range(4):  # cocos
        a = k / 4 * math.tau + 0.3
        m.ico(top + Vector((math.cos(a) * 0.22, math.sin(a) * 0.22, -0.22)), 0.14, "Coco", subdiv=1)
    return export(m)


def bleachers():
    m = Model("arquibancada")
    W, rows, step_d, step_h = 8.0, 4, 0.85, 0.48
    depth = rows * step_d
    # degraus de concreto (cada um um bloco que vai até o chão) + assento colorido em cima
    for r in range(rows):
        y = r * step_d + step_d / 2
        h = (r + 1) * step_h
        m.box((0, y, h / 2), (W, step_d, h), "Degrau")
        m.box((0, y - step_d * 0.18, h + 0.06), (W - 0.3, step_d * 0.42, 0.12), "Assento")
        m.box((0, y + step_d * 0.18, h + 0.11), (W - 0.3, 0.08, 0.22), "Assento")  # encosto baixo
    # estrutura: pilares e travessas atrás
    for x in (-W / 2 + 0.2, 0, W / 2 - 0.2):
        m.box((x, depth + 0.1, (rows * step_h + 1.0) / 2), (0.14, 0.14, rows * step_h + 1.0), "Estrutura")
    # guarda-corpo atrás e nas laterais
    top_h = rows * step_h
    m.box((0, depth + 0.1, top_h + 1.0), (W, 0.07, 0.07), "Estrutura")
    m.box((0, depth + 0.1, top_h + 0.55), (W, 0.05, 0.05), "Estrutura")
    for sx in (-1, 1):
        x = sx * (W / 2 + 0.05)
        for r in range(rows):
            y = r * step_d + step_d / 2
            m.box((x, y, (r + 1) * step_h + 0.5), (0.06, 0.06, 1.0), "Estrutura")
    return export(m)


def umbrella():
    m = Model("guarda_sol")
    m.cyl((0, 0, -0.3), (0, 0, 2.4), 0.035, 0.03, "Haste", segs=8)
    n, R, apex, edge = 8, 1.35, 2.55, 2.05
    tip = m.bm.verts.new((0, 0, apex))
    ring = [m.bm.verts.new((math.cos(k / n * math.tau) * R, math.sin(k / n * math.tau) * R, edge)) for k in range(n)]
    for k in range(n):
        f = m.bm.faces.new((tip, ring[k], ring[(k + 1) % n]))
        m._set([f], "LonaA" if k % 2 == 0 else "LonaB")
        # aba de baixo (franja)
        a, b = ring[k].co, ring[(k + 1) % n].co
        v = [m.bm.verts.new(a), m.bm.verts.new(b), m.bm.verts.new(b + Vector((0, 0, -0.12))), m.bm.verts.new(a + Vector((0, 0, -0.12)))]
        m._set([m.bm.faces.new(v)], "LonaB" if k % 2 == 0 else "LonaA")
    m.ico((0, 0, apex + 0.05), 0.07, "Haste", subdiv=1)
    return export(m)


def lounge():
    m = Model("espreguicadeira")
    L, Wd = 1.9, 0.65
    # pernas
    for x in (-Wd / 2 + 0.05, Wd / 2 - 0.05):
        for y in (-0.7, 0.75):
            m.box((x, y, 0.15), (0.06, 0.06, 0.3), "Madeira")
    # laterais do assento e ripas (de −0,78 a 0,88)
    for x in (-Wd / 2 + 0.03, Wd / 2 - 0.03):
        m.box((x, 0.05, 0.31), (0.05, 1.66, 0.06), "Madeira")
    for i in range(8):
        m.box((0, -0.68 + i * 0.2, 0.33), (Wd, 0.16, 0.04), "Madeira")
    # encosto preso na ponta de trás (+Y), inclinado para trás — quem deita olha para −Y
    ang = math.radians(55)
    c = Vector((0, 0.86 + 0.38 * math.cos(ang), 0.33 + 0.38 * math.sin(ang)))
    m.box(tuple(c), (Wd, 0.76, 0.04), "Madeira", rot_x=ang)
    # toalha no assento e no encosto
    m.box((0, 0.05, 0.365), (Wd - 0.1, 1.55, 0.03), "Toalha")
    m.box(tuple(c + Vector((0, -0.025, 0.035))), (Wd - 0.1, 0.7, 0.03), "Toalha", rot_x=ang)
    return export(m)


def lifeguard():
    m = Model("torre_salva_vidas")
    H = 2.2
    for x in (-0.8, 0.8):
        for y in (-0.8, 0.8):
            m.cyl((x * 1.15, y * 1.15, 0), (x, y, H), 0.07, 0.06, "MadeiraBranca", segs=6)
    m.box((0, 0, H + 0.05), (2.0, 2.0, 0.12), "Madeira")
    # cabine
    m.box((0, 0.25, H + 0.75), (1.7, 1.2, 1.3), "MadeiraBranca")
    m.box((0, -0.36, H + 0.95), (1.4, 0.02, 0.6), "Metal")  # janela
    # telhado de duas águas
    for s in (-1, 1):
        m.box((s * 0.5, 0.25, H + 1.62), (1.15, 1.6, 0.08), "Telhado", rot_z=0, rot_x=0)
    m.box((0, 0.25, H + 1.72), (0.12, 1.6, 0.12), "Telhado")
    # guarda-corpo da frente
    m.box((0, -0.95, H + 0.55), (2.0, 0.05, 0.05), "MadeiraBranca")
    for x in (-0.95, 0, 0.95):
        m.box((x, -0.95, H + 0.3), (0.05, 0.05, 0.5), "MadeiraBranca")
    # escada (frente)
    for i in range(6):
        z = 0.3 + i * (H / 6)
        y = -1.0 - (H - z) * 0.45
        m.box((0, y, z), (0.8, 0.08, 0.04), "Madeira")
    for sx in (-0.42, 0.42):
        m.box((sx, -1.0 - H * 0.22, H / 2), (0.06, 0.06, H * 1.1), "Madeira", rot_x=-math.atan2(H * 0.45, H))
    # bandeira
    m.cyl((0.75, 0.75, H + 1.6), (0.75, 0.75, H + 2.9), 0.03, 0.025, "Metal", segs=6)
    m.box((0.75, 0.75 + 0.35, H + 2.65), (0.02, 0.7, 0.45), "Bandeira")
    return export(m)


def light_pole():
    m = Model("poste_luz")
    H = 9.0
    m.cyl((0, 0, 0), (0, 0, H), 0.16, 0.1, "Metal", segs=10)
    m.cyl((0, 0, 0), (0, 0, 0.5), 0.3, 0.26, "Metal", segs=10)  # base
    m.box((0, -0.2, H + 0.1), (1.6, 0.3, 0.9), "Metal")  # painel
    for x in (-0.5, 0, 0.5):
        for z in (-0.22, 0.22):
            m.box((x, -0.37, H + 0.1 + z), (0.38, 0.05, 0.3), "Lampada")
    return export(m)


def rock(name, seed, scale):
    m = Model(name)
    m.ico((0, 0, 0.35 * scale[2]), 0.6, "Pedra", subdiv=2, scale=scale, jitter=0.18, seed=seed)
    m.ico((0.45 * scale[0], 0.2, 0.18), 0.32, "PedraEscura", subdiv=1, scale=(1, 0.9, 0.7), jitter=0.2, seed=seed + 1)
    return export(m)


def bush():
    m = Model("arbusto")
    rnd = random.Random(5)
    for k in range(6):
        a = k / 6 * math.tau
        r = rnd.uniform(0.35, 0.55)
        m.ico((math.cos(a) * 0.45, math.sin(a) * 0.45, r * 0.8), r, "Folha" if k % 2 else "FolhaEscura", subdiv=1, jitter=0.12, seed=k)
    m.ico((0, 0, 0.75), 0.6, "Folha", subdiv=2, jitter=0.1, seed=9)
    return export(m)


def surfboard():
    m = Model("prancha")
    # elipsoide achatado, fincado em pé e um pouco inclinado
    tilt = Matrix.Rotation(math.radians(12), 4, "Y")
    r = bmesh.ops.create_uvsphere(m.bm, u_segments=14, v_segments=10, radius=1.0,
                                  matrix=Matrix.Translation((0, 0, 0.75)) @ tilt @ Matrix.Diagonal((0.28, 0.05, 1.0, 1)))
    m._set({f for v in r["verts"] for f in v.link_faces}, "Prancha")
    m.box((0.16, 0, 0.75), (0.02, 0.105, 1.6), "PranchaFaixa", rot_z=0)
    return export(m)


def main():
    ab.reset_scene()
    palm("palmeira_a", 7.0, 1.6, 0.0, 9, 1)
    palm("palmeira_b", 5.2, 2.2, 0.0, 8, 2)
    bleachers()
    umbrella()
    lounge()
    lifeguard()
    light_pole()
    rock("pedra_a", 3, (1.2, 1.0, 0.8))
    rock("pedra_b", 7, (0.8, 1.1, 1.0))
    bush()
    surfboard()
    ab.save_blend("scenery")


main()
