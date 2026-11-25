using System;
using System.Collections.Generic;
using Godot;

namespace AB;

/// <summary>Configurações: áudio, vídeo e controles (sensibilidade, corrida alternar, remapeamento).</summary>
public partial class SettingsMenu : Control
{
    public Action OnClosed;
    /// <summary>v0.26: personagem mostrado na aba Informações (−1 = o escolhido no menu).</summary>
    public int CharacterId = -1;
    public bool AbilitiesOn = true;

    string _waitAction;
    bool _waitPad;
    ulong _waitStart;
    readonly Dictionary<(string, bool), Button> _bindBtns = new();
    Button _backBtn;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(UiKit.FullRect(new ColorRect { Color = new Color(0.05f, 0.08f, 0.14f, 0.6f) }));
        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1100, 800) };
        center.AddChild(panel);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vb);
        vb.AddChild(UiKit.Lbl("Configurações", 48));

        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        vb.AddChild(tabs);
        var S = UserSettings.I;

        // ---------- Geral ----------
        var geral = new VBoxContainer { Name = "Geral" };
        geral.AddThemeConstantOverride("separation", 16);
        tabs.AddChild(geral);
        geral.AddChild(UiKit.SliderRow("Volume geral", 0, 1, 0.01f, S.MasterVolume, v => { S.MasterVolume = v; S.ApplyAudio(); }));
        geral.AddChild(UiKit.SliderRow("Volume dos efeitos", 0, 1, 0.01f, S.SfxVolume, v => { S.SfxVolume = v; S.ApplyAudio(); }));
        geral.AddChild(UiKit.SliderRow("Volume da música", 0, 1, 0.01f, S.MusicVolume, v => { S.MusicVolume = v; S.ApplyAudio(); }));
        geral.AddChild(UiKit.CheckRow("Tela cheia", S.Fullscreen, b => { S.Fullscreen = b; S.ApplyVideo(); }));

        // ---------- Controles ----------
        var ctlScroll = new ScrollContainer { Name = "Controles", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(ctlScroll);
        var ctl = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        ctl.AddThemeConstantOverride("separation", 12);
        ctlScroll.AddChild(ctl);
        Func<float, string> mult = v => v.ToString("0.00") + "x";
        ctl.AddChild(UiKit.SliderRow("Sensibilidade do mouse", 0.1f, 3f, 0.05f, S.MouseSensitivity, v => S.MouseSensitivity = v, mult));
        ctl.AddChild(UiKit.SliderRow("Sensibilidade do analógico", 0.1f, 3f, 0.05f, S.StickSensitivity, v => S.StickSensitivity = v, mult));
        ctl.AddChild(UiKit.CheckRow("Inverter eixo Y da câmera", S.InvertY, b => S.InvertY = b));
        ctl.AddChild(UiKit.CheckRow("Inverter eixo X da câmera", S.InvertX, b => S.InvertX = b));
        ctl.AddChild(UiKit.SliderRow("Campo de visão", -15f, 20f, 1f, S.FovOffset, v => S.FovOffset = v, v => (v >= 0 ? "+" : "") + v.ToString("0") + "°"));
        ctl.AddChild(UiKit.SliderRow("Distância da câmera", 0.7f, 1.5f, 0.05f, S.CameraDistanceMult, v => S.CameraDistanceMult = v, mult));
        ctl.AddChild(UiKit.CheckRow("Vibração do controle", S.Vibration, b => S.Vibration = b));
        ctl.AddChild(UiKit.SliderRow("Força da vibração", 0f, 1.5f, 0.05f, S.VibrationStrength, v => S.VibrationStrength = v, mult));
        ctl.AddChild(UiKit.CheckRow("Corrida: alternar (em vez de segurar)", S.SprintToggle, b => S.SprintToggle = b));
        // diagnóstico (v0.10): quais controles o jogo está enxergando
        var pads = UiKit.Lbl(InputDevice.PadReport(), 18, new Color("5b6577"));
        pads.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ctl.AddChild(pads);
        var padTimer = new Timer { WaitTime = 1, Autostart = true };
        padTimer.Timeout += () => pads.Text = InputDevice.PadReport();
        ctl.AddChild(padTimer);
        ctl.AddChild(UiKit.Lbl("Clique em um atalho e pressione a nova tecla/botão (Esc cancela no teclado).", 20, new Color("5b6577")));

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 8);
        ctl.AddChild(grid);
        grid.AddChild(UiKit.Lbl("Ação", 22, new Color("5b6577")));
        grid.AddChild(UiKit.Lbl("Teclado / Mouse", 22, new Color("5b6577")));
        grid.AddChild(UiKit.Lbl("Controle", 22, new Color("5b6577")));
        foreach (var (action, label, padRebindable) in Act.Rebindable)
        {
            grid.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(320, 0) });
            var kb = UiKit.Btn("", null, 300);
            kb.Pressed += () => BeginWait(action, false);
            grid.AddChild(kb);
            _bindBtns[(action, false)] = kb;
            var pad = UiKit.Btn("", null, 300);
            if (padRebindable) pad.Pressed += () => BeginWait(action, true);
            else pad.Disabled = true;
            grid.AddChild(pad);
            _bindBtns[(action, true)] = pad;
        }
        ctl.AddChild(UiKit.Btn("Restaurar controles padrão", () => { InputSetup.ResetToDefaults(); InputSetup.Apply(); UserSettings.Save(); RefreshBinds(); }));
        ctl.AddChild(UiKit.Lbl("Fixos: mira = mouse / analógico direito · pausa = Esc / Menu · F5 = recarregar tuning.json", 20, new Color("5b6577")));
        RefreshBinds();

        // ---------- Informações (v0.26): os controles saíram da tela de jogo e estão aqui, com as habilidades ----------
        var infoScroll = new ScrollContainer { Name = "Informações", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(infoScroll);
        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 10);
        infoScroll.AddChild(info);
        BuildInfo(info);

        _backBtn = UiKit.Btn("Voltar", Close);
        vb.AddChild(_backBtn);
        _backBtn.CallDeferred(Control.MethodName.GrabFocus);
    }

    void BuildInfo(VBoxContainer info)
    {
        var grey = new Color("5b6577");
        info.AddChild(UiKit.Lbl("Controles", 34));
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 8);
        info.AddChild(grid);
        foreach (var (action, desc) in Act.Info)
        {
            string label = Array.Find(Act.Rebindable, x => x.Action == action).Label ?? (action == Act.Pause ? "Pausa" : action);
            if (action == Act.MoveF) label = "Mover";
            string kb = action == Act.MoveF ? "W A S D" : action == Act.Pause ? "Esc" : InputSetup.Describe(InputSetup.GetKb(action));
            string pad = action == Act.MoveF ? "Analógico E" : action == Act.Pause ? "Menu" : InputSetup.Describe(InputSetup.GetPad(action));
            grid.AddChild(UiKit.Lbl(label, 22));
            grid.AddChild(UiKit.Lbl(kb + "  /  " + pad, 20, new Color("2f80ed")));
            var d = UiKit.Lbl(desc, 18, grey);
            d.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            d.CustomMinimumSize = new Vector2(560, 0);
            grid.AddChild(d);
        }

        int chr = Characters.Clamp(CharacterId >= 0 ? CharacterId : UserSettings.I.Character);
        var def = Characters.Get(chr);
        info.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        info.AddChild(UiKit.Lbl("Habilidades de " + def.Name + (AbilitiesOn ? "" : "  (desligadas nesta partida)"), 34));
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
            var r = new HBoxContainer();
            r.AddThemeConstantOverride("separation", 14);
            r.AddChild(new AbilityIconView { Icon = ab.Icon, Col = ab.Color, CustomMinimumSize = new Vector2(56, 56) });
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 2);
            string kb = InputSetup.Describe(InputSetup.GetKb(keys[i])), pad = InputSetup.Describe(InputSetup.GetPad(keys[i]));
            col.AddChild(UiKit.Lbl((i == 3 ? "ULTIMATE · " : $"({i + 1}) ") + ab.Name + $"   [{kb} / {pad}]", 22, i == 3 ? new Color("b8860b") : null));
            var d = UiKit.Lbl(Characters.Describe(chr, i), 18, grey);
            d.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            d.CustomMinimumSize = new Vector2(900, 0);
            col.AddChild(d);
            r.AddChild(col);
            info.AddChild(r);
        }
        info.AddChild(UiKit.Lbl("A barra dourada da ultimate enche bem devagar com o tempo; tocos, passes, roubos e principalmente cestas dão bônus na hora. As recargas não voltam no começo de um round.", 18, grey));
        info.GetChild<Label>(info.GetChildCount() - 1).AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    void RefreshBinds()
    {
        foreach (var ((action, pad), btn) in _bindBtns)
            btn.Text = InputSetup.Describe(pad ? InputSetup.GetPad(action) : InputSetup.GetKb(action));
    }

    void BeginWait(string action, bool pad)
    {
        _waitAction = action;
        _waitPad = pad;
        _waitStart = Time.GetTicksMsec();
        _bindBtns[(action, pad)].Text = pad ? "Aperte um botão do controle..." : "Aperte uma tecla...";
    }

    public override void _Input(InputEvent e)
    {
        if (_waitAction == null) return;
        if (Time.GetTicksMsec() - _waitStart < 200) { if (e is not InputEventMouseMotion) GetViewport().SetInputAsHandled(); return; }
        if (e is InputEventMouseMotion) return;
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape } or InputEventKey { Pressed: true, PhysicalKeycode: Key.Escape })
        {
            _waitAction = null;
            RefreshBinds();
            GetViewport().SetInputAsHandled();
            return;
        }
        var b = InputSetup.FromEvent(e, _waitPad);
        GetViewport().SetInputAsHandled();
        if (b == null) return;
        InputSetup.SetBind(_waitAction, b, _waitPad);
        InputSetup.Apply();
        UserSettings.Save();
        _waitAction = null;
        RefreshBinds();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed(Act.Pause)) { GetViewport().SetInputAsHandled(); Close(); }
    }

    void Close()
    {
        UserSettings.Save();
        OnClosed?.Invoke();
        QueueFree();
    }
}
