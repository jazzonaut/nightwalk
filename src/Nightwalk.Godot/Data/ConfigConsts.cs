namespace Nightwalk.Godot.Data;

/// <summary>
/// Constants for configuration files.
/// </summary>
public static class ConfigConsts
{
    /// <summary>
    /// Path to system configuration files (read-only defaults).
    /// </summary>
    public const string SystemConfigPath = "res://data/";

    /// <summary>
    /// Path to user configuration files (user overrides).
    /// </summary>
    public const string UserConfigPath = "user://config/";

    /// <summary>
    /// File extension for configuration files.
    /// </summary>
    public const string FileExtension = ".toml";
}
