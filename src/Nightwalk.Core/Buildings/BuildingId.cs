using Nightwalk.Core.Chunks;

namespace Nightwalk.Core.Buildings;

/// <summary>
/// Unique identifier for a building, combining chunk coordinates and building index.
/// </summary>
public readonly struct BuildingId : IEquatable<BuildingId>
{
    public readonly ChunkCoord ChunkCoord;
    public readonly int BuildingIndex;

    public BuildingId(ChunkCoord chunkCoord, int buildingIndex)
    {
        ChunkCoord = chunkCoord;
        BuildingIndex = buildingIndex;
    }

    public BuildingId(int chunkX, int chunkZ, int buildingIndex)
    {
        ChunkCoord = new ChunkCoord(chunkX, chunkZ);
        BuildingIndex = buildingIndex;
    }

    public bool Equals(BuildingId other) =>
        ChunkCoord.Equals(other.ChunkCoord) && BuildingIndex == other.BuildingIndex;

    public override bool Equals(object? obj) =>
        obj is BuildingId other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(ChunkCoord, BuildingIndex);

    public static bool operator ==(BuildingId left, BuildingId right) => left.Equals(right);
    public static bool operator !=(BuildingId left, BuildingId right) => !left.Equals(right);

    public override string ToString() => $"Building({ChunkCoord.X},{ChunkCoord.Z}:{BuildingIndex})";
}
