using Nightwalk.Core.Events;

namespace Nightwalk.Core.Skycraft.Events;

/// <summary>
/// Fact: a Skycraft has settled on a landing POI. Consumed by hitching, telemetry, audio.
/// </summary>
public sealed record SkycraftLandedEvent(SkycraftId Id, SkycraftProfile Profile, SkycraftPoiKind PoiKind, float X, float Y, float Z) : IDomainEvent
{
    public ulong Tick => 0;
}
