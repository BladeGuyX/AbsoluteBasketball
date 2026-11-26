using Godot;

namespace AB;

/// <summary>
/// v0.28: barra de estamina e cargas de drible do jogador local (desenhadas). Barra arredondada com contorno, brilho em cima
/// e marquinhas a cada 25%; a cor vai de verde para laranja/vermelho quando acaba (e pisca quando não dá para correr).
/// Cargas de drible = bolinhas de basquete; a que está recarregando enche como um relógio.
/// </summary>
public partial class StaminaHud : Control
{
    public Match M;
    float _shown = -1, _t;
    static readonly Color Green = new("5ad17a"), Yellow = new("ffc83d"), Red = new("ff5a4a"), Ball = new("ff8a3d");
    static readonly Color Back = new(0.07f, 0.09f, 0.15f, 0.72f), Ink = new(0.05f, 0.06f, 0.1f, 0.9f);

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        var p = M?.LocalPlayer;
        if (p == null || !GodotObject.IsInstanceValid(p)) { Visible = false; return; }
        float k = p.Stamina / Mathf.Max(p.St.StaminaMax, 1f);
        _shown = _shown < 0 ? k : Mathf.MoveToward(_shown, k, (float)delta * 1.5f); // rastro claro que acompanha devagar
        QueueRedraw();
    }

    public override void _Draw()
    {
        var p = M?.LocalPlayer;
        if (p == null || !GodotObject.IsInstanceValid(p)) return;
        var t = Tuning.I;
        float k = Mathf.Clamp(p.Stamina / Mathf.Max(p.St.StaminaMax, 1f), 0, 1);
        int max = p.St.DribbleCharges;
        const float pipR = 12f, pipGap = 8f;
        float pipsW = max * (pipR * 2) + (max - 1) * pipGap;
        float barW = 360f, barH = 18f, gap = 22f;
        float total = barW + gap + pipsW;
        float x0 = Size.X / 2 - total / 2, cy = Size.Y / 2;

        // ---- barra ----
        var bar = new Rect2(x0, cy - barH / 2, barW, barH);
        Box(bar.Grow(3), Ink, 12);
        Box(bar, Back, 9);
        bool cantRun = !p.Running && p.Stamina < t.StaminaMinToRun; // mesma regra do Player (não começa a correr)
        var fillCol = k > 0.5f ? Green : k > 0.25f ? Yellow.Lerp(Green, (k - 0.25f) / 0.25f) : Red.Lerp(Yellow, k / 0.25f);
        if (cantRun) fillCol = fillCol.Lerp(Colors.White, 0.25f + 0.25f * Mathf.Sin(_t * 12f));
        // rastro (quanto acabou de gastar)
        if (_shown > k + 0.002f) Box(new Rect2(bar.Position, new Vector2(barW * _shown, barH)), new Color(1f, 1f, 1f, 0.28f), 9);
        if (k > 0.001f)
        {
            float fw = Mathf.Max(barW * k, barH);
            var fill = new Rect2(bar.Position, new Vector2(fw, barH));
            Box(fill, fillCol, 9);
            Box(new Rect2(fill.Position + new Vector2(4, 2), new Vector2(fw - 8, barH * 0.36f)), new Color(1f, 1f, 1f, 0.3f), 5); // brilho
        }
        for (int i = 1; i < 4; i++)
        {
            float x = bar.Position.X + barW * i / 4f;
            DrawLine(new Vector2(x, bar.Position.Y + 3), new Vector2(x, bar.End.Y - 3), new Color(0, 0, 0, 0.25f), 2f);
        }
        // raiozinho no começo da barra (estamina)
        var bc = new Vector2(bar.Position.X - 18, cy);
        DrawCircle(bc, 13, Ink);
        DrawCircle(bc, 11, fillCol.Darkened(0.15f));
        DrawColoredPolygon(new[] { bc + new Vector2(1.5f, -7), bc + new Vector2(-4.5f, 1), bc + new Vector2(-0.5f, 1), bc + new Vector2(-2, 7), bc + new Vector2(4.5f, -1.5f), bc + new Vector2(0.5f, -1.5f) }, Colors.White);

        // ---- cargas de drible ----
        float px = bar.End.X + gap + pipR;
        for (int i = 0; i < max; i++)
        {
            var c = new Vector2(px + i * (pipR * 2 + pipGap), cy);
            bool full = i < p.DribbleCharges;
            DrawCircle(c, pipR + 2.5f, Ink);
            if (full) BallIcon(c, pipR, 1f);
            else
            {
                DrawCircle(c, pipR, Back);
                if (i == p.DribbleCharges)
                {
                    float rk = Mathf.Clamp(p.DribbleRecharge / Mathf.Max(t.DribbleRecharge, 0.1f), 0, 1);
                    if (rk > 0.01f) Pie(c, pipR - 1, rk, Ball with { A = 0.55f });
                }
                DrawArc(c, pipR - 0.5f, 0, Mathf.Tau, 24, new Color(1, 1, 1, 0.18f), 1.5f, true);
            }
        }
        if (p.IsDribbling) // drible ativo: anel girando em volta das cargas
        {
            var a = new Vector2(px - pipR - 5, cy - pipR - 5);
            var sb = new StyleBoxFlat { DrawCenter = false, BorderColor = new Color(1f, 0.82f, 0.3f, 0.6f + 0.4f * Mathf.Sin(_t * 16f)), AntiAliasing = true };
            sb.SetBorderWidthAll(2); sb.SetCornerRadiusAll(16);
            DrawStyleBox(sb, new Rect2(a, new Vector2(pipsW + 10, pipR * 2 + 10)));
        }
    }

    void BallIcon(Vector2 c, float r, float alpha)
    {
        DrawCircle(c, r, Ball with { A = alpha });
        DrawCircle(c + new Vector2(-r * 0.3f, -r * 0.35f), r * 0.35f, new Color(1f, 1f, 1f, 0.25f * alpha));
        var seam = new Color(0.25f, 0.1f, 0.02f, 0.85f * alpha);
        DrawLine(c + new Vector2(-r, 0), c + new Vector2(r, 0), seam, 1.5f, true);
        DrawLine(c + new Vector2(0, -r), c + new Vector2(0, r), seam, 1.5f, true);
        DrawArc(c + new Vector2(-r * 1.25f, 0), r * 0.85f, -0.85f, 0.85f, 10, seam, 1.5f, true);
        DrawArc(c + new Vector2(r * 1.25f, 0), r * 0.85f, Mathf.Pi - 0.85f, Mathf.Pi + 0.85f, 10, seam, 1.5f, true);
    }

    void Pie(Vector2 c, float r, float k, Color col)
    {
        int n = Mathf.Max(3, (int)(24 * k));
        var pts = new Vector2[n + 2];
        pts[0] = c;
        for (int i = 0; i <= n; i++) { float a = -Mathf.Pi / 2 + Mathf.Tau * k * i / n; pts[i + 1] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; }
        DrawColoredPolygon(pts, col);
    }

    void Box(Rect2 r, Color c, int radius)
    {
        var sb = new StyleBoxFlat { BgColor = c, AntiAliasing = true };
        sb.SetCornerRadiusAll(radius);
        DrawStyleBox(sb, r);
    }
}
