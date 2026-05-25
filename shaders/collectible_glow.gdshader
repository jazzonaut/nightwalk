shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back;

uniform vec4 glow_color : source_color = vec4(1.0, 0.4, 1.0, 1.0);
uniform float emission_strength : hint_range(0.0, 20.0) = 8.0;
uniform float proximity_alpha : hint_range(0.0, 1.0) = 1.0;
uniform float pulse_speed : hint_range(0.0, 5.0) = 2.0;
uniform float pulse_amount : hint_range(0.0, 0.5) = 0.15;
uniform float time_offset : hint_range(0.0, 100.0) = 0.0;
uniform float metallic : hint_range(0.0, 1.0) = 0.9;
uniform float roughness : hint_range(0.0, 1.0) = 0.1;

void fragment() {
    // Subtle pulsing effect
    float pulse = 1.0 + sin(TIME * pulse_speed + time_offset) * pulse_amount;

    // Fresnel - strong at edges, weak at center
    float fresnel = pow(1.0 - abs(dot(NORMAL, VIEW)), 3.0);

    // Emission concentrated at edges (fresnel), center shows metallic
    float center_mask = 1.0 - fresnel;
    float edge_emission = fresnel * 2.0;

    // Bright metallic base color
    ALBEDO = glow_color.rgb * (0.6 + center_mask * 0.3);

    // Emission stronger at edges, weaker in center to show metallic
    EMISSION = glow_color.rgb * emission_strength * pulse * (0.3 + edge_emission);

    ALPHA = proximity_alpha;
    ROUGHNESS = roughness;
    METALLIC = metallic;

    // Add some specular to make it shinier
    SPECULAR = 0.8;
}
