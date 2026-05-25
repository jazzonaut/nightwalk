// Common wet surface calculations for roads and sidewalks
// Shared between wet_surface.gdshader and wet_sidewalk.gdshader

// Calculate effective wetness with noise variation
float calculate_effective_wetness(vec3 world_pos, float wetness, float wetness_noise_scale, float wetness_noise_strength, sampler2D puddle_noise) {
    float wetness_noise = texture(puddle_noise, world_pos.xz * wetness_noise_scale + vec2(0.5, 0.5)).r;
    wetness_noise = (wetness_noise - 0.5) * 2.0 * wetness_noise_strength;
    return clamp(wetness + wetness_noise, 0.0, 1.0);
}

// Calculate puddle mask from world position
float calculate_puddle_mask(vec3 world_pos, bool puddles_enabled, float puddle_scale, float puddle_threshold, float puddle_edge_softness, sampler2D puddle_noise) {
    if (!puddles_enabled) {
        return 0.0;
    }

    vec2 puddle_uv = world_pos.xz * puddle_scale;
    float large_puddles = texture(puddle_noise, puddle_uv * 0.5).r;
    float medium_puddles = texture(puddle_noise, puddle_uv * 1.2 + vec2(0.3, 0.7)).r;
    float puddle_value = large_puddles * 0.6 + medium_puddles * 0.4;
    float breakup = texture(puddle_noise, puddle_uv * 4.0 + vec2(0.1, 0.9)).r;
    puddle_value = puddle_value * (0.7 + breakup * 0.3);

    return smoothstep(puddle_threshold - puddle_edge_softness,
                      puddle_threshold + puddle_edge_softness,
                      puddle_value);
}

// Calculate final roughness based on wetness and puddle mask
float calculate_wet_roughness(float base_roughness, float wet_roughness, float effective_wetness, float puddle_mask) {
    float final_roughness = mix(base_roughness, wet_roughness, effective_wetness);
    return mix(final_roughness, wet_roughness * 0.7, puddle_mask);
}

// Calculate wet surface color with darkening and puddle effects
vec3 calculate_wet_color(vec3 surface_color, float wet_darkening, float effective_wetness, float puddle_mask) {
    vec3 base_color = surface_color * (1.0 - wet_darkening * effective_wetness * 0.7);
    vec3 puddle_color = surface_color * 0.5;
    return mix(base_color, puddle_color, puddle_mask);
}

// Calculate ripple normal for puddles
vec3 calculate_ripples(vec3 world_pos, bool ripples_enabled, float puddle_mask, float ripple_speed, float ripple_scale, float ripple_strength, float time_scale, float time, sampler2D ripple_normal) {
    vec3 final_normal = vec3(0.5, 0.5, 1.0);

    if (!ripples_enabled || puddle_mask < 0.1) {
        return final_normal;
    }

    float anim_time = time * ripple_speed * time_scale;

    vec2 ripple_uv_large = world_pos.xz * ripple_scale * 0.15 + vec2(anim_time * 0.02, anim_time * 0.015);
    vec2 ripple_uv_med = world_pos.xz * ripple_scale * 0.4 + vec2(anim_time * 0.04, anim_time * 0.03);

    vec3 ripple_large = texture(ripple_normal, ripple_uv_large).xyz;
    vec3 ripple_med = texture(ripple_normal, ripple_uv_med).xyz;

    vec3 combined = ripple_large * 0.6 + ripple_med * 0.4;

    float ripple_blend = puddle_mask * ripple_strength;
    return mix(final_normal, combined, ripple_blend);
}

// Calculate specular boost for wet surfaces
float calculate_wet_specular(float effective_wetness, float puddle_mask) {
    float specular = mix(0.5, 0.8, effective_wetness);
    return mix(specular, 0.9, puddle_mask);
}
