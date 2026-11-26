using Godot;

namespace AB;

/// <summary>Ícone desenhado (sem imagem) de cada habilidade — ver <see cref="AbilityIcons"/>.</summary>
public enum AbIcon { Eye, Aura, PerfectDribble, Crown, Dice, Target, Dash, Clover, Lightning, Swap, Tower, Duel }

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
    public const int King = 0, Luck = 1, Bolt = 2;
    public static readonly string[] StatNames = { "Velocidade", "Agilidade", "Estamina", "Precisão", "Salto" };

    public sealed class Def
    {
        public string Name, Model, Tagline;
        /// <summary>Texto da roda por id de expressão (índice 0 vazio). Os objetos do .glb têm sempre os mesmos nomes.</summary>
        public string[] FaceLabels;
        public Color Color;
        public AbilityDef[] Abilities; // 0..2 = habilidades (teclas 1, 2, 3), 3 = ultimate (4)
    }

    static readonly Color KingRed = new("e8313a"), LuckGreen = new("3ccf6e"), BoltCyan = new("3fd7ff");

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
                new() { Name = "Não me Decepciona", Icon = AbIcon.Target, Color = LuckGreen,
                    Short = "Escolha um adversário: a cesta dele vale para você.",
                    Full = "Abre um menu para escolher (com o mouse) um jogador adversário. Se ele fizer uma cesta nos próximos {LuckBetDuration} s, os pontos vão para o time do Luck e todo mundo é avisado. Só o time do Luck vê o trevo verde em cima do alvo — o outro time só descobre na cesta. Recarga: {LuckBetCooldown} s." },
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
    };

    public static int Count => All.Length;
    public static int Clamp(int id) => id >= 0 && id < All.Length ? id : 0;
    public static Def Get(int id) => All[Clamp(id)];
    public static string FaceLabel(int id, int face) => Get(id).FaceLabels[face];

    /// <summary>Personagem dos bots/outros jogadores: alterna pela vaga.</summary>
    public static int ForSlot(int slot) => Clamp(slot % All.Length);

    public static bool Available(int id) => ResourceLoader.Exists(Get(id).Model);

    /// <summary>Estrelas (1–5) de cada atributo: Velocidade, Agilidade, Estamina, Precisão, Salto (vêm do Tuning).</summary>
    static readonly int[][] _starCache = new int[3][];
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
    public static bool NeedsTarget(int id, int slot) => (Clamp(id) == Luck && slot == 1) || (Clamp(id) == Bolt && slot == 3);
}
