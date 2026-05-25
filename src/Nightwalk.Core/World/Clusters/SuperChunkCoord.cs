namespace Nightwalk.Core.World.Clusters;

/// <summary>
/// Coordinate for a super-chunk in the cluster generation system.
/// Super-chunks are large regions (e.g., 2000x2000 units) that deterministically
/// generate cluster candidates.
/// </summary>
public readonly struct SuperChunkCoord : IEquatable<SuperChunkCoord>
{
    public readonly int X;
    public readonly int Z;

    public SuperChunkCoord(int x, int z)
    {
        X = x;
        Z = z;
    }

    /// <summary>
    /// Creates a super-chunk coordinate from a world position.
    /// </summary>
    public static SuperChunkCoord FromWorldPosition(float worldX, float worldZ, float superChunkSize)
    {
        // Use floor division to handle negative coordinates correctly
        int x = (int)MathF.Floor(worldX / superChunkSize);
        int z = (int)MathF.Floor(worldZ / superChunkSize);
        return new SuperChunkCoord(x, z);
    }

    /// <summary>
    /// Gets the world-space minimum corner of this super-chunk.
    /// </summary>
    public (float MinX, float MinZ) ToWorldPosition(float superChunkSize)
    {
        return (X * superChunkSize, Z * superChunkSize);
    }

    /// <summary>
    /// Gets the world-space center of this super-chunk.
    /// </summary>
    public (float CenterX, float CenterZ) ToWorldCenter(float superChunkSize)
    {
        float halfSize = superChunkSize / 2f;
        return (X * superChunkSize + halfSize, Z * superChunkSize + halfSize);
    }

    /// <summary>
    /// Generates a deterministic seed for this super-chunk based on world seed.
    /// </summary>
    public int GetSeed(int worldSeed)
    {
        // Use a stable hash combining world seed and coordinates
        unchecked
        {
            int hash = worldSeed;
            hash = hash * 31 + X;
            hash = hash * 31 + Z;
            return hash;
        }
    }

    public bool Equals(SuperChunkCoord other) => X == other.X && Z == other.Z;
    public override bool Equals(object? obj) => obj is SuperChunkCoord other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Z);
    public override string ToString() => $"SuperChunk({X}, {Z})";

    public static bool operator ==(SuperChunkCoord left, SuperChunkCoord right) => left.Equals(right);
    public static bool operator !=(SuperChunkCoord left, SuperChunkCoord right) => !left.Equals(right);
}
