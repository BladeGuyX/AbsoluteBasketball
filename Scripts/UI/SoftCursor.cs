using Godot;

namespace AB;

/// <summary>
/// Cursor desenhado pelo próprio jogo. O cursor do Windows fica transparente o tempo todo (GameRoot):
/// em tela cheia, depois de um alt-tab, o cursor do sistema às vezes "congela" na imagem ou reaparece
/// durante o jogo. Este aqui segue a posição real do mouse e some quando o mouse está preso (jogando).
/// </summary>
public partial class SoftCursor : Control
{
    static readonly Vector2[] Arrow =
    {
        new(0, 0), new(0, 26), new(7, 20), new(12, 31), new(17, 29), new(12, 18), new(21, 18),
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
        TopLevel = true;
    }

    public override void _Process(double delta)
    {
        bool show = Input.MouseMode != Input.MouseModeEnum.Captured;
        Visible = show;
        if (show) Position = GetViewport().GetMousePosition();
    }

    public override void _Draw()
    {
        var outline = new Vector2[Arrow.Length + 1];
        for (int i = 0; i < Arrow.Length; i++) outline[i] = Arrow[i];
        outline[^1] = Arrow[0];
        DrawColoredPolygon(Arrow, Colors.White);
        DrawPolyline(outline, new Color(0.08f, 0.1f, 0.15f), 2.5f, true);
    }
}
