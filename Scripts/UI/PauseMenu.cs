using Godot;

namespace AB;

/// <summary>Menu de pausa. v0.27: Informações (controles e habilidades) e, no modo de teste, Trapaças.</summary>
public partial class PauseMenu : Control
{
    public Match M;
    public bool IsOpen => Visible;
    Button _first;
    Control _sub; // configurações / informações / trapaças abertas por cima

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        Visible = false;
        AddChild(UiKit.Dim(0.55f));
        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        var panel = new PanelContainer();
        center.AddChild(panel);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vb);
        vb.AddChild(UiKit.Title(M != null && M.Cfg.TestMode ? "Pausado · Teste" : "Pausado"));
        vb.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        _first = UiKit.Btn("Continuar", Close, UiKit.Green, 420);
        vb.AddChild(_first);
        vb.AddChild(UiKit.Btn("Informações", OpenInfo, 420));
        if (M != null && M.Cfg.TestMode) vb.AddChild(UiKit.Btn("Trapaças", OpenCheats, UiKit.Purple, 420));
        vb.AddChild(UiKit.Btn("Configurações", OpenSettings, 420));
        if (M == null || !M.IsOnline) vb.AddChild(UiKit.Btn("Reiniciar partida", () => GameRoot.I.Rematch(), UiKit.Grey, 420));
        vb.AddChild(UiKit.Btn(M != null && M.IsHost ? "Encerrar partida online" : M != null && M.IsClient ? "Sair da partida online" : "Sair para o menu",
            () => GameRoot.I.LeaveToMenu(), UiKit.Red, 420));
        if (M != null && M.IsOnline) vb.AddChild(UiKit.Lbl("Online o jogo não pausa.", 20, UiKit.Muted, HorizontalAlignment.Center));
    }

    void OpenSub(Control c)
    {
        _sub = c;
        AddChild(c);
    }

    void SubClosed() { _sub = null; _first.GrabFocus(); }

    void OpenSettings() => OpenSub(new SettingsMenu { OnClosed = SubClosed });

    void OpenInfo() => OpenSub(new InfoMenu { OnClosed = SubClosed, CharacterId = M?.LocalPlayer?.CharacterId ?? -1, AbilitiesOn = M == null || M.AbilitiesOn });

    public void DebugCheats() => OpenCheats();

    void OpenCheats() => OpenSub(new CheatsMenu { M = M, OnClosed = SubClosed });

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed(Act.Pause) || M == null || M.Phase == MatchPhase.Ended || M.Cfg.SmokeTest) return;
        GetViewport().SetInputAsHandled();
        if (Visible) Close(); else Open();
    }

    public void Open()
    {
        Visible = true;
        if (M == null || !M.IsOnline) GetTree().Paused = true;
        _first.GrabFocus();
    }

    public void Close()
    {
        if (_sub != null && IsInstanceValid(_sub)) return;
        _sub = null;
        Visible = false;
        GetTree().Paused = false;
        CameraRig.IgnoreMouseUntilMs = Time.GetTicksMsec() + 150;
    }
}
