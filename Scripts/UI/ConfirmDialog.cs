using System;
using Godot;

namespace AB;

/// <summary>
/// v0.31: janela "Tem certeza?" usada em todos os botões de sair (do jogo, da partida, do lobby). Escurece o fundo,
/// "Cancelar" já vem selecionado; Esc/pausa também cancela. Funciona com o jogo pausado.
/// </summary>
public partial class ConfirmDialog : Control
{
    public string Message = "";
    public string YesText = "Sair";
    public Action OnYes;
    Button _no;

    /// <summary>Abre a confirmação por cima de <paramref name="parent"/> (o pai precisa estar na árvore).</summary>
    public static void Ask(Node parent, string message, string yesText, Action onYes)
    {
        if (parent == null || !parent.IsInsideTree()) { onYes?.Invoke(); return; }
        // por cima de tudo (inclusive HUD e menus de pausa), numa camada própria
        var layer = new CanvasLayer { Layer = 30, ProcessMode = ProcessModeEnum.Always };
        parent.GetTree().Root.AddChild(layer);
        layer.AddChild(new ConfirmDialog { Message = message, YesText = yesText, OnYes = onYes });
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        UiKit.FullRect(this);
        Theme = UiKit.Theme;
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(UiKit.Dim(0.55f));
        var center = UiKit.FullRect(new CenterContainer());
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(620, 0) };
        center.AddChild(panel);
        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 18);
        panel.AddChild(vb);
        vb.AddChild(UiKit.Title("Tem certeza?", 44, UiKit.Red));
        var msg = UiKit.Lbl(Message, 24, UiKit.Muted);
        msg.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vb.AddChild(msg);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        vb.AddChild(row);
        _no = UiKit.Btn("Cancelar", Close, UiKit.Grey, 0);
        _no.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_no);
        var yes = UiKit.Btn(YesText, () => { var a = OnYes; Close(); a?.Invoke(); }, UiKit.Red, 0);
        yes.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(yes);
        _no.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _Input(InputEvent e)
    {
        if (e.IsActionPressed(Act.Pause) || e.IsActionPressed("ui_cancel")) { GetViewport().SetInputAsHandled(); Close(); }
    }

    void Close()
    {
        var layer = GetParent();
        if (layer is CanvasLayer && IsInstanceValid(layer)) layer.QueueFree(); else QueueFree();
    }
}
