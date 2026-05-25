# Beacon Synchronization

The signature puzzle: activate a source beacon, route its colored signal beam through player-placed prisms to a same-frequency target beacon.

> **Engineering decision:** [ADR-0011 — beam routing via iterative ray-march with prism snap](../adr/0011-beacon-beam-routing.md).

## Player-facing summary

Beacons are crystalline pyramids on tall rooftops, one of eight colors. Walk up to one and press `E` — it activates and a first prism spawns above. Same-frequency beacons within range show marker beams pointing skyward. Place more prisms to route the signal; each placement costs 10 points. The beam reaches a matching beacon → sync completes → cinematic flyby plays → points awarded (base + distance bonus + unused-prism bonus).

## Rules

| Rule | Source |
|---|---|
| Spawn chance per eligible building | `spawn_chance = 0.1` |
| Min building height for beacon | `min_building_height = 15.0` |
| Spatial spread for frequencies | `frequency_grid_size = 150.0` (one frequency per 150m cell) |
| Min distance between same-frequency beacons | `min_same_frequency_distance = 200.0` |
| Number of frequencies | 8 (Cyan, Orange, Lime, Magenta, Coral, Mint, Purple, Yellow) |
| Activation range | `activation_distance = 3.0` |
| Beacon spawn / despawn (idle) | 150m / 200m |
| Beacon spawn / despawn (active frequency) | 1000m / 1200m — extended so the puzzle's target stays in view across the city |
| Beam max length | `signal_beam_max_length = 150.0` before "broken" |
| Snap threshold | `beam_snap_threshold = 2.0` |
| Max prisms per sync | `max_prisms = 100` (difficulty-overridable) |
| Prism placement cost | `placement_cost = 10` |

## Rewards

Sync completion awards:

```
base_points (200)
+ floor(beam_total_distance * distance_bonus (1.0))
+ (effective_max_prisms - prisms_used) * prism_bonus (10)
```

…all scaled by `IDifficultyService.GetBeaconRewardPoints`. Implementation in `BeaconSyncManager.CompleteSync`.

## Frequency assignment

Beacons of different frequencies can occupy the same neighborhood, but same-frequency beacons are kept apart by the grid + min-distance constraint. Implementation in `BeaconGenerator` uses [ADR-0009](../adr/0009-deterministic-seeded-generation.md) deterministic RNG seeded from chunk coordinates — same world seed produces the same color at the same rooftop.

## Tuning — `data/beacons.toml`

Verified 2026-05-25. Full table in TOML lines 3-93.

| Group | Keys | Purpose |
|---|---|---|
| `[beacon]` appearance | `base_radius`, `height`, `transparency`, `tint_color`, `edge_color`, `edge_glow_intensity`, `edge_thickness`, `glow_intensity`, `active_glow_intensity` | The hex pyramid look |
| `[beacon]` generation | `spawn_chance`, `min_building_height`, `frequency_grid_size`, `min_same_frequency_distance`, `frequencies` | Placement rules |
| `[beacon]` beams | `marker_beam_*`, `signal_beam_*`, `broken_beam_opacity` | Beam visuals |
| `[prism]` | See [Prism Tool](prism-tool.md) | Prism config (same TOML file) |
| `[rewards]` | `base_points (200)`, `distance_bonus (1.0)`, `prism_bonus (10)` | Sync reward formula |
| `[interaction]` | `activation_distance (3.0)`, `spawn_distance (150)`, `despawn_distance (200)`, `active_frequency_spawn_distance (1000)`, `active_frequency_despawn_distance (1200)` | Approach + visibility ranges |

## States

### Per-beacon

- `Idle` — not participating; dim glow at `glow_intensity`.
- `Source` — currently emitting; bright glow at `active_glow_intensity`, beam follows refraction path.
- `Target` — same-frequency, marker beam visible upward.
- `Completed` — previously synced (from this beacon or to it); locked, decorative.

### Per-sync (on `BeaconTracker`)

- No active sync — players can activate any non-completed beacon.
- Active sync — source set; new prisms placeable; beam recomputed on each prism change.
- Complete — sync recorded; flyby triggered; prisms in the path are locked.

## Implementation

- `src/Nightwalk.Core/Beacons/BeaconFrequency.cs` — value-type wrapper for the integer frequency.
- `src/Nightwalk.Core/Beacons/BeaconGenerator.cs` — chunk-seeded placement of beacons on eligible rooftops; respects spatial grid + min-distance.
- `src/Nightwalk.Core/Beacons/BeaconTracker.cs` — Core-side state for active sync.
- `src/Nightwalk.Godot/Beacons/BeaconSyncManager.cs` — Godot-side renderer + interaction + beam computation. Implements [ADR-0011](../adr/0011-beacon-beam-routing.md). ~800 lines.
- `src/Nightwalk.Godot/Beacons/BeaconSpawner.cs` — proximity-based spawn/despawn of beacon nodes.
- `src/Nightwalk.Godot/Beacons/PrismNode.cs` — see [Prism Tool](prism-tool.md).
- `src/Nightwalk.Godot/Beacons/SignalBeamRenderer.cs` — polyline renderer for the path.
- `src/Nightwalk.Godot/Beacons/PrismPOVController.cs` — see [Prism Tool](prism-tool.md).

## Integration

| With | How |
|---|---|
| **Prism Tool** | Player tool that places `PrismNode`s while sync is active; this manager owns the placement and the beam routing |
| **Scoring** | `CompleteSync` calls `AddScore(rewards, BeaconSync)` and `IncrementCombo` |
| **Flyby camera** | `BeaconSyncManager.SetFlybyCamera` wired in `GameRoot`; flyby triggered post-sync |
| **Audio** | `SfxManager` fires on activation, prism placement, sync completion |
| **Difficulty** | `GetMaxPrisms`, `GetPrismPlacementCost`, `GetBeaconRewardPoints` all route through `IDifficultyService` |
| **Resonance** | The beam puzzle is also the mechanism for repelling the Warden — sustained beam contact triggers `OnWardenRepelled`. See [Resonance](resonance.md). |

## Acceptance checklist

Verified against code 2026-05-25 (mostly via the [ADR-0011](../adr/0011-beacon-beam-routing.md) verification trail).

- [x] Beacons spawn on eligible buildings (≥15m, 10% chance) — `BeaconGenerator`.
- [x] Same-frequency beacons maintain 200m separation — `min_same_frequency_distance`.
- [x] Activation requires E within 3m — `activation_distance`.
- [x] First prism spawns automatically above beacon at `spawn_height = 3.0`.
- [x] Marker beams appear on matching targets — `BeaconSyncManager.StartSync`.
- [x] Beam refracts through prisms and snaps within `beam_snap_threshold` (2.0m).
- [x] Sync completes when segment passes within snap threshold of same-frequency target — `CheckBeaconHitAlongRay`.
- [x] Reward = base + distance bonus + unused-prism bonus, difficulty-scaled.
- [x] Completed beacons cannot start a new sync (state check in `StartSync`).
- [x] Flyby triggers after sync — `BeaconSyncManager.CompleteSync` → `FlybyCamera`.
- [x] Active-frequency spawn distance is extended (`1000m` vs `150m`) so targets stay visible.

## Related

- [Prism Tool](prism-tool.md), [Scoring](scoring.md), [Resonance](resonance.md), [Worldgen](worldgen.md).
- ADR-0011 (Beam routing), ADR-0009 (Determinism), ADR-0010 (Difficulty).
