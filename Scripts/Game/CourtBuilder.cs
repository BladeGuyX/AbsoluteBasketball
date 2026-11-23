using Godot;

namespace AB;

/// <summary>Constrói quadra, cestas, colisores, ambiente e decoração (placeholders estilizados).</summary>
public static class CourtBuilder
{
    const float LineW = 0.07f;
    static StandardMaterial3D _lineNoCull;

    public static void Build(Node3D root, CourtGeo g)
    {
        BuildEnvironment(root);
        BuildGround(root, g);
        BuildLines(root, g);
        for (int h = 0; h < 2; h++) BuildHoop(root, g, h);
        BuildWalls(root, g);
        BuildDecor(root, g);
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
    }

    static void BuildGround(Node3D root, CourtGeo g)
    {
        // colisor do chão
        var floor = new StaticBody3D { Name = "Floor", CollisionLayer = 1, CollisionMask = 0 };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(200, 1, 200) }, Position = new Vector3(0, -0.5f, 0) });
        root.AddChild(floor);

        root.AddChild(Mat.MeshNode(new PlaneMesh { Size = new Vector2(160, 160) }, Mat.Toon(Palette.Sand, 0), Vector3.Zero, false));
        // área jogável (margem) e quadra
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(g.MaxX - g.MinX, 0.06f, g.MaxZ - g.MinZ) }, Mat.Toon(Palette.CourtApron, 0), new Vector3(0, -0.02f, 0), false));
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(g.Length, 0.06f, g.Width) }, Mat.Toon(Palette.Court, 0), new Vector3(0, -0.015f, 0), false));

        // garrafão
        float keyW = Mathf.Min(6f, g.Width * 0.3f), keyL = g.ThreePt * 0.72f;
        for (int h = 0; h < 2; h++)
        {
            float s = CourtGeo.HoopSign(h);
            root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(keyL, 0.06f, keyW) }, Mat.Toon(Palette.Paint, 0),
                new Vector3(s * (g.HalfL - keyL / 2f), -0.01f, 0), false));
            AddRing(root, new Vector3(s * (g.HalfL - keyL), 0, 0), keyW / 2f, -90, 90, h == 1);
        }
        // círculo central
        root.AddChild(Mat.MeshNode(new CylinderMesh { TopRadius = 2.6f, BottomRadius = 2.6f, Height = 0.06f, RadialSegments = 48 },
            Mat.Toon(Palette.Paint, 0), new Vector3(0, -0.008f, 0), false));
    }

    static void BuildLines(Node3D root, CourtGeo g)
    {
        var m = Mat.Toon(Palette.Lines, 0);
        float y = 0.02f;
        void Box(Vector3 c, Vector3 size) => root.AddChild(Mat.MeshNode(new BoxMesh { Size = size }, m, c, false));
        Box(new Vector3(0, y, -g.HalfW), new Vector3(g.Length + LineW, 0.02f, LineW));
        Box(new Vector3(0, y, g.HalfW), new Vector3(g.Length + LineW, 0.02f, LineW));
        Box(new Vector3(-g.HalfL, y, 0), new Vector3(LineW, 0.02f, g.Width));
        Box(new Vector3(g.HalfL, y, 0), new Vector3(LineW, 0.02f, g.Width));
        Box(new Vector3(0, y, 0), new Vector3(LineW, 0.02f, g.Width));
        AddRing(root, Vector3.Zero, 2.6f, 0, 360, false);

        for (int h = 0; h < 2; h++)
        {
            float s = CourtGeo.HoopSign(h);
            var c = g.RimCenter[h]; c.Y = 0;
            float r = g.ThreePt;
            float maxZ = g.HalfW - 0.25f;
            if (r <= maxZ)
            {
                AddRing(root, c, r, -90, 90, h == 1);
                float len = Mathf.Abs(s * g.HalfL - c.X);
                Box(new Vector3(c.X + s * len / 2f, y, r), new Vector3(len, 0.02f, LineW));
                Box(new Vector3(c.X + s * len / 2f, y, -r), new Vector3(len, 0.02f, LineW));
            }
            else
            {
                float a = Mathf.RadToDeg(Mathf.Asin(maxZ / r));
                AddRing(root, c, r, -a, a, h == 1);
            }
        }
    }

    /// <summary>Arco plano no chão. Ângulos em graus; 0° aponta para +X, a menos que 'facingNegX'.</summary>
    static void AddRing(Node3D root, Vector3 center, float radius, float a0, float a1, bool facingNegX)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        int seg = Mathf.Max(8, (int)(Mathf.Abs(a1 - a0) / 4f));
        float rin = radius - LineW / 2f, rout = radius + LineW / 2f;
        float flip = facingNegX ? -1f : 1f;
        for (int i = 0; i < seg; i++)
        {
            float t0 = Mathf.DegToRad(Mathf.Lerp(a0, a1, i / (float)seg));
            float t1 = Mathf.DegToRad(Mathf.Lerp(a0, a1, (i + 1) / (float)seg));
            Vector3 P(float a, float rr) => new(flip * Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
            var p0 = P(t0, rin); var p1 = P(t0, rout); var p2 = P(t1, rout); var p3 = P(t1, rin);
            st.SetNormal(Vector3.Up);
            // ordem que gera face para cima independente do flip
            if (flip > 0) { st.AddVertex(p0); st.AddVertex(p2); st.AddVertex(p1); st.AddVertex(p0); st.AddVertex(p3); st.AddVertex(p2); }
            else { st.AddVertex(p0); st.AddVertex(p1); st.AddVertex(p2); st.AddVertex(p0); st.AddVertex(p2); st.AddVertex(p3); }
        }
        _lineNoCull ??= new StandardMaterial3D
        {
            AlbedoColor = Palette.Lines,
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var mi = Mat.MeshNode(st.Commit(), _lineNoCull, center + new Vector3(0, 0.021f, 0), false);
        root.AddChild(mi);
    }

    static void BuildHoop(Node3D root, CourtGeo g, int h)
    {
        var t = Tuning.I;
        float s = CourtGeo.HoopSign(h);
        var rim = g.RimCenter[h];
        var board = g.Board[h];
        var dark = Mat.ToonOutlined(new Color("39414f"), 0.02f);

        // poste + braço
        var pb = g.PoleBase[h];
        root.AddChild(Mat.MeshNode(new CylinderMesh { TopRadius = g.PoleRadius, BottomRadius = g.PoleRadius * 1.2f, Height = g.PoleHeight }, dark,
            pb + Vector3.Up * g.PoleHeight / 2f));
        float armLen = Mathf.Abs(pb.X - board.GetCenter().X);
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(armLen, 0.14f, 0.14f) }, dark,
            new Vector3((pb.X + board.GetCenter().X) / 2f, board.GetCenter().Y, 0)));
        // base acolchoada
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = new Vector3(0.7f, 1.4f, 0.7f) }, Mat.ToonOutlined(Palette.TeamDark[CourtGeo.ScoringTeamForHoop(h) == 0 ? 1 : 0]),
            pb + new Vector3(0, 0.7f, 0)));
        var poleBody = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        poleBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.7f, 4f, 0.7f) }, Position = pb + new Vector3(0, 2f, 0) });
        root.AddChild(poleBody);

        // tabela
        root.AddChild(Mat.MeshNode(new BoxMesh { Size = board.Size }, Mat.ToonOutlined(new Color("f7fbff"), 0.015f), board.GetCenter()));
        var frontX = board.GetCenter().X - s * (board.Size.X / 2f + 0.002f);
        var sq = Mat.Toon(Palette.Rim, 0);
        float sqW = t.RimRadius * 2.6f, sqH = t.RimRadius * 1.9f, sqY = t.HoopHeight + 0.08f + sqH / 2f, lw = 0.07f;
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

    static void BuildWalls(Node3D root, CourtGeo g)
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
