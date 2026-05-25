# ADR-0015: Chunk geometry built via `MeshBuilder`, not `SurfaceTool`

## Status

Accepted (2026-05-25)

## Context

Each chunk's geometry — building bodies, ledges, windows (lit + unlit), facade detail meshes (AC units, pipes), road quads, road markings — is built procedurally on the main thread during chunk rendering (see [ADR-0013](0013-progressive-chunk-rendering.md) for the progressive scheduler that hosts this work).

Godot's idiomatic mesh-construction API is `SurfaceTool`: call `Begin(Triangles)`, then `SetNormal`/`SetUV`/`SetUV2` followed by `AddVertex` for each vertex, then `Index()` and `Commit()` to produce an `ArrayMesh`. The API reads cleanly but every call crosses the C# ↔ Godot boundary, and a single building generates hundreds of these calls (6 faces × 4 vertices × multiple property setters per vertex, plus 4 ledges per floor × multiple floors, plus windows lit/unlit).

Profiling (2026-05-25, CodeTrack) found this was the dominant chunk-construction cost on a Ryzen 7 7700 + RX 9060 XT:

| Method | Cumulative time | Own % |
|---|---|---|
| `GeometryBuilder.AddQuad` (SurfaceTool path) | 2.57 s | 1.69 % own |
| `BuildingRenderer.AddWindowGeometry` | 1.15 s | 4.80 % own |
| `Godot.SurfaceTool.SetNormal(Vector3)` | 438 ms | 78 ms own |
| `Godot.Vector3.LengthSquared` | 195 ms | 100 % own (called from `SurfaceTool.GenerateNormals` / `Index`) |

The low *own* percentages on `AddQuad` and `AddWindowGeometry` (1-5 %) confirm the time isn't in our code — it's in the marshalled calls those methods make. The same pattern as [ADR-0014](0014-csharp-godot-interop-cost.md).

A precedent already existed: `RoadRenderer` (added before this ADR) used a class called `MeshBuilder` that accumulated geometry into managed `List<Vector3>` / `List<Vector2>` / `List<int>` and submitted it via a single `ArrayMesh.AddSurfaceFromArrays` call per material. The pattern was correct; it just hadn't been propagated to the building-geometry path.

### Alternatives considered

- **Keep `SurfaceTool`, accept the cost.** Rejected by the profile data — chunk-integration spikes were a measurable hitch source.
- **Move chunk construction to a background thread.** Rejected for the same reasons as [ADR-0013](0013-progressive-chunk-rendering.md): Godot's `Node`/`Mesh` APIs are largely main-thread-only and the parts that are thread-safe are mixed with parts that aren't.
- **Use `MultiMeshInstance3D` per chunk per surface.** Considered; ruled out for static merged chunk geometry. `MultiMesh` is the right tool for many instances of the *same* mesh, where you'd otherwise have many `MeshInstance3D` nodes. Buildings and their facades are already merged into one mesh per material per chunk; `MultiMesh` would add per-instance overhead without reducing draw calls below the current "one draw call per surface per chunk" baseline.
- **Adopt the pre-existing `MeshBuilder` pattern for building geometry and propagate it.** Lowest-risk, matches the precedent, no new abstraction.

The last option is what this ADR commits to.

## Decision

`MeshBuilder` (`src/Nightwalk.Godot/Rendering/Geometry/MeshBuilder.cs`) is the preferred mesh-construction tool for chunk geometry. `SurfaceTool` is the legacy path and is only used for cases where its features (vertex deduplication via `Index()`, `GenerateNormals`, `GenerateTangents`) are actually needed.

### Shape

`MeshBuilder` accumulates geometry into managed C# lists:

- `List<Vector3> _vertices, _normals` — required, always populated.
- `List<Vector2> _uvs, _uv2s` — optional, flagged by `_hasUVs` / `_hasUV2s`.
- `List<Color> _colors` — optional, flagged by `_hasColors`.
- `List<int> _indices` — required; quads write `[0,1,2, 0,2,3]` patterns explicitly.

Methods cover the primitives chunk geometry needs:

| Method | Use |
|---|---|
| `AddQuad(a, b, c, d, normal)` | Plain quad; ledges and parapets. |
| `AddTopQuad(cx, cy, cz, w, d)` | Horizontal quad facing `+Y`; road and pavement surfaces. |
| `AddBox(cx, cy, cz, w, h, d)` | 6-face box; facade detail (AC units, pipes), terrace parapets. |
| `AddQuadWithUV(a, b, c, d, normal, uvA, uvB, uvC, uvD)` | Per-vertex UVs. |
| `AddQuadWithUVColor(...)` / `AddPlotTopQuad(...)` | UV + UV2 + Color quad; plot pavements use the latter for per-vertex edge colour. |
| `AddQuadUniformUV2(a, b, c, d, normal, uv, uv2)` | Same UV + UV2 on all 4 vertices; lit window quads. |
| `AddQuadWithSeed(a, b, c, d, normal, seed)` | UV.x = seed on all 4 vertices, no UV2; building/facade geometry that drives the weathering shader. |
| `AddBoxWithSeed(cx, cy, cz, w, h, d, seed)` | 6-face box with seed UV; building bodies. |
| `ToSurfaceArrays() → Godot.Collections.Array?` | Builds the array dictionary expected by `ArrayMesh.AddSurfaceFromArrays`; returns `null` for empty builders so the caller skips that surface. |

`GeometryBuilder` (same folder) provides `MeshBuilder` overloads of `AddBox`, `AddBoxWithSeed`, `AddWindowQuad`, and `AddWindowQuadWithUV` alongside the existing `SurfaceTool` overloads. The signatures are otherwise identical, so call-sites swap `SurfaceTool` → `MeshBuilder` cleanly.

### The chunk-merge pattern

Each `IPhasedSubRenderer` for chunk geometry holds one `MeshBuilder` per material (more precisely, per intended `ArrayMesh` surface) on `ChunkRenderState`. At commit time, each `MeshBuilder.ToSurfaceArrays()` becomes one `ArrayMesh.AddSurfaceFromArrays` call. The whole chunk's geometry ends up as a single `MeshInstance3D` (`"Buildings"`, `"Roads"`, `"GroundMesh"`, `"PlotPavements"`, …) with one surface per material. Per-vertex variation (window state, weathering seed, road edge colour) is carried in UV / UV2 / Color rather than per-instance shader parameters.

### Where `SurfaceTool` is still legitimate

| File | Reason |
|---|---|
| `Collectibles/PolyhedronMeshGenerator.cs` | One-shot polyhedron meshes; not per-chunk hot path. |
| `Beacons/PrismNode.cs`, `Beacons/BeaconNode.cs` | One-shot per beacon. |
| `Buildings/BuildingHighlightRenderer.cs` | Event-driven (climbed-building overlay), not per-chunk. |
| `CityAliveness/CarTrailRenderer.cs` | Event-driven trail meshes. |
| `Resonance/WardenRenderer.cs` (via shard meshes) | Event-driven boss meshes. |
| `Rendering/Sky/DistantCitySilhouette.cs` | One-shot, generated once at world load. |
| `Rendering/SubRenderers/ManholeRenderer.cs` | One-shot per manhole; small mesh, low frequency. |
| `Rendering/SubRenderers/PlotPavementRenderer.cs` | Currently SurfaceTool; could migrate to MeshBuilder but is not on the spike-causing hot path. |
| `Ziplines/ZiplineCable.cs` | Event-driven (player builds zipline). |

These remain on `SurfaceTool` because they are either one-shot construction (no spike-frequency issue) or rely on `GenerateNormals`/`GenerateTangents`/`Index` features that `MeshBuilder` does not offer.

### Verified

- `MeshBuilder` class at `src/Nightwalk.Godot/Rendering/Geometry/MeshBuilder.cs:10`.
- Added methods for building geometry parity: `AddQuadUniformUV2` (`:128`), `AddQuadWithSeed` (`:167`), `AddBoxWithSeed` (`:200`).
- `MeshBuilder` overloads in `GeometryBuilder` at `src/Nightwalk.Godot/Rendering/Geometry/GeometryBuilder.cs:249-292`, alongside the original `SurfaceTool` overloads.
- `ChunkRenderState` holds `BuildingBodyMBs`, `LedgeMB`, `WindowLitMB`, `WindowUnlitMB`, `DetailsMB` (`src/Nightwalk.Godot/Rendering/ChunkRenderState.cs:76-86`); `Cleanup()` no longer disposes Vulkan resources because `MeshBuilder` is managed C# (`:175`).
- `BuildingRenderer.InitializeMeshBuilders` (`src/Nightwalk.Godot/Rendering/SubRenderers/BuildingRenderer.cs:231-243`) allocates one `MeshBuilder` per body material plus four for ledge/window-lit/window-unlit/details.
- `BuildingRenderer.FinalizeMeshesIncremental` (`:300-368`) commits each `MeshBuilder` via `CommitMeshBuilderSurface` (`:373-380`), which calls `ArrayMesh.AddSurfaceFromArrays` once per non-empty builder. The progressive time-budget loop is preserved.
- `FacadeDetailGenerator.AddFacadeDetails` (`src/Nightwalk.Godot/Rendering/SubRenderers/FacadeDetailGenerator.cs`) takes `MeshBuilder` for AC-unit and pipe geometry.
- `RoadRenderer` is the pre-existing precedent: `new MeshBuilder()` at `src/Nightwalk.Godot/Rendering/SubRenderers/RoadRenderer.cs:39-40`.
- Build clean (zero warnings, zero errors) verified via `dotnet build` after the migration.
- Winding order and per-face normals match the previous `SurfaceTool` implementation exactly — see the `// Winding matches AddBox / GeometryBuilder.AddBoxWithSeed exactly.` comment at `MeshBuilder.cs:215`.

## Consequences

**Positive:**

- The per-vertex C# ↔ Godot interop cost on the chunk-build hot path is eliminated. Each surface now costs one marshalled `AddSurfaceFromArrays` call instead of (4 vertices × 3-4 setters + 1 add) per quad × thousands of quads.
- `MeshBuilder` and `MeshBuilder`-flavoured `GeometryBuilder` overloads coexist with the `SurfaceTool` versions, so the migration could proceed incrementally — and remaining `SurfaceTool` callers do not have to move until their geometry shows up as a hot path.
- The chunk-merge pattern is now codified: one `MeshInstance3D` per chunk per surface, with per-vertex variation in UV/UV2/Color. This blocks future "should we batch X into MultiMesh?" detours for static chunk geometry — the answer is "it's already batched at the chunk level, more efficiently than MultiMesh would manage".
- Builders are pure managed C# objects. No Vulkan resources to dispose, so `ChunkRenderState.Cleanup()` is simpler and there's no leak risk if a chunk is cancelled mid-build.
- `ToSurfaceArrays()` returning `null` when empty replaces the previous "commit a SurfaceTool, check `SurfaceCount > 0`" dance with a clean nullcheck.

**Negative:**

- `MeshBuilder` does not deduplicate vertices the way `SurfaceTool.Index()` did. For building geometry this is a no-op because every face has a unique normal (flat-shaded), so deduplication never reduced vertex count anyway — but a future caller adding smooth-shaded geometry would lose the dedup. If that case appears, either compute indices manually in the caller or extend `MeshBuilder` with an opt-in dedup pass.
- `MeshBuilder` does not provide `GenerateNormals` / `GenerateTangents`. Callers that relied on those on `SurfaceTool` must compute normals/tangents up-front (as the building renderer always did) or stay on `SurfaceTool`. The list under "Where `SurfaceTool` is still legitimate" above is the current inventory.
- A new contributor writing a new chunk renderer must know to reach for `MeshBuilder` rather than `SurfaceTool`. The existence of this ADR plus the `// avoiding the per-vertex SurfaceTool marshalling that previously dominated chunk-build cost.` comment at `ChunkRenderState.cs:78` are the discoverability hooks. Future-proof reviewers should treat a fresh `new SurfaceTool()` in a phased sub-renderer as a regression flag.
- Memory pressure during chunk-build shifts from native (SurfaceTool's internal buffers) to managed C# `List<T>`. With max 2 concurrent renders ([ADR-0013](0013-progressive-chunk-rendering.md)), peak in-flight managed memory is bounded (~1 MB order of magnitude for a populated chunk).
- The previous `SurfaceTool` path used `Commit() → SurfaceGetArrays(0)` to extract a `Godot.Collections.Array` of a fully-constructed ArrayMesh — implicitly forcing a buffer copy in Godot. The new path passes managed arrays directly. The wire-level data is the same, but the ownership story is simpler.

## Related

- [ADR-0001](0001-csharp-over-gdscript.md) — language choice that creates the interop boundary.
- [ADR-0013](0013-progressive-chunk-rendering.md) — the time-budget chunk scheduler. The MeshBuilder migration compounds with the progressive budget: each surface now commits cheaper, so more chunks fit within the same per-frame budget.
- [ADR-0014](0014-csharp-godot-interop-cost.md) — the cost-model ADR; this ADR is one concrete application of rule 5 (managed array building) from that one.
- `docs/systems/rendering.md` — system-level overview of the rendering subsystem.
