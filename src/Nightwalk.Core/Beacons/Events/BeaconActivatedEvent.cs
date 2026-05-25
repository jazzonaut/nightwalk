using Nightwalk.Core.Events;

namespace Nightwalk.Core.Beacons.Events;

/// <summary>
/// Published when a beacon is activated as the source for a sync attempt.
/// </summary>
public sealed record BeaconActivatedEvent(
    BeaconId SourceId,
    BeaconFrequency Frequency
) : IDomainEvent
{
    public ulong Tick => 0;
}
