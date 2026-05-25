shader_type canvas_item;

// Screen texture
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

// === Color Grading (Cool shadows / Warm highlights) ===
uniform bool color_grading_enabled = true;
uniform vec3 shadow_tint : source_color = vec3(0.85, 0.9, 1.0);
uniform vec3 highlight_tint : source_color = vec3(1.0, 0.95, 0.85);
uniform float color_grading_strength : hint_range(0.0, 1.0) = 0.25;
uniform float contrast : hint_range(0.5, 1.5) = 1.05;
uniform float saturation : hint_range(0.0, 2.0) = 1.08;

// === Vignette ===
uniform bool vignette_enabled = true;
uniform float vignette_intensity : hint_range(0.0, 1.0) = 0.35;
uniform float vignette_softness : hint_range(0.0, 1.0) = 0.45;
uniform vec3 vignette_color : source_color = vec3(0.0, 0.0, 0.03);

// === Film Grain ===
uniform bool film_grain_enabled = true;
uniform float film_grain_intensity : hint_range(0.0, 0.5) = 0.06;
uniform float film_grain_speed : hint_range(0.1, 10.0) = 2.5;

// === Chromatic Aberration ===
uniform bool chromatic_aberration_enabled = true;
uniform float chromatic_aberration_strength : hint_range(0.0, 0.02) = 0.002;
uniform float chromatic_aberration_falloff : hint_range(0.0, 2.0) = 1.2;

// === Speed Effects ===
uniform bool speed_effects_enabled = true;
uniform float speed_factor : hint_range(0.0, 1.0) = 0.0;  // 0 = stopped, 1 = max speed
uniform float radial_blur_max_strength : hint_range(0.0, 0.05) = 0.015;
uniform int radial_blur_samples : hint_range(4, 16) = 5;
uniform float speed_chromatic_boost : hint_range(0.0, 0.02) = 0.008;
uniform float speed_vignette_boost : hint_range(0.0, 0.5) = 0.3;
uniform float speed_fov_lines_intensity : hint_range(0.0, 0.3) = 0.15;

// Time for animated effects
uniform float time : hint_range(0.0, 1000000.0) = 0.0;

float luminance(vec3 color) {
    return dot(color, vec3(0.2126, 0.7152, 0.0722));
}

float hash(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

float grain(vec2 uv, float t) {
    float noise = hash(uv * 1000.0 + t * film_grain_speed);
    return (noise - 0.5) * 2.0;
}

// Speed lines effect - radial streaks from center
float speed_lines(vec2 uv, float t) {
    vec2 centered = uv - vec2(0.5);
    float angle = atan(centered.y, centered.x);
    float dist = length(centered);

    // Create radial lines using angle
    float lines = sin(angle * 60.0 + t * 5.0) * 0.5 + 0.5;
    lines = pow(lines, 3.0);  // Sharpen the lines

    // Fade in from edges, stronger at screen edges
    float edge_fade = smoothstep(0.2, 0.7, dist);

    return lines * edge_fade;
}

void fragment() {
    vec2 uv = SCREEN_UV;
    vec2 center_offset = uv - vec2(0.5);
    float dist_from_center = length(center_offset);

    // Calculate speed-adjusted parameters
    float effective_speed = speed_effects_enabled ? speed_factor : 0.0;
    float blur_strength = radial_blur_max_strength * effective_speed * effective_speed;  // Quadratic for smooth ramp
    float chromatic_boost = speed_chromatic_boost * effective_speed;

    // === Radial Blur (Speed-based) ===
    vec3 color = vec3(0.0);
    vec2 blur_direction = normalize(center_offset + vec2(0.0001));

    if (effective_speed > 0.4 && blur_strength > 0.001) {
        // Sample along radial direction for motion blur
        float total_weight = 0.0;
        for (int i = 0; i < radial_blur_samples; i++) {
            float t = float(i) / float(radial_blur_samples - 1);
            float offset = (t - 0.5) * blur_strength * dist_from_center;
            vec2 sample_uv = uv + blur_direction * offset;

            // Weight samples by distance from center (more blur at edges)
            float weight = 1.0 - abs(t - 0.5) * 0.5;
            color += texture(screen_texture, sample_uv).rgb * weight;
            total_weight += weight;
        }
        color /= total_weight;
    } else {
        color = texture(screen_texture, uv).rgb;
    }

    // === Chromatic Aberration (with speed boost) ===
    if (chromatic_aberration_enabled) {
        float aberration = (chromatic_aberration_strength + chromatic_boost) * pow(dist_from_center, chromatic_aberration_falloff);

        float r = texture(screen_texture, uv + blur_direction * aberration).r;
        float g = color.g;  // Keep center channel from blur result
        float b = texture(screen_texture, uv - blur_direction * aberration).b;
        color = vec3(r, g, b);
    }

    // === Color Grading ===
    if (color_grading_enabled) {
        float lum = luminance(color);

        float shadow_weight = 1.0 - smoothstep(0.0, 0.5, lum);
        float highlight_weight = smoothstep(0.5, 1.0, lum);

        vec3 tinted = color;
        tinted = mix(tinted, tinted * shadow_tint, shadow_weight * color_grading_strength);
        tinted = mix(tinted, tinted * highlight_tint, highlight_weight * color_grading_strength);

        tinted = (tinted - 0.5) * contrast + 0.5;

        float gray = luminance(tinted);
        tinted = mix(vec3(gray), tinted, saturation);

        color = tinted;
    }

    // === Film Grain ===
    if (film_grain_enabled) {
        float noise = grain(uv, time);
        float lum = luminance(color);
        float grain_mask = 1.0 - abs(lum - 0.5) * 2.0;
        grain_mask = max(grain_mask, 0.3);
        color += vec3(noise * film_grain_intensity * grain_mask);
    }

    // === Vignette (with speed boost for tunnel vision) ===
    if (vignette_enabled) {
        float speed_vignette = vignette_intensity + (speed_vignette_boost * effective_speed);
        float vignette = 1.0 - smoothstep(vignette_softness - effective_speed * 0.15, 1.0, dist_from_center * 2.0);
        vignette = pow(vignette, 1.0 + speed_vignette);
        color = mix(vignette_color, color, vignette);
    }

    // === Speed Lines (radial streaks at high speed) ===
    if (speed_effects_enabled && effective_speed > 0.5) {
        float lines = speed_lines(uv, time);
        float line_intensity = speed_fov_lines_intensity * (effective_speed - 0.5) * 2.0;  // Only visible above 50% speed
        color += vec3(lines * line_intensity);
    }

    COLOR = vec4(clamp(color, 0.0, 1.0), 1.0);
}
