# Building Climbing

Detection of "the player reached a building's rooftop." A first-climb event fires once per building per session and unlocks city aliveness on adjacent roads, plus the sky-beam / rising-particle effects on the building.

> **Engineering decisions:** none specific. Pure event-driven bookkeeping on top of [EventBus](../adr/0004-event-bus.md).

## Player-facing summary

Stand on top of any building of sufficient height — a sky beam fires upward, glowing particles rise from the rooftop, points are awarded, and any roads adjacent to the building come alive with car trails.

## Behaviour

- `BuildingClimbService` (Godot-side) detects when the player is grounded on a building above `min_rooftop_height` and within `rooftop_height_tolerance` of the rooftop level.
- It calls `BuildingTracker.MarkClimbed(buildingId, height)`; the tracker returns `true` only on the first climb of that building. If first, it publishes `BuildingClimbedEvent(buildingId, height, totalClimbed)`.
- The visual effects (sky beam, rising particles, optional window highlights) are driven by `BuildingHighlightRenderer` listening to `BuildingClimbedEvent`.
- [City Aliveness](city-aliveness.md) also listens and marks adjacent roads alive.

## Tuning — `data/gameplay.toml`

Verified 2026-05-25.

| Key | Default | Purpose |
|---|---|---|
| `buildings.rooftop_height_tolerance` | 2.0 | Distance from rooftop to count as "on roof" |
| `buildings.min_rooftop_height` | 10.0 | Minimum building height to qualify |
| `buildings.highlight_windows` | false | Toggle window overlay effect |
| `buildings.window_highlight_color` | #ffcc88 | Window tint |
| `buildings.window_highlight_intensity` | 4.0 | Window emission boost |
| `buildings.highlight_beam` | true | Toggle sky beam |
| `buildings.beam_color` | #88a8ffff | Sky-beam color |
| `buildings.beam_intensity` | 1.0 | Brightness |
| `buildings.beam_width` | 0.4 | Beam thickness |
| `buildings.beam_height` | 500.0 | Beam vertical extent |
| `buildings.beam_start_offset` | 5.0 | Height above roof where beam starts |
| `buildings.highlight_particles` | true | Toggle rising particles |
| `buildings.particle_*` | various | Particle count, speed, lifetime, visibility LOD |

## Implementation

- `src/Nightwalk.Core/Buildings/BuildingTracker.cs` — set-based tracking with `MarkClimbed` / `IsClimbed` / `Reset`.
- `src/Nightwalk.Core/Buildings/BuildingId.cs` — chunk-coord + building-index identifier.
- `src/Nightwalk.Core/Buildings/Events/BuildingClimbedEvent.cs` — published on first climb.
- `src/Nightwalk.Godot/Buildings/BuildingClimbService.cs` — Godot-side proximity check; calls `MarkClimbed`.
- `src/Nightwalk.Godot/Buildings/BuildingHighlightRenderer.cs` — sky beam + rising particles + optional window highlight per climbed building.

## Integration

| With | How |
|---|---|
| **Scoring** | First climb publishes `BuildingClimbedEvent`; scoring handlers award `ScoreCategory.Building` (formula in caller, not in tracker) |
| **Aliveness** | `CityAlivenessService.OnBuildingClimbed` marks adjacent roads alive |
| **Highlight rendering** | `BuildingHighlightRenderer` listens for events and adds visual effects per building |
| **HUD** | Optional climbed-count display from `BuildingTracker.TotalClimbed` |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] First-climb-only: `MarkClimbed` returns false on duplicates (`HashSet.Add`).
- [x] Only buildings ≥ `min_rooftop_height` qualify (climb service threshold).
- [x] Sky beam, particles, window highlight all toggleable independently via TOML.
- [x] `Reset()` clears tracking for new session.
- [x] `BuildingClimbedEvent` carries building height (used downstream for reward calculation).

## Related

- [Scoring](scoring.md), [City Aliveness](city-aliveness.md), [Worldgen](worldgen.md).
- ADR-0004 (EventBus).
