namespace Nightwalk.Core.Chunks;

/// <summary>
/// Lifecycle state of a chunk.
/// </summary>
public enum ChunkState
{
    /// <summary>
    /// Chunk data not loaded.
    /// </summary>
    Unloaded,

    /// <summary>
    /// Chunk is queued for generation.
    /// </summary>
    Queued,

    /// <summary>
    /// Chunk is being generated.
    /// </summary>
    Generating,

    /// <summary>
    /// Chunk is fully loaded and active.
    /// </summary>
    Loaded,

    /// <summary>
    /// Chunk is being unloaded.
    /// </summary>
    Unloading
}
