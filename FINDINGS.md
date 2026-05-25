# FINDINGS

Throwaway triage list of code-vs-code inconsistencies and README staleness surfaced during the ADR migration (started 2026-05).

Each entry: short description, location, suggested action. Delete entries once resolved. Delete this file when empty.

---

## README staleness (entire `README.md` is generated, not maintained)

- **Resonance game mode is entirely missing.** Codebase has `GameModeManager`, `ResonanceMode`, `AlertMeter`, `ExposureMeter`, `WardenController`, `SeekerController`, `ResonanceVisualEffects`, `DecoyTool` — none of which appear in README. README still describes the game as "exploration + puzzles" only. *Action: when pruning README, rewrite the overview to include game modes.*
- **Game Mode as a top-level concept is missing.** Difficulty is presented as the only "mode" axis; game modes (Silence / Resonance) are a separate orthogonal axis. *Action: clarify in slim README.*
- **Cluster / super-chunk system is missing.** README only mentions chunks. The codebase has `ClusterInfluenceProvider`, `SuperChunkCoord`, `super_chunk_size = 2000.0` driving downtown/density. *Action: per-system ADR candidate (worldgen) should call this out.*
- **Command Bus is scaffolded but barely used.** `ICommandBus` / `CommandBus` exists in `Core/Commands` but is only referenced from the World layer (`WorldContext`, `IWorldContext`, `WorldBuilder`, `WorldState`) — not from input, tools, scoring, or any of the systems where you'd expect a command pattern. Likely planned for tick-based deterministic command processing but not yet load-bearing. *Action: do NOT give it its own ADR yet; mention as a sibling in ADR-0004. If usage grows, promote to its own ADR.*
- **System Registry / `ISystem` framework is missing.** `SystemRegistry`, `ISystem`, `SystemPhase`, `ITickable`, `SystemBase` form a registry pattern README doesn't describe. *Action: per-system ADR candidate.*
- **Determinism scaffolding is missing.** `DeterministicRng`, `IRng`, `StableHash` exist. README doesn't mention deterministic generation. *Action: confirm whether this is a load-bearing decision (used for reproducible worldgen?) and ADR if so.*
- **ViewMode taxonomy is muddled.** README §3.11 lists "Walk/flight/bird's-eye/prism POV/screensaver/flyby cameras" as view modes. Code: `ViewMode` enum is only `Walk`, `Flight`, `BirdsEye`. Prism POV, Flyby, and Screensaver are *overlay* controllers (`PrismPOVController`, `FlybyCamera`, `ScreensaverController`), not view modes. *Action: corrected in `CONTEXT.md`; per-system ADRs/docs must use the new taxonomy.*

## Tools / mechanics differences

- **Decoy tool exists, not mentioned in README.** `DecoyTool.cs` registered. *Action: include in per-system pass.*

## Versioning / project metadata

- **Godot version mismatch.** README claims Godot 4.5.1; `project.godot` declares `4.6` (and `Godot.NET.Sdk/4.6.3` in `Nightwalk.csproj`). *Action: fix in slim README; non-trivial only because it affects what docs/agents should reference.*
- **`tests/` directory missing.** README §1.1 lists `tests/` as "Unit test projects" but no such folder exists. Either tests were never written or were removed. *Action: decide whether to introduce tests (the whole "C# for testability" argument loses force without them) or drop the claim from the slim README. Logged here, not blocking.*
- **9 config files, not 8.** README §1.1 says "TOML configuration files (8 files)" — actual count is 9: `core, physics, gameplay, worldgen, audio, tools, beacons, aliveness, resonance`. README missed `resonance.toml`. *Action: corrected in ADR-0006; README pruning will drop the count or update it.*

## ECS scaffolded but dormant

- **`EntityManager` / `IEntityManager` exists, components defined, but not actually used by gameplay.** The 6 component types (`Position3D`, `Velocity3D`, `Rotation3D`, `Grounded`, `Energy`, `MovementState`, `Player`) appear only in their own files and in `WorldInitializer` which registers two as "render-relevant" — they are never added to entities, never queried, never read. `EntityManager` is constructed by `WorldBuilder` and exposed via `WorldContext.EntityManager` / `WorldState.Entities` but no caller invokes `CreateEntity` / `AddComponent` / `GetComponent` from outside the EntityManager class itself. Gameplay is implemented in plain OO services (`PlayerEntity` as `CharacterBody3D`, `BuildingTracker`, `CollectibleTracker`, etc.). *Action: ADR-0005 reflects this honestly — the infrastructure exists, the usage does not. Decide later whether to (a) finish wiring it through gameplay, (b) delete it, or (c) leave it dormant for future use.*
- **EntityType enum has values for `Player`, `Chunk`, `Building`, `RoadSegment`, `NeonLight`, `Prop` — none of which are created as entities.** Reinforces the above.
- **`RenderRelevantComponents` HashSet is initialised empty** with the comment "Will be populated as we add render-relevant components". Two registrations exist (`WorldInitializer.cs:65,67`) but no entity ever carries those components, so render-dirty tracking has no live signal.
- **`CreateEntity(type, id)` is commented "For network sync"** despite the project being single-player (`PROMPT.md` explicitly says so). Likely vestigial design ambition.

## Verification queue (to walk during deep ADR work)

- Confirm `project.godot` has no `[autoload]` entries (ADR-0003).
- Confirm `Nightwalk.Core.csproj` does not reference `GodotSharp` (ADR-0002).
- Confirm `GameRoot._Ready()` constructs services in the order README claims (ADR-0003).
- Confirm `EventBus` is the only cross-service messaging path; grep for direct service-to-service refs that should be events (ADR-0004).
- Confirm `GameDataService` exposes a single `OnConfigReloaded` event consumed by all subscribers (ADR-0006).
- Confirm only `MainMenuState`, `PlayingState`, `PausedState` exist as `IGameState` implementations (ADR-0007).

---

*Add new findings as ADRs are written. Resolve = delete the line.*
