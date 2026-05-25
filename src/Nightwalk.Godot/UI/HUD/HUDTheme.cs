using Godot;

namespace Nightwalk.Godot.UI.HUD;

/// <summary>
/// Shared styling constants for HUD elements.
/// </summary>
public static class HUDTheme
{
    // Colors
    public static readonly Color AccentColor = new(0f, 1f, 1f, 1f);       // Cyan
    public static readonly Color TextColor = new(1f, 1f, 1f, 0.9f);       // White
    public static readonly Color DimTextColor = new(1f, 1f, 1f, 0.6f);    // Dimmed white
    public static readonly Color ShadowColor = new(0f, 0f, 0f, 0.7f);
    public static readonly Color ScoreColor = new(0.2f, 0.6f, 1f, 1f);     // Neon blue
    public static readonly Color ComboColor = new(1f, 0.5f, 0f, 1f);      // Orange

    // Font sizes
    public const int LargeFontSize = 28;
    public const int MediumFontSize = 20;
    public const int SmallFontSize = 16;

    // Spacing
    public const int EdgeMargin = 16;
    public const int ElementSpacing = 8;

    // Shadow
    public const int ShadowOffsetX = 1;
    public const int ShadowOffsetY = 1;

    // Animation
    public const float FadeDuration = 1.0f;
    public const float DisplayDuration = 3.0f;

    // Layer
    public const int HUDLayer = 101;

    /// <summary>
    /// Applies standard HUD label styling.
    /// </summary>
    public static void ApplyStyle(Label label, int fontSize, Color color)
    {
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", ShadowColor);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeConstantOverride("shadow_offset_x", ShadowOffsetX);
        label.AddThemeConstantOverride("shadow_offset_y", ShadowOffsetY);
    }

    /// <summary>
    /// Sets the alpha of a label's font color.
    /// </summary>
    public static void SetLabelAlpha(Label label, float alpha, Color baseColor)
    {
        var color = baseColor;
        color.A = alpha;
        label.AddThemeColorOverride("font_color", color);
    }
}
