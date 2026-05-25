namespace Nightwalk.Core.World.Clusters;

/// <summary>
/// Represents a single city cluster with its position, radius, and parameters.
/// </summary>
public sealed class CityCluster
{
    /// <summary>
    /// World X coordinate of cluster center.
    /// </summary>
    public float CenterX { get; init; }

    /// <summary>
    /// World Z coordinate of cluster center.
    /// </summary>
    public float CenterZ { get; init; }

    /// <summary>
    /// Radius of the cluster's influence area.
    /// </summary>
    public float Radius { get; init; }

    /// <summary>
    /// Height multiplier applied to buildings at the cluster center.
    /// Additive bonus: final multiplier = 1.0 + sum of (influence * (heightMultiplier - 1.0))
    /// </summary>
    public float HeightMultiplier { get; init; }

    /// <summary>
    /// Falloff curve for how influence decreases from center to edge.
    /// </summary>
    public ClusterFalloff Falloff { get; init; }

    /// <summary>
    /// Per-cluster style and aesthetic parameters.
    /// </summary>
    public ClusterParameterSet Parameters { get; init; } = null!;

    /// <summary>
    /// Whether this is the guaranteed origin cluster.
    /// </summary>
    public bool IsOriginCluster { get; init; }

    /// <summary>
    /// Calculates the influence of this cluster at a given world position.
    /// Returns 0 if outside radius, 1 at center, with falloff curve applied.
    /// </summary>
    public float GetInfluenceAt(float worldX, float worldZ)
    {
        float dx = worldX - CenterX;
        float dz = worldZ - CenterZ;
        float distance = MathF.Sqrt(dx * dx + dz * dz);

        if (distance >= Radius)
            return 0f;

        float normalizedDistance = distance / Radius;
        return ClusterFalloffEvaluator.Evaluate(Falloff, normalizedDistance);
    }

    /// <summary>
    /// Calculates the squared distance from this cluster's center to a point.
    /// Useful for quick distance comparisons without sqrt.
    /// </summary>
    public float DistanceSquaredTo(float worldX, float worldZ)
    {
        float dx = worldX - CenterX;
        float dz = worldZ - CenterZ;
        return dx * dx + dz * dz;
    }

    /// <summary>
    /// Checks if a point is within this cluster's radius.
    /// </summary>
    public bool ContainsPoint(float worldX, float worldZ)
    {
        return DistanceSquaredTo(worldX, worldZ) < Radius * Radius;
    }

    public override string ToString()
    {
        return $"Cluster({CenterX:F0}, {CenterZ:F0}) r={Radius:F0} h={HeightMultiplier:F2}";
    }
}
