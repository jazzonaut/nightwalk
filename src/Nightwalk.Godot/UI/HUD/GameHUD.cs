using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Events;
using Nightwalk.Core.GameState.Events;
using Nightwalk.Core.Resonance.Events;
using Nightwalk.Core.Scoring;
using Nightwalk.Core.Scoring.Events;
using Nightwalk.Godot.Player;

namespace Nightwalk.Godot.UI.HUD;

/// <summary>
/// Main HUD container that manages all HUD elements.
/// Positioned as a CanvasLayer above the game world.
/// </summary>
public sealed partial class GameHUD : CanvasLayer
{
    private IEventBus? _eventBus;

    // Top-left elements (persistent stats)
    private Label _scoreLabel = null!;
    private Label _comboLabel = null!;
    private FuelIndicator _fuelIndicator = null!;
    private FadingLabel _presetLabel = null!;
    private FadingLabel _viewModeLabel = null!;
    private FadingLabel _toolLabel = null!;

    // Resonance mode indicators
    private AlertIndicator _alertIndicator = null!;
    private ExposureIndicator _exposureIndicator = null!;
    private VBoxContainer? _resonanceGroup;

    // Player reference for fuel indicator
    private PlayerEntity? _player;

    // Top-right elements
    private Control? _fpsContainer;
    private Label? _fpsLabel;

    // Score popups with pooling
    private Control _popupContainer = null!;
    private Vector2 _popupSpawnOffset = new(100, 60);
    private readonly Stack<ScorePopup> _popupPool = new();
    private const int MaxPooledPopups = 8;

    // Configuration
    private bool _showFps;
    private bool _hideScore;

    // Score visibility (for Observer mode)
    private VBoxContainer? _scoreGroup;

    private long _displayedScore;
    private long _targetScore;
    private float _comboMultiplier = 1f;

    // Loss popup debouncing
    private long _pendingLoss;
    private float _lossDebounceTimer;
    private const float LossDebounceDelay = 0.25f;

    public override void _Ready()
    {
        Layer = HUDTheme.HUDLayer;

        CreateTopLeftElements();
        CreateTopCenterElements();
        CreateTopRightElements();

        // Container for score popups
        _popupContainer = new Control { Name = "PopupContainer" };
        AddChild(_popupContainer);
    }

    /// <summary>
    /// Connects the HUD to the event bus for score updates.
    /// </summary>
    public void Connect(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<ScoreChangedEvent>(OnScoreChanged);
        _eventBus.Subscribe<ComboChangedEvent>(OnComboChanged);
        _eventBus.Subscribe<ViewModeChangedEvent>(OnViewModeChanged);
        _eventBus.Subscribe<AlertChangedEvent>(OnAlertChanged);
        _eventBus.Subscribe<ExposureChangedEvent>(OnExposureChanged);
    }

    /// <summary>
    /// Disconnects from the event bus.
    /// </summary>
    public void Disconnect()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<ScoreChangedEvent>(OnScoreChanged);
            _eventBus.Unsubscribe<ComboChangedEvent>(OnComboChanged);
            _eventBus.Unsubscribe<ViewModeChangedEvent>(OnViewModeChanged);
            _eventBus.Unsubscribe<AlertChangedEvent>(OnAlertChanged);
            _eventBus.Unsubscribe<ExposureChangedEvent>(OnExposureChanged);
            _eventBus = null;
        }
    }

    /// <summary>
    /// Sets the player reference for fuel indicator updates.
    /// </summary>
    public void SetPlayer(PlayerEntity player)
    {
        _player = player;
    }

    /// <summary>
    /// Configures HUD settings.
    /// </summary>
    /// <param name="showFps">Whether to show FPS counter.</param>
    /// <param name="hideScore">Whether to hide score elements (for Observer mode).</param>
    public void Configure(bool showFps, bool hideScore = false)
    {
        _showFps = showFps;
        _hideScore = hideScore;

        if (_fpsContainer != null)
        {
            _fpsContainer.Visible = showFps;
        }

        if (_scoreGroup != null)
        {
            _scoreGroup.Visible = !hideScore;
        }
    }

    private void CreateTopLeftElements()
    {
        // Cyberpunk angled background
        var background = new HUDBackground { Name = "HUDBackground" };
        AddChild(background);

        var container = new VBoxContainer { Name = "TopLeft" };
        container.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        container.Position = new Vector2(HUDTheme.EdgeMargin, HUDTheme.EdgeMargin);
        container.AddThemeConstantOverride("separation", 2);  // Tighter spacing
        AddChild(container);

        // Score + Combo grouped tightly together
        _scoreGroup = new VBoxContainer { Name = "ScoreGroup" };
        _scoreGroup.AddThemeConstantOverride("separation", 0);
        container.AddChild(_scoreGroup);

        _scoreLabel = new Label { Name = "ScoreLabel", Text = "Score: 0" };
        HUDTheme.ApplyStyle(_scoreLabel, HUDTheme.MediumFontSize, HUDTheme.ScoreColor);
        _scoreGroup.AddChild(_scoreLabel);

        // Combo multiplier (only shown when > 1) - tight to score
        _comboLabel = new Label { Name = "ComboLabel", Text = "" };
        HUDTheme.ApplyStyle(_comboLabel, HUDTheme.SmallFontSize, HUDTheme.ComboColor);
        _comboLabel.AddThemeConstantOverride("margin_top", -4);  // Pull up closer
        _scoreGroup.AddChild(_comboLabel);

        // Fuel indicator
        _fuelIndicator = new FuelIndicator { Name = "FuelIndicator" };
        container.AddChild(_fuelIndicator);

        // Resonance mode indicators (initially hidden)
        _resonanceGroup = new VBoxContainer { Name = "ResonanceGroup", Visible = false };
        _resonanceGroup.AddThemeConstantOverride("separation", 4);
        container.AddChild(_resonanceGroup);

        // Alert indicator label
        var alertLabel = new Label { Text = "ALERT", HorizontalAlignment = HorizontalAlignment.Left };
        HUDTheme.ApplyStyle(alertLabel, HUDTheme.SmallFontSize - 2, new Color(1f, 0.6f, 0.1f, 0.7f));
        _resonanceGroup.AddChild(alertLabel);

        _alertIndicator = new AlertIndicator { Name = "AlertIndicator" };
        _resonanceGroup.AddChild(_alertIndicator);

        // Exposure indicator label
        var exposureLabel = new Label { Text = "EXPOSURE", HorizontalAlignment = HorizontalAlignment.Left };
        HUDTheme.ApplyStyle(exposureLabel, HUDTheme.SmallFontSize - 2, new Color(1f, 0.2f, 0.2f, 0.7f));
        _resonanceGroup.AddChild(exposureLabel);

        _exposureIndicator = new ExposureIndicator { Name = "ExposureIndicator" };
        _resonanceGroup.AddChild(_exposureIndicator);

        // Spacer before tool
        var toolSpacer = new Control { CustomMinimumSize = new Vector2(0, 4) };
        container.AddChild(toolSpacer);

        // Tool name (persistent while active)
        _toolLabel = new FadingLabel
        {
            Name = "ToolLabel",
            HorizontalAlignment = HorizontalAlignment.Left,
            BaseColor = HUDTheme.AccentColor,
            FontSize = HUDTheme.SmallFontSize
        };
        container.AddChild(_toolLabel);
    }

    private void CreateTopCenterElements()
    {
        // Preset label - top center, fades after showing
        _presetLabel = new FadingLabel
        {
            Name = "PresetLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            BaseColor = HUDTheme.AccentColor,
            FontSize = HUDTheme.MediumFontSize
        };
        _presetLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _presetLabel.Position = new Vector2(-100, HUDTheme.EdgeMargin);  // Offset to center
        _presetLabel.CustomMinimumSize = new Vector2(200, 30);
        AddChild(_presetLabel);

        // View mode label - top center, fades after showing (offset below preset label)
        _viewModeLabel = new FadingLabel
        {
            Name = "ViewModeLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            BaseColor = HUDTheme.AccentColor,
            FontSize = HUDTheme.MediumFontSize
        };
        _viewModeLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _viewModeLabel.Position = new Vector2(-100, HUDTheme.EdgeMargin + 24);
        _viewModeLabel.CustomMinimumSize = new Vector2(200, 30);
        AddChild(_viewModeLabel);
    }

    private void CreateTopRightElements()
    {
        _fpsContainer = new VBoxContainer { Name = "TopRight" };
        _fpsContainer.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _fpsContainer.GrowHorizontal = Control.GrowDirection.Begin;
        _fpsContainer.Position = new Vector2(-HUDTheme.EdgeMargin, HUDTheme.EdgeMargin);
        _fpsContainer.Visible = _showFps;
        AddChild(_fpsContainer);

        // FPS counter
        _fpsLabel = new Label { Name = "FpsLabel", Text = "FPS: --", HorizontalAlignment = HorizontalAlignment.Right };
        HUDTheme.ApplyStyle(_fpsLabel, HUDTheme.SmallFontSize, HUDTheme.DimTextColor);
        _fpsContainer.AddChild(_fpsLabel);
    }

    public override void _Process(double delta)
    {
        // Skip processing when HUD is hidden (e.g., in main menu)
        if (!Visible) return;

        var dt = (float)delta;

        // Update FPS (if enabled)
        if (_fpsLabel != null)
        {
            _fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
        }

        // Update fuel indicator
        if (_player != null)
        {
            _fuelIndicator.UpdateEnergy(_player.EnergyPercent);
        }

        // Flush pending loss popup after debounce delay
        if (_pendingLoss > 0)
        {
            _lossDebounceTimer -= dt;
            if (_lossDebounceTimer <= 0)
            {
                SpawnScorePopup(-_pendingLoss);
                _pendingLoss = 0;
            }
        }

        // Animate score counting (both up and down)
        if (_displayedScore != _targetScore)
        {
            long diff = _targetScore - _displayedScore;
            long increment = Math.Max(1L, Math.Abs(diff) / 10);

            if (diff > 0)
            {
                _displayedScore = Math.Min(_displayedScore + increment, _targetScore);
            }
            else
            {
                _displayedScore = Math.Max(_displayedScore - increment, _targetScore);
            }
            UpdateScoreLabel();
        }
    }

    private void UpdateScoreLabel()
    {
        _scoreLabel.Text = $"Score: {_displayedScore:N0}";
    }

    // Public methods for showing HUD elements

    /// <summary>
    /// Shows the current tool name persistently while selected.
    /// </summary>
    /// <param name="toolName">Tool display name, or null for no tool.</param>
    public void ShowTool(string? toolName)
    {
        if (toolName == null)
        {
            _toolLabel.Hide();
        }
        else
        {
            _toolLabel.ShowPersistent(toolName);
        }
    }

    /// <summary>
    /// Shows the preset name temporarily.
    /// </summary>
    public void ShowPreset(string presetName)
    {
        _presetLabel.ShowTemporary($"Preset: {presetName}");
    }

    /// <summary>
    /// Shows the view mode name temporarily.
    /// </summary>
    public void ShowViewMode(string modeName)
    {
        _viewModeLabel.ShowTemporary($"Mode: {modeName}");
    }

    /// <summary>
    /// Sets the score directly (used for initialization/reset).
    /// </summary>
    public void SetScore(long score)
    {
        _displayedScore = score;
        _targetScore = score;
        UpdateScoreLabel();
    }

    /// <summary>
    /// Resets the HUD state (for session reset).
    /// </summary>
    public void Reset()
    {
        SetScore(0);
        _comboMultiplier = 1f;
        _comboLabel.Text = "";
        _fuelIndicator.Reset();
        _alertIndicator.Reset();
        _exposureIndicator.Reset();
        _toolLabel.Hide();
        _presetLabel.Hide();
        _viewModeLabel.Hide();
        _pendingLoss = 0;
        _lossDebounceTimer = 0;

        // Clear popup pool (pooled nodes will be freed with their parent)
        _popupPool.Clear();
    }

    /// <summary>
    /// Shows or hides the Resonance mode indicators.
    /// </summary>
    public void SetResonanceModeActive(bool active)
    {
        if (_resonanceGroup != null)
        {
            _resonanceGroup.Visible = active;
        }
    }

    // Event handlers

    private void OnScoreChanged(ScoreChangedEvent e)
    {
        _targetScore = e.NewTotal;

        if (e.Points > 0)
        {
            // Gains show immediately
            SpawnScorePopup(e.Points);
        }
        else if (e.Points < 0)
        {
            // Losses are debounced/accumulated
            _pendingLoss += -e.Points;
            _lossDebounceTimer = LossDebounceDelay;
        }
    }

    private void OnViewModeChanged(ViewModeChangedEvent e)
    {
        ShowViewMode(GetViewModeLabel(e.Mode));
    }

    private static string GetViewModeLabel(Nightwalk.Core.GameState.ViewMode mode)
    {
        return mode switch
        {
            Nightwalk.Core.GameState.ViewMode.Walk => "Walk",
            Nightwalk.Core.GameState.ViewMode.Flight => "Flight",
            Nightwalk.Core.GameState.ViewMode.BirdsEye => "Bird's Eye",
            _ => mode.ToString()
        };
    }

    private void SpawnScorePopup(long points)
    {
        ScorePopup? popup = null;

        // Try to get a valid popup from pool
        while (_popupPool.Count > 0 && popup == null)
        {
            var candidate = _popupPool.Pop();
            if (GodotObject.IsInstanceValid(candidate))
            {
                popup = candidate;
                popup.Visible = true;
            }
        }

        // Create new if pool was empty or all pooled objects were invalid
        if (popup == null)
        {
            popup = new ScorePopup();
            popup.OnAnimationComplete += ReturnPopupToPool;
            _popupContainer.AddChild(popup);
        }

        // Position near score label with some randomization
        var randomOffset = new Vector2(
            GD.RandRange(-15, 15),
            GD.RandRange(-8, 8)
        );
        popup.Initialize(points, _popupSpawnOffset + randomOffset);
    }

    private void ReturnPopupToPool(ScorePopup popup)
    {
        // Don't pool invalid objects
        if (!GodotObject.IsInstanceValid(popup))
            return;

        // Stop processing to prevent repeated OnAnimationComplete calls
        popup.SetProcess(false);
        popup.Visible = false;
        if (_popupPool.Count < MaxPooledPopups)
        {
            _popupPool.Push(popup);
        }
        else
        {
            popup.OnAnimationComplete -= ReturnPopupToPool;
            popup.QueueFree();
        }
    }

    private void OnComboChanged(ComboChangedEvent e)
    {
        _comboMultiplier = e.Multiplier;
        if (e.Multiplier > 1f)
        {
            _comboLabel.Text = $"x{e.Multiplier:F1}";
        }
        else
        {
            _comboLabel.Text = "";
        }
    }

    private void OnAlertChanged(AlertChangedEvent e)
    {
        _alertIndicator.UpdateValue(e.NewValue);
        UpdateResonanceGroupVisibility();
    }

    private void OnExposureChanged(ExposureChangedEvent e)
    {
        _exposureIndicator.UpdateValue(e.NewValue);
        UpdateResonanceGroupVisibility();
    }

    /// <summary>
    /// Updates resonance group visibility based on alert and exposure values.
    /// Shows when either is non-zero, hides when both are zero.
    /// </summary>
    private void UpdateResonanceGroupVisibility()
    {
        if (_resonanceGroup == null) return;

        bool hasAlert = _alertIndicator.CurrentValue > 0.01f;
        bool hasExposure = _exposureIndicator.CurrentValue > 0.01f;

        _resonanceGroup.Visible = hasAlert || hasExposure;
    }

    public override void _ExitTree()
    {
        Disconnect();
    }
}
