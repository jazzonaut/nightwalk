using System;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;

namespace Nightwalk.Core.Resonance.Warden;

/// <summary>
/// Controls Warden behavior and state transitions.
/// Handles spawn/despawn logic, movement, and vulnerability.
/// </summary>
public sealed class WardenController
{
    private readonly IEventBus _eventBus;
    private readonly WardenConfig _config;

    private WardenState _state = WardenState.Absent;
    private float _vulnerabilityTimer;
    private float _calmWindowTimer;
    private bool _inCalmWindow;

    // Position tracking (Y is altitude)
    private float _targetX, _targetZ;
    private float _currentX, _currentZ, _currentY;
    private float _targetY; // Target altitude (player Y + spawn height)

    /// <summary>
    /// Current Warden state.
    /// </summary>
    public WardenState State => _state;

    /// <summary>
    /// Current Warden position.
    /// </summary>
    public (float X, float Y, float Z) Position => (_currentX, _currentY, _currentZ);

    /// <summary>
    /// Whether the Warden is in the calm window.
    /// </summary>
    public bool InCalmWindow => _inCalmWindow;

    /// <summary>
    /// Event fired when Warden becomes vulnerable.
    /// </summary>
    public event Action? OnBecameVulnerable;

    /// <summary>
    /// Event fired when vulnerability ends without repel.
    /// </summary>
    public event Action? OnVulnerabilityEnded;

    public WardenController(IEventBus eventBus, WardenConfig config)
    {
        _eventBus = eventBus;
        _config = config;
    }

    /// <summary>
    /// Requests the Warden to spawn above the given position.
    /// </summary>
    public void RequestSpawn(float playerX, float playerY, float playerZ)
    {
        if (_state != WardenState.Absent) return;

        _targetX = playerX;
        _targetZ = playerZ;
        _currentX = playerX;
        _currentZ = playerZ;

        // Spawn high above the player's current position
        _targetY = _config.SpawnHeight;
        _currentY = _targetY + _config.SpawnDescentOffset; // Start slightly higher and descend

        _state = WardenState.Arriving;

        Log.Info(LogCategory.Sim, $"Warden spawning above player at altitude {_currentY}");
    }

    /// <summary>
    /// Requests the Warden to despawn.
    /// </summary>
    public void RequestDespawn()
    {
        if (_state == WardenState.Absent || _state == WardenState.Departing) return;

        _state = WardenState.Departing;

        Log.Info(LogCategory.Sim, "Warden departing");
    }

    /// <summary>
    /// Forces the Warden into vulnerable state.
    /// </summary>
    public void SetVulnerable()
    {
        if (_state != WardenState.Present) return;

        _state = WardenState.Vulnerable;
        _vulnerabilityTimer = _config.VulnerabilityDuration;
        OnBecameVulnerable?.Invoke();

        Log.Info(LogCategory.Sim, "Warden became vulnerable");
    }

    /// <summary>
    /// Called when the Warden is successfully repelled by beam.
    /// </summary>
    /// <param name="calmWindowDuration">Duration of the calm window (no scans) in seconds.</param>
    public void OnRepelled(float calmWindowDuration)
    {
        if (_state != WardenState.Vulnerable) return;

        _state = WardenState.Departing;
        _inCalmWindow = true;
        _calmWindowTimer = calmWindowDuration;

        Log.Info(LogCategory.Sim, $"Warden repelled! Calm window: {calmWindowDuration}s");
    }

    /// <summary>
    /// Updates the player position for drift tracking.
    /// </summary>
    public void UpdatePlayerPosition(float playerX, float playerZ)
    {
        _targetX = playerX;
        _targetZ = playerZ;
    }

    /// <summary>
    /// Updates the Warden each frame.
    /// </summary>
    /// <param name="delta">Time since last update.</param>
    /// <param name="alertLevel">Current alert level (0-1) for scan frequency.</param>
    public void Update(float delta, float alertLevel)
    {
        // Update calm window
        if (_inCalmWindow)
        {
            _calmWindowTimer -= delta;
            if (_calmWindowTimer <= 0)
            {
                _inCalmWindow = false;
            }
        }

        switch (_state)
        {
            case WardenState.Arriving:
                UpdateArriving(delta);
                break;

            case WardenState.Present:
                UpdatePresent(delta, alertLevel);
                break;

            case WardenState.Vulnerable:
                UpdateVulnerable(delta);
                break;

            case WardenState.Departing:
                UpdateDeparting(delta);
                break;
        }
    }

    private void UpdateArriving(float delta)
    {
        // Descend to target altitude (faster descent)
        _currentY = MathF.Max(_targetY, _currentY - _config.DriftSpeed * _config.DescentSpeedMultiplier * delta);

        // Also drift toward player
        DriftTowardPlayer(delta);

        // Check if arrived (snap to exact height)
        if (_currentY <= _targetY + _config.ArrivalThreshold)
        {
            _currentY = _targetY;
            _state = WardenState.Present;
            Log.Info(LogCategory.Sim, "Warden arrived");
        }
    }

    private void UpdatePresent(float delta, float alertLevel)
    {
        DriftTowardPlayer(delta);
    }

    private void UpdateVulnerable(float delta)
    {
        // Still drift slowly
        DriftTowardPlayer(delta * _config.VulnerableDriftMultiplier);

        _vulnerabilityTimer -= delta;
        if (_vulnerabilityTimer <= 0)
        {
            // Vulnerability ended without repel
            _state = WardenState.Present;
            OnVulnerabilityEnded?.Invoke();
            Log.Info(LogCategory.Sim, "Warden vulnerability ended");
        }
    }

    private void UpdateDeparting(float delta)
    {
        // Ascend rapidly
        _currentY += _config.DepartureSpeed * delta;

        // Check if gone (ascended above target position)
        if (_currentY > _targetY + _config.DepartureDistance)
        {
            _state = WardenState.Absent;
            Log.Info(LogCategory.Sim, "Warden departed");
        }
    }

    private void DriftTowardPlayer(float delta)
    {
        float dx = _targetX - _currentX;
        float dz = _targetZ - _currentZ;
        float distance = MathF.Sqrt(dx * dx + dz * dz);

        if (distance > _config.DriftMinDistance)
        {
            float speed = _config.DriftSpeed * delta;
            float ratio = MathF.Min(speed / distance, 1f);
            _currentX += dx * ratio;
            _currentZ += dz * ratio;
        }
    }

    /// <summary>
    /// Resets the controller state.
    /// </summary>
    public void Reset()
    {
        _state = WardenState.Absent;
        _vulnerabilityTimer = 0f;
        _calmWindowTimer = 0f;
        _inCalmWindow = false;
        _targetY = 0f;
        _currentY = 0f;
    }
}
