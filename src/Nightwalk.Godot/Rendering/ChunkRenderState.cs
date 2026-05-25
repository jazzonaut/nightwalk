using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Godot.Rendering.Geometry;
using Nightwalk.Godot.Rendering.SubRenderers;

namespace Nightwalk.Godot.Rendering;

/// <summary>
/// Phases of progressive chunk rendering, ordered by priority.
/// </summary>
public enum ChunkRenderPhase
{
    /// <summary>Chunk queued but not started.</summary>
    Pending,
    /// <summary>Phase 1: Ground plane and collision.</summary>
    Ground,
    /// <summary>Phase 2: Roads and road markings.</summary>
    Roads,
    /// <summary>Phase 3: Manholes on roads.</summary>
    Manholes,
    /// <summary>Phase 4: Plot pavements (sidewalks).</summary>
    Pavement,
    /// <summary>Phase 5: Building bodies, windows, ledges, rooftops.</summary>
    Buildings,
    /// <summary>Phase 6: Neon signs.</summary>
    Neons,
    /// <summary>Phase 7: Street lamps and lights.</summary>
    StreetLamps,
    /// <summary>All phases complete.</summary>
    Complete
}

/// <summary>
/// Tracks the rendering state for a chunk during progressive rendering.
/// Allows rendering to be spread across multiple frames.
/// </summary>
public sealed class ChunkRenderState
{
    /// <summary>
    /// The chunk coordinate being rendered.
    /// </summary>
    public ChunkCoord Coord { get; }

    /// <summary>
    /// The chunk data to render.
    /// </summary>
    public ChunkData Data { get; }

    /// <summary>
    /// The root node for this chunk in the scene tree.
    /// Created when rendering starts.
    /// </summary>
    public Node3D? ChunkNode { get; set; }

    /// <summary>
    /// Current render phase.
    /// </summary>
    public ChunkRenderPhase CurrentPhase { get; set; } = ChunkRenderPhase.Pending;

    /// <summary>
    /// Shared rendering context with materials and utilities.
    /// </summary>
    public ChunkRenderContext? Context { get; set; }

    /// <summary>
    /// Progress index for incremental building rendering.
    /// </summary>
    public int BuildingIndex { get; set; }

    /// <summary>
    /// Progress index for incremental neon rendering.
    /// </summary>
    public int NeonIndex { get; set; }

    // Pre-initialized MeshBuilders for building phase (created once, reused).
    // MeshBuilder accumulates geometry in managed arrays and submits to ArrayMesh in one call,
    // avoiding the per-vertex SurfaceTool marshalling that previously dominated chunk-build cost.
    public MeshBuilder[]? BuildingBodyMBs { get; set; }
    public MeshBuilder? LedgeMB { get; set; }
    public MeshBuilder? WindowLitMB { get; set; }
    public MeshBuilder? WindowUnlitMB { get; set; }
    public MeshBuilder? DetailsMB { get; set; }
    public List<BoxCollider>? CollisionBoxes { get; set; }

    /// <summary>
    /// Tracks whether building MeshBuilders have been initialized.
    /// </summary>
    public bool BuildingMBsInitialized { get; set; }

    /// <summary>
    /// Tracks building finalization sub-step for incremental completion.
    /// 0 = not started, 1 = collisions done, 2 = mesh done, 3 = occluders done (complete)
    /// </summary>
    public int BuildingFinalizeStep { get; set; }

    /// <summary>
    /// Progress index for incremental collision shape creation.
    /// </summary>
    public int CollisionBoxIndex { get; set; }

    /// <summary>
    /// StaticBody3D for building collisions, created incrementally.
    /// </summary>
    public StaticBody3D? CollisionBody { get; set; }

    /// <summary>
    /// Progress index for incremental mesh surface commits.
    /// Surfaces: 0..N-1 = body materials, N = ledge, N+1 = windowLit, N+2 = windowUnlit, N+3 = details
    /// </summary>
    public int MeshSurfaceIndex { get; set; }

    /// <summary>
    /// ArrayMesh being built incrementally across frames.
    /// </summary>
    public ArrayMesh? BuildingArrayMesh { get; set; }

    /// <summary>
    /// Tracks whether building mesh has been finalized and added to scene.
    /// </summary>
    public bool BuildingMeshFinalized { get; set; }

    /// <summary>
    /// Container node for neon signs during incremental rendering.
    /// </summary>
    public Node3D? NeonsNode { get; set; }

    // ---- Street Lamp incremental state ----

    /// <summary>
    /// Container node for street lamps during incremental rendering.
    /// </summary>
    public Node3D? StreetLampsNode { get; set; }

    /// <summary>
    /// Collected lamp positions for incremental processing.
    /// </summary>
    public List<Vector3>? StreetLampPositions { get; set; }

    /// <summary>
    /// Progress index for incremental lamp light creation.
    /// </summary>
    public int StreetLampIndex { get; set; }

    /// <summary>
    /// Tracks whether street lamp MultiMeshes have been initialized.
    /// </summary>
    public bool StreetLampsInitialized { get; set; }

    // ---- Manhole incremental state ----

    /// <summary>
    /// Container node for manholes during incremental rendering.
    /// </summary>
    public Node3D? ManholesNode { get; set; }

    /// <summary>
    /// Collected manhole positions for incremental processing.
    /// </summary>
    public List<(Vector3 Position, int Seed)>? ManholePositions { get; set; }

    /// <summary>
    /// Progress index for incremental manhole creation.
    /// </summary>
    public int ManholeIndex { get; set; }

    public ChunkRenderState(ChunkCoord coord, ChunkData data)
    {
        Coord = coord;
        Data = data;
    }

    /// <summary>
    /// Cleanup any resources held by this state.
    /// MeshBuilders are pure managed C# objects — no explicit disposal needed.
    /// </summary>
    public void Cleanup()
    {
        BuildingBodyMBs = null;
        LedgeMB = null;
        WindowLitMB = null;
        WindowUnlitMB = null;
        DetailsMB = null;
        CollisionBoxes = null;

        // CollisionBody is added to scene tree immediately when created,
        // so it will be cleaned up with the chunk node. Just clear the reference.
        CollisionBody = null;

        // Free ArrayMesh if it wasn't assigned to a MeshInstance.
        // Once assigned, BuildingArrayMesh is set to null so MeshInstance owns it.
        BuildingArrayMesh?.Dispose();
        BuildingArrayMesh = null;
    }
}
