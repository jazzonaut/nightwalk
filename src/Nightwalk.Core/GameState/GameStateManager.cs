using System;
using System.Collections.Generic;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.GameState.Events;

namespace Nightwalk.Core.GameState;

/// <summary>
/// Manages game state transitions and provides access to current state properties.
/// </summary>
public sealed class GameStateManager
{
    private readonly Dictionary<Type, IGameState> _states = new();
    private readonly IEventBus _eventBus;
    private IGameState? _currentState;

    /// <summary>
    /// The currently active game state.
    /// </summary>
    public IGameState? CurrentState => _currentState;

    /// <summary>
    /// Whether the game world should be ticking.
    /// </summary>
    public bool AllowsGameTick => _currentState?.AllowsGameTick ?? false;

    /// <summary>
    /// Whether player input should be processed.
    /// </summary>
    public bool AllowsPlayerInput => _currentState?.AllowsPlayerInput ?? false;

    /// <summary>
    /// Whether HUD should be visible.
    /// </summary>
    public bool ShowsHUD => _currentState?.ShowsHUD ?? false;

    /// <summary>
    /// Whether cursor should be visible.
    /// </summary>
    public bool ShowsCursor => _currentState?.ShowsCursor ?? true;

    public GameStateManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Registers a state instance for later transitions.
    /// </summary>
    public void RegisterState<T>(T state) where T : class, IGameState
    {
        _states[typeof(T)] = state;
        Log.Info(LogCategory.Sim, $"Registered game state: {state.Name}");
    }

    /// <summary>
    /// Transitions to a new state by type.
    /// </summary>
    public void TransitionTo<T>() where T : class, IGameState
    {
        if (!_states.TryGetValue(typeof(T), out var newState))
        {
            Log.Error(LogCategory.Sim, $"Cannot transition to unregistered state: {typeof(T).Name}");
            return;
        }

        TransitionTo(newState);
    }

    /// <summary>
    /// Transitions to a new state instance.
    /// </summary>
    public void TransitionTo(IGameState newState)
    {
        var previousState = _currentState;
        var previousName = previousState?.Name;

        // Exit current state
        previousState?.Exit(newState);

        // Enter new state
        _currentState = newState;
        _currentState.Enter(previousState);

        Log.Info(LogCategory.Sim, $"Game state: {previousName ?? "(none)"} -> {newState.Name}");

        // Publish event
        _eventBus.Publish(new GameStateChangedEvent(previousName, newState.Name));
    }

    /// <summary>
    /// Gets a registered state by type.
    /// </summary>
    public T? GetState<T>() where T : class, IGameState
    {
        return _states.TryGetValue(typeof(T), out var state) ? state as T : null;
    }

    /// <summary>
    /// Checks if currently in a specific state type.
    /// </summary>
    public bool IsInState<T>() where T : class, IGameState
    {
        return _currentState is T;
    }

    /// <summary>
    /// Process input for the current state.
    /// </summary>
    public void ProcessInput(double delta)
    {
        _currentState?.ProcessInput(delta);
    }

    /// <summary>
    /// Update the current state.
    /// </summary>
    public void Update(double delta)
    {
        _currentState?.Update(delta);
    }
}
