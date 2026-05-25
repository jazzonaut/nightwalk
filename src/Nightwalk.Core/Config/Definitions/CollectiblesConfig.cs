namespace Nightwalk.Core.Config;

/// <summary>
/// Configuration for collectible polyhedra on rooftops.
/// </summary>
public sealed class CollectiblesConfig
{
    // Shape type
    /// <summary>Minimum face count for shape selection (4=tetra, 6=cube, 8=octa, 12=dodeca, 20=icosa).</summary>
    public int MinFaces { get; set; } = 4;

    /// <summary>Maximum face count for shape selection (4=tetra, 6=cube, 8=octa, 12=dodeca, 20=icosa).</summary>
    public int MaxFaces { get; set; } = 20;

    // Appearance
    /// <summary>Shape radius in meters.</summary>
    public float Size { get; set; }

    /// <summary>Height above rooftop center in meters.</summary>
    public float FloatHeight { get; set; }

    /// <summary>Rotation speed in degrees per second.</summary>
    public float SpinSpeed { get; set; }

    /// <summary>Vertical oscillation amplitude in meters.</summary>
    public float BobAmplitude { get; set; }

    /// <summary>Vertical oscillation cycles per second.</summary>
    public float BobSpeed { get; set; }

    // Glow effect
    /// <summary>Hex color for the emissive glow.</summary>
    public string GlowColor { get; set; } = null!;

    /// <summary>Emission energy multiplier.</summary>
    public float GlowIntensity { get; set; }

    /// <summary>Hex color for the edge lines.</summary>
    public string EdgeColor { get; set; } = null!;

    /// <summary>Thickness of edge lines as a fraction of size.</summary>
    public float EdgeThickness { get; set; }

    // Point light
    /// <summary>OmniLight3D range in meters.</summary>
    public float LightRange { get; set; }

    /// <summary>Light brightness energy.</summary>
    public float LightEnergy { get; set; }

    // Proximity visibility
    /// <summary>Distance at which collectible starts fading in.</summary>
    public float VisibilityDistance { get; set; }

    /// <summary>Distance over which the fade-in occurs.</summary>
    public float VisibilityFadeMargin { get; set; }

    // Scoring
    /// <summary>Base points awarded per collection.</summary>
    public int BasePoints { get; set; }

    /// <summary>Additional points per meter of building height.</summary>
    public float HeightBonusMultiplier { get; set; }

    // Empty plot collectibles
    /// <summary>Fixed points awarded for empty plot collectibles.</summary>
    public int EmptyPlotPoints { get; set; }

    /// <summary>Height above ground level for empty plot collectibles.</summary>
    public float EmptyPlotFloatHeight { get; set; }

    /// <summary>Probability (0-1) that an empty plot spawns a collectible.</summary>
    public float EmptyPlotSpawnChance { get; set; }

    // Collection effect
    /// <summary>Number of lightning bolts in burst effect.</summary>
    public int ParticleCount { get; set; }

    /// <summary>Lightning bolt length in meters.</summary>
    public float ParticleSpeed { get; set; }

    /// <summary>Duration of lightning effect in seconds.</summary>
    public float ParticleDuration { get; set; }

    /// <summary>Hex color for the lightning effect (should match beam).</summary>
    public string EffectColor { get; set; } = null!;

    /// <summary>Lightning bolt thickness.</summary>
    public float EffectThickness { get; set; }

    /// <summary>Lightning bolt base transparency (0-1).</summary>
    public float EffectAlpha { get; set; }

    /// <summary>How fast bolts extend (seconds).</summary>
    public float EffectGrowDuration { get; set; }
}
