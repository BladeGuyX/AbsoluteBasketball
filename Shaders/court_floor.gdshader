// Piso da quadra (Scripts/Game/Court.cs). Tudo em METROS no espaço local do nó Court (centro = (0,0), X = comprimento).
// - Linhas: calculadas por distância (SDF) a partir das medidas — nada pintado em textura.
//   Retas e piso esticam com half_size; garrafão, círculos e arco de 3 têm tamanho fixo, ancorados nas linhas de
//   fundo (cada metade é espelhada por abs(x)) e no centro.
// - Madeira: UV = posição de MUNDO / wood_tile, então repete sem esticar qualquer que seja o tamanho da quadra.
// - Fora da quadra: faixa (apron) azul-marinho.
shader_type spatial;
render_mode diffuse_toon, specular_toon;

uniform vec2 half_size = vec2(14.0, 7.5);
uniform float line_width = 0.07;
uniform float three_radius = 6.75;
uniform float hoop_inset = 1.575;
uniform float key_width = 4.9;
uniform float key_length = 5.8;
uniform float ft_radius = 1.8;
uniform float center_radius = 1.8;
uniform float dash_length = 0.35;
uniform float logo_enabled = 1.0;

uniform sampler2D wood_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float wood_tile = 4.0;
uniform vec4 wood_tint : source_color = vec4(1.0);
uniform vec4 line_color : source_color = vec4(0.97, 0.97, 0.95, 1.0);
uniform vec4 apron_color : source_color = vec4(0.149, 0.169, 0.329, 1.0);
uniform vec4 logo_color : source_color = vec4(0.235, 0.29, 0.51, 1.0);

varying vec3 local_pos;
varying vec3 world_pos;

void vertex() {
	local_pos = VERTEX;
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float sd_box(vec2 p, vec2 b) {
	vec2 q = abs(p) - b;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
}

// estrela de 5 pontas (Inigo Quilez), ponta em +y
float sd_star5(vec2 p, float r, float rf) {
	const vec2 k1 = vec2(0.809016994375, -0.587785252292);
	const vec2 k2 = vec2(-0.809016994375, -0.587785252292);
	p.x = abs(p.x);
	p -= 2.0 * max(dot(k1, p), 0.0) * k1;
	p -= 2.0 * max(dot(k2, p), 0.0) * k2;
	p.x = abs(p.x);
	p.y -= r;
	vec2 ba = rf * vec2(-k1.y, k1.x) - vec2(0.0, 1.0);
	float h = clamp(dot(p, ba) / dot(ba, ba), 0.0, r);
	return length(p - ba * h) * sign(p.y * ba.x - p.x * ba.y);
}

void fragment() {
	vec2 p = local_pos.xz;
	vec2 hs = half_size;
	float aa = max(length(fwidth(p)) * 0.7, 0.0005);
	float hw = line_width * 0.5;

	// piso: madeira dentro, apron fora
	vec3 wood = texture(wood_tex, world_pos.xz / wood_tile).rgb * wood_tint.rgb;
	float sd_court = sd_box(p, hs);
	vec3 col = mix(wood, apron_color.rgb, smoothstep(-aa, aa, sd_court));

	// logo central (fixo): anel e estrela
	float rl = length(p);
	float band = abs(rl - center_radius * 0.66) - center_radius * 0.09;
	float star = sd_star5(vec2(p.x, -p.y), center_radius * 0.42, 0.45);
	col = mix(col, logo_color.rgb, (1.0 - smoothstep(-aa, aa, min(band, star))) * logo_enabled);

	// ---- linhas (distância ao eixo de cada linha) ----
	float dl = abs(p.x);                         // linha do meio
	dl = min(dl, abs(rl - center_radius));       // círculo central

	// metade espelhada: u = distância a partir da linha de fundo (para dentro), v = lateral
	vec2 a = vec2(hs.x - abs(p.x), p.y);
	// 3 pontos: arco na frente do aro, retas paralelas à lateral atrás dele (mesma regra de CourtGeo.IsThree)
	float d3 = a.x >= hoop_inset
		? abs(length(vec2(a.x - hoop_inset, a.y)) - three_radius)
		: abs(abs(a.y) - three_radius);
	// garrafão (retângulo da linha de fundo até a linha de lance livre)
	float dk = abs(sd_box(vec2(a.x - key_length * 0.5, a.y), vec2(key_length * 0.5, key_width * 0.5)));
	// semicírculo do lance livre: metade de fora contínua, metade de dentro tracejada
	vec2 fc = vec2(a.x - key_length, a.y);
	float dfc = abs(length(fc) - ft_radius);
	if (fc.x < 0.0) {
		float arc = atan(fc.y, -fc.x) * ft_radius;
		if (fract(arc / dash_length + 0.25) > 0.5) dfc = 1e3;
	}
	dl = min(dl, min(d3, min(dk, dfc)));
	dl = sd_court <= hw ? dl : 1e3;              // marcações internas só dentro da quadra
	dl = min(dl, abs(sd_court));                 // linhas laterais e de fundo

	float cov = 1.0 - smoothstep(hw - aa, hw + aa, dl);
	col = mix(col, line_color.rgb, cov);

	ALBEDO = col;
	ROUGHNESS = 0.8;
	SPECULAR = 0.2;
}
