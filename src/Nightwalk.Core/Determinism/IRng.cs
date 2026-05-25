namespace Nightwalk.Core.Determinism;

/// <summary>
/// Interface for deterministic random number generation.
/// </summary>
public interface IRng
{
    int NextInt();
    int NextInt(int maxExclusive);
    int NextInt(int minInclusive, int maxExclusive);
    float NextFloat();  // 0.0 to 1.0
    float NextFloat(float min, float max);
    bool NextBool();
    double NextDouble();

    /// <summary>
    /// Returns a random element from the list.
    /// </summary>
    T Choose<T>(IReadOnlyList<T> items);

    /// <summary>
    /// Returns true with the given probability (0.0 to 1.0).
    /// </summary>
    bool Chance(float probability);

    /// <summary>
    /// Shuffles the list in place.
    /// </summary>
    void Shuffle<T>(IList<T> list);
}
