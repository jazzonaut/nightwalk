using Nightwalk.Core.Events;

namespace Nightwalk.Core.GameState.Events;

/// <summary>
/// Published when the player's view mode changes.
/// </summary>
public sealed record ViewModeChangedEvent(ViewMode Mode) : IDomainEvent
{
    public ulong Tick => 0;
}
