"""
Confere um .glb exportado SEM abrir o Blender: tamanho (m), base, malhas, materiais, UV, ossos e animações.
Avisa o que fere as regras de arte (escala, UV, nomes de animação, "-loop", ossos fora do rig_base).

Uso:  python tools/inspect_glb.py assets/models/ball.glb [mais arquivos...]
"""
import json
import struct
import sys

RIG_BONES = {"Root", "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head"} | {
    f"{s}{b}" for s in ("Left", "Right")
    for b in ("Shoulder", "UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg", "Foot", "Toes")
}


def load(path):
    with open(path, "rb") as f:
        data = f.read()
    magic, _, _ = struct.unpack_from("<III", data, 0)
    if magic != 0x46546C67:
        raise ValueError("não é GLB")
    ln, kind = struct.unpack_from("<II", data, 12)
    return json.loads(data[20:20 + ln])


def mat_mul(a, b):
    return [sum(a[r + 4 * k] * b[k + 4 * c] for k in range(4)) for c in range(4) for r in range(4)]


def node_matrix(n):
    if "matrix" in n:
        return n["matrix"]
    t = n.get("translation", [0, 0, 0]); q = n.get("rotation", [0, 0, 0, 1]); s = n.get("scale", [1, 1, 1])
    x, y, z, w = q
    r = [1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w), 0,
         2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w), 0,
         2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y), 0,
         0, 0, 0, 1]
    for c in range(3):
        for rr in range(3):
            r[rr + 4 * c] *= s[c]
    r[12], r[13], r[14] = t
    return r


def inspect(path):
    g = load(path)
    print(f"\n== {path}")
    warn = []
    lo = [1e9] * 3; hi = [-1e9] * 3

    def walk(i, parent):
        n = g["nodes"][i]
        m = mat_mul(parent, node_matrix(n))
        if "mesh" in n:
            for prim in g["meshes"][n["mesh"]]["primitives"]:
                acc = g["accessors"][prim["attributes"]["POSITION"]]
                for corner in [(x, y, z) for x in (0, 1) for y in (0, 1) for z in (0, 1)]:
                    v = [acc["max"][k] if corner[k] else acc["min"][k] for k in range(3)]
                    w = [m[r] * v[0] + m[4 + r] * v[1] + m[8 + r] * v[2] + m[12 + r] for r in range(3)]
                    for k in range(3):
                        lo[k] = min(lo[k], w[k]); hi[k] = max(hi[k], w[k])
        for c in n.get("children", []):
            walk(c, m)

    ident = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
    for root in g["scenes"][g.get("scene", 0)]["nodes"]:
        walk(root, ident)
    if lo[0] < 1e8:
        # glTF: Y para cima. Na Godot: X largura, Y altura, Z profundidade (frente do modelo = +Z)
        print(f"tamanho: largura {hi[0]-lo[0]:.3f} m · altura {hi[1]-lo[1]:.3f} m · profundidade {hi[2]-lo[2]:.3f} m · base Y={lo[1]:.3f}")
        if hi[1] - lo[1] > 30 or max(hi[0]-lo[0], hi[1]-lo[1], hi[2]-lo[2]) < 0.02:
            warn.append("tamanho estranho — escala aplicada? (1 unidade = 1 m)")
    meshes = g.get("meshes", [])
    print(f"malhas: {len(meshes)} · materiais: {', '.join(m.get('name', '?') for m in g.get('materials', [])) or 'nenhum'}")
    for me in meshes:
        for prim in me["primitives"]:
            if "TEXCOORD_0" not in prim["attributes"]:
                warn.append(f"malha '{me.get('name')}' sem UV")
            if "material" not in prim:
                warn.append(f"malha '{me.get('name')}' sem material")
    for sk in g.get("skins", []):
        names = [g["nodes"][j].get("name", "?") for j in sk["joints"]]
        print(f"esqueleto: {len(names)} ossos")
        extra = [n for n in names if n not in RIG_BONES]
        missing = sorted(RIG_BONES - set(names))
        if extra:
            warn.append(f"ossos fora do rig_base: {', '.join(extra)}")
        if missing:
            warn.append(f"ossos do rig_base faltando: {', '.join(missing)}")
    for a in g.get("animations", []):
        name = a.get("name", "?")
        times = [g["accessors"][s["input"]].get("max", [0])[0] for s in a["samplers"]]
        dur = max(times) if times else 0
        print(f"animação: {name}  ({dur:.2f} s, {round(dur * 30)} frames a 30 fps)")
        base = name[:-5] if name.endswith("-loop") else name
        if base != base.lower() or " " in base:
            warn.append(f"animação '{name}' fora do snake_case")
    for w in warn:
        print("AVISO:", w)
    if not warn:
        print("OK: nenhuma regra violada")
    return not warn


if __name__ == "__main__":
    ok = all([inspect(p) for p in sys.argv[1:]])
    sys.exit(0 if ok else 1)
