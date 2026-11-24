using System;
using System.IO;
using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Nó raiz (Main.tscn). Inicializa configurações/input/tuning e troca entre menu e partida.
/// Argumentos (após "--"):  --smoke-test   |   --dump-tuning-schema &lt;arquivo.json&gt;
/// </summary>
public partial class GameRoot : Node
{
    public static GameRoot I { get; private set; }

    CanvasLayer _ui;
    Control _menu;
    Match _match;
    Label _toast;
    float _toastT;
    MatchConfig _lastCfg = new();

    public override void _Ready()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
        UserSettings.EnsureAudioBuses();
        UserSettings.Load();
        InputSetup.Apply();
        UserSettings.I.ApplyAudio();
        Callable.From(UserSettings.I.ApplyVideo).CallDeferred();
        string tuningMsg = Tuning.Load();

        var args = OS.GetCmdlineUserArgs();
        int di = Array.IndexOf(args, "--dump-tuning-schema");
        if (di >= 0 && di + 1 < args.Length)
        {
            File.WriteAllText(args[di + 1], Tuning.SchemaJson());
            GD.Print("[TUNING] esquema salvo em " + args[di + 1]);
            GetTree().Quit();
            return;
        }

        if (args.Contains("--shot-stats")) { ShotStats(); GetTree().Quit(); return; }

        // ---- online (Etapa 5): Steam só no jogo normal (os testes automáticos não mexem na sua Steam) ----
        bool automated = args.Any(a => a.StartsWith("--smoke") || a.StartsWith("--net-smoke") || a == "--screenshot" || a == "--video-test");
        if (!automated)
        {
            if (SteamService.RelaunchForOverlay(args)) { GetTree().Quit(); return; }
            SteamService.Init();
        }
        else if (args.Contains("--steam-test")) SteamService.Init();
        AddChild(new NetSession { Name = "Net" });
        InputDevice.LogPads();

        _ui = new CanvasLayer { Layer = 20 };
        AddChild(_ui);
        SetupCursor(args);
        _toast = UiKit.Outlined("", 22, Colors.White, HorizontalAlignment.Right, 8);
        _ui.AddChild(UiKit.Anchor(_toast, 1, 0, 1, 0, -1200, 120, -20, 160));

        if (args.Contains("--video-test"))
        {
            ShowMainMenu();
            void Report(string tag) => GD.Print($"[VIDEO] {tag}: mode={DisplayServer.WindowGetMode()} size={DisplayServer.WindowGetSize()} borderless={DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.Borderless)} screen={DisplayServer.ScreenGetSize()}");
            var tree = GetTree();
            tree.CreateTimer(1.0).Timeout += () => { Report("inicio"); UserSettings.I.Fullscreen = false; UserSettings.I.ApplyVideo(); };
            tree.CreateTimer(2.0).Timeout += () => { Report("apos desligar"); UserSettings.I.Fullscreen = true; UserSettings.I.ApplyVideo(); };
            tree.CreateTimer(3.0).Timeout += () => { Report("apos ligar"); UserSettings.I.Fullscreen = false; UserSettings.I.ApplyVideo(); };
            tree.CreateTimer(4.0).Timeout += () => { Report("apos desligar 2"); UserSettings.Load(); tree.Quit(); };
            return;
        }
        if (args.Contains("--pad")) InputDevice.Set(true); // prints com os ícones do controle
        int si = Array.IndexOf(args, "--screenshot");
        if (si >= 0 && si + 1 < args.Length)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(1600, 900));
            if (args.Contains("--menu") || args.Contains("--online") || args.Contains("--lobby"))
            {
                if (args.Contains("--online")) ShowOnlineMenu();
                else if (args.Contains("--lobby")) { NetSession.I.HostDirect(NetSmokePort, "Blade", loopbackOnly: true); NetSession.I.SetMyTeam(0); }
                else ShowMainMenu();
                GetTree().CreateTimer(1.5).Timeout += () =>
                {
                    GetViewport().GetTexture().GetImage().SavePng(args[si + 1]);
                    GetTree().Quit();
                };
                return;
            }
            StartMatch(new MatchConfig { AutoPilotLocal = true, ScreenshotPath = args[si + 1], Size = CourtSize.Small, AllyDummies = 1, EnemyDummies = 1, TrainingDummies = true });
            return;
        }
        if (args.Contains("--smoke-test"))
        {
            SetTestSpeed(2.0f);
            StartMatch(new MatchConfig { SmokeTest = true, Size = CourtSize.Medium, TargetScore = 99, AllyDummies = 0, EnemyDummies = 0 });
            return;
        }
        if (args.Contains("--smoke-e4"))
        {
            SetTestSpeed(3.0f);
            StartMatch(new MatchConfig { SmokeE4 = true, Size = CourtSize.Medium, TargetScore = 999, AllyDummies = 2, EnemyDummies = 3, BotDifficulty = 1 });
            return;
        }
        // teste de rede: duas instâncias (host e cliente) conversando por IP local, bots controlando os dois jogadores
        int lag = Array.IndexOf(args, "--net-lag");
        if (lag >= 0 && lag + 1 < args.Length && int.TryParse(args[lag + 1], out int lagMs)) EnetTransport.LagMs = lagMs;
        int nh = Array.IndexOf(args, "--net-smoke-host");
        if (nh >= 0)
        {
            SetTestSpeed(2.0f);
            var ns = NetSession.I;
            ns.SmokeNet = true;
            if (!ns.HostDirect(NetSmokePort, "Host", loopbackOnly: true)) { GD.Print("[NETTEST-HOST] não abriu a porta -> FALHOU"); GetTree().Quit(1); return; }
            ns.HostSetSettings(new OnlineSettings { Size = CourtSize.Small, TeamSize = 2, TargetScore = 99, BotDifficulty = 1 });
            ns.LobbyChanged += () => { if (!ns.InMatch && ns.Members.Count >= 2) ns.HostStartMatch(); };
            GetTree().CreateTimer(60).Timeout += () => { if (!ns.InMatch) { GD.Print("[NETTEST-HOST] o cliente não entrou -> FALHOU"); GetTree().Quit(1); } };
            return;
        }
        if (args.Contains("--net-smoke-client"))
        {
            SetTestSpeed(2.0f);
            var ns = NetSession.I;
            ns.SmokeNet = true;
            int shot = Array.IndexOf(args, "--net-shot");
            if (shot >= 0 && shot + 1 < args.Length) ns.SmokeShot = args[shot + 1];
            // tenta algumas vezes (o host pode ainda estar abrindo)
            int tries = 0;
            void TryJoin()
            {
                if (ns.InMatch || (ns.Active && ns.Connected)) return;
                if (++tries > 20) { GD.Print("[NETTEST-CLIENT] não conectou -> FALHOU"); GetTree().Quit(1); return; }
                ns.JoinDirect("127.0.0.1", NetSmokePort, "Cliente");
                GetTree().CreateTimer(3).Timeout += TryJoin;
            }
            TryJoin();
            return;
        }
        if (args.Contains("--steam-test"))
        {
            GD.Print(SteamService.Initialized ? $"[STEAMTEST] OK: Steam iniciada como {SteamService.LocalName}" : $"[STEAMTEST] Steam indisponível: {SteamService.Error}");
            GetTree().Quit(0);
            return;
        }
        if (args.Contains("--smoke-block"))
        {
            SetTestSpeed(2.0f);
            StartMatch(new MatchConfig { SmokeBlock = true, Size = CourtSize.Medium, TargetScore = 999, AllyDummies = 0, EnemyDummies = 1, BotDifficulty = 1 });
            return;
        }
        if (args.Contains("--smoke-e3"))
        {
            SetTestSpeed(2.0f);
            int ei = Array.IndexOf(args, "--e3-shot");
            StartMatch(new MatchConfig { SmokeE3 = true, Size = CourtSize.Small, TargetScore = 99, AllyDummies = 0, EnemyDummies = 1, TrainingDummies = true,
                ScreenshotPath = ei >= 0 && ei + 1 < args.Length ? args[ei + 1] : null });
            return;
        }
        if (args.Contains("--smoke-e2"))
        {
            SetTestSpeed(2.0f);
            StartMatch(new MatchConfig { SmokeE2 = true, Size = CourtSize.Small, TargetScore = 99, AllyDummies = 1, EnemyDummies = 1, TrainingDummies = true });
            return;
        }
        ShowMainMenu();
        Toast(tuningMsg);
    }

    /// <summary>Estatística de arremessos com erro humano simulado (diagnóstico de tuning).</summary>
    static void ShotStats()
    {
        var g = CourtGeo.Build(CourtSize.Medium);
        var rng = new RandomNumberGenerator { Seed = 42 };
        int hoop = CourtGeo.AttackHoop(0);
        var rim = g.RimCenter[hoop];
        foreach (float dist in new[] { 4f, 7f, 10f, 13f })
        {
            int made = 0, rimHits = 0, bank = 0, n = 400;
            for (int i = 0; i < n; i++)
            {
                float ang = rng.RandfRange(-0.6f, 0.6f);
                var feet = new Vector3(rim.X - CourtGeo.HoopSign(hoop) * dist * Mathf.Cos(ang), 0, dist * Mathf.Sin(ang));
                var toRim = (rim - feet) with { Y = 0 };
                float yaw = ShotMath.YawOf(toRim) + Mathf.DegToRad(rng.RandfRange(-5f, 5f));
                var rel = ShotMath.ReleasePos(feet, yaw);
                float d = new Vector2(rim.X - rel.X, rim.Z - rel.Z).Length() + rng.RandfRange(-1.5f, 1.5f);
                var plan = ShotMath.Plan(feet, 0, yaw, ShotMath.PitchForDistance(d), g);
                var pos = plan.Pos; var vel = plan.Vel;
                bool hitRim = false, hitBoard = false, scored = false;
                for (int k = 0; k < 400; k++)
                {
                    var h = BallPhys.Simulate(ref pos, ref vel, 1f / 60f, g, hoop);
                    hitRim |= h.Rim; hitBoard |= h.Board;
                    if (h.ScoredHoop == hoop) { scored = true; break; }
                    if (h.Floor) break;
                }
                if (scored) made++;
                if (hitRim) rimHits++;
                if (hitBoard && scored) bank++;
            }
            GD.Print($"[STATS] {dist,4:0}m: cestas {100f * made / n:0}%  tocou aro {100f * rimHits / n:0}%  tabelinhas convertidas {100f * bank / n:0}%");
        }
        // cesta por baixo não pode valer: bola jogada para cima por dentro do aro
        int under = 0, wouldScore = 0;
        for (int i = 0; i < 50; i++)
        {
            var pos = rim + new Vector3(rng.RandfRange(-0.2f, 0.2f), -2.2f, rng.RandfRange(-0.2f, 0.2f));
            var vel = new Vector3(rng.RandfRange(-0.3f, 0.3f), rng.RandfRange(8f, 11f), rng.RandfRange(-0.3f, 0.3f));
            int block = -1;
            for (int k = 0; k < 300; k++)
            {
                var h = BallPhys.Simulate(ref pos, ref vel, 1f / 60f, g, hoop);
                if (h.UpThroughHoop >= 0) block = h.UpThroughHoop;
                if (h.ScoredHoop >= 0 && h.ScoredHoop == block) { wouldScore++; break; }
                if (h.ScoredHoop >= 0) { under++; break; }
                if (h.Floor) break;
            }
        }
        GD.Print($"[STATS] cestas por baixo que valeriam (deve ser 0): {under}/50 · bloqueadas pela regra: {wouldScore}");
    }

    /// <summary>Acelera os testes SEM mudar o passo da simulação (mantém 1/60 s de jogo por tick, como no jogo real).</summary>
    static void SetTestSpeed(float k)
    {
        Engine.TimeScale = k;
        Engine.PhysicsTicksPerSecond = Mathf.RoundToInt(60 * k);
        Engine.MaxPhysicsStepsPerFrame = 24;
    }

    const int NetSmokePort = 27961;

    public void ShowMainMenu()
    {
        FreeMatch();
        CloseMenu();
        _menu = new MainMenu { Name = "MainMenu" };
        _ui.AddChild(_menu);
    }

    public void StartMatch(MatchConfig cfg)
    {
        _lastCfg = cfg;
        CloseMenu();
        FreeMatch();
        _match = new Match { Cfg = cfg, Name = "Match" };
        AddChild(_match);
    }

    public void Rematch() => StartMatch(_lastCfg.Clone());

    /// <summary>Tela Online (criar/entrar em lobby).</summary>
    public void ShowOnlineMenu(string status = null)
    {
        FreeMatch();
        CloseMenu();
        _menu = new OnlineMenu { Name = "OnlineMenu", Status = status };
        _ui.AddChild(_menu);
    }

    /// <summary>Tela do lobby (depois de criar/entrar, ou quando a partida online acaba).</summary>
    public void ShowLobby()
    {
        FreeMatch();
        CloseMenu();
        _menu = new LobbyMenu { Name = "LobbyMenu" };
        _ui.AddChild(_menu);
    }

    /// <summary>Sair para o menu principal (online: sai da sessão; se for o host, a partida acaba para todos).</summary>
    public void LeaveToMenu()
    {
        if (NetSession.I != null && NetSession.I.Active) NetSession.I.Leave(toMenu: true);
        else ShowMainMenu();
    }

    void FreeMatch()
    {
        GetTree().Paused = false;
        if (_match != null && IsInstanceValid(_match))
        {
            RemoveChild(_match);
            _match.QueueFree();
        }
        _match = null;
    }

    void CloseMenu()
    {
        if (_menu != null && IsInstanceValid(_menu)) _menu.QueueFree();
        _menu = null;
    }

    public void Toast(string text, float dur = 4f)
    {
        if (_toast == null) return;
        _toast.Text = text;
        _toastT = dur;
        GD.Print("[TOAST] " + text);
    }

    public override void _Input(InputEvent e) => InputDevice.Feed(e); // último dispositivo usado (ícones / vibração)

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed(Act.ReloadTuning))
        {
            if (NetSession.I != null && NetSession.I.IsClient && NetSession.I.InMatch) { Toast("Online: os valores de tuning são os do host."); return; }
            string msg = Tuning.Load();
            Toast(msg + (_match != null ? "  · tamanhos de quadra/aro valem na próxima partida" : ""));
        }
    }

    public override void _Notification(int what)
    {
        // Não mexe no modo do mouse com a janela sem foco (isso deixava o cursor "congelado" ao voltar do alt-tab);
        // o modo certo é aplicado em UpdateMouse() quando o foco volta.
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
        {
            if (NetSession.I != null && NetSession.I.Active) NetSession.I.Leave(toMenu: false);
            SteamService.Shutdown();
        }
        if (what == NotificationApplicationFocusOut)
        {
            if (_match != null && IsInstanceValid(_match)) _match.OnFocusLost();
        }
        else if (what == NotificationApplicationFocusIn || what == NotificationWMWindowFocusIn)
        {
            CameraRig.IgnoreMouseUntilMs = Time.GetTicksMsec() + 250;
            _mouseRefresh = 6;
        }
    }

    int _mouseRefresh;

    /// <summary>
    /// Cursor do Windows sempre TRANSPARENTE (em tela cheia ele congelava/reaparecia depois do alt-tab);
    /// o cursor visível é o SoftCursor, desenhado pelo jogo na posição real do mouse.
    /// </summary>
    void SetupCursor(string[] args)
    {
        var img = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        var tex = ImageTexture.CreateFromImage(img);
        foreach (Input.CursorShape shape in System.Enum.GetValues(typeof(Input.CursorShape)))
            Input.SetCustomMouseCursor(tex, shape, Vector2.Zero);
        if (args.Contains("--screenshot") || args.Contains("--smoke-test") || args.Contains("--smoke-e2") || args.Contains("--smoke-e3") || args.Contains("--smoke-block") || args.Any(a => a.StartsWith("--net-smoke"))) return;
        var layer = new CanvasLayer { Layer = 128 };
        AddChild(layer);
        layer.AddChild(new SoftCursor { Name = "SoftCursor" });
    }

    /// <summary>Único lugar que define o modo do mouse: preso jogando; visível em menus, pausa e fim de jogo.</summary>
    void UpdateMouse()
    {
        if (!DisplayServer.WindowIsFocused()) return;
        bool capture = _match != null && IsInstanceValid(_match) && _match.WantsCapturedMouse;
        var want = capture ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        if (_mouseRefresh > 0)
        {
            // ao voltar do alt-tab: reaplica o modo (passa por outro modo e volta) para o Windows
            // refazer a captura/esconder o cursor — setar o mesmo modo não faz nada.
            _mouseRefresh--;
            if (_mouseRefresh == 3) { Input.MouseMode = capture ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Hidden; return; }
            if (_mouseRefresh == 2) { Input.MouseMode = want; return; }
        }
        if (Input.MouseMode != want) Input.MouseMode = want;
    }

    public override void _Process(double delta)
    {
        UpdateMouse();
        if (_toastT > 0)
        {
            _toastT -= (float)delta;
            _toast.Modulate = new Color(1, 1, 1, Mathf.Clamp(_toastT / 0.5f, 0, 1));
        }
    }
}
