shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "includes/weathering_common.gdshaderinc"

// Base surface properties (matching building materials)
uniform vec3 albedo : source_color = vec3(0.055, 0.063, 0.094);
uniform float base_roughness : hint_range(0.0, 1.0) = 0.85;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;

// Noise textures for procedural effects
uniform sampler2D weathering_noise : hint_default_white;
uniform sampler2D detail_noise : hint_default_white;

// Dirt/grime accumulation
uniform float dirt_intensity : hint_range(0.0, 1.0) = 0.4;
uniform float dirt_height_falloff : hint_range(0.0, 1.0) = 0.3;
uniform float dirt_corner_intensity : hint_range(0.0, 1.0) = 0.5;
uniform vec3 dirt_color : source_color = vec3(0.02, 0.012, 0.008);

// Water damage streaks
uniform float water_streak_intensity : hint_range(0.0, 1.0) = 0.3;
uniform float water_streak_scale : hint_range(0.1, 2.0) = 0.5;
uniform float water_streak_sparsity : hint_range(0.0, 1.0) = 0.6;
uniform float water_streak_strength : hint_range(0.0, 1.0) = 0.6;
uniform vec3 water_stain_color : source_color = vec3(0.03, 0.03, 0.025);

// Rust stains (from metal fixtures)
uniform float rust_intensity : hint_range(0.0, 1.0) = 0.2;
uniform vec3 rust_color : source_color = vec3(0.15, 0.05, 0.02);
uniform float rust_drip_length : hint_range(0.5, 5.0) = 2.0;
uniform float rust_chance : hint_range(0.0, 1.0) = 0.35;
uniform float rust_strength : hint_range(0.0, 1.0) = 0.7;

// Roughness variation
uniform float roughness_variation : hint_range(0.0, 0.3) = 0.15;
uniform float worn_roughness_boost : hint_range(0.0, 0.2) = 0.1;

// Floor heights for rust pattern alignment
uniform float floor_height : hint_range(2.0, 6.0) = 3.75;
uniform float ground_floor_height : hint_range(0.0, 3.0) = 1.25;

// World position passed from vertex shader for stable weathering
varying vec3 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec3 world_pos = world_position;

    // Building seed from UV (set per-building during geometry creation)
    float building_seed = UV.x;

    // === DIRT/GRIME ACCUMULATION ===
    float dirt_factor = calculate_dirt(world_pos, dirt_intensity, dirt_height_falloff, dirt_corner_intensity, weathering_noise);

    // Per-building variation in weathering amount
    float building_weather_mult = 0.6 + hash11(building_seed * 3.7) * 0.8;
    dirt_factor *= building_weather_mult;

    // === WATER DAMAGE STREAKS ===
    float streak_mask = calculate_water_streaks(world_pos, water_streak_scale, water_streak_intensity, water_streak_sparsity, building_seed, detail_noise);

    // === RUST STAINS ===
    float rust_factor = calculate_rust(world_pos, rust_intensity, rust_drip_length, building_seed, floor_height, ground_floor_height, rust_chance, weathering_noise);

    // === ROUGHNESS VARIATION ===
    float final_roughness = calculate_worn_roughness(base_roughness, roughness_variation, worn_roughness_boost, dirt_factor, detail_noise, world_pos);

    // === FINAL COLOR COMPOSITION ===
    vec3 final_color = albedo;

    // Apply dirt (darkens) - primary weathering effect
    final_color = mix(final_color, dirt_color, dirt_factor);

    // Apply water stains (darkening from drips)
    final_color = mix(final_color, water_stain_color, streak_mask * water_streak_strength);

    // Apply rust (warm orange-brown tint, sparse)
    final_color = mix(final_color, rust_color, rust_factor * rust_strength);

    ALBEDO = final_color;
    ROUGHNESS = final_roughness;
    METALLIC = metallic;
}
