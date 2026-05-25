# City Aliveness

Roads adjacent to climbed buildings become "alive" — car-trail particles run along them. Climbed buildings also get window-silhouette overlays.

> **Engineering decisions:** none specific. Pure event-driven bookkeeping; uses `BuildingClimbedEvent` from the [EventBus](../adr/0004-event-bus.md) as the trigger, plus chunk-load events to backfill aliveness on chunks that load after a climb.

## Player-facing summary

Climb a tall building and the streets nearby light up with animated car traffic — warm headlights one way, red taillights the other. Lit windows in the same building gain animated silhouettes (cats, tech equipment) suggesting habitation.

## Behaviour

- `CityAlivenessService` subscribes to `BuildingClimbedEvent` on construction.
- On a `BuildingClimbedEvent`, it records the building's bounding rectangle (with `AdjacencyMargin` expansion) and walks every road in currently-loaded chunks overlapping that rectangle; each adjacent road is added to `_aliveRoads` and a `RoadAlivenessChangedEvent` is published.
- `OnChunkLoaded(coord, chunk)` is called externally as chunks stream in — the service checks every road in the new chunk against every recorded climbed-building bounds and marks them alive on demand. This is how a chunk that loads *after* the climb still gets the trails.
- "Adjacent" means the road's perpendicular position lies inside the bounds (east-west road → bounds.minZ ≤ road.Z ≤ bounds.maxZ, north-south reversed). Both directions add their parallel-axis check via the bounds.
- `RoadAlivenessChangedEvent` carries `RoadId`, direction, position, half-width — enough for the renderer to lay out the trail mesh without re-querying.
- `silhouettes` is a separate sub-system using the same trigger (climbed building) to overlay window-silhouettes on lit windows in the building itself.

## Tuning — `data/aliveness.toml`

Verified 2026-05-25.

### `[car_trails]`

| Key | Default | Purpose |
|---|---|---|
| `enabled` | true | Master toggle |
| `adjacency_margin` | 20.0 | Bounds expansion (m) for "adjacent" road detection |
| `lanes_per_direction` | 2 | Number of lanes per direction |
| `scroll_speed` | 10.0 | Base trail scroll speed |
| `scroll_speed_variation` | 3.0 | Random per-lane variation |
| `streak_color` | #FFCC66 | Warm headlights |
| `streak_color_oncoming` | #FF4444 | Red taillights |
| `emission_strength` | 1.5 | Glow multiplier |
| `trail_width` | 0.02 | Thickness fraction |
| `fuzziness` | 0.5 | Edge softness (0=sharp, 1=very fuzzy) |
| `height_offset` | 0.08 | Above road surface (m) |
| `visibility_range` | 150.0 | Max render distance |
| `visibility_margin` | 20.0 | Fade margin |
| `motion_blur_strength` | 0.3 | Elongation (0=off, 1+=strong) |
| `bloom_boost` | 0.0 | Extra glow |

### `[silhouettes]`

| Key | Default | Purpose |
|---|---|---|
| `enabled` | true | Master toggle |
| `density` | 0.4 | Fraction of lit windows getting a silhouette |
| `blur_radius` | 0.05 | Soft edge |
| `opacity` | 0.7 | Silhouette darkness |
| `visibility_range` | 80.0 | LOD fade |
| `visibility_margin` | 20.0 | Fade gradient |

## Implementation

- `src/Nightwalk.Core/CityAliveness/CityAlivenessService.cs` — the Core-side service. Listens for `BuildingClimbedEvent`, walks loaded chunks, marks roads, publishes `RoadAlivenessChangedEvent`. ~175 lines.
- `src/Nightwalk.Core/CityAliveness/RoadId.cs` — chunk-coord + road-index identifier.
- `src/Nightwalk.Core/CityAliveness/Events/RoadAlivenessChangedEvent.cs` — published per road on first-alive.
- `src/Nightwalk.Godot/CityAliveness/CarTrailRenderer.cs` — Godot-side: listens for `RoadAlivenessChangedEvent`, generates the trail mesh + shader material per road.
- `src/Nightwalk.Godot/Buildings/SilhouetteState.cs` — window silhouette overlay generator.

## Integration

| With | How |
|---|---|
| **Buildings** | `BuildingClimbedEvent` is the trigger |
| **Worldgen** | `CityAlivenessService.OnChunkLoaded` is called externally per chunk-load (so chunks that load after climbs still get backfilled trails) |
| **Rendering** | `CarTrailRenderer` instantiates trail meshes per alive road; hot-reload calls `UpdateMaterials` |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Climbing a building marks all currently-loaded adjacent roads alive (`OnBuildingClimbed` walks `GetLoadedChunksOverlapping`).
- [x] Chunks loaded after a climb backfill the trails (`OnChunkLoaded` checks against `_climbedBuildings`).
- [x] Adjacency uses building bounds + `adjacency_margin` and the road's perpendicular axis (`IsRoadAdjacent`).
- [x] Same building can't double-add (`_climbedBuildingIds.Add` guard at line 94).
- [x] Same road can't double-mark (`_aliveRoads.Contains` guard at line 76).
- [x] `RoadAlivenessChangedEvent` includes direction, position, half-width.
- [x] `Reset` and `Cleanup` clear state and unsubscribe.
- [x] Disabled-via-config short-circuits both the climbed and chunk-load paths.

## Related

- [Building Climbing](building-climbing.md), [Worldgen](worldgen.md), [Rendering](rendering.md).
- ADR-0004 (EventBus).
