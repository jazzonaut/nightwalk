using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Config.Attributes;
using Nightwalk.Core.Config.Settings;
using Nightwalk.Godot.UI.Framework;
using Nightwalk.Godot.UI.Settings;

namespace Nightwalk.Godot.UI.Menus;

/// <summary>
/// Dynamic settings menu that generates UI from config property attributes.
/// </summary>
public sealed partial class SettingsMenuScreen : UIScreen
{
    public event Action? OnBackPressed;

    private TabContainer _tabContainer = null!;
    private ConfigScanner? _scanner;
    private UserPreferencesService? _preferences;
    private Dictionary<SettingsCategory, List<SettingMetadata>>? _discoveredSettings;
    private readonly Dictionary<string, Action<object?>> _valueUpdaters = new();
    private Func<MenuBackgroundMode>? _backgroundResolver;

    public override bool PausesGame => true;
    public override MenuBackgroundMode BackgroundMode => _backgroundResolver?.Invoke() ?? base.BackgroundMode;

    /// <summary>
    /// Initializes the settings menu with the config scanner and preferences service.
    /// </summary>
    public void Initialize(ConfigScanner scanner, UserPreferencesService preferences)
    {
        _scanner = scanner;
        _preferences = preferences;

        // Rebuild tabs now that scanner is available
        // (BuildContent runs during _Ready before Initialize is called)
        BuildDynamicTabs();
    }

    /// <summary>
    /// Provides a resolver to select a background mode based on game state.
    /// </summary>
    public void SetBackgroundResolver(Func<MenuBackgroundMode> resolver)
    {
        _backgroundResolver = resolver;
    }

    protected override void BuildContent(Control root)
    {
        // Centered layout (no panel border - matches main menu style)
        var centerContainer = CreateCenteredContainer();
        root.AddChild(centerContainer);

        var content = new VBoxContainer
        {
            Name = "Content",
            CustomMinimumSize = new Vector2(800, 600)
        };
        content.AddThemeConstantOverride("separation", UITheme.LargePadding);
        centerContainer.AddChild(content);

        // Title
        var titleLabel = new Label
        {
            Text = "SETTINGS",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(titleLabel, UITheme.HeadingFontSize, UITheme.AccentColor);
        content.AddChild(titleLabel);

        // Tab container
        _tabContainer = new TabContainer
        {
            Name = "Tabs",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 420)
        };
        ApplyTabContainerStyle(_tabContainer);
        content.AddChild(_tabContainer);

        // Tabs are built dynamically when Initialize() is called with the scanner

        // Button row
        var buttonRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };
        buttonRow.AddThemeConstantOverride("separation", UITheme.MediumPadding);
        content.AddChild(buttonRow);

        // Back button
        var backButton = CreateMenuButton("BACK", () =>
        {
            // Save any pending changes when leaving settings
            if (_preferences?.HasUnsavedChanges == true)
            {
                _preferences.Save();
            }
            OnBackPressed?.Invoke();
        });
        buttonRow.AddChild(backButton);

        // Reset button
        var resetButton = CreateMenuButton("RESET ALL", () =>
        {
            _preferences?.DiscardChanges();
            _preferences?.ResetAllToDefaults();
            RefreshValues();
        });
        buttonRow.AddChild(resetButton);
    }

    private void BuildDynamicTabs()
    {
        // Clear existing tabs and updaters
        foreach (var child in _tabContainer.GetChildren())
        {
            child.QueueFree();
        }
        _valueUpdaters.Clear();

        // Scan for settings if scanner is available
        _discoveredSettings = _scanner?.ScanAll();

        if (_discoveredSettings == null || _discoveredSettings.Count == 0)
        {
            // Fallback: create empty placeholder
            var placeholder = new Label
            {
                Text = "No settings available",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            UITheme.ApplyLabelStyle(placeholder);
            placeholder.Name = "Placeholder";
            _tabContainer.AddChild(placeholder);
            return;
        }

        // Create tabs in enum-defined order
        foreach (var category in Enum.GetValues<SettingsCategory>())
        {
            if (!_discoveredSettings.TryGetValue(category, out var settings) || settings.Count == 0)
            {
                continue;
            }

            var tab = CreateCategoryTab(category, settings);
            tab.Name = category.ToString();
            _tabContainer.AddChild(tab);
        }
    }

    private Control CreateCategoryTab(SettingsCategory category, List<SettingMetadata> settings)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", UITheme.LargePadding);
        margin.AddThemeConstantOverride("margin_right", UITheme.LargePadding);
        margin.AddThemeConstantOverride("margin_top", UITheme.LargePadding);
        margin.AddThemeConstantOverride("margin_bottom", UITheme.MediumPadding);

        var scrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };

        // Inner margin to create space between content and scrollbar
        var innerMargin = new MarginContainer();
        innerMargin.AddThemeConstantOverride("margin_right", 20);
        innerMargin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", UITheme.LargePadding);
        container.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        foreach (var setting in settings)
        {
            var currentValue = _scanner?.GetValue(setting);
            var (row, updateValue) = SettingsControlFactory.CreateControlRow(
                setting,
                currentValue,
                newValue => HandleSettingChanged(setting, newValue));

            container.AddChild(row);

            // Store the updater for refreshing values later
            var key = $"{setting.ConfigName}.{setting.PropertyPath}";
            _valueUpdaters[key] = updateValue;
        }

        // Wrap settings + (optional) controls reference so we control their
        // spacing independently of the inter-row separation inside `container`.
        var contentColumn = new VBoxContainer();
        contentColumn.AddThemeConstantOverride("separation", UITheme.MediumPadding);
        contentColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        contentColumn.AddChild(container);

        if (category == SettingsCategory.Controls)
        {
            contentColumn.AddChild(BuildControlsReference());
        }

        innerMargin.AddChild(contentColumn);
        scrollContainer.AddChild(innerMargin);
        margin.AddChild(scrollContainer);
        return margin;
    }

    private static Control BuildControlsReference()
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", UITheme.LargePadding);
        grid.AddThemeConstantOverride("v_separation", UITheme.MediumPadding);
        grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var sections = new (string Heading, (string Key, string Action)[] Bindings)[]
        {
            ("Movement", new[]
            {
                ("WASD", "Move"),
                ("Space", "Jump / Jetpack"),
                ("Shift", "Sprint"),
                ("E", "Interact"),
            }),
            ("View", new[]
            {
                ("Tab", "Toggle flight"),
                ("Q", "Bird's-eye view"),
                ("Scroll", "Cycle tools / zoom"),
            }),
            ("Building", new[]
            {
                ("Left Click", "Place / rotate prism or zipline"),
                ("Right Click", "Remove prism or zipline"),
            }),
            ("Other", new[]
            {
                ("1 - 9", "Change visual preset"),
                ("N", "Screensaver"),
            }),
        };

        foreach (var (heading, bindings) in sections)
        {
            grid.AddChild(BuildControlsSection(heading, bindings));
        }

        return grid;
    }

    private static Control BuildControlsSection(string heading, (string Key, string Action)[] bindings)
    {
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", UITheme.SmallPadding);
        section.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var headingLabel = new Label { Text = heading };
        UITheme.ApplyLabelStyle(headingLabel, UITheme.BodyFontSize, UITheme.AccentColor);
        section.AddChild(headingLabel);

        var bindingsGrid = new GridContainer { Columns = 2 };
        bindingsGrid.AddThemeConstantOverride("h_separation", UITheme.MediumPadding);
        bindingsGrid.AddThemeConstantOverride("v_separation", UITheme.SmallPadding / 2);

        foreach (var (key, action) in bindings)
        {
            var keyLabel = new Label
            {
                Text = key,
                CustomMinimumSize = new Vector2(110, 0),
            };
            UITheme.ApplyLabelStyle(keyLabel, UITheme.SmallFontSize, UITheme.TextColor);

            var actionLabel = new Label { Text = action };
            UITheme.ApplyLabelStyle(actionLabel, UITheme.SmallFontSize, UITheme.TextDimColor);

            bindingsGrid.AddChild(keyLabel);
            bindingsGrid.AddChild(actionLabel);
        }

        section.AddChild(bindingsGrid);
        return section;
    }

    private void HandleSettingChanged(SettingMetadata setting, object? newValue)
    {
        // Set the preference (held in memory until saved on menu close)
        _preferences?.SetPreference(setting.ConfigName, setting.PropertyPath, newValue);
    }

    /// <summary>
    /// Refreshes all control values from the current config state.
    /// Call this when configs are reloaded externally.
    /// </summary>
    public void RefreshValues()
    {
        if (_discoveredSettings == null || _scanner == null)
        {
            return;
        }

        foreach (var settings in _discoveredSettings.Values)
        {
            foreach (var setting in settings)
            {
                var key = $"{setting.ConfigName}.{setting.PropertyPath}";
                if (_valueUpdaters.TryGetValue(key, out var updater))
                {
                    var currentValue = _scanner.GetValue(setting);
                    updater(currentValue);
                }
            }
        }
    }

    private static void ApplyTabContainerStyle(TabContainer tabs)
    {
        var tabStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.18f, 0.8f),
            ContentMarginLeft = UITheme.SmallPadding,
            ContentMarginRight = UITheme.SmallPadding,
            ContentMarginTop = UITheme.SmallPadding,
            ContentMarginBottom = UITheme.SmallPadding
        };

        var tabSelectedStyle = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0.2f, 0.2f, 0.9f),
            BorderColor = UITheme.AccentColor,
            BorderWidthBottom = 2,
            ContentMarginLeft = UITheme.SmallPadding,
            ContentMarginRight = UITheme.SmallPadding,
            ContentMarginTop = UITheme.SmallPadding,
            ContentMarginBottom = UITheme.SmallPadding
        };

        tabs.AddThemeStyleboxOverride("tab_unselected", tabStyle);
        tabs.AddThemeStyleboxOverride("tab_selected", tabSelectedStyle);
        tabs.AddThemeStyleboxOverride("tab_hovered", tabSelectedStyle);
        tabs.AddThemeColorOverride("font_selected_color", UITheme.AccentColor);
        tabs.AddThemeColorOverride("font_unselected_color", UITheme.TextDimColor);
        tabs.AddThemeFontSizeOverride("font_size", UITheme.BodyFontSize);
    }

    public override void OnBecameActive()
    {
        base.OnBecameActive();
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Refresh values when opening settings (in case they changed externally)
        RefreshValues();
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsVisible) return;

        // ESC goes back
        if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause"))
        {
            // Save any pending changes when leaving settings
            if (_preferences?.HasUnsavedChanges == true)
            {
                _preferences.Save();
            }
            OnBackPressed?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }
}
