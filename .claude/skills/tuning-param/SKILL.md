---
name: tuning-param
description: Como criar ou mudar um valor ajustável de gameplay no Absolute Basketball (Scripts/Core/Tuning.cs com [Param]) e manter o artifact de tuning em dia. Use sempre que for introduzir um número de gameplay (velocidade, tempo, distância, chance, recarga, força de efeito) — nunca hardcode.
---

# Valor de tuning novo

## Onde
`Scripts/Core/Tuning.cs`, na seção certa (constantes de seção no topo do arquivo, ex.: `SKing`, `SDummy`).

```csharp
[Param(SKing, "(1) Olho do Rei: duração", "Segundos que o Olho do Rei fica ativo ...", 1, 20, 0.5f, Sub = Hab)]
public float KingEyeDuration = 7f;
```
- Argumentos: seção, rótulo (pt-BR, curto), descrição (pt-BR, diz unidade e efeito), min, max, passo.
- `Sub` = aba dentro da seção no artifact (ex.: `Star` = "Estrelas", `Hab` = habilidades). Personagens usam `<Nome>Star*` com `Sub = Star`.
- Prefira `float`. Nome em inglês PascalCase, rótulo/descrição em português.
- Leia no código por `Tuning.I.<Campo>` (veja como os vizinhos leem; não guarde em cache se o F5 tiver que recarregar).
- Texto de habilidade pode citar o valor com `{Campo}` em `Characters.cs` (`Characters.Describe` substitui).

## Depois
1. `dotnet build` para compilar.
2. Esquema + artifact:
   ```
   <godot_console> --headless --path . -- --dump-tuning-schema docs/tuning_schema.json
   python tools/build_tuning_artifact.py "vX.Y · Etapa N"
   ```
3. Publicar `docs/tuning_editor.html` com a ferramenta Artifact no url `https://claude.ai/artifact/FjmhXo9dKiHoCRZk5zZLFo`
   (outro chat: `action: read` antes). Nunca `action: open`. Só publique se algum `[Param]` mudou.
4. Valores antigos: no editor, o `tuning.json` da RAIZ do projeto guarda valores velhos e sobrepõe os padrões — apague antes de
   testar à mão (o build.ps1 já apaga). Na build, campos novos são acrescentados ao `Build\tuning.json` mantendo os do usuário.
5. Online: o host manda o tuning aos clientes (`Tuning.ApplyJson`), não precisa mexer na rede para valor novo.

`<godot_console>` = `%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`
