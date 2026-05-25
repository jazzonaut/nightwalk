namespace Nightwalk.Core.Audio;

/// <summary>
/// Engine-agnostic interface for music playback control.
/// </summary>
public interface IMusicService
{
    /// <summary>Current playback state.</summary>
    MusicState State { get; }

    /// <summary>Currently playing track ID, or null if none.</summary>
    string? CurrentTrackId { get; }

    /// <summary>Play a track by ID with optional crossfade.</summary>
    void Play(string trackId, bool crossfade = true);

    /// <summary>Stop playback with optional fadeout.</summary>
    void Stop(bool fadeOut = true);

    /// <summary>Pause playback.</summary>
    void Pause();

    /// <summary>Resume paused playback.</summary>
    void Resume();

    /// <summary>Play a random track from a category.</summary>
    void PlayCategory(string category, bool crossfade = true);
}

/// <summary>
/// Music playback states.
/// </summary>
public enum MusicState
{
    Stopped,
    Playing,
    Paused,
    CrossFading
}
