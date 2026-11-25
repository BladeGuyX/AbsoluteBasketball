using Godot;

namespace AB;

/// <summary>Nó de efeito: anima escala/transparência das peças e se apaga sozinho.</summary>
public partial class FxNode : Node3D
{
    public float Life = 1f;
    float _t;
    readonly System.Collections.Generic.List<(GeometryInstance3D N, float Start, float Dur, float S0, float S1, float YScale)> _items = new();

    public void Anim(GeometryInstance3D n, float start, float dur, float s0, float s1, float yScale)
    {
        _items.Add((n, start, dur, s0, s1, yScale));
        n.Visible = start <= 0;
        n.Scale = new Vector3(s0, s0 * yScale, s0);
    }

    public override void _Ready() => PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        foreach (var (n, start, dur, s0, s1, ys) in _items)
        {
            float k = Mathf.Clamp((_t - start) / Mathf.Max(dur, 0.01f), 0, 1);
            n.Visible = _t >= start && k < 1f;
            float e = 1f - (1f - k) * (1f - k) * (1f - k); // sai rápido e desacelera
            float sc = Mathf.Lerp(s0, s1, e);
            n.Scale = new Vector3(sc, sc * ys, sc);
            n.Transparency = Mathf.Clamp(k * k, 0, 1);
        }
        if (_t >= Life) QueueFree();
    }
}
