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
    public float DashT;                           // Luck 3: impulso
    public float ModeT, ModeBonus;                // Luck ult: modo apostador
    public int ModeWins, ModeGambles;
    public bool SkyJump;                          // Bolt 3: no ar do Arranha-Céu

    public static Btn SlotBtn(int slot) => slot switch { 0 => Btn.Ab1, 1 => Btn.Ab2, 2 => Btn.Ab3, _ => Btn.Ult };

    public bool Enabled => M != null && M.AbilitiesOn && !P.IsDummy;
    public bool Ready(int slot) => slot < 3 ? Cd[slot] <= 0 : Ult >= 0.999f || (Char == Characters.Luck && ModeT > 0);
    public float CdFrac(int slot) => slot < 3 ? Mathf.Clamp(Cd[slot] / Mathf.Max(Characters.Cooldown(Char, slot), 0.01f), 0, 1) : 0;

    /// <summary>Bônus instantâneo na barra da ultimate (bloqueio, passe, roubo, cesta). Não enche com a ult ativa.</summary>
    public void AddUlt(float v)
    {
        if (!Enabled || ModeT > 0 || v <= 0) return;
        Ult = Mathf.Min(1f, Ult + v);
    }

    /// <summary>Início de round: efeitos momentâneos acabam (recargas e carga da ult continuam).</summary>
    public void OnRoundReset()
    {
        EyeArmed = false; PerfectDribble = false; DashT = 0; SkyJump = false;
    }

    // =====================================================================
    public void Tick(PlayerInput inp, float dt)
    {
        for (int i = 0; i < 3; i++) if (Cd[i] > 0) Cd[i] = Mathf.Max(0, Cd[i] - dt);
        if (EyeT > 0) EyeT -= dt;
        if (PresenceT > 0) PresenceT -= dt;
        if (JinxFlash > 0) JinxFlash -= dt;
        if (BetT > 0) { BetT -= dt; if (BetT <= 0) BetTarget = 0; }
        if (DashT > 0) DashT -= dt;
        if (ModeT > 0) { ModeT -= dt; if (ModeT <= 0) EndMode(); }
        if (PerfectDribble && !P.IsDribbling) PerfectDribble = false;
        if (!Enabled) return;
        if (M.Phase == MatchPhase.Playing && ModeT <= 0) Ult = Mathf.Min(1f, Ult + dt / Mathf.Max(T.UltChargeTime, 1f));

        bool can = M.CanAct && P.Stun <= 0 && P.Flight == FlightKind.None && P.RimHang <= 0 && !P.InScriptedMove;
        if (!can) return;
        for (int s = 0; s < 3; s++)
            if (inp.WasPressed(SlotBtn(s)) && !Characters.NeedsTarget(Char, s)) TryUse(s, inp);
        if (inp.WasPressed(Btn.Ult) && !Characters.NeedsTarget(Char, 3)) TryUlt();
        if (inp.Target > 0) TryTarget(inp.TargetSlot, inp.Target);
    }

    void StartCd(int slot) => Cd[slot] = M.Cfg.TestMode && M.NoCooldowns ? 0f : Characters.Cooldown(Char, slot);

    /// <summary>v0.27 (modo de teste): recargas zeradas e ultimate cheia.</summary>
    public void Refill() { Cd[0] = Cd[1] = Cd[2] = 0; Ult = 1f; }

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
            _ => false,
        };
        if (!ok) return;
        StartCd(slot);
        M.LogEvent("ABILITY", P);
    }

    void TryUlt()
    {
        if (Char == Characters.Luck) { LuckUlt(); return; }
        if (Ult < 0.999f) return;
        if (Char == Characters.King && CanStartCutscene() && M.StartCutscene(CutKind.KingUlt, P, null)) { Ult = 0; M.LogEvent("ULT", P); }
    }

    void TryTarget(int slot, int targetId)
    {
        var tgt = M.Players.FirstOrDefault(q => q.Id == targetId);
        if (tgt == null || tgt.Team == P.Team) return;
        if (Char == Characters.Luck && slot == 1 && Cd[1] <= 0)
        {
            BetTarget = tgt.Id; BetT = T.LuckBetDuration; StartCd(1);
            Sfx.Play3D(M, "fx_bet", tgt.GlobalPosition + Vector3.Up * 2.4f);
            M.LogEvent("ABILITY", P, tgt);
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
        if (h == null || h.Team == P.Team || h.Airborne || h.Flight != FlightKind.None) return;
        var rel = h.GlobalPosition - P.GlobalPosition; rel.Y = 0;
        if (rel.Length() > T.LuckDashStealReach) return;
        DashT = 0;
        if (h.Ab.PerfectDribble && h.IsDribbling) { h.Ab.PunishStealer(P); return; }
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
            if (q.Team == p.Team || q.Flight != FlightKind.None || q.RimHang > 0) continue;
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
            if (q == p || q.Flight != FlightKind.None || q.RimHang > 0) continue;
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
            if (q.Team == P.Team) continue;
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
    // Rede

    public struct NetData
    {
        public float Cd0, Cd1, Cd2, Ult, EyeT, PresenceT, JinxFlash, BetT, DashT, ModeT, ModeBonus;
        public byte Flags, BetTarget, ModeWins;
    }

    public NetData Capture() => new()
    {
        Cd0 = Cd[0], Cd1 = Cd[1], Cd2 = Cd[2], Ult = Ult, EyeT = EyeT, PresenceT = PresenceT, JinxFlash = JinxFlash, BetT = BetT,
        DashT = DashT, ModeT = ModeT, ModeBonus = ModeBonus, BetTarget = (byte)Mathf.Clamp(BetTarget, 0, 255), ModeWins = (byte)Mathf.Clamp(ModeWins, 0, 255),
        Flags = (byte)((EyeArmed ? 1 : 0) | (PerfectDribble ? 2 : 0) | (SkyJump ? 4 : 0)),
    };

    public static void Write(NetWriter w, in NetData d)
    {
        w.F(d.Cd0); w.F(d.Cd1); w.F(d.Cd2); w.F(d.Ult); w.F(d.EyeT); w.F(d.PresenceT); w.F(d.JinxFlash); w.F(d.BetT); w.F(d.DashT);
        w.F(d.ModeT); w.F(d.ModeBonus); w.U8(d.Flags); w.U8(d.BetTarget); w.U8(d.ModeWins);
    }

    public static NetData Read(NetReader r) => new()
    {
        Cd0 = r.F(), Cd1 = r.F(), Cd2 = r.F(), Ult = r.F(), EyeT = r.F(), PresenceT = r.F(), JinxFlash = r.F(), BetT = r.F(), DashT = r.F(),
        ModeT = r.F(), ModeBonus = r.F(), Flags = r.U8(), BetTarget = r.U8(), ModeWins = r.U8(),
    };

    public void Apply(in NetData d)
    {
        Cd[0] = d.Cd0; Cd[1] = d.Cd1; Cd[2] = d.Cd2; Ult = d.Ult; EyeT = d.EyeT; PresenceT = d.PresenceT; JinxFlash = d.JinxFlash;
        BetT = d.BetT; DashT = d.DashT; ModeT = d.ModeT; ModeBonus = d.ModeBonus; BetTarget = d.BetTarget; ModeWins = d.ModeWins;
        EyeArmed = (d.Flags & 1) != 0; PerfectDribble = (d.Flags & 2) != 0; SkyJump = (d.Flags & 4) != 0;
    }
}
