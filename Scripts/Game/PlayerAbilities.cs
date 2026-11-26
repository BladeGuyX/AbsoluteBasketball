using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// v0.26: habilidades (teclas 1, 2, 3 / setinhas ←, ↑, →) e ultimate (4 / ↓) de um jogador.
/// Roda na autoridade (host/offline) dentro do <see cref="Player.ServerTick"/>; o estado vai para os clientes no snapshot
/// (<see cref="NetData"/>) só para a HUD e os efeitos. As recargas NÃO voltam no início de um round; tudo fica parado
/// durante as cutscenes (os jogadores não são simulados).
/// </summary>
public sealed class PlayerAbilities
{
    readonly Player P;
    public PlayerAbilities(Player p) { P = p; }

    Match M => P.M;
    int Char => P.CharacterId;
    Tuning T => Tuning.I;

    // ---- estado (replicado) ----
    public readonly float[] Cd = new float[3];   // recarga restante de cada habilidade (s)
    public float Ult;                             // carga da ultimate (0–1)
    public float EyeT;                            // King 1: Olho do Rei esperando o próximo passe/arremesso
    public bool EyeArmed;                         // King 1: gasto neste passe/arremesso que está carregando
    public float PresenceT;                       // King 2: Presença Implacável
    public bool PerfectDribble;                   // King 3: o drible atual é o Drible Perfeito
    public float JinxFlash;                       // arremesso afetado pela Presença: borda vermelha na tela deste jogador
    public int BetTarget; public float BetT;      // Luck 2: adversário marcado e tempo restante
    public bool DoubleNext;                       // v0.38: alvo do "Não me Decepciona" que não fez cesta a tempo — a próxima cesta dele vale o dobro
    public float DashT;                           // Luck 3: impulso
    public float ModeT, ModeBonus;                // Luck ult: modo apostador
    public int ModeWins, ModeGambles;
    public bool SkyJump;                          // Bolt 3: no ar do Arranha-Céu
    public float WallT;                           // Iron 1: Muralha de Aço de pé (s restantes)
    public float BonkT;                           // Iron 2: animação da Pancada (s restantes)
    public float ChargeT;                         // Iron 3: tempo desde o começo da Investida (0 = sem investida)
    public float TiredT;                          // Iron 3: parado no fim da Investida (cansado; bateu na parede = Stun)
    public float UltT;                            // Iron ult: Indestrutível (s restantes)
    public bool BlockCatch;                       // Iron ult: pegou a bola no bloqueio (imune a roubo até poder driblar)
    public float RealmT;                          // One 1: dentro do próprio Realm (s restantes)
    public float AbductT;                         // abduzido por um One (s restantes no Realm; só anda e corre)
    public float CurseT;                          // amaldiçoado por um One (s restantes)
    // (One ult: Domínio das Sombras usa UltT)
    /// <summary>v0.41: no Realm (sozinho ou abduzido): atravessa os jogadores, não interage com eles nem com a bola.</summary>
    public bool Phased => RealmT > 0 || AbductT > 0;
    public bool Domain => UltT > 0 && Char == Characters.One;
    /// <summary>One mais rápido: no Realm (até abduzido) ou com o Domínio das Sombras dele ativo.</summary>
    public bool RealmSpeed => Char == Characters.One && (RealmT > 0 || AbductT > 0 || UltT > 0);
    public bool Charging => ChargeT > 0;
    public bool Indestructible => UltT > 0 && Char == Characters.Iron;
    readonly System.Collections.Generic.HashSet<Player> _chargeHits = new();

    public static Btn SlotBtn(int slot) => slot switch { 0 => Btn.Ab1, 1 => Btn.Ab2, 2 => Btn.Ab3, _ => Btn.Ult };

    public bool Enabled => M != null && M.AbilitiesOn && !P.IsDummy;
    public bool Ready(int slot) => slot < 3 ? Cd[slot] <= 0 : (Ult >= 0.999f && UltT <= 0) || (Char == Characters.Luck && ModeT > 0);
    public float CdFrac(int slot) => slot < 3 ? Mathf.Clamp(Cd[slot] / Mathf.Max(Characters.Cooldown(Char, slot), 0.01f), 0, 1) : 0;

    /// <summary>Bônus instantâneo na barra da ultimate (bloqueio, passe, roubo, cesta). Não enche com a ult ativa.</summary>
    public void AddUlt(float v)
    {
        if (!Enabled || ModeT > 0 || UltT > 0 || v <= 0) return;
        Ult = Mathf.Min(1f, Ult + v);
    }

    /// <summary>Início de round: efeitos momentâneos acabam (recargas e carga da ult continuam).</summary>
    public void OnRoundReset()
    {
        EyeArmed = false; PerfectDribble = false; DashT = 0; SkyJump = false; BonkT = 0; TiredT = 0; BlockCatch = false; EndCharge();
        if (RealmT > 0) { RealmT = 0; if (M != null) StartCd(0); }
        AbductT = 0;
    }

    // =====================================================================
    public void Tick(PlayerInput inp, float dt)
    {
        // v0.39: no modo Indestrutível as recargas correm IronUltCdSpeedup mais rápido
        float cdRate = Indestructible ? 1f + Mathf.Max(0f, T.IronUltCdSpeedup) : 1f;
        // v0.41: Domínio das Sombras de um One adversário = recargas (e a barra da ultimate) mais lentas
        float slow = M != null && M.DomainAgainst(P) != null ? Mathf.Clamp(1f - T.OneUltEnemyCdSlow, 0.05f, 1f) : 1f;
        cdRate *= slow;
        for (int i = 0; i < 3; i++) if (Cd[i] > 0) Cd[i] = Mathf.Max(0, Cd[i] - dt * cdRate);
        if (EyeT > 0) EyeT -= dt;
        if (PresenceT > 0) PresenceT -= dt;
        if (JinxFlash > 0) JinxFlash -= dt;
        if (BetT > 0)
        {
            BetT -= dt;
            if (BetT <= 0)
            {
                // v0.38: o alvo não fez a cesta nos segundos da aposta → a próxima cesta dele vale o dobro (para o time dele)
                var tgt = M?.Players.FirstOrDefault(q => q.Id == BetTarget);
                if (tgt != null && tgt.Team != P.Team)
                {
                    tgt.Ab.DoubleNext = true;
                    Sfx.Play3D(M, "fx_betx2", tgt.GlobalPosition + Vector3.Up * 2.4f);
                    M.LogEvent("BET_DOUBLE", P, tgt);
                }
                BetTarget = 0;
            }
        }
        if (DashT > 0) DashT -= dt;
        if (ModeT > 0) { ModeT -= dt; if (ModeT <= 0) EndMode(); }
        if (PerfectDribble && !P.IsDribbling) PerfectDribble = false;
        if (WallT > 0) WallT = Mathf.Max(0, WallT - dt);
        if (BonkT > 0) BonkT = Mathf.Max(0, BonkT - dt);
        if (TiredT > 0) TiredT = Mathf.Max(0, TiredT - dt);
        if (UltT > 0) UltT = Mathf.Max(0, UltT - dt);
        if (CurseT > 0) CurseT = Mathf.Max(0, CurseT - dt);
        if (AbductT > 0) { AbductT = Mathf.Max(0, AbductT - dt); if (AbductT <= 0 && RealmT <= 0) P.PushOutOfPlayers(); }
        if (RealmT > 0) { RealmT = Mathf.Max(0, RealmT - dt); if (RealmT <= 0) ExitRealm(); }
        if (BlockCatch && (!P.HasBall || (!P.Airborne && P.LandLock <= 0))) BlockCatch = false;
        if (!Enabled) return;
        if (M.Phase == MatchPhase.Playing && ModeT <= 0 && UltT <= 0) Ult = Mathf.Min(1f, Ult + dt * slow / Mathf.Max(T.UltChargeTime, 1f));

        bool can = M.CanAct && P.Stun <= 0 && TiredT <= 0 && !Charging && P.Flight == FlightKind.None && P.RimHang <= 0 && !P.InScriptedMove && AbductT <= 0;
        if (!can) return;
        // v0.41: no Realm, apertar o Realm de novo sai dele
        if (Char == Characters.One && RealmT > 0 && inp.WasPressed(Btn.Ab1)) { ExitRealm(); inp.Pressed &= ~Btn.Ab1; }
        for (int s = 0; s < 3; s++)
            if (inp.WasPressed(SlotBtn(s)) && !Characters.NeedsTarget(Char, s)) TryUse(s, inp);
        if (inp.WasPressed(Btn.Ult) && !Characters.NeedsTarget(Char, 3)) TryUlt();
        if (inp.Target > 0) TryTarget(inp.TargetSlot, inp.Target);
    }

    void StartCd(int slot) => Cd[slot] = M.Cfg.TestMode && NoCd ? 0f : Characters.Cooldown(Char, slot);

    /// <summary>v0.39: Muralhas de Aço que a bola lançada por <paramref name="thrower"/> atravessa (bit = cesta): o Iron
    /// Indestrutível não é afetado por nenhuma habilidade adversária, nem pela muralha do outro time.</summary>
    public static int WallPassMask(Player thrower) =>
        thrower?.Ab != null && thrower.Ab.Indestructible ? 1 << CourtGeo.AttackHoop(thrower.Team) : 0;

    /// <summary>v0.32 (modo de teste): "Sem recarga" deste jogador (cada um liga o seu). Replicado.</summary>
    public bool NoCd;

    /// <summary>v0.27 (modo de teste): recargas zeradas e ultimate cheia.</summary>
    public void Refill() { Cd[0] = Cd[1] = Cd[2] = 0; if (UltT <= 0) Ult = 1f; }

    void TryUse(int slot, PlayerInput inp)
    {
        if (Cd[slot] > 0) return;
        bool ok = (Char, slot) switch
        {
            (Characters.King, 0) => KingEye(),
            (Characters.King, 1) => KingPresence(),
            (Characters.King, 2) => KingPerfectDribble(),
            (Characters.Luck, 0) => LuckRisky(),
            (Characters.Luck, 2) => LuckDash(inp.AimYaw),
            (Characters.Bolt, 0) => BoltDribble(inp.AimYaw),
            (Characters.Bolt, 1) => DoSwap(inp.AimYaw), // v0.27: troca na hora (o alvo fica com o anel do chão claro antes)
            (Characters.Bolt, 2) => BoltSky(),
            (Characters.Iron, 0) => IronWall(),
            (Characters.Iron, 1) => IronBonk(inp.AimYaw),
            (Characters.Iron, 2) => IronCharge(inp.AimYaw),
            (Characters.One, 0) => OneRealm(),
            (Characters.One, 1) => OneAbduct(inp.AimYaw),
            _ => false,
        };
        if (!ok) return;
        if (!(Char == Characters.One && slot == 0)) StartCd(slot); // o Realm começa a recarga só ao sair
        M.LogEvent("ABILITY", P);
    }

    void TryUlt()
    {
        if (Char == Characters.Luck) { LuckUlt(); return; }
        if (Ult < 0.999f) return;
        if (Char == Characters.Iron) { IronUlt(); return; }
        if (Char == Characters.One) { OneUlt(); return; }
        if (Char == Characters.King && CanStartCutscene() && M.StartCutscene(CutKind.KingUlt, P, null)) { Ult = 0; M.LogEvent("ULT", P); }
    }

    void TryTarget(int slot, int targetId)
    {
        var tgt = M.Players.FirstOrDefault(q => q.Id == targetId);
        if (tgt == null || tgt.Team == P.Team || Characters.Immune(tgt, P)) return;
        if (Char == Characters.Luck && slot == 1 && Cd[1] <= 0)
        {
            BetTarget = tgt.Id; BetT = T.LuckBetDuration; StartCd(1);
            Sfx.Play3D(M, "fx_bet", tgt.GlobalPosition + Vector3.Up * 2.4f);
            M.LogEvent("ABILITY", P, tgt);
        }
        else if (Char == Characters.One && slot == 2 && Cd[2] <= 0)
        {
            tgt.Ab.CurseT = T.OneCurseDuration; StartCd(2);
            Sfx.Play3D(M, "fx_curse", tgt.GlobalPosition + Vector3.Up * 2.3f);
            M.LogEvent("CURSE", P, tgt);
        }
        else if (Char == Characters.Bolt && slot == 3 && Ult >= 0.999f && CanStartCutscene() && M.StartCutscene(CutKind.BoltDuel, P, tgt))
        {
            Ult = 0; M.LogEvent("ULT", P, tgt);
        }
    }

    bool CanStartCutscene() => P.HasBall && M.Phase == MatchPhase.Playing && !P.Airborne && !P.ChargingShot && !P.ChargingPass;

    // =====================================================================
    // King

    bool KingEye()
    {
        EyeT = T.KingEyeDuration;
        Sfx.Play3D(M, "fx_eye", P.GlobalPosition + Vector3.Up * 1.9f);
        return true;
    }

    /// <summary>Chamado ao começar a carregar um passe ou arremesso: o Olho do Rei é gasto neste lance.</summary>
    public void ArmEyeIfActive()
    {
        if (EyeT <= 0) return;
        EyeT = 0;
        EyeArmed = true;
    }

    public float EyeMult => EyeArmed ? Mathf.Max(T.KingEyeRangeMult, 1f) : 1f;

    /// <summary>v0.31: alcance do arremesso com o Olho do Rei — multiplicado, mas no máximo ~meia quadra (KingEyeMaxCourtFrac).</summary>
    public static float EyeShotRange(Player p, float range)
    {
        float r = range * Mathf.Max(Tuning.I.KingEyeRangeMult, 1f);
        if (p.M?.Geo != null) r = Mathf.Min(r, p.M.Geo.Length * Tuning.I.KingEyeMaxCourtFrac);
        return Mathf.Max(r, range);
    }

    bool KingPresence()
    {
        PresenceT = T.KingPresenceDuration;
        Sfx.Play3D(M, "fx_presence", P.GlobalPosition + Vector3.Up);
        return true;
    }

    bool KingPerfectDribble()
    {
        if (!P.HasBall || P.Airborne || P.ChargingShot || P.ChargingPass) return false;
        P.StartDribble(free: true, extra: T.KingDribbleExtra);
        PerfectDribble = true;
        Sfx.Play3D(M, "fx_perfect", P.HeldBallPosition());
        return true;
    }

    // =====================================================================
    // Luck

    bool LuckRisky()
    {
        if (!P.HasBall || P.Flight != FlightKind.None) return false;
        int hoop = CourtGeo.AttackHoop(P.Team);
        if (P.GlobalPosition.X * CourtGeo.HoopSign(hoop) < 0) return false; // só no lado adversário
        bool miss = M.Rng.Randf() < T.LuckRiskyMissChance;
        P.ThrowDirectShot(miss ? T.LuckRiskyMissOffset : 0f);
        Sfx.Play3D(M, "fx_dice", P.GlobalPosition + Vector3.Up * 2f);
        return true;
    }

    bool LuckDash(float aimYaw)
    {
        if (P.Airborne || !P.IsOnFloor()) return false;
        P.CancelShot(false); P.CancelPass();
        if (M.Rng.Randf() < T.LuckDashChance)
        {
            P.Yaw = aimYaw;
            DashT = T.LuckDashTime;
            P.StealDash = 0;
            Sfx.Play3D(M, "fx_dash", P.GlobalPosition + Vector3.Up);
        }
        else
        {
            P.Stun = T.LuckTripStun;
            Sfx.Play3D(M, "fx_trip", P.GlobalPosition + Vector3.Up * 0.5f);
            M.LogEvent("TRIP", P);
        }
        return true;
    }

    /// <summary>No impulso (sem a bola): passou por quem tem a bola → rouba.</summary>
    public void CheckDashSteal()
    {
        if (DashT <= 0 || P.HasBall) return;
        var h = M.Ball.Holder;
        if (h == null || h.Team == P.Team || h.Airborne || h.Flight != FlightKind.None || Characters.Immune(h, P)) return;
        var rel = h.GlobalPosition - P.GlobalPosition; rel.Y = 0;
        if (rel.Length() > T.LuckDashStealReach) return;
        DashT = 0;
        if (h.Ab.PerfectDribble && h.IsDribbling && !Characters.Immune(P, h)) { h.Ab.PunishStealer(P); return; }
        h.CancelShot(false); h.CancelPass();
        M.Ball.GiveTo(P);
        M.OnSteal(P, h);
    }

    /// <summary>King 3: quem tentou roubar durante o Drible Perfeito cai atordoado.</summary>
    public void PunishStealer(Player thief)
    {
        thief.StealDash = 0;
        thief.Ab.DashT = 0;
        thief.Stun = T.KingDribbleStun;
        Sfx.Play3D(M, "fx_parry", M.Ball.GlobalPosition);
        Sfx.Play3D(M, "fx_perfect", M.Ball.GlobalPosition);
        M.LogEvent("PERFECT_PUNISH", P, thief);
    }

    void LuckUlt()
    {
        if (ModeT <= 0)
        {
            if (Ult < 0.999f) return;
            Ult = 0;
            ModeT = T.LuckModeDuration; ModeBonus = T.LuckModeBonus; ModeWins = 0; ModeGambles = 0;
            Sfx.Play3D(M, "fx_luckon", P.GlobalPosition + Vector3.Up);
            M.LogEvent("ULT", P);
            M.StartUltPose(P); // v0.35: pose curta para todo mundo ver (o tempo do modo não corre durante ela)
            return;
        }
        // aposta
        float lose = Mathf.Min(T.LuckGambleLoseChance + T.LuckGambleLoseStep * ModeGambles, T.LuckGambleLoseMax);
        ModeGambles++;
        if (M.Rng.Randf() < lose)
        {
            EndMode();
            P.Stun = T.LuckGambleStun;
            Sfx.Play3D(M, "fx_lucklose", P.GlobalPosition + Vector3.Up);
            M.LogEvent("GAMBLE_LOSE", P);
        }
        else
        {
            ModeWins++;
            ModeBonus = Mathf.Min(ModeBonus * 2f, T.LuckModeBonusMax);
            ModeT += T.LuckGambleExtraTime;
            Sfx.Play3D(M, "fx_luckwin", P.GlobalPosition + Vector3.Up);
            M.LogEvent("GAMBLE_WIN", P);
        }
    }

    void EndMode() { ModeT = 0; ModeBonus = 0; ModeWins = 0; ModeGambles = 0; }

    /// <summary>Pontos a mais da cesta do Luck no modo apostador.</summary>
    public int ModePoints => ModeT > 0 ? Mathf.RoundToInt(T.LuckModePoints) * (1 + ModeWins) : 0;

    // =====================================================================
    // Bolt

    /// <summary>Adversário mais perto do centro do olhar, a até BoltDribbleRange (Drible Relâmpago).</summary>
    public static Player DribbleCandidate(Player p, float aimYaw)
    {
        var t = Tuning.I;
        Player best = null; float bestA = Mathf.DegToRad(t.BoltDribbleAngle);
        foreach (var q in p.M.Players)
        {
            if (q.Team == p.Team || q.Flight != FlightKind.None || q.RimHang > 0 || Characters.Immune(q, p)) continue;
            var to = q.GlobalPosition - p.GlobalPosition; to.Y = 0;
            if (to.Length() > t.BoltDribbleRange || to.Length() < 0.05f) continue;
            float a = Mathf.Abs(ShotMath.WrapAngle(ShotMath.YawOf(to) - aimYaw));
            if (a <= bestA) { bestA = a; best = q; }
        }
        return best;
    }

    /// <summary>Jogador (aliado ou adversário) mais perto do centro do olhar (Swap). Usado também pelo destaque na tela.</summary>
    public static Player SwapCandidate(Player p, float aimYaw)
    {
        var t = Tuning.I;
        Player best = null; float bestA = Mathf.DegToRad(t.BoltSwapAngle);
        foreach (var q in p.M.Players)
        {
            if (q == p || q.Flight != FlightKind.None || q.RimHang > 0 || Characters.Immune(q, p)) continue;
            var to = q.GlobalPosition - p.GlobalPosition; to.Y = 0;
            if (to.Length() > t.BoltSwapRange || to.Length() < 0.3f) continue;
            float a = Mathf.Abs(ShotMath.WrapAngle(ShotMath.YawOf(to) - aimYaw));
            if (a <= bestA) { bestA = a; best = q; }
        }
        return best;
    }

    bool BoltDribble(float aimYaw)
    {
        if (!P.HasBall || P.Airborne || P.ChargingShot || P.ChargingPass) return false;
        var tgt = DribbleCandidate(P, aimYaw);
        if (tgt == null) return false;
        var from = P.GlobalPosition;
        var dir = (tgt.GlobalPosition - from) with { Y = 0 };
        dir = dir.LengthSquared() > 1e-4f ? dir.Normalized() : P.Forward;
        var to = tgt.GlobalPosition + dir * T.BoltDribbleAhead;
        to = M.Geo.ClampInside(to with { Y = from.Y }, 0.6f);
        P.Yaw = ShotMath.YawOf(dir);
        P.StartScriptedMove(from, to, T.BoltDribbleTime, 0f); // v0.27: reto, atravessa o adversário como um raio
        tgt.CancelShot(false); tgt.CancelPass();
        tgt.Stun = Mathf.Max(tgt.Stun, T.BoltDribbleStun);
        tgt.StealDash = 0;
        Sfx.Play3D(M, "fx_bolt", from + Vector3.Up);
        Sfx.Play3D(M, "fx_bolt", to + Vector3.Up);
        Sfx.Play3D(M, "fx_boltA", from + Vector3.Up * 1.1f); // rastro reto (ImpactFx junta A e B)
        Sfx.Play3D(M, "fx_boltB", to + Vector3.Up * 1.1f);
        M.LogEvent("BOLT_DRIBBLE", P, tgt);
        return true;
    }

    bool DoSwap(float aimYaw)
    {
        var q = SwapCandidate(P, aimYaw);
        if (q == null) return false;
        var a = P.GlobalPosition; var b = q.GlobalPosition;
        P.QueueRelocate(b);
        q.QueueRelocate(a);
        Sfx.Play3D(M, "fx_swap", a + Vector3.Up);
        Sfx.Play3D(M, "fx_swap", b + Vector3.Up);
        M.LogEvent("SWAP", P, q);
        return true;
    }

    bool BoltSky()
    {
        if (P.HasBall || P.Airborne || !P.IsOnFloor() || P.ChargingJump) return false;
        P.SkyJumpNow(T.BoltSkyJumpVel);
        SkyJump = true;
        foreach (var q in M.Players)
        {
            if (q.Team == P.Team || Characters.Immune(q, P)) continue;
            var d = (q.GlobalPosition - P.GlobalPosition) with { Y = 0 };
            if (d.Length() > T.BoltSkyStunRadius) continue;
            q.CancelShot(false); q.CancelPass();
            q.Stun = Mathf.Max(q.Stun, T.BoltSkyStun);
        }
        Sfx.Play3D(M, "fx_thunder", P.GlobalPosition with { Y = 0.05f });
        M.LogEvent("SKY", P);
        return true;
    }

    // =====================================================================
    // Iron (v0.37)

    bool IronWall()
    {
        WallT = T.IronWallDuration;
        var rim = M.Geo.RimCenter[CourtGeo.DefendHoop(P.Team)];
        float inward = -CourtGeo.HoopSign(CourtGeo.DefendHoop(P.Team));
        Sfx.Play3D(M, "fx_wall", new Vector3(rim.X + inward * M.Geo.ThreePt, 1.5f, 0));
        M.LogEvent("WALL", P);
        return true;
    }

    /// <summary>Adversário (sem a bola) a até IronBonkRange, o mais perto do centro do olhar (Pancada). Também usado pelo destaque na tela.</summary>
    public static Player BonkCandidate(Player p, float aimYaw)
    {
        var t = Tuning.I;
        Player best = null; float bestA = Mathf.DegToRad(t.IronBonkAngle);
        foreach (var q in p.M.Players)
        {
            if (q.Team == p.Team || q.HasBall || q.Flight != FlightKind.None || q.RimHang > 0 || Characters.Immune(q, p)) continue;
            var to = (q.GlobalPosition - p.GlobalPosition) with { Y = 0 };
            float d = to.Length();
            if (d > t.IronBonkRange) continue;
            float a = d < 0.05f ? 0f : Mathf.Abs(ShotMath.WrapAngle(ShotMath.YawOf(to) - aimYaw));
            if (a <= bestA) { bestA = a; best = q; }
        }
        return best;
    }

    bool IronBonk(float aimYaw)
    {
        if (P.Airborne) return false;
        var q = BonkCandidate(P, aimYaw);
        if (q == null) return false;
        if (!P.ChargingShot && !P.ChargingPass)
            P.Yaw = ShotMath.YawOf((q.GlobalPosition - P.GlobalPosition) with { Y = 0 });
        BonkT = 0.4f;
        q.CancelShot(false); q.CancelPass();
        q.StealDash = 0; q.Ab.DashT = 0;
        if (q.Ab.Charging) q.Ab.EndCharge();
        q.Stun = Mathf.Max(q.Stun, T.IronBonkStun);
        Sfx.Play3D(M, "fx_bonk", q.GlobalPosition + Vector3.Up * 2.1f);
        M.LogEvent("BONK", P, q);
        return true;
    }

    bool IronCharge(float aimYaw)
    {
        if (P.HasBall || P.Airborne || !P.IsOnFloor() || P.ChargingJump) return false;
        P.CancelShot(false); P.CancelPass();
        P.StealDash = 0;
        P.Yaw = aimYaw;
        ChargeT = 0.0001f;
        _chargeHits.Clear();
        P.CollisionMask = 1; // atravessa os jogadores (quem está no caminho é jogado para o lado)
        P.CollisionLayer = 0; // v0.39: e ninguém colide com ele (antes a física empurrava quem ele não atropela, como o Iron Indestrutível)
        Sfx.Play3D(M, "fx_charge", P.GlobalPosition + Vector3.Up);
        M.LogEvent("CHARGE", P);
        return true;
    }

    /// <summary>Teste (--smoke-abilities): começa a Investida direto (bonecos não usam habilidades).</summary>
    public bool TestCharge(float yaw) => IronCharge(yaw);

    /// <summary>Velocidade atual da Investida (começa devagar e acelera).</summary>
    public float ChargeSpeed => Mathf.Lerp(T.IronChargeSpeedStart, T.IronChargeSpeedMax,
        Mathf.Clamp(ChargeT / Mathf.Max(T.IronChargeDuration, 0.1f), 0, 1));

    /// <summary>Fim da Investida: sem efeito se não estava investindo. wall = bateu na parede (atordoado), senão cansado.</summary>
    public void EndCharge(bool? wall = null)
    {
        if (ChargeT <= 0) return;
        ChargeT = 0;
        _chargeHits.Clear();
        if (P == null) return;
        P.CollisionMask = 1 | 2;
        P.CollisionLayer = 2;
        if (wall == null) return;
        if (wall.Value)
        {
            P.Stun = Mathf.Max(P.Stun, T.IronChargeRecover);
            Sfx.Play3D(M, "fx_clang", P.GlobalPosition + Vector3.Up * 1.2f + P.Forward * 0.5f);
            M.LogEvent("CHARGE_WALL", P);
        }
        else
        {
            TiredT = T.IronChargeRecover;
            M.LogEvent("CHARGE_TIRED", P);
        }
    }

    /// <summary>Na Investida (Player.ServerTick, antes de mover): atropela quem está na frente (aliados também).</summary>
    public void ChargeHits()
    {
        var t = T;
        foreach (var q in M.Players)
        {
            if (q == P || _chargeHits.Contains(q) || q.Flight != FlightKind.None || q.RimHang > 0 || Characters.Immune(q, P)) continue;
            var rel = (q.GlobalPosition - P.GlobalPosition) with { Y = 0 };
            if (rel.Length() > t.IronChargeHitRadius || rel.Dot(P.Forward) < -0.15f) continue;
            if (q.GlobalPosition.Y - P.GlobalPosition.Y > 1.6f) continue; // muito alto no ar: passa por baixo
            _chargeHits.Add(q);
            float side = rel.Dot(P.Right) >= 0 ? 1f : -1f;
            var dest = M.Geo.ClampInside(q.GlobalPosition + P.Right * side * t.IronChargeKnock + P.Forward * 0.5f, 0.6f);
            if (q.HasBall)
            {
                var bp = M.Ball.GlobalPosition;
                q.CancelShot(false); q.CancelPass();
                M.Ball.Drop(bp, P.Forward * 2.5f + P.Right * side * 2.5f + Vector3.Up * 2.5f);
                q.BallLockoutTimer = 0.6f;
                M.LogEvent("CHARGE_DROP", P, q);
            }
            q.CancelShot(false); q.CancelPass();
            q.StealDash = 0; q.Ab.DashT = 0;
            if (q.Ab.Charging) q.Ab.EndCharge();
            q.Stun = Mathf.Max(q.Stun, t.IronChargeStun);
            q.StartKnockback(dest, 0.22f);
            Sfx.Play3D(M, "fx_ram", q.GlobalPosition + Vector3.Up);
            M.LogEvent("CHARGE_HIT", P, q);
        }
    }

    void IronUlt()
    {
        Ult = 0;
        UltT = T.IronUltDuration;
        Sfx.Play3D(M, "fx_ironon", P.GlobalPosition + Vector3.Up);
        M.LogEvent("ULT", P);
        M.StartUltPose(P); // cena: a bigorna cai na cabeça dele e quica (o tempo do modo não corre durante ela)
    }

    // =====================================================================
    // One (v0.41)

    bool OneRealm()
    {
        if (P.HasBall || Phased || M.AnyDomain || P.Airborne || !P.IsOnFloor()) return false; // v0.42: já no Realm/Domínio = não
        P.StealDash = 0; P.ChargingJump = false;
        RealmT = T.OneRealmDuration;
        Sfx.Play3D(M, "fx_realm", P.GlobalPosition + Vector3.Up);
        M.LogEvent("REALM", P);
        return true;
    }

    /// <summary>Sai do Realm na hora (apertou de novo, pulou, tentou roubar ou acabou o tempo). A recarga começa agora.</summary>
    public void ExitRealm()
    {
        RealmT = 0;
        if (M == null) return;
        StartCd(0);
        Sfx.Play3D(M, "fx_realm", P.GlobalPosition + Vector3.Up);
        M.LogEvent("REALM_OUT", P);
        if (!Phased) P.PushOutOfPlayers();
    }

    /// <summary>Jogador sem a bola (aliado ou adversário) a até OneAbductRange, o mais perto do centro do olhar (Abdução).</summary>
    public static Player AbductCandidate(Player p, float aimYaw)
    {
        var t = Tuning.I;
        Player best = null; float bestA = Mathf.DegToRad(t.OneAbductAngle);
        if (p.M.AnyDomain) return null; // v0.42: no Domínio todo mundo já está no Realm
        foreach (var q in p.M.Players)
        {
            if (q == p || q.HasBall || q.Phased || q.Flight != FlightKind.None || q.RimHang > 0 || Characters.Immune(q, p)) continue;
            var to = (q.GlobalPosition - p.GlobalPosition) with { Y = 0 };
            float d = to.Length();
            if (d > t.OneAbductRange) continue;
            float a = d < 0.05f ? 0f : Mathf.Abs(ShotMath.WrapAngle(ShotMath.YawOf(to) - aimYaw));
            if (a <= bestA) { bestA = a; best = q; }
        }
        return best;
    }

    bool OneAbduct(float aimYaw)
    {
        var q = AbductCandidate(P, aimYaw);
        if (q == null) return false;
        q.CancelShot(false); q.CancelPass();
        q.StealDash = 0; q.ChargingJump = false;
        q.Ab.DashT = 0;
        if (q.Ab.Charging) q.Ab.EndCharge();
        q.Ab.AbductT = T.OneAbductDuration;
        Sfx.Play3D(M, "fx_abduct", q.GlobalPosition + Vector3.Up);
        M.LogEvent("ABDUCT", P, q);
        return true;
    }

    /// <summary>Teste (--smoke-abilities): Realm (0) ou Abdução (1) direto (bonecos não usam habilidades).</summary>
    public bool TestOne(int slot, float yaw)
    {
        bool ok = slot == 0 ? OneRealm() : OneAbduct(yaw);
        if (ok && slot == 1) StartCd(1);
        return ok;
    }

    void OneUlt()
    {
        if (Phased || M.AnyDomain) return; // v0.42: não dá estando no Realm nem com outro Domínio ativo
        Ult = 0;
        UltT = T.OneUltDuration;
        Sfx.Play3D(M, "fx_domain", P.GlobalPosition + Vector3.Up);
        M.LogEvent("ULT", P);
        M.StartUltPose(P); // cena: a quadra é engolida pelas sombras (o tempo do domínio não corre durante ela)
    }

    // =====================================================================
    // Rede

    public struct NetData
    {
        public float Cd0, Cd1, Cd2, Ult, EyeT, PresenceT, JinxFlash, BetT, DashT, ModeT, ModeBonus, WallT, BonkT, ChargeT, TiredT, UltT, RealmT, AbductT, CurseT;
        public byte Flags, BetTarget, ModeWins;
    }

    public NetData Capture() => new()
    {
        Cd0 = Cd[0], Cd1 = Cd[1], Cd2 = Cd[2], Ult = Ult, EyeT = EyeT, PresenceT = PresenceT, JinxFlash = JinxFlash, BetT = BetT,
        DashT = DashT, ModeT = ModeT, ModeBonus = ModeBonus, BetTarget = (byte)Mathf.Clamp(BetTarget, 0, 255), ModeWins = (byte)Mathf.Clamp(ModeWins, 0, 255),
        WallT = WallT, BonkT = BonkT, ChargeT = ChargeT, TiredT = TiredT, UltT = UltT, RealmT = RealmT, AbductT = AbductT, CurseT = CurseT,
        Flags = (byte)((EyeArmed ? 1 : 0) | (PerfectDribble ? 2 : 0) | (SkyJump ? 4 : 0) | (NoCd ? 8 : 0) | (BlockCatch ? 16 : 0) | (DoubleNext ? 32 : 0)),
    };

    public static void Write(NetWriter w, in NetData d)
    {
        w.F(d.Cd0); w.F(d.Cd1); w.F(d.Cd2); w.F(d.Ult); w.F(d.EyeT); w.F(d.PresenceT); w.F(d.JinxFlash); w.F(d.BetT); w.F(d.DashT);
        w.F(d.ModeT); w.F(d.ModeBonus); w.U8(d.Flags); w.U8(d.BetTarget); w.U8(d.ModeWins);
        w.F(d.WallT); w.F(d.BonkT); w.F(d.ChargeT); w.F(d.TiredT); w.F(d.UltT); w.F(d.RealmT); w.F(d.AbductT); w.F(d.CurseT);
    }

    public static NetData Read(NetReader r) => new()
    {
        Cd0 = r.F(), Cd1 = r.F(), Cd2 = r.F(), Ult = r.F(), EyeT = r.F(), PresenceT = r.F(), JinxFlash = r.F(), BetT = r.F(), DashT = r.F(),
        ModeT = r.F(), ModeBonus = r.F(), Flags = r.U8(), BetTarget = r.U8(), ModeWins = r.U8(),
        WallT = r.F(), BonkT = r.F(), ChargeT = r.F(), TiredT = r.F(), UltT = r.F(), RealmT = r.F(), AbductT = r.F(), CurseT = r.F(),
    };

    public void Apply(in NetData d)
    {
        Cd[0] = d.Cd0; Cd[1] = d.Cd1; Cd[2] = d.Cd2; Ult = d.Ult; EyeT = d.EyeT; PresenceT = d.PresenceT; JinxFlash = d.JinxFlash;
        BetT = d.BetT; DashT = d.DashT; ModeT = d.ModeT; ModeBonus = d.ModeBonus; BetTarget = d.BetTarget; ModeWins = d.ModeWins;
        EyeArmed = (d.Flags & 1) != 0; PerfectDribble = (d.Flags & 2) != 0; SkyJump = (d.Flags & 4) != 0; NoCd = (d.Flags & 8) != 0;
        WallT = d.WallT; BonkT = d.BonkT; ChargeT = d.ChargeT; TiredT = d.TiredT; UltT = d.UltT; BlockCatch = (d.Flags & 16) != 0;
        DoubleNext = (d.Flags & 32) != 0;
        RealmT = d.RealmT; AbductT = d.AbductT; CurseT = d.CurseT;
    }
}
