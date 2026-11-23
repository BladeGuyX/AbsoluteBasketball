using Godot;

namespace AB;

/// <summary>
/// Indicadores discretos em volta da mira (SEM TEXTO — pedido do usuário):
/// - passe carregado: ícone abaixo da mira (seta reta amarela = normal; arco rosa = alto) + anel do passe automático;
/// - arremesso: anel fino até o arremesso automático;
/// - drible ativo / penalidade do roubo / atordoado / pousando: anel curto que esvazia, em cores diferentes;
/// - enterrada: skill check (anel com zona certa e ponteiro) + flash verde/vermelho do resultado.
/// </summary>
public partial class AimIndicator : Control
{
    public Match M;

    static readonly Color Yellow = new("ffd23f");
    static readonly Color Pink = new("ff7ad9");
    static readonly Color Orange = new("ffb020");
    static readonly Color Red = new("ff5a5a");
    static readonly Color Grey = new(1, 1, 1, 0.55f);
    static readonly Color Ink = new(0.08f, 0.1f, 0.15f, 0.55f);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var p = M?.LocalPlayer;
        if (p == null) return;
        var t = Tuning.I;
        var c = Size / 2f;

        // ---- enterrada: skill check ----
        if (p.Flight == FlightKind.Dunk || p.SkillFlash > 0)
        {
            DrawSkillCheck(p, c, t);
            return;
        }

        // ---- passe carregado ----
        if (p.ChargingPass && p.PassCharge >= t.PassTapTime)
        {
            var col = p.PassHigh ? Pink : Yellow;
            var ic = c + new Vector2(0, 46);
            if (p.PassHigh)
            {
                DrawArc(ic + new Vector2(0, 8), 16, Mathf.Pi * 1.1f, Mathf.Pi * 1.9f, 16, Ink, 6, true);
                DrawArc(ic + new Vector2(0, 8), 16, Mathf.Pi * 1.1f, Mathf.Pi * 1.9f, 16, col, 3, true);
            }
            else
            {
                DrawLine(ic + new Vector2(-14, 0), ic + new Vector2(14, 0), Ink, 6, true);
                DrawLine(ic + new Vector2(-14, 0), ic + new Vector2(14, 0), col, 3, true);
                DrawLine(ic + new Vector2(6, -6), ic + new Vector2(14, 0), col, 3, true);
                DrawLine(ic + new Vector2(6, 6), ic + new Vector2(14, 0), col, 3, true);
            }
            Ring(c, 22, p.PassCharge / Mathf.Max(t.PassAutoReleaseTime, 0.1f), col);
        }
        else if (p.ChargingShot)
            Ring(c, 22, p.ShotCharge / Mathf.Max(t.ShotAutoReleaseTime, 0.1f), Orange);

        // ---- estados temporários (anel que esvazia) ----
        if (p.Stun > 0) Ring(c, 30, p.Stun / Mathf.Max(t.DunkStunTime, 0.1f), Grey, true);
        else if (p.StealFail > 0) Ring(c, 30, p.StealFail / Mathf.Max(t.StealFailTime, 0.1f), Red, true);
        else if (p.IsDribbling) Ring(c, 30, p.DribbleActive / Mathf.Max(t.DribbleDuration, 0.1f), Orange, true);
        else if (p.LandLock > 0) Ring(c, 30, p.LandLock / Mathf.Max(t.LandLockTime, 0.1f), Grey, true);
    }

    void Ring(Vector2 c, float r, float frac, Color col, bool thin = false)
    {
        frac = Mathf.Clamp(frac, 0, 1);
        if (frac <= 0.001f) return;
        float w = thin ? 3 : 4;
        float a0 = -Mathf.Pi / 2;
        DrawArc(c, r, a0, a0 + Mathf.Tau * frac, 48, Ink, w + 3, true);
        DrawArc(c, r, a0, a0 + Mathf.Tau * frac, 48, col with { A = 0.9f }, w, true);
    }

    void DrawSkillCheck(Player p, Vector2 c, Tuning t)
    {
        const float R = 62;
        const float Sweep = Mathf.Pi * 1.5f;          // o ponteiro percorre 270° até tocar a cesta
        float a0 = -Mathf.Pi / 2;
        float dur = Mathf.Max(p.FlightDur, 0.05f);
        float perSec = Sweep / dur;
        DrawArc(c, R, 0, Mathf.Tau, 64, Ink, 9, true);
        DrawArc(c, R, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.35f), 3, true);
        // zona certa (centrada no toque na cesta)
        float zc = a0 + Sweep;
        float zh = t.DunkWindow * 0.5f * perSec;
        var zoneCol = p.SkillFlash > 0 ? (p.SkillFlashOk ? new Color("5ad17a") : Red) : Colors.White;
        DrawArc(c, R, zc - zh, zc + zh, 12, zoneCol, 8, true);
        if (p.SkillFlash > 0 && p.Flight != FlightKind.Dunk)
        {
            DrawArc(c, R + 10, 0, Mathf.Tau, 64, zoneCol with { A = Mathf.Clamp(p.SkillFlash / 0.6f, 0, 1) * 0.8f }, 4, true);
            return;
        }
        // ponteiro
        float a = a0 + Mathf.Min(p.FlightT, dur + t.DunkWindow) * perSec;
        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        DrawLine(c + dir * (R - 14), c + dir * (R + 12), Ink, 7, true);
        DrawLine(c + dir * (R - 14), c + dir * (R + 12), Red, 4, true);
        if (p.DunkPressed)
        {
            float pa = a0 + p.DunkPressT * perSec;
            var pd = new Vector2(Mathf.Cos(pa), Mathf.Sin(pa));
            DrawCircle(c + pd * R, 5, Yellow);
        }
    }
}
