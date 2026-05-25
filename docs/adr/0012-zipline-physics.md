# ADR-0012: Zipline physics — parametric `t` + gravity-slide + climb-efficiency

## Status

Accepted (retrofitted 2026-05)

## Context

The zipline lets the player attach to a cable between two anchor points and traverse it. The motion should feel rideable: gravity pulls the player down a sloped cable, the player can climb against gravity (but only so far), holding `Shift` makes them faster, jumping off detaches with momentum, and proximity to either anchor pins them in place so they can stop and survey before continuing.

Three broad ways to model this:

### Alternatives considered

- **Full physics constraint** — model the cable as a path constraint on the player's `CharacterBody3D`, let Godot's physics resolve forces and gravity. Authentic but expensive, sensitive to integration step, hard to make "feel right" without per-cable tuning, and the on/off attachment is awkward.
- **Fixed speed along cable** — once attached, the player moves at a constant speed in their input direction. Trivial but feels arcade-y and ignores cable slope entirely — uphill and downhill feel identical, which loses the central read of "this cable is going down, lean into it."
- **Parametric `t` with gravity-slide + climb-efficiency** — represent the player's position as a normalised parameter `t ∈ [0, 1]` along the cable. Each tick: derive the cable tangent at `t`, compute slope from `tangent.Y`, apply gravity along the cable direction, modulate climbing speed by slope steepness. Detaches and attachments are explicit state transitions.

The third matches the design goal — slopes drive the feel — while being cheap, deterministic, and decoupled from the player's main `MoveAndSlide` physics.

## Decision

`ZiplineRider` (Godot-side) models cable traversal with a **parametric `t` ∈ [0, 1]** along the cable, with these mechanics:

### State

- `_currentCable: ZiplineCable?` — the attached cable.
- `_cableT: float` — normalised position along the cable.
- `_cableVelocity: float` — current speed *along the cable* (positive = toward end-anchor).
- `_cooldownCable: ZiplineCable?`, `_cooldownTimer: float` — per-cable re-attachment cooldown after a deliberate jump-off.
- `_transition: SmoothTransition` — blend of the player's world position over `AttachTransitionDuration` after attach.

### Per-frame `ProcessMovement(dt, moveInput, runHeld, playerForward, currentPosition)`

1. **Cable-removed check** — if the cable was deleted from the registry while riding, detach without jump and return current position (don't teleport).
2. **Transition step** — if mid-attach blend, advance the transition and early-return its position.
3. **Tangent + slope** — `tangent = cable.GetTangentAtT(_cableT)`; `slopeAngle = asin(-tangent.Y)` (positive = cable goes downhill in +tangent direction).
4. **Facing-relative input direction** — `moveInput` is W/S (-1..+1); the *cable* direction the player intends depends on whether they're facing along or against the cable's horizontal projection. `facingDirection = sign(horizontalTangent · horizontalForward)`.
5. **Anchor hold** — if `_cableT` is within `AnchorHoldDistance/cableLength` of either end *and* the player is not inputting movement away from that end, snap to `holdOffsetT` (just inside the threshold) and zero the velocity. Optionally play a brief transition into the hold position.
6. **Climbing vs sliding** — if `moveInput` is significant:
   - Direction *opposes* slope → climbing. Compute `climbEfficiency = max(MinClimbEfficiency, 1 - slopeRatio)` where `slopeRatio = |slopeAngle| / (π/2)`. Steeper slope → lower efficiency, bounded below by `MinClimbEfficiency` so even vertical cables remain climbable, slowly. Accelerate toward `climbSpeed * climbEfficiency * inputDirection` at `ClimbAcceleration`.
   - Direction *with* slope → gravity-assisted ride. Target speed jumps to `±maxSlideSpeed` (sign matches input × slope). `MoveToward` at the same `ClimbAcceleration`.
   - No input → accumulate `gravityAccel = GravitySlideAcceleration * sin(slopeAngle)` into `_cableVelocity` directly.
7. **Sprint** — `runHeld` applies `RunSpeedMultiplier` to *both* `climbSpeed` and `maxSlideSpeed`.
8. **Velocity clamp + drag** — clamp to `±maxSlideSpeed`; on flat cable with no input, apply `* 0.98` per tick as light friction.
9. **`t`-update** — `tDelta = velocity * dt / cableLength`. `_cableT += tDelta`. Clamp to `[0, 1]`; if hitting an end, allow velocity only in the away-from-end direction.
10. **Position out** — return `cable.GetPositionAtT(_cableT)`.

### Attach / detach

- `TryAttach` — finds nearest cable within `AttachmentDistance`; skips if cable is on cooldown; calls `AttachToCable(cable, t, startPosition)` which begins the smooth transition.
- `TryTransferTo` — explicit jump-to-targeted-cable (e.g., when the player is looking at another cable and presses jump). No cooldown for intentional transfers, only for jump-off detaches.
- `Detach(applyJump)` — extracts current cable tangent × `_cableVelocity` for exit momentum, optionally adds `DetachJumpForce` to Y, applies cooldown only if `applyJump` (prevents accidental re-grab; resets and cable deletion skip cooldown).

### Verified

- `ZiplineRider.cs:174-308` implements exactly the loop above.
- `_cableT`, `_cableVelocity`, `_cooldownCable`, `_cooldownTimer` are the entire mutable state (`:18-27`).
- Anchor-hold logic at `:218-233`.
- Climbing efficiency: `climbEfficiency = max(MinClimbEfficiency, 1 - slopeRatio)` at `:253-254`.
- Gravity-only mode (`moveInput < 0.1`): `_cableVelocity += gravityAccel * dt` at `:276`.
- Per-cable cooldown applied only on `applyJump` detach (`:152-156`).
- All tuning values live in `data/tools.toml` under `[zipline]`; difficulty scales `MaxDistance` and `MaxZiplines` via [ADR-0010](0010-difficulty-multiplier-service.md).

## Consequences

**Positive:**
- Cheap and deterministic. No physics-step interaction; per-frame work is a few transcendentals plus a couple of `MoveToward` calls.
- The feel maps directly to physical intuition: steep cables are fast to ride, slow to climb; flat cables coast with light friction; anchors are a natural rest point.
- Attach/detach are explicit state transitions with their own cooldowns and transitions — no "stuck on the cable" failure modes.
- The transition (`SmoothTransition`) on attach hides the snap-to-cable teleport that would otherwise be jarring.
- Direction-relative-to-facing means W always means "where I'm looking", regardless of which way the cable was placed. Players don't need to learn "this cable runs north".
- Per-cable cooldown (vs global cooldown) means you can jump off cable A and immediately grab cable B, but not bounce off cable A and re-grab it.

**Negative:**
- The model is *not* physically accurate. A cable has no mass, no sway, no bend under load. This is invisible to most players but rules out future mechanics that would want those.
- The cable's shape (sag) is fixed by `ZiplineCable.GetPositionAtT` — the rider just samples that. Changing sag at runtime (e.g., dynamic loading) would require recomputing tangents and could cause discontinuities in `_cableVelocity`.
- The `0.98` friction multiplier on flat cables is a magic number. Slightly more or less changes how "draggy" coasting feels.
- The climbing-efficiency curve is linear in slope angle, bounded below — simple but not physically motivated. Players might expect a non-linear "wall climbing" cliff, or a stamina component.
- "Anchor hold" uses an offset (`AnchorHoldOffset`) just inside the threshold (`AnchorHoldDistance`), so the player snaps to a fixed position when in the hold zone. This is visible: if a player enters the hold zone from a steep slide, the velocity zeros and the position snaps. Acceptable but felt.
- `_cableT` and `_cableVelocity` are world-frame-rate-independent only insofar as `dt` is well-behaved. A long frame would integrate gravity inaccurately. Standard symplectic-Euler caveats apply.
- The detach cooldown applies only to the *specific* cable jumped off, not to "any cable". A dense zipline network can re-grab almost immediately, which may or may not be desirable.

**Related:**
- `ZiplineCable.GetPositionAtT` / `GetTangentAtT` define the cable shape; the rider doesn't need to know it's a catenary or a polynomial or a polyline. Loose coupling, but it means changing the cable shape implementation is silent — no test catches a regression.
- [ADR-0010](0010-difficulty-multiplier-service.md) — `MaxPlacementDistance` and `MaxZiplines` come through `IDifficultyService`. The rider physics itself doesn't change with difficulty.
- `ZiplineRegistry` (the Godot-side store of placed cables) is referenced by the rider for attach/transfer searches. Not an ADR-worthy decision (it's just a list with a "find nearest" helper).
