using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Godot.Audio;
using Nightwalk.Godot.Beacons;
using Nightwalk.Godot.Camera;
using Nightwalk.Godot.Player;
using Nightwalk.Godot.Rendering;
using Nightwalk.Godot.Ziplines;
using GameDataService = Nightwalk.Core.Config.GameDataService;

namespace Nightwalk.Godot.Setup;

/// <summary>
/// Result of player spawning.
/// </summary>
public sealed record PlayerSpawnResult(
    PlayerEntity Player,
    PlayerToolManager ToolManager,
    PrismPOVController PrismPOV,
    FlybyCamera FlybyCamera,
    RainSystem? RainSystem,
    AmbientManager? AmbientManager
)
{
    public void Cleanup()
    {
        if (RainSystem != null && AmbientManager != null)
        {
            RainSystem.OnIntensityChanged -= AmbientManager.SetRainIntensity;
        }

        // Note: Player, ToolManager, and RainSystem are freed automatically
        // as children of GameRoot when it exits the tree
    }
}

/// <summary>
/// Spawns and configures the player and related systems.
/// </summary>
public static class PlayerSpawner
{
    public static readonly Vector3 SpawnPosition = new(0f, 2f, 0f);
    public static readonly Vector3 SpawnRotation = new(0f, Mathf.DegToRad(-45f), 0f);

    /// <summary>
    /// Spawns the player with all attached systems.
    /// </summary>
    /// <param name="parent">The parent node to add the player to.</param>
    /// <param name="data">Game data service for configuration.</param>
    /// <param name="eventBus">Event bus for tool events.</param>
    /// <param name="sfxManager">Optional SFX manager for player sounds.</param>
    /// <param name="synthLoopManager">Optional synth loop manager for procedural audio.</param>
    /// <param name="ambientManager">Optional ambient manager for rain sounds.</param>
    /// <param name="presetManager">Optional preset manager for rain system.</param>
    /// <returns>The spawned player and related components.</returns>
    public static PlayerSpawnResult Spawn(
        Node parent,
        GameDataService data,
        IEventBus eventBus,
        SfxManager? sfxManager,
        SynthLoopManager? synthLoopManager,
        AmbientManager? ambientManager,
        VisualPresetManager? presetManager)
    {
        Log.Info(LogCategory.Entity, "Spawning player...");

        // Create and configure player
        var player = new PlayerEntity(data) { Name = "Player" };
        player.Configure(eventBus);

        // Connect SFX to player
        if (sfxManager != null)
        {
            player.SetSfxService(sfxManager);
            player.SetFallDamageSfxManager(sfxManager);
        }

        // Connect synth loop manager for falling wind sounds
        if (synthLoopManager != null)
        {
            player.SetFallDamageSynthLoopManager(synthLoopManager);
            player.SetSynthLoopManager(synthLoopManager);
        }

        parent.AddChild(player);

        // Position player slightly above ground
        player.Position = SpawnPosition;
        player.Rotation = SpawnRotation;
        if (player.Head != null)
            player.Head.Rotation = Vector3.Zero;

        // Spawn rain system if enabled
        RainSystem? rainSystem = null;
        if (data.Core.Weather.RainEnabled)
        {
            rainSystem = SpawnRainSystem(player, data.Core.Weather, ambientManager, presetManager);
        }

        // Create zipline registry (shared between tools and player)
        var ziplineRegistry = new ZiplineRegistry();

        // Spawn tool manager
        var toolManager = SpawnToolManager(player, data, eventBus, ziplineRegistry);

        // Setup zipline rider
        var ziplineRider = new ZiplineRider(ziplineRegistry, data);
        player.SetZiplineRider(ziplineRider);

        // Setup prism POV controller (sync manager set later by GameRoot)
        var prismPOV = new PrismPOVController(data.Beacons.Prism);
        prismPOV.SetCamera(player.Camera!, player);
        player.SetPrismPOVController(prismPOV);

        // Setup flyby camera (sync manager set later by GameRoot)
        var flybyCamera = new FlybyCamera(data.Gameplay.FlybyCamera);
        flybyCamera.SetCamera(player.Camera!, player);
        player.SetFlybyCamera(flybyCamera);

        player.InitializeViewModes();

        Log.Info(LogCategory.Entity, "Player spawned at origin");

        return new PlayerSpawnResult(player, toolManager, prismPOV, flybyCamera, rainSystem, ambientManager);
    }

    private static RainSystem SpawnRainSystem(
        PlayerEntity player,
        WeatherConfig weatherConfig,
        AmbientManager? ambientManager,
        VisualPresetManager? presetManager)
    {
        var rainSystem = new RainSystem { Name = "RainSystem" };
        rainSystem.Configure(weatherConfig);
        player.AddChild(rainSystem);
        presetManager?.SetRainSystem(rainSystem);

        // Connect rain audio to ambient manager
        if (ambientManager != null)
        {
            rainSystem.OnIntensityChanged += ambientManager.SetRainIntensity;
            ambientManager.SetRainIntensity(weatherConfig.RainIntensity);
        }

        Log.Info(LogCategory.Entity, "Rain system spawned");
        return rainSystem;
    }

    private static PlayerToolManager SpawnToolManager(
        PlayerEntity player,
        GameDataService data,
        IEventBus eventBus,
        ZiplineRegistry ziplineRegistry)
    {
        var toolManager = new PlayerToolManager { Name = "ToolManager" };
        toolManager.Configure(data);
        toolManager.SetEventBus(eventBus);
        toolManager.SetZiplineRegistry(ziplineRegistry);
        player.Head!.AddChild(toolManager);
        player.SetToolManager(toolManager);

        // Initialize tools after manager is in the scene tree
        // (required for physics space state access)
        toolManager.InitializeTools(player);

        Log.Info(LogCategory.Entity, "Tool manager spawned");
        return toolManager;
    }
}
