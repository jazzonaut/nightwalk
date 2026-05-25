using Nightwalk.Core.Diagnostics;
using Tomlyn;
using Tomlyn.Model;

namespace Nightwalk.Core.Config;

/// <summary>
/// Utility for deep-merging TOML documents.
/// Used to overlay user preferences over system defaults.
/// </summary>
public static class TomlMerger
{
    /// <summary>
    /// Merges override TOML into base TOML. Override values replace base values.
    /// Nested tables are merged recursively.
    /// If the override TOML is malformed, logs a warning and returns the base TOML unchanged.
    /// </summary>
    /// <param name="baseToml">The base TOML document (system defaults).</param>
    /// <param name="overrideToml">The override TOML document (user preferences). Can be null or empty.</param>
    /// <returns>Merged TOML string, or base TOML if override is invalid.</returns>
    public static string Merge(string baseToml, string? overrideToml)
    {
        if (string.IsNullOrWhiteSpace(overrideToml))
            return baseToml;

        // Validate override TOML before attempting to merge
        var overrideDoc = Toml.Parse(overrideToml);
        if (overrideDoc.HasErrors)
        {
            Log.Warn(LogCategory.Config, "User config override is malformed, ignoring. Fix or delete the file.");
            return baseToml;
        }

        var baseModel = Toml.ToModel(baseToml);
        var overrideModel = Toml.ToModel(overrideToml);

        MergeTable(baseModel, overrideModel);

        return Toml.FromModel(baseModel);
    }

    private static void MergeTable(TomlTable target, TomlTable source)
    {
        foreach (var kvp in source)
        {
            if (kvp.Value is TomlTable sourceNested &&
                target.TryGetValue(kvp.Key, out var existing) &&
                existing is TomlTable targetNested)
            {
                // Recursively merge nested tables
                MergeTable(targetNested, sourceNested);
            }
            else
            {
                // Override the value (or add new key)
                target[kvp.Key] = kvp.Value;
            }
        }
    }
}
