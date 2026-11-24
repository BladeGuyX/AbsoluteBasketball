using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Teste automático da Etapa 2 (--smoke-e2): passe e devolução, drible, bloqueio e roubo.
/// Controla o jogador local; os bonecos usam a lógica normal deles.
/// </summary>
public sealed class ScenarioPilot
{
    readonly Match M;
    readonly Player P;
    int _step;
    float _t;
    int _tick;
    int _stealTries;
    float _afterShot;
    public bool PassOk, ReturnOk, AimPassOk, DribbleOk, JumpOk, BlockOk, StealFailOk, StealOk;
    public bool Done;

    public ScenarioPilot(Match m, Player p) { M = m; P = p; }

    Player Ally => M.Players.FirstOrDefault(q => q != P && q.Team == P.Team);
    Player Enemy => M.Players.FirstOrDefault(q => q.Team != P.Team);

    public void OnEvent(string kind, Player a, Player b)
    {
        if (kind == "CATCH" && a == Ally && _step == 0) PassOk = true;
        if (kind == "CATCH" && a == P && PassOk && _step == 0) ReturnOk = true;
        if (kind == "CATCH" && a == Ally && _step == 1) AimPassOk = true;
        if (kind == "DRIBBLE" && a == P) DribbleOk = true;
        if (kind == "JUMP" && a == Enemy) JumpOk = true;
        if (kind == "BLOCK" && a == Enemy) BlockOk = true;
        if (kind == "STEAL_FAIL" && a == P) StealFailOk = true;
        if (kind == "STEAL" && a == P) StealOk = true;
    }

    void Next() { _step++; _t = 0; _tick = 0; GD.Print($"[E2] passo {_step}"); }

    public PlayerInput Build(float dt)
    {
        var inp = new PlayerInput { AimYaw = P.Yaw };
        if (!M.CanAct) return inp;
        _t += dt;
        _tick++;
        switch (_step)
        {
            case 0: // passe (toque) para o aliado e espera a devolução
                if (_tick == 2 && P.HasBall) { inp.Pressed |= Btn.Pass; inp.Held |= Btn.Pass; }
                if (ReturnOk || _t > 8f) Next();
                break;
            case 1: // passe carregado na direção do aliado (sem alvo): segura 0,6 s e solta
            {
                var al = Ally;
                if (al == null || (AimPassOk && P.HasBall) || _t > 8f) { Next(); break; }
                if (P.HasBall)
                {
                    var to = al.GlobalPosition - P.GlobalPosition; to.Y = 0;
                    float d = to.Length();
                    var t = Tuning.I;
                    float th = 0.5f * Mathf.Asin(Mathf.Clamp(t.BallGravity * d / (t.AimPassSpeed * t.AimPassSpeed), 0, 1));
                    inp.AimYaw = ShotMath.YawOf(to);
                    inp.AimPitch = th - Mathf.DegToRad(t.AimPassLiftDeg);
                    if (!P.ChargingPass) { inp.Pressed |= Btn.Pass; inp.Held |= Btn.Pass; }
                    else if (P.PassCharge < 0.6f) inp.Held |= Btn.Pass;
                }
                break;
            }
            case 2: // drible especial
                if (_tick == 5 && P.HasBall) inp.Pressed |= Btn.Dribble;
                if (_t > 1.5f) Next();
                break;
            case 3: // arremesso na frente do boneco adversário → ele pula e bloqueia
            {
                var e = Enemy;
                if (e == null) { Next(); break; }
                if (!P.HasBall && !P.ChargingShot) { _afterShot += dt; if (BlockOk || _afterShot > 2.5f) Next(); break; }
                int hoop = CourtGeo.AttackHoop(P.Team);
                var rim = M.Geo.RimCenter[hoop];
                var dirToRim = (rim - e.GlobalPosition) with { Y = 0 };
                var spot = e.GlobalPosition - dirToRim.Normalized() * 0.9f;
                var to = spot - P.GlobalPosition; to.Y = 0;
                inp.AimYaw = ShotMath.YawOf((rim - P.GlobalPosition) with { Y = 0 });
                inp.AimPitch = ShotMath.PitchForDistance(9f);
                if (!P.ChargingShot && to.Length() > 0.3f) { inp.Move = new Vector2(to.Normalized().X, to.Normalized().Z); break; }
                if (!P.ChargingShot) { inp.Pressed |= Btn.Shoot; inp.Held |= Btn.Shoot; break; }
                bool release = e.Airborne && e.Velocity.Y < 2.5f;
                if (!release && P.ShotCharge < 2.5f) inp.Held |= Btn.Shoot;
                if (BlockOk || _t > 8f) Next();
                break;
            }
            case 4: // roubo: dá a bola ao boneco adversário e tenta roubar até conseguir
            {
                var e = Enemy;
                if (e == null) { Next(); break; }
                if (_tick == 1) { M.Ball.GiveTo(e); }
                if (StealOk || _t > 25f || _stealTries > 6) { Next(); break; }
                if (P.StealFail > 0 || P.StealDash > 0) break;
                var to = e.GlobalPosition - P.GlobalPosition; to.Y = 0;
                var d = to.Normalized();
                inp.Move = new Vector2(d.X, d.Z);
                if (to.Length() < 1.4f && M.Ball.Holder == e) { inp.Pressed |= Btn.Steal; _stealTries++; }
                break;
            }
            default:
                Done = true;
                break;
        }
        return inp;
    }
}
