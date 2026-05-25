using Godot;
using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Godot.Audio;

/// <summary>
/// Wraps an AudioStreamPlayer for a single music track with fade support.
/// </summary>
public sealed partial class MusicPlayer : Node
{
    private AudioStreamPlayer _player = null!;
    private Tween? _fadeTween;
    private float _targetVolume;
    private bool _isFading;

    public bool IsPlaying => _player.Playing;
    public bool IsFading => _isFading;
    public float CurrentVolumeDb => _player.VolumeDb;

    public override void _Ready()
    {
        _player = new AudioStreamPlayer();
        _player.Bus = "Music";
        AddChild(_player);
    }

    public void LoadTrack(string path)
    {
        var stream = GD.Load<AudioStream>(path);
        if (stream == null)
        {
            Log.Error(LogCategory.Audio, $"Failed to load audio: {path}");
            return;
        }
        _player.Stream = stream;
    }

    public void Play(float volumeDb = 0f, bool loop = true)
    {
        _player.VolumeDb = volumeDb;
        _targetVolume = volumeDb;

        // Configure looping based on stream type
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

        _player.Play();
    }

    public void Stop()
    {
        _fadeTween?.Kill();
        _isFading = false;
        _player.Stop();
    }

    public void FadeIn(float targetVolumeDb, float duration)
    {
        _fadeTween?.Kill();
        _player.VolumeDb = -80f; // Start silent
        _targetVolume = targetVolumeDb;
        _isFading = true;

        if (!_player.Playing)
            _player.Play();

        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(_player, "volume_db", targetVolumeDb, duration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        _fadeTween.Finished += () => _isFading = false;
    }

    public void FadeOut(float duration, bool stopAfter = true)
    {
        _fadeTween?.Kill();
        _isFading = true;

        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(_player, "volume_db", -80f, duration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Cubic);

        if (stopAfter)
        {
            _fadeTween.Finished += () =>
            {
                _player.Stop();
                _isFading = false;
            };
        }
    }

    public void SetVolumeImmediate(float volumeDb)
    {
        _player.VolumeDb = volumeDb;
        _targetVolume = volumeDb;
    }

    public void SetPaused(bool paused)
    {
        _player.StreamPaused = paused;
    }

    public bool IsPaused => _player.StreamPaused;
}
