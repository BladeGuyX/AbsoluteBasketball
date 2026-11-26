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

        // fogo: labaredas (quadradinhos com degradê redondo, virados para a câmera) nascendo na superfície da bola, subindo e
        // encolhendo; amarelo → laranja → vermelho → some. Coordenadas do mundo: ficam para trás quando a bola quica.
        float r = Tuning.I.BallRadius;
        var g = new Gradient();
        g.Offsets = new[] { 0f, 0.55f, 1f };
        g.Colors = new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.45f), new Color(1, 1, 1, 0) };
        var tex = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f), Width = 32, Height = 32 };
        var fm = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var fr = new Gradient();
        fr.Offsets = new[] { 0f, 0.25f, 0.6f, 1f };
        fr.Colors = new[] { new Color(1f, 0.9f, 0.45f, 0.95f), new Color(1f, 0.5f, 0.1f, 0.85f), new Color(0.95f, 0.12f, 0.05f, 0.6f), new Color(0.5f, 0f, 0f, 0f) };
        var shrink = new Curve();
        shrink.AddPoint(new Vector2(0, 0.6f)); shrink.AddPoint(new Vector2(0.25f, 1f)); shrink.AddPoint(new Vector2(1, 0.1f));
        _fire = new CpuParticles3D
        {
            Emitting = false, Amount = 48, Lifetime = 0.42f, LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = r * 0.95f,
            Direction = Vector3.Up, Spread = 25f, InitialVelocityMin = 0.6f, InitialVelocityMax = 1.4f, Gravity = new Vector3(0, 1.8f, 0),
            DampingMin = 0.5f, DampingMax = 1.5f, ScaleAmountMin = 0.8f, ScaleAmountMax = 1.4f, ScaleAmountCurve = shrink,
            AngleMin = 0, AngleMax = 360, ColorRamp = fr,
            Mesh = new QuadMesh { Size = Vector2.One * r * 0.9f, Material = fm },
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
