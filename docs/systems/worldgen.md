# World Generation

Procedural endless city. Chunks stream as the player moves; clusters define downtown-like regions; buildings, roads, neon, manholes, smoke, street lights, and weather all derive from deterministic per-chunk seeds.

> **Engineering decisions:**
> - [ADR-0009 — deterministic seeded generation](../adr/0009-deterministic-seeded-generation.md) — chunk seeds, RNG, hashing.
> - [ADR-0013 — progressive chunk rendering](../adr/0013-progressive-chunk-rendering.md) — per-frame work budget that hosts the seven sub-renderer phases.
> - [ADR-0014 — C# ↔ Godot interop cost model](../adr/0014-csharp-godot-interop-cost.md) — informs every per-frame perf decision on the chunk-construction hot path.
> - [ADR-0015 — `MeshBuilder` over `SurfaceTool` for chunk geometry](../adr/0015-meshbuilder-for-chunk-geometry.md) — the chunk-merge pattern: one `MeshInstance3D` per surface per chunk, geometry accumulated in managed arrays.

## Player-facing summary

The city stretches endlessly. As you move, new buildings, roads, and neon signs appear at the horizon; chunks far behind unload. A "downtown" radius around the world origin has notably taller buildings; additional cluster regions, procedurally placed in 2000m super-chunks, create extra downtown-like cores. Every spawn is deterministic per world seed.

## Architecture

```
World seed (config)
       │
       ▼
ChunkManager (load/unload by Chebyshev distance)
       │
       ├─→ ChunkCoord → ChunkSeed = StableHash.Combine(worldSeed, x, z)
       │       │
       │       ▼
       │   ChunkData with Roads, Plots, Buildings, Neons, EmptyPlotIndices
       │       │
       │       ▼
       │   ChunkRenderer (progressive, phased — see ADR-0013)
       │
       └─→ ClusterInfluenceProvider (super-chunk grid, 2000m)
                │
                ▼
            Cluster centers placed per super-chunk
            (origin cluster + N procedural clusters)
                │
                ▼
            Per-building: cluster influence modulates height, neon density,
            window-lit %, etc.
```

## Streaming

| Setting | Default | Source |
|---|---|---|
| Chunk size | 64.0 m | `worldgen.chunks.size` |
| Load radius | 8 chunks | `worldgen.chunks.load_radius` (Chebyshev) |
| Unload radius | 12 chunks | `worldgen.chunks.unload_radius` |
| Max loads per tick | 4 | `worldgen.chunks.max_loads_per_tick` |
| Max concurrent loads | 2 | `worldgen.chunks.max_concurrent_loads` |
| Progressive rendering | enabled | `worldgen.chunks.progressive_rendering_enabled` |
| Render budget | 10000 µs/frame | `worldgen.chunks.render_budget_microseconds` |
| Max concurrent renders | 2 | `worldgen.chunks.max_concurrent_renders` |

Screensaver mode overrides the focus point: chunk loading follows the camera, not the player (`GameRoot._PhysicsProcess`).

## Clusters / super-chunks

`ClusterInfluenceProvider` runs on a `super_chunk_size = 2000.0` coarse grid. Per super-chunk:

- `average_clusters_per_super_chunk = 10` cluster centers are seeded deterministically.
- `minimum_spacing = 250.0` between centers within a super-chunk.
- A `pre_warm_radius = 5` super-chunks are pre-generated at startup so distant exploration doesn't stall.
- The origin cluster (`enabled = true`, `radius = 500.0`, `height_multiplier = 2.0`) is always present at world origin and sets the "downtown" feel.
- Other clusters get radius in `[150, 500]` and height multipliers `[2.5, 3.0]`, with a chosen falloff from `linear` / `smoothstep` / `exponential` / `inverse_square` (weighted by `falloff_weights`).

Cluster influence modulates per-building values (height, neon density, window-lit %, etc.) at the position of each building.

## Roads

Hierarchical grid: major roads at `major_spacing = 160`, minor at `minor_spacing = 48`. Widths `21` / `10`. Sidewalks `4` wide. Curb dimensions and road height tuned via TOML.

## Buildings

| Setting | Default |
|---|---|
| Min / max height | 15.0 / 150.0 |
| `edge_margin` | 1.5 (gap on non-road edges → 1m between adjacent buildings) |
| Empty-plot chance | 0.08 (park-style) |
| Two-building chance | 0.4 |
| Stepped-building chance | 0.35 (tall buildings with setbacks) |
| Stepped: min height to qualify | 60.0 |
| Stepped: min/max sections | 2 / 4 |
| Stepped: setback fraction | 0.15 / 0.30 |
| Stepped: asymmetric chance | 0.3 |

Buildings come in 8 material variants (`building_dark`, `building`, `building_blue`, `building_purple`, `building_warm`, `building_weathered`, plus standard window and ledge). Choice is per-building, seeded.

## Facades, weathering, details

`[facades]` sets ground-floor height, floor height (3.75m), window dimensions, ledge depth/height, facade inset, rooftop-feature chance.

`[facade_weathering]` adds dirt/grime, water-damage streaks, rust stains, surface-wear roughness — all driven by noise textures keyed off the building seed.

`[facade_details]` adds AC units (15% per eligible spot) and vertical pipes (80% chance, up to 10 floors).

## Neon

`placement_density = 0.25`. 8 color palette. Large signs, vertical corner strips, full-height strips, kanji signs, billboards — each with their own chance and size ranges. Some neon signs cast actual scene light (when `neon_light_casting_enabled = true`); LOD reduces parallax layers past 50m and fades small-element visibility past 300m.

## Windows

`lit_percentage = 0.15` baseline (Noir: fewer but brighter). Floor clustering creates dark/fully-lit/checkerboard floors; warm vs cool color temperature; rare magenta/cyan accents (`accent_chance = 0.03`). Curtains/blinds tinted per shader.

## Street lights, manholes, smoke

`[street_lights]` — sodium-colored warm pools at 40m spacing along roads.

`[manholes]` — 0-2 per chunk, with grid/concentric_rings/radial_lines patterns and chance of smoke emission.

`[smoke]` — particle config for the manhole smoke wisps.

## Wet surfaces

`[wet_surface]` controls puddle placement, ripple animation (currently disabled), and the overall "asphalt looks wet under rain" shader.

## Occlusion culling

`[occlusion_culling]` enabled: buildings ≥10m tall and ≥50m² footprint create occluders that hide objects behind them. `occluder_margin = 0.5` shrinks the box to avoid edge artifacts.

## Implementation

Core side (`Nightwalk.Core/World/`):

- `WorldBuilder.cs` — fluent builder; assembles `SystemRegistry`, `DeterministicRng`, `ServiceContainer`, registers modules, returns `WorldState`.
- `WorldContext.cs` / `IWorldContext.cs` — shared per-world services bag.
- `WorldState.cs` — tick-aware container; calls `_eventBus.ProcessDeferredEvents()` each tick.
- `WorldConfig.cs` — chunk size, seed.
- `IWorldModule.cs` — pluggable module interface; modules are sorted by priority.
- `World/Buildings/BuildingGenerator.cs`, `BuildingData.cs`, `PlotData.cs`, `FacadeGrid.cs` — per-chunk building placement.
- `World/Roads/RoadGenerator.cs`, `RoadSegment.cs`, `RoadType.cs`.
- `World/Neon/NeonPlacementGenerator.cs`, `NeonLightData.cs`, `NeonPlacementConstants.cs`.
- `World/Clusters/SuperChunkCoord.cs`, `ClusterInfluenceProvider.cs`, `ClusterConfig.cs`.

Godot side (`Nightwalk.Godot/`):

- `Setup/WorldInitializer.cs` — assembles the `WorldBuilder`, calls `.Build()`, wires `ChunkRenderer`.
- `Rendering/ChunkRenderer.cs` — progressive rendering ([ADR-0013](../adr/0013-progressive-chunk-rendering.md)).
- `Rendering/SubRenderers/*.cs` — `GroundRenderer`, `RoadRenderer`, `ManholeRenderer`, `PlotPavementRenderer`, `BuildingRenderer`, `NeonRenderer`, `StreetLampRenderer`, plus `FacadeDetailGenerator`.
- `Rendering/Sky/DistantCitySilhouette.cs` — see [Sky Effects](sky-effects.md).
- `Rendering/Effects/SmokeEmitter.cs` — manhole smoke (with `ClearCache()` on world reload).
- `Rendering/RainSystem.cs` — rain particles, follows player or screensaver camera.

## Tuning

Everything above (and more) is in `data/worldgen.toml`. Hot-reloadable; a worldgen change triggers `GameRoot.ReloadWorld()` (see [Configuration](configuration.md)).

## Integration

| With | How |
|---|---|
| **City Aliveness** | `OnChunkLoaded` callback to backfill car trails on newly-loaded chunks |
| **Collectibles** | `CollectibleSpawner.OnChunkLoaded` spawns per rooftop / empty plot |
| **Beacon Sync** | `BeaconSpawner.OnChunkLoaded` spawns beacons on eligible buildings |
| **Building Climbing** | `BuildingClimbService` matches the player's grounded position to building rooftops |
| **Rendering** | `ChunkRenderer` consumes `ChunkData` from `ChunkManager` and renders progressively |
| **Hot reload** | A `worldgen` change in DEBUG rebuilds the entire world (see [Configuration](configuration.md)) |
| **Determinism** | All generation routes through `DeterministicRng` seeded from chunk coords (see [ADR-0009](../adr/0009-deterministic-seeded-generation.md)) |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Same world seed produces identical city (chunk seeds derived deterministically).
- [x] Chunks load within `load_radius` (Chebyshev) of the focus point.
- [x] Chunks unload outside `unload_radius`.
- [x] Origin downtown applies `height_multiplier = 2.0` within `radius = 500`.
- [x] Additional clusters placed per super-chunk with `minimum_spacing = 250`.
- [x] Buildings respect min/max heights and per-cluster modulation.
- [x] Empty plots (8% chance) carry special collectibles.
- [x] Worldgen TOML change triggers a full rebuild (`GameRoot.ReloadWorld`).
- [x] Pre-warm pre-generates `pre_warm_radius = 5` super-chunks at startup.
- [x] Screensaver camera overrides the chunk-load focus (`GameRoot._PhysicsProcess`).

## Related

- [Rendering](rendering.md), [Sky Effects](sky-effects.md), [Collectibles](collectibles.md), [Beacon Sync](beacon-sync.md), [Building Climbing](building-climbing.md), [City Aliveness](city-aliveness.md), [Configuration](configuration.md).
- ADR-0009 (Determinism), ADR-0013 (Progressive rendering), ADR-0006 (Hot reload), ADR-0014 (Interop cost model), ADR-0015 (`MeshBuilder` over `SurfaceTool`).
