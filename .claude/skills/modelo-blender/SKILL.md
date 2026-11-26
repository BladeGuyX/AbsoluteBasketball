---
name: modelo-blender
description: Criar ou alterar um modelo 3D do Absolute Basketball pelo pipeline Blender → .glb → Godot (bola, cesta, cenário, adereços, personagem). Use quando o usuário mandar uma concept art ou pedir um modelo/textura novo ou mudança em um modelo existente.
---

# Modelo pelo Blender (Etapa 7)

Leia antes `docs/ARTE.md` (medidas do jogo, esqueleto, modelos prontos). Regras do CLAUDE.md ("Regras de arte 3D") valem sempre.

## Regras
- **Todo modelo sai de um script** `art_source/scripts/make_<nome>.py` (reproduzível; nunca editar .blend à mão).
  Reaproveite `ab_common.py` (e `make_rei.py`/`make_ametista.py` para personagens).
- 1 unidade = 1 m, Z para cima no Blender, exporta glTF `.glb` com +Y Up para `assets/models/<nome>.glb`
  (texturas geradas: `assets/textures/`). Escala/rotação aplicadas. Origem no centro de massa (bola) ou nos pés (personagens).
- UV unwrap + materiais Principled BSDF simples com **nomes estáveis** — a Godot troca por toon (`Mat.ApplyToon`) pelo nome.
- Concept art (`art_source/concepts/`) é referência: ignore sombras/brilhos pintados, foque em forma e cor. O toon clareia: escolha cores
  uns tons mais escuras do que quer ver.
- Animações: preferir fazer na Godot (`CharacterVisual`, poses por osso). Se for no Blender: Actions separadas snake_case, `-loop`
  no fim dos loops, 30 fps, in place, Fake User, curvas Bézier com ease.

## Comandos
```
powershell -ExecutionPolicy Bypass -File tools\blender.ps1 art_source\scripts\make_<nome>.py
powershell -ExecutionPolicy Bypass -File tools\blender.ps1 art_source\scripts\render_preview.py assets\models\<nome>.glb [--no-ref] [--hide Obj1,Obj2]
python tools/inspect_glb.py assets/models/<nome>.glb
<godot_console> --headless --path . -- --inspect-model assets/models/<nome>.glb
```
O preview (4 vistas + escala) vai para `art_source/previews/` — **abra os PNGs com Read** e compare com a concept. Itere.

## Integração na Godot
- Rode `<godot_console> --headless --path . --import` (o build.ps1 também importa).
- Ligue no código (ex.: `Ball._Ready`, `HoopVisual`, `CourtBuilder.BuildDecor`), troque materiais por `Mat.ApplyToon(raiz, nome => espessura)`.
- Mantenha um fallback se o .glb faltar (como `PlayerRig`).
- Confira no jogo com a skill `prints` (`--screenshot`, `--court-shot`, `--pose-shot`).
- Registre em `docs/ARTE.md` → "Modelos prontos" (script, concept, integração, versão).

Personagem novo → use a skill `novo-personagem`.
