using Godot;

namespace Nightwalk.Godot.UI.HUD;

/// <summary>
/// Segmented exposure indicator for Resonance mode.
/// Pulses continuously while any exposure exists (danger state).
/// </summary>
public sealed partial class ExposureIndicator : SegmentedIndicator
{
    // Red colors for exposure/danger
    protected override Color FullColor => new(1f, 0.2f, 0.2f, 0.9f);
    protected override Color FullBorderColor => new(1f, 0.4f, 0.3f, 0.9f);
    protected override Color EmptyColor => new(0.15f, 0.08f, 0.08f, 0.4f);
    protected override Color EmptyBorderColor => new(0.3f, 0.15f, 0.15f, 0.3f);
    protected override Color GlowColor => new(1f, 0.2f, 0.1f, 0.4f);
    protected override float PulseSpeed => 6f;

    // Pulse whenever there's any exposure
    protected override bool ShouldPulse => CurrentValue > 0;
}
