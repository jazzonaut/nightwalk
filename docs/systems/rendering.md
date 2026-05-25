# Rendering

Chunk-based progressive rendering coordinated by `ChunkRenderer`, with seven phased sub-renderers per chunk. Materials, lighting, post-processing, weather, and sky live alongside.

> **Engineering decisions:**
> - [ADR-0013 — progressive chunk rendering](../adr/0013-progressive-chunk-rendering.md) — the time-budget scheduler that hosts the seven phases.
> - [ADR-0014 — C# ↔ Godot interop cost discipline](../adr/0014-csharp-godot-interop-cost.md) — the cost model that informs every perf decision in this subsystem.
> - [ADR-0015 — `MeshBuilder` over `SurfaceTool` for chunk geometry](../adr/0015-meshbuilder-for-chunk-geometry.md) — the chunk-merge pattern (one `MeshInstance3D` per surface per chunk) and the mesh-builder migration status.
>
> Atmospheric/post-process choices are designer tuning, not architectural decisions — they live in this doc as reference only.

## Phases

The seven phases run in order per chunk:

```
Ground → Roads → Manholes → Pavement → Buildings → Neons → StreetLamps
```

Each implements `IPhasedSubRenderer.RenderIncremental(state, budgetUsec) → bool` — returning `true` when the phase is complete. The coordinator (`ChunkRenderer.ProcessRenderQueue`) gives each phase a slice of the per-frame budget (`render_budget_microseconds = 10000`) and advances multiple chunks in lockstep so ground/roads always appear before buildings.

## Sub-renderers

| Class | Phase | Notes |
|---|---|---|
| `GroundRenderer` | Ground | Per-chunk ground quad with `[materials.ground]` |
| `RoadRenderer` | Roads | Road meshes per `RoadSegment`; lane markings; wet-surface shader |
| `ManholeRenderer` | Manholes | 0-2 per chunk, grid/concentric/radial pattern, optional smoke emitter |
| `PlotPavementRenderer` | Pavement | Sidewalk strips per plot |
| `BuildingRenderer` | Buildings | Most expensive: facade meshes, weathering shader, windows lit/unlit, ledges, rooftop features |
| `NeonRenderer` | Neons | Large signs, vertical strips, kanji/billboards |
| `StreetLampRenderer` | StreetLamps | Capped at `max_per_chunk = 6` lamps; LOD fades past 200m |

## Materials

8 building variants for visual variety: `building`, `building_dark`, `building_blue`, `building_purple`, `building_warm`, `building_weathered`, plus `ground`, `road`, `sidewalk`, `window_unlit`, `window_lit`, `ledge`, `road_marking`. All defined in `data/worldgen.toml` `[materials.*]` sections.

`MaterialFactory` (`src/Nightwalk.Godot/Rendering/Materials/`) creates `StandardMaterial3D` or `ShaderMaterial` per definition. `CreateWeatheredFacadeMaterial` builds the weathered-facade shader when `[facade_weathering].enabled = true`.

## Post-processing

`PostProcessingSetup` (`src/Nightwalk.Godot/Setup/PostProcessingSetup.cs`) configures a post-process material:

| Effect | Config keys | Defaults |
|---|---|---|
| Bloom | `bloom_*` | Enabled, intensity 1.5, threshold 0.6 |
| Tonemap | `tonemap_*` | Exposure 0.8, white 8.0 |
| SSAO | `ssao_*` | Disabled by default |
| SSR | `ssr_*` | Enabled (wet surface reflections) |
| SSIL | `ssil_*` | Disabled |
| DOF | `dof_*` | Disabled |
| Auto-exposure | `auto_exposure_*` | Disabled |
| Color grading | `color_grading_*` | Enabled, noir tint |
| Vignette | `vignette_*` | Disabled (yet `vignette_intensity` exists separately) |
| Film grain | `film_grain_*` | Enabled, subtle (0.06) |
| Chromatic aberration | `chromatic_aberration_*` | Enabled, mild (0.002) |
| Score effects (tint on gain/loss) | `[score_effects]` in `gameplay.toml` | See [Scoring](scoring.md) |

`VisualPresetManager` swaps preset bundles bound to number keys 1-9 (`GameRoot._Input` handles the key event). Preset index persists via `UserPreferences`.

## Environment

`EnvironmentSetup` configures the `WorldEnvironment` node:

- Background sky (`sky_enabled`, gradient between top/horizon/ground colors).
- Ambient color/energy (`ambient_color = #1A2535`, `ambient_energy = 0.4`).
- Standard fog (`fog_enabled`, `fog_density = 0.004`, blue tint).
- Height fog (`fog_height_enabled`, warm orange light-pollution glow at horizon).
- Volumetric fog (`volumetric_fog_*`, enabled, temporal reprojection for stability).

## Weather

`RainSystem` (`src/Nightwalk.Godot/Rendering/RainSystem.cs`) — particle-based rain that follows the player (or screensaver camera). Tuning under `[weather]` in `core.toml`: 10,000 particles, 40m coverage radius, `rain_color = #708098`. Optional splash particles (disabled) and ground mist.

## Wet surface

`[wet_surface]` in `worldgen.toml` controls puddle placement and the wet-asphalt shader. Ripples currently disabled. Reflections via SSR.

## Occlusion culling

Enabled by default. Buildings ≥10m tall and ≥50m² footprint become occluders. `occluder_margin = 0.5` shrinks the box to avoid edge artifacts. Rooftop features included as occluders.

## Implementation

- `src/Nightwalk.Godot/Rendering/ChunkRenderer.cs` — coordinator (see [ADR-0013](../adr/0013-progressive-chunk-rendering.md)).
- `src/Nightwalk.Godot/Rendering/SubRenderers/*.cs` — seven phased sub-renderers + `FacadeDetailGenerator`.
- `src/Nightwalk.Godot/Rendering/ChunkRenderState.cs` — per-chunk progressive state.
- `src/Nightwalk.Godot/Rendering/ChunkRenderContext.cs` — shared materials + camera position.
- `src/Nightwalk.Godot/Rendering/Geometry/MeshBuilder.cs`, `GeometryBuilder.cs` — mesh-construction helpers. `MeshBuilder` is the preferred path for chunk geometry; `SurfaceTool` overloads on `GeometryBuilder` are kept for one-shot/utility meshes only (see [ADR-0015](../adr/0015-meshbuilder-for-chunk-geometry.md)).
- `src/Nightwalk.Godot/Rendering/Materials/MaterialFactory.cs`, `TextureFactory.cs` — material/texture creation.
- `src/Nightwalk.Godot/Rendering/Pooling/NodePool.cs` — node reuse.
- `src/Nightwalk.Godot/Rendering/ScoreEffectSystem.cs` — score-driven post-process tint (see [Scoring](scoring.md)).
- `src/Nightwalk.Godot/Setup/PostProcessingSetup.cs`, `EnvironmentSetup.cs`, `SkySystemSetup.cs` — wiring.
- `src/Nightwalk.Godot/Rendering/RainSystem.cs` — rain particles.
- `src/Nightwalk.Godot/Rendering/Effects/SmokeEmitter.cs` — manhole smoke.
- `src/Nightwalk.Godot/Rendering/VisualPresetManager.cs` (and `VisualPreset.cs`) — preset swapping.

## Integration

| With | How |
|---|---|
| **Worldgen** | `ChunkLoadedEvent` triggers `OnChunkLoaded` which starts (or completes) chunk rendering |
| **Scoring** | `ScoreEffectSystem` listens to `ScoreChangedEvent` and tints post-process |
| **Game State** | Visible only during Playing (most subsystems unaffected, but post-process tint is muted) |
| **Configuration** | Hot reload reconfigures post-process material, environment, sky, rain, camera attributes, materials per renderer |
| **Sky Effects** | Separate subsystem ([Sky Effects](sky-effects.md)) for aurora/lightning/embers/horizon silhouette |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Progressive rendering enabled by default (`progressive_rendering_enabled = true`).
- [x] Per-frame render budget 10000 µs (10ms).
- [x] Max 2 chunks rendering concurrently.
- [x] Seven phases run in order (Ground → Roads → Manholes → Pavement → Buildings → Neons → StreetLamps).
- [x] Sort by phase each frame ensures ground appears before buildings.
- [x] 8 building material variants instantiated.
- [x] Weathered facade shader created when `facade_weathering.enabled = true`.
- [x] Wet surface shader for roads when `wet_surface` enabled.
- [x] Number keys 1-9 swap visual presets via `VisualPresetManager`.
- [x] Hot reload reconfigures all materials and post-processing.

## Related

- [Worldgen](worldgen.md), [Sky Effects](sky-effects.md), [Scoring](scoring.md), [Configuration](configuration.md).
- ADR-0013 (Progressive rendering).
