using Nightwalk.Core.Events;

namespace Nightwalk.Core.Resonance.Events;

/// <summary>
/// Published when the Warden is successfully repelled.
/// </summary>
public sealed record WardenRepelledEvent() : IDomainEvent
{
    public ulong Tick => 0;
}
