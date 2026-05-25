using Nightwalk.Core.Config.Attributes;

namespace Nightwalk.Core.Config.Settings;

/// <summary>
/// Metadata describing a user-configurable setting discovered via reflection.
/// </summary>
public sealed class SettingMetadata
{
    /// <summary>
    /// Config name without extension (e.g., "core", "audio", "physics").
    /// </summary>
    public required string ConfigName { get; init; }

    /// <summary>
    /// Dot-separated property path using PascalCase (e.g., "Graphics.BloomEnabled").
    /// </summary>
    public required string PropertyPath { get; init; }

    /// <summary>
    /// The CLR type of the property (bool, int, float, enum, etc.).
    /// </summary>
    public required Type PropertyType { get; init; }

    /// <summary>
    /// Settings category for grouping in the UI.
    /// </summary>
    public required SettingsCategory Category { get; init; }

    /// <summary>
    /// Resolved control type to use in the UI.
    /// </summary>
    public required SettingsControlType ControlType { get; init; }

    /// <summary>
    /// Display order within the category.
    /// </summary>
    public required int Order { get; init; }

    /// <summary>
    /// Display name shown in the UI.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional description/tooltip.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Range for slider controls.
    /// </summary>
    public SettingsRange? Range { get; init; }

    /// <summary>
    /// Display formatting options.
    /// </summary>
    public SettingsDisplayOptions? DisplayOptions { get; init; }

    /// <summary>
    /// Options for dropdown controls (from SettingsOptionsAttribute).
    /// </summary>
    public IReadOnlyList<DropdownOption>? DropdownOptions { get; init; }

    /// <summary>
    /// Enum values for enum-type dropdowns.
    /// </summary>
    public IReadOnlyList<EnumOption>? EnumOptions { get; init; }

    /// <summary>
    /// Delegate to retrieve the current value from the config.
    /// </summary>
    public required Func<object?> ValueGetter { get; init; }
}

/// <summary>
/// Range specification for slider controls.
/// </summary>
public sealed record SettingsRange(double Min, double Max, double Step);

/// <summary>
/// Display formatting options.
/// </summary>
public sealed record SettingsDisplayOptions(string? ValueFormat, double DisplayMultiplier);

/// <summary>
/// A dropdown option from SettingsOptionsAttribute.
/// </summary>
public sealed record DropdownOption(string Label, int Value);

/// <summary>
/// An enum option for enum-type dropdowns.
/// </summary>
public sealed record EnumOption(string Label, object Value);
