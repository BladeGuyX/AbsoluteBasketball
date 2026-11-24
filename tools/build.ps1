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
        # testes sempre com os valores padrão do código (o tuning.json da raiz guarda valores antigos)
        if (Test-Path tuning.json) { Remove-Item tuning.json -Force }
        Write-Host "== smoke test ==" -ForegroundColor Cyan
        & $godot --headless --path . -- --smoke-test 2>&1 | Select-String "SMOKE|SCORE|ERROR" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "smoke test falhou" }
        Write-Host "== smoke test etapa 2 ==" -ForegroundColor Cyan
        & $godot --headless --path . -- --smoke-e2 2>&1 | Select-String "\[E2\] passe|ERROR" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "smoke test etapa 2 falhou" }
        Write-Host "== smoke test etapa 3 ==" -ForegroundColor Cyan
        & $godot --headless --path . -- --smoke-e3 2>&1 | Select-String "\[E3\] enterrada|ERROR" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "smoke test etapa 3 falhou" }
        Write-Host "== smoke test bloqueio dos bots ==" -ForegroundColor Cyan
        & $godot --headless --path . -- --smoke-block 2>&1 | Select-String "\[BLOCKTEST\]|ERROR" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "smoke test de bloqueio falhou" }
        Write-Host "== smoke test etapa 4 (partida so de bots) ==" -ForegroundColor Cyan
        & $godot --headless --path . -- --smoke-e4 2>&1 | Select-String "\[E4\]|ERROR" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "smoke test etapa 4 falhou" }
        Write-Host "== smoke test etapa 5 (rede: host + cliente por IP local) ==" -ForegroundColor Cyan
        $hostLog = Join-Path $env:TEMP "ab_net_host.log"
        $hostProc = Start-Process -FilePath $godot -ArgumentList "--headless", "--path", ".", "--", "--net-smoke-host" -PassThru -NoNewWindow -RedirectStandardOutput $hostLog
        $null = $hostProc.Handle   # sem guardar o handle o PowerShell não informa o ExitCode
        Start-Sleep -Seconds 2
        & $godot --headless --path . -- --net-smoke-client 2>&1 | Select-String "\[NETTEST-CLIENT\]|ERROR" | Out-Host
        $clientExit = $LASTEXITCODE
        $hostProc.WaitForExit(120000) | Out-Null
        Select-String -Path $hostLog -Pattern "\[NETTEST-HOST\]" | ForEach-Object { $_.Line } | Out-Host
        if ($clientExit -ne 0 -or $hostProc.ExitCode -ne 0) { throw "smoke test etapa 5 (rede) falhou" }
    }

    Write-Host "== export ==" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force "Build" | Out-Null
    & $godot --headless --path . --export-release "Windows Desktop" "Build/AbsoluteBasketball.exe" 2>&1 | Select-String "ERROR|error|savepack|Export" | Out-Host
    if (-not (Test-Path "Build\AbsoluteBasketball.exe")) { throw "export falhou" }
    # Steam: a DLL nativa e o steam_appid.txt (480 = app de teste da Valve) ficam ao lado do .exe
    Copy-Item "ThirdParty\Steamworks\steam_api64.dll" "Build\" -Force
    Copy-Item "steam_appid.txt" "Build\" -Force
    Write-Host "OK -> $root\Build\AbsoluteBasketball.exe" -ForegroundColor Green
}
finally { Pop-Location }
