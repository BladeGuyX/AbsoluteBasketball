using Godot;

namespace AB;

/// <summary>
/// Vibração do controle (Etapa 6). Vigia o estado do jogador local (funciona igual no host e no cliente online,
/// porque usa só o estado replicado) e dá pulsos leves/médios/fortes (força e duração no Tuning, seção "Vibração").
/// Só vibra se o último dispositivo usado foi o controle e a vibração estiver ligada nas configurações.
/// </summary>
public sealed class Rumble
{
    readonly Match M;
    bool _init, _hadBall, _air, _dunk;
    float _stun, _stealFail, _skillFlash, _ftFlash, _drib;
    int _myScore;

    public Rumble(Match m) { M = m; }

    enum Level { Light, Medium, Strong }

    static void Pulse(Level lv, float scale = 1f)
    {
        var s = UserSettings.I;
        if (!s.Vibration || !InputDevice.Gamepad || s.VibrationStrength <= 0.01f) return;
        var t = Tuning.I;
        float k = s.VibrationStrength * scale;
        var (weak, strong, dur) = lv switch
        {
            Level.Light => (t.RumbleLight, 0f, t.RumbleLightTime),
            Level.Medium => (t.RumbleMedium * 0.7f, t.RumbleMedium, t.RumbleMediumTime),
            _ => (t.RumbleStrong * 0.8f, t.RumbleStrong, t.RumbleStrongTime),
        };
        foreach (int id in Input.GetConnectedJoypads())
            Input.StartJoyVibration(id, Mathf.Clamp(weak * k, 0, 1), Mathf.Clamp(strong * k, 0, 1), dur);
    }

    public void Tick()
    {
        var p = M.LocalPlayer;
        if (p == null || !GodotObject.IsInstanceValid(p)) return;
        int my = M.Score[p.Team];
        if (!_init)
        {
            _init = true; _hadBall = p.HasBall; _air = p.Airborne; _myScore = my;
            return;
        }
        var ball = M.Ball;
        bool hasBall = p.HasBall;
        if (hasBall && !_hadBall) Pulse(Level.Light);                                     // pegou a bola
        if (!hasBall && _hadBall && ball.Holder != null && ball.Holder.Team != p.Team) Pulse(Level.Strong); // roubaram de você
        if (p.Stun > 0 && _stun <= 0) Pulse(Level.Strong, 1.2f);                          // atordoado (errou a enterrada)
        if (p.StealFail > 0 && _stealFail <= 0) Pulse(Level.Light, 1.3f);                 // errou o roubo
        if (p.SkillFlash > 0 && _skillFlash <= 0 && p.SkillFlashOk) Pulse(Level.Strong);  // enterrada certa
        if (M.FtFlash > 0 && _ftFlash <= 0 && M.FtFlashOk && M.FtFlashPlayer == p) Pulse(Level.Medium); // lance livre certo
        if (p.Flight == FlightKind.Dunk && !_dunk) Pulse(Level.Light);                     // saiu do chão para enterrar
        if (!p.Airborne && _air && p.Flight == FlightKind.None) Pulse(Level.Light, 0.6f);  // pousou
        if (p.DribbleActive > 0 && _drib <= 0) Pulse(Level.Light, 0.8f);                  // começou o drible
        if (my > _myScore) Pulse(Level.Medium);                                            // seu time pontuou
        _hadBall = hasBall; _stun = p.Stun; _stealFail = p.StealFail; _skillFlash = p.SkillFlash; _ftFlash = M.FtFlash;
        _dunk = p.Flight == FlightKind.Dunk; _air = p.Airborne; _drib = p.DribbleActive; _myScore = my;
    }
}
