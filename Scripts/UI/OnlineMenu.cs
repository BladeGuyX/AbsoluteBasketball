using Godot;

namespace AB;

/// <summary>
/// Menu Online: criar/entrar em lobby pela Steam (só amigos, público, código, lista de públicos) e IP direto
/// (testar com duas janelas no mesmo PC ou rede local).
/// v0.28: reorganizado em dois cartões lado a lado (Steam | IP direto), tudo em grade com larguras iguais; status e Voltar
/// numa barra embaixo.
/// </summary>
public partial class OnlineMenu : Control
{
    public string Status;
    VBoxContainer _found;
    Label _status;
    LineEdit _code, _name, _addr;
    SpinBox _port;

    const float ColW = 560;

    public override void _Ready()
    {
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        AddChild(UiKit.FullRect(new MenuBackdrop()));
        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        var panel = new PanelContainer();
        center.AddChild(panel);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 18);
        panel.AddChild(vb);
        vb.AddChild(UiKit.Title("Online"));

        var ns = NetSession.I;
        bool steam = ns?.Steam != null;
        var cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 24);
        vb.AddChild(cols);

        // ---------------- Steam ----------------
        var sc = Card(cols, "Pela Steam", steam ? $"Conectado como {SteamService.LocalName}" : $"Indisponível — {SteamService.Error ?? "abra a Steam e reinicie o jogo"}",
            steam ? UiKit.Green : UiKit.Red);
        var create = new GridContainer { Columns = 2 };
        create.AddThemeConstantOverride("h_separation", 12);
        sc.AddChild(create);
        var friends = Wide(UiKit.Btn("Criar (só amigos)", () => ns.HostSteam(false), UiKit.Green, 0));
        var pub = Wide(UiKit.Btn("Criar (público)", () => ns.HostSteam(true), UiKit.Green, 0));
        friends.Disabled = pub.Disabled = !steam;
        create.AddChild(friends);
        create.AddChild(pub);

        sc.AddChild(Label("Entrar com o código de um amigo"));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        _code = new LineEdit { PlaceholderText = "código do lobby", CustomMinimumSize = new Vector2(0, 58), Editable = steam, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_code);
        var joinCode = UiKit.Btn("Entrar", () => ns.JoinSteamByCode(_code.Text), 170);
        joinCode.Disabled = !steam;
        row.AddChild(joinCode);
        sc.AddChild(row);

        sc.AddChild(Label("Lobbies públicos"));
        var search = Wide(UiKit.Btn("Procurar lobbies públicos", () => { ns.Steam.Search(); SetStatus("Procurando..."); }, 0));
        search.Disabled = !steam;
        sc.AddChild(search);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 150), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var listBg = new PanelContainer();
        listBg.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Field, 12, 10, 8));
        listBg.AddChild(scroll);
        _found = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _found.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_found);
        sc.AddChild(listBg);
        _found.AddChild(UiKit.Lbl(steam ? "Clique em \"Procurar\" para ver os lobbies." : "Precisa da Steam aberta.", 18, UiKit.Muted));
        if (steam) ns.Steam.ListUpdated += FillFound;

        // ---------------- IP direto ----------------
        var ip = Card(cols, "IP direto", "Duas janelas no mesmo PC ou rede local", UiKit.Muted);
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 12);
        ip.AddChild(grid);
        grid.AddChild(Label("Seu nome"));
        _name = new LineEdit { Text = SteamService.LocalName ?? "Jogador", CustomMinimumSize = new Vector2(0, 58), MaxLength = 20, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(_name);
        grid.AddChild(Label("Porta"));
        _port = new SpinBox { MinValue = 1024, MaxValue = 65535, Value = EnetTransport.DefaultPort, CustomMinimumSize = new Vector2(0, 58), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(_port);
        grid.AddChild(Label("IP do host"));
        _addr = new LineEdit { Text = "127.0.0.1", PlaceholderText = "IP do host", CustomMinimumSize = new Vector2(0, 58), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddChild(_addr);
        ip.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var btns = new GridContainer { Columns = 2 };
        btns.AddThemeConstantOverride("h_separation", 12);
        ip.AddChild(btns);
        btns.AddChild(Wide(UiKit.Btn("Hospedar", () => ns.HostDirect((int)_port.Value, _name.Text), UiKit.Green, 0)));
        btns.AddChild(Wide(UiKit.Btn("Entrar no IP", () => { if (ns.JoinDirect(_addr.Text.Trim(), (int)_port.Value, _name.Text)) SetStatus("Conectando..."); }, 0)));
        var tip = UiKit.Lbl("Quem hospeda passa o próprio IP (aparece no lobby) para o amigo digitar em \"IP do host\".", 17, UiKit.Muted);
        tip.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ip.AddChild(tip);

        // ---------------- barra de baixo ----------------
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 16);
        vb.AddChild(bar);
        var back = UiKit.Btn("Voltar", () => { ns.Leave(toMenu: false); GameRoot.I.ShowMainMenu(); }, UiKit.Grey, 220);
        bar.AddChild(back);
        _status = UiKit.Lbl(Status ?? "", 20, UiKit.Muted);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bar.AddChild(_status);
        (steam ? friends : back).CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Cartão com título, linha de status e conteúdo (coluna de largura fixa).</summary>
    static VBoxContainer Card(HBoxContainer parent, string title, string sub, Color subColor)
    {
        var pc = new PanelContainer { CustomMinimumSize = new Vector2(ColW, 0) };
        var sb = UiKit.Box(new Color("f4f7fc"), 20, 24, 20);
        sb.BorderColor = new Color("e1e8f2"); sb.SetBorderWidthAll(2); sb.BorderWidthBottom = 5;
        pc.AddThemeStyleboxOverride("panel", sb);
        parent.AddChild(pc);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 12);
        pc.AddChild(vb);
        vb.AddChild(UiKit.Section(title, 30));
        var s = UiKit.Lbl(sub, 18, subColor);
        s.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vb.AddChild(s);
        return vb;
    }

    static Label Label(string t)
    {
        var l = UiKit.Lbl(t, 20, UiKit.Muted);
        l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return l;
    }

    static Button Wide(Button b) { b.SizeFlagsHorizontal = SizeFlags.ExpandFill; return b; }

    public override void _ExitTree()
    {
        if (NetSession.I?.Steam != null) NetSession.I.Steam.ListUpdated -= FillFound;
    }

    void SetStatus(string s) { if (_status != null) _status.Text = s; }

    void FillFound()
    {
        foreach (var c in _found.GetChildren()) c.QueueFree();
        var ns = NetSession.I;
        if (ns.Steam.Found.Count == 0) { SetStatus("Nenhum lobby público agora."); _found.AddChild(UiKit.Lbl("Nenhum lobby público agora.", 18, UiKit.Muted)); return; }
        SetStatus($"{ns.Steam.Found.Count} lobby(s) encontrado(s).");
        foreach (var l in ns.Steam.Found)
        {
            var id = l.Id;
            var b = UiKit.Btn($"{l.Name}  ({l.Members}/{NetSession.MaxPlayers})", () => ns.JoinSteamLobby(id), 0);
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _found.AddChild(b);
        }
    }
}
