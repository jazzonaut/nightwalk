using Godot;
using Nightwalk.Core.GameState;

namespace Nightwalk.Godot.GameState;

/// <summary>
/// Normal gameplay state - player has control, world is ticking.
/// </summary>
public sealed class PlayingState : IGameState
{
    private readonly GameStateManager _stateManager;

    public string Name => GameStateNames.Playing;
    public bool AllowsGameTick => true;
    public bool AllowsPlayerInput => true;
    public bool ShowsHUD => true;
    public bool ShowsCursor => false;

    public PlayingState(GameStateManager stateManager)
    {
        _stateManager = stateManager;
    }

    public void Enter(IGameState? previousState)
    {
        // Capture mouse for FPS controls
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public void Exit(IGameState? nextState)
    {
        // State exit handled by next state's Enter
    }

    public void ProcessInput(double delta)
    {
        // Check for pause
        if (Input.IsActionJustPressed("pause"))
        {
            _stateManager.TransitionTo<PausedState>();
        }
    }

    public void Update(double delta)
    {
        // Normal gameplay update handled elsewhere
    }
}
