shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;

uniform vec4 cone_color : source_color = vec4(1.0, 0.0, 0.25, 0.5);
uniform float intensity : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    // Gradient from center to edge
    float edge_dist = length(UV - 0.5) * 2.0;
    float alpha = (1.0 - edge_dist) * intensity * 0.3;

    ALBEDO = cone_color.rgb;
    ALPHA = alpha;
    EMISSION = cone_color.rgb * intensity * 0.5;
}
