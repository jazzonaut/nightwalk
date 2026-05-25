namespace Nightwalk.Core.Config;

/// <summary>
/// Configuration for the zipline tool.
/// </summary>
public sealed class ZiplineConfig
{
    // Limits
    /// <summary>
    /// Maximum number of ziplines that can exist at once. 0 = unlimited.
    /// </summary>
    public int MaxZiplines { get; set; }

    /// <summary>
    /// Score cost to place a zipline. Deducted when both anchors are placed.
    /// </summary>
    public int PlacementCost { get; set; }

    // Placement
    /// <summary>
    /// Maximum raycast distance for placement.
    /// </summary>
    public float MaxDistance { get; set; }

    /// <summary>
    /// Minimum distance between anchor points.
    /// </summary>
    public float MinCableLength { get; set; }

    // Removal highlighting
    /// <summary>
    /// Color to use when highlighting a zipline for removal.
    /// </summary>
    public string HighlightColor { get; set; } = null!;

    /// <summary>
    /// Maximum raycast distance for hover detection on existing ziplines.
    /// </summary>
    public float HighlightDistance { get; set; }

    // Preview
    /// <summary>
    /// Radius of the preview dot.
    /// </summary>
    public float PreviewDotRadius { get; set; }

    /// <summary>
    /// Color of the preview dot.
    /// </summary>
    public string PreviewColor { get; set; } = null!;

    /// <summary>
    /// Emission intensity of the preview.
    /// </summary>
    public float PreviewEmission { get; set; }

    /// <summary>
    /// Color when placement is valid.
    /// </summary>
    public string ValidPlacementColor { get; set; } = null!;

    /// <summary>
    /// Color when placement is obstructed.
    /// </summary>
    public string ObstructedPlacementColor { get; set; } = null!;

    /// <summary>
    /// Color when placement is invalid (out of range).
    /// </summary>
    public string InvalidPlacementColor { get; set; } = null!;

    // Anchor
    /// <summary>
    /// Radius of anchor point spheres.
    /// </summary>
    public float AnchorRadius { get; set; }

    /// <summary>
    /// Color of anchor points.
    /// </summary>
    public string AnchorColor { get; set; } = null!;

    /// <summary>
    /// Emission intensity of anchor points.
    /// </summary>
    public float AnchorEmission { get; set; }

    /// <summary>
    /// Light energy of anchor glow.
    /// </summary>
    public float AnchorLightEnergy { get; set; }

    /// <summary>
    /// Range of anchor glow light.
    /// </summary>
    public float AnchorLightRange { get; set; }


    /// <summary>
    /// Emission intensity of the cable.
    /// </summary>
    public float CableEmission { get; set; }

    /// <summary>
    /// Thickness of the cable.
    /// </summary>
    public float CableThickness { get; set; }

    /// <summary>
    /// Maximum sag at cable midpoint.
    /// </summary>
    public float CableSag { get; set; }

    // Riding - Attachment
    /// <summary>
    /// Maximum distance from cable to auto-attach.
    /// </summary>
    public float AttachmentDistance { get; set; }

    /// <summary>
    /// Maximum distance for jump-transfer to a highlighted zipline.
    /// </summary>
    public float TransferDistance { get; set; }

    /// <summary>
    /// Duration in seconds to blend onto a zipline when attaching/transferring.
    /// </summary>
    public float AttachTransitionDuration { get; set; }

    /// <summary>
    /// Distance in meters near each anchor to allow holding still.
    /// </summary>
    public float AnchorHoldDistance { get; set; }

    /// <summary>
    /// Offset in meters away from anchors to hold the player when stopped.
    /// </summary>
    public float AnchorHoldOffset { get; set; }

    // Riding - Gravity sliding
    /// <summary>
    /// Acceleration when sliding downhill (m/s^2).
    /// </summary>
    public float GravitySlideAcceleration { get; set; }

    /// <summary>
    /// Maximum sliding velocity.
    /// </summary>
    public float MaxSlideSpeed { get; set; }

    /// <summary>
    /// Speed multiplier when holding run on a zipline.
    /// </summary>
    public float RunSpeedMultiplier { get; set; }

    // Riding - Climbing
    /// <summary>
    /// Base climbing speed when moving against gravity.
    /// </summary>
    public float ClimbSpeed { get; set; }

    /// <summary>
    /// How fast player reaches climb speed.
    /// </summary>
    public float ClimbAcceleration { get; set; }

    /// <summary>
    /// Minimum climb efficiency at steep slopes (0.0-1.0).
    /// </summary>
    public float MinClimbEfficiency { get; set; }

    // Riding - Detachment
    /// <summary>
    /// Upward velocity when jumping off zipline.
    /// </summary>
    public float DetachJumpForce { get; set; }

    /// <summary>
    /// Cooldown in seconds before player can re-attach after detaching.
    /// </summary>
    public float DetachCooldown { get; set; }
}
