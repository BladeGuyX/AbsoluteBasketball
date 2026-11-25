using System;
using System.Collections.Generic;
using Godot;

namespace AB;

/// <summary>
/// v0.21: --pose-shot &lt;png&gt; — prints das animações do personagem (sem partida), para conferir as poses:
/// &lt;png&gt;_a / _b / _c (vários estados lado a lado, vista 3/4) e &lt;png&gt;_faces (as 6 expressões).
/// Cada boneco é animado por 1,5 s simulados com um RigState roteirizado.
/// </summary>
public static class PoseShot
{
    const float T = 1.5f, Dt = 1f / 60f;

    record Case(string Name, Func<float, Vector3, RigState> State);

    static Vector3 Held(float yaw, float up, float fwd, float right = 0) =>
        Vector3.Up * up + ShotMath.Dir(yaw) * fwd + new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw)) * right;

    static float Dribble(float t, float period, float hand)
    {
        float r = Tuning.I.BallRadius;
        float ph = t * Mathf.Pi / period;
        return r + (hand - r) * Mathf.Abs(Mathf.Cos(ph));
    }

    static Vector3 DunkBall(float yaw, float k)
    {
        float reach = Tuning.I.DunkHandReach, up, fwd;
        if (k < 0.55f) { float a = Mathf.SmoothStep(0, 1, k / 0.55f); up = Mathf.Lerp(1.15f, reach + 0.05f, a); fwd = Mathf.Lerp(0.45f, -0.2f, a); }
        else if (k < 0.8f) { float a = (k - 0.55f) / 0.25f; up = reach + 0.05f + 0.1f * a; fwd = -0.2f - 0.05f * a; }
        else { float a = Mathf.SmoothStep(0, 1, (k - 0.8f) / 0.2f); up = Mathf.Lerp(reach + 0.15f, reach - 0.05f, a); fwd = Mathf.Lerp(-0.25f, 0.55f, a); }
        return Held(yaw, up, fwd);
    }

    static float Yaw = Mathf.DegToRad(40f);

    static List<Case> Cases()
    {
        var t = Tuning.I;
        float y = Yaw;
        return new List<Case>
        {
            new("parado", (s, o) => new RigState { Yaw = y }),
            new("andando", (s, o) => new RigState { Yaw = y, Speed = t.WalkSpeed }),
            new("correndo", (s, o) => new RigState { Yaw = y, Speed = t.RunSpeed }),
            new("quique (alto)", (s, o) => new RigState { Yaw = y, HasBall = true, BallSide = 1, BallPos = o + Held(y, Dribble(1.0f, t.DribblePeriod, t.DribbleHandHeight), 0.36f, 0.46f) }),
            new("quique (baixo)", (s, o) => new RigState { Yaw = y, HasBall = true, BallSide = 1, BallPos = o + Held(y, Dribble(s < 1.25f ? s : 1.25f, t.DribblePeriod, t.DribbleHandHeight), 0.36f, 0.46f) }),
            new("andando c/ bola", (s, o) => new RigState { Yaw = y, Speed = t.WalkSpeed * t.CarrierSpeedMult, HasBall = true, BallSide = 1, BallPos = o + Held(y, Dribble(s, t.DribblePeriod, t.DribbleHandHeight), 0.36f, 0.46f) }),
            new("drible especial", (s, o) =>
            {
                float ph = s * t.DribbleBounceMult * Mathf.Pi / t.DribblePeriod, side = Mathf.Sin(ph * 0.5f + 0.6f);
                float hand = Mathf.Lerp(t.DribbleHandHeight, t.BallRadius * 2.2f, Mathf.Clamp(t.DribbleCrouch, 0, 1) * 0.6f);
                float h = t.BallRadius + (hand - t.BallRadius) * Mathf.Abs(Mathf.Cos(ph));
                return new RigState { Yaw = y, HasBall = true, Dribbling = true, BallSide = side, BallPos = o + Held(y, h, 0.36f, 0.46f * side) };
            }),
            new("carregando arremesso", (s, o) => new RigState { Yaw = y, HasBall = true, Charging = true, Charge = s, BallPos = o + Held(y, 2.02f, 0.33f) }),
            new("soltou arremesso", (s, o) => new RigState { Yaw = y, Release = 0.62f }),
            new("carregando passe", (s, o) => new RigState { Yaw = y, HasBall = true, PassCharging = true, BallPos = o + Held(y, 1.25f, 0.5f) }),
            new("soltou passe", (s, o) => new RigState { Yaw = y, PassRelease = 0.7f }),
            new("carregando salto", (s, o) => new RigState { Yaw = y, JumpCharging = true }),
            new("no ar (toco)", (s, o) => new RigState { Yaw = y, Airborne = true, VelY = 3f }),
            new("caindo", (s, o) => new RigState { Yaw = y, Airborne = true, VelY = -4f }),
            new("pousou", (s, o) => new RigState { Yaw = y, Landing = true }),
            new("marcando", (s, o) => new RigState { Yaw = y, Defending = true }),
            new("roubo (bote)", (s, o) => new RigState { Yaw = y, Dashing = true }),
            new("errou o roubo", (s, o) => new RigState { Yaw = y, Slowed = true, Speed = 1.2f }),
            new("atordoado", (s, o) => new RigState { Yaw = y, Stunned = true }),
            new("enterrada 15%", (s, o) => Dunk(o, s, 0.15f)),
            new("enterrada 45%", (s, o) => Dunk(o, s, 0.45f)),
            new("enterrada 75%", (s, o) => Dunk(o, s, 0.75f)),
            new("enterrada 97%", (s, o) => Dunk(o, s, 0.97f)),
            new("pendurado no aro", (s, o) => new RigState { Yaw = y, Airborne = true, Hanging = true, RimPos = o + Held(y, t.DunkHandReach - 0.12f, t.RimRadius + 0.22f) }),
            new("comemorando", (s, o) => new RigState { Yaw = y, Mood = 1 }),
            new("perdeu", (s, o) => new RigState { Yaw = y, Mood = 2 }),
            new("correndo c/ bola", (s, o) => new RigState { Yaw = y, Speed = t.RunSpeed * t.CarrierSpeedMult, HasBall = true, BallSide = 1, BallPos = o + Held(y, Dribble(s, t.DribblePeriod, t.DribbleHandHeight), 0.36f, 0.46f) }),
        };
    }

    static RigState Dunk(Vector3 o, float s, float target)
    {
        float k = Mathf.Min(target, s / T * target * 1.4f);
        return new RigState { Yaw = Yaw, HasBall = true, Dunking = true, Airborne = true, FlightK = k, BallPos = o + DunkBall(Yaw, k) };
    }

    public static void Run(Node host, string path)
    {
        if (OS.GetCmdlineUserArgs() is var ua && System.Array.IndexOf(ua, "--side") >= 0) Yaw = Mathf.DegToRad(-80f);
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetSize(new Vector2I(1800, 1000));
        var root = new Node3D { Name = "PoseShot" };
        host.AddChild(root);
        var env = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("8fb8d8"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.75f, 0.78f, 0.85f), AmbientLightEnergy = 0.6f };
        root.AddChild(new WorldEnvironment { Environment = env });
        root.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -30, 0), ShadowEnabled = true, LightEnergy = 1.1f });
        root.AddChild(Mat.MeshNode(new PlaneMesh { Size = new Vector2(80, 40) }, Mat.Toon(new Color("c99a62")), Vector3.Zero));
        var cam = new Camera3D { Fov = 31, Current = true };
        root.AddChild(cam);
        var ballMat = Mat.ToonOutlined(Palette.Ball, 0.012f);

        var cases = Cases();
        const int perPage = 6, cols = 6;
        int pages = (cases.Count + perPage - 1) / perPage;
        var tree = host.GetTree();
        var labels = new List<Node>();
        string Out(string sfx) => System.IO.Path.ChangeExtension(path, null) + sfx + ".png";

        void Page(int pg)
        {
            foreach (var n in labels) n.QueueFree();
            labels.Clear();
            for (int i = 0; i < perPage; i++)
            {
                int ci = pg * perPage + i;
                if (ci >= cases.Count) break;
                var c = cases[ci];
                var holder = new Node3D { Position = new Vector3((i % cols) * 2.1f, 0, 0) };
                root.AddChild(holder);
                labels.Add(holder);
                var vis = new CharacterVisual(holder, i % 2, null);
                var ball = Mat.MeshNode(new SphereMesh { Radius = Tuning.I.BallRadius, Height = Tuning.I.BallRadius * 2 }, ballMat, Vector3.Zero);
                holder.AddChild(ball);
                var pre = c.State(T, holder.GlobalPosition);
                float lift = pre.Hanging || pre.Dunking ? 0.9f : pre.Airborne ? 0.6f : 0f;
                holder.Position += Vector3.Up * lift;
                RigState st = default;
                for (float s = 0; s <= T; s += Dt)
                {
                    st = c.State(s, holder.GlobalPosition);
                    vis.Animate(st, Dt);
                }
                ball.Visible = st.HasBall;
                if (st.HasBall) ball.GlobalPosition = st.BallPos;
                if (st.Hanging)
                {
                    var rim = Mat.MeshNode(new TorusMesh { InnerRadius = Tuning.I.RimRadius - 0.03f, OuterRadius = Tuning.I.RimRadius + 0.03f }, Mat.Toon(Palette.Rim), Vector3.Zero, false);
                    holder.AddChild(rim);
                    rim.GlobalPosition = st.RimPos;
                    rim.RotationDegrees = new Vector3(0, 0, 0);
                }
                var lbl = new Label3D { Text = c.Name, FontSize = 64, OutlineSize = 14, PixelSize = 0.004f, Position = new Vector3(0, 0.05f - lift, -1.1f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
                holder.AddChild(lbl);
            }
            float w = (perPage - 1) * 2.1f;
            cam.Position = new Vector3(w / 2f, 2.0f, -12.5f);
            cam.LookAt(new Vector3(w / 2f, 1.0f, 0), Vector3.Up);
        }

        void Faces()
        {
            foreach (var n in labels) n.QueueFree();
            labels.Clear();
            for (int i = 1; i <= Expressions.Count; i++)
            {
                var holder = new Node3D { Position = new Vector3((i - 1) * 0.8f, 0, 20) };
                root.AddChild(holder);
                var vis = new CharacterVisual(holder, 0, null);
                for (float s = 0; s <= 0.5f; s += Dt) vis.Animate(new RigState { Yaw = Mathf.Pi, Emote = (byte)i }, Dt);
                var lbl = new Label3D { Text = Expressions.Label[i], FontSize = 48, OutlineSize = 12, PixelSize = 0.0015f, Position = new Vector3(0, 1.1f, 0.5f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
                holder.AddChild(lbl);
            }
            float sc = Tuning.I.CharacterScale;
            float cx = (Expressions.Count - 1) * 0.8f / 2f;
            cam.Position = new Vector3(cx, 1.3f * sc, 20 + 4.4f);
            cam.LookAt(new Vector3(cx, 1.28f * sc, 20), Vector3.Up);
        }

        int page = 0;
        void Next()
        {
            if (page < pages) { Page(page); }
            else if (page == pages) { Faces(); }
            else { tree.Quit(); return; }
            tree.CreateTimer(0.6).Timeout += () =>
            {
                string name = page < pages ? "_" + (char)('a' + page) : "_faces";
                host.GetViewport().GetTexture().GetImage().SavePng(Out(name));
                GD.Print("[POSE] " + Out(name));
                page++;
                Next();
            };
        }
        Next();
    }
}
