using Godot;
using Nightwalk.Core.GameState;

namespace Nightwalk.Godot.GameState;

/// <summary>
/// Paused state - game world frozen, cursor visible.
/// </summary>
public sealed class PausedState : IGameState
{
    private readonly GameStateManager _stateManager;

    public string Name => GameStateNames.Paused;
    public bool AllowsGameTick => false;
    public bool AllowsPlayerInput => false;
    public bool ShowsHUD => true;
    public bool ShowsCursor => true;

    public PausedState(GameStateManager stateManager)
    {
        _stateManager = stateManager;
    }

    public void Enter(IGameState? previousState)
    {
        // Show cursor for menu interaction
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Exit(IGameState? nextState)
    {
        // State exit handled by next state's Enter
    }

    public void ProcessInput(double delta)
    {
        // Resume on pause key or escape
        if (Input.IsActionJustPressed("pause"))
        {
            _stateManager.TransitionTo<PlayingState>();
        }
    }

    public void Update(double delta)
    {
        // Could update pause menu animations here
    }
}
