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

        var cc = M.Cut.CheckFor(p); // v0.26: skill check da cutscene (dono / alvo do duelo)
        // ---- online: o cliente mostra o resultado do próprio aperto na hora (ponteiro onde ele viu) ----
        // (no duelo, o próximo skill check começa na hora: ele tem prioridade sobre o resultado do anterior)
        bool newerCut = cc != null && cc.Active && cc.Seq != M.ClientSkillSeq && M.ClientSkillSeq >= 0;
        if (M.IsClient && M.ClientSkillHold > 0 && !newerCut)
        {
            DrawSkillCheck(c, t, M.ClientSkillT, M.ClientSkillDur, false, true, M.ClientSkillT, M.ClientSkillHold, M.ClientSkillOk, 1f, M.ClientSkillWindow);
            return;
        }
        bool mute = M.IsClient && M.ClientSkillMuteT > 0; // resultado do host chegando atrasado: já foi mostrado

        if (M.Cut.Active)
        {
            if (cc != null && (cc.Active || (cc.Flash > 0 && !mute)))
                DrawSkillCheck(c, t, Mathf.Max(cc.T, 0), cc.Dur, cc.Active, cc.Pressed, cc.PressT, cc.Flash, cc.Ok, cc.T + t.CutCheckPrep, cc.Window, pointer: cc.T >= 0 || !cc.Active);
            return;
        }

        // ---- enterrada: skill check ----
        if ((p.Flight == FlightKind.Dunk && !p.DunkJinx) || (p.SkillFlash > 0 && !mute)) // v0.27: enterrada com a Presença não tem skill check
        {
            DrawSkillCheck(c, t, p.FlightT, p.FlightDur, p.Flight == FlightKind.Dunk, p.DunkPressed, p.DunkPressT, p.SkillFlash, p.SkillFlashOk, p.FlightT, t.DunkWindow);
            return;
        }
        // ---- lances livres: mesmo skill check ----
        if ((M.FtChecking && M.FtShooter == p && M.FtT >= 0) || (M.FtFlash > 0 && M.FtFlashPlayer == p && !mute))
        {
            DrawSkillCheck(c, t, M.FtT, M.FtDur, M.FtChecking && M.FtShooter == p, M.FtPressed, M.FtPressT, M.FtFlash, M.FtFlashOk,
                M.FtT + t.FreeThrowPrep, t.DunkWindow);
            return;
        }
        if (M.FtChecking && M.FtShooter == p)
        {
            // preparação: anel com a zona marcada, esperando o ponteiro
            DrawSkillCheck(c, t, 0, M.FtDur, true, false, 0, 0, false, M.FtT + t.FreeThrowPrep, t.DunkWindow, pointer: false);
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
        else if (p.StealFail > 0) Ring(c, 30, p.StealFail / Mathf.Max(p.St.StealFailTime, 0.1f), Red, true);
        else if (p.IsDribbling) Ring(c, 30, p.DribbleActive / Mathf.Max(t.DribbleDuration + (p.Ab.PerfectDribble ? t.KingDribbleExtra : 0), 0.1f), p.Ab.PerfectDribble ? Red : Orange, true);
        else if (p.LandLock > 0) Ring(c, 30, p.LandLock / Mathf.Max(p.St.LandLockTime, 0.1f), Grey, true);
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
    /// (ou onde passou do tempo), zona e ponteiro verdes (certo) ou vermelhos (errado) e um ✓/✗. Design simples (v0.20): sem rastro.
    /// <paramref name="introT"/> = segundos desde que o anel apareceu (animação de entrada).
    /// </summary>
    void DrawSkillCheck(Vector2 c, Tuning t, float time, float durRaw, bool active, bool pressed, float pressT, float flash, bool flashOk,
        float introT, float window, bool pointer = true)
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

        // ---- faixa de fundo + traço fino no ponto de partida ----
        DrawArc(o, R, 0, Mathf.Tau, 80, Fa(Band), 14, true);
        DrawArc(o, R, 0, Mathf.Tau, 80, Fa(new Color(1, 1, 1, 0.22f)), 2, true);
        var d0 = Dir(a0);
        DrawLine(d0 * (R - 5), d0 * (R + 5), Fa(new Color(1, 1, 1, 0.5f)), 2, true);

        // ---- ângulo do ponteiro (no resultado: parado onde apertou / onde acabou o tempo) ----
        float stopT = result && pressed ? pressT : time;
        float a = a0 + Mathf.Clamp(stopT, 0, dur + window) * perSec;

        // ---- zona certa (centrada no toque na cesta) ----
        float zc = a0 + Sweep;
        float zh = Mathf.Max(window * 0.5f * perSec, 0.035f);
        var zoneCol = result ? resCol : Gold;
        DrawArc(o, R, zc - zh, zc + zh, 12, Fa(Ink), 12, true);
        DrawArc(o, R, zc - zh, zc + zh, 12, Fa(zoneCol with { A = pointer || result ? 1f : 0.6f }), 9, true);

        // ---- resultado: ✓ / ✗ do lado da zona ----
        if (result)
        {
            var ic = Dir(zc) * (R + 26);
            const float isz = 8f;
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

        // ---- ponteiro: agulha simples com contorno ----
        if (pointer)
        {
            var dir = Dir(a);
            var needle = result ? resCol : Red;
            DrawLine(dir * (R - 13), dir * (R + 13), Fa(Ink), 8, true);
            DrawLine(dir * (R - 12), dir * (R + 12), Fa(needle), 4, true);
            if (!result && pressed)
            {
                var pd = Dir(a0 + pressT * perSec) * R;
                DrawCircle(pd, 6, Fa(Ink));
                DrawCircle(pd, 4.5f, Fa(Yellow));
            }
        }

        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        _alpha = 1f;
    }
}
