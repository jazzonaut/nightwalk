using System;
using Godot;
using Nightwalk.Godot.UI.Framework;

namespace Nightwalk.Godot.UI.Menus;

/// <summary>
/// Pause menu screen - minimal overlay during gameplay.
/// </summary>
public sealed partial class PauseMenuScreen : UIScreen
{
    public event Action? OnResumePressed;
    public event Action? OnSettingsPressed;
    public event Action? OnMainMenuPressed;

    public override bool PausesGame => true;
    public override MenuBackgroundMode BackgroundMode => MenuBackgroundMode.Solid;

    protected override void BuildContent(Control root)
    {
        // Centered layout (no panel border - matches main menu style)
        var centerContainer = CreateCenteredContainer();
        root.AddChild(centerContainer);

        var content = new VBoxContainer
        {
            Name = "Content"
        };
        content.AddThemeConstantOverride("separation", UITheme.MediumPadding);
        centerContainer.AddChild(content);

        // Pause title
        var titleLabel = new Label
        {
            Text = "PAUSED",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(titleLabel, UITheme.HeadingFontSize, UITheme.AccentColor);
        content.AddChild(titleLabel);

        // Buttons
        var buttonContainer = new VBoxContainer
        {
            Name = "Buttons"
        };
        buttonContainer.AddThemeConstantOverride("separation", UITheme.ButtonSpacing);
        content.AddChild(buttonContainer);

        // Resume button
        var resumeButton = CreateMenuButton("RESUME", () => OnResumePressed?.Invoke());
        buttonContainer.AddChild(resumeButton);

        // Settings button
        var settingsButton = CreateMenuButton("SETTINGS", () => OnSettingsPressed?.Invoke());
        buttonContainer.AddChild(settingsButton);

        // Main Menu button
        var mainMenuButton = CreateMenuButton("MAIN MENU", () => OnMainMenuPressed?.Invoke());
        buttonContainer.AddChild(mainMenuButton);

        // Hint text
        var hintLabel = new Label
        {
            Text = "Press ESC to resume",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(hintLabel, UITheme.SmallFontSize, UITheme.TextDimColor);
        content.AddChild(hintLabel);
    }

    public override void OnBecameActive()
    {
        base.OnBecameActive();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
}
