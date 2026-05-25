shader_type spatial;
render_mode blend_add, depth_draw_never, cull_front, unshaded;

#include "res://shaders/includes/noise_functions.gdshaderinc"

// Aurora uniforms
uniform bool aurora_enabled = true;
uniform vec3 aurora_primary_color : source_color = vec3(0.0, 1.0, 0.53);
uniform vec3 aurora_secondary_color : source_color = vec3(1.0, 0.0, 1.0);
uniform float aurora_intensity : hint_range(0.0, 5.0) = 1.5;
uniform float aurora_speed : hint_range(0.0, 1.0) = 0.15;
uniform float aurora_band_count : hint_range(1.0, 10.0) = 3.0;

// Lightning uniforms
uniform bool lightning_enabled = true;
uniform float lightning_flash : hint_range(0.0, 1.0) = 0.0;
uniform vec2 lightning_center = vec2(0.5, 0.3);
uniform float lightning_intensity : hint_range(0.0, 5.0) = 1.0;

// Light pollution uniforms
uniform bool light_pollution_enabled = true;
uniform float light_pollution_intensity : hint_range(0.0, 3.0) = 1.0;
uniform vec3 light_pollution_warm_color : source_color = vec3(1.0, 0.6, 0.2);
uniform vec3 light_pollution_cool_color : source_color = vec3(0.3, 0.9, 1.0);
uniform float light_pollution_height : hint_range(0.3, 0.7) = 0.5;
uniform float light_pollution_fade_up : hint_range(0.05, 0.5) = 0.2;
uniform float light_pollution_fade_down : hint_range(0.05, 0.5) = 0.2;
uniform float light_pollution_edge_noise : hint_range(0.0, 0.2) = 0.06;

// ==================== LIGHT POLLUTION ====================
vec3 light_pollution(vec2 uv, float time) {
    if (!light_pollution_enabled) return vec3(0.0);

    float sky_height = 1.0 - uv.y;

    // Add noise to break up the horizontal edge
    float edge_noise = snoise(vec2(uv.x * 8.0 + time * 0.01, time * 0.02)) * light_pollution_edge_noise * 0.67;
    edge_noise += snoise(vec2(uv.x * 20.0, time * 0.03)) * light_pollution_edge_noise * 0.33;

    // Light pollution band centered on configurable height with soft fades
    float center = light_pollution_height + edge_noise;
    float below = smoothstep(center - light_pollution_fade_down, center, sky_height);
    float above = smoothstep(center + light_pollution_fade_up, center, sky_height);
    float horiz_band = below * above;

    // Subtle glow at the center
    float horizon_mask = 1.0 - abs(sky_height - center) / max(light_pollution_fade_up, 0.01);
    horizon_mask = clamp(horizon_mask, 0.0, 1.0);
    horizon_mask = horizon_mask * horizon_mask * 0.3;

    // Warm city glow
    vec3 warm_glow = light_pollution_warm_color * 0.18;

    // Cool cyberpunk tint with variation
    float pollution_wave = snoise(vec2(uv.x * 2.0 + time * 0.02, time * 0.01));
    vec3 cool_tint = light_pollution_cool_color * 0.10 * (pollution_wave * 0.5 + 0.5);

    // Variation across horizon
    float variation = snoise(vec2(uv.x * 4.0, time * 0.03)) * 0.2 + 0.8;

    // Combine
    float intensity = (horiz_band + horizon_mask * 0.5) * light_pollution_intensity;

    return (warm_glow + cool_tint) * intensity * variation;
}

// ==================== AURORA ====================
vec3 aurora_effect(vec2 uv, float time) {
    if (!aurora_enabled) return vec3(0.0);

    // UV.y on SphereMesh: 0 = top (zenith), 0.5 = equator (horizon), 1 = bottom
    // sky_height: 0 = below horizon, 0.5 = horizon, 1.0 = zenith
    float sky_height = 1.0 - uv.y;

    // Aurora lives NEAR the horizon, fading as it rises
    float horizon_glow = smoothstep(0.48, 0.54, sky_height);
    float upper_fade = smoothstep(0.80, 0.65, sky_height);
    float vert_mask = horizon_glow * upper_fade;
    if (vert_mask < 0.01) return vec3(0.0);

    // Horizontal angle around the dome
    float angle = uv.x * 6.28318;

    // Time components: slow drift vs fast shimmer
    float slow_time = time * aurora_speed;
    float fast_time = time * aurora_speed * 8.0;

    vec3 color = vec3(0.0);

    // === THE ARC: broad glowing band near horizon ===
    float arc_center = 0.55;
    float arc_dist = abs(sky_height - arc_center);
    float arc_glow = smoothstep(0.08, 0.0, arc_dist) * 0.4;
    float arc_wave = snoise(vec2(angle * 0.5 + slow_time * 0.1, slow_time * 0.05)) * 0.02;
    arc_glow *= smoothstep(0.06, 0.0, abs(sky_height - (arc_center + arc_wave)));
    color += mix(aurora_primary_color, aurora_secondary_color, 0.3) * arc_glow * aurora_intensity;

    // === CURTAINS: ribbons rising from the arc ===
    for (float i = 0.0; i < aurora_band_count; i++) {
        float idx = i / aurora_band_count;

        // === HERO CURTAINS: band 0 is the star, others are supporting ===
        bool is_hero = (i < 0.5);
        float hero_boost = is_hero ? 1.4 : 0.8;  // Subtler difference

        // Diagonal drift - curtains aren't perfectly vertical
        float drift = snoise(vec2(angle * 0.2 + idx * 3.0, slow_time * 0.05)) * 0.1;
        float curtain_phase = angle + idx * 2.1 + slow_time * 0.15 + drift;

        // Curtain base position
        float base_wave = fbm3(vec2(curtain_phase * 0.3, slow_time * 0.08 + idx * 5.0));
        float curtain_base = 0.54 + idx * 0.04 + base_wave * 0.03;

        // Curtain height - heroes slightly taller
        float fold_noise = fbm3(vec2(curtain_phase * 0.8 + idx * 3.0, slow_time * 0.12));
        float base_height = is_hero ? 0.14 : 0.12;
        float curtain_top = curtain_base + base_height + fold_noise * 0.08;

        // Inside curtain check
        float in_curtain = smoothstep(curtain_base - 0.02, curtain_base + 0.02, sky_height) *
                          smoothstep(curtain_top + 0.03, curtain_top - 0.02, sky_height);
        if (in_curtain < 0.01) continue;

        // === VERTICAL RAYS (original visibility) ===
        float ray_freq = 25.0 + idx * 10.0;
        float ray_phase = angle * ray_freq + slow_time * 0.5;
        float ray_pattern = sin(ray_phase) * 0.5 + 0.5;
        ray_pattern = pow(ray_pattern, 1.5);  // Original sharpening

        // Fast shimmer/flicker
        float shimmer = snoise(vec2(ray_phase * 0.5, fast_time + idx * 10.0));
        shimmer = 0.6 + shimmer * 0.4;
        ray_pattern *= shimmer;

        // === STRIATIONS (original subtlety) ===
        float striation_freq = 40.0;
        float striation = sin((sky_height - curtain_base) * striation_freq + slow_time * 2.0) * 0.5 + 0.5;
        striation = mix(0.7, 1.0, striation);  // Original subtle banding

        // === FOLDS ===
        float fold = fbm3(vec2(curtain_phase * 1.5 + slow_time * 0.2, sky_height * 3.0 + idx * 7.0));
        fold = fold * 0.5 + 0.5;
        float crease = smoothstep(0.7, 0.85, fold);
        fold = mix(fold, 1.0, crease * 0.5);

        // === TRAVELING PULSE ===
        float pulse_pos = fract(slow_time * 0.3 + idx * 0.33);
        float pulse_angle = pulse_pos * 6.28318;
        float pulse_dist = abs(angle - pulse_angle);
        pulse_dist = min(pulse_dist, 6.28318 - pulse_dist);
        float pulse = smoothstep(0.8, 0.0, pulse_dist) * 0.5;

        // === COLOR gradient ===
        float height_in_curtain = (sky_height - curtain_base) / max(curtain_top - curtain_base, 0.01);
        float color_blend = clamp(height_in_curtain, 0.0, 1.0);
        color_blend += snoise(vec2(curtain_phase * 0.3, slow_time * 0.1)) * 0.3;
        vec3 curtain_color = mix(aurora_primary_color, aurora_secondary_color, clamp(color_blend, 0.0, 1.0));

        // Combine all factors with hero boost
        float intensity = in_curtain * ray_pattern * striation * fold * (1.0 + pulse);
        intensity *= aurora_intensity / aurora_band_count;
        intensity *= hero_boost;

        // Brighter at base, fading upward
        float vert_falloff = smoothstep(curtain_top, curtain_base, sky_height);
        intensity *= mix(0.4, 1.0, vert_falloff);

        color += curtain_color * intensity * 1.5;
    }

    // === SOFT BLOOM underneath ===
    float bloom_zone = smoothstep(0.45, 0.55, sky_height) * smoothstep(0.65, 0.55, sky_height);
    vec3 bloom_color = mix(aurora_primary_color, aurora_secondary_color, 0.2) * 0.08;
    color += bloom_color * bloom_zone * aurora_intensity;

    return color * vert_mask;
}

// ==================== LIGHTNING ====================
vec3 lightning_effect(vec2 uv) {
    if (!lightning_enabled || lightning_flash < 0.01) return vec3(0.0);

    // Distance from lightning center
    float dist = length(uv - lightning_center);

    // Radial glow with falloff
    float glow = exp(-dist * 3.0) * lightning_flash * lightning_intensity;

    // Flash color (bright white with slight blue tint)
    vec3 flash_color = vec3(0.9, 0.95, 1.0);

    // Ambient sky illumination
    float ambient = lightning_flash * lightning_intensity * 0.3;

    return flash_color * (glow + ambient);
}

// ==================== MAIN ====================
void fragment() {
    vec3 color = vec3(0.0);

    // Layer effects (back to front for additive blending)
    color += light_pollution(UV, TIME);  // City glow at horizon
    color += aurora_effect(UV, TIME);
    color += lightning_effect(UV);

    ALBEDO = color;
    ALPHA = max(color.r, max(color.g, color.b));

    // Prevent writing to depth (sky is always behind everything)
    ALPHA = clamp(ALPHA, 0.0, 1.0);
}
