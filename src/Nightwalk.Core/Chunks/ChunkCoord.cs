namespace Nightwalk.Core.Chunks;

/// <summary>
/// Integer coordinate identifying a chunk in the world grid.
/// </summary>
public readonly struct ChunkCoord : IEquatable<ChunkCoord>
{
    public readonly int X;
    public readonly int Z;

    public ChunkCoord(int x, int z)
    {
        X = x;
        Z = z;
    }

    /// <summary>
    /// Converts a world position to chunk coordinates.
    /// </summary>
    public static ChunkCoord FromWorldPosition(float worldX, float worldZ, float chunkSize)
    {
        return new ChunkCoord(
            (int)MathF.Floor(worldX / chunkSize),
            (int)MathF.Floor(worldZ / chunkSize)
        );
    }

    /// <summary>
    /// Gets the world position of the chunk's minimum corner (origin).
    /// </summary>
    public (float X, float Z) ToWorldPosition(float chunkSize)
    {
        return (X * chunkSize, Z * chunkSize);
    }

    /// <summary>
    /// Gets the world position of the chunk's center.
    /// </summary>
    public (float X, float Z) ToWorldCenter(float chunkSize)
    {
        var half = chunkSize * 0.5f;
        return (X * chunkSize + half, Z * chunkSize + half);
    }

    /// <summary>
    /// Manhattan distance to another chunk.
    /// </summary>
    public int DistanceTo(ChunkCoord other)
    {
        return Math.Abs(X - other.X) + Math.Abs(Z - other.Z);
    }

    /// <summary>
    /// Chebyshev distance (max of X/Z distance) - better for square loading radius.
    /// </summary>
    public int ChebyshevDistanceTo(ChunkCoord other)
    {
        return Math.Max(Math.Abs(X - other.X), Math.Abs(Z - other.Z));
    }

    public bool Equals(ChunkCoord other) => X == other.X && Z == other.Z;
    public override bool Equals(object? obj) => obj is ChunkCoord other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Z);
    public override string ToString() => $"({X}, {Z})";

    public static bool operator ==(ChunkCoord left, ChunkCoord right) => left.Equals(right);
    public static bool operator !=(ChunkCoord left, ChunkCoord right) => !left.Equals(right);
}
