using System;
using Godot;

namespace Nightwalk.Godot.UI.Framework;

/// <summary>
/// Base class for full-screen UI panels (menus, overlays).
/// Provides common functionality for showing/hiding with animations.
/// </summary>
public abstract partial class UIScreen : CanvasLayer
{
    /// <summary>
    /// Fired when the screen has finished showing.
    /// </summary>
    public event Action? OnShown;

    /// <summary>
    /// Fired when the screen has finished hiding.
    /// </summary>
    public event Action? OnHidden;

    /// <summary>
    /// The root control that contains all UI elements.
    /// </summary>
    protected Control Root { get; private set; } = null!;

    /// <summary>
    /// Whether this screen blocks input to screens below it.
    /// </summary>
    public virtual bool BlocksInput => true;

    /// <summary>
    /// Whether this screen should pause the game when shown.
    /// </summary>
    public virtual bool PausesGame => false;

    /// <summary>
    /// Preferred background mode for this screen.
    /// </summary>
    public virtual MenuBackgroundMode BackgroundMode => MenuBackgroundMode.Shader;

    private bool _isVisible;
    private Tween? _activeTween;

    public override void _Ready()
    {
        // Set high layer to render above game (but below navigator's background layer)
        Layer = 110;

        // Create full-screen root container
        Root = new Control
        {
            Name = "ScreenRoot"
        };
        Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Root.MouseFilter = BlocksInput ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
        AddChild(Root);

        // Note: Background overlay is managed by UINavigator to prevent fade flickering
        // when navigating between screens

        // Let derived classes build their content
        BuildContent(Root);

        // Start hidden
        Root.Modulate = new Color(1, 1, 1, 0);
        Root.Visible = false;
        _isVisible = false;
    }

    /// <summary>
    /// Override to build the screen's UI content.
    /// </summary>
    /// <param name="root">The root control to add content to.</param>
    protected abstract void BuildContent(Control root);

    /// <summary>
    /// Shows the screen with a fade-in animation.
    /// </summary>
    public new void Show()
    {
        if (_isVisible) return;
        _isVisible = true;

        _activeTween?.Kill();
        Root.Visible = true;

        _activeTween = CreateTween();
        _activeTween.TweenProperty(Root, "modulate:a", 1.0f, UITheme.FadeInDuration)
            .SetEase(Tween.EaseType.Out);
        _activeTween.TweenCallback(Callable.From(() => OnShown?.Invoke()));
    }

    /// <summary>
    /// Hides the screen with a fade-out animation.
    /// </summary>
    public new void Hide()
    {
        if (!_isVisible) return;
        _isVisible = false;

        _activeTween?.Kill();

        _activeTween = CreateTween();
        _activeTween.TweenProperty(Root, "modulate:a", 0.0f, UITheme.FadeOutDuration)
            .SetEase(Tween.EaseType.In);
        _activeTween.TweenCallback(Callable.From(() =>
        {
            Root.Visible = false;
            OnHidden?.Invoke();
        }));
    }

    /// <summary>
    /// Immediately shows/hides without animation.
    /// </summary>
    public void SetVisibleImmediate(bool visible)
    {
        _activeTween?.Kill();
        _isVisible = visible;
        Root.Visible = visible;
        Root.Modulate = new Color(1, 1, 1, visible ? 1 : 0);

        if (visible)
            OnShown?.Invoke();
        else
            OnHidden?.Invoke();
    }

    /// <summary>
    /// Whether the screen is currently visible.
    /// </summary>
    public new bool IsVisible => _isVisible;

    /// <summary>
    /// Called when this screen becomes the active screen.
    /// </summary>
    public virtual void OnBecameActive() { }

    /// <summary>
    /// Called when this screen is no longer the active screen.
    /// </summary>
    public virtual void OnBecameInactive() { }

    /// <summary>
    /// Helper to create a centered container for menu content.
    /// </summary>
    protected Control CreateCenteredContainer()
    {
        var container = new CenterContainer
        {
            Name = "CenterContainer"
        };
        container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return container;
    }

    /// <summary>
    /// Helper to create a styled title label.
    /// </summary>
    protected Label CreateTitle(string text)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(label, UITheme.TitleFontSize, UITheme.AccentColor);
        return label;
    }

    /// <summary>
    /// Helper to create a styled menu button.
    /// </summary>
    protected Button CreateMenuButton(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(UITheme.ButtonMinWidth, UITheme.ButtonMinHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };
        UITheme.ApplyButtonStyle(button);
        button.Pressed += onClick;
        return button;
    }
}

/// <summary>
/// Background styles for menu screens.
/// </summary>
public enum MenuBackgroundMode
{
    Shader,
    Solid,
    Hidden
}
