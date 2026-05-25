using Nightwalk.Core.Events;

namespace Nightwalk.Core.Resonance.Events;

/// <summary>
/// Published when the exposure level changes.
/// </summary>
public sealed record ExposureChangedEvent(
    float PreviousValue,
    float NewValue
) : IDomainEvent
{
    public ulong Tick => 0;
}
