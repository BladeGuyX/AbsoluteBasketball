using Godot;

namespace AB;

/// <summary>Câmera em terceira pessoa (sobre o ombro). Mouse e analógico direito funcionam ao mesmo tempo.</summary>
public partial class CameraRig : Node3D
{
    public float Yaw;
    public float Pitch = Mathf.DegToRad(-12f);
    public Player Target;
    public Camera3D Cam;
    public bool LookEnabled = true;
    /// <summary>Ignora movimento do mouse até este instante (evita "pulo" da câmera ao voltar do alt-tab).</summary>
    public static ulong IgnoreMouseUntilMs;
    Vector3 _pivot;
    bool _hasPivot;

    public override void _Ready()
    {
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        Cam = new Camera3D { Fov = Tuning.I.CameraFov, Current = true, Far = 400f };
        Cam.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        AddChild(Cam);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!LookEnabled || Input.MouseMode != Input.MouseModeEnum.Captured) return;
        if (e is InputEventMouseMotion mm)
        {
            if (Time.GetTicksMsec() < IgnoreMouseUntilMs || mm.ScreenRelative.Length() > 400f) return;
            float k = Mathf.DegToRad(Tuning.I.MouseDegPerPixel) * UserSettings.I.MouseSensitivity;
            Yaw -= mm.ScreenRelative.X * k;
            Pitch -= mm.ScreenRelative.Y * k * (UserSettings.I.InvertY ? -1f : 1f);
            ClampPitch();
        }
    }

    void ClampPitch() =>
        Pitch = Mathf.Clamp(Pitch, Mathf.DegToRad(Tuning.I.CameraPitchMinDeg), Mathf.DegToRad(Tuning.I.CameraPitchMaxDeg));

    public void SnapBehind(float yaw)
    {
        Yaw = yaw;
        Pitch = Mathf.DegToRad(-12f);
        _hasPivot = false;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var t = Tuning.I;
        Cam.Fov = t.CameraFov;
        if (LookEnabled)
        {
            var look = Input.GetVector(Act.LookL, Act.LookR, Act.LookU, Act.LookD);
            float k = Mathf.DegToRad(t.StickDegPerSec) * UserSettings.I.StickSensitivity * dt;
            Yaw -= look.X * k;
            Pitch -= look.Y * k * (UserSettings.I.InvertY ? -1f : 1f);
            ClampPitch();
        }
        if (Target == null || !IsInstanceValid(Target)) return;

        var targetPivot = Target.GetGlobalTransformInterpolated().Origin + Vector3.Up * t.CameraHeight;
        if (!_hasPivot) { _pivot = targetPivot; _hasPivot = true; }
        _pivot = _pivot.Lerp(targetPivot, 1f - Mathf.Exp(-t.CameraFollowSmooth * dt));

        var basis = Basis.FromEuler(new Vector3(Pitch, Yaw, 0));
        var pos = _pivot + basis.Z * t.CameraDistance + basis.X * t.CameraShoulderOffset;
        if (pos.Y < 0.35f) pos.Y = 0.35f;
        Cam.GlobalTransform = new Transform3D(basis, pos);
    }
}

/// <summary>Lê teclado/mouse/controle e gera o PlayerInput do jogador local (chamado no tick de física).</summary>
public sealed class LocalInputReader
{
    bool _sprintToggled;

    public PlayerInput Build(CameraRig cam, bool enabled)
    {
        var inp = new PlayerInput { AimYaw = cam.Yaw, AimPitch = cam.Pitch };
        if (!enabled) { _sprintToggled = false; return inp; }

        var mv = Input.GetVector(Act.MoveL, Act.MoveR, Act.MoveF, Act.MoveB);
        float y = cam.Yaw;
        var right = new Vector3(Mathf.Cos(y), 0, -Mathf.Sin(y));
        var back = new Vector3(Mathf.Sin(y), 0, Mathf.Cos(y));
        var w = right * mv.X + back * mv.Y;
        if (w.LengthSquared() > 1f) w = w.Normalized();
        inp.Move = new Vector2(w.X, w.Z);

        void B(string action, Btn b)
        {
            if (Input.IsActionPressed(action)) inp.Held |= b;
            if (Input.IsActionJustPressed(action)) inp.Pressed |= b;
        }
        B(Act.Shoot, Btn.Shoot); B(Act.Pass, Btn.Pass); B(Act.Jump, Btn.Jump);
        B(Act.Dribble, Btn.Dribble); B(Act.Steal, Btn.Steal); B(Act.Variation, Btn.Variation);

        // corrida: segurar ou alternar
        if (UserSettings.I.SprintToggle)
        {
            if (Input.IsActionJustPressed(Act.Sprint)) _sprintToggled = !_sprintToggled;
            if (mv.LengthSquared() < 0.01f) _sprintToggled = false;
            if (_sprintToggled) inp.Held |= Btn.Sprint;
        }
        else if (Input.IsActionPressed(Act.Sprint)) inp.Held |= Btn.Sprint;
        if (Input.IsActionJustPressed(Act.Sprint)) inp.Pressed |= Btn.Sprint;
        return inp;
    }
}
