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
    OptionButton _size, _mode, _diff, _abil, _test;
    SpinBox _pts, _mins, _teamSize;
    Label _ptsLbl, _minsLbl;
    Button _start;
    bool _updating;
    float _friendsRefresh;

    public override void _Ready()
    {
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        AddChild(UiKit.FullRect(new MenuBackdrop()));
        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        var panel = new PanelContainer();
        center.AddChild(panel);
        // v0.28: reorganizado — cabeçalho, blocos lado a lado (times | configurações | amigos) e barra de botões embaixo
        var main = new VBoxContainer();
        main.AddThemeConstantOverride("separation", 18);
        panel.AddChild(main);

        var ns = NetSession.I;
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 20);
        main.AddChild(head);
        head.AddChild(UiKit.Title("Lobby"));
        _info = UiKit.Lbl("", 20, UiKit.Muted);
        _info.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(_info);

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 20);
        main.AddChild(body);

        // ---------------- times ----------------
        var tc = Card(body, "Times", 0);
        var teams = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        teams.AddThemeConstantOverride("separation", 16);
        tc.AddChild(teams);
        _teamA = TeamColumn(teams, 0);
        _teamB = TeamColumn(teams, 1);

        // ---------------- configurações (só o host muda) ----------------
        var cc = Card(body, ns.IsHost ? "Partida" : "Partida (o host escolhe)", 0);
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 10);
        cc.AddChild(grid);
        _size = Opt();
        foreach (CourtSize s in new[] { CourtSize.Small, CourtSize.Medium, CourtSize.Large })
            _size.AddItem($"{CourtGeo.Name(s)} (até {CourtGeo.MaxPlayers(s)})", (int)s);
        Row(grid, "Quadra", _size);
        _mode = Opt();
        _mode.AddItem("Por pontos", 0);
        _mode.AddItem("Por tempo", 1);
        Row(grid, "Vitória", _mode);
        _pts = new SpinBox { MinValue = 1, MaxValue = 99, CustomMinimumSize = new Vector2(300, 52) };
        _ptsLbl = Row(grid, "Pontos para vencer", _pts);
        _mins = new SpinBox { MinValue = 1, MaxValue = 60, Step = 0.5, Suffix = "min", CustomMinimumSize = new Vector2(300, 52) };
        _minsLbl = Row(grid, "Duração", _mins);
        _teamSize = new SpinBox { MinValue = 1, MaxValue = 5, CustomMinimumSize = new Vector2(300, 52) };
        Row(grid, "Jogadores por time", _teamSize);
        _diff = Opt();
        _diff.AddItem("Fácil", 0); _diff.AddItem("Normal", 1); _diff.AddItem("Difícil", 2);
        Row(grid, "Bots (vagas vazias)", _diff);
        _abil = Opt();
        _abil.AddItem("Com habilidades", 0); _abil.AddItem("Sem habilidades", 1);
        Row(grid, "Habilidades", _abil);
        // v0.27: modo de teste (partida infinita + trapaças para todos)
        _test = Opt();
        _test.AddItem("Partida normal", 0); _test.AddItem("Teste (infinita + trapaças)", 1);
        Row(grid, "Modo", _test);
        _abil.ItemSelected += _ => OnSettingChanged();
        _test.ItemSelected += _ => OnSettingChanged();
        _size.ItemSelected += _ => OnSettingChanged();
        _mode.ItemSelected += _ => OnSettingChanged();
        _pts.ValueChanged += _ => OnSettingChanged();
        _mins.ValueChanged += _ => OnSettingChanged();
        _teamSize.ValueChanged += _ => OnSettingChanged();
        _diff.ItemSelected += _ => OnSettingChanged();

        // ---------------- convidar amigos (Steam) ----------------
        if (ns.ViaSteam && ns.Steam != null)
        {
            var fc = Card(body, "Convidar amigos", 380);
            if (ns.Steam.OverlayAvailable)
            {
                var ov = UiKit.Btn("Abrir convite da Steam", () => ns.Steam.OpenOverlayInvite(), 0);
                ov.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                fc.AddChild(ov);
            }
            var hint = UiKit.Lbl("O amigo precisa estar com o jogo aberto para aceitar o convite (ou usar o código).", 17, UiKit.Muted);
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            fc.AddChild(hint);
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 330), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
            _friends = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _friends.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_friends);
            fc.AddChild(scroll);
            FillFriends();
        }

        // ---------------- barra de botões ----------------
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 14);
        main.AddChild(bar);
        bar.AddChild(UiKit.Btn("Sair do lobby", () => ns.Leave(toMenu: true), UiKit.Red, 230));
        _wait = UiKit.Lbl("Aguardando o host começar...", 20, UiKit.Muted);
        _wait.Visible = !ns.IsHost;
        _wait.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bar.AddChild(_wait);
        bar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        // v0.26: cada um escolhe o personagem no lobby (vai para o host e aparece para todos)
        Button chrBtn = null;
        chrBtn = UiKit.Btn("Escolher personagem", () => AddChild(new CharacterSelect
        {
            ConfirmText = "Escolher", AbilitiesOn = NetSession.I.Settings.Abilities,
            OnConfirm = id => NetSession.I.SetMyCharacter(id), OnBack = () => chrBtn.GrabFocus(),
        }), UiKit.Purple, 290);
        bar.AddChild(chrBtn);
        _start = UiKit.Btn("Começar partida", () => ns.HostStartMatch(), UiKit.Green, 290);
        _start.Visible = ns.IsHost;
        bar.AddChild(_start);

        ns.LobbyChanged += Refresh;
        Refresh();
        if (ns.IsHost) _start.CallDeferred(Control.MethodName.GrabFocus);
        else chrBtn.CallDeferred(Control.MethodName.GrabFocus);
    }

    static OptionButton Opt() => new() { CustomMinimumSize = new Vector2(300, 52) };

    /// <summary>Bloco com título (cartão claro com borda).</summary>
    static VBoxContainer Card(HBoxContainer parent, string title, float minW)
    {
        var pc = new PanelContainer { CustomMinimumSize = new Vector2(minW, 0) };
        var sb = UiKit.Box(new Color("f4f7fc"), 20, 22, 18);
        sb.BorderColor = new Color("e1e8f2"); sb.SetBorderWidthAll(2); sb.BorderWidthBottom = 5;
        pc.AddThemeStyleboxOverride("panel", sb);
        parent.AddChild(pc);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 12);
        pc.AddChild(vb);
        vb.AddChild(UiKit.Section(title, 26));
        return vb;
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
        var l = UiKit.Lbl(label, 22);
        l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        g.AddChild(l);
        g.AddChild(c);
        return l;
    }

    VBoxContainer TeamColumn(HBoxContainer parent, int team)
    {
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 6);
        var head = new PanelContainer();
        head.AddThemeStyleboxOverride("panel", UiKit.Raised(Palette.Team[team], 5, 14, 16, 6));
        var ht = UiKit.Outlined(Palette.TeamName[team], 26, Colors.White, outline: 0);
        ht.LabelSettings.Font = UiKit.FontBold;
        head.AddChild(ht);
        col.AddChild(head);
        var list = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        col.AddChild(list);
        col.AddChild(UiKit.Btn("Entrar neste time", () => NetSession.I.SetMyTeam(team), Palette.Team[team].Darkened(0.1f), 300));
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
            Abilities = _abil.Selected == 0, TestMode = _test.Selected == 1,
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
        _abil.Selected = s.Abilities ? 0 : 1;
        _test.Selected = s.TestMode ? 1 : 0;
        foreach (var c in new Control[] { _size, _mode, _pts, _mins, _teamSize, _diff, _abil, _test })
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
                string tag = (m.NetId == 0 ? " ★" : "") + (m.NetId == ns.MyNetId ? " (você)" : ""); // ★ = host
                list.AddChild(MemberRow(m.Name + tag, Characters.Get(m.Character), team, m.NetId == ns.MyNetId));
            }
            int bots = Mathf.Clamp(s.TeamSize, 1, perTeam) - humans.Count;
            for (int i = 0; i < bots; i++) list.AddChild(MemberRow("Bot (vaga vazia)", null, team, false));
        }
        if (_start != null) _start.Disabled = ns.Members.Count == 0;
    }

    /// <summary>v0.27: cartão de um jogador do lobby: barra da cor do time, nome e o personagem numa etiqueta colorida.</summary>
    static Control MemberRow(string name, Characters.Def chr, int team, bool me)
    {
        var pc = new PanelContainer();
        var sb = UiKit.Box(me ? new Color("eaf2ff") : new Color("f4f7fc"), 12, 12, 6);
        sb.BorderColor = Palette.Team[team]; sb.BorderWidthLeft = 6;
        pc.AddThemeStyleboxOverride("panel", sb);
        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        var l = chr != null ? UiKit.Section(name, 20, me ? Palette.Team[team].Darkened(0.2f) : null) : UiKit.Lbl(name, 19, new Color("8a94a6"));
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        l.ClipText = true;
        hb.AddChild(l);
        if (chr != null)
        {
            var tag = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
            tag.AddThemeStyleboxOverride("panel", UiKit.Box(chr.Color, 10, 10, 2));
            var tl = UiKit.Lbl(chr.Name, 17, Colors.White);
            tl.AddThemeFontOverride("font", UiKit.FontBold);
            tag.AddChild(tl);
            hb.AddChild(tag);
        }
        pc.AddChild(hb);
        return pc;
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
