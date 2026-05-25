using Godot;
using Nightwalk.Core.GameState;

namespace Nightwalk.Godot.GameState;

/// <summary>
/// Main menu state - no gameplay, cursor visible for menu navigation.
/// </summary>
public sealed class MainMenuState : IGameState
{
    private readonly GameStateManager _stateManager;

    public string Name => GameStateNames.MainMenu;
    public bool AllowsGameTick => false;
    public bool AllowsPlayerInput => false;
    public bool ShowsHUD => false;
    public bool ShowsCursor => true;

    public MainMenuState(GameStateManager stateManager)
    {
        _stateManager = stateManager;
    }

    public void Enter(IGameState? previousState)
    {
        // Show cursor for menu
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Exit(IGameState? nextState)
    {
        // Menu cleanup handled elsewhere
    }

    public void ProcessInput(double delta)
    {
        // Menu input handled by UI controls
    }

    public void Update(double delta)
    {
        // Menu animations could go here
    }

    /// <summary>
    /// Called when player clicks "Play" in menu.
    /// </summary>
    public void StartGame()
    {
        _stateManager.TransitionTo<PlayingState>();
    }
}
