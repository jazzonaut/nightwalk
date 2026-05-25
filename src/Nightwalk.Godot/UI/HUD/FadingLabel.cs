using Godot;

namespace Nightwalk.Godot.UI.HUD;

/// <summary>
/// A label that can show text temporarily with automatic fade-out.
/// </summary>
public sealed partial class FadingLabel : Label
{
    private float _displayTimer;
    private float _fadeDuration = HUDTheme.FadeDuration;
    private float _displayDuration = HUDTheme.DisplayDuration;
    private Color _baseColor = HUDTheme.AccentColor;
    private int _fontSize = HUDTheme.MediumFontSize;
    private bool _persistent;

    /// <summary>
    /// If true, label stays visible without fading.
    /// </summary>
    public bool Persistent
    {
        get => _persistent;
        set
        {
            _persistent = value;
            if (_persistent)
            {
                HUDTheme.SetLabelAlpha(this, 1f, _baseColor);
            }
        }
    }

    /// <summary>
    /// The base color to use (alpha will be modified during fade).
    /// </summary>
    public Color BaseColor
    {
        get => _baseColor;
        set
        {
            _baseColor = value;
            HUDTheme.SetLabelAlpha(this, Modulate.A, _baseColor);
        }
    }

    /// <summary>
    /// How long the label stays fully visible before fading.
    /// </summary>
    public float DisplayDuration
    {
        get => _displayDuration;
        set => _displayDuration = value;
    }

    /// <summary>
    /// How long the fade-out takes.
    /// </summary>
    public float FadeDuration
    {
        get => _fadeDuration;
        set => _fadeDuration = value;
    }

    /// <summary>
    /// Font size for the label.
    /// </summary>
    public int FontSize
    {
        get => _fontSize;
        set => _fontSize = value;
    }

    public override void _Ready()
    {
        HUDTheme.ApplyStyle(this, _fontSize, _baseColor);
    }

    public override void _Process(double delta)
    {
        if (_persistent) return;

        if (_displayTimer > 0)
        {
            _displayTimer -= (float)delta;

            if (_displayTimer <= _fadeDuration)
            {
                float alpha = _displayTimer / _fadeDuration;
                HUDTheme.SetLabelAlpha(this, alpha, _baseColor);
            }

            if (_displayTimer <= 0)
            {
                Text = "";
            }
        }
    }

    /// <summary>
    /// Shows text temporarily (will fade out).
    /// </summary>
    public void ShowTemporary(string text)
    {
        Text = text;
        HUDTheme.SetLabelAlpha(this, 1f, _baseColor);
        _displayTimer = _displayDuration + _fadeDuration;
    }

    /// <summary>
    /// Shows text and keeps it visible (no fade).
    /// </summary>
    public void ShowPersistent(string text)
    {
        Text = text;
        HUDTheme.SetLabelAlpha(this, 1f, _baseColor);
        _persistent = true;
    }

    /// <summary>
    /// Hides the label immediately.
    /// </summary>
    public new void Hide()
    {
        Text = "";
        _displayTimer = 0;
    }
}
