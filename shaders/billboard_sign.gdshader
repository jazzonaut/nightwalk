shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled;

#include "includes/hash_functions.gdshaderinc"

// Sign colors - per-instance
instance uniform vec3 primary_color : source_color = vec3(0.0, 1.0, 1.0);
instance uniform vec3 secondary_color : source_color = vec3(1.0, 0.0, 0.5);
uniform vec3 frame_color : source_color = vec3(0.15, 0.15, 0.2);
instance uniform float emission_strength : hint_range(1.0, 20.0) = 6.0;

// Billboard style - per-instance
instance uniform float billboard_seed : hint_range(0.0, 1000.0) = 0.0;
uniform bool has_frame = true;
uniform float frame_width : hint_range(0.02, 0.15) = 0.04;

// Animation (slower for atmospheric feel)
uniform bool animate = true;
uniform float scroll_speed : hint_range(0.0, 2.0) = 0.15;

// Animation style - per-instance
instance uniform int animation_style : hint_range(0, 4) = 0;

// Broken state - per-instance
instance uniform bool is_broken = false;
instance uniform float broken_intensity : hint_range(0.0, 1.0) = 0.5;

// Parallax layer info - per-instance
instance uniform int current_layer = 0;
instance uniform int total_layers = 1;

// Broken billboard display effects
vec3 apply_broken_effects(vec3 color, float emission, vec2 content_uv, float time) {
    if (!is_broken) return color * emission;

    vec3 result = color;

    // Scanline effect (CRT-like) - more visible
    float scanline = 0.7 + 0.3 * step(0.5, fract(content_uv.y * 40.0 + time * 3.0));
    result *= mix(1.0, scanline, broken_intensity * 0.8);

    // Horizontal glitch bands (more frequent)
    float glitch_time = floor(time * 8.0);
    float glitch_show = step(0.7, hash11(glitch_time + billboard_seed));
    float glitch_y = hash11(glitch_time * 0.789 + billboard_seed);
    float glitch_band = step(abs(content_uv.y - glitch_y), 0.06 + hash11(glitch_time) * 0.04);
    result = mix(result, vec3(0.0), glitch_band * glitch_show * broken_intensity);

    // Color channel separation (chromatic aberration glitch) - more frequent
    if (hash11(floor(time * 5.0) + billboard_seed * 2.0) < broken_intensity * 0.4) {
        float shift = 0.05 * broken_intensity;
        result.r *= 1.0 + shift * 3.0;
        result.b *= 1.0 - shift * 2.0;
    }

    // Random brightness drops - more frequent
    float dropout = hash11(floor(time * 10.0) + billboard_seed * 3.0);
    if (dropout < broken_intensity * 0.25) {
        result *= 0.05;
    }

    // Color drift effect - swap color channels occasionally
    if (hash11(floor(time * 2.0) + billboard_seed * 4.0) < broken_intensity * 0.2) {
        result = result.gbr; // Swap channels for color distortion
    }

    // Rolling blackout bands - sinusoidal horizontal bands
    float roll_phase = content_uv.y * 15.0 + time * 4.0;
    float roll_band = sin(roll_phase);
    if (roll_band > (1.0 - broken_intensity * 0.6)) {
        result *= 0.1;
    }

    // Vertical tear/offset glitch
    if (hash11(floor(time * 4.0) + billboard_seed * 5.0) < broken_intensity * 0.15) {
        float tear_y = hash11(floor(time * 4.0) * 0.5 + billboard_seed);
        if (abs(content_uv.y - tear_y) < 0.08) {
            // Shift the color based on position to simulate horizontal tear
            result *= 0.5 + 0.5 * sin(content_uv.x * 30.0);
        }
    }

    return result * emission;
}

void fragment() {
    vec2 uv = UV;
    uv.x = 1.0 - uv.x; // Flip for front-facing

    vec3 final_color = vec3(0.0);
    float final_emission = 0.0;

    // Determine billboard style from seed (10 different styles)
    float style = hash11(billboard_seed);
    int billboard_type = int(style * 10.0); // 0-9 different styles

    // Frame check
    bool in_frame = false;
    if (has_frame) {
        in_frame = uv.x < frame_width || uv.x > 1.0 - frame_width ||
                   uv.y < frame_width || uv.y > 1.0 - frame_width;
    }

    if (in_frame) {
        // Dark metallic frame
        final_color = frame_color;
        final_emission = 0.0;
    } else {
        // Content area UV (normalized within frame)
        vec2 content_uv = (uv - frame_width) / (1.0 - 2.0 * frame_width);

        // Get animation offset based on style
        float anim_offset = 0.0;
        if (animate) {
            if (animation_style == 0) {
                anim_offset = TIME * scroll_speed; // Horizontal scroll
            } else if (animation_style == 1) {
                anim_offset = TIME * scroll_speed * 0.7; // Vertical scroll
            } else if (animation_style == 2) {
                anim_offset = TIME * 0.5; // Color cycle
            } else if (animation_style == 3) {
                anim_offset = floor(TIME * 3.0); // Segment flash
            } else {
                anim_offset = TIME * 1.5; // Pulse
            }
        }

        if (billboard_type == 0) {
            // Horizontal stripes
            float stripe_count = 4.0 + hash11(billboard_seed * 2.0) * 4.0;
            float stripe = step(0.5, fract(content_uv.y * stripe_count));
            final_color = mix(primary_color, secondary_color, stripe);
            final_emission = emission_strength;

        } else if (billboard_type == 1) {
            // Gradient background with logo shape
            float gradient = content_uv.y;
            vec3 bg_color = mix(primary_color * 0.3, secondary_color * 0.3, gradient);

            // Central "logo" circle or diamond
            vec2 center = content_uv - 0.5;
            float logo_shape = hash11(billboard_seed * 3.0) > 0.5 ?
                length(center) : // Circle
                abs(center.x) + abs(center.y); // Diamond

            float logo_size = 0.2 + hash11(billboard_seed * 4.0) * 0.15;
            float logo = smoothstep(logo_size + 0.02, logo_size, logo_shape);

            final_color = mix(bg_color, primary_color, logo);
            final_emission = mix(emission_strength * 0.4, emission_strength, logo);

        } else if (billboard_type == 2) {
            // Scrolling text bars (horizontal lines that scroll)
            float scroll_offset = anim_offset;
            float bar_y = fract(content_uv.y * 6.0 + scroll_offset);

            // Create text-like blocks
            float block_x = floor(content_uv.x * 12.0);
            float block_seed = hash21(vec2(block_x, floor(content_uv.y * 6.0 + scroll_offset)));

            float text_block = step(0.3, block_seed) * step(bar_y, 0.7) * step(0.15, bar_y);
            final_color = mix(secondary_color * 0.2, primary_color, text_block);
            final_emission = mix(emission_strength * 0.3, emission_strength, text_block);

        } else if (billboard_type == 3) {
            // Grid of dots/pixels
            float grid_size = 8.0 + hash11(billboard_seed * 5.0) * 8.0;
            vec2 grid_uv = fract(content_uv * grid_size);
            vec2 grid_id = floor(content_uv * grid_size);

            float dot_on = step(0.4, hash21(grid_id + billboard_seed));
            float dot_radius = 0.35;
            float dot = 1.0 - smoothstep(dot_radius - 0.1, dot_radius, length(grid_uv - 0.5));

            vec3 dot_color = hash21(grid_id * 0.5 + billboard_seed) > 0.5 ? primary_color : secondary_color;
            final_color = dot_color * dot * dot_on;
            final_emission = emission_strength * dot * dot_on;

        } else if (billboard_type == 4) {
            // Vertical bars with varying brightness
            float bar_count = 6.0 + hash11(billboard_seed * 6.0) * 6.0;
            float bar_id = floor(content_uv.x * bar_count);
            float bar_brightness = 0.3 + hash11(bar_id + billboard_seed) * 0.7;

            // Animate brightness
            if (animate) {
                bar_brightness *= 0.8 + 0.2 * sin(TIME * 2.0 + bar_id);
            }

            vec3 bar_color = hash11(bar_id * 0.3 + billboard_seed) > 0.5 ? primary_color : secondary_color;
            final_color = bar_color * bar_brightness;
            final_emission = emission_strength * bar_brightness;

        } else if (billboard_type == 5) {
            // Neon outline - glowing border with dark center
            float border_dist = min(min(content_uv.x, 1.0 - content_uv.x),
                                   min(content_uv.y, 1.0 - content_uv.y));
            float outline = smoothstep(0.18, 0.08, border_dist);
            float inner_glow = smoothstep(0.35, 0.08, border_dist) * 0.4;

            // Pulsing animation
            float pulse = animate ? 0.8 + 0.2 * sin(TIME * 2.5) : 1.0;

            final_color = mix(primary_color, secondary_color, content_uv.y) * (outline + inner_glow);
            final_emission = emission_strength * (outline * pulse + inner_glow * 0.5);

        } else if (billboard_type == 6) {
            // Checkerboard with animation
            float check_size = 4.0 + hash11(billboard_seed * 7.0) * 3.0;
            vec2 check_id = floor(content_uv * check_size);
            float checker = mod(check_id.x + check_id.y, 2.0);

            // Animated on/off for each square
            float anim_state = animate ? step(0.5, fract(TIME * 0.8 + checker * 0.5 + hash21(check_id) * 0.3)) : 1.0;

            final_color = mix(primary_color, secondary_color, checker) * anim_state;
            final_emission = emission_strength * mix(0.4, 1.0, anim_state);

        } else if (billboard_type == 7) {
            // Vertical bars sweep - bars light up sequentially
            float bar_count = 8.0 + hash11(billboard_seed * 8.0) * 6.0;
            float bar_id = floor(content_uv.x * bar_count);
            float bar_norm = bar_id / bar_count;

            // Sweeping highlight
            float sweep_pos = animate ? fract(TIME * 0.4) : 0.5;
            float sweep_dist = abs(bar_norm - sweep_pos);
            float sweep_brightness = smoothstep(0.25, 0.0, sweep_dist);

            float base_brightness = 0.15 + hash11(bar_id + billboard_seed) * 0.2;
            float total_brightness = base_brightness + sweep_brightness * 0.85;

            vec3 bar_color = mix(secondary_color, primary_color, sweep_brightness);
            final_color = bar_color * total_brightness;
            final_emission = emission_strength * total_brightness;

        } else if (billboard_type == 8) {
            // Static noise / TV snow effect
            float noise_scale = 30.0 + hash11(billboard_seed * 9.0) * 20.0;
            vec2 noise_uv = content_uv * noise_scale;

            // Animated noise
            float time_seed = animate ? floor(TIME * 15.0) : 0.0;
            float noise = hash21(floor(noise_uv) + time_seed + billboard_seed);

            // Vary brightness
            float brightness = noise * (0.5 + 0.5 * noise);

            // Occasional color tint
            vec3 noise_color = mix(vec3(1.0), primary_color, step(0.85, noise));
            final_color = noise_color * brightness;
            final_emission = emission_strength * brightness * 0.7;

        } else {
            // Heartbeat pulse - expanding ring pattern
            vec2 center = content_uv - 0.5;
            float dist = length(center);

            // Multiple rings expanding outward
            float ring_speed = animate ? TIME * 1.2 : 0.0;
            float ring1 = smoothstep(0.02, 0.0, abs(dist - fract(ring_speed) * 0.6));
            float ring2 = smoothstep(0.02, 0.0, abs(dist - fract(ring_speed + 0.5) * 0.6)) * 0.6;

            // Central dot
            float center_dot = smoothstep(0.08, 0.03, dist);

            float total = ring1 + ring2 + center_dot;
            final_color = mix(secondary_color, primary_color, center_dot) * total;
            final_emission = emission_strength * total;
        }
    }

    // Flicker effect for whole sign (subtle)
    float flicker = 1.0;
    if (animate && hash11(billboard_seed * 7.0) < 0.15) {
        flicker = 0.92 + 0.08 * sin(TIME * 6.0 + billboard_seed);
    }

    // Layer opacity (back layers slightly dimmer)
    float layer_opacity = 1.0 - float(current_layer) * 0.12;

    // Apply broken display effects (reuse content_uv calculated at line 98)
    vec3 emission_result;
    if (in_frame) {
        emission_result = vec3(0.0);
    } else {
        // content_uv is already computed in the else branch at line 98
        vec2 broken_uv = (uv - frame_width) / (1.0 - 2.0 * frame_width);
        emission_result = apply_broken_effects(final_color, final_emission * flicker * layer_opacity, broken_uv, TIME);
    }

    // Black albedo to look unlit, all color from emission
    ALBEDO = vec3(0.0);
    EMISSION = emission_result;
}
