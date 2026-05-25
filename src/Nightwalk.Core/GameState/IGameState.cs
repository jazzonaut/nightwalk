namespace Nightwalk.Core.GameState;

/// <summary>
/// Interface for game states in the state machine.
/// Each state controls what systems are active and how input is processed.
/// </summary>
public interface IGameState
{
    /// <summary>
    /// Display name for the state (for debugging/UI).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether the game world simulation should tick in this state.
    /// False for menus, pause screens, etc.
    /// </summary>
    bool AllowsGameTick { get; }

    /// <summary>
    /// Whether the player can control their character in this state.
    /// False for menus, cutscenes, etc.
    /// </summary>
    bool AllowsPlayerInput { get; }

    /// <summary>
    /// Whether HUD elements (score, tools, etc.) should be visible.
    /// </summary>
    bool ShowsHUD { get; }

    /// <summary>
    /// Whether the mouse cursor should be visible and unlocked.
    /// True for menus, false for gameplay.
    /// </summary>
    bool ShowsCursor { get; }

    /// <summary>
    /// Called when entering this state.
    /// </summary>
    /// <param name="previousState">The state being exited, or null if this is the initial state.</param>
    void Enter(IGameState? previousState);

    /// <summary>
    /// Called when exiting this state.
    /// </summary>
    /// <param name="nextState">The state being entered.</param>
    void Exit(IGameState? nextState);

    /// <summary>
    /// Called every frame to process state-specific input.
    /// </summary>
    /// <param name="delta">Time since last frame in seconds.</param>
    void ProcessInput(double delta);

    /// <summary>
    /// Called every frame to update state-specific logic.
    /// </summary>
    /// <param name="delta">Time since last frame in seconds.</param>
    void Update(double delta);
}
