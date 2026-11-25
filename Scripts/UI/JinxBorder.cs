using Godot;

namespace AB;

/// <summary>v0.26: borda da tela vermelha por um instante quando o seu arremesso foi estragado pela Presença Implacável.</summary>
public partial class JinxBorder : Control
{
    public Match M;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var p = M?.LocalPlayer;
        if (p?.Ab == null || p.Ab.JinxFlash <= 0) return;
        float a = Mathf.Clamp(p.Ab.JinxFlash / Mathf.Max(Tuning.I.KingPresenceFlash, 0.05f), 0, 1);
        var sz = Size;
        float w = Mathf.Min(sz.X, sz.Y) * 0.07f;
        for (int i = 0; i < 8; i++)
        {
            float k = i / 8f;
            var col = new Color(0.9f, 0.05f, 0.05f, 0.5f * a * (1f - k));
            float o = w * k;
            DrawRect(new Rect2(o, o, sz.X - 2 * o, sz.Y - 2 * o), col, false, w / 8f + 1f);
        }
    }
}
