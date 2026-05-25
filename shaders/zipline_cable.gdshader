shader_type spatial;
render_mode unshaded, cull_disabled;

uniform float hue_offset : hint_range(0.0, 1.0) = 0.5;
uniform float hue_range : hint_range(0.1, 0.5) = 0.16;
uniform float saturation : hint_range(0.5, 1.0) = 0.8;
uniform float brightness : hint_range(0.3, 0.8) = 0.5;
uniform float emission_strength : hint_range(0.5, 2.0) = 1.0;
uniform vec3 highlight_color : source_color = vec3(1.0, 0.5, 0.0);
uniform float highlight_mix : hint_range(0.0, 1.0) = 0.0;

// HSV to RGB conversion
vec3 hsv_to_rgb(float h, float s, float v) {
    vec3 c = vec3(h, s, v);
    vec3 rgb = clamp(abs(mod(c.x * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
    return c.z * mix(vec3(1.0), rgb, c.y);
}

void fragment() {
    // View-based iridescence (like oil on water or metallic paint)
    float fresnel = 1.0 - abs(dot(NORMAL, VIEW));

    // Shift hue based on viewing angle
    float hue = hue_offset + fresnel * hue_range;

    // Brighter at glancing angles (fresnel effect)
    float view_brightness = brightness + fresnel * 0.3;

    vec3 iridescent_color = hsv_to_rgb(hue, saturation, view_brightness);

    // Blend with highlight color when highlighted
    vec3 color = mix(iridescent_color, highlight_color, highlight_mix);
    float final_emission = mix(emission_strength, 2.0, highlight_mix);

    ALBEDO = color;
    EMISSION = color * final_emission;
}
