namespace Nightwalk.Core.Config;

/// <summary>
/// Gameplay configuration.
/// </summary>
public sealed class GameplayConfig
{
    public EnergyConfig Energy { get; set; } = null!;
    public BuildingsConfig Buildings { get; set; } = null!;
    public ScoringConfig Scoring { get; set; } = null!;
    public FallDamageConfig FallDamage { get; set; } = null!;
    public ScoreEffectsConfig ScoreEffects { get; set; } = null!;
    public CollectiblesConfig Collectibles { get; set; } = null!;
    public ViewModesConfig ViewModes { get; set; } = null!;
    public FlybyCameraConfig FlybyCamera { get; set; } = null!;
    public ScreensaverConfig Screensaver { get; set; } = null!;
}

public sealed class EnergyConfig
{
    public float MaxEnergy { get; set; }
    public float RegenRate { get; set; }
}

public sealed class BuildingsConfig
{
    /// <summary>How close to rooftop height player must be to count as on rooftop.</summary>
    public float RooftopHeightTolerance { get; set; }

    /// <summary>Minimum height to consider for rooftop detection (optimization).</summary>
    public float MinRooftopHeight { get; set; }

    /// <summary>Whether to show window overlay on climbed buildings.</summary>
    public bool HighlightWindows { get; set; }

    /// <summary>Hex color for window highlights.</summary>
    public string WindowHighlightColor { get; set; } = null!;

    /// <summary>Emission intensity for window highlights.</summary>
    public float WindowHighlightIntensity { get; set; }

    /// <summary>Whether to show sky beam from climbed buildings.</summary>
    public bool HighlightBeam { get; set; }

    /// <summary>Hex color for sky beam.</summary>
    public string BeamColor { get; set; } = null!;

    /// <summary>Intensity for sky beam.</summary>
    public float BeamIntensity { get; set; }

    /// <summary>Width of the sky beam in meters.</summary>
    public float BeamWidth { get; set; }

    /// <summary>Height of the sky beam in meters.</summary>
    public float BeamHeight { get; set; }

    /// <summary>Height offset above roof where beam starts (to avoid roof obstruction).</summary>
    public float BeamStartOffset { get; set; }

    /// <summary>Whether to show rising particles from climbed buildings.</summary>
    public bool HighlightParticles { get; set; }

    /// <summary>Height above roof where particles spawn.</summary>
    public float ParticleStartOffset { get; set; }

    /// <summary>Spawn area radius for particles.</summary>
    public float ParticleSpawnRadius { get; set; }

    /// <summary>Upward velocity for particles.</summary>
    public float ParticleRiseSpeed { get; set; }

    /// <summary>Particle lifetime in seconds.</summary>
    public float ParticleLifetime { get; set; }

    /// <summary>Number of active particles.</summary>
    public int ParticleCount { get; set; }

    /// <summary>Base particle size.</summary>
    public float ParticleSize { get; set; }

    /// <summary>Hex color for particles (uses BeamColor if null/empty).</summary>
    public string? ParticleColor { get; set; }

    /// <summary>Distance at which particles fade out (0 = no LOD).</summary>
    public float ParticleVisibilityRange { get; set; }

    /// <summary>Fade gradient length for particle LOD.</summary>
    public float ParticleVisibilityMargin { get; set; }
}

public sealed class ScoringConfig
{
    /// <summary>Maximum combo multiplier.</summary>
    public float ComboMaxMultiplier { get; set; }

    /// <summary>Multiplier increase per combo action.</summary>
    public float ComboIncrement { get; set; }

    /// <summary>How fast combo decays per second.</summary>
    public float ComboDecayRate { get; set; }

    /// <summary>Seconds of inactivity before combo starts decaying.</summary>
    public float ComboDecayDelay { get; set; }
}

public sealed class FallDamageConfig
{
    /// <summary>Minimum downward velocity to trigger fall penalty.</summary>
    public float VelocityThreshold { get; set; }

    /// <summary>Points removed per unit of velocity above threshold.</summary>
    public float PointsPerVelocity { get; set; }

    /// <summary>Maximum penalty from a single fall.</summary>
    public int MaxPenalty { get; set; }

    /// <summary>Minimum penalty once threshold is exceeded.</summary>
    public int MinPenalty { get; set; }
}

/// <summary>
/// Visual effects configuration for score changes.
/// </summary>
public sealed class ScoreEffectsConfig
{
    /// <summary>Duration of the effect in seconds.</summary>
    public float EffectDuration { get; set; }

    // Gain effect (score increase) - neon vibes
    /// <summary>Peak saturation during gain effect.</summary>
    public float GainSaturationPeak { get; set; }

    /// <summary>Peak bloom intensity during gain effect.</summary>
    public float GainBloomPeak { get; set; }

    /// <summary>Peak contrast during gain effect (lower = brighter).</summary>
    public float GainContrastPeak { get; set; }

    /// <summary>Highlight tint color during gain effect (hex).</summary>
    public string GainHighlightTint { get; set; } = null!;

    // Loss effect (score decrease) - danger/damage
    /// <summary>Peak saturation during loss effect.</summary>
    public float LossSaturationPeak { get; set; }

    /// <summary>Peak chromatic aberration during loss effect.</summary>
    public float LossChromaticPeak { get; set; }

    /// <summary>Peak contrast during loss effect.</summary>
    public float LossContrastPeak { get; set; }

    /// <summary>Color grading strength during loss effect.</summary>
    public float LossColorGradingStrength { get; set; }

    /// <summary>Shadow tint color during loss effect (hex).</summary>
    public string LossShadowTint { get; set; } = null!;
}

/// <summary>
/// Configuration for the flyby camera system.
/// </summary>
public sealed class FlybyCameraConfig
{
    /// <summary>Base speed in meters per second.</summary>
    public float BaseSpeed { get; set; }

    /// <summary>Minimum speed on sharp turns in m/s.</summary>
    public float MinSpeedOnTurns { get; set; }

    /// <summary>Maximum camera roll angle in degrees.</summary>
    public float MaxRollDegrees { get; set; }

    /// <summary>Roll smoothing factor (higher = snappier).</summary>
    public float RollSmoothing { get; set; }

    /// <summary>How far ahead to look on the path (0-1 parameter).</summary>
    public float LookAheadT { get; set; }

    /// <summary>Base field of view in degrees.</summary>
    public float BaseFov { get; set; }

    /// <summary>Maximum FOV increase at high speed.</summary>
    public float MaxFovIncrease { get; set; }

    /// <summary>Duration of enter transition in seconds.</summary>
    public float EnterTransitionDuration { get; set; }

    /// <summary>Duration of exit transition in seconds.</summary>
    public float ExitTransitionDuration { get; set; }

    /// <summary>Vertical offset above beam path in meters.</summary>
    public float VerticalOffset { get; set; }

    /// <summary>Multiplier for post-processing effects during flyby.</summary>
    public float PostProcessMultiplier { get; set; }

    /// <summary>Curvature threshold for speed reduction (radians/meter).</summary>
    public float CurvatureThreshold { get; set; }

    /// <summary>Minimum waypoints required to trigger flyby</summary>
    public int MinWaypoints { get; set; }
}

/// <summary>
/// Configuration for player view modes.
/// </summary>
public sealed class ViewModesConfig
{
    /// <summary>Bird's eye camera height above the player (meters).</summary>
    public float BirdsEyeHeight { get; set; }

    /// <summary>Minimum bird's eye camera height above the player (meters).</summary>
    public float BirdsEyeMinHeight { get; set; }

    /// <summary>Maximum bird's eye camera height above the player (meters).</summary>
    public float BirdsEyeMaxHeight { get; set; }

    /// <summary>Mouse wheel zoom step in meters.</summary>
    public float BirdsEyeZoomStep { get; set; }

    /// <summary>Zoom speed in meters per second.</summary>
    public float BirdsEyeZoomSpeed { get; set; }

    /// <summary>Default pitch angle in degrees for bird's eye view.</summary>
    public float BirdsEyePitchDegrees { get; set; }

    /// <summary>Minimum pitch angle in degrees for bird's eye view.</summary>
    public float BirdsEyePitchMinDegrees { get; set; }

    /// <summary>Maximum pitch angle in degrees for bird's eye view.</summary>
    public float BirdsEyePitchMaxDegrees { get; set; }

    /// <summary>Mouse sensitivity multiplier for bird's eye look.</summary>
    public float BirdsEyeLookSensitivity { get; set; }

    /// <summary>Transition duration for entering/exiting bird's eye view.</summary>
    public float BirdsEyeTransitionDuration { get; set; }
}
