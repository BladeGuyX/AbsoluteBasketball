using Godot;

namespace AB;

/// <summary>
/// v0.41 (One): o que ESTA tela vê do Realm. Cada tela decide sozinha pelo estado replicado (vale no cliente online):
/// - One no Realm: os adversários não o veem; os aliados o veem transparente; ele vê todo mundo transparente.
/// - Abduzido: não vê ninguém de fora do Realm; quem está no Realm se vê normalmente.
/// - Domínio das Sombras: o time adversário do One não vê ninguém do time dele (a bola continua visível).
/// Também desenha o "clima" roxo e escuro na tela (forte dentro do Realm, mais leve no Domínio).
/// Fica numa CanvasLayer abaixo da HUD (por cima do 3D).
/// </summary>
public partial class RealmView : CanvasLayer
{
    public Match M;
    ColorRect _rect;
    ShaderMaterial _mat;
    float _k, _t;

    const string ShaderCode = @"
shader_type canvas_item;
render_mode blend_mul;
uniform float strength = 0.0;
uniform float time = 0.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
void fragment() {
    // multiplica a imagem: tudo fica mais escuro e roxo, bem mais escuro nas bordas (com fumaça mexendo)
    vec2 uv = UV - 0.5;
    float r = length(uv * vec2(1.25, 1.0));
    float vig = smoothstep(0.2, 0.78, r);
    float n = noise(uv * 5.0 + vec2(time * 0.15, -time * 0.22)) * 0.6 + noise(uv * 11.0 - vec2(time * 0.3, time * 0.1)) * 0.4;
    float smoke = smoothstep(0.4, 0.9, n) * vig;
    vec3 center = vec3(0.62, 0.48, 0.86);
    vec3 edge = vec3(0.16, 0.06, 0.3);
    vec3 tint = mix(center, edge, clamp(vig + smoke * 0.35, 0.0, 1.0));
    COLOR = vec4(mix(vec3(1.0), tint, clamp(strength, 0.0, 1.0)), 1.0);
}";

    public override void _Ready()
    {
        Layer = -1;
        _mat = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        _rect = new ColorRect { Color = Colors.White, Material = _mat, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        AddChild(UiKit.FullRect(_rect));
    }

    public override void _Process(double delta)
    {
        if (M == null) return;
        float dt = (float)delta;
        _t += dt;
        var lp = M.LocalPlayer;
        bool cut = M.Phase == MatchPhase.Cutscene;
        foreach (var p in M.Players) p.ApplyView(Alpha(M, lp, p));
        float target = cut ? 0f : lp != null && lp.Phased ? 1f : M.AnyDomain ? 0.6f : 0f;
        _k = Mathf.MoveToward(_k, target, dt * (target > _k ? 4f : 2.5f));
        _rect.Visible = _k > 0.002f;
        if (_rect.Visible) { _mat.SetShaderParameter("strength", _k); _mat.SetShaderParameter("time", _t); }
    }

    /// <summary>Opacidade com que <paramref name="viewer"/> (o jogador desta tela) vê <paramref name="target"/>.</summary>
    public static float Alpha(Match m, Player viewer, Player target)
    {
        if (viewer == null || target == null || m == null || !m.AbilitiesOn) return 1f;
        float g = Mathf.Clamp(Tuning.I.OneGhostAlpha, 0.05f, 1f);
        if (target == viewer) return viewer.Phased ? Mathf.Max(g, 0.6f) : 1f;
        // Domínio das Sombras de um One adversário: ninguém do time dele aparece
        if (target.Team != viewer.Team && m.DomainAgainst(viewer) != null) return 0f;
        bool vp = viewer.Phased, tp = target.Phased;
        if (vp && tp) return 1f;                                    // os dois no Realm: se veem normalmente
        if (vp) return viewer.Ab.AbductT > 0 ? 0f : g;              // abduzido não vê quem está fora; o One vê transparente
        if (tp) return target.Team == viewer.Team && target.Ab.AbductT <= 0 ? g : 0f; // aliado no Realm = transparente; adversário ou abduzido = invisível
        return 1f;
    }
}
