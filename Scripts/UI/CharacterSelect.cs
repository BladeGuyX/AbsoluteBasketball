using System;
using Godot;

namespace AB;

/// <summary>
/// v0.26: tela de escolha de personagem (antes da partida offline e no lobby online). Lista dos personagens à esquerda;
/// à direita a prévia 3D girando, os atributos em estrelas (1–5) e as habilidades com o botão de cada uma.
/// No modo sem habilidades os atributos continuam valendo (as habilidades aparecem apagadas).
/// </summary>
public partial class CharacterSelect : Control
{
    public Action<int> OnConfirm;
    public Action OnBack;
    public string ConfirmText = "Jogar";
    public bool AbilitiesOn = true;
    int _sel;
    Label _name, _tag;
    VBoxContainer _stats, _abs;
    readonly Button[] _cards = new Button[Characters.Count];
    Node3D _stage;
    IPlayerVisual _vis;
    int _visChar = -1;
    float _t;

    public override void _Ready()
    {
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        _sel = Characters.Clamp(UserSettings.I.Character);
        AddChild(UiKit.FullRect(new ColorRect { Color = new Color(0.05f, 0.07f, 0.12f, 0.92f) }));
        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        var panel = new PanelContainer();
        center.AddChild(panel);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        panel.AddChild(root);
        root.AddChild(UiKit.Lbl("Escolha seu personagem", 44));

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 26);
        root.AddChild(row);

        // ---- lista ----
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 10);
        row.AddChild(list);
        for (int i = 0; i < Characters.Count; i++)
        {
            int id = i;
            var d = Characters.Get(i);
            var b = UiKit.Btn(d.Name, () => Select(id), 240);
            b.CustomMinimumSize = new Vector2(240, 76);
            b.AddThemeFontSizeOverride("font_size", 30);
            b.FocusEntered += () => Select(id);
            _cards[i] = b;
            list.AddChild(b);
        }

        // ---- prévia 3D ----
        var vpc = new SubViewportContainer { Stretch = true, CustomMinimumSize = new Vector2(300, 440) };
        row.AddChild(vpc);
        var vp = new SubViewport { OwnWorld3D = true, TransparentBg = true, Size = new Vector2I(300, 440), Msaa3D = Viewport.Msaa.Msaa4X };
        vpc.AddChild(vp);
        _stage = new Node3D();
        vp.AddChild(_stage);
        var cam = new Camera3D { Fov = 32, Current = true };
        _stage.AddChild(cam);
        cam.LookAtFromPosition(new Vector3(0, 1.45f, -5.4f), new Vector3(0, 1.0f, 0), Vector3.Up);
        _stage.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40, 160, 0), LightEnergy = 1.2f });
        var env = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.ClearColor, AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.75f, 0.8f, 0.9f), AmbientLightEnergy = 0.8f };
        _stage.AddChild(new WorldEnvironment { Environment = env });

        // ---- detalhes ----
        var det = new VBoxContainer { CustomMinimumSize = new Vector2(620, 0) };
        det.AddThemeConstantOverride("separation", 8);
        row.AddChild(det);
        _name = UiKit.Lbl("", 54);
        det.AddChild(_name);
        _tag = UiKit.Lbl("", 20, new Color("5b6577"));
        det.AddChild(_tag);
        _stats = new VBoxContainer();
        _stats.AddThemeConstantOverride("separation", 4);
        det.AddChild(_stats);
        det.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        _abs = new VBoxContainer();
        _abs.AddThemeConstantOverride("separation", 8);
        det.AddChild(_abs);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 12);
        root.AddChild(buttons);
        buttons.AddChild(UiKit.Btn("Voltar", () => { OnBack?.Invoke(); QueueFree(); }, 220));
        var ok = UiKit.Btn(ConfirmText, Confirm, 300);
        buttons.AddChild(ok);
        Select(_sel);
        _cards[_sel].CallDeferred(Control.MethodName.GrabFocus);
    }

    void Confirm()
    {
        UserSettings.I.Character = _sel;
        UserSettings.Save();
        OnConfirm?.Invoke(_sel);
        QueueFree();
    }

    void Select(int id)
    {
        _sel = Characters.Clamp(id);
        var d = Characters.Get(_sel);
        for (int i = 0; i < _cards.Length; i++)
            _cards[i].Modulate = i == _sel ? new Color(1.25f, 1.25f, 1.25f) : new Color(0.75f, 0.78f, 0.85f);
        _name.Text = d.Name;
        _name.AddThemeColorOverride("font_color", d.Color.Darkened(0.15f));
        _tag.Text = d.Tagline + (AbilitiesOn ? "" : "   (modo sem habilidades: só os atributos valem)");
        foreach (var c in _stats.GetChildren()) c.QueueFree();
        var stars = Characters.Stars(_sel);
        for (int i = 0; i < 5; i++)
        {
            var r = new HBoxContainer();
            r.AddThemeConstantOverride("separation", 10);
            r.AddChild(UiKit.Lbl(Characters.StatNames[i], 22));
            r.GetChild<Label>(0).CustomMinimumSize = new Vector2(150, 0);
            r.AddChild(new StarsView { Stars = stars[i], CustomMinimumSize = new Vector2(170, 28) });
            _stats.AddChild(r);
        }
        foreach (var c in _abs.GetChildren()) c.QueueFree();
        string[] keys = { Act.Ab1, Act.Ab2, Act.Ab3, Act.Ult };
        for (int i = 0; i < 4; i++)
        {
            var ab = d.Abilities[i];
            var r = new HBoxContainer { Modulate = AbilitiesOn ? Colors.White : new Color(1, 1, 1, 0.45f) };
            r.AddThemeConstantOverride("separation", 12);
            r.AddChild(new AbilityIconView { Icon = ab.Icon, Col = ab.Color, CustomMinimumSize = new Vector2(52, 52) });
            var txt = new VBoxContainer();
            txt.AddThemeConstantOverride("separation", 0);
            string key = InputDevice.Glyph(keys[i]);
            txt.AddChild(UiKit.Lbl((i == 3 ? "ULTIMATE · " : "") + ab.Name + "   [" + key + "]", 22, i == 3 ? new Color("b8860b") : null));
            var sl = UiKit.Lbl(ab.Short, 18, new Color("5b6577"));
            sl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            sl.CustomMinimumSize = new Vector2(540, 0);
            txt.AddChild(sl);
            r.AddChild(txt);
            _abs.AddChild(r);
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_visChar != _sel && _stage != null && CharacterVisual.Available)
        {
            if (_vis != null) { _vis.Root.GetParent()?.QueueFree(); _vis = null; }
            var holder = new Node3D();
            _stage.AddChild(holder);
            _vis = new CharacterVisual(holder, 0, null, _sel);
            _visChar = _sel;
            foreach (var c in holder.GetChildren()) if (c.Name == "TeamRing") ((Node3D)c).Visible = false;
        }
        _vis?.Animate(new RigState { Yaw = Mathf.Sin(_t * 0.7f) * 0.5f, Mood = Mathf.PosMod(_t, 6f) > 4.5f ? 1 : 0, Emote = Expressions.SorrisoFrio }, (float)delta);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed(Act.Pause)) { GetViewport().SetInputAsHandled(); OnBack?.Invoke(); QueueFree(); }
    }
}
