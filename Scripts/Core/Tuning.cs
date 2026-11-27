using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Godot;

namespace AB;

/// <summary>Metadados de um valor ajustável. Usado para gerar o artifact de tuning (tools/build_tuning_artifact).</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ParamAttribute : Attribute
{
    public string Section { get; }
    public string Label { get; }
    public string Desc { get; }
    public float Min { get; }
    public float Max { get; }
    public float Step { get; }
    /// <summary>v0.26: subgrupo dentro da seção (abas de personagem: "Estrelas", "Habilidades", "Avançado").</summary>
    public string Sub { get; set; } = "";
    public ParamAttribute(string section, string label, string desc, float min, float max, float step)
    { Section = section; Label = label; Desc = desc; Min = min; Max = max; Step = step; }
}

/// <summary>
/// TODOS os valores de gameplay ajustáveis. Lidos de tuning.json (ao lado do .exe na build,
/// na raiz do projeto no editor). F5 no jogo recarrega. Campos novos entram automaticamente no arquivo.
/// REGRA: todo campo novo precisa de [Param] e o artifact de tuning precisa ser regenerado.
/// </summary>
public sealed class Tuning
{
    public static Tuning I { get; private set; } = new();
    public static int Revision { get; private set; }

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    const string SMove = "Movimento";
    const string SStam = "Estamina";
    const string SCam = "Câmera";
    const string SBall = "Bola";
    const string SShot = "Arremesso";
    const string SCourt = "Quadra";
    const string SRules = "Regras";
    const string SPass = "Passe";
    const string SJump = "Salto e bloqueio";
    const string SSteal = "Roubo";
    const string SDrib = "Drible";
    const string SDunk = "Enterrada";
    const string SFree = "Lances livres";
    const string SDummy = "Bonecos de treino";
    const string SBots = "Bots";
    const string SRumble = "Vibração (controle)";
    const string SChar = "Personagem (visual)";
    const string SStars = "Estrelas (valores)";
    const string SUlt = "Habilidades e ultimate";
    const string SKing = "King";
    const string SLuck = "Luck";
    const string SBolt = "Bolt";
    const string SIron = "Iron";
    const string SOne = "One";
    const string Star = "Estrelas", Hab = "Habilidades", Adv = "Avançado";

    // ---------------- Movimento ----------------
    [Param(SMove, "Velocidade andando", "Velocidade máxima andando (m/s).", 1, 10, 0.1f)]
    public float WalkSpeed = 4.0f;
    [Param(SMove, "Velocidade correndo", "Velocidade máxima correndo (m/s).", 1, 14, 0.1f)]
    public float RunSpeed = 6.8f;
    [Param(SMove, "Aceleração", "Quão rápido chega na velocidade máxima (m/s²).", 1, 120, 1)]
    public float Acceleration = 28f;
    [Param(SMove, "Desaceleração", "Quão rápido para ao soltar o direcional (m/s²).", 1, 120, 1)]
    public float Deceleration = 34f;
    [Param(SMove, "Rapidez para virar", "Suavização da rotação do personagem (maior = vira mais rápido).", 1, 40, 0.5f)]
    public float TurnSpeed = 14f;
    [Param(SMove, "Multiplicador com a bola", "Velocidade de quem está com a bola (1 = igual).", 0.3f, 1.5f, 0.01f)]
    public float CarrierSpeedMult = 0.85f;
    [Param(SMove, "Gravidade dos jogadores", "Gravidade aplicada aos jogadores (m/s²).", 5, 60, 0.5f)]
    public float PlayerGravity = 22f;

    // ---------------- Estamina ----------------
    [Param(SStam, "Estamina máxima", "Quantidade total de estamina.", 10, 500, 1)]
    public float StaminaMax = 150f;
    [Param(SStam, "Gasto por segundo correndo", "Estamina gasta por segundo enquanto corre.", 1, 200, 1)]
    public float StaminaDrainPerSec = 28f;
    [Param(SStam, "Recarga por segundo", "Estamina recuperada por segundo sem correr.", 1, 300, 1)]
    public float StaminaRegenPerSec = 35f;
    [Param(SStam, "Atraso da recarga", "Segundos após parar de correr até começar a recarregar.", 0, 3, 0.05f)]
    public float StaminaRegenDelay = 0.35f;
    [Param(SStam, "Mínimo para começar a correr", "Estamina mínima para iniciar uma corrida.", 0, 100, 1)]
    public float StaminaMinToRun = 15f;

    // ---------------- Câmera ----------------
    [Param(SCam, "Distância", "Distância da câmera atrás do jogador (m).", 1.5f, 15, 0.1f)]
    public float CameraDistance = 5.2f;
    [Param(SCam, "Altura do pivô", "Altura do ponto que a câmera orbita (m acima dos pés).", 0.5f, 4, 0.05f)]
    public float CameraHeight = 2.1f;
    [Param(SCam, "Deslocamento lateral", "Câmera sobre o ombro (m, negativo = esquerda).", -2, 2, 0.05f)]
    public float CameraShoulderOffset = 0.8f;
    [Param(SCam, "Campo de visão (FOV)", "FOV vertical em graus.", 40, 110, 1)]
    public float CameraFov = 70f;
    [Param(SCam, "Inclinação mínima", "Quanto a câmera pode olhar para baixo (graus, negativo).", -89, 0, 1)]
    public float CameraPitchMinDeg = -55f;
    [Param(SCam, "Inclinação máxima", "Quanto a câmera pode olhar para cima (graus).", 0, 89, 1)]
    public float CameraPitchMaxDeg = 30f;
    [Param(SCam, "Suavização do seguimento", "Maior = câmera gruda mais rápido no jogador.", 1, 60, 0.5f)]
    public float CameraFollowSmooth = 18f;
    [Param(SCam, "Sensibilidade base do mouse", "Graus por pixel (multiplicado pela sensibilidade das Configurações).", 0.01f, 1, 0.01f)]
    public float MouseDegPerPixel = 0.12f;
    [Param(SCam, "Sensibilidade base do analógico", "Graus por segundo com o analógico no máximo (multiplicado pelas Configurações).", 30, 720, 5)]
    public float StickDegPerSec = 220f;
    [Param(SCam, "Curva do analógico", "Resposta do analógico direito: 1 = linear; maior = movimentos pequenos mais precisos (e o máximo continua igual).", 1, 3, 0.05f)]
    public float StickLookExponent = 1.6f;
    [Param(SCam, "Mira carregando o arremesso", "Multiplicador da sensibilidade (mouse e analógico) enquanto carrega o arremesso ou o passe — mira mais precisa.", 0.2f, 1, 0.01f)]
    public float ChargeLookMult = 0.65f;

    // ---------------- Bola ----------------
    [Param(SBall, "Raio da bola", "Raio da bola (m). Estilizada: maior que a real (0.12).", 0.08f, 0.5f, 0.005f)]
    public float BallRadius = 0.22f;
    [Param(SBall, "Gravidade da bola", "Gravidade da bola (m/s²). Maior = voo mais rápido e reto.", 2, 30, 0.1f)]
    public float BallGravity = 11f;
    [Param(SBall, "Quique no chão", "Fração da velocidade vertical mantida ao quicar no chão.", 0, 1, 0.01f)]
    public float FloorBounce = 0.72f;
    [Param(SBall, "Quique no aro", "Elasticidade do aro.", 0, 1, 0.01f)]
    public float RimBounce = 0.35f;
    [Param(SBall, "Aro amigável", "Quando a bola bate no aro por dentro, quanto ela perde a velocidade para fora (0 = física pura, 1 = quase sempre cai dentro).", 0, 1, 0.01f)]
    public float RimFriendliness = 0.6f;
    [Param(SBall, "Tabelinha: ajuda", "Arremesso que bate no quadrado da tabela é desviado um pouco em direção à cesta (0 = física pura, 1 = sempre entra). Só em arremessos na cesta adversária.", 0, 1, 0.01f)]
    public float BankAssist = 0.3f;
    [Param(SBall, "Quique na tabela", "Elasticidade da tabela.", 0, 1, 0.01f)]
    public float BoardBounce = 0.55f;
    [Param(SBall, "Quique nas paredes invisíveis", "Elasticidade dos limites da área de jogo.", 0, 1, 0.01f)]
    public float WallBounce = 0.55f;
    [Param(SBall, "Atrito no quique", "Fração da velocidade horizontal mantida a cada quique no chão.", 0, 1, 0.01f)]
    public float BounceFriction = 0.85f;
    [Param(SBall, "Atrito rolando", "Desaceleração da bola rolando no chão (m/s²).", 0, 20, 0.1f)]
    public float RollDecel = 1.5f;
    [Param(SBall, "Alcance para pegar", "Distância horizontal máxima para pegar a bola (m).", 0.2f, 2, 0.05f)]
    public float PickupRadius = 0.8f;
    [Param(SBall, "Altura para pegar", "Altura máxima da bola (acima dos pés) para pegar (m).", 0.5f, 4, 0.05f)]
    public float CatchHeight = 2.4f;
    [Param(SBall, "Período do quique", "Segundos por quique do drible automático.", 0.15f, 1.5f, 0.01f)]
    public float DribblePeriod = 0.5f;
    [Param(SBall, "Altura da mão no quique", "Altura em que a mão encontra a bola no quique (m).", 0.4f, 1.6f, 0.05f)]
    public float DribbleHandHeight = 0.6f;
    [Param(SBall, "Giro da bola na mão", "Quanto a bola gira enquanto está com o jogador (quicando/carregando), em fração do giro de rolar no chão. 0 = não gira (só gira arremessada, passada ou rolando/quicando solta); 1 = rola como no chão.", 0f, 1f, 0.05f)]
    public float HeldBallSpin = 0f;
    [Param(SBall, "Volume do quique (dB)", "Volume do som do drible automático. Mais negativo = mais baixo.", -40, 0, 1)]
    public float DribbleSoundDb = -18f;

    // ---------------- Arremesso ----------------
    [Param(SShot, "Distância no meio da tela", "Distância (m) do arremesso com a câmera no meio da tela. Suba a câmera para ir mais longe.", 0.5f, 15, 0.1f)]
    public float ShotRangeMin = 3f;
    [Param(SShot, "Alcance máximo", "Distância máxima (m) do arremesso, disponível desde o início (não depende de segurar). Para usar, suba a câmera.", 2, 40, 0.1f)]
    public float ShotRangeMax = 14f;
    [Param(SShot, "Arremesso automático", "Segurando por esse tempo, o jogador arremessa sozinho (s).", 0.5f, 10, 0.1f)]
    public float ShotAutoReleaseTime = 1.5f;
    [Param(SShot, "Tempo mínimo mirando", "Depois de apertar o arremesso, ele só sai depois deste tempo (s), mesmo soltando o botão antes (apertou e soltou rápido = fica mirando até completar). Vale também no pulo.", 0, 3, 0.05f)]
    public float ShotMinChargeTime = 0.5f;
    [Param(SShot, "Câmera: inclinação do meio", "Inclinação da câmera (graus) que corresponde ao 'meio da tela'.", -60, 30, 1)]
    public float ShotPitchBaseDeg = -12f;
    [Param(SShot, "Câmera: inclinação do alcance máximo", "Inclinação da câmera (graus) que pede o alcance máximo. Entre o meio e esta, a distância cresce.", -30, 60, 1)]
    public float ShotPitchTopDeg = 22f;
    [Param(SShot, "Câmera: inclinação do mais curto", "Inclinação da câmera (graus) que pede o arremesso mais curto.", -89, 0, 1)]
    public float ShotPitchBottomDeg = -50f;
    [Param(SShot, "Distância mais curta", "Distância (m) pedida com a câmera totalmente para baixo.", 0.2f, 10, 0.1f)]
    public float ShotRangeClosest = 1.0f;
    [Param(SShot, "Ângulo de perto", "Ângulo vertical (graus) em arremessos curtos. Mais alto = mais curva.", 15, 85, 0.5f)]
    public float ShotAngleNearDeg = 55f;
    [Param(SShot, "Ângulo de longe", "Ângulo vertical (graus) no alcance máximo. Mais baixo = bola mais rápida e reta.", 15, 85, 0.5f)]
    public float ShotAngleFarDeg = 44f;
    [Param(SShot, "Ângulo mínimo de entrada", "Inclinação mínima (graus) com que a bola desce na altura do aro. Maior = arcos mais altos, mais fácil entrar.", 10, 80, 0.5f)]
    public float ShotMinEntryAngleDeg = 42f;
    [Param(SShot, "Altura de soltura", "Altura de onde a bola sai no arremesso (m).", 1, 4, 0.05f)]
    public float ShotReleaseHeight = 2.35f;
    [Param(SShot, "Velocidade carregando", "Multiplicador de velocidade enquanto carrega o arremesso.", 0, 1, 0.01f)]
    public float ShotChargeMoveMult = 0.22f;
    [Param(SShot, "Assistência de direção", "Correção da direção quando a mira já está perto da cesta adversária (diminui suavemente até zero na borda do ângulo). Nunca na própria cesta.", 0, 1, 0.01f)]
    public float ShotAimAssist = 0.85f;
    [Param(SShot, "Ângulo da assistência", "A assistência de direção só age mirando dentro deste ângulo da cesta adversária (graus).", 0, 45, 0.5f)]
    public float ShotAimAssistMaxAngleDeg = 7f;
    [Param(SShot, "Assistência de distância: alcance", "Se a distância/altura do arremesso estiver a até isso (m) do ideal, ela é corrigida (suavemente, mais forte quanto mais perto).", 0, 5, 0.05f)]
    public float ShotDistAssistMeters = 1.0f;
    [Param(SShot, "Assistência de distância: força", "Quanto a distância é corrigida (0 = nada, 1 = acerto exato).", 0, 1, 0.01f)]
    public float ShotDistAssistStrength = 0.85f;
    [Param(SShot, "Bloqueio de repegar", "Segundos em que quem arremessou não pode pegar a própria bola.", 0, 2, 0.05f)]
    public float ShotPickupLockout = 0.7f;
    [Param(SShot, "Duração da trajetória mostrada", "Segundos de voo desenhados no indicador de trajetória.", 0.3f, 5, 0.1f)]
    public float TrajectoryPreviewTime = 3f;

    // ---------------- Quadra ----------------
    [Param(SCourt, "Altura do aro", "Altura do aro (m). Real = 3.05.", 1.5f, 7, 0.05f)]
    public float HoopHeight = 4.3f;
    [Param(SCourt, "Raio do aro", "Raio interno do aro (m). Real ≈ 0.23.", 0.15f, 1.2f, 0.01f)]
    public float RimRadius = 0.55f;
    [Param(SCourt, "Largura da tabela", "Largura da tabela (m).", 0.5f, 8, 0.05f)]
    public float BoardWidth = 3.3f;
    [Param(SCourt, "Altura da tabela", "Altura da tabela (m).", 0.5f, 5, 0.05f)]
    public float BoardHeight = 1.85f;
    [Param(SCourt, "Balanço da rede", "Força do balanço visual da rede quando a bola entra ou bate no aro (0 = parada).", 0, 3, 0.05f)]
    public float NetSwishStrength = 1f;
    [Param(SCourt, "Tremor do aro na enterrada", "Quanto o aro entorta para baixo numa enterrada certa (graus). Só visual.", 0, 25, 0.5f)]
    public float RimDunkShake = 9f;
    [Param(SCourt, "Tremor do aro na batida", "Força do tremor visual do aro quando a bola bate nele (0 = não treme).", 0, 3, 0.05f)]
    public float RimHitShake = 1f;
    [Param(SCourt, "Tolerância da cesta", "Quanto (fração do raio da bola) ela pode encostar no aro e ainda contar ao passar.", 0, 1, 0.05f)]
    public float ScoreTolerance = 0.5f;
    [Param(SCourt, "Distância do aro à linha de fundo", "Distância do centro do aro até a linha de fundo (m). FIBA: 1,575.", 0.5f, 5, 0.025f)]
    public float HoopInset = 1.575f;
    [Param(SCourt, "Pequena: comprimento", "Comprimento da quadra pequena (m).", 10, 150, 0.5f)]
    public float CourtSmallLength = 44f;
    [Param(SCourt, "Pequena: largura", "Largura da quadra pequena (m).", 6, 100, 0.5f)]
    public float CourtSmallWidth = 24f;
    [Param(SCourt, "Pequena: raio dos 3 pontos", "Raio da linha de 3 na quadra pequena (m).", 2, 20, 0.1f)]
    public float ThreePtSmall = 8.5f;
    [Param(SCourt, "Média: comprimento", "Comprimento da quadra média (m).", 10, 80, 0.5f)]
    public float CourtMediumLength = 58f;
    [Param(SCourt, "Média: largura", "Largura da quadra média (m).", 6, 100, 0.5f)]
    public float CourtMediumWidth = 32f;
    [Param(SCourt, "Média: raio dos 3 pontos", "Raio da linha de 3 na quadra média (m).", 2, 20, 0.1f)]
    public float ThreePtMedium = 9.5f;
    [Param(SCourt, "Grande: comprimento", "Comprimento da quadra grande (m).", 10, 80, 0.5f)]
    public float CourtLargeLength = 80f;
    [Param(SCourt, "Grande: largura", "Largura da quadra grande (m).", 6, 100, 0.5f)]
    public float CourtLargeWidth = 44f;
    [Param(SCourt, "Grande: raio dos 3 pontos", "Raio da linha de 3 na quadra grande (m).", 2, 20, 0.1f)]
    public float ThreePtLarge = 10.5f;
    [Param(SCourt, "Margem além das linhas", "Espaço jogável além das linhas antes das paredes invisíveis (não existe 'fora').", 0.2f, 6, 0.1f)]
    public float BoundsMargin = 1.5f;

    // ---------------- Regras ----------------
    [Param(SRules, "Contagem regressiva", "Segundos de contagem no início da partida (rounds seguintes começam direto).", 0, 10, 0.5f)]
    public float CountdownSeconds = 3f;
    [Param(SRules, "Pausa após cesta", "Segundos de comemoração antes de reiniciar o round.", 0, 6, 0.1f)]
    public float PostScoreDelay = 1.6f;
    [Param(SRules, "Pontos de dentro", "Pontos de uma cesta de dentro da linha de 3.", 1, 10, 1)]
    public float PointsInside = 2f;
    [Param(SRules, "Pontos de fora", "Pontos de uma cesta de fora da linha de 3.", 1, 10, 1)]
    public float PointsOutside = 3f;


    // ---------------- Passe ----------------
    [Param(SPass, "Alcance do passe rápido", "Distância máxima (m) até o aliado no passe rápido (toque).", 2, 60, 0.5f)]
    public float PassRange = 22f;
    [Param(SPass, "Tempo do toque rápido", "Soltando antes disso (s) = passe rápido para o aliado livre mais próximo; segurando mais = passe carregado na direção da câmera.", 0.05f, 1, 0.01f)]
    public float PassTapTime = 0.2f;

    [Param(SPass, "Memória do aperto do passe", "Apertou passe num instante em que não dava (pegando a bola, pousando, fim do drible): o aperto fica guardado por este tempo (s) e o passe sai assim que der.", 0, 1, 0.01f)]
    public float PassBufferTime = 0.25f;
    [Param(SPass, "Velocidade do passe rápido", "Velocidade horizontal (m/s) do passe rápido normal. Maior = mais rápido e mais reto.", 4, 50, 0.5f)]
    public float PassSpeed = 21f;
    [Param(SPass, "Altura de recepção", "Altura (m) do peito do recebedor, mirada pelo passe rápido.", 0.5f, 2.5f, 0.05f)]
    public float PassReceiveHeight = 1.3f;
    [Param(SPass, "Largura do corredor", "Um jogador a menos disso (m) da linha do passe rápido bloqueia o alvo.", 0.1f, 2, 0.05f)]
    public float PassLaneRadius = 0.9f;
    [Param(SPass, "Altura do passe alto", "Altura máxima (m) que o passe rápido alto atinge.", 3, 12, 0.1f)]
    public float HighPassApex = 5.5f;
    [Param(SPass, "Passe carregado: velocidade", "Velocidade (m/s) da bola no passe carregado normal (lançado na direção da câmera).", 4, 50, 0.5f)]
    public float AimPassSpeed = 18f;
    [Param(SPass, "Passe carregado: elevação", "Graus somados à inclinação da câmera no passe carregado normal (compensa a gravidade).", -20, 45, 0.5f)]
    public float AimPassLiftDeg = 6f;
    [Param(SPass, "Passe carregado alto: velocidade", "Velocidade (m/s) da bola no passe carregado alto.", 4, 50, 0.5f)]
    public float AimHighPassSpeed = 12.5f;
    [Param(SPass, "Passe carregado alto: elevação", "Graus somados à inclinação da câmera no passe carregado alto.", 0, 80, 0.5f)]
    public float AimHighPassLiftDeg = 32f;
    [Param(SPass, "Passe automático", "Segurando o passe por esse tempo (s), o jogador passa sozinho na direção da câmera.", 0.5f, 10, 0.1f)]
    public float PassAutoReleaseTime = 2.5f;
    [Param(SPass, "Velocidade carregando o passe", "Multiplicador de velocidade enquanto carrega o passe (não pode ter a bola roubada).", 0, 1, 0.01f)]
    public float PassChargeMoveMult = 0.22f;
    [Param(SPass, "Bloqueio de repegar", "Segundos em que quem passou não pode pegar a própria bola.", 0, 2, 0.05f)]
    public float PassLockout = 0.3f;

    // ---------------- Salto e bloqueio ----------------
    [Param(SJump, "Impulso do salto rápido", "Velocidade vertical (m/s) de um toque no salto. Altura ≈ v²/(2·gravidade).", 1, 20, 0.1f)]
    public float JumpVelMin = 6.5f;
    [Param(SJump, "Impulso do salto carregado", "Velocidade vertical (m/s) segurando o salto até o fim da carga.", 1, 20, 0.1f)]
    public float JumpVelMax = 7.8f;
    [Param(SJump, "Tempo de carga do salto", "Segundos segurando para o salto mais alto (parado enquanto carrega). O salto sai ao soltar o botão.", 0.05f, 2, 0.01f)]
    public float JumpChargeTime = 0.3f;
    [Param(SJump, "Controle de direção do salto", "Fração da velocidade de movimento levada para o salto (direção escolhida antes de pular).", 0, 1.5f, 0.01f)]
    public float JumpCarry = 0.82f;
    [Param(SJump, "Travamento ao pousar", "Segundos sem mover nem agir depois de pousar.", 0, 2, 0.05f)]
    public float LandLockTime = 0.4f;
    [Param(SJump, "Raio do bloqueio", "Alcance horizontal (m) do corpo/braços para bloquear no ar.", 0.2f, 2, 0.05f)]
    public float BlockRadius = 0.6f;
    [Param(SJump, "Altura do bloqueio", "Até que altura (m acima dos pés) os braços bloqueiam.", 1, 4, 0.05f)]
    public float BlockReach = 2.6f;
    [Param(SJump, "Velocidade do rebote do bloqueio", "Velocidade (m/s) com que a bola quica no bloqueador.", 0.5f, 15, 0.1f)]
    public float BlockBounceSpeed = 3.5f;

    // ---------------- Roubo ----------------
    [Param(SSteal, "Velocidade do bote", "Velocidade (m/s) do impulso para frente ao tentar roubar.", 1, 30, 0.5f)]
    public float StealDashSpeed = 10f;
    [Param(SSteal, "Duração do bote", "Duração (s) do impulso do roubo.", 0.05f, 1, 0.01f)]
    public float StealDashTime = 0.25f;
    [Param(SSteal, "Alcance do roubo", "Distância (m) até quem está com a bola para acertar o roubo.", 0.3f, 3, 0.05f)]
    public float StealReach = 1.1f;
    [Param(SSteal, "Ângulo do roubo", "O alvo precisa estar dentro deste ângulo (graus) à frente.", 10, 180, 1)]
    public float StealAngleDeg = 70f;
    [Param(SSteal, "Penalidade por errar", "Segundos lento (sem correr, pular ou roubar) após errar o roubo.", 0, 6, 0.1f)]
    public float StealFailTime = 1.8f;
    [Param(SSteal, "Velocidade na penalidade", "Multiplicador de velocidade durante a penalidade.", 0, 1, 0.01f)]
    public float StealFailSpeedMult = 0.25f;

    // ---------------- Drible ----------------
    [Param(SDrib, "Cargas de drible", "Quantas cargas de drible o jogador tem.", 1, 6, 1)]
    public float DribbleCharges = 2f;
    [Param(SDrib, "Duração do drible", "Segundos de drible (imune a roubo).", 0.2f, 4, 0.05f)]
    public float DribbleDuration = 0.9f;
    [Param(SDrib, "Velocidade driblando", "Velocidade durante o drible como fração da velocidade ANDANDO (não corre driblando; o multiplicador de quem tem a bola não se aplica).", 0.1f, 1.5f, 0.01f)]
    public float DribbleSpeedMult = 0.82f;
    [Param(SDrib, "Recarga por carga", "Segundos para recuperar 1 carga (conta sempre que falta alguma).", 1, 60, 0.5f)]
    public float DribbleRecharge = 8f;
    [Param(SDrib, "Quique driblando", "Multiplicador da velocidade do quique durante o drible (maior = quica mais rápido).", 1, 5, 0.1f)]
    public float DribbleBounceMult = 2.6f;
    [Param(SDrib, "Agachamento driblando", "Quanto o boneco agacha durante o drible (0 = nada, 1 = como carregando o salto).", 0, 1.3f, 0.05f)]
    public float DribbleCrouch = 0.8f;

    // ---------------- Enterrada ----------------
    [Param(SDunk, "Distância para enterrar", "Com a bola, indo (ou virado) na direção da cesta adversária e a até esta distância (m) do aro, o salto vira enterrada. Não precisa correr.", 0.5f, 8, 0.1f)]
    public float DunkRange = 7f;
    [Param(SDunk, "Ângulo da corrida", "O movimento (ou, parado, a frente do jogador) precisa estar dentro deste ângulo (graus) da direção da cesta para enterrar.", 5, 90, 1)]
    public float DunkMaxAngleDeg = 45f;
    [Param(SDunk, "Duração do voo", "Segundos do salto da enterrada até tocar a cesta (momento do skill check).", 0.3f, 2, 0.05f)]
    public float DunkFlightTime = 0.8f;
    [Param(SDunk, "Janela do skill check", "Duração (s) da janela certa para apertar o arremesso (Espaço não vale), centrada no toque na cesta. Vale também para os lances livres. Menor = mais difícil.", 0.03f, 0.6f, 0.01f)]
    public float DunkWindow = 0.05f;
    [Param(SDunk, "Resultado do skill check na tela", "Segundos que o skill check fica na tela depois de acertar ou errar, com o ponteiro parado onde foi apertado (verde = certo, vermelho = errado). Vale também para os lances livres. Só visual.", 0, 3, 0.05f)]
    public float SkillResultHold = 0.75f;
    [Param(SDunk, "Variação da duração do voo", "Cada enterrada (e cada lance livre) dura a duração ± até este valor (s), sorteado: o ponteiro do skill check muda de velocidade e não dá para decorar.", 0, 0.6f, 0.01f)]
    public float DunkFlightJitter = 0.2f;
    [Param(SDunk, "Alcance da mão", "Altura (m) da bola acima dos pés com os braços esticados na enterrada.", 1.5f, 3.5f, 0.05f)]
    public float DunkHandReach = 2.3f;
    [Param(SDunk, "Atordoamento ao errar", "Segundos atordoado (sem mover nem agir) depois de errar a enterrada.", 0, 6, 0.1f)]
    public float DunkStunTime = 2f;
    [Param(SDunk, "Pendurado no aro", "Segundos que o jogador fica pendurado no aro depois de uma enterrada certa, antes de cair (v0.21).", 0, 1.5f, 0.05f)]
    public float DunkHangTime = 0.35f;
    [Param(SDunk, "Tremor da câmera na enterrada", "Força do tremor da câmera quando uma enterrada entra perto de você (0 = sem tremor). Só visual.", 0, 2, 0.05f)]
    public float DunkCameraShake = 0.6f;
    [Param(SDunk, "Efeito de impacto", "Tamanho do clarão, da onda e das faíscas da enterrada (0 = sem efeito). Só visual.", 0, 2.5f, 0.05f)]
    public float DunkImpactFx = 1f;
    [Param(SDunk, "Pausa da falta", "Segundos entre a falta (enterrada bloqueada) e o reinício do round.", 0, 4, 0.1f)]
    public float FoulDelay = 1.5f;

    // ---------------- Lances livres ----------------
    [Param(SFree, "Pontos por lance livre", "Pontos de cada lance livre convertido (a falta dá 2 lances).", 1, 5, 1)]
    public float FreeThrowPoints = 1f;
    [Param(SFree, "Distância da linha", "Distância (m) do aro até onde o jogador cobra os lances livres.", 2, 15, 0.1f)]
    public float FreeThrowDistance = 5.5f;
    [Param(SFree, "Duração do skill check", "Segundos até o ponteiro chegar na zona certa (± a variação da enterrada). A janela é a mesma da enterrada.", 0.3f, 3, 0.05f)]
    public float FreeThrowSkillTime = 0.8f;
    [Param(SFree, "Preparação", "Segundos parado antes do ponteiro começar a girar em cada lance livre.", 0, 3, 0.05f)]
    public float FreeThrowPrep = 0.9f;
    [Param(SFree, "Pausa entre os lances", "Segundos entre o 1º e o 2º lance livre (depois de errar ou da bola cair na cesta).", 0, 3, 0.05f)]
    public float FreeThrowGap = 0.8f;
    [Param(SFree, "Tempo de voo da bola", "Segundos de voo da bola no lance livre convertido (maior = arco mais alto).", 0.5f, 2.5f, 0.05f)]
    public float FreeThrowFlightTime = 1.15f;

    // ---------------- Personagem (visual) ----------------
    [Param(SChar, "Tamanho do personagem", "Escala do modelo (o Rei tem 1,4 m na concept; 1,3 = ~1,8 m, a altura da colisão). Só visual.", 0.6f, 2, 0.05f)]
    public float CharacterScale = 1.3f;
    [Param(SChar, "Passada andando", "Metros andados a cada ciclo de dois passos andando (maior = passos mais lentos e longos).", 0.4f, 3, 0.05f)]
    public float AnimWalkStride = 1.5f;
    [Param(SChar, "Passada correndo", "Metros por ciclo de dois passos correndo.", 0.4f, 4, 0.05f)]
    public float AnimRunStride = 2.3f;
    [Param(SChar, "Rapidez das transições", "Quão rápido uma animação passa para a outra (maior = mais seco).", 2, 40, 0.5f)]
    public float AnimBlendSpeed = 14f;
    [Param(SChar, "Rapidez das mãos", "Quão rápido as mãos flutuantes seguem o alvo (maior = mais grudadas).", 4, 80, 1)]
    public float HandFollow = 26f;
    [Param(SChar, "Contato da mão no quique", "Quantos metros a mão acompanha a bola no alto do quique (empurra/amortece). O resto do caminho a bola vai sozinha no ar.", 0, 0.3f, 0.005f)]
    public float DribbleHandContact = 0.06f;
    [Param(SChar, "Cara brava ao sofrer ponto", "Segundos que o personagem fica bravo sozinho quando o time adversário pontua (a expressão da roda tem prioridade). 0 = desliga.", 0, 10, 0.5f)]
    public float ConcedeAngryTime = 3f;
    [Param(SChar, "Duração da expressão", "Segundos que a expressão escolhida na roda fica no rosto (ou até escolher outra).", 1, 20, 0.5f)]
    public float EmoteDuration = 5f;


    // ---------------- Estrelas (v0.26) ----------------
    // Cada atributo vale 1 + (estrelas − 2,5) × passo. O jogo de antes (sem personagens) fica entre 2 e 3 estrelas.
    [Param(SStars, "Velocidade: % por estrela", "Quanto cada estrela de Velocidade muda a velocidade andando e correndo (%). Valor final = 1 + (estrelas − 2,5) × este %.", 0, 20, 0.5f)]
    public float StarSpeedStep = 7f;
    [Param(SStars, "Agilidade: % por estrela", "Velocidade do bote, penalidade por errar roubo (menor = melhor) e velocidade driblando (%). Cargas de drible: 1★ = −1, 2–3★ = normal, 4–5★ = +1.", 0, 20, 0.5f)]
    public float StarAgilityStep = 5.5f;
    [Param(SStars, "Estamina: % por estrela", "Estamina máxima e recarga de estamina (%).", 0, 25, 0.5f)]
    public float StarStaminaStep = 9f;
    [Param(SStars, "Precisão: % por estrela", "Alcance do passe rápido, velocidade do passe e altura do passe alto (%). O alcance do arremesso usa o valor abaixo.", 0, 20, 0.5f)]
    public float StarPrecisionStep = 8f;
    [Param(SStars, "Alcance do arremesso: % por estrela", "Só o alcance máximo do arremesso por estrela de Precisão (%). Menor que o da Precisão para as estrelas não mudarem tanto a distância (v0.32).", 0, 20, 0.5f)]
    public float StarShotRangeStep = 3.5f;
    [Param(SStars, "Salto: % por estrela", "Impulso do salto, travamento ao pousar (menor = melhor), tempo de carga do salto (menor = melhor) e distância para enterrar (%).", 0, 20, 0.5f)]
    public float StarJumpStep = 6f;

    // ---------------- Habilidades e ultimate (geral) ----------------
    [Param(SUlt, "Ultimate: tempo para encher sozinha", "Segundos de jogo para a barra da ultimate encher sem bônus nenhum (enche bem devagar). Não enche durante cutscenes nem com a própria ultimate ativa.", 30, 900, 5)]
    public float UltChargeTime = 205f;
    [Param(SUlt, "Ultimate: bônus por bloqueio", "Fração da barra ganha ao dar um toco.", 0, 1, 0.01f)]
    public float UltBonusBlock = 0.06f;
    [Param(SUlt, "Ultimate: bônus por passe", "Fração da barra ganha quando um passe seu chega no aliado.", 0, 1, 0.005f)]
    public float UltBonusPass = 0.025f;
    [Param(SUlt, "Ultimate: bônus por roubo", "Fração da barra ganha ao roubar a bola.", 0, 1, 0.01f)]
    public float UltBonusSteal = 0.06f;
    [Param(SUlt, "Ultimate: bônus por cesta", "Fração da barra ganha ao fazer uma cesta (o maior bônus).", 0, 1, 0.01f)]
    public float UltBonusScore = 0.12f;
    [Param(SUlt, "Ultimate: bônus por enterrada", "Fração da barra ganha ao fazer uma cesta de enterrada (no lugar do bônus por cesta; deve ser maior).", 0, 1, 0.01f)]
    public float UltBonusDunk = 0.17f;
    [Param(SUlt, "Skill check da cutscene: preparação", "Segundos com o anel na tela antes do ponteiro começar a girar nos skill checks das cutscenes.", 0, 2, 0.05f)]
    public float CutCheckPrep = 0.35f;
    [Param(SUlt, "Pose da ultimate: duração", "Segundos da cena curta ao usar uma ultimate que não é cutscene (ex.: Sorte Infinita = o Luck joga os dados). O jogo fica congelado durante ela. Menos de ~1,6 s corta a cena do Luck.", 0.5f, 4, 0.1f)]
    public float UltPoseDuration = 2.1f;

    // ---------------- King ----------------
    [Param(SKing, "Velocidade", "Estrelas de Velocidade do King.", 1, 5, 1, Sub = Star)] public float KingStarSpeed = 3f;
    [Param(SKing, "Agilidade", "Estrelas de Agilidade do King.", 1, 5, 1, Sub = Star)] public float KingStarAgility = 3f;
    [Param(SKing, "Estamina", "Estrelas de Estamina do King.", 1, 5, 1, Sub = Star)] public float KingStarStamina = 2f;
    [Param(SKing, "Precisão", "Estrelas de Precisão do King.", 1, 5, 1, Sub = Star)] public float KingStarPrecision = 5f;
    [Param(SKing, "Salto", "Estrelas de Salto do King.", 1, 5, 1, Sub = Star)] public float KingStarJump = 2f;
    [Param(SKing, "(1) Olho do Rei: duração", "Segundos que o Olho do Rei fica ativo esperando o próximo passe/arremesso.", 1, 20, 0.5f, Sub = Hab)] public float KingEyeDuration = 7f;
    [Param(SKing, "(1) Olho do Rei: recarga", "Segundos de recarga do Olho do Rei.", 1, 90, 0.5f, Sub = Hab)] public float KingEyeCooldown = 25f;
    [Param(SKing, "(1) Olho do Rei: multiplicador de alcance", "Multiplica o alcance do arremesso e do passe rápido (e a velocidade do passe carregado) quando o efeito é gasto.", 1, 4, 0.05f, Sub = Hab)] public float KingEyeRangeMult = 1.6f;
    [Param(SKing, "(1) Olho do Rei: alcance máximo (fração da quadra)", "O arremesso com o Olho do Rei nunca passa desta fração do comprimento da quadra (0,5 = meia quadra).", 0.2f, 1, 0.05f, Sub = Hab)] public float KingEyeMaxCourtFrac = 0.5f;
    [Param(SKing, "(2) Presença Implacável: duração", "Segundos que a Presença Implacável fica ativa.", 1, 20, 0.5f, Sub = Hab)] public float KingPresenceDuration = 5f;
    [Param(SKing, "(2) Presença Implacável: raio", "Adversário a até esta distância (m) do King que soltar um arremesso erra com certeza.", 1, 20, 0.5f, Sub = Hab)] public float KingPresenceRadius = 7f;
    [Param(SKing, "(2) Presença Implacável: recarga", "Segundos de recarga da Presença Implacável.", 1, 90, 0.5f, Sub = Hab)] public float KingPresenceCooldown = 35f;
    [Param(SKing, "(3) Drible Perfeito: duração extra", "Segundos a mais que o drible normal.", 0, 3, 0.05f, Sub = Hab)] public float KingDribbleExtra = 0.5f;
    [Param(SKing, "(3) Drible Perfeito: atordoamento", "Segundos atordoado (caído) de quem tentar roubar durante o Drible Perfeito.", 0, 10, 0.25f, Sub = Hab)] public float KingDribbleStun = 4f;
    [Param(SKing, "(3) Drible Perfeito: recarga", "Segundos de recarga do Drible Perfeito.", 1, 90, 0.5f, Sub = Hab)] public float KingDribbleCooldown = 15f;
    [Param(SKing, "Ult: pontos da cesta", "Pontos da cesta da ultimate Sua Derrota é Absoluta.", 1, 20, 1, Sub = Hab)] public float KingUltPoints = 4f;
    [Param(SKing, "Ult: 1º skill check (duração)", "Segundos até o ponteiro chegar na zona no 1º skill check (drible).", 0.3f, 3, 0.05f, Sub = Hab)] public float KingUltCheck1Time = 0.85f;
    [Param(SKing, "Ult: 1º skill check (janela)", "Janela certa (s) do 1º skill check.", 0.02f, 0.5f, 0.005f, Sub = Hab)] public float KingUltCheck1Window = 0.08f;
    [Param(SKing, "Ult: skill check do bloqueio (duração)", "Segundos até o ponteiro chegar na zona no skill check do meio (quando o adversário pula para bloquear). Errou = o King arremessa e toma o toco.", 0.3f, 3, 0.05f, Sub = Hab)] public float KingUltCheckBlockTime = 0.72f;
    [Param(SKing, "Ult: skill check do bloqueio (janela)", "Janela certa (s) do skill check do bloqueio (entre o 1º e o último em dificuldade).", 0.02f, 0.5f, 0.005f, Sub = Hab)] public float KingUltCheckBlockWindow = 0.065f;
    [Param(SKing, "Ult: último skill check (duração)", "Segundos até o ponteiro chegar na zona no último skill check (arremesso). Menor = mais rápido.", 0.3f, 3, 0.05f, Sub = Hab)] public float KingUltCheck2Time = 0.55f;
    [Param(SKing, "Ult: último skill check (janela)", "Janela certa (s) do último skill check (o mais difícil).", 0.02f, 0.5f, 0.005f, Sub = Hab)] public float KingUltCheck2Window = 0.04f;
    [Param(SKing, "Presença: aviso na tela", "Segundos que a borda da tela de quem teve o arremesso afetado fica vermelha.", 0, 3, 0.05f, Sub = Adv)] public float KingPresenceFlash = 0.7f;
    [Param(SKing, "Presença: desvio do arremesso", "Quanto (m) o arremesso afetado é desviado para bater no aro e sair.", 0.2f, 3, 0.05f, Sub = Adv)] public float KingPresenceMissOffset = 0.75f;

    // ---------------- Luck ----------------
    [Param(SLuck, "Velocidade", "Estrelas de Velocidade do Luck.", 1, 5, 1, Sub = Star)] public float LuckStarSpeed = 4f;
    [Param(SLuck, "Agilidade", "Estrelas de Agilidade do Luck.", 1, 5, 1, Sub = Star)] public float LuckStarAgility = 2f;
    [Param(SLuck, "Estamina", "Estrelas de Estamina do Luck.", 1, 5, 1, Sub = Star)] public float LuckStarStamina = 5f;
    [Param(SLuck, "Precisão", "Estrelas de Precisão do Luck.", 1, 5, 1, Sub = Star)] public float LuckStarPrecision = 2f;
    [Param(SLuck, "Salto", "Estrelas de Salto do Luck.", 1, 5, 1, Sub = Star)] public float LuckStarJump = 2f;
    [Param(SLuck, "(1) Arremesso Arriscado: chance de errar", "Chance (0–1) do Arremesso Arriscado errar.", 0, 1, 0.01f, Sub = Hab)] public float LuckRiskyMissChance = 0.3f;
    [Param(SLuck, "(1) Arremesso Arriscado: recarga", "Segundos de recarga do Arremesso Arriscado.", 1, 90, 0.5f, Sub = Hab)] public float LuckRiskyCooldown = 35f;
    [Param(SLuck, "(2) Aposta Certeira: chance de dobrar (aliado)", "Chance (0–1) da próxima cesta valer o dobro quando o alvo é aliado (ou o próprio Luck). Senão, a cesta não vale nada.", 0, 1, 0.01f, Sub = Hab)] public float LuckBetAllyDoubleChance = 0.8f;
    [Param(SLuck, "(2) Aposta Certeira: chance de dobrar (adversário)", "Chance (0–1) da próxima cesta valer o dobro quando o alvo é adversário. Senão, a cesta não vale nada.", 0, 1, 0.01f, Sub = Hab)] public float LuckBetEnemyDoubleChance = 0.2f;
    [Param(SLuck, "(2) Aposta Certeira: recarga", "Segundos de recarga da Aposta Certeira.", 1, 120, 0.5f, Sub = Hab)] public float LuckBetCooldown = 60f;
    [Param(SLuck, "(3) Impulso Descuidado: chance de dar certo", "Chance (0–1) de dar o impulso. Se não der, o Luck tropeça.", 0, 1, 0.01f, Sub = Hab)] public float LuckDashChance = 0.85f;
    [Param(SLuck, "(3) Impulso Descuidado: atordoamento do tropeço", "Segundos atordoado quando o Luck tropeça.", 0, 10, 0.25f, Sub = Hab)] public float LuckTripStun = 2.5f;
    [Param(SLuck, "(3) Impulso Descuidado: recarga", "Segundos de recarga do Impulso Descuidado.", 1, 60, 0.5f, Sub = Hab)] public float LuckDashCooldown = 15f;
    [Param(SLuck, "Ult: duração", "Segundos do modo apostador (Sorte Infinita).", 2, 40, 0.5f, Sub = Hab)] public float LuckModeDuration = 12f;
    [Param(SLuck, "Ult: bônus nos atributos", "Bônus (fração) em todos os valores dos atributos de estrela no modo apostador. 0,15 = +15%.", 0, 1, 0.01f, Sub = Hab)] public float LuckModeBonus = 0.15f;
    [Param(SLuck, "Ult: pontos a mais por cesta", "Pontos a mais em cada cesta do Luck no modo apostador (cada aposta ganha soma mais este valor).", 0, 5, 1, Sub = Hab)] public float LuckModePoints = 1f;
    [Param(SLuck, "Ult: chance de perder a 1ª aposta", "Chance (0–1) de perder ao apostar (apertar a ultimate de novo) pela 1ª vez.", 0, 1, 0.01f, Sub = Hab)] public float LuckGambleLoseChance = 0.5f;
    [Param(SLuck, "Ult: aumento da chance de perder", "Quanto a chance de perder sobe a cada aposta depois da 1ª.", 0, 1, 0.01f, Sub = Hab)] public float LuckGambleLoseStep = 0.15f;
    [Param(SLuck, "Ult: chance máxima de perder", "A chance de perder nunca passa disso.", 0, 1, 0.01f, Sub = Hab)] public float LuckGambleLoseMax = 0.95f;
    [Param(SLuck, "Ult: tempo ganho na aposta", "Segundos somados à duração do modo a cada aposta ganha.", 0, 15, 0.5f, Sub = Hab)] public float LuckGambleExtraTime = 5f;
    [Param(SLuck, "Ult: atordoamento ao perder", "Segundos atordoado quando perde a aposta (o modo acaba na hora).", 0, 10, 0.25f, Sub = Hab)] public float LuckGambleStun = 3f;
    [Param(SLuck, "Impulso: velocidade", "Velocidade (m/s) do Impulso Descuidado.", 5, 40, 0.5f, Sub = Adv)] public float LuckDashSpeed = 17f;
    [Param(SLuck, "Impulso: duração", "Duração (s) do Impulso Descuidado.", 0.05f, 1.5f, 0.01f, Sub = Adv)] public float LuckDashTime = 0.35f;
    [Param(SLuck, "Impulso: alcance do roubo", "Distância (m) até quem tem a bola para roubar passando por ele no impulso.", 0.3f, 3, 0.05f, Sub = Adv)] public float LuckDashStealReach = 1.2f;
    [Param(SLuck, "Ult: bônus máximo", "O bônus nos atributos dobra a cada aposta ganha, mas nunca passa disso (1 = +100%).", 0.1f, 3, 0.05f, Sub = Adv)] public float LuckModeBonusMax = 1f;
    [Param(SLuck, "Arriscado: desvio quando erra", "Quanto (m) o Arremesso Arriscado é desviado quando erra.", 0.2f, 3, 0.05f, Sub = Adv)] public float LuckRiskyMissOffset = 0.75f;

    // ---------------- Bolt ----------------
    [Param(SBolt, "Velocidade", "Estrelas de Velocidade do Bolt.", 1, 5, 1, Sub = Star)] public float BoltStarSpeed = 5f;
    [Param(SBolt, "Agilidade", "Estrelas de Agilidade do Bolt.", 1, 5, 1, Sub = Star)] public float BoltStarAgility = 4f;
    [Param(SBolt, "Estamina", "Estrelas de Estamina do Bolt.", 1, 5, 1, Sub = Star)] public float BoltStarStamina = 4f;
    [Param(SBolt, "Precisão", "Estrelas de Precisão do Bolt.", 1, 5, 1, Sub = Star)] public float BoltStarPrecision = 1f;
    [Param(SBolt, "Salto", "Estrelas de Salto do Bolt.", 1, 5, 1, Sub = Star)] public float BoltStarJump = 3f;
    [Param(SBolt, "(1) Drible Relâmpago: alcance", "Distância máxima (m) até o adversário que o Bolt está olhando.", 1, 15, 0.5f, Sub = Hab)] public float BoltDribbleRange = 5f;
    [Param(SBolt, "(1) Drible Relâmpago: distância na frente", "Quantos metros o Bolt sai na frente do adversário driblado.", 0.5f, 10, 0.25f, Sub = Hab)] public float BoltDribbleAhead = 3f;
    [Param(SBolt, "(1) Drible Relâmpago: atordoamento", "Segundos que o adversário driblado fica atordoado.", 0, 10, 0.25f, Sub = Hab)] public float BoltDribbleStun = 1f;
    [Param(SBolt, "(1) Drible Relâmpago: recarga", "Segundos de recarga do Drible Relâmpago.", 1, 90, 0.5f, Sub = Hab)] public float BoltDribbleCooldown = 30f;
    [Param(SBolt, "(2) Swap: recarga", "Segundos de recarga do Swap.", 1, 90, 0.5f, Sub = Hab)] public float BoltSwapCooldown = 45f;
    [Param(SBolt, "(3) Arranha-Céu: impulso", "Velocidade vertical (m/s) do pulo. Altura ≈ v²/(2·gravidade dos jogadores).", 5, 40, 0.5f, Sub = Hab)] public float BoltSkyJumpVel = 16f;
    [Param(SBolt, "(3) Arranha-Céu: raio do atordoamento", "Adversários a até esta distância (m) quando ele pula ficam atordoados.", 0, 10, 0.25f, Sub = Hab)] public float BoltSkyStunRadius = 3.25f;
    [Param(SBolt, "(3) Arranha-Céu: atordoamento", "Segundos atordoado de quem estava perto.", 0, 10, 0.25f, Sub = Hab)] public float BoltSkyStun = 2f;
    [Param(SBolt, "(3) Arranha-Céu: recarga", "Segundos de recarga do Arranha-Céu.", 1, 90, 0.5f, Sub = Hab)] public float BoltSkyCooldown = 40f;
    [Param(SBolt, "Ult: pontos da cesta", "Pontos da cesta quando o Bolt vence o Duelo.", 1, 20, 1, Sub = Hab)] public float BoltDuelPoints = 6f;
    [Param(SBolt, "Ult: 1º skill check (duração)", "Segundos até o ponteiro chegar na zona no 1º skill check do Duelo.", 0.2f, 3, 0.05f, Sub = Hab)] public float BoltDuelStartTime = 1.0f;
    [Param(SBolt, "Ult: aceleração por segundo", "Quanto os skill checks ficam mais rápidos a cada segundo de duelo (0,18 = 18% por segundo).", 0, 2, 0.01f, Sub = Hab)] public float BoltDuelSpeedup = 0.18f;
    [Param(SBolt, "Ult: duração mínima do skill check", "O skill check do Duelo nunca fica mais rápido que isso (s).", 0.1f, 2, 0.01f, Sub = Hab)] public float BoltDuelMinTime = 0.25f;
    [Param(SBolt, "Ult: janela do skill check", "Janela certa (s) do 1º skill check do Duelo (vale para os dois). v0.28: encolhe junto com a duração quando acelera, então a zona fica do mesmo tamanho na tela.", 0.02f, 0.5f, 0.005f, Sub = Hab)] public float BoltDuelWindow = 0.085f;
    [Param(SBolt, "Ult: duração máxima entre bots", "Quando os dois do Duelo são bots, a troca de skill checks dura no máximo isso (s); depois um dos dois erra (50%).", 1, 20, 0.5f, Sub = Adv)] public float BotDuelMaxTime = 5f;
    [Param(SBolt, "Drible Relâmpago: ângulo do olhar", "O adversário precisa estar a até este ângulo (graus) de onde o Bolt olha (câmera).", 5, 90, 1, Sub = Adv)] public float BoltDribbleAngle = 35f;
    [Param(SBolt, "Drible Relâmpago: duração do impulso", "Segundos do impulso do Drible Relâmpago.", 0.05f, 1, 0.01f, Sub = Adv)] public float BoltDribbleTime = 0.18f;
    [Param(SBolt, "Swap: ângulo do olhar", "Jogador a até este ângulo (graus) de onde o Bolt olha pode ser trocado (o mais perto do centro).", 5, 90, 1, Sub = Adv)] public float BoltSwapAngle = 25f;
    [Param(SBolt, "Swap: alcance", "Distância máxima (m) do Swap.", 3, 120, 1, Sub = Adv)] public float BoltSwapRange = 45f;
    [Param(SBolt, "Arranha-Céu: raio do bloqueio", "Alcance horizontal (m) do bloqueio no Arranha-Céu (o normal é o Raio do bloqueio).", 0.3f, 5, 0.05f, Sub = Adv)] public float BoltSkyBlockRadius = 1.45f;

    // ---------------- Iron (v0.37) ----------------
    [Param(SIron, "Velocidade", "Estrelas de Velocidade do Iron.", 1, 5, 1, Sub = Star)] public float IronStarSpeed = 1f;
    [Param(SIron, "Agilidade", "Estrelas de Agilidade do Iron.", 1, 5, 1, Sub = Star)] public float IronStarAgility = 2f;
    [Param(SIron, "Estamina", "Estrelas de Estamina do Iron.", 1, 5, 1, Sub = Star)] public float IronStarStamina = 4f;
    [Param(SIron, "Precisão", "Estrelas de Precisão do Iron.", 1, 5, 1, Sub = Star)] public float IronStarPrecision = 3f;
    [Param(SIron, "Salto", "Estrelas de Salto do Iron.", 1, 5, 1, Sub = Star)] public float IronStarJump = 5f;
    [Param(SIron, "(1) Muralha de Aço: duração", "Segundos que a muralha fica de pé na linha de 3 do time do Iron.", 1, 20, 0.5f, Sub = Hab)] public float IronWallDuration = 5f;
    [Param(SIron, "(1) Muralha de Aço: recarga", "Segundos de recarga da Muralha de Aço.", 1, 120, 0.5f, Sub = Hab)] public float IronWallCooldown = 40f;
    [Param(SIron, "(2) Pancada: alcance", "Distância máxima (m) até o adversário para a Pancada.", 0.5f, 8, 0.25f, Sub = Hab)] public float IronBonkRange = 3f;
    [Param(SIron, "(2) Pancada: atordoamento", "Segundos atordoado de quem leva a Pancada.", 0, 10, 0.25f, Sub = Hab)] public float IronBonkStun = 3f;
    [Param(SIron, "(2) Pancada: recarga", "Segundos de recarga da Pancada.", 1, 120, 0.5f, Sub = Hab)] public float IronBonkCooldown = 30f;
    [Param(SIron, "(3) Investida: duração máxima", "Segundos máximos da Investida (acabou o tempo = o Iron fica cansado).", 0.5f, 8, 0.1f, Sub = Hab)] public float IronChargeDuration = 2f;
    [Param(SIron, "(3) Investida: atordoamento de quem é atropelado", "Segundos atordoado de quem estava no caminho (aliado ou adversário).", 0, 10, 0.25f, Sub = Hab)] public float IronChargeStun = 5f;
    [Param(SIron, "(3) Investida: Iron parado no fim", "Segundos que o Iron fica parado no fim da Investida (cansado, ou atordoado se bateu na parede).", 0, 10, 0.25f, Sub = Hab)] public float IronChargeRecover = 2f;
    [Param(SIron, "(3) Investida: recarga", "Segundos de recarga da Investida.", 1, 120, 0.5f, Sub = Hab)] public float IronChargeCooldown = 40f;
    [Param(SIron, "Ult: duração", "Segundos do modo Indestrutível.", 2, 60, 0.5f, Sub = Hab)] public float IronUltDuration = 30f;
    [Param(SIron, "Ult: estrelas de velocidade", "Velocidade do Iron no modo Indestrutível, em estrelas (se as dele forem menos).", 1, 5, 1, Sub = Hab)] public float IronUltSpeedStars = 3f;
    [Param(SIron, "Ult: recargas mais rápidas", "Quanto mais rápido as recargas das habilidades do Iron correm enquanto o modo Indestrutível está ativo (0,15 = 15% mais rápido).", 0, 2, 0.01f, Sub = Hab)] public float IronUltCdSpeedup = 0.15f;
    [Param(SIron, "Muralha: sem pegar a bola depois de derrubar", "Adversário que atravessa a Muralha de Aço com a bola (no chão ou no ar) a deixa cair na hora e não pode pegá-la de volta por este tempo (s).", 0, 5, 0.1f, Sub = Adv)] public float IronWallDropLockout = 1f;
    [Param(SIron, "Muralha: altura", "Altura (m) da Muralha de Aço (alta o bastante para barrar qualquer arremesso normal).", 3, 30, 0.5f, Sub = Adv)] public float IronWallHeight = 14f;
    [Param(SIron, "Muralha: quique da bola", "Quanto da velocidade a bola guarda ao bater na muralha (0–1).", 0, 1, 0.05f, Sub = Adv)] public float IronWallBounce = 0.45f;
    [Param(SIron, "Investida: velocidade inicial", "Velocidade (m/s) no começo da Investida (impulso inicial: um pouco mais rápido que o Iron correndo, ~6 m/s).", 1, 20, 0.25f, Sub = Adv)] public float IronChargeSpeedStart = 7.5f;
    [Param(SIron, "Investida: velocidade máxima", "Velocidade (m/s) no fim da Investida (cresce aos poucos).", 2, 30, 0.25f, Sub = Adv)] public float IronChargeSpeedMax = 11f;
    [Param(SIron, "Investida: curva", "Quanto (graus por segundo) a Investida vira, no máximo, seguindo a câmera (os botões de movimento não fazem nada).", 0, 720, 5, Sub = Adv)] public float IronChargeTurnRate = 140f;
    [Param(SIron, "Investida: raio do atropelo", "Distância (m) entre o Iron e um jogador para atropelar.", 0.3f, 3, 0.05f, Sub = Adv)] public float IronChargeHitRadius = 1.05f;
    [Param(SIron, "Investida: empurrão para o lado", "Quantos metros quem é atropelado é jogado para o lado.", 0, 5, 0.1f, Sub = Adv)] public float IronChargeKnock = 1.6f;
    [Param(SIron, "Pancada: ângulo do olhar", "Prefere o adversário mais perto do centro do olhar; acima deste ângulo (graus) ninguém é escolhido.", 10, 180, 5, Sub = Adv)] public float IronBonkAngle = 120f;

    // ---------------- One (v0.41) ----------------
    [Param(SOne, "Velocidade", "Estrelas de Velocidade do One.", 1, 5, 1, Sub = Star)] public float OneStarSpeed = 3f;
    [Param(SOne, "Agilidade", "Estrelas de Agilidade do One.", 1, 5, 1, Sub = Star)] public float OneStarAgility = 3f;
    [Param(SOne, "Estamina", "Estrelas de Estamina do One.", 1, 5, 1, Sub = Star)] public float OneStarStamina = 3f;
    [Param(SOne, "Precisão", "Estrelas de Precisão do One.", 1, 5, 1, Sub = Star)] public float OneStarPrecision = 3f;
    [Param(SOne, "Salto", "Estrelas de Salto do One.", 1, 5, 1, Sub = Star)] public float OneStarJump = 3f;
    [Param(SOne, "(1) Realm: duração máxima", "Segundos máximos dentro do Realm (sai antes apertando de novo, pulando ou tentando roubar).", 0.5f, 20, 0.25f, Sub = Hab)] public float OneRealmDuration = 3f;
    [Param(SOne, "(1) Realm: bônus de velocidade", "Quanto mais rápido o One fica no Realm (0,2 = 20%). Vale também para o One abduzido e para o One que usou a ultimate.", 0, 2, 0.01f, Sub = Hab)] public float OneRealmSpeedBonus = 0.2f;
    [Param(SOne, "(1) Realm: recarga", "Segundos de recarga do Realm (contam a partir da saída).", 1, 120, 0.5f, Sub = Hab)] public float OneRealmCooldown = 25f;
    [Param(SOne, "(2) Abdução: alcance", "Distância máxima (m) até o jogador abduzido.", 0.5f, 15, 0.25f, Sub = Hab)] public float OneAbductRange = 5f;
    [Param(SOne, "(2) Abdução: duração", "Segundos que o jogador abduzido fica no Realm (só anda e corre, não vê quem está fora).", 0.5f, 20, 0.25f, Sub = Hab)] public float OneAbductDuration = 3f;
    [Param(SOne, "(2) Abdução: recarga", "Segundos de recarga da Abdução.", 1, 120, 0.5f, Sub = Hab)] public float OneAbductCooldown = 40f;
    [Param(SOne, "(3) Maldição: duração", "Segundos que a Maldição dura no adversário escolhido.", 1, 60, 0.5f, Sub = Hab)] public float OneCurseDuration = 15f;
    [Param(SOne, "(3) Maldição: atributos restantes", "Fração que sobra de todos os atributos de quem está amaldiçoado (0,7 = perde 30%).", 0.1f, 1, 0.05f, Sub = Hab)] public float OneCurseStatMult = 0.7f;
    [Param(SOne, "(3) Maldição: pontos a menos", "Quantos pontos a menos valem as cestas de quem está amaldiçoado (lance livre não muda).", 0, 3, 1, Sub = Hab)] public float OneCursePointLoss = 1f;
    [Param(SOne, "(3) Maldição: recarga", "Segundos de recarga da Maldição.", 1, 120, 0.5f, Sub = Hab)] public float OneCurseCooldown = 40f;
    [Param(SOne, "Ult: duração", "Segundos do Domínio das Sombras.", 2, 60, 0.5f, Sub = Hab)] public float OneUltDuration = 20f;
    [Param(SOne, "Ult: atributos restantes do adversário", "Fração que sobra dos atributos do time adversário durante o Domínio (0,8 = −20%).", 0.1f, 1, 0.05f, Sub = Hab)] public float OneUltEnemyStatMult = 0.8f;
    [Param(SOne, "Ult: recargas do adversário mais lentas", "Quanto mais devagar as recargas (e a barra da ultimate) do time adversário correm durante o Domínio (0,5 = 50% mais devagar).", 0, 0.95f, 0.05f, Sub = Hab)] public float OneUltEnemyCdSlow = 0.5f;
    [Param(SOne, "(2) Abdução contra 1 adversário: duração", "Duração da Abdução quando o time adversário tem só 1 jogador.", 0.5f, 20, 0.25f, Sub = Hab)] public float OneAbductDuration1v1 = 2f;
    [Param(SOne, "(2) Abdução contra 1 adversário: recarga", "Recarga da Abdução quando o time adversário tem só 1 jogador.", 1, 120, 0.5f, Sub = Hab)] public float OneAbductCooldown1v1 = 50f;
    [Param(SOne, "(3) Maldição contra 1 adversário: atributos restantes", "Fração que sobra dos atributos do amaldiçoado quando o time dele tem só 1 jogador (0,5 = perde 50%).", 0.1f, 1, 0.05f, Sub = Hab)] public float OneCurseStatMult1v1 = 0.5f;
    [Param(SOne, "Ult: duração da cena", "Segundos da cena da quadra sendo engolida pelas sombras (o jogo fica congelado durante ela).", 1, 5, 0.1f, Sub = Adv)] public float OneUltPoseDuration = 2.8f;
    [Param(SOne, "Abdução: ângulo do olhar", "Prefere o jogador mais perto do centro do olhar; acima deste ângulo (graus) ninguém é escolhido.", 10, 180, 5, Sub = Adv)] public float OneAbductAngle = 60f;
    [Param(SOne, "Realm: transparência de quem vê", "Opacidade (0–1) dos jogadores vistos 'de outra dimensão' (One no Realm vendo quem está fora; aliados vendo o One no Realm).", 0.05f, 1, 0.05f, Sub = Adv)] public float OneGhostAlpha = 0.35f;

    // ---------------- Bots ----------------
    [Param(SBots, "Erro de mira (graus)", "Erro máximo de direção do arremesso do bot no Normal (graus). Fácil/Difícil multiplicam.", 0, 20, 0.1f)]
    public float BotAimErrorDeg = 4f;
    [Param(SBots, "Erro de distância (m)", "Erro máximo de distância do arremesso do bot no Normal (m).", 0, 5, 0.05f)]
    public float BotDistErrorM = 1.3f;
    [Param(SBots, "Erro no skill check da enterrada (s)", "Erro máximo do bot ao apertar no skill check (Normal). Compare com metade da janela da enterrada.", 0, 0.3f, 0.005f)]
    public float BotDunkTimingError = 0.05f;
    [Param(SBots, "Tempo de reação (s)", "Atraso do bot para pular contra uma enterrada e para começar a se posicionar contra um arremesso (Normal).", 0, 2, 0.01f)]
    public float BotReactionTime = 0.3f;
    [Param(SBots, "Fácil: multiplicador", "Multiplica erros e tempos de reação dos bots no Fácil (e divide as chances de roubo/bloqueio).", 1, 4, 0.05f)]
    public float BotEasyMult = 1.8f;
    [Param(SBots, "Difícil: multiplicador", "Multiplica erros e tempos de reação dos bots no Difícil.", 0.1f, 1, 0.05f)]
    public float BotHardMult = 0.55f;
    [Param(SBots, "Distância preferida de arremesso", "Bot arremessa livre a até esta distância (m) da cesta (Normal; Fácil −2, Difícil +1,5).", 2, 20, 0.5f)]
    public float BotShootRange = 9f;
    [Param(SBots, "Distância para estar livre", "Defensor mais perto que isso (m) = marcado; o bot prefere passar ou infiltrar.", 0.5f, 8, 0.1f)]
    public float BotOpenDistance = 2.6f;
    [Param(SBots, "Chance de infiltrar/enterrar", "Chance (por decisão) de o bot partir para a enterrada quando o caminho está livre.", 0, 1, 0.01f)]
    public float BotDunkChance = 0.45f;
    [Param(SBots, "Chance de passar", "Chance (por decisão) de passar quando um aliado está em situação melhor.", 0, 1, 0.01f)]
    public float BotPassChance = 0.5f;
    [Param(SBots, "Tempo máximo com a bola", "Segurando a bola por mais que isso (s), o bot arremessa assim que puder.", 1, 20, 0.5f)]
    public float BotMaxHoldTime = 4f;
    [Param(SBots, "Chance de arremesso marcado", "Chance (por decisão, ~5 por segundo) de o bot arremessar mesmo marcado, dentro da distância preferida.", 0, 1, 0.01f)]
    public float BotContestedShotChance = 0.06f;
    [Param(SBots, "Chance de roubo por segundo", "Chance por segundo de tentar roubar quando colado em quem tem a bola (Normal).", 0, 5, 0.05f)]
    public float BotStealChancePerSec = 0.2f;
    [Param(SBots, "Chance de cair no drible", "Quando quem ele marca começa o drible especial por perto, chance de o bot dar o bote mesmo assim (e errar, ficando lento). Normal; o Fácil cai mais, o Difícil menos.", 0, 1, 0.01f)]
    public float BotDribbleBaitChance = 0.6f;
    [Param(SBots, "Chance de pular para bloquear", "Chance de o bot pular contra um arremesso/enterrada perto dele (Normal).", 0, 1, 0.01f)]
    public float BotBlockChance = 0.45f;
    [Param(SBots, "Reação ao arremesso (s)", "Atraso do bot (já posicionado na frente da bola) para pular quando o adversário solta o arremesso (Normal). Acima de ~0,1 s quase não bloqueia.", 0, 0.5f, 0.005f)]
    public float BotBlockReaction = 0.03f;
    [Param(SBots, "Erro de posição no bloqueio (m)", "Erro lateral máximo do bot ao se colocar na frente do arremesso (Normal).", 0, 2, 0.05f)]
    public float BotBlockPosError = 0.35f;
    [Param(SBots, "Troca de posição sem a bola (s)", "A cada quanto tempo (em média) um bot sem a bola e sem marcar ninguém procura outro espaço livre.", 0.5f, 15, 0.1f)]
    public float BotRoamInterval = 3.5f;
    [Param(SBots, "Distância da marcação", "Distância (m) que o bot fica de quem tem a bola, entre ele e a cesta.", 0.5f, 5, 0.1f)]
    public float BotMarkDistance = 1.4f;

    // ---------------- Vibração (controle) ----------------
    [Param(SRumble, "Pulso leve: força", "Força do pulso leve (pegar a bola, pousar, começar o drible, errar roubo). 0 = desliga.", 0, 1, 0.01f)]
    public float RumbleLight = 0.3f;
    [Param(SRumble, "Pulso leve: duração", "Segundos do pulso leve.", 0.02f, 1, 0.01f)]
    public float RumbleLightTime = 0.08f;
    [Param(SRumble, "Pulso médio: força", "Força do pulso médio (seu time pontuou, lance livre certo).", 0, 1, 0.01f)]
    public float RumbleMedium = 0.55f;
    [Param(SRumble, "Pulso médio: duração", "Segundos do pulso médio.", 0.02f, 1, 0.01f)]
    public float RumbleMediumTime = 0.18f;
    [Param(SRumble, "Pulso forte: força", "Força do pulso forte (roubaram de você, enterrada certa, atordoado).", 0, 1, 0.01f)]
    public float RumbleStrong = 0.9f;
    [Param(SRumble, "Pulso forte: duração", "Segundos do pulso forte.", 0.02f, 1.5f, 0.01f)]
    public float RumbleStrongTime = 0.3f;

    // ---------------- Bonecos de treino ----------------
    [Param(SDummy, "Reação do boneco", "Atraso (s) para o boneco adversário pular contra um arremesso.", 0, 2, 0.05f)]
    public float DummyReactionTime = 0.3f;
    [Param(SDummy, "Distância para pular", "Boneco adversário pula se alguém carregar arremesso a até isso (m).", 0.5f, 10, 0.1f)]
    public float DummyJumpRange = 4f;
    [Param(SDummy, "Espera para devolver", "Segundos que o boneco aliado segura a bola antes de passar de volta.", 0, 5, 0.1f)]
    public float DummyPassBackDelay = 0.8f;
    [Param(SDummy, "Velocidade do boneco", "Multiplicador de velocidade dos bonecos quando andam.", 0, 1.5f, 0.01f)]
    public float DummySpeedMult = 0.6f;
    [Param(SDummy, "Marcação perto da cesta", "Boneco adversário vai para a frente da cesta quando quem tem a bola chega a esta distância (m) dela. 0 = desliga.", 0, 20, 0.5f)]
    public float DummyGuardRange = 7f;
    [Param(SDummy, "Raio do passeio", "Bonecos sem a bola e sem marcar andam devagar por pontos até esta distância (m) da posição inicial, parando de vez em quando. 0 = ficam parados.", 0, 8, 0.1f)]
    public float DummyWanderRadius = 2.2f;

    // =====================================================================

    public static string FilePath =>
        OS.HasFeature("editor")
            ? ProjectSettings.GlobalizePath("res://tuning.json")
            : Path.Combine(OS.GetExecutablePath().GetBaseDir(), "tuning.json");

    /// <summary>Carrega tuning.json (cria se não existir). Retorna mensagem para mostrar ao jogador.</summary>
    public static string Load()
    {
        string path = FilePath;
        try
        {
            if (File.Exists(path))
            {
                var t = JsonSerializer.Deserialize<Tuning>(File.ReadAllText(path), JsonOpts);
                int moved = 0;
                if (t != null) { moved = FollowNewDefaults(t); I = t; }
                Revision++;
                TrySave(); // acrescenta campos novos mantendo os valores do usuário
                if (moved > 0) GD.Print($"[TUNING] {moved} valor(es) não mexidos seguiram os padrões novos desta versão");
                return "Tuning carregado (" + path + ")";
            }
            TrySave();
            return "tuning.json criado com valores padrão (" + path + ")";
        }
        catch (Exception e)
        {
            GD.PushError("Erro lendo tuning.json: " + e.Message);
            return "ERRO no tuning.json — valores anteriores mantidos: " + e.Message;
        }
    }

    /// <summary>v0.31: padrões da versão que gravou o tuning.json (fica ao lado dele).</summary>
    static string DefaultsPath => Path.Combine(Path.GetDirectoryName(FilePath) ?? "", "tuning.defaults.json");

    /// <summary>v0.31: o tuning.json guarda TODOS os valores, então padrões novos de uma versão nova nunca chegavam (o arquivo
    /// velho sobrepunha). Agora cada valor do arquivo que ainda é igual ao padrão da versão que o gravou (o usuário não mexeu)
    /// passa a seguir o padrão desta versão. Valores mexidos pelo usuário continuam.</summary>
    static int FollowNewDefaults(Tuning t)
    {
        try
        {
            if (!File.Exists(DefaultsPath)) return 0;
            var old = JsonSerializer.Deserialize<Tuning>(File.ReadAllText(DefaultsPath), JsonOpts);
            if (old == null) return 0;
            var cur = new Tuning();
            int n = 0;
            foreach (var f in typeof(Tuning).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType != typeof(float) || f.GetCustomAttribute<ParamAttribute>() == null) continue;
                float v = (float)f.GetValue(t), o = (float)f.GetValue(old), c = (float)f.GetValue(cur);
                if (Mathf.Abs(v - o) < 1e-5f && Mathf.Abs(c - o) >= 1e-5f) { f.SetValue(t, c); n++; }
            }
            return n;
        }
        catch (Exception e) { GD.PushWarning("tuning.defaults.json inválido: " + e.Message); return 0; }
    }

    static void TrySave()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(I, JsonOpts));
            File.WriteAllText(DefaultsPath, JsonSerializer.Serialize(new Tuning(), JsonOpts));
        }
        catch (Exception e) { GD.PushWarning("Não foi possível salvar tuning.json: " + e.Message); }
    }

    public static string ToJson() => JsonSerializer.Serialize(I, JsonOpts);

    /// <summary>Online (cliente): usa os valores do host só em memória (não grava o tuning.json local).</summary>
    public static void ApplyJson(string json)
    {
        try
        {
            var t = JsonSerializer.Deserialize<Tuning>(json, JsonOpts);
            if (t != null) { I = t; Revision++; }
        }
        catch (Exception e) { GD.PushError("Tuning do host inválido: " + e.Message); }
    }

    /// <summary>Esquema com seções/rótulos/limites/padrões — usado para gerar o artifact.</summary>
    public static string SchemaJson()
    {
        var def = new Tuning();
        var list = new List<Dictionary<string, object>>();
        foreach (var f in typeof(Tuning).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var p = f.GetCustomAttribute<ParamAttribute>();
            if (p == null) continue;
            list.Add(new Dictionary<string, object>
            {
                ["key"] = f.Name, ["section"] = p.Section, ["label"] = p.Label, ["desc"] = p.Desc,
                ["min"] = p.Min, ["max"] = p.Max, ["step"] = p.Step, ["default"] = f.GetValue(def), ["sub"] = p.Sub,
            });
        }
        return JsonSerializer.Serialize(list, JsonOpts);
    }
}
