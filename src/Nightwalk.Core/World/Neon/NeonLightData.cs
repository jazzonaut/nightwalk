namespace Nightwalk.Core.World.Neon;

/// <summary>
/// Neon light shape types.
/// </summary>
public enum NeonShape
{
    /// <summary>Horizontal bar across facade.</summary>
    HorizontalBar,
    /// <summary>Vertical strip on building edge.</summary>
    VerticalStrip,
    /// <summary>Outline around window or section.</summary>
    Outline,
    /// <summary>Sign-like rectangle.</summary>
    Sign,
    /// <summary>Large rectangular sign on upper building facade.</summary>
    LargeSign,
    /// <summary>Vertical strip along building corner (L/T/Cross/Bracket shapes).</summary>
    CornerStrip,
    /// <summary>Strip running full height of building.</summary>
    FullHeightStrip,
    /// <summary>Sign with procedural kanji-like glyphs.</summary>
    KanjiSign,
    /// <summary>Large billboard with animated patterns.</summary>
    Billboard
}

/// <summary>
/// Data for a neon light placed on a building.
/// </summary>
public sealed class NeonLightData
{
    /// <summary>World X position.</summary>
    public float X { get; set; }

    /// <summary>World Y position (height).</summary>
    public float Y { get; set; }

    /// <summary>World Z position.</summary>
    public float Z { get; set; }

    /// <summary>Width of the neon element.</summary>
    public float Width { get; set; }

    /// <summary>Height of the neon element.</summary>
    public float Height { get; set; }

    /// <summary>Neon shape type.</summary>
    public NeonShape Shape { get; set; }

    /// <summary>Color as hex string (e.g., "#00FFFF").</summary>
    public string Color { get; set; } = "#00FFFF";

    /// <summary>Emission intensity.</summary>
    public float Intensity { get; set; }

    /// <summary>Rotation around Y axis in radians (for facade alignment).</summary>
    public float RotationY { get; set; }

    /// <summary>Seed for procedural variation (used by kanji/billboard shaders).</summary>
    public int Seed { get; set; }

    /// <summary>Secondary color for billboard signs.</summary>
    public string? SecondaryColor { get; set; }

    /// <summary>Whether this sign is broken/malfunctioning.</summary>
    public bool IsBroken { get; set; }

    /// <summary>Severity of broken state (0.0-1.0).</summary>
    public float BrokenIntensity { get; set; }

    /// <summary>Number of parallax layers (1-3).</summary>
    public int ParallaxLayers { get; set; } = 1;

    /// <summary>Total depth offset for parallax effect.</summary>
    public float ParallaxDepth { get; set; }

    /// <summary>Style for corner strips (0=L, 1=T, 2=Cross, 3=Bracket).</summary>
    public int CornerStyle { get; set; }
}
