using System;
using Nightwalk.Core.Config;
using Nightwalk.Core.Difficulty;
using Nightwalk.Core.Scoring;
using Nightwalk.Godot.Audio;

namespace Nightwalk.Godot.Player;

/// <summary>
/// Handles fall damage detection and flight mode score drain.
/// Plain C# class - tracks velocity and applies penalties.
/// </summary>
public sealed class PlayerFallDamage
{
    private IScoreService? _scoreService;
    private GameDataService? _data;
    private IDifficultyService? _difficultyService;
    private SfxManager? _sfxManager;
    private SynthLoopManager? _synthLoopManager;

    private float _velocityBeforeLanding;
    private float _flightDrainAccumulator;
    private bool _isFallingFast;

    // Threshold for starting falling wind sound (normalized velocity)
    // 0.5 = ~25 m/s downward before wind sound starts
    private const float FallingWindThreshold = 0.5f;

    /// <summary>
    /// Sets the score service for applying penalties.
    /// </summary>
    public void SetScoreService(IScoreService scoreService)
    {
        _scoreService = scoreService;
    }

    /// <summary>
    /// Sets the difficulty service for scaling values.
    /// </summary>
    public void SetDifficultyService(IDifficultyService? difficultyService)
    {
        _difficultyService = difficultyService;
    }

    /// <summary>
    /// Sets the game data service for config values.
    /// </summary>
    public void Configure(GameDataService data)
    {
        _data = data;
    }

    /// <summary>
    /// Sets the SFX manager for playing generated sounds.
    /// </summary>
    public void SetSfxManager(SfxManager? sfxManager)
    {
        _sfxManager = sfxManager;
    }

    /// <summary>
    /// Sets the synth loop manager for falling wind sounds.
    /// </summary>
    public void SetSynthLoopManager(SynthLoopManager? synthLoopManager)
    {
        _synthLoopManager = synthLoopManager;
    }

    /// <summary>
    /// Track downward velocity while airborne and manage falling wind sound.
    /// </summary>
    public void TrackVelocity(float verticalVelocity, bool isGrounded)
    {
        if (!isGrounded)
        {
            _velocityBeforeLanding = -verticalVelocity; // Convert to positive for downward

            // Calculate normalized falling velocity for wind sound (0-1 range)
            // Assume max velocity around 50 m/s for normalization
            float normalizedVelocity = Math.Clamp(_velocityBeforeLanding / 50f, 0f, 1f);

            // Start/update falling wind loop when falling fast enough
            if (normalizedVelocity > FallingWindThreshold)
            {
                if (!_isFallingFast)
                {
                    _isFallingFast = true;
                    _synthLoopManager?.StartFallingLoop();
                }
                _synthLoopManager?.UpdateFallingVelocity(normalizedVelocity);
            }
        }
        else if (_isFallingFast)
        {
            // Landed - stop falling wind
            _isFallingFast = false;
            _synthLoopManager?.StopFallingLoop(0.1f);
        }
    }

    /// <summary>
    /// Check for fall damage on landing.
    /// </summary>
    public void UpdateFallDamage(bool isGrounded, bool wasGrounded, bool isFlightModeActive)
    {
        if (_data == null || _scoreService == null) return;

        // Skip if fall damage is disabled
        if (_difficultyService?.IsFallDamageDisabled == true) return;

        var fallDamageConfig = _data.Gameplay.FallDamage;

        // Check for hard landing (transitioning from airborne to grounded)
        if (isGrounded && !wasGrounded && !isFlightModeActive)
        {
            // Get scaled threshold and damage values
            float threshold = _difficultyService?.GetFallDamageThreshold(fallDamageConfig.VelocityThreshold)
                ?? fallDamageConfig.VelocityThreshold;
            float damagePerVelocity = _difficultyService?.GetFallDamagePerVelocity(fallDamageConfig.PointsPerVelocity)
                ?? fallDamageConfig.PointsPerVelocity;

            if (_velocityBeforeLanding > threshold)
            {
                // Calculate penalty based on excess velocity
                float excessVelocity = _velocityBeforeLanding - threshold;
                int penalty = (int)(excessVelocity * damagePerVelocity);

                // Clamp to configured range
                penalty = Math.Clamp(penalty, fallDamageConfig.MinPenalty, fallDamageConfig.MaxPenalty);

                _scoreService.RemoveScore(penalty, ScoreCategory.FallPenalty);

                // Play fall penalty sound
                if (_sfxManager != null)
                    _sfxManager.PlayGenerated(SfxSynth.FallPenalty());
            }
        }

        // Reset tracked velocity when grounded
        if (isGrounded)
        {
            _velocityBeforeLanding = 0;
        }
    }

    /// <summary>
    /// Apply score drain while actively flying.
    /// </summary>
    public void UpdateFlightDrain(float dt, bool isActivelyFlying)
    {
        if (_data == null || _scoreService == null) return;

        if (!isActivelyFlying)
        {
            _flightDrainAccumulator = 0;
            return;
        }

        // Get scaled drain rate
        var baseDrainRate = _data.Physics.Flight.ScoreDrainRate;
        var drainRate = _difficultyService?.GetFlightScoreDrain(baseDrainRate) ?? baseDrainRate;
        _flightDrainAccumulator += drainRate * dt;

        // Drain whole points when accumulated
        if (_flightDrainAccumulator >= 1f)
        {
            int pointsToDrain = (int)_flightDrainAccumulator;
            _flightDrainAccumulator -= pointsToDrain;
            _scoreService.RemoveScore(pointsToDrain, ScoreCategory.FlightDrain);
        }
    }

    /// <summary>
    /// Reset state (for session reset).
    /// </summary>
    public void Reset()
    {
        _velocityBeforeLanding = 0;
        _flightDrainAccumulator = 0;

        // Stop falling wind if active
        if (_isFallingFast)
        {
            _isFallingFast = false;
            _synthLoopManager?.StopFallingLoop(0f);
        }
    }
}
