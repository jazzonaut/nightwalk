using Nightwalk.Core.Config;
using Nightwalk.Core.Events;
using Nightwalk.Core.Resonance.Events;

namespace Nightwalk.Core.Resonance;

/// <summary>
/// Tracks the alert level (0-1) that triggers Warden spawn and affects Seeker behavior.
/// Alert rises on collectible pickup and Seeker detection, decays when undetected.
/// </summary>
public sealed class AlertMeter
{
    private readonly IEventBus _eventBus;
    private readonly AlertConfig _config;

    private float _value;
    private float _timeSinceLastIncrease;
    private bool _decayPaused;

    /// <summary>
    /// Current alert value (0-1).
    /// </summary>
    public float Value => _value;

    /// <summary>
    /// Whether alert decay is currently paused (e.g., during calm window).
    /// </summary>
    public bool DecayPaused
    {
        get => _decayPaused;
        set => _decayPaused = value;
    }

    public AlertMeter(IEventBus eventBus, AlertConfig config)
    {
        _eventBus = eventBus;
        _config = config;
        _value = 0f;
    }

    /// <summary>
    /// Updates the alert meter, handling decay.
    /// </summary>
    public void Update(float delta)
    {
        _timeSinceLastIncrease += delta;

        // Decay after delay, if not paused
        if (!_decayPaused && _timeSinceLastIncrease >= _config.DecayDelay && _value > 0)
        {
            float previousValue = _value;
            _value = System.Math.Max(0f, _value - _config.DecayRate * delta);

            if (System.Math.Abs(_value - previousValue) > 0.001f)
            {
                _eventBus.Publish(new AlertChangedEvent(previousValue, _value));
            }
        }
    }

    /// <summary>
    /// Adds to the alert level.
    /// </summary>
    public void AddAlert(float amount)
    {
        float previousValue = _value;
        _value = System.Math.Min(1f, _value + amount);
        _timeSinceLastIncrease = 0f;

        if (System.Math.Abs(_value - previousValue) > 0.001f)
        {
            _eventBus.Publish(new AlertChangedEvent(previousValue, _value));
        }
    }

    /// <summary>
    /// Drops the alert level by the specified amount.
    /// </summary>
    public void DropAlert(float amount)
    {
        float previousValue = _value;
        _value = System.Math.Max(0f, _value - amount);

        if (System.Math.Abs(_value - previousValue) > 0.001f)
        {
            _eventBus.Publish(new AlertChangedEvent(previousValue, _value));
        }
    }

    /// <summary>
    /// Resets the meter to zero.
    /// </summary>
    public void Reset()
    {
        float previousValue = _value;
        _value = 0f;
        _timeSinceLastIncrease = 0f;
        _decayPaused = false;

        if (previousValue > 0.001f)
        {
            _eventBus.Publish(new AlertChangedEvent(previousValue, 0f));
        }
    }
}
