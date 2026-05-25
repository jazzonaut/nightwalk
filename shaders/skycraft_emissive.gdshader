shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque;

// Profile accent baked at construction (per ADR-0016: profile = silhouette + accent colour).
uniform vec3 base_emission : source_color = vec3(0.0, 1.0, 1.0);
uniform float energy : hint_range(0.0, 16.0) = 4.5;

// MultiMesh per-instance hue jitter rides on COLOR when MultiMesh.use_colors = true.
// COLOR is a *delta multiplier* against base_emission, not the final color — the renderer
// writes (tinted_accent / base_accent) so that base_emission * COLOR yields the intended
// jittered color. For non-MultiMesh (near-tier MeshInstance3D) COLOR defaults to white,
// so emission resolves to base_emission unchanged.
varying vec3 instance_tint;

void vertex() {
    instance_tint = COLOR.rgb;
}

void fragment() {
    // Silhouette softening: thin emissive trim strips are 1-2 pixels wide and bloom + TAA
    // amplify their hard HDR/dark edges into firefly shimmer. Fading emission at glancing
    // angles smooths the bloom integral. Floor at 0.4 so the trim never reads as completely
    // dark.
    float face = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
    float silhouette = 0.4 + 0.6 * pow(face, 0.8);
    EMISSION = base_emission * instance_tint * energy * silhouette;
    ALBEDO = vec3(0.0);
}
