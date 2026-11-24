# Absolute Basketball

Jogo de basquete 3D em terceira pessoa, com multijogador online pela Steam. Feito na Godot 4.7 (.NET / C#).

## Como jogar
1. Baixe o `AbsoluteBasketball-windows.zip` da [última versão](https://github.com/BladeGuyX/AbsoluteBasketball/releases/latest).
2. Extraia o .zip numa pasta sua (por exemplo, `Documentos\AbsoluteBasketball`). Não use "Arquivos de Programas", porque o atualizador precisa gravar na pasta.
3. Abra o `AbsoluteBasketball.exe`. Para jogar online, a Steam precisa estar aberta.

## Atualizações
O jogo procura uma versão nova sozinho ao abrir o menu. Quando houver uma, clique em **"Atualizar para vX"** no menu principal:
- ele baixa a versão nova, fecha, troca os arquivos e abre de novo;
- seus ajustes de `tuning.json` ficam guardados em `tuning.old.json`.

Online, todos precisam estar na mesma versão.

## Controle não funciona?
O jogo usa o AppID de teste da Steam (Spacewar), e o Steam Input dele pode capturar o controle. Para resolver:
1. Na Steam, abra Biblioteca → Spacewar → Propriedades → Controle.
2. Escolha **Desativar Steam Input**.
3. Abra o jogo de novo.

## Desenvolvimento
Veja `CLAUDE.md` e a pasta `docs/`:
- `GDD.md`: regras e mecânicas;
- `PLANO.md`: etapas e status;
- `TESTES.md`: roteiros de teste manual;
- `ARTE.md`: pipeline de arte no Blender.

Para gerar a build: `powershell -ExecutionPolicy Bypass -File tools\build.ps1`.

Para publicar uma versão:
1. Suba `GameVersion.Number`.
2. Rode `powershell -ExecutionPolicy Bypass -File tools\release.ps1`.
