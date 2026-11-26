---
name: diagnostico-log
description: Investigar crash, travamento ou comportamento estranho do Absolute Basketball pelos logs da Godot (os do usuário ou de um amigo). Use quando o usuário disser que o jogo fechou/crashou/travou, mandar um arquivo .log, ou relatar um bug que não dá para reproduzir.
---

# Diagnóstico por log

## Onde estão
`%APPDATA%\Godot\app_userdata\Absolute Basketball\logs\godot<data>.log` (até 40 arquivos; os testes automáticos também escrevem lá —
olhe a data/hora para achar a sessão do usuário). Settings do jogador: `...\Absolute Basketball\settings.json`.
Listar os mais recentes: `ls -t "$APPDATA/Godot/app_userdata/Absolute Basketball/logs" | head`

## O que procurar
- `[SAFE]` — guardas de NaN (`Player.SanitizeState`, `Ball.ServerTick`) reposicionaram algo: a causa está no que aconteceu antes.
- `CrashHandler`, `ERROR`, `SCRIPT ERROR`, `Unhandled exception`, `NullReferenceException` + pilha C#.
- `0xc000001d` / exit 132 = checagem fatal do motor (crash da v0.8, causa desconhecida). Exit 132 no desligamento = alguém fechou
  sem `GameRoot.QuitClean` (sempre usar ele, nunca `GetTree().Quit` direto).
- `[PAD]` — diagnóstico do controle (Steam Input do Spacewar capturando = desativar na Steam).
- Linhas de rede (`NetSession`, versão de protocolo, desconexão) para bugs online.
- Versão do jogo no começo do log: confirme que é a release atual (`Scripts/Core/GameVersion.cs`).

## Reproduzir
- Monte o caso num teste automático (`ScenarioPilot`/`DunkScenario`/`AbilityScenario`/`BlockScenario`) ou use o modo de teste
  (trapaças) e os prints (skill `prints`).
- `--smoke-e4` é reproduzível (semente fixa em `Match.Rng`) — bom para bugs de partida só de bots.
- Ao rodar pela Godot, apague o `tuning.json` da raiz (valores antigos) — ou copie o `Build\tuning.json` do usuário se o bug depender dos valores dele.

Depois de consertar: skill `entrega`.
