"""Gera a textura de madeira do piso (sem dependências: Python puro + zlib).

Saída: assets/textures/wood_floor.png — 1024x1024, sem emendas (repete nos dois eixos).
Um ladrilho representa WOOD_TILE metros (o shader do piso usa o mesmo valor: Court.WoodTileSize).
Tábuas correm ao longo de U (= eixo X do mundo, comprimento da quadra).

Paleta da concept (court.png): Wood Light #E6AA67, Wood Dark #C27B46.
Uso:  python art_source/scripts/make_wood_floor.py
"""
import math
import os
import random
import struct
import zlib

SIZE = 1024
ROWS = 12                 # tábuas por ladrilho (4 m / 12 = 0,33 m cada)
LIGHT = (0xE6, 0xAA, 0x67)
DARK = (0xC2, 0x7B, 0x46)
SEAM = (0xB0, 0x6C, 0x3C)
SEED = 11

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "textures", "wood_floor.png")


def lerp(a, b, t):
    return a + (b - a) * t


def make():
    rng = random.Random(SEED)
    row_h = SIZE / ROWS
    rows = []
    for r in range(ROWS):
        # cortes (fim de tábua) distribuídos em volta do ladrilho: posições em [0, SIZE), circulares
        n = rng.choice([2, 3, 3, 4])
        start = rng.uniform(0, SIZE)
        cuts = sorted((start + i * SIZE / n + rng.uniform(-0.12, 0.12) * SIZE / n) % SIZE for i in range(n))
        segs = []
        for _ in cuts:
            tone = rng.random() ** 1.6 * 0.55          # quase todas claras, algumas mais escuras
            segs.append({
                "tone": tone,
                "ph": [rng.uniform(0, math.tau) for _ in range(4)],
                "k": [rng.choice([5, 7, 9, 11]), rng.choice([13, 17, 19]), rng.choice([1, 2, 3])],
            })
        rows.append((cuts, segs))

    px = bytearray()
    for y in range(SIZE):
        r = int(y // row_h)
        fy = (y - r * row_h) / row_h              # 0..1 dentro da tábua
        cuts, segs = rows[r]
        edge_y = min(y - r * row_h, (r + 1) * row_h - y)
        px.append(0)  # filtro PNG: nenhum
        for x in range(SIZE):
            # tábua desta coluna = último corte <= x (circular)
            i = len(cuts) - 1
            for j, c in enumerate(cuts):
                if c <= x:
                    i = j
            seg = segs[i]
            k, ph = seg["k"], seg["ph"]
            u = x / SIZE
            # veio: listras finas ao longo da tábua, levemente onduladas (periódicas em u → sem emenda)
            wob = 0.035 * math.sin(math.tau * k[2] * u + ph[0])
            g = 0.55 * math.sin(math.tau * (fy + wob) * k[0] + ph[1]) + 0.45 * math.sin(math.tau * (fy - wob) * k[1] + ph[2])
            g = g * 0.5 + 0.5
            t = seg["tone"] + 0.10 * g ** 3
            t += 0.03 * math.sin(math.tau * 2 * u + ph[3])
            col = [lerp(LIGHT[c], DARK[c], min(max(t, 0.0), 1.0)) for c in range(3)]
            # juntas: entre tábuas (2 px) e nas pontas (2 px)
            dx = min(abs(x - c) for c in cuts)
            dx = min(dx, SIZE - dx)
            if edge_y < 1.0 or dx < 1.0:
                col = list(SEAM)
            elif edge_y < 2.0 or dx < 2.0:
                col = [lerp(col[c], SEAM[c], 0.5) for c in range(3)]
            px.extend(int(round(v)) for v in col)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(px), 9)) + chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "wb") as f:
        f.write(png)
    print("ok ->", os.path.abspath(OUT))


if __name__ == "__main__":
    make()
