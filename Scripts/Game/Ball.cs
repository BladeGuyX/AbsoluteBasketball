using Godot;

namespace AB;

/// <summary>
/// Held = na mão. Loose = solta (quicando/rolando). Shot = arremesso em voo.
/// Pass = passe normal (qualquer um no caminho pega). HighPass = passe alto (só pega caindo; bloqueável no salto).
/// </summary>
public enum BallState { Held, Loose, Shot, Pass, HighPass }

public partial class Ball : Node3D
{
    public Match M;
    public BallState State = BallState.Loose;
    public Player Holder;
    public Vector3 Vel;
    public Player LastThrower;
    public int ThrowTeam = -1;     // time de quem arremessou/passou (para bloqueio)
    public Vector3 ShotOrigin;     // pés de quem arremessou (2 ou 3 pontos)
    public Player PassTarget;
    public float FlightTime;
    int _noScoreHoop = -1;         // passou subindo pelo aro: a descida não vale cesta

    MeshInstance3D _mesh;

    public override void _Ready()
    {
        float r = Tuning.I.BallRadius;
        _mesh = Mat.MeshNode(new SphereMesh { Radius = r, Height = r * 2f, RadialSegments = 24, Rings = 12 },
            Mat.ToonOutlined(Palette.Ball, 0.012f), Vector3.Zero);
        AddChild(_mesh);
        var seam = Mat.Unshaded(Palette.Ink);
        _mesh.AddChild(Mat.MeshNode(new TorusMesh { InnerRadius = r * 0.985f, OuterRadius = r * 1.03f, Rings = 24, RingSegments = 4 }, seam, Vector3.Zero, false));
        var t2 = Mat.MeshNode(new TorusMesh { InnerRadius = r * 0.985f, OuterRadius = r * 1.03f, Rings = 24, RingSegments = 4 }, seam, Vector3.Zero, false);
        t2.RotationDegrees = new Vector3(90, 0, 0);
        _mesh.AddChild(t2);
    }

    public void GiveTo(Player p)
    {
        State = BallState.Held;
        Holder = p;
        Vel = Vector3.Zero;
        PassTarget = null;
        _noScoreHoop = -1;
        GlobalPosition = p.HeldBallPosition();
        ResetPhysicsInterpolation();
    }

    /// <summary>Solta a bola em voo (arremesso ou passe).</summary>
    public void Throw(Player by, Vector3 pos, Vector3 vel, BallState state, Player target = null)
    {
        Holder = null;
        State = state;
        LastThrower = by;
        ThrowTeam = by.Team;
        ShotOrigin = by.GlobalPosition;
        _noScoreHoop = -1;
        PassTarget = target;
        FlightTime = 0;
        GlobalPosition = pos;
        Vel = vel;
        ResetPhysicsInterpolation();
    }

    public void Drop(Vector3 pos, Vector3 vel)
    {
        Holder = null;
        State = BallState.Loose;
        PassTarget = null;
        GlobalPosition = pos;
        Vel = vel;
        ResetPhysicsInterpolation();
    }

    public BallHit ServerTick(float dt)
    {
        if (State == BallState.Held)
        {
            if (Holder != null)
            {
                var np = Holder.HeldBallPosition();
                Spin(np - GlobalPosition);
                GlobalPosition = np;
            }
            return new BallHit { ScoredHoop = -1 };
        }

        var p = GlobalPosition;
        int assist = State == BallState.Shot && ThrowTeam >= 0 ? CourtGeo.AttackHoop(ThrowTeam) : -1;
        var hit = BallPhys.Simulate(ref p, ref Vel, dt, M.Geo, assist);
        if (hit.UpThroughHoop >= 0) _noScoreHoop = hit.UpThroughHoop;
        if (_noScoreHoop >= 0)
        {
            if (hit.ScoredHoop == _noScoreHoop) hit.ScoredHoop = -1;
            var c = M.Geo.RimCenter[_noScoreHoop];
            float hd = new Vector2(p.X - c.X, p.Z - c.Z).Length();
            if (p.Y < c.Y - 0.6f || hd > Tuning.I.RimRadius + Tuning.I.BallRadius + 0.4f) _noScoreHoop = -1;
        }
        Spin(p - GlobalPosition);
        GlobalPosition = p;
        FlightTime += dt;
        if (hit.Floor && State != BallState.Loose) { State = BallState.Loose; PassTarget = null; }
        return hit;
    }

    void Spin(Vector3 delta)
    {
        var h = new Vector3(delta.X, 0, delta.Z);
        float d = h.Length();
        if (d < 1e-5f) return;
        var axis = Vector3.Up.Cross(h / d).Normalized();
        _mesh.RotateObjectLocal(_mesh.Basis.Inverse() * axis, d / Tuning.I.BallRadius);
        _mesh.Orthonormalize();
    }

    public bool CanBeCaughtBy(Player p)
    {
        if (State == BallState.Held || p.BallLockoutTimer > 0 || !p.CanCatch) return false;
        var t = Tuning.I;
        var d = GlobalPosition - p.GlobalPosition;
        float hd = new Vector2(d.X, d.Z).Length();
        if (hd > t.PickupRadius + t.BallRadius) return false;
        if (d.Y < -0.3f || d.Y > t.CatchHeight) return false;
        // arremesso e passe alto subindo: ninguém pega (só o bloqueio no salto interfere)
        if ((State == BallState.Shot || State == BallState.HighPass) && Vel.Y > 0) return false;
        return true;
    }
}
