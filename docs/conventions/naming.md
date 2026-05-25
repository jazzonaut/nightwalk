# Naming conventions

Reconstructed from the codebase as it stands 2026-05. The goal is one rule per category, derived from existing files — when in doubt, follow what's already there. Cross-cutting decisions on *where* code lives belong in [docs/adr/](../adr/) (especially [ADR-0002](../adr/0002-assembly-split.md) for the assembly split). Code-structure patterns live in [patterns.md](patterns.md).

Glossary terms (Beacon, Prism, Frequency, Chunk, Cluster, Warden, Seeker, …) come from [CONTEXT.md](../../CONTEXT.md). Use those nouns verbatim in code — `BeaconId`, not `LighthouseId`; `Sync`, not `Pair`.

---

## Files and folders

- **One public type per file**, with the file named after the type. `BeaconTracker.cs` contains `BeaconTracker`. Private nested helpers (e.g. `EventBus.QueuedEvent`) are fine inside the same file.
- **Folders are PascalCase, plural for collections of like things**: `Beacons/`, `Collectibles/`, `Buildings/`, `Tools/`. Singular when the folder is a system, not a bag: `Config/`, `Determinism/`, `Resonance/`.
- **`Events/` subfolder** under any feature folder holds that feature's `*Event` records: `Beacons/Events/`, `Scoring/Events/`, `GameState/Events/`, `Tools/Events/`, etc.
- **`Setup/` (Godot side only)** holds the one-off wiring helpers (`AudioSystemSetup`, `WorldInitializer`, `PlayerSpawner`, `MenuSystemSetup`, …). See [ADR-0003](../adr/0003-manual-di-no-autoloads.md).
- **`Config/Definitions/`, `Config/Settings/`, `Config/Attributes/`** are organizational subfolders that *do not* introduce sub-namespaces — every `Config/**/*.cs` declares `namespace Nightwalk.Core.Config`. Folder is for navigation, namespace is for the API surface.
- **TOML config files** in `data/` are lowercase, no separator: `core.toml`, `worldgen.toml`, `beacons.toml`, `resonance.toml`, `aliveness.toml`, `audio.toml`, `gameplay.toml`, `physics.toml`, `tools.toml`. One file per top-level config root.
- **Shaders** in `shaders/` are `snake_case.gdshader`: `signal_beam.gdshader`, `seeker_scan_cone.gdshader`, `weathered_facade.gdshader`. Match Godot's GDShader convention.

## Namespaces

- **Root is `Nightwalk.Core` or `Nightwalk.Godot`** — never plain `Nightwalk`. The split is the load-bearing rule from [ADR-0002](../adr/0002-assembly-split.md): `Core` may not reference `Godot.*`.
- **One namespace per folder**, mirroring path: `src/Nightwalk.Core/Beacons/Events/` → `namespace Nightwalk.Core.Beacons.Events;`.
- **Exception**: `Config/Definitions`, `Config/Settings`, `Config/Attributes` all flatten into `Nightwalk.Core.Config`. The folders organize files; the namespace stays single so consumers `using Nightwalk.Core.Config;` once.
- **File-scoped namespace declaration** (`namespace Foo;`) is the project default — no curly-brace nesting.

## Types

| Kind | Convention | Examples |
|------|-----------|----------|
| Class / struct / record | `PascalCase` noun | `BeaconTracker`, `PlotData`, `ScoreChangedEvent` |
| Interface | `I` + `PascalCase` | `IEventBus`, `IDomainEvent`, `IGameState`, `ITool`, `IWorldModule` |
| Abstract base | `Base` + `PascalCase` (Godot-side abstractions) | `BaseTool`, `BaseLightTool`. Pure-`Core` bases use a domain noun: `SystemBase`. |
| Enum | `PascalCase` singular | `SystemPhase`, `ViewMode`, `EventPriority`, `DifficultyLevel`, `PolyhedronType` |
| Attribute | `PascalCase` + `Attribute` (use without the suffix at call sites) | `UserConfigurableAttribute`, `SettingsRangeAttribute` |
| Static helper / extension class | `PascalCase` noun, no suffix | `AudioMath`, `StableHash`, `GameStateNames`, `SfxId` |

## Type suffixes (load-bearing — they signal role)

Use these consistently. Mixing them weakens the at-a-glance read.

| Suffix | Role | Examples |
|--------|------|----------|
| `*Event` | Immutable domain fact, implements `IDomainEvent`. Always a `record`. Lives under `Events/`. | `ScoreChangedEvent`, `BuildingClimbedEvent`, `BeaconActivatedEvent`, `ViewModeChangedEvent` |
| `*Id` | Strongly-typed identifier `readonly struct` with `IEquatable<T>` and value-equality operators. | `BeaconId`, `CollectibleId`, `BuildingId`, `RoadId`, `ChunkCoord` |
| `*Data` | Plain immutable record/struct of facts about a domain entity. No behavior. | `PlotData`, `BeaconData`, `SyncData`, `ChunkData`, `NeonLightData`, `MusicTrackInfo` |
| `*State` | Mutable state object or state-machine state. | `WorldState`, `BeaconState`, `ChunkState`, `SilhouetteState`, `ZiplineState`, `PlayingState`/`PausedState`/`MainMenuState` |
| `*Config` | TOML-deserialized configuration class. Lives in `Core/Config/Definitions/`. | `AudioConfig`, `PhysicsConfig`, `BeaconsConfig`, `ZiplineConfig`, `ScreensaverConfig` |
| `*Service` | Long-lived `Core` service constructed in `GameRoot` (or a `Setup` helper). Owns rules/state, not `Node`s. | `DifficultyService`, `ScoreService`, `CityAlivenessService`, `UserPreferencesService` |
| `*Manager` | Long-lived Godot-side service (`Node`/`Node3D`-derived) or a `Core` state-machine driver. | `GameStateManager`, `GameModeManager`, `MusicManager`, `SfxManager`, `BeaconSyncManager`, `PlayerToolManager`, `SynthLoopManager` |
| `*Tracker` | `Core` bookkeeping for which-entities-are-in-what-state during a session. Reset on session reset. | `BuildingTracker`, `BeaconTracker`, `CollectibleTracker` |
| `*Controller` | Drives a single subsystem's runtime behavior. May be `Core` (rules) or `Godot` (scene-bound). | `WardenController`, `SeekerController`, `ScreensaverController`, `PrismPOVController` |
| `*Node` | `Node3D`-derived visual representation of a domain entity. | `BeaconNode`, `PrismNode`, `WardenNode`, `SeekerNode` |
| `*Renderer` | `Godot` class that builds meshes / draws something. Includes sub-renderers under `Rendering/SubRenderers/`. | `SignalBeamRenderer`, `WardenRenderer`, `CarTrailRenderer`, `BuildingHighlightRenderer`, `GroundRenderer`, `NeonRenderer`, `PlotPavementRenderer` |
| `*Spawner` | Stateless or near-stateless factory that creates and registers entities in a chunk/scene. | `BeaconSpawner`, `CollectibleSpawner`, `ProximitySpawner`, `PlayerSpawner` |
| `*Generator` | Deterministic algorithm that produces data given a seed/context. | `RoadGenerator`, `BeaconGenerator`, `PolyhedronMeshGenerator`, `ScreensaverWaypointGenerator` |
| `*Factory` | Constructs Godot resources (materials, controls). | `MaterialFactory`, `TextureFactory`, `SettingsControlFactory` |
| `*Setup` / `*Result` | One-off wiring helper class + its return record. Always used as a pair. See [patterns.md § Setup helpers](patterns.md#setup-helpers). | `AudioSystemSetup` / `AudioSystemResult`, `PlayerSpawner` / `PlayerSpawnResult`, `WorldInitializer` / `WorldInitResult`, `MenuSystemSetup` / `MenuSystemResult` |
| `*Pool` | Object pool for reusable Godot nodes. | `SeekerPool`, `NodePool` |
| `*Tool` | Player tool in the tool belt (implements `ITool`/extends `BaseTool`). | `PrismTool`, `ZiplineTool`, `DecoyTool`, `SpotlightTool` |
| `*Effect` / `*Effects` | Visual one-shot or continuous effect node. | `PlasmaGlobeEffect`, `ScoreEffectSystem`, `ResonanceVisualEffects` |
| `*Helper` | Stateless utility class. Last resort — prefer a domain noun. | `RaycastHelper` |

## Members

- **Public properties, methods, types**: `PascalCase`.
- **Private fields**: `_camelCase` with leading underscore. (`_eventBus`, `_handlers`, `_currentState`, `_gameStarted`.)
- **Public constants**: `PascalCase` (`SpawnPosition`, `HexSegments`).
- **String constants used as IDs / categories**: `PascalCase` for the C# constant, the value is the wire format. The category constants in `LogCategory` (`Sim`, `World`, `Audio`) use `PascalCase` values; the SFX IDs in `SfxId` use `snake_case` values (`"footstep_walk"`) because they match TOML keys. Match the value to whatever the consumer reads — engine, TOML, or both.
- **Parameters and locals**: `camelCase`.
- **`null!` initialization** for fields set in Godot lifecycle methods (`_Ready()`) rather than the constructor is the documented project pattern — see `GameRoot.cs`.

## Events (`*Event` records)

- Always a `sealed record` implementing `IDomainEvent`.
- Constructor-positional immutable properties — no public setters.
- Implement `Tick` (often `=> 0` for events not produced from a tick context; revisit when [ADR-0009](../adr/0009-deterministic-seeded-generation.md) determinism work hardens).
- Past-tense or state-change names: `ScoreChangedEvent`, `BeaconActivatedEvent`, `BuildingClimbedEvent`, `WardenRepelledEvent`. Request/intent is a direct call to the owning service (e.g. `_scoreService.AddScore(...)`), not an event — except for the deliberately-named `SessionResetRequestedEvent`, which is a request modelled as a fact ("a reset was requested") because multiple unrelated systems need to react to it. See [ADR-0004 § Direct service references](../adr/0004-event-bus.md) for the rule.
- File and folder: `<Feature>/Events/<Verb>Event.cs`.

## Capability flags

State objects (`IGameState`, `GameMode`) expose boolean **capability flags** rather than relying on type-switches at call sites. The naming pattern is `Allows*` or `Shows*`:

- `AllowsGameTick`, `AllowsPlayerInput`
- `ShowsHUD`, `ShowsCursor`

When adding a new ambient behavior that depends on game state, prefer a new capability flag on `IGameState` over `if (state is PlayingState)`. See [ADR-0007](../adr/0007-state-machine-capability-flags.md).

## Config classes (`*Config`)

- Top-level config classes correspond 1-to-1 with `data/*.toml` files: `AudioConfig` ↔ `audio.toml`, `BeaconsConfig` ↔ `beacons.toml`, etc.
- Nested sections become nested classes: `MusicConfig`, `SfxConfig`, `SfxPlayerConfig` inside `AudioConfig`.
- All properties have public getters and setters (Tomlyn requires settability). Initialize reference-type properties with `= null!;` or `new();` so an uninitialized config doesn't NRE during partial deserialization.
- User-tunable properties get the attribute trio: `[UserConfigurable(...)]` + `[SettingsRange(...)]` + `[SettingsDisplay(...)]`. See `Core/Config/Attributes/`.

## Logging

- Log via `Log.Info(LogCategory.X, "...")` — never `GD.Print`, never `Console.WriteLine`.
- The category is a constant from `LogCategory` (`Sim`, `World`, `Chunk`, `Entity`, `Config`, `Perf`, `Input`, `Audio`, `UI`). Add new categories there, don't pass string literals.
- Debug-only spam is gated with `Log.IsDebugEnabled(LogCategory.X)` before formatting.

## Asset / scene paths

- Scenes in `scenes/` are `PascalCase.tscn`: `Main.tscn`, `ShaderTest.tscn`.
- TOML keys and shader uniforms are `snake_case` (engine convention).
- C# identifiers are `PascalCase`. The `[UserConfigurable]`/Tomlyn boundary translates one to the other.

---

## When to deviate

The conventions exist to make a 250-file codebase readable at a glance. If a new domain concept genuinely doesn't fit a suffix above, prefer adding a row to this table over inventing a one-off. If you do add a new suffix, also update [CONTEXT.md](../../CONTEXT.md) so the term enters the shared vocabulary.
