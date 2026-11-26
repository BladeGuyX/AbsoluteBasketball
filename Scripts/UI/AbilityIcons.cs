using Godot;

namespace AB;

/// <summary>v0.26: ícones vetoriais das habilidades (sem imagens), desenhados em qualquer Control (HUD, seleção, Informações).</summary>
public static class AbilityIcons
{
    /// <summary>Desenha o ícone centrado em c com raio r (fundo redondo colorido + símbolo branco).</summary>
    public static void Draw(CanvasItem ci, AbIcon icon, Vector2 c, float r, Color col, float alpha = 1f, bool background = true)
    {
        Color A(Color x) => x with { A = x.A * alpha };
        if (background)
        {
            ci.DrawCircle(c, r, A(new Color(0.07f, 0.08f, 0.12f, 0.9f)));
            ci.DrawCircle(c, r * 0.9f, A(col.Darkened(0.35f)));
            ci.DrawArc(c, r * 0.9f, 0, Mathf.Tau, 40, A(col.Lightened(0.2f)), Mathf.Max(1.5f, r * 0.08f), true);
        }
        var w = A(Colors.White);
        float lw = Mathf.Max(1.5f, r * 0.11f);
        Vector2 P(float x, float y) => c + new Vector2(x, y) * r;
        switch (icon)
        {
            case AbIcon.Eye:
            {
                var pts = new Vector2[25];
                for (int i = 0; i <= 12; i++) { float k = i / 12f; pts[i] = P(Mathf.Lerp(-0.6f, 0.6f, k), -0.32f * Mathf.Sin(k * Mathf.Pi)); }
                for (int i = 0; i < 12; i++) { float k = 1f - (i + 1) / 12f; pts[13 + i] = P(Mathf.Lerp(-0.6f, 0.6f, k), 0.32f * Mathf.Sin(k * Mathf.Pi)); }
                ci.DrawPolyline(pts, w, lw, true);
                ci.DrawCircle(c, r * 0.17f, A(new Color("ff3b3b")));
                ci.DrawCircle(c, r * 0.07f, w);
                break;
            }
            case AbIcon.Aura:
                for (int i = 0; i < 3; i++) ci.DrawArc(c, r * (0.22f + i * 0.18f), 0, Mathf.Tau, 32, A(new Color(1, 1 - i * 0.25f, 1 - i * 0.3f)), lw * 0.8f, true);
                break;
            case AbIcon.PerfectDribble:
                ci.DrawCircle(P(0, 0.05f), r * 0.3f, A(new Color("ff8a3d")));
                ci.DrawArc(P(0, 0.05f), r * 0.3f, 0, Mathf.Tau, 24, w, lw * 0.7f, true);
                ci.DrawLine(P(-0.3f, 0.05f), P(0.3f, 0.05f), w, lw * 0.5f, true);
                ci.DrawArc(P(0, 0.05f), r * 0.5f, -2.4f, -0.7f, 16, A(new Color("ff3b3b")), lw, true);
                ci.DrawArc(P(0, 0.05f), r * 0.5f, 0.7f, 2.4f, 16, A(new Color("ff3b3b")), lw, true);
                break;
            case AbIcon.Crown:
                ci.DrawColoredPolygon(new[] { P(-0.55f, 0.35f), P(-0.55f, -0.25f), P(-0.28f, 0.05f), P(0, -0.45f), P(0.28f, 0.05f), P(0.55f, -0.25f), P(0.55f, 0.35f) }, A(new Color("ffd23f")));
                ci.DrawPolyline(new[] { P(-0.55f, 0.35f), P(-0.55f, -0.25f), P(-0.28f, 0.05f), P(0, -0.45f), P(0.28f, 0.05f), P(0.55f, -0.25f), P(0.55f, 0.35f), P(-0.55f, 0.35f) }, A(new Color(0.3f, 0.2f, 0)), lw * 0.6f, true);
                ci.DrawCircle(P(0, 0.15f), r * 0.08f, A(new Color("e8313a")));
                break;
            case AbIcon.Dice:
                ci.DrawRect(new Rect2(P(-0.42f, -0.42f), new Vector2(0.84f, 0.84f) * r), w, true);
                foreach (var d in new[] { new Vector2(-0.22f, -0.22f), new Vector2(0.22f, 0.22f), Vector2.Zero, new Vector2(0.22f, -0.22f), new Vector2(-0.22f, 0.22f) })
                    ci.DrawCircle(P(d.X, d.Y), r * 0.08f, A(new Color(0.1f, 0.4f, 0.2f)));
                break;
            case AbIcon.Target:
                ci.DrawArc(c, r * 0.5f, 0, Mathf.Tau, 32, w, lw, true);
                ci.DrawArc(c, r * 0.25f, 0, Mathf.Tau, 24, w, lw, true);
                ci.DrawLine(P(-0.7f, 0), P(-0.35f, 0), w, lw, true); ci.DrawLine(P(0.35f, 0), P(0.7f, 0), w, lw, true);
                ci.DrawLine(P(0, -0.7f), P(0, -0.35f), w, lw, true); ci.DrawLine(P(0, 0.35f), P(0, 0.7f), w, lw, true);
                break;
            case AbIcon.Dash:
                for (int i = 0; i < 3; i++) ci.DrawLine(P(-0.6f, -0.25f + i * 0.25f), P(-0.15f, -0.25f + i * 0.25f), A(new Color(1, 1, 1, 0.6f)), lw * 0.7f, true);
                ci.DrawColoredPolygon(new[] { P(-0.05f, -0.4f), P(0.6f, 0), P(-0.05f, 0.4f), P(0.1f, 0) }, w);
                break;
            case AbIcon.Clover:
                for (int i = 0; i < 4; i++) { float a = i * Mathf.Tau / 4f + Mathf.Pi / 4f; ci.DrawCircle(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.25f, r * 0.24f, A(new Color("3ccf6e").Lightened(0.3f))); }
                ci.DrawLine(P(0, 0), P(0.15f, 0.6f), A(new Color("3ccf6e").Lightened(0.3f)), lw, true);
                break;
            case AbIcon.Lightning:
                ci.DrawColoredPolygon(new[] { P(0.12f, -0.65f), P(-0.38f, 0.08f), P(-0.02f, 0.08f), P(-0.15f, 0.65f), P(0.38f, -0.12f), P(0.04f, -0.12f) }, A(new Color("fff27a")));
                break;
            case AbIcon.Swap:
                ci.DrawArc(c, r * 0.45f, Mathf.Pi * 1.1f, Mathf.Pi * 1.9f, 16, w, lw, true);
                ci.DrawArc(c, r * 0.45f, Mathf.Pi * 0.1f, Mathf.Pi * 0.9f, 16, w, lw, true);
                ci.DrawColoredPolygon(new[] { P(0.3f, -0.55f), P(0.58f, -0.2f), P(0.2f, -0.2f) }, w);
                ci.DrawColoredPolygon(new[] { P(-0.3f, 0.55f), P(-0.58f, 0.2f), P(-0.2f, 0.2f) }, w);
                break;
            case AbIcon.Tower:
                ci.DrawColoredPolygon(new[] { P(0, -0.65f), P(0.32f, -0.2f), P(0.12f, -0.2f), P(0.12f, 0.45f), P(-0.12f, 0.45f), P(-0.12f, -0.2f), P(-0.32f, -0.2f) }, w);
                ci.DrawLine(P(-0.55f, 0.55f), P(0.55f, 0.55f), A(new Color("fff27a")), lw, true);
                break;
            case AbIcon.Duel:
            {
                // v0.27: duas espadas se cruzando (pontas para cima) com um raio no choque
                var gold = A(new Color("ffd23f"));
                var steel = A(new Color("e9f1ff"));
                var grip = A(new Color("7a4a2a"));
                for (int k = -1; k <= 1; k += 2)
                {
                    // espada que vai do canto de baixo (k) até o canto de cima oposto
                    var tip = new Vector2(-k * 0.56f, -0.56f);
                    var hilt = new Vector2(k * 0.34f, 0.34f);
                    var d = (tip - hilt).Normalized();
                    var n = new Vector2(-d.Y, d.X);
                    Vector2 Q(Vector2 v) => c + v * r;
                    // lâmina com ponta
                    ci.DrawColoredPolygon(new[] { Q(hilt + n * 0.07f), Q(tip - d * 0.14f + n * 0.07f), Q(tip), Q(tip - d * 0.14f - n * 0.07f), Q(hilt - n * 0.07f) }, steel);
                    ci.DrawLine(Q(hilt), Q(tip - d * 0.1f), A(new Color(0.6f, 0.7f, 0.85f)), Mathf.Max(1f, lw * 0.3f), true);
                    // guarda, cabo e pomo
                    ci.DrawLine(Q(hilt + n * 0.2f), Q(hilt - n * 0.2f), gold, lw * 1.1f, true);
                    ci.DrawLine(Q(hilt), Q(hilt - d * 0.2f), grip, lw * 1.2f, true);
                    ci.DrawCircle(Q(hilt - d * 0.24f), r * 0.065f, gold);
                }
                // raio no ponto do choque
                var bolt = A(new Color("fff27a"));
                ci.DrawColoredPolygon(new[] { P(0.06f, -0.42f), P(-0.13f, -0.12f), P(0.0f, -0.12f), P(-0.08f, 0.1f), P(0.14f, -0.2f), P(0.02f, -0.2f) }, bolt);
                ci.DrawLine(P(-0.2f, -0.28f), P(-0.32f, -0.36f), bolt, lw * 0.45f, true);
                ci.DrawLine(P(0.2f, -0.28f), P(0.32f, -0.36f), bolt, lw * 0.45f, true);
                ci.DrawLine(P(0f, -0.48f), P(0f, -0.6f), bolt, lw * 0.45f, true);
                break;
            }
        }
    }
}
