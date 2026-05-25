using Nightwalk.Core.Buildings;
using Nightwalk.Core.Buildings.Events;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.CityAliveness.Events;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.World.Roads;

namespace Nightwalk.Core.CityAliveness;

/// <summary>
/// Tracks which roads are "alive" (showing car trail effects).
/// Roads become alive when adjacent buildings are climbed.
/// </summary>
public sealed class CityAlivenessService
{
    private readonly IEventBus _eventBus;
    private readonly ChunkManager _chunkManager;
    private readonly GameDataService _data;
    private readonly HashSet<RoadId> _aliveRoads = new();
    private readonly HashSet<BuildingId> _climbedBuildingIds = new();
    private readonly List<ClimbedBuildingBounds> _climbedBuildings = new();

    private CarTrailsSettings Config => _data.Aliveness.CarTrails;

    private readonly struct ClimbedBuildingBounds
    {
        public readonly float MinX, MaxX, MinZ, MaxZ;

        public ClimbedBuildingBounds(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }
    }

    public CityAlivenessService(
        IEventBus eventBus,
        ChunkManager chunkManager,
        GameDataService data)
    {
        _eventBus = eventBus;
        _chunkManager = chunkManager;
        _data = data;

        _eventBus.Subscribe<BuildingClimbedEvent>(OnBuildingClimbed);
    }

    public bool IsAlive(RoadId roadId) => _aliveRoads.Contains(roadId);

    public IReadOnlySet<RoadId> GetAliveRoads() => _aliveRoads;

    public void Reset()
    {
        _aliveRoads.Clear();
        _climbedBuildingIds.Clear();
        _climbedBuildings.Clear();
    }

    public void Cleanup()
    {
        _eventBus.Unsubscribe<BuildingClimbedEvent>(OnBuildingClimbed);
    }

    public void OnChunkLoaded(ChunkCoord coord, ChunkData chunk)
    {
        if (!Config.Enabled || _climbedBuildings.Count == 0)
            return;

        for (int i = 0; i < chunk.Roads.Count; i++)
        {
            var roadId = new RoadId(coord, i);
            if (_aliveRoads.Contains(roadId))
                continue;

            var road = chunk.Roads[i];
            foreach (var bounds in _climbedBuildings)
            {
                if (TryMarkRoadAlive(roadId, road, bounds))
                    break;
            }
        }
    }

    private void OnBuildingClimbed(BuildingClimbedEvent e)
    {
        if (!Config.Enabled)
            return;

        // Skip if already processed this building
        if (!_climbedBuildingIds.Add(e.BuildingId))
            return;

        var chunk = _chunkManager.GetChunk(e.BuildingId.ChunkCoord);
        if (chunk == null || e.BuildingId.BuildingIndex >= chunk.Buildings.Count)
            return;

        var building = chunk.Buildings[e.BuildingId.BuildingIndex];
        var margin = Config.AdjacencyMargin;

        var bounds = new ClimbedBuildingBounds(
            building.X - building.Width / 2f - margin,
            building.X + building.Width / 2f + margin,
            building.Z - building.Depth / 2f - margin,
            building.Z + building.Depth / 2f + margin
        );

        _climbedBuildings.Add(bounds);

        // Check roads in currently loaded chunks that overlap with building bounds
        foreach (var chunkCoord in GetLoadedChunksOverlapping(bounds))
        {
            var checkChunk = _chunkManager.GetChunk(chunkCoord);
            if (checkChunk == null)
                continue;

            for (int i = 0; i < checkChunk.Roads.Count; i++)
            {
                var roadId = new RoadId(chunkCoord, i);
                if (!_aliveRoads.Contains(roadId))
                {
                    TryMarkRoadAlive(roadId, checkChunk.Roads[i], bounds);
                }
            }
        }
    }

    private bool TryMarkRoadAlive(RoadId roadId, RoadSegment road, ClimbedBuildingBounds bounds)
    {
        if (!IsRoadAdjacent(road, bounds))
            return false;

        _aliveRoads.Add(roadId);
        _eventBus.Publish(new RoadAlivenessChangedEvent(
            roadId,
            road.Direction,
            road.Position,
            road.HalfWidth
        ));
        return true;
    }

    private List<ChunkCoord> GetLoadedChunksOverlapping(ClimbedBuildingBounds bounds)
    {
        var chunkSize = _chunkManager.ChunkSize;

        // Find chunk range that overlaps with the bounds
        var minChunk = ChunkCoord.FromWorldPosition(bounds.MinX, bounds.MinZ, chunkSize);
        var maxChunk = ChunkCoord.FromWorldPosition(bounds.MaxX, bounds.MaxZ, chunkSize);

        var chunks = new List<ChunkCoord>((maxChunk.X - minChunk.X + 1) * (maxChunk.Z - minChunk.Z + 1));

        for (int x = minChunk.X; x <= maxChunk.X; x++)
        {
            for (int z = minChunk.Z; z <= maxChunk.Z; z++)
            {
                var coord = new ChunkCoord(x, z);
                if (_chunkManager.GetChunk(coord) != null)
                    chunks.Add(coord);
            }
        }

        return chunks;
    }

    private static bool IsRoadAdjacent(RoadSegment road, ClimbedBuildingBounds bounds)
    {
        return road.Direction == RoadDirection.EastWest
            ? road.Position >= bounds.MinZ && road.Position <= bounds.MaxZ
            : road.Position >= bounds.MinX && road.Position <= bounds.MaxX;
    }
}
