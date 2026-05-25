using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Beacons;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Collectibles;
using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Difficulty;
using Nightwalk.Core.Scoring;
using Nightwalk.Core.World.Buildings;
using Nightwalk.Godot.Audio;
using Nightwalk.Godot.Rendering.SubRenderers;
using Nightwalk.Godot.Spawning;

namespace Nightwalk.Godot.Collectibles;

/// <summary>
/// Lightweight data for a potential collectible location.
/// </summary>
public readonly struct CollectibleData
{
    public readonly CollectibleId Id;
    public readonly Vector3 Position;
    public readonly int Points;
    public readonly PolyhedronType ShapeType;
    public readonly float? Height;  // For building rooftops (used for height info in events)

    public CollectibleData(CollectibleId id, Vector3 position, int points, PolyhedronType shapeType, float? height = null)
    {
        Id = id;
        Position = position;
        Points = points;
        ShapeType = shapeType;
        Height = height;
    }
}

/// <summary>
/// Spawns and manages collectible polyhedron shapes on building rooftops and other locations.
/// Uses proximity-based spawning to only instantiate nodes when player is nearby.
/// </summary>
public sealed partial class CollectibleSpawner : ProximitySpawner<CollectibleId, CollectibleData, CollectibleShape>
{
    private readonly CollectibleTracker _tracker;
    private readonly IScoreService _scoreService;
    private readonly GameDataService _data;
    private readonly IDifficultyService? _difficultyService;
    private SfxManager? _sfxManager;
    private Action? _sfxCallback; // Cached so CreateNode doesn't allocate a fresh closure per spawn.
    private int _proximityUpdateCounter; // Used to throttle CollectibleShape.UpdateProximity calls.

    // Update collectible visibility (fade alpha) every 4th physics tick (~15Hz at 60Hz physics).
    // Each UpdateProximity call costs ~4 marshalled engine calls per collectible; skipping 3/4
    // of them is invisible to the player because the fade band is small.
    private const int ProximityUpdateInterval = 4;

    private CollectiblesConfig Config => _data.Gameplay.Collectibles;

    public CollectibleTracker Tracker => _tracker;

    public CollectibleSpawner(
        CollectibleTracker tracker,
        IScoreService scoreService,
        GameDataService data,
        IDifficultyService? difficultyService = null)
    {
        _tracker = tracker;
        _scoreService = scoreService;
        _data = data;
        _difficultyService = difficultyService;
    }

    /// <summary>
    /// Sets the SFX manager for playing collectible sounds.
    /// </summary>
    public void SetSfxManager(SfxManager? sfxManager)
    {
        _sfxManager = sfxManager;
        _sfxCallback = sfxManager != null ? PlayCollectibleSfx : null;
    }

    private void PlayCollectibleSfx()
    {
        _sfxManager?.PlayGenerated(SfxSynth.Collectible());
    }

    public override void _Ready()
    {
        Name = "CollectibleSpawner";
        Log.Info(LogCategory.Sim, "CollectibleSpawner initialized");
    }

    /// <summary>
    /// Updates collectible spawning based on player proximity.
    /// Overload that uses chunk size from config.
    /// </summary>
    public void UpdateProximity(Vector3 playerPosition)
    {
        UpdateProximity(playerPosition, _data.Worldgen.Chunks.Size);
    }

    // --- ProximitySpawner implementation ---

    protected override List<CollectibleData> GenerateChunkData(ChunkCoord coord, ChunkData data)
    {
        var collectibleData = new List<CollectibleData>();
        var config = Config;

        // Get valid shape types based on config face range
        var validShapeTypes = PolyhedronTypeExtensions.GetTypesInRange(config.MinFaces, config.MaxFaces);
        if (validShapeTypes.Length == 0)
        {
            // Fallback to dodecahedron if range is invalid
            validShapeTypes = [PolyhedronType.Dodecahedron];
        }

        // Add building rooftop collectibles
        var beaconConfig = _data.Beacons?.Beacon;
        for (int i = 0; i < data.Buildings.Count; i++)
        {
            var id = CollectibleId.ForBuilding(coord, i);

            // Skip if already collected
            if (_tracker.IsCollected(id))
                continue;

            var building = data.Buildings[i];

            // Skip if this building has a beacon (beacons and collectibles are mutually exclusive)
            if (beaconConfig != null && BeaconGenerator.WouldHaveBeacon(building, beaconConfig))
                continue;

            var rooftop = BuildingRenderer.GetRooftopCenterPosition(building, _data.Worldgen.Facades.RooftopFeatureChance);
            var position = new Vector3(rooftop.X, rooftop.Y + config.FloatHeight, rooftop.Z);
            var points = config.BasePoints + (int)(building.Height * config.HeightBonusMultiplier);

            // Deterministically select shape type based on chunk seed and building index
            var shapeIndex = StableHash.ToInt(data.ChunkSeed, i, validShapeTypes.Length);
            var shapeType = validShapeTypes[shapeIndex];

            collectibleData.Add(new CollectibleData(id, position, points, shapeType, building.Height));
        }

        // Add empty plot collectibles
        if (data.EmptyPlotIndices.Count > 0 && config.EmptyPlotSpawnChance > 0f)
        {
            // Use deterministic RNG based on chunk seed for consistent spawning
            var rng = new DeterministicRng(StableHash.Combine(data.ChunkSeed, 0xC011EC7));

            foreach (var plotIndex in data.EmptyPlotIndices)
            {
                // Check spawn chance
                if (rng.NextDouble() > config.EmptyPlotSpawnChance)
                    continue;

                var id = CollectibleId.ForEmptyPlot(coord, plotIndex);

                // Skip if already collected
                if (_tracker.IsCollected(id))
                    continue;

                var plot = data.Plots[plotIndex];
                var position = CalculateEmptyPlotCollectiblePosition(plot, config.EmptyPlotFloatHeight);

                // Deterministically select shape type based on chunk seed and plot index
                var shapeIndex = StableHash.ToInt(data.ChunkSeed, plotIndex + 0x10000, validShapeTypes.Length);
                var shapeType = validShapeTypes[shapeIndex];

                collectibleData.Add(new CollectibleData(id, position, config.EmptyPlotPoints, shapeType));
            }
        }

        return collectibleData;
    }

    protected override CollectibleShape CreateNode(CollectibleData data)
    {
        var collectible = new CollectibleShape(
            data.Id,
            data.ShapeType,
            data.Points,
            Config,
            OnCollectibleCollected,
            _sfxCallback
        );

        collectible.Position = data.Position;
        return collectible;
    }

    protected override CollectibleId GetId(CollectibleData data) => data.Id;

    protected override Vector3 GetPosition(CollectibleData data) => data.Position;

    protected override ChunkCoord GetChunkCoord(CollectibleId id) => id.ChunkCoord;

    protected override float GetMaxSpawnDistance() => Config.VisibilityDistance + 10f;

    protected override bool ShouldSkipSpawn(CollectibleData data) => _tracker.IsCollected(data.Id);

    protected override bool ShouldDespawn(CollectibleId id, CollectibleShape node, Vector3 playerPosition)
    {
        // Skip if already freed (e.g., collected this frame)
        if (!GodotObject.IsInstanceValid(node))
            return true;

        var despawnDistance = Config.VisibilityDistance + 30f; // Hysteresis
        var despawnDistSq = despawnDistance * despawnDistance;
        return node.GlobalPosition.DistanceSquaredTo(playerPosition) > despawnDistSq;
    }

    protected override void DespawnDistantNodes(Vector3 playerPosition)
    {
        // Throttle visibility updates to every Nth tick. Despawn/validity checks still run
        // every tick because we want responsive cleanup.
        bool doProximityUpdate = (++_proximityUpdateCounter % ProximityUpdateInterval) == 0;

        _toRemove.Clear();
        foreach (var (id, collectible) in _activeNodes)
        {
            // Skip if already freed (e.g., collected this frame)
            if (!GodotObject.IsInstanceValid(collectible))
            {
                _toRemove.Add(id);
                continue;
            }

            if (ShouldDespawn(id, collectible, playerPosition))
            {
                collectible.QueueFree();
                _toRemove.Add(id);
            }
            else if (doProximityUpdate)
            {
                // Update proximity for visible collectibles (for visual effects)
                collectible.UpdateProximity(playerPosition);
            }
        }

        foreach (var collId in _toRemove)
        {
            _activeNodes.Remove(collId);
        }
    }

    public override void Reset()
    {
        base.Reset();
        _tracker.Reset();
    }

    /// <summary>
    /// Updates materials and properties on all active collectibles. Called on hot-reload.
    /// </summary>
    public void UpdateMaterials()
    {
        var config = Config;
        foreach (var (_, collectible) in _activeNodes)
        {
            collectible.UpdateConfig(config);
        }
    }

    private static Vector3 CalculateEmptyPlotCollectiblePosition(PlotData plot, float floatHeight)
    {
        // Position at center of plot at ground level + float height
        return new Vector3(plot.CenterX, floatHeight, plot.CenterZ);
    }

    private void OnCollectibleCollected(CollectibleId id, int points)
    {
        // Read position from the live node so the collected-event reports where the shape
        // actually was when collected (bob animation may have shifted it slightly from spawn pos).
        // The shape is still in _activeNodes at this point; QueueFree is deferred via a tween.
        Vector3 pos = default;
        if (_activeNodes.TryGetValue(id, out var node) && GodotObject.IsInstanceValid(node))
        {
            pos = node.GlobalPosition;
        }

        // Scale points based on difficulty before tracking/awarding
        var scaledPoints = _difficultyService?.GetCollectiblePoints(points) ?? points;

        // Mark as collected (publishes event with scaled points and position)
        if (_tracker.MarkCollected(id, scaledPoints, pos.X, pos.Y, pos.Z))
        {
            // Award score
            _scoreService.AddScore(scaledPoints, ScoreCategory.Collectible);
            _scoreService.IncrementCombo();

            Log.Debug(LogCategory.Sim, $"Collectible collected: {id}, points={scaledPoints}");
        }

        // Remove from active tracking (node will QueueFree itself)
        _activeNodes.Remove(id);

        // Also remove from data so it won't respawn
        if (_dataPerChunk.TryGetValue(id.ChunkCoord, out var dataList))
        {
            dataList.RemoveAll(d => d.Id == id);
        }
    }
}
