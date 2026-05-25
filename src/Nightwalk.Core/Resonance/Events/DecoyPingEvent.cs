using Nightwalk.Core.Events;

namespace Nightwalk.Core.Resonance.Events;

/// <summary>
/// Published when the player creates a decoy ping with the spotlight.
/// Seekers prefer these over noise pings.
/// </summary>
public sealed record DecoyPingEvent(
    float X,
    float Y,
    float Z
) : IDomainEvent
{
    public ulong Tick => 0;
}
