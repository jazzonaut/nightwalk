using Godot;

namespace Nightwalk.Godot.UI.HUD;

/// <summary>
/// Segmented alert indicator for Resonance mode.
/// Pulses when alert is high.
/// </summary>
public sealed partial class AlertIndicator : SegmentedIndicator
{
    private const float HighAlertThreshold = 0.7f;

    // Orange/amber colors for alert
    protected override Color FullColor => new(1f, 0.6f, 0.1f, 0.9f);
    protected override Color FullBorderColor => new(1f, 0.7f, 0.2f, 0.9f);
    protected override Color EmptyColor => new(0.15f, 0.12f, 0.08f, 0.4f);
    protected override Color EmptyBorderColor => new(0.3f, 0.25f, 0.15f, 0.3f);
    protected override Color GlowColor => new(1f, 0.5f, 0f, 0.3f);
    protected override float PulseSpeed => 4f;

    protected override bool ShouldPulse => CurrentValue >= HighAlertThreshold;
}
