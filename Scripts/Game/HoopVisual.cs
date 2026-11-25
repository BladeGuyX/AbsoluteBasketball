using Godot;

namespace AB;

/// <summary>
/// Etapa 7 (v0.17): cesta do Blender (hoop_azul.glb / hoop_laranja.glb, art_source/scripts/make_hoop.py).
/// Cada peça é posicionada pela geometria do tuning (CourtGeo), então mudar aro/tabela no tuning continua batendo
/// com a física. Só visual: as colisões continuam as do CourtBuilder.
/// Animação: a rede balança por shader (cesta, aro) e o aro treme (enterrada, bola no aro). Os gatilhos vêm de
/// Sfx.Played (sons "swish", "rim" e a deixa muda "fx_dunk"), que também tocam no cliente online — sem mensagem nova.
/// </summary>
public partial class HoopVisual : Node3D
{
    // medidas do modelo (make_hoop.py)
    const float ModelRimR = 0.55f, ModelBoardW = 3.3f, ModelBoardH = 1.85f, ModelBoardZ = 4.95f;
    const float ModelSqW = 1.76f, ModelSqH = 1.27f, ModelArmLen = 1.72f, ModelPoleTopRef = 5.875f, ModelNetLen = 0.935f;

    int _hoop;
    CourtGeo _g;
    Node3D _pivot;               // dobradiça do aro (na frente da tabela): Aro e Rede são filhos
    ShaderMaterial _net, _netLine;
    float _tilt, _tiltV;         // aro: rad, + = frente desce
    Vector2 _push, _pushV;       // rede: deslocamento horizontal do fundo (m, local)
    float _stretch, _stretchV;   // rede: estica para baixo (m)
    float _flare, _flareV;       // rede: abre/fecha no meio (fração do raio)
    float _jitter;               // tremidinha rápida do aro (enterrada)

    public static HoopVisual Create(CourtGeo g, int hoop)
    {
        string path = $"res://assets/models/hoop_{(hoop == 0 ? "azul" : "laranja")}.glb";
        if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path) is not { } scene) return null;
        var v = new HoopVisual { Name = $"Hoop{hoop}", _hoop = hoop, _g = g };
        v.Build(scene.Instantiate<Node3D>());
        return v;
    }

    void Build(Node3D model)
    {
        var t = Tuning.I;
        float s = CourtGeo.HoopSign(_hoop);
        var rim = _g.RimCenter[_hoop];
        var board = _g.Board[_hoop];
        // frente do modelo (+Z depois do +Y Up) aponta para o centro da quadra
        var xf = new Transform3D(new Basis(Vector3.Up, -s * Mathf.Pi / 2f), new Vector3(rim.X, 0, rim.Z));
        Transform = xf;
        var inv = xf.AffineInverse();
        Vector3 L(Vector3 world) => inv * world;

        AddChild(model);
        Mat.ApplyToon(model, name => name switch
        {
            "Escuro" or "Acolchoado" => 0.02f,
            "Faixa" or "Vidro" => 0.015f,
            "Aro" => 0.012f,
            "Borracha" => 0.01f,
            _ => 0f,
        });

        float bx = board.GetCenter().X;
        float frontX = bx - s * board.Size.X / 2f;
        float backX = bx + s * board.Size.X / 2f;
        float rimScale = t.RimRadius / ModelRimR;

        _pivot = new Node3D { Name = "RimPivot", PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off };
        _pivot.Position = L(new Vector3(frontX, rim.Y, rim.Z));
        model.AddChild(_pivot);
        foreach (var name in new[] { "Aro", "Rede" })
        {
            if (model.FindChild(name, true, false) is not Node3D n) continue;
            n.Owner = null;
            n.GetParent().RemoveChild(n);
            _pivot.AddChild(n);
            n.Position = L(rim) - _pivot.Position;
            n.Scale = Vector3.One * rimScale;
            if (name == "Rede" && n is MeshInstance3D mi) SetupNet(mi);
        }
        if (model.FindChild("Tabela", true, false) is Node3D tb)
        {
            tb.Position = L(board.GetCenter());
            tb.Scale = new Vector3(board.Size.Z / ModelBoardW, board.Size.Y / ModelBoardH, 1);
        }
        if (model.FindChild("Quadrado", true, false) is Node3D sq)
        {
            float sqH = _g.BankMaxY - _g.BankMinY;
            sq.Position = L(new Vector3(frontX, (_g.BankMinY + _g.BankMaxY) / 2f, rim.Z));
            sq.Scale = new Vector3(_g.BankHalfWidth * 2f / ModelSqW, sqH / ModelSqH, 1);
            if (sq is GeometryInstance3D gi) gi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        var pole = L(_g.PoleBase[_hoop]);
        float poleScale = _g.PoleHeight / ModelPoleTopRef;
        if (model.FindChild("Poste", true, false) is Node3D po) { po.Position = pole; po.Scale = new Vector3(1, poleScale, 1); }
        if (model.FindChild("Base", true, false) is Node3D ba) ba.Position = pole;
        if (model.FindChild("Braco", true, false) is Node3D br)
        {
            br.Position = pole + Vector3.Up * (board.GetCenter().Y - ModelBoardZ);
            br.Scale = new Vector3(1, 1, Mathf.Abs(_g.PoleBase[_hoop].X - backX) / ModelArmLen);
        }
    }

    const string NetVertex = @"
uniform vec2 push = vec2(0.0);
uniform float stretch = 0.0;
uniform float flare = 0.0;
uniform float net_len = 0.935;
void vertex() {
    float w = clamp(-VERTEX.y / net_len, 0.0, 1.0);
    VERTEX.xz *= 1.0 + flare * sin(w * 3.14159);
    VERTEX.y -= stretch * w;
    VERTEX.xz += push * w * w;
    VERTEX += NORMAL * outline_px;
}";

    static Shader _netShader, _netLineShader;

    void SetupNet(MeshInstance3D mi)
    {
        _netShader ??= new Shader
        {
            Code = "shader_type spatial;\nrender_mode cull_disabled, diffuse_toon, specular_toon;\nconst float outline_px = 0.0;\nuniform vec4 color : source_color = vec4(1.0);"
                + NetVertex + "\nvoid fragment() { ALBEDO = color.rgb; ROUGHNESS = 0.9; }",
        };
        _netLineShader ??= new Shader
        {
            Code = "shader_type spatial;\nrender_mode unshaded, cull_front, depth_draw_opaque;\nuniform float outline_px = 0.004;\nuniform vec4 color : source_color = vec4(0.5, 0.55, 0.66, 1.0);"
                + NetVertex + "\nvoid fragment() { ALBEDO = color.rgb; }",
        };
        _net = new ShaderMaterial { Shader = _netShader };
        _netLine = new ShaderMaterial { Shader = _netLineShader };
        _net.NextPass = _netLine;
        _net.SetShaderParameter("net_len", ModelNetLen);
        _netLine.SetShaderParameter("net_len", ModelNetLen);
        for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++) mi.SetSurfaceOverrideMaterial(i, _net);
        mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    public override void _EnterTree() => Sfx.Played += OnSound;
    public override void _ExitTree() => Sfx.Played -= OnSound;

    void OnSound(string name, Vector3 pos, float db)
    {
        var t = Tuning.I;
        var rim = _g.RimCenter[_hoop];
        if (pos.DistanceTo(rim) > t.RimRadius * 3f + 0.6f) return;
        // posição do som no espaço da cesta (+Z = frente, lado da quadra)
        var local = Transform.AffineInverse() * pos - new Vector3(0, rim.Y, 0);
        var dir = new Vector2(local.X, local.Z);
        dir = dir.LengthSquared() > 1e-4f ? dir.Normalized() : Vector2.Zero;
        float amp = Mathf.Clamp(Mathf.DbToLinear(db - 2f), 0.05f, 1f);
        switch (name)
        {
            case "swish":
                float k = t.NetSwishStrength;
                _stretchV += 1.6f * k;
                _flareV += 3.5f * k;
                _pushV += new Vector2((float)GD.RandRange(-0.5, 0.5), (float)GD.RandRange(-0.5, 0.5)) * k;
                _tiltV += Mathf.DegToRad(1.5f) * 16f * k;
                break;
            case "rim":
                _tiltV += Mathf.DegToRad(3f) * 16f * amp * t.RimHitShake * (dir.Y >= -0.3f ? 1f : -0.4f);
                _pushV -= dir * 0.8f * amp * t.NetSwishStrength;
                break;
            case "fx_dunk":
                _tiltV += Mathf.DegToRad(t.RimDunkShake) * 16f;
                _jitter = 1f;
                _stretchV += 2.2f * t.NetSwishStrength;
                _pushV += new Vector2(0, -1.5f) * t.NetSwishStrength;
                break;
        }
    }

    static void Spring(ref float x, ref float v, float k, float c, float dt) { v += (-k * x - c * v) * dt; x += v * dt; }

    public override void _Process(double delta)
    {
        if (_pivot == null) return;
        float dt = Mathf.Min((float)delta, 1f / 30f);
        // sub-passos: molas duras ficam estáveis com fps baixo
        for (int i = 0; i < 2; i++)
        {
            float h = dt / 2f;
            Spring(ref _tilt, ref _tiltV, 260f, 7f, h);
            Spring(ref _stretch, ref _stretchV, 90f, 6f, h);
            Spring(ref _flare, ref _flareV, 120f, 8f, h);
            float px = _push.X, pvx = _pushV.X, py = _push.Y, pvy = _pushV.Y;
            Spring(ref px, ref pvx, 60f, 4f, h);
            Spring(ref py, ref pvy, 60f, 4f, h);
            _push = new Vector2(px, py); _pushV = new Vector2(pvx, pvy);
        }
        _tilt = Mathf.Clamp(_tilt, -0.35f, 0.35f);
        _stretch = Mathf.Clamp(_stretch, -0.1f, 0.4f);
        _flare = Mathf.Clamp(_flare, -0.4f, 0.5f);
        _jitter = Mathf.Max(0, _jitter - dt * 2.5f);
        float jit = _jitter * _jitter * Mathf.DegToRad(1.2f) * Mathf.Sin(Time.GetTicksMsec() * 0.09f);
        _pivot.Rotation = new Vector3(_tilt + jit, 0, 0);
        if (_net != null)
        {
            foreach (var m in new[] { _net, _netLine })
            {
                m.SetShaderParameter("push", _push);
                m.SetShaderParameter("stretch", _stretch);
                m.SetShaderParameter("flare", _flare);
            }
        }
    }
}
