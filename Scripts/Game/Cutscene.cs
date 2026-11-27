using System;
using System.Linq;
using Godot;

namespace AB;

public enum CutKind : byte { None, KingUlt, BoltDuel, UltPose }

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
    public const int KIntro = 0, KCheck1 = 1, KSteal = 2, KBreak = 3, KRun = 4, KBlock = 5, KSpin = 6, KScore = 7, KRimOut = 8, KBlocked = 9;
    const float KIntroDur = 1.7f, KStealDur = 1.6f, KBreakDur = 0.9f, KRunDur = 1.0f, KBlockDur = 0.75f, KSpinMove = 0.55f, KScoreDur = 2.4f, KRimDur = 2.1f, KBlockedDur = 2.0f;
    const float KBlockedHit = 0.32f;
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
        else if (kind == CutKind.UltPose) { ActorIds[1] = -1; ActorIds[2] = -1; } // v0.35: só o dono fazendo pose
        else { ActorIds[1] = target.Id; ActorChars[1] = (byte)target.CharacterId; ActorIds[2] = -1; }
        Sfx.Play2D(M, "cut_start", -3);
        M.LogEvent(kind == CutKind.KingUlt ? "CUT_KING" : kind == CutKind.BoltDuel ? "CUT_DUEL" : "CUT_POSE", owner, target);
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

        if (Kind == CutKind.UltPose)
        {
            var po = Owner;
            if (po != null && po.CharacterId == Characters.Luck)
            {
                if (Cross(prev, StageT, LuckLandAt)) Sfx.Play2D(M, "dice", -2);
                if (Cross(prev, StageT, LuckCheerAt)) Sfx.Play2D(M, "chime", -6);
            }
            if (StageT >= Mathf.Max(0.3f, PoseDuration)) Finish(true);
            return;
        }
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
                    if (StageT >= KBreakDur) Next(KRun);
                    break;
                case KRun:
                    // v0.33: skill check do meio, enquanto o adversário pula para bloquear
                    if (StageT >= KRunDur) { Next(KBlock); A.Start(Jit(t.KingUltCheckBlockTime), t.KingUltCheckBlockWindow, KBlockDur * 0.4f); }
                    break;
                case KBlock:
                    A.Tick(dt, io, go);
                    if (A.Done && A.Since >= Linger)
                    {
                        if (A.Ok) { Next(KSpin); A.Start(Jit(t.KingUltCheck2Time), t.KingUltCheck2Window, KSpinMove + 0.15f); }
                        else Next(KBlocked);
                    }
                    break;
                case KBlocked:
                    if (Cross(prev, StageT, KBlockedHit)) { Sfx.Play2D(M, "cut_hit", -1); Sfx.Play2D(M, "board", -2, 0.8f); }
                    if (StageT >= KBlockedDur) Finish(false);
                    break;
                case KSpin:
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
        // v0.28: a janela encolhe junto com a duração (a zona desenhada = janela ÷ duração ficava maior a cada acerto)
        float win = Mathf.Max(t.BoltDuelWindow * dur / Mathf.Max(t.BoltDuelStartTime, 0.05f), t.BoltDuelMinWindow); // v0.46: nunca menor que a mínima
        A.Start(dur * (1f + M.Rng.RandfRange(-0.08f, 0.08f)), win, prep);
        B.Start(dur * (1f + M.Rng.RandfRange(-0.08f, 0.08f)), win, prep);
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

    /// <summary>v0.46 (cliente): avança os ponteiros entre os snapshots.</summary>
    public void ClientAdvance(float dt) { if (!Active) return; A.ClientAdvance(dt); B.ClientAdvance(dt); }

    /// <summary>Skill check deste jogador agora (dono = A, alvo do duelo = B), ou null.</summary>
    public SkillCheckState CheckFor(Player p)
    {
        if (!Active || p == null) return null;
        if (p.Id == OwnerId && (Stage == KCheck1 || Stage == KBlock || Stage == KSpin) && Kind == CutKind.KingUlt) return A;
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
    float _vt, _shake, _time, _flashCooldown, _blockReach = KBlockDur;
    bool _blockHitFx;
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
        // pose da ultimate: só o dono some (vira o boneco da cena); o resto fica parado à vista
        bool pose = Kind == CutKind.UltPose;
        foreach (var p in M.Players) if (p.Visible && (!pose || p.Id == OwnerId)) p.Visible = false;
        if (M.Ball.Visible && (!pose || PoseHasBall)) M.Ball.Visible = false;
        if (Kind == CutKind.KingUlt) KingFrame(_vt, dt);
        else if (pose) PoseFrame(_vt, dt);
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
        if (Kind == CutKind.UltPose && ActorChars[0] == Characters.Luck)
            for (int i = 0; i < 2; i++) { _dice[i] = MakeDie(); _dice[i].Visible = false; AddChild(_dice[i]); }
        if (Kind == CutKind.UltPose && ActorChars[0] == Characters.Iron) { _anvil = MakeAnvil(); AddChild(_anvil); }
        if (Kind == CutKind.UltPose && ActorChars[0] == Characters.One) { _shadow = MakeShadowFloor(); AddChild(_shadow); _domSnd = false; }
        _poseFx = false; _anvilHit = false; _anvilLand = false;
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
        int titleChar = Kind == CutKind.KingUlt ? Characters.King : Kind == CutKind.BoltDuel ? Characters.Bolt : ActorChars[0];
        _ov.Title = Characters.Get(titleChar).Abilities[3].Name.ToUpperInvariant();
        _ov.Accent = Kind == CutKind.KingUlt ? new Color("e8313a") : Kind == CutKind.BoltDuel ? new Color("3fd7ff") : Characters.Get(titleChar).Color;
        _ov.TitleT = 0;
        _time = 0;
    }

    void TearDown()
    {
        _visKind = CutKind.None;
        _visStage = -1;
        foreach (var c in GetChildren()) c.QueueFree();
        for (int i = 0; i < 3; i++) _act[i] = null;
        _cam = null; _ball = null; _ov = null; _beam = null; _dice[0] = _dice[1] = null; _anvil = null; _shadow = null; _shadowMat = null;
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
            if (stage == KBlock) _blockReach = KBlockDur;
            if (stage == KBlocked) _blockHitFx = false;
        }
        else if (Kind == CutKind.UltPose)
        {
            _ov.Flash(_ov.Accent.Lightened(0.5f), 0.35f);
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
                // v0.33: o pulo do defensor chega no alto junto com a zona do skill check (câmera lenta)
                if (A.Active) _blockReach = Mathf.Max(0.2f, u + (A.Dur - A.T));
                float k = Mathf.Clamp(u / _blockReach, 0, 1);
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
            case KBlocked:
            {
                // v0.33: errou o skill check do bloqueio: o King arremessa e o defensor dá o toco
                var toRim = ((rim - threeSpot) with { Y = 0 }).Normalized();
                var side = new Vector3(-toRim.Z, 0, toRim.X);
                float jk = Mathf.Clamp(u / 0.28f, 0, 1);
                float down = Mathf.Clamp((u - 0.5f) / 0.4f, 0, 1);
                K.Pos = threeSpot + Vector3.Up * (0.55f * Mathf.Sin(jk * Mathf.Pi * 0.5f) * (1f - down));
                K.Yaw = YawTo(threeSpot, rim);
                K.St.Airborne = K.Pos.Y > 0.05f; K.St.VelY = down > 0 ? -2f : 2f;
                K.St.HasBall = u < KBlockedHit; K.St.Charging = u < KBlockedHit; K.St.Charge = 0.6f;
                K.St.Release = u >= KBlockedHit ? Mathf.Clamp(1f - (u - KBlockedHit) / 0.4f, 0, 1) : 0;
                K.St.Emote = u < KBlockedHit + 0.15f ? Expressions.Focado : Expressions.Surpreso;
                var release = K.Pos + Vector3.Up * 2.2f + Dir(K.Yaw) * 0.35f;
                var hit = d2Spot + Vector3.Up * (1.5f + 2.2f) - toRim * 0.45f; // nas mãos do defensor, no alto do pulo
                if (u < KBlockedHit) ball = (threeSpot + Vector3.Up * 2.0f + Dir(K.Yaw) * 0.3f).Lerp(release, u / KBlockedHit).Lerp(hit, Smooth(u / KBlockedHit));
                else
                {
                    // a bola volta rebatida para trás e para o lado, quicando
                    float bt = u - KBlockedHit;
                    // tapa para baixo e para o lado (longe do King), quica e sai rolando
                    var outDir = (side * 1f - toRim * 0.25f).Normalized();
                    ball = hit + outDir * (6.5f * bt / (1f + bt)) + Vector3.Up * (-2.5f * bt - 7f * bt * bt);
                    float rb = Tuning.I.BallRadius;
                    if (ball.Y < rb)
                    {
                        float tb = bt - 0.42f; // ~quando chega no chão
                        ball.Y = rb + Mathf.Abs(Mathf.Sin(Mathf.Max(tb, 0) * 6f)) * 1.1f * Mathf.Exp(-tb * 2.2f);
                    }
                }
                if (D2 != null)
                {
                    float top = 1.5f;
                    D2.Pos = d2Spot + Vector3.Up * Mathf.Max(0, top - top * Mathf.Clamp((u - KBlockedHit) / 0.7f, 0, 1));
                    D2.Yaw = YawTo(d2Spot, threeSpot);
                    D2.St.Airborne = D2.Pos.Y > 0.05f; D2.St.VelY = u < KBlockedHit ? 0.5f : -2f;
                    D2.St.Defending = false; D2.St.Release = u >= KBlockedHit ? Mathf.Clamp(1f - (u - KBlockedHit) / 0.3f, 0, 1) : 0;
                    D2.St.Emote = u > KBlockedHit ? Expressions.SorrisoFrio : Expressions.Bravo;
                    D2.St.Mood = u > 0.9f ? 1 : 0;
                }
                if (u >= KBlockedHit && !_blockHitFx) { _blockHitFx = true; _ov.Flash(new Color(1, 0.2f, 0.2f), 0.5f); _ov.ImpactFrame = 0.1f; _shake = 1.2f; }
                var mid = (threeSpot + d2Spot) * 0.5f;
                var cp = mid + Right(face) * 5.0f + Vector3.Up * 1.1f - fwd * 1.0f;
                float ck = Smooth((u - KBlockedHit) / 1.2f);
                Cam(cp.Lerp(cp + Right(face) * 1.5f + Vector3.Up * 0.6f - fwd * 1.5f, ck), (mid + Vector3.Up * 2.2f).Lerp(ball, 0.45f * ck), Mathf.Lerp(46, 54, ck));
                _ov.Vignette = 0.55f;
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
    // v0.35/v0.36: cena curta da ultimate (ults que não são cutscene) — avisa todo mundo. Cada personagem tem a SUA cena,
    // ligada ao que a ultimate faz (pedido do usuário: não é só uma comemoração). Sem a bola em cena.

    bool PoseHasBall => M.Ball.Holder != null && M.Ball.Holder.Id == OwnerId;

    // Luck (Sorte Infinita): joga dois dados (assets/models/dado.glb), eles giram no ar e caem com o 6 para cima
    public const float LuckThrowAt = 0.35f, LuckLandAt = 1.05f, LuckCheerAt = 1.25f;
    readonly Node3D[] _dice = new Node3D[2];
    bool _poseFx;

    static Node3D MakeDie()
    {
        var n = new Node3D { Name = "Dado" };
        const string path = "res://assets/models/dado.glb";
        if (ResourceLoader.Exists(path) && GD.Load<PackedScene>(path) is { } sc)
        {
            var m = sc.Instantiate<Node3D>();
            Mat.ApplyToon(m, name => name == "Dado" ? 0.012f : 0f);
            n.AddChild(m);
        }
        else n.AddChild(Mat.MeshNode(new BoxMesh { Size = Vector3.One * 0.26f }, Mat.ToonOutlined(new Color("ede6d6"), 0.012f), Vector3.Zero));
        return n;
    }

    void PoseFrame(float u, float dt)
    {
        var L = _act[0];
        if (L == null) return;
        var o = Owner;
        var pos = o != null && IsInstanceValid(o) ? o.GlobalPosition with { Y = 0 } : Vector3.Zero;
        float yaw = o?.Yaw ?? 0f;
        _ov.Bars = Mathf.MoveToward(_ov.Bars, 1f, dt * 6f);
        _ov.Speed = 0; _ov.Vignette = 0.4f;
        _ov.TitleT = u;
        ClearBeam();
        _ball.Visible = false;
        L.Pos = pos; L.Yaw = yaw; L.St = Base();
        if (ActorChars[0] == Characters.Luck) LuckPose(L, pos, yaw, u);
        else if (ActorChars[0] == Characters.Iron) IronPose(L, pos, yaw, u);
        else if (ActorChars[0] == Characters.One) OnePose(L, pos, yaw, u);
        else
        {
            // genérico (personagem novo ainda sem cena própria): comemoração com a câmera fechando
            float k = Smooth(u / Mathf.Max(0.3f, Tuning.I.UltPoseDuration));
            L.St.Mood = 1; L.St.Emote = Expressions.Feliz;
            var f = Dir(yaw); var r = Right(yaw);
            Cam((pos + f * 4.4f + r * 1.7f + Vector3.Up * 1.0f).Lerp(pos + f * 3.3f - r * 0.6f + Vector3.Up * 1.6f, k),
                pos + Vector3.Up * Mathf.Lerp(1.3f, 1.6f, k), Mathf.Lerp(50, 46, k));
        }
    }

    void LuckPose(Actor L, Vector3 pos, float yaw, float u)
    {
        var f = Dir(yaw); var r = Right(yaw);
        float half = 0.13f;
        var rest = Basis.FromEuler(new Vector3(0, yaw, 0));
        var lands = new[] { pos + f * 1.7f - r * 0.32f, pos + f * 1.55f + r * 0.38f };
        var handPos = pos + Vector3.Up * 1.45f + f * 0.45f + r * 0.2f;
        var mid = (lands[0] + lands[1]) * 0.5f;

        // corpo: prepara (sorriso de canto) → lança (braço na frente) → comemora quando o 6 e 6 aparecem
        if (u < LuckThrowAt) { L.St.Emote = Expressions.SorrisoFrio; L.St.Charging = true; L.St.Charge = Mathf.Clamp(u / LuckThrowAt, 0, 1) * 0.5f; }
        else if (u < LuckCheerAt) { L.St.Emote = Expressions.Focado; L.St.Release = Mathf.Clamp(1f - (u - LuckThrowAt) / 0.5f, 0, 1); }
        else { L.St.Mood = 1; L.St.Emote = Expressions.Feliz; }

        for (int i = 0; i < 2; i++)
        {
            var d = _dice[i];
            if (d == null) continue;
            d.Visible = u >= LuckThrowAt;
            if (!d.Visible) continue;
            var axis = new Vector3(i == 0 ? 1f : 0.6f, 0.4f, i == 0 ? 0.5f : -1f).Normalized();
            Vector3 p; float spin;
            if (u < LuckLandAt)
            {
                float k = (u - LuckThrowAt) / (LuckLandAt - LuckThrowAt);
                var start = handPos + r * (i == 0 ? -0.1f : 0.1f);
                var end = lands[i] + Vector3.Up * half;
                p = start.Lerp(end, k) + Vector3.Up * (2.6f * 4f * k * (1f - k) * (i == 0 ? 1f : 0.85f));
                spin = (1f - k) * (i == 0 ? 15f : 12f) + 0.6f;
            }
            else
            {
                // quiquinho e assenta com o 6 para cima
                float bt = u - LuckLandAt;
                float hop = 0.14f * Mathf.Max(0, Mathf.Sin(Mathf.Clamp(bt / 0.22f, 0, 1) * Mathf.Pi));
                p = lands[i] + Vector3.Up * (half + hop) + f * (bt < 0.22f ? bt * 0.4f : 0.088f);
                spin = 0.6f * (1f - Smooth(bt / 0.22f));
            }
            d.GlobalPosition = p;
            d.GlobalBasis = new Basis(axis, spin) * rest;
        }

        // efeitos locais: clarão verde quando os dados assentam
        if (u >= LuckCheerAt && !_poseFx) { _poseFx = true; _ov.Flash(new Color("b8ffcf"), 0.45f); _shake = 0.4f; }

        // câmera: de frente → acompanha os dados subindo → baixa, nos dados com o Luck comemorando atrás
        var c0 = pos + f * 3.8f + r * 1.2f + Vector3.Up * 1.3f;
        var l0 = pos + Vector3.Up * 1.3f;
        var c1 = pos + f * 4.2f - r * 0.8f + Vector3.Up * 1.0f;
        var l1 = pos + f * 1.0f + Vector3.Up * 2.2f;
        var c2 = mid + f * 2.6f + r * 0.5f + Vector3.Up * 1.75f;
        var l2 = mid.Lerp(pos + Vector3.Up * 1.2f, 0.5f) + Vector3.Up * 0.3f;
        if (u < LuckThrowAt + 0.3f)
        {
            float k = Smooth(u / (LuckThrowAt + 0.3f));
            Cam(c0.Lerp(c1, k), l0.Lerp(l1, k), Mathf.Lerp(48, 54, k));
        }
        else
        {
            float k = Smooth((u - LuckThrowAt - 0.3f) / 0.55f);
            Cam(c1.Lerp(c2, k), l1.Lerp(l2, k), Mathf.Lerp(54, 46, k));
        }
    }

    // =====================================================================
    // v0.37 — Iron (Indestrutível): uma bigorna (assets/models/bigorna.glb) cai na cabeça dele, faz "KLANG" e quica
    // para longe; ele nem pisca. Depois solta o grito de guerra com um brilho prateado.
    public const float IronHitAt = 0.6f, IronAnvilLandAt = 1.3f, IronShoutAt = 1.4f;
    Node3D _anvil;
    bool _anvilHit, _anvilLand;

    static Node3D MakeAnvil()
    {
        var n = new Node3D { Name = "Bigorna" };
        const string path = "res://assets/models/bigorna.glb";
        if (ResourceLoader.Exists(path) && GD.Load<PackedScene>(path) is { } sc)
        {
            var m = sc.Instantiate<Node3D>();
            m.Scale = Vector3.One * 1.3f;
            Mat.ApplyToon(m, name => name == "Ferro" ? 0.014f : 0f);
            n.AddChild(m);
        }
        else n.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.8f, 0.5f, 0.3f) }, Mat.ToonOutlined(new Color("4b5058"), 0.014f), Vector3.Up * 0.25f));
        return n;
    }

    void IronPose(Actor L, Vector3 pos, float yaw, float u)
    {
        var f = Dir(yaw); var r = Right(yaw);
        float head = 1.23f * Tuning.I.CharacterScale;          // topo da cabeça do modelo (make_iron.py) na escala do jogo
        var top = pos + Vector3.Up * head;
        var land = pos + r * 1.5f + f * 0.9f;

        // corpo: parado e sério (nem liga) → o impacto amassa um pouquinho → grito de guerra
        L.St.Emote = Expressions.Serio;
        if (u >= IronHitAt && u < IronHitAt + 0.18f) L.St.Landing = true;
        if (u >= IronShoutAt) { L.St.Emote = Expressions.Feliz; L.St.Defending = true; }

        if (_anvil != null)
        {
            Vector3 p; float spin;
            var rest = Basis.FromEuler(new Vector3(0, yaw + 0.5f, 0));
            if (u < IronHitAt)
            {
                float k = Mathf.Clamp(u / IronHitAt, 0, 1);
                p = top + Vector3.Up * (7.5f * (1f - k * k));      // cai acelerando
                spin = 0;
            }
            else if (u < IronAnvilLandAt)
            {
                float k = (u - IronHitAt) / (IronAnvilLandAt - IronHitAt);
                p = top.Lerp(land, k) + Vector3.Up * (2.2f * 4f * k * (1f - k));
                spin = k * 4.2f;
            }
            else
            {
                float bt = u - IronAnvilLandAt;
                float hop = 0.12f * Mathf.Max(0, Mathf.Sin(Mathf.Clamp(bt / 0.18f, 0, 1) * Mathf.Pi));
                p = land + Vector3.Up * hop;
                spin = 4.2f + 0.3f * Smooth(bt / 0.18f);
            }
            _anvil.GlobalPosition = p;
            _anvil.GlobalBasis = new Basis(f, spin) * rest;
        }

        // sons e efeitos só nesta tela (a cena roda igual em todo mundo)
        if (u >= IronHitAt && !_anvilHit)
        {
            _anvilHit = true;
            _ov.Flash(Colors.White, 0.5f); _ov.ImpactFrame = 0.1f; _shake = 1.1f;
            Sfx.PlayLocal3D(this, "clang", top, 2f);
            Sfx.PlayLocal3D(this, "bonk", top, -2f, 0.7f);
        }
        if (u >= IronAnvilLandAt && !_anvilLand)
        {
            _anvilLand = true;
            _shake = Mathf.Max(_shake, 0.5f);
            Sfx.PlayLocal3D(this, "cut_hit", land, 0f, 0.8f);
            ImpactFx.Dust(M, land, 0.9f);
        }
        if (u >= IronShoutAt && !_poseFx) { _poseFx = true; _ov.Flash(new Color("e6edf5"), 0.45f); Sfx.PlayLocal3D(this, "clang", top, -6f, 0.75f); }

        // câmera: baixa, de frente, olhando para cima (vê a bigorna caindo) → fecha no Iron gritando
        // (o Iron fica à direita do quadro: o título da ult entra pela esquerda)
        var c0 = pos + f * 4.4f - r * 0.2f + Vector3.Up * 0.9f;
        var l0 = pos + r * 1.2f + Vector3.Up * 2.0f;
        var c1 = pos + f * 4.0f + r * 0.4f + Vector3.Up * 1.3f;
        var l1 = pos + r * 0.6f + Vector3.Up * 1.3f;
        var c2 = pos + f * 2.9f - r * 0.5f + Vector3.Up * 1.45f;
        var l2 = pos + Vector3.Up * 1.35f;
        var lHit = top + r * 1.1f + Vector3.Up * 0.3f;
        if (u < IronHitAt) Cam(c0, l0.Lerp(lHit, Smooth(u / IronHitAt)), 58);
        else if (u < IronShoutAt) { float k = Smooth((u - IronHitAt) / (IronShoutAt - IronHitAt)); Cam(c0.Lerp(c1, k), lHit.Lerp(l1, k), Mathf.Lerp(58, 52, k)); }
        else { float k = Smooth((u - IronShoutAt) / 0.5f); Cam(c1.Lerp(c2, k), l1.Lerp(l2, k), Mathf.Lerp(52, 44, k)); }
    }

    // =====================================================================
    // v0.41 — One (Domínio das Sombras): os olhos dele brilham, ele abre os braços e uma mancha de sombra roxa sai dos
    // pés e engole a quadra inteira (com os outros jogadores parados nela); a câmera sobe para mostrar tudo.

    /// <summary>Duração da pose da ultimate do personagem que está nela (o One tem a cena mais longa).</summary>
    float PoseDuration => ActorChars[0] == Characters.One ? Tuning.I.OneUltPoseDuration : Tuning.I.UltPoseDuration;

    public const float OneSpreadAt = 0.55f, OneCoverAt = 2.0f;
    MeshInstance3D _shadow;
    ShaderMaterial _shadowMat;
    bool _domSnd;

    const string ShadowShader = @"
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;
uniform vec2 center = vec2(0.0);
uniform float radius = 0.0;
uniform float time = 0.0;
varying vec3 wp;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    vec2 d = wp.xz - center;
    float r = length(d);
    float a = atan(d.y, d.x);
    // borda irregular com 'tentáculos' que avançam na frente
    float edge = radius + 0.9 * sin(a * 7.0 + time * 2.3) + 0.6 * sin(a * 13.0 - time * 3.1) + 1.2 * (noise(vec2(a * 3.0, time * 0.8)) - 0.5) * min(radius * 0.15, 3.0);
    float inside = 1.0 - smoothstep(edge - 0.7, edge, r);
    float rim = smoothstep(edge - 2.2, edge - 0.2, r) * inside;
    // veias escuras girando por dentro
    float veins = smoothstep(0.55, 0.8, noise(vec2(a * 4.0 + time * 0.4, r * 0.35 - time * 0.9)));
    vec3 col = mix(vec3(0.006, 0.001, 0.016), vec3(0.045, 0.008, 0.1), veins * 0.8);
    col = mix(col, vec3(0.45, 0.14, 0.95), rim * 0.9);
    if (inside < 0.5) discard;
    ALBEDO = col;
}";

    MeshInstance3D MakeShadowFloor()
    {
        _shadowMat = new ShaderMaterial { Shader = new Shader { Code = ShadowShader } };
        var mi = new MeshInstance3D
        {
            Name = "Sombras",
            Mesh = new PlaneMesh { Size = new Vector2(160, 160) },
            MaterialOverride = _shadowMat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0.035f, 0),
        };
        return mi;
    }

    void OnePose(Actor L, Vector3 pos, float yaw, float u)
    {
        var f = Dir(yaw); var r = Right(yaw);
        float dur = Mathf.Max(0.3f, PoseDuration);
        float spreadK = Mathf.Clamp((u - OneSpreadAt) / Mathf.Max(0.3f, OneCoverAt - OneSpreadAt), 0, 1);
        float radius = 60f * spreadK * spreadK * (0.4f + 0.6f * spreadK) + 1.5f * Smooth(u / OneSpreadAt);

        // corpo: olhar frio (olhos brilhando) e braços abertos; no fim, o sorriso sinistro
        L.St.Emote = u < OneCoverAt ? Expressions.Bravo : Expressions.SorrisoFrio;
        L.St.Defending = u >= 0.25f;

        if (_shadow != null)
        {
            _shadow.GlobalPosition = new Vector3(pos.X, 0.035f, pos.Z);
            _shadowMat.SetShaderParameter("center", new Vector2(pos.X, pos.Z));
            _shadowMat.SetShaderParameter("radius", radius);
            _shadowMat.SetShaderParameter("time", _time);
        }
        _ov.Vignette = Mathf.Lerp(0.4f, 0.75f, spreadK);

        // som e efeitos só nesta tela
        if (!_domSnd) { _domSnd = true; Sfx.PlayLocal3D(this, "domain", pos + Vector3.Up, 2f); Sfx.PlayLocal3D(this, "realm", pos + Vector3.Up, -2f, 0.6f); }
        if (u >= OneCoverAt && !_poseFx) { _poseFx = true; _ov.Flash(new Color("c597ff"), 0.5f); _shake = 0.6f; Sfx.PlayLocal3D(this, "curse", pos + Vector3.Up, 0f, 0.7f); }

        // câmera: perto e baixa no rosto → sobe e se afasta mostrando a quadra sendo engolida → fica no alto
        var c0 = pos + f * 2.6f + r * 0.5f + Vector3.Up * 1.25f;
        var l0 = pos + Vector3.Up * 1.25f;
        var c1 = pos + f * 7f - r * 4f + Vector3.Up * 8f;
        var l1 = pos + f * 2.5f + Vector3.Up * 0.2f;
        if (u < OneSpreadAt + 0.15f) Cam(c0.Lerp(c0 + f * 0.4f, Smooth(u / (OneSpreadAt + 0.15f))), l0, 48);
        else
        {
            float k = Smooth((u - OneSpreadAt - 0.15f) / Mathf.Max(0.3f, dur - OneSpreadAt - 0.5f));
            Cam(c0.Lerp(c1, k), l0.Lerp(l1, k), Mathf.Lerp(48, 62, k));
        }
    }

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
                    beam = true; beamW = 0.01f + 0.008f * k;
                    Cam((bSpot + oSpot) * 0.5f + side * Mathf.Lerp(2.2f, 3.6f, k) + Vector3.Up * 1.75f, (eyeB + eyeO) * 0.5f, Mathf.Lerp(44, 56, k));
                }
                break;
            }
            case DDuel:
            {
                beam = true; beamW = 0.018f + 0.002f * Mathf.Min(Round, 8); // v0.28: uma linha fina
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
        // v0.28: uma linha fina só (ciano do Bolt → laranja do adversário, ondulando de leve) e, no meio, onde os olhares se
        // chocam, um brilho pequeno pulsando com 4 faíscas curtas que saem e somem suavemente (nada sorteado a cada quadro)
        var mid = (a + b) * 0.5f + new Vector3(0, Mathf.Sin(_time * 6f) * 0.02f, 0);
        var ca = new Color(0.55f, 0.92f, 1f, 0.95f);
        var cb = new Color(1f, 0.62f, 0.4f, 0.95f);
        var cm = new Color(1f, 0.97f, 0.88f, 1f);
        _beamMesh.ClearSurfaces();
        _beamMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        const int segs = 14;
        for (int i = 0; i < segs; i++)
        {
            float k0 = i / (float)segs, k1 = (i + 1) / (float)segs;
            Strip(Wave(a, mid, k0), Wave(a, mid, k1), w, ca.Lerp(cm, k0 * k0), ca.Lerp(cm, k1 * k1));
            Strip(Wave(b, mid, k0), Wave(b, mid, k1), w, cb.Lerp(cm, k0 * k0), cb.Lerp(cm, k1 * k1));
        }
        // brilho do choque: disco macio virado para a câmera, pulsando
        var basis = _cam.GlobalTransform.Basis;
        var right = basis.X; var up = basis.Y;
        float glow = 0.11f + 0.025f * Mathf.Sin(_time * 14f) + w * 0.6f;
        const int fan = 20;
        for (int i = 0; i < fan; i++)
        {
            float t0 = i / (float)fan * Mathf.Tau, t1 = (i + 1) / (float)fan * Mathf.Tau;
            Vtx(mid, cm with { A = 0.75f });
            Vtx(mid + (right * Mathf.Cos(t0) + up * Mathf.Sin(t0)) * glow, cm with { A = 0f });
            Vtx(mid + (right * Mathf.Cos(t1) + up * Mathf.Sin(t1)) * glow, cm with { A = 0f });
        }
        // faíscas: cada uma nasce no meio, voa um pouco para fora e some; a direção muda a cada "vida"
        for (int i = 0; i < 4; i++)
        {
            float life = _time * 2.2f + i * 0.25f;
            int gen = Mathf.FloorToInt(life);
            float f = life - gen;
            float ang = Hash(gen * 7 + i * 13) * Mathf.Tau;
            var dir = (right * Mathf.Cos(ang) + up * Mathf.Sin(ang) * 0.8f).Normalized();
            var p0 = mid + dir * (0.05f + 0.3f * f);
            var p1 = p0 + dir * (0.07f * (1f - f));
            Strip(p0, p1, w * 0.7f, cm with { A = 0.85f * (1f - f) }, cm with { A = 0f });
        }
        _beamMesh.SurfaceEnd();
        _beamDrawn = true;
    }

    /// <summary>Ponto da linha do olhar com uma ondinha suave (zero nas pontas).</summary>
    Vector3 Wave(Vector3 from, Vector3 to, float k)
    {
        var p = from.Lerp(to, k);
        float env = Mathf.Sin(k * Mathf.Pi);
        return p + Vector3.Up * (Mathf.Sin(k * 9f - _time * 10f) * 0.012f * env);
    }

    static float Hash(int n) { n = (n << 13) ^ n; return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f; }

    void Vtx(Vector3 p, Color c) { _beamMesh.SurfaceSetColor(c); _beamMesh.SurfaceAddVertex(p); }

    /// <summary>Faixa fina virada para a câmera de a até b, com cor em degradê.</summary>
    void Strip(Vector3 a, Vector3 b, float w, Color ca, Color cb)
    {
        var seg = b - a;
        if (seg.LengthSquared() < 1e-8f) return;
        var toCam = (_cam.GlobalPosition - (a + b) * 0.5f).Normalized();
        var off = seg.Cross(toCam).Normalized() * (w * 0.5f);
        Vtx(a - off, ca); Vtx(a + off, ca); Vtx(b + off, cb);
        Vtx(a - off, ca); Vtx(b + off, cb); Vtx(b - off, cb);
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
