using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Teste automático da Etapa 3 (--smoke-e3): enterrada certa, enterrada errada (atordoado),
/// tapinha (bola solta perto do aro) e falta (boneco bloqueia a enterrada).
/// </summary>
public sealed class DunkScenario
{
    readonly Match M;
    readonly Player P;
    int _step;
    float _t;
    int _tick;
    bool _setup, _reachedWaypoint, _pressed, _sawFoulRestart;
    public bool DunkOk, DunkFailOk, StunOk, TipOk, FoulOk, FoulCarrierOk;
    public bool Done;

    public DunkScenario(Match m, Player p) { M = m; P = p; }

    Player Enemy => M.Players.FirstOrDefault(q => q.Team != P.Team);

    public void OnEvent(string kind, Player a, Player b)
    {
        if (kind == "DUNK_OK" && a == P && _step == 0) DunkOk = true;
        if (kind == "DUNK_FAIL" && a == P && _step == 1) DunkFailOk = true;
        if (kind == "TIP_OK" && a == P && _step == 2) TipOk = true;
        if (kind == "FOUL" && b == P && _step == 3) FoulOk = true;
    }

    void Next() { _step++; _t = 0; _tick = 0; _setup = false; _reachedWaypoint = false; _pressed = false; GD.Print($"[E3] passo {_step}"); }

    int Hoop => CourtGeo.AttackHoop(P.Team);
    Vector3 Rim => M.Geo.RimCenter[Hoop];
    float S => CourtGeo.HoopSign(Hoop);

    static void RunTo(ref PlayerInput inp, Player p, Vector3 target, bool sprint)
    {
        var to = target - p.GlobalPosition; to.Y = 0;
        if (to.LengthSquared() < 1e-4f) return;
        var d = to.Normalized();
        inp.Move = new Vector2(d.X, d.Z);
        if (sprint) inp.Held |= Btn.Sprint;
    }

    /// <summary>Corre até a cesta (passando por um ponto lateral) e salta para enterrar; opcionalmente acerta o skill check.</summary>
    void DunkRun(ref PlayerInput inp, float dt, bool hitSkill)
    {
        if (P.Flight == FlightKind.Dunk)
        {
            if (hitSkill && !_pressed && P.FlightT + dt >= P.FlightDur - 0.01f) { inp.Pressed |= Btn.Shoot; inp.Held |= Btn.Shoot; _pressed = true; }
            return;
        }
        if (!P.HasBall || P.Airborne) return;
        var wp = Rim - new Vector3(S * 7f, 0, -3f); wp.Y = 0;
        if (!_reachedWaypoint && (P.GlobalPosition - wp).Length() < 1f) _reachedWaypoint = true;
        var rimFlat = Rim with { Y = 0 };
        RunTo(ref inp, P, _reachedWaypoint ? rimFlat : wp, true);
        if (_reachedWaypoint && P.CanDunk()) inp.Pressed |= Btn.Jump;
    }

    public PlayerInput Build(float dt)
    {
        var inp = new PlayerInput { AimYaw = P.Yaw };
        _t += dt;
        if (!M.CanAct)
        {
            if (_step == 3 && FoulOk) _sawFoulRestart = true;
            return inp;
        }
        _tick++;
        if (_t > 25f) { Next(); return inp; }
        switch (_step)
        {
            case 0: // enterrada com o skill check certo
            case 1: // enterrada sem apertar → erra e fica atordoado
            {
                if (!_setup)
                {
                    if (!P.HasBall) M.Ball.GiveTo(P);
                    _setup = true;
                }
                bool done = _step == 0 ? DunkOk : DunkFailOk;
                if (!done) { DunkRun(ref inp, dt, _step == 0); break; }
                if (_step == 1)
                {
                    if (P.Stun > 0) StunOk = true;
                    if (StunOk && P.Stun <= 0) Next();
                }
                else if (!P.Airborne) Next();
                break;
            }
            case 2: // tapinha: bola solta perto do aro, pula na direção dela
            {
                if (!_setup)
                {
                    var start = Rim - new Vector3(S * 2.2f, 0, 0); start.Y = 0.05f;
                    P.Teleport(start, ShotMath.YawOf(new Vector3(S, 0, 0)));
                    if (M.Ball.Holder != null) M.Ball.Drop(M.Ball.GlobalPosition, Vector3.Zero);
                    M.Ball.Drop(Rim + new Vector3(-S * (Tuning.I.RimRadius + 0.35f), 0.3f, 0), new Vector3(0, 2.0f, 0));
                    _setup = true;
                    _tick = 0;
                }
                if (TipOk) { if (!P.Airborne) Next(); break; }
                RunTo(ref inp, P, Rim with { Y = 0 }, false);
                if (_tick == 1) { inp.Pressed |= Btn.Jump; inp.Held |= Btn.Jump; }
                break;
            }
            case 3: // falta: boneco na frente da cesta bloqueia a enterrada
            {
                var e = Enemy;
                if (e == null) { Next(); break; }
                if (!_setup)
                {
                    if (!P.HasBall) M.Ball.GiveTo(P);
                    var guard = Rim - new Vector3(S * 1.6f, 0, 0); guard.Y = 0.05f;
                    e.Teleport(guard, ShotMath.YawOf(new Vector3(-S, 0, 0)));
                    var start = Rim - new Vector3(S * 8f, 0, 0.4f); start.Y = 0.05f;
                    P.Teleport(start, ShotMath.YawOf(new Vector3(S, 0, 0)));
                    _reachedWaypoint = true;
                    _setup = true;
                }
                if (_sawFoulRestart)
                {
                    FoulCarrierOk = M.Ball.Holder == P;
                    Next();
                    break;
                }
                if (!FoulOk) DunkRun(ref inp, dt, false);
                break;
            }
            default:
                Done = true;
                break;
        }
        return inp;
    }
}
