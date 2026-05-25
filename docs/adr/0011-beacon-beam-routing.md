# ADR-0011: Beacon beam routing via iterative ray-march with prism snap

## Status

Accepted (retrofitted 2026-05)

## Context

The beacon synchronization puzzle requires a beam to travel from an activated source beacon, optionally pass through any number of player-placed prisms that refract it, and terminate either at a same-frequency target beacon (success) or at geometry (or empty space) at maximum length (failure). The beam is *also* rendered, snaps onto nearby prisms during placement, and is recomputed every time a prism is added, removed, or rotated — many times per second under active play.

Two broad approaches:

### Alternatives considered

- **Physics-simulated reflection** — emit a small projectile from the beacon and let Godot's physics step compute reflections off prism collision shapes. Visually authentic, but slow (the path must converge in a single physics step), unstable at high angles, hard to test, and incompatible with the "rebuild the entire beam on prism rotation" UX where the beam must update instantly per frame.
- **Constraint solver** — formulate the puzzle as "find any path from source to target via N prisms" and solve. Overkill for a player-driven puzzle where the path *is* the choice.
- **Iterative ray-march with prism-snap** — start at the beacon, walk forward; at each step, find the closest active prism within a snap threshold of the current ray; if found, jump to the prism, apply its refraction direction, continue; if none, raycast to find the terminal geometry hit. The beam is just a sequence of straight segments. Trivially fast, deterministic, animation-free.

The third approach matches how the puzzle actually plays — players treat each prism as a "kink" in the beam, not an optical surface. They aim by rotating prisms in `PrismPOV`, and they expect a placed prism to immediately "catch" a beam passing nearby.

## Decision

The beam is computed as an **iterative ray-march with prism-snap**, implemented in `BeaconSyncManager.CalculateBeamPath`.

### Algorithm

1. Start at the beacon's emission point (`beacon.GlobalPosition + Vector3.Up * Beacon.Height`).
2. Initial direction: `Vector3.Up`.
3. For up to `MaxPrisms` iterations:
   - Find the closest active, non-locked, non-visited prism within `BeamSnapThreshold` of the current ray. "Closest" means smallest `t` along the ray (so the beam catches the *first* prism it would pass, not the spatially nearest).
   - If found: append the prism's position to the path, mark it visited, set the new direction to `prism.RefractBeam(currentDir)`, advance `currentPos` to the prism.
   - If none: break.
4. After the loop ends, **raycast from `currentPos` in `currentDir`** using Godot's `PhysicsRayQueryParameters3D.IntersectRay`, up to `SignalBeamMaxLength`. The raycast is started a small offset (`Prism.Radius + 0.5`) past the current position so it doesn't immediately hit the prism we just exited. All active prism RIDs are added to the `Exclude` list to defend against stale collision shapes.
5. The terminal segment ends at the raycast hit, or at `currentPos + currentDir * maxBeamLength` if there's no hit (the beam is then "broken").
6. Separately, `CheckBeaconHitAlongRay(segmentStart, segmentEnd)` tests if any segment passes within `BeamSnapThreshold` of a same-frequency target beacon. If so, the sync completes — the endpoint snaps onto the beacon top and `CompleteSync` runs.

### Key invariants

- The **visited set** (`_visitedPrismsBuffer`) prevents two prisms from forming a loop.
- The **per-iteration cap** is `MaxPrisms` — even without the visited set, the loop terminates.
- **Locked prisms are skipped** during routing (`prism.IsLocked` from a completed sync are decorative remnants and don't participate in new routing).
- The raycast at step 4 happens **once per beam recomputation**, not per segment — refraction segments are determined entirely by prism geometry, no physics needed.

### Performance shape

- `UpdateBeamPath` is throttled to **60Hz** (`BeamUpdateMinInterval = 1.0 / 60.0`) — the puzzle UX doesn't need higher refresh.
- `_beamPathBuffer`, `_visitedPrismsBuffer`, and `_excludeRidsBuffer` are **reused** across calls to avoid allocations during active sync.
- Prism-search is `O(prisms_in_world * iterations)` per recompute. With a typical session of <20 prisms this is negligible.

### Verified

- `CalculateBeamPath` (`BeaconSyncManager.cs:671-753`) implements exactly the algorithm above.
- `FindClosestPrismOnBeam` (`:759-791`) does the ray-to-point distance check via `RaycastHelper.PointToRayDistance`, picking the smallest `t` above `0.1`.
- The physics raycast uses `query.CollideWithAreas = false` and a populated `Exclude` list for active prism RIDs (`:725-733`).
- Inactive prisms hit by the raycast are detected (`PrismNode.FindFromCollider(hitCollider) != null`) and treated as "broken" (the beam reaches max length).
- Beam-update throttling at 60Hz: `BeamUpdateMinInterval = 1.0 / 60.0` (`BeaconSyncManager.cs:44`).
- Reusable buffers: `_beamPathBuffer`, `_visitedPrismsBuffer`, `_excludeRidsBuffer` declared once (`:39-41`).

## Consequences

**Positive:**
- Recomputation is essentially free at puzzle scale. Rotating a prism updates the beam in the next 60Hz tick.
- The beam is a list of points, trivially renderable by `SignalBeamRenderer` as a polyline.
- Deterministic — no physics-step timing or frame-rate sensitivity.
- The snap behaviour ("prism catches a nearby beam") matches the puzzle's mental model and forgives imprecise placement.
- Loop prevention is structural (visited set + iteration cap), not heuristic.
- The Godot physics raycast does only the final hit-test, not the whole path — so changes to scene physics don't ripple through the puzzle's stability.

**Negative:**
- The snap-to-prism logic uses the *smallest `t` along the ray*, not the smallest perpendicular distance. A prism slightly to the side that's *closer* might be skipped in favour of one further along the ray. This is correct for the puzzle ("catch the first prism the beam reaches") but counterintuitive when debugging.
- The `0.1f` threshold on `t > 0.1f` for skipping nearly-coincident prisms is a magic number — a prism placed very close to the current beam origin (e.g., a stacked placement) might be missed.
- `CollideWithAreas = false` means area-typed shapes (sometimes used for triggers) don't block the beam. Currently this is intended; the future failure mode is "we added a trigger volume and the beam started passing through it unexpectedly."
- The raycast-start offset (`Prism.Radius + 0.5`) is empirical. Smaller and you get self-hits on the prism just exited; larger and a very close target may be skipped.
- The visited set prevents loops but also prevents a beam from re-entering a prism it already passed — which is correct for this puzzle but rules out "wrap-around" puzzle designs.
- "Broken" is a single boolean — there's no diagnostic for *why* (out of length vs hit-inactive-prism vs no-physics-state). Useful for the player; less useful when debugging.

**Related:**
- The Prism Tool ([per-system reference, not an ADR](#)) places `PrismNode` instances. The tool relies on the snap behaviour described here to give the placement preview useful feedback.
- `RaycastHelper.PointToRayDistance` / `SegmentToSegmentDistance` are pure math helpers used widely — pure `Godot`-side utility, no ADR-worthy decision behind them.
- [ADR-0010](0010-difficulty-multiplier-service.md) — `MaxPrisms` here is the *difficulty-scaled* value via `IDifficultyService.GetMaxPrisms`. The algorithm itself doesn't change with difficulty; only the iteration cap and the bonus arithmetic do.
