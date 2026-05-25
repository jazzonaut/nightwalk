namespace Nightwalk.Core.World.Roads;

/// <summary>
/// A segment of road within a chunk.
/// Roads are infinite lines, but we only store the portion within each chunk.
/// </summary>
public readonly struct RoadSegment
{
    /// <summary>
    /// World-space position along the road's perpendicular axis.
    /// For EastWest roads, this is the Z position.
    /// For NorthSouth roads, this is the X position.
    /// </summary>
    public float Position { get; }

    /// <summary>
    /// Direction the road runs.
    /// </summary>
    public RoadDirection Direction { get; }

    /// <summary>
    /// Type of road (affects width).
    /// </summary>
    public RoadType Type { get; }

    /// <summary>
    /// Half-width of the road (from center to edge).
    /// </summary>
    public float HalfWidth { get; }

    public RoadSegment(float position, RoadDirection direction, RoadType type, float halfWidth)
    {
        Position = position;
        Direction = direction;
        Type = type;
        HalfWidth = halfWidth;
    }

    /// <summary>
    /// Full width of the road.
    /// </summary>
    public float Width => HalfWidth * 2;

    /// <summary>
    /// Checks if a world position is on this road.
    /// </summary>
    public bool ContainsPoint(float x, float z)
    {
        return Direction switch
        {
            RoadDirection.EastWest => MathF.Abs(z - Position) <= HalfWidth,
            RoadDirection.NorthSouth => MathF.Abs(x - Position) <= HalfWidth,
            _ => false
        };
    }
}
