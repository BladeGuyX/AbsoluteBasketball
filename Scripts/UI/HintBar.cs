using Godot;

namespace AB;

/// <summary>
/// Barra de dicas de controle no rodapé (Etapa 6): cada ação tem um "ícone" de tecla/botão desenhado como uma
/// teclinha, que troca entre teclado/mouse e controle conforme o último dispositivo usado (e segue os remapeamentos).
/// </summary>
public partial class HintBar : HBoxContainer
{
    static readonly (string Action, string Text)[] Items =
    {
        (Act.Shoot, "arremesso"), (Act.Pass, "passe"), (Act.Variation, "passe alto"), (Act.Jump, "salto / enterrada"),
        (Act.Dribble, "drible / roubo"), (Act.Sprint, "correr"), (Act.Pause, "pausa"),
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 5);
        InputDevice.Changed += Rebuild;
        Rebuild();
    }

    public override void _ExitTree() => InputDevice.Changed -= Rebuild;

    int _rev = -1;
    public override void _Process(double delta)
    {
        if (_rev != InputSetup.Revision) { _rev = InputSetup.Revision; Rebuild(); } // remapeou nas configurações
    }

    void Rebuild()
    {
        foreach (var c in GetChildren()) c.QueueFree();
        bool same = InputDevice.Glyph(Act.Dribble) == InputDevice.Glyph(Act.Steal);
        foreach (var (action0, text0) in Items)
        {
            var action = action0; var text = text0;
            if (action == Act.Dribble && !same) text = "drible";
            if (action == Act.Dribble && !same)
            {
                AddChild(Cap(InputDevice.Glyph(Act.Steal)));
                var ls = UiKit.Outlined("roubo / enterrada", 13, new Color(1, 1, 1, 0.9f), HorizontalAlignment.Left, 4);
                ls.MouseFilter = MouseFilterEnum.Ignore;
                AddChild(ls);
                AddChild(new Control { CustomMinimumSize = new Vector2(6, 0), MouseFilter = MouseFilterEnum.Ignore });
            }
            AddChild(Cap(InputDevice.Glyph(action)));
            var l = UiKit.Outlined(text, 13, new Color(1, 1, 1, 0.9f), HorizontalAlignment.Left, 4);
            l.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(l);
            AddChild(new Control { CustomMinimumSize = new Vector2(6, 0), MouseFilter = MouseFilterEnum.Ignore });
        }
    }

    /// <summary>Teclinha: fundo escuro arredondado (redondo para botões de face do controle), texto branco.</summary>
    static Control Cap(string glyph)
    {
        bool face = InputDevice.Gamepad && glyph is "A" or "B" or "X" or "Y";
        var col = !face ? new Color(0.1f, 0.12f, 0.18f, 0.85f) : glyph switch
        {
            "A" => new Color("2f9e44"), "B" => new Color("d9480f"), "X" => new Color("1c7ed6"), _ => new Color("f08c00"),
        };
        var p = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(face ? 24 : 22, 22) };
        var sb = UiKit.Box(col, face ? 12 : 5, face ? 4 : 5, 1);
        sb.BorderColor = new Color(1, 1, 1, 0.55f);
        sb.SetBorderWidthAll(2);
        p.AddThemeStyleboxOverride("panel", sb);
        var l = new Label { Text = glyph, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        l.AddThemeColorOverride("font_color", Colors.White);
        l.AddThemeFontSizeOverride("font_size", 12);
        p.AddChild(l);
        return p;
    }
}
