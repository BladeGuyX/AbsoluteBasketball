using Godot;

namespace AB;

public partial class MainMenu : Control
{
    VBoxContainer _box;
    static readonly MatchConfig Last = new();

    public override void _Ready()
    {
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        var grad = new Gradient();
        grad.SetColor(0, new Color("3b8fe6"));
        grad.SetColor(1, new Color("bfe6ff"));
        AddChild(UiKit.FullRect(new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = grad, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 16, Height = 256 },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        }));
        // "chão" de areia
        AddChild(UiKit.Anchor(new ColorRect { Color = Palette.Sand }, 0, 0.82f, 1, 1, 0, 0, 0, 0));

        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        _box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _box.AddThemeConstantOverride("separation", 16);
        center.AddChild(_box);

        var ver = UiKit.Outlined("v0.11 · Etapa 7", 20, Colors.White, HorizontalAlignment.Right, 6);
        AddChild(UiKit.Anchor(ver, 1, 1, 1, 1, -520, -44, -20, -12));
        ShowHome();
    }

    void Clear()
    {
        foreach (var c in _box.GetChildren()) c.QueueFree();
    }

    void ShowHome()
    {
        Clear();
        _box.AddChild(UiKit.Outlined("ABSOLUTE", 110, Colors.White, outline: 26));
        _box.AddChild(UiKit.Outlined("BASKETBALL", 110, new Color("ff8a3d"), outline: 26));
        _box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30) });
        var play = UiKit.Btn("Jogar (local)", ShowSetup, 420);
        _box.AddChild(Centered(play));
        _box.AddChild(Centered(UiKit.Btn("Online", () => GameRoot.I.ShowOnlineMenu(), 420)));
        _box.AddChild(Centered(UiKit.Btn("Configurações", OpenSettings, 420)));
        _box.AddChild(Centered(UiKit.Btn("Sair", () => GetTree().Quit(), 420)));
        play.CallDeferred(Control.MethodName.GrabFocus);
    }

    static Control Centered(Control c)
    {
        var cc = new CenterContainer();
        cc.AddChild(c);
        return cc;
    }

    void ShowSetup()
    {
        Clear();
        var panel = new PanelContainer();
        _box.AddChild(panel);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 18);
        panel.AddChild(vb);
        vb.AddChild(UiKit.Lbl("Nova partida", 48));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 30);
        grid.AddThemeConstantOverride("v_separation", 14);
        vb.AddChild(grid);

        var size = new OptionButton { CustomMinimumSize = new Vector2(360, 56) };
        foreach (CourtSize s in new[] { CourtSize.Small, CourtSize.Medium, CourtSize.Large })
            size.AddItem($"{CourtGeo.Name(s)} (até {CourtGeo.MaxPlayers(s)} jogadores)", (int)s);
        size.Selected = (int)Last.Size;
        grid.AddChild(new Label { Text = "Tamanho da quadra" });
        grid.AddChild(size);

        var mode = new OptionButton { CustomMinimumSize = new Vector2(360, 56) };
        mode.AddItem("Por pontos", 0);
        mode.AddItem("Por tempo (sem limite de pontos)", 1);
        mode.Selected = (int)Last.Mode;
        grid.AddChild(new Label { Text = "Condição de vitória" });
        grid.AddChild(mode);

        var pts = new SpinBox { MinValue = 1, MaxValue = 99, Value = Last.TargetScore, CustomMinimumSize = new Vector2(360, 56) };
        var ptsLbl = new Label { Text = "Pontos para vencer" };
        grid.AddChild(ptsLbl);
        grid.AddChild(pts);

        var mins = new SpinBox { MinValue = 1, MaxValue = 60, Step = 0.5, Value = Last.TimeMinutes, Suffix = "min", CustomMinimumSize = new Vector2(360, 56) };
        var minsLbl = new Label { Text = "Duração" };
        grid.AddChild(minsLbl);
        grid.AddChild(mins);

        var allies = new SpinBox { MinValue = 0, MaxValue = 9, Value = Last.AllyDummies, CustomMinimumSize = new Vector2(360, 56) };
        var kind = new OptionButton { CustomMinimumSize = new Vector2(360, 56) };
        kind.AddItem("Bots", 0);
        kind.AddItem("Bonecos de treino", 1);
        kind.Selected = Last.TrainingDummies ? 1 : 0;
        grid.AddChild(new Label { Text = "Outros jogadores" });
        grid.AddChild(kind);
        var diff = new OptionButton { CustomMinimumSize = new Vector2(360, 56) };
        diff.AddItem("Fácil", 0);
        diff.AddItem("Normal", 1);
        diff.AddItem("Difícil", 2);
        diff.Selected = Mathf.Clamp(Last.BotDifficulty, 0, 2);
        var diffLbl = new Label { Text = "Dificuldade dos bots" };
        grid.AddChild(diffLbl);
        grid.AddChild(diff);
        kind.ItemSelected += _ => diff.Visible = diffLbl.Visible = kind.Selected == 0;
        diff.Visible = diffLbl.Visible = kind.Selected == 0;
        grid.AddChild(new Label { Text = "Aliados (no seu time)" });
        grid.AddChild(allies);
        var enemies = new SpinBox { MinValue = 0, MaxValue = 9, Value = Last.EnemyDummies, CustomMinimumSize = new Vector2(360, 56) };
        grid.AddChild(new Label { Text = "Adversários" });
        grid.AddChild(enemies);
        void ClampDummies()
        {
            int max = CourtGeo.MaxPlayers((CourtSize)size.GetSelectedId()) - 1;
            allies.MaxValue = max;
            enemies.MaxValue = Mathf.Max(0, max - (int)allies.Value);
            if (enemies.Value > enemies.MaxValue) enemies.Value = enemies.MaxValue;
        }
        allies.ValueChanged += _ => ClampDummies();
        size.ItemSelected += _ => ClampDummies();
        ClampDummies();

        void Refresh()
        {
            bool time = mode.Selected == 1;
            pts.Visible = ptsLbl.Visible = !time;
            mins.Visible = minsLbl.Visible = time;
        }
        mode.ItemSelected += _ => Refresh();
        Refresh();

        vb.AddChild(UiKit.Lbl("Bots jogam de verdade (atacam, passam, enterram, marcam, roubam e bloqueiam).\nBonecos de treino: o aliado devolve passes; o adversário pula contra arremessos e marca a cesta.", 20, new Color("5b6577")));
        var start = UiKit.Btn("Iniciar partida", () =>
        {
            Last.Size = (CourtSize)size.GetSelectedId();
            Last.Mode = mode.Selected == 1 ? WinMode.Time : WinMode.Points;
            Last.TargetScore = (int)pts.Value;
            Last.TimeMinutes = (float)mins.Value;
            Last.AllyDummies = (int)allies.Value;
            Last.EnemyDummies = (int)enemies.Value;
            Last.TrainingDummies = kind.Selected == 1;
            Last.BotDifficulty = diff.Selected;
            GameRoot.I.StartMatch(Last.Clone());
        });
        vb.AddChild(start);
        vb.AddChild(UiKit.Btn("Voltar", ShowHome));
        start.CallDeferred(Control.MethodName.GrabFocus);
    }

    void OpenSettings()
    {
        AddChild(new SettingsMenu { OnClosed = ShowHome });
    }
}
