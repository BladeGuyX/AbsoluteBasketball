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
    static readonly RandomNumberGenerator Rng = new();
    static Shader FxShader => _shader ??= GD.Load<Shader>("res://Shaders/impact_fx.gdshader");
    static Shader _mixShader;
    /// <summary>Mesma coisa com mistura normal (não aditiva): aparece também em fundo claro (quadra, areia).</summary>
    static Shader MixShader => _mixShader ??= new Shader { Code = FxShader.Code.Replace("blend_add", "blend_mix") };

    public override void _EnterTree() => Sfx.Played += OnSound;
    public override void _ExitTree() => Sfx.Played -= OnSound;

    void OnSound(string name, Vector3 pos, float db)
    {
        if (!IsInsideTree()) return;
        if (name == "fx_parry")
        {
            Parry(this, pos);
            Sfx.PlayLocal3D(this, "parry", pos, -9f); // som só nesta tela (a deixa já chega a todo mundo)
            return;
        }
        if (name.StartsWith("fx_") && name != "fx_dunk") { Ability(name, pos); return; }
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
        float width = 0.07f, float trail = 0.3f, float breakup = 0.3f, float from = 0f, bool mix = false)
    {
        var mat = new ShaderMaterial { Shader = mix ? MixShader : FxShader };
        mat.SetShaderParameter("mode", mode);
        mat.SetShaderParameter("billboard", billboard);
        mat.SetShaderParameter("color", color);
        mat.SetShaderParameter("width", width);
        mat.SetShaderParameter("trail", trail);
        mat.SetShaderParameter("breakup", breakup);
        mat.SetShaderParameter("seed", Rng.RandfRange(0f, 50f)); // gerador próprio: efeito visual não mexe no sorteio do jogo
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

    static CpuParticles3D Sparks(int amount, float vMin, float vMax, float life, float gravity, Color c0, Color c1, Vector3 size, bool add = true)
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
        if (add) m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
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
        // v0.23: mais visível (mistura normal, maior e mais demorado), mas ainda rápido: clarão + anel + risquinhos
        var root = new FxNode { Life = 0.5f };
        parent.AddChild(root);
        root.GlobalPosition = pos;
        Piece(root, Vector3.Zero, true, 1, 0.5f, 0f, 0.14f, new Color(1f, 1f, 1f, 0.9f), mix: true);
        Piece(root, Vector3.Zero, true, 0, 0.9f, 0f, 0.36f, new Color(0.35f, 0.68f, 1f, 1f), 0.14f, 0.2f, 0.12f, 0.25f, mix: true);
        Piece(root, Vector3.Zero, true, 0, 0.62f, 0.05f, 0.26f, new Color(1f, 1f, 1f, 0.95f), 0.08f, 0f, 0.3f, 0.3f, mix: true);
        root.AddChild(Sparks(10, 2.5f, 4.5f, 0.3f, -6f, new Color(1f, 1f, 1f), new Color(0.55f, 0.8f, 1f, 0f), new Vector3(0.02f, 0.12f, 0.02f), add: false));
    }

    // ------------------------------------------------------------------ v0.26: habilidades

    static readonly Color Red = new(1f, 0.18f, 0.15f), Green = new(0.3f, 1f, 0.45f), Cyan = new(0.45f, 0.92f, 1f), Gold = new(1f, 0.85f, 0.3f),
        Silver = new(0.85f, 0.9f, 0.97f);

    void Ability(string name, Vector3 pos)
    {
        switch (name)
        {
            case "fx_eye": Burst(pos, Red, 0.5f, 10); Sfx.PlayLocal3D(this, "aura", pos, -8f, 1.6f); break;
            case "fx_presence": Ring(pos with { Y = 0.05f }, Red, 7f, 0.6f); Burst(pos, Red, 0.9f, 22); Sfx.PlayLocal3D(this, "aura", pos, -4f, 0.8f); break;
            case "fx_perfect": Burst(pos, Red, 0.6f, 14); Sfx.PlayLocal3D(this, "aura", pos, -6f, 1.3f); break;
            case "fx_jinx": Burst(pos, Red, 0.5f, 12); Sfx.PlayLocal3D(this, "fail", pos, -8f, 1.2f); break;
            case "fx_dice": Burst(pos, Green, 0.6f, 14); Sfx.PlayLocal3D(this, "dice", pos, -4f); break;
            case "fx_dash": Dust(this, pos with { Y = 0 }, 0.6f); Burst(pos, Green, 0.45f, 8); Sfx.PlayLocal3D(this, "dash", pos, -3f); break;
            case "fx_trip": Dust(this, pos with { Y = 0 }, 0.8f); Sfx.PlayLocal3D(this, "fail", pos, -5f); break;
            case "fx_bet":
                // v0.27: só o time do Luck vê quem foi marcado (o alvo é o jogador logo abaixo da deixa)
                if (BetHiddenHere(pos)) break;
                Ring(pos, Green, 1.2f, 0.5f, true); Sfx.PlayLocal3D(this, "chime", pos, -8f, 0.8f); break;
            case "fx_betwin":
                // v0.27: a cesta do alvo foi para o time do Luck — efeito grande no aro, para todo mundo
                Ring(pos, Gold, 3.2f, 0.7f, true); Ring(pos, Green, 2.2f, 0.55f, true);
                Burst(pos, Green, 1.6f, 40); Burst(pos, Gold, 1.3f, 30);
                Ring(pos with { Y = 0.05f }, Green, 6f, 0.7f);
                Sfx.PlayLocal3D(this, "chime", pos, 0f, 1.25f); Sfx.PlayLocal3D(this, "dice", pos, -2f);
                break;
            case "fx_boltA": _boltA = pos; _boltAt = Time.GetTicksMsec(); break;
            case "fx_boltB":
                // v0.27: rastro reto do Drible Relâmpago (de onde saiu até onde chegou)
                if (Time.GetTicksMsec() - _boltAt < 1500) BoltLine(_boltA, pos, 0.35f);
                break;
            case "fx_luckon": Ring(pos with { Y = 0.05f }, Green, 3f, 0.6f); Burst(pos, Green, 1.1f, 30); Sfx.PlayLocal3D(this, "chime", pos, -3f); break;
            case "fx_luckwin": Ring(pos, Gold, 2.2f, 0.5f, true); Burst(pos, Gold, 1.2f, 30); Burst(pos, Green, 0.9f, 18); Sfx.PlayLocal3D(this, "chime", pos, -2f, 1.25f); break;
            case "fx_lucklose": Burst(pos, new Color(0.6f, 0.6f, 0.6f), 0.7f, 16); Sfx.PlayLocal3D(this, "fail", pos, -3f, 0.8f); break;
            case "fx_bolt": Burst(pos, Cyan, 0.7f, 16); Sfx.PlayLocal3D(this, "zap", pos, -4f); break;
            case "fx_swap": Ring(pos, Cyan, 1.4f, 0.4f, true); Burst(pos, Cyan, 0.8f, 18); Bolt(pos + Vector3.Up * 4f, pos, 0.35f); Sfx.PlayLocal3D(this, "zap", pos, -3f, 1.2f); break;
            case "fx_betx2": Ring(pos, Gold, 1.4f, 0.5f, true); Burst(pos, Gold, 0.9f, 20); Sfx.PlayLocal3D(this, "chime", pos, -6f, 0.7f); break; // v0.38
            // ---- v0.37: Iron ----
            case "fx_wall": Burst(pos, Silver, 1.4f, 30); Sfx.PlayLocal3D(this, "wall", pos, -1f); break;
            case "fx_bonk":
                Ring(pos, Silver, 0.9f, 0.3f, true); Burst(pos, Gold, 0.6f, 14);
                Sfx.PlayLocal3D(this, "bonk", pos, -1f); Sfx.PlayLocal3D(this, "clang", pos, -10f, 1.5f);
                break;
            case "fx_charge": Dust(this, pos with { Y = 0 }, 0.8f); Sfx.PlayLocal3D(this, "charge", pos, -2f); break;
            case "fx_ram": Burst(pos, Silver, 0.8f, 16); Dust(this, pos with { Y = 0 }, 0.6f); Sfx.PlayLocal3D(this, "cut_hit", pos, -2f, 1.1f); break;
            case "fx_clang":
                Burst(pos, Gold, 0.7f, 18); Ring(pos, Silver, 1.0f, 0.3f, true);
                Sfx.PlayLocal3D(this, "clang", pos, -3f);
                if (M?.Cam != null) M.Cam.Shake(0.25f * (1f - Mathf.Clamp((M.Cam.Cam.GlobalPosition.DistanceTo(pos) - 4f) / 20f, 0, 1)));
                break;
            case "fx_ironon": Ring(pos with { Y = 0.05f }, Silver, 3f, 0.6f); Burst(pos, Silver, 1.2f, 30); Burst(pos, Gold, 0.8f, 14); Sfx.PlayLocal3D(this, "clang", pos, -4f, 0.8f); break;
            case "fx_thunder":
                Bolt(pos + Vector3.Up * 22f, pos, 0.5f);
                Ring(pos, Cyan, 6f, 0.5f);
                Burst(pos + Vector3.Up * 0.5f, Cyan, 1.4f, 30);
                Dust(this, pos, 1f);
                Sfx.PlayLocal3D(this, "thunder", pos, 0f);
                if (M?.Cam != null) M.Cam.Shake(0.7f * (1f - Mathf.Clamp((M.Cam.Cam.GlobalPosition.DistanceTo(pos) - 6f) / 25f, 0, 1)));
                break;
        }
    }

    Vector3 _boltA;
    ulong _boltAt;

    bool BetHiddenHere(Vector3 pos)
    {
        var lp = M?.LocalPlayer;
        if (lp == null) return false;
        Player best = null; float bd = 2.5f;
        foreach (var q in M.Players)
        {
            float d = ((q.GlobalPosition - pos) with { Y = 0 }).Length();
            if (d < bd) { bd = d; best = q; }
        }
        return best != null && best.Team == lp.Team;
    }

    /// <summary>Raio horizontal (Drible Relâmpago): fino, some rápido.</summary>
    void BoltLine(Vector3 from, Vector3 to, float dur)
    {
        var root = new FxNode { Life = dur };
        AddChild(root);
        root.GlobalPosition = to;
        var m = Mat.Unshaded(new Color(0.55f, 0.9f, 1f, 1f), true);
        m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        var core = Mat.Unshaded(new Color(0.9f, 0.98f, 1f), true);
        core.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        var dir = to - from;
        int segs = Mathf.Clamp((int)(dir.Length() / 0.7f), 4, 14);
        var prev = from - to;
        for (int i = 1; i <= segs; i++)
        {
            float k = i / (float)segs;
            var p = (from - to) + dir * k;
            if (i < segs) p += new Vector3(Rng.RandfRange(-1, 1), Rng.RandfRange(-0.6f, 0.6f), Rng.RandfRange(-1, 1)) * 0.22f;
            var seg = p - prev;
            float len = seg.Length();
            if (len < 0.01f) { prev = p; continue; }
            foreach (var (mat, w) in new[] { (m, 0.1f), (core, 0.035f) })
            {
                var mi = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(w, len, w) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                root.AddChild(mi);
                mi.Position = (prev + p) * 0.5f;
                var up = seg / len;
                var side = up.Cross(Vector3.Up);
                if (side.LengthSquared() < 1e-4f) side = up.Cross(Vector3.Right);
                side = side.Normalized();
                mi.Basis = new Basis(side, up, side.Cross(up).Normalized());
                float k0 = k;
                root.Add(t => mi.Visible = t < dur * (0.25f + 0.75f * (1f - k0 * 0.5f)) && (t > 0.08f || Mathf.Sin(t * 60f) > -0.3f));
            }
            prev = p;
        }
    }

    void Burst(Vector3 pos, Color c, float s, int n)
    {
        var root = new FxNode { Life = 0.6f };
        AddChild(root);
        root.GlobalPosition = pos;
        Piece(root, Vector3.Zero, true, 1, 0.6f * s, 0f, 0.15f, c with { A = 0.8f });
        root.AddChild(Sparks(n, 2f * s, 5f * s, 0.45f, -5f, c.Lightened(0.3f), c with { A = 0f }, new Vector3(0.02f, 0.12f, 0.02f)));
    }

    void Ring(Vector3 pos, Color c, float radius, float dur, bool billboard = false)
    {
        var root = new FxNode { Life = dur + 0.1f };
        AddChild(root);
        root.GlobalPosition = pos;
        Piece(root, Vector3.Zero, billboard, 0, radius, 0f, dur, c with { A = 0.85f }, 0.06f, 0.3f, 0.25f, 0.1f, mix: !billboard);
    }

    /// <summary>Raio em zigue-zague de <paramref name="from"/> até <paramref name="to"/> (some em <paramref name="dur"/> s).</summary>
    void Bolt(Vector3 from, Vector3 to, float dur)
    {
        var root = new FxNode { Life = dur };
        AddChild(root);
        root.GlobalPosition = to;
        var m = Mat.Unshaded(new Color(0.75f, 0.95f, 1f, 1f), true);
        m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        var core = Mat.Unshaded(Colors.White, true);
        core.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        int segs = 9;
        var prev = from - to;
        var dir = (to - from);
        for (int i = 1; i <= segs; i++)
        {
            float k = i / (float)segs;
            var p = (from - to) + dir * k;
            if (i < segs) p += new Vector3(Rng.RandfRange(-1, 1), 0, Rng.RandfRange(-1, 1)) * (dir.Length() / segs * 0.45f);
            var seg = p - prev;
            float len = seg.Length();
            if (len < 0.01f) { prev = p; continue; }
            foreach (var (mat, w) in new[] { (m, 0.16f), (core, 0.06f) })
            {
                var mi = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(w, len, w) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                root.AddChild(mi);
                mi.Position = (prev + p) * 0.5f;
                var up = seg / len;
                var side = up.Cross(Vector3.Forward);
                if (side.LengthSquared() < 1e-4f) side = up.Cross(Vector3.Right);
                side = side.Normalized();
                mi.Basis = new Basis(side, up, side.Cross(up).Normalized());
                root.Add(t => mi.Visible = t < dur * (0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 40f))));
            }
            prev = p;
        }
        Piece(root, Vector3.Up * 0.3f, true, 1, 1.6f, 0f, 0.18f, new Color(0.8f, 0.97f, 1f, 0.9f));
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
