# Player Movement

Walking, running, jumping, jetpack, and 6-DOF flight, plus the FOV-scaling that goes with flight.

> **Engineering decisions:** none specific to this system. Movement is a straightforward acceleration-toward-target model on `CharacterBody3D` plus a parametric jetpack. See [ADR-0002](../adr/0002-assembly-split.md) for the Core/Godot split that puts `PlayerMovement` (calc) in the Godot assembly while `PlayerEnergy` (state) lives in `Core`.

## Player-facing summary

Ground movement uses acceleration-based physics — velocity ramps up smoothly rather than snapping to max speed, giving a weighty feel. Jump from ground only (no double-jump); holding `Space` after the jump engages the jetpack while there's energy. Flight mode (`Tab`) removes restrictions but drains score while moving. FOV expands with flight speed.

## Modes

| Mode | Entered by | Movement | Notes |
|---|---|---|---|
| **Grounded** | `IsOnFloor()` true | Walk/run on XZ; gravity zeroed on impact | Run held → `RunSpeed`; otherwise `WalkSpeed` |
| **Falling** | Airborne, no jetpack | Gravity applies; XZ momentum decays via friction | |
| **Jetpacking** | Airborne, `JumpHeld`, `energy.HasEnergy` | Vertical thrust capped at `MaxVerticalSpeed`; consumes energy continuously | Energy → 0 transitions to "Hover" |
| **Hover** | Airborne, `JumpHeld`, no energy | Gravity scaled by `HoverGravityScale` (currently 1.0, i.e. no effect) | Configurable softer-fall path; currently a no-op at default tuning |
| **Flight** | `Tab` toggle | 6-DOF; sprint × multiplier; FOV expands with speed | Drains score while `HasMovementInput`; not a `ViewMode` overlay — it *is* a `ViewMode` |

## Inputs

| Action | Default key | Consumed by |
|---|---|---|
| `move_forward` / `move_back` | `W` / `S` | `PlayerInput` → `PlayerInputState.MoveForward` |
| `move_left` / `move_right` | `A` / `D` | `PlayerInput` → `PlayerInputState.MoveRight` |
| `jump` | `Space` | Triggers jump on ground, jetpack in air |
| `run` | `Shift` | Speed modifier (ground + flight sprint) |
| `toggle_flight` | `Tab` | Switches `ViewMode.Walk ↔ Flight` |

Mouse: motion drives camera yaw (on body) + pitch (on `Head`); sensitivity from `physics.movement.mouse_sensitivity`.

## Scene & node footprint

```
PlayerEntity (CharacterBody3D)
├── CollisionShape (CapsuleShape3D: radius 0.4, height 3.0, y-offset 0.9)
└── Head (Node3D, y=2.8 — eye height)
    └── Camera3D (Current = true)
```

Constructed in `PlayerEntity._Ready()`. Capsule radius/height/y-offset and head height are **hardcoded** in `PlayerEntity.cs:198-211` — not in TOML. Changing them is a code edit.

## Tuning — `data/physics.toml`

Verified against `PlayerMovement.Configure` and `PlayerEntity._Ready` on 2026-05-25.

| Key | Default | Used by |
|---|---|---|
| `movement.walk_speed` | 7.0 | `_walkSpeed` (ground target speed) |
| `movement.run_speed` | 12.0 | `_runSpeed` (ground target speed when `RunHeld`) |
| `movement.acceleration` | 40.0 | `_acceleration` (`MoveToward` rate on XZ, also flight axes) |
| `movement.friction` | 30.0 | `_friction` (XZ decay when no input; flight no-input decay) |
| `movement.gravity` | 20.0 | `_gravity` (airborne Y decay; scaled by hover when out of fuel + holding) |
| `movement.mouse_sensitivity` | 0.002 | `PlayerInput` → camera yaw/pitch |
| `jump.force` | 8.0 | `_jumpForce` (initial Y velocity on ground jump) |
| `jetpack.ascend_force` | 15.0 | `_jetpackForce` (per-second Y impulse while jetpacking) |
| `jetpack.max_vertical_speed` | 12.0 | `_jetpackMaxVerticalSpeed` (Y cap during jetpack) |
| `jetpack.hover_gravity_scale` | 1.0 | `_jetpackHoverGravityScale` (gravity scale while holding jump with no energy — currently effectively disabled at 1.0) |
| `jetpack.energy_cost_per_second` | 40.0 | `PlayerEnergy.ConsumeJetpack(dt)` |
| `flight.speed` | 15.0 | `_flightSpeed` |
| `flight.sprint_multiplier` | 5.0 | `_flightSprintMultiplier` (sprint × base) |
| `flight.base_fov` | 75.0 | Camera FOV baseline (set on flight enter) |
| `flight.max_fov_increase` | 15.0 | Added at full `SpeedFactor` |
| `flight.score_drain_rate` | 100.0 | Applied per second while `HasMovementInput` in flight — see Scoring |

## Implementation

- `PlayerEntity.cs` (Godot side) — orchestrator: owns capsule/head/camera, holds the components, drives the physics-process tick, and applies `MoveAndSlide`.
- `PlayerMovement.cs` (Godot side) — pure calculation: `ProcessNormalMovement(...)` for ground+jetpack+hover, `ProcessFlightMovement(...)` for 6-DOF. Returns new velocity; the caller writes it back to `CharacterBody3D.Velocity`.
- `PlayerInput.cs` (Godot side) — input gathering into a `PlayerInputState` struct (`Nightwalk.Core.Input`).
- `PlayerEnergy.cs` (Godot side) — separate component for energy state; documented in [Energy](energy.md).
- `PlayerFallDamage.cs` (Godot side) — separate component for impact-based score penalty; documented in [Scoring](scoring.md).
- `PlayerViewModes.cs` (Godot side) — owns the `ViewMode.{Walk,Flight,BirdsEye}` state and the bird's-eye + flight transitions; documented in [View Modes](view-modes.md).

## Integration

| With | How |
|---|---|
| **Energy** | `ProcessNormalMovement` calls `energy.ConsumeJetpack(dt)` per tick of held jetpack |
| **Scoring** | `PlayerFallDamage` reads landing velocity and `Remove`s score; flight drain is applied externally per frame while `HasMovementInput` |
| **Audio** | `PlayerAudio` reads grounded/flight/jetpack transitions for footstep/jetpack loops |
| **View modes** | `Tab` toggles `ViewMode`; `PlayerEntity` checks `IsFlightModeActive` to pick `ProcessFlight` vs `ProcessNormal` |
| **Difficulty** | Fall-damage threshold and jetpack energy cost go through [`IDifficultyService`](../adr/0010-difficulty-multiplier-service.md) |
| **Zipline** | `ZiplineRider.TryAttach` is called each tick when not riding; while riding, movement bypasses `ProcessNormalMovement` entirely. See [Zipline](zipline.md). |

## Acceptance checklist

Verified against code 2026-05-25. Items marked ⚠️ are claims I could not confirm and should be re-checked.

- [x] Walk/run/strafe at the configured speeds (`PlayerMovement.cs:106`).
- [x] Run modifier requires `Shift` held (`input.RunHeld` consumed at `:106` and `:152`).
- [x] Jump only from ground (`isGrounded && input.JumpPressed`, `:77`).
- [x] Jetpack activates with `Space` held while airborne *and* `energy.HasEnergy` (`:83`).
- [x] Jetpack respects `MaxVerticalSpeed` cap (`:92`).
- [x] Flight enables 6-DOF (`ProcessFlightMovement` uses `headBasis` forward/right, `:144-149`).
- [x] Flight FOV expansion with speed factor (`PlayerViewModes` applies `_baseFov + maxFovIncrease * SpeedFactor`).
- ⚠️ Movement stops at walls — relies on `CharacterBody3D.MoveAndSlide` collision response. Not asserted by code in this system; visible behaviour matches.
- [x] Gravity applies when not grounded (`:97-98`).
- [x] Energy preserved across config hot reload (`PlayerEnergy.Configure` preserves current value; see [Energy](energy.md)).

## Related

- ADR-0002 (Core/Godot split) — `PlayerMovement` is Godot-side because it operates on `Vector3` and basis matrices; `PlayerEnergy` is Core-side because energy is pure state.
- ADR-0010 (Difficulty) — costs and damages route through `IDifficultyService`.
- Systems: [Energy](energy.md), [Scoring](scoring.md), [View Modes](view-modes.md), [Zipline](zipline.md).
