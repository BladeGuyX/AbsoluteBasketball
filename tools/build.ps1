# Gera a build jogavel em Build\AbsoluteBasketball.exe
# Uso:  powershell -ExecutionPolicy Bypass -File tools\build.ps1  [-SkipSmoke]
param([switch]$SkipSmoke)
$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot
$godot = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter "Godot_v4.7.2-stable_mono_win64_console.exe" -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if (-not $godot) { throw "Godot 4.7.2 .NET nao encontrado (instale: winget install GodotEngine.GodotEngine.Mono)" }

Push-Location $root
try {
    Write-Host "== dotnet build ==" -ForegroundColor Cyan
    dotnet build -c ExportRelease | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet build falhou" }

    Write-Host "== import ==" -ForegroundColor Cyan
    & $godot --headless --path . --import | Out-Null

    if (-not $SkipSmoke) {
        Write-Host "== smoke test ==" -ForegroundColor Cyan
        & $godot --headless --path . -- --smoke-test 2>&1 | Select-String "SMOKE|SCORE|ERROR" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "smoke test falhou" }
        Write-Host "== smoke test etapa 2 ==" -ForegroundColor Cyan
        & $godot --headless --path . -- --smoke-e2 2>&1 | Select-String "\[E2\] passe|ERROR" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "smoke test etapa 2 falhou" }
    }

    Write-Host "== export ==" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force "Build" | Out-Null
    & $godot --headless --path . --export-release "Windows Desktop" "Build/AbsoluteBasketball.exe" 2>&1 | Select-String "ERROR|error|savepack|Export" | Out-Host
    if (-not (Test-Path "Build\AbsoluteBasketball.exe")) { throw "export falhou" }
    Write-Host "OK -> $root\Build\AbsoluteBasketball.exe" -ForegroundColor Green
}
finally { Pop-Location }
