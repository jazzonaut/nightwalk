namespace Nightwalk.Core.World.Clusters;

/// <summary>
/// Defines how cluster influence decreases from center to edge.
/// </summary>
public enum ClusterFalloff
{
    /// <summary>
    /// Linear falloff: influence = 1 - (distance / radius).
    /// </summary>
    Linear,

    /// <summary>
    /// Smoothstep falloff: smooth ease-in/ease-out curve.
    /// </summary>
    Smoothstep,

    /// <summary>
    /// Exponential falloff: sharp drop near edge.
    /// </summary>
    Exponential,

    /// <summary>
    /// Inverse-square falloff: gentle plateau then sharp drop.
    /// </summary>
    InverseSquare
}

/// <summary>
/// Static helper for evaluating cluster falloff curves.
/// </summary>
public static class ClusterFalloffEvaluator
{
    /// <summary>
    /// Evaluates the falloff curve at a given normalized distance.
    /// </summary>
    /// <param name="falloff">The falloff type to use.</param>
    /// <param name="normalizedDistance">Distance from center divided by radius (0 = center, 1 = edge).</param>
    /// <returns>Influence value from 0 to 1 (1 = full influence, 0 = no influence).</returns>
    public static float Evaluate(ClusterFalloff falloff, float normalizedDistance)
    {
        // Clamp to valid range
        if (normalizedDistance <= 0f) return 1f;
        if (normalizedDistance >= 1f) return 0f;

        return falloff switch
        {
            ClusterFalloff.Linear => 1f - normalizedDistance,
            ClusterFalloff.Smoothstep => Smoothstep(normalizedDistance),
            ClusterFalloff.Exponential => ExponentialFalloff(normalizedDistance),
            ClusterFalloff.InverseSquare => InverseSquareFalloff(normalizedDistance),
            _ => 1f - normalizedDistance
        };
    }

    /// <summary>
    /// Parses a falloff type from string (case-insensitive).
    /// </summary>
    public static ClusterFalloff Parse(string value)
    {
        return value.ToLowerInvariant().Replace("_", "").Replace("-", "") switch
        {
            "linear" => ClusterFalloff.Linear,
            "smoothstep" => ClusterFalloff.Smoothstep,
            "exponential" => ClusterFalloff.Exponential,
            "inversesquare" => ClusterFalloff.InverseSquare,
            _ => ClusterFalloff.Smoothstep
        };
    }

    private static float Smoothstep(float t)
    {
        // Smoothstep: 1 at center, 0 at edge
        // Standard smoothstep: 3t^2 - 2t^3, but we want inverse (1 at t=0, 0 at t=1)
        float x = 1f - t;
        return x * x * (3f - 2f * x);
    }

    private static float ExponentialFalloff(float t)
    {
        // Exponential decay: maintains high influence then drops sharply
        // e^(-3t) normalized to 0-1 range
        const float k = 3f;
        float raw = MathF.Exp(-k * t);
        float atEdge = MathF.Exp(-k);
        // Normalize so that at t=1, result is 0
        return (raw - atEdge) / (1f - atEdge);
    }

    private static float InverseSquareFalloff(float t)
    {
        // Inverse square: 1 / (1 + k*t^2), normalized
        // Creates a gentle plateau near center, then drops off
        const float k = 4f;
        float raw = 1f / (1f + k * t * t);
        float atEdge = 1f / (1f + k);
        // Normalize so that at t=1, result is 0
        return (raw - atEdge) / (1f - atEdge);
    }
}
