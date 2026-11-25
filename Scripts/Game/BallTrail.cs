using Godot;

namespace AB;

/// <summary>
/// v0.26: efeitos na bola. Rastro sutil de energia vermelha (passe/arremesso com o Olho do Rei, <see cref="Ball.Aura"/> = 1)
/// e aura vermelha em volta da bola durante o Drible Perfeito do King.
/// </summary>
public partial class BallTrail : Node3D
{
    public Ball B;
    CpuParticles3D _trail;
    MeshInstance3D _aura;
    StandardMaterial3D _auraMat;
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

        _auraMat = Mat.Unshaded(new Color(1f, 0.15f, 0.1f, 0.35f), true);
        _auraMat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        float r = Tuning.I.BallRadius;
        _aura = new MeshInstance3D { Mesh = new SphereMesh { Radius = r * 1.7f, Height = r * 3.4f, RadialSegments = 16, Rings = 8 }, MaterialOverride = _auraMat, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_aura);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (B == null) return;
        _trail.Emitting = B.Aura == 1 && B.State != BallState.Held;
        var h = B.Holder;
        bool aura = h != null && h.Ab != null && h.Ab.PerfectDribble && h.IsDribbling;
        _aura.Visible = aura;
        if (aura)
        {
            float s = 1f + 0.15f * Mathf.Sin(_t * 18f);
            _aura.Scale = Vector3.One * s;
            _auraMat.AlbedoColor = new Color(1f, 0.15f, 0.1f, 0.28f + 0.12f * Mathf.Sin(_t * 11f));
        }
    }
}
