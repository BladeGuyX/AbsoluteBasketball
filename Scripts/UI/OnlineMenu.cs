using Godot;

namespace AB;

/// <summary>
/// Menu Online: criar/entrar em lobby pela Steam (só amigos, público, código, lista de públicos) e IP direto
/// (testar com duas janelas no mesmo PC ou rede local).
/// </summary>
public partial class OnlineMenu : Control
{
    public string Status;
    VBoxContainer _found;
    Label _status;
    LineEdit _code, _name, _addr;
    SpinBox _port;

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
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vb);
        vb.AddChild(UiKit.Lbl("Online", 48));

        var ns = NetSession.I;
        bool steam = ns?.Steam != null;
        // ---------------- Steam ----------------
        vb.AddChild(UiKit.Lbl(steam ? $"Steam: conectado como {SteamService.LocalName}" : $"Steam indisponível — {SteamService.Error ?? "abra a Steam e reinicie o jogo"}",
            22, steam ? new Color("1f9d55") : new Color("c0392b")));
        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 12);
        var friends = UiKit.Btn("Criar lobby (só amigos)", () => ns.HostSteam(false), 330);
        var pub = UiKit.Btn("Criar lobby (público)", () => ns.HostSteam(true), 330);
        friends.Disabled = pub.Disabled = !steam;
        row1.AddChild(friends);
        row1.AddChild(pub);
        vb.AddChild(row1);

        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", 12);
        _code = new LineEdit { PlaceholderText = "código do lobby", CustomMinimumSize = new Vector2(420, 56), Editable = steam };
        row2.AddChild(_code);
        var joinCode = UiKit.Btn("Entrar com código", () => ns.JoinSteamByCode(_code.Text), 240);
        joinCode.Disabled = !steam;
        row2.AddChild(joinCode);
        vb.AddChild(row2);

        var search = UiKit.Btn("Procurar lobbies públicos", () =>
        {
            ns.Steam.Search();
            SetStatus("Procurando...");
        }, 672);
        search.Disabled = !steam;
        vb.AddChild(search);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(672, 120) };
        _found = new VBoxContainer();
        scroll.AddChild(_found);
        vb.AddChild(scroll);
        if (steam) ns.Steam.ListUpdated += FillFound;

        // ---------------- IP direto ----------------
        vb.AddChild(new HSeparator());
        vb.AddChild(UiKit.Lbl("IP direto (teste com duas janelas no mesmo PC ou rede local)", 22, new Color("5b6577")));
        var row3 = new HBoxContainer();
        row3.AddThemeConstantOverride("separation", 12);
        row3.AddChild(new Label { Text = "Seu nome" });
        _name = new LineEdit { Text = SteamService.LocalName ?? "Jogador", CustomMinimumSize = new Vector2(300, 56), MaxLength = 20 };
        row3.AddChild(_name);
        row3.AddChild(new Label { Text = "Porta" });
        _port = new SpinBox { MinValue = 1024, MaxValue = 65535, Value = EnetTransport.DefaultPort, CustomMinimumSize = new Vector2(150, 56) };
        row3.AddChild(_port);
        vb.AddChild(row3);
        var row4 = new HBoxContainer();
        row4.AddThemeConstantOverride("separation", 12);
        row4.AddChild(UiKit.Btn("Hospedar por IP", () => ns.HostDirect((int)_port.Value, _name.Text), 250));
        _addr = new LineEdit { Text = "127.0.0.1", PlaceholderText = "IP do host", CustomMinimumSize = new Vector2(220, 56) };
        row4.AddChild(_addr);
        row4.AddChild(UiKit.Btn("Entrar por IP", () =>
        {
            if (ns.JoinDirect(_addr.Text.Trim(), (int)_port.Value, _name.Text)) SetStatus("Conectando...");
        }, 190));
        vb.AddChild(row4);

        _status = UiKit.Lbl(Status ?? "", 22, new Color("5b6577"));
        vb.AddChild(_status);
        var back = UiKit.Btn("Voltar", () => { ns.Leave(toMenu: false); GameRoot.I.ShowMainMenu(); });
        vb.AddChild(back);
        (steam ? friends : back).CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _ExitTree()
    {
        if (NetSession.I?.Steam != null) NetSession.I.Steam.ListUpdated -= FillFound;
    }

    void SetStatus(string s) { if (_status != null) _status.Text = s; }

    void FillFound()
    {
        foreach (var c in _found.GetChildren()) c.QueueFree();
        var ns = NetSession.I;
        if (ns.Steam.Found.Count == 0) { SetStatus("Nenhum lobby público agora."); return; }
        SetStatus($"{ns.Steam.Found.Count} lobby(s) encontrado(s).");
        foreach (var l in ns.Steam.Found)
        {
            var id = l.Id;
            _found.AddChild(UiKit.Btn($"{l.Name}  ({l.Members}/{NetSession.MaxPlayers})", () => ns.JoinSteamLobby(id), 640));
        }
    }
}
