# HUD

In-game overlay: score, combo multiplier, energy bar, current tool, current view mode, preset feedback, FPS counter (debug), and Resonance-mode meters.

> **Engineering decisions:** none specific. HUD is `CanvasLayer`-based UI listening to [EventBus](../adr/0004-event-bus.md) events.

## Player-facing summary

The HUD shows your current score (with combo multiplier when > 1.0×), an energy bar for the jetpack, the active tool indicator, and a brief flash when switching tools, view modes, or visual presets. In Resonance mode, additional meters appear for Alert and Exposure. In Observer difficulty, the score is hidden.

## Visibility rules

- HUD is hidden during `MainMenuState` and `PausedState`; visible only during `PlayingState`.
- Within Playing, HUD is reconfigured per state change: `ShowFps` from config; `HideScoreHUD` from `IDifficultyService.IsScoreHUDHidden` (true in Observer).

## Elements

| Element | Subscribes to / reads | Implementation |
|---|---|---|
| Score readout | `ScoreChangedEvent`, `ComboChangedEvent` | `GameHUD` (text) |
| Combo multiplier | `ComboChangedEvent` (shown when > 1.0) | `GameHUD` |
| Energy bar | `PlayerEntity.EnergyPercent` per frame | `GameHUD` |
| Current tool | `PlayerToolManager.OnToolChanged` (event) | `GameHUD.ShowTool` |
| View mode indicator | `ViewModeChangedEvent` | `GameHUD` |
| Preset feedback | `VisualPresetManager.OnPresetChanged` | `GameHUD.ShowPreset` |
| FPS counter | Direct `Engine.GetFramesPerSecond()` poll | `GameHUD.Configure(showFps)` |
| Alert meter (Resonance) | `AlertChangedEvent` | Resonance UI |
| Exposure meter (Resonance) | `ExposureChangedEvent` | Resonance UI |
| Score gain/loss popups | `ScoreChangedEvent` | `ScorePopup` (`FadingLabel`) |

## Implementation

- `src/Nightwalk.Godot/UI/HUD/GameHUD.cs` — root HUD `CanvasLayer`; subscribes/unsubscribes in `Connect`/`Disconnect`.
- `src/Nightwalk.Godot/UI/HUD/HUDBackground.cs` — translucent backplates.
- `src/Nightwalk.Godot/UI/HUD/HUDTheme.cs` — colors, fonts, sizes (hardcoded constants).
- `src/Nightwalk.Godot/UI/HUD/FadingLabel.cs` — short-lived label with alpha fade.
- `src/Nightwalk.Godot/UI/HUD/ScorePopup.cs` — `FadingLabel` driven by `ScoreChangedEvent` for "+30" / "-15" floaters.

The HUD's lifecycle:

1. Constructed in `GameRoot._Ready()` and added as a child.
2. `Configure(showFps)` from `Core.Debug.ShowFps`.
3. `Connect(eventBus)` subscribes to all events.
4. `SetPlayer(player)` for energy readout polling.
5. `Visible = false` until `GameStateChangedEvent` with `Playing`.
6. `Reset()` on session reset; `Disconnect()` on shutdown.

## Integration

| With | How |
|---|---|
| **Scoring** | `ScoreChangedEvent` / `ComboChangedEvent` → score readout + combo display + popups |
| **Player Movement** | `EnergyPercent` polled each frame for energy bar |
| **Tools** | `PlayerToolManager.OnToolChanged` → tool indicator |
| **Game State** | Visibility gated by `_stateManager.AllowsGameTick` proxy on `GameStateChangedEvent` |
| **Difficulty** | `IsScoreHUDHidden` toggles score visibility on session start |
| **Resonance** | Alert/Exposure meters appear when `ResonanceMode` is active |
| **Configuration** | Hot reload re-applies `ShowFps` and `IsScoreHUDHidden` |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Hidden in `MainMenu` and `Paused` states; visible only in `Playing`.
- [x] Observer difficulty hides the score readout.
- [x] FPS toggleable via `core.debug.show_fps`.
- [x] Score and combo update from EventBus events, not polling.
- [x] Energy bar polls `EnergyPercent` per frame.
- [x] Tool changes update indicator immediately.
- [x] Session reset clears score/combo readouts.
- [x] Disconnect on shutdown unsubscribes all event handlers.

## Related

- [Scoring](scoring.md), [Energy](energy.md), [View Modes](view-modes.md), [Game State](game-state.md), [Difficulty](difficulty.md), [Resonance](resonance.md), [Menu](menu.md).
- ADR-0004 (EventBus), ADR-0007 (Game state).
