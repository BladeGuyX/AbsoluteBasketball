using Godot;

namespace AB;

/// <summary>
/// v0.27: fundo dos menus fora da partida (principal, online, lobby): céu em degradê com sol, nuvens andando devagar,
/// a quadra em perspectiva sobre a areia e uma bola de basquete grande e apagada girando atrás de tudo. Só desenho
/// (sem imagens), escala com a altura da tela.
/// </summary>
public partial class MenuBackdrop : Control
{
    float _t;
    static readonly Color SkyTop = new("1f5fc4"), SkyMid = new("4d9be8"), SkyLow = new("bfe6ff");

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
        var grad = new Gradient();
        grad.Offsets = new[] { 0f, 0.55f, 1f };
        grad.Colors = new[] { SkyTop, SkyMid, SkyLow };
        var sky = UiKit.FullRect(new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = grad, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 0.78f), Width = 8, Height = 256 },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            ShowBehindParent = true,
        });
        AddChild(sky);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var sz = Size;
        float s = sz.Y / 1080f;
        float horizon = sz.Y * 0.78f;

        // sol
        var sun = new Vector2(sz.X * 0.82f, sz.Y * 0.2f);
        for (int i = 6; i >= 1; i--) DrawCircle(sun, (60 + i * 34) * s, new Color(1f, 0.95f, 0.75f, 0.045f));
        DrawCircle(sun, 70 * s, new Color(1f, 0.96f, 0.8f, 0.95f));

        // bola grande e apagada girando (atrás do título)
        var bc = new Vector2(sz.X * 0.16f, sz.Y * 0.36f);
        float br = 260 * s;
        DrawCircle(bc, br, new Color(1f, 0.55f, 0.24f, 0.13f));
        float rot = _t * 0.12f;
        var seam = new Color(1f, 1f, 1f, 0.12f);
        DrawArc(bc, br, 0, Mathf.Tau, 64, seam, 4 * s, true);
        DrawLine(bc + Rot(new Vector2(-br, 0), rot), bc + Rot(new Vector2(br, 0), rot), seam, 4 * s, true);
        DrawLine(bc + Rot(new Vector2(0, -br), rot), bc + Rot(new Vector2(0, br), rot), seam, 4 * s, true);
        DrawArc(bc + Rot(new Vector2(-br * 1.25f, 0), rot), br * 0.85f, rot - 0.85f, rot + 0.85f, 32, seam, 4 * s, true);
        DrawArc(bc + Rot(new Vector2(br * 1.25f, 0), rot), br * 0.85f, rot + Mathf.Pi - 0.85f, rot + Mathf.Pi + 0.85f, 32, seam, 4 * s, true);

        // nuvens
        for (int i = 0; i < 5; i++)
        {
            float speed = 14f + i * 5f;
            float w = sz.X + 600 * s;
            float x = Mathf.PosMod(i * 520f * s + _t * speed * s, w) - 300 * s;
            float y = sz.Y * (0.1f + 0.09f * (i % 3)) + Mathf.Sin(_t * 0.3f + i) * 6 * s;
            Cloud(new Vector2(x, y), (0.8f + 0.25f * (i % 2)) * s);
        }

        // areia
        DrawRect(new Rect2(0, horizon, sz.X, sz.Y - horizon), Palette.Sand);
        DrawRect(new Rect2(0, horizon, sz.X, 6 * s), new Color(1f, 1f, 1f, 0.35f));

        // quadra em perspectiva
        float cx = sz.X / 2;
        float topW = sz.X * 0.42f, botW = sz.X * 1.25f;
        float y0 = horizon + 18 * s, y1 = sz.Y + 40 * s;
        var court = new[] { new Vector2(cx - topW / 2, y0), new Vector2(cx + topW / 2, y0), new Vector2(cx + botW / 2, y1), new Vector2(cx - botW / 2, y1) };
        var apron = new[] { court[0] + new Vector2(-14 * s, -8 * s), court[1] + new Vector2(14 * s, -8 * s), court[2] + new Vector2(40 * s, 0), court[3] + new Vector2(-40 * s, 0) };
        DrawColoredPolygon(apron, Palette.CourtApron);
        DrawColoredPolygon(court, Palette.Court);
        var line = new Color(1, 1, 1, 0.85f);
        DrawPolyline(new[] { court[0], court[1], court[2], court[3], court[0] }, line, 3 * s, true);
        // linha do meio e círculo central (achatado pela perspectiva)
        DrawLine(new Vector2(cx, y0), new Vector2(cx, y1), line, 3 * s, true);
        var mid = new Vector2(cx, Mathf.Lerp(y0, y1, 0.38f));
        Ellipse(mid, 120 * s, 34 * s, line, 3 * s);
        // garrafões nas laterais
        Key(court, true, line, s);
        Key(court, false, line, s);
    }

    void Key(Vector2[] court, bool left, Color line, float s)
    {
        // aproximação: um trapézio colado na lateral esquerda/direita da quadra
        float k0 = 0.28f, k1 = 0.62f;
        Vector2 Edge(float k) => left ? court[0].Lerp(court[3], k) : court[1].Lerp(court[2], k);
        float inward = left ? 1 : -1;
        var a = Edge(k0); var b = Edge(k1);
        float depthA = Mathf.Lerp(70, 200, k0) * s, depthB = Mathf.Lerp(70, 200, k1) * s;
        var pts = new[] { a, a + new Vector2(inward * depthA, 0), b + new Vector2(inward * depthB, 0), b };
        DrawColoredPolygon(pts, new Color(1f, 0.7f, 0.28f, 0.75f));
        DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3] }, line, 3 * s, true);
    }

    void Ellipse(Vector2 c, float rx, float ry, Color col, float w)
    {
        var pts = new Vector2[49];
        for (int i = 0; i <= 48; i++) { float a = i / 48f * Mathf.Tau; pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry); }
        DrawPolyline(pts, col, w, true);
    }

    void Cloud(Vector2 p, float s)
    {
        var c = new Color(0.98f, 0.99f, 1f);
        var sh = new Color(0.72f, 0.83f, 0.97f);
        DrawCircle(p + new Vector2(0, 10) * s, 46 * s, sh);
        DrawCircle(p + new Vector2(60, 14) * s, 40 * s, sh);
        DrawCircle(p, 46 * s, c);
        DrawCircle(p + new Vector2(52, -18) * s, 54 * s, c);
        DrawCircle(p + new Vector2(110, 4) * s, 40 * s, c);
        DrawCircle(p + new Vector2(-46, 10) * s, 32 * s, c);
        DrawRect(new Rect2(p + new Vector2(-46, 10) * s, new Vector2(156, 32) * s), c);
    }

    static Vector2 Rot(Vector2 v, float a) => v.Rotated(a);
}
