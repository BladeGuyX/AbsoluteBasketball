using Godot;

namespace AB;

/// <summary>
/// v0.21: efeitos de impacto. Fica na partida e escuta <see cref="Sfx.Played"/> — a deixa "fx_dunk" (enterrada certa)
/// também chega no cliente online, então o efeito aparece para todo mundo sem mensagem nova.
/// Enterrada: clarão, duas ondas (no aro e no chão), faíscas e tremor de câmera (Tuning "Efeito de impacto" e
/// "Tremor da câmera na enterrada"). Pouso: poeirinha (<see cref="Dust"/>, chamada pelo visual do personagem).
/// </summary>
public partial class ImpactFx : Node3D
{
    public Match M;

    public override void _EnterTree() => Sfx.Played += OnSound;
    public override void _ExitTree() => Sfx.Played -= OnSound;

    void OnSound(string name, Vector3 pos, float db)
    {
        if (name != "fx_dunk" || !IsInsideTree()) return;
        var t = Tuning.I;
        float s = t.DunkImpactFx;
        if (s > 0.001f) Dunk(this, pos, s);
        // tremor da câmera: forte perto, some longe
        if (M?.Cam != null && t.DunkCameraShake > 0)
        {
            float d = M.Cam.Cam.GlobalPosition.DistanceTo(pos);
            float k = 1f - Mathf.Clamp((d - 6f) / 22f, 0, 1);
            M.Cam.Shake(t.DunkCameraShake * k);
        }
    }

    static StandardMaterial3D Glow(Color c)
    {
        var m = Mat.Unshaded(c, true);
        m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        m.NoDepthTest = false;
        m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        return m;
    }

    /// <summary>Efeito da enterrada no ponto (centro do aro).</summary>
    public static void Dunk(Node parent, Vector3 pos, float s)
    {
        var root = new FxNode { Life = 0.9f };
        parent.AddChild(root);
        root.GlobalPosition = pos;

        var flash = Mat.MeshNode(new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 16, Rings = 8 }, Glow(new Color(1f, 0.75f, 0.35f)), Vector3.Zero, false);
        root.AddChild(flash);
        root.Anim(flash, 0f, 0.22f, 0.4f * s, 2.4f * s, 1f);

        var ring = Mat.MeshNode(new TorusMesh { InnerRadius = 0.9f, OuterRadius = 1f, Rings = 40, RingSegments = 6 }, Glow(new Color(1f, 0.55f, 0.2f)), Vector3.Zero, false);
        root.AddChild(ring);
        root.Anim(ring, 0f, 0.4f, 0.6f * s, 3.2f * s, 0.12f);

        // onda no chão (embaixo do aro), um pouquinho depois
        var floor = Mat.MeshNode(new TorusMesh { InnerRadius = 0.85f, OuterRadius = 1f, Rings = 40, RingSegments = 6 }, Glow(new Color(1f, 0.85f, 0.6f, 0.8f)), new Vector3(0, 0.05f - pos.Y, 0), false);
        root.AddChild(floor);
        root.Anim(floor, 0.06f, 0.5f, 0.5f * s, 4.5f * s, 0.06f);

        var sparks = new CpuParticles3D
        {
            Emitting = true, OneShot = true, Amount = Mathf.Clamp((int)(46 * s), 4, 200), Lifetime = 0.7f, Explosiveness = 1f,
            Direction = Vector3.Up, Spread = 180f, InitialVelocityMin = 5f * s, InitialVelocityMax = 11f * s,
            Gravity = new Vector3(0, -16f, 0), ScaleAmountMin = 0.6f, ScaleAmountMax = 1.4f, DampingMin = 2f, DampingMax = 4f,
            Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.18f) },
            ParticleFlagAlignY = false,
        };
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 0.95f, 0.6f));
        ramp.SetColor(1, new Color(1f, 0.35f, 0.1f, 0f));
        sparks.ColorRamp = ramp;
        var sm = Glow(Colors.White);
        sm.VertexColorUseAsAlbedo = true;
        sparks.MaterialOverride = sm;
        root.AddChild(sparks);
    }

    /// <summary>Poeirinha no chão ao pousar (s = tamanho; 1 = depois de enterrar).</summary>
    public static void Dust(Node parent, Vector3 feet, float s)
    {
        if (parent == null || !parent.IsInsideTree() || s <= 0.01f) return;
        var root = new FxNode { Life = 0.6f };
        parent.AddChild(root);
        root.GlobalPosition = feet with { Y = 0.03f };
        var mat = Mat.Unshaded(new Color(1f, 0.95f, 0.85f, 0.55f), true);
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var ring = Mat.MeshNode(new TorusMesh { InnerRadius = 0.7f, OuterRadius = 1f, Rings = 24, RingSegments = 4 }, mat, Vector3.Zero, false);
        root.AddChild(ring);
        root.Anim(ring, 0f, 0.45f, 0.3f * s, 1.5f * s, 0.08f);
        var puffs = new CpuParticles3D
        {
            Emitting = true, OneShot = true, Amount = Mathf.Clamp((int)(14 * s), 3, 40), Lifetime = 0.5f, Explosiveness = 1f,
            Direction = Vector3.Up, Spread = 80f, InitialVelocityMin = 1.2f * s, InitialVelocityMax = 2.6f * s,
            Gravity = new Vector3(0, -2f, 0), DampingMin = 3f, DampingMax = 5f, ScaleAmountMin = 0.7f, ScaleAmountMax = 1.3f,
            Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f, RadialSegments = 6, Rings = 3 },
        };
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 0.97f, 0.9f, 0.7f));
        ramp.SetColor(1, new Color(1f, 0.97f, 0.9f, 0f));
        puffs.ColorRamp = ramp;
        var pm = Mat.Unshaded(Colors.White, true);
        pm.VertexColorUseAsAlbedo = true;
        puffs.MaterialOverride = pm;
        root.AddChild(puffs);
    }
}
