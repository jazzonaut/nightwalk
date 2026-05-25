# ADR-0007: Game state machine with capability flags

## Status

Accepted (retrofitted 2026-05)

## Context

The game has top-level lifecycle phases — main menu, gameplay, pause — that gate substantial cross-cutting behavior: should the world tick? should the player accept input? should the HUD be visible? should the cursor be captured? These questions are asked from many places (the physics loop, the input pipeline, the HUD render path, several Godot setters), and getting any of them wrong creates visible bugs (e.g., world ticks while the pause menu is open, or the cursor disappears in the main menu).

Two questions: **how the lifecycle is modelled** and **how cross-cutting behaviour is gated on it**.

### Alternatives considered (modelling)

- **An enum + switch statements** — `enum GameStatePhase { MainMenu, Playing, Paused }` checked at each gate. Simple, but every gate duplicates the same conditional and adding a new state means touching every site.
- **A class-per-state state machine** — each state is an object implementing a common interface, with `Enter`/`Exit` lifecycle hooks. Transitions go through a manager that publishes a notification. Adding a state is a single new class; the gates ask the manager rather than the enum.
- **A scene-per-state pattern** — each lifecycle phase is its own Godot scene; transitions swap scenes. Heavy-handed and incompatible with running a single persistent main scene that owns the wired-up services (see [ADR-0003](0003-manual-di-no-autoloads.md)).

### Alternatives considered (cross-cutting gates)

- **Per-system if-checks against the current state name** — every gate writes `if (state == "Playing") ...`. Stringly-typed, fragile when states are added.
- **Capability flags on the state interface** — each state declares its capabilities (`AllowsGameTick`, `AllowsPlayerInput`, `ShowsHUD`, `ShowsCursor`) as boolean properties. Gates ask the manager `stateManager.AllowsGameTick` regardless of which state is active. Adding a state forces you to set the flags; adding a new flag forces you to set it in every state (compile error if you forget).
- **Per-system subscription to state-change events** — each system listens for `GameStateChangedEvent` and toggles its own behaviour. Decoupled but spreads the "what does each state allow" knowledge across many subscribers, where the capability-flag approach centralizes it on the state itself.

## Decision

Use a **class-per-state state machine** with **capability flags on the state interface**.

### Shape

`IGameState` (`Nightwalk.Core.GameState`) declares:

- `Name` — display string used in logs and events.
- Capability flags: `AllowsGameTick`, `AllowsPlayerInput`, `ShowsHUD`, `ShowsCursor` (4 booleans).
- Lifecycle: `Enter(IGameState? previousState)`, `Exit(IGameState? nextState)`.
- Per-frame: `ProcessInput(double delta)`, `Update(double delta)`.

`GameStateManager` (`Nightwalk.Core.GameState`):

- Holds a `Dictionary<Type, IGameState>` of registered states.
- `RegisterState<T>(T)` and `TransitionTo<T>()` / `TransitionTo(IGameState)`.
- Proxies the capability flags from the active state (`AllowsGameTick`, `AllowsPlayerInput`, `ShowsHUD`, `ShowsCursor`) so callers ask the manager, not the current state.
- Publishes `GameStateChangedEvent(previousName, newName)` after each transition.

States are concrete classes in `Nightwalk.Godot.GameState`:

| State | `AllowsGameTick` | `AllowsPlayerInput` | `ShowsHUD` | `ShowsCursor` |
|-------|---|---|---|---|
| `MainMenuState` | false | false | false | true |
| `PlayingState` | true | true | true | false |
| `PausedState` | false | false | (varies) | true |

The states live on the Godot side because they call into Godot for cursor mode and similar engine effects (`Input.MouseMode = ...`), but they implement the `Core`-side `IGameState` interface — so the interface contract and the manager are testable independently of Godot.

### Wiring

`GameRoot.SetupGameStates()` constructs the manager, registers `MainMenuState`, `PlayingState`, `PausedState`, subscribes to `GameStateChangedEvent` for cross-cutting updates (player input, HUD visibility, menu navigation), and transitions to `MainMenuState`.

`GameRoot` consults the manager's flags directly: `_PhysicsProcess` checks `_stateManager.AllowsGameTick`, `_Input` checks `_stateManager.AllowsPlayerInput`, the HUD listener flips visibility on the state-changed event.

### Verified

- `IGameState` defines exactly the four capability flags (`IGameState.cs:18-35`).
- `GameStateManager` exposes the same four as proxies (`GameStateManager.cs:24-41`).
- Three concrete states exist: `MainMenuState`, `PlayingState`, `PausedState` (`src/Nightwalk.Godot/GameState/`).
- `GameStateChangedEvent` is published on every transition (`GameStateManager.cs:89`).
- `GameRoot.OnGameStateChanged` reacts by toggling `player.InputEnabled`, `_gameHUD.Visible`, and pushing/popping menu screens (`GameRoot.cs:276-317`).
- The flags are actually consulted: `_PhysicsProcess` (`GameRoot.cs:511`), `_Input` (`GameRoot.cs:626`), `_Process` (`GameRoot.cs:601`).

## Consequences

**Positive:**
- Adding a state is a single new class implementing `IGameState`. The compile error from a missing capability flag forces the author to decide every gate explicitly.
- Adding a capability flag is a single addition to the interface and the manager — the compiler then forces every state to set it.
- The flags are the only contract between the state machine and gameplay code; states themselves can change `Enter`/`Exit` logic freely.
- The `Core`-side interface is testable: a fake state can be constructed, registered, transitioned to, and observed via `GameStateChangedEvent` without Godot.
- Transition logging is uniform (`GameStateManager.cs:86`).
- The flag proxy on the manager means callers don't need null-checks on the current state — defaults fall through (`AllowsGameTick → false`, `ShowsCursor → true`).

**Negative:**
- The four flags currently cover only the cross-cutting concerns we've thought of. A fifth (e.g., "AllowsPause") would be a refactor; the temptation will be to back-door it via per-state event subscriptions rather than extending the interface.
- States hold a reference to the `GameStateManager` (so `MainMenuState.StartGame()` can `_stateManager.TransitionTo<PlayingState>()`), which is a circular dependency tolerated by construction order. New states must follow the same pattern.
- `Enter(previousState)`/`Exit(nextState)` accept references to the other state — useful for "back to where we came from" logic in `PausedState`, but invites cross-state coupling if abused.
- `PausedState` is the only state whose flags vary at runtime (HUD visibility depends on what was visible when the pause started). This is the seam where the rigid capability-flag model strains.

**Related:**
- [ADR-0003](0003-manual-di-no-autoloads.md) — `GameStateManager` is constructed last in `GameRoot._Ready()`, after every subsystem it might gate.
- [ADR-0004](0004-event-bus.md) — `GameStateChangedEvent` is the broadcast mechanism for state-change reactions.
- The Resonance game mode (separate concern; see `CONTEXT.md` and `FINDINGS.md`) is *not* a game state — it lives in `GameModeManager`. The two state machines are orthogonal: game mode persists across state transitions; game state cares about lifecycle (menu vs play vs pause).
