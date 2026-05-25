using Godot;

namespace Nightwalk.Godot.UI.HUD;

/// <summary>
/// Base class for segmented bar indicators (fuel, alert, exposure).
/// Displays blocks that fill/empty based on a value from 0 to 1.
/// Can pulse when in an active state.
/// </summary>
public abstract partial class SegmentedIndicator : Control
{
    // Layout constants
    protected const int BlockCount = 8;
    protected const int BlockWidth = 10;
    protected const int BlockHeight = 14;
    protected const int BlockGap = 3;
    protected const int CornerRadius = 2;
    protected const int BorderWidth = 1;

    // Animation constants
    protected const float PulseMinAlpha = 0.4f;
    protected const float PulseMaxAlpha = 1.0f;
    private const float AlphaChangeThreshold = 0.01f;

    private readonly Panel[] _blocks = new Panel[BlockCount];
    private readonly StyleBoxFlat[] _styles = new StyleBoxFlat[BlockCount];

    private float _pulseTime;
    private float _lastPulseAlpha = 1f;

    /// <summary>
    /// Current value from 0 to 1.
    /// </summary>
    public float CurrentValue { get; private set; }

    /// <summary>
    /// Color for filled blocks.
    /// </summary>
    protected abstract Color FullColor { get; }

    /// <summary>
    /// Border color for filled blocks.
    /// </summary>
    protected abstract Color FullBorderColor { get; }

    /// <summary>
    /// Color for empty blocks.
    /// </summary>
    protected abstract Color EmptyColor { get; }

    /// <summary>
    /// Border color for empty blocks.
    /// </summary>
    protected abstract Color EmptyBorderColor { get; }

    /// <summary>
    /// Shadow color for filled blocks (for glow effect).
    /// </summary>
    protected abstract Color GlowColor { get; }

    /// <summary>
    /// Pulse animation speed (Hz).
    /// </summary>
    protected virtual float PulseSpeed => 4f;

    /// <summary>
    /// Override to determine when pulsing should be active.
    /// </summary>
    protected abstract bool ShouldPulse { get; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(
            BlockCount * BlockWidth + (BlockCount - 1) * BlockGap,
            BlockHeight
        );

        var container = new HBoxContainer();
        container.AddThemeConstantOverride("separation", BlockGap);
        AddChild(container);

        for (int i = 0; i < BlockCount; i++)
        {
            _styles[i] = new StyleBoxFlat
            {
                BgColor = EmptyColor,
                BorderColor = EmptyBorderColor,
                BorderWidthTop = BorderWidth,
                BorderWidthBottom = BorderWidth,
                BorderWidthLeft = BorderWidth,
                BorderWidthRight = BorderWidth,
                CornerRadiusTopLeft = CornerRadius,
                CornerRadiusTopRight = CornerRadius,
                CornerRadiusBottomLeft = CornerRadius,
                CornerRadiusBottomRight = CornerRadius,
                ShadowColor = Colors.Transparent,
                ShadowSize = 0,
                ShadowOffset = new Vector2(0, 1)
            };

            _blocks[i] = new Panel
            {
                CustomMinimumSize = new Vector2(BlockWidth, BlockHeight)
            };
            _blocks[i].AddThemeStyleboxOverride("panel", _styles[i]);
            container.AddChild(_blocks[i]);
        }
    }

    public override void _Process(double delta)
    {
        if (ShouldPulse)
        {
            _pulseTime += (float)delta * PulseSpeed;
            float alpha = Mathf.Lerp(PulseMinAlpha, PulseMaxAlpha,
                (Mathf.Sin(_pulseTime * Mathf.Tau) + 1f) / 2f);

            if (Mathf.Abs(alpha - _lastPulseAlpha) > AlphaChangeThreshold)
            {
                ApplyPulseAlpha(alpha);
                _lastPulseAlpha = alpha;
            }
        }
    }

    /// <summary>
    /// Updates the indicator display.
    /// </summary>
    /// <param name="value">Value from 0 to 1.</param>
    public void UpdateValue(float value)
    {
        float previousValue = CurrentValue;

        // Capture pulse state BEFORE updating CurrentValue (since ShouldPulse reads CurrentValue)
        bool wasPulsing = ShouldPulse;

        CurrentValue = Mathf.Clamp(value, 0f, 1f);
        int fullBlocks = Mathf.RoundToInt(CurrentValue * BlockCount);

        OnValueChanged(previousValue, CurrentValue);

        // Reset pulse phase when transitioning out of pulsing state
        if (wasPulsing && !ShouldPulse)
        {
            _pulseTime = 0f;
            _lastPulseAlpha = PulseMaxAlpha;
            // Restore full alpha on filled blocks
            ApplyPulseAlpha(PulseMaxAlpha);
        }

        for (int i = 0; i < BlockCount; i++)
        {
            bool isFull = i < fullBlocks;
            var style = _styles[i];

            if (isFull)
            {
                style.BgColor = FullColor;
                style.BorderColor = FullBorderColor;
                style.ShadowColor = GlowColor;
                style.ShadowSize = 2;
            }
            else
            {
                style.BgColor = EmptyColor;
                style.BorderColor = EmptyBorderColor;
                style.ShadowColor = Colors.Transparent;
                style.ShadowSize = 0;
            }
        }
    }

    /// <summary>
    /// Called when value changes. Override for custom behavior.
    /// </summary>
    protected virtual void OnValueChanged(float previousValue, float newValue) { }

    private void ApplyPulseAlpha(float alpha)
    {
        int fullBlocks = Mathf.RoundToInt(CurrentValue * BlockCount);
        for (int i = 0; i < fullBlocks; i++)
        {
            var style = _styles[i];

            var bgColor = FullColor;
            bgColor.A = alpha;
            style.BgColor = bgColor;

            var borderColor = FullBorderColor;
            borderColor.A = alpha;
            style.BorderColor = borderColor;

            var glowColor = GlowColor;
            glowColor.A = alpha * 0.4f;
            style.ShadowColor = glowColor;
            style.ShadowSize = (int)(2 + alpha * 2);
        }
    }

    /// <summary>
    /// Resets the indicator to initial state.
    /// </summary>
    public virtual void Reset()
    {
        CurrentValue = 0f;
        _pulseTime = 0f;
        _lastPulseAlpha = 1f;
        UpdateValue(0f);
    }
}
