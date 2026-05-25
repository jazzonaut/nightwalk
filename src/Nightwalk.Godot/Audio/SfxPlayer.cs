using Godot;
using Nightwalk.Core.Audio;
using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Godot.Audio;

/// <summary>
/// Wraps an AudioStreamPlayer for sound effects with support for
/// one-shot playback, looping, pitch/volume modulation, and fading.
/// </summary>
public sealed partial class SfxPlayer : Node
{
    private AudioStreamPlayer _player = null!;
    private Tween? _fadeTween;
    private float _baseVolume;
    private bool _isLooping;

    public bool IsPlaying => _player.Playing;
    public bool IsLooping => _isLooping && _player.Playing;
    public float CurrentVolumeDb => _player.VolumeDb;
    public float CurrentPitch => _player.PitchScale;

    public string Bus { get; set; } = "SFX";

    public override void _Ready()
    {
        _player = new AudioStreamPlayer();
        _player.Bus = Bus;
        AddChild(_player);

        // Connect finished signal for one-shot cleanup
        _player.Finished += OnFinished;
    }

    public void LoadSound(string path)
    {
        var stream = GD.Load<AudioStream>(path);
        if (stream == null)
        {
            Log.Error(LogCategory.Audio, $"Failed to load SFX: {path}");
            return;
        }
        _player.Stream = stream;
    }

    /// <summary>
    /// Play the sound once (one-shot).
    /// </summary>
    public void PlayOneShot(float volumeDb = 0f, float pitch = 1f)
    {
        _isLooping = false;
        ConfigureLoop(false);
        _player.VolumeDb = volumeDb;
        _baseVolume = volumeDb;
        _player.PitchScale = Mathf.Clamp(pitch, 0.1f, 4f);
        _player.Play();
    }

    /// <summary>
    /// Play a generated AudioStream once (one-shot).
    /// </summary>
    public void PlayOneShotStream(AudioStream stream, float volumeDb = 0f, float pitch = 1f)
    {
        _isLooping = false;
        _player.Stop(); // Stop any current playback first
        _player.Stream = stream;
        // Don't call ConfigureLoop - the stream is already configured
        _player.VolumeDb = volumeDb;
        _baseVolume = volumeDb;
        _player.PitchScale = Mathf.Clamp(pitch, 0.1f, 4f);
        _player.Play();
    }

    /// <summary>
    /// Play the sound as a continuous loop.
    /// </summary>
    public void PlayLoop(float volumeDb = 0f)
    {
        _isLooping = true;
        ConfigureLoop(true);
        _player.VolumeDb = volumeDb;
        _baseVolume = volumeDb;
        _player.PitchScale = 1f;
        _player.Play();
    }

    /// <summary>
    /// Stop playback immediately.
    /// </summary>
    public void Stop()
    {
        _fadeTween?.Kill();
        _isLooping = false;
        _player.Stop();
    }

    /// <summary>
    /// Stop with fade out.
    /// </summary>
    public void FadeOut(float duration)
    {
        _fadeTween?.Kill();

        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(_player, "volume_db", -80f, duration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Cubic);

        _fadeTween.Finished += () =>
        {
            _player.Stop();
            _isLooping = false;
            _player.VolumeDb = _baseVolume;
        };
    }

    /// <summary>
    /// Fade in from silent.
    /// </summary>
    public void FadeIn(float targetVolumeDb, float duration)
    {
        _fadeTween?.Kill();
        _player.VolumeDb = -80f;
        _baseVolume = targetVolumeDb;

        if (!_player.Playing)
            _player.Play();

        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(_player, "volume_db", targetVolumeDb, duration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
    }

    /// <summary>
    /// Set pitch scale (1.0 = normal, 2.0 = octave up, 0.5 = octave down).
    /// </summary>
    public void SetPitch(float pitch)
    {
        _player.PitchScale = Mathf.Clamp(pitch, 0.1f, 4f);
    }

    /// <summary>
    /// Set volume immediately.
    /// </summary>
    public void SetVolume(float volumeDb)
    {
        _player.VolumeDb = volumeDb;
    }

    /// <summary>
    /// Set volume relative to base (for modulation).
    /// </summary>
    public void SetVolumeMultiplier(float multiplier)
    {
        var adjustedDb = _baseVolume + AudioMath.LinearToDb(multiplier);
        _player.VolumeDb = adjustedDb;
    }

    private void ConfigureLoop(bool loop)
    {
        if (_player.Stream is AudioStreamOggVorbis ogg)
        {
            ogg.Loop = loop;
        }
        else if (_player.Stream is AudioStreamMP3 mp3)
        {
            mp3.Loop = loop;
        }
        else if (_player.Stream is AudioStreamWav wav)
        {
            wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
        }
    }

    private void OnFinished()
    {
        if (!_isLooping)
        {
            // Reset stream reference for pool reuse
            _player.Stream = null;
        }
    }

    public override void _ExitTree()
    {
        _player.Finished -= OnFinished;
    }
}
