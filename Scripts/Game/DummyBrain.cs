using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Bonecos de treino (antes dos bots da Etapa 4):
/// - aliado: segura a bola um pouco e passa de volta (normal ou alto, se o corredor estiver bloqueado);
/// - adversário: fica na posição, pula contra quem carrega arremesso perto, dribla quando alguém chega perto
///   e, com a bola, anda até a cesta que ataca e arremessa.
/// </summary>
public sealed class DummyBrain
{
    readonly Match M;
    readonly Player P;
    float _hold;
    int _passStep;
    bool _passHigh;
    bool _jumpedForThreat;
    float _react;

    public DummyBrain(Match m, Player p) { M = m; P = p; }

    bool IsAllyOfLocal => M.LocalPlayer != null && P.Team == M.LocalPlayer.Team;

    public PlayerInput Build(float dt)
    {
        var inp = new PlayerInput { AimYaw = P.Yaw };
        if (!M.CanAct) { _hold = 0; _passStep = 0; return inp; }

        if (P.HasBall)
        {
            _hold += dt;
            return IsAllyOfLocal ? PassBack(inp) : Attack(inp, dt);
        }
        _hold = 0;
        _passStep = 0;

        if (!IsAllyOfLocal) DefendShots(ref inp, dt);
        if (!P.Airborne && !P.ChargingJump) MoveTo(ref inp, P.HomePos, 0.6f);
        var toBall = M.Ball.GlobalPosition - P.GlobalPosition; toBall.Y = 0;
        if (toBall.LengthSquared() > 0.01f) inp.AimYaw = ShotMath.YawOf(toBall);
        return inp;
    }

    void DefendShots(ref PlayerInput inp, float dt)
    {
        var t = Tuning.I;
        var threat = M.Players.FirstOrDefault(q => q.Team != P.Team && q.ChargingShot &&
            q.GlobalPosition.DistanceTo(P.GlobalPosition) <= t.DummyJumpRange);
        if (threat == null) { _jumpedForThreat = false; _react = 0; return; }
        if (_jumpedForThreat) return;
        _react += dt;
        if (_react < t.DummyReactionTime || P.Airborne || P.LandLock > 0) return;
        inp.Pressed |= Btn.Jump; // toque: salto rápido
        _jumpedForThreat = true;
    }

    PlayerInput PassBack(PlayerInput inp)
    {
        if (_hold < Tuning.I.DummyPassBackDelay) return inp;
        switch (_passStep)
        {
            case 0:
                _passHigh = Passing.Nearest(P, false) == null;
                inp.Pressed |= Btn.Pass; inp.Held |= Btn.Pass;
                _passStep = _passHigh ? 1 : 2;
                break;
            case 1:
                inp.Pressed |= Btn.Variation; inp.Held |= Btn.Pass;
                _passStep = 2;
                break;
            default:
                _passStep = 0; // solta → toque rápido para o aliado livre mais próximo
                _hold = Tuning.I.DummyPassBackDelay * 0.5f;
                break;
        }
        return inp;
    }

    PlayerInput Attack(PlayerInput inp, float dt)
    {
        var t = Tuning.I;
        int hoop = CourtGeo.AttackHoop(P.Team);
        var rim = M.Geo.RimCenter[hoop];
        var toRim = rim - P.GlobalPosition; toRim.Y = 0;
        float yaw = ShotMath.YawOf(toRim);
        inp.AimYaw = yaw;

        // dribla quando um adversário chega perto
        var close = M.Players.Any(q => q.Team != P.Team && q.GlobalPosition.DistanceTo(P.GlobalPosition) < 2.5f);
        if (close && !P.IsDribbling && !P.ChargingShot && P.DribbleCharges > 0 && _hold > 0.3f)
        {
            inp.Pressed |= Btn.Dribble;
            return inp;
        }

        float shootDist = Mathf.Min(8f, t.ShotRangeMax - 1f);
        if (!P.ChargingShot && toRim.Length() > shootDist)
        {
            var d = toRim.Normalized();
            inp.Move = new Vector2(d.X, d.Z);
            return inp;
        }
        if (P.IsDribbling) return inp;

        var rel = ShotMath.ReleasePos(P.GlobalPosition, yaw);
        float dist = new Vector2(rim.X - rel.X, rim.Z - rel.Z).Length();
        inp.AimPitch = ShotMath.PitchForDistance(dist);
        float need = toRim.Length() <= t.CloseShotRadius ? 0f : ShotMath.ChargeForDistance(dist);
        if (float.IsNaN(need)) need = t.ShotChargeTime;
        if (!P.ChargingShot) { inp.Pressed |= Btn.Shoot; inp.Held |= Btn.Shoot; return inp; }
        if (P.ShotCharge + dt < need) inp.Held |= Btn.Shoot;
        return inp;
    }

    void MoveTo(ref PlayerInput inp, Vector3 target, float tolerance)
    {
        var to = target - P.GlobalPosition; to.Y = 0;
        if (to.Length() < tolerance) return;
        var d = to.Length() > 1f ? to.Normalized() : to;
        inp.Move = new Vector2(d.X, d.Z);
    }
}
