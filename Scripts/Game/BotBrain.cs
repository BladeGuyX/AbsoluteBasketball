using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Bot (Etapa 4). Gera um PlayerInput por tick, como um jogador humano — roda só no host.
/// Ataque com a bola: infiltra, enterra (com skill check imperfeito), arremessa com erro calibrado, passa para quem está livre, dribla sob pressão.
/// Ataque sem a bola: se espalha em posições em volta da cesta. Defesa: marcação individual (quem está com a bola primeiro),
/// roubo, salto contra arremesso/enterrada. Bola solta/arremesso no ar: o mais perto de cada time vai no rebote.
/// Dificuldade (0 fácil, 1 normal, 2 difícil) multiplica erros e tempos de reação.
/// </summary>
public sealed class BotBrain
{
    readonly Match M;
    readonly Player P;
    readonly RandomNumberGenerator R = new();

    float _think, _hold;
    // arremesso em andamento
    bool _shooting; float _shotT, _shotHold, _yawErr, _distErr;
    // passe em andamento
    bool _passing; float _passT; Player _passTo; bool _passHigh; float _passYawErr;
    // infiltração / enterrada
    bool _driving; float _dunkPressAt = -1;
    Vector3 _sideStep;
    // defesa
    Player _threat; float _threatT; bool _threatDecided;
    float _stealCd;
    float _dribbleCd;

    public BotBrain(Match m, Player p, ulong seed) { M = m; P = p; R.Seed = seed; }

    Tuning T => Tuning.I;
    float Mult => M.Cfg.BotDifficulty switch { 0 => T.BotEasyMult, 2 => T.BotHardMult, _ => 1f };
    float Noise(float amp) => (R.Randf() + R.Randf() - 1f) * amp; // triangular em ±amp

    int AttackHoop => CourtGeo.AttackHoop(P.Team);
    int DefendHoop => 1 - AttackHoop;
    static Vector3 Flat(Vector3 v) => v with { Y = 0 };
    Vector3 RimFlat(int hoop) => Flat(M.Geo.RimCenter[hoop]);
    IEnumerable<Player> Mates => M.Players.Where(q => q != P && q.Team == P.Team);
    IEnumerable<Player> Opps => M.Players.Where(q => q.Team != P.Team);

    float Openness(Player q) => Opps.Select(o => Flat(o.GlobalPosition - q.GlobalPosition).Length()).DefaultIfEmpty(99f).Min();

    // =====================================================================
    public PlayerInput Build(float dt)
    {
        var inp = new PlayerInput { AimYaw = P.Yaw, AimPitch = ShotMath.PitchForDistance(T.ShotRangeMin) };
        if (_stealCd > 0) _stealCd -= dt;
        if (_dribbleCd > 0) _dribbleCd -= dt;
        if (!M.CanAct) { ResetActions(); _hold = 0; return inp; }

        if (P.Flight == FlightKind.Dunk) { DunkSkill(ref inp, dt); return inp; }
        _dunkPressAt = -1;

        if (P.HasBall) { _hold += dt; Carrier(ref inp, dt); return inp; }
        _hold = 0;
        ResetActions();

        var ball = M.Ball;
        if (ball.State == BallState.Held && ball.Holder != null)
        {
            if (ball.Holder.Team == P.Team) OffBall(ref inp, ball.Holder);
            else Defend(ref inp, dt, ball.Holder);
        }
        else if (ball.State is BallState.Pass or BallState.HighPass && ball.ThrowTeam == P.Team && ball.PassTarget == P)
            Chase(ref inp, PredictBall(0.6f));
        else LooseBall(ref inp, dt);

        Face(ref inp, M.Ball.GlobalPosition);
        BlockThreats(ref inp, dt);
        return inp;
    }

    void ResetActions()
    {
        _shooting = false; _passing = false; _driving = false; _think = 0;
    }

    // =====================================================================
    // Com a bola

    void Carrier(ref PlayerInput inp, float dt)
    {
        var rim = RimFlat(AttackHoop);
        var me = Flat(P.GlobalPosition);
        float d = (rim - me).Length();
        float open = Openness(P);

        if (_shooting) { ContinueShot(ref inp, dt); return; }
        if (_passing) { ContinuePass(ref inp, dt); return; }
        if (P.IsDribbling) { Steer(ref inp, DriveGoal(rim, me, d), sprint: false); return; }

        if (_driving)
        {
            if (LaneBlocked(me, rim, 1.2f, 2.0f) || P.Stamina < T.StaminaMinToRun) _driving = false;
            else
            {
                Steer(ref inp, rim, sprint: true, avoid: false);
                if (P.CanDunk()) inp.Pressed |= Btn.Jump;
                return;
            }
        }

        _think -= dt;
        if (_think <= 0)
        {
            _think = 0.2f + R.Randf() * 0.15f;
            if (Decide(ref inp, rim, me, d, open)) return;
        }
        Steer(ref inp, DriveGoal(rim, me, d), sprint: d > ShootRange() + 1f && P.Stamina > T.StaminaMax * 0.45f);
    }

    float ShootRange() => M.Cfg.BotDifficulty switch { 0 => T.BotShootRange - 2f, 2 => T.BotShootRange + 1.5f, _ => T.BotShootRange };

    /// <summary>Escolhe a ação. true = já preencheu o input deste tick.</summary>
    bool Decide(ref PlayerInput inp, Vector3 rim, Vector3 me, float d, float open)
    {
        bool contested = open < 1.7f;
        // sob pressão: drible especial
        if (contested && P.DribbleCharges > 0 && _dribbleCd <= 0 && R.Randf() < 0.55f)
        {
            _dribbleCd = 2.5f;
            inp.Pressed |= Btn.Dribble;
            return true;
        }
        // infiltração para enterrar
        if (d <= T.DunkRange + 5f && !LaneBlocked(me, rim, 1.4f, 0f) && P.Stamina > T.StaminaMax * 0.35f && R.Randf() < T.BotDunkChance)
        {
            _driving = true;
            return false;
        }
        // passe para alguém melhor
        if (_hold > 0.6f && R.Randf() < T.BotPassChance)
        {
            var (to, high) = BestPass(me, rim, d, open);
            if (to != null) { StartPass(ref inp, to, high); return true; }
        }
        // arremesso
        bool impatient = _hold > T.BotMaxHoldTime;
        if (d <= ShootRange() && (open >= T.BotOpenDistance || impatient || d < 4f || R.Randf() < T.BotContestedShotChance))
        { StartShot(ref inp); return true; }
        if (impatient && d <= Mathf.Min(ShootRange() + 1.5f, T.ShotRangeMax - 0.5f)) { StartShot(ref inp); return true; }
        return false;
    }

    Vector3 DriveGoal(Vector3 rim, Vector3 me, float d)
    {
        var dir = (rim - me).Normalized();
        var goal = rim - dir * Mathf.Max(2.5f, Mathf.Min(d, ShootRange() - 2f));
        // defensor na frente: contorna pelo lado
        var def = Opps.OrderBy(o => Flat(o.GlobalPosition - me).Length()).FirstOrDefault();
        if (def != null)
        {
            var rel = Flat(def.GlobalPosition) - me;
            if (rel.Length() < 3f && rel.Normalized().Dot(dir) > 0.5f)
            {
                var perp = new Vector3(-dir.Z, 0, dir.X);
                float side = perp.Dot(rel) > 0 ? -1f : 1f;
                if (_sideStep == Vector3.Zero || R.Randf() < 0.02f) _sideStep = perp * side * 3f;
                goal = me + dir * 2f + _sideStep;
            }
            else _sideStep = Vector3.Zero;
        }
        return ClampCourt(goal);
    }

    bool LaneBlocked(Vector3 from, Vector3 to, float radius, float ignoreNearEnd)
    {
        var seg = to - from;
        float len = seg.Length();
        if (len < 0.1f) return false;
        var dir = seg / len;
        foreach (var o in Opps)
        {
            var rel = Flat(o.GlobalPosition) - from;
            float along = rel.Dot(dir);
            if (along < 0.3f || along > len - ignoreNearEnd) continue;
            if ((rel - dir * along).Length() < radius) return true;
        }
        return false;
    }

    // ---------- arremesso ----------
    void StartShot(ref PlayerInput inp)
    {
        _shooting = true;
        _shotT = 0;
        _shotHold = 0.25f + R.Randf() * 0.35f;
        _yawErr = Mathf.DegToRad(Noise(T.BotAimErrorDeg * Mult));
        _distErr = Noise(T.BotDistErrorM * Mult);
        AimShot(ref inp);
        inp.Pressed |= Btn.Shoot;
        inp.Held |= Btn.Shoot;
    }

    void AimShot(ref PlayerInput inp)
    {
        var rim = M.Geo.RimCenter[AttackHoop];
        float yaw = ShotMath.YawOf(Flat(rim - P.GlobalPosition));
        var rel = ShotMath.ReleasePos(P.GlobalPosition, yaw);
        float dist = new Vector2(rim.X - rel.X, rim.Z - rel.Z).Length();
        inp.AimYaw = yaw + _yawErr;
        inp.AimPitch = ShotMath.PitchForDistance(Mathf.Max(0.5f, dist + _distErr));
    }

    void ContinueShot(ref PlayerInput inp, float dt)
    {
        _shotT += dt;
        AimShot(ref inp);
        if (!P.ChargingShot && _shotT > 0.05f) { _shooting = false; return; }
        if (_shotT < _shotHold) inp.Held |= Btn.Shoot; // soltar = arremessa
    }

    // ---------- passe ----------
    (Player, bool) BestPass(Vector3 me, Vector3 rim, float myD, float myOpen)
    {
        Player best = null; bool bestHigh = false;
        float myScore = Mathf.Min(myOpen, 6f) - myD * 0.35f;
        float bestScore = myScore + 1.2f;
        foreach (var q in Mates)
        {
            if (q.Airborne || q.Stun > 0) continue;
            var qp = Flat(q.GlobalPosition);
            float dist = (qp - me).Length();
            if (dist < 3f || dist > 22f) continue;
            float qOpen = Openness(q);
            if (qOpen < T.BotOpenDistance * 0.75f) continue;
            bool high = false;
            if (!Passing.LaneClear(P, q, false))
            {
                if (dist > 13f || !Passing.LaneClear(P, q, true)) continue;
                high = true;
            }
            float s = Mathf.Min(qOpen, 6f) - (rim - qp).Length() * 0.35f + (q.IsLocalHuman ? 0.3f : 0f);
            if (s > bestScore) { bestScore = s; best = q; bestHigh = high; }
        }
        return (best, bestHigh);
    }

    void StartPass(ref PlayerInput inp, Player to, bool high)
    {
        _passing = true;
        _passT = 0;
        _passTo = to;
        _passHigh = high;
        _passYawErr = Mathf.DegToRad(Noise(T.BotAimErrorDeg * Mult * 0.4f));
        AimPass(ref inp);
        inp.Pressed |= Btn.Pass;
        inp.Held |= Btn.Pass;
    }

    void AimPass(ref PlayerInput inp)
    {
        if (_passTo == null) return;
        var me = Flat(P.GlobalPosition);
        float v = _passHigh ? T.AimHighPassSpeed : T.AimPassSpeed;
        float g = T.BallGravity;
        // antecipa o movimento de quem recebe
        var target = Flat(_passTo.GlobalPosition);
        for (int i = 0; i < 2; i++)
        {
            float D0 = (target - me).Length();
            float time = D0 / Mathf.Max(v * 0.8f, 1f);
            target = Flat(_passTo.GlobalPosition + _passTo.Velocity * time);
        }
        float D = (target - me).Length();
        float k = Mathf.Clamp(g * D / (v * v), 0, 1);
        float th = 0.5f * Mathf.Asin(k);
        if (_passHigh) th = Mathf.Pi / 2f - th;
        float lift = Mathf.DegToRad(_passHigh ? T.AimHighPassLiftDeg : T.AimPassLiftDeg);
        inp.AimYaw = ShotMath.YawOf(target - me) + _passYawErr;
        inp.AimPitch = th - lift;
    }

    void ContinuePass(ref PlayerInput inp, float dt)
    {
        _passT += dt;
        AimPass(ref inp);
        if (_passHigh && P.ChargingPass && !P.PassHigh) inp.Pressed |= Btn.Variation; // liga o passe alto durante a carga
        if (!P.ChargingPass && _passT > 0.05f) { _passing = false; return; }
        if (_passT < T.PassTapTime + 0.08f) inp.Held |= Btn.Pass; // segura além do toque = passe carregado
    }

    // ---------- enterrada: skill check ----------
    void DunkSkill(ref PlayerInput inp, float dt)
    {
        if (_dunkPressAt < 0) _dunkPressAt = P.FlightDur + Noise(T.BotDunkTimingError * Mult);
        // o aperto é registrado no tick seguinte (FlightT + dt): aperta no tick mais próximo do instante escolhido
        if (!P.DunkPressed && P.FlightT + dt * 1.5f >= _dunkPressAt) { inp.Pressed |= Btn.Shoot; inp.Held |= Btn.Shoot; }
        _driving = false;
    }

    // =====================================================================
    // Sem a bola

    /// <summary>Ataque sem a bola: posições espalhadas em volta da cesta, longe de quem está com a bola.</summary>
    void OffBall(ref PlayerInput inp, Player carrier)
    {
        var mates = M.Players.Where(q => q.Team == P.Team && q != carrier).OrderBy(q => q.Id).ToList();
        int idx = mates.IndexOf(P);
        var spot = OffenseSpot(idx, carrier);
        Steer(ref inp, spot, sprint: (Flat(P.GlobalPosition) - spot).Length() > 7f && P.Stamina > T.StaminaMax * 0.5f);
    }

    Vector3 OffenseSpot(int idx, Player carrier)
    {
        float[] angles = { 0f, 55f, -55f, 85f, -85f, 30f, -30f };
        float[] radii = { 7.5f, 8.5f, 8.5f, 6.5f, 6.5f, 10f, 10f };
        var rim = RimFlat(AttackHoop);
        var inward = new Vector3(-CourtGeo.HoopSign(AttackHoop), 0, 0);
        var cp = carrier != null ? Flat(carrier.GlobalPosition) : rim + inward * 30f;
        for (int k = 0; k < angles.Length; k++)
        {
            int i = (idx + k) % angles.Length;
            var spot = ClampCourt(rim + inward.Rotated(Vector3.Up, Mathf.DegToRad(angles[i])) * radii[i]);
            if ((spot - cp).Length() > 4f) return spot;
        }
        return ClampCourt(rim + inward * 9f);
    }

    /// <summary>Defesa: marcação individual (atribuição gulosa: quem tem a bola primeiro).</summary>
    void Defend(ref PlayerInput inp, float dt, Player carrier)
    {
        var target = Assignment(carrier);
        var ownRim = RimFlat(DefendHoop);
        if (target == null) { Steer(ref inp, ClampCourt(ownRim + (Flat(carrier.GlobalPosition) - ownRim).Normalized() * 5f), false); return; }
        var tp = Flat(target.GlobalPosition);
        float markDist = target == carrier ? T.BotMarkDistance : T.BotMarkDistance + 1.2f;
        var toRim = ownRim - tp;
        var spot = tp + (toRim.LengthSquared() > 0.01f ? toRim.Normalized() : Vector3.Zero) * Mathf.Min(markDist, toRim.Length() * 0.5f);
        float far = (Flat(P.GlobalPosition) - spot).Length();
        Steer(ref inp, spot, sprint: far > 3f && P.Stamina > T.StaminaMinToRun * 2f);

        // roubo
        if (target == carrier && _stealCd <= 0 && !P.Airborne && P.StealFail <= 0)
        {
            var rel = Flat(carrier.GlobalPosition - P.GlobalPosition);
            bool smartOk = carrier.CanBeStolen || (M.Cfg.BotDifficulty == 0 && R.Randf() < 0.3f);
            if (rel.Length() < T.StealReach + 0.5f && smartOk && R.Randf() < T.BotStealChancePerSec * dt / Mult)
            {
                var d = rel.Normalized();
                inp.Move = new Vector2(d.X, d.Z);
                inp.Pressed |= Btn.Steal;
                _stealCd = 2.0f;
            }
        }
    }

    Player Assignment(Player carrier)
    {
        var defenders = M.Players.Where(q => q.Team == P.Team && !q.IsLocalHuman).OrderBy(q => q.Id).ToList();
        var ownRim = RimFlat(DefendHoop);
        var attackers = Opps.OrderBy(o => o == carrier ? 0 : 1).ThenBy(o => (Flat(o.GlobalPosition) - ownRim).Length()).ToList();
        var free = new List<Player>(defenders);
        foreach (var a in attackers)
        {
            if (free.Count == 0) break;
            var d = free.OrderBy(q => (q.GlobalPosition - a.GlobalPosition).LengthSquared()).First();
            if (d == P) return a;
            free.Remove(d);
        }
        return null;
    }

    /// <summary>Bola solta ou em voo: o mais perto de cada time vai buscar; os outros se posicionam.</summary>
    void LooseBall(ref PlayerInput inp, float dt)
    {
        var target = PredictBall(0.5f);
        var team = M.Players.Where(q => q.Team == P.Team && !q.Airborne).OrderBy(q => (Flat(q.GlobalPosition) - target).Length()).ToList();
        int chasers = team.Count >= 4 ? 2 : 1;
        if (team.IndexOf(P) >= 0 && team.IndexOf(P) < chasers) { Chase(ref inp, target); return; }
        // os outros: quem arremessou/passou fica no ataque; o outro time volta para defender
        if (M.Ball.ThrowTeam == P.Team)
        {
            var mates = M.Players.Where(q => q.Team == P.Team).OrderBy(q => q.Id).ToList();
            Steer(ref inp, OffenseSpot(mates.IndexOf(P), null), false);
        }
        else
        {
            var ownRim = RimFlat(DefendHoop);
            var inward = new Vector3(-CourtGeo.HoopSign(DefendHoop), 0, 0);
            var mates = M.Players.Where(q => q.Team == P.Team).OrderBy(q => q.Id).ToList();
            float a = (mates.IndexOf(P) - (mates.Count - 1) / 2f) * 35f;
            Steer(ref inp, ClampCourt(ownRim + inward.Rotated(Vector3.Up, Mathf.DegToRad(a)) * 5.5f), false);
        }
    }

    void Chase(ref PlayerInput inp, Vector3 target) =>
        Steer(ref inp, target, sprint: (Flat(P.GlobalPosition) - target).Length() > 3f && P.Stamina > T.StaminaMinToRun * 2f, avoid: false);

    Vector3 PredictBall(float maxT)
    {
        var b = M.Ball;
        var p = b.GlobalPosition; var v = b.Vel;
        // tempo até a bola descer à altura de pegar (~1,5 m)
        float g = T.BallGravity;
        float h = p.Y - 1.5f;
        float tFall = h > 0 ? (v.Y + Mathf.Sqrt(Mathf.Max(v.Y * v.Y + 2f * g * h, 0))) / g : 0f;
        float tt = Mathf.Min(Mathf.Max(tFall, 0.1f), Mathf.Max(maxT, tFall));
        return ClampCourt(Flat(p + v * Mathf.Min(tt, 1.5f)));
    }

    // ---------- salto contra arremesso / enterrada ----------
    void BlockThreats(ref PlayerInput inp, float dt)
    {
        if (P.Airborne || P.LandLock > 0 || P.Stun > 0 || P.StealFail > 0) return;
        var threat = Opps.Where(o => o.ChargingShot || o.Flight == FlightKind.Dunk)
            .OrderBy(o => (o.GlobalPosition - P.GlobalPosition).LengthSquared()).FirstOrDefault();
        float range = threat == null ? 0 : threat.Flight == FlightKind.Dunk ? 4.5f : 3.2f;
        if (threat == null || Flat(threat.GlobalPosition - P.GlobalPosition).Length() > range)
        { _threat = null; return; }
        if (threat != _threat) { _threat = threat; _threatT = 0; _threatDecided = false; }
        _threatT += dt;
        if (_threatDecided || _threatT < T.BotReactionTime * Mult) return;
        _threatDecided = true;
        if (R.Randf() < T.BotBlockChance / Mathf.Max(Mult, 0.5f) * 0.9f)
        {
            var to = Flat(threat.GlobalPosition - P.GlobalPosition);
            if (to.LengthSquared() > 0.01f) { var d = to.Normalized(); inp.Move = new Vector2(d.X, d.Z) * 0.5f; }
            inp.Pressed |= Btn.Jump;
        }
    }

    // =====================================================================
    // Movimento

    void Steer(ref PlayerInput inp, Vector3 target, bool sprint, bool avoid = true)
    {
        var me = Flat(P.GlobalPosition);
        var to = Flat(target) - me;
        float dist = to.Length();
        Vector3 dir = dist > 0.35f ? to / dist : Vector3.Zero;
        if (dist > 0.35f && dist < 1.2f) dir *= dist / 1.2f; // chega devagar
        if (avoid)
        {
            foreach (var q in M.Players)
            {
                if (q == P) continue;
                var rel = me - Flat(q.GlobalPosition);
                float l = rel.Length();
                if (l > 0.01f && l < 1.6f) dir += rel / l * (1.6f - l) * (q.Team == P.Team ? 0.9f : 0.4f);
            }
        }
        if (dir.LengthSquared() > 1f) dir = dir.Normalized();
        inp.Move = new Vector2(dir.X, dir.Z);
        if (sprint && dir.LengthSquared() > 0.25f) inp.Held |= Btn.Sprint;
    }

    void Face(ref PlayerInput inp, Vector3 at)
    {
        var to = Flat(at - P.GlobalPosition);
        if (to.LengthSquared() > 0.01f) inp.AimYaw = ShotMath.YawOf(to);
    }

    Vector3 ClampCourt(Vector3 p)
    {
        var g = M.Geo;
        return new Vector3(Mathf.Clamp(p.X, -g.HalfL + 1f, g.HalfL - 1f), 0, Mathf.Clamp(p.Z, -g.HalfW + 1f, g.HalfW - 1f));
    }
}
