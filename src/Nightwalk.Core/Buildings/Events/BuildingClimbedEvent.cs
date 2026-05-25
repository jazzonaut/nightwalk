using Nightwalk.Core.Events;

namespace Nightwalk.Core.Buildings.Events;

/// <summary>
/// Published when a player first reaches a building's rooftop.
/// </summary>
public sealed record BuildingClimbedEvent(
    BuildingId BuildingId,
    float BuildingHeight,
    int TotalBuildingsClimbed
) : IDomainEvent
{
    public ulong Tick => 0;
}
