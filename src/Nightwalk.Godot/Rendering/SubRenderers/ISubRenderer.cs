using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Godot.Rendering;

namespace Nightwalk.Godot.Rendering.SubRenderers;

/// <summary>
/// Interface for sub-renderers that contribute to chunk visualization.
/// Each sub-renderer handles one aspect of chunk rendering (ground, roads, buildings, etc.).
/// </summary>
public interface ISubRenderer
{
    /// <summary>
    /// Render this aspect of the chunk into the parent node.
    /// </summary>
    /// <param name="parent">The chunk's root Node3D to add geometry to.</param>
    /// <param name="data">The chunk data containing world geometry information.</param>
    /// <param name="context">Shared rendering context with materials and utilities.</param>
    void Render(Node3D parent, ChunkData data, ChunkRenderContext context);
}

/// <summary>
/// Interface for sub-renderers that support incremental/phased rendering.
/// Allows rendering to be spread across multiple frames to avoid stuttering.
/// </summary>
public interface IPhasedSubRenderer
{
    /// <summary>
    /// The render phase this sub-renderer handles.
    /// </summary>
    ChunkRenderPhase Phase { get; }

    /// <summary>
    /// Render incrementally within the given time budget.
    /// </summary>
    /// <param name="state">Current chunk render state with progress tracking.</param>
    /// <param name="budgetUsec">Time budget in microseconds.</param>
    /// <returns>True if this phase is complete, false if more work is needed.</returns>
    bool RenderIncremental(ChunkRenderState state, long budgetUsec);
}
