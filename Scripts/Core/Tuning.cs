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
    const string SDummy = "Bonecos de treino";

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
    public float CarrierSpeedMult = 0.92f;
    [Param(SMove, "Gravidade dos jogadores", "Gravidade aplicada aos jogadores (m/s²).", 5, 60, 0.5f)]
    public float PlayerGravity = 22f;

    // ---------------- Estamina ----------------
    [Param(SStam, "Estamina máxima", "Quantidade total de estamina.", 10, 500, 1)]
    public float StaminaMax = 140f;
    [Param(SStam, "Gasto por segundo correndo", "Estamina gasta por segundo enquanto corre.", 1, 200, 1)]
    public float StaminaDrainPerSec = 28f;
    [Param(SStam, "Recarga por segundo", "Estamina recuperada por segundo sem correr.", 1, 300, 1)]
    public float StaminaRegenPerSec = 36f;
    [Param(SStam, "Atraso da recarga", "Segundos após parar de correr até começar a recarregar.", 0, 3, 0.05f)]
    public float StaminaRegenDelay = 0.35f;
    [Param(SStam, "Mínimo para começar a correr", "Estamina mínima para iniciar uma corrida.", 0, 100, 1)]
    public float StaminaMinToRun = 15f;

    // ---------------- Câmera ----------------
    [Param(SCam, "Distância", "Distância da câmera atrás do jogador (m).", 1.5f, 15, 0.1f)]
    public float CameraDistance = 5.5f;
    [Param(SCam, "Altura do pivô", "Altura do ponto que a câmera orbita (m acima dos pés).", 0.5f, 4, 0.05f)]
    public float CameraHeight = 2.1f;
    [Param(SCam, "Deslocamento lateral", "Câmera sobre o ombro (m, negativo = esquerda).", -2, 2, 0.05f)]
    public float CameraShoulderOffset = 0.8f;
    [Param(SCam, "Campo de visão (FOV)", "FOV vertical em graus.", 40, 110, 1)]
    public float CameraFov = 72f;
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
    public float BankAssist = 0.35f;
    [Param(SBall, "Quique na tabela", "Elasticidade da tabela.", 0, 1, 0.01f)]
    public float BoardBounce = 0.55f;
    [Param(SBall, "Quique nas paredes invisíveis", "Elasticidade dos limites da área de jogo.", 0, 1, 0.01f)]
    public float WallBounce = 0.6f;
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
    [Param(SBall, "Volume do quique (dB)", "Volume do som do drible automático. Mais negativo = mais baixo.", -40, 0, 1)]
    public float DribbleSoundDb = -18f;

    // ---------------- Arremesso ----------------
    [Param(SShot, "Distância no meio da tela", "Distância (m) do arremesso com a câmera no meio da tela. Suba a câmera para ir mais longe.", 0.5f, 15, 0.1f)]
    public float ShotRangeMin = 3f;
    [Param(SShot, "Alcance máximo", "Distância máxima (m) do arremesso, disponível desde o início (não depende de segurar). Para usar, suba a câmera.", 2, 40, 0.1f)]
    public float ShotRangeMax = 14f;
    [Param(SShot, "Arremesso automático", "Segurando por esse tempo, o jogador arremessa sozinho (s).", 0.5f, 10, 0.1f)]
    public float ShotAutoReleaseTime = 4f;
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
    public float ShotChargeMoveMult = 0.35f;
    [Param(SShot, "Assistência de direção", "Correção da direção quando a mira já está perto da cesta adversária (diminui suavemente até zero na borda do ângulo). Nunca na própria cesta.", 0, 1, 0.01f)]
    public float ShotAimAssist = 0.85f;
    [Param(SShot, "Ângulo da assistência", "A assistência de direção só age mirando dentro deste ângulo da cesta adversária (graus).", 0, 45, 0.5f)]
    public float ShotAimAssistMaxAngleDeg = 7f;
    [Param(SShot, "Assistência de distância: alcance", "Se a distância/altura do arremesso estiver a até isso (m) do ideal, ela é corrigida (suavemente, mais forte quanto mais perto).", 0, 5, 0.05f)]
    public float ShotDistAssistMeters = 1.0f;
    [Param(SShot, "Assistência de distância: força", "Quanto a distância é corrigida (0 = nada, 1 = acerto exato).", 0, 1, 0.01f)]
    public float ShotDistAssistStrength = 0.85f;
    [Param(SShot, "Bloqueio de repegar", "Segundos em que quem arremessou não pode pegar a própria bola.", 0, 2, 0.05f)]
    public float ShotPickupLockout = 0.35f;
    [Param(SShot, "Duração da trajetória mostrada", "Segundos de voo desenhados no indicador de trajetória.", 0.3f, 5, 0.1f)]
    public float TrajectoryPreviewTime = 3f;

    // ---------------- Quadra ----------------
    [Param(SCourt, "Altura do aro", "Altura do aro (m). Real = 3.05.", 1.5f, 7, 0.05f)]
    public float HoopHeight = 4.0f;
    [Param(SCourt, "Raio do aro", "Raio interno do aro (m). Real ≈ 0.23.", 0.15f, 1.2f, 0.01f)]
    public float RimRadius = 0.55f;
    [Param(SCourt, "Largura da tabela", "Largura da tabela (m).", 0.5f, 8, 0.05f)]
    public float BoardWidth = 3.4f;
    [Param(SCourt, "Altura da tabela", "Altura da tabela (m).", 0.5f, 5, 0.05f)]
    public float BoardHeight = 2.0f;
    [Param(SCourt, "Tolerância da cesta", "Quanto (fração do raio da bola) ela pode encostar no aro e ainda contar ao passar.", 0, 1, 0.05f)]
    public float ScoreTolerance = 0.5f;
    [Param(SCourt, "Distância do aro à linha de fundo", "Distância do centro do aro até a linha de fundo (m).", 0.5f, 5, 0.05f)]
    public float HoopInset = 1.8f;
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
    [Param(SPass, "Velocidade do passe rápido", "Velocidade horizontal (m/s) do passe rápido normal. Maior = mais rápido e mais reto.", 4, 50, 0.5f)]
    public float PassSpeed = 21f;
    [Param(SPass, "Altura de recepção", "Altura (m) do peito do recebedor, mirada pelo passe rápido.", 0.5f, 2.5f, 0.05f)]
    public float PassReceiveHeight = 1.3f;
    [Param(SPass, "Largura do corredor", "Um jogador a menos disso (m) da linha do passe rápido bloqueia o alvo.", 0.1f, 2, 0.05f)]
    public float PassLaneRadius = 0.9f;
    [Param(SPass, "Altura do passe alto", "Altura máxima (m) que o passe rápido alto atinge.", 3, 12, 0.1f)]
    public float HighPassApex = 5.0f;
    [Param(SPass, "Passe carregado: velocidade", "Velocidade (m/s) da bola no passe carregado normal (lançado na direção da câmera).", 4, 50, 0.5f)]
    public float AimPassSpeed = 22f;
    [Param(SPass, "Passe carregado: elevação", "Graus somados à inclinação da câmera no passe carregado normal (compensa a gravidade).", -20, 45, 0.5f)]
    public float AimPassLiftDeg = 6f;
    [Param(SPass, "Passe carregado alto: velocidade", "Velocidade (m/s) da bola no passe carregado alto.", 4, 50, 0.5f)]
    public float AimHighPassSpeed = 14f;
    [Param(SPass, "Passe carregado alto: elevação", "Graus somados à inclinação da câmera no passe carregado alto.", 0, 80, 0.5f)]
    public float AimHighPassLiftDeg = 32f;
    [Param(SPass, "Passe automático", "Segurando o passe por esse tempo (s), o jogador passa sozinho na direção da câmera.", 0.5f, 10, 0.1f)]
    public float PassAutoReleaseTime = 4f;
    [Param(SPass, "Velocidade carregando o passe", "Multiplicador de velocidade enquanto carrega o passe (não pode ter a bola roubada).", 0, 1, 0.01f)]
    public float PassChargeMoveMult = 0.25f;
    [Param(SPass, "Bloqueio de repegar", "Segundos em que quem passou não pode pegar a própria bola.", 0, 2, 0.05f)]
    public float PassLockout = 0.3f;

    // ---------------- Salto e bloqueio ----------------
    [Param(SJump, "Impulso do salto rápido", "Velocidade vertical (m/s) de um toque no salto. Altura ≈ v²/(2·gravidade).", 1, 20, 0.1f)]
    public float JumpVelMin = 6.2f;
    [Param(SJump, "Impulso do salto carregado", "Velocidade vertical (m/s) segurando o salto até o fim da carga.", 1, 20, 0.1f)]
    public float JumpVelMax = 7.6f;
    [Param(SJump, "Tempo de carga do salto", "Segundos segurando para o salto mais alto (parado enquanto carrega).", 0.05f, 2, 0.01f)]
    public float JumpChargeTime = 0.3f;
    [Param(SJump, "Controle de direção do salto", "Fração da velocidade de movimento levada para o salto (direção escolhida antes de pular).", 0, 1.5f, 0.01f)]
    public float JumpCarry = 0.7f;
    [Param(SJump, "Travamento ao pousar", "Segundos sem mover nem agir depois de pousar.", 0, 2, 0.05f)]
    public float LandLockTime = 0.5f;
    [Param(SJump, "Raio do bloqueio", "Alcance horizontal (m) do corpo/braços para bloquear no ar.", 0.2f, 2, 0.05f)]
    public float BlockRadius = 0.6f;
    [Param(SJump, "Altura do bloqueio", "Até que altura (m acima dos pés) os braços bloqueiam.", 1, 4, 0.05f)]
    public float BlockReach = 2.6f;
    [Param(SJump, "Velocidade do rebote do bloqueio", "Velocidade (m/s) com que a bola quica no bloqueador.", 0.5f, 15, 0.1f)]
    public float BlockBounceSpeed = 3.5f;

    // ---------------- Roubo ----------------
    [Param(SSteal, "Velocidade do bote", "Velocidade (m/s) do impulso para frente ao tentar roubar.", 1, 30, 0.5f)]
    public float StealDashSpeed = 9f;
    [Param(SSteal, "Duração do bote", "Duração (s) do impulso do roubo.", 0.05f, 1, 0.01f)]
    public float StealDashTime = 0.25f;
    [Param(SSteal, "Alcance do roubo", "Distância (m) até quem está com a bola para acertar o roubo.", 0.3f, 3, 0.05f)]
    public float StealReach = 1.1f;
    [Param(SSteal, "Ângulo do roubo", "O alvo precisa estar dentro deste ângulo (graus) à frente.", 10, 180, 1)]
    public float StealAngleDeg = 70f;
    [Param(SSteal, "Penalidade por errar", "Segundos lento (sem correr, pular ou roubar) após errar o roubo.", 0, 6, 0.1f)]
    public float StealFailTime = 2f;
    [Param(SSteal, "Velocidade na penalidade", "Multiplicador de velocidade durante a penalidade.", 0, 1, 0.01f)]
    public float StealFailSpeedMult = 0.2f;

    // ---------------- Drible ----------------
    [Param(SDrib, "Cargas de drible", "Quantas cargas de drible o jogador tem.", 1, 6, 1)]
    public float DribbleCharges = 2f;
    [Param(SDrib, "Duração do drible", "Segundos de drible (imune a roubo).", 0.2f, 4, 0.05f)]
    public float DribbleDuration = 1f;
    [Param(SDrib, "Velocidade driblando", "Multiplicador de velocidade durante o drible.", 0.1f, 1.5f, 0.01f)]
    public float DribbleSpeedMult = 0.5f;
    [Param(SDrib, "Recarga por carga", "Segundos para recuperar 1 carga (conta sempre que falta alguma).", 1, 60, 0.5f)]
    public float DribbleRecharge = 10f;
    [Param(SDrib, "Quique driblando", "Multiplicador da velocidade do quique durante o drible (maior = quica mais rápido).", 1, 5, 0.1f)]
    public float DribbleBounceMult = 2.6f;
    [Param(SDrib, "Agachamento driblando", "Quanto o boneco agacha durante o drible (0 = nada, 1 = como carregando o salto).", 0, 1.3f, 0.05f)]
    public float DribbleCrouch = 0.8f;

    // ---------------- Bonecos de treino ----------------
    [Param(SDummy, "Reação do boneco", "Atraso (s) para o boneco adversário pular contra um arremesso.", 0, 2, 0.05f)]
    public float DummyReactionTime = 0.3f;
    [Param(SDummy, "Distância para pular", "Boneco adversário pula se alguém carregar arremesso a até isso (m).", 0.5f, 10, 0.1f)]
    public float DummyJumpRange = 4f;
    [Param(SDummy, "Espera para devolver", "Segundos que o boneco aliado segura a bola antes de passar de volta.", 0, 5, 0.1f)]
    public float DummyPassBackDelay = 0.8f;
    [Param(SDummy, "Velocidade do boneco", "Multiplicador de velocidade dos bonecos quando andam.", 0, 1.5f, 0.01f)]
    public float DummySpeedMult = 0.6f;

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
                if (t != null) I = t;
                Revision++;
                TrySave(); // acrescenta campos novos mantendo os valores do usuário
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

    static void TrySave()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(I, JsonOpts)); }
        catch (Exception e) { GD.PushWarning("Não foi possível salvar tuning.json: " + e.Message); }
    }

    public static string ToJson() => JsonSerializer.Serialize(I, JsonOpts);

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
                ["min"] = p.Min, ["max"] = p.Max, ["step"] = p.Step, ["default"] = f.GetValue(def),
            });
        }
        return JsonSerializer.Serialize(list, JsonOpts);
    }
}
