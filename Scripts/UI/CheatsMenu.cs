using System;
using System.Linq;
using Godot;

namespace AB;

/// <summary>
/// v0.27: aba de trapaças do modo de teste (menu de pausa). Funciona offline e online: offline/host executa na hora; no
/// cliente cada botão vira um pedido ao host (<see cref="Match.RequestCheat"/>). A lista de jogadores se refaz sozinha quando
/// alguém entra/sai ou troca de personagem.
/// </summary>
public partial class CheatsMenu : Control
{
    public Match M;
    public Action OnClosed;
    VBoxContainer _players;
    CheckButton _noCd, _stam;
    OptionButton _addTeam, _addChar;
    Button _back;
    bool _dirty = true;
    float _refreshT;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(UiKit.Dim(0.6f));
        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1180, 820) };
        center.AddChild(panel);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vb);
        vb.AddChild(UiKit.Title("Trapaças", 48, UiKit.Purple));
        vb.AddChild(UiKit.Lbl(M.IsClient ? "Modo de teste · os pedidos vão para o host" : "Modo de teste · a partida não acaba", 20, UiKit.Muted));

        var cols = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 30);
        vb.AddChild(cols);

        // ---------------- esquerda: você e a partida ----------------
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0) };
        left.AddThemeConstantOverride("separation", 10);
        cols.AddChild(left);
        left.AddChild(UiKit.Section("Seu personagem", 26));
        var chars = new HBoxContainer();
        chars.AddThemeConstantOverride("separation", 8);
        for (int i = 0; i < Characters.Count; i++)
        {
            int id = i;
            chars.AddChild(UiKit.Btn(Characters.Get(i).Name, () => M.RequestCheat(Cheat.SetChar, M.LocalPlayer?.Id ?? 0, id), Characters.Get(i).Color.Darkened(0.15f), 140));
        }
        left.AddChild(chars);
        left.AddChild(UiKit.Btn("Recarregar minhas habilidades e ultimate", () => M.RequestCheat(Cheat.Refill, M.LocalPlayer?.Id ?? 0), UiKit.Purple, 440));
        left.AddChild(UiKit.Btn("Recarregar de todos", () => M.RequestCheat(Cheat.RefillAll), UiKit.Purple, 440));
        left.AddChild(UiKit.Btn("Pegar a bola", () => M.RequestCheat(Cheat.GiveBall, M.LocalPlayer?.Id ?? 0), 440));
        left.AddChild(UiKit.Btn("Zerar o placar", () => M.RequestCheat(Cheat.ResetScore), UiKit.Grey, 440));
        _noCd = UiKit.CheckRow("Sem recarga (só você)", M.LocalPlayer?.Ab.NoCd ?? false, b => M.RequestCheat(Cheat.NoCooldowns, b ? 1 : 0));
        _stam = UiKit.CheckRow("Estamina infinita", M.InfiniteStamina, b => M.RequestCheat(Cheat.InfStamina, b ? 1 : 0));
        left.AddChild(_noCd);
        left.AddChild(_stam);

        // ---------------- direita: jogadores e bots ----------------
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 10);
        cols.AddChild(right);
        right.AddChild(UiKit.Section("Jogadores", 26));
        var add = new HBoxContainer();
        add.AddThemeConstantOverride("separation", 8);
        _addTeam = new OptionButton { CustomMinimumSize = new Vector2(180, 56) };
        _addTeam.AddItem("Time AZUL", 0);
        _addTeam.AddItem("Time LARANJA", 1);
        _addTeam.Selected = 1;
        _addChar = new OptionButton { CustomMinimumSize = new Vector2(160, 56) };
        for (int i = 0; i < Characters.Count; i++) _addChar.AddItem(Characters.Get(i).Name, i);
        add.AddChild(_addTeam);
        add.AddChild(_addChar);
        add.AddChild(UiKit.Btn("+ Adicionar bot", () => M.RequestCheat(Cheat.AddBot, _addTeam.Selected, _addChar.Selected), UiKit.Green, 230));
        right.AddChild(add);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 420) };
        _players = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _players.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_players);
        right.AddChild(scroll);

        _back = UiKit.Btn("Voltar", Close, UiKit.Grey);
        vb.AddChild(_back);
        _back.CallDeferred(Control.MethodName.GrabFocus);
        M.PlayersChanged += MarkDirty;
    }

    public override void _ExitTree() { if (M != null) M.PlayersChanged -= MarkDirty; }

    void MarkDirty() => _dirty = true;

    public override void _Process(double delta)
    {
        if (M == null || !IsInstanceValid(M)) return;
        _refreshT -= (float)delta;
        if (_refreshT <= 0) { _refreshT = 0.5f; _dirty = true; } // online: o personagem dos outros chega pelo snapshot
        _noCd.SetPressedNoSignal(M.LocalPlayer != null && IsInstanceValid(M.LocalPlayer) && M.LocalPlayer.Ab.NoCd);
        _stam.SetPressedNoSignal(M.InfiniteStamina);
        if (_dirty) { _dirty = false; Rebuild(); }
    }

    string _sig;

    void Rebuild()
    {
        string sig = string.Join("|", M.Players.Select(p => $"{p.Id}:{p.CharacterId}:{p.PlayerName}:{p.Team}"));
        if (sig == _sig) return;
        _sig = sig;
        foreach (var c in _players.GetChildren()) c.QueueFree();
        foreach (var p in M.Players.OrderBy(q => q.Team).ThenBy(q => q.Id))
        {
            var row = new PanelContainer();
            var sb = UiKit.Box(new Color("f4f7fc"), 14, 14, 6);
            sb.BorderColor = Palette.Team[p.Team]; sb.BorderWidthLeft = 8;
            row.AddThemeStyleboxOverride("panel", sb);
            var hb = new HBoxContainer();
            hb.AddThemeConstantOverride("separation", 10);
            row.AddChild(hb);
            string who = p == M.LocalPlayer ? "  (você)" : p.IsBot ? "  (bot)" : p.IsDummy ? "  (boneco)" : "";
            var name = UiKit.Section(p.PlayerName + who, 22, Palette.Team[p.Team].Darkened(0.2f));
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            hb.AddChild(name);
            int pid = p.Id;
            bool canEdit = p == M.LocalPlayer || p.IsBot || p.IsDummy;
            var ch = new OptionButton { CustomMinimumSize = new Vector2(150, 50), Disabled = !canEdit };
            for (int i = 0; i < Characters.Count; i++) ch.AddItem(Characters.Get(i).Name, i);
            ch.Selected = p.CharacterId;
            ch.ItemSelected += i => M.RequestCheat(Cheat.SetChar, pid, (int)i);
            hb.AddChild(ch);
            hb.AddChild(UiKit.Btn("Recarregar", () => M.RequestCheat(Cheat.Refill, pid), UiKit.Purple, 150));
            var rm = UiKit.Btn("Remover", () => M.RequestCheat(Cheat.RemoveBot, pid), UiKit.Red, 130);
            rm.Disabled = !(p.IsBot || p.IsDummy) || p == M.LocalPlayer;
            hb.AddChild(rm);
            _players.AddChild(row);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed(Act.Pause)) { GetViewport().SetInputAsHandled(); Close(); }
    }

    void Close()
    {
        OnClosed?.Invoke();
        QueueFree();
    }
}
