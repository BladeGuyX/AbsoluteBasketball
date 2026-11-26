# Publica uma versão nova no GitHub (os amigos recebem pelo botão "Atualizar" do menu).
# Uso:  powershell -ExecutionPolicy Bypass -File tools\release.ps1 [-Notes "o que mudou"] [-SkipBuild]
#
# 1. Lê a versão de Scripts\Core\GameVersion.cs (Number) -> tag "v<Number>". Suba o número antes de publicar.
# 2. Roda o tools\build.ps1 (compila, testa, exporta), a menos que -SkipBuild.
# 3. Compacta Build\ (sem tuning.json) em dist\AbsoluteBasketball-windows.zip.
# 4. git push e cria a release com o .zip (gh release create).
param([string]$Notes = "", [switch]$SkipBuild)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$m = Select-String -Path "Scripts\Core\GameVersion.cs" -Pattern 'Number = "([^"]+)"'
if (-not $m) { throw "versão não encontrada em GameVersion.cs" }
$ver = $m.Matches[0].Groups[1].Value
$tag = "v$ver"
Write-Host "== release $tag" -ForegroundColor Cyan

$prev = $ErrorActionPreference; $ErrorActionPreference = "Continue"
gh release view $tag *> $null
$exists = $LASTEXITCODE -eq 0
$ErrorActionPreference = $prev
if ($exists) { throw "a release $tag já existe — suba GameVersion.Number antes de publicar" }

if (-not $SkipBuild) {
    & powershell -ExecutionPolicy Bypass -File tools\build.ps1
    if ($LASTEXITCODE -ne 0) { throw "build falhou" }
}
if (-not (Test-Path "Build\AbsoluteBasketball.exe")) { throw "Build\AbsoluteBasketball.exe não existe" }

New-Item -ItemType Directory -Force dist | Out-Null
$zip = "dist\AbsoluteBasketball-windows.zip"
if (Test-Path $zip) { Remove-Item $zip }
$items = Get-ChildItem Build -Force | Where-Object { $_.Name -notin @("tuning.json", "tuning.old.json", "tuning.defaults.json", ".gdignore") }
Compress-Archive -Path $items.FullName -DestinationPath $zip -CompressionLevel Optimal
Write-Host ("zip: {0:0.0} MB" -f ((Get-Item $zip).Length / 1MB))

if (-not $Notes) { $Notes = "Absolute Basketball $tag. Quem já tem o jogo: menu principal -> Atualizar. Primeira vez: baixe o .zip, extraia numa pasta e abra AbsoluteBasketball.exe (Steam aberta para jogar online)." }
git push origin HEAD
if ($LASTEXITCODE -ne 0) { throw "git push falhou" }
gh release create $tag $zip --title "Absolute Basketball $tag" --notes $Notes --target (git rev-parse HEAD)
if ($LASTEXITCODE -ne 0) { throw "gh release create falhou" }
Write-Host "Publicado: https://github.com/BladeGuyX/AbsoluteBasketball/releases/tag/$tag" -ForegroundColor Green
