using Godot;

namespace Nightwalk.Godot.Data;

/// <summary>
/// Handles loading and saving user preference config files from the user:// directory.
/// </summary>
public sealed class UserConfigLoader
{

    /// <summary>
    /// Loads raw TOML content for a user config file.
    /// </summary>
    /// <param name="name">Config name without extension (e.g., "core").</param>
    /// <returns>TOML content string, or null if file doesn't exist.</returns>
    public string? LoadRaw(string name)
    {
        var fullPath = ConfigConsts.UserConfigPath + name + ConfigConsts.FileExtension;
        if (!global::Godot.FileAccess.FileExists(fullPath))
            return null;

        using var file = global::Godot.FileAccess.Open(fullPath, global::Godot.FileAccess.ModeFlags.Read);
        return file?.GetAsText();
    }

    /// <summary>
    /// Checks if a user config file exists.
    /// </summary>
    public bool Exists(string name)
    {
        return global::Godot.FileAccess.FileExists(ConfigConsts.UserConfigPath + name + ConfigConsts.FileExtension);
    }

    /// <summary>
    /// Saves TOML content to a user config file.
    /// </summary>
    /// <param name="name">Config name without extension (e.g., "core").</param>
    /// <param name="tomlContent">TOML content to write. Empty string deletes the file.</param>
    public void Save(string name, string tomlContent)
    {
        var fullPath = ConfigConsts.UserConfigPath + name + ConfigConsts.FileExtension;

        if (string.IsNullOrEmpty(tomlContent))
        {
            // Delete the file if content is empty
            if (global::Godot.FileAccess.FileExists(fullPath))
            {
                var absolutePath = ProjectSettings.GlobalizePath(fullPath);
                DirAccess.RemoveAbsolute(absolutePath);
            }
            return;
        }

        EnsureDirectoryExists();
        using var file = global::Godot.FileAccess.Open(fullPath, global::Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(tomlContent);
    }

    /// <summary>
    /// Gets the absolute filesystem path for a user config file.
    /// Useful for file system watchers.
    /// </summary>
    public string GetAbsolutePath(string name)
    {
        return ProjectSettings.GlobalizePath(ConfigConsts.UserConfigPath + name + ConfigConsts.FileExtension);
    }

    /// <summary>
    /// Gets the absolute filesystem path for the user config directory.
    /// </summary>
    public string GetDirectoryPath()
    {
        return ProjectSettings.GlobalizePath(ConfigConsts.UserConfigPath);
    }

    /// <summary>
    /// Ensures the user config directory exists.
    /// </summary>
    public void EnsureDirectoryExists()
    {
        var absolutePath = ProjectSettings.GlobalizePath(ConfigConsts.UserConfigPath);
        DirAccess.MakeDirRecursiveAbsolute(absolutePath);
    }
}
