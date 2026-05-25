using Nightwalk.Core.Config;

namespace Nightwalk.Core.World.Roads;

/// <summary>
/// Generates road segments for a chunk based on world-aligned grid.
/// Roads are positioned at regular intervals in world space, ensuring
/// seamless connections across chunk boundaries.
/// </summary>
public sealed class RoadGenerator
{
    private readonly float _majorSpacing;
    private readonly float _minorSpacing;
    private readonly float _majorHalfWidth;
    private readonly float _minorHalfWidth;
    private readonly float _sidewalkWidth;

    public RoadGenerator(RoadConfig config)
    {
        _majorSpacing = config.MajorSpacing;
        _minorSpacing = config.MinorSpacing;
        _majorHalfWidth = config.MajorWidth / 2f;
        _minorHalfWidth = config.MinorWidth / 2f;
        _sidewalkWidth = config.SidewalkWidth;
    }

    /// <summary>
    /// Generates all road segments that pass through a chunk.
    /// </summary>
    public List<RoadSegment> GenerateRoads(float chunkMinX, float chunkMinZ, float chunkSize)
    {
        var roads = new List<RoadSegment>();
        var chunkMaxX = chunkMinX + chunkSize;
        var chunkMaxZ = chunkMinZ + chunkSize;

        // Generate East-West roads (along X axis, positioned on Z)
        GenerateRoadsAlongAxis(roads, chunkMinZ, chunkMaxZ, RoadDirection.EastWest);

        // Generate North-South roads (along Z axis, positioned on X)
        GenerateRoadsAlongAxis(roads, chunkMinX, chunkMaxX, RoadDirection.NorthSouth);

        return roads;
    }

    private void GenerateRoadsAlongAxis(List<RoadSegment> roads, float minPos, float maxPos, RoadDirection direction)
    {
        // Find all major roads in range
        var firstMajor = MathF.Floor(minPos / _majorSpacing) * _majorSpacing;
        for (var pos = firstMajor; pos <= maxPos; pos += _majorSpacing)
        {
            if (pos >= minPos - _majorHalfWidth && pos <= maxPos + _majorHalfWidth)
            {
                roads.Add(new RoadSegment(pos, direction, RoadType.Major, _majorHalfWidth));
            }
        }

        // Find all minor roads in range (skip where major roads are)
        var firstMinor = MathF.Floor(minPos / _minorSpacing) * _minorSpacing;
        for (var pos = firstMinor; pos <= maxPos; pos += _minorSpacing)
        {
            if (pos >= minPos - _minorHalfWidth && pos <= maxPos + _minorHalfWidth)
            {
                // Skip if this position has a major road
                if (!IsNearMajorRoad(pos))
                {
                    roads.Add(new RoadSegment(pos, direction, RoadType.Minor, _minorHalfWidth));
                }
            }
        }
    }

    private bool IsNearMajorRoad(float pos)
    {
        var nearestMajor = MathF.Round(pos / _majorSpacing) * _majorSpacing;
        // Skip minor roads within ~30 units of a major road
        // This ensures enough space for buildings between roads
        // (major half-width + sidewalk + min building + sidewalk + minor half-width ≈ 32)
        float minDistance = _majorHalfWidth + _minorHalfWidth + _sidewalkWidth * 2 + 8f;
        return MathF.Abs(pos - nearestMajor) < minDistance;
    }

    /// <summary>
    /// Gets the sidewalk width for road edges.
    /// </summary>
    public float SidewalkWidth => _sidewalkWidth;
}
