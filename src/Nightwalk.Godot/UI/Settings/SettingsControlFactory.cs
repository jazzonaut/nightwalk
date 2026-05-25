using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Nightwalk.Core.Config.Attributes;
using Nightwalk.Core.Config.Settings;
using Nightwalk.Godot.UI.Framework;

namespace Nightwalk.Godot.UI.Settings;

/// <summary>
/// Factory for creating Godot UI controls from setting metadata.
/// </summary>
public static class SettingsControlFactory
{
    /// <summary>
    /// Creates a control row for a setting.
    /// </summary>
    /// <param name="metadata">The setting metadata.</param>
    /// <param name="currentValue">The current value of the setting.</param>
    /// <param name="onChanged">Callback when the value changes.</param>
    /// <returns>A tuple of the row control and a method to update the display value.</returns>
    public static (Control Row, Action<object?> UpdateValue) CreateControlRow(
        SettingMetadata metadata,
        object? currentValue,
        Action<object?> onChanged)
    {
        return metadata.ControlType switch
        {
            SettingsControlType.Toggle => CreateToggleRow(metadata, currentValue, onChanged),
            SettingsControlType.Dropdown => CreateDropdownRow(metadata, currentValue, onChanged),
            SettingsControlType.Slider => CreateSliderRow(metadata, currentValue, onChanged),
            _ => CreateSliderRow(metadata, currentValue, onChanged)
        };
    }

    private static (Control Row, Action<object?> UpdateValue) CreateToggleRow(
        SettingMetadata metadata,
        object? currentValue,
        Action<object?> onChanged)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UITheme.MediumPadding);

        var label = new Label { Text = metadata.DisplayName };
        UITheme.ApplyLabelStyle(label);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);

        var toggle = new CheckButton
        {
            ButtonPressed = currentValue is true
        };
        ApplyCheckButtonStyle(toggle, currentValue is true);
        toggle.Toggled += pressed =>
        {
            UpdateCheckButtonColor(toggle, pressed);
            onChanged(pressed);
        };
        row.AddChild(toggle);

        Action<object?> updateValue = value =>
        {
            var isOn = value is true;
            toggle.SetPressedNoSignal(isOn);
            UpdateCheckButtonColor(toggle, isOn);
        };

        return (WrapWithDescription(row, metadata.Description), updateValue);
    }

    private static (Control Row, Action<object?> UpdateValue) CreateDropdownRow(
        SettingMetadata metadata,
        object? currentValue,
        Action<object?> onChanged)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UITheme.MediumPadding);

        var label = new Label { Text = metadata.DisplayName };
        UITheme.ApplyLabelStyle(label);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);

        var dropdown = new OptionButton
        {
            CustomMinimumSize = new Vector2(140, 0)
        };

        // Track string options for string-based dropdowns
        List<string>? stringOptions = null;

        // Populate options
        if (metadata.PropertyType == typeof(string) && metadata.DisplayName == "Resolution")
        {
            // Special case: Resolution dropdown with dynamic options
            stringOptions = Setup.EnvironmentSetup.GetAvailableResolutions();
            for (int i = 0; i < stringOptions.Count; i++)
            {
                dropdown.AddItem(stringOptions[i], i);
            }

            // Set current selection
            var currentStr = currentValue as string ?? "Default";
            var selectedIdx = stringOptions.IndexOf(currentStr);
            if (selectedIdx >= 0)
            {
                dropdown.Selected = selectedIdx;
            }

            var options = stringOptions; // Capture for closure
            dropdown.ItemSelected += index =>
            {
                onChanged(options[(int)index]);
            };
        }
        else if (metadata.DropdownOptions != null)
        {
            // Options from SettingsOptionsAttribute
            foreach (var option in metadata.DropdownOptions)
            {
                dropdown.AddItem(option.Label, option.Value);
            }

            // Set current selection
            if (currentValue != null)
            {
                var intValue = Convert.ToInt32(currentValue);
                for (int i = 0; i < dropdown.ItemCount; i++)
                {
                    if (dropdown.GetItemId(i) == intValue)
                    {
                        dropdown.Selected = i;
                        break;
                    }
                }
            }

            dropdown.ItemSelected += index =>
            {
                var selectedId = dropdown.GetItemId((int)index);
                onChanged(selectedId);
            };
        }
        else if (metadata.EnumOptions != null)
        {
            // Options from enum type
            int idx = 0;
            foreach (var option in metadata.EnumOptions)
            {
                dropdown.AddItem(option.Label, idx);
                idx++;
            }

            // Set current selection
            if (currentValue != null)
            {
                var enumValues = metadata.EnumOptions.ToList();
                for (int i = 0; i < enumValues.Count; i++)
                {
                    if (enumValues[i].Value.Equals(currentValue))
                    {
                        dropdown.Selected = i;
                        break;
                    }
                }
            }

            dropdown.ItemSelected += index =>
            {
                var enumValue = metadata.EnumOptions.ElementAt((int)index).Value;
                onChanged(enumValue);
            };
        }

        ApplyOptionButtonStyle(dropdown);
        row.AddChild(dropdown);

        Action<object?> updateValue = value =>
        {
            if (stringOptions != null && value is string strValue)
            {
                var idx = stringOptions.IndexOf(strValue);
                if (idx >= 0)
                {
                    dropdown.Selected = idx;
                }
            }
            else if (metadata.DropdownOptions != null && value != null)
            {
                var intValue = Convert.ToInt32(value);
                for (int i = 0; i < dropdown.ItemCount; i++)
                {
                    if (dropdown.GetItemId(i) == intValue)
                    {
                        dropdown.Selected = i;
                        break;
                    }
                }
            }
            else if (metadata.EnumOptions != null && value != null)
            {
                var enumValues = metadata.EnumOptions.ToList();
                for (int i = 0; i < enumValues.Count; i++)
                {
                    if (enumValues[i].Value.Equals(value))
                    {
                        dropdown.Selected = i;
                        break;
                    }
                }
            }
        };

        return (WrapWithDescription(row, metadata.Description), updateValue);
    }

    private static (Control Row, Action<object?> UpdateValue) CreateSliderRow(
        SettingMetadata metadata,
        object? currentValue,
        Action<object?> onChanged)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UITheme.MediumPadding);

        var label = new Label { Text = metadata.DisplayName };
        UITheme.ApplyLabelStyle(label);
        label.CustomMinimumSize = new Vector2(150, 0);
        row.AddChild(label);

        // Determine range
        var min = metadata.Range?.Min ?? 0;
        var max = metadata.Range?.Max ?? 1;
        var step = metadata.Range?.Step ?? 0.01;

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = ConvertToDouble(currentValue),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(150, 0)
        };
        ApplySliderStyle(slider);
        row.AddChild(slider);

        var valueLabel = new Label
        {
            Text = FormatValue(currentValue, metadata.DisplayOptions),
            CustomMinimumSize = new Vector2(60, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        UITheme.ApplyLabelStyle(valueLabel);
        row.AddChild(valueLabel);

        slider.ValueChanged += value =>
        {
            valueLabel.Text = FormatValue(value, metadata.DisplayOptions);
            // Convert back to the original type
            var typedValue = ConvertFromDouble(value, metadata.PropertyType);
            onChanged(typedValue);
        };

        Action<object?> updateValue = value =>
        {
            slider.SetValueNoSignal(ConvertToDouble(value));
            valueLabel.Text = FormatValue(value, metadata.DisplayOptions);
        };

        return (WrapWithDescription(row, metadata.Description), updateValue);
    }

    private static Control WrapWithDescription(Control row, string? description)
    {
        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 2);
        container.AddChild(row);

        if (!string.IsNullOrEmpty(description))
        {
            var descLabel = new Label
            {
                Text = description,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            UITheme.ApplyLabelStyle(descLabel, UITheme.SmallFontSize, UITheme.TextDimColor);
            container.AddChild(descLabel);
        }

        // Add subtle divider
        var divider = new HSeparator();
        divider.AddThemeStyleboxOverride("separator", new StyleBoxLine
        {
            Color = new Color(1f, 1f, 1f, 0.08f),
            Thickness = 1
        });
        divider.AddThemeConstantOverride("separation", 8);
        container.AddChild(divider);

        return container;
    }

    private static double ConvertToDouble(object? value)
    {
        return value switch
        {
            null => 0,
            int i => i,
            float f => f,
            double d => d,
            long l => l,
            _ => 0
        };
    }

    private static object ConvertFromDouble(double value, Type targetType)
    {
        if (targetType == typeof(int))
            return (int)Math.Round(value);
        if (targetType == typeof(float))
            return (float)value;
        if (targetType == typeof(long))
            return (long)Math.Round(value);
        return value;
    }

    private static string FormatValue(object? value, SettingsDisplayOptions? displayOptions)
    {
        var numValue = ConvertToDouble(value);
        var multiplier = displayOptions?.DisplayMultiplier ?? 1.0;
        var displayValue = numValue * multiplier;

        var format = displayOptions?.ValueFormat;
        if (!string.IsNullOrEmpty(format))
        {
            return string.Format(format, displayValue);
        }

        // Default formatting based on the value
        if (Math.Abs(displayValue - Math.Round(displayValue)) < 0.001)
        {
            return $"{displayValue:F0}";
        }
        return $"{displayValue:F1}";
    }

    private static void ApplySliderStyle(HSlider slider)
    {
        var grabber = new StyleBoxFlat
        {
            BgColor = UITheme.AccentColor,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4
        };

        var track = new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.2f, 0.25f),
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };

        slider.AddThemeStyleboxOverride("grabber_area", grabber);
        slider.AddThemeStyleboxOverride("slider", track);
    }

    private static void ApplyOptionButtonStyle(OptionButton option)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.18f, 0.8f),
            BorderColor = UITheme.AccentDimColor,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            ContentMarginLeft = UITheme.SmallPadding,
            ContentMarginRight = UITheme.SmallPadding,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4
        };

        option.AddThemeStyleboxOverride("normal", style);
        option.AddThemeStyleboxOverride("hover", style);
        option.AddThemeStyleboxOverride("pressed", style);
        option.AddThemeColorOverride("font_color", UITheme.TextColor);
        option.AddThemeFontSizeOverride("font_size", UITheme.BodyFontSize);
    }

    private static Texture2D? _checkedIcon;
    private static Texture2D? _uncheckedIcon;

    private static void ApplyCheckButtonStyle(CheckButton check, bool isOn)
    {
        check.AddThemeColorOverride("font_color", UITheme.TextColor);
        check.AddThemeColorOverride("font_pressed_color", UITheme.AccentColor);

        // Create custom toggle icons with proper colors
        _checkedIcon ??= CreateToggleIcon(UITheme.AccentColor, true);
        _uncheckedIcon ??= CreateToggleIcon(new Color(0.4f, 0.4f, 0.45f), false);

        check.AddThemeIconOverride("checked", _checkedIcon);
        check.AddThemeIconOverride("unchecked", _uncheckedIcon);
        check.AddThemeIconOverride("checked_disabled", _checkedIcon);
        check.AddThemeIconOverride("unchecked_disabled", _uncheckedIcon);

        // Remove focus border
        check.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static void UpdateCheckButtonColor(CheckButton check, bool isOn)
    {
        // Icons are already colored, no dynamic update needed
    }

    private static Texture2D CreateToggleIcon(Color color, bool isOn)
    {
        // Create a simple toggle switch icon with anti-aliasing
        var width = 36;
        var height = 18;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

        var trackColor = isOn ? color : new Color(0.25f, 0.25f, 0.28f);
        var knobColor = isOn ? Colors.White : new Color(0.6f, 0.6f, 0.6f);

        // Add 1px margin for anti-aliasing
        var margin = 1f;
        var radius = (height - margin * 2) / 2f;
        var centerY = height / 2f;
        var knobRadius = radius - 2.5f;
        var knobX = isOn ? width - margin - radius - knobRadius : margin + radius - knobRadius;
        var knobCenterX = knobX + knobRadius;
        var knobCenterY = centerY;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Calculate distance to track edge (stadium/capsule shape)
                float trackDist;
                var leftCapX = margin + radius;
                var rightCapX = width - margin - radius;

                if (x < leftCapX)
                {
                    var dx = x - leftCapX;
                    var dy = y - centerY;
                    trackDist = radius - Mathf.Sqrt(dx * dx + dy * dy);
                }
                else if (x > rightCapX)
                {
                    var dx = x - rightCapX;
                    var dy = y - centerY;
                    trackDist = radius - Mathf.Sqrt(dx * dx + dy * dy);
                }
                else
                {
                    // Middle section - distance to top/bottom edge
                    trackDist = radius - Mathf.Abs(y - centerY);
                }

                // Calculate distance to knob edge
                var knobDx = x - knobCenterX;
                var knobDy = y - knobCenterY;
                var knobDist = knobRadius - Mathf.Sqrt(knobDx * knobDx + knobDy * knobDy);

                // Anti-aliased blending
                var trackAlpha = Mathf.Clamp(trackDist + 0.5f, 0f, 1f);
                var knobAlpha = Mathf.Clamp(knobDist + 0.5f, 0f, 1f);

                if (trackAlpha > 0)
                {
                    var finalColor = trackColor;
                    finalColor.A = trackAlpha;

                    if (knobAlpha > 0)
                    {
                        // Blend knob over track
                        finalColor = knobColor;
                        finalColor.A = knobAlpha;
                        if (knobAlpha < 1 && trackAlpha > 0)
                        {
                            // Partial knob coverage - blend with track
                            finalColor = trackColor.Lerp(knobColor, knobAlpha);
                            finalColor.A = Mathf.Max(trackAlpha, knobAlpha);
                        }
                    }

                    image.SetPixel(x, y, finalColor);
                }
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }
}
