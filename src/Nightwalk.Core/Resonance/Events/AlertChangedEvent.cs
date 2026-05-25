using Nightwalk.Core.Events;

namespace Nightwalk.Core.Resonance.Events;

/// <summary>
/// Published when the alert level changes.
/// </summary>
public sealed record AlertChangedEvent(
    float PreviousValue,
    float NewValue
) : IDomainEvent
{
    public ulong Tick => 0;
}
