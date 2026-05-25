# Sky Effects

Aurora bands, lightning, floating embers, light pollution at the horizon, and a ring of distant city silhouettes that masks the world edge.

> **Engineering decisions:** none. Sky-dome shader + helper systems; tuning lives in `data/core.toml [sky_effects]`.

## Player-facing summary

Look up: aurora bands ripple in green/magenta, occasional lightning flashes light up the cityscape, ember particles drift through the atmosphere (when enabled), and a warm-orange + cool-cyan light-pollution band glows at the horizon. Around the edge of the playable world, a ring of distant city silhouettes hides the procedural-chunk boundary.

## Components

### Sky dome

- `dome_radius = 500.0`, `dome_segments = 32`.
- Implemented as a single sphere shader that mixes all the effects below.

### Aurora

- `aurora_enabled = true`.
- Primary color `#00FF88` (green), secondary `#FF00FF` (magenta).
- `aurora_intensity = 1.5`, `aurora_speed = 0.10` (slow drift).
- `aurora_band_count = 4` bands woven across the upper sky.

### Lightning

- `lightning_enabled = true`.
- `lightning_frequency = 30.0` seconds (mean interval; randomised ±50-200% via `lightning_interval_variation = 1.5`).
- `lightning_duration = 0.8` per stroke base.
- `lightning_world_illumination = 1.5` (additional scene light during a stroke).
- `lightning_color = #E6F2FF`.
- `lightning_volumetric_fog_energy = 2.0` (fog lights up too).

Multi-stroke realism:

- `lightning_multi_stroke_chance = 0.9` (most strikes have multiple strokes).
- `lightning_max_strokes = 3`.
- `lightning_stroke_duration_min/max = 0.8 / 1.5` (multiplier of base duration).
- `lightning_first_stroke_intensity_min/max = 0.1 / 1.0`.
- `lightning_follow_up_intensity_min/max = 0.4 / 0.9`.
- `lightning_stroke_gap_min/max = 0.05 / 1.0` seconds.
- `lightning_position_drift = 0.1` (subsequent strokes drift slightly).

### Electrical atmosphere (embers)

- `electrical_atmosphere_enabled = false` (currently off by default).
- `ember_particle_count = 120` floating particles.
- `ember_color = #ff5e00ff` (orange), `ember_lifetime = 20s`.
- Spawn area `40m × 40m`, height range `40m`, gravity *up* (`+0.3`), turbulence `1.5`.

### Light pollution

- `light_pollution_enabled = true`.
- `light_pollution_intensity = 1.0`.
- Warm color `#FF9933`, cool color `#4DE6FF`.
- `light_pollution_height = 0.5` (horizon line).
- Soft edge: `light_pollution_fade_up = 0.12`, `light_pollution_fade_down = 0.15`.
- `light_pollution_edge_noise = 0.03` (subtle break of the line).

### Distant city silhouettes

- `silhouettes_enabled = true`.
- Ring from `silhouette_inner_radius = 780` to `silhouette_outer_radius = 1250`.
- `silhouette_building_count = 1500` placeholder boxes.
- Heights 20-200m; widths 15-50m.
- Color `#0A0A12` (almost pure black, against dim sky).
- Inner edge gets a `1.5×` height boost so the skyline rises smoothly to the procedural city.
- Variation: `silhouette_tower_chance = 0.10` (tall + narrow), `silhouette_spire_chance = 0.02` (super-tall + very narrow, 1.8× height).
- Generated deterministically with `new DeterministicRng(42)` (fixed seed — silhouettes don't change with world seed; see [ADR-0009](../adr/0009-deterministic-seeded-generation.md)).

## Implementation

- `src/Nightwalk.Godot/Rendering/Sky/SkyDomeManager.cs` — the dome + shader-driven effects (aurora, lightning, light pollution, embers).
- `src/Nightwalk.Godot/Rendering/Sky/DistantCitySilhouette.cs` — generates silhouette boxes around the world edge.
- `src/Nightwalk.Godot/Setup/SkySystemSetup.cs` — wiring; returns a `SkySystemResult` for `GameRoot` to hold.

## Integration

| With | How |
|---|---|
| **Player tracking** | `SetFollowTarget(player)` makes the sky dome follow the player (so it always looks centered) |
| **Rendering** | Lightning's `world_illumination` and `volumetric_fog_energy` interact with the scene lighting and volumetric fog from `EnvironmentSetup` |
| **Configuration** | Hot reload reconfigures everything via `SkySystemResult.Configure(...)` |
| **Resonance** | `ResonanceVisualEffects` applies a subtle sky tint when Resonance is active and Alert is high |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Sky dome enabled and rendered as a single 500m sphere shader.
- [x] Aurora animated with 4 bands, drift speed `0.10`.
- [x] Lightning fires at ~30s mean interval with multi-stroke behavior (90% chance of 2-3 strokes).
- [x] Embers off by default (`electrical_atmosphere_enabled = false`).
- [x] Light pollution band at horizon with warm/cool colors.
- [x] 1500 silhouette boxes in the 780-1250m ring with tower/spire variations.
- [x] Silhouette generation uses fixed seed `42` — stable across sessions.
- [x] Sky dome follows player position.
- [x] Resonance overlay tint applies via `ResonanceVisualEffects`.

## Related

- [Rendering](rendering.md), [Worldgen](worldgen.md), [Configuration](configuration.md), [Resonance](resonance.md).
- ADR-0009 (Determinism — silhouettes use fixed seed by design).
