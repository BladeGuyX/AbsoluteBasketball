using Godot;

namespace AB;

public partial class Hud : CanvasLayer
{
    public Match M;

    Label _scoreA, _scoreB, _info, _countdown, _msg, _sub;
    ProgressBar _power;
    Control _powerBox, _endPanel;
    Label _endTitle, _endScore;
    Button _rematchBtn;
    float _countT, _msgT, _subT;
    Control _top, _bottom, _cross, _abilityBar, _staminaHud;
    JinxBorder _jinx;

    public override void _Ready()
    {
        Layer = 5;
        var root = UiKit.FullRect(new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiKit.Theme });
        AddChild(root);

        // ---- placar ----
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        UiKit.Anchor(top, 0.5f, 0, 0.5f, 0, -330, 18, 330, 102);
        _top = top;
        top.Alignment = BoxContainer.AlignmentMode.Center;
        top.AddThemeConstantOverride("separation", 0);
        root.AddChild(top);
        _scoreA = ScoreCell(top, 0);
        var mid = new PanelContainer { CustomMinimumSize = new Vector2(190, 84) };
        mid.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.11f, 0.14f, 0.2f, 0.88f), 0, 10, 6));
        _info = UiKit.Outlined("", 28, Colors.White, outline: 0);
        mid.AddChild(_info);
        top.AddChild(mid);
        _scoreB = ScoreCell(top, 1);

        // ---- contagem / mensagens ----
        _countdown = UiKit.FullRect(UiKit.Outlined("", 170, new Color("ffd23f"), outline: 22));
        _countdown.VerticalAlignment = VerticalAlignment.Center;
        root.AddChild(_countdown);
        _msg = UiKit.Outlined("", 64, Colors.White, outline: 18);
        UiKit.Anchor(_msg, 0.5f, 0, 0.5f, 0, -600, 190, 600, 290);
        root.AddChild(_msg);
        // v0.27: linha menor embaixo da mensagem (ex.: cesta roubada pelo "Não me Decepciona")
        _sub = UiKit.Outlined("", 34, Colors.White, outline: 12);
        UiKit.Anchor(_sub, 0.5f, 0, 0.5f, 0, -900, 290, 900, 345);
        root.AddChild(_sub);

        // ---- mira ----
        var cross = new ColorRect { Color = new Color(1, 1, 1, 0.8f), Size = new Vector2(6, 6), MouseFilter = Control.MouseFilterEnum.Ignore };
        UiKit.Anchor(cross, 0.5f, 0.5f, 0.5f, 0.5f, -3, -3, 3, 3);
        root.AddChild(cross);
        _cross = cross;
        // v0.26: borda vermelha quando a Presença Implacável estragou o seu arremesso
        _jinx = UiKit.FullRect(new JinxBorder { M = M });
        root.AddChild(_jinx);
        root.AddChild(UiKit.FullRect(new AimIndicator { M = M }));

        // ---- barras ----
        var bottom = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(420, 0) };
        UiKit.Anchor(bottom, 0.5f, 1, 0.5f, 1, -210, -180, 210, -146);
        _bottom = bottom;
        bottom.AddThemeConstantOverride("separation", 8);
        root.AddChild(bottom);
        _powerBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _power = Bar(new Color("ffb020"), 22);
        _powerBox.AddChild(_power);
        bottom.AddChild(_powerBox);
        // v0.28: estamina + cargas de drible desenhadas (StaminaHud), mais embaixo; a caixa de cima fica só com a força do arremesso
        _staminaHud = new StaminaHud { M = M, Name = "StaminaHud" };
        UiKit.Anchor(_staminaHud, 0.5f, 1, 0.5f, 1, -300, -138, 300, -100);
        root.AddChild(_staminaHud);

        // v0.26: os controles não ficam mais na tela (estão em Configurações → Informações). No rodapé: as habilidades.
        _abilityBar = new AbilityBar { M = M, Name = "AbilityBar" };
        UiKit.Anchor(_abilityBar, 0.5f, 1, 0.5f, 1, -260, -100, 260, -6);
        root.AddChild(_abilityBar);

        // ---- fim de jogo ----
        _endPanel = UiKit.FullRect(new CenterContainer { Visible = false });
        var panel = new PanelContainer();
        var vb = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vb.AddThemeConstantOverride("separation", 18);
        _endTitle = UiKit.Lbl("", 64, null, HorizontalAlignment.Center);
        _endScore = UiKit.Lbl("", 40, null, HorizontalAlignment.Center);
        vb.AddChild(_endTitle);
        vb.AddChild(_endScore);
        if (M != null && M.IsOnline)
        {
            // online: só o host decide (jogar de novo / voltar ao lobby); o cliente pode sair
            _rematchBtn = UiKit.Btn(M.IsHost ? "Jogar de novo" : "Aguardando o host...", M.IsHost ? () => NetSession.I.HostStartMatch() : null);
            _rematchBtn.Disabled = !M.IsHost;
            vb.AddChild(_rematchBtn);
            if (M.IsHost) vb.AddChild(UiKit.Btn("Voltar ao lobby", () => NetSession.I.HostBackToLobby()));
            vb.AddChild(UiKit.Btn(M.IsHost ? "Encerrar e ir ao menu" : "Sair", () => GameRoot.I.LeaveToMenu()));
        }
        else
        {
            _rematchBtn = UiKit.Btn("Jogar de novo", () => GameRoot.I.Rematch());
            vb.AddChild(_rematchBtn);
            vb.AddChild(UiKit.Btn("Menu principal", () => GameRoot.I.ShowMainMenu()));
        }
        panel.AddChild(vb);
        _endPanel.AddChild(panel);
        root.AddChild(_endPanel);
    }

    Label ScoreCell(HBoxContainer parent, int team)
    {
        var pc = new PanelContainer { CustomMinimumSize = new Vector2(230, 84) };
        var sb = UiKit.Box(Palette.Team[team], 0, 16, 6);
        if (team == 0) { sb.CornerRadiusTopLeft = 20; sb.CornerRadiusBottomLeft = 20; }
        else { sb.CornerRadiusTopRight = 20; sb.CornerRadiusBottomRight = 20; }
        pc.AddThemeStyleboxOverride("panel", sb);
        var hb = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        hb.AddThemeConstantOverride("separation", 14);
        var name = UiKit.Outlined(Palette.TeamName[team], 24, Colors.White, outline: 0);
        var score = UiKit.Outlined("0", 54, Colors.White, outline: 10);
        if (team == 0) { hb.AddChild(name); hb.AddChild(score); }
        else { hb.AddChild(score); hb.AddChild(name); }
        pc.AddChild(hb);
        parent.AddChild(pc);
        return score;
    }

    static ProgressBar Bar(Color fill, int height)
    {
        var b = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(420, height), MouseFilter = Control.MouseFilterEnum.Ignore };
        b.AddThemeStyleboxOverride("background", UiKit.Box(new Color(0.1f, 0.12f, 0.18f, 0.7f), height / 2, 0, 0));
        b.AddThemeStyleboxOverride("fill", UiKit.Box(fill, height / 2, 0, 0));
        return b;
    }

    public void ShowCountdown(string text, float dur)
    {
        _countdown.Text = text;
        _countT = dur;
        _countdown.Modulate = Colors.White;
        _countdown.Scale = Vector2.One;
    }

    public void ShowMessage(string text, float dur, Color? color = null, bool small = false)
    {
        _msg.Text = text;
        _msg.LabelSettings.FontColor = color ?? Colors.White;
        _msg.LabelSettings.FontSize = small ? 36 : 64;
        _msgT = dur;
        _msg.Modulate = Colors.White;
    }

    public void ShowSub(string text, float dur, Color color)
    {
        _sub.Text = text;
        _sub.LabelSettings.FontColor = color;
        _subT = dur;
        _sub.Modulate = Colors.White;
        _sub.Scale = Vector2.One;
    }

    public void ShowEnd(int winner)
    {
        _endPanel.Visible = true;
        _endTitle.Text = winner >= 0 ? $"VITÓRIA DO TIME {Palette.TeamName[winner]}!" : "FIM DE JOGO";
        if (winner >= 0) _endTitle.AddThemeColorOverride("font_color", Palette.Team[winner]);
        _endScore.Text = $"{M.Score[0]}  x  {M.Score[1]}";
        _rematchBtn.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (M == null) return;
        bool cut = M.Cut != null && M.Cut.Active; // v0.26: na cutscene só fica a cena (e o skill check)
        _top.Visible = !cut; _bottom.Visible = !cut; _cross.Visible = !cut; _staminaHud.Visible = !cut;
        if (!M.AbilitiesOn) { _bottom.OffsetTop = -110; _bottom.OffsetBottom = -76; _staminaHud.OffsetTop = -62; _staminaHud.OffsetBottom = -24; } // sem habilidades: tudo mais embaixo
        _scoreA.Text = M.Score[0].ToString();
        _scoreB.Text = M.Score[1].ToString();
        if (M.Cfg.Mode == WinMode.Time)
        {
            int s = Mathf.CeilToInt(Mathf.Max(M.Clock, 0));
            _info.Text = M.Overtime ? "PRORROG." : $"{s / 60}:{s % 60:00}";
        }
        else _info.Text = $"ATÉ {M.Cfg.TargetScore}";
        if (M.Cfg.TestMode) _info.Text = "TESTE"; // v0.27: partida infinita

        var p = M.LocalPlayer;
        if (p != null)
        {
            _powerBox.Visible = p.ChargingShot;
            _power.Value = p.GetShotPlan(out var plan) ? plan.Power : 0; // força = distância usada (sem texto)
        }

        if (_countT > 0)
        {
            _countT -= dt;
            float a = Mathf.Clamp(_countT / 0.25f, 0, 1);
            _countdown.Modulate = new Color(1, 1, 1, a);
            if (_countT <= 0) _countdown.Text = "";
        }
        if (_subT > 0)
        {
            _subT -= dt;
            _sub.Modulate = new Color(1, 1, 1, Mathf.Clamp(_subT / 0.3f, 0, 1));
            if (_subT <= 0) _sub.Text = "";
        }
        if (_msgT > 0)
        {
            _msgT -= dt;
            _msg.Modulate = new Color(1, 1, 1, Mathf.Clamp(_msgT / 0.3f, 0, 1));
            if (_msgT <= 0) _msg.Text = "";
        }
    }
}
