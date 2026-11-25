using System;
using Godot;

namespace AB;

/// <summary>
/// v0.21 (Etapa 7): o Rei (assets/models/rei.glb, art_source/scripts/make_rei.py) com as animações feitas aqui na Godot.
///
/// Como funciona:
/// - Cada animação é uma POSE por osso (graus nos eixos do personagem: frente −Z, direita +X, cima +Y) calculada a
///   partir do estado do jogo (<see cref="RigState"/>): fase da passada, carga do arremesso, progresso do voo da
///   enterrada etc. Assim a animação fica sempre sincronizada com a física (quique da bola, duração do voo...).
/// - As camadas se misturam com pesos suaves (Tuning "Rapidez das transições"): locomoção (parado/andar/correr) →
///   estados por cima (arremesso, passe, drible, salto, roubo, enterrada...), com máscara de corpo de cima/baixo.
/// - As MÃOS não têm osso: são esferas que flutuam (Root) e vão até a bola (quique, arremesso, enterrada) ou até o aro.
/// - O rosto troca de objeto (Rosto_*): expressão da roda (5 s) ou automática pelo estado.
/// </summary>
public sealed class CharacterVisual : IPlayerVisual
{
    public const string ModelPath = "res://assets/models/rei.glb";
    static bool? _available;
    public static bool Available => _available ??= ResourceLoader.Exists(ModelPath);

    public Node3D Root { get; }
    readonly Player P;            // null no --pose-shot
    readonly Node3D _model;       // o .glb (girado 180°); a escala dele faz o "squash & stretch"
    readonly Skeleton3D _sk;
    readonly Node3D _handL, _handR;
    readonly Node3D[] _faces = new Node3D[Expressions.Count + 1];
    int _faceShown = -1;

    enum B { Hips, Spine, Chest, UpperChest, Neck, Head, ShL, ShR, ULegL, LLegL, FootL, ToesL, ULegR, LLegR, FootR, ToesR }
    const int NB = 16;
    static readonly string[] BoneNames =
    {
        "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head", "LeftShoulder", "RightShoulder",
        "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes", "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
    };
    static bool IsUpper(int i) => i >= (int)B.Spine && i <= (int)B.ShR;

    readonly int[] _bi = new int[NB];
    readonly Quaternion[] _rest = new Quaternion[NB];
    readonly Quaternion[] _frame = new Quaternion[NB];   // orientação de repouso do osso no espaço do Root
    Vector3 _hipsRest;
    Basis _hipsConv = Basis.Identity;
    float _thighLen = 0.19f, _shinLen = 0.185f;
    const float HandR = 0.078f;   // raio da mão no modelo

    [Flags] enum Mask { Upper = 1, Lower = 2, Hands = 4, Full = 7, UpperHands = 5 }

    /// <summary>Pose: rotação por osso (graus; X = pitch, Y = yaw, Z = roll, nos eixos do Root), quadril e mãos.</summary>
    sealed class Pose
    {
        public readonly Vector3[] R = new Vector3[NB];
        public Vector3 Hips, HandL, HandR;
        public float Squash;

        public void Clear()
        {
            Array.Clear(R);
            Hips = Vector3.Zero; HandL = new Vector3(-0.33f, 0.4f, -0.05f); HandR = new Vector3(0.33f, 0.4f, -0.05f); Squash = 0;
        }

        public void Mix(Pose o, float w, Mask m)
        {
            if (w <= 0.0001f) return;
            w = Mathf.Min(w, 1f);
            for (int i = 0; i < NB; i++)
            {
                bool up = IsUpper(i);
                if ((up && (m & Mask.Upper) != 0) || (!up && (m & Mask.Lower) != 0)) R[i] = R[i].Lerp(o.R[i], w);
            }
            if ((m & Mask.Lower) != 0) { Hips = Hips.Lerp(o.Hips, w); Squash = Mathf.Lerp(Squash, o.Squash, w); }
            if ((m & Mask.Hands) != 0) { HandL = HandL.Lerp(o.HandL, w); HandR = HandR.Lerp(o.HandR, w); }
        }
    }

    // camadas
    enum S { Ball, Defense, Celebrate, Sad, StealFail, Dribble, PassCharge, ShootCharge, JumpCharge, Air, Land, Steal, Stun, Dunk, Hang, Count }
    readonly float[] _w = new float[(int)S.Count];
    readonly Pose _pose = new(), _tmp = new(), _tmp2 = new();

    float _time, _phase, _moveW, _runW, _hangT, _catchT, _prevBallY, _apex, _landFx;
    bool _hadBall, _wasAir, _bigFall, _handInit, _ballHandRight = true, _rising;
    float _curMax;
    Vector3 _hl, _hr;

    public CharacterVisual(Player p) : this(p, p.Team, p) { }

    public CharacterVisual(Node3D parent, int team, Player p)
    {
        P = p;
        Root = new Node3D { Name = "Visual" };
        parent.AddChild(Root);
        _model = GD.Load<PackedScene>(ModelPath).Instantiate<Node3D>();
        _model.Name = "Rei";
        _model.Rotation = new Vector3(0, Mathf.Pi, 0); // o modelo olha para +Z; o jogo usa frente = −Z
        Root.AddChild(_model);
        Root.Scale = Vector3.One * Tuning.I.CharacterScale;
        ApplyMaterials(_model);

        _sk = _model.FindChild("*", true, false) as Skeleton3D ?? FindSkeleton(_model);
        _handL = Detach("MaoEsquerda");
        _handR = Detach("MaoDireita");
        for (int i = 1; i <= Expressions.Count; i++) _faces[i] = _model.FindChild("Rosto_" + Expressions.Node[i], true, false) as Node3D;
        SetupBones();

        // anel do time no chão (todos os jogadores usam o mesmo personagem por enquanto)
        var ringMat = Mat.Unshaded(Palette.Team[team] with { A = 0.85f });
        ringMat.NoDepthTest = false;
        var ring = Mat.MeshNode(new TorusMesh { InnerRadius = 0.42f, OuterRadius = 0.5f, Rings = 32, RingSegments = 6 }, ringMat,
            new Vector3(0, 0.02f, 0), false);
        ring.Scale = new Vector3(1, 0.08f, 1);
        ring.Name = "TeamRing";
        parent.AddChild(ring);
    }

    static Skeleton3D FindSkeleton(Node n)
    {
        foreach (var c in n.FindChildren("*", "Skeleton3D", true, false)) return (Skeleton3D)c;
        return null;
    }

    Node3D Detach(string name)
    {
        if (_model.FindChild(name, true, false) is not Node3D n) return null;
        n.GetParent().RemoveChild(n);
        n.Owner = null;
        Root.AddChild(n);
        n.Transform = Transform3D.Identity;
        n.PhysicsInterpolationMode = Node.PhysicsInterpolationModeEnum.Off;
        return n;
    }

    /// <summary>Materiais do .glb → toon do jogo (contorno por nome); rosto e brilho da coroa sem sombra.</summary>
    static void ApplyMaterials(Node root)
    {
        foreach (var child in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)child;
            string nm = mi.Name.ToString();
            // cabelo, coroa e rosto não fazem sombra (a sombra das mechas listrava a cara no toon)
            if (nm.StartsWith("Rosto_") || nm == "Cabeca" || nm == "Coroa") mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                var src = mi.Mesh.SurfaceGetMaterial(i);
                var c = src is BaseMaterial3D b ? b.AlbedoColor : Colors.White;
                string n = src?.ResourceName ?? "";
                Material m = n switch
                {
                    "CoroaBrilho" or "Gema" or "Olhos" or "Pupila" or "Brilho" or "Tinta" or "Boca" or "Dentes" => Mat.Unshaded(c),
                    "Pele" or "Cabelo" or "Calca" or "Botas" => Mat.ToonOutlined(c, 0.012f),
                    "Casaco" => Mat.ToonOutlined(c, 0.014f),
                    "Coroa" or "Sola" => Mat.ToonOutlined(c, 0.008f),
                    _ => Mat.Toon(c),
                };
                mi.SetSurfaceOverrideMaterial(i, m);
            }
        }
    }

    void SetupBones()
    {
        if (_sk == null) { GD.PushWarning("[ART] rei.glb sem Skeleton3D"); return; }
        var s = (Root.GlobalTransform.AffineInverse() * _sk.GlobalTransform).Basis;
        var sRot = s.Orthonormalized().GetRotationQuaternion();
        for (int i = 0; i < NB; i++)
        {
            int idx = _sk.FindBone(BoneNames[i]);
            _bi[i] = idx;
            if (idx < 0) { GD.PushWarning("[ART] osso não encontrado: " + BoneNames[i]); continue; }
            _rest[i] = _sk.GetBoneRest(idx).Basis.GetRotationQuaternion();
            _frame[i] = (sRot * _sk.GetBoneGlobalRest(idx).Basis.Orthonormalized().GetRotationQuaternion()).Normalized();
        }
        int hips = _bi[(int)B.Hips];
        if (hips >= 0)
        {
            _hipsRest = _sk.GetBoneRest(hips).Origin;
            int parent = _sk.GetBoneParent(hips);
            var pg = parent >= 0 ? _sk.GetBoneGlobalRest(parent).Basis : Basis.Identity;
            _hipsConv = (s * pg).Inverse();
        }
        float Len(B a, B b)
        {
            int ia = _bi[(int)a], ib = _bi[(int)b];
            return ia >= 0 && ib >= 0 ? (_sk.GetBoneGlobalRest(ia).Origin - _sk.GetBoneGlobalRest(ib).Origin).Length() : 0.19f;
        }
        _thighLen = Len(B.ULegL, B.LLegL);
        _shinLen = Len(B.LLegL, B.FootL);
    }

    // =========================================================================================== helpers de pose
    static void Add(Pose p, B b, float pitch, float yaw, float roll) => p.R[(int)b] += new Vector3(pitch, yaw, roll);

    /// <summary>Tronco: fwd + = inclina para a frente, right + = tomba para a direita, twist + = gira para a direita.</summary>
    static void Lean(Pose p, float fwd, float right = 0, float twist = 0)
    {
        Add(p, B.Spine, -fwd * 0.4f, -twist * 0.35f, -right * 0.4f);
        Add(p, B.Chest, -fwd * 0.35f, -twist * 0.35f, -right * 0.35f);
        Add(p, B.UpperChest, -fwd * 0.25f, -twist * 0.3f, -right * 0.25f);
    }

    /// <summary>Cabeça: down + = olha para baixo, right + = vira para a direita, tilt + = inclina para a direita.</summary>
    static void Look(Pose p, float down, float right = 0, float tilt = 0)
    {
        Add(p, B.Neck, -down * 0.4f, -right * 0.4f, -tilt * 0.4f);
        Add(p, B.Head, -down * 0.6f, -right * 0.6f, -tilt * 0.6f);
    }

    static void HipsRot(Pose p, float fwd, float twist = 0, float right = 0) => Add(p, B.Hips, -fwd, -twist, -right);

    /// <summary>Perna: thigh + = coxa para a frente, knee + = dobra o joelho, foot + = ponta do pé para cima
    /// (0 = pé paralelo ao chão, compensado sozinho), spread + = abre para fora.</summary>
    static void Leg(Pose p, bool left, float thigh, float knee, float foot = 0, float spread = 0)
    {
        var (u, l, f) = left ? (B.ULegL, B.LLegL, B.FootL) : (B.ULegR, B.LLegR, B.FootR);
        Add(p, u, thigh, 0, left ? -spread : spread);
        Add(p, l, -knee, 0, 0);
        Add(p, f, -(thigh - knee) + foot, 0, 0);
    }

    static void Shrug(Pose p, float up)
    {
        Add(p, B.ShL, 0, 0, -up);
        Add(p, B.ShR, 0, 0, up);
    }

    /// <summary>Queda do quadril (m) com coxa/joelho dobrados assim (geometria das pernas do modelo).</summary>
    float Drop(float thigh, float knee) =>
        _thighLen + _shinLen - (_thighLen * Mathf.Cos(Mathf.DegToRad(thigh)) + _shinLen * Mathf.Cos(Mathf.DegToRad(thigh - knee)));

    /// <summary>Agacha por cima do que já está na pose (soma), sem tirar a passada.</summary>
    void Crouch(Pose p, float a)
    {
        if (a <= 0.001f) return;
        float th = 36f * a, kn = 68f * a;
        Leg(p, true, th, kn); Leg(p, false, th, kn);
        p.Hips.Y -= Drop(th, kn);
        Lean(p, 12f * a);
        Look(p, -8f * a);
    }

    /// <summary>Curva por chaves (t, valor) com transição suave entre elas.</summary>
    static float Key(float x, float[] k)
    {
        if (x <= k[0]) return k[1];
        for (int i = 2; i < k.Length; i += 2)
            if (x <= k[i]) { float a = (x - k[i - 2]) / Mathf.Max(k[i] - k[i - 2], 1e-4f); return Mathf.Lerp(k[i - 1], k[i + 1], a * a * (3 - 2 * a)); }
        return k[^1];
    }

    static float SmoothStep(float a, float b, float x) => Mathf.SmoothStep(a, b, x);

    // =========================================================================================== poses
    void Idle(Pose p, float t)
    {
        p.Clear();
        float br = Mathf.Sin(t * 2.1f);
        Lean(p, 3 + br * 1.2f);
        Look(p, -2 - br, 6f * Mathf.Sin(t * 0.45f));
        p.Hips = new Vector3(0, -0.004f + br * 0.003f, 0);
        Leg(p, true, 2, 5, 0, 4); Leg(p, false, 2, 5, 0, 4);
        Shrug(p, br * 2);
        p.HandL = new Vector3(-0.34f, 0.40f + 0.022f * Mathf.Sin(t * 2.1f + 0.6f), -0.05f);
        p.HandR = new Vector3(0.34f, 0.40f + 0.022f * Mathf.Sin(t * 2.1f + 1.4f), -0.05f);
    }

    static void Walk(Pose p, float ph)
    {
        p.Clear();
        for (int side = 0; side < 2; side++)
        {
            float f = ph + side * Mathf.Pi, s = Mathf.Sin(f), c = Mathf.Cos(f);
            float thigh = 26 * s, knee = 5 + 42 * Mathf.Pow(Mathf.Max(0, Mathf.Cos(f - 0.25f)), 2), foot = 8 * s + 6 * Mathf.Max(0, c);
            Leg(p, side == 0, thigh, knee, foot, 2);
        }
        float sl = Mathf.Sin(ph);
        p.Hips = new Vector3(0, 0.016f * Mathf.Cos(2 * ph) - 0.012f, 0);
        HipsRot(p, 0, 7 * sl);
        Lean(p, 5, 0, -6 * sl);
        Look(p, -4, -1 * sl);
        p.HandL = new Vector3(-0.32f + 0.04f * Mathf.Max(0, -sl), 0.40f + 0.06f * Mathf.Max(0, -sl), 0.17f * sl);
        p.HandR = new Vector3(0.32f - 0.04f * Mathf.Max(0, sl), 0.40f + 0.06f * Mathf.Max(0, sl), -0.17f * sl);
    }

    static void Run(Pose p, float ph)
    {
        p.Clear();
        for (int side = 0; side < 2; side++)
        {
            float f = ph + side * Mathf.Pi, s = Mathf.Sin(f);
            float thigh = 42 * s + 10, knee = 18 + 85 * Mathf.Pow(Mathf.Max(0, Mathf.Cos(f - 0.5f)), 1.6f), foot = 14 * s - 10 * Mathf.Max(0, -s);
            Leg(p, side == 0, thigh, knee, foot, 2);
        }
        float sl = Mathf.Sin(ph);
        p.Hips = new Vector3(0, 0.03f * Mathf.Cos(2 * ph) - 0.035f, 0);
        HipsRot(p, 0, 10 * sl);
        Lean(p, 16, 0, -12 * sl);
        Look(p, -12, -2 * sl);
        p.Squash = 0.025f * Mathf.Cos(2 * ph);
        p.HandL = new Vector3(-0.30f + 0.10f * Mathf.Max(0, -sl), 0.48f + 0.14f * Mathf.Max(0, -sl), 0.28f * sl);
        p.HandR = new Vector3(0.30f - 0.10f * Mathf.Max(0, sl), 0.48f + 0.14f * Mathf.Max(0, sl), -0.28f * sl);
    }

    // curvas da enterrada (progresso do voo 0..1 → valor)
    static readonly float[] DkThighR = { 0, 45, 0.2f, 85, 0.7f, 80, 0.9f, 70, 1, 62 };
    static readonly float[] DkKneeR = { 0, 80, 0.2f, 105, 0.7f, 95, 1, 88 };
    static readonly float[] DkThighL = { 0, 45, 0.15f, -12, 0.6f, -22, 0.85f, 38, 1, 55 };
    static readonly float[] DkKneeL = { 0, 80, 0.15f, 10, 0.6f, 38, 0.85f, 80, 1, 88 };
    static readonly float[] DkFootL = { 0, 0, 0.15f, -35, 0.6f, -30, 1, -10 };
    static readonly float[] DkLean = { 0, 25, 0.15f, 0, 0.6f, -18, 0.8f, -12, 0.93f, 32, 1, 36 };
    static readonly float[] DkLook = { 0, -10, 0.6f, -26, 0.93f, 6, 1, 10 };
    static readonly float[] DkSquash = { 0, -0.09f, 0.12f, 0.13f, 0.5f, 0.04f, 0.9f, -0.03f, 1, 0.05f };
    static readonly float[] DkHips = { 0, -0.08f, 0.15f, 0.02f, 1, 0 };
    static readonly float[] DkShrug = { 0, 0, 0.6f, 12, 1, -6 };

    // =========================================================================================== quadro
    public void Animate(in RigState s, float dt)
    {
        var t = Tuning.I;
        dt = Mathf.Clamp(dt, 0, 0.1f);
        _time += dt;
        float sc = Mathf.Max(t.CharacterScale, 0.1f);
        Root.Scale = Vector3.One * sc;
        Root.Rotation = new Vector3(0, s.Yaw, 0);
        float k = 1f - Mathf.Exp(-t.AnimBlendSpeed * dt);
        float kFast = 1f - Mathf.Exp(-t.AnimBlendSpeed * 2.2f * dt);

        // ---------- locomoção ----------
        float speed = s.Speed;
        float runK = Mathf.Clamp((speed - t.WalkSpeed * 0.95f) / Mathf.Max(t.RunSpeed - t.WalkSpeed * 0.95f, 0.1f), 0, 1);
        float stride = Mathf.Lerp(t.AnimWalkStride, t.AnimRunStride, runK) * sc / 1.3f;
        _phase = (_phase + speed * dt / Mathf.Max(stride, 0.1f) * Mathf.Tau) % (Mathf.Tau * 100f);
        _moveW = Mathf.Lerp(_moveW, Mathf.Clamp(speed / 0.9f, 0, 1), k);
        _runW = Mathf.Lerp(_runW, runK, k);
        Idle(_pose, _time);
        Walk(_tmp, _phase);
        Run(_tmp2, _phase);
        _tmp.Mix(_tmp2, _runW, Mask.Full);
        _pose.Mix(_tmp, _moveW, Mask.Full);
        float still = 1f - _moveW;

        // ---------- pesos das camadas ----------
        bool ground = !s.Airborne && !s.Dunking && !s.Hanging;
        bool holdBounce = s.HasBall && !s.Charging && !s.PassCharging && !s.Dunking;
        void W(S st, bool on, bool fast = false) => _w[(int)st] = Mathf.Lerp(_w[(int)st], on ? 1f : 0f, fast ? kFast : k);
        W(S.Ball, holdBounce && !s.Dribbling);
        W(S.Defense, s.Defending && ground && !s.Dashing);
        W(S.Celebrate, s.Mood == 1 && ground && !s.HasBall);
        W(S.Sad, s.Mood == 2 && ground);
        W(S.StealFail, s.Slowed && ground);
        W(S.Dribble, s.Dribbling && holdBounce, true);
        W(S.PassCharge, s.HasBall && s.PassCharging, true);
        W(S.ShootCharge, s.HasBall && s.Charging, true);
        W(S.JumpCharge, s.JumpCharging && !s.Airborne, true);
        W(S.Air, s.Airborne && !s.Dunking && !s.Hanging, true);
        W(S.Land, s.Landing && !s.Airborne);
        W(S.Steal, s.Dashing, true);
        W(S.Stun, s.Stunned && ground);
        W(S.Dunk, s.Dunking, true);
        W(S.Hang, s.Hanging, true);
        float side = s.BallSide >= 0 ? 1f : -1f;

        // ---------- camadas ----------
        float w;
        if ((w = _w[(int)S.Ball]) > 0.001f)
        {
            _tmp.Clear();
            Lean(_tmp, 8 + _moveW * 6, 4 * side);
            Look(_tmp, -6);
            Shrug(_tmp, 4);
            var guard = new Vector3(-0.30f * side, 0.52f + 0.02f * Mathf.Sin(_time * 3), -0.16f);
            if (side > 0) _tmp.HandL = guard; else _tmp.HandR = guard;
            _pose.Mix(_tmp, w, Mask.UpperHands);
        }
        if ((w = _w[(int)S.Defense]) > 0.001f)
        {
            _tmp.Clear();
            float wig = Mathf.Sin(_time * 6f);
            Lean(_tmp, 18);
            Look(_tmp, -16);
            Shrug(_tmp, 6);
            Leg(_tmp, true, 30, 58, 0, 14); Leg(_tmp, false, 30, 58, 0, 14);
            _tmp.Hips = new Vector3(0.012f * wig, -Drop(30, 58) + 0.01f, 0);
            _tmp.HandL = new Vector3(-0.47f, 0.62f + 0.04f * wig, -0.2f);
            _tmp.HandR = new Vector3(0.47f, 0.62f - 0.04f * wig, -0.2f);
            _pose.Mix(_tmp, w, Mask.UpperHands);
            _pose.Mix(_tmp, w * still, Mask.Lower);
            Crouch(_pose, 0.35f * w * _moveW);
        }
        if ((w = _w[(int)S.Celebrate]) > 0.001f)
        {
            _tmp.Clear();
            float a = _time * 9f, hop = Mathf.Abs(Mathf.Sin(a));
            Lean(_tmp, -6);
            Look(_tmp, -14);
            Leg(_tmp, true, 6, 10 + 22 * (1 - hop), -8 * hop); Leg(_tmp, false, 6, 10 + 22 * (1 - hop), -8 * hop);
            _tmp.Hips = new Vector3(0, 0.06f * hop - Drop(6, 10 + 22 * (1 - hop)), 0);
            _tmp.Squash = 0.05f * hop;
            _tmp.HandL = new Vector3(-0.24f, 1.0f + 0.3f * Mathf.Max(0, Mathf.Sin(a)), -0.12f);
            _tmp.HandR = new Vector3(0.24f, 1.0f + 0.3f * Mathf.Max(0, Mathf.Sin(a + Mathf.Pi)), -0.12f);
            _pose.Mix(_tmp, w, Mask.UpperHands);
            _pose.Mix(_tmp, w * still, Mask.Lower);
        }
        if ((w = _w[(int)S.Sad]) > 0.001f)
        {
            _tmp.Clear();
            Lean(_tmp, 26 + 2 * Mathf.Sin(_time * 1.5f));
            Look(_tmp, 24);
            Shrug(_tmp, -12);
            Leg(_tmp, true, 4, 10); Leg(_tmp, false, 4, 10);
            _tmp.Hips = new Vector3(0, -Drop(4, 10), 0);
            _tmp.HandL = new Vector3(-0.22f, 0.28f, -0.12f);
            _tmp.HandR = new Vector3(0.22f, 0.28f, -0.12f);
            _pose.Mix(_tmp, w, Mask.UpperHands);
            _pose.Mix(_tmp, w * still, Mask.Lower);
        }
        if ((w = _w[(int)S.StealFail]) > 0.001f)
        {
            _tmp.Clear();
            float pant = Mathf.Sin(_time * 7f);
            Lean(_tmp, 24 + 3 * pant);
            Look(_tmp, 12);
            Shrug(_tmp, -10 + 3 * pant);
            _tmp.HandL = new Vector3(-0.30f, 0.27f + 0.02f * pant, -0.06f);
            _tmp.HandR = new Vector3(0.30f, 0.27f + 0.02f * pant, -0.06f);
            _pose.Mix(_tmp, w, Mask.UpperHands);
            Crouch(_pose, 0.15f * w);
        }
        if ((w = _w[(int)S.Dribble]) > 0.001f)
        {
            // drible especial: baixo, tronco protegendo a bola, a outra mão na frente como escudo
            float bs = Mathf.Clamp(s.BallSide, -1, 1);
            _tmp.Clear();
            Lean(_tmp, 26, 7 * bs, 12 * bs);
            Look(_tmp, -26, -12 * bs);
            Shrug(_tmp, 5);
            var shield = new Vector3(-0.30f * side, 0.60f, -0.34f);
            if (side > 0) _tmp.HandL = shield; else _tmp.HandR = shield;
            _pose.Mix(_tmp, w, Mask.UpperHands);
            _tmp.Clear();
            Leg(_tmp, true, 4, 8, 0, 10); Leg(_tmp, false, 4, 8, 0, 10);
            _pose.Mix(_tmp, w * still, Mask.Lower);
            _pose.Hips.X += 0.05f * bs * w;
            HipsRot(_pose, 0, 0, 5 * bs * w);
            Crouch(_pose, Mathf.Clamp(t.DribbleCrouch, 0, 1.3f) * 0.85f * w);
        }
        float crouchCharge = Mathf.Clamp(s.Charge / 0.25f, 0, 1);
        if ((w = _w[(int)S.PassCharge]) > 0.001f)
        {
            _tmp.Clear();
            Lean(_tmp, -6, 0, 12);
            Look(_tmp, -2, -10);
            Leg(_tmp, true, 18, 22); Leg(_tmp, false, -12, 10);
            _tmp.Hips = new Vector3(0, -0.02f, 0);
            _pose.Mix(_tmp, w, Mask.Upper);
            _pose.Mix(_tmp, w * still * (s.Airborne ? 0 : 1), Mask.Lower);
        }
        if ((w = _w[(int)S.ShootCharge]) > 0.001f)
        {
            float breathe = Mathf.Sin(_time * 4f) * 0.5f;
            _tmp.Clear();
            Lean(_tmp, 2 - 7 * crouchCharge + breathe);
            Look(_tmp, -16);
            Shrug(_tmp, 9);
            float th = 22 * crouchCharge, kn = 42 * crouchCharge;
            Leg(_tmp, true, th + 4, kn, 0, 6); Leg(_tmp, false, th - 4, kn, 0, 6);
            _tmp.Hips = new Vector3(0, -Drop(th, kn), 0);
            _pose.Mix(_tmp, w, Mask.Upper);
            if (!s.Airborne) { _pose.Mix(_tmp, w * still, Mask.Lower); Crouch(_pose, 0.35f * crouchCharge * w * _moveW); }
        }
        if ((w = _w[(int)S.JumpCharge]) > 0.001f)
        {
            _tmp.Clear();
            Lean(_tmp, 30);
            Look(_tmp, -24);
            Leg(_tmp, true, 52, 95, 0, 6); Leg(_tmp, false, 52, 95, 0, 6);
            _tmp.Hips = new Vector3(0, -Drop(52, 95), 0.03f);
            _tmp.Squash = -0.06f;
            _tmp.HandL = new Vector3(-0.28f, 0.30f, 0.22f);
            _tmp.HandR = new Vector3(0.28f, 0.30f, 0.22f);
            _pose.Mix(_tmp, w, s.Charging ? Mask.Lower : Mask.Full);
        }
        if ((w = _w[(int)S.Air]) > 0.001f)
        {
            float fall = SmoothStep(1.5f, -2.5f, s.VelY);
            _tmp.Clear();
            Lean(_tmp, Mathf.Lerp(-4, 4, fall));
            Look(_tmp, Mathf.Lerp(-12, -4, fall));
            Leg(_tmp, true, Mathf.Lerp(55, 25, fall), Mathf.Lerp(88, 32, fall), -10);
            Leg(_tmp, false, Mathf.Lerp(14, 8, fall), Mathf.Lerp(30, 16, fall), -22);
            _tmp.Squash = Mathf.Lerp(0.07f, 0.02f, fall);
            _tmp.HandL = new Vector3(-0.2f, 1.32f, -0.06f).Lerp(new Vector3(-0.36f, 0.95f, -0.08f), fall);
            _tmp.HandR = new Vector3(0.2f, 1.32f, -0.06f).Lerp(new Vector3(0.36f, 0.95f, -0.08f), fall);
            _pose.Mix(_tmp, w, s.Charging || s.PassCharging ? Mask.Lower : Mask.Full);
        }
        if ((w = _w[(int)S.Land]) > 0.001f)
        {
            Crouch(_pose, 0.85f * w);
            _pose.Squash -= 0.07f * w;
            _tmp.Clear();
            _tmp.HandL = new Vector3(-0.38f, 0.34f, -0.14f);
            _tmp.HandR = new Vector3(0.38f, 0.34f, -0.14f);
            if (!s.HasBall) _pose.Mix(_tmp, 0.6f * w, Mask.Hands);
        }
        if (s.PassRelease > 0)
        {
            float pr = 1f - Mathf.Clamp(s.PassRelease, 0, 1);
            w = SmoothStep(0, 0.12f, pr) * (1f - SmoothStep(0.5f, 1f, pr));
            _tmp.Clear();
            Lean(_tmp, 16, 0, -8);
            Look(_tmp, -8, 6);
            Leg(_tmp, true, 25, 25); Leg(_tmp, false, -18, 8);
            _tmp.Hips = new Vector3(0, -0.02f, -0.03f);
            float hy = s.PassHigh ? 1.3f : 0.8f, hz = s.PassHigh ? -0.32f : -0.52f;
            _tmp.HandL = new Vector3(-0.12f, hy, hz);
            _tmp.HandR = new Vector3(0.12f, hy, hz);
            _pose.Mix(_tmp, w, Mask.UpperHands);
            _pose.Mix(_tmp, w * still * (s.Airborne ? 0 : 1), Mask.Lower);
        }
        if (s.Release > 0)
        {
            // soltou o arremesso: estica o corpo todo, pontas dos pés, mão de arremesso segue para a frente/cima
            float pr = 1f - Mathf.Clamp(s.Release, 0, 1);
            w = SmoothStep(0, 0.12f, pr) * (1f - SmoothStep(0.55f, 1f, pr));
            float pop = Mathf.Sin(Mathf.Clamp(pr / 0.5f, 0, 1) * Mathf.Pi);
            _tmp.Clear();
            Lean(_tmp, -6);
            Look(_tmp, -18);
            Shrug(_tmp, 10);
            Leg(_tmp, true, 2, 0, -24); Leg(_tmp, false, -2, 0, -24);
            _tmp.Hips = new Vector3(0, 0.05f * pop, 0);
            _tmp.Squash = 0.08f * pop;
            _tmp.HandR = new Vector3(0.08f, 1.30f, -0.32f);
            _tmp.HandL = new Vector3(-0.22f, 1.12f, -0.12f);
            _pose.Mix(_tmp, w, s.Airborne ? Mask.UpperHands : Mask.Full);
        }
        if ((w = _w[(int)S.Steal]) > 0.001f)
        {
            _tmp.Clear();
            Lean(_tmp, 38, 0, 10);
            Look(_tmp, -30, -8);
            Leg(_tmp, true, 55, 48); Leg(_tmp, false, -35, 12, -15);
            _tmp.Hips = new Vector3(0, -0.075f, 0);
            _tmp.Squash = -0.03f;
            _tmp.HandR = new Vector3(0.06f, 0.42f, -0.64f);
            _tmp.HandL = new Vector3(-0.34f, 0.52f, 0.12f);
            _pose.Mix(_tmp, w, Mask.Full);
        }
        if ((w = _w[(int)S.Stun]) > 0.001f)
        {
            float a = _time;
            _tmp.Clear();
            Lean(_tmp, 14 + 6 * Mathf.Sin(a * 2.2f), 9 * Mathf.Sin(a * 3.1f));
            Look(_tmp, 10, 14 * Mathf.Sin(a * 1.7f), 18 * Mathf.Sin(a * 3.1f + 1));
            Leg(_tmp, true, 12, 26, 0, 6 + 3 * Mathf.Sin(a * 3.1f)); Leg(_tmp, false, 12, 26, 0, 6 - 3 * Mathf.Sin(a * 3.1f));
            _tmp.Hips = new Vector3(0.025f * Mathf.Sin(a * 3.1f), -Drop(12, 26), 0);
            _tmp.HandL = new Vector3(-0.36f + 0.06f * Mathf.Cos(a * 4), 0.48f + 0.07f * Mathf.Sin(a * 4), -0.05f + 0.05f * Mathf.Sin(a * 4.3f));
            _tmp.HandR = new Vector3(0.36f + 0.06f * Mathf.Cos(a * 4 + 3), 0.48f + 0.07f * Mathf.Sin(a * 4 + 3), -0.05f + 0.05f * Mathf.Sin(a * 4.3f + 3));
            _pose.Mix(_tmp, w, Mask.Full);
        }
        if ((w = _w[(int)S.Dunk]) > 0.001f)
        {
            float f = s.FlightK;
            _tmp.Clear();
            Lean(_tmp, Key(f, DkLean));
            Look(_tmp, Key(f, DkLook));
            Shrug(_tmp, Key(f, DkShrug));
            Leg(_tmp, false, Key(f, DkThighR), Key(f, DkKneeR), -10);
            Leg(_tmp, true, Key(f, DkThighL), Key(f, DkKneeL), Key(f, DkFootL));
            _tmp.Hips = new Vector3(0, Key(f, DkHips), 0);
            _tmp.Squash = Key(f, DkSquash);
            _pose.Mix(_tmp, w, Mask.Full);
        }
        _hangT = s.Hanging ? _hangT + dt : 0;
        if ((w = _w[(int)S.Hang]) > 0.001f)
        {
            float sw = Mathf.Sin(_hangT * 10f) * Mathf.Exp(-_hangT * 3f);
            _tmp.Clear();
            Lean(_tmp, -6 + 6 * sw);
            Look(_tmp, -14);
            Shrug(_tmp, 16);
            Leg(_tmp, true, 18 + 24 * sw, 30 + 14 * sw, -15); Leg(_tmp, false, 10 + 20 * sw, 24 + 10 * sw, -20);
            _tmp.Squash = 0.06f;
            _pose.Mix(_tmp, w, Mask.Full);
        }

        // pegou a bola: amortece um pouquinho
        if (s.HasBall && !_hadBall) _catchT = 0.25f;
        _hadBall = s.HasBall;
        if (_catchT > 0)
        {
            _catchT -= dt;
            float env = Mathf.Sin(Mathf.Clamp(_catchT / 0.25f, 0, 1) * Mathf.Pi);
            if (ground) Crouch(_pose, 0.3f * env);
        }

        // olha para a bola (de leve) quando não está com ela
        if (P != null && !s.HasBall && !s.Dunking && !s.Hanging && !s.Stunned && P.M?.Ball != null && GodotObject.IsInstanceValid(P.M.Ball))
        {
            var lb = Root.ToLocal(P.M.Ball.GlobalPosition);
            float ang = Mathf.RadToDeg(Mathf.Atan2(lb.X, -lb.Z));
            Look(_pose, 0, Mathf.Clamp(ang, -50, 50) * 0.55f);
        }

        ApplyHands(s, dt, sc);
        ApplySkeleton();
        float sq = Mathf.Clamp(_pose.Squash, -0.25f, 0.25f);
        _model.Scale = new Vector3(1f - 0.5f * sq, 1f + sq, 1f - 0.5f * sq);
        UpdateFace(s);
        LandingDust(s);
    }

    // =========================================================================================== mãos
    void ApplyHands(in RigState s, float dt, float sc)
    {
        var t = Tuning.I;
        var tl = _pose.HandL;
        var tr = _pose.HandR;
        float rateL = t.HandFollow, rateR = t.HandFollow;
        float r = t.BallRadius / sc, reach = r + HandR * 0.8f;

        if (s.HasBall)
        {
            var b = Root.ToLocal(s.BallPos);
            if (s.Charging)
            {
                // arremesso: mão direita embaixo/atrás da bola, a esquerda guia do lado
                tr = b + new Vector3(0.3f, -0.72f, 0.6f).Normalized() * reach;
                tl = b + new Vector3(-1f, -0.05f, 0.15f).Normalized() * reach;
                rateL = rateR = 60;
            }
            else if (s.PassCharging)
            {
                tl = b + new Vector3(-1f, 0, 0.18f).Normalized() * reach;
                tr = b + new Vector3(1f, 0, 0.18f).Normalized() * reach;
                rateL = rateR = 60;
            }
            else if (s.Dunking)
            {
                tl = b + new Vector3(-0.85f, 0.35f, 0.3f).Normalized() * reach;
                tr = b + new Vector3(0.85f, 0.35f, 0.3f).Normalized() * reach;
                rateL = rateR = 70;
            }
            else
            {
                // quique (v0.22): a mão fica no alto e só encosta nos últimos centímetros — amortece a bola que chega
                // e dá um empurrãozinho; o resto do caminho a bola faz sozinha (sobe e desce no ar)
                bool right = s.BallSide >= 0;
                if (Mathf.Abs(s.BallSide) > 0.3f) _ballHandRight = right;
                if (b.Y > _prevBallY + 1e-4f) { _rising = true; _curMax = Mathf.Max(_curMax, b.Y); }
                else if (b.Y < _prevBallY - 1e-4f && _rising) { _rising = false; _apex = Mathf.Max(_curMax, b.Y); _curMax = b.Y; }
                if (_apex <= 0) _apex = b.Y;
                float top = b.Y + reach;
                float rest = _apex + reach - Tuning.I.DribbleHandContact / sc;
                var target = new Vector3(b.X, Mathf.Max(top, rest), b.Z);
                if (_ballHandRight) { tr = target; rateR = 70; } else { tl = target; rateL = 70; }
            }
            _prevBallY = b.Y;
        }
        else
        {
            _apex = 0; _curMax = 0;
            if (s.Hanging)
            {
                // mãos agarradas na frente do aro
                var rim = s.RimPos;
                var toRim = rim - Root.GlobalPosition; toRim.Y = 0;
                var d = toRim.LengthSquared() > 1e-4f ? toRim.Normalized() : -Root.GlobalBasis.Z;
                var right = d.Cross(Vector3.Up).Normalized();
                var front = rim - d * Tuning.I.RimRadius + Vector3.Up * 0.03f;
                tl = Root.ToLocal(front - right * 0.2f);
                tr = Root.ToLocal(front + right * 0.2f);
                rateL = rateR = 50;
            }
        }

        if (!_handInit) { _hl = tl; _hr = tr; _handInit = true; }
        _hl = _hl.Lerp(tl, 1f - Mathf.Exp(-rateL * dt));
        _hr = _hr.Lerp(tr, 1f - Mathf.Exp(-rateR * dt));
        if (_handL != null) _handL.Position = _hl;
        if (_handR != null) _handR.Position = _hr;
    }

    void ApplySkeleton()
    {
        if (_sk == null) return;
        for (int i = 0; i < NB; i++)
        {
            int idx = _bi[i];
            if (idx < 0) continue;
            var e = _pose.R[i] * (Mathf.Pi / 180f);
            var q = new Quaternion(Vector3.Up, e.Y) * new Quaternion(Vector3.Right, e.X) * new Quaternion(Vector3.Back, e.Z);
            var delta = _frame[i].Inverse() * q * _frame[i];
            _sk.SetBonePoseRotation(idx, (_rest[i] * delta).Normalized());
        }
        int h = _bi[(int)B.Hips];
        if (h >= 0) _sk.SetBonePosePosition(h, _hipsRest + _hipsConv * _pose.Hips);
    }

    // =========================================================================================== rosto e efeitos
    void UpdateFace(in RigState s)
    {
        int id = s.Emote;
        if (id <= 0 || id > Expressions.Count)
        {
            id = Expressions.Serio;
            if (s.Charging || s.Dribbling) id = Expressions.Focado;
            if (s.Slowed) id = Expressions.Bravo;
            if (s.Dunking || s.Hanging) id = Expressions.Bravo;
            if (s.Mood == 1) id = Expressions.Feliz;
            if (s.Angry) id = Expressions.Bravo;
            if (s.Stunned) id = Expressions.Surpreso;
        }
        if (id == _faceShown) return;
        _faceShown = id;
        for (int i = 1; i <= Expressions.Count; i++)
            if (_faces[i] != null) _faces[i].Visible = i == id;
    }

    void LandingDust(in RigState s)
    {
        if (s.Hanging) _bigFall = true;
        bool air = s.Airborne || s.Dunking || s.Hanging;
        if (_wasAir && !air && P?.M != null)
        {
            ImpactFx.Dust(P.M, P.GlobalPosition, _bigFall ? 1f : 0.45f);
            _bigFall = false;
        }
        _wasAir = air;
    }

    /// <summary>Só para o --pose-shot: força uma expressão.</summary>
    public void ForceFace(int id)
    {
        _faceShown = -1;
        UpdateFace(new RigState { Emote = (byte)id });
    }
}
