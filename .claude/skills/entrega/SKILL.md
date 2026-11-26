---
name: entrega
description: Fecha uma mudança no Absolute Basketball do jeito que o usuário exige — build jogável, testes automáticos, roteiro de testes no topo do docs/TESTES.md, CLAUDE.md e docs/PLANO.md atualizados, artifact de tuning (só se um valor mudou), versão + release no GitHub. Use ao terminar qualquer mudança de código/arte/valores, ou quando o usuário disser "entregue", "publique", "faça a release", "gere a build".
---

# Entrega (checklist obrigatório)

Regras do usuário (CLAUDE.md, "Regras de trabalho"). Responda em pt-BR. Siga na ordem:

## 1. Juntar outros chats
Vários chats trabalham em paralelo. Antes de fechar: `git status` + `git log --oneline -5`. Se houver mudanças que não são suas
(outros chats), integre (docs, versão, código) sem perguntar; só avise o usuário se houver conflito real.

## 2. Valores
Todo valor de gameplay novo está em `Scripts/Core/Tuning.cs` com `[Param(...)]` — nada hardcoded (ver skill `tuning-param`).

## 3. Build + testes
```
powershell -ExecutionPolicy Bypass -File tools\build.ps1
```
Compila Release e Debug, importa, roda todos os smoke tests (arremesso, E2, E3, bloqueio, E4, habilidades, rede host+cliente) e exporta
`Build\AbsoluteBasketball.exe`. Demora alguns minutos — rode com timeout alto (600000) ou em segundo plano.
Se um teste falhar: leia a saída, conserte e rode de novo. **Nunca diga que passou sem ver "OK -> ...Build\AbsoluteBasketball.exe".**
Teste novo para a mudança? Acrescente ao roteiro do smoke correspondente (`ScenarioPilot`, `DunkScenario`, `AbilityScenario`, `BlockScenario`).

## 4. Conferir visualmente (se mudou algo que aparece na tela)
Use a skill `prints` e olhe os PNGs com Read antes de entregar.

## 5. Artifact de tuning — SÓ se algum `[Param]` foi criado/alterado
```
<godot_console> --headless --path . -- --dump-tuning-schema docs/tuning_schema.json
python tools/build_tuning_artifact.py "vX.Y · Etapa N"
```
Publicar `docs/tuning_editor.html` com a ferramenta Artifact no MESMO url (`https://claude.ai/artifact/FjmhXo9dKiHoCRZk5zZLFo`).
Outro chat: `action: read` nesse url antes de publicar. **Nunca usar `action: open`** (pedido do usuário).

## 6. Documentos
- `docs/TESTES.md`: nova seção **no topo** (`## vX.Y — resumo`), passos numerados, curtos, do ponto de vista de quem joga o .exe.
- `docs/PLANO.md`: linha `| — | **vX.Y:** ... | ✅ |` na tabela.
- `CLAUDE.md`: atualizar "Estado atual" (primeira linha de Documentos) e a seção de arquitetura com classes/campos/args novos,
  para outro chat conseguir continuar.
- Mudou mensagem/snapshot de rede? Subiu `NetProtocol.Version`? (ver skill `mudanca-rede`)

## 7. Versão + release (os amigos só recebem assim)
1. Subir `Number` em `Scripts/Core/GameVersion.cs` (ex.: "0.29" → "0.30").
2. Commit: `git add -A && git commit` com mensagem `vX.Y: resumo em pt-BR` + linha `Co-Authored-By` pedida pelo sistema.
3. `powershell -ExecutionPolicy Bypass -File tools\release.ps1 -Notes "o que mudou (pt-BR, curto)"`
   (builda de novo, zipa `Build\` sem tuning.json, faz push, cria a release com `gh`). Use `-SkipBuild` só se a build do passo 3
   for deste mesmo código.
Commit/release só quando o usuário pediu ou é o fechamento de uma entrega combinada.

## 8. Resposta final ao usuário
- O que mudou (curto) + roteiro de testes (o mesmo do TESTES.md).
- Link da release se publicou.
- **Sempre lembrar:** a predição do próprio jogador no cliente online continua pendente (não fazer até ele pedir).
