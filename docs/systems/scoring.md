# Scoring & Combo

Points and a combo multiplier. Every gameplay action that awards points is multiplied by the current combo; the combo decays after an idle window.

> **Engineering decisions:** none specific. The scoring service is a small `Core` service publishing `ScoreChangedEvent` / `ComboChangedEvent` through the [EventBus](../adr/0004-event-bus.md). Difficulty multipliers route through [`IDifficultyService`](../adr/0010-difficulty-multiplier-service.md).

## Player-facing summary

Every collectible, building climb, and beacon sync adds points multiplied by your combo. The combo grows with consecutive actions and decays after 2 seconds of inactivity. Falls hard enough to penalise *also* reset the combo to 1.0×. Score-disabling difficulties (Observer) short-circuit all of this.

## Behaviour

- `AddScore(basePoints, category)` — applies combo multiplier; publishes `ScoreChangedEvent` with the actual (multiplied) points.
- `RemoveScore(points, category)` — does *not* apply the multiplier; clamps to 0. If `category == FallPenalty`, also resets the combo.
- `IncrementCombo()` — `count++; multiplier = clamp(1 + count * ComboIncrement, ..., ComboMaxMultiplier)`. Publishes `ComboChangedEvent` only on >0.01 multiplier change.
- `Update(dt)` — once `_timeSinceLastScore > ComboDecayDelay`, the multiplier decays at `ComboDecayRate` per second; the count is **re-derived from the multiplier** (`newCount = (mult-1)/increment`) so the next `IncrementCombo` doesn't jump back to the old count.
- `IsScoringDisabled` (from difficulty) short-circuits both `AddScore` and `RemoveScore`.

## Tuning — `data/gameplay.toml`

Verified against `ScoreService.cs` 2026-05-25.

| Key | Default | Used by |
|---|---|---|
| `scoring.combo_max_multiplier` | 4.0 | `ComboMaxMultiplier` (cap) |
| `scoring.combo_increment` | 0.25 | Per-action multiplier delta |
| `scoring.combo_decay_rate` | 0.5 | Per-second decay once delay elapses |
| `scoring.combo_decay_delay` | 2.0 | Seconds of inactivity before decay starts |
| `fall_damage.velocity_threshold` | 20.0 | `PlayerFallDamage` — minimum downward speed to penalise |
| `fall_damage.points_per_velocity` | 5.0 | Penalty per velocity unit above threshold |
| `fall_damage.max_penalty` | 500 | Cap on a single fall penalty |
| `fall_damage.min_penalty` | 10 | Floor once the threshold is crossed |
| `score_effects.effect_duration` | 3.2 | `ScoreEffectSystem` post-process fade duration |
| `score_effects.gain_*` | various | Cyan-green tint on `AddScore` |
| `score_effects.loss_*` | various | Orange-red tint on `RemoveScore` |

## Score categories

From `ScoreCategory` enum:

| Category | Source | Sign |
|---|---|---|
| `Building` | First climb of a building rooftop | + |
| `Collectible` | Collected polyhedron | + |
| `BeaconSync` | Completed beacon sync | + |
| `WardenRepelled` | Resonance: repelled the Warden | + |
| `FallPenalty` | Hard landing — **also resets combo** | − |
| `FlightDrain` | Flight mode while moving | − |
| `ZiplinePlacement` | Cost of placing a zipline | − |
| `PrismPlacement` | Cost of placing a prism | − |
| `ExposureDrain` | Resonance: while Seekers detect player | − |
| `MaxExposure` | Resonance: penalty at 100% exposure | − |

README's table did not include the three Resonance categories (`ExposureDrain`, `MaxExposure`, `WardenRepelled`).

## Implementation

- `src/Nightwalk.Core/Scoring/ScoreService.cs` — the service. ~150 lines, `Core`, no Godot.
- `src/Nightwalk.Core/Scoring/IScoreService.cs` — interface (used by `PlayerEntity`, `PrismTool`, `ZiplineTool`, `ResonanceMode`, etc.).
- `src/Nightwalk.Core/Scoring/Events/ScoreChangedEvent.cs`, `ComboChangedEvent.cs` — published events.
- `src/Nightwalk.Godot/Player/PlayerFallDamage.cs` — landing-velocity → `RemoveScore(category: FallPenalty)`.
- `src/Nightwalk.Godot/Rendering/ScoreEffectSystem.cs` — listens to `ScoreChangedEvent` and animates the post-process tint per gain/loss.

## Integration

| With | How |
|---|---|
| **Player Movement** | Flight drain applied while `IsFlightModeActive && HasMovementInput`; fall damage on hard landings |
| **Collectibles** | `CollectibleSpawner` calls `AddScore(category: Collectible)` + `IncrementCombo` on pickup |
| **Buildings** | `BuildingClimbService` calls `AddScore(category: Building)` on first climb |
| **Beacons** | `BeaconSyncManager.CompleteSync` calls `AddScore(category: BeaconSync)` with the reward formula (see [Beacon Sync](beacon-sync.md)) |
| **Tools** | `PrismTool` / `ZiplineTool` call `RemoveScore(category: PrismPlacement/ZiplinePlacement)` on placement |
| **Resonance** | `ResonanceMode` drives `ExposureDrain`, `MaxExposure`, `WardenRepelled` |
| **HUD** | Subscribes to `ScoreChangedEvent` and `ComboChangedEvent` for the score readout and combo display |
| **Difficulty** | `IsScoringDisabled` short-circuits adds/removes (Observer) |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Points awarded with combo multiplier (`AddScore` line 46).
- [x] Score cannot go negative (`RemoveScore` clamps to 0).
- [x] Combo increments per scoring action (`IncrementCombo` callers).
- [x] Combo capped at `ComboMaxMultiplier` (line 91).
- [x] Decay starts after `ComboDecayDelay` (line 130).
- [x] Decay rate matches config (`Update` line 132).
- [x] Fall damage resets combo immediately (`RemoveScore` line 81-84).
- [x] Observer mode disables scoring (`IsScoringDisabled` short-circuit lines 43, 61).
- [x] `ScoreChangedEvent` / `ComboChangedEvent` published on changes.

## Related

- [Player Movement](player-movement.md), [Collectibles](collectibles.md), [Building Climbing](building-climbing.md), [Beacon Sync](beacon-sync.md), [Resonance](resonance.md).
- ADR-0004 (EventBus), ADR-0010 (Difficulty).
