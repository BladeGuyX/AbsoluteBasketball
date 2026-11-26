"""Compara um tuning.json (do artifact/do jogador) com os padrões do código (docs/tuning_schema.json).

Uso (na raiz do projeto):
  python tools/tuning_diff.py [Build/tuning.json]

Imprime só os valores diferentes do padrão (chave, padrão -> novo, seção/rótulo), as chaves que não existem
mais no esquema e os valores fora do min/max. Regenere o esquema antes se o Tuning.cs mudou:
  <godot_console> --headless --path . -- --dump-tuning-schema docs/tuning_schema.json
"""
import json
import pathlib
import sys

root = pathlib.Path(__file__).resolve().parent.parent
src = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else root / "Build" / "tuning.json"
schema = {p["key"]: p for p in json.loads((root / "docs" / "tuning_schema.json").read_text(encoding="utf-8"))}
vals = json.loads(src.read_text(encoding="utf-8-sig"))

diffs, unknown, out_of_range = [], [], []
for k, v in vals.items():
    p = schema.get(k)
    if p is None:
        unknown.append(k)
        continue
    if not isinstance(v, (int, float)):
        continue
    if abs(float(v) - float(p["default"])) > 1e-6:
        diffs.append((p["section"], k, p["default"], v, p["label"]))
    if v < p["min"] - 1e-6 or v > p["max"] + 1e-6:
        out_of_range.append((k, v, p["min"], p["max"]))

print(f"arquivo: {src}")
print(f"{len(diffs)} valor(es) diferente(s) do padrão:")
for sec, k, d, v, label in sorted(diffs):
    print(f"  [{sec}] {k}: {float(d):g} -> {float(v):g}   ({label})")
if unknown:
    print(f"{len(unknown)} chave(s) fora do esquema (ignorar ou foram renomeadas): {', '.join(unknown)}")
if out_of_range:
    print("fora do min/max (ampliar o [Param] se for de propósito):")
    for k, v, lo, hi in out_of_range:
        print(f"  {k} = {v:g} (min {lo:g}, max {hi:g})")
missing = [k for k in schema if k not in vals]
if missing:
    print(f"{len(missing)} chave(s) do esquema ausentes no arquivo (ficam no padrão).")
