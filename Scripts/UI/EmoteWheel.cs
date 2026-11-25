using Godot;

namespace AB;

/// <summary>
/// v0.21: roda de expressões. Segure B (controle) / Q (teclado), aponte com o analógico esquerdo ou o mouse e SOLTE
/// o botão: o personagem fica com a expressão por Tuning.EmoteDuration s (ou até escolher outra). Soltar no meio
/// (sem apontar) não muda nada. Enquanto aberta: o mouse não gira a câmera e, no controle, o analógico esquerdo não anda.
/// A escolha vai no input do jogador (<see cref="PlayerInput.Emote"/>), então funciona online.
/// </summary>
public partial class EmoteWheel : Control
{
    public static bool IsOpen { get; private set; }
    static byte _chosen;

    /// <summary>Expressão escolhida desde o último tick (0 = nenhuma). Lida pelo LocalInputReader.</summary>
    public static byte TakeChosen()
    {
        var c = _chosen;
        _chosen = 0;
        return c;
    }

    public Match M;
    Vector2 _mouse;
    int _sel = -1;
    float _anim;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        UiKit.FullRect(this);
        IsOpen = false;
    }

    public override void _ExitTree() => IsOpen = false;

    public override void _Input(InputEvent e)
    {
        if (IsOpen && e is InputEventMouseMotion mm)
        {
            _mouse += mm.ScreenRelative;
            if (_mouse.Length() > 160f) _mouse = _mouse.Normalized() * 160f;
        }
    }

    public override void _Process(double delta)
    {
        bool can = M != null && GodotObject.IsInstanceValid(M.LocalPlayer) && !M.InputBlocked && GetWindow().HasFocus();
        bool held = can && Input.IsActionPressed(Act.Emote);
        if (held && !IsOpen) { IsOpen = true; _mouse = Vector2.Zero; _sel = -1; }
        if (IsOpen)
        {
            var stick = Input.GetVector(Act.MoveL, Act.MoveR, Act.MoveF, Act.MoveB);
            Vector2 v = Vector2.Zero;
            if (_mouse.Length() > 28f) v = _mouse;
            else if (InputDevice.Gamepad && stick.Length() > 0.45f) v = stick;
            if (v != Vector2.Zero) _sel = Slice(v);
            else if (InputDevice.Gamepad) _sel = -1; // soltou o analógico no meio: cancela
            if (!held)
            {
                if (can && _sel >= 0) _chosen = (byte)(_sel + 1);
                IsOpen = false;
            }
        }
        _anim = Mathf.MoveToward(_anim, IsOpen ? 1f : 0f, (float)delta * 9f);
        Visible = _anim > 0.001f;
        if (Visible) QueueRedraw();
    }

    static int Slice(Vector2 v)
    {
        float a = Mathf.RadToDeg(Mathf.Atan2(v.Y, v.X)) + 90f + 30f; // 0 = em cima, sentido horário
        a = ((a % 360f) + 360f) % 360f;
        return Mathf.Clamp((int)(a / 60f), 0, Expressions.Count - 1);
    }

    public override void _Draw()
    {
        float s = Size.Y / 1080f * (0.85f + 0.15f * _anim);
        var c = Size / 2f;
        float R = 190f * s;
        float alpha = _anim;
        DrawCircle(c, R, new Color(0.06f, 0.07f, 0.11f, 0.62f * alpha));
        for (int i = 0; i < Expressions.Count; i++)
        {
            float a0 = Mathf.DegToRad(-120f + 60f * i), a1 = a0 + Mathf.DegToRad(60f);
            if (i == _sel)
            {
                var pts = new Vector2[18];
                pts[0] = c;
                for (int k = 0; k < 17; k++)
                {
                    float a = Mathf.Lerp(a0, a1, k / 16f);
                    pts[k + 1] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R;
                }
                DrawColoredPolygon(pts, new Color(0.9f, 0.15f, 0.12f, 0.55f * alpha));
            }
            DrawLine(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * R * 0.3f, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * R,
                new Color(1, 1, 1, 0.18f * alpha), 2f * s);
            float am = (a0 + a1) / 2f;
            var p = c + new Vector2(Mathf.Cos(am), Mathf.Sin(am)) * R * 0.64f;
            float big = i == _sel ? 1.18f : 1f;
            DrawFace(p + new Vector2(0, -8 * s), 30f * s * big, i + 1, alpha);
            Label(p + new Vector2(0, 40 * s * big), Expressions.Label[i + 1], (int)(17 * s * big), alpha);
        }
        DrawArc(c, R, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.35f * alpha), 3f * s);
        DrawCircle(c, R * 0.26f, new Color(0.06f, 0.07f, 0.11f, 0.8f * alpha));
        Label(c + new Vector2(0, 6 * s), _sel >= 0 ? Expressions.Label[_sel + 1] : "Expressões", (int)(15 * s), alpha);
    }

    void Label(Vector2 center, string text, int size, float alpha)
    {
        var f = ThemeDB.FallbackFont;
        var sz = f.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var pos = center - new Vector2(sz.X / 2f, -size * 0.35f);
        DrawStringOutline(f, pos, text, HorizontalAlignment.Left, -1, size, 4, new Color(0, 0, 0, 0.8f * alpha));
        DrawString(f, pos, text, HorizontalAlignment.Left, -1, size, new Color(1, 1, 1, alpha));
    }

    /// <summary>Carinha simples de cada expressão (cabelo, olhos vermelhos, boca).</summary>
    void DrawFace(Vector2 p, float r, int id, float alpha)
    {
        Color A(Color col) => col with { A = col.A * alpha };
        var skin = A(new Color("f5e6d3"));
        var hair = A(new Color("1e1e24"));
        var ink = A(new Color("141416"));
        var eye = A(new Color("e3121c"));
        var mouth = A(new Color("5c1014"));
        float w = Mathf.Max(1.5f, r * 0.09f);
        DrawCircle(p + new Vector2(0, -r * 0.12f), r * 1.08f, hair);
        DrawCircle(p + new Vector2(0, r * 0.08f), r * 0.92f, skin);
        // franja
        for (int k = -2; k <= 2; k++)
        {
            var b = p + new Vector2(k * r * 0.32f, -r * 0.55f);
            DrawColoredPolygon(new[] { b + new Vector2(-r * 0.2f, -r * 0.2f), b + new Vector2(r * 0.2f, -r * 0.2f), b + new Vector2(k * r * 0.04f, r * 0.32f) }, hair);
        }
        float ey = p.Y + r * 0.08f, ex = r * 0.38f, my = p.Y + r * 0.55f;
        void Eye(float sx, float h, float lidTilt)
        {
            var e = new Vector2(p.X + sx * ex, ey);
            DrawColoredPolygon(new[] { e + new Vector2(-r * 0.2f, -h * 0.2f + lidTilt * sx * -1), e + new Vector2(r * 0.2f, -h * 0.2f + lidTilt * sx),
                e + new Vector2(r * 0.16f, h * 0.55f), e + new Vector2(-r * 0.16f, h * 0.55f) }, eye);
            DrawLine(e + new Vector2(-r * 0.24f, -h * 0.2f + lidTilt * sx * -1), e + new Vector2(r * 0.24f, -h * 0.2f + lidTilt * sx), ink, w);
        }
        void Brow(float sx, float tilt, float lift)
        {
            var e = new Vector2(p.X + sx * ex, ey - r * 0.38f - lift);
            DrawLine(e + new Vector2(-r * 0.2f, tilt * sx * -1), e + new Vector2(r * 0.2f, tilt * sx), ink, w);
        }
        switch (id)
        {
            case Expressions.Serio:
                Eye(1, r * 0.4f, -r * 0.04f); Eye(-1, r * 0.4f, -r * 0.04f);
                Brow(1, -r * 0.05f, 0); Brow(-1, -r * 0.05f, 0);
                DrawLine(new Vector2(p.X - r * 0.16f, my), new Vector2(p.X + r * 0.16f, my), ink, w);
                break;
            case Expressions.Focado:
                Eye(1, r * 0.28f, -r * 0.07f); Eye(-1, r * 0.28f, -r * 0.07f);
                Brow(1, -r * 0.1f, -r * 0.04f); Brow(-1, -r * 0.1f, -r * 0.04f);
                DrawLine(new Vector2(p.X - r * 0.1f, my), new Vector2(p.X + r * 0.1f, my), ink, w);
                break;
            case Expressions.SorrisoFrio:
                Eye(1, r * 0.36f, -r * 0.02f); Eye(-1, r * 0.3f, -r * 0.06f);
                Brow(1, r * 0.04f, r * 0.06f); Brow(-1, -r * 0.08f, 0);
                DrawPolyline(new[] { new Vector2(p.X - r * 0.18f, my), new Vector2(p.X + r * 0.05f, my + r * 0.02f), new Vector2(p.X + r * 0.22f, my - r * 0.1f) }, ink, w);
                break;
            case Expressions.Bravo:
                Eye(1, r * 0.3f, -r * 0.12f); Eye(-1, r * 0.3f, -r * 0.12f);
                Brow(1, -r * 0.16f, -r * 0.05f); Brow(-1, -r * 0.16f, -r * 0.05f);
                DrawRect(new Rect2(p.X - r * 0.22f, my - r * 0.1f, r * 0.44f, r * 0.22f), mouth);
                DrawRect(new Rect2(p.X - r * 0.17f, my - r * 0.07f, r * 0.34f, r * 0.15f), A(Colors.White));
                DrawLine(new Vector2(p.X - r * 0.17f, my), new Vector2(p.X + r * 0.17f, my), ink, w * 0.6f);
                break;
            case Expressions.Feliz:
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    var e = new Vector2(p.X + sx * ex, ey + r * 0.06f);
                    DrawPolyline(new[] { e + new Vector2(-r * 0.18f, r * 0.06f), e + new Vector2(0, -r * 0.12f), e + new Vector2(r * 0.18f, r * 0.06f) }, ink, w * 1.2f);
                    Brow(sx, r * 0.03f, r * 0.08f);
                }
                DrawColoredPolygon(new[] { new Vector2(p.X - r * 0.24f, my - r * 0.06f), new Vector2(p.X + r * 0.24f, my - r * 0.06f), new Vector2(p.X + r * 0.12f, my + r * 0.16f), new Vector2(p.X - r * 0.12f, my + r * 0.16f) }, mouth);
                break;
            default: // surpreso
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    var e = new Vector2(p.X + sx * ex, ey);
                    DrawCircle(e, r * 0.2f, eye);
                    DrawArc(e, r * 0.21f, Mathf.Pi, Mathf.Tau, 12, ink, w * 0.8f);
                    Brow(sx, r * 0.03f, r * 0.12f);
                }
                DrawCircle(new Vector2(p.X, my + r * 0.02f), r * 0.1f, mouth);
                break;
        }
    }
}
