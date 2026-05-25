shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, unshaded;

// Silhouette atlas texture (4x4 grid = 16 silhouettes)
uniform sampler2D silhouette_atlas : source_color, filter_linear, repeat_disable;

// Silhouette properties (no window background - overlays existing lit windows)
uniform float silhouette_opacity : hint_range(0.0, 1.0) = 0.7;
uniform float blur_radius : hint_range(0.0, 0.2) = 0.06;
uniform int blur_samples : hint_range(1, 16) = 7;

// Animation speeds
uniform float fade_speed : hint_range(0.1, 5.0) = 1.2;
uniform float walk_speed : hint_range(0.1, 10.0) = 2.0;
uniform float drift_speed : hint_range(0.01, 1.0) = 0.15;

// Per-vertex data from UV channels
// UV.x = atlas index (0-15 encoded as 0.0-1.0)
// UV.y = animation type (0=static, 0.33=fade, 0.67=walk, 1.0=drift)
// UV2.x = animation seed (timing variation)
// UV2.y = animation phase offset
// COLOR.xy = local quad UV (0,0 to 1,1)
// COLOR.z = blur multiplier (depth from window: low=sharp/close, high=fuzzy/far)
varying vec2 silhouette_data;
varying vec2 animation_data;
varying vec2 quad_uv;
varying float blur_mult;

// Atlas constants
const float ATLAS_SIZE = 4.0;  // 4x4 grid
const float CELL_SIZE = 1.0 / 4.0;  // 0.25

void vertex() {
    silhouette_data = UV;
    animation_data = UV2;
    // Local quad UV passed through COLOR attribute
    quad_uv = COLOR.xy;
    // Blur multiplier based on silhouette depth from window
    blur_mult = COLOR.z;
}

// Sample silhouette from atlas with offset
float sample_silhouette(vec2 local_uv, int atlas_index) {
    // Calculate atlas cell position
    int cell_x = atlas_index % 4;
    int cell_y = atlas_index / 4;

    // Transform local UV to atlas UV
    vec2 atlas_uv = vec2(
        (float(cell_x) + local_uv.x) * CELL_SIZE,
        (float(cell_y) + local_uv.y) * CELL_SIZE
    );

    // Clamp to cell bounds to prevent bleeding
    vec2 cell_min = vec2(float(cell_x), float(cell_y)) * CELL_SIZE;
    vec2 cell_max = cell_min + vec2(CELL_SIZE);
    atlas_uv = clamp(atlas_uv, cell_min + 0.001, cell_max - 0.001);

    return texture(silhouette_atlas, atlas_uv).r;
}

// Multi-sample blur for soft edges
float sample_silhouette_blurred(vec2 local_uv, int atlas_index, float radius) {
    if (radius <= 0.001 || blur_samples <= 1) {
        return sample_silhouette(local_uv, atlas_index);
    }

    float total = 0.0;
    float weight_sum = 0.0;

    // Gaussian-approximated blur using concentric samples
    for (int i = 0; i < blur_samples; i++) {
        for (int j = 0; j < blur_samples; j++) {
            vec2 offset = vec2(
                float(i - blur_samples / 2) / float(blur_samples) * radius * 2.0,
                float(j - blur_samples / 2) / float(blur_samples) * radius * 2.0
            );

            // Gaussian weight based on distance
            float dist = length(offset);
            float weight = exp(-dist * dist / (radius * radius * 0.5));

            total += sample_silhouette(local_uv + offset, atlas_index) * weight;
            weight_sum += weight;
        }
    }

    return total / weight_sum;
}

void fragment() {
    // Decode atlas index (0-14, 15 types)
    int atlas_index = int(silhouette_data.x * 14.0 + 0.5);

    // Decode animation type (0=static, 1=fade, 2=walk, 3=drift)
    int anim_type = int(silhouette_data.y * 3.0 + 0.1);

    float anim_seed = animation_data.x;
    float anim_phase = animation_data.y;

    // Calculate animated UV offset
    vec2 uv_offset = vec2(0.0);
    float opacity_mult = 1.0;

    float time_offset = TIME + anim_seed * 10.0 + anim_phase * 6.28;

    if (anim_type == 1) {
        // Fade animation - pulse opacity (appear/disappear)
        float fade_cycle = sin(time_offset * fade_speed);
        opacity_mult = smoothstep(-0.3, 0.5, fade_cycle);
    } else if (anim_type == 2) {
        // Walk animation - horizontal movement across window
        float walk_cycle = fract(time_offset * walk_speed * 0.05);
        // Ping-pong motion within window bounds
        float walk_pos = walk_cycle < 0.5 ? walk_cycle * 2.0 : 2.0 - walk_cycle * 2.0;
        uv_offset.x = (walk_pos - 0.5) * 0.5;  // ±25% of window width
    } else if (anim_type == 3) {
        // Drift animation - subtle swaying motion
        uv_offset.x = sin(time_offset * drift_speed * 6.28) * 0.08;
        uv_offset.y = cos(time_offset * drift_speed * 4.0) * 0.03;
    }
    // anim_type == 0 is static, no animation

    // Apply UV offset and sample silhouette
    // Flip Y because atlas has Y=0 at top, but quad UV has Y=0 at bottom
    vec2 sample_uv = vec2(quad_uv.x, 1.0 - quad_uv.y) + uv_offset;

    // Clamp to valid range with margin
    sample_uv = clamp(sample_uv, vec2(0.05), vec2(0.95));

    // Sample with blur (multiplied by per-silhouette depth factor)
    float effective_blur = blur_radius * blur_mult;
    float silhouette = sample_silhouette_blurred(sample_uv, atlas_index, effective_blur);

    // Final silhouette opacity - dark overlay on existing lit windows
    float final_alpha = silhouette * silhouette_opacity * opacity_mult;

    // Render as dark transparent overlay
    // Where silhouette exists: darken the background (black with alpha)
    // Where no silhouette: fully transparent (shows lit window through)
    ALBEDO = vec3(0.0);
    ALPHA = final_alpha;
}

