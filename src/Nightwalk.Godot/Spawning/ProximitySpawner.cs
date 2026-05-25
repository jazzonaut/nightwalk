using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Chunks;

namespace Nightwalk.Godot.Spawning;

/// <summary>
/// Base class for proximity-based spawners that cache lightweight data per chunk
/// and spawn/despawn nodes based on player distance.
/// </summary>
/// <typeparam name="TId">Identifier type for spawned entities.</typeparam>
/// <typeparam name="TData">Lightweight data stored per chunk.</typeparam>
/// <typeparam name="TNode">Node type that gets spawned.</typeparam>
public abstract partial class ProximitySpawner<TId, TData, TNode> : Node3D
    where TId : struct
    where TNode : Node3D
{
    // Lightweight data per chunk (cached, not spawned)
    protected readonly Dictionary<ChunkCoord, List<TData>> _dataPerChunk = new();

    // Active spawned nodes
    protected readonly Dictionary<TId, TNode> _activeNodes = new();

    // Reusable lists for batch operations (avoid allocations)
    protected readonly List<TId> _toRemove = new();
    protected readonly List<TData> _toSpawn = new();

    /// <summary>
    /// Called when a chunk is loaded. Generates and caches data (does not spawn nodes).
    /// </summary>
    public void OnChunkLoaded(ChunkCoord coord, ChunkData data)
    {
        if (_dataPerChunk.ContainsKey(coord))
            return;

        _dataPerChunk[coord] = GenerateChunkData(coord, data);
    }

    /// <summary>
    /// Called when a chunk is unloaded. Removes cached data and despawns active nodes.
    /// </summary>
    public void OnChunkUnloaded(ChunkCoord coord)
    {
        _dataPerChunk.Remove(coord);

        // Remove active nodes in this chunk
        _toRemove.Clear();
        foreach (var (id, node) in _activeNodes)
        {
            if (GetChunkCoord(id).Equals(coord))
            {
                OnNodeDespawning(node);
                node.QueueFree();
                _toRemove.Add(id);
            }
        }

        foreach (var id in _toRemove)
        {
            _activeNodes.Remove(id);
        }
    }

    /// <summary>
    /// Updates spawning based on player proximity.
    /// </summary>
    public void UpdateProximity(Vector3 playerPosition, float chunkSize)
    {
        // Despawn distant nodes
        DespawnDistantNodes(playerPosition);

        // Spawn nearby nodes
        SpawnNearbyNodes(playerPosition, chunkSize);

        // Post-update hook for subclass-specific logic
        OnProximityUpdated(playerPosition);
    }

    /// <summary>
    /// Despawns nodes that are too far from the player.
    /// </summary>
    protected virtual void DespawnDistantNodes(Vector3 playerPosition)
    {
        _toRemove.Clear();
        foreach (var (id, node) in _activeNodes)
        {
            if (!GodotObject.IsInstanceValid(node))
            {
                _toRemove.Add(id);
                continue;
            }

            if (ShouldDespawn(id, node, playerPosition))
            {
                OnNodeDespawning(node);
                node.QueueFree();
                _toRemove.Add(id);
            }
        }

        foreach (var id in _toRemove)
        {
            _activeNodes.Remove(id);
        }
    }

    /// <summary>
    /// Spawns nodes that are close enough to the player.
    /// </summary>
    protected virtual void SpawnNearbyNodes(Vector3 playerPosition, float chunkSize)
    {
        var spawnDistance = GetMaxSpawnDistance();
        int chunkRadius = (int)Mathf.Ceil(spawnDistance / chunkSize) + 1;
        var playerChunk = ChunkCoord.FromWorldPosition(playerPosition.X, playerPosition.Z, chunkSize);

        _toSpawn.Clear();
        for (int dx = -chunkRadius; dx <= chunkRadius; dx++)
        {
            for (int dz = -chunkRadius; dz <= chunkRadius; dz++)
            {
                var coord = new ChunkCoord(playerChunk.X + dx, playerChunk.Z + dz);

                if (!_dataPerChunk.TryGetValue(coord, out var dataList))
                    continue;

                foreach (var data in dataList)
                {
                    var id = GetId(data);

                    // Skip if already spawned
                    if (_activeNodes.ContainsKey(id))
                        continue;

                    // Skip if filtered out (e.g., already collected/completed)
                    if (ShouldSkipSpawn(data))
                        continue;

                    if (ShouldSpawn(data, playerPosition))
                    {
                        _toSpawn.Add(data);
                    }
                }
            }
        }

        // Cap spawns per frame to avoid AddChild bursts on chunk-crossings / game start.
        // Any entries we skip will be re-picked-up next tick by the eligibility loop above
        // (since _activeNodes.ContainsKey will still return false).
        int budget = MaxSpawnsPerFrame;
        foreach (var data in _toSpawn)
        {
            if (budget-- <= 0) break;
            var node = CreateNode(data);
            AddChild(node);
            _activeNodes[GetId(data)] = node;
        }
    }

    /// <summary>
    /// Resets all spawned nodes and cached data.
    /// </summary>
    public virtual void Reset()
    {
        foreach (var (_, node) in _activeNodes)
        {
            node.QueueFree();
        }
        _activeNodes.Clear();
        _dataPerChunk.Clear();
    }

    // --- Abstract methods that subclasses must implement ---

    /// <summary>
    /// Generates lightweight data for a chunk. Called once when chunk loads.
    /// </summary>
    protected abstract List<TData> GenerateChunkData(ChunkCoord coord, ChunkData data);

    /// <summary>
    /// Creates and configures a node from data.
    /// </summary>
    protected abstract TNode CreateNode(TData data);

    /// <summary>
    /// Gets the identifier from data.
    /// </summary>
    protected abstract TId GetId(TData data);

    /// <summary>
    /// Gets the world position from data.
    /// </summary>
    protected abstract Vector3 GetPosition(TData data);

    /// <summary>
    /// Gets the chunk coordinate from an identifier.
    /// </summary>
    protected abstract ChunkCoord GetChunkCoord(TId id);

    /// <summary>
    /// Gets the maximum spawn distance (used for chunk radius calculation).
    /// </summary>
    protected abstract float GetMaxSpawnDistance();

    // --- Virtual methods with default implementations ---

    /// <summary>
    /// Maximum new nodes spawned per UpdateProximity call. Caps the AddChild burst
    /// that would otherwise hit when many entities cross into spawn range on the
    /// same frame (game start, respawn, fast traversal). Subclasses can override.
    /// </summary>
    protected virtual int MaxSpawnsPerFrame => 8;


    /// <summary>
    /// Determines if data should be skipped during spawn checks (e.g., already collected).
    /// </summary>
    protected virtual bool ShouldSkipSpawn(TData data) => false;

    /// <summary>
    /// Determines if data should spawn based on distance. Override for custom distance logic.
    /// </summary>
    protected virtual bool ShouldSpawn(TData data, Vector3 playerPosition)
    {
        var spawnDistSq = GetMaxSpawnDistance() * GetMaxSpawnDistance();
        var distSq = GetPosition(data).DistanceSquaredTo(playerPosition);
        return distSq <= spawnDistSq;
    }

    /// <summary>
    /// Determines if a node should despawn. Override for custom despawn logic.
    /// </summary>
    protected virtual bool ShouldDespawn(TId id, TNode node, Vector3 playerPosition)
    {
        var despawnDistance = GetMaxSpawnDistance() + 20f; // Default hysteresis
        var despawnDistSq = despawnDistance * despawnDistance;
        return node.GlobalPosition.DistanceSquaredTo(playerPosition) > despawnDistSq;
    }

    /// <summary>
    /// Called when a node is about to be despawned. Override for cleanup logic.
    /// </summary>
    protected virtual void OnNodeDespawning(TNode node) { }

    /// <summary>
    /// Called after proximity update completes. Override for post-update logic.
    /// </summary>
    protected virtual void OnProximityUpdated(Vector3 playerPosition) { }
}
