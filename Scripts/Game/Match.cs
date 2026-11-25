using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AB;

public enum MatchPhase { Countdown, Playing, Scored, FreeThrow, Ended }
public enum WinMode { Points, Time }

public sealed class MatchConfig
{
    public CourtSize Size = CourtSize.Medium;
    public WinMode Mode = WinMode.Points;
    public int TargetScore = 21;
    public float TimeMinutes = 5f;
    public int AllyDummies = 1;      // bonecos de treino no seu time
    public int EnemyDummies = 2;     // bonecos de treino no time adversário
    public bool TrainingDummies;     // true = bonecos de treino; false = bots (os campos acima são as quantidades)
    public int BotDifficulty = 1;    // 0 fácil, 1 normal, 2 difícil
    public bool SmokeE4;             // --smoke-e4 (partida só de bots)
    public bool SmokeTest;           // --smoke-test (arremessos)
    public bool SmokeE2;             // --smoke-e2 (passe, drible, bloqueio, roubo)
    public bool SmokeE3;             // --smoke-e3 (enterrada, skill check, falta)
    public bool SmokeBlock;          // --smoke-block (bot bloqueando arremessos)
    public bool AutoPilotLocal;      // jogador local controlado pelo piloto (screenshot/demo)
    public string ScreenshotPath;    // se definido: tira prints e fecha
    // ---- online (Etapa 5) ----
    public NetRole Net;              // None = offline; Host = simula e manda snapshots; Client = só mostra e manda input
    public List<RosterEntry> Roster; // escalação online (mesma ordem em todos → mesmos Player.Id)
    public int MyNetId;
    public bool SmokeNet;            // --net-smoke-* (bot controla o jogador local dos dois lados)

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
    Rumble _rumble;
    readonly LocalInputReader _reader = new();
    readonly Dictionary<Player, DummyBrain> _dummies = new();
    readonly Dictionary<Player, BotBrain> _bots = new();
    public readonly Dictionary<string, int> EventCount = new();
    public readonly Dictionary<string, int> RemoteEvents = new();   // eventos de jogadores remotos (teste de rede)
    PauseMenu _pause;
    AutoPilot _smoke;
    ScenarioPilot _scenario;
    DunkScenario _dunkScenario;
    BlockScenario _blockScenario;
    int _lastCountShown = -1;
    float _stuckTimer;
    float _smokeTime;
    int _nextPossession;
    Player _pendingFt;                // falta: quem sofreu cobra 2 lances livres depois da pausa

    // ---- lances livres (v0.7): 2 skill checks; acertou = arremesso automático que sempre entra ----
    enum FtStage { Check, Flight, Gap }
    public Player FtShooter;          // quem está cobrando (null fora dos lances livres)
    public Player FtFlashPlayer;      // de quem é o flash do resultado (continua depois do fim dos lances)
    public int FtAttempt;             // 1 ou 2
    public float FtT, FtDur;          // FtT < 0 = preparação; o ponteiro chega na zona certa em FtT = FtDur
    public bool FtPressed;
    public float FtPressT;
    public float FtFlash;             // >0: mostra o resultado do skill check (verde/vermelho)
    public bool FtFlashOk;
    public bool FtChecking => IsClient ? _netFtChecking : Phase == MatchPhase.FreeThrow && _ftStage == FtStage.Check;
    bool _netFtChecking;
    FtStage _ftStage;
    float _ftStageT;
    PlayerInput _ftInput;

    public bool CanMove => Phase is MatchPhase.Playing or MatchPhase.Scored;
    public bool CanAct => Phase == MatchPhase.Playing;
    public bool InputBlocked => (_pause != null && _pause.IsOpen) || Phase == MatchPhase.Ended;
    public bool Automated => Cfg.SmokeTest || Cfg.SmokeE2 || Cfg.SmokeE3 || Cfg.SmokeE4 || Cfg.SmokeBlock || Cfg.SmokeNet || Cfg.AutoPilotLocal;
    public bool IsOnline => Cfg.Net != NetRole.None;
    public bool IsHost => Cfg.Net == NetRole.Host;
    public bool IsClient => Cfg.Net == NetRole.Client;
    readonly Dictionary<Player, int> _remote = new();   // host: jogador → NetId do cliente que o controla

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        Geo = CourtGeo.Build(Cfg.Size);
        CourtBuilder.Build(this, Geo);

        Ball = new Ball { M = this, Name = "Ball" };
        AddChild(Ball);

        if (Cfg.Roster != null) SpawnRoster();
        else
        {
            LocalPlayer = SpawnPlayer(0, "Você", isLocal: true);
            int max = CourtGeo.MaxPlayers(Cfg.Size);
            int allies = Mathf.Clamp(Cfg.AllyDummies, 0, max - 1);
            int enemies = Mathf.Clamp(Cfg.EnemyDummies, 0, max - 1 - allies);
            for (int i = 0; i < allies; i++) AddOther(0, $"Aliado {i + 1}");
            for (int i = 0; i < enemies; i++) AddOther(1, $"Adversário {i + 1}");
            if (Cfg.SmokeE4) _bots[LocalPlayer] = new BotBrain(this, LocalPlayer, 999);
        }

        Cam = new CameraRig { Target = LocalPlayer ?? Players.FirstOrDefault(), Name = "CameraRig" };
        AddChild(Cam);
        _preview = new TrajectoryPreview { Name = "Trajectory" };
        AddChild(_preview);
        Hud = new Hud { M = this, Name = "Hud" };
        AddChild(Hud);
        AddChild(new ImpactFx { M = this, Name = "ImpactFx" });
        if (LocalPlayer != null && !Automated)
        {
            var layer = new CanvasLayer { Layer = 6, Name = "EmoteLayer" };
            AddChild(layer);
            layer.AddChild(new EmoteWheel { M = this, Name = "EmoteWheel" });
        }
        _pause = new PauseMenu { M = this, Name = "Pause" };
        AddChild(_pause);

        Clock = Cfg.TimeMinutes * 60f;
        if (Cfg.SmokeTest || Cfg.AutoPilotLocal) _smoke = new AutoPilot(this, LocalPlayer);
        if (Cfg.SmokeE2) _scenario = new ScenarioPilot(this, LocalPlayer);
        if (Cfg.SmokeE3) _dunkScenario = new DunkScenario(this, LocalPlayer);
        if (Cfg.SmokeBlock) _blockScenario = new BlockScenario(this, LocalPlayer);
        if (IsOnline) NetSession.I?.RegisterMatch(this);
        if (IsClient)
        {
            // cliente: não simula — tudo vem dos snapshots do host
            Phase = MatchPhase.Countdown;
            if (LocalPlayer != null) Cam.SnapBehind(ShotMath.YawOf(new Vector3(CourtGeo.HoopSign(CourtGeo.AttackHoop(LocalPlayer.Team)), 0, 0)));
            if (Cfg.SmokeNet && LocalPlayer != null) _clientBot = new BotBrain(this, LocalPlayer, 4321);
            return;
        }
        StartRound(0, null, countdown: true);
    }

    /// <summary>Online: cria os jogadores na ordem da escalação. Host: bots têm cérebro; humanos remotos usam o input da rede.</summary>
    void SpawnRoster()
    {
        foreach (var e in Cfg.Roster)
        {
            bool mine = !e.IsBot && e.NetId == Cfg.MyNetId;
            var p = SpawnPlayer(e.Team, e.Name, isLocal: mine);
            p.IsBot = e.IsBot;
            if (mine) LocalPlayer = p;
            if (!IsHost) continue;
            if (e.IsBot) _bots[p] = new BotBrain(this, p, (ulong)(p.Id * 7919 + 17));
            else if (!mine) _remote[p] = e.NetId;
        }
        if (IsHost && Cfg.SmokeNet && LocalPlayer != null) _bots[LocalPlayer] = new BotBrain(this, LocalPlayer, 999);
    }

    /// <summary>Host: um cliente saiu no meio da partida → um bot assume o jogador dele.</summary>
    public void OnRemoteLeft(int netId, string name)
    {
        var p = _remote.FirstOrDefault(kv => kv.Value == netId).Key;
        if (p == null) return;
        _netLeftPackets = NetSession.I.InputPackets(netId);
        _netLeftName = p.PlayerName;
        _remote.Remove(p);
        p.IsBot = true;
        _bots[p] = new BotBrain(this, p, (ulong)(p.Id * 7919 + 17));
        GameRoot.I.Toast($"{name} saiu — um bot assumiu o lugar.");
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

    void AddOther(int team, string name)
    {
        if (Cfg.TrainingDummies) { AddDummy(team, name); return; }
        var p = SpawnPlayer(team, name);
        p.IsBot = true;
        _bots[p] = new BotBrain(this, p, (ulong)(p.Id * 7919 + 17));
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
                list[i].ResetForRound(); // v0.9: estamina e cooldowns zerados a cada round
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

    // ---- HUD: no host também vai para os clientes ----
    void UiCountdown(string text, float dur)
    {
        Hud?.ShowCountdown(text, dur);
        if (IsHost) NetSession.I?.HostHud(0, text, dur, Colors.White, false, -1);
    }

    void UiMessage(string text, float dur, Color color)
    {
        Hud?.ShowMessage(text, dur, color: color);
        if (IsHost) NetSession.I?.HostHud(1, text, dur, color, false, -1);
    }

    void UiEnd(int winner)
    {
        Hud?.ShowEnd(winner);
        if (IsHost) NetSession.I?.HostHud(2, "", 0, Colors.White, false, winner);
    }

    void BeginPlay()
    {
        Phase = MatchPhase.Playing;
        UiCountdown("VAI!", 0.7f);
        Sfx.Play2D(this, "go", -4);
    }

    // ------------------------------------------------------------------ tick

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (IsClient) { ClientPhysics(dt); return; }
        switch (Phase)
        {
            case MatchPhase.Countdown:
                PhaseTimer -= dt;
                int n = Mathf.CeilToInt(PhaseTimer);
                if (n != _lastCountShown && n > 0)
                {
                    _lastCountShown = n;
                    UiCountdown(n.ToString(), 1.0f);
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
                    else if (_pendingFt != null && IsInstanceValid(_pendingFt)) StartFreeThrows(_pendingFt);
                    else StartRound(_nextPossession, null, countdown: false);
                    _pendingFt = null;
                }
                break;
        }

        foreach (var p in Players)
        {
            PlayerInput inp = default;
            if (p == LocalPlayer)
            {
                if (_bots.TryGetValue(p, out var lb)) inp = lb.Build(dt);
                else if (_scenario != null) inp = _scenario.Build(dt);
                else if (_dunkScenario != null) inp = _dunkScenario.Build(dt);
                else if (_blockScenario != null) inp = _blockScenario.Build(dt);
                else if (_smoke != null) inp = _smoke.Build(dt);
                else inp = _reader.Build(Cam, !InputBlocked);
            }
            else if (_dummies.TryGetValue(p, out var brain)) inp = brain.Build(dt);
            else if (_bots.TryGetValue(p, out var bot)) inp = bot.Build(dt);
            else if (_remote.TryGetValue(p, out int netId)) inp = NetSession.I.TakeInput(netId);
            if (!_remote.ContainsKey(p)) inp.SkillT = -1; // local e bots: o host julga pelo próprio tempo
            p.SkillGrace = _remote.TryGetValue(p, out int gid) ? NetSession.I.SkillGrace(gid) : 0f;
            if (p == FtShooter) _ftInput = inp;
            p.ServerTick(inp, dt);
        }
        if (FtFlash > 0) FtFlash -= dt;
        if (Phase == MatchPhase.FreeThrow) FreeThrowTick(dt);

        var hit = Ball.ServerTick(dt);
        HandleBallHit(hit);
        if (CanMove && Ball.State is BallState.Shot or BallState.HighPass) CheckBlocks();
        if (CanAct) CheckFlightBlocks();
        if (CanAct && Ball.State != BallState.Held) TryPickup();
        CheckStuckBall(dt);

        if (Cfg.SmokeTest) SmokeTick(dt);
        if (Cfg.SmokeE2) SmokeE2Tick(dt);
        if (Cfg.SmokeE3) SmokeE3Tick(dt);
        if (Cfg.SmokeE4) SmokeE4Tick(dt);
        if (Cfg.SmokeBlock) SmokeBlockTick(dt);
        if (Cfg.SmokeNet) SmokeNetHostTick(dt);
        if (IsHost) NetSession.I?.HostAfterTick(this);
    }

    void HandleBallHit(BallHit hit)
    {
        // 1º lance livre errado: o 2º começa assim que a bola toca o chão
        if (hit.Floor && Phase == MatchPhase.FreeThrow && _ftStage == FtStage.Flight && FtAttempt == 1 && FtShooter != null) BeginFtAttempt(2);
        var pos = Ball.GlobalPosition;
        float vol(float imp) => Mathf.Clamp(Mathf.LinearToDb(Mathf.Clamp(imp / 8f, 0.05f, 1f)), -30, 0);
        if (hit.Rim) Sfx.Play3D(this, "rim", pos, vol(hit.Impact) + 2);
        else if (hit.Board || hit.Pole) Sfx.Play3D(this, "board", pos, vol(hit.Impact));
        if (hit.Floor && hit.Impact > 1.0f) Sfx.Play3D(this, "bounce", pos, vol(hit.Impact));
        if (hit.Rim) LogEvent("RIM", null);
        if (hit.Board) LogEvent("BOARD", null);
        if (hit.ScoredHoop >= 0 && Phase is MatchPhase.Playing or MatchPhase.FreeThrow) OnScore(hit.ScoredHoop);
    }

    void OnScore(int hoop)
    {
        var t = Tuning.I;
        int team = CourtGeo.ScoringTeamForHoop(hoop);
        bool freeThrow = Ball.FreeThrow;
        bool three = !freeThrow && Ball.State == BallState.Shot && Geo.IsThree(Ball.ShotOrigin, hoop);
        int pts = Mathf.RoundToInt(freeThrow ? t.FreeThrowPoints : three ? t.PointsOutside : t.PointsInside);
        Ball.FreeThrow = false;
        Score[team] += pts;
        Ball.Vel = new Vector3(Ball.Vel.X * 0.15f, Ball.Vel.Y * 0.4f, Ball.Vel.Z * 0.15f);
        Sfx.Play3D(this, "swish", Geo.RimCenter[hoop], -2);
        Sfx.Play2D(this, "score", -7);
        UiMessage($"+{pts}  {Palette.TeamName[team]}!", 1.6f, Palette.Team[team]);
        LogEvent(freeThrow ? "SCORE_FT" : Ball.LastThrower != null && Ball.LastThrower.Flight == FlightKind.None && Ball.ShotOrigin.Y < 0.5f ? "SCORE_SHOT" : "SCORE_OTHER", Ball.LastThrower);
        GD.Print($"[SCORE] time {team} +{pts} ({(freeThrow ? "lance livre" : three ? "3pt" : "2pt")}) placar {Score[0]} x {Score[1]}");

        bool wins = (Cfg.Mode == WinMode.Points && Score[team] >= Cfg.TargetScore) || (Cfg.Mode == WinMode.Time && Overtime);
        if (Phase == MatchPhase.FreeThrow)
        {
            // 1º lance convertido: vai para o 2º (a não ser que já tenha vencido)
            if (!wins && FtAttempt == 1) { _ftStage = FtStage.Gap; _ftStageT = t.FreeThrowGap; return; }
            FtShooter = null;
        }
        _nextPossession = 1 - team;
        Phase = MatchPhase.Scored;
        PhaseTimer = t.PostScoreDelay;
        if (wins) Winner = team;
    }

    void OnTimeUp()
    {
        Clock = 0;
        Sfx.Play2D(this, "horn", -4);
        if (Score[0] == Score[1])
        {
            Overtime = true;
            UiMessage("PRORROGAÇÃO! Próxima cesta vence", 2.5f, Colors.White);
            return;
        }
        Winner = Score[0] > Score[1] ? 0 : 1;
        EndMatch();
    }

    void EndMatch()
    {
        Phase = MatchPhase.Ended;
        Sfx.Play2D(this, "win", -5);
        UiEnd(Winner);
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

    /// <summary>Salto do adversário na frente da mão/corpo de quem está enterrando = FALTA (round reinicia com quem sofreu, com a bola).</summary>
    void CheckFlightBlocks()
    {
        var t = Tuning.I;
        foreach (var f in Players)
        {
            if (f.Flight == FlightKind.None) continue;
            var hand = f.GlobalPosition + Vector3.Up * t.DunkHandReach + f.Forward * 0.35f;
            foreach (var p in Players)
            {
                if (!p.Airborne || p.Flight != FlightKind.None || p.Team == f.Team) continue;
                var rel = hand - p.GlobalPosition;
                float hd = new Vector2(rel.X, rel.Z).Length();
                bool handHit = hd <= t.BlockRadius + t.BallRadius + 0.2f && rel.Y >= 0.8f && rel.Y <= t.BlockReach + t.BallRadius;
                // corpo no caminho: defensor no ar cruzando a trajetória de quem voa (alturas se sobrepõem)
                var bd = f.GlobalPosition - p.GlobalPosition;
                float bodyH = new Vector2(bd.X, bd.Z).Length();
                float fy0 = f.GlobalPosition.Y + 0.8f, fy1 = f.GlobalPosition.Y + t.DunkHandReach;
                float py0 = p.GlobalPosition.Y, py1 = p.GlobalPosition.Y + t.BlockReach;
                bool bodyHit = bodyH <= 0.9f && fy0 <= py1 && py0 <= fy1;
                if (!handHit && !bodyHit) continue;
                OnFoul(f, p);
                return;
            }
        }
    }

    void OnFoul(Player victim, Player fouler)
    {
        victim.EndFlight();
        if (Ball.Holder != victim) Ball.GiveTo(victim);
        Sfx.Play2D(this, "whistle", -6);
        LogEvent("FOUL", fouler, victim);
        _nextPossession = victim.Team;
        _pendingFt = victim;          // depois da pausa: 2 lances livres
        Phase = MatchPhase.Scored;
        PhaseTimer = Tuning.I.FoulDelay;
    }

    // ------------------------------------------------------------------ lances livres

    /// <summary>Falta: quem sofreu vai para a linha e cobra 2 lances livres. Ninguém se mexe até o fim.</summary>
    void StartFreeThrows(Player shooter)
    {
        Phase = MatchPhase.FreeThrow;
        FtShooter = shooter;
        Possession = shooter.Team;
        PlaceFreeThrow(shooter);
        LogEvent("FREE_THROWS", shooter);
        BeginFtAttempt(1);
    }

    Vector3 FtSpot(Player shooter, out float yaw)
    {
        int hoop = CourtGeo.AttackHoop(shooter.Team);
        var rim = Geo.RimCenter[hoop] with { Y = 0 };
        var inward = new Vector3(-CourtGeo.HoopSign(hoop), 0, 0);
        yaw = ShotMath.YawOf(-inward);
        var spot = rim + inward * Tuning.I.FreeThrowDistance;
        return new Vector3(Mathf.Clamp(spot.X, -Geo.HalfL + 1f, Geo.HalfL - 1f), 0.05f, spot.Z);
    }

    /// <summary>Arremessador na linha; os outros nos lados do garrafão (defensores mais perto da cesta), virados para o aro.</summary>
    void PlaceFreeThrow(Player shooter)
    {
        var t = Tuning.I;
        int hoop = CourtGeo.AttackHoop(shooter.Team);
        var rim = Geo.RimCenter[hoop] with { Y = 0 };
        var inward = new Vector3(-CourtGeo.HoopSign(hoop), 0, 0);
        var spot = FtSpot(shooter, out float yaw);
        shooter.Teleport(spot, yaw, setHome: false);

        var def = Players.Where(p => p.Team != shooter.Team).ToList();
        var att = Players.Where(p => p.Team == shooter.Team && p != shooter).ToList();
        var order = new List<Player>();
        int di = 0, ai = 0;
        while (di < def.Count || ai < att.Count)
        {
            for (int k = 0; k < 2 && di < def.Count; k++) order.Add(def[di++]);
            for (int k = 0; k < 2 && ai < att.Count; k++) order.Add(att[ai++]);
        }
        float laneEnd = Mathf.Max(t.FreeThrowDistance - 1.0f, 2f);
        for (int i = 0; i < order.Count; i++)
        {
            Vector3 pos;
            int row = i / 2;
            float side = i % 2 == 0 ? 1f : -1f;
            if (row < 3)
            {
                float along = Mathf.Lerp(1.6f, laneEnd, row / 2f);
                pos = rim + inward * along + new Vector3(0, 0, side * 2.6f);
            }
            else
            {
                int k = i - 6;
                pos = rim + inward * (t.FreeThrowDistance + 2.5f + (k / 4) * 2f) + new Vector3(0, 0, (k % 4 - 1.5f) * 2.4f);
            }
            pos = new Vector3(Mathf.Clamp(pos.X, -Geo.HalfL + 0.8f, Geo.HalfL - 0.8f), 0.05f, Mathf.Clamp(pos.Z, -Geo.HalfW + 0.8f, Geo.HalfW - 0.8f));
            order[i].Teleport(pos, ShotMath.YawOf(rim - pos), setHome: false);
        }
    }

    void BeginFtAttempt(int n)
    {
        var t = Tuning.I;
        FtAttempt = n;
        _ftStage = FtStage.Check;
        FtT = -t.FreeThrowPrep;
        FtPressed = false;
        FtFlash = 0; // o resultado do lance anterior sai quando o próximo começa
        FtDur = Mathf.Max(0.25f, t.FreeThrowSkillTime + (float)GD.RandRange(-t.DunkFlightJitter, t.DunkFlightJitter));
        var spot = FtSpot(FtShooter, out float yaw);
        FtShooter.Teleport(spot, yaw, setHome: false);
        Ball.GiveTo(FtShooter);
        if (FtShooter == LocalPlayer) Cam.SnapBehind(yaw);
    }

    void FreeThrowTick(float dt)
    {
        var t = Tuning.I;
        if (FtShooter == null || !IsInstanceValid(FtShooter)) { EndFreeThrows(); return; }
        switch (_ftStage)
        {
            case FtStage.Check:
            {
                // igual ao skill check da enterrada: só o botão de arremesso; cedo = erra ao chegar na zona
                FtT += dt;
                float half = t.DunkWindow * 0.5f;
                if (FtT >= 0 && !FtPressed && _ftInput.WasPressed(Btn.Shoot))
                {
                    FtPressed = true;
                    FtPressT = _ftInput.SkillT >= 0 ? Mathf.Min(_ftInput.SkillT, FtT) : FtT; // remoto: o que ele via
                }
                if (FtPressed) FtResult(Mathf.Abs(FtPressT - FtDur) <= half); // errou: erra NA HORA (v0.9)
                else if (FtT >= FtDur + half + FtShooter.SkillGrace) FtResult(false);
                break;
            }
            case FtStage.Flight:
                _ftStageT += dt;
                if (_ftStageT > 3.5f) // segurança: a bola não entrou
                {
                    if (FtAttempt == 1) { _ftStage = FtStage.Gap; _ftStageT = t.FreeThrowGap; }
                    else EndFreeThrows();
                }
                break;
            case FtStage.Gap:
                _ftStageT -= dt;
                if (_ftStageT <= 0) BeginFtAttempt(2);
                break;
        }
    }

    void FtResult(bool ok)
    {
        var t = Tuning.I;
        FtFlash = Mathf.Max(t.SkillResultHold, 0.001f); // resultado fica na tela com o ponteiro parado (v0.19)
        FtFlashOk = ok;
        FtFlashPlayer = FtShooter;
        LogEvent(ok ? "FT_OK" : "FT_MISS", FtShooter);
        if (ok)
        {
            ThrowFreeThrow(true);
            _ftStage = FtStage.Flight;
            _ftStageT = 0;
        }
        else if (FtAttempt == 1)
        {
            // errou o 1º: arremessa e erra; o 2º começa quando a bola tocar o chão (v0.9)
            ThrowFreeThrow(false);
            _ftStage = FtStage.Flight;
            _ftStageT = 0;
        }
        else
        {
            ThrowFreeThrow(false); // errou o 2º: bola bate no aro e o jogo continua (rebote)
            EndFreeThrows();
        }
    }

    /// <summary>Lança a bola do lance livre: convertido = cai no centro do aro; errado = bate na frente do aro.</summary>
    void ThrowFreeThrow(bool make)
    {
        var t = Tuning.I;
        var s = FtShooter;
        var rim = Geo.RimCenter[CourtGeo.AttackHoop(s.Team)];
        float yaw = ShotMath.YawOf((rim - s.GlobalPosition) with { Y = 0 });
        var pos = ShotMath.ReleasePos(s.GlobalPosition, yaw);
        var flat = (rim - pos) with { Y = 0 };
        float dist = Mathf.Max(flat.Length(), 0.5f);
        var dir = flat / dist;
        var target = make ? rim : rim - dir * (t.RimRadius + t.BallRadius * 0.6f);
        float d = ((target - pos) with { Y = 0 }).Length();
        float T = Mathf.Max(t.FreeThrowFlightTime, 0.3f);
        float vy = (target.Y - pos.Y + 0.5f * t.BallGravity * T * T) / T;
        var vel = dir * (d / T) + Vector3.Up * vy;
        s.Yaw = yaw;
        s.BallLockoutTimer = 0.8f;
        s.PlayReleaseAnim();
        Ball.Throw(s, pos, vel, make ? BallState.Shot : BallState.Loose);
        Ball.FreeThrow = true;
        LogEvent("FT_THROW", s);
    }

    void EndFreeThrows()
    {
        FtShooter = null;
        if (Phase == MatchPhase.FreeThrow) Phase = MatchPhase.Playing;
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
        EventCount[kind] = EventCount.GetValueOrDefault(kind) + 1;
        if (a != null && _remote.ContainsKey(a)) RemoteEvents[kind] = RemoteEvents.GetValueOrDefault(kind) + 1;
        if (Automated && !Cfg.SmokeE4 && !Cfg.SmokeNet) GD.Print($"[EVENT] {kind} {a?.PlayerName}{(b != null ? " -> " + b.PlayerName : "")}");
        _scenario?.OnEvent(kind, a, b);
        _dunkScenario?.OnEvent(kind, a, b);
        _blockScenario?.OnEvent(kind, a, b);
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
        if (!Automated) (_rumble ??= new Rumble(this)).Tick();

        if (Cfg.ScreenshotPath != null && !Cfg.SmokeE3 && !Cfg.SmokeNet) ScreenshotTick((float)delta);
        // teste de rede com --net-shot: prints do cliente aos 20 s e 40 s de jogo
        if (Cfg.SmokeNet && IsClient && Cfg.ScreenshotPath != null)
            foreach (int sec in new[] { 20, 40 })
                if (_netT >= sec && !_e3Shots.Contains("net" + sec)) E3Shot("net" + sec);
        // diagnóstico: --smoke-e3 --e3-shot <png> salva prints: _ready (setinhas "pode enterrar"), _dunk (skill check da enterrada), _ft (lance livre)
        if (Cfg.SmokeE3 && Cfg.ScreenshotPath != null && lp != null)
        {
            _readyFrames = lp.CanDunk() ? _readyFrames + 1 : 0;
            if (!_e3Shots.Contains("ready") && _readyFrames >= 4) E3Shot("ready");
            if (!_e3Shots.Contains("dunk") && lp.Flight == FlightKind.Dunk && lp.FlightT > lp.FlightDur * 0.6f) E3Shot("dunk");
            if (!_e3Shots.Contains("ft") && FtChecking && FtShooter == lp && FtT > FtDur * 0.5f) E3Shot("ft");
            // resultado do skill check (ponteiro parado), um print para certo e outro para errado
            float hold = Mathf.Max(Tuning.I.SkillResultHold, 0.001f);
            bool resDunk = lp.Flight == FlightKind.None && lp.SkillFlash > 0 && lp.SkillFlash < hold * 0.75f;
            bool resFt = FtFlash > 0 && FtFlashPlayer == lp && FtFlash < hold * 0.75f;
            if (resDunk || resFt)
            {
                string tag = (resDunk ? "dunkres_" : "ftres_") + ((resDunk ? lp.SkillFlashOk : FtFlashOk) ? "ok" : "miss");
                if (!_e3Shots.Contains(tag)) E3Shot(tag);
            }
        }
    }

    readonly HashSet<string> _e3Shots = new();
    int _readyFrames;
    void E3Shot(string tag)
    {
        _e3Shots.Add(tag);
        string path = Cfg.ScreenshotPath.Replace(".png", $"_{tag}.png");
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print("[SHOT] salvo " + path);
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
        if (IsOnline) return; // online não pausa: o jogo continua para os outros
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

    int _possChanges, _lastPossTeam = -1;
    float _noScoreT;

    /// <summary>Partida só de bots: verifica se jogam de verdade (pontos dos dois lados, passes, variedade, sem travar).</summary>
    void SmokeE4Tick(float dt)
    {
        _smokeTime += dt;
        if (Ball.Holder != null && Ball.Holder.Team != _lastPossTeam) { _lastPossTeam = Ball.Holder.Team; _possChanges++; }
        _noScoreT = Phase == MatchPhase.Scored ? 0 : _noScoreT + dt;
        if (_noScoreT > 40f && Mathf.PosMod(_noScoreT, 10f) < dt)
        {
            var h = Ball.Holder;
            GD.Print($"[E4] sem cesta há {_noScoreT:0}s: fase={Phase} bola={Ball.State} pos={Ball.GlobalPosition} vel={Ball.Vel} " +
                     $"dono={(h == null ? "-" : h.PlayerName + " t" + h.Team + " @" + h.GlobalPosition + " st=" + h.Stamina.ToString("0") + " drib=" + h.IsDribbling + " shot=" + h.ChargingShot + " pass=" + h.ChargingPass)}");
            foreach (var q in Players) GD.Print($"[E4]    {q.PlayerName} t{q.Team} @{q.GlobalPosition} air={q.Airborne} stun={q.Stun:0.0} fail={q.StealFail:0.0}");
        }
        if (_smokeTime < 150f) return;
        int C(string k) => EventCount.GetValueOrDefault(k);
        int total = Score[0] + Score[1];
        // v0.21: critério de arremessos era ≥ 6 e ficava no limite (os bots às vezes trocam arremesso por enterrada):
        // agora ≥ 4 arremessos e ≥ 8 tentativas de pontuar (arremessos + enterradas)
        bool ok = total >= 8 && Score[0] >= 2 && Score[1] >= 2 && C("SHOT") >= 4 && C("SHOT") + C("DUNK") >= 8
                  && C("PASS") + C("HIGHPASS") >= 3 && _possChanges >= 8;
        GD.Print($"[E4] placar={Score[0]}x{Score[1]} arremessos={C("SCORE_SHOT")}/{C("SHOT")} passes={C("PASS")}+{C("HIGHPASS")} enterradas={C("DUNK_OK")}/{C("DUNK")} (erros {C("DUNK_FAIL")}) " +
                 $"faltas={C("FOUL")} lances_livres={C("SCORE_FT")}/{C("FT_OK") + C("FT_MISS")} roubos={C("STEAL")}/{C("STEAL_TRY")} tocos={C("BLOCK")} dribles={C("DRIBBLE")} pulos={C("JUMP")} " +
                 $"trocas_de_posse={_possChanges} -> {(ok ? "OK" : "FALHOU")}");
        GetTree().Quit(ok ? 0 : 1);
        Cfg.SmokeE4 = false;
    }

    void SmokeBlockTick(float dt)
    {
        _smokeTime += dt;
        if (!_blockScenario.Done && _smokeTime < 220f) return;
        var s = _blockScenario;
        // bot Normal tenta contestar ~45% das vezes; precisa bloquear de verdade e quase não pular à toa
        bool ok = s.Trials >= BlockScenario.TrialCount && s.Blocks >= 3 && s.UselessJumps <= s.Jumps / 2
                  && s.Dribbles >= BlockScenario.DribbleCount && s.Baited >= 3 && s.StolenDuringDribble == 0 && s.OnTopTested && s.SlidOff;
        GD.Print($"[BLOCKTEST] tentativas={s.Trials} tocos={s.Blocks} pulos={s.Jumps} pulos_inuteis={s.UselessJumps} " +
                 $"dribles={s.Dribbles} bot_caiu_no_drible={s.Baited} roubou_driblando={s.StolenDuringDribble} -> {(ok ? "OK" : "FALHOU")}");
        GetTree().Quit(ok ? 0 : 1);
        Cfg.SmokeBlock = false;
    }

    void SmokeE3Tick(float dt)
    {
        _smokeTime += dt;
        if (!_dunkScenario.Done && _smokeTime < 120f) return;
        var s = _dunkScenario;
        bool ok = s.DunkOk && s.DunkFailOk && s.StunOk && s.WalkDunkOk && s.SpaceIgnoredOk && s.FoulOk && s.FtPointOk && s.FtLiveOk;
        GD.Print($"[E3] enterrada={s.DunkOk} erro={s.DunkFailOk} atordoado={s.StunOk} enterrada_andando={s.WalkDunkOk} espaço_ignorado={s.SpaceIgnoredOk} falta={s.FoulOk} " +
                 $"lance_livre_+1={s.FtPointOk} 2º_errado_bola_viva={s.FtLiveOk} placar={Score[0]}x{Score[1]} -> {(ok ? "OK" : "FALHOU")}");
        GetTree().Quit(ok ? 0 : 1);
        Cfg.SmokeE3 = false;
    }

    void SmokeE2Tick(float dt)
    {
        _smokeTime += dt;
        if (!_scenario.Done && _smokeTime < 70f) return;
        var s = _scenario;
        bool ok = s.PassOk && s.ReturnOk && s.AimPassOk && s.DribbleOk && s.JumpOk && s.StealOk && s.JumpShotLandOk && s.JumpShotAirOk && s.PassBufferOk && s.MovingLaneOk;
        GD.Print($"[E2] passe={s.PassOk} devolução={s.ReturnOk} passe_carregado={s.AimPassOk} drible={s.DribbleOk} boneco_pulou={s.JumpOk} toco={s.BlockOk} " +
                 $"roubo_falhou(imune)={s.StealFailOk} roubo={s.StealOk} pulo_arremesso_pouso={s.JumpShotLandOk} pulo_arremesso_ar={s.JumpShotAirOk} passe_guardado={s.PassBufferOk} passe_aliado_andando={s.MovingLaneOk} -> {(ok ? "OK" : "FALHOU")}");
        GetTree().Quit(ok ? 0 : 1);
        Cfg.SmokeE2 = false;
    }

    // ------------------------------------------------------------------ online: cliente (só mostra o que o host manda)

    const float InterpDelayTicks = 5f;   // desenha ~83 ms atrás do último snapshot (absorve perda e variação da rede)
    readonly List<NetSnapshot> _snaps = new();
    float _renderTick = -1;
    BotBrain _clientBot;
    /// <summary>Cliente: apertou no skill check → mostra o resultado na hora, com o ponteiro onde estava na tela dele
    /// (sem esperar o host). Depois, <see cref="ClientSkillMuteT"/> evita repetir o resultado que o host manda atrasado.</summary>
    public float ClientSkillHold, ClientSkillT, ClientSkillDur, ClientSkillMuteT;
    public bool ClientSkillOk;
    public int ClientSnapshotsApplied;

    /// <summary>Cliente: snapshot recebido do host (guarda para interpolar).</summary>
    public void ClientSnapshot(NetSnapshot s)
    {
        if (_snaps.Count > 0 && s.Tick <= _snaps[^1].Tick) return; // velho / fora de ordem
        _snaps.Add(s);
        if (_snaps.Count > 40) _snaps.RemoveAt(0);
    }

    /// <summary>Cliente: mensagem da HUD vinda do host.</summary>
    public void ClientHud(int kind, string text, float dur, Color color, bool small, int winner)
    {
        if (kind == 0) Hud?.ShowCountdown(text, dur);
        else if (kind == 1) Hud?.ShowMessage(text, dur, color, small);
        else if (kind == 2) { Winner = winner; Phase = MatchPhase.Ended; Hud?.ShowEnd(winner); }
    }

    void ClientPhysics(float dt)
    {
        // input local → host (o personagem só se mexe quando o snapshot do host voltar)
        PlayerInput inp = _clientBot != null ? _clientBot.Build(dt) : _reader.Build(Cam, !InputBlocked);
        if (Cfg.SmokeNet && !_netEmoteSent && _netT > 20f) { inp.Emote = Expressions.Bravo; _netEmoteSent = true; } // teste: expressão online
        if (Cfg.SmokeNet && LocalPlayer != null && LocalPlayer.Emote == Expressions.Bravo) _netSawEmote = true;
        if (LocalPlayer != null) LocalPlayer.LastInput = inp;
        if (NetSession.I != null) NetSession.I.ClientRenderTick = Mathf.RoundToInt(_renderTick);
        // skill check: manda onde o ponteiro estava na tela quando apertou (o host julga por isso)
        inp.SkillT = -1;
        if (inp.WasPressed(Btn.Shoot) && LocalPlayer != null)
        {
            float dur = -1;
            if (LocalPlayer.Flight == FlightKind.Dunk) { inp.SkillT = LocalPlayer.FlightT; dur = LocalPlayer.FlightDur; }
            else if (FtChecking && FtShooter == LocalPlayer && FtT >= 0) { inp.SkillT = Mathf.Max(FtT, 0f); dur = FtDur; }
            // resultado na tela dele na hora (o host julga pelo mesmo SkillT, então dá o mesmo veredito)
            if (dur > 0 && ClientSkillHold <= 0)
            {
                ClientSkillT = inp.SkillT; ClientSkillDur = dur;
                ClientSkillOk = Mathf.Abs(inp.SkillT - dur) <= Tuning.I.DunkWindow * 0.5f;
                ClientSkillHold = Mathf.Max(Tuning.I.SkillResultHold, 0.001f);
            }
        }
        if (ClientSkillHold > 0)
        {
            ClientSkillHold -= dt;
            if (ClientSkillHold <= 0) ClientSkillMuteT = 0.6f; // o flash do host chega atrasado: não mostrar de novo
        }
        else if (ClientSkillMuteT > 0) ClientSkillMuteT -= dt;
        NetSession.I?.SendInput(inp);

        if (_snaps.Count > 0)
        {
            var last = _snaps[^1];
            float target = last.Tick - InterpDelayTicks;
            if (_renderTick < 0 || Mathf.Abs(_renderTick - target) > 30) _renderTick = target;
            else _renderTick += 1f + (target - _renderTick) * 0.05f; // anda 1 tick por tick e corrige devagar
            NetSnapshot a = _snaps[0], b = _snaps[0];
            for (int i = 0; i < _snaps.Count; i++)
                if (_snaps[i].Tick <= _renderTick) { a = _snaps[i]; b = i + 1 < _snaps.Count ? _snaps[i + 1] : _snaps[i]; }
            float k = b.Tick > a.Tick ? Mathf.Clamp((_renderTick - a.Tick) / (b.Tick - a.Tick), 0, 1) : 1f;
            while (_snaps.Count > 3 && _snaps[1].Tick < _renderTick - 4) _snaps.RemoveAt(0);
            ApplyClientState(a, b, k, dt);
        }
        if (Cfg.SmokeNet) SmokeNetClientTick(dt);
    }

    Player ById(int id)
    {
        if (id <= 0) return null;
        if (id - 1 < Players.Count && Players[id - 1].Id == id) return Players[id - 1];
        return Players.FirstOrDefault(p => p.Id == id);
    }

    void ApplyClientState(NetSnapshot a, NetSnapshot b, float k, float dt)
    {
        ClientSnapshotsApplied++;
        var prevPhase = Phase;
        if (Phase != MatchPhase.Ended) Phase = b.Phase;
        PhaseTimer = b.PhaseTimer; Score[0] = b.Score0; Score[1] = b.Score1; Clock = b.Clock; Overtime = b.Overtime; Possession = b.Possession;
        if (Phase != MatchPhase.Ended) Winner = b.Winner;
        FtShooter = ById(b.FtShooter); FtAttempt = b.FtAttempt; FtT = b.FtT; FtDur = b.FtDur; FtPressed = b.FtPressed; FtPressT = b.FtPressT;
        FtFlash = b.FtFlash; FtFlashOk = b.FtFlashOk; FtFlashPlayer = ById(b.FtFlashPlayer); _netFtChecking = b.FtChecking;

        foreach (var (id, sb) in b.Players)
        {
            var p = ById(id);
            if (p == null) continue;
            var sa = sb;
            foreach (var (ida, s2) in a.Players) if (ida == id) { sa = s2; break; }
            p.ClientApply(Player.Lerp(sa, sb, k), dt);
        }
        Ball.ClientApply(b.BallState, ById(b.BallHolder), b.BallThrowTeam, ById(b.BallLastThrower), ById(b.BallPassTarget),
            a.BallPos.Lerp(b.BallPos, k), b.BallVel, b.BallFlightTime);

        // câmera atrás do jogador quando um round/lance livre recomeça com ele teleportado
        var lp = LocalPlayer;
        if (lp != null && prevPhase != Phase && (Phase == MatchPhase.FreeThrow && FtShooter == lp || prevPhase == MatchPhase.Scored && Phase == MatchPhase.Playing))
            Cam.SnapBehind(lp.Yaw);
    }

    // ------------------------------------------------------------------ teste de rede (--net-smoke-host / --net-smoke-client)

    float _netT, _netDist;
    int _netLeftPackets = -1;
    int _netFtForced;
    string _netLeftName;
    Vector3 _netLast;
    bool _netInit, _netSawPlaying, _netEmoteSent, _netSawEmote;

    static void Track(Player p, ref bool init, ref Vector3 last, ref float dist)
    {
        if (p == null) return;
        var pos = p.GlobalPosition;
        float step = (pos - last).Length();
        if (init && step < 0.5f) dist += step; // ignora teleportes (início de round)
        last = pos;
        init = true;
    }

    void SmokeNetHostTick(float dt)
    {
        _netT += dt;
        var rp = _remote.Keys.FirstOrDefault();
        Track(rp, ref _netInit, ref _netLast, ref _netDist);
        if (rp != null && rp.Emote == Expressions.Bravo) _netSawEmote = true;
        // força lances livres para o cliente (skill checks julgados com o atraso da rede)
        if (rp != null && Phase == MatchPhase.Playing && _netFtForced < 3 && _netT > 12f + _netFtForced * 16f)
        {
            _netFtForced++;
            StartFreeThrows(rp);
        }
        if (_netT < 75f) return;
        int acts = 0;
        foreach (var k in new[] { "SHOT", "PASS", "HIGHPASS", "JUMP", "STEAL_TRY", "DRIBBLE", "DUNK", "FT_OK", "FT_MISS" }) acts += RemoteEvents.GetValueOrDefault(k);
        int packets = rp != null ? NetSession.I.InputPackets(_remote[rp]) : _netLeftPackets;
        int dOk = RemoteEvents.GetValueOrDefault("DUNK_OK"), dFail = RemoteEvents.GetValueOrDefault("DUNK_FAIL");
        int fOk = RemoteEvents.GetValueOrDefault("FT_OK"), fMiss = RemoteEvents.GetValueOrDefault("FT_MISS");
        GD.Print($"[NETTEST-HOST] skill checks do remoto (com atraso de rede): enterradas {dOk}/{dOk + dFail}, lances livres {fOk}/{fOk + fMiss}");
        // o bot do cliente erra pouco no Normal: se tentou 3+ skill checks e não acertou nenhum, o atraso não está compensado
        bool skillOk = fOk + fMiss >= 4 && fOk >= 2 && (dOk + dFail < 3 || dOk > 0);
        bool ok = (rp != null || _netLeftPackets >= 0) && _netDist > 15f && acts >= 1 && packets > 1000 && skillOk && _netSawEmote;
        GD.Print($"[NETTEST-HOST] expressão do remoto recebida={_netSawEmote}");
        GD.Print($"[NETTEST-HOST] remoto={rp?.PlayerName ?? _netLeftName + " (já saiu)"} pacotes_de_input={packets} andou={_netDist:0}m acoes_do_remoto={acts} " +
                 $"({string.Join(" ", RemoteEvents.Select(kv => kv.Key + "=" + kv.Value))}) placar={Score[0]}x{Score[1]} -> {(ok ? "OK" : "FALHOU")}");
        Cfg.SmokeNet = false;
        GetTree().Quit(ok ? 0 : 1);
    }

    void SmokeNetClientTick(float dt)
    {
        _netT += dt;
        Track(LocalPlayer, ref _netInit, ref _netLast, ref _netDist);
        if (Phase == MatchPhase.Playing) _netSawPlaying = true;
        if (_netT < 70f) return;
        var ns = NetSession.I;
        bool ok = ns.SnapshotsReceived > 1000 && ClientSnapshotsApplied > 1000 && _netDist > 15f && _netSawPlaying && _netSawEmote;
        GD.Print($"[NETTEST-CLIENT] expressão voltou no snapshot={_netSawEmote}");
        GD.Print($"[NETTEST-CLIENT] snapshots={ns.SnapshotsReceived} inputs_enviados={ns.InputsSent} andou={_netDist:0}m viu_jogo={_netSawPlaying} " +
                 $"placar_visto={Score[0]}x{Score[1]} -> {(ok ? "OK" : "FALHOU")}");
        Cfg.SmokeNet = false;
        GetTree().Quit(ok ? 0 : 1);
    }
}
