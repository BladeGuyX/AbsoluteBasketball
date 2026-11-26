using System.Collections.Generic;
using Godot;

namespace AB;

/// <summary>
/// Indicador de trajetória do arremesso (só para quem está carregando, localmente).
/// v0.27: pontinhos com espaçamento igual que "andam" devagar no sentido do voo, degradê de amarelo para laranja, ficando
/// menores e mais transparentes no fim, e um anel no ponto onde a bola bate (aro, tabela, chão).
/// </summary>
public partial class TrajectoryPreview : MultiMeshInstance3D
{
    const int MaxDots = 90;
    const float Spacing = 0.34f;     // m entre os pontinhos
    const float FlowSpeed = 0.9f;    // m/s que eles andam
    static readonly Color Start = new("ffd23f"), End = new("ff7a2e");
    MultiMesh _mm;
    MeshInstance3D _ring;
    StandardMaterial3D _ringMat;
    readonly List<Vector3> _path = new();
    float _flow;

    public override void _Ready()
    {
        TopLevel = true;
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        CastShadow = ShadowCastingSetting.Off;
        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            InstanceCount = MaxDots,
            Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 10, Rings = 5 },
        };
        Multimesh = _mm;
        var m = Mat.Unshaded(Colors.White, true);
        m.VertexColorUseAsAlbedo = true;
        m.NoDepthTest = true;
        m.RenderPriority = 10;
        MaterialOverride = m;

        _ringMat = Mat.Unshaded(new Color(1f, 0.75f, 0.3f, 0.9f), true);
        _ringMat.NoDepthTest = true;
        _ringMat.RenderPriority = 10;
        _ringMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.16f, OuterRadius = 0.22f, Rings = 24, RingSegments = 6 },
            MaterialOverride = _ringMat, CastShadow = ShadowCastingSetting.Off, TopLevel = true,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
        };
        AddChild(_ring);
        Visible = false;
    }

    public void UpdateFor(Player p, CourtGeo g)
    {
        if (p == null || !p.GetShotPlan(out var plan)) { Visible = false; return; }
        var pos = plan.Pos; var vel = plan.Vel;
        Visible = true;
        _flow = Mathf.PosMod(_flow + (float)GetProcessDeltaTime() * FlowSpeed, Spacing);

        // simula o voo e guarda o caminho
        _path.Clear();
        _path.Add(pos);
        const float dt = 1f / 60f;
        float time = 0;
        bool hitSomething = false;
        Vector3 hitNormal = Vector3.Up;
        while (time < Tuning.I.TrajectoryPreviewTime && _path.Count < 400)
        {
            var before = pos;
            var hit = BallPhys.Simulate(ref pos, ref vel, dt, g, CourtGeo.AttackHoop(p.Team), PlayerAbilities.WallPassMask(p));
            time += dt;
            _path.Add(pos);
            if (hit.Any || hit.ScoredHoop >= 0)
            {
                hitSomething = true;
                if (hit.Floor) hitNormal = Vector3.Up;
                else { var d = (pos - before); hitNormal = d.LengthSquared() > 1e-6f ? -d.Normalized() : Vector3.Up; }
                break;
            }
        }

        // pontinhos com o mesmo espaçamento ao longo do caminho (andando devagar)
        float total = 0;
        for (int i = 1; i < _path.Count; i++) total += _path[i].DistanceTo(_path[i - 1]);
        int count = 0;
        float target = 0.25f + _flow, acc = 0;
        for (int i = 1; i < _path.Count && count < MaxDots; i++)
        {
            float segLen = _path[i].DistanceTo(_path[i - 1]);
            while (acc + segLen >= target && count < MaxDots)
            {
                float k = segLen > 1e-5f ? (target - acc) / segLen : 0;
                var at = _path[i - 1].Lerp(_path[i], k);
                float u = total > 0.01f ? target / total : 0;               // 0 = mão, 1 = fim
                float size = Mathf.Lerp(1.05f, 0.55f, u);
                float alpha = Mathf.Clamp(Mathf.Min(target / 0.6f, 1f) * Mathf.Lerp(1f, 0.5f, u), 0, 1);
                _mm.SetInstanceTransform(count, new Transform3D(Basis.Identity.Scaled(Vector3.One * size), at));
                _mm.SetInstanceColor(count, Start.Lerp(End, u) with { A = alpha });
                count++;
                target += Spacing;
            }
            acc += segLen;
        }
        _mm.VisibleInstanceCount = count;

        // anel onde a bola bate
        _ring.Visible = hitSomething;
        if (hitSomething)
        {
            var end = _path[^1];
            var n = hitNormal;
            var side = n.Cross(Mathf.Abs(n.Y) > 0.9f ? Vector3.Right : Vector3.Up).Normalized();
            var basis = new Basis(side, n, side.Cross(n).Normalized());
            float pulse = 1f + 0.12f * Mathf.Sin(Time.GetTicksMsec() * 0.008f);
            _ring.GlobalTransform = new Transform3D(basis * Basis.FromScale(new Vector3(pulse, 0.25f, pulse)), end + n * 0.03f);
        }
    }
}
