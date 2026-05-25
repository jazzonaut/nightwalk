namespace Nightwalk.Core.Config;

/// <summary>
/// Configuration for the city aliveness system (car trails on roads, window silhouettes).
/// </summary>
public sealed class AlivenessConfig
{
    /// <summary>
    /// Car trail rendering settings.
    /// </summary>
    public CarTrailsSettings CarTrails { get; set; } = null!;

    /// <summary>
    /// Window silhouette settings for climbed buildings.
    /// </summary>
    public SilhouetteSettings Silhouettes { get; set; } = null!;
}

/// <summary>
/// Settings for animated car trail effects on alive roads.
/// </summary>
public sealed class CarTrailsSettings
{
    /// <summary>
    /// Whether car trails are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Margin beyond building plot edges to detect adjacent roads.
    /// </summary>
    public float AdjacencyMargin { get; set; }

    /// <summary>
    /// Number of traffic lanes per direction.
    /// </summary>
    public int LanesPerDirection { get; set; }

    /// <summary>
    /// Base scrolling speed for car streaks (units per second).
    /// </summary>
    public float ScrollSpeed { get; set; }

    /// <summary>
    /// Random variation in scroll speed between lanes.
    /// </summary>
    public float ScrollSpeedVariation { get; set; }

    /// <summary>
    /// Color for headlights (cars going one direction).
    /// </summary>
    public string StreakColor { get; set; } = null!;

    /// <summary>
    /// Color for taillights (cars going opposite direction).
    /// </summary>
    public string StreakColorOncoming { get; set; } = null!;

    /// <summary>
    /// Emission intensity multiplier for glow effect.
    /// </summary>
    public float EmissionStrength { get; set; }

    /// <summary>
    /// Width of the trail (smaller = thinner).
    /// </summary>
    public float TrailWidth { get; set; }

    /// <summary>
    /// Softness of trail edges (0 = sharp, 1 = very fuzzy).
    /// </summary>
    public float Fuzziness { get; set; }

    /// <summary>
    /// Height offset above road surface for the overlay mesh.
    /// </summary>
    public float HeightOffset { get; set; }

    /// <summary>
    /// Maximum distance at which car trails are visible.
    /// </summary>
    public float VisibilityRange { get; set; }

    /// <summary>
    /// Fade margin for visibility range LOD.
    /// </summary>
    public float VisibilityMargin { get; set; }

    // === Enhanced Visual Effects ===

    /// <summary>
    /// Motion blur strength for elongated streaks (0 = off, higher = longer streaks).
    /// </summary>
    public float MotionBlurStrength { get; set; }

    /// <summary>
    /// Bloom boost multiplier for post-process glow (0 = default, higher = more bloom).
    /// </summary>
    public float BloomBoost { get; set; }
}

/// <summary>
/// Settings for window silhouettes on climbed buildings.
/// </summary>
public sealed class SilhouetteSettings
{
    /// <summary>
    /// Whether silhouettes are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Fraction of lit windows that get silhouettes (0-1).
    /// </summary>
    public float Density { get; set; }

    /// <summary>
    /// Blur radius for soft silhouette edges.
    /// </summary>
    public float BlurRadius { get; set; }

    /// <summary>
    /// Opacity of silhouette overlay (0-1).
    /// </summary>
    public float Opacity { get; set; }

    /// <summary>
    /// Distance at which silhouettes fade out.
    /// </summary>
    public float VisibilityRange { get; set; }

    /// <summary>
    /// Fade gradient distance.
    /// </summary>
    public float VisibilityMargin { get; set; }
}
