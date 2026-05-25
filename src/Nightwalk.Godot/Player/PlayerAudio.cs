using Godot;
using Nightwalk.Core.Audio;
using Nightwalk.Core.Config;
using Nightwalk.Godot.Audio;

namespace Nightwalk.Godot.Player;

/// <summary>
/// Handles all player audio: footsteps, jump, land, jetpack, flight sounds.
/// Plain C# class - manages audio state and triggers.
/// </summary>
public sealed class PlayerAudio
{
    private ISfxService? _sfx;
    private GameDataService? _data;
    private SynthLoopManager? _synthLoops;

    // State tracking (only audio-specific state; grounded/flight state comes from caller)
    private bool _wasJetpacking;
    private float _footstepTimer;
    private float _jetpackHoldTime;

    private const float JetpackLoopStartDelay = 0.12f;

    /// <summary>
    /// Sets the SFX service for audio playback.
    /// </summary>
    public void SetSfxService(ISfxService sfx)
    {
        _sfx = sfx;
    }

    /// <summary>
    /// Sets the synth loop manager for procedural audio loops.
    /// </summary>
    public void SetSynthLoopManager(SynthLoopManager? synthLoopManager)
    {
        _synthLoops = synthLoopManager;
    }

    /// <summary>
    /// Sets the game data service for config values.
    /// </summary>
    public void Configure(GameDataService data)
    {
        _data = data;
    }

    /// <summary>
    /// Update audio state and play sounds based on movement state.
    /// </summary>
    /// <param name="wasGrounded">Previous frame's grounded state (from caller).</param>
    /// <param name="wasFlightActive">Previous frame's flight mode state (from caller).</param>
    public void Update(
        float dt,
        bool isGrounded,
        bool wasGrounded,
        Vector3 velocity,
        bool isFlightModeActive,
        bool wasFlightActive,
        bool jetpackHeld,
        bool runHeld,
        bool hasEnergy,
        float energyPercent,
        float speedFactor,
        float walkSpeed)
    {
        if (_sfx == null) return;

        // Landing sound
        if (isGrounded && !wasGrounded)
        {
            _sfx.Play(SfxId.Land);
        }

        // Jump sound
        if (!isGrounded && wasGrounded && velocity.Y > 0)
        {
            _sfx.Play(SfxId.Jump);
        }

        // Flight mode activation/deactivation
        if (isFlightModeActive && !wasFlightActive)
        {
            _sfx.Play(SfxId.FlightActivate);
            _sfx.PlayLoop(SfxId.FlightLoop);
        }
        else if (!isFlightModeActive && wasFlightActive)
        {
            _sfx.Play(SfxId.FlightDeactivate);
            _sfx.StopLoop(SfxId.FlightLoop, 0.3f);
        }

        // Flight loop pitch modulation
        if (isFlightModeActive && _sfx.IsLooping(SfxId.FlightLoop))
        {
            _sfx.SetLoopPitch(SfxId.FlightLoop, 0.8f + speedFactor * 0.4f);
        }

        // Jetpack sounds (only in normal mode)
        if (!isFlightModeActive)
        {
            bool wantsJetpack = !isGrounded && jetpackHeld && hasEnergy;
            if (wantsJetpack)
                _jetpackHoldTime += dt;
            else
                _jetpackHoldTime = 0f;

            bool isJetpacking = wantsJetpack && (_wasJetpacking || _jetpackHoldTime >= JetpackLoopStartDelay);

            if (isJetpacking && !_wasJetpacking)
            {
                _sfx.Play(SfxId.JetpackActivate);
                if (_synthLoops != null)
                {
                    _synthLoops.StartJetpackLoop(0.18f);
                }
                else
                {
                    _sfx.PlayLoop(SfxId.JetpackLoop);
                }
            }
            else if (!isJetpacking && _wasJetpacking)
            {
                _sfx.Play(SfxId.JetpackDeactivate);
                if (_synthLoops != null)
                {
                    _synthLoops.StopJetpackLoop(0.2f);
                }
                else
                {
                    _sfx.StopLoop(SfxId.JetpackLoop, 0.2f);
                }
            }

            // Jetpack modulation based on energy
            if (isJetpacking && _synthLoops != null && _synthLoops.IsJetpackLoopPlaying)
            {
                _synthLoops.UpdateJetpackThrust(0.3f + energyPercent * 0.7f);
            }
            else if (isJetpacking && _sfx.IsLooping(SfxId.JetpackLoop))
            {
                _sfx.SetLoopPitch(SfxId.JetpackLoop, 0.9f + energyPercent * 0.2f);
            }

            _wasJetpacking = isJetpacking;
        }

        // Footsteps (only when grounded and moving, not in flight mode)
        if (!isFlightModeActive && isGrounded)
        {
            var horizontalSpeed = new Vector2(velocity.X, velocity.Z).Length();
            bool isMoving = horizontalSpeed > 0.5f;

            if (isMoving && _data != null)
            {
                _footstepTimer -= dt;
                if (_footstepTimer <= 0)
                {
                    // Determine footstep type and play
                    bool isRunning = runHeld && horizontalSpeed > walkSpeed * 0.9f;
                    var footstepId = isRunning ? SfxId.FootstepRun : SfxId.FootstepWalk;

                    if (isRunning)
                        _sfx.PlayWithPitch(footstepId, _data.Audio.Sfx.Player.RunPitchMultiplier);
                    else
                        _sfx.Play(footstepId);

                    // Adjust interval for running (faster footsteps)
                    var footstepInterval = _data.Audio.Sfx.Player.FootstepInterval;
                    _footstepTimer = isRunning ? footstepInterval * 0.7f : footstepInterval;
                }
            }
            else
            {
                _footstepTimer = 0; // Reset so next step plays immediately
            }
        }
    }

    /// <summary>
    /// Reset audio state (for session reset).
    /// </summary>
    public void Reset()
    {
        // Stop any active loops
        _sfx?.StopLoop(SfxId.FlightLoop, 0f);
        _sfx?.StopLoop(SfxId.JetpackLoop, 0f);
        _synthLoops?.StopJetpackLoop(0f);

        _wasJetpacking = false;
        _jetpackHoldTime = 0f;
        _footstepTimer = 0;
    }
}
