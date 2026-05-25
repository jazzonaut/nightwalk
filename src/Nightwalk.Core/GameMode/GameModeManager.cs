using System;
using System.Collections.Generic;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;

namespace Nightwalk.Core.GameMode;

/// <summary>
/// Manages game mode lifecycle and switching.
/// Only one mode can be active at a time.
/// </summary>
public sealed class GameModeManager
{
    private readonly IEventBus _eventBus;
    private readonly Dictionary<string, IGameMode> _modes = new();
    private IGameMode? _activeMode;
    private string _pendingModeId = "silence";

    /// <summary>
    /// The currently active game mode.
    /// </summary>
    public IGameMode? ActiveMode => _activeMode;

    /// <summary>
    /// The mode ID that will be activated on next game start.
    /// </summary>
    public string PendingModeId => _pendingModeId;

    /// <summary>
    /// All registered mode IDs.
    /// </summary>
    public IEnumerable<string> RegisteredModes => _modes.Keys;

    public GameModeManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Registers a game mode.
    /// </summary>
    public void RegisterMode(IGameMode mode)
    {
        if (_modes.ContainsKey(mode.Id))
        {
            Log.Warn(LogCategory.Sim, $"Game mode '{mode.Id}' already registered, replacing");
        }
        _modes[mode.Id] = mode;
        Log.Debug(LogCategory.Sim, $"Registered game mode: {mode.Id}");
    }

    /// <summary>
    /// Gets a registered mode by ID.
    /// </summary>
    public IGameMode? GetMode(string id)
    {
        return _modes.TryGetValue(id, out var mode) ? mode : null;
    }

    /// <summary>
    /// Sets the mode to be activated when the game starts.
    /// Does not immediately switch - call ActivatePendingMode() to apply.
    /// </summary>
    public void SetPendingMode(string modeId)
    {
        if (!_modes.ContainsKey(modeId))
        {
            Log.Warn(LogCategory.Sim, $"Unknown game mode '{modeId}', keeping current pending mode '{_pendingModeId}'");
            return;
        }
        _pendingModeId = modeId;
        Log.Info(LogCategory.Sim, $"Pending game mode set to: {modeId}");
    }

    /// <summary>
    /// Activates the pending mode. Called when gameplay starts.
    /// </summary>
    public void ActivatePendingMode()
    {
        if (!_modes.TryGetValue(_pendingModeId, out var mode))
        {
            Log.Error(LogCategory.Sim, $"Cannot activate unknown mode: {_pendingModeId}");
            return;
        }

        if (_activeMode == mode)
        {
            Log.Debug(LogCategory.Sim, $"Mode '{_pendingModeId}' already active");
            return;
        }

        // Exit current mode
        _activeMode?.Exit();

        // Enter new mode
        _activeMode = mode;
        _activeMode.Enter();

        Log.Info(LogCategory.Sim, $"Activated game mode: {_activeMode.DisplayName}");
    }

    /// <summary>
    /// Deactivates the current mode. Called when returning to menu.
    /// </summary>
    public void DeactivateCurrentMode()
    {
        if (_activeMode != null)
        {
            Log.Info(LogCategory.Sim, $"Deactivating game mode: {_activeMode.DisplayName}");
            _activeMode.Exit();
            _activeMode = null;
        }
    }

    /// <summary>
    /// Updates the active mode each physics tick.
    /// </summary>
    public void Tick(double delta)
    {
        _activeMode?.Tick(delta);
    }

    /// <summary>
    /// Updates the active mode each frame.
    /// </summary>
    public void Process(double delta)
    {
        _activeMode?.Process(delta);
    }

    /// <summary>
    /// Resets the active mode for a new session.
    /// </summary>
    public void Reset()
    {
        _activeMode?.Reset();
    }
}
