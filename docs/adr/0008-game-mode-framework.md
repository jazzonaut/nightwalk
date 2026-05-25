# ADR-0008: Pluggable game-mode framework (`IGameMode` + `GameModeManager`)

## Status

Accepted (retrofitted 2026-05)

## Context

Nightwalk has two cross-cutting axes that change how the game plays:

1. **Game state** — main menu vs playing vs paused. Lifecycle phases, modelled by [ADR-0007](0007-state-machine-capability-flags.md).
2. **Game mode** — *what kind of game* is being played. Today: `Silence` (pure exploration + puzzles) and `Resonance` (adds Alert/Exposure/Warden/Seeker tension mechanics).

These two axes are orthogonal: a `Resonance` session goes through main-menu → playing → paused like any other, and pausing in resonance doesn't switch to silence. They also have different lifetimes — game state changes many times per session; game mode changes at most once when the player starts a session.

The mode-axis decision could be made many ways:

### Alternatives considered

- **Subclass `PlayingState`** — one playing-state per mode (`SilencePlayingState`, `ResonancePlayingState`). Couples game state and game mode, violating their orthogonality; pause/menu states would need parallel subclasses too.
- **Boolean flag on the game** — `bool _isResonanceMode` checked in many places. Works for two modes; falls apart at three; spreads mode logic across every system that needs to vary by mode.
- **Hard-coded per-mode logic gated on the difficulty system** — overload difficulty to include mode. But difficulty (Observer/Explorer/Wanderer/Architect) scales costs and rewards; mode adds *mechanics*. Different concept; merging them confuses both.
- **Pluggable `IGameMode` registry** — modes are objects with lifecycle hooks (`Enter`/`Exit`/`Tick`/`Process`/`Reset`); a `GameModeManager` owns activation and forwards per-tick/per-frame calls; modes subscribe to events on `Enter` and unsubscribe on `Exit`. Adding a new mode is one class.

## Decision

Use a **pluggable `IGameMode` framework** in `Nightwalk.Core.GameMode`:

### Shape

`IGameMode`:

- `Id`, `DisplayName`, `Description` — identification and UI.
- `Enter()` — subscribe to events, initialise sub-systems.
- `Exit()` — unsubscribe, despawn mode-specific entities, clean up.
- `Tick(double delta)` — physics-rate update for mode logic.
- `Process(double delta)` — frame-rate update for visual/interpolation.
- `Reset()` — reset state for a new session without exiting.

`GameModeManager`:

- `RegisterMode(IGameMode)` — register an implementation under its `Id`.
- `SetPendingMode(string)` — choose what activates on next session start (set by the menu).
- `ActivatePendingMode()` — `Exit` the current mode (if any), `Enter` the pending one.
- `DeactivateCurrentMode()` — exit on return to main menu.
- `Tick` / `Process` / `Reset` — forwarded to the active mode.

`GameModeManager` lives in `Nightwalk.Core`; mode implementations live wherever they fit:

- `SilenceMode` (`Core`) — no-op implementation; the default. Pure exploration + puzzles.
- `ResonanceMode` (`Core`) — owns `AlertMeter`, `ExposureMeter`, score-drain accumulation; subscribes to `CollectibleCollectedEvent`, `AlertChangedEvent`, `MaxExposureEvent`; fires `OnWardenSpawnRequested`, `OnWardenDespawnRequested`, `OnWardenVulnerable` for the Godot side to spawn/despawn `Warden` and `Seeker` nodes; publishes `WardenRepelledEvent` on success.

`ResonanceModeSystem` (Godot) listens for `OnWarden*` events from `ResonanceMode` and bridges to Godot-side scene management (spawning `WardenNode` and `Seeker` actors). The `Core` mode never touches the scene tree.

### Wiring

`GameRoot._Ready()`:

1. Constructs `GameModeManager` immediately after `EventBus` (`GameRoot.cs:77`).
2. Registers both modes after gameplay systems are up (`GameRoot.cs:187-188`).
3. Sets pending mode from `_data.Resonance.Mode.DefaultMode` (TOML-configured default).
4. `ActivatePendingMode()` is *not* called in `_Ready` — it's called when transitioning to `PlayingState` (via the state-changed handler) and `DeactivateCurrentMode()` is called on return to `MainMenuState` (`GameRoot.cs:302`).

The pre-tick lifecycle in `GameRoot._PhysicsProcess` and `_Process` calls `_gameModeManager.Tick(delta)` and `Process(delta)` only while gameplay is allowed (`AllowsGameTick`).

### Verified

- `IGameMode` declares exactly the lifecycle quoted above (`IGameMode.cs`).
- `GameModeManager` holds modes in `Dictionary<string, IGameMode>` and uses `_pendingModeId` for deferred activation (`GameModeManager.cs:14-17`).
- Default mode is sourced from `_data.Resonance.Mode.DefaultMode` (`GameRoot.cs:189`).
- Two mode implementations: `SilenceMode` (no-op), `ResonanceMode` (~245 lines with alert/exposure plumbing).
- `ResonanceMode.Enter()` subscribes to `CollectibleCollectedEvent`, `AlertChangedEvent`, `MaxExposureEvent`; `Exit()` unsubscribes — symmetric (`ResonanceMode.cs:84-99`).
- `ResonanceModeSystem` is the Godot-side scene-tree bridge; `ResonanceMode` itself is `Core`-only.

## Consequences

**Positive:**
- Modes are independently testable: `SilenceMode` and `ResonanceMode` are both `Core` classes constructible with mocked dependencies (just `IEventBus`, `GameDataService`, `IScoreService`).
- Game state and game mode evolve independently. Pausing in resonance keeps resonance state intact; the manager doesn't tick during pause (gated on `AllowsGameTick`).
- Adding a third mode (e.g., a time-trial mode) is one new class plus a `RegisterMode` call.
- Mode mechanics subscribe and unsubscribe symmetrically on `Enter`/`Exit`, so leaks aren't structural — the lifecycle pattern encourages cleanup.
- Per-mode configuration is co-located in TOML (`data/resonance.toml`); other modes can add their own sections.

**Negative:**
- The `Core`-side `ResonanceMode` cannot directly spawn Godot nodes, so it exposes `Action`-typed events (`OnWardenSpawnRequested`, etc.) for the Godot bridge to listen to. This is a second event mechanism *alongside* the `EventBus` ([ADR-0004](0004-event-bus.md)) — they coexist for good reason (these are mode-private callbacks, not domain-wide broadcasts) but the duality is a learning hurdle.
- "Pending mode" is a separate concept from "active mode"; the menu sets pending, gameplay starts activates it. The two-step is necessary (the menu shouldn't activate mode mechanics) but easy to get wrong.
- Modes can only be selected before gameplay starts. Switching mid-session would require `DeactivateCurrentMode` + `ActivatePendingMode`, which currently only the state-change handler triggers.
- `Tick` and `Process` are both forwarded to the mode, but the contract for *what belongs where* is informal. `ResonanceMode.Tick` does the meter updates; `Process` is empty and Godot-side `ResonanceModeSystem` does the visual work.

**Related:**
- [ADR-0007](0007-state-machine-capability-flags.md) — game state vs game mode are orthogonal axes managed by separate state machines.
- [ADR-0010](0010-difficulty-multiplier-service.md) — difficulty is a *third* orthogonal axis (scales costs/rewards within whichever mode is active).
- The Resonance mechanics themselves (Warden behaviour, Seeker AI, alert/exposure curves) are *gameplay design* — they may warrant per-system reference docs but not their own ADRs, since the engineering shape is "Core-side mode object + Godot-side bridge", which is what this ADR captures.
