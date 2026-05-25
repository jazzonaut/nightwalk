using System;
using Nightwalk.Core.Config;

namespace Nightwalk.Core.Resonance;

/// <summary>
/// Tracks beam contact with the Warden's vulnerable core.
/// </summary>
public sealed class WardenCore
{
    private readonly RepelConfig _config;

    private bool _isVulnerable;
    private bool _isReceivingBeam;
    private float _beamContactTime;
    private float _cooldownTimer;

    /// <summary>
    /// Whether the core is currently vulnerable (can be targeted).
    /// </summary>
    public bool IsVulnerable => _isVulnerable && _cooldownTimer <= 0;

    /// <summary>
    /// Whether the core is currently receiving a beam.
    /// </summary>
    public bool IsReceivingBeam => _isReceivingBeam;

    /// <summary>
    /// Current beam contact progress (0-1).
    /// </summary>
    public float ContactProgress => _beamContactTime / _config.BeamContactDuration;

    /// <summary>
    /// Event fired when the Warden is successfully repelled.
    /// </summary>
    public event Action? OnRepelled;

    public WardenCore(RepelConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Sets the vulnerability state.
    /// </summary>
    public void SetVulnerable(bool vulnerable)
    {
        _isVulnerable = vulnerable;

        if (!vulnerable)
        {
            _beamContactTime = 0f;
            _isReceivingBeam = false;
        }
    }

    /// <summary>
    /// Called when a beam hits the core.
    /// </summary>
    public void OnBeamContact(float delta)
    {
        if (!IsVulnerable) return;

        _isReceivingBeam = true;
        _beamContactTime += delta;

        if (_beamContactTime >= _config.BeamContactDuration)
        {
            // Repel successful!
            OnRepelled?.Invoke();
            _isVulnerable = false;
            _beamContactTime = 0f;
            _cooldownTimer = _config.RepelCooldown;
        }
    }

    /// <summary>
    /// Called when beam contact is lost.
    /// </summary>
    public void OnBeamLost()
    {
        // Only set the flag - Update() handles delta-scaled decay
        _isReceivingBeam = false;
    }

    /// <summary>
    /// Updates the core state.
    /// </summary>
    public void Update(float delta)
    {
        // Update cooldown
        if (_cooldownTimer > 0)
        {
            _cooldownTimer -= delta;
        }

        // Decay beam contact if not receiving
        if (!_isReceivingBeam && _beamContactTime > 0)
        {
            _beamContactTime = Math.Max(0f, _beamContactTime - delta * _config.BeamContactDecayRate);
        }
    }

    /// <summary>
    /// Resets the core state.
    /// </summary>
    public void Reset()
    {
        _isVulnerable = false;
        _isReceivingBeam = false;
        _beamContactTime = 0f;
        _cooldownTimer = 0f;
    }
}
