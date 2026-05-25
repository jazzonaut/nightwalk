namespace Nightwalk.Core.World.Clusters;

/// <summary>
/// Per-cluster style and aesthetic parameters.
/// These are blended based on cluster influence when multiple clusters overlap.
/// </summary>
public sealed class ClusterParameterSet
{
    // Density parameters
    public float EmptyPlotChance { get; init; }
    public float TwoBuildingChance { get; init; }

    // Architectural style parameters
    public float SteppedBuildingChance { get; init; }
    public float SetbackRatioMin { get; init; }
    public float SetbackRatioMax { get; init; }
    public float AsymmetricChance { get; init; }

    // Visual aesthetics parameters
    public float NeonDensityMultiplier { get; init; }
    public float WindowLitPercentage { get; init; }


    /// <summary>
    /// Blends two parameter sets based on a weight (0 = this, 1 = other).
    /// </summary>
    public ClusterParameterSet BlendWith(ClusterParameterSet other, float weight)
    {
        float w1 = 1f - weight;
        float w2 = weight;

        return new ClusterParameterSet
        {
            EmptyPlotChance = EmptyPlotChance * w1 + other.EmptyPlotChance * w2,
            TwoBuildingChance = TwoBuildingChance * w1 + other.TwoBuildingChance * w2,
            SteppedBuildingChance = SteppedBuildingChance * w1 + other.SteppedBuildingChance * w2,
            SetbackRatioMin = SetbackRatioMin * w1 + other.SetbackRatioMin * w2,
            SetbackRatioMax = SetbackRatioMax * w1 + other.SetbackRatioMax * w2,
            AsymmetricChance = AsymmetricChance * w1 + other.AsymmetricChance * w2,
            NeonDensityMultiplier = NeonDensityMultiplier * w1 + other.NeonDensityMultiplier * w2,
            WindowLitPercentage = WindowLitPercentage * w1 + other.WindowLitPercentage * w2
        };
    }

    /// <summary>
    /// Blends multiple parameter sets weighted by their influences.
    /// Returns null if no valid weights.
    /// </summary>
    public static ClusterParameterSet? BlendMultiple(
        IReadOnlyList<(ClusterParameterSet Params, float Influence)> weighted)
    {
        if (weighted.Count == 0)
            return null;

        if (weighted.Count == 1)
            return weighted[0].Params;

        float totalWeight = 0f;
        foreach (var (_, influence) in weighted)
            totalWeight += influence;

        if (totalWeight <= 0f)
            return null;

        float emptyPlotChance = 0f;
        float twoBuildingChance = 0f;
        float steppedBuildingChance = 0f;
        float setbackRatioMin = 0f;
        float setbackRatioMax = 0f;
        float asymmetricChance = 0f;
        float neonDensityMultiplier = 0f;
        float windowLitPercentage = 0f;

        foreach (var (param, influence) in weighted)
        {
            float w = influence / totalWeight;
            emptyPlotChance += param.EmptyPlotChance * w;
            twoBuildingChance += param.TwoBuildingChance * w;
            steppedBuildingChance += param.SteppedBuildingChance * w;
            setbackRatioMin += param.SetbackRatioMin * w;
            setbackRatioMax += param.SetbackRatioMax * w;
            asymmetricChance += param.AsymmetricChance * w;
            neonDensityMultiplier += param.NeonDensityMultiplier * w;
            windowLitPercentage += param.WindowLitPercentage * w;
        }

        return new ClusterParameterSet
        {
            EmptyPlotChance = emptyPlotChance,
            TwoBuildingChance = twoBuildingChance,
            SteppedBuildingChance = steppedBuildingChance,
            SetbackRatioMin = setbackRatioMin,
            SetbackRatioMax = setbackRatioMax,
            AsymmetricChance = asymmetricChance,
            NeonDensityMultiplier = neonDensityMultiplier,
            WindowLitPercentage = windowLitPercentage
        };
    }
}
