namespace Nightwalk.Core.World.Roads;

/// <summary>
/// Type of road affecting width and visual style.
/// </summary>
public enum RoadType
{
    /// <summary>
    /// Major roads are wider, spaced further apart.
    /// </summary>
    Major,

    /// <summary>
    /// Minor roads are narrower, form the smaller grid.
    /// </summary>
    Minor
}

/// <summary>
/// Direction a road segment runs.
/// </summary>
public enum RoadDirection
{
    /// <summary>
    /// Road runs along the X axis (East-West).
    /// </summary>
    EastWest,

    /// <summary>
    /// Road runs along the Z axis (North-South).
    /// </summary>
    NorthSouth
}
