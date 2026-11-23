using Godot;

namespace AB;

/// <summary>Indicador de trajetória do arremesso (só para quem está carregando, localmente).</summary>
public partial class TrajectoryPreview : MultiMeshInstance3D
{
    const int MaxDots = 70;
    MultiMesh _mm;

    public override void _Ready()
    {
        TopLevel = true;
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        CastShadow = ShadowCastingSetting.Off;
        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = MaxDots,
            Mesh = new SphereMesh { Radius = 0.065f, Height = 0.13f, RadialSegments = 10, Rings = 5 },
        };
        Multimesh = _mm;
        var m = Mat.Unshaded(new Color("ffd23f"));
        m.NoDepthTest = true;
        m.RenderPriority = 10;
        MaterialOverride = m;
        Visible = false;
    }

    public void UpdateFor(Player p, CourtGeo g)
    {
        if (p == null || !p.GetShotPlan(out var plan)) { Visible = false; return; }
        var pos = plan.Pos; var vel = plan.Vel;
        Visible = true;
        int count = 0;
        const float dt = 1f / 60f;
        float time = 0;
        bool stop = false;
        while (!stop && time < Tuning.I.TrajectoryPreviewTime && count < MaxDots)
        {
            for (int k = 0; k < 2; k++)
            {
                var hit = BallPhys.Simulate(ref pos, ref vel, dt, g, CourtGeo.AttackHoop(p.Team));
                time += dt;
                if (hit.Any || hit.ScoredHoop >= 0) { stop = true; break; }
            }
            float s = stop ? 2.4f : 1f;
            _mm.SetInstanceTransform(count++, new Transform3D(Basis.Identity.Scaled(Vector3.One * s), pos));
        }
        _mm.VisibleInstanceCount = count;
    }
}
