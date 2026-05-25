shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform vec4 particle_color : source_color = vec4(0.5, 0.7, 1.0, 1.0);
uniform float glow_intensity : hint_range(0.0, 5.0) = 1.5;
uniform float pulse_speed : hint_range(0.0, 10.0) = 2.0;
uniform float pulse_amount : hint_range(0.0, 1.0) = 0.3;

void vertex() {
    // Billboard: make particle always face camera
    mat4 mat_world = mat4(
        normalize(INV_VIEW_MATRIX[0]) * length(MODEL_MATRIX[0]),
        normalize(INV_VIEW_MATRIX[1]) * length(MODEL_MATRIX[1]),
        normalize(INV_VIEW_MATRIX[2]) * length(MODEL_MATRIX[2]),
        MODEL_MATRIX[3]
    );
    MODELVIEW_MATRIX = VIEW_MATRIX * mat_world;
}

void fragment() {
    // Soft circular falloff (not hard rectangle edges)
    vec2 uv_centered = UV * 2.0 - 1.0;
    float dist = length(uv_centered);
    float soft_circle = 1.0 - smoothstep(0.3, 1.0, dist);

    // Core glow (brighter center)
    float core = 1.0 - smoothstep(0.0, 0.4, dist);

    // Subtle size pulsing per particle (using vertex color alpha for variation)
    float pulse = 1.0 + sin(TIME * pulse_speed + COLOR.a * 6.28) * pulse_amount;

    // Combined glow with soft edges
    float glow = (soft_circle + core * 0.5) * glow_intensity * pulse;

    ALBEDO = particle_color.rgb;
    ALPHA = glow * COLOR.a * particle_color.a;
}
