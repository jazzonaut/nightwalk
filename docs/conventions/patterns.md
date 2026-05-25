# Patterns and code structure

Reconstructed from the codebase as it stands 2026-05. Naming rules live in [naming.md](naming.md); architectural rationale lives in [docs/adr/](../adr/). This doc is the *how*: the recurring shapes that already exist in the code and should be reused rather than re-invented.

The cross-cutting rules:

1. **Pure logic in `Nightwalk.Core`; engine-bound code in `Nightwalk.Godot`.** Compiler-enforced by [ADR-0002](../adr/0002-assembly-split.md).
2. **All long-lived services are constructed manually in `GameRoot._Ready()`.** No autoloads, no DI container, no service locator. [ADR-0003](../adr/0003-manual-di-no-autoloads.md).
3. **Cross-system communication uses the typed `EventBus`.** Godot signals are reserved for intra-node UI plumbing. [ADR-0004](../adr/0004-event-bus.md).

Everything below is a consequence of those three rules.

---

## 1. Assembly split — what goes where

`Nightwalk.Core` is a plain `Microsoft.NET.Sdk` library. It must not `using Godot;`. Anything that needs `Node`, `Vector3`, `Camera3D`, `Input`, `Time`, etc. belongs in `Nightwalk.Godot`.

Rule of thumb when adding a new class:

| Question | If yes → |
|---|---|
| Does it implement game rules, scoring math, deterministic generation, config? | `Core` |
| Does it hold pure data (no behavior, no engine types)? | `Core` |
| Does it own a `Node3D` / `MeshInstance3D` / `AudioStreamPlayer`? | `Godot` |
| Does it need `_Process` / `_PhysicsProcess` / `_Input`? | `Godot` |
| Does it generate meshes, materials, shaders? | `Godot` |
| Is it a state-machine driver with no engine calls (e.g. `GameStateManager`)? | `Core` (states themselves may be `Godot` if they need `Input.MouseMode` etc.) |

For position/vector types crossing the boundary, `Core` exposes `float X, Y, Z` (or `System.Numerics.Vector3`) and `Godot`-side code converts at the seam. See `Beacons/BeaconTracker.cs` (uses `System.Numerics.Vector3`) and `Godot/Beacons/BeaconSyncManager.cs` (consumes `Godot.Vector3`).

## 2. Manual DI in `GameRoot`

`GameRoot.cs` is the single composition root. Read it top to bottom and you see every dependency edge in the project.

Construction order matters and is numbered with comments:

```
0. Logger
1. EventBus
1b. DifficultyService
1c. GameModeManager
2. Config (TomlDataLoader → MergingConfigProvider → GameDataService)
2a. Hot reload watcher  (#if DEBUG)
2b. ConfigureLogging()
3. WorldEnvironment, sky
4. VisualPreset
5. Post-processing, HUD
6. Visual preset system
7. AudioSystemSetup
8. WorldInitializer
9. PlayerSpawner
10. Cross-wiring (tool manager ↔ HUD, etc.)
11. GameplaySystemsSetup, game modes, Resonance, score effects, screensaver
12. SetupGameStates → MainMenu
```

`_ExitTree() → CleanupGame()` tears down in **reverse order**.

### Rules for adding a long-lived service

1. **Decide its assembly** using the table in §1.
2. **Build it where it's needed first** — if more than one consumer uses it, it goes in `GameRoot`. If only one helper uses it (e.g. a renderer's material factory), keep it local.
3. **Pass dependencies through the constructor** — no globals, no `GetNode("/root/...")`, no service locator.
4. **If construction takes more than ~5 lines or wires several related objects, write a `*Setup` helper** (see §3).
5. **Add an `Unsubscribe` / `Cleanup` call in `CleanupGame()`** symmetric to whatever you did in `_Ready`.
6. **If the service is reconfigurable from TOML, wire it into `OnConfigReloaded`** so hot reload (DEBUG) and the settings menu both work.

## 3. Setup helpers (`*Setup` + `*Result`)

Used for any group of related construction in `GameRoot`. The pattern:

```csharp
// 1. The return record — owns its cleanup.
public sealed record AudioSystemResult(
    MusicManager MusicManager,
    SfxManager SfxManager,
    AmbientManager AmbientManager,
    SynthLoopManager SynthLoopManager
)
{
    public void Cleanup()
    {
        MusicManager?.QueueFree();
        SfxManager?.QueueFree();
        AmbientManager?.QueueFree();
        SynthLoopManager?.QueueFree();
    }
}

// 2. The static helper — Configure() / Spawn() / Initialize() returns the record.
public static class AudioSystemSetup
{
    public static AudioSystemResult Configure(Node parent, AudioConfig config) { /* ... */ }
}
```

Conventions:

- The helper is **static** and exposes one entry point named for what it does: `Configure`, `Spawn`, `Initialize`.
- The result is a **`sealed record`** with a `Cleanup()` method (for any unsubscribe/dispose work — child `Node`s freed by the engine don't need explicit `QueueFree` but it's the convention to do so anyway).
- The result is **stored as a nullable field** on `GameRoot` and nulled out in `CleanupGame()`.
- The helper logs a single `Log.Info(LogCategory.X, "<system> initialized")` line at the end of `Configure`.

Existing exemplars: `AudioSystemSetup`, `WorldInitializer`, `PlayerSpawner`, `GameplaySystemsSetup`, `MenuSystemSetup`, `SkySystemSetup`, `PostProcessingSetup`, `EnvironmentSetup` (no result — pure side effects on the scene).

## 4. EventBus (`IEventBus`)

The cross-system pub/sub. Lives in `Core`. See [ADR-0004](../adr/0004-event-bus.md) for full shape.

**Publishing:**

```csharp
_eventBus.Publish(new ScoreChangedEvent(points, newTotal, category, multiplier));
// or, when the event should drain on the next world tick:
_eventBus.PublishDeferred(new MyEvent(...), EventPriority.Normal);
```

**Subscribing — every `Subscribe<T>` MUST have a matching `Unsubscribe<T>`:**

```csharp
public void Connect(IEventBus bus)
{
    bus.Subscribe<ScoreChangedEvent>(OnScoreChanged);
    bus.Subscribe<ComboChangedEvent>(OnComboChanged);
}

public void Disconnect()
{
    _eventBus.Unsubscribe<ScoreChangedEvent>(OnScoreChanged);
    _eventBus.Unsubscribe<ComboChangedEvent>(OnComboChanged);
}
```

DEBUG builds track subscription sources for leak detection (`EventBus.GetSubscriptionStats()`). If you forget the `Unsubscribe`, you leak the subscriber object and its entire reference graph.

**Defining a new event:**

1. New file under `<Feature>/Events/<Verb>Event.cs`.
2. `sealed record` implementing `IDomainEvent` with positional immutable properties.
3. Past-tense name (`*Changed`, `*Activated`, `*Climbed`, `*Repelled`).
4. Don't add a base type or wildcard — there is no `IScoreEvent`. Each handler subscribes to concrete types.

**Do not use Godot signals for cross-system communication.** Signals stay inside their owning node (a button's `Pressed` signal → its parent menu). Crossing system boundaries requires the bus.

## 5. State machine + capability flags

Game lifecycle (`MainMenu` / `Playing` / `Paused`) is modeled as `IGameState` instances registered with `GameStateManager`. The manager publishes `GameStateChangedEvent` on transitions.

Each state declares its **capability flags**:

```csharp
public string Name => GameStateNames.Playing;
public bool AllowsGameTick => true;
public bool AllowsPlayerInput => true;
public bool ShowsHUD => true;
public bool ShowsCursor => false;
```

Consumers read flags off `_stateManager`, not the current state's concrete type:

```csharp
if (!_stateManager.AllowsGameTick) return;
player.InputEnabled = _stateManager.AllowsPlayerInput;
```

When a new ambient behavior needs to be gated on state, **add a new flag** on `IGameState` rather than introducing a type check. [ADR-0007](../adr/0007-state-machine-capability-flags.md).

`GameMode` (`Silence` / `Resonance`) follows a similar pattern via `GameModeManager` — orthogonal to game state. [ADR-0008](../adr/0008-game-mode-framework.md).

## 6. Game systems (`ISystem` / `SystemBase`)

Long-lived per-tick logic implements `ISystem` (extends `ITickable`) and is registered with `SystemRegistry`. Phases are declared via `SystemPhase`, with optional `Priority` and `Dependencies` (intra-phase ordering).

Minimal skeleton:

```csharp
public sealed class MySystem : SystemBase
{
    public override SystemPhase Phase => SystemPhase.Simulation;
    public override int Priority => 100;

    public override void Initialize() { /* one-time setup */ }
    public override void Tick(ulong currentTick) { /* per-tick work */ }
    public override void Shutdown() { /* cleanup */ }
}
```

The registry is currently lightly used — most per-tick work happens through direct calls in `GameRoot._PhysicsProcess`. Prefer `ISystem` over ad-hoc per-tick wiring when the work is genuinely tick-scoped and needs ordering.

## 7. World modules (`IWorldModule`)

World construction is pluggable. `WorldBuilder` walks registered `IWorldModule`s in priority order; each module mutates `IWorldContext` (registers services, world state, generation steps). Used for `Core`-side worldgen contributions that should remain independent of `GameRoot`'s wiring.

## 8. Strongly-typed IDs (`*Id` structs)

Domain identifiers are `readonly struct`s with:

- `IEquatable<T>` implementation.
- `Equals(object?)`, `GetHashCode()`, `ToString()` overrides.
- `==` / `!=` operator overloads.
- A static factory if the constructor isn't expressive enough (`BeaconId.ForBuilding(...)`).

Examples: `BeaconId`, `CollectibleId`, `BuildingId`, `RoadId`, `ChunkCoord`. Never use raw `int`/`string` for entity identity that crosses a system boundary.

## 9. Config (TOML → `*Config` → service)

Pipeline (see [ADR-0006](../adr/0006-toml-hot-reload.md)):

1. `TomlDataLoader` reads `data/<name>.toml` from `res://`.
2. `UserConfigLoader` reads user overrides from the platform's appdata.
3. `MergingConfigProvider` overlays user on system.
4. `GameDataService.LoadAll()` deserializes into typed `*Config` records via Tomlyn.
5. Consumers receive their slice through `data.Core.Graphics`, `data.Audio.Music`, `data.Beacons`, etc.

**Adding a config:**

1. Create `data/<feature>.toml`.
2. Create `Core/Config/Definitions/<Feature>Config.cs` mirroring the TOML structure (public get/set on every property; reference types default to `null!` or `new()`).
3. Register it in `GameDataService`.
4. Consume via constructor injection — never re-load the TOML from a consumer.
5. If anything in the config is user-tunable, decorate the property with `[UserConfigurable]` + `[SettingsRange]`/`[SettingsOptions]` + `[SettingsDisplay]`.
6. In `GameRoot.OnConfigReloaded`, call the consumer's `Configure(newConfig)` (or `Reconfigure(...)`) so hot reload + the settings menu both work. Reload fan-out is verbose by design — every reconfigurable service gets a line.

## 10. Logging

```csharp
Log.Info(LogCategory.Sim, "Game state initialized");
Log.Warn(LogCategory.World, "Chunk failed to load: ...");
Log.Error(LogCategory.Config, "FATAL: ...", ex);

if (Log.IsDebugEnabled(LogCategory.Chunk))
    Log.Debug(LogCategory.Chunk, $"Generated {n} buildings in {ms}ms");
```

Never call `GD.Print`. The `Log` indirection routes through `IGameLog`, which is `NullGameLog` in `Core` tests and `GodotLogger` in-game.

## 11. Tools and renderers

**Player tools** implement `ITool` and (for Godot-side tools) extend `BaseTool`. The pattern:

- `Id` (string constant), `DisplayName` (HUD label).
- `Enter()` / `Exit()` toggle visibility.
- `Update(delta)` is gated by `RequiresUpdate`; `HandleInput(evt)` is gated by `RequiresInput`.
- `OnInitialize()` is the override hook; consumers don't call `Initialize` themselves — `PlayerToolManager` does.

**Sub-renderers** implement `ISubRenderer` (or `IPhasedSubRenderer` for progressive rendering — see [ADR-0013](../adr/0013-progressive-chunk-rendering.md)). Each handles one aspect of chunk rendering and writes into a parent `Node3D`. Add new visual layers as new sub-renderers; do not extend an existing one to do two things.

## 12. Determinism

When generation must be reproducible from a seed, use `DeterministicRng` (`IRng`) and `StableHash` from `Core/Determinism/`. Never call `Random.Shared`, `System.Random`, or Godot's `GD.Randi()` from generation code. See [ADR-0009](../adr/0009-deterministic-seeded-generation.md).

## 13. Session reset

The session reset path (triggered by `SessionResetRequestedEvent`) is the canonical "wipe in-session state" hook. Every system that accumulates session state exposes a `Reset()` / `ResetForSession()` method:

- `BeaconTracker.Reset()`, `CollectibleTracker.Reset()`, `BuildingTracker.Reset()`
- `ChunkManager.ResetForSession()`
- `PlayerToolManager.ResetForSession()`
- `_gameplayResult.Reset()`, `_gameHUD.Reset()`, etc.

When you add new session-scoped state, add a matching `Reset` and wire it into `GameRoot.OnSessionResetRequested`.

## 14. The `null!` field pattern

Fields that are initialized in `_Ready()` (not the constructor) use `null!`:

```csharp
private IEventBus _eventBus = null!;
private GameDataService _data = null!;
```

This is deliberate. The compiler treats them as non-null at use sites; an out-of-order access in `_Ready()` produces a clear NRE that points at the bug. Don't replace these with `?` and litter the code with `!.` — fix the construction order instead.

## 15. Interop cost discipline (per-frame hot paths)

Every Godot call from C# crosses the managed↔native boundary. On a profile this dominates over gameplay or rendering algorithms. The discipline is named in [ADR-0014](../adr/0014-csharp-godot-interop-cost.md) and concretely applied to chunk geometry in [ADR-0015](../adr/0015-meshbuilder-for-chunk-geometry.md). The patterns:

- **Cache marshalled property reads** when read more than once per frame. `node.GlobalPosition`, `node.GlobalTransform.Basis`, `material.GetShaderParameter("x")` each marshal. Read once into a local, mutate, write once.
- **Cached delegate field for callbacks/signals** that fire more than rarely. Build the `Action`/`Callable` once (in `Configure`/`SetX`/`_Ready`), store it on a field, reuse it. Replaces per-event `Callable.From(() => ...)` closures that allocate on construction. Example: `CollectibleSpawner._sfxCallback`.
- **Method-group reference over capture-lambda** when wiring callbacks. `OnFooHappened` (method group, no allocation) is preferable to `(arg1, arg2) => OnFooHappened(arg1, arg2, somethingElse)` (capture-lambda, heap allocation). If you need extra state, look it up from the live object inside the handler instead of capturing it.
- **Throttle per-entity per-frame updates** when visual smoothness allows. The convention is a constant `XUpdateInterval` and a counter mod-N gate (`CollectibleSpawner.ProximityUpdateInterval = 4`). Run cheap checks (validity, despawn) every tick; run expensive marshalled work every 4th.
- **Short-circuit on unchanged state.** Compare against a stored sentinel at the top of the update method (`CollectibleShape._currentAlpha`, default `-1f` so the first call always runs).
- **Per-frame work budgets for scene-tree mutations.** Cap `AddChild` bursts. `ProximitySpawner.MaxSpawnsPerFrame = 8`. Same shape as [ADR-0013](../adr/0013-progressive-chunk-rendering.md) at a coarser grain.
- **Build geometry in managed arrays, submit in one call.** Per-vertex `SurfaceTool.AddVertex` / `SetNormal` / `SetUV` calls marshal individually. Use `MeshBuilder` (`src/Nightwalk.Godot/Rendering/Geometry/MeshBuilder.cs`) for chunk geometry — accumulate into `List<Vector3>` / `List<Vector2>`, commit via one `ArrayMesh.AddSurfaceFromArrays` per surface. See [ADR-0015](../adr/0015-meshbuilder-for-chunk-geometry.md) for the migration status and which call-sites legitimately stay on `SurfaceTool`.
- **Profile with a .NET sampling profiler** (CodeTrack, dotnet-trace, JetBrains dotTrace). Godot's built-in Script Profiler does not capture C# methods — `Script Functions` will read 0.00 ms. Sort by `MaxTime` (spike) when investigating stutters, not just average.

A `new SurfaceTool()` inside a phased sub-renderer, a `Callable.From(lambda)` in a hot path, or an unguarded per-entity per-frame property write are all reviewer-visible regression flags.

---

## When to deviate

Patterns earn their keep by being predictable. If a new requirement genuinely doesn't fit one of the shapes above, the bar is: (a) write down the new pattern here in the same form, (b) update [naming.md](naming.md) if it introduces a new type suffix, and (c) consider an ADR if the deviation is load-bearing.
