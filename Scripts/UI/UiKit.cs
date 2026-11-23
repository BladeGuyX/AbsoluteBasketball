using System;
using Godot;

namespace AB;

/// <summary>Tema e fábricas de widgets — visual claro, arredondado e colorido.</summary>
public static class UiKit
{
    static Theme _theme;
    public static Theme Theme => _theme ??= BuildTheme();

    public static StyleBoxFlat Box(Color c, int radius = 16, float padX = 22, float padY = 10)
    {
        var sb = new StyleBoxFlat { BgColor = c };
        sb.SetCornerRadiusAll(radius);
        sb.ContentMarginLeft = sb.ContentMarginRight = padX;
        sb.ContentMarginTop = sb.ContentMarginBottom = padY;
        return sb;
    }

    static Theme BuildTheme()
    {
        var t = new Theme { DefaultFontSize = 26 };
        var blue = Palette.Team[0];
        t.SetStylebox("normal", "Button", Box(blue));
        t.SetStylebox("hover", "Button", Box(blue.Lightened(0.15f)));
        t.SetStylebox("pressed", "Button", Box(blue.Darkened(0.2f)));
        t.SetStylebox("disabled", "Button", Box(new Color("b9c2cf")));
        var focus = new StyleBoxFlat { DrawCenter = false, BorderColor = new Color("ffcf3d") };
        focus.SetBorderWidthAll(4);
        focus.SetCornerRadiusAll(16);
        t.SetStylebox("focus", "Button", focus);
        foreach (var k in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            t.SetColor(k, "Button", Colors.White);
        t.SetColor("font_disabled_color", "Button", new Color("eef1f5"));

        foreach (var type in new[] { "CheckButton", "CheckBox" })
        {
            foreach (var s in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
                t.SetStylebox(s, type, Box(new Color(0, 0, 0, 0.04f), 12, 12, 6));
            foreach (var k in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
                t.SetColor(k, type, Palette.Ink);
        }

        t.SetColor("font_color", "Label", Palette.Ink);
        t.SetStylebox("panel", "PanelContainer", Box(new Color(1, 1, 1, 0.96f), 26, 34, 28));
        t.SetStylebox("panel", "TabContainer", Box(new Color("f1f5fb"), 16, 20, 16));
        t.SetStylebox("tab_selected", "TabContainer", Box(blue, 12, 18, 8));
        t.SetStylebox("tab_unselected", "TabContainer", Box(new Color("dfe6f0"), 12, 18, 8));
        t.SetStylebox("tab_hovered", "TabContainer", Box(new Color("c9d6ea"), 12, 18, 8));
        t.SetColor("font_selected_color", "TabContainer", Colors.White);
        t.SetColor("font_unselected_color", "TabContainer", Palette.Ink);
        t.SetColor("font_hovered_color", "TabContainer", Palette.Ink);
        return t;
    }

    public static Button Btn(string text, Action onClick, int minWidth = 360)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 60), FocusMode = Control.FocusModeEnum.All };
        if (onClick != null) b.Pressed += onClick;
        return b;
    }

    public static Label Lbl(string text, int size = 26, Color? color = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = new Label { Text = text, HorizontalAlignment = align };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
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

    public static HBoxContainer SliderRow(string label, float min, float max, float step, float value, Action<float> onChange, Func<float, string> fmt = null)
    {
        fmt ??= v => Mathf.RoundToInt(v * 100) + "%";
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(330, 0) });
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 36) };
        var vl = new Label { Text = fmt(value), CustomMinimumSize = new Vector2(90, 0), HorizontalAlignment = HorizontalAlignment.Right };
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
