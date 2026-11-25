using Godot;

namespace AB;

/// <summary>Ícone de habilidade num Control (seleção e Informações).</summary>
public partial class AbilityIconView : Control
{
    public AbIcon Icon;
    public Color Col = Colors.White;
    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Draw() => AbilityIcons.Draw(this, Icon, Size / 2f, Mathf.Min(Size.X, Size.Y) / 2f, Col);
}
