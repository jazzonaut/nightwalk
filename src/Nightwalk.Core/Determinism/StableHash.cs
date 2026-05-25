namespace Nightwalk.Core.Determinism;

/// <summary>
/// Stable, deterministic 32-bit hash for seeding.
/// Avoids HashCode.Combine which is randomized per process.
/// Zero-allocation alternative to System.Random for deterministic values.
/// </summary>
public static class StableHash
{
    private const uint FnvOffsetBasis = 2166136261u;
    private const uint FnvPrime = 16777619u;

    public static int Combine(int a, int b)
    {
        unchecked
        {
            uint hash = FnvOffsetBasis;
            hash = (hash ^ (uint)a) * FnvPrime;
            hash = (hash ^ (uint)b) * FnvPrime;
            return (int)hash;
        }
    }

    public static int Combine(int a, int b, int c)
    {
        unchecked
        {
            uint hash = FnvOffsetBasis;
            hash = (hash ^ (uint)a) * FnvPrime;
            hash = (hash ^ (uint)b) * FnvPrime;
            hash = (hash ^ (uint)c) * FnvPrime;
            return (int)hash;
        }
    }

    public static int Combine(int a, int b, int c, int d)
    {
        unchecked
        {
            uint hash = FnvOffsetBasis;
            hash = (hash ^ (uint)a) * FnvPrime;
            hash = (hash ^ (uint)b) * FnvPrime;
            hash = (hash ^ (uint)c) * FnvPrime;
            hash = (hash ^ (uint)d) * FnvPrime;
            return (int)hash;
        }
    }

    /// <summary>
    /// Returns a deterministic float in [0, 1) from the hash inputs.
    /// </summary>
    public static float ToFloat01(int a, int b)
    {
        var hash = Combine(a, b);
        return (uint)hash / (float)uint.MaxValue;
    }

    /// <summary>
    /// Returns a deterministic float in [0, 1) from the hash inputs.
    /// </summary>
    public static float ToFloat01(int a, int b, int c)
    {
        var hash = Combine(a, b, c);
        return (uint)hash / (float)uint.MaxValue;
    }

    /// <summary>
    /// Returns a deterministic float in [0, 1) from the hash inputs.
    /// </summary>
    public static float ToFloat01(int a, int b, int c, int d)
    {
        var hash = Combine(a, b, c, d);
        return (uint)hash / (float)uint.MaxValue;
    }

    /// <summary>
    /// Returns a deterministic int in [0, maxExclusive) from the hash inputs.
    /// </summary>
    public static int ToInt(int a, int b, int maxExclusive)
    {
        if (maxExclusive <= 0) return 0;
        var hash = (uint)Combine(a, b);
        return (int)(hash % (uint)maxExclusive);
    }

    /// <summary>
    /// Returns a deterministic int in [0, maxExclusive) from the hash inputs.
    /// </summary>
    public static int ToInt(int a, int b, int c, int maxExclusive)
    {
        if (maxExclusive <= 0) return 0;
        var hash = (uint)Combine(a, b, c);
        return (int)(hash % (uint)maxExclusive);
    }
}
