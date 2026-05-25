shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled;

#include "includes/hash_functions.gdshaderinc"

// Sign color (neon glow) - per-instance
instance uniform vec3 sign_color : source_color = vec3(1.0, 0.0, 1.0);
uniform vec3 background_color : source_color = vec3(0.02, 0.02, 0.03);
instance uniform float emission_strength : hint_range(1.0, 20.0) = 8.0;

// Glyph parameters - per-instance
instance uniform float glyph_seed : hint_range(0.0, 10000.0) = 0.0;
instance uniform int glyph_columns : hint_range(1, 4) = 1;
uniform bool vertical_text = true;

// Animation
uniform bool flicker_enabled = true;
uniform float flicker_speed : hint_range(0.1, 5.0) = 1.0;

// Broken state - per-instance
instance uniform bool is_broken = false;
instance uniform float broken_intensity : hint_range(0.0, 1.0) = 0.5;

// Electrical arc parameters
uniform int arc_segments : hint_range(3, 8) = 5;
uniform float arc_speed : hint_range(1.0, 20.0) = 8.0;

// Flicker pattern - per-instance
instance uniform int flicker_pattern : hint_range(0, 5) = 0;

// Parallax layer info - per-instance
instance uniform int current_layer = 0;
instance uniform int total_layers = 1;

// Broken sign flicker - irregular, dramatic
float broken_flicker(float time, vec2 cell_id, float seed) {
    if (!is_broken) return 1.0;

    // Random blackouts (whole sign goes dark briefly)
    float blackout_roll = hash11(floor(time * 6.0) + seed);
    if (blackout_roll < broken_intensity * 0.3) return 0.05;

    // Per-cell failures (some glyphs flicker independently)
    float cell_seed = seed + cell_id.x * 13.0 + cell_id.y * 29.0;
    float segment_roll = hash11(cell_seed + floor(time * 4.0));
    if (segment_roll < broken_intensity * 0.35) return 0.1;

    // Irregular brightness noise
    float noise = hash11(time * 10.0 + seed);
    return 0.4 + noise * 0.6;
}

// Draw a single jagged arc segment between two points
float draw_arc_segment(vec2 uv, vec2 p1, vec2 p2, float time, float seed, float thickness) {
    vec2 dir = p2 - p1;
    float len = length(dir);
    if (len < 0.001) return 0.0;

    vec2 norm = normalize(dir);
    vec2 perp = vec2(-norm.y, norm.x);

    // Project UV onto line segment
    vec2 rel = uv - p1;
    float t = clamp(dot(rel, norm) / len, 0.0, 1.0);

    // Jagged offset perpendicular to line (varies along length)
    float jag_freq = 6.0 + hash11(seed) * 10.0;
    float jag_amp = 0.015 + hash11(seed * 1.3) * 0.025;
    float jag_offset = sin(t * jag_freq + time * arc_speed + seed) * jag_amp;
    jag_offset += sin(t * jag_freq * 2.3 + time * arc_speed * 1.7) * jag_amp * 0.5;

    // Point on jagged arc
    vec2 arc_point = p1 + norm * t * len + perp * jag_offset;
    float dist = length(uv - arc_point);

    // Sharp falloff for electric look
    float arc = smoothstep(thickness, thickness * 0.2, dist);

    return arc;
}

// Electrical arc effect for broken signs - realistic branching arcs
vec3 electrical_arc_effect(float time, vec2 uv) {
    if (!is_broken) return vec3(0.0);

    vec3 total_arcs = vec3(0.0);

    // Generate 2-3 active arcs at any time
    for (int arc_idx = 0; arc_idx < 3; arc_idx++) {
        float arc_seed = glyph_seed + float(arc_idx) * 137.0;
        float arc_time = floor(time * 4.0 + float(arc_idx) * 0.4);

        // Only show arc probabilistically based on broken intensity
        float show_chance = hash11(arc_time + arc_seed);
        if (show_chance > broken_intensity * 0.5) continue;

        // Flickering intensity for this arc
        float arc_flicker = 0.4 + 0.6 * hash11(floor(time * 15.0) + arc_seed);

        // Random start point (usually near edges)
        float start_edge = hash11(arc_time * 0.1 + arc_seed);
        vec2 start_pos;
        if (start_edge < 0.25) {
            start_pos = vec2(0.0, hash11(arc_time * 0.2 + arc_seed)); // Left edge
        } else if (start_edge < 0.5) {
            start_pos = vec2(1.0, hash11(arc_time * 0.3 + arc_seed)); // Right edge
        } else if (start_edge < 0.75) {
            start_pos = vec2(hash11(arc_time * 0.4 + arc_seed), 0.0); // Bottom edge
        } else {
            start_pos = vec2(hash11(arc_time * 0.5 + arc_seed), 1.0); // Top edge
        }

        // Random end point (opposite area)
        vec2 end_pos = vec2(
            0.3 + hash11(arc_time * 0.6 + arc_seed) * 0.4,
            0.3 + hash11(arc_time * 0.7 + arc_seed) * 0.4
        );

        // Draw main arc with multiple segments for jaggedness
        float arc_total = 0.0;
        vec2 prev = start_pos;
        for (int seg = 0; seg < 6; seg++) {
            if (seg >= arc_segments) break;

            float seg_t = float(seg + 1) / float(arc_segments);
            vec2 next = mix(start_pos, end_pos, seg_t);

            // Add randomness to intermediate points
            if (seg < arc_segments - 1) {
                float rand_offset = 0.08 + hash11(arc_seed + float(seg) * 7.0) * 0.08;
                next += vec2(
                    (hash11(arc_seed + float(seg) * 11.0 + time * 3.0) - 0.5) * rand_offset,
                    (hash11(arc_seed + float(seg) * 13.0 + time * 2.0) - 0.5) * rand_offset
                );
            }

            arc_total += draw_arc_segment(uv, prev, next, time, arc_seed + float(seg), 0.006);
            prev = next;
        }

        // Secondary branching arcs (smaller, 40% chance)
        if (hash11(arc_seed * 2.0) < 0.4) {
            float branch_t = 0.3 + hash11(arc_seed * 3.0) * 0.4;
            vec2 branch_start = mix(start_pos, end_pos, branch_t);
            vec2 branch_end = branch_start + vec2(
                (hash11(arc_seed * 4.0) - 0.5) * 0.25,
                (hash11(arc_seed * 5.0) - 0.5) * 0.2
            );

            vec2 branch_prev = branch_start;
            for (int bseg = 0; bseg < 3; bseg++) {
                float bseg_t = float(bseg + 1) / 3.0;
                vec2 branch_next = mix(branch_start, branch_end, bseg_t);
                arc_total += draw_arc_segment(uv, branch_prev, branch_next, time, arc_seed + 100.0 + float(bseg), 0.004) * 0.5;
                branch_prev = branch_next;
            }
        }

        // Electric blue-white color with slight variation
        vec3 arc_color = mix(vec3(0.6, 0.8, 1.0), vec3(1.0, 1.0, 1.0), arc_total * 0.5);

        // Add core brightness
        float core_glow = arc_total * arc_total * 2.0;
        arc_color += vec3(0.3, 0.4, 0.5) * core_glow;

        total_arcs += arc_color * arc_total * arc_flicker * broken_intensity * 15.0;
    }

    // Add occasional bright flash at random point
    float flash_time = floor(time * 6.0);
    if (hash11(flash_time + glyph_seed * 7.0) < broken_intensity * 0.15) {
        vec2 flash_pos = vec2(
            hash11(flash_time * 0.123 + glyph_seed),
            hash11(flash_time * 0.456 + glyph_seed)
        );
        float flash_dist = length(uv - flash_pos);
        float flash = smoothstep(0.08, 0.0, flash_dist);
        total_arcs += vec3(1.0, 0.95, 0.9) * flash * 10.0;
    }

    return total_arcs;
}

// Smooth line drawing with anti-aliasing
float smooth_line_h(vec2 uv, float y, float x1, float x2, float thickness, float softness) {
    float d = abs(uv.y - y);
    float line = 1.0 - smoothstep(thickness - softness, thickness + softness, d);
    float bounds = smoothstep(x1 - softness, x1 + softness, uv.x) *
                   (1.0 - smoothstep(x2 - softness, x2 + softness, uv.x));
    return line * bounds;
}

float smooth_line_v(vec2 uv, float x, float y1, float y2, float thickness, float softness) {
    float d = abs(uv.x - x);
    float line = 1.0 - smoothstep(thickness - softness, thickness + softness, d);
    float bounds = smoothstep(y1 - softness, y1 + softness, uv.y) *
                   (1.0 - smoothstep(y2 - softness, y2 + softness, uv.y));
    return line * bounds;
}

// Diagonal line from p1 to p2
float smooth_line_diag(vec2 uv, vec2 p1, vec2 p2, float thickness, float softness) {
    vec2 dir = p2 - p1;
    float len = length(dir);
    if (len < 0.001) return 0.0;

    vec2 norm = normalize(dir);

    // Project uv onto line segment
    vec2 rel = uv - p1;
    float t = clamp(dot(rel, norm) / len, 0.0, 1.0);
    vec2 closest = p1 + norm * t * len;

    float d = length(uv - closest);
    return 1.0 - smoothstep(thickness - softness, thickness + softness, d);
}

// Small circle/dot
float smooth_circle(vec2 uv, vec2 center, float radius, float softness) {
    float d = length(uv - center);
    return 1.0 - smoothstep(radius - softness, radius + softness, d);
}

// L-shaped hook stroke
float smooth_hook_l(vec2 uv, vec2 corner, float h_len, float v_len, float thickness, float softness) {
    // Vertical part going up from corner
    float vert = smooth_line_v(uv, corner.x, corner.y, corner.y + v_len, thickness, softness);
    // Horizontal part going right from corner
    float horiz = smooth_line_h(uv, corner.y, corner.x, corner.x + h_len, thickness, softness);
    return max(vert, horiz);
}

// J-shaped hook stroke
float smooth_hook_j(vec2 uv, vec2 top, float v_len, float curve_len, float thickness, float softness) {
    // Vertical part going down
    float vert = smooth_line_v(uv, top.x, top.y - v_len, top.y, thickness, softness);
    // Curved bottom - approximate with diagonal
    vec2 bottom = vec2(top.x, top.y - v_len);
    vec2 curve_end = bottom + vec2(-curve_len, -curve_len * 0.5);
    float curve = smooth_line_diag(uv, bottom, curve_end, thickness, softness);
    return max(vert, curve);
}

// Draw a kanji-like character with expanded stroke variety
float draw_glyph(vec2 uv, float seed) {
    float result = 0.0;
    float thickness = 0.055;
    float softness = 0.022;

    // Number of strokes based on seed (3-7)
    int strokes = 3 + int(hash11(seed) * 5.0);

    for (int i = 0; i < 8; i++) {
        if (i >= strokes) break;

        float s = seed + float(i) * 7.13;
        float type = hash11(s);

        if (type < 0.25) {
            // Horizontal line
            float y = 0.15 + hash11(s * 1.1) * 0.7;
            float x1 = hash11(s * 1.2) * 0.25;
            float x2 = 0.75 + hash11(s * 1.3) * 0.25;
            result = max(result, smooth_line_h(uv, y, x1, x2, thickness, softness));
        }
        else if (type < 0.5) {
            // Vertical line
            float x = 0.15 + hash11(s * 2.1) * 0.7;
            float y1 = hash11(s * 2.2) * 0.25;
            float y2 = 0.75 + hash11(s * 2.3) * 0.25;
            result = max(result, smooth_line_v(uv, x, y1, y2, thickness, softness));
        }
        else if (type < 0.62) {
            // Diagonal stroke (top-left to bottom-right or opposite)
            float diag_type = hash11(s * 4.1);
            vec2 p1, p2;
            if (diag_type < 0.5) {
                p1 = vec2(0.15 + hash11(s * 4.2) * 0.3, 0.7 + hash11(s * 4.3) * 0.25);
                p2 = vec2(0.55 + hash11(s * 4.4) * 0.3, 0.15 + hash11(s * 4.5) * 0.25);
            } else {
                p1 = vec2(0.55 + hash11(s * 4.2) * 0.3, 0.7 + hash11(s * 4.3) * 0.25);
                p2 = vec2(0.15 + hash11(s * 4.4) * 0.3, 0.15 + hash11(s * 4.5) * 0.25);
            }
            result = max(result, smooth_line_diag(uv, p1, p2, thickness, softness));
        }
        else if (type < 0.72) {
            // L-hook stroke
            vec2 corner = vec2(0.2 + hash11(s * 5.1) * 0.3, 0.2 + hash11(s * 5.2) * 0.3);
            float h_len = 0.25 + hash11(s * 5.3) * 0.35;
            float v_len = 0.25 + hash11(s * 5.4) * 0.35;
            result = max(result, smooth_hook_l(uv, corner, h_len, v_len, thickness, softness));
        }
        else if (type < 0.82) {
            // J-hook stroke
            vec2 top = vec2(0.35 + hash11(s * 6.1) * 0.3, 0.7 + hash11(s * 6.2) * 0.25);
            float v_len = 0.3 + hash11(s * 6.3) * 0.3;
            float curve_len = 0.1 + hash11(s * 6.4) * 0.15;
            result = max(result, smooth_hook_j(uv, top, v_len, curve_len, thickness, softness));
        }
        else if (type < 0.88) {
            // Small circle/dot accent
            vec2 center = vec2(0.25 + hash11(s * 7.1) * 0.5, 0.25 + hash11(s * 7.2) * 0.5);
            float radius = 0.04 + hash11(s * 7.3) * 0.04;
            result = max(result, smooth_circle(uv, center, radius, softness * 0.7));
        }
        else {
            // Box outline
            float bx = 0.2 + hash11(s * 3.1) * 0.2;
            float by = 0.2 + hash11(s * 3.2) * 0.2;
            float bw = 0.35 + hash11(s * 3.3) * 0.25;
            float bh = 0.35 + hash11(s * 3.4) * 0.25;

            result = max(result, smooth_line_v(uv, bx, by, by + bh, thickness, softness));
            result = max(result, smooth_line_v(uv, bx + bw, by, by + bh, thickness, softness));
            result = max(result, smooth_line_h(uv, by, bx, bx + bw, thickness, softness));
            result = max(result, smooth_line_h(uv, by + bh, bx, bx + bw, thickness, softness));
        }
    }

    return clamp(result, 0.0, 1.0);
}

// Get flicker value based on selected pattern
float get_flicker_value(float time, float seed, vec2 cell_id) {
    float t = time * flicker_speed;

    if (flicker_pattern == 0) {
        // Smooth sine wave (original)
        return 0.85 + 0.15 * sin(t + hash11(seed) * 6.28);
    }
    else if (flicker_pattern == 1) {
        // Strobe - sharp on/off
        float strobe_rate = 2.0 + hash11(seed) * 2.0;
        float strobe = step(0.5, fract(t * strobe_rate * 0.5));
        return mix(0.15, 1.0, strobe);
    }
    else if (flicker_pattern == 2) {
        // Wave - ripples across sign from bottom to top
        float wave_phase = cell_id.y * 0.4 + t;
        return 0.5 + 0.5 * sin(wave_phase);
    }
    else if (flicker_pattern == 3) {
        // Random segment failure - some glyphs go dark randomly
        float seg_roll = hash11(floor(t * 3.0) + cell_id.x * 13.0 + cell_id.y * 29.0 + seed);
        return seg_roll < 0.25 ? 0.08 : 1.0;
    }
    else if (flicker_pattern == 4) {
        // Chasing lights - sequential illumination top to bottom
        float chase_pos = fract(t * 0.4);
        float cell_norm = cell_id.y / 6.0; // Normalize assuming ~6 rows
        float dist = abs(cell_norm - chase_pos);
        return smoothstep(0.3, 0.0, dist);
    }
    else {
        // Nervous flicker - rapid random variation
        float noise = hash11(floor(t * 12.0) + seed + cell_id.x * 7.0);
        return 0.5 + 0.5 * noise;
    }
}

void fragment() {
    vec2 uv = UV;

    // Grid layout - vary row count based on seed for variety (2-6 glyphs)
    int cols = glyph_columns;
    int base_rows = 2 + int(hash11(glyph_seed * 0.1) * 5.0); // 2-6 rows
    int rows = vertical_text ? base_rows : max(2, cols);

    vec2 grid = uv * vec2(float(cols), float(rows));
    vec2 cell_id = floor(grid);
    vec2 cell_uv = fract(grid);

    // Padding
    float pad = 0.12;
    vec2 glyph_uv = (cell_uv - pad) / (1.0 - 2.0 * pad);

    float glyph = 0.0;
    if (glyph_uv.x > 0.0 && glyph_uv.x < 1.0 && glyph_uv.y > 0.0 && glyph_uv.y < 1.0) {
        float cell_seed = glyph_seed + cell_id.x * 13.0 + cell_id.y * 29.0;
        glyph = draw_glyph(glyph_uv, cell_seed);
    }

    // Flicker based on selected pattern
    float flicker = 1.0;
    if (flicker_enabled) {
        flicker = get_flicker_value(TIME, glyph_seed, cell_id);
    }

    // Broken sign dramatic flicker (overrides normal flicker)
    float broken_mult = broken_flicker(TIME, cell_id, glyph_seed);

    // Layer opacity (back layers slightly dimmer)
    float layer_opacity = 1.0 - float(current_layer) * 0.12;

    // Output - dark background, bright glyphs
    vec3 color = mix(background_color, sign_color, glyph);
    float emit = glyph * emission_strength * flicker * broken_mult * layer_opacity;

    // Add electrical arc effect for broken signs
    vec3 arcs = electrical_arc_effect(TIME, uv);

    // Final output - use black albedo to look unlit, all color from emission
    ALBEDO = vec3(0.0);
    EMISSION = sign_color * emit + arcs;
}
