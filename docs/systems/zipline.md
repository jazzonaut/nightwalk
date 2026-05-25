# Zipline

Player-placed cables between two anchors. Riders are subject to gravity-slide + climb-efficiency physics.

> **Engineering decision:** [ADR-0012 — parametric `t` + gravity-slide + climb-efficiency](../adr/0012-zipline-physics.md).

## Player-facing summary

Two clicks place a zipline cable between anchor points. Walk near the cable and you auto-attach. Gravity slides you toward the lower end; W/S adjusts direction; `Shift` is sprint; `Space` detaches with upward momentum. If another cable is within transfer range, `Space` will jump you onto it instead of detaching into air. Each placement costs 15 points. Anchor-hold lets you rest at endpoints to plan the next move.

## Rules

| Rule | Source |
|---|---|
| Max cables per session | `max_ziplines = 10` (0 = unlimited; difficulty-overridable) |
| Min cable length | `min_cable_length = 5.0` |
| Max placement raycast | `max_distance = 50.0` (difficulty-scaled by `GetZiplineMaxDistance`) |
| Placement cost | 15 (difficulty-scaled by `GetZiplinePlacementCost`) |
| Auto-attach radius | `attachment_distance = 2.0` |
| Jump-transfer radius | `transfer_distance = 8.0` |
| Attach transition | `attach_transition_duration = 0.4` (s) |
| Anchor hold | within `anchor_hold_distance = 2.0` of either end |
| Detach jump force | `detach_jump_force = 5.0` (upward velocity) |
| Detach cooldown | `detach_cooldown = 2.0` (per-cable) |

## Riding physics

See [ADR-0012](../adr/0012-zipline-physics.md) for the algorithmic detail. In short:

- Position is `_cableT ∈ [0, 1]`.
- Velocity is along the cable. Gravity component: `GravitySlideAcceleration * sin(slopeAngle)`. Max slide speed: `MaxSlideSpeed = 15.0`.
- Climbing reduces speed by `climbEfficiency = max(MinClimbEfficiency, 1 - |slope| / (π/2))`; minimum is 0.2 so vertical climbs are slow but possible.
- Sprint (`Shift`) multiplies both climb and slide speed by `RunSpeedMultiplier = 1.6`.
- Hold near anchors: if `_cableT` is within `AnchorHoldDistance / cableLength` of either end and the player isn't inputting away-from-end, velocity is zeroed and position snaps to `AnchorHoldOffset` inside.

## Tuning — `data/tools.toml` `[zipline]` section

Verified 2026-05-25.

| Key | Default | Purpose |
|---|---|---|
| `max_ziplines` | 10 | Hard cap (0 = unlimited) |
| `placement_cost` | 15 | Score per placement |
| `max_distance` | 50.0 | Placement raycast distance |
| `min_cable_length` | 5.0 | Minimum length between anchors |
| `highlight_color`, `highlight_distance` | orange / 50.0 | Hover effect on existing ziplines (for removal) |
| `preview_dot_radius`, `preview_color`, `valid/obstructed/invalid_placement_color` | various | Placement preview |
| `anchor_radius`, `anchor_color`, `anchor_emission`, `anchor_light_*` | cyan neon spheres | Anchor visuals |
| `cable_emission`, `cable_thickness`, `cable_sag` | 3.0 / 0.12 / 3.0 | Cable visuals (sag at midpoint) |
| `attachment_distance` | 2.0 | Auto-attach radius |
| `transfer_distance` | 8.0 | Jump-transfer radius |
| `attach_transition_duration` | 0.4 | Blend-on smoothing |
| `anchor_hold_distance` | 2.0 | Hold zone |
| `anchor_hold_offset` | 1.0 | How far inside to snap when held |
| `gravity_slide_acceleration` | 8.0 | Downhill acceleration |
| `max_slide_speed` | 15.0 | Cap |
| `run_speed_multiplier` | 1.6 | Sprint × |
| `climb_speed` | 5.0 | Base climb speed |
| `climb_acceleration` | 10.0 | Climb ramp-up |
| `min_climb_efficiency` | 0.2 | Floor at vertical |
| `detach_jump_force` | 5.0 | Upward Y on detach |
| `detach_cooldown` | 2.0 | Per-cable re-attach prevention |

## Implementation

- `src/Nightwalk.Godot/Player/Tools/ZiplineTool.cs` — placement tool: two-click flow, preview, raycast.
- `src/Nightwalk.Godot/Ziplines/ZiplineCable.cs` — the placed cable: shape, `GetPositionAtT(t)`, `GetTangentAtT(t)`, `FindClosestT(point)`.
- `src/Nightwalk.Godot/Ziplines/ZiplineAnchor.cs` — anchor visual.
- `src/Nightwalk.Godot/Ziplines/ZiplineRegistry.cs` — store of placed cables + `FindNearestCable(pos, range)`.
- `src/Nightwalk.Godot/Ziplines/ZiplineRider.cs` — ride physics (the algorithm — [ADR-0012](../adr/0012-zipline-physics.md)).

## Integration

| With | How |
|---|---|
| **Player Movement** | `ZiplineRider.TryAttach` tested each tick when not riding; while riding, `ProcessMovement` returns the world position to apply (bypassing `MoveAndSlide`-style movement) |
| **Player Tool Manager** | Registered as a player tool |
| **Scoring** | `RemoveScore(placement_cost, ZiplinePlacement)` per placement |
| **Difficulty** | `GetMaxZiplines`, `GetZiplineMaxDistance`, `GetZiplinePlacementCost` |
| **Events** | `ZiplineCreatedEvent` published on placement |
| **Audio** | Attach/detach SFX from `SfxManager` |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Two-click placement creates a cable; placement raycast respects `max_distance`.
- [x] `min_cable_length` enforced (placement rejected if anchors too close).
- [x] `max_ziplines` cap (difficulty-overridable; 0 = unlimited).
- [x] Placement cost deducted.
- [x] Auto-attach when within `attachment_distance` and moving toward cable.
- [x] Gravity slides player downhill (`gravity_accel = GravitySlideAcceleration * sin(slopeAngle)`).
- [x] Climbing efficiency falls off with slope, floored at `min_climb_efficiency`.
- [x] Sprint applies `run_speed_multiplier` to both slide and climb.
- [x] Anchor hold when within `anchor_hold_distance` of end + not inputting away.
- [x] Space detaches with `detach_jump_force` upward velocity.
- [x] Per-cable `detach_cooldown` prevents immediate re-attach.
- [x] Jump-transfer when another cable within `transfer_distance`.
- [x] Right-click removes hovered zipline.
- [x] Cable removed while riding → graceful detach without teleport.

## Related

- [Player Movement](player-movement.md), [Scoring](scoring.md).
- ADR-0012 (Zipline physics), ADR-0010 (Difficulty).
