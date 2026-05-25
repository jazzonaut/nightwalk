# Menu

Main menu, new-game screen (difficulty + game mode selection), pause menu, settings menu. Driven by game state transitions and a small navigator.

> **Engineering decisions:** none specific. Menus are Godot `Control`-based screens pushed onto a `MenuNavigator` stack; screen lifecycle reacts to [game-state transitions](../adr/0007-state-machine-capability-flags.md).

## Player-facing summary

Game starts in the main menu. Press `Play` → new-game screen with difficulty and game-mode selection → start. During play, `Escape` opens the pause menu (Resume / Settings / Quit to Menu). Settings menu lets you tweak controls, audio levels, and graphics presets; changes write to user TOML and trigger hot reload.

## Screens

| Screen | When shown | What it does |
|---|---|---|
| `MainMenuScreen` | `MainMenuState` | Title + Play / Settings / Quit |
| `NewGameScreen` | After Play selected | Choose difficulty (Observer / Explorer / Wanderer / Architect) and game mode (Silence / Resonance) |
| `PauseMenuScreen` | `PausedState` | Resume / Settings / Quit to Menu |
| `SettingsMenuScreen` | Settings selected | Tabbed: controls, audio, graphics. Writes to user-TOML overlay |

## Navigator

`MenuNavigator` (Godot side) maintains a stack of active screens. The high-level flow:

- `GameStateChangedEvent → MainMenu`: `PopAll()`, `Push<MainMenuScreen>()`.
- `GameStateChangedEvent → Paused`: `PopAll()`, `Push<PauseMenuScreen>()`.
- `GameStateChangedEvent → Playing`: `PopAll()` (no menu visible).
- Settings invoked from either MainMenu or Paused: `Push<SettingsMenuScreen>()`; back button pops it.

## New-game flow

`NewGameScreen` lets the player set:

- Difficulty via `IDifficultyService.SetDifficulty(level)`.
- Game mode via `GameModeManager.SetPendingMode(modeId)` — "silence" or "resonance".

Then transitions to `PlayingState`; `GameRoot.OnGameStateChanged` handler calls `GameModeManager.ActivatePendingMode()`.

## Settings menu

Tabbed `SettingsMenuScreen` reads from `GameDataService` and writes to `UserPreferencesService`. Tabs (visible in code):

- Controls (mouse sensitivity, key bindings)
- Audio (master / SFX / music volumes)
- Graphics (preset selection, window mode, vsync, MSAA, post-processing toggles)

On any change, `UserPreferencesService.Save(...)` writes user-side TOML; `MergingConfigProvider.ClearCache()` invalidates the merge cache; reload fires; `RefreshValues()` re-pulls displayed values to reflect any normalisation.

## Implementation

- `src/Nightwalk.Godot/UI/Menus/MainMenuScreen.cs` — title screen.
- `src/Nightwalk.Godot/UI/Menus/NewGameScreen.cs` — difficulty + game mode picker.
- `src/Nightwalk.Godot/UI/Menus/PauseMenuScreen.cs` — pause overlay.
- `src/Nightwalk.Godot/UI/Menus/SettingsMenuScreen.cs` — settings tabs.
- `src/Nightwalk.Godot/Setup/MenuSystemSetup.cs` — wires up the navigator and screens in `GameRoot`.

## Integration

| With | How |
|---|---|
| **Game state** | Menus push/pop on `GameStateChangedEvent` |
| **Difficulty** | `NewGameScreen` calls `IDifficultyService.SetDifficulty` |
| **Game mode** | `NewGameScreen` calls `GameModeManager.SetPendingMode` |
| **Configuration / user prefs** | `SettingsMenuScreen` reads `GameDataService`, writes `UserPreferencesService` |
| **Audio** | Volume sliders go through `Audio` config + hot reload |
| **HUD** | Hidden while menus are active (Playing-only) |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Main menu shown on game start (`GameRoot.SetupGameStates` transitions to `MainMenuState`).
- [x] Pause menu opens on Escape during Playing.
- [x] Resume returns to Playing (`PausedState` transition).
- [x] Settings menu writes to user TOML and triggers hot reload.
- [x] New-game screen sets difficulty + pending game mode before transition to Playing.
- [x] Quit-to-menu calls `SessionResetRequestedEvent` and returns to MainMenu.
- [x] Settings menu's `RefreshValues` is called on `OnConfigReloaded` so external edits update the UI.

## Related

- [Game State](game-state.md), [Difficulty](difficulty.md), [Game Mode](game-mode.md), [Configuration](configuration.md), [HUD](hud.md).
- ADR-0006 (Hot reload), ADR-0007 (Game state).
