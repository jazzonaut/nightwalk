using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Godot.Audio;

namespace Nightwalk.Godot.Setup;

/// <summary>
/// Result of audio system setup.
/// </summary>
public sealed record AudioSystemResult(
    MusicManager MusicManager,
    SfxManager SfxManager,
    AmbientManager AmbientManager,
    SynthLoopManager SynthLoopManager
)
{
    public void Cleanup()
    {
        MusicManager?.QueueFree();
        SfxManager?.QueueFree();
        AmbientManager?.QueueFree();
        SynthLoopManager?.QueueFree();
    }
}

/// <summary>
/// Configures the audio system (music, SFX, ambient sounds).
/// </summary>
public static class AudioSystemSetup
{
    /// <summary>
    /// Creates and configures the audio system.
    /// </summary>
    /// <param name="parent">The parent node to add audio managers to.</param>
    /// <param name="config">Audio configuration.</param>
    /// <returns>The created audio managers.</returns>
    public static AudioSystemResult Configure(Node parent, AudioConfig config)
    {
        // Always create audio managers so they can respond to settings changes
        var musicManager = new MusicManager { Name = "MusicManager" };
        musicManager.Configure(config.Music);
        parent.AddChild(musicManager);

        // Auto-play ambient music if enabled and tracks are configured
        // Defer to ensure _Ready has initialized the players
        if (config.Music.Enabled && musicManager.HasTracks)
        {
            Callable.From(() => musicManager.PlayCategory("ambient", crossfade: false)).CallDeferred();
        }

        var sfxManager = new SfxManager { Name = "SfxManager" };
        sfxManager.Configure(config.Sfx);
        parent.AddChild(sfxManager);

        var ambientManager = new AmbientManager { Name = "AmbientManager" };
        ambientManager.Configure(config.Sfx);
        parent.AddChild(ambientManager);

        // Start city ambient if enabled
        // Defer to ensure _Ready has initialized the players
        if (config.Sfx.Enabled)
        {
            Callable.From(() => ambientManager.StartCityAmbient()).CallDeferred();
        }

        // Create synth loop manager for procedural audio loops
        var synthLoopManager = new SynthLoopManager();
        synthLoopManager.Configure(config.Sfx);
        parent.AddChild(synthLoopManager);

        Log.Info(LogCategory.Audio, "Audio system initialized");

        return new AudioSystemResult(musicManager, sfxManager, ambientManager, synthLoopManager);
    }
}
