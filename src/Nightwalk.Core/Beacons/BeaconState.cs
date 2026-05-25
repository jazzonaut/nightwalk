namespace Nightwalk.Core.Beacons;

/// <summary>
/// State of a beacon during gameplay.
/// </summary>
public enum BeaconState
{
    /// <summary>
    /// Beacon is inactive, waiting to be activated.
    /// </summary>
    Idle,

    /// <summary>
    /// Beacon is the source of a sync attempt, emitting signal beam.
    /// </summary>
    Source,

    /// <summary>
    /// Beacon is a potential target for the current sync (same frequency as source).
    /// </summary>
    Receiving,

    /// <summary>
    /// Beacon has been synced and is no longer available for this session.
    /// </summary>
    Completed
}
