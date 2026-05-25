using Nightwalk.Core.Events;
using Nightwalk.Core.World.Roads;

namespace Nightwalk.Core.CityAliveness.Events;

/// <summary>
/// Published when a road segment becomes alive (starts showing car trail effects).
/// </summary>
public sealed record RoadAlivenessChangedEvent(
    RoadId RoadId,
    RoadDirection Direction,
    float Position,
    float HalfWidth
) : IDomainEvent
{
    public ulong Tick => 0;
}
