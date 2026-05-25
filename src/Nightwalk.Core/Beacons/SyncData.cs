using System.Numerics;

namespace Nightwalk.Core.Beacons;

/// <summary>
/// Stores the state of a beacon sync (active or completed).
/// This is the source of truth for persistence across reloads.
/// </summary>
public sealed class SyncData
{
    /// <summary>
    /// The source beacon that initiated the sync.
    /// </summary>
    public BeaconId SourceBeaconId { get; init; }

    /// <summary>
    /// The target beacon that received the sync. Null if sync is still active.
    /// </summary>
    public BeaconId? TargetBeaconId { get; set; }

    /// <summary>
    /// The frequency of the sync (determines beam/prism color).
    /// </summary>
    public BeaconFrequency Frequency { get; init; }

    /// <summary>
    /// Prism positions and rotations for this sync.
    /// </summary>
    public List<PrismData> Prisms { get; } = new();

    /// <summary>
    /// The final beam path (set when completed, null while active).
    /// While active, beam path is calculated dynamically.
    /// </summary>
    public List<Vector3>? BeamPath { get; set; }

    /// <summary>
    /// Whether this sync is completed (has a target beacon).
    /// </summary>
    public bool IsCompleted => TargetBeaconId.HasValue;
}

/// <summary>
/// Position and rotation data for a prism.
/// </summary>
public readonly record struct PrismData(Vector3 Position, Vector3 Rotation);
