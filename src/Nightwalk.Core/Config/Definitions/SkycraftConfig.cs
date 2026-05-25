using Nightwalk.Core.Config.Attributes;

namespace Nightwalk.Core.Config;

/// <summary>
/// Configuration for the Skycraft ambient air-traffic system. See ADR-0016.
/// </summary>
public sealed class SkycraftConfig
{
    /// <summary>
    /// Player-facing density (0-100). Scales the active pool size linearly.
    /// </summary>
    [UserConfigurable(SettingsCategory.Gameplay, SettingsControlType.Slider, Order = 200)]
    [SettingsRange(0, 100, Step = 5)]
    [SettingsDisplay(DisplayName = "Skycraft Density",
                     Description = "Ambient flying-vehicle traffic. 0 disables the system.",
                     ValueFormat = "{0}%")]
    public int Density { get; set; } = 100;

    /// <summary>Pool sizing and spawn behavior.</summary>
    public PoolSettings Pool { get; set; } = null!;

    /// <summary>Per-profile sub-budget ratios. Must sum to <= 1.</summary>
    public ProfileMixSettings Mix { get; set; } = null!;

    /// <summary>Express profile (high cruise).</summary>
    public ProfileSettings Express { get; set; } = null!;

    /// <summary>Commuter profile (mid cruise + rooftop landings).</summary>
    public ProfileSettings Commuter { get; set; } = null!;

    /// <summary>Drifter profile (low band, road corridors).</summary>
    public ProfileSettings Drifter { get; set; } = null!;

    /// <summary>Landing approach + dwell behavior.</summary>
    public LandingSettings Landing { get; set; } = null!;

    /// <summary>Render tier swap distance + hysteresis.</summary>
    public RenderSettings Render { get; set; } = null!;

    /// <summary>POI generation rules.</summary>
    public PoiSettings Poi { get; set; } = null!;

    /// <summary>Road-grid turn tuning (Drifter + Commuter).</summary>
    public TurnSettings Turn { get; set; } = null!;
}

/// <summary>
/// Pool sizing. Effective pool = round(MaxPoolSize * Density / 100). Defaults mirror
/// <c>data/skycraft.toml</c> so a missing config file degrades to the tuned values
/// rather than a third-density fallback.
/// </summary>
public sealed class PoolSettings
{
    /// <summary>Upper bound at Density=100. Matches <c>data/skycraft.toml</c>.</summary>
    public int MaxPoolSize { get; set; } = 120;

    /// <summary>Spawn radius from player (far edge of active region).</summary>
    public float SpawnRadius { get; set; } = 500f;

    /// <summary>Despawn radius (must exceed SpawnRadius for hysteresis).</summary>
    public float DespawnRadius { get; set; } = 700f;

    /// <summary>Seconds between spawn-fill checks per profile.</summary>
    public float SpawnCheckInterval { get; set; } = 0.15f;
}

/// <summary>
/// Per-profile share of the total pool. ADR example: 0.3 / 0.5 / 0.2.
/// </summary>
public sealed class ProfileMixSettings
{
    public float Express { get; set; } = 0.3f;
    public float Commuter { get; set; } = 0.5f;
    public float Drifter { get; set; } = 0.2f;
}

/// <summary>
/// Movement + visual identity for a single profile.
/// </summary>
public sealed class ProfileSettings
{
    /// <summary>Lower bound of altitude band (meters).</summary>
    public float AltitudeMin { get; set; }

    /// <summary>Upper bound of altitude band (meters).</summary>
    public float AltitudeMax { get; set; }

    /// <summary>Cruise speed (m/s).</summary>
    public float Speed { get; set; }

    /// <summary>Acceleration (m/s^2).</summary>
    public float Acceleration { get; set; } = 6f;

    /// <summary>Probability of converting a POI arrival into a landing (0-1). Express should be 0.</summary>
    public float LandingChance { get; set; }

    /// <summary>Visual identity: accent emissive color (hex).</summary>
    public string AccentColor { get; set; } = "#80E0FF";

    /// <summary>Base scale; per-instance jitter applied multiplicatively.</summary>
    public float BaseScale { get; set; } = 1f;
}

/// <summary>
/// Landing approach state machine timing.
/// </summary>
public sealed class LandingSettings
{
    /// <summary>Horizontal deceleration distance from POI.</summary>
    public float ApproachRadius { get; set; } = 30f;

    /// <summary>Final settle altitude above target (meters). Small — craft is a hovering
    /// vehicle, mesh origin sits ~0.3m above its underside, so 1.5m leaves a ~1m visual gap.</summary>
    public float SettleAltitude { get; set; } = 1.5f;

    /// <summary>Vertical descent speed during approach (m/s).</summary>
    public float DescentSpeed { get; set; } = 6f;

    /// <summary>Vertical takeoff speed (m/s). Independent of descent so landings can be
    /// tuned gentler without slowing climbouts.</summary>
    public float TakeoffSpeed { get; set; } = 6f;

    /// <summary>Vertical takeoff clearance above target (meters).</summary>
    public float TakeoffClearance { get; set; } = 30f;

    /// <summary>Commuter dwell minimum (seconds).</summary>
    public float CommuterDwellMin { get; set; } = 4f;

    /// <summary>Commuter dwell maximum (seconds).</summary>
    public float CommuterDwellMax { get; set; } = 10f;

    /// <summary>Drifter dwell minimum (seconds).</summary>
    public float DrifterDwellMin { get; set; } = 10f;

    /// <summary>Drifter dwell maximum (seconds).</summary>
    public float DrifterDwellMax { get; set; } = 30f;
}

/// <summary>
/// Hybrid renderer tier-swap.
/// </summary>
public sealed class RenderSettings
{
    /// <summary>Distance below which a Skycraft uses the near-tier full Node3D path.</summary>
    public float NearTierDistance { get; set; } = 150f;

    /// <summary>Hysteresis margin added on the way out (prevents flicker).</summary>
    public float NearTierHysteresis { get; set; } = 20f;

    /// <summary>Spotlight (headlight) range in meters.</summary>
    public float HeadlightRange { get; set; } = 60f;

    /// <summary>Spotlight cone half-angle in degrees.</summary>
    public float HeadlightConeDegrees { get; set; } = 25f;

    /// <summary>Spotlight energy at cruise.</summary>
    public float HeadlightEnergy { get; set; } = 4f;

    /// <summary>Spotlight energy while dwelling (engines dim).</summary>
    public float HeadlightDwellEnergy { get; set; } = 0.2f;

    /// <summary>Headlight color (hex, cool white-blue).</summary>
    public string HeadlightColor { get; set; } = "#CDE7FF";

    /// <summary>Backlight (omni) range.</summary>
    public float BacklightRange { get; set; } = 4f;

    /// <summary>Backlight energy at cruise.</summary>
    public float BacklightEnergy { get; set; } = 1.5f;

    /// <summary>Backlight energy while dwelling (pulses brighter as engines dim).</summary>
    public float BacklightDwellEnergy { get; set; } = 2.2f;

    /// <summary>Backlight color (warm red/amber).</summary>
    public string BacklightColor { get; set; } = "#FF7A30";

    /// <summary>Per-instance hue jitter range (degrees, applied to accent emissive).</summary>
    public float AccentHueJitter { get; set; } = 15f;

    /// <summary>Per-instance scale jitter (fraction; 0.05 = +/-5%).</summary>
    public float ScaleJitter { get; set; } = 0.05f;
}

/// <summary>
/// Road-grid turn geometry shared by Drifter and Commuter. These are tuned to the road
/// scale (lane width, intersection size) rather than per-profile behaviour, so they live
/// outside ProfileSettings.
/// </summary>
public sealed class TurnSettings
{
    /// <summary>Turn arc radius (meters). Matches a banked road turn at this scale.</summary>
    public float Radius { get; set; } = 9f;

    /// <summary>Runway before the curve where speed bleeds from cruise to curve speed.</summary>
    public float DecelDistance { get; set; } = 28f;

    /// <summary>Fraction of cruise speed held through the arc.</summary>
    public float CurveSpeedFactor { get; set; } = 0.45f;

    /// <summary>Right-hand-traffic lane offset magnitude (meters from road centerline).</summary>
    public float LaneOffsetMagnitude { get; set; } = 2.0f;
}

/// <summary>
/// POI marking rules. Applied per chunk during landing-POI derivation.
/// </summary>
public sealed class PoiSettings
{
    /// <summary>Minimum rooftop area (m^2) to be eligible as a Commuter landing pad.</summary>
    public float MinRooftopArea { get; set; } = 60f;

    /// <summary>Fraction of eligible rooftops that get marked (0-1).</summary>
    public float RooftopMarkChance { get; set; } = 0.35f;

    /// <summary>Fraction of road segments marked as Drifter landing zones.</summary>
    public float RoadMarkChance { get; set; } = 0.25f;

    /// <summary>Sky waypoints emitted per chunk (Express altitude band).</summary>
    public int ExpressWaypointsPerChunk { get; set; } = 2;

    /// <summary>Sky waypoints emitted per chunk (Commuter altitude band).</summary>
    public int CommuterWaypointsPerChunk { get; set; } = 2;
}
