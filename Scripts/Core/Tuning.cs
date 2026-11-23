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

    // ---------------- Movimento ----------------
    [Param(SMove, "Velocidade andando", "Velocidade máxima andando (m/s).", 1, 10, 0.1f)]
    public float WalkSpeed = 4.0f;
    [Param(SMove, "Velocidade correndo", "Velocidade máxima correndo (m/s).", 1, 14, 0.1f)]
    public float RunSpeed = 6.0f;
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
    public float StaminaMax = 100f;
    [Param(SStam, "Gasto por segundo correndo", "Estamina gasta por segundo enquanto corre.", 1, 200, 1)]
    public float StaminaDrainPerSec = 28f;
    [Param(SStam, "Recarga por segundo", "Estamina recuperada por segundo sem correr.", 1, 300, 1)]
    public float StaminaRegenPerSec = 45f;
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
    public float RimBounce = 0.5f;
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
    [Param(SShot, "Alcance com carga mínima", "Distância (m) do arremesso logo no início da carga.", 0.5f, 15, 0.1f)]
    public float ShotRangeMin = 3f;
    [Param(SShot, "Alcance com carga máxima", "Distância máxima (m) que um arremesso alcança (até a altura do aro).", 2, 40, 0.1f)]
    public float ShotRangeMax = 13f;
    [Param(SShot, "Tempo para carga total", "Segundos segurando até o alcance máximo.", 0.2f, 6, 0.05f)]
    public float ShotChargeTime = 2.0f;
    [Param(SShot, "Arremesso automático", "Segurando por esse tempo, o jogador arremessa sozinho (s).", 0.5f, 10, 0.1f)]
    public float ShotAutoReleaseTime = 3f;
    [Param(SShot, "Câmera: inclinação de alcance total", "Com a câmera nesta inclinação ou acima (graus), o arremesso usa todo o alcance carregado.", -89, 45, 1)]
    public float ShotPitchFullDeg = -15f;
    [Param(SShot, "Câmera: inclinação de alcance mínimo", "Com a câmera nesta inclinação ou abaixo (graus), o arremesso fica no mais curto.", -89, 45, 1)]
    public float ShotPitchMinDeg = -50f;
    [Param(SShot, "Câmera: fração mínima do alcance", "Fração do alcance carregado usada com a câmera totalmente para baixo.", 0, 1, 0.01f)]
    public float ShotPitchMinRangeFactor = 0.15f;
    [Param(SShot, "Ângulo de perto", "Ângulo vertical (graus) em arremessos curtos. Mais alto = mais curva.", 15, 85, 0.5f)]
    public float ShotAngleNearDeg = 55f;
    [Param(SShot, "Ângulo de longe", "Ângulo vertical (graus) no alcance máximo. Mais baixo = bola mais rápida e reta.", 15, 85, 0.5f)]
    public float ShotAngleFarDeg = 44f;
    [Param(SShot, "Ângulo mínimo de entrada", "Inclinação mínima (graus) com que a bola desce na altura do aro. Maior = arcos mais altos, mais fácil entrar.", 10, 80, 0.5f)]
    public float ShotMinEntryAngleDeg = 38f;
    [Param(SShot, "Altura de soltura", "Altura de onde a bola sai no arremesso (m).", 1, 4, 0.05f)]
    public float ShotReleaseHeight = 2.35f;
    [Param(SShot, "Velocidade carregando", "Multiplicador de velocidade enquanto carrega o arremesso.", 0, 1, 0.01f)]
    public float ShotChargeMoveMult = 0.35f;
    [Param(SShot, "Assistência de direção", "Quanto a direção é puxada para a cesta adversária (0 = nenhuma, 1 = total). Nunca na própria cesta.", 0, 1, 0.01f)]
    public float ShotAimAssist = 0.6f;
    [Param(SShot, "Ângulo da assistência", "A assistência só age mirando dentro deste ângulo da cesta adversária (graus).", 0, 90, 1)]
    public float ShotAimAssistMaxAngleDeg = 30f;
    [Param(SShot, "Assistência de distância: alcance", "Se o arremesso cairia a até esta distância (m) da cesta adversária, ele é puxado para ela.", 0, 5, 0.05f)]
    public float ShotDistAssistMeters = 1.5f;
    [Param(SShot, "Assistência de distância: força", "Quanto a distância é corrigida (0 = nada, 1 = acerto exato).", 0, 1, 0.01f)]
    public float ShotDistAssistStrength = 0.6f;
    [Param(SShot, "Área de perto (bandeja)", "Dentro deste raio (m) da cesta adversária o arremesso vira bandeja: mira e distância automáticas.", 0, 8, 0.1f)]
    public float CloseShotRadius = 3f;
    [Param(SShot, "Ângulo da bandeja", "Ângulo vertical (graus) do arremesso de perto.", 30, 85, 0.5f)]
    public float CloseShotAngleDeg = 66f;
    [Param(SShot, "Bloqueio de repegar", "Segundos em que quem arremessou não pode pegar a própria bola.", 0, 2, 0.05f)]
    public float ShotPickupLockout = 0.35f;
    [Param(SShot, "Duração da trajetória mostrada", "Segundos de voo desenhados no indicador de trajetória.", 0.3f, 5, 0.1f)]
    public float TrajectoryPreviewTime = 3f;

    // ---------------- Quadra ----------------
    [Param(SCourt, "Altura do aro", "Altura do aro (m). Real = 3.05.", 1.5f, 7, 0.05f)]
    public float HoopHeight = 4.0f;
    [Param(SCourt, "Raio do aro", "Raio interno do aro (m). Real ≈ 0.23.", 0.15f, 1.2f, 0.01f)]
    public float RimRadius = 0.5f;
    [Param(SCourt, "Largura da tabela", "Largura da tabela (m).", 0.5f, 8, 0.05f)]
    public float BoardWidth = 3.4f;
    [Param(SCourt, "Altura da tabela", "Altura da tabela (m).", 0.5f, 5, 0.05f)]
    public float BoardHeight = 2.0f;
    [Param(SCourt, "Tolerância da cesta", "Quanto (fração do raio da bola) ela pode encostar no aro e ainda contar ao passar.", 0, 1, 0.05f)]
    public float ScoreTolerance = 0.35f;
    [Param(SCourt, "Distância do aro à linha de fundo", "Distância do centro do aro até a linha de fundo (m).", 0.5f, 5, 0.05f)]
    public float HoopInset = 1.8f;
    [Param(SCourt, "Pequena: comprimento", "Comprimento da quadra pequena (m).", 10, 80, 0.5f)]
    public float CourtSmallLength = 28f;
    [Param(SCourt, "Pequena: largura", "Largura da quadra pequena (m).", 6, 50, 0.5f)]
    public float CourtSmallWidth = 16f;
    [Param(SCourt, "Pequena: raio dos 3 pontos", "Raio da linha de 3 na quadra pequena (m).", 2, 20, 0.1f)]
    public float ThreePtSmall = 7.0f;
    [Param(SCourt, "Média: comprimento", "Comprimento da quadra média (m).", 10, 80, 0.5f)]
    public float CourtMediumLength = 34f;
    [Param(SCourt, "Média: largura", "Largura da quadra média (m).", 6, 50, 0.5f)]
    public float CourtMediumWidth = 19f;
    [Param(SCourt, "Média: raio dos 3 pontos", "Raio da linha de 3 na quadra média (m).", 2, 20, 0.1f)]
    public float ThreePtMedium = 8.0f;
    [Param(SCourt, "Grande: comprimento", "Comprimento da quadra grande (m).", 10, 80, 0.5f)]
    public float CourtLargeLength = 40f;
    [Param(SCourt, "Grande: largura", "Largura da quadra grande (m).", 6, 50, 0.5f)]
    public float CourtLargeWidth = 22f;
    [Param(SCourt, "Grande: raio dos 3 pontos", "Raio da linha de 3 na quadra grande (m).", 2, 20, 0.1f)]
    public float ThreePtLarge = 9.0f;
    [Param(SCourt, "Margem além das linhas", "Espaço jogável além das linhas antes das paredes invisíveis (não existe 'fora').", 0.2f, 6, 0.1f)]
    public float BoundsMargin = 1.5f;

    // ---------------- Regras ----------------
    [Param(SRules, "Contagem regressiva", "Segundos de contagem antes de cada round.", 0, 10, 0.5f)]
    public float CountdownSeconds = 3f;
    [Param(SRules, "Pausa após cesta", "Segundos de comemoração antes de reiniciar o round.", 0, 6, 0.1f)]
    public float PostScoreDelay = 1.6f;
    [Param(SRules, "Pontos de dentro", "Pontos de uma cesta de dentro da linha de 3.", 1, 10, 1)]
    public float PointsInside = 2f;
    [Param(SRules, "Pontos de fora", "Pontos de uma cesta de fora da linha de 3.", 1, 10, 1)]
    public float PointsOutside = 3f;

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
