# Absolute Basketball — guia para o Claude (LEIA PRIMEIRO)

Jogo de basquete 3D, 3ª pessoa, multijogador online, **Godot 4.7.2 .NET (C#)**. Arte final virá do Blender.
O usuário fala português (pt-BR) — responda em pt-BR.

## Documentos
- `docs/GDD.md` — especificação completa do usuário (regras/mecânicas/controles) + decisões de implementação. **Fonte da verdade.**
- `docs/PLANO.md` — etapas e status. **Estado atual: v0.3 — Etapas 0, 1 e 2 concluídas. Próxima: Etapa 3 (enterrada).**
- `docs/TESTES.md` — roteiros de teste manual por versão.
- Artifact de tuning (editor de valores): https://claude.ai/artifact/FjmhXo9dKiHoCRZk5zZLFo

## Regras de trabalho (pedidas pelo usuário — obrigatórias)
1. **Sempre gerar build jogável** (`Build\AbsoluteBasketball.exe`) após mudanças. O usuário não testa pela Godot.
2. **Sempre entregar um roteiro de testes** após cada mudança (e adicioná-lo ao topo de `docs/TESTES.md`).
3. **Sempre atualizar o artifact de tuning** quando um valor ajustável for criado/alterado (passos abaixo), publicando no MESMO URL.
4. **Sempre atualizar este arquivo e `docs/PLANO.md`** com o estado atual, para outro chat poder continuar.
5. Todo valor de gameplay novo vai em `Scripts/Core/Tuning.cs` com `[Param(seção, rótulo, descrição, min, max, passo)]` — nunca hardcode.

## Decisões do usuário a lembrar
- **Multiplayer pela Steam** (Steamworks, sem publicar: AppID de teste 480/Spacewar, como já foi feito em outro jogo do usuário). Só na Etapa 5.
- Controle de versão: **git** neste diretório (commits só quando o usuário pedir ou ao fechar uma entrega combinada).
- Revisões de regras (v0.2, v0.3) estão em `docs/GDD.md` ("Revisões do usuário") e prevalecem sobre o texto original.

## Ferramentas instaladas
- Godot: `%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`
- Export templates 4.7.2.stable.mono em `%APPDATA%\Godot\export_templates\` · .NET SDK 8 · Python 3 · Node.

## Comandos
- Build completo (compila, importa, teste automático, exporta): `powershell -ExecutionPolicy Bypass -File tools\build.ps1` (`-SkipSmoke` pula o teste)
- Só compilar: `dotnet build`
- Teste automático de arremesso: `<godot_console> --headless --path . -- --smoke-test` (bandeja / meia distância / 3 pts).
- Teste automático da Etapa 2: `... -- --smoke-e2` (passe + devolução, drible, pulo/toco do boneco, roubo falhando por imunidade e roubo certo). O build.ps1 roda os dois.
- Estatística de arremesso com erro humano simulado: `... -- --shot-stats` (use para calibrar assistências/aro/tabelinha).
- Prints para verificar visual: `<godot_console> --path . -- --screenshot <arquivo.png>` → salva `_0` parado quicando, `_1` andando, `_2` carregando, `_3` soltando. Menu: `... --screenshot <png> --menu`.
- Diagnóstico de janela: `<godot_console> --path . -- --video-test` (liga/desliga tela cheia e imprime o modo).
- Artifact de tuning:
  1. `<godot_console> --headless --path . -- --dump-tuning-schema docs/tuning_schema.json`
  2. `python tools/build_tuning_artifact.py "vX.Y · Etapa N"`
  3. Publicar `docs/tuning_editor.html` com a ferramenta Artifact usando `url` = URL acima (outro chat: ler antes com action read).

## Arquitetura
- `Scenes/Main.tscn` → `Scripts/Core/GameRoot.cs`: inicializa settings/input/tuning, menus ↔ partida, F5 recarrega tuning, args de linha de comando.
- `Scripts/Core/`: `Tuning` (valores + esquema), `UserSettings` (user://settings.json: volume, fullscreen, sensibilidade, corrida alternar, binds), `InputSetup` (InputMap em código, remapeamento com troca automática de conflitos), `Mat` (materiais toon + contorno, paleta), `Sfx` (sons procedurais placeholder).
- `Scripts/Game/`:
  - `Match` — **autoridade** (host/offline). Fases Countdown→Playing→Scored→(Ended). Lê `PlayerInput` de cada jogador, chama `Player.ServerTick`, `Ball.ServerTick`, pontuação, posse, relógio, pickups.
  - `Player` (CharacterBody3D) — movimento, estamina, arremesso (sem cancelar; auto em 3 s), passe, salto, roubo, drible especial, quique automático (sempre).
  - `Passing` — alvos de passe (toque = mais próximo livre; segurando = na direção da câmera), corredor livre, trajetória normal/alta.
  - `DummyBrain` — bonecos de treino (precursor dos bots). `ScenarioPilot` — roteiro do `--smoke-e2`.
  - Bola: estados Held/Loose/Shot/Pass/HighPass. `Match.CheckBlocks` (no ar bloqueia Shot/HighPass do adversário), `TryPickup` (recepção/interceptação), `LogEvent` (eventos impressos em modos automáticos).
  - `PlayerRig` (classe C# comum) — boneco placeholder + animação procedural (passada, balanço, IK simples dos braços até a bola, agachar na carga, pulinho ao soltar). Trocar pelo modelo do Blender na Etapa 7.
  - `Ball` + `BallPhys` — física própria (gravidade, quiques, aro em toro, tabela AABB, poste, paredes) e detecção de cesta. `ShotMath.Plan()` = arremesso (distância = `PitchDistance(câmera)` limitada por `MaxRange(carga)`; assistências suaves só na cesta adversária; bandeja dentro de `CloseShotRadius`; ângulo mínimo de entrada). `BallPhys`: aro amigável e tabelinha (`CourtGeo.Bank*`) — compartilhado por jogador, prévia, HUD e bots.
  - `CourtGeo` (geometria lógica, 3 tamanhos, `IsThree`) e `CourtBuilder` (visual + colisores + ambiente).
  - `CameraRig` (+ `LocalInputReader`), `TrajectoryPreview`, `AutoPilot` (piloto do teste; base para os bots).
- `Scripts/UI/`: `UiKit` (tema/widgets; use `UiKit.Anchor`/`FullRect` — nunca `Position` em controles ancorados), `Hud`, `MainMenu`, `SettingsMenu`, `PauseMenu`.
- Convenções: cesta 0 em −X, cesta 1 em +X; time 0 (AZUL) ataca a 1, time 1 (LARANJA) ataca a 0. Yaw: frente = (−sin, 0, −cos).
- **Uma classe Godot por arquivo, com o mesmo nome do arquivo** (requisito do C# da Godot).
- Physics interpolation ligada: ao teleportar nós, chamar `ResetPhysicsInterpolation()`.
- **Janela:** o projeto inicia em modo janela (`window/size/mode=0`) e a tela cheia é aplicada por código (`UserSettings.ApplyVideo`, modo Fullscreen sem bordas). NÃO voltar para mode=3 no project.godot: no Windows isso vira tela cheia exclusiva e não deixa sair (bug da v0.1) e trava o alt-tab.
- Perder o foco da janela pausa a partida (`Match.OnFocusLost`); `CameraRig.IgnoreMouseUntilMs` evita pulo da câmera ao voltar.
- Sons são sintetizados em `Sfx.cs` (dribble, bounce, rim, board, swish, score, win, horn, beep, go, catch).

## Arquivos de dados
- `tuning.json`: no editor fica na raiz do projeto; na build fica **ao lado do .exe** (criado automaticamente; campos novos são acrescentados mantendo os valores do usuário). Não vai para o .pck.
- Settings do jogador: `%APPDATA%\Godot\app_userdata\Absolute Basketball\settings.json`.

## Próximos passos (Etapa 3 — enterrada)
Com a bola, correndo em direção à cesta adversária e a menos de 3 m: salto vira enterrada (voo roteirizado até o aro).
Skill check estilo Dead by Daylight (mais difícil) com o botão de arremesso no momento de tocar a cesta. Erro: bola cai ao lado,
jogador atordoado 2 s. Bloqueio de enterrada por salto = falta: round reinicia com o jogador que sofreu a falta com a bola
(`StartRound(time, carrier, countdown:false)`). Depois: Etapa 4 (bots, evoluindo `DummyBrain`/`AutoPilot`).

## (histórico) Etapa 2 — já feita
Passe (toque = aliado livre mais próximo; segurar = aliado na direção da câmera com marcador; variação = passe alto; checagem de linha livre; interceptação por qualquer jogador no caminho), salto (carga, bloqueio de arremessos subindo e passes altos → bola quica e cai; 0,5 s de trava ao pousar), roubo (impulso, falha = 2 s a 20% sem correr/pular/roubar; imune: driblando, no ar, carregando arremesso), drible (2 cargas, 1 s, 50% mais lento, recarga 10 s contínua). Adicionar estados ao `Ball` (Pass/HighPass) e flags no `Player`. Para testar sem bots, considerar bonecos parados (dummies) no time adversário.
