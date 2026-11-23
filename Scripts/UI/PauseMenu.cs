using Godot;

namespace AB;

public partial class PauseMenu : Control
{
    public Match M;
    public bool IsOpen => Visible;
    Button _first;
    SettingsMenu _settings;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.FullRect(new ColorRect { Color = new Color(0.05f, 0.08f, 0.14f, 0.55f) }));
        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        var panel = new PanelContainer();
        center.AddChild(panel);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 16);
        panel.AddChild(vb);
        vb.AddChild(UiKit.Lbl("Pausado", 52, null, HorizontalAlignment.Center));
        _first = UiKit.Btn("Continuar", Close);
        vb.AddChild(_first);
        vb.AddChild(UiKit.Btn("Configurações", OpenSettings));
        vb.AddChild(UiKit.Btn("Reiniciar partida", () => GameRoot.I.Rematch()));
        vb.AddChild(UiKit.Btn("Sair para o menu", () => GameRoot.I.ShowMainMenu()));
    }

    void OpenSettings()
    {
        _settings = new SettingsMenu { OnClosed = () => { _settings = null; _first.GrabFocus(); } };
        AddChild(_settings);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed(Act.Pause) || M == null || M.Phase == MatchPhase.Ended || M.Cfg.SmokeTest) return;
        GetViewport().SetInputAsHandled();
        if (Visible) Close(); else Open();
    }

    public void Open()
    {
        Visible = true;
        GetTree().Paused = true;
        _first.GrabFocus();
    }

    public void Close()
    {
        if (_settings != null) return;
        Visible = false;
        GetTree().Paused = false;
        CameraRig.IgnoreMouseUntilMs = Time.GetTicksMsec() + 150;
    }
}
