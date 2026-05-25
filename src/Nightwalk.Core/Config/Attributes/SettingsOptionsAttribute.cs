namespace Nightwalk.Core.Config.Attributes;

/// <summary>
/// Specifies dropdown options for a non-enum property.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingsOptionsAttribute : Attribute
{
    /// <summary>
    /// Available options as (Label, Value) pairs.
    /// </summary>
    public (string Label, int Value)[] Options { get; }

    /// <summary>
    /// Creates options from alternating label/value pairs.
    /// </summary>
    /// <param name="labelValuePairs">Alternating: "Label1", value1, "Label2", value2, ...</param>
    /// <example>
    /// [SettingsOptions("Off", 0, "2x MSAA", 2, "4x MSAA", 4, "8x MSAA", 8)]
    /// </example>
    public SettingsOptionsAttribute(params object[] labelValuePairs)
    {
        if (labelValuePairs.Length % 2 != 0)
        {
            throw new ArgumentException("Options must be provided as label/value pairs", nameof(labelValuePairs));
        }

        var options = new List<(string Label, int Value)>();
        for (int i = 0; i < labelValuePairs.Length; i += 2)
        {
            var label = labelValuePairs[i] as string
                ?? throw new ArgumentException($"Expected string label at index {i}", nameof(labelValuePairs));
            var value = labelValuePairs[i + 1] is int intVal
                ? intVal
                : throw new ArgumentException($"Expected int value at index {i + 1}", nameof(labelValuePairs));
            options.Add((label, value));
        }

        Options = options.ToArray();
    }
}
