using Godot;

namespace AB;

public partial class Hud : CanvasLayer
{
    public Match M;

    Label _scoreA, _scoreB, _info, _countdown, _msg;
    ProgressBar _stamina, _power;
    Label _powerLabel, _status;
    HBoxContainer _dribblePips;
    Control _powerBox, _endPanel;
    Label _endTitle, _endScore;
    Button _rematchBtn;
    float _countT, _msgT;

    public override void _Ready()
    {
        Layer = 5;
        var root = UiKit.FullRect(new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiKit.Theme });
        AddChild(root);

        // ---- placar ----
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        UiKit.Anchor(top, 0.5f, 0, 0.5f, 0, -330, 18, 330, 102);
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

        // ---- mira ----
        var cross = new ColorRect { Color = new Color(1, 1, 1, 0.8f), Size = new Vector2(6, 6), MouseFilter = Control.MouseFilterEnum.Ignore };
        UiKit.Anchor(cross, 0.5f, 0.5f, 0.5f, 0.5f, -3, -3, 3, 3);
        root.AddChild(cross);

        // ---- barras ----
        var bottom = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(420, 0) };
        UiKit.Anchor(bottom, 0.5f, 1, 0.5f, 1, -210, -130, 210, -30);
        bottom.AddThemeConstantOverride("separation", 8);
        root.AddChild(bottom);
        _powerBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _powerLabel = UiKit.Outlined("ALCANCE", 22, Colors.White, outline: 8);
        _powerBox.AddChild(_powerLabel);
        _power = Bar(new Color("ffb020"), 22);
        _powerBox.AddChild(_power);
        bottom.AddChild(_powerBox);
        _stamina = Bar(new Color("5ad17a"), 14);
        bottom.AddChild(_stamina);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(UiKit.Outlined("DRIBLE", 18, Colors.White, outline: 6));
        _dribblePips = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _dribblePips.AddThemeConstantOverride("separation", 6);
        row.AddChild(_dribblePips);
        _status = UiKit.Outlined("", 18, new Color("ffd23f"), outline: 6);
        row.AddChild(_status);
        bottom.AddChild(row);

        var hint = UiKit.Outlined("Esq. arremesso (câmera define distância) · Dir. passe (R = alto) · Espaço salto · Q drible · E roubo · Shift correr · Esc pausa · F5 tuning",
            18, new Color(1, 1, 1, 0.85f), HorizontalAlignment.Left, 6);
        UiKit.Anchor(hint, 0, 1, 0, 1, 20, -44, 1500, -12);
        root.AddChild(hint);

        // ---- fim de jogo ----
        _endPanel = UiKit.FullRect(new CenterContainer { Visible = false });
        var panel = new PanelContainer();
        var vb = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vb.AddThemeConstantOverride("separation", 18);
        _endTitle = UiKit.Lbl("", 64, null, HorizontalAlignment.Center);
        _endScore = UiKit.Lbl("", 40, null, HorizontalAlignment.Center);
        vb.AddChild(_endTitle);
        vb.AddChild(_endScore);
        _rematchBtn = UiKit.Btn("Jogar de novo", () => GameRoot.I.Rematch());
        vb.AddChild(_rematchBtn);
        vb.AddChild(UiKit.Btn("Menu principal", () => GameRoot.I.ShowMainMenu()));
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

    void UpdateDribblePips(Player p)
    {
        int max = Mathf.RoundToInt(Tuning.I.DribbleCharges);
        while (_dribblePips.GetChildCount() < max)
        {
            var pip = new Panel { CustomMinimumSize = new Vector2(18, 18), MouseFilter = Control.MouseFilterEnum.Ignore };
            _dribblePips.AddChild(pip);
        }
        for (int i = 0; i < _dribblePips.GetChildCount(); i++)
        {
            var pip = _dribblePips.GetChild<Panel>(i);
            pip.Visible = i < max;
            bool full = i < p.DribbleCharges;
            var sb = UiKit.Box(full ? new Color("ffb020") : new Color(0.1f, 0.12f, 0.18f, 0.7f), 9, 0, 0);
            if (!full && i == p.DribbleCharges)
            {
                float k = p.DribbleRecharge / Mathf.Max(Tuning.I.DribbleRecharge, 0.1f);
                sb.BgColor = new Color(0.1f, 0.12f, 0.18f, 0.7f).Lerp(new Color("ffb020"), k * 0.6f);
            }
            pip.AddThemeStyleboxOverride("panel", sb);
        }
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
        _scoreA.Text = M.Score[0].ToString();
        _scoreB.Text = M.Score[1].ToString();
        if (M.Cfg.Mode == WinMode.Time)
        {
            int s = Mathf.CeilToInt(Mathf.Max(M.Clock, 0));
            _info.Text = M.Overtime ? "PRORROG." : $"{s / 60}:{s % 60:00}";
        }
        else _info.Text = $"ATÉ {M.Cfg.TargetScore}";

        var p = M.LocalPlayer;
        if (p != null)
        {
            _stamina.Value = p.Stamina / Mathf.Max(Tuning.I.StaminaMax, 1);
            _powerBox.Visible = p.ChargingShot;
            _power.Value = p.ShotPower;
            if (p.GetShotPlan(out var plan))
                _powerLabel.Text = plan.Close ? "BANDEJA" : $"{plan.Distance:0.0} m   (máx. {plan.MaxRange:0.0} m{(plan.Capped ? " — carregue mais" : "")})";
            UpdateDribblePips(p);
            _status.Text = p.ChargingPass ? (p.PassHigh ? "PASSE ALTO" : "PASSE") + (p.PassTarget != null ? $" → {p.PassTarget.PlayerName}" : "")
                : p.StealFail > 0 ? "LENTO (errou o roubo)"
                : p.LandLock > 0 ? "POUSANDO"
                : p.IsDribbling ? "DRIBLANDO (imune)" : "";
        }

        if (_countT > 0)
        {
            _countT -= dt;
            float a = Mathf.Clamp(_countT / 0.25f, 0, 1);
            _countdown.Modulate = new Color(1, 1, 1, a);
            if (_countT <= 0) _countdown.Text = "";
        }
        if (_msgT > 0)
        {
            _msgT -= dt;
            _msg.Modulate = new Color(1, 1, 1, Mathf.Clamp(_msgT / 0.3f, 0, 1));
            if (_msgT <= 0) _msg.Text = "";
        }
    }
}
