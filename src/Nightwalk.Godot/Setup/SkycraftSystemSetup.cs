using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.Skycraft;
using Nightwalk.Godot.Skycraft;

namespace Nightwalk.Godot.Setup;

/// <summary>
/// Owns the Skycraft system's lifecycle: POI registry, Core controller, Godot renderer,
/// and the chunk-load/unload subscriptions that populate the POI registry.
/// </summary>
public sealed class SkycraftSystemResult
{
    public SkycraftController Controller { get; }
    public SkycraftPoiRegistry PoiRegistry { get; }
    public SkycraftRenderer Renderer { get; }
    private readonly ChunkManager _chunkManager;

    public SkycraftSystemResult(
        SkycraftController controller,
        SkycraftPoiRegistry registry,
        SkycraftRenderer renderer,
        ChunkManager chunkManager)
    {
        Controller = controller;
        PoiRegistry = registry;
        Renderer = renderer;
        _chunkManager = chunkManager;

        _chunkManager.OnChunkLoaded += OnChunkLoaded;
        _chunkManager.OnChunkUnloaded += OnChunkUnloaded;
    }

    private void OnChunkLoaded(ChunkLoadedEvent e) => PoiRegistry.OnChunkLoaded(e.Coord, e.Data);

    private void OnChunkUnloaded(ChunkUnloadedEvent e) => PoiRegistry.OnChunkUnloaded(e.Coord);

    /// <summary>
    /// Per-physics-tick update. Caller should gate by <c>AllowsGameTick</c>.
    /// </summary>
    public void Update(float delta, Vector3 playerPosition)
        => Controller.Update(delta, playerPosition.X, playerPosition.Y, playerPosition.Z);

    /// <summary>
    /// Per-frame camera-position update for tier-swap decisions.
    /// </summary>
    public void UpdateCamera(Vector3 cameraPosition) => Renderer.SetCameraPosition(cameraPosition);

    public void Reconfigure(SkycraftConfig config)
    {
        Controller.Reconfigure(config);
        Renderer.Reconfigure(config);
    }

    public void Reset()
    {
        Controller.Reset();
        // Defensive: ChunkManager.ResetForSession fires OnChunkUnloaded per chunk which
        // already drains the registry. Clear() is idempotent; it covers any future caller
        // that resets the Skycraft system without cycling chunks.
        PoiRegistry.Clear();
    }

    public void Cleanup()
    {
        _chunkManager.OnChunkLoaded -= OnChunkLoaded;
        _chunkManager.OnChunkUnloaded -= OnChunkUnloaded;
        Renderer.Cleanup();
        Renderer.QueueFree();
    }
}

/// <summary>
/// Constructs the Skycraft system (ADR-0016). Pool-managed Core simulation +
/// hybrid Godot renderer; subscribed to <see cref="ChunkManager"/> for POI sync.
/// </summary>
public static class SkycraftSystemSetup
{
    public static SkycraftSystemResult Configure(
        Node3D parent,
        IEventBus eventBus,
        ChunkManager chunkManager,
        SkycraftConfig config,
        float chunkSize)
    {
        var registry = new SkycraftPoiRegistry(config, chunkSize);
        var controller = new SkycraftController(eventBus, registry, chunkManager, config);

        var renderer = new SkycraftRenderer { Name = "SkycraftRenderer" };
        parent.AddChild(renderer);
        renderer.Initialize(controller, config, eventBus);

        // Construct the result first so its constructor subscribes to chunk events. Then
        // backfill chunks that were already loaded when this system came online. The
        // registry's idempotency guard (ContainsKey check in OnChunkLoaded) absorbs any
        // chunk that happens to fire between the subscribe and the backfill loop —
        // subscribe-then-backfill is the conventional ordering even though today's
        // single-threaded chunk events make either order race-free.
        var result = new SkycraftSystemResult(controller, registry, renderer, chunkManager);

        foreach (var kv in chunkManager.LoadedChunks)
        {
            registry.OnChunkLoaded(kv.Key, kv.Value);
        }

        Log.Info(LogCategory.Sim, $"Skycraft system initialized (pool cap {controller.EffectivePoolCap})");

        return result;
    }
}
