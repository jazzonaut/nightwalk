using System;
using Godot;
using Nightwalk.Godot.UI.Framework;

namespace Nightwalk.Godot.UI.Menus;

/// <summary>
/// Main menu screen - minimalistic cyberpunk/noir style.
/// </summary>
public sealed partial class MainMenuScreen : UIScreen
{
    public event Action? OnPlayPressed;
    public event Action? OnSettingsPressed;
    public event Action? OnQuitPressed;

    public override bool PausesGame => true;

    protected override void BuildContent(Control root)
    {
        // Centered layout
        var centerContainer = CreateCenteredContainer();
        root.AddChild(centerContainer);

        var mainContainer = new VBoxContainer
        {
            Name = "MainContainer"
        };
        mainContainer.AddThemeConstantOverride("separation", UITheme.LargePadding);
        centerContainer.AddChild(mainContainer);

        // Title section
        var titleSection = CreateTitleSection();
        mainContainer.AddChild(titleSection);

        // Menu buttons
        var buttonContainer = new VBoxContainer
        {
            Name = "Buttons"
        };
        buttonContainer.AddThemeConstantOverride("separation", UITheme.ButtonSpacing);
        mainContainer.AddChild(buttonContainer);

        // Play button
        var playButton = CreateMenuButton("ENTER", () => OnPlayPressed?.Invoke());
        buttonContainer.AddChild(playButton);

        // Settings button
        var settingsButton = CreateMenuButton("SETTINGS", () => OnSettingsPressed?.Invoke());
        buttonContainer.AddChild(settingsButton);

        // Quit button
        var quitButton = CreateMenuButton("EXIT", () => OnQuitPressed?.Invoke());
        buttonContainer.AddChild(quitButton);

        // Version/copyright in bottom right
        var versionLabel = new Label
        {
            Text = "v0.1.0"
        };
        UITheme.ApplyLabelStyle(versionLabel, UITheme.SmallFontSize, UITheme.TextDimColor);
        versionLabel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        versionLabel.GrowHorizontal = Control.GrowDirection.Begin;
        versionLabel.GrowVertical = Control.GrowDirection.Begin;
        versionLabel.Position = new Vector2(-UITheme.MediumPadding, -UITheme.MediumPadding);
        root.AddChild(versionLabel);
    }

    private Control CreateTitleSection()
    {
        var container = new VBoxContainer
        {
            Name = "TitleSection"
        };
        container.AddThemeConstantOverride("separation", 4);

        // Main title
        var titleLabel = new Label
        {
            Text = "NIGHTWALK",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(titleLabel, UITheme.TitleFontSize, UITheme.AccentColor);
        container.AddChild(titleLabel);

        // Subtitle/tagline
        var subtitleLabel = new Label
        {
            Text = "explore the neon city",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(subtitleLabel, UITheme.SmallFontSize, UITheme.TextDimColor);
        container.AddChild(subtitleLabel);

        return container;
    }

    public override void OnBecameActive()
    {
        base.OnBecameActive();
        // Release mouse when entering menu
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
}
