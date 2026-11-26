using System;
using Godot;

namespace AB;

/// <summary>
/// Tema e fábricas de widgets — visual claro, arredondado e colorido.
/// v0.27: tema refeito — botões com relevo (borda de baixo mais escura, afunda ao apertar), painéis com sombra, campos de
/// texto/número e listas (OptionButton) no mesmo estilo, menus suspensos, sliders, barras de rolagem e abas.
/// </summary>
public static class UiKit
{
    static Theme _theme;
    public static Theme Theme => _theme ??= BuildTheme();

    public static readonly Color Muted = new("5b6577");
    public static readonly Color Field = new("eef3fa");
    public static readonly Color FieldBorder = new("c9d6ea");
    public static readonly Color Gold = new("ffcf3d");

    static Font _bold;
    /// <summary>Fonte padrão (a da Godot) e a versão em negrito (mesma fonte, engrossada).</summary>
    public static Font Font => ThemeDB.FallbackFont;
    public static Font FontBold => _bold ??= new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.75f };

    public static StyleBoxFlat Box(Color c, int radius = 16, float padX = 22, float padY = 10)
    {
        var sb = new StyleBoxFlat { BgColor = c, AntiAliasing = true };
        sb.SetCornerRadiusAll(radius);
        sb.ContentMarginLeft = sb.ContentMarginRight = padX;
        sb.ContentMarginTop = sb.ContentMarginBottom = padY;
        return sb;
    }

    /// <summary>Botão com relevo: corpo + borda de baixo mais escura (afunda ao apertar).</summary>
    public static StyleBoxFlat Raised(Color c, int depth = 6, int radius = 16, float padX = 22, float padY = 10)
    {
        var sb = Box(c, radius, padX, padY);
        sb.BorderColor = c.Darkened(0.28f);
        sb.BorderWidthBottom = depth;
        sb.ContentMarginTop = padY + Mathf.Max(0, 6 - depth) * 0.5f;
        sb.ContentMarginBottom = padY + depth * 0.5f;
        sb.ShadowColor = new Color(0.05f, 0.1f, 0.25f, depth > 2 ? 0.18f : 0.08f);
        sb.ShadowSize = depth > 2 ? 6 : 2;
        sb.ShadowOffset = new Vector2(0, depth > 2 ? 3 : 1);
        return sb;
    }

    static StyleBoxFlat Bordered(StyleBoxFlat sb, Color border) { sb.BorderColor = border; return sb; }

    static StyleBoxFlat FieldBox(bool focus = false, bool disabled = false)
    {
        var sb = Box(disabled ? new Color("e3e8ef") : Field, 12, 16, 8);
        sb.BorderColor = focus ? Palette.Team[0] : FieldBorder;
        sb.SetBorderWidthAll(focus ? 3 : 2);
        return sb;
    }

    static StyleBoxFlat Panel()
    {
        var sb = Box(new Color(1, 1, 1, 0.97f), 28, 36, 30);
        sb.ShadowColor = new Color(0.03f, 0.08f, 0.2f, 0.32f);
        sb.ShadowSize = 28;
        sb.ShadowOffset = new Vector2(0, 10);
        sb.BorderColor = new Color("dbe5f3");
        sb.BorderWidthBottom = 6;
        return sb;
    }

    static ImageTexture Circle(int size, Color fill, Color border, float borderW)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float r = size / 2f - 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).Length();
            float a = Mathf.Clamp(r - d + 0.5f, 0, 1);
            var c = d > r - borderW ? border : fill;
            img.SetPixel(x, y, c with { A = c.A * a });
        }
        return ImageTexture.CreateFromImage(img);
    }

    static Theme BuildTheme()
    {
        var t = new Theme { DefaultFontSize = 26 };
        var blue = Palette.Team[0];

        // ---- botões ----
        t.SetStylebox("normal", "Button", Raised(blue));
        t.SetStylebox("hover", "Button", Raised(blue.Lightened(0.12f)));
        t.SetStylebox("pressed", "Button", Raised(blue.Darkened(0.12f), 2));
        t.SetStylebox("hover_pressed", "Button", Raised(blue.Darkened(0.05f), 2));
        t.SetStylebox("disabled", "Button", Raised(new Color("b9c2cf"), 3));
        var focus = new StyleBoxFlat { DrawCenter = false, BorderColor = Gold, AntiAliasing = true };
        focus.SetBorderWidthAll(4);
        focus.SetCornerRadiusAll(18);
        focus.ExpandMarginLeft = focus.ExpandMarginRight = focus.ExpandMarginTop = focus.ExpandMarginBottom = 3;
        t.SetStylebox("focus", "Button", focus);
        foreach (var k in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            t.SetColor(k, "Button", Colors.White);
        t.SetColor("font_disabled_color", "Button", new Color("f4f6f9"));
        t.SetFont("font", "Button", FontBold);
        t.SetConstant("outline_size", "Button", 0);

        // ---- listas (OptionButton) e menus suspensos ----
        t.SetStylebox("normal", "OptionButton", Bordered(Raised(Field, 4, 12, 16, 8), FieldBorder));
        t.SetStylebox("hover", "OptionButton", Bordered(Raised(new Color("e2ecf8"), 4, 12, 16, 8), new Color("aebfd8")));
        t.SetStylebox("pressed", "OptionButton", Bordered(Raised(new Color("d6e3f5"), 2, 12, 16, 8), new Color("aebfd8")));
        t.SetStylebox("hover_pressed", "OptionButton", Bordered(Raised(new Color("d6e3f5"), 2, 12, 16, 8), new Color("aebfd8")));
        t.SetStylebox("disabled", "OptionButton", Raised(new Color("e3e8ef"), 2, 12, 16, 8));
        t.SetStylebox("focus", "OptionButton", focus);
        foreach (var k in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            t.SetColor(k, "OptionButton", Palette.Ink);
        t.SetColor("font_disabled_color", "OptionButton", Muted);
        t.SetFont("font", "OptionButton", FontBold);

        var pop = Box(Colors.White, 14, 10, 10);
        pop.ShadowColor = new Color(0.03f, 0.08f, 0.2f, 0.3f);
        pop.ShadowSize = 16;
        pop.ShadowOffset = new Vector2(0, 6);
        pop.BorderColor = FieldBorder;
        pop.SetBorderWidthAll(2);
        t.SetStylebox("panel", "PopupMenu", pop);
        t.SetStylebox("hover", "PopupMenu", Box(new Color("dbe8fb"), 10, 12, 6));
        t.SetColor("font_color", "PopupMenu", Palette.Ink);
        t.SetColor("font_hover_color", "PopupMenu", Palette.Team[0].Darkened(0.3f));
        t.SetColor("font_disabled_color", "PopupMenu", Muted);
        t.SetFontSize("font_size", "PopupMenu", 24);
        t.SetConstant("v_separation", "PopupMenu", 12);
        t.SetConstant("item_start_padding", "PopupMenu", 12);

        // ---- campos de texto / número ----
        t.SetStylebox("normal", "LineEdit", FieldBox());
        t.SetStylebox("focus", "LineEdit", FieldBox(true));
        t.SetStylebox("read_only", "LineEdit", FieldBox(false, true));
        t.SetColor("font_color", "LineEdit", Palette.Ink);
        t.SetColor("font_uneditable_color", "LineEdit", Muted);
        t.SetColor("font_placeholder_color", "LineEdit", new Color("9aa6b8"));
        t.SetColor("caret_color", "LineEdit", Palette.Team[0]);
        t.SetColor("selection_color", "LineEdit", new Color(0.18f, 0.5f, 0.93f, 0.3f));

        // ---- textos ----
        t.SetColor("font_color", "Label", Palette.Ink);

        // ---- painéis ----
        t.SetStylebox("panel", "PanelContainer", Panel());
        t.SetStylebox("panel", "TabContainer", Box(new Color("f4f7fc"), 18, 22, 18));
        t.SetStylebox("tab_selected", "TabContainer", Raised(blue, 4, 12, 20, 8));
        t.SetStylebox("tab_unselected", "TabContainer", Box(new Color("e3eaf4"), 12, 20, 8));
        t.SetStylebox("tab_hovered", "TabContainer", Box(new Color("cfdcf0"), 12, 20, 8));
        t.SetStylebox("tab_focus", "TabContainer", focus);
        t.SetColor("font_selected_color", "TabContainer", Colors.White);
        t.SetColor("font_unselected_color", "TabContainer", Palette.Ink);
        t.SetColor("font_hovered_color", "TabContainer", Palette.Ink);
        t.SetFont("font", "TabContainer", FontBold);
        t.SetConstant("side_margin", "TabContainer", 4);
        t.SetConstant("tab_separation", "TabContainer", 6);

        // ---- check ----
        foreach (var type in new[] { "CheckButton", "CheckBox" })
        {
            foreach (var s in new[] { "normal", "pressed", "disabled" })
                t.SetStylebox(s, type, Box(new Color(0, 0, 0, 0.0f), 12, 12, 6));
            foreach (var s in new[] { "hover", "hover_pressed" })
                t.SetStylebox(s, type, Box(new Color(0.18f, 0.5f, 0.93f, 0.08f), 12, 12, 6));
            t.SetStylebox("focus", type, focus);
            foreach (var k in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
                t.SetColor(k, type, Palette.Ink);
        }

        // ---- slider ----
        var track = Box(new Color("dfe6f0"), 6, 0, 5);
        t.SetStylebox("slider", "HSlider", track);
        var fill = Box(blue, 6, 0, 5);
        t.SetStylebox("grabber_area", "HSlider", fill);
        t.SetStylebox("grabber_area_highlight", "HSlider", Box(blue.Lightened(0.12f), 6, 0, 5));
        t.SetIcon("grabber", "HSlider", Circle(30, Colors.White, blue, 5));
        t.SetIcon("grabber_highlight", "HSlider", Circle(30, Colors.White, blue.Lightened(0.15f), 6));
        t.SetIcon("grabber_disabled", "HSlider", Circle(30, Colors.White, new Color("b9c2cf"), 5));
        t.SetStylebox("focus", "HSlider", focus);

        // ---- barras de rolagem ----
        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            t.SetStylebox("scroll", type, Box(new Color(0, 0, 0, 0.05f), 8, 5, 5));
            t.SetStylebox("grabber", type, Box(new Color("b6c4d8"), 8, 5, 5));
            t.SetStylebox("grabber_highlight", type, Box(new Color("8ea3c2"), 8, 5, 5));
            t.SetStylebox("grabber_pressed", type, Box(blue, 8, 5, 5));
        }

        // ---- separadores ----
        var line = new StyleBoxLine { Color = new Color("dbe3ee"), Thickness = 2 };
        t.SetStylebox("separator", "HSeparator", line);
        t.SetConstant("separation", "HSeparator", 14);

        t.SetStylebox("panel", "TooltipPanel", Box(Palette.Ink, 10, 12, 8));
        t.SetColor("font_color", "TooltipLabel", Colors.White);
        return t;
    }

    public static Button Btn(string text, Action onClick, int minWidth = 360)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 62), FocusMode = Control.FocusModeEnum.All };
        if (onClick != null) b.Pressed += onClick;
        b.MouseEntered += () => { if (!b.Disabled) Sfx.PlayUi(b, "tick", -20f); };
        b.Pressed += () => Sfx.PlayUi(b, "click", -12f);
        return b;
    }

    /// <summary>Botão de outra cor (mesmo relevo). Ex.: verde para "começar", vermelho para "sair", cinza para "voltar".</summary>
    public static Button Btn(string text, Action onClick, Color color, int minWidth = 360)
    {
        var b = Btn(text, onClick, minWidth);
        Tint(b, color);
        return b;
    }

    public static void Tint(Button b, Color color)
    {
        b.AddThemeStyleboxOverride("normal", Raised(color));
        b.AddThemeStyleboxOverride("hover", Raised(color.Lightened(0.12f)));
        b.AddThemeStyleboxOverride("pressed", Raised(color.Darkened(0.12f), 2));
        b.AddThemeStyleboxOverride("hover_pressed", Raised(color.Darkened(0.05f), 2));
    }

    public static readonly Color Green = new("27ae60"), Red = new("e5534b"), Grey = new("7d8aa0"), Purple = new("8e5cf0");

    public static Label Lbl(string text, int size = 26, Color? color = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = new Label { Text = text, HorizontalAlignment = align };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
        return l;
    }

    /// <summary>Título de tela (negrito) com uma faixa colorida embaixo.</summary>
    public static Control Title(string text, int size = 48, Color? accent = null)
    {
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 4);
        var l = Lbl(text, size);
        l.AddThemeFontOverride("font", FontBold);
        vb.AddChild(l);
        var bar = new Panel { CustomMinimumSize = new Vector2(84, 7), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        bar.AddThemeStyleboxOverride("panel", Box(accent ?? new Color("ff8a3d"), 4, 0, 0));
        vb.AddChild(bar);
        return vb;
    }

    /// <summary>Subtítulo de seção (negrito, cor de destaque).</summary>
    public static Label Section(string text, int size = 30, Color? color = null)
    {
        var l = Lbl(text, size, color);
        l.AddThemeFontOverride("font", FontBold);
        return l;
    }

    /// <summary>Texto grande com contorno, para HUD sobre o 3D.</summary>
    public static Label Outlined(string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Center, int outline = 14)
    {
        return new Label
        {
            Text = text,
            HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = new LabelSettings
            {
                FontSize = size, FontColor = color, OutlineSize = outline, OutlineColor = Palette.Ink,
                ShadowSize = 0,
            },
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    public static T Anchor<T>(T c, float al, float at, float ar, float ab, float ol, float ot, float or, float ob) where T : Control
    {
        c.AnchorLeft = al; c.AnchorTop = at; c.AnchorRight = ar; c.AnchorBottom = ab;
        c.OffsetLeft = ol; c.OffsetTop = ot; c.OffsetRight = or; c.OffsetBottom = ob;
        return c;
    }

    public static T FullRect<T>(T c) where T : Control
    {
        c.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return c;
    }

    /// <summary>Fundo escuro de menus sobre o jogo (pausa, configurações, informações): escurece mais nas bordas.</summary>
    public static Control Dim(float alpha = 0.6f)
    {
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.04f, 0.07f, 0.14f, alpha * 0.75f));
        grad.SetColor(1, new Color(0.02f, 0.03f, 0.08f, Mathf.Min(1f, alpha * 1.25f)));
        return FullRect(new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = grad, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1.1f, 1.1f), Width = 128, Height = 128 },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Stop,
        });
    }

    /// <summary>Painel escuro desenhado (menus da partida): sombra, corpo e faixa colorida no topo.</summary>
    public static void DrawPanel(CanvasItem ci, Rect2 r, Color accent, float alpha, float radius)
    {
        var sh = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.35f * alpha), AntiAliasing = true, ShadowColor = new Color(0, 0, 0, 0.3f * alpha), ShadowSize = 24 };
        sh.SetCornerRadiusAll((int)radius);
        ci.DrawStyleBox(sh, r with { Position = r.Position + new Vector2(0, 8) });
        var body = new StyleBoxFlat { BgColor = new Color(0.07f, 0.09f, 0.15f, 0.96f * alpha), AntiAliasing = true, BorderColor = accent with { A = alpha }, BorderWidthTop = 6 };
        body.SetCornerRadiusAll((int)radius);
        ci.DrawStyleBox(body, r);
    }

    public static HBoxContainer SliderRow(string label, float min, float max, float step, float value, Action<float> onChange, Func<float, string> fmt = null)
    {
        fmt ??= v => Mathf.RoundToInt(v * 100) + "%";
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(330, 0) });
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 36), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var vl = Lbl(fmt(value), 24, Palette.Team[0], HorizontalAlignment.Right);
        vl.CustomMinimumSize = new Vector2(90, 0);
        vl.AddThemeFontOverride("font", FontBold);
        s.ValueChanged += v => { vl.Text = fmt((float)v); onChange((float)v); };
        row.AddChild(s);
        row.AddChild(vl);
        return row;
    }

    public static CheckButton CheckRow(string label, bool value, Action<bool> onChange)
    {
        var c = new CheckButton { Text = label, ButtonPressed = value };
        c.Toggled += b => onChange(b);
        return c;
    }
}
