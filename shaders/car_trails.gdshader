shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

// Road parameters (set per-instance)
uniform float road_half_width = 5.0;
uniform float road_center = 0.0;
uniform bool is_east_west = true;

// Lane settings
uniform int lanes_per_direction : hint_range(1, 4) = 2;

// Animation
uniform float scroll_speed = 12.0;
uniform float scroll_speed_variation = 3.0;

// Visual
uniform vec4 streak_color : source_color = vec4(1.0, 0.8, 0.4, 1.0);
uniform vec4 streak_color_oncoming : source_color = vec4(1.0, 0.27, 0.27, 1.0);
uniform float emission_strength : hint_range(0.0, 10.0) = 2.0;
uniform float trail_width : hint_range(0.01, 0.3) = 0.04;
uniform float fuzziness : hint_range(0.0, 1.0) = 0.7;

// Motion blur effect - elongates trails based on speed
uniform float motion_blur_strength : hint_range(0.0, 2.0) = 0.0;

// Bloom boost - amplifies emission for post-process bloom
uniform float bloom_boost : hint_range(0.0, 5.0) = 0.0;

varying vec3 world_pos;

// Hash functions
float hash(float n) {
    return fract(sin(n) * 43758.5453);
}

float hash2(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Smooth noise
float noise(float x) {
    float i = floor(x);
    float f = fract(x);
    float u = f * f * (3.0 - 2.0 * f);
    return mix(hash(i), hash(i + 1.0), u);
}

// 2D noise
float noise2d(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);

    float a = hash2(i);
    float b = hash2(i + vec2(1.0, 0.0));
    float c = hash2(i + vec2(0.0, 1.0));
    float d = hash2(i + vec2(1.0, 1.0));

    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// Fractal noise
float fbm(vec2 p) {
    float value = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 4; i++) {
        value += amplitude * noise2d(p);
        p *= 2.0;
        amplitude *= 0.5;
    }
    return value;
}

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

// Calculate trail intensity for a given lane position
// Returns: vec4(intensity, going_positive as 0/1, lane_speed, scroll_pos)
vec4 calculate_trail(float pos_along, float in_lane_pos, int lane_index, float lane_seed, float blur_offset) {
    // Very thin core with fuzzy falloff
    float lane_center = 0.5;
    float dist_from_center = abs(in_lane_pos - lane_center);

    // Noise-modulated width for organic edges
    float width_wobble = fbm(vec2(pos_along * 0.8 + TIME * 0.2, lane_seed)) * trail_width * 0.5;

    // Motion blur widens the trail based on speed
    float blur_width_factor = 1.0 + motion_blur_strength * 0.5;
    float effective_width = (trail_width + width_wobble) * blur_width_factor;

    // Fuzzy gaussian-like falloff instead of hard edge
    float fuzzy_falloff = exp(-dist_from_center * dist_from_center / (effective_width * effective_width * (0.5 + fuzziness)));
    float width_mask = fuzzy_falloff;

    // Direction and speed
    bool going_positive = lane_index < lanes_per_direction;
    float speed_var = (hash(lane_seed) - 0.5) * 2.0 * scroll_speed_variation;
    float lane_speed = scroll_speed + speed_var;
    float scroll_dir = going_positive ? 1.0 : -1.0;

    // Motion blur elongation - sample multiple positions along movement direction
    float scroll_pos = pos_along * scroll_dir - TIME * lane_speed + blur_offset * lane_speed * motion_blur_strength * 0.1;

    // Ghostly wisps - very sparse and ethereal
    float ghost1 = fbm(vec2(scroll_pos * 0.15 + TIME * 0.15, lane_seed * 0.5));
    float ghost2 = fbm(vec2(scroll_pos * 0.25 - TIME * 0.1, lane_seed * 0.7 + 50.0));
    float ghost3 = noise(scroll_pos * 0.5 + TIME * 0.4 + lane_seed * 3.0);

    float ghost_intensity = ghost1 * 0.35 + ghost2 * 0.35 + ghost3 * 0.3;

    // High threshold for very sparse wisps
    ghost_intensity = smoothstep(0.5, 0.85, ghost_intensity);

    // Additional breakup for isolated fragments
    float breakup1 = fbm(vec2(scroll_pos * 0.6 + lane_seed * 20.0, TIME * 0.25));
    float breakup2 = noise(scroll_pos * 1.2 + lane_seed * 100.0 + TIME * 0.2);
    float breakup = breakup1 * 0.6 + breakup2 * 0.4;
    breakup = smoothstep(0.35, 0.7, breakup);
    ghost_intensity *= breakup;

    // Slow temporal breathing - entire sections fade in/out
    float breath = sin(TIME * 0.3 + lane_seed * 1.5 + pos_along * 0.01) * 0.5 + 0.5;
    breath = smoothstep(0.2, 0.7, breath);
    ghost_intensity *= breath;

    float intensity = width_mask * ghost_intensity;
    return vec4(intensity, going_positive ? 1.0 : 0.0, lane_speed, scroll_pos);
}

void fragment() {
    float pos_along;
    float pos_across;

    if (is_east_west) {
        pos_along = world_pos.x;
        pos_across = world_pos.z - road_center;
    } else {
        pos_along = world_pos.z;
        pos_across = world_pos.x - road_center;
    }

    float normalized_across = pos_across / road_half_width;

    int total_lanes = lanes_per_direction * 2;
    float lane_width = (road_half_width * 2.0) / float(total_lanes);

    float lane_pos_continuous = (pos_across + road_half_width) / lane_width;
    int lane_index = int(floor(lane_pos_continuous));
    lane_index = clamp(lane_index, 0, total_lanes - 1);
    float in_lane_pos = fract(lane_pos_continuous);

    float lane_seed = float(lane_index) * 127.1 + (is_east_west ? 0.0 : 1000.0);

    // Calculate main trail with motion blur samples
    vec4 trail_data = calculate_trail(pos_along, in_lane_pos, lane_index, lane_seed, 0.0);
    float main_intensity = trail_data.x;
    bool going_positive = trail_data.y > 0.5;

    // Motion blur: blend multiple temporal samples for streak effect
    if (motion_blur_strength > 0.0) {
        float blur_samples = 3.0;
        float blur_accum = main_intensity;
        for (float i = 1.0; i <= blur_samples; i += 1.0) {
            vec4 sample_trail = calculate_trail(pos_along, in_lane_pos, lane_index, lane_seed, i);
            blur_accum += sample_trail.x * (1.0 - i / (blur_samples + 1.0)); // Fade older samples
        }
        main_intensity = blur_accum / (1.0 + blur_samples * 0.5);
    }

    // Color
    vec3 color = going_positive ? streak_color.rgb : streak_color_oncoming.rgb;

    // Subtle color variation
    float color_var = fbm(vec2(pos_along * 0.03, TIME * 0.08 + lane_seed)) * 0.15;
    color = mix(color, color * 1.2, color_var);

    // Road edge fade
    float edge_dist = 1.0 - abs(normalized_across);
    float edge_fade = smoothstep(0.0, 0.2, edge_dist);

    // Main trail intensity
    float final_alpha = main_intensity * edge_fade * emission_strength;
    final_alpha = pow(final_alpha, 0.7) * 0.6;

    // Apply bloom boost to color intensity
    vec3 final_color = color * (1.0 + bloom_boost);

    ALBEDO = final_color;
    ALPHA = final_alpha;
}
