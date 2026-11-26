---
name: prints
description: Tirar prints do Absolute Basketball pela linha de comando para conferir o visual sem o usuário (menus, HUD, personagens/poses, quadra, cutscenes, skill checks, efeitos, trajetória, partida online). Use depois de mudar qualquer coisa que aparece na tela, antes de entregar.
---

# Prints automáticos

Rodar da raiz do projeto, **sem `--headless`** (precisa renderizar). Salve em `art_source/previews/` (fora do git) ou no scratchpad.
Antes: `dotnet build` (os testes pela Godot usam a DLL de depuração) e apague o `tuning.json` da raiz se quiser os padrões do código.
Depois: **abra os PNGs com Read e olhe de verdade** — descreva o que viu ao usuário só se for relevante.

`G` = `%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`

| O que conferir | Comando (`& $G --path . -- ...`) | Arquivos gerados |
|---|---|---|
| Jogador quicando/andando/carregando/soltando | `--screenshot x.png` | `x_0`..`x_3` |
| Ícones do controle em vez do teclado | acrescente `--pad` | |
| Menu principal / online / lobby / escolha de personagem / informações / jogar local | `--screenshot x.png --menu` · `--online` · `--lobby` · `--charsel` · `--info` · `--setup` | |
| Trapaças (modo de teste) / menu de alvo | `--screenshot x.png --cheats` · `--picker` (`--one` = 1 adversário) | |
| Linha da trajetória | `--screenshot x.png --traj` | |
| Poses e rostos do personagem | `--pose-shot x.png [--side] [--char 0/1/2]` | `_a`..`_e`, `_faces`, `_fx` |
| Quadra (sem jogadores) | `--court-shot x.png [--fiba]` | `_top`, `_34` |
| Cutscene da ultimate | `--cut-shot x.png` (King) · `--duel` (Bolt) · `--miss` (erra) | um a cada 0,3 s |
| Setinhas/enterrada/lance livre/resultado do skill check | `--smoke-e3 --e3-shot x.png` | `_ready`, `_dunk`, `_ft`, `_dunkres_ok/_miss`, `_ftres_ok` |
| Tela do cliente online | host `--headless ... --net-smoke-host` + cliente `--net-smoke-client --net-shot x.png` | aos 20 s e 40 s |

Personagens: 0 = King (rei.glb), 1 = Luck (ametista.glb), 2 = Bolt (raio.glb).

Novo modo de print? Os argumentos ficam em `Scripts/Core/GameRoot.cs` (procure `--screenshot`); lembre de incluir o argumento na
lista `automated` e documentar no CLAUDE.md.

Modelos no Blender (sem Godot): `powershell -ExecutionPolicy Bypass -File tools\blender.ps1 art_source\scripts\render_preview.py assets\models\<x>.glb --no-ref`.
