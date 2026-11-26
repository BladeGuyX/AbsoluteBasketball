---
name: aplicar-artifact
description: Quando o usuário diz "aplique o artifact" (ou "apliquei os valores", "usa o meu tuning.json") no Absolute Basketball — transforma os valores do Build\tuning.json salvo pelo artifact em padrões do Tuning.cs, regenera o artifact e SEMPRE publica release.
---

# Aplicar o artifact

O usuário ajusta valores no artifact de tuning e salva o `tuning.json` em `Build\tuning.json`.

1. **Esquema atual:** se o `Tuning.cs` mudou desde o último dump, regenere:
   `<godot_console> --headless --path . -- --dump-tuning-schema docs/tuning_schema.json`
2. **Diferenças:** `python tools/tuning_diff.py Build/tuning.json`
   Mostra só o que difere do padrão, chaves desconhecidas e valores fora do min/max.
3. **Gravar** cada valor diferente como padrão no campo correspondente de `Scripts/Core/Tuning.cs` (`public float X = valor;` com `f`).
   Valor fora do min/max: amplie o `[Param]` (foi de propósito). Chave desconhecida: provavelmente renomeada — procure no git log.
   Se um valor tem texto em `Characters.cs` com `{CampoDoTuning}`, ele já se atualiza sozinho.
4. Confira de novo: regenere o esquema e rode o diff → deve dar **0 valores diferentes**.
5. Regenere e publique o artifact (passo 5 da skill `entrega`), com a versão nova no rótulo.
6. Roteiro de testes: liste os valores que mudaram ("agora são o padrão: ...").
7. **Sempre release** (pedido do usuário): siga a skill `entrega` (versão, commit, `tools\release.ps1`).

Observação: `Build\tuning.json` NÃO vai no zip da release; o atualizador renomeia o do amigo para `tuning.old.json`, então os
padrões novos chegam para todos.
