# ADR-0003: Manual dependency injection in `GameRoot`; no Godot autoloads

## Status

Accepted (retrofitted 2026-05)

## Context

Godot's preferred way to expose long-lived services is **autoloads**: scripts registered in `project.godot` under `[autoload]` that are instantiated by the engine before the main scene loads and accessible as globals everywhere. Each autoload becomes effectively a singleton with an engine-managed lifecycle.

Nightwalk has many cross-cutting services that need shared lifetimes — `EventBus`, `GameDataService`, `DifficultyService`, `ScoreService`, `GameStateManager`, `GameModeManager`, plus per-system services like `BuildingTracker`, `CollectibleTracker`, `CityAlivenessService`. The question is how they are constructed, when, and how downstream consumers obtain references.

### Alternatives considered

- **Godot autoloads** — declare each service as a `[autoload]` entry. The engine instantiates them in declared order before the main scene loads. Pros: zero plumbing, globally accessible. Cons: (a) constructors can't take dependencies because the engine builds them via `new()`, forcing post-hoc wiring through globals; (b) instantiation order is hidden in `project.godot` and easy to break silently; (c) accessing autoloads requires `GetNode("/root/...")` or generated C# accessors, neither of which surface dependency edges in code; (d) autoloads cannot live in `Nightwalk.Core` (they must extend `Node`), making the cross-assembly split (ADR-0002) impossible.
- **A DI container (Microsoft.Extensions.DependencyInjection, Autofac, etc.)** — register services, let the container construct them. Pros: full DI, easy testing. Cons: substantial dependency for a single-player game; runtime resolution; the wiring order and dependency edges are spread across registration code rather than visible in a single place.
- **Manual construction in a single entry point** — one class (`GameRoot`) instantiates services in explicit order, passing dependencies as constructor arguments. Pros: dependency edges are read top-to-bottom in `_Ready()`; no hidden globals; `Core` services need not extend `Node`; trivial to break apart for testing. Cons: requires discipline to keep the entry point short; manual cleanup.

## Decision

Use **manual DI in `GameRoot._Ready()`**. No Godot autoloads — `project.godot` contains no `[autoload]` section. `GameRoot` (the only script attached to the root of `scenes/Main.tscn`) is responsible for constructing every long-lived service, in dependency order, and handing references to downstream consumers via constructor arguments, setter methods, or setup-helper return objects.

Process priority is set to **`ProcessPriority = -100`** in `_Ready()` so `GameRoot._Process` runs *before* any other `_Process` on the same frame (notably before rendering interpolation runs on entities).

Wiring is broken into small `*Setup` helpers (`EnvironmentSetup`, `PostProcessingSetup`, `SkySystemSetup`, `AudioSystemSetup`, `WorldInitializer`, `PlayerSpawner`, `GameplaySystemsSetup`, `MenuSystemSetup`) that take the dependencies they need, instantiate a cluster of related objects, and return a `*Result` record. `GameRoot` holds the result records and orchestrates them.

Verified:
- `project.godot` contains no `[autoload]` block (grep for `autoload` returns nothing).
- `GameRoot.cs` is the only `_Ready()` orchestrator; service construction is explicit and ordered:
  1. `Log.SetLogger(...)`
  2. `_eventBus = new EventBus()`
  3. `_difficultyService = new DifficultyService()`
  4. `_gameModeManager = new GameModeManager(_eventBus)`
  5. `GameDataService` via `MergingConfigProvider` (system + user TOML)
  6. World, audio, player, gameplay, resonance, score effects
  7. `GameStateManager` (registered last, then `TransitionTo<MainMenuState>()`)
- `ProcessPriority = -100` is set as the first line of `_Ready()` (`GameRoot.cs:64`).
- `_ExitTree()` calls `CleanupGame()`, which unsubscribes from events and tears down services in reverse order.

## Consequences

**Positive:**
- Every dependency edge is visible by reading `GameRoot._Ready()` top to bottom. There is no hidden "I'll fetch this from a global" — references come from constructor arguments or explicit setters.
- Services in `Nightwalk.Core` (e.g., `EventBus`, `DifficultyService`, `ScoreService`, `GameStateManager`, `GameModeManager`) do **not** extend `Node` and can be constructed in tests without the Godot runtime.
- Construction order is loud — if you accidentally use a service before it's built, the compile-time field is `null!` and the NRE at runtime points clearly at the bug.
- `_ExitTree()` cleanup is symmetric and explicit; no engine-managed lifecycles surprising the shutdown path.
- The `*Setup` + `*Result` pattern keeps `GameRoot._Ready()` readable despite the volume of wiring (~170 lines of construction).

**Negative:**
- `GameRoot.cs` is large (~700 lines). Refactoring it requires understanding the full wiring graph.
- Adding a new long-lived service means editing `GameRoot._Ready()`, the relevant `*Setup` helper (or creating one), and `CleanupGame()` — three coordinated edits.
- Hot reload (see [ADR-0006](0006-toml-hot-reload.md)) requires `OnConfigReloaded` to know about every reconfigurable service, leading to a long fan-out method (~70 lines).
- `null!` fields trade compile-time non-null safety for runtime initialization; an out-of-order access is an NRE, not a compile error.

**Related:**
- [ADR-0002](0002-assembly-split.md) — manual DI is what makes the `Core` assembly's lack of `Node` inheritance practical.
- [ADR-0004](0004-event-bus.md) — `EventBus` is the first service constructed and is passed to almost everything that follows.
- [ADR-0006](0006-toml-hot-reload.md) — config reload fan-out lives in `GameRoot.OnConfigReloaded`.
- [ADR-0007](0007-state-machine-capability-flags.md) — `GameStateManager` is constructed last, immediately before transition to main menu.
