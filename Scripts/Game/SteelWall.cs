using Godot;

namespace AB;

/// <summary>
/// v0.37: visual da Muralha de Aço do Iron — parede prateada meio transparente em cima da linha de 3 de uma cesta
/// (arco + cantos retos até o fim da quadra), da altura <see cref="Tuning.IronWallHeight"/>. Aparece/some suave conforme
/// <see cref="CourtGeo.Wall"/> (que o Match liga a partir do estado replicado — funciona no host e no cliente).
/// </summary>
public partial class SteelWall : Node3D
{
    public Match M;
    public int Hoop;

    MeshInstance3D _mesh;
    ShaderMaterial _mat;
    float _alpha, _builtFor = -1, _builtH = -1;

    const string Code = @"shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
uniform float alpha = 0.0;
uniform float height = 14.0;
varying vec3 wp;
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    float h = clamp(wp.y / height, 0.0, 1.0);
    float base = mix(0.34, 0.05, sqrt(h));
    float scan = smoothstep(0.9, 1.0, fract(wp.y * 0.9 - TIME * 0.5)) * 0.22 * (1.0 - h);
    float seam = smoothstep(0.94, 1.0, fract(UV.x / 1.6)) * 0.28 * (1.0 - h);
    float rivet = smoothstep(0.96, 1.0, fract(wp.y * 0.5)) * seam * 2.0;
    float foot = smoothstep(0.35, 0.0, wp.y) * 0.45;
    float top = smoothstep(0.04, 0.0, abs(h - 1.0)) * 0.0;
    vec3 steel = mix(vec3(0.66, 0.71, 0.78), vec3(0.92, 0.95, 1.0), h * 0.5);
    ALBEDO = mix(steel, vec3(1.0), clamp(scan + seam + rivet + foot, 0.0, 1.0));
    ALPHA = clamp(base + scan + seam + rivet + foot + top, 0.0, 0.92) * alpha;
}";

    public override void _Ready()
    {
        _mat = new ShaderMaterial { Shader = new Shader { Code = Code } };
        _mesh = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, MaterialOverride = _mat, Visible = false };
        AddChild(_mesh);
    }

    void Build()
    {
        var g = M.Geo;
        float H = Tuning.I.IronWallHeight;
        _builtFor = g.ThreePt; _builtH = H;
        float sg = CourtGeo.HoopSign(Hoop), R = g.ThreePt;
        var c = g.RimCenter[Hoop] with { Y = 0 };
        float edge = sg * g.MaxX;
        var pts = new System.Collections.Generic.List<Vector3>();
        int nStraight = 6;
        for (int i = 0; i <= nStraight; i++) pts.Add(new Vector3(Mathf.Lerp(edge, c.X, i / (float)nStraight), 0, R));
        int nArc = 64;
        for (int i = 1; i < nArc; i++)
        {
            float a = Mathf.Pi * i / nArc;
            pts.Add(c + new Vector3(-sg * Mathf.Sin(a), 0, Mathf.Cos(a)) * R);
        }
        for (int i = 0; i <= nStraight; i++) pts.Add(new Vector3(Mathf.Lerp(c.X, edge, i / (float)nStraight), 0, -R));

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        float s = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            if (i > 0) s += pts[i].DistanceTo(pts[i - 1]);
            st.SetUV(new Vector2(s, 0)); st.AddVertex(pts[i]);
            st.SetUV(new Vector2(s, 1)); st.AddVertex(pts[i] + Vector3.Up * H);
        }
        for (int i = 0; i < pts.Count - 1; i++)
        {
            int a = i * 2;
            st.AddIndex(a); st.AddIndex(a + 1); st.AddIndex(a + 2);
            st.AddIndex(a + 1); st.AddIndex(a + 3); st.AddIndex(a + 2);
        }
        _mesh.Mesh = st.Commit();
        _mat.SetShaderParameter("height", H);
    }

    public override void _Process(double delta)
    {
        if (M?.Geo == null) return;
        bool on = M.Geo.Wall[Hoop];
        _alpha = Mathf.MoveToward(_alpha, on ? 1f : 0f, (float)delta * (on ? 9f : 3f));
        _mesh.Visible = _alpha > 0.005f;
        if (!_mesh.Visible) return;
        if (!Mathf.IsEqualApprox(_builtFor, M.Geo.ThreePt) || !Mathf.IsEqualApprox(_builtH, Tuning.I.IronWallHeight)) Build();
        _mat.SetShaderParameter("alpha", _alpha);
    }
}
