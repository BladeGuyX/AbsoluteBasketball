using Godot;

namespace AB;

/// <summary>
/// Indicadores discretos em volta da mira (SEM TEXTO — pedido do usuário):
/// - passe carregado: ícone abaixo da mira (seta reta amarela = normal; arco rosa = alto) + anel do passe automático;
/// - arremesso: anel fino até o arremesso automático;
/// - drible ativo / penalidade do roubo / atordoado / pousando: anel curto que esvazia, em cores diferentes;
/// - enterrada: setinhas verdes acima da mira = dá para enterrar agora (Espaço); skill check (anel com zona certa e ponteiro) + flash;
/// - lances livres: o mesmo skill check (só para quem está cobrando).
/// </summary>
public partial class AimIndicator : Control
{
    public Match M;

    static readonly Color Yellow = new("ffd23f");
    static readonly Color Pink = new("ff7ad9");
    static readonly Color Orange = new("ffb020");
    static readonly Color Red = new("ff5a5a");
    static readonly Color Green = new("5ad17a");
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

        if (M.IsClient && M.ClientSkillHideT > 0 && (p.Flight == FlightKind.Dunk || M.FtShooter == p)) return; // errou: anel some

        // ---- enterrada: skill check ----
        if (p.Flight == FlightKind.Dunk || p.SkillFlash > 0)
        {
            DrawSkillCheck(c, t, p.FlightT, p.FlightDur, p.Flight == FlightKind.Dunk, p.DunkPressed, p.DunkPressT, p.SkillFlash, p.SkillFlashOk);
            return;
        }
        // ---- lances livres: mesmo skill check ----
        if ((M.FtChecking && M.FtShooter == p && M.FtT >= 0) || (M.FtFlash > 0 && M.FtFlashPlayer == p))
        {
            DrawSkillCheck(c, t, M.FtT, M.FtDur, M.FtChecking && M.FtShooter == p, M.FtPressed, M.FtPressT, M.FtFlash, M.FtFlashOk);
            return;
        }
        if (M.FtChecking && M.FtShooter == p)
        {
            // preparação: anel vazio esperando o ponteiro
            DrawArc(c, 62, 0, Mathf.Tau, 64, Ink, 9, true);
            DrawArc(c, 62, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.35f), 3, true);
            return;
        }

        // ---- dá para enterrar agora: duas setinhas verdes acima da mira ----
        if (p.DunkReadyVisual) DunkReady(c);

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

    void DunkReady(Vector2 c)
    {
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.GetTicksMsec() * 0.012f);
        var col = Green with { A = pulse };
        for (int i = 0; i < 2; i++)
        {
            var tip = c + new Vector2(0, -36 - i * 12);
            var pts = new[] { tip + new Vector2(-13, 9), tip, tip + new Vector2(13, 9) };
            DrawPolyline(pts, Ink, 8, true);
            DrawPolyline(pts, col, 4, true);
        }
    }

    /// <summary>Anel do skill check (enterrada e lances livres): o ponteiro gira 270° e chega na zona certa em time = dur.</summary>
    void DrawSkillCheck(Vector2 c, Tuning t, float time, float durRaw, bool active, bool pressed, float pressT, float flash, bool flashOk)
    {
        const float R = 62;
        const float Sweep = Mathf.Pi * 1.5f;          // o ponteiro percorre 270° até tocar a cesta
        float a0 = -Mathf.Pi / 2;
        float dur = Mathf.Max(durRaw, 0.05f);
        float perSec = Sweep / dur;
        DrawArc(c, R, 0, Mathf.Tau, 64, Ink, 9, true);
        DrawArc(c, R, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.35f), 3, true);
        // zona certa (centrada no toque na cesta)
        float zc = a0 + Sweep;
        float zh = t.DunkWindow * 0.5f * perSec;
        var zoneCol = flash > 0 ? (flashOk ? Green : Red) : Colors.White;
        DrawArc(c, R, zc - zh, zc + zh, 12, zoneCol, 8, true);
        if (flash > 0 && !active)
        {
            DrawArc(c, R + 10, 0, Mathf.Tau, 64, zoneCol with { A = Mathf.Clamp(flash / 0.6f, 0, 1) * 0.8f }, 4, true);
            return;
        }
        // ponteiro
        float a = a0 + Mathf.Clamp(time, 0, dur + t.DunkWindow) * perSec;
        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        DrawLine(c + dir * (R - 14), c + dir * (R + 12), Ink, 7, true);
        DrawLine(c + dir * (R - 14), c + dir * (R + 12), Red, 4, true);
        if (pressed)
        {
            float pa = a0 + pressT * perSec;
            var pd = new Vector2(Mathf.Cos(pa), Mathf.Sin(pa));
            DrawCircle(c + pd * R, 5, Yellow);
        }
    }
}
