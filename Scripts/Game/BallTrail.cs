using Godot;

namespace AB;

/// <summary>
/// v0.26: efeitos na bola. Rastro sutil de energia vermelha (passe/arremesso com o Olho do Rei, <see cref="Ball.Aura"/> = 1)
/// e fogo vermelho saindo da bola durante o Drible Perfeito do King (v0.32: antes era uma esfera vermelha em volta).
/// </summary>
public partial class BallTrail : Node3D
{
    public Ball B;
    CpuParticles3D _trail;
    CpuParticles3D _fire;
    float _t;

    public override void _Ready()
    {
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        _trail = new CpuParticles3D
        {
            Emitting = false, Amount = 40, Lifetime = 0.45f, LocalCoords = false, Direction = Vector3.Up, Spread = 180f,
            InitialVelocityMin = 0.05f, InitialVelocityMax = 0.3f, Gravity = Vector3.Zero, ScaleAmountMin = 0.7f, ScaleAmountMax = 1.3f,
            Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f, RadialSegments = 6, Rings = 3 },
        };
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 0.2f, 0.15f, 0.75f));
        ramp.SetColor(1, new Color(0.6f, 0f, 0f, 0f));
        _trail.ColorRamp = ramp;
        var m = Mat.Unshaded(Colors.White, true);
        m.VertexColorUseAsAlbedo = true;
        m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        _trail.MaterialOverride = m;
        AddChild(_trail);

        // v0.33: partículas vermelhas estilizadas (quadradinhos sólidos girando, sem degradê suave) saindo da bola e subindo
        // como faíscas de fogo; vermelho vivo → vermelho escuro → somem encolhendo. Mistura normal (não soma): fica vermelho.
        float r = Tuning.I.BallRadius;
        var fm = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false,
        };
        var fr = new Gradient();
        fr.Offsets = new[] { 0f, 0.15f, 0.7f, 1f };
        fr.Colors = new[] { new Color(1f, 0.3f, 0.22f, 1f), new Color(1f, 0.08f, 0.06f, 1f), new Color(0.75f, 0.02f, 0.04f, 0.95f), new Color(0.45f, 0f, 0.02f, 0f) };
        var shrink = new Curve();
        shrink.AddPoint(new Vector2(0, 1f)); shrink.AddPoint(new Vector2(0.6f, 0.8f)); shrink.AddPoint(new Vector2(1, 0f));
        _fire = new CpuParticles3D
        {
            Emitting = false, Amount = 44, Lifetime = 0.55f, LocalCoords = false, Explosiveness = 0f, Randomness = 0.5f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = r * 0.9f,
            Direction = Vector3.Up, Spread = 55f, InitialVelocityMin = 0.5f, InitialVelocityMax = 1.6f, Gravity = new Vector3(0, 1.2f, 0),
            DampingMin = 0.8f, DampingMax = 2f, ScaleAmountMin = 0.6f, ScaleAmountMax = 1.3f, ScaleAmountCurve = shrink,
            AngleMin = 0, AngleMax = 360, AngularVelocityMin = -360f, AngularVelocityMax = 360f, ColorRamp = fr,
            Mesh = new QuadMesh { Size = Vector2.One * r * 0.42f, Material = fm },
        };
        AddChild(_fire);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (B == null) return;
        _trail.Emitting = B.Aura == 1 && B.State != BallState.Held;
        var h = B.Holder;
        bool aura = h != null && h.Ab != null && h.Ab.PerfectDribble && h.IsDribbling;
        _fire.Emitting = aura;
    }
}
