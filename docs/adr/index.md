# Architecture Decision Records — Index

Decisions are append-only. Numbers are immutable identifiers — a missing number (e.g. 0005) means that ADR was deleted; do **not** reuse the number. Read the relevant ADRs before working in the area they cover.

For the *why* behind every ADR, click through. For the *how* the codebase applies them, see [docs/conventions/patterns.md](../conventions/patterns.md) and [docs/conventions/naming.md](../conventions/naming.md). For *what* the systems do at runtime, see [docs/systems/](../systems/).

---

## Foundational — language and assembly shape

These ADRs constrain the whole codebase. Everything else assumes them.

| # | Title | Summary |
|---|---|---|
| [0001](0001-csharp-over-gdscript.md) | C# over GDScript | C# (.NET 8) as sole language; no `.gd` files. Enables the assembly split and the pure-managed `Core`. |
| [0002](0002-assembly-split.md) | Two-assembly split (`Nightwalk.Core` vs `Nightwalk.Godot`) | Pure-managed `Core` (no `using Godot`) plus an engine-bound `Godot` assembly that references it. Compiler-enforced boundary. |
| [0003](0003-manual-di-no-autoloads.md) | Manual DI in `GameRoot`; no Godot autoloads | Every long-lived service is constructed and wired explicitly in `GameRoot._Ready()`. `ProcessPriority = -100`. |

## Cross-cutting infrastructure

The five pillars that nearly every gameplay system touches.

| # | Title | Summary |
|---|---|---|
| [0004](0004-event-bus.md) | Custom typed `EventBus` over Godot signals | In-process pub/sub with deferred + priority. Mandatory for cross-system messaging; signals stay intra-node. |
| [0006](0006-toml-hot-reload.md) | TOML configs with `FileSystemWatcher` hot reload | Nine TOML files in `data/`; atomic reload via `GameDataService`; user overlay via `MergingConfigProvider`; DEBUG-only file watching. |
| [0007](0007-state-machine-capability-flags.md) | Game state machine with capability flags | `MainMenu` / `Playing` / `Paused` states expose `AllowsGameTick`, `AllowsPlayerInput`, `ShowsHUD`, `ShowsCursor` rather than relying on type checks. |
| [0008](0008-game-mode-framework.md) | Pluggable game-mode framework (`IGameMode`) | `Silence` / `Resonance` modes with lifecycle hooks. Orthogonal to game state and difficulty. |
| [0009](0009-deterministic-seeded-generation.md) | Deterministic seeded generation | Hand-rolled Xorshift32 + FNV-1a (`StableHash.Combine`). Chunk seed = `Combine(worldSeed, x, z)`. Bypasses `HashCode.Combine`'s process-randomization. |

## Performance and interop discipline

The cost model that informs every per-frame perf decision in the project.

| # | Title | Summary |
|---|---|---|
| [0014](0014-csharp-godot-interop-cost.md) | Treat C# ↔ Godot interop as the primary cost model | Marshalled engine calls dominate profile time. Rules: cache property reads, prefer method-groups over capture-lambdas, throttle unchanged updates, build geometry in managed arrays. |

## Gameplay-system design choices

Algorithmic decisions inside specific gameplay systems.

| # | Title | Summary |
|---|---|---|
| [0010](0010-difficulty-multiplier-service.md) | Difficulty as a multiplier service | Central `DifficultyService` with four hardcoded multiplier curves (Observer/Explorer/Wanderer/Architect). Scaled accessors + flag short-circuits. |
| [0011](0011-beacon-beam-routing.md) | Beacon beam routing — iterative ray-march with prism snap | Discrete walk through prisms, final physics raycast to terminal geometry. Snap threshold catches "nearby" prisms; visited set prevents loops. |
| [0012](0012-zipline-physics.md) | Zipline physics — parametric `t` + gravity-slide + climb-efficiency | Position is `_cableT ∈ [0, 1]`; velocity is along-cable. Climb efficiency floors at `MinClimbEfficiency`; sprint multiplies both slide and climb. |
| [0016](0016-skycraft-air-traffic.md) | Skycraft ambient air-traffic system | Pool-managed flying NPCs in three profiles (Express/Commuter/Drifter); hybrid near-`Node3D` / far-`MultiMesh` rendering with shadowless near-tier spotlights; Drifters route along existing `RoadSegment` graph; collision-freedom is structural; no Resonance coupling. |

## Rendering

The chunk-construction story.

| # | Title | Summary |
|---|---|---|
| [0013](0013-progressive-chunk-rendering.md) | Progressive chunk rendering with phased sub-renderers | Per-frame `RenderBudgetMicroseconds` time-budget scheduler; phases run in fixed order; chunks advance in lockstep so ground appears before buildings. |
| [0015](0015-meshbuilder-for-chunk-geometry.md) | Chunk geometry built via `MeshBuilder`, not `SurfaceTool` | Accumulate geometry in managed `List<>`s; commit once per surface via `ArrayMesh.AddSurfaceFromArrays`. Eliminates per-vertex interop. |

---

## How to add a new ADR

1. Pick the next unused number (currently 0017). Numbers never reuse — even if an ADR is deleted (like 0005 was), its number stays vacant.
2. File name: `NNNN-kebab-case-title.md`.
3. Nygard format: `Status` / `Context` / `Decision` / `Consequences`. Include an "Alternatives considered" subsection in `Context` when there were realistic options.
4. Status: `Accepted (YYYY-MM-DD)` for fresh decisions; `Accepted (retrofitted YYYY-MM)` for after-the-fact ADRs grounded in code that already exists.
5. Add a row to the relevant section in this index.
6. Add the link to [README.md](../../README.md)'s Documentation map.
7. Cross-link from any ADRs whose decisions the new one builds on (forward refs in the new ADR; consider adding backward refs in the older ADRs' `Related` sections).
8. If the new ADR introduces a new domain term, add it to [CONTEXT.md](../../CONTEXT.md). If it introduces a new code shape, add it to [docs/conventions/](../conventions/).

## How to retire an ADR

If a decision is fully reversed (not just superseded), delete the file and remove every reference. Leave the number vacant — do not renumber subsequent ADRs. The git history is the audit trail.

If a decision is *superseded* by a new one (the choice changes but the area remains covered), keep the old ADR with `Status: Superseded by ADR-NNNN` and write the new one. Both stay in the index.
