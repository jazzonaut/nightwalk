using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Beacons;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Godot.Rendering.SubRenderers;
using Nightwalk.Godot.Spawning;
using SysVector3 = System.Numerics.Vector3;

namespace Nightwalk.Godot.Beacons;

/// <summary>
/// Spawns and manages beacon nodes using proximity-based spawning.
/// </summary>
public sealed partial class BeaconSpawner : ProximitySpawner<BeaconId, BeaconData, BeaconNode>
{
    private readonly BeaconTracker _tracker;
    private BeaconsConfig _config;
    private readonly GameDataService _data;
    private BeaconSyncManager? _syncManager;

    /// <summary>
    /// Sets the sync manager reference for cancelling sync on beacon despawn.
    /// </summary>
    public void SetSyncManager(BeaconSyncManager syncManager)
    {
        _syncManager = syncManager;
    }

    /// <summary>
    /// Updates the configuration (for hot-reload).
    /// </summary>
    public void Reconfigure(BeaconsConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Gets all active beacons of a specific frequency.
    /// </summary>
    public IEnumerable<BeaconNode> GetBeaconsByFrequency(BeaconFrequency frequency)
    {
        foreach (var (_, beacon) in _activeNodes)
        {
            if (beacon.Data.Frequency == frequency)
                yield return beacon;
        }
    }

    /// <summary>
    /// Gets a beacon node by its ID, if active.
    /// </summary>
    public BeaconNode? GetBeacon(BeaconId id)
    {
        return _activeNodes.GetValueOrDefault(id);
    }

    public BeaconSpawner(
        BeaconTracker tracker,
        BeaconsConfig config,
        GameDataService data)
    {
        _tracker = tracker;
        _config = config;
        _data = data;
    }

    public override void _Ready()
    {
        Name = "BeaconSpawner";
        Log.Info(LogCategory.Sim, "BeaconSpawner initialized");
    }

    /// <summary>
    /// Updates beacon spawning based on player proximity.
    /// Overload that uses chunk size from config.
    /// </summary>
    public void UpdateProximity(Vector3 playerPosition)
    {
        UpdateProximity(playerPosition, _data.Worldgen.Chunks.Size);
    }

    // --- ProximitySpawner implementation ---

    protected override List<BeaconData> GenerateChunkData(ChunkCoord coord, ChunkData data)
    {
        var beaconData = new List<BeaconData>();
        var beaconConfig = _config.Beacon;
        var minDistSq = beaconConfig.MinSameFrequencyDistance * beaconConfig.MinSameFrequencyDistance;
        var rooftopFeatureChance = _data.Worldgen.Facades.RooftopFeatureChance;

        // Check each building for beacon spawn
        for (int i = 0; i < data.Buildings.Count; i++)
        {
            var building = data.Buildings[i];

            // Skip if already completed
            var potentialId = BeaconId.ForBuilding(coord, i);
            if (_tracker.IsBeaconCompleted(potentialId))
                continue;

            var beacon = BeaconGenerator.TryGenerateBeacon(coord, i, building, beaconConfig);
            if (beacon.HasValue)
            {
                // Use shared rooftop position calculation (accounts for stepped sections and rooftop features)
                var rooftop = BuildingRenderer.GetRooftopCenterPosition(building, rooftopFeatureChance);
                var adjustedPos = new SysVector3(rooftop.X, rooftop.Y, rooftop.Z);

                // Apply spatial offset to spread frequencies across the world
                var adjustedFrequency = GetSpatiallyAdjustedFrequency(
                    beacon.Value.Frequency,
                    adjustedPos.X,
                    adjustedPos.Z,
                    beaconConfig.FrequencyGridSize);

                // Check for same-frequency beacons that are too close and reassign if needed
                adjustedFrequency = EnsureMinimumFrequencyDistance(
                    adjustedFrequency,
                    adjustedPos,
                    minDistSq,
                    beaconData,
                    beacon.Value.Id);

                var beaconValue = new BeaconData(
                    beacon.Value.Id,
                    adjustedPos,
                    adjustedFrequency);

                beaconData.Add(beaconValue);
            }
        }

        return beaconData;
    }

    protected override BeaconNode CreateNode(BeaconData data)
    {
        var beacon = new BeaconNode();
        beacon.Configure(data, _config);

        var pos = new Vector3(data.Position.X, data.Position.Y, data.Position.Z);
        beacon.Position = pos;

        // Check if this beacon should be in receiving state (if sync is active)
        var activeSync = _tracker.ActiveSync;
        if (activeSync != null &&
            activeSync.Frequency == data.Frequency &&
            activeSync.SourceBeaconId != data.Id)
        {
            beacon.SetState(BeaconState.Receiving, _config.Beacon);
            beacon.SetMarkerVisible(true);
        }
        else if (_tracker.IsBeaconCompleted(data.Id))
        {
            beacon.SetState(BeaconState.Completed, _config.Beacon);
        }

        return beacon;
    }

    protected override BeaconId GetId(BeaconData data) => data.Id;

    protected override Vector3 GetPosition(BeaconData data) =>
        new(data.Position.X, data.Position.Y, data.Position.Z);

    protected override ChunkCoord GetChunkCoord(BeaconId id) => id.ChunkCoord;

    protected override float GetMaxSpawnDistance()
    {
        var interaction = _config.Interaction;
        var activeSync = _tracker.ActiveSync;
        return activeSync != null
            ? interaction.ActiveFrequencySpawnDistance
            : interaction.SpawnDistance;
    }

    protected override bool ShouldSkipSpawn(BeaconData data) =>
        _tracker.IsBeaconCompleted(data.Id);

    protected override bool ShouldSpawn(BeaconData data, Vector3 playerPosition)
    {
        var interaction = _config.Interaction;
        var activeSync = _tracker.ActiveSync;
        var activeFrequency = activeSync?.Frequency;

        // Use extended distance for beacons matching active frequency
        var spawnDistSq = (activeFrequency != null && data.Frequency == activeFrequency.Value)
            ? interaction.ActiveFrequencySpawnDistance * interaction.ActiveFrequencySpawnDistance
            : interaction.SpawnDistance * interaction.SpawnDistance;

        var pos = GetPosition(data);
        return pos.DistanceSquaredTo(playerPosition) <= spawnDistSq;
    }

    protected override bool ShouldDespawn(BeaconId id, BeaconNode node, Vector3 playerPosition)
    {
        var interaction = _config.Interaction;
        var activeSync = _tracker.ActiveSync;
        var activeFrequency = activeSync?.Frequency;
        var sourceBeaconId = activeSync?.SourceBeaconId;

        // Never despawn the source beacon during active sync
        if (sourceBeaconId != null && id.Equals(sourceBeaconId.Value))
            return false;

        var distSq = node.GlobalPosition.DistanceSquaredTo(playerPosition);

        // Use extended distance for beacons matching active frequency
        var despawnDistSq = (activeFrequency != null && node.Data.Frequency == activeFrequency.Value)
            ? interaction.ActiveFrequencyDespawnDistance * interaction.ActiveFrequencyDespawnDistance
            : interaction.DespawnDistance * interaction.DespawnDistance;

        return distSq > despawnDistSq;
    }

    protected override void OnNodeDespawning(BeaconNode node)
    {
        CancelSyncIfSourceBeacon(node);
    }

    public override void Reset()
    {
        base.Reset();
        // Tracker reset is handled externally
    }

    /// <summary>
    /// Attempts to activate a beacon within interaction distance of the player.
    /// Selects the nearest eligible beacon if multiple are in range.
    /// </summary>
    public PrismNode? TryActivateNearbyBeacon(Vector3 playerPosition, BeaconSyncManager syncManager)
    {
        var activationDistSq = _config.Interaction.ActivationDistance * _config.Interaction.ActivationDistance;

        BeaconNode? nearestBeacon = null;
        BeaconId nearestId = default;
        float nearestDistSq = float.MaxValue;

        foreach (var (id, beacon) in _activeNodes)
        {
            if (beacon.State != BeaconState.Idle)
                continue;

            var distSq = beacon.GlobalPosition.DistanceSquaredTo(playerPosition);
            if (distSq <= activationDistSq && distSq < nearestDistSq)
            {
                nearestBeacon = beacon;
                nearestId = id;
                nearestDistSq = distSq;
            }
        }

        if (nearestBeacon != null)
        {
            var spawnedPrism = syncManager.StartSync(nearestBeacon, playerPosition);
            if (spawnedPrism != null)
            {
                Log.Info(LogCategory.Sim, $"Activated beacon {nearestId}");
                return spawnedPrism;
            }
        }

        return null;
    }

    /// <summary>
    /// Cancels the active sync if the given beacon is the source.
    /// </summary>
    private void CancelSyncIfSourceBeacon(BeaconNode beacon)
    {
        if (_syncManager != null && _syncManager.SourceBeacon == beacon)
        {
            Log.Info(LogCategory.Sim, "Cancelling sync: source beacon despawned");
            _syncManager.CancelSync();
        }
    }

    /// <summary>
    /// Ensures a beacon's frequency doesn't conflict with nearby same-frequency beacons.
    /// </summary>
    private BeaconFrequency EnsureMinimumFrequencyDistance(
        BeaconFrequency frequency,
        SysVector3 position,
        float minDistSq,
        List<BeaconData> currentChunkBeacons,
        BeaconId selfId)
    {
        if (minDistSq <= 0)
            return frequency;

        // Try each frequency starting from the preferred one
        for (int attempt = 0; attempt < BeaconFrequency.Count; attempt++)
        {
            var candidateFreq = new BeaconFrequency((frequency.Index + attempt) % BeaconFrequency.Count);
            bool hasConflict = false;

            // Check against beacons in the current chunk being built
            foreach (var other in currentChunkBeacons)
            {
                if (other.Frequency == candidateFreq)
                {
                    var dx = position.X - other.Position.X;
                    var dz = position.Z - other.Position.Z;
                    var distSq = dx * dx + dz * dz;
                    if (distSq < minDistSq)
                    {
                        hasConflict = true;
                        break;
                    }
                }
            }

            // Check against beacons in already-loaded chunks
            if (!hasConflict)
            {
                foreach (var (_, chunkBeacons) in _dataPerChunk)
                {
                    foreach (var other in chunkBeacons)
                    {
                        if (other.Id.Equals(selfId))
                            continue;

                        if (other.Frequency == candidateFreq)
                        {
                            var dx = position.X - other.Position.X;
                            var dz = position.Z - other.Position.Z;
                            var distSq = dx * dx + dz * dz;
                            if (distSq < minDistSq)
                            {
                                hasConflict = true;
                                break;
                            }
                        }
                    }
                    if (hasConflict) break;
                }
            }

            if (!hasConflict)
                return candidateFreq;
        }

        // All frequencies have conflicts; return the original (rare edge case)
        return frequency;
    }

    /// <summary>
    /// Adjusts a beacon's frequency based on its spatial position.
    /// </summary>
    private static BeaconFrequency GetSpatiallyAdjustedFrequency(
        BeaconFrequency baseFrequency,
        float x,
        float z,
        float gridSize)
    {
        if (gridSize <= 0)
            return baseFrequency;

        int gridX = (int)MathF.Floor(x / gridSize);
        int gridZ = (int)MathF.Floor(z / gridSize);

        int cellHash = gridX * 73856093 ^ gridZ * 19349663;
        int offset = ((cellHash % BeaconFrequency.Count) + BeaconFrequency.Count) % BeaconFrequency.Count;

        int adjustedIndex = (baseFrequency.Index + offset) % BeaconFrequency.Count;
        return new BeaconFrequency(adjustedIndex);
    }
}
