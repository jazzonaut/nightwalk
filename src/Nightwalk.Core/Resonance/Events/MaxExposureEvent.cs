using Nightwalk.Core.Events;

namespace Nightwalk.Core.Resonance.Events;

/// <summary>
/// Published when exposure reaches 100%.
/// Triggers score penalty and alert spike.
/// </summary>
public sealed record MaxExposureEvent() : IDomainEvent
{
    public ulong Tick => 0;
}
