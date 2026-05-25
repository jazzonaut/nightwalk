shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;

uniform vec4 fill_color : source_color = vec4(0.2, 1.0, 0.8, 0.8);
uniform vec4 bg_color : source_color = vec4(0.1, 0.3, 0.3, 0.3);
uniform float fill_amount : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    // Use UV.x as angle around the ring (0 to 1)
    float angle = UV.x;

    // Fill from 0 to fill_amount
    vec4 color = angle < fill_amount ? fill_color : bg_color;

    ALBEDO = color.rgb;
    ALPHA = color.a;
    EMISSION = fill_color.rgb * fill_amount * 0.5;
}
