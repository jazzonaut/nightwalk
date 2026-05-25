using Nightwalk.Core.Events;

namespace Nightwalk.Core.GameState.Events;

/// <summary>
/// Fired when a session reset is requested (e.g., returning to main menu).
/// Listeners should clean up session-specific state like scores, player position, etc.
/// </summary>
public sealed record SessionResetRequestedEvent : IDomainEvent
{
    /// <summary>The tick at which this event occurred (0 for non-simulation events).</summary>
    public ulong Tick => 0;
}
