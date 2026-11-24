using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace AB;

/// <summary>
/// Atualização automática pelas Releases do GitHub (repositório público).
/// - Ao abrir o menu: consulta a última release; se a tag for mais nova que <see cref="GameVersion.Number"/>, o botão
///   "Atualizar" do menu avisa.
/// - Ao clicar: baixa o .zip da release para user://, escreve um script PowerShell que espera o jogo fechar, extrai por
///   cima da pasta do .exe (guardando o tuning.json antigo como tuning.old.json, para os valores novos valerem) e abre
///   o jogo de novo. Só instala na build exportada (rodando pela Godot apenas verifica).
/// </summary>
public partial class Updater : Node
{
    public const string Repo = "BladeGuyX/AbsoluteBasketball";
    public const string AssetName = "AbsoluteBasketball-windows.zip";

    public enum St { Idle, Checking, UpToDate, Available, Downloading, Installing, Failed }

    public static Updater I { get; private set; }
    public St State { get; private set; } = St.Idle;
    public string Latest { get; private set; }
    public string Error { get; private set; }
    public float Progress { get; private set; }
    public event Action Changed;

    /// <summary>Só a build exportada (Windows) se substitui; pela Godot apenas verifica.</summary>
    public static bool CanInstall => OS.HasFeature("template") && OS.GetName() == "Windows";

    HttpRequest _http;
    bool _downloading;
    string _assetUrl;
    long _assetSize;
    string ZipPath => ProjectSettings.GlobalizePath("user://update.zip");

    public override void _Ready()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
        _http = new HttpRequest { Timeout = 30, MaxRedirects = 8, UseThreads = true };
        AddChild(_http);
        _http.RequestCompleted += OnCompleted;
    }

    public override void _ExitTree() { if (I == this) I = null; }

    public override void _Process(double delta)
    {
        if (State != St.Downloading || _assetSize <= 0) return;
        float p = Mathf.Clamp((float)_http.GetDownloadedBytes() / _assetSize, 0, 1);
        if (Mathf.Abs(p - Progress) >= 0.01f) { Progress = p; Changed?.Invoke(); }
    }

    /// <summary>Teste automático (--update-test): instala sozinho e a cópia reaberta só imprime a versão e fecha.</summary>
    public bool Auto;

    void Set(St s, string err = null)
    {
        State = s;
        Error = err;
        Changed?.Invoke();
        if (!Auto) return;
        GD.Print($"[UPDATE-TEST] estado={s} atual=v{GameVersion.Number} última=v{Latest} {err}");
        if (s == St.Available && !CanInstall) GetTree().Quit(1);
        else if (s == St.Available) Callable.From(Activate).CallDeferred();
        else if (s is St.UpToDate or St.Failed) GetTree().Quit(s == St.Failed ? 1 : 0);
    }

    public bool Busy => State is St.Checking or St.Downloading or St.Installing;

    /// <summary>Consulta a última release no GitHub.</summary>
    public void Check()
    {
        if (Busy) return;
        _downloading = false;
        Set(St.Checking);
        var err = _http.Request($"https://api.github.com/repos/{Repo}/releases/latest",
            new[] { "User-Agent: AbsoluteBasketball-Updater", "Accept: application/vnd.github+json" });
        if (err != Godot.Error.Ok) Set(St.Failed, $"sem conexão ({err})");
    }

    /// <summary>Botão do menu: verifica; se já tem versão nova, baixa e instala.</summary>
    public void Activate()
    {
        if (Busy) return;
        if (State == St.Available && CanInstall) Download();
        else Check();
    }

    void Download()
    {
        _downloading = true;
        Progress = 0;
        if (File.Exists(ZipPath)) File.Delete(ZipPath);
        _http.DownloadFile = ZipPath;
        Set(St.Downloading);
        var err = _http.Request(_assetUrl, new[] { "User-Agent: AbsoluteBasketball-Updater", "Accept: application/octet-stream" });
        if (err != Godot.Error.Ok) { _http.DownloadFile = ""; Set(St.Failed, $"não deu para baixar ({err})"); }
    }

    void OnCompleted(long result, long code, string[] headers, byte[] body)
    {
        if (_downloading)
        {
            _downloading = false;
            _http.DownloadFile = "";
            long size = File.Exists(ZipPath) ? new FileInfo(ZipPath).Length : 0;
            if (result != (long)HttpRequest.Result.Success || code != 200 || size == 0 || (_assetSize > 0 && size != _assetSize))
            {
                Set(St.Failed, $"download incompleto (HTTP {code}, {size / 1048576f:0.0} MB)");
                return;
            }
            Install();
            return;
        }

        if (result != (long)HttpRequest.Result.Success) { Set(St.Failed, "sem conexão com o GitHub"); return; }
        if (code == 404) { Set(St.UpToDate); return; } // ainda não há nenhuma release
        if (code != 200) { Set(St.Failed, $"GitHub respondeu {code}"); return; }
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            Latest = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
            _assetUrl = null;
            foreach (var a in root.GetProperty("assets").EnumerateArray())
                if (a.GetProperty("name").GetString() == AssetName)
                {
                    _assetUrl = a.GetProperty("browser_download_url").GetString();
                    _assetSize = a.GetProperty("size").GetInt64();
                }
            if (!IsNewer(Latest, GameVersion.Number)) { Set(St.UpToDate); return; }
            if (_assetUrl == null) { Set(St.Failed, $"a v{Latest} não tem o arquivo {AssetName}"); return; }
            GD.Print($"[UPDATE] versão nova: v{Latest} ({_assetSize / 1048576f:0.0} MB)");
            Set(St.Available);
        }
        catch (Exception e) { Set(St.Failed, "resposta inválida do GitHub: " + e.Message); }
    }

    /// <summary>"0.13" &gt; "0.12"? Compara número a número (0.10 &gt; 0.9).</summary>
    public static bool IsNewer(string latest, string current)
    {
        static int[] P(string s) => Array.ConvertAll((s ?? "0").Split('.'),
            x => int.TryParse(new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.TakeWhile(x, char.IsDigit))), out var n) ? n : 0);
        var a = P(latest); var b = P(current);
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    void Install()
    {
        Set(St.Installing);
        try
        {
            string exe = OS.GetExecutablePath();
            string dir = Path.GetDirectoryName(exe);
            string script = ProjectSettings.GlobalizePath("user://update.ps1");
            File.WriteAllText(script, Script, new System.Text.UTF8Encoding(true));
            int pid = OS.GetProcessId();
            GD.Print($"[UPDATE] instalando v{Latest} em {dir}");
            int child = OS.CreateProcess("powershell.exe", new[]
            {
                "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script,
                "-ProcId", pid.ToString(), "-Zip", ZipPath, "-Dir", dir, "-Exe", exe, "-Version", Latest ?? "",
                "-ExeArgs", Auto ? "--print-version" : "",
            });
            if (child <= 0) { Set(St.Failed, "não consegui iniciar o instalador"); return; }
            GetTree().Quit();
        }
        catch (Exception e) { Set(St.Failed, e.Message); }
    }

    /// <summary>Instalador: espera o jogo fechar, extrai por cima, guarda o tuning.json antigo e reabre o jogo.</summary>
    const string Script = """
param([int]$ProcId, [string]$Zip, [string]$Dir, [string]$Exe, [string]$Version, [string]$ExeArgs = "")
$Host.UI.RawUI.WindowTitle = "Absolute Basketball - atualizando"
$log = Join-Path $env:TEMP "AbsoluteBasketball-update.log"
Write-Host "Atualizando Absolute Basketball para a v$Version..." -ForegroundColor Cyan
try {
    Wait-Process -Id $ProcId -Timeout 60 -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 700
    $tmp = Join-Path $env:TEMP ("ab_update_" + [guid]::NewGuid().ToString("N"))
    Write-Host "Extraindo..."
    Expand-Archive -LiteralPath $Zip -DestinationPath $tmp -Force
    $tj = Join-Path $Dir "tuning.json"
    if (Test-Path $tj) { Move-Item $tj (Join-Path $Dir "tuning.old.json") -Force }
    Write-Host "Copiando arquivos..."
    $ok = $false
    for ($i = 0; $i -lt 10 -and -not $ok; $i++) {
        try { Copy-Item -Path (Join-Path $tmp "*") -Destination $Dir -Recurse -Force -ErrorAction Stop; $ok = $true }
        catch { Start-Sleep -Seconds 1 }
    }
    if (-not $ok) { throw "nao consegui substituir os arquivos (o jogo ainda esta aberto?)" }
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $Zip -Force -ErrorAction SilentlyContinue
    "OK v$Version $(Get-Date)" | Out-File $log
    Write-Host "Pronto! Abrindo o jogo." -ForegroundColor Green
} catch {
    "ERRO $(Get-Date): $_" | Out-File $log
    Write-Host "Erro na atualizacao: $_" -ForegroundColor Red
    Write-Host "Baixe manualmente em https://github.com/BladeGuyX/AbsoluteBasketball/releases"
    Start-Sleep -Seconds 8
}
if ($ExeArgs) { Start-Process -FilePath $Exe -WorkingDirectory $Dir -ArgumentList @("--", $ExeArgs) }
else { Start-Process -FilePath $Exe -WorkingDirectory $Dir }
""";
}
