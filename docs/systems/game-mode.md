# Game Mode

A pluggable layer above the base game that adds extra rules and mechanics. Two modes exist today: `Silence` (the default — no extras) and `Resonance` (tension mechanics, see [Resonance](resonance.md)).

> **Engineering decision:** [ADR-0008 — pluggable game-mode framework (`IGameMode` + `GameModeManager`)](../adr/0008-game-mode-framework.md). Game modes are orthogonal to [game state](game-state.md) (Playing/Paused/MainMenu) and [difficulty](difficulty.md).

## Why this exists

The base game has exploration + puzzles. Resonance layers Alert / Exposure / Warden / Seeker mechanics on top. A third mode (time-trial, photo, etc.) would slot in the same way without touching base systems. Modes own their own subscriptions and lifecycle so adding one doesn't ripple through the codebase.

## Modes today

| ID | Display name | Behaviour |
|---|---|---|
| `silence` | Silence | No-op. Pure exploration + beacon puzzles. Default if `resonance.mode.default_mode = "silence"`. |
| `resonance` | Resonance | Alert/Exposure/Warden/Seeker tension layer. See [Resonance](resonance.md). |

## Lifecycle

```
   Main menu
       │
       │  Player selects mode in New Game screen
       ▼
   SetPendingMode(modeId)
       │
       │  Transition to Playing
       ▼
   ActivatePendingMode() → mode.Enter()
       │
       │  Per physics tick
       ▼
   mode.Tick(dt)
       │
       │  Per frame
       ▼
   mode.Process(dt)
       │
       │  Return to Main menu
       ▼
   DeactivateCurrentMode() → mode.Exit()
```

## Default mode

Set in `data/resonance.toml`:

```toml
[mode]
default_mode = "silence"   # or "resonance"
```

## Implementation

- `src/Nightwalk.Core/GameMode/IGameMode.cs` — interface (`Id`, `DisplayName`, `Description`, `Enter`, `Exit`, `Tick`, `Process`, `Reset`).
- `src/Nightwalk.Core/GameMode/GameModeManager.cs` — registry, pending-vs-active separation, activation lifecycle.
- `src/Nightwalk.Core/GameMode/SilenceMode.cs` — no-op default.
- `src/Nightwalk.Core/GameMode/ResonanceMode.cs` — Resonance mode logic (see [Resonance](resonance.md)).
- `src/Nightwalk.Godot/Resonance/ResonanceModeSystem.cs` — Godot bridge: spawns/despawns `WardenNode` and `Seeker` in response to mode-level events.

## Integration

| With | How |
|---|---|
| **Game state** | Mode activates on transition to Playing; deactivates on return to MainMenu (`GameRoot.OnGameStateChanged`) |
| **Menu** | `NewGameScreen` selects pending mode; settings TOML carries the default |
| **EventBus** | Modes subscribe in `Enter`, unsubscribe in `Exit`. Cross-system events (`CollectibleCollectedEvent` etc.) reach the mode like any other subscriber |
| **Difficulty** | Mode-agnostic; both axes apply independently |
| **Resonance Godot bridge** | `ResonanceModeSystem` listens to `Action`-typed events on `ResonanceMode` (`OnWardenSpawnRequested`, etc.) — this is a second event mechanism alongside EventBus, see [ADR-0008](../adr/0008-game-mode-framework.md) consequences |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Two `IGameMode` implementations registered (`SilenceMode`, `ResonanceMode`).
- [x] Default mode loaded from `resonance.mode.default_mode`.
- [x] `SetPendingMode` is decoupled from `ActivatePendingMode` (selection vs activation).
- [x] `Tick` and `Process` only called while `AllowsGameTick`.
- [x] Symmetric `Enter`/`Exit` subscribe/unsubscribe in `ResonanceMode`.
- [x] Return to MainMenu calls `DeactivateCurrentMode()` (so Warden despawns, etc.).

## Related

- [Resonance](resonance.md), [Game State](game-state.md), [Difficulty](difficulty.md), [Menu](menu.md).
- ADR-0008 (the engineering decision in full).
