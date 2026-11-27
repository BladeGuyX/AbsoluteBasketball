using Godot;

namespace AB;

/// <summary>Ícone desenhado (sem imagem) de cada habilidade — ver <see cref="AbilityIcons"/>.</summary>
public enum AbIcon { Eye, Aura, PerfectDribble, Crown, Dice, Target, Dash, Clover, Lightning, Swap, Tower, Duel, Wall, Bonk, Charge, Shield, Realm, Abduct, Skull, Shadow }

/// <summary>Uma habilidade (slots 0–2) ou a ultimate (slot 3) de um personagem.</summary>
public sealed class AbilityDef
{
    public string Name, Short, Full;
    public AbIcon Icon;
    public Color Color;
}

/// <summary>
/// v0.24: personagens jogáveis. Todos usam o mesmo esqueleto (rig_base) e as mesmas animações (CharacterVisual).
/// v0.26: cada um tem atributos em estrelas (Tuning, aba do personagem no artifact), 3 habilidades e uma ultimate.
/// Os ids não mudaram: 0 = King (era o Rei), 1 = Luck (era a Ametista), 2 = Bolt (era o Raio).
/// </summary>
public static class Characters
{
    public const int King = 0, Luck = 1, Bolt = 2, Iron = 3, One = 4;
    public static readonly string[] StatNames = { "Velocidade", "Agilidade", "Estamina", "Precisão", "Salto" };

    public sealed class Def
    {
        public string Name, Model, Tagline;
        /// <summary>Texto da roda por id de expressão (índice 0 vazio). Os objetos do .glb têm sempre os mesmos nomes.</summary>
        public string[] FaceLabels;
        public Color Color;
        public AbilityDef[] Abilities; // 0..2 = habilidades (teclas 1, 2, 3), 3 = ultimate (4)
    }

    static readonly Color KingRed = new("e8313a"), LuckGreen = new("3ccf6e"), BoltCyan = new("3fd7ff"), IronSilver = new("c3cad3"), OnePurple = new("a066f5");

    public static readonly Def[] All =
    {
        new()
        {
            Name = "King", Model = "res://assets/models/rei.glb", FaceLabels = Expressions.Label, Color = KingRed,
            Tagline = "Precisão absoluta. Arremessa de qualquer lugar.",
            Abilities = new AbilityDef[]
            {
                new() { Name = "Olho do Rei", Icon = AbIcon.Eye, Color = KingRed,
                    Short = "Próximo passe/arremesso com muito mais alcance.",
                    Full = "Fica ativo por {KingEyeDuration} s. O próximo passe ou arremesso (incluindo o passe rápido) gasta o efeito e tem {KingEyeRangeMult}x o alcance. Para arremessar mais longe, suba a câmera além do normal. A bola deixa um rastro vermelho. Recarga: {KingEyeCooldown} s." },
                new() { Name = "Presença Implacável", Icon = AbIcon.Aura, Color = KingRed,
                    Short = "Adversários perto de você erram qualquer arremesso.",
                    Full = "Dura {KingPresenceDuration} s. Todo adversário a até {KingPresenceRadius} m que soltar um arremesso erra com certeza (a trajetória é desviada); quem começar uma enterrada perto dele não tem skill check e erra. Só vale se for ativada antes de ele soltar o botão. A área não aparece: o King solta partículas vermelhas e quem teve o arremesso afetado vê a borda da tela ficar vermelha. Recarga: {KingPresenceCooldown} s." },
                new() { Name = "Drible Perfeito", Icon = AbIcon.PerfectDribble, Color = KingRed,
                    Short = "Drible mais longo; quem tentar roubar cai.",
                    Full = "Só com a bola. Faz na hora um drible (bola com aura vermelha) {KingDribbleExtra} s mais longo que o normal e que não gasta cargas de drible. Quem tentar roubar a bola durante ele cai no chão atordoado por {KingDribbleStun} s. Recarga: {KingDribbleCooldown} s." },
                new() { Name = "Sua Derrota é Absoluta", Icon = AbIcon.Crown, Color = new Color("ffd23f"),
                    Short = "Cutscene: dribla, dá a volta no bloqueio e arremessa.",
                    Full = "Ultimate, só com a bola. Começa uma cutscene: no meio da quadra o King tenta driblar um adversário (skill check). Se errar, ele rouba a bola: a rodada reinicia com a bola do time adversário. Se acertar, o King vai para a linha de 3, outro adversário pula para bloquear (2º skill check: se errar, o King arremessa, toma o toco e a rodada reinicia com a bola do adversário). Se acertar, o King dá a volta nele e arremessa pelo lado da cesta (último skill check, o mais difícil). Acertou: cesta de {KingUltPoints} pontos. Errou: a bola bate no aro e sai, e a rodada reinicia com a bola do adversário." },
            },
        },
        new()
        {
            Name = "Luck", Model = "res://assets/models/ametista.glb", Color = LuckGreen,
            FaceLabels = new[] { "", "Relaxado", "Focado", "Sorriso convencido", "Bravo", "Rindo", "Surpreso" },
            Tagline = "Muita estamina e muita sorte (quase sempre).",
            Abilities = new AbilityDef[]
            {
                new() { Name = "Arremesso Arriscado", Icon = AbIcon.Dice, Color = LuckGreen,
                    Short = "Arremessa direto na cesta (pode errar).",
                    Full = "Só com a bola e no lado adversário da quadra. Arremessa na hora direto na cesta do oponente. Pode ser bloqueado e tem {%LuckRiskyMissChance} de chance de errar. Recarga: {LuckRiskyCooldown} s." },
                new() { Name = "Aposta Certeira", Icon = AbIcon.Clover, Color = LuckGreen,
                    Short = "Aposta na próxima cesta de um jogador: dobra ou zera.",
                    Full = "Abre o menu para escolher qualquer jogador (adversário, aliado ou o próprio Luck). A próxima cesta dele (as de cutscene não contam) tem {%LuckBetAllyDoubleChance} de chance de valer o dobro e o resto de não valer nada se ele for do time do Luck; se for adversário, só {%LuckBetEnemyDoubleChance} de chance de dobrar. Todo mundo vê o trevo verde na cabeça dele até ele fazer a cesta. Não dá para usar enquanto algum adversário estiver com o trevo. Recarga: {LuckBetCooldown} s." },
                new() { Name = "Impulso Descuidado", Icon = AbIcon.Dash, Color = LuckGreen,
                    Short = "Impulso para frente que rouba a bola (pode tropeçar).",
                    Full = "{%LuckDashChance} de chance de dar um impulso para onde você está olhando. Sem a bola, passar por quem está com ela durante o impulso rouba a bola. Senão, o Luck tropeça e fica atordoado por {LuckTripStun} s. Recarga: {LuckDashCooldown} s." },
                new() { Name = "Sorte Infinita", Icon = AbIcon.Clover, Color = new Color("ffd23f"),
                    Short = "Modo apostador: atributos melhores e cestas valem mais. Aposte de novo!",
                    Full = "Ultimate. O Luck entra no modo apostador por {LuckModeDuration} s: +{%LuckModeBonus} em todos os valores dos atributos de estrela e as cestas dele valem {LuckModePoints} ponto(s) a mais (partículas verdes). Apertando a ultimate de novo durante o modo ele aposta: na 1ª vez, {%LuckGambleLoseChance} de chance de perder (o modo acaba na hora e ele fica atordoado por {LuckGambleStun} s); se ganhar, o bônus dos atributos dobra, o modo ganha +{LuckGambleExtraTime} s e as cestas valem mais +{LuckModePoints}. A cada aposta seguinte a chance de perder sobe {%LuckGambleLoseStep} (no máximo {%LuckGambleLoseMax})." },
            },
        },
        new()
        {
            Name = "Bolt", Model = "res://assets/models/raio.glb", Color = BoltCyan,
            FaceLabels = new[] { "", "Tranquilo", "Focado", "Sorriso confiante", "Irritado", "Empolgado", "Surpreso" },
            Tagline = "O mais rápido da quadra. Mira de relâmpago (só que não).",
            Abilities = new AbilityDef[]
            {
                new() { Name = "Drible Relâmpago", Icon = AbIcon.Lightning, Color = BoltCyan,
                    Short = "Passa como um raio pelo adversário que você olha.",
                    Full = "Só com a bola e olhando para um adversário a até {BoltDribbleRange} m. Dá um impulso como um relâmpago, dribla o alvo e sai {BoltDribbleAhead} m na frente dele, que fica atordoado por {BoltDribbleStun} s. Recarga: {BoltDribbleCooldown} s." },
                new() { Name = "Swap", Icon = AbIcon.Swap, Color = BoltCyan,
                    Short = "Troca de lugar com o jogador para onde você olha.",
                    Full = "Troca de lugar na hora (ao apertar) com o jogador (aliado ou adversário) mais perto de onde você está olhando. Enquanto o Swap está pronto, quem vai ser trocado fica com o anel do chão bem mais claro (só na sua tela). Recarga: {BoltSwapCooldown} s." },
                new() { Name = "Arranha-Céu", Icon = AbIcon.Tower, Color = BoltCyan,
                    Short = "Pulo enorme com trovão: bloqueia muito e atordoa quem está perto.",
                    Full = "Só sem a bola. Dá na hora um pulo reto para cima, altíssimo, com um trovão. No ar a área de bloqueio é bem maior, e quem estava a até {BoltSkyStunRadius} m quando ele pulou fica atordoado por {BoltSkyStun} s. Recarga: {BoltSkyCooldown} s." },
                new() { Name = "Duelo", Icon = AbIcon.Duel, Color = new Color("ffd23f"),
                    Short = "Cutscene: duelo de skill checks contra um adversário.",
                    Full = "Ultimate, só com a bola. Escolha um adversário: começa uma cutscene com os dois se encarando e uma sequência de skill checks para os dois, cada vez mais rápida. Se o Bolt errar primeiro, o adversário rouba a bola e o jogo volta com ela na mão dele. Se o adversário errar, o Bolt passa por ele como um trovão e faz uma cesta de {BoltDuelPoints} pontos." },
            },
        },
        new()
        {
            Name = "Iron", Model = "res://assets/models/iron.glb", Color = IronSilver,
            FaceLabels = new[] { "", "Severo", "Focado", "Meio sorriso", "Bravo", "Grito de guerra", "Surpreso" },
            Tagline = "Uma muralha de aço. Lento, mas ninguém passa.",
            Abilities = new AbilityDef[]
            {
                new() { Name = "Muralha de Aço", Icon = AbIcon.Wall, Color = IronSilver,
                    Short = "Muralha na sua linha de 3 que barra a bola.",
                    Full = "Na hora, surge uma muralha prateada e meio transparente, bem alta, em cima da linha de 3 inteira do time do Iron. A bola bate nela como numa parede: arremessos e passes de fora não passam. Os jogadores atravessam, mas o adversário que passar por ela com a bola (no chão ou no ar) a deixa cair na hora e não pode pegá-la de volta por {IronWallDropLockout} s. Dura {IronWallDuration} s. Recarga: {IronWallCooldown} s." },
                new() { Name = "Pancada", Icon = AbIcon.Bonk, Color = IronSilver,
                    Short = "Pancada na cabeça de um adversário perto (sem a bola).",
                    Full = "Só com um adversário a até {IronBonkRange} m. O Iron dá uma pancada na cabeça do adversário mais perto de onde ele está olhando, que fica atordoado por {IronBonkStun} s. Não pode ser usada em quem está com a bola, mas o Iron pode usar estando com ela. Recarga: {IronBonkCooldown} s." },
                new() { Name = "Investida", Icon = AbIcon.Charge, Color = IronSilver,
                    Short = "Investida que acelera e atropela todo mundo no caminho.",
                    Full = "Só sem a bola. O Iron dispara para frente com um impulso e vai acelerando; a direção segue a câmera (os botões de movimento não fazem nada). Todo mundo no caminho (até aliados) é jogado para o lado e fica atordoado por {IronChargeStun} s; quem estava com a bola a deixa cair. Acaba ao bater numa parede (o Iron fica atordoado) ou depois de {IronChargeDuration} s (fica cansado) — nos dois casos ele fica parado por {IronChargeRecover} s. Recarga: {IronChargeCooldown} s." },
                new() { Name = "Indestrutível", Icon = AbIcon.Shield, Color = new Color("ffd23f"),
                    Short = "Imune a habilidades, mais rápido, estamina infinita.",
                    Full = "Ultimate. Por {IronUltDuration} s o Iron fica imune a todas as habilidades dos adversários, corre como alguém de {IronUltSpeedStars} estrelas de velocidade, tem estamina infinita e as recargas das habilidades dele correm {%IronUltCdSpeedup} mais rápido. Nem efeitos indiretos pegam nele (a Muralha de Aço adversária não barra a bola que ele lança, o Arranha-Céu não aumenta o bloqueio contra ele). Se ele bloquear um arremesso no pulo, pega a bola no ar (fica imune a roubo até pousar e poder driblar); bloquear uma enterrada não é falta: ele fica com a bola." },
            },
        },
        new()
        {
            Name = "One", Model = "res://assets/models/one.glb", Color = OnePurple,
            FaceLabels = new[] { "", "Calmo", "Focado", "Sorriso sinistro", "Olhar frio", "Risada sombria", "Surpreso" },
            Tagline = "Vem de outra dimensão. Ninguém vê de onde ele veio.",
            Abilities = new AbilityDef[]
            {
                new() { Name = "Realm", Icon = AbIcon.Realm, Color = OnePurple,
                    Short = "Entra numa dimensão sombria: invisível e intocável.",
                    Full = "Só sem a bola e fora de qualquer Realm (não dá durante um Domínio das Sombras). O One entra no Realm por até {OneRealmDuration} s: fica {%OneRealmSpeedBonus} mais rápido, os adversários não o veem (os aliados o veem transparente) e ele vê todo mundo transparente. Lá ele atravessa os jogadores e não pode interagir com eles nem com a bola (mas pode usar as outras habilidades). Sai na hora apertando a habilidade de novo, pulando ou tentando roubar (a ação acontece normalmente). Se sair dentro de alguém, é empurrado só o bastante para fora. Recarga: {OneRealmCooldown} s (depois de sair)." },
                new() { Name = "Abdução", Icon = AbIcon.Abduct, Color = OnePurple,
                    Short = "Puxa um jogador sem a bola para o Realm.",
                    Full = "O jogador sem a bola (aliado ou adversário) a até {OneAbductRange} m mais perto de onde você está olhando some em partículas roxas e fica {OneAbductDuration} s no Realm ({OneAbductDuration1v1} s se o time adversário tiver só 1 jogador): só pode andar e correr (nem habilidades), não interage com ninguém nem com a bola, não vê quem está fora e ninguém de fora o vê (nem os aliados dele). Só vale em quem não está em nenhum Realm (nem durante um Domínio das Sombras); o One pode usá-la estando no Realm. Se o One também estiver no Realm, os dois se veem normalmente. Outro One abduzido mantém o bônus de velocidade do Realm. Recarga: {OneAbductCooldown} s ({OneAbductCooldown1v1} s contra 1 adversário)." },
                new() { Name = "Maldição", Icon = AbIcon.Skull, Color = OnePurple,
                    Short = "Escolha um adversário: atributos menores e cestas valem menos.",
                    Full = "Abre um menu para escolher um adversário. Por {OneCurseDuration} s ele fica com {%OneCurseStatMult} de todos os atributos ({%OneCurseStatMult1v1} se o time dele tiver só 1 jogador) e as cestas dele valem {OneCursePointLoss} ponto(s) a menos (lance livre não muda). Todo mundo vê uma caveirinha em cima dele. Recarga: {OneCurseCooldown} s." },
                new() { Name = "Domínio das Sombras", Icon = AbIcon.Shadow, Color = new Color("ffd23f"),
                    Short = "A quadra vira o Realm: o time do One fica invisível para o adversário.",
                    Full = "Ultimate (não dá para usar estando no Realm nem com outro Domínio ativo). Uma cena mostra a quadra sendo engolida pelas sombras e todos vão para o Realm por {OneUltDuration} s (todo mundo continua jogando e interagindo normalmente). Só o One que usou fica {%OneRealmSpeedBonus} mais rápido. O time adversário fica com {%OneUltEnemyStatMult} dos atributos, as recargas e a barra da ultimate deles correm {%OneUltEnemyCdSlow} mais devagar e eles não veem nenhum jogador do time do One — só a bola (que parece flutuar na mão de quem eles não veem)." },
            },
        },
    };

    public static int Count => All.Length;
    public static int Clamp(int id) => id >= 0 && id < All.Length ? id : 0;
    public static Def Get(int id) => All[Clamp(id)];
    public static string FaceLabel(int id, int face) => Get(id).FaceLabels[face];

    /// <summary>Personagem dos bots/outros jogadores: alterna pela vaga.</summary>
    public static int ForSlot(int slot) => Clamp(slot % All.Length);

    public static bool Available(int id) => ResourceLoader.Exists(Get(id).Model);

    /// <summary>Estrelas (1–5) de cada atributo: Velocidade, Agilidade, Estamina, Precisão, Salto (vêm do Tuning).</summary>
    static readonly int[][] _starCache = new int[8][];
    static int _starRev = -1;
    static Tuning _starTuning;

    /// <summary>Não alterar o array devolvido (fica em cache até o Tuning mudar).</summary>
    public static int[] Stars(int id)
    {
        id = Clamp(id);
        if (_starRev != Tuning.Revision || !ReferenceEquals(_starTuning, Tuning.I)) { System.Array.Clear(_starCache); _starRev = Tuning.Revision; _starTuning = Tuning.I; }
        return _starCache[id] ??= ComputeStars(id);
    }

    static int[] ComputeStars(int id)
    {
        var t = Tuning.I;
        float[] v = Clamp(id) switch
        {
            Luck => new[] { t.LuckStarSpeed, t.LuckStarAgility, t.LuckStarStamina, t.LuckStarPrecision, t.LuckStarJump },
            Bolt => new[] { t.BoltStarSpeed, t.BoltStarAgility, t.BoltStarStamina, t.BoltStarPrecision, t.BoltStarJump },
            One => new[] { t.OneStarSpeed, t.OneStarAgility, t.OneStarStamina, t.OneStarPrecision, t.OneStarJump },
            Iron => new[] { t.IronStarSpeed, t.IronStarAgility, t.IronStarStamina, t.IronStarPrecision, t.IronStarJump },
            _ => new[] { t.KingStarSpeed, t.KingStarAgility, t.KingStarStamina, t.KingStarPrecision, t.KingStarJump },
        };
        var r = new int[5];
        for (int i = 0; i < 5; i++) r[i] = Mathf.Clamp(Mathf.RoundToInt(v[i]), 1, 5);
        return r;
    }

    /// <summary>Recarga (s) de uma habilidade (slot 0–2). A ultimate usa a barra de carga.</summary>
    public static float Cooldown(int id, int slot)
    {
        var t = Tuning.I;
        return (Clamp(id), slot) switch
        {
            (King, 0) => t.KingEyeCooldown, (King, 1) => t.KingPresenceCooldown, (King, 2) => t.KingDribbleCooldown,
            (Luck, 0) => t.LuckRiskyCooldown, (Luck, 1) => t.LuckBetCooldown, (Luck, 2) => t.LuckDashCooldown,
            (Bolt, 0) => t.BoltDribbleCooldown, (Bolt, 1) => t.BoltSwapCooldown, (Bolt, 2) => t.BoltSkyCooldown,
            (Iron, 0) => t.IronWallCooldown, (Iron, 1) => t.IronBonkCooldown, (Iron, 2) => t.IronChargeCooldown,
            (One, 0) => t.OneRealmCooldown, (One, 1) => t.OneAbductCooldown, (One, 2) => t.OneCurseCooldown,
            _ => 10f,
        };
    }

    /// <summary>Descrição completa com os valores atuais do Tuning ({Campo} = número, {%Campo} = porcentagem).</summary>
    public static string Describe(int id, int slot)
    {
        string s = Get(id).Abilities[slot].Full;
        return System.Text.RegularExpressions.Regex.Replace(s, @"\{(%?)(\w+)\}", m =>
        {
            var f = typeof(Tuning).GetField(m.Groups[2].Value);
            if (f == null) return m.Value;
            float v = (float)f.GetValue(Tuning.I);
            if (m.Groups[1].Value == "%") v *= 100f;
            string txt = Mathf.IsEqualApprox(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            return m.Groups[1].Value == "%" ? txt + "%" : txt;
        });
    }

    /// <summary>A habilidade abre o menu de escolher um adversário (Luck 2, ultimate do Bolt).</summary>
    /// <summary>v0.43: a Aposta Certeira do Luck pode escolher qualquer jogador (aliado, adversário ou ele mesmo).</summary>
    public static bool TargetsAnyone(int id, int slot) => Clamp(id) == Luck && slot == 1;

    public static bool NeedsTarget(int id, int slot) => (Clamp(id) == Luck && slot == 1) || (Clamp(id) == Bolt && slot == 3) || (Clamp(id) == One && slot == 2);

    /// <summary>v0.37: o Iron no modo Indestrutível não sofre habilidades de adversários (<paramref name="by"/>).</summary>
    /// <summary>v0.41: também quem está no Realm do One (sozinho ou abduzido) não sofre habilidades de quem está fora (aliado ou não);
    /// de dentro do Realm dá para usar habilidades em quem está fora.</summary>
    public static bool Immune(Player target, Player by) =>
        target != null && by != null && target != by && target.Ab != null &&
        ((target.Team != by.Team && target.Ab.UltT > 0 && target.CharacterId == Iron) || (target.Phased && !by.Phased));
}
