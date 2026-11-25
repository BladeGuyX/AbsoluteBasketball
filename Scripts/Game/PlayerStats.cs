using Godot;

namespace AB;

/// <summary>
/// v0.26: multiplicadores dos atributos de estrela de um jogador (1 = o jogo antigo, sem personagens).
/// Valor = 1 + (estrelas − 2,5) × passo% (Tuning "Estrelas (valores)"), vezes o bônus do modo apostador do Luck.
/// Valores em que MENOS é melhor (penalidade do roubo, travamento ao pousar, carga do salto) são divididos.
/// </summary>
public struct PlayerStats
{
    public float Speed, Agility, Stamina, Precision, Jump;
    public int DribbleBonus;

    public static readonly PlayerStats Neutral = new() { Speed = 1, Agility = 1, Stamina = 1, Precision = 1, Jump = 1 };

    public static float Mult(int stars, float stepPct) => 1f + (stars - 2.5f) * stepPct / 100f;

    /// <summary>Cargas de drible a mais pela Agilidade: 1★ = −1, 2–3★ = 0, 4–5★ = +1.</summary>
    public static int DribbleFor(int agilityStars) => agilityStars <= 1 ? -1 : agilityStars >= 4 ? 1 : 0;

    public static PlayerStats For(int character, float bonus = 0f, bool useStars = true)
    {
        var t = Tuning.I;
        if (!useStars)
        {
            float k = 1f + Mathf.Max(bonus, 0f);
            return new PlayerStats { Speed = k, Agility = k, Stamina = k, Precision = k, Jump = k };
        }
        var s = Characters.Stars(character);
        float b = 1f + Mathf.Max(bonus, 0f);
        return new PlayerStats
        {
            Speed = Mult(s[0], t.StarSpeedStep) * b,
            Agility = Mult(s[1], t.StarAgilityStep) * b,
            Stamina = Mult(s[2], t.StarStaminaStep) * b,
            Precision = Mult(s[3], t.StarPrecisionStep) * b,
            Jump = Mult(s[4], t.StarJumpStep) * b,
            DribbleBonus = DribbleFor(s[1]),
        };
    }

    // ---- valores finais (Tuning × atributo) ----
    public readonly float WalkSpeed => Tuning.I.WalkSpeed * Speed;
    public readonly float RunSpeed => Tuning.I.RunSpeed * Speed;
    public readonly float StealDashSpeed => Tuning.I.StealDashSpeed * Agility;
    public readonly float StealFailTime => Tuning.I.StealFailTime / Agility;
    public readonly float DribbleSpeedMult => Tuning.I.DribbleSpeedMult * Agility;
    public readonly int DribbleCharges => Mathf.Max(1, Mathf.RoundToInt(Tuning.I.DribbleCharges) + DribbleBonus);
    public readonly float StaminaMax => Tuning.I.StaminaMax * Stamina;
    public readonly float StaminaRegen => Tuning.I.StaminaRegenPerSec * Stamina;
    public readonly float ShotRangeMax => Tuning.I.ShotRangeMax * Precision;
    public readonly float PassRange => Tuning.I.PassRange * Precision;
    public readonly float PassSpeed => Tuning.I.PassSpeed * Precision;
    public readonly float AimPassSpeedMult => Precision;
    public readonly float HighPassApex => Tuning.I.HighPassApex * Precision;
    public readonly float JumpVelMin => Tuning.I.JumpVelMin * Jump;
    public readonly float JumpVelMax => Tuning.I.JumpVelMax * Jump;
    public readonly float LandLockTime => Tuning.I.LandLockTime / Jump;
    public readonly float JumpChargeTime => Tuning.I.JumpChargeTime / Jump;
    public readonly float DunkRange => Tuning.I.DunkRange * Jump;
}
