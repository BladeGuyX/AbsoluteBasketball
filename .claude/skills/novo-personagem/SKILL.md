---
name: novo-personagem
description: Adicionar um personagem jogável novo ao Absolute Basketball de ponta a ponta — modelo pelo Blender, registro em Characters.cs, estrelas e habilidades no Tuning, lógica em PlayerAbilities, bots, ícones, aba de informações, rede e testes. Use quando o usuário mandar a concept/descrição de um personagem novo ou pedir um 4º personagem.
---

# Novo personagem

Personagens atuais: 0 King (`rei.glb`), 1 Luck (`ametista.glb`), 2 Bolt (`raio.glb`), 3 Iron (`iron.glb`, v0.37 — bom exemplo completo: habilidade que mexe na física da bola, movimento próprio, imunidade e cena com adereço). 4 One (`one.glb`, v0.41 — estado visto diferente por tela: `RealmView`; jogador "fora de fase" `Player.Phased`; cena da ult com shader em vez de adereço). Leia `docs/ARTE.md` ("Novos personagens")
e a seção v0.26 do `docs/GDD.md`. Pergunte ao usuário o que faltar das 3 habilidades + ultimate (é decisão dele).

## 1. Modelo (skill `modelo-blender`)
- `art_source/scripts/make_<nome>.py` importando `make_rei as rei` (cabeça `make_head`, rostos `Face`/`eye`/`brow`, `skin`, pesos,
  `make_legs`, `rounded_box`, `tube`); paleta em `rei.M` ou dicionário `PAL` (como o `make_raio.py`).
- Objetos com **os mesmos nomes**: `Corpo`, `Cabeca`, `Coroa` (opcional), `Rosto_Serio/Focado/SorrisoFrio/Bravo/Feliz/Surpreso`,
  `MaoEsquerda`/`MaoDireita` sem osso. Mesmo esqueleto (`rig_base`). Saída `assets/models/<nome>.glb`.
- Material novo que precisa de contorno/sem sombra: incluir no `switch` de `CharacterVisual.ApplyMaterials`.

## 2. Código
- `Scripts/Game/Characters.cs`: entrada nova em `All` (nome, .glb, cor, rótulos da roda de expressões, `AbilityDef[4]` com
  nome/ícone/texto usando `{CampoDoTuning}`), `Stars`, `Cooldown`, `NeedsTarget`.
- `Scripts/Core/Tuning.cs`: seção do personagem com `<Nome>Star*` (`Sub = Star`) e valores das habilidades (`Sub = Hab`) — skill `tuning-param`.
- `Scripts/Game/PlayerAbilities.cs`: efeitos de cada habilidade + ultimate; estado novo que o cliente precisa ver vai no `NetData`.
  **Ultimate que NÃO é cutscene (pedido do usuário, v0.35):** ao ativar (só a 1ª vez, não nas repetições) chame `M.StartUltPose(P)` —
  cena curta com o título da ult na cor do personagem, jogo congelado (`CutKind.UltPose`, `Tuning.UltPoseDuration`).
  **A cena tem que ter a ver com a ultimate (v0.36, pedido do usuário) — NÃO só uma comemoração.** Faça uma função própria
  em `Cutscene.PoseFrame` (exemplo: `LuckPose` = joga dois dados, `dado.glb`, que caem 6 e 6), com adereço do Blender se precisar.
  Sem a bola na cena. Confira com `--cut-shot <png> --luck` (adapte o print para o personagem novo).
- `Scripts/Game/BotBrain.cs`: `AbilitiesWithBall` / `AbilitiesOffBall` (e `CutsceneSkill` se a ult tiver cutscene).
- Visual: ícones em `AbilityIcons`, efeitos `AbilityAura`/`ImpactFx.Ability` (deixas `fx_*`), sons no `Sfx`.
- UI: aba nova no `InfoMenu`; conferir `CharacterSelect`, `CheatsMenu` (trocar personagem) e `Match.RefreshNames`.
- `Characters.ForSlot` (bots alternam por vaga) — conferir se inclui o novo.
- Imunidade da ult do Iron: toda habilidade nova que afeta um adversário precisa respeitar `Characters.Immune(alvo, quem)`.
- `PlayerAbilities.Reset` do `AbilityScenario` zera o estado das habilidades: acrescente os campos novos lá.
- Rede: se mudou snapshot/mensagem, subir `NetProtocol.Version` (skill `mudanca-rede`).

## 3. Testes
- `--smoke-abilities` (`AbilityScenario`): acrescentar verificações de cada habilidade nova (e da ult acertando/errando).
- Prints: `--pose-shot x.png --char N`, `--screenshot x.png --charsel`, `--cut-shot` se tiver cutscene. Olhe os PNGs.
- `tools\build.ps1` completo.

## 4. Docs
`docs/ARTE.md` (modelo pronto + notas do personagem), `docs/GDD.md` (habilidades), CLAUDE.md (lista de personagens), e a skill `entrega`.
