using System.Numerics;
using Nightwalk.Core.Beacons.Events;
using Nightwalk.Core.Events;

namespace Nightwalk.Core.Beacons;

/// <summary>
/// Tracks beacon sync states during a gameplay session.
/// This is the source of truth for all sync data, including persistence across reloads.
/// </summary>
public sealed class BeaconTracker
{
    private readonly IEventBus _eventBus;
    private readonly List<SyncData> _syncs = new();

    /// <summary>
    /// All syncs (active and completed).
    /// </summary>
    public IReadOnlyList<SyncData> Syncs => _syncs;

    /// <summary>
    /// The currently active sync, if any (at most one).
    /// </summary>
    public SyncData? ActiveSync => _syncs.FirstOrDefault(s => !s.IsCompleted);

    /// <summary>
    /// Number of completed syncs.
    /// </summary>
    public int CompletedCount => _syncs.Count(s => s.IsCompleted);

    /// <summary>
    /// Whether a sync is currently in progress.
    /// </summary>
    public bool IsSyncActive => ActiveSync != null;

    public BeaconTracker(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Checks if a beacon has been part of a completed sync.
    /// </summary>
    public bool IsBeaconCompleted(BeaconId id)
    {
        return _syncs.Any(s => s.IsCompleted &&
            (s.SourceBeaconId == id || s.TargetBeaconId == id));
    }

    /// <summary>
    /// Starts a new sync from the given beacon.
    /// </summary>
    /// <returns>The new sync data, or null if a sync is already active or beacon is completed.</returns>
    public SyncData? StartSync(BeaconId sourceId, BeaconFrequency frequency)
    {
        if (ActiveSync != null)
            return null;

        if (IsBeaconCompleted(sourceId))
            return null;

        var sync = new SyncData
        {
            SourceBeaconId = sourceId,
            Frequency = frequency
        };
        _syncs.Add(sync);

        _eventBus.Publish(new BeaconActivatedEvent(sourceId, frequency));
        return sync;
    }

    /// <summary>
    /// Completes the active sync.
    /// </summary>
    /// <param name="targetId">The beacon that received the signal.</param>
    /// <param name="beamPath">The final beam path to store.</param>
    /// <param name="prisms">The prisms used in this sync (will be stored in sync data).</param>
    /// <param name="totalDistance">Total distance the beam traveled.</param>
    /// <param name="pointsAwarded">Points awarded for the sync.</param>
    /// <returns>True if successful, false if no active sync.</returns>
    public bool CompleteSync(BeaconId targetId, List<Vector3> beamPath, IEnumerable<PrismData> prisms, float totalDistance, int pointsAwarded)
    {
        var sync = ActiveSync;
        if (sync == null)
            return false;

        sync.TargetBeaconId = targetId;
        sync.BeamPath = beamPath;
        sync.Prisms.Clear();
        sync.Prisms.AddRange(prisms);

        _eventBus.Publish(new BeaconSyncedEvent(
            sync.SourceBeaconId,
            targetId,
            sync.Frequency,
            totalDistance,
            sync.Prisms.Count,
            pointsAwarded
        ));

        return true;
    }

    /// <summary>
    /// Cancels the active sync and removes it.
    /// </summary>
    public void CancelSync()
    {
        var sync = ActiveSync;
        if (sync == null)
            return;

        var sourceId = sync.SourceBeaconId;
        _syncs.Remove(sync);

        _eventBus.Publish(new BeaconDeactivatedEvent(sourceId));
    }

    /// <summary>
    /// Resets all tracking (for session reset).
    /// </summary>
    public void Reset()
    {
        _syncs.Clear();
    }
}
