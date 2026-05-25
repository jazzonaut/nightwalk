namespace Nightwalk.Core.Determinism;

/// <summary>
/// Deterministic random number generator using Xorshift32.
/// Fast, deterministic, good distribution.
/// </summary>
public sealed class DeterministicRng : IRng
{
    private uint _state;

    public DeterministicRng(int seed)
    {
        _state = (uint)seed;
        if (_state == 0) _state = 1;  // Avoid zero state
    }

    /// <summary>
    /// Creates an RNG seeded from multiple values.
    /// Useful for chunk-based seeding.
    /// </summary>
    public static DeterministicRng FromCoordinates(int baseSeed, int x, int z)
    {
        var combined = StableHash.Combine(baseSeed, x, z);
        return new DeterministicRng(combined);
    }

    // Xorshift32 - fast, deterministic, good distribution
    private uint Next()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return _state;
    }

    public int NextInt() => (int)(Next() & 0x7FFFFFFF);

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Must be positive");
        return NextInt() % maxExclusive;
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Must be greater than minInclusive");
        return minInclusive + NextInt(maxExclusive - minInclusive);
    }

    public float NextFloat() => (Next() & 0xFFFFFF) / (float)0xFFFFFF;

    public float NextFloat(float min, float max) => min + NextFloat() * (max - min);

    public bool NextBool() => (Next() & 1) == 1;

    public double NextDouble() => (Next() & 0xFFFFFF) / (double)0xFFFFFF;

    /// <summary>
    /// Returns a random element from the list.
    /// </summary>
    public T Choose<T>(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
            throw new ArgumentException("List cannot be empty", nameof(items));
        return items[NextInt(items.Count)];
    }

    /// <summary>
    /// Returns true with the given probability (0.0 to 1.0).
    /// </summary>
    public bool Chance(float probability)
    {
        return NextFloat() < probability;
    }

    /// <summary>
    /// Shuffles the list in place.
    /// </summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
