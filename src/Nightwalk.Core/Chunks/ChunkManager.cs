using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.World.Buildings;
using Nightwalk.Core.World.Neon;
using Nightwalk.Core.World.Roads;

namespace Nightwalk.Core.Chunks;

/// <summary>
/// Event raised when a chunk finishes loading.
/// </summary>
public sealed class ChunkLoadedEvent
{
    public ChunkCoord Coord { get; }
    public ChunkData Data { get; }

    public ChunkLoadedEvent(ChunkCoord coord, ChunkData data)
    {
        Coord = coord;
        Data = data;
    }
}

/// <summary>
/// Event raised when a chunk is unloaded.
/// </summary>
public sealed class ChunkUnloadedEvent
{
    public ChunkCoord Coord { get; }

    public ChunkUnloadedEvent(ChunkCoord coord)
    {
        Coord = coord;
    }
}

/// <summary>
/// Manages chunk loading and unloading based on a focus position (player).
/// </summary>
public sealed class ChunkManager : IDisposable
{
    private readonly Dictionary<ChunkCoord, ChunkData> _loadedChunks = new();
    private readonly Queue<ChunkCoord> _loadQueue = new();
    private readonly HashSet<ChunkCoord> _pendingLoads = new();
    // Thread-safe: accessed from both main thread and background Task threads
    private readonly ConcurrentDictionary<ChunkCoord, byte> _inFlightLoads = new();
    private readonly ConcurrentQueue<ChunkLoadResult> _completedLoads = new();
    private readonly ConcurrentDictionary<ChunkCoord, CancellationTokenSource> _loadCancellations = new();
    private bool _disposed;

    // LRU cache for recently unloaded chunks (avoids regeneration when player oscillates at boundaries)
    private readonly LinkedList<ChunkCoord> _cacheOrder = new();
    private readonly Dictionary<ChunkCoord, LinkedListNode<ChunkCoord>> _cacheNodes = new();
    private readonly Dictionary<ChunkCoord, ChunkData> _chunkCache = new();
    private const int ChunkCacheCapacity = 32;

    // Limit completed loads per frame to prevent spikes when many chunks finish simultaneously
    private const int MaxCompletedLoadsPerFrame = 2;

    // Limit unloads per frame - unloading is low priority, can happen gradually
    private const int MaxUnloadsPerFrame = 1;

    // Queue of chunks pending unload (processed gradually)
    private readonly Queue<ChunkCoord> _unloadQueue = new();
    private readonly HashSet<ChunkCoord> _pendingUnloads = new(); // O(1) contains check

    private readonly float _chunkSize;
    private readonly int _loadRadius;
    private readonly int _unloadRadius;
    private readonly int _maxLoadsPerTick;
    private readonly int _worldSeed;
    private readonly SemaphoreSlim _loadSemaphore;
    private readonly RoadGenerator? _roadGenerator;
    private readonly BuildingGenerator? _buildingGenerator;
    private readonly NeonPlacementGenerator? _neonGenerator;

    private ChunkCoord _lastFocusChunk;
    private bool _initialized;

    /// <summary>
    /// Called when a chunk is loaded. Renderer should subscribe to this.
    /// </summary>
    public event Action<ChunkLoadedEvent>? OnChunkLoaded;

    /// <summary>
    /// Called when a chunk is unloaded. Renderer should subscribe to this.
    /// </summary>
    public event Action<ChunkUnloadedEvent>? OnChunkUnloaded;

    public ChunkManager(
        float chunkSize,
        int loadRadius,
        int unloadRadius,
        int maxLoadsPerTick,
        int maxConcurrentLoads,
        int worldSeed,
        RoadGenerator? roadGenerator = null,
        BuildingGenerator? buildingGenerator = null,
        NeonPlacementGenerator? neonGenerator = null)
    {
        _chunkSize = chunkSize;
        _loadRadius = loadRadius;
        _unloadRadius = unloadRadius;
        _maxLoadsPerTick = maxLoadsPerTick;
        _worldSeed = worldSeed;
        _loadSemaphore = new SemaphoreSlim(Math.Max(1, maxConcurrentLoads));
        _roadGenerator = roadGenerator;
        _buildingGenerator = buildingGenerator;
        _neonGenerator = neonGenerator;
    }

    public IReadOnlyDictionary<ChunkCoord, ChunkData> LoadedChunks => _loadedChunks;
    public float ChunkSize => _chunkSize;

    /// <summary>
    /// Updates chunk loading/unloading based on focus position.
    /// Call this every tick with the player's world position.
    /// </summary>
    public void Update(float focusX, float focusZ)
    {
        var focusChunk = ChunkCoord.FromWorldPosition(focusX, focusZ, _chunkSize);

        // Only do full recalculation if focus chunk changed or first update
        if (!_initialized || focusChunk != _lastFocusChunk)
        {
            _lastFocusChunk = focusChunk;
            _initialized = true;

            // Queue chunks that should be loaded
            RebuildLoadQueue(focusChunk);

            // Queue distant chunks for gradual unloading
            QueueDistantChunksForUnload(focusChunk);
        }

        // Process finished chunk loads (main thread) - high priority
        ProcessCompletedLoads(focusChunk);

        // Process load queue (limited per tick) - high priority
        ProcessLoadQueue();

        // Process unload queue (limited per tick) - low priority, can happen gradually
        ProcessUnloadQueue();
    }

    /// <summary>
    /// Unloads all chunks and clears pending/in-flight work for session resets.
    /// </summary>
    public void ResetForSession()
    {
        // Cancel in-flight loads
        foreach (var cts in _loadCancellations.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _loadCancellations.Clear();
        _inFlightLoads.Clear();

        // Clear pending and completed work
        _pendingLoads.Clear();
        _loadQueue.Clear();
        _unloadQueue.Clear();
        _pendingUnloads.Clear();
        while (_completedLoads.TryDequeue(out _)) { }

        // Unload all loaded chunks (fires events)
        if (_loadedChunks.Count > 0)
        {
            var loadedCoords = new List<ChunkCoord>(_loadedChunks.Keys);
            foreach (var coord in loadedCoords)
            {
                UnloadChunk(coord);
            }
        }

        // Clear cache to force regeneration on next load
        _cacheOrder.Clear();
        _cacheNodes.Clear();
        _chunkCache.Clear();

        _initialized = false;
    }

    private void RebuildLoadQueue(ChunkCoord focus)
    {
        // Cancel in-flight loads that are now outside the load radius
        CancelDistantInFlightLoads(focus);

        // Remove pending loads that are now outside the load radius
        PruneDistantPendingLoads(focus);

        // Only add new chunks that entered the load radius
        QueueNewChunksInRadius(focus);
    }

    private void CancelDistantInFlightLoads(ChunkCoord focus)
    {
        foreach (var coord in _inFlightLoads.Keys)
        {
            if (coord.ChebyshevDistanceTo(focus) > _loadRadius)
            {
                if (_loadCancellations.TryGetValue(coord, out var cts))
                {
                    cts.Cancel();
                }
            }
        }
    }

    private void PruneDistantPendingLoads(ChunkCoord focus)
    {
        // Remove chunks from pending that are now outside the load radius
        // We need to rebuild the queue without distant chunks
        if (_pendingLoads.Count == 0)
            return;

        var stillNeeded = new List<ChunkCoord>();
        while (_loadQueue.Count > 0)
        {
            var coord = _loadQueue.Dequeue();
            if (coord.ChebyshevDistanceTo(focus) <= _loadRadius)
            {
                stillNeeded.Add(coord);
            }
            else
            {
                _pendingLoads.Remove(coord);
            }
        }

        // Re-enqueue in priority order (nearest first)
        stillNeeded.Sort((a, b) => a.ChebyshevDistanceTo(focus).CompareTo(b.ChebyshevDistanceTo(focus)));
        foreach (var coord in stillNeeded)
        {
            _loadQueue.Enqueue(coord);
        }
    }

    private void QueueNewChunksInRadius(ChunkCoord focus)
    {
        // Only iterate the perimeter rings that could contain new chunks
        // Spiral outward from focus to load nearest chunks first
        for (int radius = 0; radius <= _loadRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    // Only process the perimeter at this radius (or center at radius 0)
                    if (radius > 0 && Math.Abs(dx) != radius && Math.Abs(dz) != radius)
                        continue;

                    var coord = new ChunkCoord(focus.X + dx, focus.Z + dz);

                    if (!_loadedChunks.ContainsKey(coord) && !_pendingLoads.Contains(coord) && !_inFlightLoads.ContainsKey(coord))
                    {
                        _loadQueue.Enqueue(coord);
                        _pendingLoads.Add(coord);
                    }
                }
            }
        }
    }

    private void QueueDistantChunksForUnload(ChunkCoord focus)
    {
        foreach (var (coord, _) in _loadedChunks)
        {
            if (coord.ChebyshevDistanceTo(focus) > _unloadRadius)
            {
                // Only queue if not already queued (O(1) check)
                if (_pendingUnloads.Add(coord))
                {
                    _unloadQueue.Enqueue(coord);
                }
            }
        }
    }

    private void ProcessLoadQueue()
    {
        int loaded = 0;
        while (_loadQueue.Count > 0 && loaded < _maxLoadsPerTick)
        {
            var coord = _loadQueue.Dequeue();
            _pendingLoads.Remove(coord);

            // Skip if already loaded (might have been loaded in a previous pass)
            if (_loadedChunks.ContainsKey(coord) || _inFlightLoads.ContainsKey(coord))
                continue;

            // Try to restore from cache first (instant, no async work needed)
            var cachedData = TryGetFromCache(coord);
            if (cachedData != null)
            {
                Log.Debug(LogCategory.Chunk, $"Restored chunk from cache: {coord}");
                _loadedChunks[coord] = cachedData;
                OnChunkLoaded?.Invoke(new ChunkLoadedEvent(coord, cachedData));
                loaded++;
                continue;
            }

            LoadChunkAsync(coord);
            loaded++;
        }
    }

    private void ProcessUnloadQueue()
    {
        int unloaded = 0;
        while (_unloadQueue.Count > 0 && unloaded < MaxUnloadsPerFrame)
        {
            var coord = _unloadQueue.Dequeue();
            _pendingUnloads.Remove(coord);

            // Skip if no longer loaded (might have been unloaded by ResetForSession)
            if (!_loadedChunks.ContainsKey(coord))
                continue;

            // Skip if player moved back toward this chunk - it's no longer distant
            if (coord.ChebyshevDistanceTo(_lastFocusChunk) <= _unloadRadius)
                continue;

            UnloadChunk(coord);
            unloaded++;
        }
    }

    private void LoadChunkAsync(ChunkCoord coord)
    {
        // Atomically check and add - if already in-flight, skip to prevent duplicate tasks
        if (!_inFlightLoads.TryAdd(coord, 0))
        {
            Log.Debug(LogCategory.Chunk, $"Chunk {coord} already in-flight, skipping duplicate load");
            return;
        }

        Log.Debug(LogCategory.Chunk, $"Queueing chunk load {coord}");

        var cts = new CancellationTokenSource();
        _loadCancellations[coord] = cts;
        var token = cts.Token;

        Task.Run(async () =>
        {
            bool acquiredSemaphore = false;
            try
            {
                await _loadSemaphore.WaitAsync(token);
                acquiredSemaphore = true;

                token.ThrowIfCancellationRequested();
                var data = GenerateChunkData(coord, token);
                _completedLoads.Enqueue(new ChunkLoadResult(coord, data, null, wasCancelled: false));
            }
            catch (OperationCanceledException)
            {
                // Cancelled while waiting for semaphore or during generation
                _completedLoads.Enqueue(new ChunkLoadResult(coord, null, null, wasCancelled: true));
            }
            catch (ObjectDisposedException)
            {
                // Semaphore disposed during shutdown - silently exit
            }
            catch (Exception ex)
            {
                _completedLoads.Enqueue(new ChunkLoadResult(coord, null, ex, wasCancelled: false));
            }
            finally
            {
                if (acquiredSemaphore)
                {
                    try { _loadSemaphore.Release(); }
                    catch (ObjectDisposedException) { /* Shutdown in progress */ }
                }
                // Remove from in-flight set so Dispose can track completion
                _inFlightLoads.TryRemove(coord, out _);
                _loadCancellations.TryRemove(coord, out var removed);
                removed?.Dispose();
            }
        });
    }

    private ChunkData GenerateChunkData(ChunkCoord coord, CancellationToken token)
    {
        Log.Debug(LogCategory.Chunk, $"Loading chunk {coord}");

        var data = new ChunkData(coord, _chunkSize, _worldSeed);
        data.State = ChunkState.Generating;

        var (minX, minZ) = coord.ToWorldPosition(_chunkSize);

        // Generate roads for this chunk
        if (_roadGenerator != null)
        {
            token.ThrowIfCancellationRequested();
            var roads = _roadGenerator.GenerateRoads(minX, minZ, _chunkSize);
            data.Roads.AddRange(roads);
        }

        // Generate buildings and plots for this chunk (needs roads to define city blocks)
        if (_buildingGenerator != null)
        {
            token.ThrowIfCancellationRequested();
            var (buildings, plots, emptyPlotIndices) = _buildingGenerator.GenerateBuildings(minX, minZ, _chunkSize, data.ChunkSeed, data.Roads);
            data.Buildings.AddRange(buildings);
            data.Plots.AddRange(plots);
            data.EmptyPlotIndices.AddRange(emptyPlotIndices);
        }

        // Generate neon lights on buildings
        if (_neonGenerator != null)
        {
            token.ThrowIfCancellationRequested();
            foreach (var building in data.Buildings)
            {
                var neons = _neonGenerator.GenerateNeons(building, data.ChunkSeed);
                data.Neons.AddRange(neons);
            }
        }

        data.State = ChunkState.Loaded;
        return data;
    }

    private void ProcessCompletedLoads(ChunkCoord focus)
    {
        // Process only a limited number per frame to prevent spikes
        // Count every dequeue toward the limit, not just successful loads
        int processed = 0;
        while (processed < MaxCompletedLoadsPerFrame && _completedLoads.TryDequeue(out var result))
        {
            processed++;
            _inFlightLoads.TryRemove(result.Coord, out _);

            // Skip cancelled loads silently
            if (result.WasCancelled)
            {
                Log.Debug(LogCategory.Chunk, $"Chunk load cancelled: {result.Coord}");
                continue;
            }

            if (result.Error != null)
            {
                Log.Error(LogCategory.Chunk, $"Failed to load chunk {result.Coord}", result.Error);
                continue;
            }

            if (result.Data == null)
                continue;

            // Skip if no longer needed
            if (result.Coord.ChebyshevDistanceTo(focus) > _loadRadius)
                continue;

            if (_loadedChunks.ContainsKey(result.Coord))
                continue;

            _loadedChunks[result.Coord] = result.Data;
            OnChunkLoaded?.Invoke(new ChunkLoadedEvent(result.Coord, result.Data));
        }
    }

    private void UnloadChunk(ChunkCoord coord)
    {
        if (_loadedChunks.TryGetValue(coord, out var data))
        {
            Log.Debug(LogCategory.Chunk, $"Unloading chunk {coord}");

            data.State = ChunkState.Unloading;
            _loadedChunks.Remove(coord);

            // Add to LRU cache instead of discarding
            AddToCache(coord, data);

            OnChunkUnloaded?.Invoke(new ChunkUnloadedEvent(coord));
        }
    }

    private void AddToCache(ChunkCoord coord, ChunkData data)
    {
        // If already in cache, move to front
        if (_cacheNodes.TryGetValue(coord, out var existingNode))
        {
            _cacheOrder.Remove(existingNode);
            _cacheOrder.AddFirst(existingNode);
            return;
        }

        // Evict oldest if at capacity
        while (_cacheOrder.Count >= ChunkCacheCapacity)
        {
            var oldest = _cacheOrder.Last;
            if (oldest != null)
            {
                var oldCoord = oldest.Value;
                _cacheOrder.RemoveLast();
                _cacheNodes.Remove(oldCoord);

                // Actually clear the evicted chunk's data
                if (_chunkCache.TryGetValue(oldCoord, out var oldData))
                {
                    oldData.Roads.Clear();
                    oldData.Buildings.Clear();
                    oldData.Plots.Clear();
                    oldData.EmptyPlotIndices.Clear();
                    oldData.Neons.Clear();
                    _chunkCache.Remove(oldCoord);
                }
            }
        }

        // Add new entry at front
        var node = _cacheOrder.AddFirst(coord);
        _cacheNodes[coord] = node;
        _chunkCache[coord] = data;
    }

    private ChunkData? TryGetFromCache(ChunkCoord coord)
    {
        if (!_chunkCache.TryGetValue(coord, out var data))
            return null;

        // Remove from cache (it will be moved to loaded chunks)
        if (_cacheNodes.TryGetValue(coord, out var node))
        {
            _cacheOrder.Remove(node);
            _cacheNodes.Remove(coord);
        }
        _chunkCache.Remove(coord);

        // Reset state for reuse
        data.State = ChunkState.Loaded;
        return data;
    }

    /// <summary>
    /// Gets chunk data at the given coordinate, or null if not loaded.
    /// </summary>
    public ChunkData? GetChunk(ChunkCoord coord)
    {
        return _loadedChunks.TryGetValue(coord, out var data) ? data : null;
    }

    /// <summary>
    /// Gets chunk data at the given world position, or null if not loaded.
    /// </summary>
    public ChunkData? GetChunkAtPosition(float worldX, float worldZ)
    {
        var coord = ChunkCoord.FromWorldPosition(worldX, worldZ, _chunkSize);
        return GetChunk(coord);
    }

    private readonly struct ChunkLoadResult
    {
        public ChunkCoord Coord { get; }
        public ChunkData? Data { get; }
        public Exception? Error { get; }
        public bool WasCancelled { get; }

        public ChunkLoadResult(ChunkCoord coord, ChunkData? data, Exception? error, bool wasCancelled = false)
        {
            Coord = coord;
            Data = data;
            Error = error;
            WasCancelled = wasCancelled;
        }
    }

    /// <summary>
    /// Disposes resources including the load semaphore.
    /// Waits briefly for in-flight tasks to complete after cancellation.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Cancel all in-flight loads
        foreach (var cts in _loadCancellations.Values)
        {
            cts.Cancel();
        }

        // Wait for in-flight tasks to acknowledge cancellation (with timeout)
        // Tasks catch ObjectDisposedException, so this is a grace period
        const int maxWaitMs = 500;
        const int pollIntervalMs = 10;
        int waited = 0;
        while (_inFlightLoads.Count > 0 && waited < maxWaitMs)
        {
            Thread.Sleep(pollIntervalMs);
            waited += pollIntervalMs;
        }

        if (_inFlightLoads.Count > 0)
        {
            Log.Warn(LogCategory.Chunk, $"ChunkManager.Dispose: {_inFlightLoads.Count} loads still in-flight after timeout");
        }

        // Dispose CTS objects
        foreach (var cts in _loadCancellations.Values)
        {
            cts.Dispose();
        }
        _loadCancellations.Clear();

        _loadSemaphore.Dispose();
    }
}
