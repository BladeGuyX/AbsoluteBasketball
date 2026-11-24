using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// Lobby online: times (AZUL / LARANJA), configurações da partida (só o host muda), convite de amigos da Steam,
/// código do lobby / endereço IP, começar (host) e sair. Vagas vazias de cada time viram bots.
/// </summary>
public partial class LobbyMenu : Control
{
    VBoxContainer _teamA, _teamB, _friends;
    Label _info, _wait;
    OptionButton _size, _mode, _diff;
    SpinBox _pts, _mins, _teamSize;
    Label _ptsLbl, _minsLbl;
    Button _start;
    bool _updating;
    float _friendsRefresh;

    public override void _Ready()
    {
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        AddChild(UiKit.FullRect(new ColorRect { Color = new Color("3b8fe6") }));
        AddChild(UiKit.Anchor(new ColorRect { Color = Palette.Sand }, 0, 0.82f, 1, 1, 0, 0, 0, 0));
        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        var panel = new PanelContainer();
        center.AddChild(panel);
        var root = new HBoxContainer();
        root.AddThemeConstantOverride("separation", 34);
        panel.AddChild(root);

        var ns = NetSession.I;
        // ---------------- coluna esquerda: times + configurações ----------------
        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 12);
        root.AddChild(left);
        left.AddChild(UiKit.Lbl("Lobby", 48));
        _info = UiKit.Lbl("", 20, new Color("5b6577"));
        left.AddChild(_info);

        var teams = new HBoxContainer();
        teams.AddThemeConstantOverride("separation", 20);
        left.AddChild(teams);
        _teamA = TeamColumn(teams, 0);
        _teamB = TeamColumn(teams, 1);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 8);
        left.AddChild(grid);
        _size = new OptionButton { CustomMinimumSize = new Vector2(340, 50) };
        foreach (CourtSize s in new[] { CourtSize.Small, CourtSize.Medium, CourtSize.Large })
            _size.AddItem($"{CourtGeo.Name(s)} (até {CourtGeo.MaxPlayers(s)})", (int)s);
        Row(grid, "Quadra", _size);
        _mode = new OptionButton { CustomMinimumSize = new Vector2(340, 50) };
        _mode.AddItem("Por pontos", 0);
        _mode.AddItem("Por tempo", 1);
        Row(grid, "Vitória", _mode);
        _pts = new SpinBox { MinValue = 1, MaxValue = 99, CustomMinimumSize = new Vector2(340, 50) };
        _ptsLbl = Row(grid, "Pontos para vencer", _pts);
        _mins = new SpinBox { MinValue = 1, MaxValue = 60, Step = 0.5, Suffix = "min", CustomMinimumSize = new Vector2(340, 50) };
        _minsLbl = Row(grid, "Duração", _mins);
        _teamSize = new SpinBox { MinValue = 1, MaxValue = 5, CustomMinimumSize = new Vector2(340, 50) };
        Row(grid, "Jogadores por time", _teamSize);
        _diff = new OptionButton { CustomMinimumSize = new Vector2(340, 50) };
        _diff.AddItem("Fácil", 0); _diff.AddItem("Normal", 1); _diff.AddItem("Difícil", 2);
        Row(grid, "Bots (vagas vazias)", _diff);
        _size.ItemSelected += _ => OnSettingChanged();
        _mode.ItemSelected += _ => OnSettingChanged();
        _pts.ValueChanged += _ => OnSettingChanged();
        _mins.ValueChanged += _ => OnSettingChanged();
        _teamSize.ValueChanged += _ => OnSettingChanged();
        _diff.ItemSelected += _ => OnSettingChanged();

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 12);
        left.AddChild(buttons);
        _start = UiKit.Btn("Começar partida", () => ns.HostStartMatch(), 300);
        _start.Visible = ns.IsHost;
        buttons.AddChild(_start);
        buttons.AddChild(UiKit.Btn("Sair do lobby", () => ns.Leave(toMenu: true), 240));
        _wait = UiKit.Lbl("Aguardando o host começar...", 20, new Color("5b6577"));
        _wait.Visible = !ns.IsHost;
        left.AddChild(_wait);

        // ---------------- coluna direita: convidar amigos (Steam) ----------------
        if (ns.ViaSteam && ns.Steam != null)
        {
            var right = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
            right.AddThemeConstantOverride("separation", 10);
            root.AddChild(right);
            right.AddChild(UiKit.Lbl("Convidar amigos", 32));
            if (ns.Steam.OverlayAvailable) right.AddChild(UiKit.Btn("Abrir convite da Steam", () => ns.Steam.OpenOverlayInvite(), 400));
            right.AddChild(UiKit.Lbl("O amigo precisa estar com o jogo aberto\npara aceitar o convite (ou usar o código).", 18, new Color("5b6577")));
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(420, 420) };
            _friends = new VBoxContainer();
            _friends.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_friends);
            right.AddChild(scroll);
            FillFriends();
        }

        ns.LobbyChanged += Refresh;
        Refresh();
        if (ns.IsHost) _start.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _ExitTree()
    {
        if (NetSession.I != null) NetSession.I.LobbyChanged -= Refresh;
    }

    public override void _Process(double delta)
    {
        if (_friends == null) return;
        _friendsRefresh += (float)delta;
        if (_friendsRefresh > 5f) { _friendsRefresh = 0; FillFriends(); }
    }

    static Label Row(GridContainer g, string label, Control c)
    {
        var l = new Label { Text = label };
        g.AddChild(l);
        g.AddChild(c);
        return l;
    }

    VBoxContainer TeamColumn(HBoxContainer parent, int team)
    {
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        col.AddThemeConstantOverride("separation", 6);
        var head = new PanelContainer();
        head.AddThemeStyleboxOverride("panel", UiKit.Box(Palette.Team[team], 14, 16, 6));
        head.AddChild(UiKit.Outlined(Palette.TeamName[team], 26, Colors.White, outline: 0));
        col.AddChild(head);
        var list = new VBoxContainer { CustomMinimumSize = new Vector2(300, 200) };
        col.AddChild(list);
        col.AddChild(UiKit.Btn("Entrar neste time", () => NetSession.I.SetMyTeam(team), 300));
        parent.AddChild(col);
        return list;
    }

    void OnSettingChanged()
    {
        UpdateVisibility();
        if (_updating || !NetSession.I.IsHost) return;
        var s = new OnlineSettings
        {
            Size = (CourtSize)_size.GetSelectedId(),
            Mode = _mode.Selected == 1 ? WinMode.Time : WinMode.Points,
            TargetScore = (int)_pts.Value,
            TimeMinutes = (float)_mins.Value,
            TeamSize = (int)_teamSize.Value,
            BotDifficulty = _diff.Selected,
        };
        NetSession.I.HostSetSettings(s);
    }

    void UpdateVisibility()
    {
        bool time = _mode.Selected == 1;
        _pts.Visible = _ptsLbl.Visible = !time;
        _mins.Visible = _minsLbl.Visible = time;
        _teamSize.MaxValue = Mathf.Max(1, CourtGeo.MaxPlayers((CourtSize)_size.GetSelectedId()) / 2);
    }

    void Refresh()
    {
        var ns = NetSession.I;
        if (ns == null || !ns.Active) return;
        _updating = true;
        var s = ns.Settings;
        _size.Selected = (int)s.Size;
        _mode.Selected = s.Mode == WinMode.Time ? 1 : 0;
        _pts.Value = s.TargetScore;
        _mins.Value = s.TimeMinutes;
        UpdateVisibility();
        _teamSize.Value = s.TeamSize;
        _diff.Selected = Mathf.Clamp(s.BotDifficulty, 0, 2);
        foreach (var c in new Control[] { _size, _mode, _pts, _mins, _teamSize, _diff })
        {
            if (c is OptionButton ob) ob.Disabled = !ns.IsHost;
            else if (c is SpinBox sb) sb.Editable = ns.IsHost;
        }
        _updating = false;

        if (ns.ViaSteam && ns.Steam != null && ns.Steam.InLobby)
            _info.Text = $"Pela Steam · código do lobby: {ns.Steam.Code}";
        else _info.Text = ns.IsHost ? $"IP direto · seu endereço: {ns.DirectAddress}" : $"IP direto · conectado em {ns.DirectAddress}";
        if (ns.IsClient) { int ping = ns.PingToHost(); if (ping >= 0) _info.Text += $" · ping {ping} ms"; }

        int perTeam = Mathf.Max(1, CourtGeo.MaxPlayers(s.Size) / 2);
        for (int team = 0; team < 2; team++)
        {
            var list = team == 0 ? _teamA : _teamB;
            foreach (var c in list.GetChildren()) c.QueueFree();
            var humans = ns.Members.Where(m => m.Team == team).ToList();
            foreach (var m in humans)
            {
                string tag = (m.NetId == 0 ? "  (host)" : "") + (m.NetId == ns.MyNetId ? "  (você)" : "");
                list.AddChild(UiKit.Lbl(m.Name + tag, 24, m.NetId == ns.MyNetId ? Palette.Team[team] : null));
            }
            int bots = Mathf.Clamp(s.TeamSize, 1, perTeam) - humans.Count;
            for (int i = 0; i < bots; i++) list.AddChild(UiKit.Lbl("Bot", 22, new Color("8a94a6")));
        }
        if (_start != null) _start.Disabled = ns.Members.Count == 0;
    }

    void FillFriends()
    {
        var ns = NetSession.I;
        if (_friends == null || ns?.Steam == null) return;
        foreach (var c in _friends.GetChildren()) c.QueueFree();
        var list = ns.Steam.Friends().Where(f => f.Online).ToList();
        if (list.Count == 0) { _friends.AddChild(UiKit.Lbl("Nenhum amigo online.", 20, new Color("5b6577"))); return; }
        foreach (var f in list)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var name = UiKit.Lbl(f.Name + (f.InThisGame ? "  · no jogo" : ""), 20, f.InThisGame ? new Color("1f9d55") : null);
            name.CustomMinimumSize = new Vector2(250, 0);
            name.ClipText = true;
            row.AddChild(name);
            var id = f.Id;
            var b = UiKit.Btn(ns.Steam.WasInvited(id) ? "Convidado" : "Convidar", null, 140);
            b.Pressed += () => { if (ns.Steam.Invite(id)) b.Text = "Convidado"; };
            row.AddChild(b);
            _friends.AddChild(row);
        }
    }
}
