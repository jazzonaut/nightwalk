namespace Nightwalk.Core.Config.Attributes;

/// <summary>
/// Specifies the valid range for a numeric setting.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingsRangeAttribute : Attribute
{
    /// <summary>
    /// Minimum value for the setting.
    /// </summary>
    public double Min { get; }

    /// <summary>
    /// Maximum value for the setting.
    /// </summary>
    public double Max { get; }

    /// <summary>
    /// Step size for the slider. 0 means auto-determine based on range.
    /// </summary>
    public double Step { get; set; }

    public SettingsRangeAttribute(double min, double max)
    {
        Min = min;
        Max = max;
        Step = 0;
    }
}
