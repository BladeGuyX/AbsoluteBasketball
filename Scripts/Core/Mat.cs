using System.Collections.Generic;
using Godot;

namespace AB;

/// <summary>Materiais estilizados (toon + contorno) — placeholder até a arte do Blender.</summary>
public static class Mat
{
    static Shader _outline;
    static readonly Dictionary<string, Material> Cache = new();

    static Shader OutlineShader => _outline ??= new Shader
    {
        Code = @"shader_type spatial;
render_mode unshaded, cull_front, depth_draw_opaque;
uniform vec4 color : source_color = vec4(0.11, 0.12, 0.17, 1.0);
uniform float thickness = 0.025;
void vertex() { VERTEX += NORMAL * thickness; }
void fragment() { ALBEDO = color.rgb; }",
    };

    public static StandardMaterial3D Toon(Color c, float rim = 0.25f)
    {
        string key = "toon" + c.ToHtml() + rim;
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = c,
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Toon,
            Roughness = 0.85f,
            RimEnabled = rim > 0,
            Rim = rim,
            RimTint = 0.6f,
        };
        Cache[key] = mat;
        return mat;
    }

    public static StandardMaterial3D ToonOutlined(Color c, float thickness = 0.025f)
    {
        string key = "toonO" + c.ToHtml() + thickness;
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = (StandardMaterial3D)Toon(c).Duplicate();
        var o = new ShaderMaterial { Shader = OutlineShader };
        o.SetShaderParameter("thickness", thickness);
        mat.NextPass = o;
        Cache[key] = mat;
        return mat;
    }

    public static StandardMaterial3D Unshaded(Color c, bool transparent = false)
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = c,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        if (transparent || c.A < 1f) mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        return mat;
    }

    public static MeshInstance3D MeshNode(Mesh mesh, Material mat, Vector3 pos, bool shadows = true)
    {
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Position = pos };
        mi.CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
        return mi;
    }
}

public static class Palette
{
    public static readonly Color[] Team = { new("2f80ed"), new("ff6b4a") };
    public static readonly Color[] TeamDark = { new("1d4f9a"), new("b8442b") };
    public static readonly string[] TeamName = { "AZUL", "LARANJA" };
    public static readonly Color Skin = new("f2c29b");
    public static readonly Color Ink = new("1d2433");
    public static readonly Color Court = new("3ca0d8");
    public static readonly Color CourtApron = new("8fd0ee");
    public static readonly Color Paint = new("ffb347");
    public static readonly Color Lines = new("ffffff");
    public static readonly Color Sand = new("f3dca2");
    public static readonly Color Ball = new("ff8a3d");
    public static readonly Color Rim = new("ff5a1f");
}
