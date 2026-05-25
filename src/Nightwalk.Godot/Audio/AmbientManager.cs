using Godot;
using Nightwalk.Core.Audio;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Godot.Audio;

/// <summary>
/// Manages environmental ambient audio with smooth crossfading.
/// Handles rain intensity levels and background city ambience.
/// </summary>
public sealed partial class AmbientManager : Node
{
    private SfxConfig _config = null!;
    private float _crossfadeDuration;
    private float _cityAmbientVolume;

    // Rain audio players (for crossfading between intensities)
    private SfxPlayer? _rainPlayerA;
    private SfxPlayer? _rainPlayerB;
    private SfxPlayer? _activeRainPlayer;
    private string _currentRainLevel = string.Empty;

    // City ambient
    private SfxPlayer? _cityPlayer;

    private float _currentRainIntensity;

    public void Configure(SfxConfig config)
    {
        _config = config;
        _crossfadeDuration = config.Environment.RainCrossfadeDuration;
        _cityAmbientVolume = config.Environment.CityAmbientVolume;

        // Handle enabled state
        if (!config.Enabled)
        {
            // SFX disabled - stop all ambient sounds
            StopAll();
        }
        else
        {
            // Apply volume to currently playing sounds
            if (_activeRainPlayer != null && _activeRainPlayer.IsPlaying && !string.IsNullOrEmpty(_currentRainLevel))
            {
                var rainVolume = GetRainVolume(_currentRainLevel, _currentRainIntensity);
                var volumeDb = AudioMath.LinearToDb(rainVolume * config.MasterVolume);
                _activeRainPlayer.SetVolume(volumeDb);
            }

            if (_cityPlayer != null && _cityPlayer.IsPlaying)
            {
                var volumeDb = AudioMath.LinearToDb(_cityAmbientVolume * config.MasterVolume);
                _cityPlayer.SetVolume(volumeDb);
            }
        }
    }

    public override void _Ready()
    {
        // Create dual rain players for crossfading
        _rainPlayerA = new SfxPlayer { Name = "RainPlayerA", Bus = "Ambient" };
        _rainPlayerB = new SfxPlayer { Name = "RainPlayerB", Bus = "Ambient" };
        AddChild(_rainPlayerA);
        AddChild(_rainPlayerB);
        _activeRainPlayer = _rainPlayerA;

        // Create city ambient player
        _cityPlayer = new SfxPlayer { Name = "CityAmbient", Bus = "Ambient" };
        AddChild(_cityPlayer);
    }

    /// <summary>
    /// Start playing city ambient sound.
    /// </summary>
    public void StartCityAmbient()
    {
        if (_cityPlayer == null || !_config.Enabled) return;

        var path = GetSoundPath(SfxId.CityAmbient);
        if (string.IsNullOrEmpty(path)) return;

        _cityPlayer.LoadSound(path);
        var volumeDb = AudioMath.LinearToDb(_cityAmbientVolume * _config.MasterVolume);
        _cityPlayer.FadeIn(volumeDb, 2f);
    }

    /// <summary>
    /// Stop city ambient sound.
    /// </summary>
    public void StopCityAmbient()
    {
        _cityPlayer?.FadeOut(2f);
    }

    /// <summary>
    /// Update rain audio to match visual intensity.
    /// </summary>
    /// <param name="intensity">Rain intensity from 0 (off) to 1 (heavy).</param>
    public void SetRainIntensity(float intensity)
    {
        if (!_config.Enabled) return;

        _currentRainIntensity = intensity;

        // Determine which rain sound to play based on intensity
        string targetLevel;
        if (intensity <= 0.01f)
        {
            targetLevel = string.Empty; // No rain
        }
        else if (intensity < 0.4f)
        {
            targetLevel = SfxId.RainLight;
        }
        else if (intensity < 0.7f)
        {
            targetLevel = SfxId.RainMedium;
        }
        else
        {
            targetLevel = SfxId.RainHeavy;
        }

        // If level changed, crossfade to new sound
        if (targetLevel != _currentRainLevel)
        {
            CrossfadeRain(targetLevel, intensity);
            _currentRainLevel = targetLevel;
        }
        else if (!string.IsNullOrEmpty(targetLevel) && _activeRainPlayer != null)
        {
            // Just adjust volume based on intensity within current level
            var volume = GetRainVolume(targetLevel, intensity);
            var volumeDb = AudioMath.LinearToDb(volume * _config.MasterVolume);
            _activeRainPlayer.SetVolume(volumeDb);
        }
    }

    private void CrossfadeRain(string newLevel, float intensity)
    {
        if (string.IsNullOrEmpty(newLevel))
        {
            // Fade out current rain
            _activeRainPlayer?.FadeOut(_crossfadeDuration);
            return;
        }

        var path = GetSoundPath(newLevel);
        if (string.IsNullOrEmpty(path)) return;

        // Swap players
        var inactivePlayer = _activeRainPlayer == _rainPlayerA ? _rainPlayerB : _rainPlayerA;

        // Fade out current
        if (_activeRainPlayer != null && _activeRainPlayer.IsPlaying)
        {
            _activeRainPlayer.FadeOut(_crossfadeDuration);
        }

        // Fade in new
        inactivePlayer?.LoadSound(path);
        var volume = GetRainVolume(newLevel, intensity);
        var volumeDb = AudioMath.LinearToDb(volume * _config.MasterVolume);
        inactivePlayer?.FadeIn(volumeDb, _crossfadeDuration);

        _activeRainPlayer = inactivePlayer;
    }

    private float GetRainVolume(string level, float intensity)
    {
        // Scale volume within the intensity range for this level
        return level switch
        {
            SfxId.RainLight => 0.4f + (intensity / 0.4f) * 0.2f,   // 0.4-0.6
            SfxId.RainMedium => 0.5f + ((intensity - 0.4f) / 0.3f) * 0.2f, // 0.5-0.7
            SfxId.RainHeavy => 0.6f + ((intensity - 0.7f) / 0.3f) * 0.4f,  // 0.6-1.0
            _ => 0.5f
        };
    }

    private string GetSoundPath(string soundId)
    {
        foreach (var sound in _config.Sounds)
        {
            if (sound.Id == soundId)
                return sound.Path;
        }
        return string.Empty;
    }

    /// <summary>
    /// Stop all ambient sounds.
    /// </summary>
    public void StopAll()
    {
        _rainPlayerA?.Stop();
        _rainPlayerB?.Stop();
        _cityPlayer?.Stop();
        _currentRainLevel = string.Empty;
    }

    public override void _ExitTree()
    {
        StopAll();
    }
}
