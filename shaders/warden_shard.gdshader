shader_type spatial;
render_mode cull_back, depth_draw_opaque;

// Color uniforms
uniform vec4 base_color : source_color = vec4(0.039, 0.086, 0.157, 1.0); // #0a1628 navy
uniform vec4 bright_color : source_color = vec4(0.0, 1.0, 1.0, 1.0);     // #00ffff cyan
uniform vec4 vulnerable_color : source_color = vec4(1.0, 0.5, 0.0, 1.0); // #ff8000 orange

// Shard properties
uniform float distance_from_core : hint_range(0.0, 1.0) = 0.5;
uniform float vulnerable_blend : hint_range(0.0, 1.0) = 0.0;
uniform float shimmer_speed = 1.5;
uniform float shimmer_intensity : hint_range(0.0, 1.0) = 0.15;

// PBR properties
uniform float metallic : hint_range(0.0, 1.0) = 0.85;
uniform float roughness : hint_range(0.0, 1.0) = 0.15;

// Time uniform for animation
uniform float time = 0.0;

varying vec3 world_vertex;

void vertex() {
    world_vertex = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    // Fresnel edge highlight - stronger glow at glancing angles
    float fresnel = pow(1.0 - abs(dot(NORMAL, VIEW)), 4.0);

    // Shimmer effect using world position and time
    float shimmer = sin(world_vertex.x * 3.0 + world_vertex.y * 2.0 + world_vertex.z * 2.5 + time * shimmer_speed);
    shimmer = shimmer * 0.5 + 0.5; // Normalize to 0-1
    shimmer *= shimmer_intensity;

    // Gradient from base (outer) to bright (inner) based on distance from core
    float gradient_factor = 1.0 - distance_from_core;
    vec3 gradient_color = mix(base_color.rgb, bright_color.rgb, gradient_factor);

    // Add shimmer variation
    gradient_color = mix(gradient_color, bright_color.rgb, shimmer * gradient_factor);

    // Vulnerable state - blend toward orange
    vec3 final_color = mix(gradient_color, vulnerable_color.rgb, vulnerable_blend);

    // Apply fresnel highlight (adds cyan glow at edges)
    vec3 fresnel_color = mix(final_color, bright_color.rgb, fresnel * 0.6);

    // Metallic obsidian look
    ALBEDO = fresnel_color;
    METALLIC = metallic;
    ROUGHNESS = roughness;

    // Emission for glow effect (stronger near core and when vulnerable)
    float emission_strength = mix(gradient_factor * 0.3, 1.0, vulnerable_blend);
    emission_strength += fresnel * 0.5;
    emission_strength += shimmer * 0.2;
    EMISSION = mix(bright_color.rgb, vulnerable_color.rgb, vulnerable_blend) * emission_strength;
}
