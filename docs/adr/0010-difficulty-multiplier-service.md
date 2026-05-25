# ADR-0010: Difficulty as a multiplier service

## Status

Accepted (retrofitted 2026-05)

## Context

Nightwalk has four difficulty levels — `Observer`, `Explorer`, `Wanderer`, `Architect` — that should affect many tunable values at once: costs (zipline/prism placement, jetpack energy), rewards (collectible/beacon points, combo cap), damage (fall damage threshold and severity, flight score drain), limits (max ziplines, max prisms, zipline range), and some hard flags (Observer disables scoring entirely, hides the score HUD, makes placements free).

The values are mostly *scaled* versions of base config numbers — Wanderer multiplies the prism placement cost by 1.5x, Architect by 2.0x. Some are absolute overrides (Wanderer caps ziplines at 4, Architect at 2, Observer makes them unlimited). Some are pure flags (Observer's `DisableFallDamage`, `DisableScoring`).

The question is where to encode this and how systems consume it.

### Alternatives considered

- **Branch on `DifficultyLevel` in each system** — every system that cares writes `switch (difficulty) { case Observer: ... case Wanderer: ... }`. Logic is local but the multiplier table is shattered across the codebase. Adding a difficulty level means touching every site.
- **Separate config-per-difficulty** — `data/physics_observer.toml`, `data/physics_wanderer.toml`, etc. The TOML hot-reload pipeline ([ADR-0006](0006-toml-hot-reload.md)) loads the right one. Editing the curve is a designer task. Downside: enormous duplication; a tweak to the base value has to be applied four times; comparing across difficulties is hard.
- **Central `DifficultyService` with per-difficulty `DifficultyMultipliers` data** — services and systems request scaled values through the service (`GetCollectiblePoints(basePoints)`, `GetPrismPlacementCost(baseCost)`, etc.). The base values stay in their existing configs; the *deltas* across difficulties live in one place.

## Decision

A **central `DifficultyService`** in `Nightwalk.Core.Difficulty` exposing scaled accessors. The four difficulty curves live as static `DifficultyMultipliers` instances on the `DifficultyMultipliers` class — *not* in TOML. Systems that depend on difficulty take `IDifficultyService` as a constructor dependency and ask for scaled values rather than reading config directly.

### Shape

`DifficultyService` exposes three categories:

1. **Flags** (boolean queries): `IsScoringDisabled`, `IsFallDamageDisabled`, `IsScoreHUDHidden`, `ArePlacementsFree`. Used for branch-like behaviour: "in Observer, don't show the score HUD at all."
2. **Scaled-value getters** (multiplicative, sometimes with short-circuit): `GetCollectiblePoints(int basePoints)`, `GetBeaconRewardPoints`, `GetZiplinePlacementCost`, `GetPrismPlacementCost`, `GetFallDamageThreshold`, `GetFallDamagePerVelocity`, `GetFlightScoreDrain`, `GetEnergyRegen`, `GetJetpackEnergyCost`. Each takes a base config value and returns the scaled version. Flag-driven shortcuts apply where appropriate (Observer's `FreePlacements` returns 0; `DisableScoring` returns 0 from reward getters).
3. **Limit getters with three-state semantics** (`GetMaxZiplines`, `GetMaxPrisms`): the multiplier can be **absolute > 0** (use this), **0** (unlimited), or **-1** (fall back to config). Letting Observer's "unlimited" and Architect's "exactly 2" coexist with Explorer's "use whatever the config says" required this three-state encoding.

`DifficultyMultipliers` is a plain `init`-only data class. The four instances (`Observer`, `Explorer`, `Wanderer`, `Architect`) are `static` properties initialised once. `DifficultyMultipliers.ForLevel(level)` is the lookup.

Setting the difficulty is `SetDifficulty(DifficultyLevel)` — called by the menu when the player chooses a difficulty. The service immediately swaps in the corresponding `DifficultyMultipliers` instance; subsequent getter calls return scaled values for the new level. No event is published; consumers read on demand.

### Verified

- `DifficultyService` constructed in `GameRoot._Ready()` immediately after `EventBus` (`GameRoot.cs:74`).
- Difficulty multipliers are **hardcoded** as static instances on `DifficultyMultipliers`, not loaded from TOML.
- Consumers include: `ScoreService` (via `IsScoringDisabled`), `PlayerEnergy` (jetpack cost), `PlayerFallDamage` (threshold and severity), `PrismTool` and `ZiplineTool` (placement cost and limits), `GameHUD` (`IsScoreHUDHidden`), `GameRoot.OnGameStateChanged` (reconfigure HUD on session start).
- The four curves are summarised in the table below (full values in `DifficultyMultipliers.cs`).

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
| Hide score HUD | yes | no | no | no |

## Consequences

**Positive:**
- Difficulty curves are in one file. Adding a new level means adding one static instance to `DifficultyMultipliers` and one case to the enum and `ForLevel`. No system code changes.
- Systems consume difficulty through typed methods (`GetPrismPlacementCost(baseCost)`) — the method name documents intent and lets the service handle short-circuits (e.g., `FreePlacements` shortcut in `GetPrismPlacementCost`).
- The base config values stay as designer-tuned numbers; difficulty multipliers compose on top without rewriting them per-level.
- The flag/multiplier/absolute split mirrors what difficulty actually means in practice — sometimes you scale, sometimes you cap, sometimes you switch off.
- Easy to test: `DifficultyService` is `Core`, no Godot dependency; setting a level and checking a getter is two lines.

**Negative:**
- **Difficulty curves are hardcoded.** A change to "make Wanderer 0.8× instead of 0.75× rewards" requires a code edit and recompile. Designers cannot tune difficulty curves at runtime via hot reload ([ADR-0006](0006-toml-hot-reload.md)), unlike everything else. This is the biggest tradeoff and worth revisiting if difficulty becomes a frequently iterated dimension.
- The three-state limit encoding (`-1` = use config, `0` = unlimited, `>0` = absolute) is clever but easy to misread. A boolean + nullable would be clearer; the current form is concise but requires reading the getter to understand.
- Difficulty changes mid-session are technically supported (`SetDifficulty` is callable) but only the next reads pick up the change — already-spent energy or already-locked-in beacon rewards aren't retroactive. The UI guards against this by only offering difficulty selection from the main menu.
- Consumers must remember to route through the service. A direct `config.Beacons.Rewards.BasePoints` read bypasses difficulty entirely, and there's no compile-time signal that this is wrong.
- No event published on `SetDifficulty` — systems polling getters get the new value next tick; systems that *cache* a scaled value (e.g., a one-shot config snapshot) wouldn't see it. None do currently; if one is added later, this could surprise.

**Related:**
- [ADR-0006](0006-toml-hot-reload.md) — base values are hot-reloadable; difficulty multipliers are not.
- [ADR-0008](0008-game-mode-framework.md) — game mode (Silence/Resonance) and difficulty are independent axes. A Resonance session can run at Architect, Observer, etc. The two services compose by applying both stacks of rules to the same base values.
