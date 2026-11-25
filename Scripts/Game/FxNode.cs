using System;
using System.Collections.Generic;
using Godot;

namespace AB;

/// <summary>Nó de efeito: roda as animações das peças (funções do tempo) e se apaga sozinho depois de <see cref="Life"/>.</summary>
public partial class FxNode : Node3D
{
    public float Life = 1f;
    float _t;
    readonly List<Action<float>> _updates = new();

    /// <summary>Registra uma animação (recebe o tempo desde o início do efeito).</summary>
    public void Add(Action<float> update)
    {
        _updates.Add(update);
        update(0f);
    }

    public override void _Ready() => PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        foreach (var u in _updates) u(_t);
        if (_t >= Life) QueueFree();
    }
}
