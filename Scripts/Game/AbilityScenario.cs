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
        P.Ab.EyeT = 0; P.Ab.PresenceT = 0; P.Ab.ModeT = 0; P.Ab.ModeBonus = 0;
        P.Ab.UltT = 0; P.Ab.WallT = 0; P.Ab.TiredT = 0; P.Ab.BonkT = 0; P.Ab.BlockCatch = false; P.Ab.EndCharge();
        P.UpdateStats();
        P.Ab.RealmT = 0;
        foreach (var q in M.Players) { q.Stun = 0; q.StealFail = 0; q.Ab.AbductT = 0; q.Ab.CurseT = 0; q.Ab.BetBy = 0; q.UpdateStats(); }
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

        // ---------------- Luck 2: Aposta Certeira (v0.43): trevo no alvo; a próxima cesta dele dobra ou zera ----------------
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Luck);
        float keepEnemy = t.LuckBetEnemyDoubleChance, keepAlly = t.LuckBetAllyDoubleChance;
        t.LuckBetEnemyDoubleChance = 0f; t.LuckBetAllyDoubleChance = 1f; // sorteio fixo no teste
        Place(e0, spot.X, spot.Z, rim1); Place(P, -spot.X, 0, null); Place(e1, 0, 6f); Place(ally, 0, -6f);
        M.Ball.GiveTo(e0);
        _inp.Target = (byte)e0.Id; _inp.TargetSlot = 1; yield return 0;
        bool marked = e0.Ab.BetBy == P.Id && P.Ab.Cd[1] > 0;
        // com um adversário de trevo, não dá para apostar de novo (nem num aliado)
        P.Ab.Cd[1] = 0;
        _inp.Target = (byte)ally.Id; _inp.TargetSlot = 1; yield return 0;
        bool blocked = ally.Ab.BetBy == 0 && P.Ab.Cd[1] <= 0;
        int eSc = M.Score[e0.Team], zero0 = Ev("BET_ZERO");
        e0.TestShot();
        n = 0; while (Ev("BET_ZERO") == zero0 && n++ < 240) yield return 0;
        foreach (var _ in Wait(0.1f)) yield return 0;
        bool zeroed = Ev("BET_ZERO") > zero0 && M.Score[e0.Team] == eSc && e0.Ab.BetBy == 0;
        // aposta em si mesmo (aliado): a cesta dobra
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Luck);
        var rimL = Rim(P.Team);
        var spotL = rimL - new Vector3(CourtGeo.HoopSign(CourtGeo.AttackHoop(P.Team)) * 4f, 0, 0); spotL.Y = 0;
        Place(P, spotL.X, spotL.Z, rimL); Place(e0, -spotL.X, 6f); Place(e1, -spotL.X, -6f); Place(ally, 0, 8f);
        M.Ball.GiveTo(P);
        _inp.Target = (byte)P.Id; _inp.TargetSlot = 1; yield return 0;
        bool self = P.Ab.BetBy == P.Id;
        int myBet = M.Score[P.Team], dbl0 = Ev("BET_DOUBLE");
        P.TestShot();
        n = 0; while (Ev("BET_DOUBLE") == dbl0 && n++ < 240) yield return 0;
        foreach (var _ in Wait(0.1f)) yield return 0;
        bool doubled = Ev("BET_DOUBLE") > dbl0 && M.Score[P.Team] - myBet == 2 * Mathf.RoundToInt(t.PointsInside) && P.Ab.BetBy == 0;
        t.LuckBetEnemyDoubleChance = keepEnemy; t.LuckBetAllyDoubleChance = keepAlly;
        bool okBet = marked && blocked && zeroed && self && doubled;
        if (!okBet) GD.Print($"[ABIL] aposta_certeira: marcou={marked} bloqueou={blocked} zerou={zeroed} si_mesmo={self} dobrou={doubled}");
        Rec("aposta_certeira", okBet);

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

        // ---------------- v0.37: Iron ----------------
        foreach (var r in IronTests()) yield return r;
        foreach (var r in OneTests()) yield return r;

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

    IEnumerable<int> IronTests()
    {
        var t = Tuning.I;
        var e0 = Enemies[0]; var e1 = Enemies[1]; var ally = Ally;
        float s = CourtGeo.HoopSign(CourtGeo.AttackHoop(P.Team));
        int def = CourtGeo.DefendHoop(P.Team);           // cesta que o Iron defende = onde o adversário arremessa
        var rimD = M.Geo.RimCenter[def];
        float inward = -CourtGeo.HoopSign(def);          // da cesta para o meio da quadra

        // ---- (1) Muralha de Aço: arremesso de fora bate nela; de dentro entra ----
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Iron);
        Place(P, rimD.X + inward * 3f, 5f); Place(ally, 0, 8f); Place(e1, 0, -8f);
        Place(e0, rimD.X + inward * (M.Geo.ThreePt + 1.2f), 0.5f, rimD);
        M.Ball.GiveTo(e0);
        foreach (var _ in Wait(0.2f)) yield return 0;
        _inp.Pressed |= Btn.Ab1; yield return 0;
        bool wallOn = M.Geo.Wall[def] && P.Ab.WallT > t.IronWallDuration - 0.1f;
        int st0 = Ev("STEEL_HIT"), sc0 = M.Score[e0.Team];
        e0.TestShot();
        foreach (var _ in Wait(2.0f)) yield return 0;
        bool blocked = Ev("STEEL_HIT") > st0 && M.Score[e0.Team] == sc0;
        Place(e0, rimD.X + inward * 3.5f, 0.3f, rimD);
        M.Ball.GiveTo(e0);
        foreach (var _ in Wait(0.1f)) yield return 0;
        bool stillOn = M.Geo.Wall[def];
        e0.TestShot();
        int n = 0; while (M.Score[e0.Team] == sc0 && n++ < 200) yield return 0;
        bool inside = M.Score[e0.Team] > sc0;
        if (!(wallOn && blocked && stillOn && inside)) GD.Print($"[ABIL] muralha: on={wallOn} barrou={blocked} ainda={stillOn} dentro={inside}");
        // v0.43: atravessar a muralha com a bola = a bola cai e ele não pega de volta por um tempo
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Iron);
        P.Ab.WallT = 5f;
        Place(e0, rimD.X + inward * (M.Geo.ThreePt + 1.0f), 0.5f, rimD);
        M.Ball.GiveTo(e0);
        foreach (var _ in Wait(0.1f)) yield return 0;
        int wd0 = Ev("WALL_DROP");
        for (int i = 1; i <= 10; i++)   // anda para dentro atravessando a linha de 3
        {
            e0.GlobalPosition = new Vector3(rimD.X + inward * (M.Geo.ThreePt + 1.0f - 0.2f * i), e0.GlobalPosition.Y, 0.5f);
            yield return 0;
        }
        bool dropped = Ev("WALL_DROP") > wd0 && M.Ball.Holder != e0 && e0.BallLockoutTimer > t.IronWallDropLockout - 0.1f;
        P.Ab.WallT = 0;
        if (!dropped) GD.Print($"[ABIL] muralha: derrubou={dropped} ev={Ev("WALL_DROP") - wd0} dono={(M.Ball.Holder == e0)} lock={e0.BallLockoutTimer:0.00} wall={M.Geo.Wall[def]} tres={M.Geo.IsThree(e0.GlobalPosition, def)} pos={e0.GlobalPosition} fase={M.Phase}");
        Rec("muralha_de_aco", wallOn && blocked && stillOn && inside && dropped);

        // ---- (2) Pancada: atordoa quem está perto (o Iron pode estar com a bola); não vale em quem tem a bola ----
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Iron);
        Place(P, 0, 0, new Vector3(s, 0, 0)); Place(e0, s * 2f, 0, Vector3.Zero); Place(e1, -s * 10f, 6f); Place(ally, -s * 10f, -6f);
        M.Ball.GiveTo(P);
        foreach (var _ in Wait(0.1f)) yield return 0;
        int b0 = Ev("BONK");
        _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); _inp.Pressed |= Btn.Ab2; yield return 0;
        bool bonked = Ev("BONK") > b0 && e0.Stun > t.IronBonkStun - 0.1f && M.Ball.Holder == P && P.Ab.Cd[1] > 0;
        Reset(Characters.Iron);
        M.Ball.GiveTo(e0);
        foreach (var _ in Wait(0.1f)) yield return 0;
        b0 = Ev("BONK");
        _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); _inp.Pressed |= Btn.Ab2; yield return 0;
        bool notHolder = Ev("BONK") == b0 && e0.Stun <= 0 && P.Ab.Cd[1] <= 0;
        Rec("pancada", bonked && notHolder);

        // ---- (3) Investida: atropela adversário com a bola e aliado; acaba cansado; na parede = atordoado ----
        Reset(Characters.Iron);
        Place(P, -s * 8f, 0, new Vector3(s, 0, 0)); Place(e0, -s * 4f, 0.2f, Vector3.Zero); Place(ally, s * 1f, -0.3f, null); Place(e1, s * 3f, 10f);
        M.Ball.GiveTo(e0);
        foreach (var _ in Wait(0.1f)) yield return 0;
        int h0 = Ev("CHARGE_HIT"), d0 = Ev("CHARGE_DROP"), tired0 = Ev("CHARGE_TIRED");
        _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); _inp.Pressed |= Btn.Ab3; yield return 0;
        bool started = P.Ab.Charging;
        float e0Stun = 0, allyStun = 0;
        n = 0;
        bool fastStart = false;
        while ((P.Ab.Charging || n < 3) && n++ < 400)
        {
            // v0.38: a direção segue a câmera; os botões de movimento não fazem nada (manda andar para o lado e não vira)
            _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); _inp.Move = new Vector2(0, 1);
            if (n == 3) fastStart = new Vector2(P.Velocity.X, P.Velocity.Z).Length() > P.St.RunSpeed;
            e0Stun = Mathf.Max(e0Stun, e0.Stun); allyStun = Mathf.Max(allyStun, ally.Stun); yield return 0;
        }
        bool straight = Mathf.Abs(ShotMath.WrapAngle(P.Yaw - ShotMath.YawOf(new Vector3(s, 0, 0)))) < 0.05f;
        bool tired = Ev("CHARGE_TIRED") > tired0 && P.Ab.TiredT > 0 && P.Stun <= 0;
        bool hits = Ev("CHARGE_HIT") >= h0 + 2 && Ev("CHARGE_DROP") > d0 && e0Stun > t.IronChargeStun - 0.5f && allyStun > t.IronChargeStun - 0.5f;
        foreach (var _ in Wait(t.IronChargeRecover + 0.2f)) yield return 0;
        Reset(Characters.Iron);
        if (M.Ball.Holder == P) M.Ball.Drop(new Vector3(0, 0.3f, M.Geo.HalfW - 2f), Vector3.Zero);
        Place(P, s * (M.Geo.HalfL - 3f), -5f, new Vector3(s, 0, 0));
        foreach (var _ in Wait(0.1f)) yield return 0;
        int w0 = Ev("CHARGE_WALL");
        _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); _inp.Pressed |= Btn.Ab3; yield return 0;
        n = 0; while (P.Ab.Charging && n++ < 240) yield return 0;
        bool wall = Ev("CHARGE_WALL") > w0 && P.Stun > 0;
        // v0.38: virando a câmera, a investida vira junto
        foreach (var _ in Wait(t.IronChargeRecover + 0.2f)) yield return 0;
        Reset(Characters.Iron);
        Place(P, -s * 6f, -8f, new Vector3(s, 0, 0));
        foreach (var _ in Wait(0.1f)) yield return 0;
        _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); _inp.Pressed |= Btn.Ab3; yield return 0;
        float wantYaw = ShotMath.YawOf(new Vector3(s, 0, 1));
        for (int i = 0; i < 30 && P.Ab.Charging; i++) { _inp.AimYaw = wantYaw; yield return 0; }
        bool turned = Mathf.Abs(ShotMath.WrapAngle(P.Yaw - wantYaw)) < 0.1f;
        n = 0; while (P.Ab.Charging && n++ < 300) { _inp.AimYaw = wantYaw; yield return 0; }
        bool ok3 = started && hits && tired && wall && fastStart && straight && turned;
        if (!ok3) GD.Print($"[ABIL] investida: começou={started} atropelou={hits} ({Ev("CHARGE_HIT") - h0} hits, e0={e0Stun:0.0} aliado={allyStun:0.0}) cansado={tired} parede={wall} impulso={fastStart} reto={straight} virou={turned}");
        Rec("investida", ok3);
        foreach (var _ in Wait(t.IronChargeRecover)) yield return 0;

        // ---- Ult: Indestrutível (pose, velocidade, estamina, imune, pega a bola no bloqueio, pune roubo no drible) ----
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.Iron);
        Place(P, 0, 0, new Vector3(s, 0, 0)); Place(e0, s * 6f, 3f); Place(e1, -s * 2f, 2f); Place(ally, -s * 10f, -6f);
        P.Ab.Ult = 1f;
        _inp.Pressed |= Btn.Ult; yield return 0;
        bool posed = M.Phase == MatchPhase.Cutscene && M.Cut.Kind == CutKind.UltPose;
        n = 0; while (M.Cut.Active && n++ < 600) yield return 0;
        yield return 0;
        bool on = P.Ab.UltT > t.IronUltDuration - 0.3f && P.Ab.Ult <= 0.01f;
        bool fast = P.St.Speed >= PlayerStats.Mult(Mathf.RoundToInt(t.IronUltSpeedStars), t.StarSpeedStep) - 0.001f;
        P.Stamina = 0.1f; yield return 0;
        bool stamina = P.Stamina >= P.St.StaminaMax - 0.01f;
        // imune: Presença de um King adversário ao lado não vale; o Swap de um Bolt não o escolhe
        int keepChar = e1.CharacterId;
        e1.CharacterId = Characters.King; e1.Ab.PresenceT = 3f;
        bool immune = !M.PresenceJinx(P) && PlayerAbilities.SwapCandidate(e1, ShotMath.YawOf((P.GlobalPosition - e1.GlobalPosition) with { Y = 0 })) != P;
        P.Ab.UltT = 0; bool control = M.PresenceJinx(P); P.Ab.UltT = t.IronUltDuration;
        e1.Ab.PresenceT = 0; e1.CharacterId = keepChar;
        // pega a bola no bloqueio
        Place(e0, rimD.X + inward * 6f, 0, rimD); Place(e1, -s * 10f, 6f);
        M.Ball.GiveTo(e0);
        foreach (var _ in Wait(0.1f)) yield return 0;
        e0.TestShot();
        yield return 0; yield return 0;
        P.GlobalPosition = M.Ball.GlobalPosition - Vector3.Up * 1.5f; P.Velocity = Vector3.Zero; P.Airborne = true; P.ResetPhysicsInterpolation();
        yield return 0; yield return 0;
        bool caught = M.Ball.Holder == P && P.Ab.BlockCatch && !P.CanBeStolen && Ev("IRON_CATCH") > 0;
        n = 0; while ((P.Airborne || P.LandLock > 0) && n++ < 300) yield return 0;
        yield return 0;
        bool released = !P.Ab.BlockCatch;
        // v0.39: recargas correm 15% mais rápido no modo; bloquear enterrada não é falta (fica com a bola)
        Reset(Characters.Iron); P.Ab.UltT = t.IronUltDuration;
        Place(P, rimD.X + inward * 4f, 6f); Place(e1, -s * 10f, 6f); Place(e0, s * 10f, -6f);
        _inp.Pressed |= Btn.Ab1; yield return 0;
        float cdA = P.Ab.Cd[0];
        foreach (var _ in Wait(1f)) yield return 0;
        float ran = cdA - P.Ab.Cd[0];
        bool cdShort = Mathf.Abs(cdA - t.IronWallCooldown) < 0.05f && Mathf.Abs(ran - 1f * (1f + t.IronUltCdSpeedup)) < 0.06f;
        foreach (var _ in WaitPlaying()) yield return 0;
        P.Ab.WallT = 0;
        var rimE = M.Geo.RimCenter[CourtGeo.AttackHoop(e0.Team)];
        float sgE = CourtGeo.HoopSign(CourtGeo.AttackHoop(e0.Team));
        Place(e0, rimE.X - sgE * 1.8f, rimE.Z, rimE); Place(P, rimE.X - sgE * 1.8f, rimE.Z + 5f, null);
        e0.Stun = 0;
        M.Ball.GiveTo(e0);
        foreach (var _ in Wait(0.25f)) yield return 0;
        int f0 = Ev("FOUL"), db0 = Ev("IRON_DUNK_BLOCK");
        bool dunking = e0.TestDunk();
        bool dunkBlocked = false;
        for (int i = 0; i < 60 && e0.Flight != FlightKind.None; i++)
        {
            var hand = e0.GlobalPosition + Vector3.Up * t.DunkHandReach + e0.Forward * 0.35f;
            P.GlobalPosition = hand - Vector3.Up * 1.5f; P.Velocity = Vector3.Zero; P.Airborne = true; P.ResetPhysicsInterpolation();
            yield return 0;
        }
        dunkBlocked = dunking && Ev("IRON_DUNK_BLOCK") > db0 && Ev("FOUL") == f0 && M.Ball.Holder == P && M.Phase == MatchPhase.Playing;
        n = 0; while ((P.Airborne || P.LandLock > 0) && n++ < 300) yield return 0;
        // v0.39: nenhuma habilidade adversária pega nele — Investida de um Iron adversário passa sem atropelar nem empurrar
        foreach (var _ in WaitPlaying()) yield return 0;
        if (M.Ball.Holder == P) M.Ball.Drop(new Vector3(0, 0.3f, M.Geo.HalfW - 2f), Vector3.Zero);
        P.Ab.UltT = t.IronUltDuration; P.Stun = 0;
        int keepE1 = e1.CharacterId;
        e1.CharacterId = Characters.Iron; e1.Stun = 0;
        Place(P, 0, 0, null); Place(e1, -s * 4f, 0, new Vector3(s, 0, 0)); Place(e0, s * 10f, -8f); Place(ally, -s * 10f, 8f);
        foreach (var _ in Wait(0.1f)) yield return 0;
        var pBefore = P.GlobalPosition;
        e1.Ab.TestCharge(ShotMath.YawOf(new Vector3(s, 0, 0)));
        float moved = 0; n = 0;
        while (e1.Ab.Charging && n++ < 240) { moved = Mathf.Max(moved, ((P.GlobalPosition - pBefore) with { Y = 0 }).Length()); yield return 0; }
        bool chargeImmune = P.Stun <= 0 && moved < 0.25f && (e1.GlobalPosition.X - P.GlobalPosition.X) * s > 0.5f;
        e1.Ab.TiredT = 0; e1.Stun = 0; e1.CharacterId = keepE1;
        // ... e a Muralha de Aço do adversário não barra a bola dele
        foreach (var _ in WaitPlaying()) yield return 0;
        int keepE0 = e0.CharacterId;
        e0.CharacterId = Characters.Iron; e0.Ab.WallT = 5f;
        int atk = CourtGeo.AttackHoop(P.Team);
        var rimA = M.Geo.RimCenter[atk];
        float inA = -CourtGeo.HoopSign(atk);
        Place(P, rimA.X + inA * (M.Geo.ThreePt + 1.2f), 0.5f, rimA); Place(e0, rimA.X + inA * 2f, 9f); Place(e1, 0, -9f);
        M.Ball.GiveTo(P);
        foreach (var _ in Wait(0.1f)) yield return 0;
        bool wallUp = M.Geo.Wall[atk];
        int stA = Ev("STEEL_HIT"), myA = M.Score[P.Team];
        P.TestShot();
        n = 0; while (M.Score[P.Team] == myA && n++ < 200) yield return 0;
        bool wallPass = wallUp && Ev("STEEL_HIT") == stA && M.Score[P.Team] > myA;
        e0.Ab.WallT = 0; e0.CharacterId = keepE0;
        bool ok = posed && on && fast && stamina && immune && control && caught && released && cdShort && dunkBlocked && chargeImmune && wallPass;
        if (!ok) GD.Print($"[ABIL] indestrutivel: pose={posed} ativo={on} rápido={fast} estamina={stamina} imune={immune} controle={control} pegou={caught} soltou={released} recarga={cdShort} ({ran:0.00}s) enterrada={dunkBlocked} investida={chargeImmune} (empurrado {moved:0.00} m) muralha={wallPass}");
        Rec("indestrutivel", ok);
        P.Ab.UltT = 0; P.UpdateStats();
    }

    // =====================================================================
    // v0.41 — One
    IEnumerable<int> OneTests()
    {
        var t = Tuning.I;
        var e0 = Enemies[0]; var e1 = Enemies[1]; var ally = Ally;
        float s = CourtGeo.HoopSign(CourtGeo.AttackHoop(P.Team));
        float g = Mathf.Clamp(t.OneGhostAlpha, 0.05f, 1f);
        int n;

        // ---- (1) Realm: invisível para o adversário, transparente para o aliado, atravessa, não pega a bola, sai ao roubar ----
        foreach (var _ in WaitPlaying()) yield return 0;
        if (M.Ball.Holder != null) M.Ball.Drop(new Vector3(0, 0.3f, M.Geo.HalfW - 2f), Vector3.Zero);
        Reset(Characters.One);
        Place(P, -s * 3f, 0, new Vector3(s, 0, 0)); Place(e0, -s * 1.5f, 0, null); Place(e1, s * 8f, 8f); Place(ally, s * 8f, -8f);
        yield return 0;
        float baseSpeed = P.St.Speed;
        _inp.Pressed |= Btn.Ab1; yield return 0;
        yield return 0;
        bool inRealm = P.Phased && P.Ab.RealmT > t.OneRealmDuration - 0.2f && P.Ab.Cd[0] <= 0 && P.CollisionLayer == 0;
        bool faster = Mathf.Abs(P.St.Speed - baseSpeed * (1f + t.OneRealmSpeedBonus)) < 0.01f;
        bool sight = RealmView.Alpha(M, e0, P) == 0f && Mathf.IsEqualApprox(RealmView.Alpha(M, ally, P), g) && Mathf.IsEqualApprox(RealmView.Alpha(M, P, e0), g)
                     && RealmView.Alpha(M, e0, ally) == 1f;
        // atravessa o adversário andando para a frente
        n = 0;
        while (n++ < 50) { _inp.Move = new Vector2(s, 0); yield return 0; }
        bool through = (P.GlobalPosition.X - e0.GlobalPosition.X) * s > 0.4f;
        // não pega a bola
        M.Ball.Drop(P.GlobalPosition + Vector3.Up * 1.0f, Vector3.Zero);
        foreach (var _ in Wait(0.4f)) yield return 0;
        bool noBall = M.Ball.Holder != P && P.Phased;
        // tentar roubar tira do Realm (e o roubo acontece)
        int st0 = Ev("STEAL_TRY");
        _inp.Pressed |= Btn.Steal; _inp.Held |= Btn.Steal; yield return 0;
        bool exitSteal = !P.Phased && P.Ab.Cd[0] > t.OneRealmCooldown - 0.1f && Ev("STEAL_TRY") > st0;
        // saiu dentro de alguém: empurrado só para fora
        foreach (var _ in Wait(1.2f)) yield return 0;
        if (M.Ball.Holder != null) M.Ball.Drop(new Vector3(0, 0.3f, M.Geo.HalfW - 2f), Vector3.Zero);
        else M.Ball.Drop(new Vector3(0, 0.3f, M.Geo.HalfW - 2f), Vector3.Zero);
        Reset(Characters.One);
        Place(P, 0, 0, new Vector3(s, 0, 0)); Place(e0, s * 2f, 0, null);
        yield return 0;
        _inp.Pressed |= Btn.Ab1; yield return 0;
        P.GlobalPosition = e0.GlobalPosition with { Y = P.GlobalPosition.Y } + new Vector3(0, 0, 0.1f);
        yield return 0;
        _inp.Pressed |= Btn.Ab1; yield return 0; // de novo = sai
        var e0At = e0.GlobalPosition;
        foreach (var _ in Wait(0.1f)) yield return 0;
        bool e0Still = ((e0.GlobalPosition - e0At) with { Y = 0 }).Length() < 0.05f && P.CollisionLayer == 2;
        float sep = ((P.GlobalPosition - e0.GlobalPosition) with { Y = 0 }).Length();
        bool pushed = !P.Phased && sep >= 0.7f && sep < 0.85f && e0Still;
        bool ok1 = inRealm && faster && sight && through && noBall && exitSteal && pushed;
        if (!ok1) GD.Print($"[ABIL] realm: entrou={inRealm} rápido={faster} visão={sight} atravessou={through} sem_bola={noBall} saiu_roubando={exitSteal} empurrado={pushed} ({sep:0.00} m)");
        Rec("realm", ok1);

        // ---- (2) Abdução: some no Realm por X s, não vê quem está fora, não pega a bola; não vale em quem tem a bola ----
        foreach (var _ in Wait(0.3f)) yield return 0;
        Reset(Characters.One);
        Place(P, 0, 0, new Vector3(s, 0, 0)); Place(e0, s * 3f, 0.3f, null); Place(e1, -s * 8f, 8f); Place(ally, -s * 3f, -1f);
        M.Ball.GiveTo(e1);
        yield return 0;
        int ab0 = Ev("ABDUCT");
        _inp.AimYaw = ShotMath.YawOf(new Vector3(s, 0, 0)); _inp.Pressed |= Btn.Ab2; yield return 0;
        bool abducted = Ev("ABDUCT") > ab0 && e0.Phased && e0.Ab.AbductT > t.OneAbductDuration - 0.1f && P.Ab.Cd[1] > 0;
        bool blind = RealmView.Alpha(M, e0, ally) == 0f && RealmView.Alpha(M, e0, P) == 0f && RealmView.Alpha(M, e1, e0) == 0f
                     && RealmView.Alpha(M, P, e0) == 0f;
        M.Ball.Drop(e0.GlobalPosition + Vector3.Up * 1.0f, Vector3.Zero);
        foreach (var _ in Wait(0.4f)) yield return 0;
        bool noCatch = M.Ball.Holder != e0;
        foreach (var _ in Wait(t.OneAbductDuration)) yield return 0;
        bool back = !e0.Phased;
        Reset(Characters.One);
        M.Ball.GiveTo(e0);
        yield return 0;
        bool notHolder = PlayerAbilities.AbductCandidate(P, ShotMath.YawOf(new Vector3(s, 0, 0))) == null;
        // v0.42: quem já está no Realm não pode ser abduzido (mas o One pode abduzir estando no Realm)
        M.Ball.Drop(new Vector3(0, 0.3f, M.Geo.HalfW - 2f), Vector3.Zero);
        e0.Ab.AbductT = 1f;
        bool notPhased = PlayerAbilities.AbductCandidate(P, ShotMath.YawOf(new Vector3(s, 0, 0))) == null;
        e0.Ab.AbductT = 0; P.Ab.RealmT = 2f;
        bool fromRealm = PlayerAbilities.AbductCandidate(P, ShotMath.YawOf(new Vector3(s, 0, 0))) == e0;
        P.Ab.RealmT = 0;
        notHolder &= notPhased && fromRealm;
        bool ok2 = abducted && blind && noCatch && back && notHolder;
        if (!ok2) GD.Print($"[ABIL] abducao: abduziu={abducted} cego={blind} sem_bola={noCatch} voltou={back} nao_no_dono={notHolder}");
        Rec("abducao", ok2);

        // ---- (3) Maldição: atributos pela metade e cesta valendo 1 a menos ----
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.One);
        var rimE = M.Geo.RimCenter[CourtGeo.AttackHoop(e0.Team)];
        float sgE = CourtGeo.HoopSign(CourtGeo.AttackHoop(e0.Team));
        Place(e0, rimE.X - sgE * 3.5f, 0.3f, rimE); Place(P, 0, 8f); Place(e1, 0, -8f); Place(ally, -sgE * 8f, 0);
        M.Ball.GiveTo(e0);
        yield return 0; yield return 0;
        float eSpeed = e0.St.Speed;
        _inp.Target = (byte)e0.Id; _inp.TargetSlot = 2; yield return 0;
        yield return 0; yield return 0;
        bool cursed = e0.Ab.CurseT > t.OneCurseDuration - 0.2f && P.Ab.Cd[2] > 0 && Mathf.Abs(e0.St.Speed - eSpeed * t.OneCurseStatMult) < 0.01f;
        int sc0 = M.Score[e0.Team];
        e0.TestShot();
        n = 0; while (M.Score[e0.Team] == sc0 && Ev("SCORE_CURSED") == 0 && n++ < 240) yield return 0;
        foreach (var _ in Wait(0.1f)) yield return 0;
        int gained = M.Score[e0.Team] - sc0;
        bool less = Ev("SCORE_CURSED") > 0 && gained == Mathf.Max(0, Mathf.RoundToInt(t.PointsInside) - Mathf.RoundToInt(t.OneCursePointLoss));
        bool ok3 = cursed && less;
        if (!ok3) GD.Print($"[ABIL] maldicao: amaldiçoou={cursed} (vel {eSpeed:0.00}→{e0.St.Speed:0.00}) pontos={gained} evento={Ev("SCORE_CURSED")}");
        Rec("maldicao", ok3);

        // ---- Ult: Domínio das Sombras (cena, time do One invisível para o adversário, −20%, recargas mais lentas) ----
        foreach (var _ in WaitPlaying()) yield return 0;
        Reset(Characters.One);
        if (M.Ball.Holder != null) M.Ball.Drop(new Vector3(0, 0.3f, M.Geo.HalfW - 2f), Vector3.Zero);
        Place(P, 0, 0, new Vector3(s, 0, 0)); Place(e0, s * 5f, 3f); Place(e1, -s * 5f, -3f); Place(ally, -s * 6f, 5f);
        yield return 0;
        float e0Base = e0.St.Speed, pBase = P.St.Speed, allyBase = ally.St.Speed;
        P.Ab.Ult = 1f;
        _inp.Pressed |= Btn.Ult; yield return 0;
        bool posed = M.Phase == MatchPhase.Cutscene && M.Cut.Kind == CutKind.UltPose;
        n = 0; while (M.Cut.Active && n++ < 600) yield return 0;
        yield return 0; yield return 0;
        bool on = P.Ab.Domain && P.Ab.UltT > t.OneUltDuration - 0.3f && P.Ab.Ult <= 0.01f;
        bool stats = Mathf.Abs(e0.St.Speed - e0Base * t.OneUltEnemyStatMult) < 0.01f && Mathf.Abs(P.St.Speed - pBase * (1f + t.OneRealmSpeedBonus)) < 0.01f
                     && Mathf.Abs(ally.St.Speed - allyBase) < 0.01f;
        bool hidden = RealmView.Alpha(M, e0, P) == 0f && RealmView.Alpha(M, e0, ally) == 0f && RealmView.Alpha(M, P, e0) == 1f && RealmView.Alpha(M, ally, P) == 1f
                      && RealmView.Alpha(M, e0, e1) == 1f;
        e0.Ab.Cd[0] = 10f; ally.Ab.Cd[0] = 10f;
        foreach (var _ in Wait(1f)) yield return 0;
        float eRan = 10f - e0.Ab.Cd[0], aRan = 10f - ally.Ab.Cd[0];
        bool slow = Mathf.Abs(eRan - (1f - t.OneUltEnemyCdSlow)) < 0.05f && Mathf.Abs(aRan - 1f) < 0.05f;
        // o adversário ainda interage normalmente: pega a bola solta
        M.Ball.Drop(e0.GlobalPosition + Vector3.Up * 1.0f, Vector3.Zero);
        n = 0; while (M.Ball.Holder != e0 && n++ < 60) yield return 0;
        bool interacts = M.Ball.Holder == e0;
        e0.Ab.Cd[0] = 0; ally.Ab.Cd[0] = 0;
        // v0.42: no Domínio não dá para entrar no Realm, abduzir ninguém nem usar outro Domínio
        P.Ab.Cd[0] = 0; P.Ab.Cd[1] = 0; P.Ab.Ult = 1f;
        _inp.Pressed |= Btn.Ab1 | Btn.Ult; yield return 0;
        bool gated = P.Ab.RealmT <= 0 && P.Ab.Ult >= 0.999f && M.Phase == MatchPhase.Playing && PlayerAbilities.AbductCandidate(P, P.Yaw) == null;
        P.Ab.Ult = 0;
        bool ok4 = posed && on && stats && hidden && slow && interacts && gated;
        if (!ok4) GD.Print($"[ABIL] dominio: pose={posed} ativo={on} atributos={stats} invisível={hidden} recargas={slow} ({eRan:0.00}/{aRan:0.00}) interage={interacts} bloqueios={gated}");
        Rec("dominio_das_sombras", ok4);
        P.Ab.UltT = 0;
        foreach (var q in M.Players) q.UpdateStats();
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
