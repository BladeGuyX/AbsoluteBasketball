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
    float _shake, _shakeT;

    /// <summary>v0.21: tremor (enterrada). Some sozinho em ~0,5 s.</summary>
    public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

    public override void _Ready()
    {
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        Cam = new Camera3D { Fov = Tuning.I.CameraFov, Current = true, Far = 400f };
        Cam.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        AddChild(Cam);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!LookEnabled || EmoteWheel.IsOpen || Input.MouseMode != Input.MouseModeEnum.Captured) return;
        if (e is InputEventMouseMotion mm)
        {
            if (Time.GetTicksMsec() < IgnoreMouseUntilMs || mm.ScreenRelative.Length() > 400f) return;
            float k = Mathf.DegToRad(Tuning.I.MouseDegPerPixel) * UserSettings.I.MouseSensitivity * ChargeMult();
            Yaw -= mm.ScreenRelative.X * k * (UserSettings.I.InvertX ? -1f : 1f);
            Pitch -= mm.ScreenRelative.Y * k * (UserSettings.I.InvertY ? -1f : 1f);
            ClampPitch();
        }
    }

    /// <summary>Carregando arremesso/passe: mira mais precisa (sensibilidade menor).</summary>
    float ChargeMult() => Target != null && IsInstanceValid(Target) && (Target.ChargingShot || Target.ChargingPass) ? Tuning.I.ChargeLookMult : 1f;

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
        Cam.Fov = Mathf.Clamp(t.CameraFov + UserSettings.I.FovOffset, 30f, 120f);
        if (LookEnabled)
        {
            var look = Input.GetVector(Act.LookL, Act.LookR, Act.LookU, Act.LookD);
            // curva: movimentos pequenos mais precisos, o máximo continua igual
            float len = look.Length();
            if (len > 0.001f) look = look / len * Mathf.Pow(Mathf.Min(len, 1f), Mathf.Max(t.StickLookExponent, 1f));
            float k = Mathf.DegToRad(t.StickDegPerSec) * UserSettings.I.StickSensitivity * ChargeMult() * dt;
            Yaw -= look.X * k * (UserSettings.I.InvertX ? -1f : 1f);
            Pitch -= look.Y * k * (UserSettings.I.InvertY ? -1f : 1f);
            ClampPitch();
        }
        if (Target == null || !IsInstanceValid(Target)) return;

        var targetPivot = Target.GetGlobalTransformInterpolated().Origin + Vector3.Up * t.CameraHeight;
        if (!_hasPivot) { _pivot = targetPivot; _hasPivot = true; }
        _pivot = _pivot.Lerp(targetPivot, 1f - Mathf.Exp(-t.CameraFollowSmooth * dt));

        var basis = Basis.FromEuler(new Vector3(Pitch, Yaw, 0));
        var pos = _pivot + basis.Z * t.CameraDistance * Mathf.Clamp(UserSettings.I.CameraDistanceMult, 0.5f, 2f) + basis.X * t.CameraShoulderOffset;
        if (pos.Y < 0.35f) pos.Y = 0.35f;
        if (_shake > 0.002f)
        {
            _shakeT += dt;
            pos += basis * new Vector3(Mathf.Sin(_shakeT * 71f), Mathf.Sin(_shakeT * 53f + 1f), 0) * (_shake * 0.13f);
            Cam.Fov -= 7f * _shake;
            _shake *= Mathf.Exp(-7f * dt);
        }
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
        // roda de expressões aberta no controle: o analógico esquerdo escolhe a expressão (não anda)
        if (EmoteWheel.IsOpen && InputDevice.Gamepad) mv = Vector2.Zero;
        inp.Emote = EmoteWheel.TakeChosen();
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
