using Nightwalk.Core.Events;

namespace Nightwalk.Core.GameState.Events;

/// <summary>
/// Published when the game transitions between states.
/// </summary>
public sealed class GameStateChangedEvent : IDomainEvent
{
    /// <summary>
    /// The tick at which this event occurred (0 for UI events).
    /// </summary>
    public ulong Tick { get; }

    /// <summary>
    /// The state that was exited, or null if this is the initial state.
    /// </summary>
    public string? PreviousStateName { get; }

    /// <summary>
    /// The state that was entered.
    /// </summary>
    public string NewStateName { get; }

    public GameStateChangedEvent(string? previousStateName, string newStateName, ulong tick = 0)
    {
        PreviousStateName = previousStateName;
        NewStateName = newStateName;
        Tick = tick;
    }
}
