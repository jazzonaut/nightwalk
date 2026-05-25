# Difficulty

Four levels — Observer, Explorer, Wanderer, Architect — that scale costs, rewards, damage, and limits across every gameplay system.

> **Engineering decision:** [ADR-0010 — central `DifficultyService` with hardcoded multiplier curves](../adr/0010-difficulty-multiplier-service.md). All multiplier values for all four levels live in `Nightwalk.Core/Difficulty/DifficultyMultipliers.cs`, **not** in TOML.

## Player-facing summary

- **Observer** — no scoring, no damage, no costs, no limits. Free exploration mode.
- **Explorer** — baseline (1.0× everything).
- **Wanderer** — 1.5× costs, 0.75× rewards, max 4 ziplines, harder fall damage.
- **Architect** — 2.0× costs, 0.5× rewards, max 2 ziplines, brutal fall damage.

Difficulty is chosen from the New Game / settings menu before a session starts. It cannot be changed mid-session (only menu transitions trigger it).

## Multiplier curves

(Reproduced from ADR-0010 for convenience. Source of truth: `DifficultyMultipliers.cs`.)

| Multiplier | Observer | Explorer | Wanderer | Architect |
|---|---|---|---|---|
| Zipline / prism placement cost | free | 1.0× | 1.5× | 2.0× |
| Jetpack energy cost | 0.3× | 1.0× | 1.2× | 1.5× |
| Collectible / beacon points | 0 (scoring off) | 1.0× | 0.75× | 0.5× |
| Combo cap | 1.0× | 1.0× | 0.85× | 0.7× |
| Fall damage threshold | — (disabled) | 1.0× | 0.85× | 0.7× |
| Fall damage per velocity | — | 1.0× | 1.3× | 2.0× |
| Flight score drain | — | 1.0× | 1.3× | 2.0× |
| Max ziplines | unlimited | config | 4 | 2 |
| Zipline max distance | 1.5× | 1.0× | 0.85× | 0.7× |
| Hide score HUD | yes | no | no | no |
| Disable fall damage | yes | no | no | no |
| Disable scoring | yes | no | no | no |
| Free placements | yes | no | no | no |

## Implementation

- `src/Nightwalk.Core/Difficulty/DifficultyLevel.cs` — enum.
- `src/Nightwalk.Core/Difficulty/DifficultyMultipliers.cs` — the curve table as static instances.
- `src/Nightwalk.Core/Difficulty/IDifficultyService.cs`, `DifficultyService.cs` — interface + impl with scaled accessors (`GetCollectiblePoints`, `GetPrismPlacementCost`, `GetMaxZiplines`, etc.).

## API surface (for systems consuming difficulty)

Flag queries: `IsScoringDisabled`, `IsFallDamageDisabled`, `IsScoreHUDHidden`, `ArePlacementsFree`.

Scaled getters: `GetCollectiblePoints(int)`, `GetBeaconRewardPoints(int)`, `GetZiplinePlacementCost(int)`, `GetPrismPlacementCost(int)`, `GetFallDamageThreshold(float)`, `GetFallDamagePerVelocity(float)`, `GetFlightScoreDrain(float)`, `GetEnergyRegen(float)`, `GetJetpackEnergyCost(float)`.

Limit getters with three-state semantics (`-1` = use config, `0` = unlimited, `>0` = absolute): `GetMaxZiplines(int)`, `GetMaxPrisms(int)`, `GetZiplineMaxDistance(float)`.

Setter: `SetDifficulty(DifficultyLevel)` — called by the menu on selection. Synchronous swap; no event published.

## Integration

| With | How |
|---|---|
| **Scoring** | `IsScoringDisabled` short-circuits Add/Remove |
| **Energy** | `GetJetpackEnergyCost`, `GetEnergyRegen` |
| **Player Movement / Fall Damage** | `GetFallDamageThreshold`, `GetFallDamagePerVelocity`, `IsFallDamageDisabled` |
| **Tools** | `GetPrismPlacementCost`, `GetZiplinePlacementCost`, `GetMaxPrisms`, `GetMaxZiplines`, `GetZiplineMaxDistance`, `ArePlacementsFree` |
| **HUD** | `IsScoreHUDHidden` hides score readout in Observer |
| **Beacon Sync** | `GetBeaconRewardPoints` scales the sync reward |
| **Collectibles** | `GetCollectiblePoints` scales the per-pickup award |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Four levels exist as enum + static `DifficultyMultipliers` instances.
- [x] `SetDifficulty(level)` swaps the active multipliers immediately.
- [x] Observer disables fall damage, scoring, and placement costs (`DisableFallDamage`, `DisableScoring`, `FreePlacements`).
- [x] Observer hides the score HUD (`HideScoreHUD`).
- [x] Wanderer caps ziplines at 4; Architect at 2; Observer at 0 (unlimited).
- [x] All scaled getters apply `Disable*` / `FreePlacements` short-circuits where appropriate.
- [x] **No** event is published on `SetDifficulty` — consumers must read on demand or check on session start.

## Related

- ADR-0010 (the engineering decision in full).
- Affects: every gameplay system listed in "Integration" above.
