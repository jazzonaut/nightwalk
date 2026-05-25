// Shared weathering functions for building facades
// Used by: weathered_facade.gdshader

#include "hash_functions.gdshaderinc"

// Calculate dirt accumulation - ground grime with organic stains and soft transitions
float calculate_dirt(vec3 world_pos, float dirt_intensity, float height_falloff, float corner_intensity, sampler2D noise_tex) {
    // Facade UV for noise sampling (x+z for horizontal, y for vertical)
    vec2 facade_uv = vec2(world_pos.x + world_pos.z, world_pos.y);

    // Height fade - everything is more weathered lower down
    float height_fade = 1.0 - smoothstep(0.0, 60.0, world_pos.y);

    // === GROUND LEVEL GRIME (first 15 meters, strong at bottom) ===
    float edge_noise = texture(noise_tex, facade_uv * 0.15).r;
    float grime_edge = 15.0 + (edge_noise - 0.5) * 8.0;
    float ground_grime = 1.0 - smoothstep(0.0, grime_edge, world_pos.y);
    ground_grime *= height_falloff * 1.2;

    // === OVERALL GRIME LAYER (base weathering, fades with height) ===
    float base_noise = texture(noise_tex, facade_uv * 0.06).r;
    float base_grime = base_noise * corner_intensity * 0.5 * height_fade;

    // === MEDIUM STAINS (soft splotches, more at bottom) ===
    float stain_noise = texture(noise_tex, facade_uv * 0.1 + vec2(0.3, 0.1)).r;
    float stain = smoothstep(0.4, 0.7, stain_noise) * corner_intensity * 0.5 * height_fade;

    // === SMALL PATCHES (variation, more at bottom) ===
    float patch_noise = texture(noise_tex, facade_uv * 0.2 + vec2(0.5, 0.2)).r;
    float patches = smoothstep(0.45, 0.75, patch_noise) * corner_intensity * 0.35 * height_fade;

    // === FINE DETAIL (texture, more at bottom) ===
    float detail_noise = texture(noise_tex, facade_uv * 0.35 + vec2(0.2, 0.6)).r;
    float detail = smoothstep(0.5, 0.8, detail_noise) * corner_intensity * 0.2 * height_fade;

    // Combine effects
    float total = ground_grime + base_grime + stain + patches + detail;

    return clamp(total, 0.0, 1.0) * dirt_intensity;
}

// Calculate vertical water streak pattern - sparse, organic drip marks
float calculate_water_streaks(vec3 world_pos, float scale, float intensity, float sparsity, float building_seed, sampler2D noise_tex) {
    // Horizontal position offset by building_seed for per-building variation
    float h_pos = world_pos.x + world_pos.z + building_seed * 100.0;

    // Quantize to streak columns - scale affects spacing (base ~1.5m at scale=1.0)
    float col_spacing = 1.5 * scale;
    float streak_col = floor(h_pos / col_spacing);

    // Per-streak random values
    float streak_rand1 = hash21(vec2(streak_col * 37.0 + building_seed, building_seed * 61.0));
    float streak_rand2 = hash21(vec2(building_seed * 47.0, streak_col * 29.0 + building_seed));
    float streak_rand3 = hash21(vec2(streak_col + building_seed * 23.0, streak_col * 41.0));

    // Random position within column
    float streak_center = (streak_col + 0.5) * col_spacing + (streak_rand1 - 0.5) * col_spacing * 0.67;

    // Per-streak chance based on sparsity
    float streak_roll = hash21(vec2(streak_col * 53.0, building_seed * 67.0 + streak_col));
    float threshold = 0.5 + sparsity * 0.4;
    float has_streak = step(threshold, streak_roll);

    // Horizontal falloff with random width - scales with column spacing
    float streak_width = (0.08 + streak_rand2 * 0.15) * scale;
    float dist_from_center = abs(h_pos - streak_center);
    float h_falloff = 1.0 - smoothstep(0.0, streak_width, dist_from_center);

    // Meander - streak wanders horizontally as it goes down
    float meander_offset = streak_rand3 * 10.0;
    float meander = texture(noise_tex, vec2(world_pos.y * 0.02 / scale + meander_offset, building_seed)).r * 2.0 - 1.0;
    h_falloff *= 1.0 - smoothstep(0.0, streak_width * 2.0, abs(meander * 0.3 * scale));

    // Vertical flow pattern - unique per streak, scale affects vertical detail
    float flow_scale = (0.08 + streak_rand1 * 0.06) / scale;
    vec2 flow_uv = vec2(
        streak_center * 0.5 + streak_rand2 * 20.0,
        world_pos.y * flow_scale + streak_rand3 * 20.0
    );
    float flow = texture(noise_tex, flow_uv).r;
    float flow_mask = smoothstep(0.25 + streak_rand1 * 0.2, 0.6 + streak_rand2 * 0.15, flow);

    // Combine
    float mask = has_streak * h_falloff * flow_mask;

    // Fade out near top of very tall buildings only
    float height_norm = clamp(world_pos.y / 100.0, 0.0, 1.0);
    float height_mask = 1.0 - smoothstep(0.8, 0.95, height_norm);

    return mask * height_mask * intensity;
}

// Calculate rust stain pattern (drips DOWN from ledges)
float calculate_rust(vec3 world_pos, float intensity, float drip_length, float building_seed, float floor_height, float ground_floor_height, float chance, sampler2D noise_tex) {
    // Offset horizontal position by building_seed - makes each building's rust pattern unique
    float h_pos = world_pos.x + world_pos.z + building_seed * 100.0;

    // Ledges are at: ground_floor_height + n * floor_height
    // Find which floor we're on and the ledge above
    float y_above_ground = max(world_pos.y - ground_floor_height, 0.0);

    float floor_index = floor(y_above_ground / floor_height);
    float ledge_above_y = ground_floor_height + (floor_index + 1.0) * floor_height;
    float dist_below_ledge = ledge_above_y - world_pos.y;

    // Rust is strongest just below ledge, fades as you go down
    float fixture_proximity = 1.0 - smoothstep(0.0, drip_length, dist_below_ledge);

    // Quantize horizontal position to window-sized columns (~2m spacing)
    float column_index = floor(h_pos / 2.0);

    // Per-column rust chance - each column on each floor has independent chance
    float rust_roll = hash21(vec2(building_seed * 137.0 + floor_index * 17.0 + column_index, column_index * 31.0 + building_seed * 73.0 + floor_index));
    float has_rust = step(1.0 - chance, rust_roll);

    // Randomize drip position and width per (column, floor, building)
    float pos_rand = hash21(vec2(column_index * 23.0 + building_seed, floor_index * 19.0 + building_seed * 41.0));
    float width_rand = hash21(vec2(floor_index * 37.0 + column_index, building_seed * 53.0 + column_index));

    // Horizontal position with random offset (-0.6 to +0.6 from column center)
    float column_center = (column_index + 0.5) * 2.0 + (pos_rand - 0.5) * 1.2;
    float dist_from_center = abs(h_pos - column_center);

    // Random width between 0.15 and 0.4
    float drip_width = 0.15 + width_rand * 0.25;
    float h_falloff = 1.0 - smoothstep(0.0, drip_width, dist_from_center);

    // Per-drip random offsets for unique visual patterns
    float noise_offset_x = hash21(vec2(column_index * 47.0, floor_index * 29.0 + building_seed));
    float noise_offset_y = hash21(vec2(floor_index * 61.0 + building_seed, column_index * 43.0));
    float noise_scale = 0.3 + hash21(vec2(building_seed * 31.0, column_index + floor_index)) * 0.4;

    // Drip shape variation - each drip samples different part of noise
    vec2 drip_uv = vec2(
        h_pos * 0.5 + noise_offset_x * 20.0,
        world_pos.y * noise_scale + noise_offset_y * 20.0
    );
    float drip_noise = texture(noise_tex, drip_uv).r;
    float drip_shape = smoothstep(0.25 + width_rand * 0.15, 0.55 + pos_rand * 0.15, drip_noise);

    return fixture_proximity * h_falloff * drip_shape * intensity * has_rust;
}

// Calculate roughness variation for worn surfaces - only increases roughness, never shiny
float calculate_worn_roughness(float base_roughness, float variation, float worn_boost, float dirt_factor, sampler2D noise_tex, vec3 world_pos) {
    // Use facade-appropriate UV (x+z for horizontal, y for vertical)
    vec2 facade_uv = vec2(world_pos.x + world_pos.z, world_pos.y);

    // Noise-based variation - only ADD roughness, never subtract
    float rough_noise = texture(noise_tex, facade_uv * 0.25).r;
    float extra_roughness = rough_noise * variation;

    // Dirty areas are rougher (matte)
    float dirt_roughness = dirt_factor * worn_boost;

    // Final roughness: base + noise variation + dirt contribution (always increases)
    return clamp(base_roughness + extra_roughness + dirt_roughness, base_roughness, 1.0);
}
