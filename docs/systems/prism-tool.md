# Prism Tool

Hexagonal glass prism that refracts beacon signal beams. Players place, rotate, and remove prisms during an active sync.

> **Engineering decisions:** the beam-routing semantics that the prism participates in are in [ADR-0011](../adr/0011-beacon-beam-routing.md). The prism *itself* is straightforward — no separate ADR.

## Player-facing summary

While a beacon sync is active, the prism tool is automatically equipped. A green preview shows where a click would place a prism (orange if obstructed, red if invalid). Left-click places (-10 points). Right-click removes hovered prism (refund). With a prism in your crosshair, press `E` to enter prism POV — the camera moves inside the prism for precise aiming with the mouse; scroll wheel zooms. Press `E` again or right-click to exit.

## Behaviour

- **Auto-equipped** during active sync; `PlayerToolManager` rotates it in.
- **Placement preview** uses `placement_snap_threshold = 2.0` to snap onto the current beam path when close — so extending a beam is forgiving.
- **Max prisms per sync** is `max_prisms = 100` (difficulty-scaled via `IDifficultyService.GetMaxPrisms`).
- **Placement cost** `placement_cost = 10` deducted on each placement (difficulty-scaled via `GetPrismPlacementCost`).
- **Refraction** is the prism's "forward axis" — the beam enters and exits along the prism's local forward. Rotating the prism changes the exit direction.
- **POV camera** offsets behind the prism by `pov_camera_offset = 3.0` with `vertical_offset = 0.5` so the beam is in frame. Zoom is 20°-90° FOV in `pov_zoom_step = 5°` increments.
- **Wall offset** `wall_offset = 1.5` keeps placements away from clipping into walls during raycast snap.
- **Locked prisms** (after a completed sync) cannot be moved or used as routing waypoints in a new sync.

## Tuning — `data/beacons.toml` `[prism]` section

Verified 2026-05-25.

| Key | Default | Purpose |
|---|---|---|
| `radius` | 1.0 | Hexagon radius |
| `length` | 1.0 | Prism depth |
| `transparency` | 0.4 | Glass alpha |
| `tint_color` | #E0F0FF | Slight blue tint |
| `edge_color`, `edge_glow_intensity`, `edge_thickness` | white/3.0/0.01 | Hex edge highlight |
| `spawn_height` | 3.0 | Height above beacon for the auto-spawned first prism |
| `max_prisms` | 100 | Hard cap per sync (difficulty-overridable) |
| `placement_cost` | 10 | Score deducted per placement |
| `rotation_sensitivity` | 0.05 | Mouse-rotate scale in POV mode |
| `max_placement_distance` | 50.0 | Raycast cap from crosshair |
| `preview_color` | #f2fff9ff | Valid preview |
| `invalid_color` | #FF4444 | Obstructed/invalid preview |
| `pov_camera_offset` | 3.0 | Distance behind prism in POV |
| `pov_camera_vertical_offset` | 0.5 | Y offset to keep beam visible |
| `pov_prism_opacity` | 0.15 | Prism alpha while in POV |
| `pov_transition_duration` | 0.3 | Smooth camera transition seconds |
| `pov_zoom_enabled` | true | Toggle |
| `pov_min_fov` / `pov_max_fov` | 20.0 / 90.0 | FOV bounds |
| `pov_zoom_step` | 5.0 | Per-tick zoom amount |
| `beam_snap_threshold` | 2.0 | Distance for beam to catch prism |
| `placement_snap_threshold` | 2.0 | Distance for preview to snap to beam |
| `wall_offset` | 1.5 | Margin from surface raycast hits |
| `exit_glow_color` | #FFFAE0 | Inside-prism warm light |
| `exit_light_energy`, `exit_light_range` | 0.5 / 0.9 | Inside-prism `OmniLight3D` |

## Implementation

- `src/Nightwalk.Godot/Player/Tools/PrismTool.cs` — the player-side tool (`ITool`/`IPlaceableTool`): preview, raycast, click handling.
- `src/Nightwalk.Godot/Beacons/PrismNode.cs` — the per-instance node: visual hex tube, refraction direction, `RefractBeam(incomingDir)`.
- `src/Nightwalk.Godot/Beacons/PrismPOVController.cs` — POV mode: camera transition, rotation input, zoom, exit.

## Integration

| With | How |
|---|---|
| **Beacon Sync** | `BeaconSyncManager.AddPrism(position)` is the entry point; the manager owns the placed nodes and the beam routing |
| **Player Tool Manager** | Registered as one of the player tools; auto-equipped on `StartSync` |
| **Scoring** | `RemoveScore(placement_cost, PrismPlacement)` per placement |
| **Difficulty** | `GetMaxPrisms(configValue)` and `GetPrismPlacementCost(configValue)` |
| **Input** | Left/right click (place/remove); `E` to toggle POV; mouse-look in POV; wheel zoom in POV |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Preview only when a sync is active.
- [x] Preview snaps to beam within `placement_snap_threshold` (2m).
- [x] Placement deducts `placement_cost` × difficulty.
- [x] Cannot exceed `GetMaxPrisms(max_prisms)`.
- [x] POV enters via `E`, exits via `E` or right-click.
- [x] Mouse rotates prism in POV via `rotation_sensitivity`.
- [x] Scroll wheel adjusts FOV between `pov_min_fov` and `pov_max_fov` in `pov_zoom_step` increments.
- [x] Right-click removes prism and refunds (sync still active).
- [x] Refraction direction = prism forward axis.
- [x] Locked prisms (completed syncs) can't be rotated or removed.

## Related

- [Beacon Sync](beacon-sync.md), [Scoring](scoring.md), [View Modes](view-modes.md).
- ADR-0011 (Beam routing), ADR-0010 (Difficulty).
