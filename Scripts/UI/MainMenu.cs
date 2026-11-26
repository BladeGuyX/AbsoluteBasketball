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
        AddChild(UiKit.FullRect(new MenuBackdrop())); // v0.27: céu, nuvens, quadra e bola girando

        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        _box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _box.AddThemeConstantOverride("separation", 16);
        center.AddChild(_box);

        var ver = UiKit.Outlined(GameVersion.Label, 20, Colors.White, HorizontalAlignment.Right, 6);
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
        _box.AddChild(Logo("ABSOLUTE", Colors.White));
        _box.AddChild(Logo("BASKETBALL", new Color("ff8a3d")));
        _box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });
        var play = UiKit.Btn("Jogar (local)", ShowSetup, UiKit.Green, 440);
        _box.AddChild(Centered(play));
        _box.AddChild(Centered(UiKit.Btn("Online", () => GameRoot.I.ShowOnlineMenu(), 440)));
        _box.AddChild(Centered(UiKit.Btn("Informações", () => AddChild(new InfoMenu { OnClosed = ShowHome }), 440)));
        _box.AddChild(Centered(UiKit.Btn("Configurações", OpenSettings, 440)));
        // v0.26: ver os personagens (atributos e habilidades) e deixar um escolhido (vale também no online)
        Button chr = null;
        chr = UiKit.Btn(CharText(), () => AddChild(new CharacterSelect { ConfirmText = "Escolher", OnConfirm = _ => chr.Text = CharText(), OnBack = () => chr.GrabFocus() }), UiKit.Purple, 440);
        _box.AddChild(Centered(chr));
        _update = UiKit.Btn("", () => Updater.I?.Activate(), UiKit.Grey, 440);
        _box.AddChild(Centered(_update));
        _box.AddChild(Centered(UiKit.Btn("Sair", () => GameRoot.QuitClean(this), UiKit.Red, 440)));
        RefreshUpdate();
        play.CallDeferred(Control.MethodName.GrabFocus);
    }

    // ---------- atualização (GitHub Releases) ----------
    Button _update;

    public override void _EnterTree() { if (Updater.I != null) Updater.I.Changed += RefreshUpdate; }
    public override void _ExitTree() { if (Updater.I != null) Updater.I.Changed -= RefreshUpdate; }

    void RefreshUpdate()
    {
        if (_update == null || !IsInstanceValid(_update)) return;
        var u = Updater.I;
        _update.Disabled = u == null || u.Busy;
        _update.Modulate = u?.State == Updater.St.Available ? new Color(0.6f, 1.6f, 0.6f) : Colors.White;
        _update.Text = u == null ? "Atualizar" : u.State switch
        {
            Updater.St.Checking => "Procurando atualização...",
            Updater.St.UpToDate => $"Atualizado (v{GameVersion.Number})",
            Updater.St.Available => Updater.CanInstall ? $"Atualizar para v{u.Latest}" : $"v{u.Latest} disponível (só na build)",
            Updater.St.Downloading => $"Baixando v{u.Latest}... {u.Progress * 100:0}%",
            Updater.St.Installing => "Instalando... o jogo vai reabrir",
            Updater.St.Failed => "Atualizar (erro: " + u.Error + ")",
            _ => "Procurar atualização",
        };
    }

    static string CharText() => "Personagens (" + Characters.Get(UserSettings.I.Character).Name + ")";

    /// <summary>Letreiro do título: contorno escuro grosso + sombra deslocada.</summary>
    static Control Logo(string text, Color color)
    {
        var l = UiKit.Outlined(text, 112, color, outline: 26);
        l.LabelSettings.Font = UiKit.FontBold;
        l.LabelSettings.ShadowSize = 2;
        l.LabelSettings.ShadowColor = new Color(0.05f, 0.1f, 0.25f, 0.55f);
        l.LabelSettings.ShadowOffset = new Vector2(8, 10);
        return l;
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
        vb.AddChild(UiKit.Title("Nova partida"));

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

        // v0.27: modo de teste = partida infinita e trapaças no menu de pausa
        var test = new OptionButton { CustomMinimumSize = new Vector2(360, 56) };
        test.AddItem("Partida normal", 0);
        test.AddItem("Teste (infinita + trapaças)", 1);
        test.Selected = Last.TestMode ? 1 : 0;
        grid.AddChild(new Label { Text = "Modo" });
        grid.AddChild(test);

        var abil = new OptionButton { CustomMinimumSize = new Vector2(360, 56) };
        abil.AddItem("Com habilidades", 0);
        abil.AddItem("Sem habilidades", 1);
        abil.Selected = Last.Abilities ? 0 : 1;
        grid.AddChild(new Label { Text = "Habilidades" });
        grid.AddChild(abil);

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
        Button start = null;
        start = UiKit.Btn("Escolher personagem e jogar", () =>
        {
            Last.Size = (CourtSize)size.GetSelectedId();
            Last.Mode = mode.Selected == 1 ? WinMode.Time : WinMode.Points;
            Last.TargetScore = (int)pts.Value;
            Last.TimeMinutes = (float)mins.Value;
            Last.AllyDummies = (int)allies.Value;
            Last.EnemyDummies = (int)enemies.Value;
            Last.TrainingDummies = kind.Selected == 1;
            Last.BotDifficulty = diff.Selected;
            Last.Abilities = abil.Selected == 0;
            Last.TestMode = test.Selected == 1;
            // v0.26: escolha do personagem antes de começar
            AddChild(new CharacterSelect { AbilitiesOn = Last.Abilities, OnConfirm = _ => GameRoot.I.StartMatch(Last.Clone()), OnBack = () => start.GrabFocus() });
        });
        UiKit.Tint(start, UiKit.Green);
        var btns = new HBoxContainer();
        btns.AddThemeConstantOverride("separation", 14);
        btns.AddChild(UiKit.Btn("Voltar", ShowHome, UiKit.Grey, 220));
        start.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        btns.AddChild(start);
        vb.AddChild(btns);
        start.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void DebugSetup() => ShowSetup();

    void OpenSettings()
    {
        AddChild(new SettingsMenu { OnClosed = ShowHome });
    }
}
