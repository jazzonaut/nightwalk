using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Difficulty;
using Nightwalk.Core.Events;
using Nightwalk.Core.GameMode;
using Nightwalk.Core.GameState;
using Nightwalk.Godot.Camera;
using Nightwalk.Godot.Data;
using Nightwalk.Godot.Diagnostics;
using GameDataService = Nightwalk.Core.Config.GameDataService;
using Nightwalk.Godot.GameState;
using Nightwalk.Godot.Rendering;
using Nightwalk.Godot.Rendering.Effects;
using Nightwalk.Godot.Rendering.Sky;
using Nightwalk.Godot.Resonance;
using Nightwalk.Godot.Setup;
using Nightwalk.Godot.UI.HUD;
using Nightwalk.Godot.UI.Menus;

namespace Nightwalk.Godot;

/// <summary>
/// Main entry point for the game.
/// Manages game lifecycle, tick loop, and render interpolation.
/// </summary>
public partial class GameRoot : Node3D
{
    // Fields initialized in _Ready() - use null! pattern for Godot lifecycle
    private IEventBus _eventBus = null!;
    private GameDataService _data = null!;
    private GameStateManager _stateManager = null!;
    private DifficultyService _difficultyService = null!;
    private GameModeManager _gameModeManager = null!;

    // Timing
    private ulong _lastWorldTickTimestampUsec;
    private bool _gameStarted;

    // Setup results (own their cleanup)
    private PostProcessingResult? _postProcessResult;
    private AudioSystemResult? _audioResult;
    private WorldInitResult? _worldResult;
    private PlayerSpawnResult? _playerResult;
    private MenuSystemResult? _menuResult;
    private SkySystemResult? _skySystemResult;

    // Components created directly in GameRoot
    private WorldEnvironment? _worldEnvironment;
    private VisualPresetManager? _presetManager;
    private GameHUD? _gameHUD;
    private GameplaySystemsResult? _gameplayResult;
    private ScoreEffectSystem? _scoreEffectSystem;
    private ScreensaverController? _screensaverController;
    private ResonanceModeSystem? _resonanceModeSystem;
    private ResonanceVisualEffects? _resonanceVisualEffects;
    private SkycraftSystemResult? _skycraftResult;

    public override void _Ready()
    {
        // CRITICAL: Set process priority to ensure proper tick/render ordering
        // GameRoot._Process must run BEFORE EntityRenderer._Process for interpolation
        ProcessPriority = -100;

        // 0. Initialize logging with minimal output until config loads
        Log.SetLogger(new GodotLogger(debugEnabled: false));
        Log.Info(LogCategory.Config, "Nightwalk initializing...");

        // 1. Initialize event bus
        _eventBus = new EventBus();

        // 1b. Initialize difficulty service
        _difficultyService = new DifficultyService();

        // 1c. Initialize game mode manager (modes registered after gameplay setup)
        _gameModeManager = new GameModeManager(_eventBus);

        // 2. Load configuration with user preferences support
        var systemConfigProvider = new TomlDataLoader(ConfigConsts.SystemConfigPath);
        var userConfigLoader = new UserConfigLoader();

        // Create merging provider that combines system + user configs
        var mergingProvider = new MergingConfigProvider(
            systemConfigProvider,
            userConfigLoader.LoadRaw
        );

        _data = new GameDataService(mergingProvider);
        _data.ConfigureUserPreferences(
            userConfigLoader.LoadRaw,
            userConfigLoader.Save
        );

        try
        {
            _data.LoadAll();
            Log.Info(LogCategory.Config, "Configuration loaded successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(LogCategory.Config, "FATAL: Failed to load configuration", ex);
            Log.Error(LogCategory.Config, "Ensure all TOML files exist in res://data/");
            throw;
        }

        // Apply window mode immediately to prevent flash of wrong size
        EnvironmentSetup.ApplyWindowSettings(_data.Core.Graphics);

        // Subscribe to config reload events (needed for settings menu in all builds)
        _data.OnConfigReloaded += OnConfigReloaded;

#if DEBUG
        // 2a. Start hot reload file watching (dev only)
        var systemDataPath = ProjectSettings.GlobalizePath(ConfigConsts.SystemConfigPath);
        userConfigLoader.EnsureDirectoryExists();
        var userDataPath = userConfigLoader.GetDirectoryPath();
        _data.StartWatching(systemDataPath, userDataPath);
        Log.Info(LogCategory.Config, $"Hot reload enabled for: {systemDataPath} and {userDataPath}");
#endif

        // 2b. Reconfigure logging based on loaded config
        ConfigureLogging();

        // 3. Setup world environment
        _worldEnvironment = EnvironmentSetup.Configure(this, _data.Core.Graphics);

        // 3b. Setup animated sky effects
        _skySystemResult = SkySystemSetup.Configure(this, _data.Core.SkyEffects);

        // 4. Initialize visual presets from config (must be before post-processing)
        VisualPreset.InitializeDefault(_data.Core.Graphics, _data.Core.Weather);

        // 5. Setup post-processing effects
        _postProcessResult = PostProcessingSetup.Configure(this, _data.Core.Graphics);

        // 5. Setup unified HUD system (hidden until gameplay starts)
        _gameHUD = new GameHUD { Name = "GameHUD", Visible = false };
        _gameHUD.Configure(_data.Core.Debug.ShowFps);
        AddChild(_gameHUD);

        // 6. Setup visual preset system
        SetupPresetSystem();

        // 7. Setup audio system
        _audioResult = AudioSystemSetup.Configure(this, _data.Audio);

        // 8. Initialize the game world
        _worldResult = WorldInitializer.Initialize(this, _data.Worldgen, _eventBus);

        // 9. Spawn player and related systems
        _playerResult = PlayerSpawner.Spawn(
            this,
            _data,
            _eventBus,
            _audioResult.SfxManager,
            _audioResult.SynthLoopManager,
            _audioResult.AmbientManager,
            _presetManager
        );

        // 10. Connect tool manager to HUD
        if (_playerResult.ToolManager != null && _gameHUD != null)
        {
            _playerResult.ToolManager.OnToolChanged += _gameHUD.ShowTool;
        }

        // 10a. Connect audio managers to tool manager for procedural sounds
        _playerResult.ToolManager?.SetSfxManager(_audioResult.SfxManager);
        _playerResult.ToolManager?.SetSynthLoopManager(_audioResult.SynthLoopManager);

        // 10a2. Configure camera attributes (DOF, auto-exposure)
        EnvironmentSetup.UpdateCameraAttributes(_playerResult.Player.Camera, _data.Core.Graphics);

        // 10b. Connect player for fuel indicator
        _gameHUD?.SetPlayer(_playerResult.Player);

        // 10c. Connect sky dome to follow player position
        _skySystemResult?.SetFollowTarget(_playerResult.Player);

        // 11. Setup gameplay systems (scoring, building tracking, etc.)
        _gameplayResult = GameplaySystemsSetup.Configure(this, _eventBus, _worldResult, _data, _difficultyService);
        _gameplayResult.SetSfxManager(_audioResult.SfxManager);
        _gameHUD?.Connect(_eventBus);

        // 11a. Register game modes
        _gameModeManager.RegisterMode(new SilenceMode());
        _gameModeManager.RegisterMode(new ResonanceMode(_eventBus, _data, _gameplayResult.ScoreService));
        _gameModeManager.SetPendingMode(_data.Resonance.Mode.DefaultMode);

        // 11b. Setup Resonance mode system (manages Warden, Seekers in Godot)
        _resonanceModeSystem = new ResonanceModeSystem { Name = "ResonanceModeSystem" };
        _resonanceModeSystem.Initialize(_eventBus, _data, _gameModeManager, _playerResult.Player);
        AddChild(_resonanceModeSystem);

        // 11b2. Setup Resonance visual effects (sky tint, vignette)
        _resonanceVisualEffects = new ResonanceVisualEffects { Name = "ResonanceVisualEffects" };
        _resonanceVisualEffects.Initialize(_eventBus, _worldEnvironment);
        AddChild(_resonanceVisualEffects);

        // 11c. Connect score service to player for fall damage and tool manager for placement costs
        _playerResult.Player.SetScoreService(_gameplayResult.ScoreService);
        _playerResult.Player.SetDifficultyService(_difficultyService);
        _playerResult.ToolManager!.SetScoreService(_gameplayResult.ScoreService);
        _playerResult.ToolManager!.SetDifficultyService(_difficultyService);
        _playerResult.ToolManager!.SetBeaconSyncManager(_gameplayResult.BeaconSyncManager);
        _playerResult.PrismPOV.SetSyncManager(_gameplayResult.BeaconSyncManager);
        _gameplayResult.BeaconSyncManager.SetFlybyCamera(_playerResult.FlybyCamera);
        _gameplayResult.BeaconSyncManager.SetPrismPOVController(_playerResult.PrismPOV);
        _playerResult.Player.OnInteractPressed += OnPlayerInteract;

        // 11c. Initialize mouse input scaling for current resolution
        _playerResult.Player.UpdateContentScaleCompensation();

        // 11e. Setup score visual effects
        _scoreEffectSystem = new ScoreEffectSystem { Name = "ScoreEffectSystem" };
        _scoreEffectSystem.Configure(_postProcessResult.Material, _presetManager, _eventBus, _worldEnvironment, _data);
        AddChild(_scoreEffectSystem);

        // 11f. Setup Skycraft ambient air-traffic system (ADR-0016)
        _skycraftResult = SkycraftSystemSetup.Configure(
            this,
            _eventBus,
            _worldResult.ChunkManager,
            _data.Skycraft,
            _data.Worldgen.Chunks.Size);

        // 11g. Setup screensaver controller
        _screensaverController = new ScreensaverController(_data.Gameplay.Screensaver);
        _screensaverController.SetCamera(_playerResult.Player.Camera!, _playerResult.Player);
        _screensaverController.SetChunkManager(_worldResult.ChunkManager);
        _playerResult.Player.SetScreensaverController(_screensaverController);

        _gameStarted = true;

        // 12. Setup game state system
        SetupGameStates();
    }

    private void ConfigureLogging()
    {
        var debug = _data.Core.Debug;

        if (!debug.LoggingEnabled)
        {
            Log.SetLogger(new GodotLogger(debugEnabled: false));
        }
        else if (debug.LoggingCategories.Length > 0)
        {
            Log.SetLogger(new GodotLogger(debug.LoggingCategories));
            Log.Info(LogCategory.Config, $"Debug logging enabled for: {string.Join(", ", debug.LoggingCategories)}");
        }
        else
        {
            Log.SetLogger(new GodotLogger(debugEnabled: true));
            Log.Info(LogCategory.Config, "Debug logging enabled for all categories");
        }
    }

    private void SetupGameStates()
    {
        _stateManager = new GameStateManager(_eventBus);

        // Register all game states
        _stateManager.RegisterState(new MainMenuState(_stateManager));
        _stateManager.RegisterState(new PlayingState(_stateManager));
        _stateManager.RegisterState(new PausedState(_stateManager));

        // Setup menu system
        _menuResult = MenuSystemSetup.Configure(this, _stateManager, _eventBus, _data, _difficultyService, _gameModeManager);

        // Subscribe to state changes to update player input and menus
        _eventBus.Subscribe<Core.GameState.Events.GameStateChangedEvent>(OnGameStateChanged);

        // Subscribe to session reset requests
        _eventBus.Subscribe<Core.GameState.Events.SessionResetRequestedEvent>(OnSessionResetRequested);

        // Start in main menu
        _stateManager.TransitionTo<MainMenuState>();

        Log.Info(LogCategory.Sim, "Game state system initialized");
    }

    private void OnGameStateChanged(Core.GameState.Events.GameStateChangedEvent e)
    {
        // Update player input based on state
        if (_playerResult != null)
        {
            _playerResult.Player.InputEnabled = _stateManager.AllowsPlayerInput;
        }

        // Update HUD visibility - only show during gameplay
        if (_gameHUD != null)
        {
            _gameHUD.Visible = e.NewStateName == GameStateNames.Playing;

            // Reconfigure HUD when game starts based on difficulty (hide score in Observer mode)
            if (e.NewStateName == GameStateNames.Playing)
            {
                _gameHUD.Configure(_data.Core.Debug.ShowFps, _difficultyService.IsScoreHUDHidden);
            }
        }

        // Sync mode-gated tools (Decoy is Resonance-only). Runs after the menu flow has
        // called ActivatePendingMode, so ActiveMode is current here.
        if (_playerResult?.ToolManager != null)
        {
            if (e.NewStateName == GameStateNames.Playing)
            {
                _playerResult.ToolManager.SyncToolsForMode(_gameModeManager.ActiveMode?.Id);
            }
            else if (e.NewStateName == GameStateNames.MainMenu)
            {
                _playerResult.ToolManager.SyncToolsForMode(null);
            }
        }

        // Update menus based on state
        if (_menuResult != null)
        {
            switch (e.NewStateName)
            {
                case GameStateNames.MainMenu:
                    _gameModeManager.DeactivateCurrentMode();
                    _menuResult.Navigator.PopAll();
                    _menuResult.Navigator.Push<MainMenuScreen>();
                    break;

                case GameStateNames.Paused:
                    _menuResult.Navigator.PopAll();
                    _menuResult.Navigator.Push<PauseMenuScreen>();
                    break;

                case GameStateNames.Playing:
                    _menuResult.Navigator.PopAll();
                    break;
            }
        }
    }

    private void OnConfigReloaded(IReadOnlySet<string> changedConfigs)
    {
        Log.Info(LogCategory.Config, $"Config hot reload triggered - changed: {string.Join(", ", changedConfigs)}");

        // Reconfigure logging
        ConfigureLogging();

        // Reconfigure player movement/gameplay parameters
        _playerResult?.Player.Configure(_eventBus);

        // Reconfigure tool visuals (light colors, ranges, etc.)
        _playerResult?.ToolManager.ReconfigureTools();

        // Reconfigure audio systems
        _audioResult?.MusicManager?.Configure(_data.Audio.Music);
        _audioResult?.SfxManager?.Configure(_data.Audio.Sfx);
        _audioResult?.AmbientManager?.Configure(_data.Audio.Sfx);
        _audioResult?.SynthLoopManager?.Configure(_data.Audio.Sfx);

        // Reconfigure rain system
        _playerResult?.RainSystem?.Configure(_data.Core.Weather);

        // Reconfigure HUD (preserve difficulty-based score visibility)
        _gameHUD?.Configure(_data.Core.Debug.ShowFps, _difficultyService?.IsScoreHUDHidden ?? false);

        // Reconfigure post-processing effects
        _postProcessResult?.Configure(_data.Core.Graphics);

        // Reconfigure display settings
        EnvironmentSetup.ApplyWindowSettings(_data.Core.Graphics);
        EnvironmentSetup.UpdateViewport(GetViewport(), _data.Core.Graphics);
        EnvironmentSetup.UpdateEnvironment(_worldEnvironment, _data.Core.Graphics);

        // Reconfigure camera attributes (DOF, auto-exposure)
        EnvironmentSetup.UpdateCameraAttributes(_playerResult?.Player.Camera, _data.Core.Graphics);

        // Reconfigure sky dome and silhouette effects
        _skySystemResult?.Configure(_data.Core.SkyEffects);

        // Update mouse input scaling for new resolution
        _playerResult?.Player.UpdateContentScaleCompensation();

        // Reconfigure building highlight materials
        _gameplayResult?.BuildingHighlights.UpdateMaterials();

        // Reconfigure collectible materials
        _gameplayResult?.CollectibleSpawner.UpdateMaterials();

        // Reconfigure car trail materials
        _gameplayResult?.CarTrailRenderer.UpdateMaterials();

        // Reconfigure beacon systems
        _gameplayResult?.BeaconSyncManager.Reconfigure(_data.Beacons);
        _gameplayResult?.BeaconSpawner.Reconfigure(_data.Beacons);
        _playerResult?.PrismPOV.Configure(_data.Beacons.Prism);

        // Reconfigure screensaver controller
        _screensaverController?.Configure(_data.Gameplay.Screensaver);

        // Reconfigure Skycraft (density slider, mix, profile band tuning, render thresholds)
        _skycraftResult?.Reconfigure(_data.Skycraft);

        // Refresh settings menu values (in case config was edited externally)
        _menuResult?.SettingsMenu.RefreshValues();

        // Reload world if worldgen config changed
        if (changedConfigs.Contains("worldgen"))
        {
            ReloadWorld();
        }

        // Re-apply current visual preset (hot reload resets to config defaults)
        _presetManager?.ReapplyCurrentPreset();

        Log.Info(LogCategory.Config, "Config hot reload complete");
    }

    private void ReloadWorld()
    {
        Log.Info(LogCategory.World, "Reloading world due to worldgen config change...");

        // Cancel screensaver if active (before cleanup)
        _screensaverController?.ForceCancel();

        // Save player position to restore after reload
        var playerPos = _playerResult?.Player.GlobalPosition ?? Vector3.Zero;

        // Cleanup old gameplay systems (disconnects from old ChunkManager)
        _gameplayResult?.Cleanup();
        _gameplayResult = null;

        // Cleanup Skycraft (disconnects from old ChunkManager; will be re-created below)
        _skycraftResult?.Cleanup();
        _skycraftResult = null;

        // Cleanup old world (renderer, world state)
        _worldResult?.Cleanup();
        _worldResult = null;

        // Clear static caches that persist across world reloads
        SmokeEmitter.ClearCache();

        // Recreate world with new config
        _worldResult = WorldInitializer.Initialize(this, _data.Worldgen, _eventBus);

        // Recreate gameplay systems connected to new ChunkManager
        _gameplayResult = GameplaySystemsSetup.Configure(this, _eventBus, _worldResult, _data, _difficultyService);
        _gameplayResult.SetSfxManager(_audioResult?.SfxManager);

        // Recreate Skycraft system attached to the new ChunkManager
        _skycraftResult = SkycraftSystemSetup.Configure(
            this,
            _eventBus,
            _worldResult.ChunkManager,
            _data.Skycraft,
            _data.Worldgen.Chunks.Size);

        // Re-assign score service and difficulty service to player and tool manager (old one was cleaned up)
        _playerResult?.Player.SetScoreService(_gameplayResult.ScoreService);
        _playerResult?.Player.SetDifficultyService(_difficultyService);
        _playerResult?.ToolManager.SetScoreService(_gameplayResult.ScoreService);
        _playerResult?.ToolManager.SetDifficultyService(_difficultyService);
        _playerResult?.ToolManager.SetBeaconSyncManager(_gameplayResult.BeaconSyncManager);
        _playerResult?.PrismPOV.SetSyncManager(_gameplayResult.BeaconSyncManager);
        if (_playerResult != null)
        {
            _gameplayResult.BeaconSyncManager.SetFlybyCamera(_playerResult.FlybyCamera);
            _gameplayResult.BeaconSyncManager.SetPrismPOVController(_playerResult.PrismPOV);
        }

        // Reconnect screensaver to new ChunkManager
        _screensaverController?.SetChunkManager(_worldResult.ChunkManager);

        // Trigger chunk loading around player position
        _worldResult.ChunkManager.Update(playerPos.X, playerPos.Z);

        Log.Info(LogCategory.World, "World reload complete");
    }

    private void OnSessionResetRequested(Core.GameState.Events.SessionResetRequestedEvent e)
    {
        Log.Info(LogCategory.Sim, "Session reset requested - cleaning up game session");

        // Cancel screensaver if active
        _screensaverController?.ForceCancel();

        // Stop all SFX loops (flight, jetpack, etc.)
        _audioResult?.SfxManager?.StopAll();
        _audioResult?.SynthLoopManager?.StopAll();

        // Reset world state (reload chunks around origin)
        if (_worldResult != null)
        {
            _worldResult.ChunkManager.ResetForSession();
            _worldResult.WorldState.Tick(0); // Reset tick counter
            _worldResult.ChunkManager.Update(0, 0); // Reset to origin
        }

        // Reset player position to spawn point
        if (_playerResult != null)
        {
            // Exit prism POV if active
            _playerResult.PrismPOV.Reset();

            _playerResult.Player.GlobalPosition = PlayerSpawner.SpawnPosition;
            _playerResult.Player.Rotation = PlayerSpawner.SpawnRotation;
            _playerResult.Player.ResetVelocity();
            if (_playerResult.Player.Head != null)
                _playerResult.Player.Head.Rotation = Vector3.Zero;

            _playerResult.ToolManager.ResetForSession();
        }

        // Reset gameplay systems, game mode, and HUD
        _gameplayResult?.Reset();
        _gameModeManager.Reset();
        _resonanceModeSystem?.Reset();
        _resonanceVisualEffects?.Reset();
        _skycraftResult?.Reset();
        _gameHUD?.Reset();
    }

    private void SetupPresetSystem()
    {
        // Create preset manager
        if (_postProcessResult?.Material != null)
        {
            _presetManager = new VisualPresetManager(
                _postProcessResult.Material,
                _worldEnvironment,
                _data.UserPreferences,
                _data.Core.Graphics.VisualPresetIndex);

            // Connect preset changes to HUD
            if (_gameHUD != null)
            {
                _presetManager.SetOnPresetChanged(_gameHUD.ShowPreset);
            }

            Log.Info(LogCategory.Config, "Visual preset system initialized");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_gameStarted || _worldResult == null) return;

        // Only tick the world if the current state allows it
        if (!_stateManager.AllowsGameTick) return;

        // Advance simulation
        _worldResult.WorldState.Tick(_worldResult.WorldState.CurrentTick + 1);

        // Update chunk loading based on player or screensaver camera position
        if (_playerResult != null)
        {
            // During screensaver, use camera position for chunk loading and rain
            Vector3 focusPos;
            if (_screensaverController?.IsActive == true)
            {
                focusPos = _screensaverController.CameraPosition;
                // Make rain follow the screensaver camera
                _playerResult.RainSystem?.SetPositionOverride(focusPos);
            }
            else
            {
                focusPos = _playerResult.Player.GlobalPosition;
                // Rain follows player normally
                _playerResult.RainSystem?.SetPositionOverride(null);
            }
            _worldResult.ChunkManager.Update(focusPos.X, focusPos.Z);

            // Update gameplay systems (building tracking, etc.)
            // Still use player position for gameplay logic
            var playerPos = _playerResult.Player.GlobalPosition;
            _gameplayResult?.Update(playerPos, _playerResult.Player.IsOnFloor());

            // Update Skycraft ambient simulation (ADR-0016). Spawns happen at the
            // active region's far edge, so we feed the player's actual position even
            // during screensaver — Skycraft are ambient and shouldn't follow the camera.
            _skycraftResult?.Update((float)delta, playerPos);
        }

        // Update game mode
        _gameModeManager.Tick(delta);

        _lastWorldTickTimestampUsec = Time.GetTicksUsec();
    }

    public override void _Process(double delta)
    {
#if DEBUG
        // Check for pending config hot reload
        _data.ProcessPendingReload();
#endif

        // Process game state input and updates (always, even when paused)
        _stateManager.ProcessInput(delta);
        _stateManager.Update(delta);

        if (!_gameStarted || _worldResult == null) return;

        // Update camera position for LOD calculations, then process progressive rendering.
        // cameraPos is the player's position (existing ChunkRenderer contract). Skycraft's
        // tier-swap needs the actual rendering camera instead — during screensaver the
        // camera is far from the player and near-tier nodes must follow the lens, not the
        // body. We resolve that separately rather than changing the ChunkRenderer contract.
        var cameraPos = _playerResult?.Player.GlobalPosition ?? Vector3.Zero;
        _worldResult.ChunkRenderer.SetCameraPosition(cameraPos);
        _worldResult.ChunkRenderer.ProcessRenderQueue();

        Vector3 skycraftCameraPos;
        if (_screensaverController?.IsActive == true)
            skycraftCameraPos = _screensaverController.CameraPosition;
        else
            skycraftCameraPos = _playerResult?.Player.Camera?.GlobalPosition ?? cameraPos;
        _skycraftResult?.UpdateCamera(skycraftCameraPos);

        // Calculate interpolation factor between physics ticks
        float tickFraction = 0f;
        if (_lastWorldTickTimestampUsec > 0)
        {
            var elapsedSeconds = (Time.GetTicksUsec() - _lastWorldTickTimestampUsec) / 1_000_000.0;
            var tickDurationSeconds = 1.0 / Engine.PhysicsTicksPerSecond;
            tickFraction = (float)(elapsedSeconds / tickDurationSeconds);
            tickFraction = Mathf.Clamp(tickFraction, 0f, 1f);
        }

        // Update post-processing uniforms (wrap time to prevent float precision loss after ~24 hours)
        const ulong dayMillis = 86400000; // 24 hours in milliseconds
        _postProcessResult?.Material.SetShaderParameter("time", (float)(Time.GetTicksMsec() % dayMillis) / 1000.0f);

        // Update speed-based effects (flight mode and flyby camera)
        if (_playerResult != null && _postProcessResult != null)
        {
            float speedFactor;
            if (_playerResult.Player.IsInFlyby)
            {
                // Use flyby camera's speed factor during cinematic
                speedFactor = _playerResult.Player.FlybySpeedFactor;
            }
            else if (_playerResult.Player.IsFlightModeActive)
            {
                // Use player's speed factor in flight mode
                speedFactor = _playerResult.Player.SpeedFactor;
            }
            else
            {
                speedFactor = 0f;
            }
            _postProcessResult.Material.SetShaderParameter("speed_factor", speedFactor);
        }

        // Update gameplay systems (combo decay, etc.)
        if (_stateManager.AllowsGameTick)
        {
            _gameplayResult?.ProcessUpdate((float)delta);
            _gameModeManager.Process(delta);
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Handle visual preset switching (keys 1-9)
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.PhysicalKeycode >= Key.Key1 && keyEvent.PhysicalKeycode <= Key.Key9)
            {
                int index = (int)keyEvent.PhysicalKeycode - (int)Key.Key1;
                _presetManager?.SetPreset(index);
                _audioResult?.SfxManager?.Play(Core.Audio.SfxId.PresetSwitch);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // Pause is handled by game state system via _Process -> _stateManager.ProcessInput()

        // Capture mouse for FPS controls (only in playing state)
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && _stateManager.AllowsPlayerInput)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        CleanupGame();
    }

    private void OnPlayerInteract()
    {
        if (_gameplayResult == null || _playerResult == null)
            return;

        // Try to activate a nearby beacon
        // (Prism POV toggling is handled by PlayerEntity before this is invoked)
        var spawnedPrism = _gameplayResult.TryActivateBeacon(_playerResult.Player.GlobalPosition);

        // Auto-enter POV mode for the spawned prism
        if (spawnedPrism != null)
        {
            _playerResult.PrismPOV.EnterPOV(spawnedPrism);
        }
    }

    private void CleanupGame()
    {
        Log.Info(LogCategory.World, "Shutting down game...");

#if DEBUG
        // Stop hot reload file watching
        _data.OnConfigReloaded -= OnConfigReloaded;
        _data.StopWatching();
#endif

        // Unsubscribe player interact
        if (_playerResult?.Player != null)
        {
            _playerResult.Player.OnInteractPressed -= OnPlayerInteract;
        }

        // Unsubscribe event handlers first
        _eventBus.Unsubscribe<Core.GameState.Events.GameStateChangedEvent>(OnGameStateChanged);
        _eventBus.Unsubscribe<Core.GameState.Events.SessionResetRequestedEvent>(OnSessionResetRequested);

        // Disconnect tool manager from HUD
        if (_playerResult?.ToolManager != null && _gameHUD != null)
        {
            _playerResult.ToolManager.OnToolChanged -= _gameHUD.ShowTool;
        }

        // Disconnect HUD from event bus
        _gameHUD?.Disconnect();

        // Cleanup in reverse order of initialization
        _menuResult?.Cleanup();
        _skycraftResult?.Cleanup();
        _resonanceVisualEffects?.Cleanup();
        _resonanceModeSystem?.Cleanup();
        _gameplayResult?.Cleanup();
        _playerResult?.Cleanup();
        _worldResult?.Cleanup();

        // Cleanup preset system before post-processing (preset manager uses the material)
        _presetManager = null;
        _scoreEffectSystem = null;  // Node freed automatically as child

        _skySystemResult?.Cleanup();
        _postProcessResult?.Cleanup();
        _audioResult?.Cleanup();
        _gameHUD?.QueueFree();

        // Clear all references
        _menuResult = null;
        _skycraftResult = null;
        _gameplayResult = null;
        _playerResult = null;
        _worldResult = null;
        _skySystemResult = null;
        _postProcessResult = null;
        _audioResult = null;
        _gameHUD = null;
        _worldEnvironment = null;
        _gameStarted = false;
    }
}
