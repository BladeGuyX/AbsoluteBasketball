using Godot;

namespace AB;

/// <summary>
/// Efeitos de impacto (v0.21; v0.22 com shader próprio <c>Shaders/impact_fx.gdshader</c>). Fica na partida e escuta
/// <see cref="Sfx.Played"/> — as deixas sem som chegam também ao cliente online, então todo mundo vê o efeito:
/// - "fx_dunk" (enterrada certa): brilho curto, onda no aro + eco, onda no chão, faíscas finas e tremor de câmera
///   (Tuning "Efeito de impacto" e "Tremor da câmera na enterrada");
/// - "fx_parry" (tentou roubar de quem está driblando): anelzinho azulado e umas faíscas na bola.
/// <see cref="Dust"/> = poeira ao pousar (chamada pelo visual do personagem).
/// </summary>
public partial class ImpactFx : Node3D
{
    public Match M;
    static Shader _shader;
    static Shader FxShader => _shader ??= GD.Load<Shader>("res://Shaders/impact_fx.gdshader");

    public override void _EnterTree() => Sfx.Played += OnSound;
    public override void _ExitTree() => Sfx.Played -= OnSound;

    void OnSound(string name, Vector3 pos, float db)
    {
        if (!IsInsideTree()) return;
        if (name == "fx_parry") { Parry(this, pos); return; }
        if (name != "fx_dunk") return;
        var t = Tuning.I;
        if (t.DunkImpactFx > 0.001f) Dunk(this, pos, t.DunkImpactFx);
        if (M?.Cam != null && t.DunkCameraShake > 0)
        {
            float d = M.Cam.Cam.GlobalPosition.DistanceTo(pos);
            M.Cam.Shake(t.DunkCameraShake * (1f - Mathf.Clamp((d - 6f) / 22f, 0, 1)));
        }
    }

    static float EaseOut(float k) => 1f - Mathf.Pow(1f - Mathf.Clamp(k, 0, 1), 3f);

    /// <summary>Uma peça do shader (onda ou brilho) animada no tempo.</summary>
    static void Piece(FxNode root, Vector3 local, bool billboard, int mode, float radius, float start, float dur, Color color,
        float width = 0.07f, float trail = 0.3f, float breakup = 0.3f, float from = 0f)
    {
        var mat = new ShaderMaterial { Shader = FxShader };
        mat.SetShaderParameter("mode", mode);
        mat.SetShaderParameter("billboard", billboard);
        mat.SetShaderParameter("color", color);
        mat.SetShaderParameter("width", width);
        mat.SetShaderParameter("trail", trail);
        mat.SetShaderParameter("breakup", breakup);
        mat.SetShaderParameter("seed", (float)GD.RandRange(0.0, 50.0));
        Mesh mesh = billboard ? new QuadMesh { Size = Vector2.One * radius * 2f } : new PlaneMesh { Size = Vector2.One * radius * 2f };
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Position = local, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(mi);
        root.Add(t =>
        {
            float k = (t - start) / Mathf.Max(dur, 0.01f);
            mi.Visible = k >= 0f && k < 1f;
            if (mi.Visible) mat.SetShaderParameter("progress", mode == 1 ? k : Mathf.Lerp(from, 1f, EaseOut(k)));
        });
    }

    static CpuParticles3D Sparks(int amount, float vMin, float vMax, float life, float gravity, Color c0, Color c1, Vector3 size)
    {
        var p = new CpuParticles3D
        {
            Emitting = true, OneShot = true, Amount = Mathf.Clamp(amount, 2, 200), Lifetime = life, Explosiveness = 1f,
            Direction = Vector3.Up, Spread = 180f, InitialVelocityMin = vMin, InitialVelocityMax = vMax,
            Gravity = new Vector3(0, gravity, 0), DampingMin = 1.5f, DampingMax = 3f, ScaleAmountMin = 0.6f, ScaleAmountMax = 1.2f,
            Mesh = new BoxMesh { Size = size }, ParticleFlagAlignY = true, // risquinhos alinhados com a velocidade
        };
        var ramp = new Gradient();
        ramp.SetColor(0, c0);
        ramp.SetColor(1, c1);
        p.ColorRamp = ramp;
        var m = Mat.Unshaded(Colors.White, true);
        m.VertexColorUseAsAlbedo = true;
        m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        p.MaterialOverride = m;
        return p;
    }

    /// <summary>Enterrada certa (pos = centro do aro).</summary>
    public static void Dunk(Node parent, Vector3 pos, float s)
    {
        var root = new FxNode { Life = 0.8f };
        parent.AddChild(root);
        root.GlobalPosition = pos;
        var warm = new Color(1f, 0.72f, 0.42f, 0.9f);
        Piece(root, Vector3.Zero, true, 1, 0.75f * s, 0f, 0.16f, new Color(1f, 0.86f, 0.62f, 0.8f));                 // brilho
        Piece(root, Vector3.Zero, false, 0, 2.0f * s, 0f, 0.42f, warm, 0.07f, 0.3f, 0.3f, 0.25f);                       // onda no aro
        Piece(root, Vector3.Up * 0.02f, false, 0, 1.5f * s, 0.07f, 0.36f, new Color(1f, 0.95f, 0.85f, 0.55f), 0.035f, 0.15f, 0.5f, 0.3f); // eco
        Piece(root, new Vector3(0, 0.04f - pos.Y, 0), false, 0, 2.6f * s, 0.05f, 0.55f, new Color(1f, 0.93f, 0.8f, 0.5f), 0.05f, 0.25f, 0.45f); // chão
        root.AddChild(Sparks((int)(18 * s), 3.5f * s, 7f * s, 0.45f, -12f, new Color(1f, 0.78f, 0.35f), new Color(1f, 0.38f, 0.1f, 0f),
            new Vector3(0.018f, 0.14f, 0.018f)));
        Dust(parent, pos with { Y = 0f }, 0.6f * s);
    }

    /// <summary>Roubo barrado por quem está driblando (pos = bola): efeito leve.</summary>
    public static void Parry(Node parent, Vector3 pos)
    {
        var root = new FxNode { Life = 0.4f };
        parent.AddChild(root);
        root.GlobalPosition = pos;
        Piece(root, Vector3.Zero, true, 1, 0.35f, 0f, 0.1f, new Color(0.85f, 0.95f, 1f, 0.55f));
        Piece(root, Vector3.Zero, true, 0, 0.55f, 0f, 0.24f, new Color(0.65f, 0.85f, 1f, 0.85f), 0.1f, 0.25f, 0.2f, 0.35f);
        root.AddChild(Sparks(7, 2f, 3.5f, 0.25f, -4f, new Color(0.9f, 0.97f, 1f), new Color(0.5f, 0.75f, 1f, 0f), new Vector3(0.012f, 0.08f, 0.012f)));
    }

    /// <summary>Poeirinha no chão ao pousar (s = tamanho; 1 = depois de enterrar).</summary>
    public static void Dust(Node parent, Vector3 feet, float s)
    {
        if (parent == null || !parent.IsInsideTree() || s <= 0.01f) return;
        var root = new FxNode { Life = 0.6f };
        parent.AddChild(root);
        root.GlobalPosition = feet with { Y = 0.03f };
        Piece(root, Vector3.Zero, false, 0, 1.1f * s, 0f, 0.45f, new Color(1f, 0.95f, 0.85f, 0.35f), 0.12f, 0.5f, 0.5f, 0.2f);
        var puffs = new CpuParticles3D
        {
            Emitting = true, OneShot = true, Amount = Mathf.Clamp((int)(12 * s), 3, 40), Lifetime = 0.5f, Explosiveness = 1f,
            Direction = Vector3.Up, Spread = 80f, InitialVelocityMin = 1.2f * s, InitialVelocityMax = 2.4f * s,
            Gravity = new Vector3(0, -2f, 0), DampingMin = 3f, DampingMax = 5f, ScaleAmountMin = 0.7f, ScaleAmountMax = 1.3f,
            Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f, RadialSegments = 6, Rings = 3 },
        };
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 0.97f, 0.9f, 0.6f));
        ramp.SetColor(1, new Color(1f, 0.97f, 0.9f, 0f));
        puffs.ColorRamp = ramp;
        var pm = Mat.Unshaded(Colors.White, true);
        pm.VertexColorUseAsAlbedo = true;
        puffs.MaterialOverride = pm;
        root.AddChild(puffs);
    }
}
