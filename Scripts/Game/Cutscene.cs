using System;
using System.Linq;
using Godot;

namespace AB;

public enum CutKind : byte { None, KingUlt, BoltDuel }

/// <summary>
/// v0.26: cutscenes das ultimates (King "Sua Derrota é Absoluta" e Bolt "Duelo"). Mostradas para TODOS os jogadores.
///
/// Como funciona:
/// - Durante a cutscene a partida fica na fase <see cref="MatchPhase.Cutscene"/>: ninguém é simulado (relógio, recargas e
///   cargas de ultimate param) e os jogadores de verdade ficam escondidos, parados onde estavam — a posição real não muda.
/// - Quem aparece são BONECOS (visual do personagem) posicionados no lugar mais conveniente para a cena. Se faltarem
///   jogadores (ex.: o King precisa de 2 adversários), entram bonecos com o visual de um personagem aleatório.
/// - O host avança as etapas (<see cref="Stage"/>) e julga os skill checks (<see cref="A"/> = dono, <see cref="B"/> = alvo do
///   duelo). Os clientes recebem o estado no snapshot e desenham a mesma cena (tudo é função do tempo da etapa).
/// - Câmera própria, barras de cinema, título, linhas de velocidade e clarões (<see cref="CutOverlay"/>).
/// </summary>
public partial class Cutscene : Node3D
{
    public Match M;

    // ---- estado (replicado) ----
    public CutKind Kind;
    public int Stage;
    public float StageT;
    public int OwnerId, TargetId, Team;
    public readonly int[] ActorIds = new int[3];
    public readonly byte[] ActorChars = new byte[3];
    public readonly SkillCheckState A = new(), B = new();
    public int Round;
    public bool Active => Kind != CutKind.None;

    // King
    public const int KIntro = 0, KCheck1 = 1, KSteal = 2, KBreak = 3, KRun = 4, KBlock = 5, KSpin = 6, KScore = 7, KRimOut = 8;
    const float KIntroDur = 1.7f, KStealDur = 1.6f, KBreakDur = 0.9f, KRunDur = 1.0f, KBlockDur = 0.75f, KSpinMove = 0.55f, KScoreDur = 2.4f, KRimDur = 2.1f;
    const float KBallFlight = 1.15f;
    // Bolt
    public const int DIntro = 0, DDuel = 1, DLose = 2, DThunder = 3;
    const float DIntroDur = 2.0f, DLoseDur = 1.5f, DThunderDur = 2.5f;
    const float Linger = 0.4f;
    float _duelRoundAt;

    Player Owner => M.ById(OwnerId);
    Player Target => M.ById(TargetId);

    // =====================================================================
    // Host

    public bool Begin(CutKind kind, Player owner, Player target)
    {
        if (Active || owner == null) return false;
        if (kind == CutKind.BoltDuel && (target == null || target.Team == owner.Team)) return false;
        Kind = kind; Stage = 0; StageT = 0; Round = 0;
        OwnerId = owner.Id; TargetId = target?.Id ?? 0; Team = owner.Team;
        A.Reset(); B.Reset();
        for (int i = 0; i < 3; i++) { ActorIds[i] = 0; ActorChars[i] = (byte)GD.RandRange(0, Characters.Count - 1); }
        ActorIds[0] = owner.Id; ActorChars[0] = (byte)owner.CharacterId;
        if (kind == CutKind.KingUlt)
        {
            // dois adversários mais perto do King; faltou alguém = boneco com personagem aleatório
            var opp = M.Players.Where(q => q.Team != owner.Team).OrderBy(q => q.GlobalPosition.DistanceSquaredTo(owner.GlobalPosition)).Take(2).ToList();
            for (int i = 0; i < opp.Count; i++) { ActorIds[i + 1] = opp[i].Id; ActorChars[i + 1] = (byte)opp[i].CharacterId; }
        }
        else { ActorIds[1] = target.Id; ActorChars[1] = (byte)target.CharacterId; ActorIds[2] = -1; }
        Sfx.Play2D(M, "cut_start", -3);
        M.LogEvent(kind == CutKind.KingUlt ? "CUT_KING" : "CUT_DUEL", owner, target);
        return true;
    }

    void Next(int stage) { Stage = stage; StageT = 0; }

    static bool Cross(float before, float now, float at) => before < at && now >= at;

    public void HostTick(float dt, Func<Player, PlayerInput> input)
    {
        if (!Active) return;
        var t = Tuning.I;
        float prev = StageT;
        StageT += dt;
        var owner = Owner; var target = Target;
        var io = owner != null ? input(owner) : default;
        float go = owner?.SkillGrace ?? 0f;
        float Jit(float d) => Mathf.Max(0.25f, d + M.Rng.RandfRange(-t.DunkFlightJitter, t.DunkFlightJitter) * 0.6f);

        if (Kind == CutKind.KingUlt)
        {
            var rim = M.Geo.RimCenter[CourtGeo.AttackHoop(Team)];
            switch (Stage)
            {
                case KIntro:
                    if (StageT >= KIntroDur) { Next(KCheck1); A.Start(Jit(t.KingUltCheck1Time), t.KingUltCheck1Window, t.CutCheckPrep); }
                    break;
                case KCheck1:
                    A.Tick(dt, io, go);
                    if (A.Done && A.Since >= Linger) Next(A.Ok ? KBreak : KSteal);
                    break;
                case KSteal:
                    if (Cross(prev, StageT, 0.3f)) Sfx.Play2D(M, "cut_hit", -2);
                    if (StageT >= KStealDur) Finish(false);
                    break;
                case KBreak:
                    if (Cross(prev, StageT, 0.05f)) Sfx.Play2D(M, "cut_whoosh", -3);
                    if (StageT >= KBreakDur) Next(KRun);
                    break;
                case KRun:
                    if (StageT >= KRunDur) Next(KBlock);
                    break;
                case KBlock:
                    if (StageT >= KBlockDur) { Next(KSpin); A.Start(Jit(t.KingUltCheck2Time), t.KingUltCheck2Window, KSpinMove + 0.15f); }
                    break;
                case KSpin:
                    if (Cross(prev, StageT, 0.02f)) Sfx.Play2D(M, "cut_whoosh", -2, 1.2f);
                    A.Tick(dt, io, go);
                    if (A.Done && A.Since >= Linger) Next(A.Ok ? KScore : KRimOut);
                    break;
                case KScore:
                    if (Cross(prev, StageT, KBallFlight)) { Sfx.Play3D(M, "swish", rim, 0); Sfx.Play3D(M, "fx_dunk", rim); Sfx.Play2D(M, "cut_hit", 0); }
                    if (StageT >= KScoreDur) Finish(true);
                    break;
                case KRimOut:
                    if (Cross(prev, StageT, KBallFlight)) Sfx.Play3D(M, "rim", rim, 3, 0.9f);
                    if (StageT >= KRimDur) Finish(false);
                    break;
            }
        }
        else if (Kind == CutKind.BoltDuel)
        {
            var ti = target != null ? input(target) : default;
            float gt = target?.SkillGrace ?? 0f;
            switch (Stage)
            {
                case DIntro:
                    if (Cross(prev, StageT, 1.3f)) Sfx.Play2D(M, "cut_zap", -2);
                    if (StageT >= DIntroDur) { Next(DDuel); StartDuelRound(true); }
                    break;
                case DDuel:
                    // v0.27: dois bots se enfrentando não podem ficar trocando acertos para sempre
                    if (owner != null && target != null && (owner.IsBot || owner.IsDummy) && (target.IsBot || target.IsDummy)
                        && StageT >= Mathf.Max(0.5f, t.BotDuelMaxTime))
                    {
                        Next(M.Rng.Randf() < 0.5f ? DThunder : DLose);
                        A.Active = B.Active = false;
                        break;
                    }
                    A.Tick(dt, io, go);
                    B.Tick(dt, ti, gt);
                    if (A.Done && B.Done)
                    {
                        if (A.Ok && B.Ok) { Sfx.Play2D(M, "cut_zap", -6, 1f + Round * 0.05f); StartDuelRound(false); }
                        else if (Mathf.Min(A.Since, B.Since) >= Linger) Next(A.Ok ? DThunder : DLose);
                    }
                    break;
                case DLose:
                    if (Cross(prev, StageT, 0.3f)) Sfx.Play2D(M, "cut_hit", -2);
                    if (StageT >= DLoseDur) Finish(false);
                    break;
                case DThunder:
                    var rim = M.Geo.RimCenter[CourtGeo.AttackHoop(Team)];
                    if (Cross(prev, StageT, 0.05f)) Sfx.Play2D(M, "cut_thunder", 0);
                    if (Cross(prev, StageT, 1.15f)) { Sfx.Play3D(M, "fx_dunk", rim); Sfx.Play3D(M, "slam", rim, 3); Sfx.Play3D(M, "swish", rim); }
                    if (StageT >= DThunderDur) Finish(true);
                    break;
            }
        }
    }

    void StartDuelRound(bool first)
    {
        var t = Tuning.I;
        if (first) _duelRoundAt = 0;
        float elapsed = StageT;
        float dur = Mathf.Max(t.BoltDuelMinTime, t.BoltDuelStartTime / (1f + t.BoltDuelSpeedup * elapsed));
        float prep = first ? t.CutCheckPrep : 0.08f;
        A.Start(dur * (1f + M.Rng.RandfRange(-0.08f, 0.08f)), t.BoltDuelWindow, prep);
        B.Start(dur * (1f + M.Rng.RandfRange(-0.08f, 0.08f)), t.BoltDuelWindow, prep);
        Round++;
    }

    void Finish(bool success)
    {
        var kind = Kind;
        var owner = Owner; var target = Target;
        Kind = CutKind.None;
        A.Active = B.Active = false;
        M.OnCutsceneEnd(kind, success, owner, target, Team);
    }

    /// <summary>Skill check deste jogador agora (dono = A, alvo do duelo = B), ou null.</summary>
    public SkillCheckState CheckFor(Player p)
    {
        if (!Active || p == null) return null;
        if (p.Id == OwnerId && (Stage == KCheck1 || Stage == KSpin) && Kind == CutKind.KingUlt) return A;
        if (Kind == CutKind.BoltDuel && Stage == DDuel) return p.Id == OwnerId ? A : p.Id == TargetId ? B : null;
        return null;
    }

    // =====================================================================
    // Rede

    public void Write(NetWriter w)
    {
        w.U8((byte)Kind);
        if (!Active) return;
        w.U8((byte)Stage); w.F(StageT); w.U8((byte)OwnerId); w.U8((byte)TargetId); w.U8((byte)Team); w.U8((byte)Round);
        for (int i = 0; i < 3; i++) { w.I16((short)ActorIds[i]); w.U8(ActorChars[i]); }
        A.Write(w); B.Write(w);
    }

    public void Read(NetReader r)
    {
        Kind = (CutKind)r.U8();
        if (!Active) return;
        Stage = r.U8(); StageT = r.F(); OwnerId = r.U8(); TargetId = r.U8(); Team = r.U8(); Round = r.U8();
        for (int i = 0; i < 3; i++) { ActorIds[i] = r.I16(); ActorChars[i] = r.U8(); }
        A.Read(r); B.Read(r);
    }

    // =====================================================================
    // Visual (host e clientes)

    sealed class Actor
    {
        public Node3D Node;
        public IPlayerVisual Vis;
        public RigState St;
        public Vector3 Pos;
        public float Yaw;
        public int Team;
    }

    readonly Actor[] _act = new Actor[3];
    Camera3D _cam;
    Node3D _ball;
    CutOverlay _ov;
    CanvasLayer _ovLayer;
    MeshInstance3D _beam;
    ImmediateMesh _beamMesh;
    CutKind _visKind;
    int _visStage = -1;
    float _vt, _shake, _time, _flashCooldown;
    readonly RandomNumberGenerator _fxRng = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!Active)
        {
            if (_visKind != CutKind.None) TearDown();
            return;
        }
        if (_visKind != Kind) Build();
        if (_visStage != Stage) { _visStage = Stage; _vt = StageT; OnStageVisual(Stage); }
        else { _vt += dt; if (Mathf.Abs(_vt - StageT) > 0.15f) _vt = StageT; }
        _time += dt;
        foreach (var p in M.Players) if (p.Visible) p.Visible = false;
        if (M.Ball.Visible) M.Ball.Visible = false;
        if (Kind == CutKind.KingUlt) KingFrame(_vt, dt);
        else DuelFrame(_vt, dt);
        for (int i = 0; i < 3; i++)
        {
            var a = _act[i];
            if (a == null) continue;
            a.Node.GlobalPosition = a.Pos;
            a.St.Yaw = a.Yaw;
            a.Vis.Animate(a.St, dt);
        }
        if (_shake > 0.002f)
        {
            var b = _cam.GlobalTransform.Basis;
            _cam.GlobalPosition += b * new Vector3(Mathf.Sin(_time * 77f), Mathf.Sin(_time * 59f + 1f), 0) * (_shake * 0.12f);
            _shake *= Mathf.Exp(-7f * dt);
        }
        _ov.QueueRedraw();
    }

    void Build()
    {
        TearDown();
        _visKind = Kind;
        _visStage = -1;
        _cam = new Camera3D { Fov = 55, Far = 400f, Name = "CutCam" };
        _cam.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        AddChild(_cam);
        _cam.MakeCurrent();
        for (int i = 0; i < 3; i++)
        {
            if (ActorIds[i] < 0) continue;
            int team = i == 0 ? Team : 1 - Team;
            var node = new Node3D { Name = "Actor" + i };
            AddChild(node);
            IPlayerVisual vis = CharacterVisual.Available ? new CharacterVisual(node, team, null, ActorChars[i]) : new PuppetFallback(node, team);
            _act[i] = new Actor { Node = node, Vis = vis, Team = team };
        }
        _ball = MakeBall();
        AddChild(_ball);
        _beamMesh = new ImmediateMesh();
        var bm = Mat.Unshaded(Colors.White, true);
        bm.VertexColorUseAsAlbedo = true;
        bm.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _beam = new MeshInstance3D { Mesh = _beamMesh, MaterialOverride = bm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_beam);
        _ovLayer = new CanvasLayer { Layer = 4, Name = "CutOverlay" };
        AddChild(_ovLayer);
        _ov = new CutOverlay();
        _ovLayer.AddChild(UiKit.FullRect(_ov));
        _ov.Title = Kind == CutKind.KingUlt ? Characters.Get(Characters.King).Abilities[3].Name.ToUpperInvariant()
                                          : Characters.Get(Characters.Bolt).Abilities[3].Name.ToUpperInvariant();
        _ov.Accent = Kind == CutKind.KingUlt ? new Color("e8313a") : new Color("3fd7ff");
        _ov.TitleT = 0;
        _time = 0;
    }

    void TearDown()
    {
        _visKind = CutKind.None;
        _visStage = -1;
        foreach (var c in GetChildren()) c.QueueFree();
        for (int i = 0; i < 3; i++) _act[i] = null;
        _cam = null; _ball = null; _ov = null; _beam = null;
        if (M != null)
        {
            foreach (var p in M.Players) p.Visible = true;
            if (M.Ball != null) M.Ball.Visible = true;
            M.Cam?.Cam?.MakeCurrent();
        }
    }

    static Node3D MakeBall()
    {
        float r = Tuning.I.BallRadius;
        var n = new Node3D { Name = "CutBall" };
        const string path = "res://assets/models/ball.glb";
        if (ResourceLoader.Exists(path) && GD.Load<PackedScene>(path) is { } sc)
        {
            var m = sc.Instantiate<Node3D>();
            m.Scale = Vector3.One * (r / 0.22f);
            Mat.ApplyToon(m, name => name == "Borracha" ? 0.012f : 0f);
            n.AddChild(m);
        }
        else n.AddChild(Mat.MeshNode(new SphereMesh { Radius = r, Height = r * 2 }, Mat.ToonOutlined(Palette.Ball, 0.012f), Vector3.Zero));
        return n;
    }

    void OnStageVisual(int stage)
    {
        // clarões e tremores nas viradas importantes (cada tela faz o seu)
        if (Kind == CutKind.KingUlt)
        {
            if (stage == KSteal) { _ov.Flash(new Color(1, 0.2f, 0.2f), 0.5f); _shake = 1f; }
            if (stage == KBreak) { _ov.Flash(Colors.White, 0.6f); _ov.ImpactFrame = 0.12f; }
            if (stage == KSpin) _ov.Flash(Colors.White, 0.35f);
        }
        else
        {
            if (stage == DDuel) _ov.Flash(new Color("bff4ff"), 0.5f);
            if (stage == DLose) { _ov.Flash(new Color(1, 0.25f, 0.2f), 0.5f); _shake = 1f; }
            if (stage == DThunder) { _ov.Flash(Colors.White, 1f); _ov.ImpactFrame = 0.18f; _shake = 1.4f; }
        }
    }

    // ---------- ajudas ----------
    static Vector3 Dir(float yaw) => ShotMath.Dir(yaw);
    static float YawTo(Vector3 from, Vector3 to) => ShotMath.YawOf((to - from) with { Y = 0 });
    static float Smooth(float k) => Mathf.SmoothStep(0, 1, Mathf.Clamp(k, 0, 1));
    static float Ease(float k) { k = Mathf.Clamp(k, 0, 1); return 1f - (1f - k) * (1f - k); }

    Vector3 Held(Actor a, float phase, float side = 1f, bool fast = false)
    {
        var t = Tuning.I;
        var right = new Vector3(Mathf.Cos(a.Yaw), 0, -Mathf.Sin(a.Yaw));
        float hand = fast ? t.BallRadius * 2.4f : t.DribbleHandHeight;
        float y = t.BallRadius + (hand - t.BallRadius) * Mathf.Abs(Mathf.Cos(phase));
        return a.Pos + right * (0.46f * side) + Dir(a.Yaw) * 0.36f + Vector3.Up * y;
    }

    void Cam(Vector3 pos, Vector3 look, float fov)
    {
        if (pos.Y < 0.3f) pos.Y = 0.3f;
        _cam.Fov = fov;
        if ((look - pos).LengthSquared() < 1e-4f) look = pos + Vector3.Forward;
        _cam.LookAtFromPosition(pos, look, Vector3.Up);
    }

    static RigState Base(bool ball = false) => new() { HasBall = ball, BallSide = 1 };

    // =====================================================================
    // King: Sua Derrota é Absoluta

    void KingFrame(float u, float dt)
    {
        var K = _act[0]; var D1 = _act[1]; var D2 = _act[2];
        float s = CourtGeo.HoopSign(CourtGeo.AttackHoop(Team));
        var fwd = new Vector3(s, 0, 0);
        var rim = M.Geo.RimCenter[CourtGeo.AttackHoop(Team)];
        float three = M.Geo.ThreePt;
        var kStart = new Vector3(-s * 1.1f, 0, 0);
        var d1Spot = new Vector3(s * 1.2f, 0, 0);
        var runFrom = new Vector3(rim.X - s * (three + 6.5f), 0, 1.4f);
        var threeSpot = new Vector3(rim.X - s * (three + 0.4f), 0, 0.9f);
        var d2Spot = threeSpot + new Vector3(s * 1.45f, 0, -0.15f);
        var shotSpot = new Vector3(rim.X - s * 3.6f, 0, 3.4f);
        float face = ShotMath.YawOf(fwd), back = ShotMath.YawOf(-fwd);
        float drib = _time * Mathf.Pi / 0.32f;
        _ov.Bars = Mathf.MoveToward(_ov.Bars, 1f, dt * 4f);
        _ov.Speed = 0; _ov.Vignette = 0.35f;
        ClearBeam();

        // padrão: cada um no seu lugar
        K.Pos = kStart; K.Yaw = face; K.St = Base(true);
        if (D1 != null) { D1.Pos = d1Spot; D1.Yaw = back; D1.St = Base(); D1.St.Defending = true; }
        if (D2 != null) { D2.Pos = d2Spot; D2.Yaw = YawTo(d2Spot, threeSpot); D2.St = Base(); D2.St.Defending = true; }
        Vector3 ball;

        switch (Stage)
        {
            case KIntro:
            {
                K.St.Emote = Expressions.SorrisoFrio;
                K.St.Speed = 0;
                ball = Held(K, drib);
                _ov.TitleT = u;
                float k = Smooth(u / KIntroDur);
                var head = K.Pos + Vector3.Up * 1.3f;
                // close no rosto do King → abre por cima do ombro do defensor
                var p0 = head + Dir(face) * 1.6f + new Vector3(0, -0.1f, 0) + Right(face) * 1.3f;
                var p1 = d1Spot + Dir(back) * -2.6f + Vector3.Up * 2.0f + Right(face) * 1.4f;
                // a câmera faz um arco por fora (não atravessa o defensor)
                var arc = Right(face) * (1.6f * Mathf.Sin(k * Mathf.Pi)) + Vector3.Up * (0.8f * Mathf.Sin(k * Mathf.Pi));
                Cam(p0.Lerp(p1, k) + arc, head.Lerp(K.Pos + Vector3.Up * 1.1f, k), Mathf.Lerp(38, 58, k));
                break;
            }
            case KCheck1:
            {
                K.St.Emote = Expressions.Focado;
                K.St.Dribbling = true;
                float sway = Mathf.Sin(_time * 5f);
                K.Pos = kStart + Right(face) * (0.35f * sway);
                K.St.BallSide = sway >= 0 ? 1 : -1;
                ball = Held(K, drib * 1.6f, K.St.BallSide, true);
                if (D1 != null) { D1.Pos = d1Spot + Right(back) * (-0.3f * sway); D1.St.Speed = 1.2f; }
                var behind = K.Pos - Dir(face) * 2.8f + Vector3.Up * 2.1f + Right(face) * 1.0f;
                Cam(behind, d1Spot + Vector3.Up * 1.2f, 52);
                _ov.Vignette = 0.5f;
                break;
            }
            case KSteal:
            {
                float k = Mathf.Clamp(u / 0.45f, 0, 1);
                K.St.HasBall = false; K.St.Stunned = u > 0.35f; K.St.Emote = Expressions.Surpreso;
                if (D1 != null)
                {
                    D1.Pos = d1Spot.Lerp(kStart + fwd * 0.8f, Ease(k));
                    D1.St.Dashing = u < 0.45f; D1.St.Defending = false;
                    D1.St.HasBall = u > 0.3f; D1.St.Emote = Expressions.SorrisoFrio;
                    ball = u < 0.3f ? Held(K, drib).Lerp(D1.Pos + Vector3.Up * 1.0f + Dir(back) * 0.4f, u / 0.3f) : Held(D1, drib);
                    if (D1.St.HasBall) D1.St.BallPos = ball;
                }
                else ball = Held(K, drib);
                var mid = (K.Pos + (D1?.Pos ?? K.Pos)) * 0.5f;
                Cam(mid + new Vector3(0, 1.6f, 4.2f) + fwd * 0.5f, mid + Vector3.Up * 1.0f, 50);
                break;
            }
            case KBreak:
            {
                float k = Mathf.Clamp(u / KBreakDur, 0, 1);
                var side = Right(face);
                // crossover: vai para um lado e passa voando pelo outro
                var a = kStart + side * 0.9f;
                var b = d1Spot + side * -1.2f + fwd * 0.2f;
                var c = d1Spot + fwd * 3.6f + side * -0.4f;
                K.Pos = k < 0.3f ? kStart.Lerp(a, Smooth(k / 0.3f)) : k < 0.65f ? a.Lerp(b, Smooth((k - 0.3f) / 0.35f)) : b.Lerp(c, Ease((k - 0.65f) / 0.35f));
                K.St.Speed = k < 0.3f ? 3f : 9f;
                K.St.Dribbling = k < 0.65f;
                K.St.BallSide = k < 0.3f ? 1 : -1;
                K.St.Emote = Expressions.SorrisoFrio;
                ball = Held(K, drib * 1.8f, K.St.BallSide, K.St.Dribbling);
                if (D1 != null)
                {
                    // o defensor vai para o lado errado e cai sentado
                    D1.Pos = d1Spot + side * (0.8f * Smooth(k / 0.4f));
                    D1.Yaw = back + Mathf.Pi * 0.9f * Smooth((k - 0.45f) / 0.4f);
                    D1.St.Stunned = k > 0.55f; D1.St.Defending = k < 0.55f; D1.St.Emote = Expressions.Surpreso;
                }
                _ov.Speed = k > 0.3f ? 1f : 0.3f;
                var cp = K.Pos + side * 3.4f + Vector3.Up * 0.8f - fwd * 0.6f;
                Cam(cp, K.Pos + Vector3.Up * 1.0f + fwd * 0.6f, 74);
                break;
            }
            case KRun:
            {
                float k = Mathf.Clamp(u / KRunDur, 0, 1);
                K.Pos = runFrom.Lerp(threeSpot, Ease(k));
                K.Yaw = YawTo(runFrom, threeSpot);
                K.St.Speed = Mathf.Lerp(9f, 4f, k);
                ball = Held(K, drib * 1.4f);
                if (D2 != null) { D2.St.Defending = true; D2.St.JumpCharging = k > 0.75f; }
                _ov.Speed = 1f - k * 0.6f;
                var cp = K.Pos - Dir(K.Yaw) * 3.2f + Vector3.Up * 1.4f + Right(K.Yaw) * 0.8f;
                Cam(cp, K.Pos + Dir(K.Yaw) * 4f + Vector3.Up * 1.3f, 70);
                break;
            }
            case KBlock:
            {
                float k = Mathf.Clamp(u / KBlockDur, 0, 1);
                // finta de arremesso; o defensor pula para bloquear (em câmera lenta)
                K.Pos = threeSpot; K.Yaw = YawTo(threeSpot, rim);
                K.St.Charging = true; K.St.Charge = 0.3f;
                ball = K.Pos + Vector3.Up * 2.0f + Dir(K.Yaw) * 0.3f;
                if (D2 != null)
                {
                    float jk = Mathf.Clamp(k / 1f, 0, 1);
                    D2.Pos = d2Spot + Vector3.Up * (1.5f * Mathf.Sin(jk * Mathf.Pi * 0.5f));
                    D2.St.Airborne = jk > 0.05f; D2.St.VelY = 3f; D2.St.Defending = false; D2.St.Emote = Expressions.Bravo;
                }
                var cp = (threeSpot + d2Spot) * 0.5f + Right(face) * 5.0f + Vector3.Up * 0.9f - fwd * 1.0f;
                Cam(cp, (D2?.Pos ?? d2Spot) + Vector3.Up * 1.8f, Mathf.Lerp(58, 46, k));
                _ov.Vignette = 0.55f;
                break;
            }
            case KSpin:
            {
                float k = Mathf.Clamp(u / KSpinMove, 0, 1);
                // dá a volta no bloqueador (ainda no ar) e sobe para arremessar pelo lado da cesta
                var center = d2Spot;
                float a0 = Mathf.Atan2(threeSpot.Z - center.Z, threeSpot.X - center.X);
                float a1 = Mathf.Atan2(shotSpot.Z - center.Z, shotSpot.X - center.X);
                float delta = Mathf.Wrap(a1 - a0, -Mathf.Pi, Mathf.Pi);
                float ang = a0 + delta * Smooth(k);
                float r = Mathf.Lerp((threeSpot - center).Length(), (shotSpot - center).Length(), Smooth(k)) + 0.6f * Mathf.Sin(k * Mathf.Pi);
                K.Pos = k < 1f ? center + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * r : shotSpot;
                // giro de 360° no corpo enquanto contorna (termina virado para a cesta)
                K.Yaw = YawTo(shotSpot, rim) - Mathf.Tau * (1f - Smooth(k));
                K.St.Speed = k < 1f ? 8f : 0f;
                if (k >= 1f)
                {
                    float hk = Mathf.Clamp((u - KSpinMove) / 0.35f, 0, 1);
                    K.Pos = shotSpot + Vector3.Up * (1.1f * Ease(hk));
                    K.St.Airborne = true; K.St.VelY = 0.3f; K.St.Charging = true; K.St.Charge = 0.5f;
                    K.St.Emote = Expressions.Focado;
                    ball = K.Pos + Vector3.Up * 2.0f + Dir(K.Yaw) * 0.33f;
                }
                else
                {
                    K.St.Dribbling = true; K.St.BallSide = -1;
                    ball = Held(K, drib * 2f, -1, true);
                }
                if (D2 != null)
                {
                    D2.Pos = d2Spot + Vector3.Up * Mathf.Max(0, 1.5f - 1.5f * Mathf.Clamp(u / 1.1f, 0, 1));
                    D2.St.Airborne = D2.Pos.Y > 0.05f; D2.St.VelY = -2f; D2.St.Emote = Expressions.Surpreso;
                    D2.Yaw = YawTo(d2Spot, threeSpot) + Mathf.Clamp(u / 0.6f, 0, 1) * 1.2f;
                }
                if (k < 1f)
                {
                    float oa = ang + 0.6f;
                    Cam(center + new Vector3(Mathf.Cos(oa), 0, Mathf.Sin(oa)) * 4.5f + Vector3.Up * 1.4f, K.Pos + Vector3.Up * 1.2f, 66);
                    _ov.Speed = 0.8f;
                }
                else
                {
                    // de baixo para cima: o King no ar com a cesta ao fundo
                    var toRim = ((rim - shotSpot) with { Y = 0 }).Normalized();
                    var cp = shotSpot - toRim * 2.6f + Right(ShotMath.YawOf(toRim)) * 1.6f + Vector3.Up * 0.6f;
                    Cam(cp, (K.Pos + Vector3.Up * 1.6f).Lerp(rim, 0.35f), 48);
                    _ov.Vignette = 0.6f;
                }
                break;
            }
            case KScore:
            case KRimOut:
            {
                bool make = Stage == KScore;
                K.Pos = shotSpot + Vector3.Up * Mathf.Max(0, 1.1f - 1.1f * Mathf.Clamp(u / 0.7f, 0, 1));
                K.Yaw = YawTo(shotSpot, rim);
                K.St.HasBall = false; K.St.Airborne = K.Pos.Y > 0.05f; K.St.VelY = -2f;
                K.St.Release = Mathf.Clamp(1f - u / 0.45f, 0, 1);
                K.St.Mood = make && u > 1.2f ? 1 : 0;
                K.St.Emote = make ? (u > 1.2f ? Expressions.SorrisoFrio : Expressions.Focado) : (u > 1.3f ? Expressions.Bravo : Expressions.Focado);
                if (D2 != null) { D2.Pos = d2Spot; D2.St.Stunned = true; D2.St.Emote = Expressions.Surpreso; }
                var from = shotSpot + Vector3.Up * 3.1f + Dir(K.Yaw) * 0.3f;
                var toRim = ((rim - shotSpot) with { Y = 0 }).Normalized();
                var side = new Vector3(-toRim.Z, 0, toRim.X);
                var end = make ? rim + Vector3.Up * 0.05f : rim + side * (Tuning.I.RimRadius * 0.95f) - toRim * 0.1f;
                float fk = Mathf.Clamp(u / KBallFlight, 0, 1);
                if (fk < 1f) ball = from.Lerp(end, fk) + Vector3.Up * (2.4f * 4f * fk * (1f - fk));
                else if (make) ball = rim + Vector3.Down * Mathf.Min(4.0f, (u - KBallFlight) * 3.2f);
                else
                {
                    // bateu no aro e foi para fora
                    float bt = u - KBallFlight;
                    var outDir = (side * 1f - toRim * 0.6f).Normalized();
                    ball = end + outDir * (bt * 3.2f) + Vector3.Up * (2.2f * bt - 0.5f * 11f * bt * bt);
                    if (ball.Y < Tuning.I.BallRadius) ball.Y = Tuning.I.BallRadius + Mathf.Abs(Mathf.Sin(bt * 8f)) * 0.4f;
                }
                // câmera: segue a bola e fecha na cesta
                float ck = Smooth(u / KBallFlight);
                var camA = shotSpot - toRim * 3.0f + side * 1.4f + Vector3.Up * 2.0f;
                var camB = rim - toRim * 4.4f + side * -2.4f + Vector3.Down * 1.1f;
                Cam(camA.Lerp(camB, ck), (from.Lerp(rim, 0.5f)).Lerp(rim, ck), Mathf.Lerp(52, 44, ck));
                if (fk >= 1f && make && _flashCooldown <= 0) { _ov.Flash(Colors.White, 0.8f); _ov.ImpactFrame = 0.12f; _shake = 1f; _flashCooldown = 5f; }
                if (u > 1.6f)
                {
                    float pk = Smooth((u - 1.6f) / 0.8f);
                    Cam(camB.Lerp(K.Pos + toRim * 2.6f + Vector3.Up * 1.6f + side * 0.6f, pk), rim.Lerp(K.Pos + Vector3.Up * 1.3f, pk), 50);
                }
                break;
            }
            default:
                ball = Held(K, drib);
                break;
        }
        if (_flashCooldown > 0) _flashCooldown -= dt;
        if (K.St.HasBall) K.St.BallPos = ball;
        if (D1 != null && D1.St.HasBall) D1.St.BallPos = ball;
        _ball.GlobalPosition = ball;
        _ball.RotateY(dt * 6f);
    }

    static Vector3 Right(float yaw) => new(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));

    // =====================================================================
    // Bolt: Duelo

    void DuelFrame(float u, float dt)
    {
        var Bo = _act[0]; var O = _act[1];
        float s = CourtGeo.HoopSign(CourtGeo.AttackHoop(Team));
        var fwd = new Vector3(s, 0, 0);
        var rim = M.Geo.RimCenter[CourtGeo.AttackHoop(Team)];
        var bSpot = new Vector3(-s * 1.4f, 0, 0);
        var oSpot = new Vector3(s * 1.4f, 0, 0);
        float face = ShotMath.YawOf(fwd), back = ShotMath.YawOf(-fwd);
        float drib = _time * Mathf.Pi / 0.3f;
        _ov.Bars = Mathf.MoveToward(_ov.Bars, 1f, dt * 4f);
        _ov.Speed = 0; _ov.Vignette = 0.55f;
        Bo.Pos = bSpot; Bo.Yaw = face; Bo.St = Base(true); Bo.St.Emote = Expressions.Focado;
        O.Pos = oSpot; O.Yaw = back; O.St = Base(); O.St.Defending = true; O.St.Emote = Expressions.Bravo;
        var side = Right(face);
        Vector3 ball = Held(Bo, drib);
        var eyeB = Bo.Pos + Vector3.Up * 1.28f + Dir(face) * 0.3f;
        var eyeO = O.Pos + Vector3.Up * 1.28f + Dir(back) * 0.3f;
        bool beam = false; float beamW = 0.05f;

        switch (Stage)
        {
            case DIntro:
            {
                _ov.TitleT = u;
                if (u < 0.65f) Cam(eyeB + Dir(face) * 1.5f + side * 0.35f + Vector3.Up * 0.05f, eyeB, 30);
                else if (u < 1.3f) Cam(eyeO + Dir(back) * 1.5f - side * 0.35f + Vector3.Up * 0.05f, eyeO, 30);
                else
                {
                    float k = Smooth((u - 1.3f) / 0.7f);
                    beam = true; beamW = 0.05f + 0.05f * k;
                    Cam((bSpot + oSpot) * 0.5f + side * Mathf.Lerp(2.2f, 3.6f, k) + Vector3.Up * 1.75f, (eyeB + eyeO) * 0.5f, Mathf.Lerp(44, 56, k));
                }
                break;
            }
            case DDuel:
            {
                beam = true; beamW = 0.09f + 0.015f * Mathf.Min(Round, 8); // v0.27: mais fino
                Bo.St.Dribbling = true;
                ball = Held(Bo, drib * 1.5f, Mathf.Sin(_time * 4f) >= 0 ? 1 : -1, true);
                Bo.St.BallSide = Mathf.Sin(_time * 4f) >= 0 ? 1 : -1;
                float orbit = Mathf.Sin(_time * 0.6f) * 0.35f;
                var c = (bSpot + oSpot) * 0.5f;
                var cp = c + (side * Mathf.Cos(orbit) + fwd * Mathf.Sin(orbit)) * 4.2f + Vector3.Up * (1.5f + 0.1f * Mathf.Sin(_time * 1.3f));
                Cam(cp, c + Vector3.Up * 1.45f, 52 - Mathf.Min(Round, 10) * 0.8f);
                _ov.Vignette = 0.6f + Mathf.Min(Round, 10) * 0.02f;
                _ov.Speed = Mathf.Min(Round, 10) * 0.04f;
                break;
            }
            case DLose:
            {
                float k = Mathf.Clamp(u / 0.45f, 0, 1);
                Bo.St.HasBall = false; Bo.St.Stunned = u > 0.35f; Bo.St.Emote = Expressions.Surpreso;
                O.Pos = oSpot.Lerp(bSpot + fwd * 0.9f, Ease(k)); O.St.Dashing = u < 0.45f; O.St.Defending = false;
                O.St.HasBall = u > 0.3f; O.St.Emote = Expressions.SorrisoFrio;
                ball = u < 0.3f ? Held(Bo, drib).Lerp(O.Pos + Vector3.Up * 1.0f, u / 0.3f) : Held(O, drib);
                var mid = (Bo.Pos + O.Pos) * 0.5f;
                Cam(mid + side * 4f + Vector3.Up * 1.5f, mid + Vector3.Up * 1.0f, 50);
                break;
            }
            case DThunder:
            {
                // relâmpago: some do lugar, passa pelo adversário e aparece enterrando
                var behindO = oSpot + fwd * 2.4f;
                O.St.Stunned = u > 0.2f; O.St.Defending = false; O.St.Emote = Expressions.Surpreso;
                O.Yaw = back + Mathf.Clamp(u / 0.5f, 0, 1) * Mathf.Pi;
                if (u < 0.55f)
                {
                    float k = Ease(u / 0.18f);
                    Bo.Pos = bSpot.Lerp(behindO, k);
                    Bo.St.Dashing = true; Bo.St.Emote = Expressions.SorrisoFrio;
                    ball = Held(Bo, drib, 1, true);
                    LightningLine(bSpot + Vector3.Up * 1.1f, behindO + Vector3.Up * 1.1f, 0.12f * (1f - Mathf.Clamp((u - 0.2f) / 0.35f, 0, 1)), new Color(0.6f, 0.95f, 1f));
                    Cam(oSpot + side * 3.8f + Vector3.Up * 1.4f - fwd * 0.5f, oSpot + Vector3.Up * 1.0f + fwd * 0.5f, 62);
                    _ov.Speed = 1f;
                }
                else
                {
                    // enterrada de trovão — v0.27: mesmas posições da enterrada do jogo (o corpo para antes do aro e fica
                    // pendurado na frente dele; antes ele ia para perto demais e as mãos atravessavam o aro)
                    var tu = Tuning.I;
                    float k = Mathf.Clamp((u - 0.55f) / 0.6f, 0, 1);
                    var toRim = ((rim - oSpot) with { Y = 0 }).Normalized();
                    var take = rim - toRim * 2.6f; take.Y = 0;
                    var flyTo = rim - toRim * 0.45f + Vector3.Up * (0.35f - tu.DunkHandReach); flyTo.Y = Mathf.Max(flyTo.Y, 0.05f);
                    var hang = rim - toRim * (tu.RimRadius + 0.22f) - Vector3.Up * (tu.DunkHandReach - 0.12f); hang.Y = Mathf.Max(hang.Y, 0.05f);
                    if (k < 1f)
                    {
                        // sobe rápido e "flutua" no topo; na horizontal sai rápido e desacelera (como o voo da enterrada)
                        float kh = 1f - (1f - k) * (1f - k), kv = Mathf.Sin(k * Mathf.Pi * 0.5f);
                        Bo.Pos = take.Lerp(flyTo, kh) with { Y = Mathf.Lerp(take.Y, flyTo.Y, kv) };
                    }
                    else Bo.Pos = flyTo.Lerp(hang, Smooth((u - 1.15f) / 0.12f));
                    Bo.Yaw = ShotMath.YawOf(toRim);
                    Bo.St.Dunking = k < 1f; Bo.St.FlightK = k; Bo.St.RimPos = rim; Bo.St.Hanging = k >= 1f && u < 1.9f;
                    Bo.St.Emote = Expressions.Bravo;
                    if (u >= 1.9f) { Bo.Pos = hang.Lerp(hang with { Y = 0 }, Ease(Mathf.Clamp((u - 1.9f) / 0.4f, 0, 1))); Bo.St.Hanging = false; Bo.St.Airborne = Bo.Pos.Y > 0.05f; Bo.St.Mood = 1; }
                    ball = k < 1f ? Bo.Pos + Vector3.Up * Mathf.Lerp(1.2f, tu.DunkHandReach + 0.1f, Ease(k * 1.4f)) + Dir(Bo.Yaw) * Mathf.Lerp(0.4f, 0.55f, k)
                                  : rim + Vector3.Up * 0.45f + Vector3.Down * Mathf.Min(4.45f, (u - 1.15f) * 5f);
                    if (k < 1f)
                    {
                        // a bola (e as mãos) só chegam sobre o aro por cima — nunca atravessando a borda
                        float hd = ((ball - rim) with { Y = 0 }).Length();
                        float over = 1f - Mathf.SmoothStep(tu.RimRadius, tu.RimRadius + 0.6f, hd);
                        ball.Y = Mathf.Max(ball.Y, Mathf.Lerp(ball.Y, rim.Y + 0.4f, over));
                    }
                    Bo.St.HasBall = k < 1f;
                    var cp = rim - toRim * 5.2f + Right(Bo.Yaw) * 2.6f + Vector3.Down * 1.6f;
                    Cam(cp, rim + Vector3.Down * 0.6f, 54);
                    if (k >= 1f && _flashCooldown <= 0)
                    {
                        _ov.Flash(new Color("bff4ff"), 1f); _ov.ImpactFrame = 0.15f; _shake = 1.5f; _flashCooldown = 5f;
                        LightningStrike(rim);
                    }
                    if (u > 1.15f && u < 1.5f) LightningLine(rim + Vector3.Up * 14f, rim, 0.25f * (1f - (u - 1.15f) / 0.35f), new Color(0.75f, 0.95f, 1f));
                }
                break;
            }
        }
        if (_flashCooldown > 0) _flashCooldown -= dt;
        if (beam) Glare(eyeB, eyeO, beamW); else ClearBeamIfIdle();
        if (Bo.St.HasBall) Bo.St.BallPos = ball;
        if (O.St.HasBall) O.St.BallPos = ball;
        _ball.GlobalPosition = ball;
    }

    void LightningStrike(Vector3 rim) => ImpactFx.Dunk(this, rim, 1.4f);

    // ---------- raios (olhar elétrico do duelo e rastros) ----------
    bool _beamDrawn;
    void ClearBeam() { _beamMesh?.ClearSurfaces(); _beamDrawn = false; }
    void ClearBeamIfIdle() { if (!_beamDrawn) _beamMesh?.ClearSurfaces(); _beamDrawn = false; }

    void Glare(Vector3 a, Vector3 b, float w)
    {
        // dois raios saindo dos olhos que se chocam no meio, com faíscas
        // v0.27: mais fino, cores mais suaves (meio transparentes) e sem a linha branca tremendo no meio; o zigue-zague só
        // muda ~12 vezes por segundo (antes mudava todo quadro)
        var mid = (a + b) * 0.5f + new Vector3(0, Mathf.Sin(_time * 9f) * 0.03f, 0);
        _fxRng.Seed = (ulong)(Mathf.FloorToInt(_time * 12f) + 1000);
        _beamMesh.ClearSurfaces();
        _beamMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        Ribbon(a, mid, w, new Color(0.62f, 0.92f, 1f, 0.55f), 9, 0.07f);
        Ribbon(b, mid, w, new Color(1f, 0.7f, 0.55f, 0.55f), 9, 0.07f);
        for (int i = 0; i < 3; i++)
        {
            var d = new Vector3(_fxRng.RandfRange(-1, 1), _fxRng.RandfRange(-1, 1), _fxRng.RandfRange(-1, 1)).Normalized() * _fxRng.RandfRange(0.12f, 0.32f);
            Ribbon(mid, mid + d, w * 0.45f, new Color(1f, 0.96f, 0.8f, 0.6f), 3, 0.05f);
        }
        _beamMesh.SurfaceEnd();
        _beamDrawn = true;
    }

    void LightningLine(Vector3 a, Vector3 b, float w, Color c)
    {
        if (w <= 0.002f) return;
        if (!_beamDrawn) _beamMesh.ClearSurfaces();
        _beamMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        Ribbon(a, b, w, c, 12, 0.25f);
        Ribbon(a, b, w * 0.35f, Colors.White, 12, 0.18f);
        _beamMesh.SurfaceEnd();
        _beamDrawn = true;
    }

    /// <summary>Fita em zigue-zague virada para a câmera (um raio).</summary>
    void Ribbon(Vector3 a, Vector3 b, float w, Color c, int segs, float jag)
    {
        var camPos = _cam.GlobalPosition;
        var dir = b - a;
        float len = dir.Length();
        if (len < 1e-3f) return;
        var prev = a;
        var perp = dir.Cross(Vector3.Up);
        if (perp.LengthSquared() < 1e-4f) perp = dir.Cross(Vector3.Right);
        perp = perp.Normalized();
        var perp2 = dir.Cross(perp).Normalized();
        for (int i = 1; i <= segs; i++)
        {
            float k = i / (float)segs;
            var p = a + dir * k;
            if (i < segs) p += (perp * _fxRng.RandfRange(-1, 1) + perp2 * _fxRng.RandfRange(-1, 1)) * (jag * len / segs * 2f);
            var seg = p - prev;
            var toCam = (camPos - (prev + p) * 0.5f).Normalized();
            var off = seg.Cross(toCam).Normalized() * (w * 0.5f);
            var c0 = c;
            _beamMesh.SurfaceSetColor(c0); _beamMesh.SurfaceAddVertex(prev - off);
            _beamMesh.SurfaceSetColor(c0); _beamMesh.SurfaceAddVertex(prev + off);
            _beamMesh.SurfaceSetColor(c0); _beamMesh.SurfaceAddVertex(p + off);
            _beamMesh.SurfaceSetColor(c0); _beamMesh.SurfaceAddVertex(prev - off);
            _beamMesh.SurfaceSetColor(c0); _beamMesh.SurfaceAddVertex(p + off);
            _beamMesh.SurfaceSetColor(c0); _beamMesh.SurfaceAddVertex(p - off);
            prev = p;
        }
    }

    /// <summary>Sem o modelo do Blender: um boneco simples (só para não quebrar).</summary>
    sealed class PuppetFallback : IPlayerVisual
    {
        public Node3D Root { get; }
        public PuppetFallback(Node3D parent, int team)
        {
            Root = new Node3D();
            parent.AddChild(Root);
            Root.AddChild(Mat.MeshNode(new CapsuleMesh { Radius = 0.35f, Height = 1.8f }, Mat.Toon(Palette.Team[team]), new Vector3(0, 0.9f, 0)));
        }
        public void Animate(in RigState s, float dt) => Root.Rotation = new Vector3(0, s.Yaw, 0);
    }
}
