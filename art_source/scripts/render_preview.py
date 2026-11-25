"""
Prévia de um modelo para comparar com a concept art: 4 vistas (frente, 3/4, lado, costas) num PNG só,
com um boneco de referência de 1,9 m (cápsula cinza) ao lado para conferir a escala.

Rodar:
  powershell -ExecutionPolicy Bypass -File tools\\blender.ps1 art_source\\scripts\\render_preview.py -- assets\\models\\ball.glb
  (ou com -Blend arquivo.blend para renderizar a cena aberta)
Opções depois do arquivo:  --no-ref (sem o boneco de referência)  --frame N (frame da animação ativa)
                           --hide A,B (esconde objetos cujo nome começa com A ou B)  --out NOME (nome do PNG)
Saída: art_source/previews/<nome>.png
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import ab_common as ab  # noqa: E402

args = ab.script_args()
src = next((a for a in args if not a.startswith("--") and not a.isdigit()), None)
show_ref = "--no-ref" not in args
frame = int(args[args.index("--frame") + 1]) if "--frame" in args else None
hide = args[args.index("--hide") + 1].split(",") if "--hide" in args else []
out_name = args[args.index("--out") + 1] if "--out" in args else None
src = next((a for i, a in enumerate(args) if not a.startswith("--") and not a.isdigit()
            and (i == 0 or args[i - 1] not in ("--hide", "--out", "--frame"))), None)

if src and src.lower().endswith(".glb"):
    ab.reset_scene()
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(src))
    name = os.path.splitext(os.path.basename(src))[0]
else:
    name = os.path.splitext(os.path.basename(bpy.data.filepath or "cena"))[0]
sc = bpy.context.scene
if out_name:
    name = out_name
for o in sc.objects:
    if any(o.name.startswith(h) for h in hide if h):
        o.hide_render = True
        o.hide_set(True)
if frame is not None:
    sc.frame_set(frame)

meshes = [o for o in sc.objects if o.type == "MESH" and o.visible_get()]
if not meshes:
    raise SystemExit("[AB] nada para renderizar")
dg = bpy.context.evaluated_depsgraph_get()
lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
for o in meshes:
    for c in o.evaluated_get(dg).bound_box:
        w = o.matrix_world @ Vector(c)
        lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
size = hi - lo
print(f"[AB] {name}: largura X={size.x:.3f} m, profundidade Y={size.y:.3f} m, altura Z={size.z:.3f} m, base Z={lo.z:.3f}")

if show_ref:  # referência de escala: cápsula de 1,9 m ao lado (lado +X do modelo)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.3, depth=1.9, location=(hi.x + 0.6, (lo.y + hi.y) / 2, 0.95))
    ref = bpy.context.active_object
    ref.name = "_ref_1.9m"
    m = ab.material(None, "_ref", (0.55, 0.55, 0.6))
    ref.data.materials.append(m)
    lo.x = min(lo.x, hi.x + 0.3); hi.x = hi.x + 0.9; hi.z = max(hi.z, 1.9); lo.z = min(lo.z, 0)

center = (lo + hi) / 2
radius = max((hi - lo).length / 2, 0.1)

sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading
sh.light = "STUDIO"
sh.color_type = "MATERIAL"
sh.show_object_outline = True
sh.show_cavity = True
sc.render.resolution_x = 640
sc.render.resolution_y = 640
sc.render.film_transparent = False
sc.world = sc.world or bpy.data.worlds.new("w")

cam_data = bpy.data.cameras.new("pv")
cam_data.type = "ORTHO"
cam_data.ortho_scale = radius * 2.3
cam = bpy.data.objects.new("pv", cam_data)
sc.collection.objects.link(cam)
sc.camera = cam

os.makedirs(ab.PREVIEW_DIR, exist_ok=True)
views = [("frente", -90), ("tres_quartos", -135), ("lado", 180), ("costas", 90)]  # ângulo em torno de Z (modelo olha para -Y)
paths = []
for label, deg in views:
    a = math.radians(deg)
    d = Vector((math.cos(a), math.sin(a), 0.25)).normalized()
    cam.location = center + d * radius * 4
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    p = os.path.join(ab.PREVIEW_DIR, f"_{name}_{label}.png")
    sc.render.filepath = p
    bpy.ops.render.render(write_still=True)
    paths.append(p)

# junta as 4 vistas lado a lado
imgs = [bpy.data.images.load(p) for p in paths]
w, h = imgs[0].size
out = bpy.data.images.new("sheet", w * len(imgs), h)
px = [0.0] * (w * len(imgs) * h * 4)
for i, im in enumerate(imgs):
    src_px = list(im.pixels)
    for y in range(h):
        row = src_px[y * w * 4:(y + 1) * w * 4]
        start = (y * w * len(imgs) + i * w) * 4
        px[start:start + w * 4] = row
out.pixels = px
final = os.path.join(ab.PREVIEW_DIR, f"{name}.png")
out.filepath_raw = final
out.file_format = "PNG"
out.save()
for p in paths:
    os.remove(p)
print(f"[AB] prévia: {final}  (frente | 3/4 | lado | costas)")
