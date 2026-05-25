using Godot;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Audio;

/// <summary>
/// Manages active synthesized audio loops for gameplay systems.
/// Provides a high-level interface for starting/stopping loops and updating parameters.
/// </summary>
public partial class SynthLoopManager : Node
{
    private SynthLoopPlayer? _fallingPlayer;
    private SynthLoopPlayer? _jetpackPlayer;
    private bool _enabled = true;

    public override void _Ready()
    {
        Name = "SynthLoopManager";
    }

    /// <summary>
    /// Configures the synth loop manager with SFX settings.
    /// </summary>
    public void Configure(SfxConfig config)
    {
        _enabled = config.Enabled;

        if (!_enabled)
        {
            StopAll();
        }
    }

    /// <summary>
    /// Starts the falling wind loop.
    /// </summary>
    public void StartFallingLoop()
    {
        if (!_enabled) return;
        if (_fallingPlayer != null && _fallingPlayer.IsPlaying) return;

        _fallingPlayer ??= CreatePlayer("FallingLoop");
        _fallingPlayer.StartLoop(SynthLoopType.Falling, 0f);
    }

    /// <summary>
    /// Updates the falling loop velocity parameter (0-1).
    /// Higher velocity = louder, more high-frequency content.
    /// </summary>
    public void UpdateFallingVelocity(float velocity)
    {
        if (_fallingPlayer != null && _fallingPlayer.IsPlaying)
        {
            _fallingPlayer.Velocity = velocity;
        }
    }

    /// <summary>
    /// Stops the falling loop with optional fade out.
    /// </summary>
    public void StopFallingLoop(float fadeTime = 0f)
    {
        _fallingPlayer?.StopLoop(fadeTime);
    }

    /// <summary>
    /// Returns true if the falling loop is currently playing.
    /// </summary>
    public bool IsFallingLoopPlaying => _fallingPlayer?.IsPlaying ?? false;

    /// <summary>
    /// Starts the jetpack loop.
    /// </summary>
    public void StartJetpackLoop(float fadeInTime = 0f)
    {
        if (!_enabled) return;
        if (_jetpackPlayer != null && _jetpackPlayer.IsPlaying) return;

        _jetpackPlayer ??= CreatePlayer("JetpackLoop");
        _jetpackPlayer.StartLoop(SynthLoopType.Jetpack, fadeInTime);
    }

    /// <summary>
    /// Updates the jetpack loop thrust parameter (0-1).
    /// Higher thrust = louder, brighter sound.
    /// </summary>
    public void UpdateJetpackThrust(float thrust)
    {
        if (_jetpackPlayer != null && _jetpackPlayer.IsPlaying)
        {
            _jetpackPlayer.Thrust = thrust;
        }
    }

    /// <summary>
    /// Stops the jetpack loop with optional fade out.
    /// </summary>
    public void StopJetpackLoop(float fadeTime = 0f)
    {
        _jetpackPlayer?.StopLoop(fadeTime);
    }

    /// <summary>
    /// Returns true if the jetpack loop is currently playing.
    /// </summary>
    public bool IsJetpackLoopPlaying => _jetpackPlayer?.IsPlaying ?? false;

    /// <summary>
    /// Stops all active loops immediately.
    /// </summary>
    public void StopAll()
    {
        _fallingPlayer?.StopLoop(0f);
        _jetpackPlayer?.StopLoop(0f);
    }

    private SynthLoopPlayer CreatePlayer(string name)
    {
        var player = new SynthLoopPlayer { Name = name };
        AddChild(player);
        return player;
    }

    public override void _ExitTree()
    {
        StopAll();
    }
}
