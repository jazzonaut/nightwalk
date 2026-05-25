namespace Nightwalk.Core.Config;

/// <summary>
/// Configuration for player tools (flashlights, zipline, etc.)
/// </summary>
public sealed class ToolsConfig
{
    public SpotLightToolConfig Spotlight { get; set; } = null!;
    public ZiplineConfig Zipline { get; set; } = null!;
    public DecoyToolConfig Decoy { get; set; } = null!;
}

/// <summary>
/// Configuration for the decoy tool (laser pointer).
/// </summary>
public sealed class DecoyToolConfig
{
    /// <summary>Maximum range of the laser pointer raycast.</summary>
    public float Range { get; set; }

    /// <summary>Seconds to hold on a surface to deploy decoy.</summary>
    public float ChargeTime { get; set; }

    /// <summary>Tolerance for aim drift before charge resets.</summary>
    public float AimTolerance { get; set; }

    /// <summary>Radius of the laser dot on surfaces.</summary>
    public float DotRadius { get; set; }

    /// <summary>Hex color for the laser dot.</summary>
    public string DotColor { get; set; } = null!;

    /// <summary>Emission intensity of the laser dot.</summary>
    public float DotEmission { get; set; }
}

/// <summary>
/// Base configuration shared by all light tools.
/// </summary>
public class BaseLightToolConfig
{
    public string Color { get; set; } = null!;
    public float Energy { get; set; }
    public float Range { get; set; }
    public float Attenuation { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float OffsetZ { get; set; }
}

/// <summary>
/// Configuration for spotlight-type tools (SpotLight3D).
/// </summary>
public sealed class SpotLightToolConfig : BaseLightToolConfig
{
    public float SpotAngle { get; set; }
}
