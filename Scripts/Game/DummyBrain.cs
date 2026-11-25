using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Bonecos de treino (antes dos bots da Etapa 4):
/// - aliado: segura a bola um pouco e passa de volta (normal ou alto, se o corredor estiver bloqueado);
/// - adversário: pula contra quem carrega arremesso perto, dribla quando alguém chega perto
///   e, com a bola, anda até a cesta que ataca e arremessa.
/// - sem a bola e sem marcar: passeiam devagar perto da posição inicial, com pausas, olhando para a bola (v0.7).
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
    Vector3 _wander;
    float _wanderT;
    readonly RandomNumberGenerator _rng = new();

    public DummyBrain(Match m, Player p) { M = m; P = p; _rng.Seed = (ulong)(p.Id * 104729 + 3); }

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
        if (!P.Airborne && !P.ChargingJump)
        {
            var guard = GuardSpot();
            if (guard.HasValue) MoveTo(ref inp, guard.Value, 0.6f);
            else if (AttackComing()) MoveTo(ref inp, P.HomePos, 0.4f); // ataque chegando: volta para a posição
            else if (!(M.Ball.PassTarget == P && M.Ball.State is BallState.Pass or BallState.HighPass)) Wander(ref inp, dt);
        }
        var toBall = M.Ball.GlobalPosition - P.GlobalPosition; toBall.Y = 0;
        if (toBall.LengthSquared() > 0.01f) inp.AimYaw = ShotMath.YawOf(toBall);
        return inp;
    }

    /// <summary>Adversário com a bola perto da cesta que defendo: fico entre ele e o aro.</summary>
    Vector3? GuardSpot()
    {
        var t = Tuning.I;
        var c = M.Ball.Holder;
        if (IsAllyOfLocal || t.DummyGuardRange <= 0 || c == null || c.Team == P.Team) return null;
        var rim = M.Geo.RimCenter[CourtGeo.AttackHoop(c.Team)];
        var from = c.GlobalPosition - rim; from.Y = 0;
        if (from.Length() > t.DummyGuardRange) return null;
        var spot = rim + from.Normalized() * Mathf.Min(1.6f, from.Length() * 0.6f);
        spot.Y = 0;
        return spot;
    }

    void DefendShots(ref PlayerInput inp, float dt)
    {
        var t = Tuning.I;
        var threat = M.Players.FirstOrDefault(q => q.Team != P.Team && (q.ChargingShot || q.Flight == FlightKind.Dunk) &&
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

        float shootDist = Mathf.Min(8f, P.St.ShotRangeMax - 1f);
        if (!P.ChargingShot && toRim.Length() > shootDist)
        {
            var d = toRim.Normalized();
            inp.Move = new Vector2(d.X, d.Z);
            return inp;
        }
        if (P.IsDribbling) return inp;

        var rel = ShotMath.ReleasePos(P.GlobalPosition, yaw);
        float dist = new Vector2(rim.X - rel.X, rim.Z - rel.Z).Length();
        inp.AimPitch = ShotMath.PitchForDistance(dist, P.St.ShotRangeMax);
        const float need = 0.35f; // segurar não muda a distância: mira e solta
        if (!P.ChargingShot) { inp.Pressed |= Btn.Shoot; inp.Held |= Btn.Shoot; return inp; }
        if (P.ShotCharge + dt < need) inp.Held |= Btn.Shoot;
        return inp;
    }

    /// <summary>Adversário com a bola chegando perto da cesta que defendo (o dobro da distância de marcação).</summary>
    bool AttackComing()
    {
        var c = M.Ball.Holder;
        if (IsAllyOfLocal || c == null || c.Team == P.Team) return false;
        var rim = M.Geo.RimCenter[CourtGeo.AttackHoop(c.Team)];
        var d = c.GlobalPosition - rim; d.Y = 0;
        return d.Length() <= Mathf.Max(Tuning.I.DummyGuardRange * 2f, 10f);
    }

    /// <summary>Passeio: anda devagar até um ponto perto da posição inicial, para um pouco, escolhe outro.</summary>
    void Wander(ref PlayerInput inp, float dt)
    {
        float r = Tuning.I.DummyWanderRadius;
        if (r <= 0) { MoveTo(ref inp, P.HomePos, 0.6f); return; }
        _wanderT -= dt;
        if (_wanderT <= 0 || _wander == Vector3.Zero)
        {
            float a = _rng.Randf() * Mathf.Tau, d = Mathf.Sqrt(_rng.Randf()) * r;
            var g = M.Geo;
            var w = P.HomePos + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
            _wander = new Vector3(Mathf.Clamp(w.X, -g.HalfL + 1f, g.HalfL - 1f), 0, Mathf.Clamp(w.Z, -g.HalfW + 1f, g.HalfW - 1f));
            _wanderT = _rng.RandfRange(2.5f, 5.5f);
        }
        var to = _wander - P.GlobalPosition; to.Y = 0;
        if (to.Length() < 0.4f) return; // chegou: fica parado até o próximo ponto
        var dir = to.Normalized() * Mathf.Min(0.7f, 0.3f + to.Length() * 0.4f);
        inp.Move = new Vector2(dir.X, dir.Z);
    }

    void MoveTo(ref PlayerInput inp, Vector3 target, float tolerance)
    {
        var to = target - P.GlobalPosition; to.Y = 0;
        if (to.Length() < tolerance) return;
        var d = to.Length() > 1f ? to.Normalized() : to;
        inp.Move = new Vector2(d.X, d.Z);
    }
}
