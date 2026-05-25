using Nightwalk.Core.Config.Attributes;

namespace Nightwalk.Core.Config;

/// <summary>
/// Audio configuration for music and sound effects.
/// </summary>
public sealed class AudioConfig
{
    public MusicConfig Music { get; set; } = null!;
    public SfxConfig Sfx { get; set; } = null!;
}

/// <summary>
/// Background music configuration.
/// </summary>
public sealed class MusicConfig
{
    [UserConfigurable(SettingsCategory.Audio, SettingsControlType.Toggle, Order = 10)]
    [SettingsDisplay(DisplayName = "Music Enabled", Description = "Enable background music playback")]
    public bool Enabled { get; set; }

    [UserConfigurable(SettingsCategory.Audio, SettingsControlType.Slider, Order = 20)]
    [SettingsRange(0.0, 1.0, Step = 0.01)]
    [SettingsDisplay(DisplayName = "Music Volume", ValueFormat = "{0:F0}%", DisplayMultiplier = 100, Description = "Volume level for background music")]
    public float MasterVolume { get; set; }

    public float CrossfadeDuration { get; set; }
    public bool LoopEnabled { get; set; }
    public List<TrackConfig> Tracks { get; set; } = new();
}

/// <summary>
/// Individual track configuration.
/// </summary>
public sealed class TrackConfig
{
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public float BaseVolume { get; set; } = 1.0f;
}

/// <summary>
/// Sound effects configuration.
/// </summary>
public sealed class SfxConfig
{
    [UserConfigurable(SettingsCategory.Audio, SettingsControlType.Toggle, Order = 30)]
    [SettingsDisplay(DisplayName = "SFX Enabled", Description = "Enable sound effects like footsteps and ambient sounds")]
    public bool Enabled { get; set; }

    [UserConfigurable(SettingsCategory.Audio, SettingsControlType.Slider, Order = 40)]
    [SettingsRange(0.0, 1.0, Step = 0.01)]
    [SettingsDisplay(DisplayName = "SFX Volume", ValueFormat = "{0:F0}%", DisplayMultiplier = 100, Description = "Volume level for sound effects")]
    public float MasterVolume { get; set; }

    public SfxPlayerConfig Player { get; set; } = null!;
    public SfxEnvironmentConfig Environment { get; set; } = null!;
    public List<SoundConfig> Sounds { get; set; } = new();
}

/// <summary>
/// Player-specific SFX configuration.
/// </summary>
public sealed class SfxPlayerConfig
{
    public float FootstepInterval { get; set; }
    public float RunPitchMultiplier { get; set; }
}

/// <summary>
/// Environment SFX configuration.
/// </summary>
public sealed class SfxEnvironmentConfig
{
    public float RainCrossfadeDuration { get; set; }
    public float CityAmbientVolume { get; set; }
}

/// <summary>
/// Individual sound effect configuration.
/// </summary>
public sealed class SoundConfig
{
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public float Volume { get; set; }
    public bool Loop { get; set; }
}
