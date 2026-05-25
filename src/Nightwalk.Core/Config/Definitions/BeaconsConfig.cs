namespace Nightwalk.Core.Config;

/// <summary>
/// Configuration for the beacon synchronization system.
/// </summary>
public sealed class BeaconsConfig
{
    /// <summary>
    /// Beacon visual and generation settings.
    /// </summary>
    public BeaconSettings Beacon { get; set; } = null!;

    /// <summary>
    /// Prism appearance and behavior settings.
    /// </summary>
    public PrismSettings Prism { get; set; } = null!;

    /// <summary>
    /// Reward calculations for completing syncs.
    /// </summary>
    public RewardSettings Rewards { get; set; } = null!;

    /// <summary>
    /// Player interaction distances.
    /// </summary>
    public InteractionSettings Interaction { get; set; } = null!;
}

/// <summary>
/// Beacon visual and generation settings.
/// </summary>
public sealed class BeaconSettings
{
    /// <summary>
    /// Radius of hexagonal pyramid base.
    /// </summary>
    public float BaseRadius { get; set; }

    /// <summary>
    /// Height of beacon pyramid.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    /// Transparency/opacity of the glass body (0-1).
    /// </summary>
    public float Transparency { get; set; }

    /// <summary>
    /// Tint color for the glass body (hex string).
    /// </summary>
    public string TintColor { get; set; } = null!;

    /// <summary>
    /// Color of the glowing edges (hex string).
    /// </summary>
    public string EdgeColor { get; set; } = null!;

    /// <summary>
    /// Emission intensity for the edge glow.
    /// </summary>
    public float EdgeGlowIntensity { get; set; }

    /// <summary>
    /// Thickness of the glowing edge lines.
    /// </summary>
    public float EdgeThickness { get; set; }

    /// <summary>
    /// Light intensity when idle.
    /// </summary>
    public float GlowIntensity { get; set; }

    /// <summary>
    /// Light intensity when active (source or receiving).
    /// </summary>
    public float ActiveGlowIntensity { get; set; }

    /// <summary>
    /// Chance for beacon to spawn on eligible building (0-1).
    /// </summary>
    public float SpawnChance { get; set; }

    /// <summary>
    /// Minimum building height for beacon spawning.
    /// </summary>
    public float MinBuildingHeight { get; set; }

    /// <summary>
    /// Grid cell size for spatially spreading beacon frequencies.
    /// Beacons in different grid cells will tend toward different frequencies.
    /// </summary>
    public float FrequencyGridSize { get; set; }

    /// <summary>
    /// Minimum distance between beacons of the same frequency.
    /// Beacons closer than this will have their frequency reassigned.
    /// </summary>
    public float MinSameFrequencyDistance { get; set; }

    /// <summary>
    /// Frequency colors as hex strings.
    /// </summary>
    public string[] Frequencies { get; set; } = null!;

    /// <summary>
    /// Width of vertical marker beams.
    /// </summary>
    public float MarkerBeamWidth { get; set; }

    /// <summary>
    /// Height of vertical marker beams.
    /// </summary>
    public float MarkerBeamHeight { get; set; }

    /// <summary>
    /// Intensity of marker beams.
    /// </summary>
    public float MarkerBeamIntensity { get; set; }

    /// <summary>
    /// Width of signal routing beams.
    /// </summary>
    public float SignalBeamWidth { get; set; }

    /// <summary>
    /// Maximum length of signal beams before termination.
    /// </summary>
    public float SignalBeamMaxLength { get; set; }

    /// <summary>
    /// Opacity for beam endpoint when it reaches max length without hitting anything (0-1).
    /// </summary>
    public float BrokenBeamOpacity { get; set; }
}

/// <summary>
/// Prism appearance and behavior settings.
/// </summary>
public sealed class PrismSettings
{
    /// <summary>
    /// Hexagon radius of the prism.
    /// </summary>
    public float Radius { get; set; }

    /// <summary>
    /// Length of the prism along the beam axis.
    /// </summary>
    public float Length { get; set; }

    /// <summary>
    /// Transparency/opacity of the prism body (0-1).
    /// </summary>
    public float Transparency { get; set; }

    /// <summary>
    /// Tint color for the glass body (hex string, e.g. "#FFFFFF" for clear).
    /// </summary>
    public string TintColor { get; set; } = null!;

    /// <summary>
    /// Color of the glowing edges (hex string).
    /// </summary>
    public string EdgeColor { get; set; } = null!;

    /// <summary>
    /// Emission intensity for the edge glow.
    /// </summary>
    public float EdgeGlowIntensity { get; set; }

    /// <summary>
    /// Thickness of the glowing edge lines.
    /// </summary>
    public float EdgeThickness { get; set; }

    /// <summary>
    /// Height above beacon where prism spawns.
    /// </summary>
    public float SpawnHeight { get; set; }

    /// <summary>
    /// Maximum prisms allowed per sync attempt.
    /// </summary>
    public int MaxPrisms { get; set; }

    /// <summary>
    /// Score cost to place a prism.
    /// </summary>
    public int PlacementCost { get; set; }

    /// <summary>
    /// How fast prism rotates with mouse movement.
    /// </summary>
    public float RotationSensitivity { get; set; }

    /// <summary>
    /// Maximum raycast distance for prism interaction.
    /// </summary>
    public float MaxPlacementDistance { get; set; }

    /// <summary>
    /// Preview color for valid placement.
    /// </summary>
    public string PreviewColor { get; set; } = null!;

    /// <summary>
    /// Preview color for invalid placement.
    /// </summary>
    public string InvalidColor { get; set; } = null!;

    /// <summary>
    /// Distance behind prism for POV camera positioning.
    /// </summary>
    public float PovCameraOffset { get; set; }

    /// <summary>
    /// Vertical offset for POV camera to make beam visible.
    /// </summary>
    public float PovCameraVerticalOffset { get; set; }

    /// <summary>
    /// Prism opacity during POV mode (0-1).
    /// </summary>
    public float PovPrismOpacity { get; set; }

    /// <summary>
    /// Duration of smooth camera transition when entering/exiting POV mode.
    /// </summary>
    public float PovTransitionDuration { get; set; }

    /// <summary>
    /// Whether zoom is enabled in POV mode.
    /// </summary>
    public bool PovZoomEnabled { get; set; }

    /// <summary>
    /// Minimum FOV when zoomed in during POV mode.
    /// </summary>
    public float PovMinFov { get; set; }

    /// <summary>
    /// Maximum FOV when zoomed out during POV mode.
    /// </summary>
    public float PovMaxFov { get; set; }

    /// <summary>
    /// FOV change per mouse wheel step during POV mode.
    /// </summary>
    public float PovZoomStep { get; set; }

    /// <summary>
    /// Distance threshold for beam to snap to a prism.
    /// Beam will connect to prism if it passes within this distance.
    /// </summary>
    public float BeamSnapThreshold { get; set; }

    /// <summary>
    /// Distance threshold for placement preview to snap to nearby beam.
    /// When placing a new prism, the preview will snap to the beam line if within this distance.
    /// </summary>
    public float PlacementSnapThreshold { get; set; }

    /// <summary>
    /// Offset from wall surfaces when placing prisms.
    /// Applied when placing on vertical surfaces to prevent clipping.
    /// </summary>
    public float WallOffset { get; set; }

    /// <summary>
    /// Default color for the exit light (hex string, warm pale yellow).
    /// </summary>
    public string ExitGlowColor { get; set; } = null!;

    /// <summary>
    /// Energy/brightness of the exit light.
    /// </summary>
    public float ExitLightEnergy { get; set; }

    /// <summary>
    /// Range of the exit light in meters.
    /// </summary>
    public float ExitLightRange { get; set; }
}

/// <summary>
/// Reward calculations for completing syncs.
/// </summary>
public sealed class RewardSettings
{
    /// <summary>
    /// Base points for completing a sync.
    /// </summary>
    public int BasePoints { get; set; }

    /// <summary>
    /// Points per meter of beam travel distance.
    /// </summary>
    public float DistanceBonus { get; set; }

    /// <summary>
    /// Bonus points per prism below max (fewer prisms = more bonus).
    /// </summary>
    public int PrismBonus { get; set; }
}

/// <summary>
/// Player interaction distances.
/// </summary>
public sealed class InteractionSettings
{
    /// <summary>
    /// Maximum distance to activate a beacon.
    /// </summary>
    public float ActivationDistance { get; set; }

    /// <summary>
    /// Distance at which beacons spawn (become visible).
    /// </summary>
    public float SpawnDistance { get; set; }

    /// <summary>
    /// Distance at which beacons despawn (are removed).
    /// </summary>
    public float DespawnDistance { get; set; }

    /// <summary>
    /// Extended spawn distance for beacons matching the active sync frequency.
    /// </summary>
    public float ActiveFrequencySpawnDistance { get; set; }

    /// <summary>
    /// Extended despawn distance for beacons matching the active sync frequency.
    /// </summary>
    public float ActiveFrequencyDespawnDistance { get; set; }
}
