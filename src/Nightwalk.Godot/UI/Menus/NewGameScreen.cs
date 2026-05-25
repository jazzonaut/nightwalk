using System;
using Godot;
using Nightwalk.Core.Difficulty;
using Nightwalk.Godot.UI.Framework;

namespace Nightwalk.Godot.UI.Menus;

/// <summary>
/// New game screen with game mode and difficulty selection.
/// </summary>
public sealed partial class NewGameScreen : UIScreen
{
    public event Action<DifficultyLevel>? OnDifficultySelected;
    public event Action<string>? OnModeSelected;
    public event Action? OnBackPressed;

    public override bool PausesGame => true;

    // Difficulty card colors
    private static readonly Color ObserverColor = new(0f, 0.9f, 0.9f);    // Cyan
    private static readonly Color ExplorerColor = new(0.2f, 0.9f, 0.3f);  // Green
    private static readonly Color WandererColor = new(1f, 0.6f, 0.1f);    // Orange
    private static readonly Color ArchitectColor = new(1f, 0.2f, 0.2f);   // Red

    // Mode card colors
    private static readonly Color SilenceColor = new(0.4f, 0.6f, 0.9f);   // Soft blue
    private static readonly Color ResonanceColor = new(0.8f, 0.2f, 0.6f); // Magenta

    // Track selected mode
    private string _selectedMode = "silence";
    private PanelContainer? _silenceModeCard;
    private PanelContainer? _resonanceModeCard;
    private Label? _modeHeadingLabel;
    private HBoxContainer? _modeContainer;
    private Func<bool>? _hideModeSelectionResolver;

    protected override void BuildContent(Control root)
    {
        // Centered layout
        var centerContainer = CreateCenteredContainer();
        root.AddChild(centerContainer);

        var mainContainer = new VBoxContainer { Name = "MainContainer" };
        mainContainer.AddThemeConstantOverride("separation", UITheme.LargePadding);
        centerContainer.AddChild(mainContainer);

        // Title: "NEW GAME"
        var titleLabel = new Label
        {
            Text = "NEW GAME",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(titleLabel, UITheme.TitleFontSize, UITheme.AccentColor);
        mainContainer.AddChild(titleLabel);

        // Subheading: "GAME MODE"
        _modeHeadingLabel = new Label
        {
            Text = "GAME MODE",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(_modeHeadingLabel, UITheme.SubheadingFontSize, UITheme.TextColor);
        mainContainer.AddChild(_modeHeadingLabel);

        // Mode cards container (horizontal, centered)
        _modeContainer = new HBoxContainer { Name = "ModeCards" };
        _modeContainer.AddThemeConstantOverride("separation", UITheme.MediumPadding);
        _modeContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        mainContainer.AddChild(_modeContainer);

        // Create mode cards
        _silenceModeCard = CreateModeCard(
            "silence",
            "SILENCE",
            "Pure exploration.",
            SilenceColor,
            new[] { "No watchers", "No pressure", "Classic gameplay" }
        );
        _modeContainer.AddChild(_silenceModeCard);

        _resonanceModeCard = CreateModeCard(
            "resonance",
            "RESONANCE",
            "Tension rises.",
            ResonanceColor,
            new[] { "Alert system", "Warden + Seekers", "Stealth mechanics" }
        );
        _modeContainer.AddChild(_resonanceModeCard);

        // Subheading: "DIFFICULTY"
        var difficultyLabel = new Label
        {
            Text = "DIFFICULTY",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(difficultyLabel, UITheme.SubheadingFontSize, UITheme.TextColor);
        mainContainer.AddChild(difficultyLabel);

        // Difficulty cards container (horizontal)
        var cardsContainer = new HBoxContainer { Name = "DifficultyCards" };
        cardsContainer.AddThemeConstantOverride("separation", UITheme.MediumPadding);
        cardsContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        mainContainer.AddChild(cardsContainer);

        // Create difficulty cards
        cardsContainer.AddChild(CreateDifficultyCard(
            DifficultyLevel.Observer,
            "OBSERVER",
            "No pressure.",
            ObserverColor,
            new[] { "Free placements", "No damage", "Score disabled", "Unlimited tools" }
        ));

        cardsContainer.AddChild(CreateDifficultyCard(
            DifficultyLevel.Explorer,
            "EXPLORER",
            "Standard.",
            ExplorerColor,
            new[] { "Standard costs", "Normal damage", "Full scoring", "Baseline" }
        ));

        cardsContainer.AddChild(CreateDifficultyCard(
            DifficultyLevel.Wanderer,
            "WANDERER",
            "Demanding.",
            WandererColor,
            new[] { "Costs 1.5x", "Rewards 75%", "Damage 1.3x", "Max 4 ziplines" }
        ));

        cardsContainer.AddChild(CreateDifficultyCard(
            DifficultyLevel.Architect,
            "ARCHITECT",
            "Unforgiving.",
            ArchitectColor,
            new[] { "Costs 2x", "Rewards 50%", "Damage 2x", "Max 2 ziplines" }
        ));

        // Back button
        var backButton = CreateMenuButton("BACK", () => OnBackPressed?.Invoke());
        backButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        mainContainer.AddChild(backButton);

        // Apply initial mode card styles
        RefreshModeCardStyles();
    }

    private Control CreateDifficultyCard(
        DifficultyLevel level,
        string title,
        string description,
        Color accentColor,
        string[] features)
    {
        var card = new PanelContainer { Name = $"{level}Card" };
        card.CustomMinimumSize = new Vector2(200, 280);
        card.MouseFilter = Control.MouseFilterEnum.Stop;

        // Card background (no borders)
        var cardStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.1f, 0.9f),
            BorderWidthBottom = 0,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            ContentMarginLeft = UITheme.MediumPadding,
            ContentMarginRight = UITheme.MediumPadding,
            ContentMarginTop = UITheme.MediumPadding,
            ContentMarginBottom = UITheme.MediumPadding,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        card.AddThemeStyleboxOverride("panel", cardStyle);

        var cardContent = new VBoxContainer { Name = "Content" };
        cardContent.AddThemeConstantOverride("separation", UITheme.SmallPadding);
        card.AddChild(cardContent);

        // Title
        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(titleLabel, UITheme.HeadingFontSize, accentColor);
        cardContent.AddChild(titleLabel);

        // Description
        var descLabel = new Label
        {
            Text = description,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        UITheme.ApplyLabelStyle(descLabel, UITheme.SmallFontSize, UITheme.TextDimColor);
        cardContent.AddChild(descLabel);

        // Separator
        var separator = new HSeparator();
        separator.AddThemeStyleboxOverride("separator", new StyleBoxLine
        {
            Color = accentColor with { A = 0.3f },
            Thickness = 1
        });
        cardContent.AddChild(separator);

        // Features list
        var featuresContainer = new VBoxContainer { Name = "Features" };
        featuresContainer.AddThemeConstantOverride("separation", 4);
        cardContent.AddChild(featuresContainer);

        foreach (var feature in features)
        {
            var featureLabel = new Label
            {
                Text = $"- {feature}",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            UITheme.ApplyLabelStyle(featureLabel, UITheme.SmallFontSize, UITheme.TextColor);
            featuresContainer.AddChild(featureLabel);
        }

        // Hover effect handling
        card.MouseEntered += () =>
        {
            var hoverStyle = new StyleBoxFlat
            {
                BgColor = accentColor with { A = 0.2f },
                BorderWidthBottom = 0,
                BorderWidthLeft = 0,
                BorderWidthRight = 0,
                BorderWidthTop = 0,
                ContentMarginLeft = UITheme.MediumPadding,
                ContentMarginRight = UITheme.MediumPadding,
                ContentMarginTop = UITheme.MediumPadding,
                ContentMarginBottom = UITheme.MediumPadding,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8
            };
            card.AddThemeStyleboxOverride("panel", hoverStyle);
        };

        card.MouseExited += () =>
        {
            var normalStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.1f, 0.9f),
                BorderWidthBottom = 0,
                BorderWidthLeft = 0,
                BorderWidthRight = 0,
                BorderWidthTop = 0,
                ContentMarginLeft = UITheme.MediumPadding,
                ContentMarginRight = UITheme.MediumPadding,
                ContentMarginTop = UITheme.MediumPadding,
                ContentMarginBottom = UITheme.MediumPadding,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8
            };
            card.AddThemeStyleboxOverride("panel", normalStyle);
        };

        // Click handler to start game
        card.GuiInput += (InputEvent @event) =>
        {
            if (@event is InputEventMouseButton mouseButton &&
                mouseButton.Pressed &&
                mouseButton.ButtonIndex == MouseButton.Left)
            {
                OnDifficultySelected?.Invoke(level);
            }
        };

        return card;
    }

    private PanelContainer CreateModeCard(
        string modeId,
        string title,
        string description,
        Color accentColor,
        string[] features)
    {
        var card = new PanelContainer { Name = $"{modeId}ModeCard" };
        card.CustomMinimumSize = new Vector2(200, 180);
        card.MouseFilter = Control.MouseFilterEnum.Stop;

        // Card background will be set by RefreshModeCardStyles
        var cardContent = new VBoxContainer { Name = "Content" };
        cardContent.AddThemeConstantOverride("separation", UITheme.SmallPadding);
        card.AddChild(cardContent);

        // Title
        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyLabelStyle(titleLabel, UITheme.HeadingFontSize, accentColor);
        cardContent.AddChild(titleLabel);

        // Description
        var descLabel = new Label
        {
            Text = description,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        UITheme.ApplyLabelStyle(descLabel, UITheme.SmallFontSize, UITheme.TextDimColor);
        cardContent.AddChild(descLabel);

        // Separator
        var separator = new HSeparator();
        separator.AddThemeStyleboxOverride("separator", new StyleBoxLine
        {
            Color = accentColor with { A = 0.3f },
            Thickness = 1
        });
        cardContent.AddChild(separator);

        // Features list
        var featuresContainer = new VBoxContainer { Name = "Features" };
        featuresContainer.AddThemeConstantOverride("separation", 4);
        cardContent.AddChild(featuresContainer);

        foreach (var feature in features)
        {
            var featureLabel = new Label
            {
                Text = $"- {feature}",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            UITheme.ApplyLabelStyle(featureLabel, UITheme.SmallFontSize, UITheme.TextColor);
            featuresContainer.AddChild(featureLabel);
        }

        // Click handler for mode selection
        card.GuiInput += (InputEvent @event) =>
        {
            if (@event is InputEventMouseButton mouseButton &&
                mouseButton.Pressed &&
                mouseButton.ButtonIndex == MouseButton.Left)
            {
                SelectMode(modeId);
            }
        };

        return card;
    }

    private void SelectMode(string modeId)
    {
        if (_selectedMode != modeId)
        {
            _selectedMode = modeId;
            OnModeSelected?.Invoke(modeId);
            RefreshModeCardStyles();
        }
    }

    private void RefreshModeCardStyles()
    {
        ApplyModeCardStyle(_silenceModeCard, SilenceColor, _selectedMode == "silence");
        ApplyModeCardStyle(_resonanceModeCard, ResonanceColor, _selectedMode == "resonance");
    }

    private static void ApplyModeCardStyle(PanelContainer? card, Color accentColor, bool isSelected)
    {
        if (card == null) return;

        var bgColor = isSelected
            ? accentColor with { A = 0.4f }
            : new Color(0.08f, 0.08f, 0.1f, 0.9f);

        var cardStyle = new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderWidthBottom = 0,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            ContentMarginLeft = UITheme.MediumPadding,
            ContentMarginRight = UITheme.MediumPadding,
            ContentMarginTop = UITheme.MediumPadding,
            ContentMarginBottom = UITheme.MediumPadding,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        card.AddThemeStyleboxOverride("panel", cardStyle);
    }

    public override void OnBecameActive()
    {
        base.OnBecameActive();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        RefreshModeCardStyles();
        RefreshModeSelectionVisibility();
    }

    /// <summary>
    /// Sets the selected mode (call before screen becomes active to sync with pending mode).
    /// </summary>
    public void SetSelectedMode(string modeId)
    {
        if (string.IsNullOrEmpty(modeId)) return;
        _selectedMode = modeId;
    }

    /// <summary>
    /// Provides a live resolver for the "hide game mode selection" preference.
    /// Re-evaluated each time the screen becomes active so config changes apply immediately.
    /// </summary>
    public void SetHideModeSelectionResolver(Func<bool> resolver)
    {
        _hideModeSelectionResolver = resolver;
        RefreshModeSelectionVisibility();
    }

    private void RefreshModeSelectionVisibility()
    {
        var hide = _hideModeSelectionResolver?.Invoke() ?? false;
        if (_modeHeadingLabel != null) _modeHeadingLabel.Visible = !hide;
        if (_modeContainer != null) _modeContainer.Visible = !hide;
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsVisible) return;

        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            OnBackPressed?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }
}
