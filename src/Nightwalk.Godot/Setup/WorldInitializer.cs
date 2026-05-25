using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.World;
using Nightwalk.Core.World.Buildings;
using Nightwalk.Core.World.Clusters;
using Nightwalk.Core.World.Neon;
using Nightwalk.Core.World.Roads;
using Nightwalk.Godot.Rendering;

namespace Nightwalk.Godot.Setup;

/// <summary>
/// Result of world initialization.
/// </summary>
public sealed record WorldInitResult(
    WorldState WorldState,
    ChunkManager ChunkManager,
    ChunkRenderer ChunkRenderer
)
{
    public void Cleanup()
    {
        ChunkRenderer.UnsubscribeFrom(ChunkManager);
        ChunkRenderer.Cleanup();
        ChunkManager.Dispose();
        WorldState.Shutdown();
    }
}

/// <summary>
/// Initializes the game world, chunk system, and generators.
/// </summary>
public static class WorldInitializer
{
    /// <summary>
    /// Creates and initializes the game world.
    /// </summary>
    /// <param name="parent">The parent node for the chunk renderer.</param>
    /// <param name="worldgenConfig">World generation configuration.</param>
    /// <param name="eventBus">Event bus for world events.</param>
    /// <returns>The initialized world components.</returns>
    public static WorldInitResult Initialize(Node3D parent, WorldgenConfig worldgenConfig, IEventBus eventBus)
    {
        Log.Info(LogCategory.World, "Building game world...");

        // Create world configuration
        var worldConfig = new WorldConfig
        {
            Seed = worldgenConfig.Seed,
            ChunkSize = worldgenConfig.Chunks.Size,
            LoadRadius = worldgenConfig.Chunks.LoadRadius,
            UnloadRadius = worldgenConfig.Chunks.UnloadRadius
        };

        // Build world state
        var builder = new WorldBuilder();
        var worldState = builder.Build(worldConfig, eventBus);

        // Initialize chunk system
        var (chunkManager, chunkRenderer) = InitializeChunkSystem(parent, worldConfig, worldgenConfig);

        Log.Info(LogCategory.World, $"Game world initialized. Seed: {worldConfig.Seed}");

        return new WorldInitResult(worldState, chunkManager, chunkRenderer);
    }

    private static (ChunkManager, ChunkRenderer) InitializeChunkSystem(
        Node3D parent,
        WorldConfig config,
        WorldgenConfig worldgenConfig)
    {
        Log.Info(LogCategory.Chunk, "Initializing chunk system...");

        // Create cluster influence provider if enabled
        ClusterInfluenceProvider? clusterProvider = null;
        var clusterConfig = worldgenConfig.Buildings.Clusters;
        if (clusterConfig != null && clusterConfig.Enabled)
        {
            var baseParams = new ClusterBaseParameters
            {
                EmptyPlotChance = worldgenConfig.Buildings.EmptyPlotChance,
                TwoBuildingChance = worldgenConfig.Buildings.TwoBuildingChance,
                SteppedBuildingChance = worldgenConfig.Buildings.SteppedBuildingChance,
                SteppedSetbackMin = worldgenConfig.Buildings.SteppedSetbackMin,
                SteppedSetbackMax = worldgenConfig.Buildings.SteppedSetbackMax,
                SteppedAsymmetricChance = worldgenConfig.Buildings.SteppedAsymmetricChance,
                NeonDensityMultiplier = worldgenConfig.Neon.DensityMultiplier,
                WindowLitPercentage = worldgenConfig.Windows.LitPercentage
            };
            clusterProvider = new ClusterInfluenceProvider(clusterConfig, baseParams, config.Seed);
            Log.Info(LogCategory.Chunk, "City cluster system enabled");

            // Pre-warm cluster cache to reduce generation bursts during exploration
            if (clusterConfig.PreWarmRadius > 0)
            {
                int preWarmed = clusterProvider.PreWarm(clusterConfig.PreWarmRadius);
                Log.Info(LogCategory.Chunk, $"Pre-warmed {preWarmed} cluster super-chunks (radius {clusterConfig.PreWarmRadius})");
            }
        }

        // Create generators
        var roadGenerator = new RoadGenerator(worldgenConfig.Roads);
        var buildingGenerator = new BuildingGenerator(
            worldgenConfig.Buildings,
            worldgenConfig.Roads,
            worldgenConfig.Facades,
            worldgenConfig.Windows.LitPercentage,
            worldgenConfig.Neon.DensityMultiplier,
            clusterProvider);
        var neonGenerator = new NeonPlacementGenerator(worldgenConfig.Neon, worldgenConfig.Facades);

        // Create chunk manager with generators
        var chunkManager = new ChunkManager(
            chunkSize: config.ChunkSize,
            loadRadius: config.LoadRadius,
            unloadRadius: config.UnloadRadius,
            maxLoadsPerTick: worldgenConfig.Chunks.MaxLoadsPerTick,
            maxConcurrentLoads: worldgenConfig.Chunks.MaxConcurrentLoads,
            worldSeed: config.Seed,
            roadGenerator: roadGenerator,
            buildingGenerator: buildingGenerator,
            neonGenerator: neonGenerator
        );

        // Create chunk renderer and subscribe to manager events
        var chunkRenderer = new ChunkRenderer(
            parent,
            worldgenConfig.Chunks,
            worldgenConfig.Materials,
            worldgenConfig.Roads,
            worldgenConfig.Facades,
            worldgenConfig.FacadeWeathering,
            worldgenConfig.FacadeDetails,
            worldgenConfig.Windows,
            worldgenConfig.Neon,
            worldgenConfig.RoadMarkings,
            worldgenConfig.WetSurface,
            worldgenConfig.StreetLights,
            worldgenConfig.Manholes,
            worldgenConfig.Smoke,
            worldgenConfig.OcclusionCulling
        );
        chunkRenderer.SubscribeTo(chunkManager);

        Log.Info(LogCategory.Chunk, $"Chunk system ready: size={config.ChunkSize}, loadRadius={config.LoadRadius}");

        return (chunkManager, chunkRenderer);
    }
}
