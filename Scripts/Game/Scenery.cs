using System.Collections.Generic;
using Godot;

namespace AB;

/// <summary>
/// v0.32: cenário em volta da quadra de praia com os modelos do Blender (art_source/scripts/make_scenery.py):
/// arquibancadas nas laterais (assentos da cor do time de cada lado), postes de luz nos cantos, palmeiras, moitas e pedras
/// em volta, guarda-sóis + espreguiçadeiras + pranchas atrás das cestas, torre de salva-vidas perto do mar, o mar com
/// ondinhas e espuma num dos fundos e areia com variação de cor. Tudo com o toon do jogo. Sorteios com semente fixa
/// (todo mundo vê o mesmo cenário). Sem os .glb, <see cref="CourtBuilder"/> usa a decoração antiga.
/// </summary>
public static class Scenery
{
    static readonly Dictionary<string, PackedScene> Cache = new();
    const string Dir = "res://assets/models/";

    public static bool Available => ResourceLoader.Exists(Dir + "palmeira_a.glb") && ResourceLoader.Exists(Dir + "arquibancada.glb");

    static PackedScene Load(string name)
    {
        if (Cache.TryGetValue(name, out var s)) return s;
        s = ResourceLoader.Exists(Dir + name + ".glb") ? GD.Load<PackedScene>(Dir + name + ".glb") : null;
        Cache[name] = s;
        return s;
    }

    /// <summary>Instancia um modelo no chão, girado (yaw, rad) e em escala; materiais viram toon (Assento = tint).</summary>
    static Node3D Prop(Node3D root, string name, Vector3 pos, float yaw, float scale = 1f, Color? seat = null, float outline = 0.02f)
    {
        var sc = Load(name);
        if (sc == null) return null;
        var n = sc.Instantiate<Node3D>();
        n.Position = pos;
        n.Rotation = new Vector3(0, yaw, 0);
        n.Scale = Vector3.One * scale;
        foreach (var child in n.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)child;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                var src = mi.Mesh.SurfaceGetMaterial(i);
                var color = src is BaseMaterial3D b ? b.AlbedoColor : Colors.White;
                string mn = src?.ResourceName ?? "";
                Material m = mn switch
                {
                    "Lampada" => Mat.Unshaded(new Color(1f, 0.97f, 0.85f)),
                    "Assento" when seat.HasValue => Mat.ToonOutlined(seat.Value, outline),
                    "Folha" or "FolhaEscura" => Mat.ToonOutlined(color, outline * 0.6f),
                    _ => outline > 0 ? Mat.ToonOutlined(color, outline) : Mat.Toon(color),
                };
                mi.SetSurfaceOverrideMaterial(i, m);
            }
        }
        root.AddChild(n);
        return n;
    }

    static float Face(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.X, d.Z); } // +Z do modelo para 'to'

    public static void Build(Node3D root, CourtGeo g)
    {
        var rng = new RandomNumberGenerator { Seed = 20261126 };
        float ground = -Court.SlabDepth;
        var deco = new Node3D { Name = "Scenery" };
        root.AddChild(deco);
        V(deco, rng, g, ground);
    }

    static void V(Node3D root, RandomNumberGenerator rng, CourtGeo g, float y)
    {
        float edgeZ = g.MaxZ + 1.4f, edgeX = g.MaxX + 1.4f;

        // ---- arquibancadas nas laterais (frente para a quadra); assentos: −Z azul, +Z laranja ----
        float modW = 8.4f;
        int mods = Mathf.Max(1, Mathf.FloorToInt(g.Length * 0.8f / modW));
        for (int side = -1; side <= 1; side += 2)
        {
            var seat = Palette.Team[side < 0 ? 0 : 1].Darkened(0.12f);
            for (int i = 0; i < mods; i++)
            {
                float x = (i - (mods - 1) / 2f) * modW;
                Prop(root, "arquibancada", new Vector3(x, y, side * edgeZ), side < 0 ? 0f : Mathf.Pi, 1f, seat);
            }
        }

        // ---- postes de luz nos 4 cantos, virados para o centro ----
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sz = -1; sz <= 1; sz += 2)
        {
            var p = new Vector3(sx * (g.MaxX + 2.5f), y, sz * (g.MaxZ + 2.5f));
            Prop(root, "poste_luz", p, Face(p, new Vector3(0, y, 0)));
        }

        // ---- mar no fundo +X: faixa de espuma na beira e ondinhas ----
        float shore = edgeX + 30f;
        Ocean(root, shore, y);

        // ---- praia atrás das cestas: guarda-sóis com espreguiçadeiras, pranchas, torre de salva-vidas ----
        for (int end = -1; end <= 1; end += 2)
        {
            float baseX = end * (edgeX + 7f);
            int groups = Mathf.Clamp(Mathf.FloorToInt(g.Width / 7f), 2, 5);
            for (int i = 0; i < groups; i++)
            {
                float z = (i - (groups - 1) / 2f) * 7.5f + rng.RandfRange(-1.2f, 1.2f);
                var c = new Vector3(baseX + end * rng.RandfRange(0, 6f), y, z);
                Prop(root, "guarda_sol", c, rng.RandfRange(0, Mathf.Tau), rng.RandfRange(0.95f, 1.1f));
                for (int k = -1; k <= 1; k += 2)
                {
                    var lc = c + new Vector3(end * 0.6f, 0, k * 0.85f);
                    // a pessoa olha para o lado oposto da quadra (para o mar / fundo)
                    Prop(root, "espreguicadeira", lc, Face(lc, lc + new Vector3(end, 0, 0)) + rng.RandfRange(-0.15f, 0.15f));
                }
                if (rng.Randf() < 0.6f)
                    Prop(root, "prancha", c + new Vector3(end * rng.RandfRange(2.2f, 3.2f), 0, rng.RandfRange(-2f, 2f)), rng.RandfRange(0, Mathf.Tau));
            }
        }
        var tower = new Vector3(shore - 6f, y, g.MaxZ * 0.4f);
        Prop(root, "torre_salva_vidas", tower, Face(tower, tower + Vector3.Right));

        // ---- palmeiras, moitas e pedras em volta (fora da área ocupada) ----
        int placed = 0, tries = 0;
        var used = new List<Vector3>();
        while (placed < 42 && tries++ < 600)
        {
            float ang = rng.RandfRange(0, Mathf.Tau);
            float r = rng.RandfRange(1.0f, 1.9f);
            var p = new Vector3(Mathf.Cos(ang) * (g.MaxX + 14f) * r * 0.75f, y, Mathf.Sin(ang) * (g.MaxZ + 10f) * r);
            if (Mathf.Abs(p.X) < edgeX + 14f && Mathf.Abs(p.Z) < edgeZ + 6.5f) continue; // arquibancada/praia
            if (p.X > shore - 4f) continue;                                             // não no mar
            bool near = false;
            foreach (var u in used) if (u.DistanceTo(p) < 4.5f) { near = true; break; }
            if (near) continue;
            used.Add(p);
            placed++;
            string kind = rng.Randf() < 0.55f ? "palmeira_a" : "palmeira_b";
            Prop(root, kind, p, rng.RandfRange(0, Mathf.Tau), rng.RandfRange(0.85f, 1.2f), outline: 0.025f);
            if (rng.Randf() < 0.5f) Prop(root, "arbusto", p + new Vector3(rng.RandfRange(-2f, 2f), 0, rng.RandfRange(-2f, 2f)), rng.RandfRange(0, Mathf.Tau), rng.RandfRange(0.7f, 1.3f));
            if (rng.Randf() < 0.3f) Prop(root, rng.Randf() < 0.5f ? "pedra_a" : "pedra_b", p + new Vector3(rng.RandfRange(-2.5f, 2.5f), 0, rng.RandfRange(-2.5f, 2.5f)), rng.RandfRange(0, Mathf.Tau), rng.RandfRange(0.6f, 1.4f));
        }
        // pedras na beira do mar
        for (int i = 0; i < 6; i++)
            Prop(root, rng.Randf() < 0.5f ? "pedra_a" : "pedra_b", new Vector3(shore + rng.RandfRange(-1f, 3f), y, rng.RandfRange(-40f, 40f)), rng.RandfRange(0, Mathf.Tau), rng.RandfRange(0.8f, 1.8f));
    }

    // ------------------------------------------------------------------ mar
    const string OceanShader = @"shader_type spatial;
render_mode cull_disabled, diffuse_toon, specular_toon;
uniform vec4 deep : source_color = vec4(0.07, 0.42, 0.72, 1.0);
uniform vec4 shallow : source_color = vec4(0.22, 0.78, 0.86, 1.0);
uniform vec4 foam : source_color = vec4(0.97, 0.99, 1.0, 1.0);
uniform float shore_x = 0.0;
varying vec3 wp;
void vertex() {
    wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float d = max(wp.x - shore_x, 0.0);
    VERTEX.y += sin(wp.x * 0.35 - TIME * 1.3) * 0.08 * clamp(d / 8.0, 0.0, 1.0) + sin(wp.z * 0.5 + TIME * 0.9) * 0.04;
}
void fragment() {
    float d = wp.x - shore_x;
    vec3 col = mix(shallow.rgb, deep.rgb, clamp(d / 35.0, 0.0, 1.0));
    // linhas de espuma chegando na praia + faixa de espuma na beira
    float waves = smoothstep(0.82, 0.95, sin(d * 0.9 - TIME * 1.6 + sin(wp.z * 0.15) * 1.5)) * (1.0 - clamp(d / 14.0, 0.0, 1.0));
    float edge = 1.0 - smoothstep(0.0, 1.2 + 0.4 * sin(wp.z * 0.3 + TIME), d);
    col = mix(col, foam.rgb, clamp(waves * 0.7 + edge, 0.0, 1.0));
    ALBEDO = col;
    ROUGHNESS = 0.25;
    SPECULAR = 0.6;
}";

    static void Ocean(Node3D root, float shore, float y)
    {
        var mat = new ShaderMaterial { Shader = new Shader { Code = OceanShader } };
        mat.SetShaderParameter("shore_x", shore);
        var mesh = new PlaneMesh { Size = new Vector2(120, 240), SubdivideWidth = 60, SubdivideDepth = 60 };
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Position = new Vector3(shore + 60f - 0.5f, y + 0.03f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Name = "Ocean" };
        root.AddChild(mi);
    }

    /// <summary>Areia com variação de cor (ruído em coordenadas do mundo) e toon.</summary>
    public static Material SandMaterial()
    {
        var noise = new FastNoiseLite { Frequency = 0.08f, FractalOctaves = 3, Seed = 11 };
        var ramp = new Gradient();
        ramp.SetColor(0, new Color("cfa86a"));
        ramp.SetColor(1, new Color("e3c48a"));
        var tex = new NoiseTexture2D { Noise = noise, ColorRamp = ramp, Width = 256, Height = 256, Seamless = true };
        return new StandardMaterial3D
        {
            AlbedoTexture = tex, Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = Vector3.One * 0.05f,
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon, SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled, Roughness = 1f,
        };
    }
}
