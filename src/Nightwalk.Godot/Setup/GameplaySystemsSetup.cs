using Godot;
using Nightwalk.Core.Beacons;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.CityAliveness;
using Nightwalk.Core.Collectibles;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Difficulty;
using Nightwalk.Core.Events;
using Nightwalk.Core.Scoring;
using Nightwalk.Godot.Audio;
using Nightwalk.Godot.Beacons;
using Nightwalk.Godot.Buildings;
using Nightwalk.Godot.CityAliveness;
using Nightwalk.Godot.Collectibles;
using GameDataService = Nightwalk.Core.Config.GameDataService;

namespace Nightwalk.Godot.Setup;

/// <summary>
/// Result of gameplay systems setup.
/// Contains scoring, building tracking, and future gameplay systems.
/// </summary>
public sealed class GameplaySystemsResult
{
    public ScoreService ScoreService { get; }
    public BuildingClimbService BuildingClimbService { get; }
    public BuildingHighlightRenderer BuildingHighlights { get; }
    public CollectibleSpawner CollectibleSpawner { get; }
    public BeaconSpawner BeaconSpawner { get; }
    public BeaconSyncManager BeaconSyncManager { get; }
    public BeaconTracker BeaconTracker { get; }
    public CityAlivenessService CityAlivenessService { get; }
    public CarTrailRenderer CarTrailRenderer { get; }
    public IDifficultyService? DifficultyService { get; }

    private readonly WorldInitResult _worldResult;

    /// <summary>
    /// Creates a new gameplay systems result. Note: This constructor connects event handlers,
    /// but it's safe because this class is only instantiated from GameplaySystemsSetup.Configure(),
    /// which is called from GameRoot._Ready() after the scene tree is fully set up.
    /// </summary>
    public GameplaySystemsResult(
        ScoreService scoreService,
        BuildingClimbService buildingClimbService,
        BuildingHighlightRenderer buildingHighlights,
        CollectibleSpawner collectibleSpawner,
        BeaconSpawner beaconSpawner,
        BeaconSyncManager beaconSyncManager,
        BeaconTracker beaconTracker,
        CityAlivenessService cityAlivenessService,
        CarTrailRenderer carTrailRenderer,
        WorldInitResult worldResult,
        IDifficultyService? difficultyService = null)
    {
        ScoreService = scoreService;
        BuildingClimbService = buildingClimbService;
        BuildingHighlights = buildingHighlights;
        CollectibleSpawner = collectibleSpawner;
        BeaconSpawner = beaconSpawner;
        BeaconSyncManager = beaconSyncManager;
        BeaconTracker = beaconTracker;
        CityAlivenessService = cityAlivenessService;
        CarTrailRenderer = carTrailRenderer;
        _worldResult = worldResult;
        DifficultyService = difficultyService;

        // Connect chunk events (safe: called from GameRoot._Ready())
        _worldResult.ChunkManager.OnChunkLoaded += OnChunkLoaded;
        _worldResult.ChunkManager.OnChunkUnloaded += OnChunkUnloaded;
    }

    private void OnChunkLoaded(ChunkLoadedEvent e)
    {
        BuildingHighlights.OnChunkLoaded(e.Coord, e.Data, CollectibleSpawner.Tracker, BuildingClimbService.Tracker);
        CollectibleSpawner.OnChunkLoaded(e.Coord, e.Data);
        BeaconSpawner.OnChunkLoaded(e.Coord, e.Data);
        CityAlivenessService.OnChunkLoaded(e.Coord, e.Data);
        CarTrailRenderer.OnChunkLoaded(e.Coord, e.Data, CityAlivenessService);
    }

    private void OnChunkUnloaded(ChunkUnloadedEvent e)
    {
        BuildingHighlights.OnChunkUnloaded(e.Coord);
        CollectibleSpawner.OnChunkUnloaded(e.Coord);
        BeaconSpawner.OnChunkUnloaded(e.Coord);
        CarTrailRenderer.OnChunkUnloaded(e.Coord);
    }

    /// <summary>
    /// Updates gameplay systems. Call from _PhysicsProcess when game tick is allowed.
    /// </summary>
    public void Update(Vector3 playerPosition, bool isOnGround)
    {
        BuildingClimbService.Update(playerPosition, isOnGround);
        CollectibleSpawner.UpdateProximity(playerPosition);
        BeaconSpawner.UpdateProximity(playerPosition);
    }

    /// <summary>
    /// Updates time-based gameplay systems. Call from _Process when game tick is allowed.
    /// </summary>
    public void ProcessUpdate(float delta)
    {
        ScoreService.Update(delta);
    }

    /// <summary>
    /// Attempts to activate a nearby beacon for sync.
    /// </summary>
    /// <param name="playerPosition">Player's current position.</param>
    /// <returns>The spawned prism if a beacon was activated, null otherwise.</returns>
    public PrismNode? TryActivateBeacon(Vector3 playerPosition)
    {
        return BeaconSpawner.TryActivateNearbyBeacon(playerPosition, BeaconSyncManager);
    }

    /// <summary>
    /// Cancels the current beacon sync if one is active.
    /// </summary>
    public void CancelBeaconSync()
    {
        BeaconSyncManager.CancelSync();
    }

    /// <summary>
    /// Whether a beacon sync is currently in progress.
    /// </summary>
    public bool IsBeaconSyncActive => BeaconSyncManager.IsSyncActive;

    /// <summary>
    /// Sets the SFX manager for procedural sounds.
    /// </summary>
    public void SetSfxManager(SfxManager? sfxManager)
    {
        CollectibleSpawner.SetSfxManager(sfxManager);
        BeaconSyncManager.SetSfxManager(sfxManager);
    }

    /// <summary>
    /// Resets all gameplay systems for a new session.
    /// </summary>
    public void Reset()
    {
        ScoreService.Reset();
        BuildingClimbService.Reset();
        BuildingHighlights.Reset();
        CollectibleSpawner.Reset();
        BeaconTracker.Reset();
        BeaconSyncManager.Reset();
        BeaconSpawner.Reset();
        CityAlivenessService.Reset();
        CarTrailRenderer.Reset();
    }

    public void Cleanup()
    {
        // Disconnect chunk events
        _worldResult.ChunkManager.OnChunkLoaded -= OnChunkLoaded;
        _worldResult.ChunkManager.OnChunkUnloaded -= OnChunkUnloaded;

        BuildingHighlights.QueueFree();
        CollectibleSpawner.QueueFree();
        BeaconSyncManager.QueueFree();
        BeaconSpawner.QueueFree();
        CarTrailRenderer.QueueFree();
        CityAlivenessService.Cleanup();

        // Dispose shared static resources
        PrismNode.DisposeSharedResources();
    }
}

/// <summary>
/// Configures gameplay systems (scoring, building tracking, collectibles, etc.).
/// </summary>
public static class GameplaySystemsSetup
{
    /// <summary>
    /// Creates and configures all gameplay systems.
    /// </summary>
    /// <param name="parent">Parent node for gameplay renderer nodes.</param>
    /// <param name="eventBus">Event bus for cross-system communication.</param>
    /// <param name="worldResult">World initialization result (for chunk manager).</param>
    /// <param name="data">Game data service for configuration.</param>
    /// <param name="difficultyService">Difficulty service for scaling values.</param>
    /// <returns>The gameplay systems result.</returns>
    public static GameplaySystemsResult Configure(
        Node parent,
        IEventBus eventBus,
        WorldInitResult worldResult,
        GameDataService data,
        IDifficultyService? difficultyService = null)
    {
        // Setup scoring system
        var scoreService = new ScoreService(eventBus, data, difficultyService);

        // Setup building tracking
        var buildingClimbService = new BuildingClimbService(
            eventBus,
            worldResult.ChunkManager,
            data
        );

        // Setup building highlight renderer
        var buildingHighlights = new BuildingHighlightRenderer(
            worldResult.ChunkManager,
            eventBus,
            data
        );
        parent.AddChild(buildingHighlights);

        // Setup collectible system
        var collectibleTracker = new CollectibleTracker(eventBus);
        var collectibleSpawner = new CollectibleSpawner(
            collectibleTracker,
            scoreService,
            data,
            difficultyService
        );
        parent.AddChild(collectibleSpawner);

        // Setup beacon system
        BeaconFrequency.Count = data.Beacons.Beacon.Frequencies.Length;
        var beaconTracker = new BeaconTracker(eventBus);
        var beaconSpawner = new BeaconSpawner(
            beaconTracker,
            data.Beacons,
            data
        );
        parent.AddChild(beaconSpawner);

        var beaconSyncManager = new BeaconSyncManager();
        beaconSyncManager.Configure(
            beaconTracker,
            data.Beacons,
            scoreService,
            beaconSpawner.GetBeaconsByFrequency,
            difficultyService
        );
        parent.AddChild(beaconSyncManager);

        // Wire up sync manager to spawner for despawn handling
        beaconSpawner.SetSyncManager(beaconSyncManager);

        // Setup city aliveness system (car trails on roads)
        var cityAlivenessService = new CityAlivenessService(
            eventBus,
            worldResult.ChunkManager,
            data
        );

        var carTrailRenderer = new CarTrailRenderer(
            worldResult.ChunkManager,
            eventBus,
            data
        );
        parent.AddChild(carTrailRenderer);

        Log.Info(LogCategory.Sim, "Gameplay systems initialized");

        return new GameplaySystemsResult(
            scoreService,
            buildingClimbService,
            buildingHighlights,
            collectibleSpawner,
            beaconSpawner,
            beaconSyncManager,
            beaconTracker,
            cityAlivenessService,
            carTrailRenderer,
            worldResult,
            difficultyService
        );
    }
}
