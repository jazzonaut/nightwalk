# ADR-0013: Progressive chunk rendering — phased sub-renderers + per-frame time budget

## Status

Accepted (retrofitted 2026-05)

## Context

Each chunk in Nightwalk is 64m × 64m and carries non-trivial procedural geometry — ground, roads, manholes, sidewalk/pavement, buildings (multi-layered facades with weathering shaders, ledges, windows lit/unlit, optional facade detail meshes), neon signs, street lamps. Building the meshes is cheap individually but expensive in aggregate; the player travels into new chunks while moving, so several chunks can be entering the load radius at once.

Doing all of a chunk's mesh construction inside a single `_Process` would block the frame budget when multiple chunks load simultaneously. Frame hitches show up immediately at 60fps targets and the city's continuous-traversal feel suffers.

### Alternatives considered

- **Synchronous, all-at-once rendering** — when `ChunkLoadedEvent` fires, build the entire chunk's geometry in that handler. Simple, but a multi-chunk load stalls the frame for the duration of *all* their builds.
- **Background threading** — build meshes off the main thread; transfer to the scene tree on completion. Godot's `Node`/`Mesh` APIs are largely main-thread-only, and the parts that are thread-safe are mixed with parts that aren't — easy to hit subtle races. Substantial complexity for a single-player game.
- **Progressive rendering with per-frame time budget** — split chunk construction into named phases (`Ground`, `Roads`, `Manholes`, `Pavement`, `Buildings`, `Neons`, `StreetLamps`); each phase implements `IPhasedSubRenderer.RenderIncremental(state, budgetUsec)` returning whether it's done; the coordinator runs phases in order across multiple frames, stopping each frame when the remaining time budget is spent.

The third path is the standard streaming-LOD pattern adapted to Godot's main-thread constraints: the *coordinator* is single-threaded but spreads work across frames in measured slices.

## Decision

`ChunkRenderer` coordinates progressive rendering with:

- A fixed **phase order**: `Ground → Roads → Manholes → Pavement → Buildings → Neons → StreetLamps`.
- Each phase is implemented by a class implementing `IPhasedSubRenderer` (extends `ISubRenderer` for the all-at-once fallback path).
- Per-frame time budget: `RenderBudgetMicroseconds` (TOML-configured, in `data/worldgen.toml`'s `[chunks]` section).
- Max concurrent chunks-in-flight: `MaxConcurrentRenders` (TOML).
- A global toggle: `ProgressiveRenderingEnabled` — when false, falls back to the synchronous `CreateChunkNodeImmediate` path.

### Shape

`IPhasedSubRenderer`:

- `Phase` — which `ChunkRenderPhase` this implements.
- `RenderIncremental(state, budgetUsec)` — do work within the budget; return `true` if the phase is complete, `false` if more work is needed next frame.

`ChunkRenderer.ProcessRenderQueue()` (called from `GameRoot._Process` every frame):

1. **Start new renders** — pop chunks from `_pendingRender` into `_activeRenders` while under `_maxConcurrentRenders`. Skip pending chunks that have been unloaded.
2. **Sort active renders by phase** (in-place; lower phases first) so chunks all build their ground before any of them builds buildings — visually, the player sees a coherent floor before facades pop in.
3. **Advance phases** — for each active render, compute remaining budget (`budget - elapsed_so_far_this_frame`), call `AdvanceRenderPhase(state, remaining)`. Break out of the loop the moment the elapsed time reaches the budget.
4. **Reap completed renders** — chunks reaching `ChunkRenderPhase.Complete` get cleaned up and their nodes added to `_chunkNodes`.

Adding the finished chunk to the scene tree happens via `CallDeferred(CompleteAddChild)` to avoid mid-frame `AddChild` races with concurrent unloads (`_pendingAdd: HashSet<Node3D>` tracks in-flight additions).

The **synchronous fallback** (`_progressiveRenderingEnabled = false`) calls each `ISubRenderer.Render` once per chunk on the load event. Same renderer classes; different invocation strategy. Useful for debugging or for builds where frame budget isn't a concern.

### Verified

- `PhaseOrder` is exactly the seven names listed (`ChunkRenderer.cs:59-68`).
- `RenderBudgetMicroseconds`, `MaxConcurrentRenders`, `ProgressiveRenderingEnabled`, `DebugShowBoundaries` are all sourced from `chunks` config (`:89-93`).
- `ProcessRenderQueue` checks the budget per loop iteration and breaks when exceeded (`:253-255`).
- `_activeRenders.Sort((a, b) => a.CurrentPhase.CompareTo(b.CurrentPhase))` is in-place (`:249`) — avoids LINQ allocation. The comment confirms the intent.
- Synchronous fallback invokes each registered `IPhasedSubRenderer` cast to `ISubRenderer` (`:331-337`).
- Deferred `AddChild` to avoid concurrent-unload races (`:297`).

## Consequences

**Positive:**
- Multi-chunk loads no longer stall the frame. Each frame contributes up to `RenderBudgetMicroseconds` of construction work across all in-flight chunks; the rest waits for the next frame.
- The phase order ensures the player sees ground and roads first — the visually disturbing transition is "building pops in," not "ground pops in," and the order minimises the former.
- Each phase is a small, focused class (`GroundRenderer`, `RoadRenderer`, `ManholeRenderer`, `PlotPavementRenderer`, `BuildingRenderer`, `NeonRenderer`, `StreetLampRenderer`). Adding a new phase is one new class + one entry in `PhaseOrder`.
- The all-at-once fallback shares the renderer classes — debugging a renderer doesn't require maintaining two code paths.
- `MaxConcurrentRenders` caps the working set, so memory pressure from too many in-flight chunks is bounded.
- Time budget is hot-reloadable ([ADR-0006](0006-toml-hot-reload.md)) — tweak `RenderBudgetMicroseconds` in `data/worldgen.toml` and the next frame uses the new value.

**Negative:**
- Phases that don't yield mid-work cannot honour the budget. `RenderIncremental` is required to *check* the budget itself and return — a phase that loops without checking will overrun. The discipline is per-implementation; no compile-time enforcement.
- The budget is enforced *between* phase calls, not within them. A single building's facade weathering can take milliseconds in one go.
- Chunk visibility lags load: a chunk *exists* (the `ChunkLoadedEvent` fired and the data is generated) before its geometry appears. Most of the time this is fine — the chunk loads on entry to the load radius, which is well outside the camera. Edge cases at the camera's leading edge can show seams.
- The deferred `AddChild` race-protection (`_pendingAdd` + `CallDeferred`) is correct but indirect; a future contributor adding a fast-path `AddChild` would silently reintroduce the race.
- `_activeRenders.Sort` runs each frame even when the order hasn't changed. Cheap (small list), but a noticeable observation if the active set grows large.
- Synchronous fallback bypasses the budget entirely. Useful for testing, but flagging "we are not in the progressive path" requires reading the config — there's no runtime indicator.

**Related:**
- [ADR-0006](0006-toml-hot-reload.md) — `RenderBudgetMicroseconds` and friends are hot-reloadable; design iteration on streaming smoothness is cheap.
- [ADR-0009](0009-deterministic-seeded-generation.md) — sub-renderers consume the chunk seed for any per-chunk variation; the seed is deterministic, so a chunk that gets rendered progressively across frames produces the same geometry as one rendered all at once.
- [ADR-0014](0014-csharp-godot-interop-cost.md) — the cost model that justifies the per-frame budget pattern at this coarse grain.
- [ADR-0015](0015-meshbuilder-for-chunk-geometry.md) — refines the per-phase work: each surface commits once via `ArrayMesh.AddSurfaceFromArrays`, so more chunks fit within the same per-frame budget.
- The seven sub-renderers themselves are reference material rather than ADRs — they encode game-design choices (which surfaces use which materials, where shutters go on a building, how a manhole is built) but not engineering decisions in the ADR sense.
