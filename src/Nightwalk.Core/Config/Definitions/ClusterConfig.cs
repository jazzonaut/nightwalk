namespace Nightwalk.Core.Config;

/// <summary>
/// Configuration for the city cluster system.
/// Maps to [buildings.clusters] section in worldgen.toml.
/// </summary>
public sealed class ClusterConfig
{
    public bool Enabled { get; set; }
    public float SuperChunkSize { get; set; }
    public float AverageClustersPerSuperChunk { get; set; }
    public float MinimumSpacing { get; set; }

    /// <summary>
    /// Radius in super-chunks to pre-generate at initialization.
    /// Reduces generation bursts when exploring distant areas.
    /// Set to 0 to disable pre-warming.
    /// </summary>
    public int PreWarmRadius { get; set; }

    public ClusterOriginConfig Origin { get; set; } = null!;
    public ClusterGenerationConfig Generation { get; set; } = null!;
}

/// <summary>
/// Configuration for the guaranteed origin cluster at (0,0).
/// </summary>
public sealed class ClusterOriginConfig
{
    public bool Enabled { get; set; }
    public float Radius { get; set; }
    public float HeightMultiplier { get; set; }
    public string Falloff { get; set; } = null!;

    // Multipliers applied to base parameters for origin cluster
    public float EmptyPlotChanceMultiplier { get; set; }
    public float SteppedBuildingChanceMultiplier { get; set; }
    public float NeonDensityMultiplier { get; set; }
    public float WindowLitPercentageMultiplier { get; set; }
}

/// <summary>
/// Configuration for procedurally generated clusters.
/// </summary>
public sealed class ClusterGenerationConfig
{
    public float RadiusMin { get; set; }
    public float RadiusMax { get; set; }
    public float HeightMultiplierMin { get; set; }
    public float HeightMultiplierMax { get; set; }

    /// <summary>
    /// Multiplier range applied to base parameter values to create per-cluster variation.
    /// Base values come from [buildings], [neon], [windows] sections.
    /// </summary>
    public float MultiplierMin { get; set; }
    public float MultiplierMax { get; set; }

    public ClusterFalloffWeightsConfig FalloffWeights { get; set; } = null!;
}

/// <summary>
/// Weighted probabilities for falloff curve selection.
/// </summary>
public sealed class ClusterFalloffWeightsConfig
{
    public float Linear { get; set; }
    public float Smoothstep { get; set; }
    public float Exponential { get; set; }
    public float InverseSquare { get; set; }
}
