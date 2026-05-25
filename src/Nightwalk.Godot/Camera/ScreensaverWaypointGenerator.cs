using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.World.Roads;

namespace Nightwalk.Godot.Camera;

/// <summary>
/// Generates continuous flight paths for screensaver mode.
/// Flies along the road network at street level.
/// </summary>
public sealed class ScreensaverWaypointGenerator
{
    private readonly List<Vector3> _waypoints = new();
    private readonly Random _random = new();

    private ScreensaverConfig _config;
    private ChunkManager? _chunkManager;

    // Road following state
    private RoadDirection _currentRoadDirection;
    private float _currentRoadPosition;
    private bool _movingPositive;
    private float _currentAltitude;

    /// <summary>Current waypoints in the path.</summary>
    public IReadOnlyList<Vector3> Waypoints => _waypoints;

    /// <summary>Number of waypoints currently in the buffer.</summary>
    public int WaypointCount => _waypoints.Count;

    public ScreensaverWaypointGenerator(ScreensaverConfig config)
    {
        _config = config;
    }

    public void Configure(ScreensaverConfig config)
    {
        _config = config;
    }

    public void SetChunkManager(ChunkManager chunkManager)
    {
        _chunkManager = chunkManager;
    }

    public void Initialize(Vector3 startPosition)
    {
        _waypoints.Clear();

        if (_chunkManager == null)
            return;

        // Find the nearest road
        var (road, _) = FindNearestRoad(startPosition.X, startPosition.Z);
        if (road == null)
            return;

        // Initialize on the road
        _currentRoadDirection = road.Value.Direction;
        _currentRoadPosition = road.Value.Position;
        _movingPositive = _random.NextDouble() > 0.5;
        _currentAltitude = Mathf.Lerp(_config.LowAltitudeMin, _config.LowAltitudeMax, (float)_random.NextDouble());

        float startX, startZ;
        if (_currentRoadDirection == RoadDirection.EastWest)
        {
            startX = startPosition.X;
            startZ = _currentRoadPosition;
        }
        else
        {
            startX = _currentRoadPosition;
            startZ = startPosition.Z;
        }

        _waypoints.Add(new Vector3(startX, _currentAltitude, startZ));

        // Generate initial waypoints
        while (_waypoints.Count < _config.WaypointsAhead)
        {
            GenerateNextWaypoint();
        }
    }

    public bool RefreshWaypoints(int consumedCount)
    {
        if (consumedCount <= 0)
            return false;

        int toRemove = Math.Min(consumedCount, _waypoints.Count);
        _waypoints.RemoveRange(0, toRemove);

        bool generated = false;
        while (_waypoints.Count < _config.WaypointsAhead)
        {
            GenerateNextWaypoint();
            generated = true;
        }

        return generated;
    }

    private void GenerateNextWaypoint()
    {
        if (_waypoints.Count == 0 || _chunkManager == null)
            return;

        var lastPos = _waypoints[^1];
        float distance = Mathf.Lerp(_config.MinWaypointDistance, _config.MaxWaypointDistance, (float)_random.NextDouble());

        float nextX, nextZ;
        if (_currentRoadDirection == RoadDirection.EastWest)
        {
            nextX = lastPos.X + (_movingPositive ? distance : -distance);
            nextZ = _currentRoadPosition;
        }
        else
        {
            nextX = _currentRoadPosition;
            nextZ = lastPos.Z + (_movingPositive ? distance : -distance);
        }

        // Check for intersection and maybe turn
        var intersection = FindIntersectionNear(nextX, nextZ);
        if (intersection != null && _random.NextDouble() < _config.TurnChance)
        {
            float alt = GetAltitude();
            _waypoints.Add(new Vector3(intersection.Value.X, alt, intersection.Value.Y));

            _currentRoadDirection = _currentRoadDirection == RoadDirection.EastWest
                ? RoadDirection.NorthSouth
                : RoadDirection.EastWest;

            _currentRoadPosition = _currentRoadDirection == RoadDirection.EastWest
                ? intersection.Value.Y
                : intersection.Value.X;

            _movingPositive = _random.NextDouble() > 0.5;
            return;
        }

        // Check if next position is loaded
        var chunk = _chunkManager?.GetChunkAtPosition(nextX, nextZ);
        if (chunk == null)
        {
            TryTurnToLoadedArea(lastPos);
            return;
        }

        // Add waypoint
        _waypoints.Add(new Vector3(nextX, GetAltitude(), nextZ));
    }

    private void TryTurnToLoadedArea(Vector3 lastPos)
    {
        var intersection = FindNearestIntersection(lastPos.X, lastPos.Z);
        if (intersection != null)
        {
            float alt = GetAltitude();
            _waypoints.Add(new Vector3(intersection.Value.X, alt, intersection.Value.Y));

            _currentRoadDirection = _currentRoadDirection == RoadDirection.EastWest
                ? RoadDirection.NorthSouth
                : RoadDirection.EastWest;

            _currentRoadPosition = _currentRoadDirection == RoadDirection.EastWest
                ? intersection.Value.Y
                : intersection.Value.X;

            var center = GetLoadedChunksCenter();
            _movingPositive = _currentRoadDirection == RoadDirection.EastWest
                ? center.X > intersection.Value.X
                : center.Z > intersection.Value.Y;
        }
        else
        {
            // Just reverse direction
            _movingPositive = !_movingPositive;
            float backDist = _config.MinWaypointDistance;
            float backX, backZ;
            if (_currentRoadDirection == RoadDirection.EastWest)
            {
                backX = lastPos.X + (_movingPositive ? backDist : -backDist);
                backZ = _currentRoadPosition;
            }
            else
            {
                backX = _currentRoadPosition;
                backZ = lastPos.Z + (_movingPositive ? backDist : -backDist);
            }
            _waypoints.Add(new Vector3(backX, GetAltitude(), backZ));
        }
    }

    private float GetAltitude()
    {
        // Only change altitude based on configured chance
        if (_random.NextDouble() < _config.AltitudeChangeChance)
        {
            _currentAltitude = Mathf.Lerp(_config.LowAltitudeMin, _config.LowAltitudeMax, (float)_random.NextDouble());
        }
        return _currentAltitude;
    }

    private (RoadSegment?, ChunkData?) FindNearestRoad(float worldX, float worldZ)
    {
        if (_chunkManager == null)
            return (null, null);

        float chunkSize = _chunkManager.ChunkSize;
        RoadSegment? bestRoad = null;
        ChunkData? bestChunk = null;
        float bestDistance = float.MaxValue;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                var chunk = _chunkManager.GetChunkAtPosition(worldX + dx * chunkSize * 0.5f, worldZ + dz * chunkSize * 0.5f);
                if (chunk == null) continue;

                foreach (var road in chunk.Roads)
                {
                    float dist = road.Direction == RoadDirection.EastWest
                        ? MathF.Abs(worldZ - road.Position)
                        : MathF.Abs(worldX - road.Position);

                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestRoad = road;
                        bestChunk = chunk;
                    }
                }
            }
        }

        return (bestRoad, bestChunk);
    }

    private Vector2? FindIntersectionNear(float worldX, float worldZ)
    {
        if (_chunkManager == null)
            return null;

        var chunk = _chunkManager.GetChunkAtPosition(worldX, worldZ);
        if (chunk == null)
            return null;

        foreach (var road in chunk.Roads)
        {
            if (road.Direction == _currentRoadDirection)
                continue;

            float distToRoad = road.Direction == RoadDirection.EastWest
                ? MathF.Abs(worldZ - road.Position)
                : MathF.Abs(worldX - road.Position);

            if (distToRoad < _config.MinWaypointDistance * 0.5f)
            {
                return _currentRoadDirection == RoadDirection.EastWest
                    ? new Vector2(road.Position, _currentRoadPosition)
                    : new Vector2(_currentRoadPosition, road.Position);
            }
        }

        return null;
    }

    private Vector2? FindNearestIntersection(float worldX, float worldZ)
    {
        if (_chunkManager == null)
            return null;

        float chunkSize = _chunkManager.ChunkSize;
        Vector2? best = null;
        float bestDist = float.MaxValue;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                var chunk = _chunkManager.GetChunkAtPosition(worldX + dx * chunkSize * 0.5f, worldZ + dz * chunkSize * 0.5f);
                if (chunk == null) continue;

                var ewRoads = new List<float>();
                var nsRoads = new List<float>();

                foreach (var road in chunk.Roads)
                {
                    if (road.Direction == RoadDirection.EastWest)
                        ewRoads.Add(road.Position);
                    else
                        nsRoads.Add(road.Position);
                }

                foreach (var ewPos in ewRoads)
                {
                    foreach (var nsPos in nsRoads)
                    {
                        float distX = worldX - nsPos;
                        float distZ = worldZ - ewPos;
                        float dist = distX * distX + distZ * distZ;

                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = new Vector2(nsPos, ewPos);
                        }
                    }
                }
            }
        }

        return best;
    }

    private Vector3 GetLoadedChunksCenter()
    {
        if (_chunkManager == null || _chunkManager.LoadedChunks.Count == 0)
            return Vector3.Zero;

        float sumX = 0f, sumZ = 0f;
        int count = 0;

        foreach (var (coord, _) in _chunkManager.LoadedChunks)
        {
            var (worldX, worldZ) = coord.ToWorldPosition(_chunkManager.ChunkSize);
            sumX += worldX + _chunkManager.ChunkSize / 2f;
            sumZ += worldZ + _chunkManager.ChunkSize / 2f;
            count++;
        }

        return new Vector3(sumX / count, 0f, sumZ / count);
    }
}
