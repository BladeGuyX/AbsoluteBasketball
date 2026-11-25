using Godot;

namespace AB;

/// <summary>
/// Indicadores discretos em volta da mira (SEM TEXTO — pedido do usuário):
/// - passe carregado: ícone abaixo da mira (seta reta amarela = normal; arco rosa = alto) + anel do passe automático;
/// - arremesso: anel fino até o arremesso automático;
/// - drible ativo / penalidade do roubo / atordoado / pousando: anel curto que esvazia, em cores diferentes;
/// - enterrada: setinhas verdes acima da mira = dá para enterrar agora (Espaço); skill check (anel com zona certa e ponteiro);
///   o resultado fica na tela por SkillResultHold com o ponteiro parado (verde ✓ / vermelho ✗);
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

        // ---- online: o cliente mostra o resultado do próprio aperto na hora (ponteiro onde ele viu) ----
        if (M.IsClient && M.ClientSkillHold > 0)
        {
            DrawSkillCheck(c, t, M.ClientSkillT, M.ClientSkillDur, false, true, M.ClientSkillT, M.ClientSkillHold, M.ClientSkillOk, 1f);
            return;
        }
        bool mute = M.IsClient && M.ClientSkillMuteT > 0; // resultado do host chegando atrasado: já foi mostrado

        // ---- enterrada: skill check ----
        if (p.Flight == FlightKind.Dunk || (p.SkillFlash > 0 && !mute))
        {
            DrawSkillCheck(c, t, p.FlightT, p.FlightDur, p.Flight == FlightKind.Dunk, p.DunkPressed, p.DunkPressT, p.SkillFlash, p.SkillFlashOk, p.FlightT);
            return;
        }
        // ---- lances livres: mesmo skill check ----
        if ((M.FtChecking && M.FtShooter == p && M.FtT >= 0) || (M.FtFlash > 0 && M.FtFlashPlayer == p && !mute))
        {
            DrawSkillCheck(c, t, M.FtT, M.FtDur, M.FtChecking && M.FtShooter == p, M.FtPressed, M.FtPressT, M.FtFlash, M.FtFlashOk,
                M.FtT + t.FreeThrowPrep);
            return;
        }
        if (M.FtChecking && M.FtShooter == p)
        {
            // preparação: anel com a zona marcada, esperando o ponteiro
            DrawSkillCheck(c, t, 0, M.FtDur, true, false, 0, 0, false, M.FtT + t.FreeThrowPrep, pointer: false);
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

    static readonly Color Gold = new("ffe27a");
    static readonly Color Band = new(0.06f, 0.08f, 0.13f, 0.62f);
    float _alpha = 1f; // transparência geral do skill check (some suavemente no fim do resultado)
    Color Fa(Color col) => col with { A = col.A * _alpha };
    static Vector2 Dir(float a) => new(Mathf.Cos(a), Mathf.Sin(a));

    /// <summary>
    /// Anel do skill check (enterrada e lances livres): o ponteiro gira 270° e chega na zona certa em time = dur.
    /// Resultado (flash &gt; 0 e não ativo): fica na tela por <c>SkillResultHold</c> com o ponteiro PARADO onde foi apertado
    /// (ou onde passou do tempo), zona e ponteiro verdes (certo) ou vermelhos (errado), um anel que se expande e um ✓/✗.
    /// <paramref name="introT"/> = segundos desde que o anel apareceu (animação de entrada).
    /// </summary>
    void DrawSkillCheck(Vector2 c, Tuning t, float time, float durRaw, bool active, bool pressed, float pressT, float flash, bool flashOk,
        float introT, bool pointer = true)
    {
        const float R = 62;
        const float Sweep = Mathf.Pi * 1.5f;          // o ponteiro percorre 270° até tocar a cesta
        float a0 = -Mathf.Pi / 2;
        float dur = Mathf.Max(durRaw, 0.05f);
        float perSec = Sweep / dur;
        float hold = Mathf.Max(t.SkillResultHold, 0.001f);
        bool result = flash > 0 && !active;
        float rk = result ? Mathf.Clamp(1f - flash / hold, 0, 1) : 0;   // 0 → 1 durante o resultado
        var resCol = flashOk ? Green : Red;

        // entrada: cresce de 82% a 100%; resultado: "pulinho" no começo e some no fim
        float s = 1f;
        if (!result) { float k = Mathf.Clamp(introT / 0.14f, 0, 1); s = Mathf.Lerp(0.82f, 1f, 1f - (1f - k) * (1f - k)); }
        else s = 1f + 0.07f * Mathf.Sin(Mathf.Min(rk * 4f, 1f) * Mathf.Pi);
        _alpha = result ? Mathf.Clamp(flash / Mathf.Min(0.18f, hold), 0, 1)
                        : Mathf.Clamp(introT / 0.1f, 0, 1);
        DrawSetTransform(c, 0, new Vector2(s, s));
        var o = Vector2.Zero;

        // ---- faixa de fundo, bordas finas e marquinhas ao longo do percurso ----
        DrawArc(o, R, 0, Mathf.Tau, 80, Fa(Band), 17, true);
        DrawArc(o, R - 8.5f, 0, Mathf.Tau, 80, Fa(new Color(1, 1, 1, 0.16f)), 1.5f, true);
        DrawArc(o, R + 8.5f, 0, Mathf.Tau, 80, Fa(new Color(1, 1, 1, 0.16f)), 1.5f, true);
        for (int i = 0; i <= 9; i++)
        {
            var d = Dir(a0 + Sweep * i / 9f);
            DrawLine(d * (R - 4), d * (R + 4), Fa(new Color(1, 1, 1, i == 0 ? 0.45f : 0.2f)), 2, true);
        }

        // ---- ângulo do ponteiro (no resultado: parado onde apertou / onde acabou o tempo) ----
        float stopT = result && pressed ? pressT : time;
        float a = a0 + Mathf.Clamp(stopT, 0, dur + t.DunkWindow) * perSec;

        // ---- rastro do ponteiro (fica mais forte perto dele) ----
        if (pointer && a > a0 + 0.02f)
        {
            const int N = 28;
            var trail = result ? resCol : new Color("fff1c2");
            for (int i = 0; i < N; i++)
            {
                float u0 = (float)i / N, u1 = (float)(i + 1) / N;
                float b0 = Mathf.Lerp(a0, a, u0), b1 = Mathf.Lerp(a0, a, u1) + 0.004f;
                DrawArc(o, R, b0, b1, 3, Fa(trail with { A = 0.05f + 0.6f * u1 * u1 }), 7, true);
            }
        }

        // ---- zona certa (centrada no toque na cesta), com brilho que cresce quando o ponteiro chega perto ----
        float zc = a0 + Sweep;
        float zh = Mathf.Max(t.DunkWindow * 0.5f * perSec, 0.035f);
        var zoneCol = result ? resCol : Gold;
        float near = active && pointer ? Mathf.Clamp(1f - Mathf.Abs(time - dur) / (dur * 0.35f), 0, 1) : 0;
        float glow = result ? 0.35f * (1f - rk) + 0.15f : 0.14f + 0.36f * near;
        DrawArc(o, R, zc - zh - 0.05f, zc + zh + 0.05f, 12, Fa(zoneCol with { A = glow }), 20, true);
        DrawArc(o, R, zc - zh, zc + zh, 12, Fa(Ink), 13, true);
        DrawArc(o, R, zc - zh, zc + zh, 12, Fa(zoneCol with { A = pointer || result ? 1f : 0.6f }), 10, true);
        DrawArc(o, R, zc - zh * 0.6f, zc + zh * 0.6f, 8, Fa(new Color(1, 1, 1, result ? 0.25f : 0.55f)), 2, true);

        // ---- resultado: anel que se expande e ✓ / ✗ do lado da zona ----
        if (result)
        {
            float er = R + 10 + rk * 16;
            DrawArc(o, er, 0, Mathf.Tau, 80, Fa(resCol with { A = 0.75f * (1f - rk) }), 1.5f + 3.5f * (1f - rk), true);
            var ic = Dir(zc) * (R + 30);
            float isz = 8f * (1f + 0.25f * Mathf.Sin(Mathf.Min(rk * 4f, 1f) * Mathf.Pi));
            if (flashOk)
            {
                var pts = new[] { ic + new Vector2(-1f, 0.1f) * isz, ic + new Vector2(-0.3f, 0.8f) * isz, ic + new Vector2(1f, -0.8f) * isz };
                DrawPolyline(pts, Fa(Ink), 8, true);
                DrawPolyline(pts, Fa(resCol), 4, true);
            }
            else
            {
                foreach (var (p0, p1) in new[] { (new Vector2(-0.8f, -0.8f), new Vector2(0.8f, 0.8f)), (new Vector2(0.8f, -0.8f), new Vector2(-0.8f, 0.8f)) })
                {
                    DrawLine(ic + p0 * isz, ic + p1 * isz, Fa(Ink), 8, true);
                    DrawLine(ic + p1 * isz, ic + p0 * isz, Fa(resCol), 4, true);
                }
            }
        }

        // ---- ponteiro: agulha com contorno e ponta redonda ----
        if (pointer)
        {
            var dir = Dir(a);
            var needle = result ? resCol : Colors.White;
            DrawLine(dir * (R - 15), dir * (R + 13), Fa(Ink), 8, true);
            DrawLine(dir * (R - 14), dir * (R + 12), Fa(needle), 4, true);
            DrawCircle(dir * (R + 13), 4.5f, Fa(Ink));
            DrawCircle(dir * (R + 13), 3f, Fa(needle));
            if (!result)
            {
                DrawCircle(dir * R, 3.5f, Fa(Red));   // miolo vermelho no meio da faixa (fácil de achar)
                if (pressed)
                {
                    var pd = Dir(a0 + pressT * perSec) * R;
                    DrawCircle(pd, 6, Fa(Ink));
                    DrawCircle(pd, 4.5f, Fa(Yellow));
                }
            }
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        _alpha = 1f;
    }
}
