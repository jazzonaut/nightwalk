shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded;

uniform sampler2D smoke_texture : source_color;
uniform vec3 smoke_color = vec3(0.7, 0.7, 0.75);
uniform float fade_distance_min = 80.0;
uniform float fade_distance_max = 120.0;

varying float vertex_distance;

void vertex() {
    // Billboard
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        vec4(normalize(cross(vec3(0.0, 1.0, 0.0), INV_VIEW_MATRIX[2].xyz)), 0.0),
        vec4(0.0, 1.0, 0.0, 0.0),
        vec4(normalize(cross(INV_VIEW_MATRIX[0].xyz, vec3(0.0, 1.0, 0.0))), 0.0),
        MODEL_MATRIX[3]
    );
    MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
        vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
        vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
        vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
        vec4(0.0, 0.0, 0.0, 1.0)
    );

    vertex_distance = length((VIEW_MATRIX * MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz);
}

void fragment() {
    vec4 tex = texture(smoke_texture, UV);
    float distance_fade = 1.0 - smoothstep(fade_distance_min, fade_distance_max, vertex_distance);
    ALBEDO = smoke_color * tex.rgb;
    ALPHA = tex.a * COLOR.a * 0.4 * distance_fade;
}
