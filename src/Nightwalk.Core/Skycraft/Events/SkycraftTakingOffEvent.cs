using Nightwalk.Core.Events;

namespace Nightwalk.Core.Skycraft.Events;

/// <summary>
/// Fact: a Skycraft has begun lifting off from a landing POI.
/// </summary>
public sealed record SkycraftTakingOffEvent(SkycraftId Id, SkycraftProfile Profile) : IDomainEvent
{
    public ulong Tick => 0;
}
