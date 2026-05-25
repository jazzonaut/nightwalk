using Nightwalk.Core.Events;

namespace Nightwalk.Core.Collectibles.Events;

/// <summary>
/// Published when a player collects a collectible shape.
/// </summary>
public sealed record CollectibleCollectedEvent(
    CollectibleId CollectibleId,
    int PointsAwarded,
    int TotalCollected,
    float X = 0f,
    float Y = 0f,
    float Z = 0f
) : IDomainEvent
{
    public ulong Tick => 0;
}
