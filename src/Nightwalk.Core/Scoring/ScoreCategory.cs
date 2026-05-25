namespace Nightwalk.Core.Scoring;

/// <summary>
/// Categories for score sources.
/// </summary>
public enum ScoreCategory
{
    /// <summary>Points from climbing/exploring buildings.</summary>
    Building,

    /// <summary>Points from collecting items.</summary>
    Collectible,

    /// <summary>Points lost from hard landings.</summary>
    FallPenalty,

    /// <summary>Points drained while flying.</summary>
    FlightDrain,

    /// <summary>Points spent placing ziplines.</summary>
    ZiplinePlacement,

    /// <summary>Points earned from completing beacon syncs.</summary>
    BeaconSync,

    /// <summary>Points spent placing prisms.</summary>
    PrismPlacement,

    /// <summary>Points drained while exposed in Resonance mode.</summary>
    ExposureDrain,

    /// <summary>Points lost at max exposure in Resonance mode.</summary>
    MaxExposure,

    /// <summary>Points earned from repelling the Warden.</summary>
    WardenRepelled
}
