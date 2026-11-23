"""Gera docs/tuning_editor.html (o artifact de ajustes) a partir do esquema exportado pelo jogo.

Passos (rodar na raiz do projeto):
  1) <godot_console.exe> --headless --path . -- --dump-tuning-schema docs/tuning_schema.json
  2) python tools/build_tuning_artifact.py [versao]
  3) Publicar docs/tuning_editor.html no MESMO artifact (URL no CLAUDE.md).
"""
import json
import pathlib
import sys

root = pathlib.Path(__file__).resolve().parent.parent
schema = json.loads((root / "docs" / "tuning_schema.json").read_text(encoding="utf-8"))
for p in schema:  # arredonda floats (C# exporta 0.1f como 0.10000000149)
    for k in ("min", "max", "step", "default"):
        p[k] = float(f"{p[k]:.6g}")
template = (root / "tools" / "tuning_template.html").read_text(encoding="utf-8")
version = sys.argv[1] if len(sys.argv) > 1 else "dev"
html = template.replace("/*__SCHEMA__*/", json.dumps(schema, ensure_ascii=False).replace("</", "<\\/")).replace("__VERSION__", version)
out = root / "docs" / "tuning_editor.html"
out.write_text(html, encoding="utf-8")
print(f"OK: {len(schema)} parâmetros -> {out}")
