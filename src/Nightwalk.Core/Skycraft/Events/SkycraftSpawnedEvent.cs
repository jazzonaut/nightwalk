using Nightwalk.Core.Events;

namespace Nightwalk.Core.Skycraft.Events;

/// <summary>
/// Fact: a Skycraft entered the active region. Consumed by the renderer to attach a node/instance.
/// </summary>
public sealed record SkycraftSpawnedEvent(SkycraftId Id, SkycraftProfile Profile) : IDomainEvent
{
    public ulong Tick => 0;
}
