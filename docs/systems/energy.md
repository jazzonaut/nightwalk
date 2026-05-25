# Energy

Jetpack fuel. Drains while jetpacking, regenerates when not jetpacking and not falling.

> **Engineering decisions:** none specific. See [ADR-0002](../adr/0002-assembly-split.md) — `PlayerEnergy` is a plain Core-shaped class (no Godot dependency) even though it lives Godot-side, so it could move to `Core` later without rework.

## Player-facing summary

Energy is the jetpack's fuel, shown as a bar on the HUD. It drains while you hold `Space` in the air; it refills any time you're not jetpacking and not falling. Hitting zero mid-air cuts the jetpack and you start falling.

## Behaviour

- **Drains** when `PlayerMovement.ProcessNormalMovement` calls `energy.ConsumeJetpack(dt)` — only happens when airborne with `JumpHeld` and energy > 0.
- **Regenerates** when `!JetpackHeld && Velocity.Y >= 0` — confirmed at `PlayerEntity.cs:662-665`. Note: the README claimed regeneration only happens "when grounded"; the code actually allows regen while airborne if you're rising (e.g., right after a jump, before you start falling). It does *not* regen while falling.
- **Capacity** is reset to max on session reset (`Reset()`); on hot reload, current energy is clamped to the new max but otherwise preserved.

## Tuning — `data/gameplay.toml` and `data/physics.toml`

Verified against `PlayerEnergy.Configure` 2026-05-25.

| Key | Default | Used by |
|---|---|---|
| `gameplay.energy.max_energy` | 50.0 | `_energyMax` |
| `gameplay.energy.regen_rate` | 20.0 | `_baseRegenRate` (per second when regenerating) |
| `physics.jetpack.energy_cost_per_second` | 40.0 | `_baseJetpackCost` (per second while held) |

Both `regen_rate` and `jetpack.energy_cost_per_second` are scaled by [`IDifficultyService`](../adr/0010-difficulty-multiplier-service.md) — Observer is 0.3× jetpack cost, Wanderer is 1.2×, Architect is 1.5×.

## Implementation

`src/Nightwalk.Godot/Player/PlayerEnergy.cs` — single class, 103 lines, no Godot dependency (despite being in `Nightwalk.Godot`).

API:

- `Energy`, `EnergyMax`, `EnergyPercent`, `HasEnergy` — read accessors.
- `Configure(GameDataService)` — load from config; preserves current energy on hot reload (clamped to new max).
- `ConsumeJetpack(float dt) → float` — deducts `jetpackCost * dt` (difficulty-scaled), clamped to 0; returns actually-consumed amount.
- `Regenerate(float dt)` — adds `regenRate * dt` (difficulty-scaled), clamped to max.
- `Reset()` — restore to max.
- `SetDifficultyService(IDifficultyService?)` — late binding for difficulty scaling.

## Integration

| With | How |
|---|---|
| **Movement** | `PlayerMovement.ProcessNormalMovement` calls `ConsumeJetpack(dt)` per tick of held jetpack |
| **HUD** | Energy bar reads `EnergyPercent` each frame; `PlayerEntity.Energy/EnergyMax/EnergyPercent` proxies expose the values |
| **Difficulty** | `GetJetpackEnergyCost(base)` and `GetEnergyRegen(base)` apply the difficulty multipliers |
| **Hot reload** | `Configure` preserves current value across reloads (capped at new max) — see [Configuration](configuration.md) |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Energy starts at max on initial load (`Configure` checks `previousMax == 0`).
- [x] Jetpack drains at configured rate × difficulty (`ConsumeJetpack`).
- [x] Energy regenerates when not jetpacking *and* not falling (`PlayerEntity.cs:662`). Diverges from README's "when grounded" claim.
- [x] Jetpack disabled at 0 energy — guarded by `_energy > 0` check at `:75` plus `energy.HasEnergy` check at `PlayerMovement.cs:83`.
- [x] Difficulty multipliers affect consumption and regeneration.
- [x] Energy preserved across hot reload (`Configure` keeps current when `previousMax != 0`).

## Related

- [Player Movement](player-movement.md), [Scoring](scoring.md), [Configuration](configuration.md).
- ADR-0010 (Difficulty), ADR-0006 (Hot reload).
