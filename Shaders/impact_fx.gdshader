// v0.22: efeitos de impacto (ImpactFx). Desenha num quad/plano:
//   mode 0 = onda: anel que abre (progress 0 → 1), com rastro suave por dentro, falhas ao longo do anel e fade
//   mode 1 = brilho: disco suave que some
// billboard = vira para a câmera (anel do roubo, brilho); sem billboard = deitado (plano no aro / no chão)
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform int mode = 0;
uniform bool billboard = false;
uniform vec4 color : source_color = vec4(1.0, 0.72, 0.4, 1.0);
uniform float progress = 0.0;
uniform float width = 0.08;    // espessura do anel (fração do raio)
uniform float trail = 0.35;    // brilho do rastro por dentro do anel
uniform float breakup = 0.35;  // 0 = anel liso, 1 = bem quebrado
uniform float seed = 0.0;

void vertex() {
	if (billboard) {
		mat4 m = mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		m = m * mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0), vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
			vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0));
		MODELVIEW_MATRIX = VIEW_MATRIX * m;
	}
}

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float d = length(p);
	float fade = pow(clamp(1.0 - progress, 0.0, 1.0), 1.5);
	float v;
	if (mode == 1) {
		v = pow(clamp(1.0 - d, 0.0, 1.0), 2.2) * fade;
	} else {
		float r = progress;
		float w = max(width * (1.0 - 0.4 * progress), 0.004);
		float ring = 1.0 - smoothstep(0.0, w, abs(d - r));
		float inner = smoothstep(r - w * 5.0, r, d) * step(d, r) * trail;
		float a = atan(p.y, p.x);
		float n = 0.5 + 0.5 * sin(a * 7.0 + seed) * sin(a * 3.0 - seed * 1.7 + progress * 5.0);
		v = (ring + inner) * mix(1.0, n, breakup) * fade;
	}
	v *= 1.0 - smoothstep(0.94, 1.0, d);
	ALBEDO = color.rgb;
	ALPHA = clamp(v * color.a, 0.0, 1.0);
}
