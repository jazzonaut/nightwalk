using Nightwalk.Core.Config.Attributes;

namespace Nightwalk.Core.Config;

/// <summary>
/// Physics and movement configuration.
/// </summary>
public sealed class PhysicsConfig
{
    public MovementConfig Movement { get; set; } = null!;
    public JumpConfig Jump { get; set; } = null!;
    public JetpackConfig Jetpack { get; set; } = null!;
    public FlightConfig Flight { get; set; } = null!;
}

public sealed class MovementConfig
{
    public float WalkSpeed { get; set; }
    public float RunSpeed { get; set; }
    public float Acceleration { get; set; }
    public float Friction { get; set; }
    public float Gravity { get; set; }

    [UserConfigurable(SettingsCategory.Controls, SettingsControlType.Slider, Order = 10)]
    [SettingsRange(0.0005, 0.01, Step = 0.0005)]
    [SettingsDisplay(DisplayName = "Mouse Sensitivity", ValueFormat = "{0:F1}x", DisplayMultiplier = 500, Description = "Camera rotation speed when moving the mouse")]
    public float MouseSensitivity { get; set; }
}

public sealed class JumpConfig
{
    public float Force { get; set; }
}

public sealed class JetpackConfig
{
    public float AscendForce { get; set; }
    public float MaxVerticalSpeed { get; set; }
    public float HoverGravityScale { get; set; }
    public float EnergyCostPerSecond { get; set; }
}

public sealed class FlightConfig
{
    public float Speed { get; set; }
    public float SprintMultiplier { get; set; }
    public float BaseFov { get; set; }
    public float MaxFovIncrease { get; set; }
    public float ScoreDrainRate { get; set; }
}
