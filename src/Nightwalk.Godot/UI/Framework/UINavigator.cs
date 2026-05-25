using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Godot.UI.Framework;

/// <summary>
/// Result of UI navigation setup.
/// </summary>
public sealed record UINavigatorResult(UINavigator Navigator)
{
    public void Cleanup()
    {
        Navigator.QueueFree();
    }
}

/// <summary>
/// Manages a stack of UI screens with navigation support.
/// Provides a shared background overlay that stays constant during screen transitions.
/// </summary>
public sealed partial class UINavigator : Node
{
    /// <summary>
    /// Fired when the active screen changes.
    /// </summary>
    public event Action<UIScreen?>? OnActiveScreenChanged;

    private readonly Dictionary<Type, UIScreen> _screens = new();
    private readonly Stack<UIScreen> _screenStack = new();
    private Node? _screenContainer;
    private CanvasLayer? _backgroundLayer;
    private ColorRect? _background;
    private ShaderMaterial? _backgroundShaderMaterial;
    private Tween? _backgroundTween;
    private bool _initialized;
    private const string BackgroundShaderPath = "res://shaders/menu_background.gdshader";

    /// <summary>
    /// Ensures the navigator is initialized. Safe to call multiple times.
    /// This handles the case where RegisterScreen is called before _Ready().
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        // Create background layer (renders below screens)
        _backgroundLayer = new CanvasLayer { Name = "BackgroundLayer", Layer = 100 };
        AddChild(_backgroundLayer);

        _background = new ColorRect
        {
            Name = "SharedBackground",
            Color = UITheme.BackgroundColor
        };
        _background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _background.Modulate = new Color(1, 1, 1, 0);
        _background.Visible = false;

        // Use a procedural shader for the menu background to keep it crisp at any resolution.
        var backgroundShader = GD.Load<Shader>(BackgroundShaderPath);
        _backgroundShaderMaterial = new ShaderMaterial { Shader = backgroundShader };
        _background.Material = _backgroundShaderMaterial;

        _backgroundLayer.AddChild(_background);

        _screenContainer = new Node { Name = "Screens" };
        AddChild(_screenContainer);
    }

    public override void _Ready()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Registers a screen type. The screen is instantiated and added to the tree but hidden.
    /// </summary>
    public void RegisterScreen<T>() where T : UIScreen, new()
    {
        EnsureInitialized();

        if (_screens.ContainsKey(typeof(T)))
        {
            Log.Warn(LogCategory.UI, $"Screen {typeof(T).Name} already registered");
            return;
        }

        var screen = new T { Name = typeof(T).Name };
        _screens[typeof(T)] = screen;
        _screenContainer!.AddChild(screen);

        Log.Debug(LogCategory.UI, $"Registered screen: {typeof(T).Name}");
    }

    /// <summary>
    /// Gets a registered screen by type.
    /// </summary>
    public T? GetScreen<T>() where T : UIScreen
    {
        return _screens.TryGetValue(typeof(T), out var screen) ? (T)screen : null;
    }

    /// <summary>
    /// Pushes a screen onto the stack and shows it.
    /// The previous screen remains in the stack but becomes inactive.
    /// </summary>
    public void Push<T>() where T : UIScreen
    {
        if (!_screens.TryGetValue(typeof(T), out var screen))
        {
            Log.Error(LogCategory.UI, $"Screen {typeof(T).Name} not registered");
            return;
        }

        // Deactivate current screen (but don't hide it if new screen doesn't block)
        if (_screenStack.TryPeek(out var current))
        {
            current.OnBecameInactive();
            if (screen.BlocksInput)
            {
                current.Hide();
            }
        }

        _screenStack.Push(screen);
        ApplyBackgroundStyle(screen);
        screen.Show();
        screen.OnBecameActive();

        OnActiveScreenChanged?.Invoke(screen);
        Log.Debug(LogCategory.UI, $"Pushed screen: {typeof(T).Name}");
    }

    /// <summary>
    /// Pops the current screen and returns to the previous one.
    /// </summary>
    public void Pop()
    {
        if (_screenStack.Count == 0)
        {
            Log.Warn(LogCategory.UI, "Cannot pop - screen stack is empty");
            return;
        }

        var current = _screenStack.Pop();
        current.OnBecameInactive();
        current.Hide();

        // Show and activate previous screen
        if (_screenStack.TryPeek(out var previous))
        {
            previous.Show();
            previous.OnBecameActive();
            ApplyBackgroundStyle(previous);
            OnActiveScreenChanged?.Invoke(previous);
            Log.Debug(LogCategory.UI, $"Popped to screen: {previous.Name}");
        }
        else
        {
            ApplyBackgroundStyle(null);
            OnActiveScreenChanged?.Invoke(null);
            Log.Debug(LogCategory.UI, "Popped last screen - stack empty");
        }
    }

    /// <summary>
    /// Clears the entire stack and hides all screens.
    /// </summary>
    public void PopAll()
    {
        while (_screenStack.Count > 0)
        {
            var screen = _screenStack.Pop();
            screen.OnBecameInactive();
            screen.Hide();
        }

        ApplyBackgroundStyle(null);

        OnActiveScreenChanged?.Invoke(null);
        Log.Debug(LogCategory.UI, "Cleared screen stack");
    }

    /// <summary>
    /// Replaces the current screen with a new one (same stack depth).
    /// </summary>
    public void Replace<T>() where T : UIScreen
    {
        if (!_screens.TryGetValue(typeof(T), out var screen))
        {
            Log.Error(LogCategory.UI, $"Screen {typeof(T).Name} not registered");
            return;
        }

        // Pop current without showing previous
        if (_screenStack.Count > 0)
        {
            var current = _screenStack.Pop();
            current.OnBecameInactive();
            current.Hide();
        }

        _screenStack.Push(screen);
        ApplyBackgroundStyle(screen);
        screen.Show();
        screen.OnBecameActive();

        OnActiveScreenChanged?.Invoke(screen);
        Log.Debug(LogCategory.UI, $"Replaced with screen: {typeof(T).Name}");
    }

    /// <summary>
    /// Gets the currently active screen, or null if none.
    /// </summary>
    public UIScreen? ActiveScreen => _screenStack.TryPeek(out var screen) ? screen : null;

    /// <summary>
    /// Whether any screen is currently shown.
    /// </summary>
    public bool HasActiveScreen => _screenStack.Count > 0;

    /// <summary>
    /// The number of screens on the stack.
    /// </summary>
    public int StackDepth => _screenStack.Count;

    private void ApplyBackgroundStyle(UIScreen? screen)
    {
        if (_background == null)
            return;

        if (screen == null || screen.BackgroundMode == MenuBackgroundMode.Hidden)
        {
            HideBackground();
            return;
        }

        ShowBackground();

        if (screen.BackgroundMode == MenuBackgroundMode.Solid)
        {
            _background.Material = null;
            _background.Color = UITheme.BackgroundColor;
        }
        else
        {
            _background.Material = _backgroundShaderMaterial;
            _background.Color = Colors.White;
        }
    }

    private void ShowBackground()
    {
        if (_background == null) return;

        _backgroundTween?.Kill();
        _background.Visible = true;

        _backgroundTween = CreateTween();
        _backgroundTween.TweenProperty(_background, "modulate:a", 1.0f, UITheme.FadeInDuration)
            .SetEase(Tween.EaseType.Out);
    }

    private void HideBackground()
    {
        if (_background == null) return;

        _backgroundTween?.Kill();

        _backgroundTween = CreateTween();
        _backgroundTween.TweenProperty(_background, "modulate:a", 0.0f, UITheme.FadeOutDuration)
            .SetEase(Tween.EaseType.In);
        _backgroundTween.TweenCallback(Callable.From(() => _background.Visible = false));
    }
}
