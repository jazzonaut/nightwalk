using Nightwalk.Core.Events;

namespace Nightwalk.Core.Skycraft.Events;

/// <summary>
/// Fact: a Skycraft is leaving the active region and its pool slot will be freed.
/// Consumed by the renderer to release nodes/MultiMesh slots and by hitching to dismount riders.
/// </summary>
public sealed record SkycraftDespawningEvent(SkycraftId Id, SkycraftProfile Profile) : IDomainEvent
{
    public ulong Tick => 0;
}
