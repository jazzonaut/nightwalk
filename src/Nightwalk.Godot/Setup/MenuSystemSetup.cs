using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Config.Settings;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Difficulty;
using Nightwalk.Core.Events;
using Nightwalk.Core.GameMode;
using Nightwalk.Core.GameState;
using Nightwalk.Core.GameState.Events;
using Nightwalk.Godot.GameState;
using Nightwalk.Godot.UI.Framework;
using Nightwalk.Godot.UI.Menus;

namespace Nightwalk.Godot.Setup;

/// <summary>
/// Result of menu system setup.
/// </summary>
public sealed record MenuSystemResult(
    UINavigator Navigator,
    MainMenuScreen MainMenu,
    PauseMenuScreen PauseMenu,
    SettingsMenuScreen SettingsMenu,
    NewGameScreen NewGameMenu
)
{
    public void Cleanup()
    {
        Navigator.QueueFree();
    }
}

/// <summary>
/// Configures the menu system (navigator and screens).
/// </summary>
public static class MenuSystemSetup
{
    /// <summary>
    /// Creates and configures the menu system.
    /// </summary>
    /// <param name="parent">The parent node to add the navigator to.</param>
    /// <param name="stateManager">Game state manager for state transitions.</param>
    /// <param name="eventBus">Event bus for session events.</param>
    /// <param name="data">Game data service containing configs.</param>
    /// <param name="difficultyService">Difficulty service for level selection.</param>
    /// <param name="gameModeManager">Game mode manager for mode selection.</param>
    /// <returns>The created menu components.</returns>
    public static MenuSystemResult Configure(
        Node parent,
        GameStateManager stateManager,
        IEventBus eventBus,
        GameDataService data,
        IDifficultyService? difficultyService = null,
        GameModeManager? gameModeManager = null)
    {
        Log.Info(LogCategory.UI, "Initializing menu system...");

        // Create navigator
        var navigator = new UINavigator { Name = "UINavigator" };
        parent.AddChild(navigator);

        // Register screens
        navigator.RegisterScreen<MainMenuScreen>();
        navigator.RegisterScreen<PauseMenuScreen>();
        navigator.RegisterScreen<SettingsMenuScreen>();
        navigator.RegisterScreen<NewGameScreen>();

        // Get screen references
        var mainMenu = navigator.GetScreen<MainMenuScreen>()!;
        var pauseMenu = navigator.GetScreen<PauseMenuScreen>()!;
        var settingsMenu = navigator.GetScreen<SettingsMenuScreen>()!;
        var newGameMenu = navigator.GetScreen<NewGameScreen>()!;

        // Create and configure the config scanner
        var scanner = new ConfigScanner();
        scanner.RegisterConfig("core", () => data.Core);
        scanner.RegisterConfig("audio", () => data.Audio);
        scanner.RegisterConfig("physics", () => data.Physics);
        scanner.RegisterConfig("gameplay", () => data.Gameplay);
        scanner.RegisterConfig("skycraft", () => data.Skycraft);

        // Initialize settings menu with scanner and preferences
        settingsMenu.Initialize(scanner, data.UserPreferences!);
        settingsMenu.SetBackgroundResolver(() =>
            stateManager.CurrentState?.Name == GameStateNames.MainMenu
                ? MenuBackgroundMode.Shader
                : MenuBackgroundMode.Solid);

        // Wire up main menu actions
        mainMenu.OnPlayPressed += () =>
        {
            // Push new game screen instead of starting game directly
            navigator.Push<NewGameScreen>();
        };

        mainMenu.OnSettingsPressed += () =>
        {
            navigator.Push<SettingsMenuScreen>();
        };

        mainMenu.OnQuitPressed += () =>
        {
            Log.Info(LogCategory.UI, "Quit requested");
            parent.GetTree().Quit();
        };

        // Wire up pause menu actions
        pauseMenu.OnResumePressed += () =>
        {
            navigator.PopAll();
            stateManager.TransitionTo<PlayingState>();
        };

        pauseMenu.OnSettingsPressed += () =>
        {
            navigator.Push<SettingsMenuScreen>();
        };

        pauseMenu.OnMainMenuPressed += () =>
        {
            // Request session cleanup before returning to main menu
            eventBus.Publish(new SessionResetRequestedEvent());

            navigator.PopAll();
            navigator.Push<MainMenuScreen>();
            stateManager.TransitionTo<MainMenuState>();
        };

        // Wire up settings menu actions
        settingsMenu.OnBackPressed += () =>
        {
            navigator.Pop();
        };

        // Sync initial mode selection with pending mode from config
        if (gameModeManager != null)
        {
            newGameMenu.SetSelectedMode(gameModeManager.PendingModeId ?? "silence");
        }

        // Live preference: hide the GAME MODE section on the New Game screen when set.
        newGameMenu.SetHideModeSelectionResolver(() => data.Core.Debug.HideGameModeSelection);

        // Wire up mode selection
        newGameMenu.OnModeSelected += (modeId) =>
        {
            gameModeManager?.SetPendingMode(modeId);
            Log.Info(LogCategory.UI, $"Game mode selected: {modeId}");
        };

        // Wire up difficulty selection
        newGameMenu.OnDifficultySelected += (level) =>
        {
            difficultyService?.SetDifficulty(level);
            Log.Info(LogCategory.UI, $"Difficulty selected: {level}");
            gameModeManager?.ActivatePendingMode();
            navigator.PopAll();
            stateManager.TransitionTo<PlayingState>();
        };

        newGameMenu.OnBackPressed += () =>
        {
            navigator.Pop();
        };

        // Note: Settings are applied via the config reload pipeline when preferences are saved.
        // The SettingsMenuScreen saves preferences on close, which triggers OnConfigReloaded in GameRoot,
        // which then calls the appropriate Configure/Update methods on all systems (audio, graphics, etc.)

        Log.Info(LogCategory.UI, "Menu system initialized");

        return new MenuSystemResult(navigator, mainMenu, pauseMenu, settingsMenu, newGameMenu);
    }
}
