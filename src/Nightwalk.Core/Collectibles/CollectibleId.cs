using Nightwalk.Core.Chunks;

namespace Nightwalk.Core.Collectibles;

/// <summary>
/// Unique identifier for a collectible, combining chunk coordinates, location type, and index.
/// Supports multiple spawn location types (buildings, empty plots, future types).
/// </summary>
public readonly struct CollectibleId : IEquatable<CollectibleId>
{
    public readonly ChunkCoord ChunkCoord;
    public readonly CollectibleLocationType LocationType;
    public readonly int Index;

    public CollectibleId(ChunkCoord chunkCoord, CollectibleLocationType locationType, int index)
    {
        ChunkCoord = chunkCoord;
        LocationType = locationType;
        Index = index;
    }

    /// <summary>
    /// Creates a collectible ID for a building rooftop.
    /// </summary>
    public static CollectibleId ForBuilding(ChunkCoord chunkCoord, int buildingIndex) =>
        new(chunkCoord, CollectibleLocationType.BuildingRooftop, buildingIndex);

    /// <summary>
    /// Creates a collectible ID for an empty plot.
    /// </summary>
    public static CollectibleId ForEmptyPlot(ChunkCoord chunkCoord, int plotIndex) =>
        new(chunkCoord, CollectibleLocationType.EmptyPlot, plotIndex);

    public bool Equals(CollectibleId other) =>
        ChunkCoord.Equals(other.ChunkCoord) &&
        LocationType == other.LocationType &&
        Index == other.Index;

    public override bool Equals(object? obj) =>
        obj is CollectibleId other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(ChunkCoord, LocationType, Index);

    public static bool operator ==(CollectibleId left, CollectibleId right) => left.Equals(right);
    public static bool operator !=(CollectibleId left, CollectibleId right) => !left.Equals(right);

    public override string ToString() =>
        LocationType switch
        {
            CollectibleLocationType.BuildingRooftop => $"Building({ChunkCoord.X},{ChunkCoord.Z}:{Index})",
            CollectibleLocationType.EmptyPlot => $"EmptyPlot({ChunkCoord.X},{ChunkCoord.Z}:{Index})",
            _ => $"Collectible({LocationType},{ChunkCoord.X},{ChunkCoord.Z}:{Index})"
        };
}
