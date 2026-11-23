using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AB;

public enum MatchPhase { Countdown, Playing, Scored, Ended }
public enum WinMode { Points, Time }

public sealed class MatchConfig
{
    public CourtSize Size = CourtSize.Medium;
    public WinMode Mode = WinMode.Points;
    public int TargetScore = 21;
    public float TimeMinutes = 5f;
    public bool SmokeTest;
    public bool AutoPilotLocal;      // jogador local controlado pelo piloto (screenshot/demo)
    public string ScreenshotPath;    // se definido: tira print durante a carga do arremesso e fecha
}

/// <summary>
/// Partida: regras, rounds, placar. Roda como AUTORIDADE (host / offline).
/// Todos os jogadores são simulados aqui a partir de PlayerInput.
/// </summary>
public partial class Match : Node3D
{
    public MatchConfig Cfg = new();
    public CourtGeo Geo;
    public Ball Ball;
    public readonly List<Player> Players = new();
    public Player LocalPlayer;
    public Hud Hud;
    public CameraRig Cam;

    public MatchPhase Phase;
    public float PhaseTimer;
    public readonly int[] Score = new int[2];
    public float Clock;
    public bool Overtime;
    public int Possession;
    public int Winner = -1;

    TrajectoryPreview _preview;
    readonly LocalInputReader _reader = new();
    PauseMenu _pause;
    AutoPilot _smoke;
    int _lastCountShown = -1;
    float _stuckTimer;
    float _smokeTime;
    int _nextScoringTeamPossession;

    public bool CanMove => Phase is MatchPhase.Playing or MatchPhase.Scored;
    public bool CanAct => Phase == MatchPhase.Playing;
    public bool InputBlocked => (_pause != null && _pause.IsOpen) || Phase == MatchPhase.Ended;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        Geo = CourtGeo.Build(Cfg.Size);
        CourtBuilder.Build(this, Geo);

        Ball = new Ball { M = this, Name = "Ball" };
        AddChild(Ball);

        LocalPlayer = SpawnPlayer(0, "Você", isLocal: true);

        Cam = new CameraRig { Target = LocalPlayer, Name = "CameraRig" };
        AddChild(Cam);
        _preview = new TrajectoryPreview { Name = "Trajectory" };
        AddChild(_preview);
        Hud = new Hud { M = this, Name = "Hud" };
        AddChild(Hud);
        _pause = new PauseMenu { M = this, Name = "Pause" };
        AddChild(_pause);

        Clock = Cfg.TimeMinutes * 60f;
        if (Cfg.SmokeTest || Cfg.AutoPilotLocal) _smoke = new AutoPilot(this, LocalPlayer);
        StartRound(0, null);
        if (!Cfg.SmokeTest && !Cfg.AutoPilotLocal) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    Player SpawnPlayer(int team, string name, bool isLocal = false, bool isBot = false)
    {
        var p = new Player
        {
            Id = Players.Count + 1, Team = team, PlayerName = name, IsLocalHuman = isLocal, IsBot = isBot, M = this,
            Name = "Player" + (Players.Count + 1),
        };
        AddChild(p);
        Players.Add(p);
        return p;
    }

    public IEnumerable<Player> TeamPlayers(int team) => Players.Where(p => p.Team == team);

    // ------------------------------------------------------------------ rounds

    void StartRound(int team, Player carrier)
    {
        if (!TeamPlayers(team).Any()) team = 1 - team; // treino solo: bola sempre volta para quem existe
        Possession = team;
        carrier ??= TeamPlayers(team).First();

        PlaceTeams(team, carrier);
        Ball.GiveTo(carrier);
        Phase = MatchPhase.Countdown;
        PhaseTimer = Tuning.I.CountdownSeconds;
        _lastCountShown = -1;
        if (LocalPlayer != null) Cam.SnapBehind(LocalPlayer.Yaw);
        if (PhaseTimer <= 0) BeginPlay();
    }

    void PlaceTeams(int offenseTeam, Player carrier)
    {
        for (int team = 0; team < 2; team++)
        {
            bool offense = team == offenseTeam;
            float attackSign = CourtGeo.HoopSign(CourtGeo.AttackHoop(team));
            float yaw = ShotMath.YawOf(new Vector3(attackSign, 0, 0));
            var list = TeamPlayers(team).OrderBy(p => p == carrier ? 0 : 1).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                float x, z;
                if (offense)
                {
                    x = -attackSign * (i == 0 ? 1.5f : 0.6f);
                    z = i == 0 ? 0 : Spread(i, Geo.HalfW);
                }
                else
                {
                    x = -attackSign * 3.2f; // na própria metade, entre o centro e a cesta que defendem
                    z = Spread(i + 1, Geo.HalfW);
                    if (list.Count == 1) z = 0;
                }
                list[i].Teleport(new Vector3(x, 0.05f, z), yaw);
            }
        }
    }

    static float Spread(int i, float halfW)
    {
        // 1 -> +2.2, 2 -> -2.2, 3 -> +4.4, ...
        float step = Mathf.Min(2.2f, halfW * 0.4f);
        int k = (i + 1) / 2;
        float sign = i % 2 == 1 ? 1f : -1f;
        return Mathf.Clamp(sign * k * step, -halfW + 0.5f, halfW - 0.5f);
    }

    void BeginPlay()
    {
        Phase = MatchPhase.Playing;
        Hud?.ShowCountdown("VAI!", 0.7f);
        Sfx.Play2D(this, "go", -4);
    }

    // ------------------------------------------------------------------ tick

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        switch (Phase)
        {
            case MatchPhase.Countdown:
                PhaseTimer -= dt;
                int n = Mathf.CeilToInt(PhaseTimer);
                if (n != _lastCountShown && n > 0)
                {
                    _lastCountShown = n;
                    Hud?.ShowCountdown(n.ToString(), 1.0f);
                    Sfx.Play2D(this, "beep", -6);
                }
                if (PhaseTimer <= 0) BeginPlay();
                break;
            case MatchPhase.Playing:
                if (Cfg.Mode == WinMode.Time && !Overtime)
                {
                    Clock -= dt;
                    if (Clock <= 0) OnTimeUp();
                }
                break;
            case MatchPhase.Scored:
                PhaseTimer -= dt;
                if (PhaseTimer <= 0)
                {
                    if (Winner >= 0) EndMatch();
                    else StartRound(_nextScoringTeamPossession, null);
                }
                break;
        }

        foreach (var p in Players)
        {
            PlayerInput inp = default;
            if (p == LocalPlayer)
                inp = _smoke != null ? _smoke.Build(dt) : _reader.Build(Cam, !InputBlocked);
            p.ServerTick(inp, dt);
        }

        var hit = Ball.ServerTick(dt);
        HandleBallHit(hit);
        if (CanAct && Ball.State != BallState.Held) TryPickup();
        CheckStuckBall(dt);

        if (Cfg.SmokeTest) SmokeTick(dt);
    }

    void HandleBallHit(BallHit hit)
    {
        var pos = Ball.GlobalPosition;
        float vol(float imp) => Mathf.Clamp(Mathf.LinearToDb(Mathf.Clamp(imp / 8f, 0.05f, 1f)), -30, 0);
        if (hit.Rim) Sfx.Play3D(this, "rim", pos, vol(hit.Impact) + 2);
        else if (hit.Board || hit.Pole) Sfx.Play3D(this, "board", pos, vol(hit.Impact));
        if (hit.Floor && hit.Impact > 1.0f) Sfx.Play3D(this, "bounce", pos, vol(hit.Impact));
        if (hit.ScoredHoop >= 0 && Phase == MatchPhase.Playing) OnScore(hit.ScoredHoop);
    }

    void OnScore(int hoop)
    {
        var t = Tuning.I;
        int team = CourtGeo.ScoringTeamForHoop(hoop);
        bool three = Ball.State == BallState.Shot && Geo.IsThree(Ball.ShotOrigin, hoop);
        int pts = Mathf.RoundToInt(three ? t.PointsOutside : t.PointsInside);
        Score[team] += pts;
        Ball.Vel = new Vector3(Ball.Vel.X * 0.15f, Ball.Vel.Y * 0.4f, Ball.Vel.Z * 0.15f);
        Sfx.Play3D(this, "swish", Geo.RimCenter[hoop], -2);
        Sfx.Play2D(this, "score", -7);
        Hud?.ShowMessage($"+{pts}  {Palette.TeamName[team]}!", 1.6f, color: Palette.Team[team]);
        GD.Print($"[SCORE] time {team} +{pts} ({(three ? "3pt" : "2pt")}) placar {Score[0]} x {Score[1]}");

        _nextScoringTeamPossession = 1 - team;
        Phase = MatchPhase.Scored;
        PhaseTimer = t.PostScoreDelay;
        if ((Cfg.Mode == WinMode.Points && Score[team] >= Cfg.TargetScore) || (Cfg.Mode == WinMode.Time && Overtime))
            Winner = team;
    }

    void OnTimeUp()
    {
        Clock = 0;
        Sfx.Play2D(this, "horn", -4);
        if (Score[0] == Score[1])
        {
            Overtime = true;
            Hud?.ShowMessage("PRORROGAÇÃO! Próxima cesta vence", 2.5f);
            return;
        }
        Winner = Score[0] > Score[1] ? 0 : 1;
        EndMatch();
    }

    void EndMatch()
    {
        Phase = MatchPhase.Ended;
        Sfx.Play2D(this, "win", -5);
        Hud?.ShowEnd(Winner);
        if (!Cfg.SmokeTest) Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    void TryPickup()
    {
        Player best = null;
        float bestD = float.MaxValue;
        foreach (var p in Players)
        {
            if (!Ball.CanBeCaughtBy(p)) continue;
            float d = p.GlobalPosition.DistanceSquaredTo(Ball.GlobalPosition);
            if (d < bestD) { bestD = d; best = p; }
        }
        if (best == null) return;
        Ball.GiveTo(best);
        Sfx.Play3D(this, "catch", Ball.GlobalPosition, -4);
    }

    void CheckStuckBall(float dt)
    {
        if (Ball.State == BallState.Held || Ball.GlobalPosition.Y < 1.0f || Ball.Vel.Length() > 0.1f) { _stuckTimer = 0; return; }
        _stuckTimer += dt;
        if (_stuckTimer > 2f)
        {
            _stuckTimer = 0;
            Ball.Drop(Ball.GlobalPosition + Vector3.Up * 0.1f, new Vector3(0, 1f, 0) - new Vector3(Ball.GlobalPosition.X, 0, Ball.GlobalPosition.Z).Normalized() * 2f);
        }
    }

    public override void _Process(double delta)
    {
        _preview.UpdateFor(LocalPlayer != null && LocalPlayer.ChargingShot && !Cfg.SmokeTest ? LocalPlayer : null, Geo);
        if (Cfg.ScreenshotPath != null) ScreenshotTick((float)delta);
    }

    float _shotT;
    int _shotIdx;
    float _sinceRelease = -1;
    bool _wasCharging;
    /// <summary>Modo --screenshot: salva 4 prints (parado, andando, carregando, soltando) e fecha.</summary>
    void ScreenshotTick(float dt)
    {
        _shotT += dt;
        var lp = LocalPlayer;
        if (_wasCharging && !lp.ChargingShot) _sinceRelease = 0;
        else if (_sinceRelease >= 0) _sinceRelease += dt;
        _wasCharging = lp.ChargingShot;
        if (_shotT > 1.2f) Cam.Yaw = Mathf.LerpAngle(Cam.Yaw, lp.Yaw + 0.5f, 0.1f);

        bool take = _shotIdx switch
        {
            0 => _shotT > 2.0f,                                                    // parado quicando (contagem)
            1 => CanAct && lp.HasBall && !lp.ChargingShot && lp.Velocity.Length() > 2.5f, // andando
            2 => lp.ChargingShot && lp.ShotCharge > 0.3f,                          // carregando
            3 => _sinceRelease > 0.12f && _sinceRelease < 0.4f,                    // soltando
            _ => false,
        };
        if (_shotT > 40f) take = true;
        if (!take) return;
        string path = Cfg.ScreenshotPath.Replace(".png", $"_{_shotIdx}.png");
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print("[SHOT] salvo " + path);
        _shotIdx++;
        if (_shotIdx >= 4 || _shotT > 40f) { Cfg.ScreenshotPath = null; GetTree().Quit(); }
    }

    /// <summary>Janela perdeu o foco (alt-tab): pausa a partida offline.</summary>
    public void OnFocusLost()
    {
        if (Cfg.SmokeTest || Cfg.AutoPilotLocal || Phase == MatchPhase.Ended || _pause == null || _pause.IsOpen) return;
        _pause.Open();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed && !InputBlocked && Input.MouseMode != Input.MouseModeEnum.Captured && !Cfg.SmokeTest)
            Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    // ------------------------------------------------------------------ teste automático

    void SmokeTick(float dt)
    {
        _smokeTime += dt;
        int total = Score[0] + Score[1];
        if (total >= 6 || _smokeTime > 90f || Phase == MatchPhase.Ended)
        {
            bool ok = total > 0;
            GD.Print($"[SMOKE] fim: arremessos={_smoke.Shots} placar={Score[0]}x{Score[1]} tempo={_smokeTime:0.0}s -> {(ok ? "OK" : "FALHOU")}");
            GetTree().Quit(ok ? 0 : 1);
            Cfg.SmokeTest = false;
        }
    }
}
