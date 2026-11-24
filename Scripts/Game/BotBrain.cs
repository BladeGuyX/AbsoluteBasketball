using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Bot (Etapa 4). Gera um PlayerInput por tick, como um jogador humano — roda só no host.
/// Ataque com a bola: infiltra, enterra (com skill check imperfeito), arremessa com erro calibrado, passa para quem está livre, dribla sob pressão.
/// Ataque sem a bola: procura espaços livres em volta da cesta (troca de lugar, faz cortes, se mexe parado).
/// Defesa: marcação individual (quem está com a bola primeiro), roubo; contra arremesso se coloca NA trajetória
/// (lê o plano do arremesso) e pula quando a bola sai — só se o salto alcançar a bola. Sem marcar: zona.
/// Bola solta/arremesso no ar: o mais perto de cada time vai no rebote. Lances livres: skill check com erro.
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
    bool _blockTry; float _blockErr; float _relT = -1;
    float _stealCd;
    float _dribbleCd;
    // sem a bola: posição escolhida (troca de tempos em tempos) + balanço parado
    Vector3 _roam; float _roamT; bool _cut; float _sway;
    // lance livre
    float _ftPressAt = -1; int _ftAttempt;

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
        _sway += dt;
        _roamT -= dt;
        if (M.Phase == MatchPhase.FreeThrow)
        {
            if (M.FtShooter == P) FreeThrowSkill(ref inp, dt);
            ResetActions(); _hold = 0; _threat = null;
            return inp;
        }
        if (!M.CanAct) { ResetActions(); _hold = 0; return inp; }

        if (P.Flight == FlightKind.Dunk) { DunkSkill(ref inp, dt); return inp; }
        _dunkPressAt = -1;

        if (P.HasBall) { _hold += dt; Carrier(ref inp, dt); return inp; }
        _hold = 0;
        ResetActions();

        var ball = M.Ball;
        if (ball.State == BallState.Held && ball.Holder != null)
        {
            if (ball.Holder.Team == P.Team) OffBall(ref inp, dt, ball.Holder);
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
            if (LaneBlocked(me, rim, 1.2f, 2.0f)) _driving = false;
            else
            {
                // não precisa correr para enterrar (v0.7): corre se tiver estamina
                Steer(ref inp, rim, sprint: P.Stamina > T.StaminaMinToRun, avoid: false);
                if (P.DunkReadyVisual) inp.Pressed |= Btn.Jump;
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
        if (d <= T.DunkRange + 5f && !LaneBlocked(me, rim, 1.4f, 0f) && R.Randf() < T.BotDunkChance)
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

    // ---------- lance livre: mesmo skill check, com o mesmo erro da enterrada ----------
    void FreeThrowSkill(ref PlayerInput inp, float dt)
    {
        if (M.FtAttempt != _ftAttempt || M.FtT < -dt * 2f) { _ftAttempt = M.FtAttempt; _ftPressAt = -1; }
        if (!M.FtChecking || M.FtPressed || M.FtT < -dt * 2f) return;
        if (_ftPressAt < 0) _ftPressAt = M.FtDur + Noise(T.BotDunkTimingError * Mult);
        // o Match soma dt antes de ler o aperto: aperta no tick mais próximo do instante escolhido
        if (M.FtT + dt * 1.5f >= _ftPressAt && M.FtT + dt >= 0) { inp.Pressed |= Btn.Shoot; inp.Held |= Btn.Shoot; }
    }

    // =====================================================================
    // Sem a bola

    /// <summary>
    /// Ataque sem a bola: escolhe um espaço livre em volta da cesta (longe de defensores, aliados e de quem tem a bola),
    /// troca de lugar de tempos em tempos, às vezes corta para a cesta, e se mexe um pouco quando chega.
    /// </summary>
    void OffBall(ref PlayerInput inp, float dt, Player carrier)
    {
        var me = Flat(P.GlobalPosition);
        var cp = carrier != null ? Flat(carrier.GlobalPosition) : (Vector3?)null;
        bool bad = _roam == Vector3.Zero
            || (cp.HasValue && (cp.Value - _roam).Length() < 3.5f)
            || Mates.Any(q => q != carrier && (Flat(q.GlobalPosition) - _roam).Length() < 2.2f && (Flat(q.GlobalPosition) - me).Length() > 1f);
        if (_roamT <= 0 || bad) PickRoam(carrier);
        Roam(ref inp);
    }

    void PickRoam(Player carrier)
    {
        var rim = RimFlat(AttackHoop);
        var inward = new Vector3(-CourtGeo.HoopSign(AttackHoop), 0, 0);
        var me = Flat(P.GlobalPosition);
        bool cut = carrier != null && R.Randf() < 0.2f;
        float maxR = Mathf.Max(5.5f, Mathf.Min(ShootRange() + 0.5f, T.ShotRangeMax - 1f));
        float best = float.MinValue; var bestP = me;
        for (int i = 0; i < 12; i++)
        {
            float ang = Mathf.DegToRad(R.RandfRange(-88f, 88f));
            float rad = cut ? R.RandfRange(2.0f, 3.5f) : R.RandfRange(4.5f, maxR);
            var c = ClampCourt(rim + inward.Rotated(Vector3.Up, ang) * rad);
            float open = Mathf.Min(Opps.Select(o => (Flat(o.GlobalPosition) - c).Length()).DefaultIfEmpty(9f).Min(), 6f);
            float mate = Mathf.Min(Mates.Where(q => q != carrier).Select(q => (Flat(q.GlobalPosition) - c).Length()).DefaultIfEmpty(9f).Min(), 6f);
            float car = carrier != null ? (Flat(carrier.GlobalPosition) - c).Length() : 8f;
            float travel = (c - me).Length();
            float score = open + mate * 0.8f + Mathf.Min(car, 8f) * 0.3f - Mathf.Abs(travel - 5f) * 0.25f + R.Randf() * 1.5f;
            if (car < 3.5f) score -= 10f;
            if (score > best) { best = score; bestP = c; }
        }
        _roam = bestP;
        _cut = cut;
        _roamT = cut ? R.RandfRange(1.2f, 2.0f) : T.BotRoamInterval * R.RandfRange(0.6f, 1.4f);
    }

    /// <summary>Vai até _roam; chegando, fica se mexendo de leve (passos laterais) em vez de parar duro.</summary>
    void Roam(ref PlayerInput inp)
    {
        var me = Flat(P.GlobalPosition);
        float d = (me - _roam).Length();
        if (d > 0.9f)
        {
            Steer(ref inp, _roam, sprint: (d > 7f && P.Stamina > T.StaminaMax * 0.5f) || (_cut && d > 1.5f && P.Stamina > T.StaminaMinToRun));
            return;
        }
        var toBall = Flat(M.Ball.GlobalPosition) - me;
        var perp = toBall.LengthSquared() > 0.01f ? new Vector3(-toBall.Z, 0, toBall.X).Normalized() : Vector3.Right;
        float k = Mathf.Sin(_sway * 2.3f + P.Id * 1.7f) * 0.3f + Mathf.Sin(_sway * 0.9f + P.Id) * 0.12f;
        var mv = perp * k;
        inp.Move = new Vector2(mv.X, mv.Z);
    }

    /// <summary>Defesa: marcação individual (atribuição gulosa: quem tem a bola primeiro).</summary>
    void Defend(ref PlayerInput inp, float dt, Player carrier)
    {
        var target = Assignment(carrier);
        var ownRim = RimFlat(DefendHoop);
        if (target == null) { Zone(ref inp, carrier); return; }
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

    /// <summary>Defensor sem marcação: zona entre a bola e a própria cesta, deslocado para o lado, acompanhando a bola.</summary>
    void Zone(ref PlayerInput inp, Player carrier)
    {
        var ownRim = RimFlat(DefendHoop);
        var toBall = Flat(carrier.GlobalPosition) - ownRim;
        var dir = toBall.LengthSquared() > 0.01f ? toBall.Normalized() : new Vector3(-CourtGeo.HoopSign(DefendHoop), 0, 0);
        var perp = new Vector3(-dir.Z, 0, dir.X);
        float side = P.Id % 2 == 0 ? 1f : -1f;
        float dist = Mathf.Clamp(toBall.Length() * 0.45f, 3f, 6.5f);
        _roam = ClampCourt(ownRim + dir * dist + perp * side * 2.2f);
        _roamT = 0.5f;
        _cut = false;
        Roam(ref inp);
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
            if (_roamT <= 0 || _roam == Vector3.Zero) PickRoam(null);
            Roam(ref inp);
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
        if (P.Airborne || P.LandLock > 0 || P.Stun > 0 || P.StealFail > 0) { _threat = null; _relT = -1; return; }
        float Dist(Player o) => Flat(o.GlobalPosition - P.GlobalPosition).Length();

        // enterrada: pula na direção de quem voa (encostar = falta)
        var dunker = Opps.Where(o => o.Flight == FlightKind.Dunk).OrderBy(Dist).FirstOrDefault();
        if (dunker != null && Dist(dunker) <= 4.5f)
        {
            if (dunker != _threat) { _threat = dunker; _threatT = 0; _threatDecided = false; _relT = -1; }
            _threatT += dt;
            if (_threatDecided || _threatT < T.BotReactionTime * Mult) return;
            _threatDecided = true;
            if (R.Randf() < T.BotBlockChance / Mathf.Max(Mult, 0.5f) * 0.9f)
            {
                var to = Flat(dunker.GlobalPosition - P.GlobalPosition);
                if (to.LengthSquared() > 0.01f) { var d = to.Normalized(); inp.Move = new Vector2(d.X, d.Z) * 0.5f; }
                inp.Pressed |= Btn.Jump;
            }
            return;
        }

        // arremesso já saiu da mão de quem eu estava contestando: pula se o salto alcança a bola
        var ball = M.Ball;
        if (_threat != null && !_threat.ChargingShot)
        {
            bool released = ball.State == BallState.Shot && ball.LastThrower == _threat && ball.FlightTime < 0.3f;
            if (!released || !_blockTry) { _threat = null; _relT = -1; return; }
            if (_relT < 0) _relT = 0;
            _relT += dt;
            if (_relT < T.BotBlockReaction * Mult) return;
            // o salto herda o embalo: escolhe entre plantar os pés ou pular na direção da bola, o que alcançar
            inp.Move = Vector2.Zero;
            inp.Held &= ~Btn.Sprint;
            bool reach = false;
            foreach (var m in JumpOptions(ball.GlobalPosition, ball.Vel))
            {
                if (!JumpCanReach(ball.GlobalPosition, ball.Vel, m)) continue;
                inp.Move = new Vector2(m.X, m.Z);
                reach = true;
                break;
            }
            if (reach || (M.Cfg.BotDifficulty == 0 && R.Randf() < 0.25f)) inp.Pressed |= Btn.Jump;
            _threat = null; _relT = -1;
            return;
        }

        // carregando arremesso perto: decide se contesta e vai para a frente da bola, na trajetória
        var shooter = Opps.Where(o => o.ChargingShot).OrderBy(Dist).FirstOrDefault();
        if (shooter == null || Dist(shooter) > 5f) { _threat = null; _relT = -1; return; }
        if (shooter != _threat)
        {
            _threat = shooter; _threatT = 0; _relT = -1;
            _blockTry = R.Randf() < T.BotBlockChance / Mathf.Max(Mult, 0.5f);
            _blockErr = Noise(T.BotBlockPosError * Mult);
        }
        _threatT += dt;
        if (!_blockTry || _threatT < T.BotReactionTime * Mult * 0.5f) return;
        if (!shooter.GetShotPlan(out var plan)) return;
        var dir = Flat(plan.Vel);
        if (dir.LengthSquared() < 0.01f) return;
        dir = dir.Normalized();
        var perp = new Vector3(-dir.Z, 0, dir.X);
        var spot = Flat(shooter.GlobalPosition) + dir * 1.05f + perp * _blockErr;
        float far = (Flat(P.GlobalPosition) - spot).Length();
        Steer(ref inp, spot, sprint: far > 1.5f && P.Stamina > T.StaminaMinToRun, avoid: false);
        inp.AimYaw = ShotMath.YawOf(-dir);
    }

    /// <summary>Direções de salto a testar: parado e na direção de pontos do caminho da bola.</summary>
    IEnumerable<Vector3> JumpOptions(Vector3 bp, Vector3 bv)
    {
        yield return Vector3.Zero;
        var me = Flat(P.GlobalPosition);
        foreach (float tau in new[] { 0.08f, 0.14f, 0.2f })
        {
            var to = Flat(bp + bv * tau) - me;
            if (to.LengthSquared() > 0.01f) yield return to.Normalized();
        }
    }

    /// <summary>
    /// Se eu pular agora (salto rápido) com este direcional, meus braços encontram a bola? Mesma regra do Match.CheckBlocks
    /// e mesmo embalo do Player.HandleJump (direcional × andar × JumpCarry, ou a velocidade atual × JumpCarry se for maior).
    /// </summary>
    bool JumpCanReach(Vector3 bp, Vector3 bv, Vector3 move)
    {
        var me = P.GlobalPosition;
        var h = move * T.WalkSpeed * T.JumpCarry;
        var cur = Flat(P.Velocity);
        if (cur.Length() > h.Length()) h = cur * T.JumpCarry;
        float vy = T.JumpVelMin, g = T.PlayerGravity, gb = T.BallGravity;
        float rad = T.BlockRadius + T.BallRadius;
        const float step = 1f / 60f;
        for (float tau = step; tau <= 0.45f; tau += step)
        {
            var b = bp + bv * tau + Vector3.Down * (0.5f * gb * tau * tau);
            float feet = me.Y + Mathf.Max(0, vy * tau - 0.5f * g * tau * tau);
            var pp = me + h * tau;
            float hd = new Vector2(b.X - pp.X, b.Z - pp.Z).Length();
            float ry = b.Y - feet;
            if (hd <= rad && ry >= 0.8f && ry <= T.BlockReach + T.BallRadius) return true;
        }
        return false;
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
