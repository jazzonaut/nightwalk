using Nightwalk.Core.Events;

namespace Nightwalk.Core.Beacons.Events;

/// <summary>
/// Published when a beacon sync attempt is cancelled or deactivated.
/// </summary>
public sealed record BeaconDeactivatedEvent(
    BeaconId SourceId
) : IDomainEvent
{
    public ulong Tick => 0;
}
