namespace Nightwalk.Core.Events;

/// <summary>
/// Base interface for domain events.
/// Events are facts - notifications that something already happened.
/// </summary>
public interface IDomainEvent
{
    /// <summary>The tick at which this event occurred.</summary>
    ulong Tick { get; }
}
