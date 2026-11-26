using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// v0.26: roteiro do --smoke-abilities. Troca o personagem do jogador local (King, Luck, Bolt) e testa cada habilidade e as
/// duas cutscenes de ultimate. Os bonecos ficam parados (sem cérebro); arremessos/roubos deles são forçados pelo roteiro.
/// Cada passo é escrito como uma sequência (iterador) que roda um tick por vez e produz o input do jogador local.
/// </summary>
public sealed class AbilityScenario
{
    readonly Match M;
    readonly Player P;
    readonly IEnumerator<int> _run;
    PlayerInput _inp;
    public bool Done;
    public readonly List<(string Name, bool Ok)> Results = new();
    readonly Dictionary<string, int> _ev = new();

    public AbilityScenario(Match m, Player p) { M = m; P = p; _run = Run().GetEnumerator(); }

    public void OnEvent(string kind, Player a, Player b) => _ev[kind] = _ev.GetValueOrDefault(kind) + 1;
    int Ev(string k) => _ev.GetValueOrDefault(k);

    public PlayerInput Build(float dt)
    {
        _inp = new PlayerInput { AimYaw = P.Yaw, AimPitch = ShotMath.PitchForDistance(Tuning.I.ShotRangeMin), SkillT = -1 };
        if (!Done && !_run.MoveNext()) Done = true;
        return _inp;
    }

    void Rec(string name, bool ok)
    {
        Results.Add((name, ok));
        GD.Print($"[ABIL] {name}: {(ok ? "ok" : "FALHOU")}");
    }

    Player Ally => M.Players.First(q => q != P && q.Team == P.Team);
    List<Player> Enemies => M.Players.Where(q => q.Team != P.Team).ToList();
    Vector3 Rim(int team) => M.Geo.RimCenter[CourtGeo.AttackHoop(team)];

    void Place(Player q, float x, float z, Vector3? faceTo = null)
    {
        var pos = new Vector3(x, 0.05f, z);
        float yaw = faceTo.HasValue ? ShotMath.YawOf((faceTo.Value - pos) with { Y = 0 }) : q.Yaw;
        q.Teleport(pos, yaw, setHome: false);
    }

    void Reset(int character)
    {
        P.CharacterId = character;
        P.UpdateStats();
        for (int i = 0; i < 3; i++) P.Ab.Cd[i] = 0;
        P.Ab.EyeT = 0; P.Ab.PresenceT = 0; P.Ab.BetT = 0; P.Ab.BetTarget = 0; P.Ab.ModeT = 0; P.Ab.ModeBonus = 0;
        foreach (var q in M.Players) { q.Stun = 0; q.StealFail = 0; }
        P.Stamina = P.St.StaminaMax;
    }

    IEnumerable<int> Wait(float secs) { for (float t = 0; t < secs; t += 1f / 60f) yield return 0; }
    IEnumerable<int> WaitPlaying() { int n = 0; while (M.Phase != MatchPhase.Playing && n++ < 600) yield return 0; }

    /// <summary>Aperta o arremesso no momento certo (ok) ou bem errado (!ok) do skill check da cutscene.</summary>
    void CutPress(SkillCheckState c, bool ok)
    {
        if (c == null || !c.Active || c.Pressed) return;
        float at = ok ? c.Dur - 1f / 120f : c.Dur * 0.35f;
        if (c.T >= 0 && c.T + 1f / 60f >= at) { _inp.Pressed |= Btn.Shoot; _inp.Held |= Btn.Shoot; }
    }

    IEnumerable<int> Run()
    {
        var t = Tuning.I;
        foreach (var _ in Wait(0.5f)) yield return 0;
        foreach (var _ in WaitPlaying()) yield return 0;
        var e0 = Enemies[0]; var e1 = Enemies[1]; var ally = Ally;
        float s = CourtGeo.HoopSign(CourtGeo.AttackHoop(P.Team)); // +1: o time do jogador ataca para +X

        // ---------------- King 1: Olho do Rei (passe rápido além do alcance normal) ----------------
        Reset(Characters.King);
        Place(P, -s * 17f, 0, null); Place(ally, s * 17f, 0, null);
        Place(e0, 0, M.Geo.HalfW - 1f); Place(e1, 0, -M.Geo.HalfW + 1f);
        M.Ball.GiveTo(P);
        foreach (var _ in Wait(0.2f)) yield return 0;
        int pass0 = Ev("PASS");
        _inp.Pressed |= Btn.Pass; _inp.Held |= Btn.Pass; yield return 0; // toque sem o olho: longe demais, não passa
        foreach (var _ in Wait(0.3f)) yield return 0;
        bool noPass = Ev("PASS") == pass0 && M.Ball.Holder == P;
        _inp.Pressed |= Btn.Ab1; yield return 0;
        bool eyeOn = P.Ab.EyeT > 0;
        foreach (var _ in Wait(0.1f)) yield return 0;
        _inp.Pressed |= Btn.Pass; _inp.Held |= Btn.Pass; yield return 0;
        yield return 0; // soltou: o passe sai neste tick
        bool trail = M.Ball.Aura == 1;
        int n = 0; while (M.Ball.Holder != ally && n++ < 240) yield return 0;
        Rec("olho_do_rei", noPass && eyeOn && trail && M.Ball.Holder == ally && P.Ab.Cd[0] > 0);

        // ---------------- King 2: Presença Implacável (arremesso certo do adversário vira erro) ----------------
        Reset(Characters.King);
        var rim1 = Rim(e0.Team);
        var spot = rim1 - new Vector3(CourtGeo.HoopSign(CourtGeo.AttackHoop(e0.Team)) * 4f, 0, 0); spot.Y = 0;
        Place(e0, spot.X, spot.Z, rim1); Place(P, spot.X, spot.Z + 9f, null); Place(e1, -spot.X, 5f); Place(ally, -spot.X, -5f);
        M.Ball.GiveTo(e0);
        foreach (var _ in Wait(0.3f)) yield return 0;
        int sc0 = M.Score[e0.Team];
        e0.TestShot(); // sem a Presença (King longe): entra
        n = 0; while (M.Score[e0.Team] == sc0 && n++ < 240) yield return 0;
        bool control = M.Score[e0.Team] > sc0;
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.King);
        Place(e0, spot.X, spot.Z, rim1); Place(P, spot.X, spot.Z + 5f, null);
        M.Ball.GiveTo(e0);
        _inp.Pressed |= Btn.Ab2; yield return 0;
        foreach (var _ in Wait(0.2f)) yield return 0;
        int j0 = Ev("JINX"); sc0 = M.Score[e0.Team];
        e0.TestShot();
        foreach (var _ in Wait(2.5f)) yield return 0;
        Rec("presenca_implacavel", control && Ev("JINX") > j0 && M.Score[e0.Team] == sc0);

        // ---------------- v0.27: Presença Implacável na enterrada (sem skill check, erra) ----------------
        foreach (var _ in WaitPlaying()) yield return 0;
        float sgn = CourtGeo.HoopSign(CourtGeo.AttackHoop(e0.Team));
        Place(e0, rim1.X - sgn * 1.8f, rim1.Z, rim1); Place(P, rim1.X - sgn * 1.8f, rim1.Z + 4f, null);
        e0.Stun = 0;
        M.Ball.GiveTo(e0);
        P.Ab.PresenceT = Mathf.Max(P.Ab.PresenceT, 2f);
        foreach (var _ in Wait(0.25f)) yield return 0;
        int j1 = Ev("JINX"), df0 = Ev("DUNK_FAIL"); sc0 = M.Score[e0.Team];
        bool dunkStarted = e0.TestDunk();
        bool dunkJinxed = e0.DunkJinx;
        bool noCheck = true;
        n = 0; while (e0.Flight != FlightKind.None && n++ < 120) { if (e0.SkillFlash > 0) noCheck = false; yield return 0; }
        foreach (var _ in Wait(1.5f)) { if (e0.SkillFlash > 0) noCheck = false; yield return 0; }
        Rec("presenca_enterrada", dunkStarted && dunkJinxed && noCheck && Ev("JINX") > j1 && Ev("DUNK_FAIL") > df0 && M.Score[e0.Team] == sc0);

        // ---------------- King 3: Drible Perfeito (quem tenta roubar cai) ----------------
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.King);
        Place(P, 0, 0, new Vector3(s, 0, 0)); Place(e0, s * 1.0f, 0, Vector3.Zero);
        M.Ball.GiveTo(P);
        int charges = P.DribbleCharges;
        _inp.Pressed |= Btn.Ab3; yield return 0;
        bool longer = P.DribbleActive > t.DribbleDuration && P.Ab.PerfectDribble && P.DribbleCharges == charges;
        int pp0 = Ev("PERFECT_PUNISH");
        e0.TestSteal(P);
        foreach (var _ in Wait(0.3f)) yield return 0;
        Rec("drible_perfeito", longer && Ev("PERFECT_PUNISH") > pp0 && e0.Stun > t.KingDribbleStun - 0.6f && M.Ball.Holder == P);

        // ---------------- King ult: cutscene (acerta tudo = 7 pontos; erra = bola do adversário) ----------------
        foreach (var r in KingUlt(true)) yield return r;
        foreach (var r in KingUlt(false)) yield return r;
        foreach (var r in KingUlt(false, true)) yield return r;

        // ---------------- Luck 1: Arremesso Arriscado ----------------
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Luck);
        float keepMiss = t.LuckRiskyMissChance;
        t.LuckRiskyMissChance = 0f;
        var rimMine = Rim(P.Team);
        Place(P, rimMine.X - s * 7f, 1.5f, rimMine); Place(e0, -s * 10f, 6f); Place(e1, -s * 10f, -6f); Place(ally, -s * 5f, 0);
        M.Ball.GiveTo(P);
        foreach (var _ in Wait(0.1f)) yield return 0;
        int mySc = M.Score[P.Team];
        _inp.Pressed |= Btn.Ab1; yield return 0;
        n = 0; while (M.Score[P.Team] == mySc && n++ < 240) yield return 0;
        bool riskyIn = M.Score[P.Team] > mySc;
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Luck);
        t.LuckRiskyMissChance = 1f;
        Place(P, rimMine.X - s * 7f, 1.5f, rimMine);
        M.Ball.GiveTo(P);
        mySc = M.Score[P.Team];
        _inp.Pressed |= Btn.Ab1; yield return 0;
        foreach (var _ in Wait(2.5f)) yield return 0;
        t.LuckRiskyMissChance = keepMiss;
        // no próprio campo não funciona
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Luck);
        Place(P, -s * 6f, 0, null);
        M.Ball.GiveTo(P);
        _inp.Pressed |= Btn.Ab1; yield return 0;
        bool ownHalfBlocked = M.Ball.Holder == P && P.Ab.Cd[0] <= 0;
        Rec("arremesso_arriscado", riskyIn && M.Score[P.Team] == mySc && ownHalfBlocked);

        // ---------------- Luck 2: Não me Decepciona (cesta do adversário marcado vai para o Luck) ----------------
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Luck);
        Place(e0, spot.X, spot.Z, rim1); Place(P, -spot.X, 0, null); Place(e1, 0, 6f); Place(ally, 0, -6f);
        M.Ball.GiveTo(e0);
        _inp.Target = (byte)e0.Id; _inp.TargetSlot = 1; yield return 0;
        bool marked = P.Ab.BetTarget == e0.Id && P.Ab.BetT > 0;
        int luckSc = M.Score[P.Team], eSc = M.Score[e0.Team];
        e0.TestShot();
        n = 0; while (M.Score[P.Team] == luckSc && n++ < 240) yield return 0;
        Rec("nao_me_decepciona", marked && M.Score[P.Team] > luckSc && M.Score[e0.Team] == eSc && Ev("BET_WIN") > 0);

        // ---------------- Luck 3: Impulso Descuidado (rouba passando / tropeça) ----------------
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Luck);
        float keepDash = t.LuckDashChance;
        t.LuckDashChance = 1f;
        Place(P, 0, 0, null); Place(e0, s * 3f, 0, Vector3.Zero); Place(e1, -s * 10f, 6f); Place(ally, -s * 10f, -6f);
        M.Ball.GiveTo(e0);
        foreach (var _ in Wait(0.1f)) yield return 0;
        _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); _inp.Pressed |= Btn.Ab3; yield return 0;
        n = 0; while (M.Ball.Holder != P && n++ < 60) { _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); yield return 0; }
        bool dashSteal = M.Ball.Holder == P;
        Reset(Characters.Luck);
        t.LuckDashChance = 0f;
        Place(P, 0, 0, null);
        _inp.Pressed |= Btn.Ab3; yield return 0;
        bool tripped = P.Stun > t.LuckTripStun - 0.1f && Ev("TRIP") > 0;
        t.LuckDashChance = keepDash;
        Rec("impulso_descuidado", dashSteal && tripped);

        // ---------------- Luck ult: Sorte Infinita (bônus, aposta ganha, aposta perdida) ----------------
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Luck);
        P.Stun = 0;
        float baseSpeed = P.St.Speed;
        P.Ab.Ult = 1f;
        _inp.Pressed |= Btn.Ult; yield return 0;
        // v0.35: a 1ª ativação mostra a pose da ultimate (jogo congelado) — espera ela acabar
        bool posed = M.Phase == MatchPhase.Cutscene && M.Cut.Kind == CutKind.UltPose;
        int pn = 0;
        while (M.Cut.Active && pn++ < 600) yield return 0;
        yield return 0;
        bool modeOn = posed && P.Ab.ModeT > t.LuckModeDuration - 0.2f && Mathf.Abs(P.St.Speed - baseSpeed * (1f + t.LuckModeBonus)) < 0.01f && P.Ab.Ult <= 0.01f;
        float keepLose = t.LuckGambleLoseChance, keepStep = t.LuckGambleLoseStep;
        t.LuckGambleLoseChance = 0f; t.LuckGambleLoseStep = 0f;
        float before = P.Ab.ModeT;
        _inp.Pressed |= Btn.Ult; yield return 0;
        bool won = P.Ab.ModeWins == 1 && Mathf.Abs(P.Ab.ModeBonus - t.LuckModeBonus * 2f) < 0.001f && P.Ab.ModeT > before + t.LuckGambleExtraTime - 0.1f && P.Ab.ModePoints == 2 * Mathf.RoundToInt(t.LuckModePoints);
        float keepMax = t.LuckGambleLoseMax;
        t.LuckGambleLoseChance = 1f; t.LuckGambleLoseMax = 1f; // o teto também limita a chance
        _inp.Pressed |= Btn.Ult; yield return 0;
        t.LuckGambleLoseMax = keepMax;
        bool lost = P.Ab.ModeT <= 0 && P.Stun > t.LuckGambleStun - 0.1f;
        t.LuckGambleLoseChance = keepLose; t.LuckGambleLoseStep = keepStep;
        if (!(modeOn && won && lost)) GD.Print($"[ABIL] sorte_infinita: modo={modeOn} ganhou={won} perdeu={lost} ModeT={P.Ab.ModeT} stun={P.Stun}");
        Rec("sorte_infinita", modeOn && won && lost);

        // ---------------- Bolt 1: Drible Relâmpago ----------------
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Bolt);
        P.Stun = 0;
        Place(P, 0, 0, new Vector3(s, 0, 0)); Place(e0, s * 3f, 0, Vector3.Zero); Place(e1, -s * 10f, 6f); Place(ally, -s * 10f, -6f);
        M.Ball.GiveTo(P);
        foreach (var _ in Wait(0.1f)) yield return 0;
        _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); _inp.Pressed |= Btn.Ab1; yield return 0;
        bool stunned = e0.Stun > t.BoltDribbleStun * 0.8f; // atordoado na hora do drible
        foreach (var _ in Wait(0.5f)) yield return 0;
        bool past = (P.GlobalPosition.X - e0.GlobalPosition.X) * s > t.BoltDribbleAhead - 0.8f;
        Rec("drible_relampago", past && stunned && M.Ball.Holder == P && Ev("BOLT_DRIBBLE") > 0);

        // ---------------- Bolt 2: Swap (v0.27: troca na hora ao apertar) ----------------
        Reset(Characters.Bolt);
        Place(P, 0, 0, null); Place(ally, s * 8f, 0, null); Place(e0, 0, 8f); Place(e1, 0, -8f);
        foreach (var _ in Wait(0.1f)) yield return 0;
        var pa = P.GlobalPosition; var pb = ally.GlobalPosition;
        bool aiming = PlayerAbilities.SwapCandidate(P, ShotMath.YawOf(new Vector3(s, 0, 0))) == ally; // o anel claro fica nele
        _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); _inp.Pressed |= Btn.Ab2; yield return 0;
        bool swapped = P.GlobalPosition.DistanceTo(pb) < 0.3f && ally.GlobalPosition.DistanceTo(pa) < 0.3f;
        Rec("swap", aiming && swapped && P.Ab.Cd[1] > 0);

        // ---------------- Bolt 3: Arranha-Céu ----------------
        Reset(Characters.Bolt);
        if (M.Ball.Holder == P) M.Ball.Drop(new Vector3(0, 0.3f, M.Geo.HalfW - 2f), Vector3.Zero);
        Place(ally, -s * 8f, 6f); Place(e1, -s * 8f, -6f); Place(e0, s * 2f, 0, Vector3.Zero);
        foreach (var _ in Wait(0.1f)) yield return 0; // (o aliado estava onde o Bolt vai ficar)
        Place(P, 0, 0, null);
        foreach (var _ in Wait(0.2f)) yield return 0;
        _inp.Pressed |= Btn.Ab3; yield return 0;
        float top = 0;
        for (int i = 0; i < 90; i++) { top = Mathf.Max(top, P.GlobalPosition.Y); yield return 0; }
        Rec("arranha_ceu", top > 4f && e0.Stun > 0.3f && Ev("SKY") > 0);
        foreach (var _ in Wait(0.8f)) yield return 0;

        // ---------------- Bolt ult: Duelo (adversário erra = 7 pontos; Bolt erra = bola do adversário) ----------------
        foreach (var r in Duel(true)) yield return r;
        foreach (var r in Duel(false)) yield return r;

        // ---------------- v0.27: modo de teste (trapaças: bot entra com nome do personagem, troca e sai) ----------------
        foreach (var _ in WaitPlaying()) yield return 0;
        M.Cfg.TestMode = true;
        int nBefore = M.Players.Count;
        M.DoCheat(Cheat.AddBot, 1, Characters.Bolt, P);
        var nb = M.Players.Count == nBefore + 1 ? M.Players[^1] : null;
        bool added = nb != null && nb.IsBot && nb.Team == 1 && nb.CharacterId == Characters.Bolt && nb.PlayerName.StartsWith("Bolt");
        yield return 0;
        if (nb != null) M.DoCheat(Cheat.SetChar, nb.Id, Characters.King, P);
        bool changed = nb != null && nb.CharacterId == Characters.King && nb.PlayerName.StartsWith("King");
        P.Ab.Cd[0] = 5; P.Ab.Ult = 0;
        M.DoCheat(Cheat.Refill, 0, 0, P);
        bool refilled = P.Ab.Cd[0] <= 0 && P.Ab.Ult >= 0.999f;
        if (nb != null) M.DoCheat(Cheat.RemoveBot, nb.Id, 0, P);
        yield return 0;
        bool removed = M.Players.Count == nBefore;
        M.Cfg.TestMode = false;
        Rec("trapacas", added && changed && refilled && removed);
        Done = true;
    }

    IEnumerable<int> KingUlt(bool win, bool failBlock = false)
    {
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.King);
        P.Stun = 0;
        Place(P, 0, 0, null);
        M.Ball.GiveTo(P);
        P.Ab.Ult = 1f;
        foreach (var _ in Wait(0.1f)) yield return 0;
        int sc = M.Score[P.Team];
        _inp.Pressed |= Btn.Ult; yield return 0;
        bool started = M.Phase == MatchPhase.Cutscene && M.Cut.Kind == CutKind.KingUlt;
        int n = 0;
        bool sawBlocked = false;
        while (M.Cut.Active && n++ < 1200)
        {
            // failBlock: acerta o 1º e erra o do bloqueio (v0.33)
            bool hit = failBlock ? M.Cut.Stage == Cutscene.KCheck1 : win;
            sawBlocked |= M.Cut.Stage == Cutscene.KBlocked;
            CutPress(M.Cut.CheckFor(P), hit);
            yield return 0;
        }
        int gained = M.Score[P.Team] - sc;
        bool ok = win ? gained == Mathf.RoundToInt(Tuning.I.KingUltPoints) : gained == 0 && M.Ball.Holder != null && M.Ball.Holder.Team != P.Team;
        if (failBlock) ok &= sawBlocked;
        Rec(failBlock ? "ult_king_toco" : win ? "ult_king_acertou" : "ult_king_errou", started && ok && P.Ab.Ult < 0.01f);
    }

    IEnumerable<int> Duel(bool win)
    {
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Bolt);
        P.Stun = 0;
        var e0 = Enemies[0];
        e0.Stun = 0;
        Place(P, 0, 0, null); Place(e0, 6f, 4f, null);
        var ePos = e0.GlobalPosition;
        M.Ball.GiveTo(P);
        P.Ab.Ult = 1f;
        foreach (var _ in Wait(0.1f)) yield return 0;
        int sc = M.Score[P.Team];
        _inp.Target = (byte)e0.Id; _inp.TargetSlot = 3; yield return 0;
        bool started = M.Phase == MatchPhase.Cutscene && M.Cut.Kind == CutKind.BoltDuel;
        int n = 0;
        while (M.Cut.Active && n++ < 1500) { CutPress(M.Cut.CheckFor(P), win); yield return 0; }
        int gained = M.Score[P.Team] - sc;
        bool ok = win ? gained == Mathf.RoundToInt(Tuning.I.BoltDuelPoints)
                      : gained == 0 && M.Ball.Holder == e0 && e0.GlobalPosition.DistanceTo(ePos) < 0.5f && M.Phase == MatchPhase.Playing;
        Rec(win ? "ult_bolt_venceu" : "ult_bolt_perdeu", started && ok);
    }
}
