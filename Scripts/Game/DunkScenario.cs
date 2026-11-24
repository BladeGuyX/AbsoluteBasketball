using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Teste automático da Etapa 3 (--smoke-e3): enterrada certa, enterrada errada (atordoado),
/// enterrada ANDANDO com Espaço no skill check (tem que errar: Espaço não vale), falta (boneco bloqueia a enterrada)
/// e os 2 lances livres (acerta o 1º = +1; erra o 2º = bola viva e jogo continua).
/// </summary>
public sealed class DunkScenario
{
    readonly Match M;
    readonly Player P;
    int _step;
    float _t;
    int _tick;
    bool _setup, _reachedWaypoint, _pressed, _sawFt;
    int _scoreAtFoul = -1;
    float _readyT;
    public bool DunkOk, DunkFailOk, StunOk, WalkDunkOk, SpaceIgnoredOk, FoulOk, FtPointOk, FtLiveOk;
    public bool Done;

    public DunkScenario(Match m, Player p) { M = m; P = p; }

    Player Enemy => M.Players.FirstOrDefault(q => q.Team != P.Team);

    public void OnEvent(string kind, Player a, Player b)
    {
        if (kind == "DUNK_OK" && a == P && _step == 0) DunkOk = true;
        if (kind == "DUNK_FAIL" && a == P && _step == 1) DunkFailOk = true;
        if (kind == "DUNK" && a == P && _step == 2) WalkDunkOk = true;
        if (kind == "DUNK_FAIL" && a == P && _step == 2) SpaceIgnoredOk = true;
        if (kind == "FOUL" && b == P && _step == 3) { FoulOk = true; _scoreAtFoul = M.Score[P.Team]; }
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
    void DunkRun(ref PlayerInput inp, float dt, bool hitSkill, Btn button = Btn.Shoot, bool sprint = true)
    {
        if (P.Flight == FlightKind.Dunk)
        {
            if (hitSkill && !_pressed && P.FlightT + dt >= P.FlightDur - 0.01f) { inp.Pressed |= button; inp.Held |= button; _pressed = true; }
            return;
        }
        if (!P.HasBall || P.Airborne) return;
        var wp = Rim - new Vector3(S * 11f, 0, -3f); wp.Y = 0;
        if (!_reachedWaypoint && (P.GlobalPosition - wp).Length() < 1f) _reachedWaypoint = true;
        var rimFlat = Rim with { Y = 0 };
        RunTo(ref inp, P, _reachedWaypoint ? rimFlat : wp, sprint);
        if (_reachedWaypoint && P.CanDunk())
        {
            // modo print (--e3-shot): espera um pouco com as setinhas de "pode enterrar" na tela
            _readyT += dt;
            if (M.Cfg.ScreenshotPath == null || _readyT > 0.3f) { inp.Pressed |= Btn.Jump; _readyT = 0; }
        }
    }

    public PlayerInput Build(float dt)
    {
        var inp = new PlayerInput { AimYaw = P.Yaw };
        _t += dt;
        // lances livres: acerta o 1º (aperta na hora certa), erra o 2º (não aperta)
        if (M.Phase == MatchPhase.FreeThrow && M.FtShooter == P)
        {
            _sawFt = true;
            if (M.FtAttempt == 1 && M.FtChecking && !M.FtPressed && M.FtT + dt * 1.5f >= M.FtDur) { inp.Pressed |= Btn.Shoot; inp.Held |= Btn.Shoot; }
            return inp;
        }
        if (!M.CanAct) return inp;
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
            case 2: // enterrada ANDANDO (sem Shift) apertando ESPAÇO na hora certa: Espaço não vale → erra
            {
                if (!_setup) { if (!P.HasBall) M.Ball.GiveTo(P); _setup = true; }
                if (!SpaceIgnoredOk) { DunkRun(ref inp, dt, true, Btn.Jump, sprint: false); break; }
                if (!P.Airborne) Next();
                break;
            }
            case 3: // falta: boneco na frente da cesta bloqueia a enterrada
            {
                var e = Enemy;
                if (e == null) { Next(); break; }
                if (!_setup)
                {
                    if (!P.HasBall) M.Ball.GiveTo(P);
                    var guard = Rim - new Vector3(S * 3.5f, 0, 0); guard.Y = 0.05f;
                    e.Teleport(guard, ShotMath.YawOf(new Vector3(-S, 0, 0)));
                    var start = Rim - new Vector3(S * 12f, 0, 0.4f); start.Y = 0.05f;
                    P.Teleport(start, ShotMath.YawOf(new Vector3(S, 0, 0)));
                    P.Stamina = Tuning.I.StaminaMax;
                    _reachedWaypoint = true;
                    _setup = true;
                }
                if (_sawFt && M.Phase == MatchPhase.Playing)
                {
                    // errou o 2º lance: o jogo continua com a bola viva (ninguém com ela)
                    FtLiveOk = M.Ball.State != BallState.Held;
                    FtPointOk = M.Score[P.Team] == _scoreAtFoul + 1;
                    Next();
                    break;
                }
                if (_sawFt) break;
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
