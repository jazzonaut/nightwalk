using System;
using Godot;

namespace Nightwalk.Godot.UI.HUD;

/// <summary>
/// Animated score popup with float-up motion and cyberpunk glitch effects.
/// Displays score changes with neon styling. Supports object pooling via OnAnimationComplete event.
/// </summary>
public sealed partial class ScorePopup : Control
{
    /// <summary>
    /// Fired when the animation completes, allowing the popup to be returned to a pool.
    /// </summary>
    public event Action<ScorePopup>? OnAnimationComplete;
    // Animation parameters
    private const float FloatDistance = 60f;
    private const float AnimationDuration = 5.0f;
    private const float FadeStartPercent = 0.6f;

    // Glitch parameters
    private const float GlitchChance = 0.15f;
    private const float GlitchDuration = 0.05f;
    private const float ChromaticOffset = 3f;

    // Colors (cyberpunk neon)
    private static readonly Color PositiveColor = new(0.2f, 1f, 0.4f);      // Neon green
    private static readonly Color NegativeColor = new(1f, 0.2f, 0.5f);      // Neon magenta/red
    private static readonly Color GlitchColorR = new(1f, 0.3f, 0.3f, 0.5f); // Red channel
    private static readonly Color GlitchColorB = new(0.3f, 0.3f, 1f, 0.5f); // Blue channel

    private Label _mainLabel = null!;
    private Label _glitchLabelR = null!;
    private Label _glitchLabelB = null!;

    private float _elapsed;
    private float _glitchTimer;
    private bool _isGlitching;
    private Vector2 _startPosition;
    private bool _isNegative;
    private Color _baseColor;
    private RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        // Create chromatic aberration labels (behind main label)
        _glitchLabelR = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false
        };
        AddChild(_glitchLabelR);

        _glitchLabelB = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false
        };
        AddChild(_glitchLabelB);

        // Create main label (on top)
        _mainLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        AddChild(_mainLabel);

        _rng.Randomize();
    }

    /// <summary>
    /// Initializes the popup with score value and starts animation.
    /// </summary>
    public void Initialize(long points, Vector2 position)
    {
        _isNegative = points < 0;
        _startPosition = position;
        Position = position;
        _elapsed = 0f;

        // Reset glitch state to prevent carry-over from pooled instances
        _glitchTimer = 0f;
        _isGlitching = false;
        _glitchLabelR.Visible = false;
        _glitchLabelB.Visible = false;
        _mainLabel.Position = Vector2.Zero;

        // Format text
        string text = _isNegative ? points.ToString("N0") : $"+{points:N0}";
        _mainLabel.Text = text;
        _glitchLabelR.Text = text;
        _glitchLabelB.Text = text;

        // Apply colors
        _baseColor = _isNegative ? NegativeColor : PositiveColor;
        HUDTheme.ApplyStyle(_mainLabel, HUDTheme.MediumFontSize, _baseColor);
        HUDTheme.ApplyStyle(_glitchLabelR, HUDTheme.MediumFontSize, GlitchColorR);
        HUDTheme.ApplyStyle(_glitchLabelB, HUDTheme.MediumFontSize, GlitchColorB);

        // Size the control based on label content so it renders properly
        Vector2 labelSize = _mainLabel.GetMinimumSize();
        CustomMinimumSize = labelSize;
        Size = labelSize;

        // Center labels within the control
        _mainLabel.Size = labelSize;
        _glitchLabelR.Size = labelSize;
        _glitchLabelB.Size = labelSize;

        // Enable processing (may have been disabled when returned to pool)
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _elapsed += dt;

        if (_elapsed >= AnimationDuration)
        {
            // Notify pool handler instead of destroying (supports object pooling)
            OnAnimationComplete?.Invoke(this);
            return;
        }

        float progress = _elapsed / AnimationDuration;

        // Float upward with easing
        float floatOffset = FloatDistance * EaseOutCubic(progress);
        Position = _startPosition - new Vector2(0, floatOffset);

        // Fade out in latter portion
        float alpha = 1f;
        if (progress > FadeStartPercent)
        {
            float fadeProgress = (progress - FadeStartPercent) / (1f - FadeStartPercent);
            alpha = 1f - EaseInCubic(fadeProgress);
        }
        Modulate = new Color(1, 1, 1, alpha);

        // Glitch effect
        UpdateGlitch(dt);
    }

    private void UpdateGlitch(float delta)
    {
        _glitchTimer -= delta;

        if (_glitchTimer <= 0)
        {
            // Roll for new glitch (negative scores glitch more frequently)
            float glitchProbability = _isNegative ? GlitchChance * 3f : GlitchChance;
            if (_rng.Randf() < glitchProbability)
            {
                _isGlitching = true;
                _glitchTimer = GlitchDuration + _rng.RandfRange(0, 0.03f);

                // Random chromatic aberration offsets
                float offsetX = _rng.RandfRange(-ChromaticOffset, ChromaticOffset);
                float offsetY = _rng.RandfRange(-ChromaticOffset * 0.3f, ChromaticOffset * 0.3f);
                _glitchLabelR.Position = new Vector2(-offsetX, -offsetY);
                _glitchLabelB.Position = new Vector2(offsetX, offsetY);
            }
            else
            {
                _isGlitching = false;
            }
        }

        _glitchLabelR.Visible = _isGlitching;
        _glitchLabelB.Visible = _isGlitching;

        // Horizontal jitter during glitch
        if (_isGlitching)
        {
            _mainLabel.Position = new Vector2(_rng.RandfRange(-2, 2), 0);
        }
        else
        {
            _mainLabel.Position = Vector2.Zero;
        }
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t) => t * t * t;
}
