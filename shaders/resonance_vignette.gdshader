shader_type canvas_item;

uniform float vignette_intensity : hint_range(0.0, 1.0) = 0.0;
uniform vec4 vignette_color : source_color = vec4(0.8, 0.1, 0.1, 1.0);
uniform float pulse_speed = 3.0;

void fragment() {
    vec2 uv = UV - vec2(0.5);
    float dist = length(uv) * 1.4142;

    // Pulsing effect when active
    float pulse = 1.0 + 0.15 * sin(TIME * pulse_speed) * vignette_intensity;

    // Vignette falloff
    float vignette = smoothstep(0.3, 1.0, dist * pulse);
    vignette = pow(vignette, 1.5);

    // Apply color and intensity
    float alpha = vignette * vignette_intensity * 0.7;
    COLOR = vec4(vignette_color.rgb, alpha);
}
