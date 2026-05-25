shader_type canvas_item;
render_mode unshaded;

uniform vec4 base_color : source_color = vec4(0.04, 0.05, 0.08, 1.0);
uniform vec4 haze_color : source_color = vec4(0.10, 0.14, 0.22, 1.0);
uniform vec4 neon_cyan : source_color = vec4(0.0, 0.9, 0.9, 1.0);
uniform vec4 neon_magenta : source_color = vec4(0.9, 0.2, 0.8, 1.0);
uniform vec4 window_color : source_color = vec4(1.0, 0.92, 0.82, 1.0);

uniform float fog_strength = 0.8;
uniform float skyline_strength = 0.55;
uniform float neon_strength = 0.7;
uniform float window_strength = 0.35;
uniform float rain_strength = 0.22;
uniform float grain_strength = 0.03;
uniform float time_speed = 0.18;

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

float fbm(vec2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        v += noise(p) * a;
        p *= 2.0;
        a *= 0.5;
    }
    return v;
}

void fragment()
{
    vec2 uv = SCREEN_UV;
    vec2 screen_size = 1.0 / SCREEN_PIXEL_SIZE;
    float aspect = screen_size.x / screen_size.y;
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0);

    float gradient = smoothstep(0.0, 1.0, uv.y);
    vec3 col = mix(base_color.rgb, haze_color.rgb, gradient * fog_strength);

    float fog = fbm(uv * vec2(1.8, 1.2) + vec2(TIME * time_speed * 0.12, TIME * time_speed * 0.18));
    float fog_warp = fbm(uv * vec2(3.6, 2.6) + vec2(TIME * time_speed * 0.22, TIME * time_speed * 0.05));
    float fog_amount = clamp(fog * 0.6 + fog_warp * 0.4, 0.0, 1.0);
    float fog_mask = smoothstep(0.2, 1.0, uv.y);
    col = mix(col, col + haze_color.rgb * 0.32, fog_amount * 0.6 * fog_mask);

    float columns = 24.0;
    float column_id = floor(uv.x * columns);
    float height = 0.22 + 0.45 * hash(vec2(column_id, 3.1));
    float skyline = step(uv.y, height) * smoothstep(0.0, 0.45, 0.45 - uv.y);
    col = mix(col, col * 0.6, skyline * skyline_strength);

    vec2 grid = vec2(70.0, 36.0);
    vec2 cell = floor(vec2(uv.x * grid.x, uv.y * grid.y));
    float window_mask = step(0.88, hash(cell));
    float window_variation = 0.3 + 0.7 * hash(cell + vec2(13.3, 7.7));
    float twinkle = 0.65 + 0.35 * sin(TIME * 2.2 + hash(cell + vec2(5.4, 2.1)) * 6.28);
    float windows = window_mask * window_variation * skyline * twinkle;
    col += window_color.rgb * windows * window_strength;

    float bottom_mask = smoothstep(0.6, 0.0, uv.y);
    float sweep = TIME * time_speed;
    float band_a = 1.0 - smoothstep(0.0, 0.01, abs(fract(uv.x * 5.0 + 0.1 + sweep) - 0.5));
    float band_b = 1.0 - smoothstep(0.0, 0.008, abs(fract(uv.x * 8.0 + 0.35 - sweep * 0.7) - 0.5));
    float band_c = 1.0 - smoothstep(0.0, 0.012, abs(fract(uv.x * 3.5 + 0.7 + sweep * 0.4) - 0.5));
    float flow = 0.5 + 0.5 * sin((uv.y * 6.0) + sweep * 6.0);
    float shimmer = 0.6 + 0.4 * sin((uv.y * 2.5) + sweep * 4.0);
    float glow_mask = smoothstep(0.0, 0.6, 0.6 - abs(uv.y - 0.7));
    float neon = (band_a * 0.7 + band_b * 0.6 + band_c * 0.5) * bottom_mask * shimmer * flow;
    float neon_pick = hash(vec2(floor(uv.y * 6.0), 9.1));
    vec3 neon_color = mix(neon_cyan.rgb, neon_magenta.rgb, neon_pick);
    col += neon_color * neon * neon_strength * (0.6 + 0.4 * glow_mask);

    // Rain disabled for menu background.

    float grain = hash(uv * screen_size * 0.5 + vec2(TIME * 10.0, 0.0));
    col += (grain - 0.5) * grain_strength;

    float vignette = smoothstep(0.95, 0.2, length(p * vec2(1.2, 1.0)));
    col *= 0.8 + 0.2 * vignette;

    COLOR = vec4(clamp(col, 0.0, 1.0), 1.0);
}
