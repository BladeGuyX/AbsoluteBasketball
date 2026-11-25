# Absolute Basketball — guia para o Claude (LEIA PRIMEIRO)

Jogo de basquete 3D, 3ª pessoa, multijogador online, **Godot 4.7.2 .NET (C#)**. Arte final virá do Blender.
O usuário fala português (pt-BR) — responda em pt-BR.

## Documentos
- `docs/GDD.md` — especificação completa do usuário (regras/mecânicas/controles) + decisões de implementação. **Fonte da verdade.**
- `docs/PLANO.md` — etapas e status. **Estado atual: v0.22 (rosto refeito, quique com a mão só no alto, ondas da enterrada por shader `impact_fx.gdshader`, efeito no roubo barrado pelo drible, cara brava ao sofrer ponto, som de cesta sofrida por tela). v0.21 (1º personagem, o Rei: `make_rei.py` → `rei.glb`; animações feitas na Godot em `CharacterVisual`; mãos flutuantes por código; roda de expressões B/Q; X = drible+roubo; enterrada com impacto + pendurar no aro). v0.20 (skill check simplificado: sem rastro do ponteiro, sem marquinhas nem anel que expande). v0.19 = skill check novo: resultado fica `SkillResultHold` = 0,75 s na tela com o ponteiro parado, certo e errado). v0.18 = quadra nova. Histórico: v0.13 (valores do artifact do usuário aplicados como padrão; passe rápido consertado) — Etapas 0 a 5 (online testado pelo usuário com um amigo; falta predição do jogador local) + correções v0.9. Etapa 6 feita (ícones, vibração, câmera, arremesso no pulo, enterrada no X). Em andamento: **Etapa 7 (arte)** — guia em `docs/ARTE.md`. **v0.14: bola pronta** (`art_source/scripts/make_ball.py` → `assets/models/ball.glb`, no jogo via `Ball._Ready` + `Mat.ApplyToon`; v0.16: na mão não gira — `HeldBallSpin` = 0; só gira em voo/solta). **v0.17: cesta pronta** (`make_hoop.py` → `hoop_azul.glb`/`hoop_laranja.glb`, no jogo via `Scripts/Game/HoopVisual.cs`; rede balança por shader e aro treme, gatilhos por `Sfx.Played` — som "swish"/"rim" e deixa muda "fx_dunk" no `FinishDunk`). Próximos modelos: outros personagens, cenário (o usuário manda as concepts).**
- `docs/ARTE.md` — **guia da Etapa 7** (fluxo Blender → .glb → Godot, convenções, medidas, animações, integração). Ler antes de modelar.
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
- **Publicação (v0.12):** repositório PÚBLICO https://github.com/BladeGuyX/AbsoluteBasketball (escolha do usuário). Os amigos instalam pelo .zip dos Releases e atualizam pelo botão "Atualizar" do menu. **A cada entrega:** subir `Scripts/Core/GameVersion.cs` (`Number`) e rodar `powershell -ExecutionPolicy Bypass -File tools\release.ps1 -Notes "..."` (builda, testa, zipa `Build\` sem tuning.json, faz push e cria a release com `gh`). Sem release nova, os amigos não recebem a mudança.
- **"Aplique o artifact" (v0.13):** o usuário salva o tuning.json do artifact em `Build\tuning.json`. Comparar com `docs/tuning_schema.json` (padrões), gravar os valores diferentes como padrão no `Tuning.cs`, regenerar o artifact e **sempre publicar release** (pedido do usuário).
- Revisões de regras (v0.2 a v0.9) estão em `docs/GDD.md` ("Revisões do usuário") e prevalecem sobre o texto original.

## Ferramentas instaladas
- Godot: `%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`
- Export templates 4.7.2.stable.mono em `%APPDATA%\Godot\export_templates\` · .NET SDK 8 · Python 3 · Node.

## Comandos
- Build completo (compila, importa, teste automático, exporta): `powershell -ExecutionPolicy Bypass -File tools\build.ps1` (`-SkipSmoke` pula o teste)
- Só compilar: `dotnet build`
- Teste automático de arremesso: `<godot_console> --headless --path . -- --smoke-test` (bandeja / meia distância / 3 pts).
- Teste automático da Etapa 2: `... -- --smoke-e2` (passe rápido + devolução, passe carregado, drible, pulo/toco do boneco, roubo falhando por imunidade e roubo certo).
- Teste automático da Etapa 3: `... -- --smoke-e3` (enterrada certa, errada + atordoado, enterrada andando com Espaço no skill check = erra, falta → 2 lances livres: acerta o 1º (+1), erra o 2º (bola viva)). `--e3-shot <png>` salva prints `_ready` (setinhas), `_dunk` e `_ft` (skill checks).
- Teste de rede (Etapa 5): host e cliente em dois processos por IP local — `<godot_console> --headless --path . -- --net-smoke-host` e, em paralelo, `... -- --net-smoke-client` (bots controlam os dois humanos; o cliente confere snapshots/movimento/placar, o host confere inputs e ações do remoto). `--net-shot <png>` no cliente salva prints aos 20 s e 40 s. O build.ps1 roda os dois.
- Steam: `... -- --steam-test` (só inicia e fecha a Steam; precisa da Steam aberta). Prints dos menus: `--screenshot <png> --online` / `--lobby`.
- Teste do bloqueio dos bots: `... -- --smoke-block` (16 arremessos com bot Normal marcando: tocos e quase nenhum pulo inútil; 10 dribles perto do bot: ele cai na finta ≥3 e nunca rouba driblando; solta o jogador em cima do bot: tem que escorregar).
- **Crash da v0.8 (0xc000001d = checagem fatal do motor, sem símbolos):** causa não identificada. Guardas `Player.SanitizeState` / `Ball.ServerTick` (NaN → reposiciona, loga `[SAFE]`), logs: `debug/file_logging/max_log_files=40` (pasta `%APPDATA%\Godot\app_userdata\Absolute Basketball\logs`; os testes também escrevem lá). Se o usuário mandar um log de crash, procurar `[SAFE]`/`CrashHandler`.
- v0.9: `Player.ResetForRound` (chamado no `PlaceTeams`), `Player.SlideOffPlayers` (pisou em jogador → escorrega), skill check errado termina na hora (v0.19: o anel NÃO some mais — `SkillFlash`/`FtFlash` = `SkillResultHold` nos dois casos, ponteiro parado em `DunkPressT`/`FtPressT`; o cliente mostra o próprio resultado na hora com `Match.ClientSkillHold/ClientSkillT/ClientSkillOk` e `ClientSkillMuteT` evita repetir o flash atrasado do host; desenho em `AimIndicator.DrawSkillCheck`; prints `_dunkres_ok/_dunkres_miss/_ftres_ok` no `--e3-shot`), lance livre errado arremessa e erra (1º: o 2º começa quando a bola toca o chão, em `HandleBallHit`), `BotBrain` cai no drible (`BotDribbleBaitChance`).
- Teste automático da Etapa 4: `... -- --smoke-e4` (3x3 só de bots, 150 s de jogo: pontos dos dois lados, arremessos, passes, trocas de posse; imprime estatísticas). v0.21/v0.22: sorteios da jogabilidade em `Match.Rng` com semente fixa no teste — o resultado é sempre o mesmo e efeitos visuais não o alteram. O build.ps1 roda os quatro.
- **ATENÇÃO:** rodando pela Godot (headless) o jogo lê `tuning.json` da RAIZ do projeto, que guarda valores antigos. O build.ps1 apaga esse arquivo antes dos testes; ao testar manualmente, apague-o também.
- Estatística de arremesso com erro humano simulado: `... -- --shot-stats` (use para calibrar assistências/aro/tabelinha).
- Prints da quadra (sem jogadores): `<godot_console> --path . -- --court-shot <png> [--fiba]` → `_top` (de cima) e `_34`.
- Prints para verificar visual: `<godot_console> --path . -- --screenshot <arquivo.png>` → salva `_0` parado quicando, `_1` andando, `_2` carregando, `_3` soltando. Menu: `... --screenshot <png> --menu`.
- Diagnóstico de janela: `<godot_console> --path . -- --video-test` (liga/desliga tela cheia e imprime o modo).
- Artifact de tuning:
  1. `<godot_console> --headless --path . -- --dump-tuning-schema docs/tuning_schema.json`
  2. `python tools/build_tuning_artifact.py "vX.Y · Etapa N"`
  3. Publicar `docs/tuning_editor.html` com a ferramenta Artifact usando `url` = URL acima (outro chat: ler antes com action read). **Só publicar quando um valor do Tuning mudar, e NUNCA abrir o artifact no navegador (pedido do usuário).**

- **Sem avisos de texto no meio do jogo** (tipo "ROUBOU!", "TOCO!", metros do arremesso). Só placar/ponto/prorrogação/fim.
  Não adicionar novos sem o usuário pedir. Feedback via indicativos discretos (`Scripts/UI/AimIndicator.cs`), animação e som.
- **Mouse:** o modo do cursor é definido SÓ em `GameRoot.UpdateMouse()` (preso jogando = `Match.WantsCapturedMouse`;
  visível em menus/pausa/fim). Nunca mudar `Input.MouseMode` com a janela sem foco. O cursor do Windows é transparente
  (`GameRoot.SetupCursor`) e o visível é o `SoftCursor` desenhado pelo jogo (o do sistema congelava/reaparecia em tela cheia).

## Arquitetura
- `Scenes/Main.tscn` → `Scripts/Core/GameRoot.cs`: inicializa settings/input/tuning, menus ↔ partida, F5 recarrega tuning, args de linha de comando.
- `Scripts/Core/`: `Tuning` (valores + esquema), `UserSettings` (user://settings.json: volume, fullscreen, sensibilidade, corrida alternar, binds), `InputSetup` (InputMap em código, remapeamento com troca automática de conflitos), `Mat` (materiais toon + contorno, paleta), `Sfx` (sons procedurais placeholder).
- `Scripts/Game/`:
  - `Match` — **autoridade** (host/offline). Fases Countdown→Playing→Scored→(FreeThrow)→(Ended). Lances livres: `StartFreeThrows`/`FreeThrowTick`/`FtResult`/`ThrowFreeThrow` (campos `Ft*` lidos pela HUD e pelos bots). Lê `PlayerInput` de cada jogador, chama `Player.ServerTick`, `Ball.ServerTick`, pontuação, posse, relógio, pickups.
  - `Player` (CharacterBody3D) — movimento, estamina, arremesso (segurar não muda nada; distância = câmera; auto em 4 s), passe (toque = aliado; carregado = lança na direção da câmera, 25% de velocidade, imune a roubo, auto em 4 s), salto (corpo vira para a câmera carregando), roubo, drible especial (agachado, quique rápido), quique automático (sempre).
  - Enterrada: `Player.CanDunk` (sem precisar correr; usado pela HUD), `Player.Flight` (None/Dunk), `FlightTick` (voo roteirizado + skill check SÓ com arremesso), `FinishDunk`, `EndFlight`, `Stun`. `Match.CheckFlightBlocks` (falta) e `OnFoul` (`_pendingFt` → lances livres).
  - `BotBrain` — bots (Etapa 4; v0.7: `BlockThreats`/`JumpCanReach` = bloqueio na trajetória, `PickRoam`/`Roam`/`Zone` = movimento sem a bola, `FreeThrowSkill`). `BlockScenario` = roteiro do `--smoke-block`. `Match._bots`; `MatchConfig.TrainingDummies` escolhe bots × bonecos; `BotDifficulty`. `Match.EventCount` conta eventos (estatística do `--smoke-e4`). `DunkScenario` = roteiro do `--smoke-e3`. `DummyBrain.GuardSpot` (boneco marca a frente da cesta).
  - Prévia do arremesso: `Player._shotPlan` é calculado no fim de cada tick e o arremesso usa exatamente esse plano.
  - `Passing` — passe rápido (aliado livre mais próximo, corredor livre, trajetória normal/alta) e `AimedVelocity` (passe carregado sem alvo).
  - `DummyBrain` — bonecos de treino (precursor dos bots). `ScenarioPilot` — roteiro do `--smoke-e2`.
  - Bola: estados Held/Loose/Shot/Pass/HighPass. `Match.CheckBlocks` (no ar bloqueia Shot/HighPass do adversário), `TryPickup` (recepção/interceptação), `LogEvent` (eventos impressos em modos automáticos).
  - `PlayerRig` (classe C# comum) — boneco placeholder + animação procedural (passada, balanço, IK simples dos braços até a bola, agachar na carga, pulinho ao soltar). Trocar pelo modelo do Blender na Etapa 7.
  - `Ball` + `BallPhys` — física própria (gravidade, quiques, aro em toro, tabela AABB, poste, paredes) e detecção de cesta. `ShotMath.Plan()` = arremesso (distância = `PitchDistance(câmera)` limitada por `ShotRangeMax`; assistências suaves só na cesta adversária; sem bandeja; ângulo mínimo de entrada). `BallPhys`: aro amigável e tabelinha (`CourtGeo.Bank*`) só valem com `assistHoop` (arremesso na cesta adversária); `UpThroughHoop` + `Ball._noScoreHoop` = cesta por baixo não vale — compartilhado por jogador, prévia, HUD e bots.
  - `CourtGeo` (geometria lógica: `Build(size)` pelo Tuning, `Create(comprimento, largura, raio3, aro, margem)` livre; `IsThree`) e `CourtBuilder` (ambiente + areia, instancia a cena da quadra, decoração; `BuildHoop`/`BuildWalls` públicos, chamados pelo `Court`).
  - **Quadra (v0.18):** `Scenes/Court.tscn` → `Scripts/Game/Court.cs` (`[Tool]`, `[Export]` Length/Width/ApronWidth, peças fixas ThreePointRadius/HoopInset/KeyWidth/KeyLength/FreeThrowCircleRadius/CenterCircleRadius/LineWidth, visual WoodTileSize/cores/CenterLogo/Hoops). Na partida `Match` → `CourtBuilder.Build` → `Court.Configure(Geo)` (comprimento, largura, raio de 3, aro, margem e linha de lance livre = `HoopInset + FreeThrowDistance` vêm do Tuning; o resto fica da cena). Piso = um plano com `Shaders/court_floor.gdshader`: linhas por SDF em metros no espaço local (metade espelhada por abs(x); o arco de 3 segue a mesma regra de `CourtGeo.IsThree`), apron fora da quadra, logo (anel + estrela), madeira `assets/textures/wood_floor.png` com UV = posição de mundo / `WoodTileSize`. Borda chanfrada (`SlabEdge`) desce até a areia (`Court.SlabDepth`). Nós gerados ficam em `_Generated` sem Owner (não vão para o .tscn). Print: `--court-shot <png>` (quadra média, `_top` e `_34`) e `--fiba` (valores da cena).
  - `CameraRig` (+ `LocalInputReader`), `TrajectoryPreview`, `AutoPilot` (piloto do teste; base para os bots).
- `Scripts/Net/` (Etapa 5): `NetSession` (nó filho do GameRoot, ANTES da partida na árvore: lobby, conexões, escalação, input dos clientes, snapshots, HUD/sons replicados), `NetProtocol` (mensagens `Msg`, `NetWriter/Reader`, `OnlineSettings`, `RosterEntry`, `RemoteInput` com contadores de aperto, `NetSnapshot`), `INetTransport` + `SteamTransport` (P2P Steam) + `EnetTransport` (IP direto, ENet só como cano de pacotes), `SteamService` (init/callbacks/reabrir para o overlay; resolve o `steam_api64.dll`), `SteamLobby` (lobby, convites, código, públicos).
  - `Match` online: `Cfg.Net` (Host/Client), `Cfg.Roster`, `SpawnRoster`, `_remote` (jogador → NetId), `OnRemoteLeft` (bot assume), `UiCountdown/UiMessage/UiEnd` (HUD também vai aos clientes). Cliente: `ClientPhysics` (manda input, interpola snapshots `InterpDelayTicks`), `ApplyClientState`, `ClientHud`. `Player.NetState`/`CaptureNet`/`ClientApply`, `Ball.ClientApply`, `Sfx.Broadcast`, `Tuning.ApplyJson`.
  - Steamworks.NET 2025.164.1 (oficial, rlabrecque) em `ThirdParty/Steamworks/` (referenciado no .csproj). O build.ps1 copia `steam_api64.dll` e `steam_appid.txt` (480) para `Build/`. Testes automáticos NÃO iniciam a Steam.
  - **Mudou o formato de alguma mensagem/snapshot? Suba `NetProtocol.Version`.** (v0.8.2 = versão 3)
  - **Skill check online (v0.8.2):** o cliente manda, no aperto, onde o ponteiro estava NA TELA dele (`PlayerInput.SkillT`) e o tick do host que está desenhando (`ClientRenderTick`). O host julga pelo `SkillT` e espera o atraso medido (`NetSession.SkillGrace` → `Player.SkillGrace`) antes de dar o skill check como perdido. Teste de rede força 3 lances livres para o cliente e roda com `--net-lag 60` (atraso artificial no `EnetTransport`).
  - O build.ps1 compila também a DLL de depuração (os testes headless usam ela — antes rodavam código velho se você não compilasse à mão).
  - NetId de cliente é SEMPRE positivo (NetId < 0 = bot na escalação). O `SteamTransport` mapeia cada handle da Steam (uint grande, virava negativo) para 1, 2, 3... — bug da v0.8: o amigo virava bot.
- Etapa 6: `Scripts/Core/InputDevice.cs` (último dispositivo usado, alimentado em `GameRoot._Input`; `Glyph(action)` = nome curto do botão atual), `Scripts/UI/HintBar.cs` (rodapé com teclinhas; redesenha ao trocar dispositivo ou `InputSetup.Revision`), `Scripts/Game/Rumble.cs` (vibração por transições de estado do jogador local — vale no cliente online; forças no Tuning "Vibração (controle)"), câmera: `UserSettings.InvertX/FovOffset/CameraDistanceMult/Vibration/VibrationStrength`, Tuning `StickLookExponent`/`ChargeLookMult`. Print com ícones do controle: `--screenshot <png> --pad`.
- v0.10: arremesso no pulo em `Player.ServerTick` (com bola + `ChargingShot` → `HandleJump`; no ar só `HandleShot`; pouso com `ChargingShot` → `ReleaseShot`). `HandleJump` pula sozinho quando a carga enche. Teste: passos 5 e 6 do `--smoke-e2` (`pulo_arremesso_pouso` / `pulo_arremesso_ar`).
- v0.13: `Passing.LaneClear` simula como a recepção real (recebedor andando até o ponto previsto; aliados não bloqueiam; adversário só se estiver mais perto da bola que o recebedor). `Player._passBuffer` guarda o aperto do passe (`PassBufferTime`). Teste: passo 7 do `--smoke-e2` (`passe_guardado`, `passe_aliado_andando`).
- v0.11: enterrada também pelo botão de roubo quando é separado do drible (`Player.WantsDunk`: X no controle; no teclado E = drible+roubo, então não conta). Pulo só ao soltar. E3 faz a 1ª enterrada pelo X.
- **Vários chats em paralelo:** ao terminar, juntar automaticamente as mudanças dos outros chats (git, docs, versão); avisar o usuário só se houver conflito.
- Etapa 7: `art_source/` (com `.gdignore`; `scripts/ab_common.py`, `make_rig_base.py`, `pipeline_test.py`, `render_preview.py`; `blend/rig_base.blend`; `concepts/`; `previews/` fora do git), `tools/blender.ps1` (roda script do Blender headless; Blender 5.3 em Program Files), `tools/inspect_glb.py`, `Scripts/Core/ArtCheck.cs` (`--inspect-model assets/models/x.glb`).
- **v0.21 — Rei:** `Scripts/Game/CharacterVisual.cs` (visual do .glb, poses por osso em camadas, mãos `MaoEsquerda/MaoDireita` soltas no Root, rostos `Rosto_*`, anel do time no chão; implementa `IPlayerVisual` junto com o `PlayerRig` placeholder), `Expressions.cs` (ids/nomes), `Scripts/UI/EmoteWheel.cs` (roda: `IsOpen`, `TakeChosen()` lido no `LocalInputReader`; `PlayerInput.Emote` → `Player.Emote/EmoteT`, replicado; no cliente vai por contador no `Msg.Input`), `ImpactFx.cs`/`FxNode.cs` (efeito da enterrada pela deixa `fx_dunk` + `CameraRig.Shake`; poeira ao pousar), som `slam`. `Player.RimHang` (pendurado no aro, `DunkHangTime`), `HeldBallPosition` na enterrada arma a bola atrás da cabeça. `RigState` ganhou VelY/PassHigh/Hanging/FlightK/RimPos/Defending/Mood/Emote. Controles: `Act.Emote` (Q / B), drible no X junto do roubo. `NetProtocol.Version` = 4. Prints: `--pose-shot <png> [--side]`. Teste de rede confere a expressão indo e voltando.
- **v0.22:** `Sfx.Play2D("score_t<time>")` vira "score" ou "score_bad" conforme `Sfx.LocalTeam` (setado no `Match._Ready`) — vale no host e no cliente. Deixa `fx_parry` (no `Match.OnStealFailed` quando o alvo está driblando) → `ImpactFx.Parry`. `FxNode` agora é lista de funções do tempo; `ImpactFx.Piece` desenha onda/brilho com `Shaders/impact_fx.gdshader`. `RigState.Angry` (Player conta pontos do adversário). Quique: `CharacterVisual` guarda o topo real de cada quique (`_apex` na virada) e a mão só acompanha `DribbleHandContact`. `--pose-shot` tem a página `_fx`. `--smoke-e4` reproduzível: jogabilidade sorteia em `Match.Rng` (semente fixa no teste); visual/som/partículas usam o sorteio global.
- Atualizador: `Scripts/Core/Updater.cs` (nó filho do GameRoot; consulta `api.github.com/repos/BladeGuyX/AbsoluteBasketball/releases/latest`, asset `AbsoluteBasketball-windows.zip`; instala só na build exportada via script PowerShell que espera o jogo fechar, extrai por cima, renomeia tuning.json → tuning.old.json e reabre). Versão: `GameVersion.Number`/`Label` (menu). Teste: `AbsoluteBasketball.exe -- --update-test` numa cópia com versão menor (a cópia reaberta roda `--print-version` e grava `version_check.txt`).
- **Lembrar o usuário em toda entrega:** a predição do próprio jogador no cliente online está pendente (não fazer até ele pedir).
- **Controle que não responde:** com a Steam iniciada (AppID 480 = Spacewar), o Steam Input do Spacewar captura o controle (o `localconfig.vdf` do usuário mostra `controller_config/480` em uso). Solução do lado do usuário: desativar o Steam Input do Spacewar. Diagnóstico: `InputDevice.PadReport()` (Configurações → Controles) e `[PAD]` no log.
- `Scripts/UI/`: `OnlineMenu` (Steam: criar só amigos/público, código, procurar; IP direto: hospedar/entrar), `LobbyMenu` (times, configurações do host, convidar amigos, começar/sair), `UiKit` (tema/widgets; use `UiKit.Anchor`/`FullRect` — nunca `Position` em controles ancorados), `Hud`, `MainMenu`, `SettingsMenu`, `PauseMenu`.
- Convenções: cesta 0 em −X, cesta 1 em +X; time 0 (AZUL) ataca a 1, time 1 (LARANJA) ataca a 0. Yaw: frente = (−sin, 0, −cos).
- **Uma classe Godot por arquivo, com o mesmo nome do arquivo** (requisito do C# da Godot).
- Physics interpolation ligada: ao teleportar nós, chamar `ResetPhysicsInterpolation()`.
- **Janela:** o projeto inicia em modo janela (`window/size/mode=0`) e a tela cheia é aplicada por código (`UserSettings.ApplyVideo`, modo Fullscreen sem bordas). NÃO voltar para mode=3 no project.godot: no Windows isso vira tela cheia exclusiva e não deixa sair (bug da v0.1) e trava o alt-tab.
- Perder o foco da janela pausa a partida (`Match.OnFocusLost`); `CameraRig.IgnoreMouseUntilMs` evita pulo da câmera ao voltar.
- Sons são sintetizados em `Sfx.cs` (dribble, bounce, rim, board, swish, score, win, horn, beep, go, catch).

## Arquivos de dados
- `tuning.json`: no editor fica na raiz do projeto; na build fica **ao lado do .exe** (criado automaticamente; campos novos são acrescentados mantendo os valores do usuário). Não vai para o .pck.
- Settings do jogador: `%APPDATA%\Godot\app_userdata\Absolute Basketball\settings.json`.

## Próximos passos (resto da Etapa 5)
1. Esperar o teste real do usuário entre 2 PCs pela Steam (roteiro v0.8 em `docs/TESTES.md`) e corrigir o que aparecer.
2. Predição do jogador local no cliente (hoje o boneco do cliente responde com o atraso da rede): simular só o movimento
   localmente e corrigir suavemente pela posição do host.
3. Compressão dos snapshots (posições em 16 bits, só o que mudou) se a banda pesar; reconexão de quem caiu.

## Regras de arte 3D
- Escala: 1 unidade do Blender = 1 metro.
- Eixo: modele com Z para cima; exporte em glTF (.glb) com "+Y Up" ativado.
- Origem do objeto no centro de massa (bola) ou nos pés (personagens).
- Aplique escala/rotação (Ctrl+A) antes de exportar.
- Todo modelo deve ter UV unwrap e materiais Principled BSDF simples.
- Gere cada modelo por um script em art_source/scripts/ para ser reproduzível.
- Exporte para assets/models/<nome>.glb.
- As concept arts são apenas referências, então tente ignorar sombras e brilhos pintados e focar na forma e nas cores.

## Campo
- Dimensões padrão FIBA: 28 × 15 m, ajustáveis via [Export] na cena Court.
- Marcações por shader, em metros. Nunca escalar o nó do campo — alterar os parâmetros.
- Peças de tamanho fixo: garrafão, círculos, arco de 3 pontos. Só retas e piso esticam.
- Cestas posicionadas automaticamente a 1.575 m da linha de fundo.

## Regras de animação
- Tente criar as animações na própria Godot quando possível.
- Todos os personagens usam o mesmo esqueleto (rig_base.blend).
- 30 fps. Animações "in place" (sem root motion), personagem não sai do lugar.
- Cada animação é uma Action separada, nomeada em snake_case: idle, run, jump, shoot.
- Animações em loop terminam com "-loop" no nome (ex: run-loop); a Godot ativa o loop automaticamente.
- Primeiro e último frame de loops devem ser idênticos.
- Exportar .glb com "Animation" ativado, modo Actions, sem "Always Sample Animations" desnecessário.
- Use Actions com Fake User para não sumirem ao salvar.
- Tente usar curvas Bézier com ease em vez de interpolação linear, para não ficar robótico.

## (histórico) Etapa 2 — já feita
Passe (toque = aliado livre mais próximo; segurar = aliado na direção da câmera com marcador; variação = passe alto; checagem de linha livre; interceptação por qualquer jogador no caminho), salto (carga, bloqueio de arremessos subindo e passes altos → bola quica e cai; 0,5 s de trava ao pousar), roubo (impulso, falha = 2 s a 20% sem correr/pular/roubar; imune: driblando, no ar, carregando arremesso), drible (2 cargas, 1 s, 50% mais lento, recarga 10 s contínua). Adicionar estados ao `Ball` (Pass/HighPass) e flags no `Player`. Para testar sem bots, considerar bonecos parados (dummies) no time adversário.