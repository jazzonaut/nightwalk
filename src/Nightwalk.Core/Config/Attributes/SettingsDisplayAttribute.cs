namespace Nightwalk.Core.Config.Attributes;

/// <summary>
/// Specifies display options for a setting in the UI.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingsDisplayAttribute : Attribute
{
    /// <summary>
    /// Display name shown in the UI. If null, auto-generated from property name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional description/tooltip for the setting.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Format string for displaying the value (e.g., "{0}%", "{0:F1}x").
    /// </summary>
    public string? ValueFormat { get; set; }

    /// <summary>
    /// Multiplier applied to the display value (e.g., 100 to show 0-1 as 0-100%).
    /// </summary>
    public double DisplayMultiplier { get; set; } = 1.0;
}
