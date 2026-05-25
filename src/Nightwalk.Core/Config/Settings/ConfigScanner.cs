using System.Reflection;
using System.Text.RegularExpressions;
using Nightwalk.Core.Config.Attributes;

namespace Nightwalk.Core.Config.Settings;

/// <summary>
/// Scans config types for user-configurable properties marked with attributes.
/// </summary>
public sealed partial class ConfigScanner
{
    private readonly Dictionary<string, RegisteredConfig> _configs = new();

    /// <summary>
    /// Registers a config type for scanning.
    /// </summary>
    /// <typeparam name="T">The config type.</typeparam>
    /// <param name="name">Config name without extension (e.g., "core", "audio").</param>
    /// <param name="getter">Function that returns the current config instance.</param>
    public void RegisterConfig<T>(string name, Func<T> getter) where T : class
    {
        _configs[name] = new RegisteredConfig(typeof(T), () => getter());
    }

    /// <summary>
    /// Scans all registered configs and returns discovered settings grouped by category.
    /// </summary>
    public Dictionary<SettingsCategory, List<SettingMetadata>> ScanAll()
    {
        var results = new Dictionary<SettingsCategory, List<SettingMetadata>>();

        foreach (var category in Enum.GetValues<SettingsCategory>())
        {
            results[category] = new List<SettingMetadata>();
        }

        foreach (var (configName, registered) in _configs)
        {
            ScanType(configName, registered.Type, registered.Getter, "", results);
        }

        // Sort each category by order
        foreach (var list in results.Values)
        {
            list.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        // Remove empty categories
        foreach (var category in results.Keys.ToList())
        {
            if (results[category].Count == 0)
            {
                results.Remove(category);
            }
        }

        return results;
    }

    /// <summary>
    /// Gets the current value for a setting.
    /// </summary>
    public object? GetValue(SettingMetadata setting)
    {
        return setting.ValueGetter();
    }

    private void ScanType(
        string configName,
        Type type,
        Func<object?> parentGetter,
        string pathPrefix,
        Dictionary<SettingsCategory, List<SettingMetadata>> results)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var path = string.IsNullOrEmpty(pathPrefix)
                ? property.Name
                : $"{pathPrefix}.{property.Name}";

            // Create getter for this property
            var propInfo = property;
            Func<object?> valueGetter = () =>
            {
                var parent = parentGetter();
                return parent != null ? propInfo.GetValue(parent) : null;
            };

            // Check for UserConfigurable attribute
            var configurable = property.GetCustomAttribute<UserConfigurableAttribute>();
            if (configurable != null)
            {
                var metadata = CreateMetadata(configName, path, property, configurable, valueGetter);
                results[metadata.Category].Add(metadata);
            }
            // Recursively scan nested config classes
            else if (IsNestedConfigType(property.PropertyType))
            {
                ScanType(configName, property.PropertyType, valueGetter, path, results);
            }
        }
    }

    private static SettingMetadata CreateMetadata(
        string configName,
        string path,
        PropertyInfo property,
        UserConfigurableAttribute configurable,
        Func<object?> valueGetter)
    {
        var propertyType = property.PropertyType;
        var controlType = ResolveControlType(configurable.ControlType, propertyType);

        // Get display attribute
        var display = property.GetCustomAttribute<SettingsDisplayAttribute>();
        var displayName = display?.DisplayName ?? GenerateDisplayName(property.Name);
        var description = display?.Description;
        var displayOptions = display != null
            ? new SettingsDisplayOptions(display.ValueFormat, display.DisplayMultiplier)
            : null;

        // Get range attribute for sliders
        SettingsRange? range = null;
        var rangeAttr = property.GetCustomAttribute<SettingsRangeAttribute>();
        if (rangeAttr != null)
        {
            var step = rangeAttr.Step > 0 ? rangeAttr.Step : CalculateDefaultStep(rangeAttr.Min, rangeAttr.Max);
            range = new SettingsRange(rangeAttr.Min, rangeAttr.Max, step);
        }

        // Get dropdown options
        IReadOnlyList<DropdownOption>? dropdownOptions = null;
        var optionsAttr = property.GetCustomAttribute<SettingsOptionsAttribute>();
        if (optionsAttr != null)
        {
            dropdownOptions = optionsAttr.Options
                .Select(o => new DropdownOption(o.Label, o.Value))
                .ToList();
        }

        // Get enum options
        IReadOnlyList<EnumOption>? enumOptions = null;
        if (propertyType.IsEnum)
        {
            enumOptions = Enum.GetValues(propertyType)
                .Cast<object>()
                .Select(v => new EnumOption(GenerateDisplayName(v.ToString()!), v))
                .ToList();
        }

        return new SettingMetadata
        {
            ConfigName = configName,
            PropertyPath = path,
            PropertyType = propertyType,
            Category = configurable.Category,
            ControlType = controlType,
            Order = configurable.Order,
            DisplayName = displayName,
            Description = description,
            Range = range,
            DisplayOptions = displayOptions,
            DropdownOptions = dropdownOptions,
            EnumOptions = enumOptions,
            ValueGetter = valueGetter
        };
    }

    private static SettingsControlType ResolveControlType(SettingsControlType specified, Type propertyType)
    {
        if (specified != SettingsControlType.Auto)
        {
            return specified;
        }

        // Auto-detect based on property type
        if (propertyType == typeof(bool))
        {
            return SettingsControlType.Toggle;
        }

        if (propertyType.IsEnum)
        {
            return SettingsControlType.Dropdown;
        }

        if (IsNumericType(propertyType))
        {
            return SettingsControlType.Slider;
        }

        // Default to slider for unknown types
        return SettingsControlType.Slider;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(float) || type == typeof(double)
            || type == typeof(long) || type == typeof(short) || type == typeof(byte);
    }

    private static bool IsNestedConfigType(Type type)
    {
        // A nested config type is a class that's not a primitive, string, or collection
        return type.IsClass
            && type != typeof(string)
            && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
    }

    private static string GenerateDisplayName(string propertyName)
    {
        // Convert PascalCase to space-separated words as fallback
        // e.g., "BloomEnabled" -> "Bloom Enabled"
        // Note: For proper display names (especially acronyms), use SettingsDisplayAttribute.DisplayName
        return PascalCaseRegex().Replace(propertyName, " $1").Trim();
    }

    private static double CalculateDefaultStep(double min, double max)
    {
        var range = max - min;
        if (range <= 1)
        {
            return 0.01;
        }
        if (range <= 10)
        {
            return 0.1;
        }
        if (range <= 100)
        {
            return 1;
        }
        return Math.Floor(range / 100);
    }

    [GeneratedRegex(@"([A-Z])")]
    private static partial Regex PascalCaseRegex();

    private sealed record RegisteredConfig(Type Type, Func<object?> Getter);
}
