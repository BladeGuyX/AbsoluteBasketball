using Godot;

namespace AB;

/// <summary>
/// Piloto automático simples: busca a bola, vai até um ponto e arremessa com a força calculada.
/// Usado pelo teste automático (--smoke-test). Vai virar a base do "ataque" dos bots (Etapa 4).
/// </summary>
public sealed class AutoPilot
{
    readonly Match M;
    readonly Player P;
    float _holdTarget;
    int _spotIdx;
    public int Shots { get; private set; }

    public AutoPilot(Match m, Player p) { M = m; P = p; }

    public PlayerInput Build(float dt)
    {
        var inp = new PlayerInput { AimYaw = P.Yaw };
        if (!M.CanAct) return inp;

        if (!P.HasBall)
        {
            MoveTo(ref inp, M.Ball.GlobalPosition);
            return inp;
        }

        int hoop = CourtGeo.AttackHoop(P.Team);
        var rim = M.Geo.RimCenter[hoop];
        float[] dists = { 2.2f, 5.5f, M.Geo.ThreePt + 1.0f };
        float[] angles = { 0f, 35f, -30f };
        float dist = dists[_spotIdx % dists.Length];
        float ang = Mathf.DegToRad(angles[_spotIdx % angles.Length]);
        float s = CourtGeo.HoopSign(hoop);
        var spot = new Vector3(rim.X - s * dist * Mathf.Cos(ang), 0, dist * Mathf.Sin(ang));

        var toSpot = spot - P.GlobalPosition; toSpot.Y = 0;
        if (!P.ChargingShot && toSpot.Length() > 0.3f)
        {
            MoveTo(ref inp, spot);
            return inp;
        }

        var toRim = rim - P.GlobalPosition; toRim.Y = 0;
        float yaw = ShotMath.YawOf(toRim);
        inp.AimYaw = yaw;

        var rel = ShotMath.ReleasePos(P.GlobalPosition, yaw);
        float rimDist = new Vector2(rim.X - rel.X, rim.Z - rel.Z).Length();
        inp.AimPitch = ShotMath.PitchForDistance(rimDist);

        if (!P.ChargingShot)
        {
            if (new Vector2(P.Velocity.X, P.Velocity.Z).Length() > 0.15f) return inp; // espera parar
            // segurar não muda a distância: só mira (câmera) e solta (v0.39: depois do tempo mínimo mirando)
            _holdTarget = Mathf.Max(0.25f, Tuning.I.ShotMinChargeTime + 0.05f);
            if (rimDist > Tuning.I.ShotRangeMax) { _spotIdx++; return inp; }
            inp.Pressed |= Btn.Shoot;
            inp.Held |= Btn.Shoot;
            return inp;
        }

        if (P.ShotCharge + dt < _holdTarget) inp.Held |= Btn.Shoot;
        else
        {
            Shots++;
            var pl = ShotMath.Plan(P.GlobalPosition, P.Team, yaw, inp.AimPitch, M.Geo);
            var sp = pl.Pos; var sv = pl.Vel; string res = "nada";
            for (int i = 0; i < 400; i++) { var h = BallPhys.Simulate(ref sp, ref sv, 1f / 60f, M.Geo, hoop); if (h.ScoredHoop >= 0) { res = "CESTA"; break; } if ((h.Rim || h.Board) && res == "nada") { res = (h.Rim ? "aro" : "tabela") + $"@{sp - M.Geo.RimCenter[hoop]}"; } if (h.Floor) break; }
            GD.Print($"[SMOKE]   plano d={pl.Distance:0.00} rimDist={rimDist:0.00} carga={P.ShotCharge + dt:0.000} alvo={_holdTarget:0.000} vel={pl.Vel} sim={res}");
            GD.Print($"[SMOKE] arremesso #{Shots} de {dist:0.0}m (3pt={M.Geo.IsThree(P.GlobalPosition, hoop)})");
            _spotIdx++;
        }
        return inp;
    }

    void MoveTo(ref PlayerInput inp, Vector3 target)
    {
        var to = target - P.GlobalPosition; to.Y = 0;
        if (to.Length() < 0.05f) return;
        var d = to.Length() > 1f ? to.Normalized() : to;
        inp.Move = new Vector2(d.X, d.Z);
    }
}
