using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Teste automático do bloqueio dos bots (--smoke-block): o jogador local (piloto) para a ~8 m da cesta com um bot
/// adversário (Normal) marcando, carrega o arremesso por um tempo e solta. Conta tocos e pulos inúteis.
/// </summary>
public sealed class BlockScenario
{
    readonly Match M;
    readonly Player P;
    readonly RandomNumberGenerator R = new() { Seed = 4242 };
    public const int TrialCount = 16;
    public int Trials, Blocks, Jumps, UselessJumps;
    public const int DribbleCount = 10;
    public int Dribbles, Baited, StolenDuringDribble;
    public bool OnTopTested, SlidOff;
    bool _baitThis;
    public bool Done;
    int _stage;          // 0 = posicionar, 1 = esperando, 2 = carregando, 3 = bola no ar
    float _t, _hold;
    bool _blockedThis, _jumpedThis;

    public BlockScenario(Match m, Player p) { M = m; P = p; }

    Player Bot => M.Players.FirstOrDefault(q => q.Team != P.Team);
    int Hoop => CourtGeo.AttackHoop(P.Team);

    public void OnEvent(string kind, Player a, Player b)
    {
        if (kind == "BLOCK" && a == Bot && _stage == 3) _blockedThis = true;
        if (kind == "JUMP" && a == Bot && _stage >= 2) _jumpedThis = true;
        if (kind == "STEAL_TRY" && a == Bot && _stage == 11) _baitThis = true;
        if (kind == "STEAL" && a == Bot && _stage == 11) StolenDuringDribble++;
    }

    public PlayerInput Build(float dt)
    {
        var inp = new PlayerInput { AimYaw = P.Yaw };
        if (Done || !M.CanAct) return inp;
        _t += dt;
        var rim = M.Geo.RimCenter[Hoop];
        var bot = Bot;
        switch (_stage)
        {
            case 0:
            {
                if (bot == null) { Done = true; break; }
                if (Trials >= TrialCount) { _stage = 10; _t = 0; break; }
                var inward = new Vector3(-CourtGeo.HoopSign(Hoop), 0, 0);
                var dir = inward.Rotated(Vector3.Up, Mathf.DegToRad(R.RandfRange(-50f, 50f)));
                var pos = (rim with { Y = 0 }) + dir * 8f + Vector3.Up * 0.05f;
                P.Teleport(pos, ShotMath.YawOf(-dir));
                var perp = new Vector3(-dir.Z, 0, dir.X);
                var bp = pos - dir * 2.4f + perp * R.RandfRange(-1.2f, 1.2f);
                bot.Teleport(bp, ShotMath.YawOf(dir));
                M.Ball.GiveTo(P);
                _hold = R.RandfRange(0.7f, 1.6f);
                _blockedThis = false; _jumpedThis = false;
                _stage = 1; _t = 0;
                break;
            }
            case 1:
                if (!P.HasBall) { _stage = 0; break; } // roubou antes: repete
                if (_t > 0.35f) { _stage = 2; _t = 0; }
                break;
            case 2:
            {
                if (!P.HasBall && !P.ChargingShot) { _stage = 3; _t = 0; break; }
                var to = (rim - P.GlobalPosition) with { Y = 0 };
                inp.AimYaw = ShotMath.YawOf(to);
                inp.AimPitch = ShotMath.PitchForDistance(to.Length() - 0.25f);
                if (!P.ChargingShot) { inp.Pressed |= Btn.Shoot; inp.Held |= Btn.Shoot; }
                else if (P.ShotCharge < _hold) inp.Held |= Btn.Shoot;
                break;
            }
            case 3:
                if (_t > 1.6f)
                {
                    Trials++;
                    if (_blockedThis) Blocks++;
                    if (_jumpedThis) { Jumps++; if (!_blockedThis) UselessJumps++; }
                    GD.Print($"[BLOCK] tentativa {Trials}: toco={_blockedThis} pulou={_jumpedThis}");
                    _stage = 0; _t = 0;
                }
                break;
            case 10: // fase do drible: perto do bot, dribla e vê se ele cai na finta
            {
                if (Dribbles >= DribbleCount) { _stage = 20; _t = 0; break; }
                var inward = new Vector3(-CourtGeo.HoopSign(Hoop), 0, 0);
                var pos = (rim with { Y = 0 }) + inward * 9f + Vector3.Up * 0.05f;
                P.Teleport(pos, ShotMath.YawOf(-inward));
                P.ResetForRound();
                bot.Teleport(pos - inward * 1.6f, ShotMath.YawOf(inward));
                bot.ResetForRound();
                M.Ball.GiveTo(P);
                _baitThis = false;
                _stage = 11; _t = 0;
                break;
            }
            case 20: // solta o jogador em cima da cabeça do bot (parado): tem que escorregar para o chão
            {
                var bp = bot.GlobalPosition;
                P.Teleport(bp + Vector3.Up * 2.1f, P.Yaw);
                P.Airborne = true;
                _stage = 21; _t = 0;
                break;
            }
            case 21:
                if (_t > 1.5f)
                {
                    OnTopTested = true;
                    SlidOff = P.GlobalPosition.Y < 0.4f;
                    GD.Print($"[BLOCK] em cima do bot: altura depois de 1,5 s = {P.GlobalPosition.Y:0.00} -> {(SlidOff ? "escorregou" : "FICOU EM CIMA")}");
                    Done = true;
                }
                break;
            case 11:
                if (_t > 0.4f && _t < 0.45f && P.HasBall && P.DribbleCharges > 0) inp.Pressed |= Btn.Dribble;
                if (_t > 1.8f)
                {
                    Dribbles++;
                    if (_baitThis) Baited++;
                    GD.Print($"[BLOCK] drible {Dribbles}: bot deu o bote={_baitThis}");
                    _stage = 10; _t = 0;
                }
                break;
        }
        return inp;
    }
}
