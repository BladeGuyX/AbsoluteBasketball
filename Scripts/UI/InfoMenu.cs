using System;
using Godot;

namespace AB;

/// <summary>
/// v0.27: tela "Informações" (saiu das Configurações): controles e as habilidades de cada personagem, com os valores atuais
/// do Tuning. Abre no menu principal e no menu de pausa. A aba do personagem atual abre primeiro.
/// </summary>
public partial class InfoMenu : Control
{
    public Action OnClosed;
    /// <summary>Personagem mostrado primeiro (−1 = o escolhido no menu).</summary>
    public int CharacterId = -1;
    public bool AbilitiesOn = true;
    Button _back;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(UiKit.Dim(0.62f));
        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1240, 860) };
        center.AddChild(panel);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vb);
        vb.AddChild(UiKit.Title("Informações"));

        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        vb.AddChild(tabs);

        tabs.AddChild(Scroll("Controles", BuildControls));
        int chr = Characters.Clamp(CharacterId >= 0 ? CharacterId : UserSettings.I.Character);
        for (int i = 0; i < Characters.Count; i++)
        {
            int id = i;
            tabs.AddChild(Scroll(Characters.Get(id).Name, box => BuildCharacter(box, id)));
        }
        tabs.CurrentTab = 1 + chr;

        _back = UiKit.Btn("Voltar", Close, UiKit.Grey);
        vb.AddChild(_back);
        _back.CallDeferred(Control.MethodName.GrabFocus);
    }

    static ScrollContainer Scroll(string name, Action<VBoxContainer> build)
    {
        var sc = new ScrollContainer { Name = name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 12);
        sc.AddChild(box);
        build(box);
        return sc;
    }

    static void BuildControls(VBoxContainer info)
    {
        var grey = UiKit.Muted;
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 22);
        grid.AddThemeConstantOverride("v_separation", 10);
        info.AddChild(grid);
        foreach (var (action, desc) in Act.Info)
        {
            string label = Array.Find(Act.Rebindable, x => x.Action == action).Label ?? (action == Act.Pause ? "Pausa" : action);
            if (action == Act.MoveF) label = "Mover";
            string kb = action == Act.MoveF ? "W A S D" : action == Act.Pause ? "Esc" : InputSetup.Describe(InputSetup.GetKb(action));
            string pad = action == Act.MoveF ? "Analógico E" : action == Act.Pause ? "Menu" : InputSetup.Describe(InputSetup.GetPad(action));
            grid.AddChild(UiKit.Section(label, 22));
            grid.AddChild(Key(kb + "  /  " + pad));
            var d = UiKit.Lbl(desc, 19, grey);
            d.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            d.CustomMinimumSize = new Vector2(600, 0);
            grid.AddChild(d);
        }
        var tip = UiKit.Lbl("Os atalhos podem ser trocados em Configurações → Controles.", 19, grey);
        info.AddChild(tip);
    }

    /// <summary>"Teclinha": texto num fundo claro arredondado.</summary>
    static Control Key(string text)
    {
        var pc = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkBegin, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var sb = UiKit.Box(new Color("e8eef7"), 10, 12, 4);
        sb.BorderColor = UiKit.FieldBorder; sb.BorderWidthBottom = 3;
        pc.AddThemeStyleboxOverride("panel", sb);
        var l = UiKit.Lbl(text, 19, Palette.Team[0].Darkened(0.2f));
        l.AddThemeFontOverride("font", UiKit.FontBold);
        pc.AddChild(l);
        return pc;
    }

    void BuildCharacter(VBoxContainer info, int chr)
    {
        var grey = UiKit.Muted;
        var def = Characters.Get(chr);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 16);
        head.AddChild(UiKit.Section(def.Name, 40, def.Color.Darkened(0.15f)));
        var tag = UiKit.Lbl(def.Tagline + (AbilitiesOn ? "" : "   (habilidades desligadas nesta partida)"), 20, grey);
        tag.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(tag);
        info.AddChild(head);
        var stars = Characters.Stars(chr);
        var srow = new HBoxContainer();
        srow.AddThemeConstantOverride("separation", 16);
        for (int i = 0; i < 5; i++)
        {
            srow.AddChild(UiKit.Lbl(Characters.StatNames[i], 18, grey));
            srow.AddChild(new StarsView { Stars = stars[i], CustomMinimumSize = new Vector2(110, 20) });
        }
        info.AddChild(srow);
        string[] keys = { Act.Ab1, Act.Ab2, Act.Ab3, Act.Ult };
        for (int i = 0; i < 4; i++)
        {
            var ab = def.Abilities[i];
            var card = new PanelContainer();
            var csb = UiKit.Box(i == 3 ? new Color("fff7dc") : Colors.White, 16, 18, 12);
            csb.BorderColor = i == 3 ? new Color("f0cf6a") : new Color("e1e8f2");
            csb.SetBorderWidthAll(2);
            csb.BorderWidthBottom = 4;
            card.AddThemeStyleboxOverride("panel", csb);
            var r = new HBoxContainer();
            r.AddThemeConstantOverride("separation", 16);
            r.AddChild(new AbilityIconView { Icon = ab.Icon, Col = ab.Color, CustomMinimumSize = new Vector2(64, 64), SizeFlagsVertical = SizeFlags.ShrinkBegin });
            var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            col.AddThemeConstantOverride("separation", 4);
            var title = new HBoxContainer();
            title.AddThemeConstantOverride("separation", 12);
            title.AddChild(UiKit.Section((i == 3 ? "ULTIMATE · " : $"({i + 1}) ") + ab.Name, 24, i == 3 ? new Color("a77800") : null));
            string kb = InputSetup.Describe(InputSetup.GetKb(keys[i])), pad = InputSetup.Describe(InputSetup.GetPad(keys[i]));
            title.AddChild(Key(kb + " / " + pad));
            col.AddChild(title);
            var d = UiKit.Lbl(Characters.Describe(chr, i), 19, grey);
            d.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            d.CustomMinimumSize = new Vector2(980, 0);
            col.AddChild(d);
            r.AddChild(col);
            card.AddChild(r);
            info.AddChild(card);
        }
        var foot = UiKit.Lbl("A barra dourada da ultimate enche bem devagar com o tempo; tocos, passes, roubos e principalmente cestas dão bônus na hora (enterradas dão mais). As recargas não voltam no começo de um round.", 18, grey);
        foot.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.AddChild(foot);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed(Act.Pause) || e.IsActionPressed("ui_cancel")) { GetViewport().SetInputAsHandled(); Close(); }
    }

    void Close()
    {
        OnClosed?.Invoke();
        QueueFree();
    }
}
