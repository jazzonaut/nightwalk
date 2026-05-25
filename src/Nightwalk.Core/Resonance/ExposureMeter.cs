using Nightwalk.Core.Config;
using Nightwalk.Core.Events;
using Nightwalk.Core.Resonance.Events;

namespace Nightwalk.Core.Resonance;

/// <summary>
/// Tracks the exposure level (0-1) when player is in Seeker scan cones.
/// Exposure causes score drain. At 100%: large score penalty + alert spike.
/// </summary>
public sealed class ExposureMeter
{
    private readonly IEventBus _eventBus;
    private readonly ExposureConfig _config;

    private float _value;
    private int _detectorCount;
    private bool _maxExposureFired;

    /// <summary>
    /// Current exposure value (0-1).
    /// </summary>
    public float Value => _value;

    /// <summary>
    /// Whether the player is currently being detected by any Seeker.
    /// </summary>
    public bool IsDetected => _detectorCount > 0;

    public ExposureMeter(IEventBus eventBus, ExposureConfig config)
    {
        _eventBus = eventBus;
        _config = config;
        _value = 0f;
    }

    /// <summary>
    /// Increments the detector count when a Seeker starts detecting.
    /// </summary>
    public void AddDetector()
    {
        _detectorCount++;
    }

    /// <summary>
    /// Decrements the detector count when a Seeker stops detecting.
    /// </summary>
    public void RemoveDetector()
    {
        _detectorCount = System.Math.Max(0, _detectorCount - 1);
    }

    /// <summary>
    /// Updates the exposure meter based on detection state.
    /// </summary>
    public void Update(float delta)
    {
        float previousValue = _value;

        if (IsDetected)
        {
            // Exposure rises while detected, faster with more detectors
            _value = System.Math.Min(1f, _value + _config.RiseRate * _detectorCount * delta);
        }
        else
        {
            // Exposure falls when safe
            _value = System.Math.Max(0f, _value - _config.FallRate * delta);

            // Reset max exposure flag when we drop below threshold
            if (_value < _config.MaxExposureResetThreshold)
            {
                _maxExposureFired = false;
            }
        }

        // Check for max exposure - fire event once, stay at max until player escapes
        if (_value >= 1f && !_maxExposureFired)
        {
            _maxExposureFired = true;
            _eventBus.Publish(new MaxExposureEvent());
        }

        if (System.Math.Abs(_value - previousValue) > 0.001f)
        {
            // Publish gradual change event (only if max exposure didn't trigger)
            _eventBus.Publish(new ExposureChangedEvent(previousValue, _value));
        }
    }

    /// <summary>
    /// Resets the meter to zero.
    /// </summary>
    public void Reset()
    {
        float previousValue = _value;
        _value = 0f;
        _detectorCount = 0;
        _maxExposureFired = false;

        if (previousValue > 0.001f)
        {
            _eventBus.Publish(new ExposureChangedEvent(previousValue, 0f));
        }
    }
}
