using Nightwalk.Core.Collectibles.Events;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.Resonance;
using Nightwalk.Core.Resonance.Events;
using Nightwalk.Core.Scoring;

namespace Nightwalk.Core.GameMode;

/// <summary>
/// Resonance mode adds tension and stealth mechanics to exploration.
/// Core loop: Explore → Collect (raise Alert) → Warden arrives → Seekers investigate →
/// Manage Exposure → Repel Warden (beam puzzle) → Earn calm window → Repeat
/// </summary>
public sealed class ResonanceMode : IGameMode
{
    public string Id => "resonance";
    public string DisplayName => "Resonance";
    public string Description => "Tension rises. Watchers observe.";

    private readonly IEventBus _eventBus;
    private readonly GameDataService _data;
    private readonly IScoreService _scoreService;

    private ResonanceConfig Config => _data.Resonance;

    // Sub-systems
    private AlertMeter? _alertMeter;
    private ExposureMeter? _exposureMeter;

    // State
    private bool _isActive;
    private bool _isPlayerDetected;
    private float _accumulatedScoreDrain;

    public ResonanceMode(IEventBus eventBus, GameDataService data, IScoreService scoreService)
    {
        _eventBus = eventBus;
        _data = data;
        _scoreService = scoreService;
    }

    /// <summary>
    /// The current alert level (0-1).
    /// </summary>
    public float AlertLevel => _alertMeter?.Value ?? 0f;

    /// <summary>
    /// The current exposure level (0-1).
    /// </summary>
    public float ExposureLevel => _exposureMeter?.Value ?? 0f;

    /// <summary>
    /// Whether this mode is currently active.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Event fired when the mode needs to spawn the Warden (for Godot layer to handle).
    /// </summary>
    public event System.Action? OnWardenSpawnRequested;

    /// <summary>
    /// Event fired when the mode needs to despawn the Warden.
    /// </summary>
    public event System.Action? OnWardenDespawnRequested;

    /// <summary>
    /// Event fired when the Warden becomes vulnerable.
    /// </summary>
    public event System.Action? OnWardenVulnerable;

    public void Enter()
    {
        if (_isActive) return;
        _isActive = true;

        // Initialize sub-systems
        _alertMeter = new AlertMeter(_eventBus, Config.Alert);
        _exposureMeter = new ExposureMeter(_eventBus, Config.Exposure);

        // Subscribe to events
        _eventBus.Subscribe<CollectibleCollectedEvent>(OnCollectibleCollected);
        _eventBus.Subscribe<AlertChangedEvent>(OnAlertChanged);
        _eventBus.Subscribe<MaxExposureEvent>(OnMaxExposure);

        Log.Info(LogCategory.Sim, "Resonance mode entered");
    }

    public void Exit()
    {
        if (!_isActive) return;
        _isActive = false;

        // Unsubscribe from events
        _eventBus.Unsubscribe<CollectibleCollectedEvent>(OnCollectibleCollected);
        _eventBus.Unsubscribe<AlertChangedEvent>(OnAlertChanged);
        _eventBus.Unsubscribe<MaxExposureEvent>(OnMaxExposure);

        // Request Warden despawn if present
        OnWardenDespawnRequested?.Invoke();

        // Clean up sub-systems
        _alertMeter = null;
        _exposureMeter = null;

        Log.Info(LogCategory.Sim, "Resonance mode exited");
    }

    public void Tick(double delta)
    {
        if (!_isActive) return;

        float dt = (float)delta;

        _alertMeter?.Update(dt);
        _exposureMeter?.Update(dt);

        // Apply alert increase while detected (continuous, delta-based)
        if (_isPlayerDetected)
        {
            _alertMeter?.AddAlert(Config.Alert.SeekerDetectionIncrease * dt);
        }

        // Apply exposure score drain with fractional accumulation
        if (_exposureMeter != null && _exposureMeter.Value > 0)
        {
            float drainRate = Config.Exposure.ScoreDrainRate * _exposureMeter.Value;
            _accumulatedScoreDrain += drainRate * dt;

            // Only drain whole points to avoid constant tiny events
            if (_accumulatedScoreDrain >= 1f)
            {
                int wholeDrain = (int)_accumulatedScoreDrain;
                _accumulatedScoreDrain -= wholeDrain;
                _scoreService.RemoveScore(wholeDrain, ScoreCategory.ExposureDrain);
            }
        }
    }

    public void Process(double delta)
    {
        // Frame updates handled by Godot layer
    }

    public void Reset()
    {
        _alertMeter?.Reset();
        _exposureMeter?.Reset();
        _isPlayerDetected = false;
        _accumulatedScoreDrain = 0f;
        OnWardenDespawnRequested?.Invoke();
    }

    /// <summary>
    /// Called by Seeker system when a Seeker starts detecting the player.
    /// Each Seeker should call this once when it begins detecting.
    /// </summary>
    public void OnSeekerStartedDetecting()
    {
        if (!_isActive) return;

        _exposureMeter?.AddDetector();
        _isPlayerDetected = _exposureMeter?.IsDetected ?? false;
    }

    /// <summary>
    /// Called by Seeker system when a Seeker stops detecting the player.
    /// Each Seeker should call this once when it stops detecting.
    /// </summary>
    public void OnSeekerStoppedDetecting()
    {
        if (!_isActive) return;

        _exposureMeter?.RemoveDetector();
        _isPlayerDetected = _exposureMeter?.IsDetected ?? false;
    }

    /// <summary>
    /// Called when Warden is successfully repelled.
    /// </summary>
    public void OnWardenRepelled()
    {
        if (!_isActive) return;

        // Drop alert significantly
        _alertMeter?.DropAlert(Config.Repel.AlertDrop);

        // Award score
        _scoreService.AddScore(Config.Repel.ScoreReward, ScoreCategory.WardenRepelled);

        // Publish event
        _eventBus.Publish(new WardenRepelledEvent());

        Log.Info(LogCategory.Sim, "Warden repelled!");
    }

    private void OnCollectibleCollected(CollectibleCollectedEvent e)
    {
        if (!_isActive) return;

        // Increase alert on collection
        _alertMeter?.AddAlert(Config.Alert.PickupIncrease);

        // Emit noise ping for Seekers
        // Position will be set by the Godot layer which has access to the collectible location
    }

    private void OnAlertChanged(AlertChangedEvent e)
    {
        if (!_isActive) return;

        // Check Warden spawn threshold
        if (e.NewValue >= Config.Alert.WardenSpawnThreshold && e.PreviousValue < Config.Alert.WardenSpawnThreshold)
        {
            OnWardenSpawnRequested?.Invoke();
        }

        // Check Warden despawn threshold
        if (e.NewValue <= Config.Alert.WardenDespawnThreshold && e.PreviousValue > Config.Alert.WardenDespawnThreshold)
        {
            OnWardenDespawnRequested?.Invoke();
        }

        // Check vulnerability threshold
        if (e.NewValue >= Config.Alert.VulnerabilityThreshold && e.PreviousValue < Config.Alert.VulnerabilityThreshold)
        {
            OnWardenVulnerable?.Invoke();
        }
    }

    private void OnMaxExposure(MaxExposureEvent e)
    {
        if (!_isActive) return;

        // Large score penalty
        _scoreService.RemoveScore(Config.Exposure.MaxExposurePenalty, ScoreCategory.MaxExposure);

        // Alert spike
        _alertMeter?.AddAlert(Config.Exposure.MaxExposureAlertSpike);

        Log.Info(LogCategory.Sim, "Max exposure reached - penalty applied!");
    }
}
