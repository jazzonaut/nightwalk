namespace Nightwalk.Core.GameMode;

/// <summary>
/// Interface for game modes that modify gameplay rules and mechanics.
/// Modes are independent of game state (playing/paused/menu) - they define
/// what rules are active when the game is running.
/// </summary>
public interface IGameMode
{
    /// <summary>
    /// Unique identifier for this mode.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name shown in UI.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Brief description of the mode.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Called when this mode becomes active.
    /// </summary>
    void Enter();

    /// <summary>
    /// Called when this mode is deactivated.
    /// </summary>
    void Exit();

    /// <summary>
    /// Called each physics tick while mode is active and game is running.
    /// </summary>
    /// <param name="delta">Time since last tick in seconds.</param>
    void Tick(double delta);

    /// <summary>
    /// Called each frame for rendering/interpolation updates.
    /// </summary>
    /// <param name="delta">Time since last frame in seconds.</param>
    void Process(double delta);

    /// <summary>
    /// Resets mode state for a new session.
    /// </summary>
    void Reset();
}
