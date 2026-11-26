---
name: mudanca-rede
description: Checklist para mexer no multiplayer do Absolute Basketball (NetSession, NetProtocol, snapshots, inputs, Steam/ENet, estado replicado de jogador/bola/HUD/sons) sem quebrar o online. Use quando uma mudança cria estado novo que o cliente precisa ver, uma mensagem nova, ou quando o usuário relata bug jogando online com amigo.
---

# Mudança na rede

Arquitetura: host autoritativo (`Match` em `Cfg.Net = Host`), cliente só manda `PlayerInput` e interpola `NetSnapshot`.
Código em `Scripts/Net/` + `Player.NetState/CaptureNet/ClientApply`, `Ball.ClientApply`, `Match.ApplyClientState/ClientHud`.

## Ao adicionar estado
1. Estado do jogador → `Player.NetState` (capturar no host, aplicar no cliente). Bola → `Ball`. Habilidades → `PlayerAbilities.NetData`.
   HUD/avisos → `Match.UiCountdown/UiMessage/UiEnd/UiSub`. Som → `Sfx.Broadcast` (ou `Sfx.PlayLocal3D` / `Sfx.PlayUi` que NÃO vão para a rede).
2. Botão novo do jogador → `PlayerInput`; apertos rápidos vão por **contadores** (`RemoteInput`), não por bool.
3. Mensagem nova → `Msg` em `NetProtocol`, `NetWriter/NetReader` dos dois lados.
4. **Mudou formato de mensagem/snapshot? Suba `NetProtocol.Version`** (hoje 7) e anote no CLAUDE.md.
5. NetId de cliente é sempre positivo; NetId < 0 = bot.
6. Skill check online: cliente manda `SkillT` (onde o ponteiro estava na tela dele) + `ClientRenderTick`; host espera `SkillGrace`.
7. Sorteios de jogabilidade só em `Match.Rng` (o teste E4 depende disso); efeitos visuais usam o sorteio global.

## Testar
```
# dois processos em paralelo (o build.ps1 faz isso):
<godot_console> --headless --path . -- --net-smoke-host --net-lag 60
<godot_console> --headless --path . -- --net-smoke-client --net-lag 60 [--net-shot x.png]
```
Saídas `[NETTEST-HOST]` / `[NETTEST-CLIENT]`; ambos devem sair com código 0. Acrescente verificação do estado novo ao teste
(o cliente confere snapshots/movimento/placar/expressão; o host confere inputs e ações do remoto).
Steam: `--steam-test` (precisa da Steam aberta; os testes automáticos não iniciam a Steam). Prints do lobby: `--screenshot x.png --lobby`.

## Bugs relatados online
- Peça o log: `%APPDATA%\Godot\app_userdata\Absolute Basketball\logs` (skill `diagnostico-log`).
- Os dois precisam estar na mesma versão (botão Atualizar); `NetProtocol.Version` diferente recusa a conexão.
- Controle não responde com a Steam aberta = Steam Input do Spacewar (480) capturando — usuário desativa na Steam.
- **Não implementar a predição do jogador local** até o usuário pedir (mas lembrá-lo dela).
