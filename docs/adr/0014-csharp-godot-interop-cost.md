# ADR-0014: Treat C# ↔ Godot interop as the primary cost model

## Status

Accepted (2026-05-25)

## Context

Choosing C# over GDScript (see [ADR-0001](0001-csharp-over-gdscript.md)) gives strong typing, generics, NuGet, and a unit-testable pure-managed core. It also imposes a cost the language choice ADR does not call out: **every interaction with a Godot engine object — property read, property write, method call, signal dispatch, mesh construction, raycast query — crosses the managed↔native boundary, and the cost is per-call, not per-byte**.

Profiling (2026-05-25, CodeTrack sampling profiler against a steady-state play session) showed this empirically. With idle thread-pool rows excluded, the hottest entries were all interop infrastructure, not gameplay or rendering algorithms:

| Method | Cumulative own time | Description |
|---|---|---|
| `godotsharp_method_bind_ptrcall` | 22.33 s (98.77 % own) | Every Godot engine call from C# |
| `godotsharp_array_destroy` | 8.07 s | `Godot.Collections.Array` disposal |
| `godotsharp_internal_refcounted_disposed` | 4.64 s | Godot RefCounted cleanup |
| `Godot.StringName.op_Equality` (3 sites) | ~1.4 s combined | `StringName` comparisons |
| `System.Runtime.InteropServices.GCHandle.get_Target` | 590 ms | Managed-handle resolution |

In the same window the Godot Visual Profiler reported ~4.5 ms/frame of GPU work and 0.19 ms of physics — neither bottleneck. The engine is fine; the *bindings to it* are where the time goes.

We hit this concretely during a perf pass on `CollectibleSpawner` and `CollectibleShape`. `ProximitySpawner<...>.UpdateProximity` accumulated 1.76 s cumulative and `CollectibleShape.UpdateVisibility` accumulated 836 ms — not because the gameplay logic was slow, but because each call fanned out to multiple marshalled Godot calls (`SetShaderParameter`, `Visible`, `LightEnergy`, `Monitoring`) and a fresh per-spawn `Callable.From(lambda)` closure.

### Alternatives considered

- **Optimise the marshalling layer itself.** Out of scope — it's `Godot.NET.Sdk` and the engine.
- **Move hot code to GDScript or GDExtension/C++.** Re-opens [ADR-0001](0001-csharp-over-gdscript.md) and adds language/build complexity for a single-player game. Rejected.
- **Accept the cost; tune render passes and gameplay algorithms instead.** What we tried first and what produced misleading recommendations: volumetric-fog `volume_size`, shader sample-count trims, shadow tuning. Each gave 0.2–0.5 ms; none addressed the real bottleneck.
- **Reduce engine call-count.** Cache property reads, batch operations, build managed arrays and submit them in one call, prefer method-group references over closures for `Callable`/`Action`. Targets the actual cost.

The last option is the one this ADR commits to.

## Decision

Treat C# ↔ Godot interop call-count as **the primary cost model** for perf decisions. Concretely:

### Rules of thumb

1. **Profile before optimising.** Use a .NET sampling profiler (CodeTrack, dotnet-trace, JetBrains dotTrace) for managed code. The Godot built-in Script Profiler does not capture C# methods — `Script Functions` in the profile shows 0.00 ms because the engine's profiler only sees the GDScript VM. Sort by spike (`MaxTime`) when investigating stutters, not just average.
2. **Cache marshalled property reads in hot loops.** `node.GlobalPosition`, `node.GlobalTransform.Basis`, `material.GetShaderParameter("x")` each cross the boundary. Read once per frame into a local, mutate, write once.
3. **Prefer method-group references over `Callable.From(lambda)` / `Action`-with-capture** when wiring signals or callbacks that fire more than rarely. A lambda capturing locals is a heap allocation per construction; a method group is not.
4. **Throttle per-entity per-frame engine calls when the entity isn't visibly changing.** Short-circuit on unchanged state at the start of the update. Update at less than physics-tick frequency (e.g. every 4th tick) when visual smoothness allows.
5. **Build geometry in managed arrays, submit to the engine in one call.** Per-vertex `SurfaceTool.AddVertex` / `SetNormal` / `SetUV` calls are individually marshalled and dominate chunk-construction cost. See [ADR-0015](0015-meshbuilder-for-chunk-geometry.md) for the chunk-geometry application of this rule.
6. **`Godot.Collections.Array` and `StringName` allocations and comparisons are not free.** Reuse `Array` instances where possible. For repeated `StringName` lookups, cache the `StringName` rather than recreating it per call.

### Techniques applied in the codebase

These exist as concrete reference cases for the rules above:

- **Cached delegate field for signal/callback wiring.** `CollectibleSpawner._sfxCallback` (`src/Nightwalk.Godot/Collectibles/CollectibleSpawner.cs:51`) holds a delegate built once in `SetSfxManager` (`:81`) and re-used by every `CreateNode` call (`:178-187`), replacing the per-spawn `Callable.From(() => _sfxManager.PlayGenerated(...))` closure that used to allocate on every spawn.
- **Method-group reference instead of position-capturing lambda.** `CollectibleSpawner.CreateNode` (`:178`) passes `OnCollectibleCollected` directly as `Action<CollectibleId, int>` rather than wrapping it in `(id, pts) => OnCollectibleCollected(id, pts, pos.X, pos.Y, pos.Z)`. The position is recovered from the live node in the handler (`:272`).
- **Throttle pattern.** `CollectibleSpawner.DespawnDistantNodes` (`:218`) updates per-collectible visibility every 4th physics tick (`ProximityUpdateInterval = 4`, `:57`) while still running cheap despawn/validity checks every tick.
- **Short-circuit on unchanged state.** `CollectibleShape.UpdateVisibility` (`src/Nightwalk.Godot/Collectibles/CollectibleShape.cs:263`) returns early if `alpha == _currentAlpha` so steady-state collectibles cost zero marshalled calls per tick. The sentinel `_currentAlpha = -1f` (`:35`) ensures the first call after `_Ready` always runs.
- **Per-frame work budgets.** `ProximitySpawner.SpawnNearbyNodes` (`src/Nightwalk.Godot/Spawning/ProximitySpawner.cs:149-156`) caps `AddChild` bursts at `MaxSpawnsPerFrame = 8` (`:211`). This is the call-count discipline applied to scene-tree mutations. See also [ADR-0013](0013-progressive-chunk-rendering.md) for the same pattern at the chunk-construction layer.

### Verified

- CodeTrack profile (image stored alongside conversation history) shows `godotsharp_method_bind_ptrcall` at 22.33 s own time with 98.77 % own ratio, top of the non-idle rows.
- Godot Visual Profiler (same session, frame #8349) shows 2.72 ms CPU + 4.45 ms GPU total — under the 16.67 ms 60 fps budget — confirming the engine itself isn't the bottleneck.
- `CollectibleSpawner._sfxCallback` declared at `:51`; assigned via method-group reference at `:81`.
- `CollectibleSpawner.CreateNode` passes `OnCollectibleCollected` as method group at `:185` and `_sfxCallback` at `:186`.
- `CollectibleSpawner.DespawnDistantNodes` throttle gate at `:218`, interval constant at `:57`.
- `CollectibleShape.UpdateVisibility` early-return at `:263-264`; sentinel default at `:35`.
- `ProximitySpawner.MaxSpawnsPerFrame` declared at `:211`; budget loop at `:149-156`.

## Consequences

**Positive:**

- The team has a written cost model. Future perf work starts with "where are we crossing the boundary too often?" rather than "let's tune the shaders".
- The five techniques above are reusable patterns, not one-offs. Any per-frame per-entity update can adopt the throttle + short-circuit shape; any frequently-constructed callback can adopt the cached-delegate shape.
- Concrete file:line references in the Verified section give future contributors examples to copy.
- Makes the link to [ADR-0001](0001-csharp-over-gdscript.md) explicit — the C# choice is reaffirmed, but its cost shape is named.

**Negative:**

- The discipline is reviewer-enforced, not compiler-enforced. A new `Callable.From(lambda)` in a hot path will compile fine. Reviewers need to recognise the pattern.
- Profiling tooling adds friction: developers need a separate .NET profiler (CodeTrack, dotnet-trace, dotTrace) since the Godot built-in profiler won't show C# methods.
- Some legitimate code patterns are slightly more awkward — e.g. signal handlers that need closure state must either store the state on the object or re-look it up rather than capturing it.
- Following rule 5 (managed array building) means the existing `SurfaceTool` API is the slow path. New geometry code must use the `MeshBuilder` pattern instead. See [ADR-0015](0015-meshbuilder-for-chunk-geometry.md).
- The cost model is *empirically* skewed toward interop on the hardware we profiled on (Ryzen 7 7700 + RX 9060 XT). On a GPU-bound machine the picture changes; the *order* of optimisation changes but the rules themselves remain valid.

## Related

- [ADR-0001](0001-csharp-over-gdscript.md) — language choice this ADR builds on; the cost model is the consequence not documented there.
- [ADR-0013](0013-progressive-chunk-rendering.md) — per-frame work budget pattern at the chunk-construction layer; same discipline at a coarser grain.
- [ADR-0015](0015-meshbuilder-for-chunk-geometry.md) — concrete application of rule 5 (managed array building) to chunk geometry.
