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

        _ui = new CanvasLayer { Layer = 20 };
        AddChild(_ui);
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
        int si = Array.IndexOf(args, "--screenshot");
        if (si >= 0 && si + 1 < args.Length)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(1600, 900));
            if (args.Contains("--menu"))
            {
                ShowMainMenu();
                GetTree().CreateTimer(1.5).Timeout += () =>
                {
                    GetViewport().GetTexture().GetImage().SavePng(args[si + 1]);
                    GetTree().Quit();
                };
                return;
            }
            StartMatch(new MatchConfig { AutoPilotLocal = true, ScreenshotPath = args[si + 1], Size = CourtSize.Small, AllyDummies = 1, EnemyDummies = 1 });
            return;
        }
        if (args.Contains("--smoke-test"))
        {
            Engine.TimeScale = 2.0;
            StartMatch(new MatchConfig { SmokeTest = true, Size = CourtSize.Medium, TargetScore = 99, AllyDummies = 0, EnemyDummies = 0 });
            return;
        }
        if (args.Contains("--smoke-e2"))
        {
            Engine.TimeScale = 2.0;
            StartMatch(new MatchConfig { SmokeE2 = true, Size = CourtSize.Small, TargetScore = 99, AllyDummies = 1, EnemyDummies = 1 });
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
                var plan = ShotMath.Plan(feet, 0, 99f, yaw, ShotMath.PitchForDistance(d), g);
                var pos = plan.Pos; var vel = plan.Vel;
                bool hitRim = false, hitBoard = false, scored = false;
                for (int k = 0; k < 400; k++)
                {
                    var h = BallPhys.Simulate(ref pos, ref vel, 1f / 60f, g);
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
    }

    public void ShowMainMenu()
    {
        FreeMatch();
        CloseMenu();
        Input.MouseMode = Input.MouseModeEnum.Visible;
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

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed(Act.ReloadTuning))
        {
            string msg = Tuning.Load();
            Toast(msg + (_match != null ? "  · tamanhos de quadra/aro valem na próxima partida" : ""));
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            if (_match != null && IsInstanceValid(_match)) _match.OnFocusLost();
        }
        else if (what == NotificationApplicationFocusIn)
        {
            CameraRig.IgnoreMouseUntilMs = Time.GetTicksMsec() + 250;
        }
    }

    public override void _Process(double delta)
    {
        if (_toastT > 0)
        {
            _toastT -= (float)delta;
            _toast.Modulate = new Color(1, 1, 1, Mathf.Clamp(_toastT / 0.5f, 0, 1));
        }
    }
}
