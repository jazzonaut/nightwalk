shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "includes/hash_functions.gdshaderinc"

// Base material properties
uniform vec3 albedo : source_color = vec3(1.0, 0.88, 0.69);
uniform float roughness : hint_range(0.0, 1.0) = 0.1;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;

// Emission properties
uniform vec3 emission_color : source_color = vec3(1.0, 0.88, 0.69);
uniform float emission_strength : hint_range(0.0, 16.0) = 4.5;


// Color palette for procedural variation
uniform vec3 warm_color : source_color = vec3(1.0, 0.88, 0.69);
uniform vec3 cool_color : source_color = vec3(0.83, 0.91, 1.0);
uniform vec3 accent_magenta : source_color = vec3(1.0, 0.53, 0.87);
uniform vec3 accent_cyan : source_color = vec3(0.53, 1.0, 0.93);
uniform float accent_chance : hint_range(0.0, 0.2) = 0.03;

// Curtain/blind effect thresholds
uniform float curtain_chance : hint_range(0.0, 1.0) = 0.25;
uniform float blinds_half_chance : hint_range(0.0, 1.0) = 0.15;
uniform float blinds_closed_chance : hint_range(0.0, 1.0) = 0.05;


// Per-window data from UV channels
// UV.x = building seed (0-1), UV.y = floor number (0-1)
// UV2.x = window variation (0-1), UV2.y = horizontal position (0-1)
varying vec2 window_data;
varying vec2 window_data2;

// Determine building type from seed (0 = residential/warm, 1 = office/cool)
float building_type(float seed) {
    return step(0.45, hash11(seed * 7.31 + 0.5));
}

// Select window color based on building personality and variation
vec3 select_window_color(float building_seed, float variation, float btype) {
    // Accent colors (rare, overrides everything)
    float accent_roll = hash21(vec2(building_seed * 13.7, variation * 17.3));
    if (accent_roll < accent_chance * 0.5) {
        return accent_magenta;
    }
    if (accent_roll < accent_chance) {
        return accent_cyan;
    }

    // Mix warm/cool based on building type
    // Residential (btype=0): 75% warm, 25% cool
    // Office (btype=1): 25% warm, 75% cool
    float warm_bias = mix(0.75, 0.25, btype);

    // Per-window variation
    float color_roll = hash21(vec2(building_seed * 5.3, variation * 11.7));

    if (color_roll < warm_bias) {
        return warm_color;
    } else {
        return cool_color;
    }
}

// Get intensity multiplier based on color type
float get_color_intensity(vec3 color) {
    // Use distance comparison for float safety (avoid exact equality)
    float epsilon = 0.001;

    // Accent colors are slightly brighter
    if (distance(color, accent_magenta) < epsilon || distance(color, accent_cyan) < epsilon) {
        return 1.3;
    }
    // Cool colors slightly brighter (fluorescent feel)
    if (distance(color, cool_color) < epsilon) {
        return 1.1;
    }
    return 1.0;
}

// Compute curtain/blind dimming factor
float curtain_factor(float variation, float building_seed) {
    float curtain_roll = hash21(vec2(variation * 100.0, building_seed * 50.0));

    // Cumulative thresholds
    float closed_threshold = blinds_closed_chance;
    float half_threshold = closed_threshold + blinds_half_chance;
    float curtain_threshold = half_threshold + curtain_chance;

    if (curtain_roll < closed_threshold) {
        return 0.15;  // Blinds closed - very dim
    }
    if (curtain_roll < half_threshold) {
        return 0.4;   // Blinds half-closed
    }
    if (curtain_roll < curtain_threshold) {
        return 0.6;   // Curtains - moderately dim
    }
    return 1.0;  // No curtains
}


void vertex() {
    // Pass UV data to fragment shader
    window_data = UV;
    window_data2 = UV2;
}

void fragment() {
    float building_seed = window_data.x;
    float floor_norm = window_data.y;
    float variation = window_data2.x;
    float h_position = window_data2.y;

    // Determine building personality
    float btype = building_type(building_seed);

    // Select color based on building type and window variation
    vec3 window_color = select_window_color(building_seed, variation, btype);
    float color_intensity = get_color_intensity(window_color);

    // Apply curtain/blind dimming
    float curtain = curtain_factor(variation, building_seed);

    // Final emission calculation (no flicker - stable for clean reflections)
    float final_intensity = emission_strength * color_intensity * curtain;

    ALBEDO = window_color;
    ROUGHNESS = roughness;
    METALLIC = metallic;
    EMISSION = window_color * final_intensity;
}
