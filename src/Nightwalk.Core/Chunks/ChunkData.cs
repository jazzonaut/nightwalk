using Nightwalk.Core.Determinism;
using Nightwalk.Core.World.Buildings;
using Nightwalk.Core.World.Neon;
using Nightwalk.Core.World.Roads;

namespace Nightwalk.Core.Chunks;

/// <summary>
/// Contains all generated data for a chunk.
/// This is the pure data - rendering is handled separately.
/// </summary>
public sealed class ChunkData
{
    public ChunkCoord Coord { get; }
    public ChunkState State { get; set; }
    public float Size { get; }

    // World generation seed for this chunk (derived from world seed + coord)
    public int ChunkSeed { get; }

    // Generated content (initial capacities based on typical chunk content)
    public List<RoadSegment> Roads { get; } = new(8);
    public List<PlotData> Plots { get; } = new(16);
    public List<BuildingData> Buildings { get; } = new(32);
    public List<NeonLightData> Neons { get; } = new(64);

    /// <summary>
    /// Indices into <see cref="Plots"/> for plots that have no buildings.
    /// Used for spawning collectibles on empty plots.
    /// </summary>
    public List<int> EmptyPlotIndices { get; } = new(4);

    public ChunkData(ChunkCoord coord, float size, int worldSeed)
    {
        Coord = coord;
        Size = size;
        State = ChunkState.Unloaded;

        // Generate deterministic seed for this chunk
        ChunkSeed = StableHash.Combine(worldSeed, coord.X, coord.Z);
    }

    /// <summary>
    /// Gets the world-space bounds of this chunk.
    /// </summary>
    public (float MinX, float MinZ, float MaxX, float MaxZ) GetWorldBounds()
    {
        var (minX, minZ) = Coord.ToWorldPosition(Size);
        return (minX, minZ, minX + Size, minZ + Size);
    }
}
