shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform vec4 beam_color : source_color = vec4(1.0, 0.8, 0.5, 1.0);
uniform float intensity : hint_range(0.0, 10.0) = 2.0;
uniform float fade_start : hint_range(0.0, 1.0) = 0.1;  // Earlier fade for softer look
uniform float flicker_speed : hint_range(0.0, 10.0) = 2.5;
uniform float flicker_amount : hint_range(0.0, 1.0) = 0.15;

varying float vertex_height;
varying vec3 world_pos;

void vertex() {
    // Pass normalized height (0 = bottom, 1 = top) to fragment
    vertex_height = UV.y;
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    // Fade out towards top of beam - softer gradient
    float height_fade = 1.0 - smoothstep(fade_start, 1.0, vertex_height);
    // Additional soft fade at bottom for less harsh cutoff
    float bottom_fade = smoothstep(0.0, 0.05, vertex_height);

    // Softer edges using fresnel-like effect
    float edge_fade = pow(1.0 - abs(dot(NORMAL, VIEW)), 0.4);

    // Subtle organic flicker using layered noise
    float flicker1 = sin(TIME * flicker_speed * 3.7 + world_pos.y * 0.5) * 0.5 + 0.5;
    float flicker2 = sin(TIME * flicker_speed * 2.3 + world_pos.y * 0.3 + 1.5) * 0.5 + 0.5;
    float flicker = mix(1.0, mix(flicker1, flicker2, 0.5), flicker_amount);

    float alpha = height_fade * bottom_fade * edge_fade * intensity * flicker;
    ALBEDO = beam_color.rgb;
    ALPHA = alpha * beam_color.a;
}
