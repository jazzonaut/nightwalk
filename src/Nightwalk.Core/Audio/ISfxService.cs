namespace Nightwalk.Core.Audio;

/// <summary>
/// Engine-agnostic interface for sound effects playback.
/// </summary>
public interface ISfxService
{
    /// <summary>Play a one-shot sound effect by ID.</summary>
    void Play(string soundId);

    /// <summary>Play a one-shot sound effect with custom volume.</summary>
    void Play(string soundId, float volume);

    /// <summary>Play a one-shot sound effect with custom pitch.</summary>
    void PlayWithPitch(string soundId, float pitch);

    /// <summary>Start a looping sound effect.</summary>
    void PlayLoop(string soundId);

    /// <summary>Stop a looping sound effect.</summary>
    void StopLoop(string soundId);

    /// <summary>Stop a looping sound effect with fade out.</summary>
    void StopLoop(string soundId, float fadeOutDuration);

    /// <summary>Set the pitch of a currently playing loop.</summary>
    void SetLoopPitch(string soundId, float pitch);

    /// <summary>Set the volume of a currently playing loop.</summary>
    void SetLoopVolume(string soundId, float volume);

    /// <summary>Check if a sound is currently looping.</summary>
    bool IsLooping(string soundId);

    /// <summary>Stop all sounds immediately.</summary>
    void StopAll();
}
