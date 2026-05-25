using System.Text;
using Nightwalk.Core.Diagnostics;
using Tomlyn;
using Tomlyn.Model;

namespace Nightwalk.Core.Config;

/// <summary>
/// Service for managing user preferences that override system config defaults.
/// </summary>
public sealed class UserPreferencesService
{
    private readonly Dictionary<string, TomlTable> _pendingChanges = new();
    private readonly Func<string, string?> _userConfigReader;
    private readonly Action<string, string> _userConfigWriter;
    private readonly Action<IReadOnlySet<string>> _onPreferencesChanged;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a user preferences service.
    /// </summary>
    /// <param name="userConfigReader">Function to read existing user config TOML by name.</param>
    /// <param name="userConfigWriter">Action to write user config TOML (name, content).</param>
    /// <param name="onPreferencesChanged">Callback invoked after preferences are saved, with set of changed config names.</param>
    public UserPreferencesService(
        Func<string, string?> userConfigReader,
        Action<string, string> userConfigWriter,
        Action<IReadOnlySet<string>> onPreferencesChanged)
    {
        _userConfigReader = userConfigReader ?? throw new ArgumentNullException(nameof(userConfigReader));
        _userConfigWriter = userConfigWriter ?? throw new ArgumentNullException(nameof(userConfigWriter));
        _onPreferencesChanged = onPreferencesChanged ?? throw new ArgumentNullException(nameof(onPreferencesChanged));
    }

    /// <summary>
    /// Sets a user preference value. Use dot notation for nested paths.
    /// Changes are held in memory until Save() is called.
    /// </summary>
    /// <param name="configName">Config file name without extension (e.g., "core", "physics").</param>
    /// <param name="path">Dot-separated path using PascalCase (e.g., "Graphics.BloomEnabled").</param>
    /// <param name="value">Value to set.</param>
    /// <returns>True if successful, false if the existing user config is malformed.</returns>
    /// <example>
    /// preferences.SetPreference("core", "Graphics.BloomEnabled", false);
    /// preferences.SetPreference("physics", "Movement.MouseSensitivity", 0.003f);
    /// </example>
    public bool SetPreference<T>(string configName, string path, T value)
    {
        lock (_lock)
        {
            if (!_pendingChanges.TryGetValue(configName, out var table))
            {
                // Load existing user config or create empty table
                var existingToml = _userConfigReader(configName);
                if (string.IsNullOrEmpty(existingToml))
                {
                    table = new TomlTable();
                }
                else
                {
                    // Parse and check for errors before modifying
                    var doc = Toml.Parse(existingToml);
                    if (doc.HasErrors)
                    {
                        Log.Error(LogCategory.Config, $"User config '{configName}' is malformed, cannot modify. Fix or delete the file.");
                        return false;
                    }
                    table = Toml.ToModel(existingToml);
                }
                _pendingChanges[configName] = table;
            }

            if (!TrySetNestedValue(table, path, value))
            {
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Saves all pending preference changes to user config files.
    /// Triggers a config reload after saving.
    /// </summary>
    public void Save()
    {
        Dictionary<string, TomlTable> toSave;
        lock (_lock)
        {
            if (_pendingChanges.Count == 0)
                return;

            toSave = new Dictionary<string, TomlTable>(_pendingChanges);
            _pendingChanges.Clear();
        }

        foreach (var (configName, table) in toSave)
        {
            var tomlString = Toml.FromModel(table);
            _userConfigWriter(configName, tomlString);
        }

        var changedConfigs = toSave.Keys.ToHashSet();
        _onPreferencesChanged(changedConfigs);
    }

    /// <summary>
    /// Resets a specific config to system defaults by deleting the user override file.
    /// </summary>
    /// <param name="configName">Config file name without extension.</param>
    public void ResetToDefaults(string configName)
    {
        lock (_lock)
        {
            _pendingChanges.Remove(configName);
        }

        // Empty string signals deletion
        _userConfigWriter(configName, "");
        _onPreferencesChanged(new HashSet<string> { configName });
    }

    /// <summary>
    /// Resets all configs to system defaults by deleting all user override files.
    /// </summary>
    public void ResetAllToDefaults()
    {
        var configNames = new[] { "core", "physics", "gameplay", "audio", "tools", "aliveness", "beacons", "skycraft" };

        lock (_lock)
        {
            _pendingChanges.Clear();
        }

        foreach (var name in configNames)
        {
            _userConfigWriter(name, "");
        }

        _onPreferencesChanged(configNames.ToHashSet());
    }

    /// <summary>
    /// Discards any unsaved changes without persisting them.
    /// </summary>
    public void DiscardChanges()
    {
        lock (_lock)
        {
            _pendingChanges.Clear();
        }
    }

    /// <summary>
    /// Gets whether there are unsaved preference changes.
    /// </summary>
    public bool HasUnsavedChanges
    {
        get
        {
            lock (_lock)
            {
                return _pendingChanges.Count > 0;
            }
        }
    }

    private static bool TrySetNestedValue<T>(TomlTable table, string path, T value)
    {
        // Validate value type first
        if (!TryConvertToTomlValue(value, out var tomlValue, out var error))
        {
            Log.Error(LogCategory.Config, error);
            return false;
        }

        var parts = path.Split('.');
        var current = table;

        // Navigate/create nested tables for all but the last part
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var key = ToSnakeCase(parts[i]);
            if (!current.TryGetValue(key, out var nested) || nested is not TomlTable nestedTable)
            {
                nestedTable = new TomlTable();
                current[key] = nestedTable;
            }
            current = (TomlTable)current[key];
        }

        // Set the final value
        var finalKey = ToSnakeCase(parts[^1]);
        current[finalKey] = tomlValue;
        return true;
    }

    private static string ToSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        var sb = new StringBuilder();
        for (int i = 0; i < pascalCase.Length; i++)
        {
            var c = pascalCase[i];
            if (i > 0 && char.IsUpper(c))
            {
                // Only add underscore if previous char was lowercase
                // This keeps acronyms together: "VSync" -> "vsync", "BloomEnabled" -> "bloom_enabled"
                if (char.IsLower(pascalCase[i - 1]))
                {
                    sb.Append('_');
                }
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static bool TryConvertToTomlValue<T>(T value, out object result, out string error)
    {
        error = "";
        result = value switch
        {
            null => null!,
            string s => s,
            bool b => b,
            int i => (long)i,
            float f => (double)f,
            double d => d,
            long l => l,
            Enum e => e.ToString(),
            _ => null!
        };

        if (value == null)
        {
            error = "Cannot set null value. Use RemovePreference to clear a value.";
            return false;
        }

        if (result == null)
        {
            error = $"Unsupported preference type: {typeof(T).Name}. Supported: string, bool, int, float, double, long, enum.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Removes a user preference, reverting to system default.
    /// Changes are held in memory until Save() is called.
    /// </summary>
    /// <param name="configName">Config file name without extension.</param>
    /// <param name="path">Dot-separated path using PascalCase.</param>
    /// <returns>True if successful, false if the existing user config is malformed.</returns>
    public bool RemovePreference(string configName, string path)
    {
        lock (_lock)
        {
            if (!_pendingChanges.TryGetValue(configName, out var table))
            {
                var existingToml = _userConfigReader(configName);
                if (string.IsNullOrEmpty(existingToml))
                {
                    // No user config exists, nothing to remove
                    return true;
                }

                var doc = Toml.Parse(existingToml);
                if (doc.HasErrors)
                {
                    Log.Error(LogCategory.Config, $"User config '{configName}' is malformed, cannot modify.");
                    return false;
                }
                table = Toml.ToModel(existingToml);
                _pendingChanges[configName] = table;
            }

            RemoveNestedValue(table, path);
            return true;
        }
    }

    private static void RemoveNestedValue(TomlTable table, string path)
    {
        var parts = path.Split('.');
        var current = table;

        // Navigate to parent table
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var key = ToSnakeCase(parts[i]);
            if (!current.TryGetValue(key, out var nested) || nested is not TomlTable nestedTable)
            {
                // Path doesn't exist, nothing to remove
                return;
            }
            current = nestedTable;
        }

        // Remove the final key
        var finalKey = ToSnakeCase(parts[^1]);
        current.Remove(finalKey);
    }
}
