using Godot;

namespace AB;

public enum BallState { Held, Loose, Shot }

public partial class Ball : Node3D
{
    public Match M;
    public BallState State = BallState.Loose;
    public Player Holder;
    public Vector3 Vel;
    public Player LastShooter;
    public int ShotTeam = -1;
    public Vector3 ShotOrigin;
    public float FlightTime;

    MeshInstance3D _mesh;

    public override void _Ready()
    {
        float r = Tuning.I.BallRadius;
        _mesh = Mat.MeshNode(new SphereMesh { Radius = r, Height = r * 2f, RadialSegments = 24, Rings = 12 },
            Mat.ToonOutlined(Palette.Ball, 0.012f), Vector3.Zero);
        AddChild(_mesh);
        // "costuras" para dar leitura de rotação
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
        GlobalPosition = p.HeldBallPosition();
        ResetPhysicsInterpolation();
    }

    public void Shoot(Player by, Vector3 pos, Vector3 vel)
    {
        Holder = null;
        State = BallState.Shot;
        LastShooter = by;
        ShotTeam = by.Team;
        ShotOrigin = by.GlobalPosition;
        FlightTime = 0;
        GlobalPosition = pos;
        Vel = vel;
        ResetPhysicsInterpolation();
    }

    public void Drop(Vector3 pos, Vector3 vel)
    {
        Holder = null;
        State = BallState.Loose;
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
                Spin(np - GlobalPosition, dt);
                GlobalPosition = np;
            }
            return new BallHit { ScoredHoop = -1 };
        }

        var p = GlobalPosition;
        var hit = BallPhys.Simulate(ref p, ref Vel, dt, M.Geo);
        Spin(p - GlobalPosition, dt);
        GlobalPosition = p;
        FlightTime += dt;
        if (hit.Floor && State == BallState.Shot) State = BallState.Loose;
        return hit;
    }

    void Spin(Vector3 delta, float dt)
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
        if (State == BallState.Held || p.BallLockoutTimer > 0) return false;
        var t = Tuning.I;
        var d = GlobalPosition - p.GlobalPosition;
        float hd = new Vector2(d.X, d.Z).Length();
        if (hd > t.PickupRadius + t.BallRadius) return false;
        if (d.Y < -0.3f || d.Y > t.CatchHeight) return false;
        if (State == BallState.Shot && Vel.Y > 0) return false; // subindo: só bloqueio (salto) pode interferir
        return true;
    }
}
