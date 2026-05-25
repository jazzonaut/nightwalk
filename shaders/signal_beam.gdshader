shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform vec4 beam_color : source_color = vec4(0.0, 1.0, 1.0, 1.0);
uniform float intensity : hint_range(0.0, 10.0) = 2.0;

void fragment() {
    ALBEDO = beam_color.rgb;
    ALPHA = intensity * beam_color.a;
}
