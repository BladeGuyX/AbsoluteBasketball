using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Godot;
using Steamworks;

namespace AB;

/// <summary>
/// Inicia a API da Steam uma vez (AppID 480 / Spacewar, de teste — steam_appid.txt) e bombeia os callbacks.
/// Sem a Steam aberta o jogo funciona normalmente, só sem o online pela Steam (o IP direto continua).
/// </summary>
public static class SteamService
{
    public static bool Initialized { get; private set; }
    /// <summary>Por que a Steam não está disponível (se não estiver).</summary>
    public static string Error { get; private set; }
    public static bool OverlayOpen { get; private set; }
    public static ulong LocalId => Initialized ? SteamUser.GetSteamID().m_SteamID : 0;
    public static string LocalName => Initialized ? SteamFriends.GetPersonaName() : null;

    static bool _tried;
    static Callback<GameOverlayActivated_t> _overlay;

    /// <summary>Acha o steam_api64.dll ao lado do .exe, na pasta das DLLs C# ou em ThirdParty (editor/testes).</summary>
    static void SetupNativeResolver()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(SteamAPI).Assembly, (name, asm, path) =>
            {
                if (!name.Contains("steam_api", StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;
                string file = name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name : name + ".dll";
                foreach (var dir in new[]
                {
                    Path.GetDirectoryName(OS.GetExecutablePath()),
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    ProjectSettings.GlobalizePath("res://ThirdParty/Steamworks"),
                })
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    string p = Path.Combine(dir, file);
                    if (File.Exists(p) && NativeLibrary.TryLoad(p, out var h)) return h;
                }
                return IntPtr.Zero;
            });
        }
        catch (InvalidOperationException) { /* já configurado */ }
    }

    public static void Init()
    {
        if (_tried) return;
        _tried = true;
        SetupNativeResolver();
        try
        {
            if (!Packsize.Test() || !DllCheck.Test())
            {
                Error = "Biblioteca da Steam incompatível.";
                return;
            }
            var result = SteamAPI.InitEx(out string message);
            Initialized = result == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
            if (!Initialized) Error = $"A Steam não iniciou ({message}). A Steam está aberta?";
        }
        catch (DllNotFoundException e) { Error = "steam_api64.dll não encontrada: " + e.Message; }
        catch (Exception e) { Error = "Erro ao iniciar a Steam: " + e.Message; }
        if (Initialized)
        {
            SteamNetworkingUtils.InitRelayNetworkAccess();
            _overlay = Callback<GameOverlayActivated_t>.Create(o => OverlayOpen = o.m_bActive != 0);
            GD.Print($"[STEAM] iniciada como {LocalName} ({LocalId})");
        }
        else GD.Print("[STEAM] indisponível: " + Error);
    }

    public static void Pump()
    {
        if (Initialized) SteamAPI.RunCallbacks();
    }

    public static void Shutdown()
    {
        if (!Initialized) return;
        SteamAPI.Shutdown();
        Initialized = false;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string name);

    /// <summary>
    /// O overlay da Steam (Shift+Tab, janela de convite) só entra no processo antes dos gráficos iniciarem, o que só
    /// acontece quando a Steam abre o jogo. Aberto pelo .exe ele chega tarde — mas a Steam o coloca em processos que o
    /// jogo abrir. Então o jogo se reabre uma vez (com --steamoverlay) e esta cópia fecha. Mesmo truque do Just Fall.
    /// Retorna true se reabriu (o chamador deve sair).
    /// </summary>
    public static bool RelaunchForOverlay(string[] userArgs)
    {
        if (OS.HasFeature("editor") || DisplayServer.GetName() == "headless") return false;
        if (Array.IndexOf(userArgs, "--steamoverlay") >= 0) return false;
        try
        {
            if (GetModuleHandle("gameoverlayrenderer64.dll") != IntPtr.Zero) return false;
            Init();
            if (!Initialized) return false;
            var args = new System.Collections.Generic.List<string>(OS.GetCmdlineArgs());
            if (!args.Contains("--")) args.Add("--");
            args.Add("--steamoverlay");
            int pid = OS.CreateProcess(OS.GetExecutablePath(), args.ToArray());
            if (pid <= 0) return false;
            Shutdown();
            return true;
        }
        catch (Exception e)
        {
            GD.PushWarning("[STEAM] não deu para reabrir para o overlay: " + e.Message);
            return false;
        }
    }
}
