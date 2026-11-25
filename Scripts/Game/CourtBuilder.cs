using Godot;

namespace AB;

/// <summary>
/// Monta o cenário da partida: ambiente (céu, sol, chão de areia), a cena da quadra (Court.tscn: piso com linhas por
/// shader, cestas, paredes) e a decoração. BuildHoop/BuildWalls são chamados pelo Court.
/// </summary>
public static class CourtBuilder
{
    /// <summary>Partida: quadra com as medidas do Tuning (g).</summary>
    public static Court Build(Node3D root, CourtGeo g)
    {
        BuildEnvironment(root);
        var court = Court.Instantiate();
        court.Name = "Court";
        court.Configure(g);
        root.AddChild(court);
        BuildDecor(root, g);
        return court;
    }

    /// <summary>Quadra só com os valores da cena Court.tscn (FIBA 28 × 15) — usado pelo --court-shot --fiba.</summary>
    public static Court BuildSceneDefaults(Node3D root)
    {
        BuildEnvironment(root);
        var court = Court.Instantiate();
        court.Name = "Court";
        root.AddChild(court);
        BuildDecor(root, court.Geo);
        return court;
    }

    static void BuildEnvironment(Node3D root)
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("3b8fe6"),
            SkyHorizonColor = new Color("cdeaff"),
            GroundHorizonColor = new Color("cdeaff"),
            GroundBottomColor = new Color("8fbfd8"),
            SunAngleMax = 30,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = 1.05f,
            GlowEnabled = true,
            GlowIntensity = 0.3f,
            SsaoEnabled = true,
            SsaoIntensity = 1.2f,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.12f,
        };
        root.AddChild(new WorldEnvironment { Environment = env });

        var sun = new DirectionalLight3D
        {
            LightEnergy = 1.25f,
            LightColor = new Color("fff3dc"),
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 60,
            RotationDegrees = new Vector3(-58, -32, 0),
        };
        root.AddChild(sun);

        // chão de areia em volta (abaixo da laje da quadra)
        root.AddChild(Mat.MeshNode(new PlaneMesh { Size = new Vector2(160, 160) }, Mat.Toon(Palette.Sand, 0),
            new Vector3(0, -Court.SlabDepth - 0.005f, 0), false));
    }

    public static void BuildHoop(Node3D root, CourtGeo g, int h)
    {
        var t = Tuning.I;
        float s = CourtGeo.HoopSign(h);
        var rim = g.RimCenter[h];
        var board = g.Board[h];
        var dark = Mat.ToonOutlined(new Color("39414f"), 0.02f);
        var pb = g.PoleBase[h];
        var poleBody = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        poleBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.7f, 4f, 0.7f) }, Position = pb + new Vector3(0, 2f, 0) });
        root.AddChild(poleBody);

        // v0.17: modelo do Blender (hoop_azul/hoop_laranja.glb); sem ele, o placeholder abaixo
        if (HoopVisual.Create(g, h) is { } visual) { root.AddChild(visual); return; }

        // poste + braço
        root.AddChild(Mat.MeshNode(new CylinderMesh { TopRadius = g.PoleRadius, BottomRadius = g.PoleRadius * 1.2f, Height = g.PoleHeight }, dark,
            pb + Vector3.Up * g.PoleHeight / 2f));
        float armLen = Mathf.Abs(pb.X - board.GetCenter().X);
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(armLen, 0.14f, 0.14f) }, dark,
            new Vector3((pb.X + board.GetCenter().X) / 2f, board.GetCenter().Y, 0)));
        // base acolchoada
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.7f, 1.4f, 0.7f) }, Mat.ToonOutlined(Palette.TeamDark[CourtGeo.ScoringTeamForHoop(h) == 0 ? 1 : 0]),
            pb + new Vector3(0, 0.7f, 0)));

        // tabela
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = board.Size }, Mat.ToonOutlined(new Color("f7fbff"), 0.015f), board.GetCenter()));
        var frontX = board.GetCenter().X - s * (board.Size.X / 2f + 0.002f);
        var sq = Mat.Toon(Palette.Rim, 0);
        float sqW = g.BankHalfWidth * 2f, sqH = g.BankMaxY - g.BankMinY, sqY = (g.BankMinY + g.BankMaxY) / 2f, lw = 0.08f;
        // preenchimento leve do quadrado para ficar claro que é um alvo
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.005f, sqH, sqW) }, Mat.Toon(new Color("ffe3cf"), 0), new Vector3(frontX + s * 0.001f, sqY, 0), false));
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.01f, lw, sqW) }, sq, new Vector3(frontX, sqY - sqH / 2f, 0), false));
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.01f, lw, sqW) }, sq, new Vector3(frontX, sqY + sqH / 2f, 0), false));
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.01f, sqH, lw) }, sq, new Vector3(frontX, sqY, -sqW / 2f), false));
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.01f, sqH, lw) }, sq, new Vector3(frontX, sqY, sqW / 2f), false));

        // aro + conector
        root.AddChild(Mat.MeshNode(new TorusMesh { InnerRadius = t.RimRadius - 0.035f, OuterRadius = t.RimRadius + 0.035f, Rings = 32, RingSegments = 8 },
            Mat.ToonOutlined(Palette.Rim, 0.012f), rim));
        float connLen = Mathf.Abs(frontX - (rim.X + s * t.RimRadius));
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(connLen, 0.06f, 0.2f) }, Mat.Toon(Palette.Rim),
            new Vector3(rim.X + s * (t.RimRadius + connLen / 2f), rim.Y, 0)));

        // rede
        var net = new StandardMaterial3D
        {
            AlbedoColor = new Color(1, 1, 1, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        root.AddChild(Mat.MeshNode(new CylinderMesh { TopRadius = t.RimRadius, BottomRadius = t.RimRadius * 0.62f, Height = t.RimRadius * 1.7f, CapTop = false, CapBottom = false, RadialSegments = 16 },
            net, rim + Vector3.Down * (t.RimRadius * 0.85f), false));
    }

    public static void BuildWalls(Node3D root, CourtGeo g)
    {
        var body = new StaticBody3D { Name = "Walls", CollisionLayer = 1, CollisionMask = 0 };
        float hgt = 6f, th = 1f;
        float lx = g.MaxX - g.MinX, lz = g.MaxZ - g.MinZ;
        void W(Vector3 c, Vector3 size) => body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = c });
        W(new Vector3(g.MinX - th / 2f, hgt / 2f, 0), new Vector3(th, hgt, lz + 2));
        W(new Vector3(g.MaxX + th / 2f, hgt / 2f, 0), new Vector3(th, hgt, lz + 2));
        W(new Vector3(0, hgt / 2f, g.MinZ - th / 2f), new Vector3(lx + 2, hgt, th));
        W(new Vector3(0, hgt / 2f, g.MaxZ + th / 2f), new Vector3(lx + 2, hgt, th));
        root.AddChild(body);

        // mureta visual baixa nas bordas
        var m = Mat.ToonOutlined(new Color("ffffff"), 0.015f);
        float y = 0.25f;
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(lx + 0.6f, 0.5f, 0.3f) }, m, new Vector3(0, y, g.MinZ - 0.15f)));
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(lx + 0.6f, 0.5f, 0.3f) }, m, new Vector3(0, y, g.MaxZ + 0.15f)));
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.3f, 0.5f, lz) }, m, new Vector3(g.MinX - 0.15f, y, 0)));
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.3f, 0.5f, lz) }, m, new Vector3(g.MaxX + 0.15f, y, 0)));
    }

    static void BuildDecor(Node3D root, CourtGeo g)
    {
        var rng = new RandomNumberGenerator { Seed = 7 };
        var trunk = Mat.ToonOutlined(new Color("a0703c"), 0.02f);
        var leaves = new[] { Mat.ToonOutlined(new Color("4cc56b"), 0.03f), Mat.ToonOutlined(new Color("38a957"), 0.03f) };
        for (int i = 0; i < 26; i++)
        {
            float ang = rng.RandfRange(0, Mathf.Tau);
            float dist = rng.RandfRange(Mathf.Max(g.MaxX, g.MaxZ) + 6, 45);
            var p = new Vector3(Mathf.Cos(ang) * dist * 1.1f, 0, Mathf.Sin(ang) * dist * 0.8f);
            float hgt = rng.RandfRange(2.5f, 5f);
            root.AddChild(Mat.MeshNode(new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.28f, Height = hgt }, trunk, p + Vector3.Up * hgt / 2f));
            float lr = rng.RandfRange(1.2f, 2.2f);
            root.AddChild(Mat.MeshNode(new SphereMesh { Radius = lr, Height = lr * 1.7f }, leaves[i % 2], p + Vector3.Up * (hgt + lr * 0.5f)));
        }
        // arquibancadas nas laterais
        var seat = new[] { Mat.ToonOutlined(Palette.Team[0], 0.02f), Mat.ToonOutlined(Palette.Team[1], 0.02f) };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int row = 0; row < 3; row++)
            {
                float z = side * (g.MaxZ + 2.2f + row * 1.0f);
                root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(g.Length * 0.7f, 0.45f + row * 0.45f, 1.0f) },
                    seat[(row + (side > 0 ? 1 : 0)) % 2], new Vector3(0, (0.45f + row * 0.45f) / 2f, z)));
            }
        }
    }
}
