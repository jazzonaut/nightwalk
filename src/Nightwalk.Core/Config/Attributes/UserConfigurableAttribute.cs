namespace Nightwalk.Core.Config.Attributes;

/// <summary>
/// Marks a property as user-configurable in the settings menu.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UserConfigurableAttribute : Attribute
{
    /// <summary>
    /// The settings category this property belongs to.
    /// </summary>
    public SettingsCategory Category { get; }

    /// <summary>
    /// The type of UI control to use for this setting.
    /// </summary>
    public SettingsControlType ControlType { get; }

    /// <summary>
    /// Display order within the category (lower values appear first).
    /// </summary>
    public int Order { get; set; } = 100;

    public UserConfigurableAttribute(SettingsCategory category, SettingsControlType controlType = SettingsControlType.Auto)
    {
        Category = category;
        ControlType = controlType;
    }
}

/// <summary>
/// Categories for organizing settings in the UI.
/// </summary>
public enum SettingsCategory
{
    Audio,
    Graphics,
    Gameplay,
    Controls
}

/// <summary>
/// Types of UI controls for settings.
/// </summary>
public enum SettingsControlType
{
    /// <summary>Auto-detect based on property type (bool→Toggle, enum→Dropdown, numeric→Slider).</summary>
    Auto,

    /// <summary>Horizontal slider for numeric values.</summary>
    Slider,

    /// <summary>Toggle/checkbox for boolean values.</summary>
    Toggle,

    /// <summary>Dropdown for enum or predefined options.</summary>
    Dropdown
}
