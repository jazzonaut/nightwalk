shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "includes/wet_surface_common.gdshaderinc"

// Base surface properties (lighter than road)
uniform vec3 albedo : source_color = vec3(0.35, 0.35, 0.38);
uniform float base_roughness : hint_range(0.0, 1.0) = 0.85;

// Curb properties
uniform vec3 curb_color : source_color = vec3(0.32, 0.30, 0.28);
uniform float curb_roughness : hint_range(0.0, 1.0) = 0.25;
uniform float curb_width : hint_range(0.1, 2.0) = 0.5;

// Wetness parameters
uniform float wetness : hint_range(0.0, 1.0) = 1.0;
uniform float wet_roughness : hint_range(0.0, 1.0) = 0.05;
uniform float wet_darkening : hint_range(0.0, 0.5) = 0.25;

// Texture variation
uniform float texture_variation : hint_range(0.0, 0.5) = 0.12;
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
uniform float ripple_strength : hint_range(0.0, 0.5) = 0.25;
uniform sampler2D ripple_normal : hint_normal;

// Wetness variation
uniform float wetness_noise_scale : hint_range(0.01, 0.2) = 0.06;
uniform float wetness_noise_strength : hint_range(0.0, 0.4) = 0.15;

// Time scaling
uniform float time_scale : hint_range(0.1, 2.0) = 1.0;

varying vec3 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    // UV.y < 0 means side face
    // UV = (0-1, 0-1) for top face, UV2 = plot dimensions
    // COLOR = edge flags: R=HasRoadMinX, G=HasRoadMaxX, B=HasRoadMinZ, A=HasRoadMaxZ
    bool is_side_face = UV.y < 0.0;

    // Calculate distance to each edge in world units
    float dist_to_min_x = UV.x * UV2.x;
    float dist_to_max_x = (1.0 - UV.x) * UV2.x;
    float dist_to_min_z = UV.y * UV2.y;
    float dist_to_max_z = (1.0 - UV.y) * UV2.y;

    // Only count edges that have roads (from COLOR vertex attribute)
    float edge_sharpness = curb_width * 0.15;

    // Calculate curb blend for each edge, but only if that edge has a road
    float curb_min_x = COLOR.r > 0.5 ? (1.0 - smoothstep(curb_width - edge_sharpness, curb_width, dist_to_min_x)) : 0.0;
    float curb_max_x = COLOR.g > 0.5 ? (1.0 - smoothstep(curb_width - edge_sharpness, curb_width, dist_to_max_x)) : 0.0;
    float curb_min_z = COLOR.b > 0.5 ? (1.0 - smoothstep(curb_width - edge_sharpness, curb_width, dist_to_min_z)) : 0.0;
    float curb_max_z = COLOR.a > 0.5 ? (1.0 - smoothstep(curb_width - edge_sharpness, curb_width, dist_to_max_z)) : 0.0;

    // Combined curb blend - max of all edges that have roads
    float curb_blend = is_side_face ? 0.0 : max(max(curb_min_x, curb_max_x), max(curb_min_z, curb_max_z));

    // === CURB TEXTURE ===
    vec2 curb_uv = world_pos.xz * 0.3;
    float curb_variation = texture(puddle_noise, curb_uv).r;
    curb_variation = 1.0 + (curb_variation - 0.5) * 0.1;
    vec3 curb_albedo = curb_color * curb_variation;

    // === CONCRETE TEXTURE ===
    vec2 uv_large = world_pos.xz * texture_scale * 0.1;
    vec2 uv_medium = world_pos.xz * texture_scale * 0.4;
    vec2 uv_fine = world_pos.xz * texture_scale * 1.5;

    float large_variation = texture(puddle_noise, uv_large).r;
    float medium_variation = texture(puddle_noise, uv_medium + vec2(0.33, 0.66)).r;
    float fine_variation = texture(puddle_noise, uv_fine + vec2(0.77, 0.22)).r;

    float combined_variation = large_variation * 0.4 + medium_variation * 0.35 + fine_variation * 0.25;
    float surface_variation = 1.0 + (combined_variation - 0.5) * texture_variation * 2.0;

    vec3 surface_color = albedo * surface_variation;

    // Use shared wet surface functions
    float effective_wetness = calculate_effective_wetness(world_pos, wetness, wetness_noise_scale, wetness_noise_strength, puddle_noise);
    float puddle_mask = calculate_puddle_mask(world_pos, puddles_enabled, puddle_scale, puddle_threshold, puddle_edge_softness, puddle_noise);
    float final_roughness = calculate_wet_roughness(base_roughness, wet_roughness, effective_wetness, puddle_mask);
    vec3 final_color = calculate_wet_color(surface_color, wet_darkening, effective_wetness, puddle_mask);
    vec3 final_normal = calculate_ripples(world_pos, ripples_enabled, puddle_mask, ripple_speed, ripple_scale, ripple_strength, time_scale, TIME, ripple_normal);
    float sidewalk_specular = calculate_wet_specular(effective_wetness, puddle_mask);

    // === OUTPUT ===
    // Blend between sidewalk (interior) and curb (edges)
    ALBEDO = mix(final_color, curb_albedo, curb_blend);
    EMISSION = vec3(0.0);
    ROUGHNESS = mix(final_roughness, curb_roughness, curb_blend);
    METALLIC = mix(mix(0.0, 0.15, puddle_mask), 0.05, curb_blend);
    NORMAL_MAP = mix(final_normal, vec3(0.5, 0.5, 1.0), curb_blend);
    SPECULAR = mix(sidewalk_specular, 0.6, curb_blend);
}
