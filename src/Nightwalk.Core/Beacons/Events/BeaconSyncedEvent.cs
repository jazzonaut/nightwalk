using Nightwalk.Core.Events;

namespace Nightwalk.Core.Beacons.Events;

/// <summary>
/// Published when a beacon sync is successfully completed.
/// </summary>
public sealed record BeaconSyncedEvent(
    BeaconId SourceId,
    BeaconId TargetId,
    BeaconFrequency Frequency,
    float TotalDistance,
    int MirrorCount,
    int PointsAwarded
) : IDomainEvent
{
    public ulong Tick => 0;
}
