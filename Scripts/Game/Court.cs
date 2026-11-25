using Godot;

namespace AB;

/// <summary>
/// Etapa 7 (v0.18): quadra como cena (Scenes/Court.tscn) com medidas em [Export].
/// - Piso: um único plano com Shaders/court_floor.gdshader. As linhas são calculadas no shader em METROS a partir
///   destes parâmetros (nada pintado em textura). Retas e piso esticam com Length/Width; garrafão, círculos e arco de
///   3 têm tamanho fixo e ficam ancorados nas linhas de fundo e no centro.
/// - Madeira: assets/textures/wood_floor.png (art_source/scripts/make_wood_floor.py) com UV em coordenadas de mundo
///   (WoodTileSize metros por repetição) — repete sem esticar.
/// - Cestas (CourtBuilder.BuildHoop), paredes invisíveis e mureta (CourtBuilder.BuildWalls) e a borda chanfrada se
///   ajustam sozinhas à CourtGeo gerada destas medidas.
/// Na partida, Match chama Configure(Geo) antes de adicionar o nó: as medidas de gameplay (comprimento, largura, raio
/// de 3, aro, margem, linha de lance livre) vêm do Tuning; as peças só visuais ficam com os valores da cena.
/// NUNCA escalar este nó — mude os parâmetros. Os nós gerados não têm Owner (não são salvos na cena).
/// </summary>
[Tool]
public partial class Court : Node3D
{
    public const string ScenePath = "res://Scenes/Court.tscn";
    const string ShaderPath = "res://Shaders/court_floor.gdshader";
    const string WoodPath = "res://assets/textures/wood_floor.png";
    /// <summary>Borda da laje: quanto o piso passa da área jogável, chanfro e profundidade (m).</summary>
    public const float SlabExtra = 0.45f, SlabBevel = 0.3f, SlabDepth = 0.15f;

    float _length = 28f, _width = 15f, _apron = 1.5f;
    float _three = 6.75f, _inset = 1.575f, _keyW = 4.9f, _keyL = 5.8f, _ftR = 1.8f, _centerR = 1.8f;
    float _lineW = 0.1f, _woodTile = 5f;
    bool _logo = true, _hoops = true;
    Color _woodTint = new("dcc2ad"), _lineColor = new("f7f7f2"), _apronColor = new("262b54"),
          _logoColor = new("3c4a82"), _edgeColor = new("1c2042");

    // ---------------- estica ----------------
    [ExportGroup("Tamanho (estica)")]
    [Export(PropertyHint.Range, "10,150,0.5,suffix:m")] public float Length { get => _length; set { _length = value; Changed(); } }
    [Export(PropertyHint.Range, "6,100,0.5,suffix:m")] public float Width { get => _width; set { _width = value; Changed(); } }
    /// <summary>Faixa azul (apron) entre as linhas e as paredes invisíveis = área jogável além das linhas.</summary>
    [Export(PropertyHint.Range, "0.2,6,0.05,suffix:m")] public float ApronWidth { get => _apron; set { _apron = value; Changed(); } }

    // ---------------- fixas ----------------
    [ExportGroup("Peças fixas")]
    [Export(PropertyHint.Range, "2,20,0.05,suffix:m")] public float ThreePointRadius { get => _three; set { _three = value; Changed(); } }
    /// <summary>Centro do aro até a linha de fundo.</summary>
    [Export(PropertyHint.Range, "0.5,5,0.025,suffix:m")] public float HoopInset { get => _inset; set { _inset = value; Changed(); } }
    [Export(PropertyHint.Range, "1,12,0.05,suffix:m")] public float KeyWidth { get => _keyW; set { _keyW = value; Changed(); } }
    /// <summary>Linha de fundo até a linha de lance livre.</summary>
    [Export(PropertyHint.Range, "1,15,0.05,suffix:m")] public float KeyLength { get => _keyL; set { _keyL = value; Changed(); } }
    [Export(PropertyHint.Range, "0.5,6,0.05,suffix:m")] public float FreeThrowCircleRadius { get => _ftR; set { _ftR = value; Changed(); } }
    [Export(PropertyHint.Range, "0.5,8,0.05,suffix:m")] public float CenterCircleRadius { get => _centerR; set { _centerR = value; Changed(); } }
    [Export(PropertyHint.Range, "0.02,0.3,0.005,suffix:m")] public float LineWidth { get => _lineW; set { _lineW = value; Changed(); } }

    // ---------------- visual ----------------
    [ExportGroup("Visual")]
    /// <summary>Metros cobertos por uma repetição da textura de madeira.</summary>
    [Export(PropertyHint.Range, "0.5,20,0.1,suffix:m")] public float WoodTileSize { get => _woodTile; set { _woodTile = value; Changed(); } }
    [Export] public Color WoodTint { get => _woodTint; set { _woodTint = value; Changed(); } }
    [Export] public Color LineColor { get => _lineColor; set { _lineColor = value; Changed(); } }
    [Export] public Color ApronColor { get => _apronColor; set { _apronColor = value; Changed(); } }
    [Export] public Color EdgeColor { get => _edgeColor; set { _edgeColor = value; Changed(); } }
    [Export] public bool CenterLogo { get => _logo; set { _logo = value; Changed(); } }
    [Export] public Color LogoColor { get => _logoColor; set { _logoColor = value; Changed(); } }
    [Export] public bool Hoops { get => _hoops; set { _hoops = value; Changed(); } }

    /// <summary>Geometria lógica em uso (a da partida, ou a gerada dos [Export]).</summary>
    public CourtGeo Geo { get; private set; }
    CourtGeo _external;
    Node3D _gen;
    bool _queued;

    public static Court Instantiate() => GD.Load<PackedScene>(ScenePath).Instantiate<Court>();

    /// <summary>Partida: usa a geometria do Tuning. Chamar antes de adicionar à árvore.</summary>
    public void Configure(CourtGeo g)
    {
        _external = g;
        _length = g.Length; _width = g.Width; _apron = g.Margin;
        _three = g.ThreePt; _inset = g.HoopInset;
        _keyL = g.HoopInset + Tuning.I.FreeThrowDistance; // o arremessador do lance livre fica em cima da linha
    }

    public override void _Ready() => Rebuild();

    void Changed()
    {
        if (!IsInsideTree() || _queued) return;
        _queued = true;
        _external = null; // mexeu nos parâmetros: a cena volta a mandar
        Callable.From(Rebuild).CallDeferred();
    }

    public void Rebuild()
    {
        _queued = false;
        if (_gen != null) { RemoveChild(_gen); _gen.QueueFree(); }
        _gen = new Node3D { Name = "_Generated" };
        AddChild(_gen);
        Geo = _external ?? CourtGeo.Create(_length, _width, _three, _inset, _apron);

        BuildFloor(Geo);
        BuildSlabEdge(Geo);
        CourtBuilder.BuildWalls(_gen, Geo);
        if (_hoops) for (int h = 0; h < 2; h++) CourtBuilder.BuildHoop(_gen, Geo, h);
    }

    void BuildFloor(CourtGeo g)
    {
        var body = new StaticBody3D { Name = "FloorBody", CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(200, 1, 200) }, Position = new Vector3(0, -0.5f, 0) });
        _gen.AddChild(body);

        float ex = g.Margin + SlabExtra;
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        mat.SetShaderParameter("half_size", new Vector2(g.HalfL, g.HalfW));
        mat.SetShaderParameter("line_width", _lineW);
        mat.SetShaderParameter("three_radius", g.ThreePt);
        mat.SetShaderParameter("hoop_inset", g.HoopInset);
        mat.SetShaderParameter("key_width", _keyW);
        mat.SetShaderParameter("key_length", _keyL);
        mat.SetShaderParameter("ft_radius", _ftR);
        mat.SetShaderParameter("center_radius", _centerR);
        mat.SetShaderParameter("logo_enabled", _logo ? 1f : 0f);
        mat.SetShaderParameter("wood_tile", Mathf.Max(_woodTile, 0.01f));
        mat.SetShaderParameter("wood_tint", _woodTint);
        mat.SetShaderParameter("line_color", _lineColor);
        mat.SetShaderParameter("apron_color", _apronColor);
        mat.SetShaderParameter("logo_color", _logoColor);
        if (ResourceLoader.Exists(WoodPath)) mat.SetShaderParameter("wood_tex", GD.Load<Texture2D>(WoodPath));

        var floor = Mat.MeshNode(new PlaneMesh { Size = new Vector2(g.Length + 2f * ex, g.Width + 2f * ex) }, mat, Vector3.Zero, false);
        floor.Name = "Floor";
        _gen.AddChild(floor);
    }

    /// <summary>Chanfro em volta do piso (descendo até o chão de areia), como no corte da concept.</summary>
    void BuildSlabEdge(CourtGeo g)
    {
        float hx = g.HalfL + g.Margin + SlabExtra, hz = g.HalfW + g.Margin + SlabExtra;
        float bx = hx + SlabBevel, bz = hz + SlabBevel, y0 = 0f, y1 = -SlabDepth;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            st.SetNormal((outward * SlabDepth + Vector3.Up * SlabBevel).Normalized());
            st.AddVertex(a); st.AddVertex(b); st.AddVertex(c);
            st.AddVertex(a); st.AddVertex(c); st.AddVertex(d);
        }
        Quad(new(-hx, y0, -hz), new(hx, y0, -hz), new(bx, y1, -bz), new(-bx, y1, -bz), Vector3.Forward);
        Quad(new(hx, y0, hz), new(-hx, y0, hz), new(-bx, y1, bz), new(bx, y1, bz), Vector3.Back);
        Quad(new(-hx, y0, hz), new(-hx, y0, -hz), new(-bx, y1, -bz), new(-bx, y1, bz), Vector3.Left);
        Quad(new(hx, y0, -hz), new(hx, y0, hz), new(bx, y1, bz), new(bx, y1, -bz), Vector3.Right);
        var mat = new StandardMaterial3D
        {
            AlbedoColor = _edgeColor,
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Toon,
            Roughness = 0.85f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var mi = Mat.MeshNode(st.Commit(), mat, Vector3.Zero, false);
        mi.Name = "SlabEdge";
        _gen.AddChild(mi);
    }
}
