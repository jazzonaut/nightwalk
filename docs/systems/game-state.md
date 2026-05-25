# Game State

Top-level lifecycle: `MainMenu` / `Playing` / `Paused`. Each state advertises capability flags that gate cross-cutting behaviour.

> **Engineering decision:** [ADR-0007 — class-per-state machine with capability flags](../adr/0007-state-machine-capability-flags.md).

## States

| State | `AllowsGameTick` | `AllowsPlayerInput` | `ShowsHUD` | `ShowsCursor` |
|---|---|---|---|---|
| `MainMenuState` | false | false | false | true |
| `PlayingState` | true | true | true | false |
| `PausedState` | false | false | (varies) | true |

`PausedState`'s `ShowsHUD` depends on what was visible at pause — see implementation.

## Transitions

```
                  +-----------+
                  | MainMenu  |  ←  start of run
                  +-----------+
                       │
                       │  Player clicks "Play"
                       │  (after difficulty + game-mode selection)
                       ▼
                  +-----------+
       Escape ←   |  Playing  |   → game mode activated
                  +-----------+
                  ▲       │
                  │       │  Escape
                  │       ▼
                  │  +-----------+
   Resume  ───────┘  |  Paused   |   ← Resume / Main menu options
                     +-----------+
                          │
                          │  Quit to menu
                          ▼
                     +-----------+
                     | MainMenu  |
                     +-----------+
```

Implementation: `GameStateManager.TransitionTo<T>()` publishes `GameStateChangedEvent(previous, new)`. Consumers (`GameRoot.OnGameStateChanged`) toggle player input, HUD visibility, and push/pop menu screens.

## Implementation

- `src/Nightwalk.Core/GameState/IGameState.cs` — interface (`Name`, capability flags, `Enter`/`Exit`/`ProcessInput`/`Update`).
- `src/Nightwalk.Core/GameState/GameStateManager.cs` — registry + transitions + flag proxies.
- `src/Nightwalk.Core/GameState/GameStateNames.cs` — string constants.
- `src/Nightwalk.Core/GameState/Events/GameStateChangedEvent.cs`, `SessionResetRequestedEvent.cs` — published events.
- `src/Nightwalk.Godot/GameState/MainMenuState.cs`, `PlayingState.cs`, `PausedState.cs` — concrete states (Godot side because they touch `Input.MouseMode`).

## Integration

| With | How |
|---|---|
| **GameRoot._PhysicsProcess** | Gated on `_stateManager.AllowsGameTick` |
| **GameRoot._Input** | Mouse capture gated on `_stateManager.AllowsPlayerInput` |
| **PlayerEntity.InputEnabled** | Set from `_stateManager.AllowsPlayerInput` on every `GameStateChangedEvent` |
| **GameHUD** | Visible only during `Playing` state |
| **MenuSystem** | Navigator pushes/pops screens based on state (`MainMenuScreen` on MainMenu, `PauseMenuScreen` on Paused) |
| **GameModeManager** | `DeactivateCurrentMode()` on transition to MainMenu; `ActivatePendingMode()` is wired by the New Game flow before transitioning to Playing |
| **`SessionResetRequestedEvent`** | Separate event for "reset everything"; `GameRoot.OnSessionResetRequested` rewinds player/world/score/gameplay/resonance |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Three states registered (`MainMenuState`, `PlayingState`, `PausedState`).
- [x] Capability flags consulted at the gate sites listed above.
- [x] `GameStateChangedEvent` published on every transition.
- [x] Cursor visibility set from each state's `Enter` (`Input.MouseMode = Visible/Captured`).
- [x] Pause works from any input source (handled via state's `ProcessInput`).
- [x] Session reset rewinds player position, tool state, world, gameplay services, resonance, HUD.

## Related

- [Menu](menu.md), [HUD](hud.md), [Game Mode](game-mode.md), [Player Movement](player-movement.md).
- ADR-0007 (the engineering decision in full), ADR-0004 (EventBus — `GameStateChangedEvent`).
