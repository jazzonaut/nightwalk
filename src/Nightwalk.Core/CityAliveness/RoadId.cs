using Nightwalk.Core.Chunks;
using Nightwalk.Core.World.Roads;

namespace Nightwalk.Core.CityAliveness;

/// <summary>
/// Unique identifier for a road segment, combining chunk coordinates and road index.
/// </summary>
public readonly struct RoadId : IEquatable<RoadId>
{
    public readonly ChunkCoord ChunkCoord;
    public readonly int RoadIndex;

    public RoadId(ChunkCoord chunkCoord, int roadIndex)
    {
        ChunkCoord = chunkCoord;
        RoadIndex = roadIndex;
    }

    public RoadId(int chunkX, int chunkZ, int roadIndex)
    {
        ChunkCoord = new ChunkCoord(chunkX, chunkZ);
        RoadIndex = roadIndex;
    }

    public bool Equals(RoadId other) =>
        ChunkCoord.Equals(other.ChunkCoord) && RoadIndex == other.RoadIndex;

    public override bool Equals(object? obj) =>
        obj is RoadId other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(ChunkCoord, RoadIndex);

    public static bool operator ==(RoadId left, RoadId right) => left.Equals(right);
    public static bool operator !=(RoadId left, RoadId right) => !left.Equals(right);

    public override string ToString() => $"Road({ChunkCoord.X},{ChunkCoord.Z}:{RoadIndex})";
}
