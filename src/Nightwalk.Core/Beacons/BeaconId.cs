using Nightwalk.Core.Chunks;

namespace Nightwalk.Core.Beacons;

/// <summary>
/// Unique identifier for a beacon, combining chunk coordinate and building index.
/// </summary>
public readonly struct BeaconId : IEquatable<BeaconId>
{
    /// <summary>
    /// Chunk containing this beacon.
    /// </summary>
    public ChunkCoord ChunkCoord { get; }

    /// <summary>
    /// Building index within the chunk.
    /// </summary>
    public int BuildingIndex { get; }

    public BeaconId(ChunkCoord chunkCoord, int buildingIndex)
    {
        ChunkCoord = chunkCoord;
        BuildingIndex = buildingIndex;
    }

    /// <summary>
    /// Creates a beacon ID for a building in a chunk.
    /// </summary>
    public static BeaconId ForBuilding(ChunkCoord coord, int buildingIndex)
        => new(coord, buildingIndex);

    public bool Equals(BeaconId other)
        => ChunkCoord.Equals(other.ChunkCoord) && BuildingIndex == other.BuildingIndex;

    public override bool Equals(object? obj)
        => obj is BeaconId other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(ChunkCoord, BuildingIndex);

    public override string ToString()
        => $"Beacon({ChunkCoord.X},{ChunkCoord.Z}:{BuildingIndex})";

    public static bool operator ==(BeaconId left, BeaconId right) => left.Equals(right);
    public static bool operator !=(BeaconId left, BeaconId right) => !left.Equals(right);
}
