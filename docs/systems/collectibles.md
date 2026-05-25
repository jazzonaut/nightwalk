# Collectibles

Floating polyhedron pickups on rooftops and empty plots. Each can be collected once per session.

> **Engineering decisions:** none specific. Polyhedron generation uses [ADR-0009](../adr/0009-deterministic-seeded-generation.md) (chunk-seeded RNG). Scoring routes through [`IDifficultyService`](../adr/0010-difficulty-multiplier-service.md). The spawn/visibility hot paths in `CollectibleSpawner` and `CollectibleShape` are the canonical case study for [ADR-0014](../adr/0014-csharp-godot-interop-cost.md) (cached delegate, method-group reference, 4-tick throttle, short-circuit on unchanged state).

## Player-facing summary

Glowing polyhedron shapes hover above rooftops, bobbing gently and spinning. Walking into one collects it; a plasma-globe burst plays. Building rooftops pay `30 + height × 0.5` base points (taller buildings = bigger rewards). Empty plots pay a fixed 150.

## Behaviour

- One collectible spawns per eligible rooftop (`spawn_chance = 1.0` for buildings ≥ `min_rooftop_height`, configured 10.0).
- Empty plots get a collectible with `empty_plot_spawn_chance = 1.0` chance.
- Each collectible has a stable `CollectibleId` derived from chunk seed + index — same seed produces the same polyhedron at the same spot.
- Polyhedron face count chosen randomly from `[min_faces, max_faces]` ∈ {4, 6, 8, 12, 20} (tetrahedron, cube, octahedron, dodecahedron, icosahedron).
- Visual: spinning at `spin_speed °/sec`, bobbing vertically at `bob_speed` Hz × `bob_amplitude` m.
- Glow: emission via `glow_color` × `glow_intensity` + an `OmniLight3D` with range `light_range`.
- Proximity fade-in starts at `visibility_distance` over `visibility_fade_margin` distance.
- Collection: `CollectibleTracker.MarkCollected` is the source of truth; returns `false` if already collected. Publishes `CollectibleCollectedEvent` with position for downstream pingers (Resonance Seeker).

## Tuning — `data/gameplay.toml`

Verified against `data/gameplay.toml` 2026-05-25. (Full `[collectibles]` section reproduced in `gameplay.toml` lines 76-118.)

| Group | Keys | Purpose |
|---|---|---|
| Shape | `min_faces`, `max_faces` | Polyhedron variety: 4=tetra, 6=cube, 8=octa, 12=dodeca, 20=icosa |
| Appearance | `size`, `float_height`, `spin_speed`, `bob_amplitude`, `bob_speed` | Render and animation |
| Glow | `glow_color`, `glow_intensity`, `edge_color`, `edge_thickness` | Material |
| Light | `light_range`, `light_energy` | OmniLight3D |
| LOD | `visibility_distance`, `visibility_fade_margin` | Proximity fade |
| Scoring | `base_points` (30), `height_bonus_multiplier` (0.5) | Building rooftop reward formula |
| Empty plot | `empty_plot_points` (150), `empty_plot_float_height`, `empty_plot_spawn_chance` | Park-style plots |
| Collection FX | `particle_count`, `particle_speed`, `particle_duration`, `effect_*` | Plasma globe burst |

`base_points` and `empty_plot_points` are scaled by `IDifficultyService.GetCollectiblePoints` — Wanderer 0.75×, Architect 0.5×, Observer disables scoring entirely.

## Implementation

- `src/Nightwalk.Core/Collectibles/CollectibleTracker.cs` — Core-side set of collected IDs and `MarkCollected`/`IsCollected`/`Reset` API.
- `src/Nightwalk.Core/Collectibles/CollectibleId.cs`, `CollectibleLocationType.cs` — ID and `BuildingRooftop` / `EmptyPlot` enum.
- `src/Nightwalk.Core/Collectibles/Events/CollectibleCollectedEvent.cs` — published with position so Resonance can ping Seekers.
- `src/Nightwalk.Godot/Collectibles/CollectibleSpawner.cs` — instantiates and despawns per loaded chunk; uses `new DeterministicRng(StableHash.Combine(chunkSeed, 0xC011EC7))` for stable shape choice. Holds `_sfxCallback` as a cached delegate (rebuilt once in `SetSfxManager`, not per spawn) and passes `OnCollectibleCollected` as a method group. Visibility updates throttle to every 4th physics tick (`ProximityUpdateInterval = 4`). See [ADR-0014](../adr/0014-csharp-godot-interop-cost.md).
- `src/Nightwalk.Godot/Collectibles/CollectibleShape.cs` — the visual + collision body. Uses a `SphereShape3D` collision for proximity pickup. `UpdateVisibility` short-circuits when `alpha == _currentAlpha`, so steady-state collectibles cost zero marshalled calls per tick.
- `src/Nightwalk.Godot/Collectibles/PlasmaGlobeEffect.cs` — the collection effect.
- `src/Nightwalk.Godot/Spawning/ProximitySpawner.cs` — the generic proximity-driven spawn/despawn engine used by `CollectibleSpawner`. Caps `AddChild` bursts at `MaxSpawnsPerFrame = 8` ([ADR-0014](../adr/0014-csharp-godot-interop-cost.md) call-count discipline applied to scene-tree mutations).

## Integration

| With | How |
|---|---|
| **Scoring** | On collection: `ScoreService.AddScore(basePoints + heightBonus, Collectible)` then `IncrementCombo` |
| **Resonance** | `ResonanceMode` subscribes to `CollectibleCollectedEvent` to raise the Alert meter and spawn noise pings for Seekers |
| **HUD** | Total collected count derived from `CollectibleTracker.TotalCollected` |
| **Worldgen** | Spawned per chunk by `CollectibleSpawner.OnChunkLoaded`; despawned on unload |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] One collectible per eligible building rooftop (per-plot decision in spawner).
- [x] Empty plots get a special 150-point variant (`empty_plot_points`).
- [x] Each ID can only be collected once per session (`HashSet.Add` returns false on duplicate).
- [x] Shape randomised within `[min_faces, max_faces]` from the chunk-derived RNG.
- [x] Spinning at `spin_speed` °/sec, bobbing `bob_amplitude` at `bob_speed` Hz.
- [x] Glow magenta-pink via `glow_color` / `glow_intensity`.
- [x] Points scale with building height (`base_points + height * height_bonus_multiplier`).
- [x] Difficulty scales reward (`GetCollectiblePoints`).
- [x] `CollectibleCollectedEvent` includes position for noise-ping in Resonance.

## Related

- [Scoring](scoring.md), [Worldgen](worldgen.md), [Resonance](resonance.md).
- ADR-0009 (Determinism), ADR-0010 (Difficulty), ADR-0014 (Interop cost — the canonical case study).
