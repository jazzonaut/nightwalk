namespace Nightwalk.Core.Resonance.Seeker;

/// <summary>
/// States a Seeker can be in during Resonance mode.
/// </summary>
public enum SeekerState
{
    /// <summary>Seeker is in pool, not active.</summary>
    Pooled,

    /// <summary>Seeker is spawning near Warden.</summary>
    Dispatching,

    /// <summary>Seeker is traveling to a ping location.</summary>
    Traveling,

    /// <summary>Seeker is making final approach, searchlight activates.</summary>
    Approaching,

    /// <summary>Seeker is scanning an area.</summary>
    Scanning,

    /// <summary>Seeker is searching after losing the player.</summary>
    Hunting,

    /// <summary>Seeker is returning to Warden vicinity.</summary>
    Returning,

    /// <summary>Seeker has been recalled (Warden departing).</summary>
    Recalled
}

/// <summary>
/// Aim mode for the Seeker's searchlight.
/// </summary>
public enum SeekerAimMode
{
    /// <summary>Light disabled (traveling).</summary>
    Off,

    /// <summary>Quick lerp to ping/target.</summary>
    SnapToTarget,

    /// <summary>Brief hold on target.</summary>
    Hold,

    /// <summary>Smooth area sweep.</summary>
    Sweep,

    /// <summary>Lock onto last known player position.</summary>
    TrackPlayer
}
