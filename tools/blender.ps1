# Roda um script Python do Blender sem abrir a interface (Etapa 7 — arte).
# Uso:  powershell -ExecutionPolicy Bypass -File tools\blender.ps1 <script.py> [args do script...] [-Blend <arquivo.blend>]
# Ex.:  powershell -ExecutionPolicy Bypass -File tools\blender.ps1 art_source\scripts\make_rig_base.py
#       powershell -ExecutionPolicy Bypass -File tools\blender.ps1 art_source\scripts\render_preview.py assets\models\ball.glb
#       powershell -ExecutionPolicy Bypass -File tools\blender.ps1 art_source\scripts\render_preview.py --no-ref -Blend art_source\blend\x.blend
# (sem bloco param: assim argumentos como --no-ref passam direto para o script)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$script = $null; $blend = $null; $rest = @()
for ($i = 0; $i -lt $args.Count; $i++) {
    $a = [string]$args[$i]
    if ($a -eq "-Blend") { $blend = [string]$args[++$i]; continue }
    if ($a -eq "--") { continue }
    if (-not $script) { $script = $a } else { $rest += $a }
}
if (-not $script) { throw "uso: tools\blender.ps1 <script.py> [args...] [-Blend arquivo.blend]" }

# Blender mais novo instalado em "Program Files\Blender Foundation"
$exe = Get-ChildItem "$env:ProgramFiles\Blender Foundation" -Directory -ErrorAction SilentlyContinue |
    Sort-Object { [version](($_.Name -replace '[^0-9.]', '') + '.0') } -Descending |
    ForEach-Object { Join-Path $_.FullName "blender.exe" } | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $exe) { throw "Blender não encontrado em $env:ProgramFiles\Blender Foundation" }

$argsList = @("-b")
if ($blend) { $argsList += (Resolve-Path $blend).Path } else { $argsList += "--factory-startup" }
$argsList += @("--python-exit-code", "1", "-P", (Resolve-Path $script).Path)
if ($rest.Count -gt 0) { $argsList += "--"; $argsList += $rest }

Write-Host "== $(Split-Path -Leaf (Split-Path $exe)) · $script $($rest -join ' ')"
& $exe @argsList
exit $LASTEXITCODE
