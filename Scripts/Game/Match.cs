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
    public int AllyDummies = 1;      // bonecos de treino no seu time
    public int EnemyDummies = 1;     // bonecos de treino no time adversário
    public bool SmokeTest;           // --smoke-test (arremessos)
    public bool SmokeE2;             // --smoke-e2 (passe, drible, bloqueio, roubo)
    public bool AutoPilotLocal;      // jogador local controlado pelo piloto (screenshot/demo)
    public string ScreenshotPath;    // se definido: tira prints e fecha

    public MatchConfig Clone() => (MatchConfig)MemberwiseClone();
}

/// <summary>
/// Partida: regras, rounds, placar. Roda como AUTORIDADE (host / offline).
/// Todos os jogadores são simulados aqui a partir de PlayerInput (humano, boneco, bot ou remoto).
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
    readonly Dictionary<Player, DummyBrain> _dummies = new();
    PauseMenu _pause;
    AutoPilot _smoke;
    ScenarioPilot _scenario;
    int _lastCountShown = -1;
    float _stuckTimer;
    float _smokeTime;
    int _nextPossession;

    public bool CanMove => Phase is MatchPhase.Playing or MatchPhase.Scored;
    public bool CanAct => Phase == MatchPhase.Playing;
    public bool InputBlocked => (_pause != null && _pause.IsOpen) || Phase == MatchPhase.Ended;
    public bool Automated => Cfg.SmokeTest || Cfg.SmokeE2 || Cfg.AutoPilotLocal;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        Geo = CourtGeo.Build(Cfg.Size);
        CourtBuilder.Build(this, Geo);

        Ball = new Ball { M = this, Name = "Ball" };
        AddChild(Ball);

        LocalPlayer = SpawnPlayer(0, "Você", isLocal: true);
        int max = CourtGeo.MaxPlayers(Cfg.Size);
        int allies = Mathf.Clamp(Cfg.AllyDummies, 0, max - 1);
        int enemies = Mathf.Clamp(Cfg.EnemyDummies, 0, max - 1 - allies);
        for (int i = 0; i < allies; i++) AddDummy(0, $"Aliado {i + 1}");
        for (int i = 0; i < enemies; i++) AddDummy(1, $"Adversário {i + 1}");

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
        if (Cfg.SmokeE2) _scenario = new ScenarioPilot(this, LocalPlayer);
        StartRound(0, null, countdown: true);
    }

    Player SpawnPlayer(int team, string name, bool isLocal = false, bool isDummy = false)
    {
        var p = new Player
        {
            Id = Players.Count + 1, Team = team, PlayerName = name, IsLocalHuman = isLocal, IsDummy = isDummy, M = this,
            Name = "Player" + (Players.Count + 1),
        };
        AddChild(p);
        Players.Add(p);
        return p;
    }

    void AddDummy(int team, string name)
    {
        var p = SpawnPlayer(team, name, isDummy: true);
        _dummies[p] = new DummyBrain(this, p);
    }

    public IEnumerable<Player> TeamPlayers(int team) => Players.Where(p => p.Team == team);

    // ------------------------------------------------------------------ rounds

    /// <summary>Reinicia o round. A contagem regressiva só acontece no início da partida.</summary>
    void StartRound(int team, Player carrier, bool countdown)
    {
        if (!TeamPlayers(team).Any()) team = 1 - team;
        Possession = team;
        carrier ??= TeamPlayers(team).OrderBy(p => p.IsLocalHuman ? 0 : 1).First();

        PlaceTeams(team, carrier);
        Ball.GiveTo(carrier);
        if (LocalPlayer != null) Cam.SnapBehind(LocalPlayer.Yaw);
        if (countdown && Tuning.I.CountdownSeconds > 0)
        {
            Phase = MatchPhase.Countdown;
            PhaseTimer = Tuning.I.CountdownSeconds;
            _lastCountShown = -1;
        }
        else Phase = MatchPhase.Playing;
    }

    void PlaceTeams(int offenseTeam, Player carrier)
    {
        for (int team = 0; team < 2; team++)
        {
            bool offense = team == offenseTeam;
            float attackSign = CourtGeo.HoopSign(CourtGeo.AttackHoop(team));
            float yaw = ShotMath.YawOf(new Vector3(attackSign, 0, 0));
            var list = TeamPlayers(team).OrderBy(p => p == carrier ? 0 : p.IsLocalHuman ? 1 : 2).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                float x, z;
                if (offense)
                {
                    x = -attackSign * (i == 0 ? 2.0f : 0.5f) + attackSign * (i > 0 ? 3f : 0f);
                    z = i == 0 ? 0 : Spread(i, Geo.HalfW);
                }
                else
                {
                    x = attackSign * -(Geo.HalfL * 0.45f); // na própria metade, perto da cesta que defendem
                    z = Spread(i + 1, Geo.HalfW);
                    if (list.Count == 1) z = 0;
                }
                list[i].Teleport(new Vector3(x, 0.05f, z), yaw);
            }
        }
    }

    static float Spread(int i, float halfW)
    {
        float step = Mathf.Min(4f, halfW * 0.35f);
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
                    else StartRound(_nextPossession, null, countdown: false);
                }
                break;
        }

        foreach (var p in Players)
        {
            PlayerInput inp = default;
            if (p == LocalPlayer)
            {
                if (_scenario != null) inp = _scenario.Build(dt);
                else if (_smoke != null) inp = _smoke.Build(dt);
                else inp = _reader.Build(Cam, !InputBlocked);
            }
            else if (_dummies.TryGetValue(p, out var brain)) inp = brain.Build(dt);
            p.ServerTick(inp, dt);
        }

        var hit = Ball.ServerTick(dt);
        HandleBallHit(hit);
        if (CanMove && Ball.State is BallState.Shot or BallState.HighPass) CheckBlocks();
        if (CanAct && Ball.State != BallState.Held) TryPickup();
        CheckStuckBall(dt);

        if (Cfg.SmokeTest) SmokeTick(dt);
        if (Cfg.SmokeE2) SmokeE2Tick(dt);
    }

    void HandleBallHit(BallHit hit)
    {
        var pos = Ball.GlobalPosition;
        float vol(float imp) => Mathf.Clamp(Mathf.LinearToDb(Mathf.Clamp(imp / 8f, 0.05f, 1f)), -30, 0);
        if (hit.Rim) Sfx.Play3D(this, "rim", pos, vol(hit.Impact) + 2);
        else if (hit.Board || hit.Pole) Sfx.Play3D(this, "board", pos, vol(hit.Impact));
        if (hit.Floor && hit.Impact > 1.0f) Sfx.Play3D(this, "bounce", pos, vol(hit.Impact));
        if (hit.Rim) LogEvent("RIM", null);
        if (hit.Board) LogEvent("BOARD", null);
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

        _nextPossession = 1 - team;
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
    }

    // ------------------------------------------------------------------ bola: bloqueio e recepção

    /// <summary>Jogador no ar bloqueia arremessos e passes altos do adversário: a bola quica nele e cai.</summary>
    void CheckBlocks()
    {
        var t = Tuning.I;
        foreach (var p in Players)
        {
            if (!p.Airborne || p.Team == Ball.ThrowTeam) continue;
            var rel = Ball.GlobalPosition - p.GlobalPosition;
            float hd = new Vector2(rel.X, rel.Z).Length();
            if (hd > t.BlockRadius + t.BallRadius || rel.Y < 0.8f || rel.Y > t.BlockReach + t.BallRadius) continue;
            var away = new Vector3(rel.X, 0, rel.Z);
            away = away.LengthSquared() > 1e-4f ? away.Normalized() : -p.Forward;
            Ball.Drop(Ball.GlobalPosition, away * t.BlockBounceSpeed + Vector3.Up * 1.2f);
            p.BallLockoutTimer = 0.5f; // o bloqueador não pega a bola
            Sfx.Play3D(this, "board", Ball.GlobalPosition, -2, 0.8f);
            LogEvent("BLOCK", p);
            return;
        }
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
        bool intercepted = Ball.State == BallState.Pass && Ball.ThrowTeam != best.Team;
        Ball.GiveTo(best);
        Sfx.Play3D(this, "catch", Ball.GlobalPosition, -4);
        LogEvent(intercepted ? "INTERCEPT" : "CATCH", best);
        if (intercepted) LogEvent("CATCH", best);
    }

    public void OnSteal(Player thief, Player victim)
    {
        Sfx.Play3D(this, "catch", thief.GlobalPosition + Vector3.Up, 0, 1.3f);
        LogEvent("STEAL", thief, victim);
    }

    public void OnStealFailed(Player thief, Player immuneTarget)
    {
        LogEvent("STEAL_FAIL", thief, immuneTarget);
    }

    public void OnLanded(Player p) => LogEvent("LAND", p);

    public void LogEvent(string kind, Player a, Player b = null)
    {
        if (Automated) GD.Print($"[EVENT] {kind} {a?.PlayerName}{(b != null ? " -> " + b.PlayerName : "")}");
        _scenario?.OnEvent(kind, a, b);
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

    // ------------------------------------------------------------------ visual local

    public override void _Process(double delta)
    {
        var lp = LocalPlayer;
        _preview.UpdateFor(lp != null && lp.ChargingShot && !Cfg.SmokeTest ? lp : null, Geo);

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
            0 => _shotT > 2.0f,
            1 => CanAct && lp.HasBall && !lp.ChargingShot && lp.Velocity.Length() > 2.5f,
            2 => lp.ChargingShot && lp.ShotCharge > 0.3f,
            3 => _sinceRelease > 0.12f && _sinceRelease < 0.4f,
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
        if (Automated || Phase == MatchPhase.Ended || _pause == null || _pause.IsOpen) return;
        _pause.Open();
    }

    /// <summary>O cursor deve ficar escondido/preso (jogando, sem pausa nem tela de fim). Aplicado pelo GameRoot.</summary>
    public bool WantsCapturedMouse => !Automated && !InputBlocked;

    // ------------------------------------------------------------------ testes automáticos

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

    void SmokeE2Tick(float dt)
    {
        _smokeTime += dt;
        if (!_scenario.Done && _smokeTime < 70f) return;
        var s = _scenario;
        bool ok = s.PassOk && s.ReturnOk && s.AimPassOk && s.DribbleOk && s.JumpOk && s.StealOk;
        GD.Print($"[E2] passe={s.PassOk} devolução={s.ReturnOk} passe_carregado={s.AimPassOk} drible={s.DribbleOk} boneco_pulou={s.JumpOk} toco={s.BlockOk} " +
                 $"roubo_falhou(imune)={s.StealFailOk} roubo={s.StealOk} -> {(ok ? "OK" : "FALHOU")}");
        GetTree().Quit(ok ? 0 : 1);
        Cfg.SmokeE2 = false;
    }
}
