shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "includes/wet_surface_common.gdshaderinc"

// Base surface properties
uniform vec3 albedo : source_color = vec3(0.2, 0.2, 0.22);
uniform float base_roughness : hint_range(0.0, 1.0) = 0.85;

// Wetness parameters
uniform float wetness : hint_range(0.0, 1.0) = 1.0;
uniform float wet_roughness : hint_range(0.0, 1.0) = 0.05;
uniform float wet_darkening : hint_range(0.0, 0.5) = 0.3;

// Asphalt texture variation
uniform float texture_variation : hint_range(0.0, 0.5) = 0.15;
uniform float texture_scale : hint_range(0.1, 2.0) = 0.5;

// Puddle effect
uniform bool puddles_enabled = true;
uniform sampler2D puddle_noise : hint_default_white;
uniform float puddle_scale : hint_range(0.01, 0.5) = 0.1;
uniform float puddle_threshold : hint_range(0.0, 1.0) = 0.6;
uniform float puddle_edge_softness : hint_range(0.01, 0.3) = 0.1;

// Ripple animation
uniform bool ripples_enabled = true;
uniform float ripple_speed : hint_range(0.1, 5.0) = 1.5;
uniform float ripple_scale : hint_range(0.1, 2.0) = 0.5;
uniform float ripple_strength : hint_range(0.0, 0.5) = 0.30;
uniform sampler2D ripple_normal : hint_normal;

// Time scaling
uniform float time_scale : hint_range(0.1, 2.0) = 1.0;

// Wetness variation
uniform float wetness_noise_scale : hint_range(0.01, 0.2) = 0.06;
uniform float wetness_noise_strength : hint_range(0.0, 0.4) = 0.15;

varying vec3 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    // === ASPHALT TEXTURE ===
    vec2 uv_large = world_pos.xz * texture_scale * 0.1;
    vec2 uv_medium = world_pos.xz * texture_scale * 0.4;
    vec2 uv_fine = world_pos.xz * texture_scale * 1.5;

    float large_variation = texture(puddle_noise, uv_large).r;
    float medium_variation = texture(puddle_noise, uv_medium + vec2(0.33, 0.66)).r;
    float fine_variation = texture(puddle_noise, uv_fine + vec2(0.77, 0.22)).r;

    float combined_variation = large_variation * 0.4 + medium_variation * 0.35 + fine_variation * 0.25;
    float asphalt_variation = 1.0 + (combined_variation - 0.5) * texture_variation * 2.0;

    // Oil stains / patches
    float patches = texture(puddle_noise, world_pos.xz * 0.02 + vec2(0.5, 0.1)).r;
    float patch_mask = smoothstep(0.7, 0.75, patches);
    asphalt_variation *= mix(1.0, 0.85, patch_mask);

    vec3 asphalt_color = albedo * asphalt_variation;

    // Use shared wet surface functions
    float effective_wetness = calculate_effective_wetness(world_pos, wetness, wetness_noise_scale, wetness_noise_strength, puddle_noise);
    float puddle_mask = calculate_puddle_mask(world_pos, puddles_enabled, puddle_scale, puddle_threshold, puddle_edge_softness, puddle_noise);
    float final_roughness = calculate_wet_roughness(base_roughness, wet_roughness, effective_wetness, puddle_mask);
    vec3 final_color = calculate_wet_color(asphalt_color, wet_darkening, effective_wetness, puddle_mask);
    vec3 final_normal = calculate_ripples(world_pos, ripples_enabled, puddle_mask, ripple_speed, ripple_scale, ripple_strength, time_scale, TIME, ripple_normal);
    float final_specular = calculate_wet_specular(effective_wetness, puddle_mask);

    ALBEDO = final_color;
    EMISSION = vec3(0.0);
    ROUGHNESS = final_roughness;
    METALLIC = mix(0.0, 0.15, puddle_mask);
    NORMAL_MAP = final_normal;
    SPECULAR = final_specular;
}
