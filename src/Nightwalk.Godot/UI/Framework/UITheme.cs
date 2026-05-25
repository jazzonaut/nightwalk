using Godot;

namespace Nightwalk.Godot.UI.Framework;

/// <summary>
/// Centralized styling constants for consistent UI appearance.
/// </summary>
public static class UITheme
{
    // Colors
    public static readonly Color AccentColor = new(0f, 1f, 1f);           // Cyan
    public static readonly Color AccentDimColor = new(0f, 0.7f, 0.7f);    // Dimmed cyan
    public static readonly Color TextColor = new(1f, 1f, 1f, 0.9f);       // White with slight transparency
    public static readonly Color TextDimColor = new(1f, 1f, 1f, 0.5f);    // Dimmed white
    public static readonly Color BackgroundColor = new(0.05f, 0.05f, 0.08f, 0.75f); // Near-black
    public static readonly Color PanelColor = new(0.1f, 0.1f, 0.12f, 0.9f);
    public static readonly Color ShadowColor = new(0f, 0f, 0f, 0.7f);

    // Font sizes
    public const int TitleFontSize = 48;
    public const int SubheadingFontSize = 32;
    public const int HeadingFontSize = 24;
    public const int BodyFontSize = 18;
    public const int SmallFontSize = 14;

    // Spacing
    public const int LargePadding = 32;
    public const int MediumPadding = 16;
    public const int SmallPadding = 8;
    public const int ButtonSpacing = 12;

    // Button dimensions
    public const int ButtonMinWidth = 300;
    public const int ButtonMinHeight = 44;

    // Shadow
    public const int ShadowOffsetX = 2;
    public const int ShadowOffsetY = 2;

    // Animation durations (seconds)
    public const float FadeInDuration = 0.2f;
    public const float FadeOutDuration = 0.15f;
    public const float TransitionDuration = 0.3f;

    /// <summary>
    /// Applies standard text styling to a label.
    /// </summary>
    public static void ApplyLabelStyle(Label label, int fontSize = BodyFontSize, Color? color = null)
    {
        label.AddThemeColorOverride("font_color", color ?? TextColor);
        label.AddThemeColorOverride("font_shadow_color", ShadowColor);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeConstantOverride("shadow_offset_x", ShadowOffsetX);
        label.AddThemeConstantOverride("shadow_offset_y", ShadowOffsetY);
    }

    /// <summary>
    /// Applies standard button styling.
    /// </summary>
    public static void ApplyButtonStyle(Button button)
    {
        // Create flat style for normal state
        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.18f, 0.8f),
            BorderColor = AccentDimColor,
            BorderWidthBottom = 2,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            ContentMarginLeft = MediumPadding,
            ContentMarginRight = MediumPadding,
            ContentMarginTop = SmallPadding,
            ContentMarginBottom = SmallPadding,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4
        };

        // Hover style
        var hoverStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.2f, 0.25f, 0.9f),
            BorderColor = AccentColor,
            BorderWidthBottom = 2,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            ContentMarginLeft = MediumPadding,
            ContentMarginRight = MediumPadding,
            ContentMarginTop = SmallPadding,
            ContentMarginBottom = SmallPadding,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4
        };

        // Pressed style
        var pressedStyle = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0.3f, 0.3f, 0.9f),
            BorderColor = AccentColor,
            BorderWidthBottom = 2,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            ContentMarginLeft = MediumPadding,
            ContentMarginRight = MediumPadding,
            ContentMarginTop = SmallPadding,
            ContentMarginBottom = SmallPadding,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4
        };

        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
        button.AddThemeStyleboxOverride("pressed", pressedStyle);
        button.AddThemeStyleboxOverride("focus", hoverStyle);

        button.AddThemeColorOverride("font_color", TextColor);
        button.AddThemeColorOverride("font_hover_color", AccentColor);
        button.AddThemeColorOverride("font_pressed_color", AccentColor);
        button.AddThemeFontSizeOverride("font_size", BodyFontSize);
    }

    /// <summary>
    /// Creates a styled panel background.
    /// </summary>
    public static StyleBoxFlat CreatePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = PanelColor,
            BorderColor = AccentDimColor,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            ContentMarginLeft = MediumPadding,
            ContentMarginRight = MediumPadding,
            ContentMarginTop = MediumPadding,
            ContentMarginBottom = MediumPadding,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
    }
}
