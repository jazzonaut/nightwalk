using Godot;

namespace Nightwalk.Godot.UI.HUD;

/// <summary>
/// Segmented fuel indicator for jetpack energy.
/// Pulses when fuel is low.
/// </summary>
public sealed partial class FuelIndicator : SegmentedIndicator
{
    private const float LowFuelThreshold = 0.33f;

    protected override Color FullColor => HUDTheme.AccentColor;
    protected override Color FullBorderColor => new(0.4f, 1f, 1f, 0.9f);
    protected override Color EmptyColor => new(0.15f, 0.15f, 0.15f, 0.4f);
    protected override Color EmptyBorderColor => new(0.3f, 0.3f, 0.3f, 0.3f);
    protected override Color GlowColor => new(0f, 0.8f, 0.8f, 0.3f);
    protected override float PulseSpeed => 6f;

    protected override bool ShouldPulse => CurrentValue <= LowFuelThreshold && CurrentValue > 0;

    /// <summary>
    /// Updates the fuel display.
    /// </summary>
    public void UpdateEnergy(float energyPercent)
    {
        UpdateValue(energyPercent);
    }

    public override void Reset()
    {
        base.Reset();
        UpdateValue(1f); // Fuel starts full
    }
}
