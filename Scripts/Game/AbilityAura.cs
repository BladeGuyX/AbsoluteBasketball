using Godot;

namespace AB;

/// <summary>
/// v0.26: efeitos contínuos das habilidades em volta de um jogador (lê o estado replicado, então funciona no cliente):
/// partículas vermelhas (Presença Implacável do King), brilho vermelho na cabeça (Olho do Rei esperando), partículas verdes
/// (modo apostador do Luck), trevo verde em cima de quem foi marcado pelo "Não me Decepciona", faíscas azuis no ar
/// (Arranha-Céu), anel do chão claro em quem vai ser trocado (Swap, só na tela do Bolt — v0.27) e setinha amarela em quem está selecionado
/// no menu de escolher adversário.
/// </summary>
public partial class AbilityAura : Node3D
{
    public Player P;
    CpuParticles3D _red, _green, _sky, _eye;
    Node3D _clover, _pick;
    float _t, _swapK;

    public override void _Ready()
    {
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        _red = Particles(new Color(1f, 0.15f, 0.15f, 0.9f), new Color(0.6f, 0f, 0f, 0f), 28, 0.9f, new Vector3(0, 1.0f, 0), 0.45f, 1.4f);
        _green = Particles(new Color(0.35f, 1f, 0.45f, 0.9f), new Color(0.1f, 0.7f, 0.25f, 0f), 30, 0.9f, new Vector3(0, 1.0f, 0), 0.45f, 1.6f);
        _sky = Particles(new Color(0.7f, 0.95f, 1f, 1f), new Color(0.2f, 0.7f, 1f, 0f), 24, 0.35f, new Vector3(0, 0.9f, 0), 0.5f, 3f);
        _eye = Particles(new Color(1f, 0.25f, 0.2f, 1f), new Color(1f, 0f, 0f, 0f), 10, 0.5f, new Vector3(0, 1.85f, 0), 0.15f, 0.4f);

        _clover = new Node3D { Position = new Vector3(0, 2.75f, 0), Visible = false };
        var g = Mat.Unshaded(new Color("3ccf6e"));
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Tau / 4f;
            _clover.AddChild(Mat.MeshNode(new SphereMesh { Radius = 0.11f, Height = 0.22f, RadialSegments = 10, Rings = 5 }, g,
                new Vector3(Mathf.Cos(a) * 0.12f, Mathf.Sin(a) * 0.12f, 0), false));
        }
        _clover.AddChild(Mat.MeshNode(new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 0.22f }, g, new Vector3(0.02f, -0.2f, 0), false));
        AddChild(_clover);


        _pick = Mat.MeshNode(new CylinderMesh { TopRadius = 0.0f, BottomRadius = 0.18f, Height = 0.34f, RadialSegments = 4 }, Mat.Unshaded(new Color("ffd23f")),
            new Vector3(0, 2.75f, 0), false);
        _pick.RotationDegrees = new Vector3(180, 0, 0);
        _pick.Visible = false;
        AddChild(_pick);
    }

    CpuParticles3D Particles(Color c0, Color c1, int amount, float life, Vector3 pos, float radius, float upSpeed)
    {
        var p = new CpuParticles3D
        {
            Emitting = false, Amount = amount, Lifetime = life, Position = pos, LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = radius,
            Direction = Vector3.Up, Spread = 25f, InitialVelocityMin = upSpeed * 0.5f, InitialVelocityMax = upSpeed,
            Gravity = Vector3.Zero, ScaleAmountMin = 0.6f, ScaleAmountMax = 1.2f,
            Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3 },
        };
        var ramp = new Gradient();
        ramp.SetColor(0, c0);
        ramp.SetColor(1, c1);
        p.ColorRamp = ramp;
        var m = Mat.Unshaded(Colors.White, true);
        m.VertexColorUseAsAlbedo = true;
        m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        p.MaterialOverride = m;
        AddChild(p);
        return p;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (P?.Ab == null || P.M == null) return;
        var a = P.Ab;
        _red.Emitting = a.PresenceT > 0;
        _green.Emitting = a.ModeT > 0;
        _sky.Emitting = a.SkyJump && P.Airborne;
        _eye.Emitting = a.EyeT > 0 || a.EyeArmed;

        // marcado pelo "Não me Decepciona" de algum Luck adversário — v0.27: só o time do Luck vê (o alvo não sabe)
        var lp = P.M.LocalPlayer;
        bool marked = false;
        foreach (var q in P.M.Players)
            if (q.Team != P.Team && q.Ab != null && q.Ab.BetT > 0 && q.Ab.BetTarget == P.Id && (lp == null || lp.Team == q.Team)) { marked = true; break; }
        _clover.Visible = marked;
        if (marked) { _clover.Rotation = new Vector3(0, _t * 3f, 0); _clover.Position = new Vector3(0, 2.75f + 0.06f * Mathf.Sin(_t * 4f), 0); }

        // v0.27: alvo do Swap = anel do chão bem mais claro, sempre que você é o Bolt e o Swap está pronto (só nesta tela)
        bool swap = lp != null && lp != P && lp.CharacterId == Characters.Bolt && P.M.AbilitiesOn && !TargetPicker.IsOpen && lp.Ab.Cd[1] <= 0
                    && P.M.Cam != null && P.M.CanAct && PlayerAbilities.SwapCandidate(lp, P.M.Cam.Yaw) == P;
        _swapK = Mathf.MoveToward(_swapK, swap ? 1f : 0f, (float)delta * 10f);
        P.SetRingHighlight(_swapK);

        bool pick = TargetPicker.Current == P;
        _pick.Visible = pick;
        if (pick) _pick.Position = new Vector3(0, 2.8f + 0.1f * Mathf.Sin(_t * 6f), 0);
    }
}
