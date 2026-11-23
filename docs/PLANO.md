# Plano de desenvolvimento — Absolute Basketball

Cada etapa termina com: build em `Build\AbsoluteBasketball.exe`, teste automático passando,
artifact de tuning atualizado, `CLAUDE.md` atualizado e roteiro de testes em `docs/TESTES.md`.

| # | Etapa | Status |
|---|---|---|
| 0 | Fundação: dependências, projeto C#, pipeline de build/export, tuning.json + artifact, docs | ✅ concluída |
| 1 | Núcleo jogável (v0.1, revisado na v0.2: escala, novo arremesso, animações, sons, tela cheia): quadra (3 tamanhos), jogador 3ª pessoa, câmera, corrida/estamina, quique automático, pegar bola, arremesso (carga, trajetória, auto em 3 s, bandeja, alcance pela câmera), cestas 2/3, rounds + contagem 3 s, vitória por pontos/tempo, menus, pausa, configurações (volume, fullscreen, sensibilidade, corrida alternar, remapeamento) | ✅ concluída |
| 2 | Mecânicas completas: passe (rápido / mirado / alto, marcador no alvo, interceptação), salto (carregado, bloqueio, trava de 0,5 s), roubo (impulso, penalidade), drible (cargas, imunidade) + bonecos de treino | ✅ concluída (v0.3) |
| 3 | ⏭ **próxima** — Enterrada: condição de corrida + 3 m, salto roteirizado, skill check, erro com atordoamento, falta por bloqueio | |
| 4 | Bots: ataque (posicionar, arremessar com erro calibrado, passar, driblar, enterrar), defesa (marcação, roubo, salto para bloquear), rebote; níveis de dificuldade; composição de times no menu | |
| 5 | Multiplayer online **via Steam** (Steamworks, AppID 480 de teste — sem publicar): lobby Steam, convite de amigos, P2P; lobby com escolha de time, tamanho da quadra e regras, sincronização (snapshots + interpolação, predição do jogador local), tuning do host enviado aos clientes | |
| 6 | Refinos de controle: ícones de botões trocando entre teclado/controle, vibração, ajustes de câmera | |
| 7 | Arte: personagens/quadra no Blender (pipeline glTF → `res://Art/`), animações, efeitos, UI final | |
| 8 | Áudio e polimento: música, sons reais, partículas, replays de cesta, menus finais | |

## Arquitetura (resumo)
- `GameRoot` (cena `Main.tscn`) → menus ou `Match`.
- `Match` = autoridade: lê `PlayerInput` de cada jogador → `Player.ServerTick` → `Ball.ServerTick` (física própria em `BallPhys`) → regras.
- Bots e clientes remotos só precisam produzir `PlayerInput` (o `AutoPilot` já faz isso para o teste automático).
- Valores de gameplay em `Tuning` (C#, com `[Param]`) ⇄ `tuning.json` ⇄ artifact.
