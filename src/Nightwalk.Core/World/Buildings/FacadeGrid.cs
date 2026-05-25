namespace Nightwalk.Core.World.Buildings;

/// <summary>
/// Pre-computed window grid layout for a facade.
/// Ensures consistent window positioning across all systems.
/// </summary>
public readonly struct FacadeGrid
{
    /// <summary>Number of window columns on front/back facades (along X axis).</summary>
    public int ColumnsX { get; init; }

    /// <summary>Number of window columns on left/right facades (along Z axis).</summary>
    public int ColumnsZ { get; init; }

    /// <summary>Offset from left edge to first window center (X axis).</summary>
    public float StartOffsetX { get; init; }

    /// <summary>Offset from front edge to first window center (Z axis).</summary>
    public float StartOffsetZ { get; init; }

    /// <summary>Number of floors with windows.</summary>
    public int NumFloors { get; init; }

    /// <summary>Window spacing used for this grid.</summary>
    public float WindowSpacing { get; init; }

    /// <summary>Floor height used for this grid.</summary>
    public float FloorHeight { get; init; }

    /// <summary>Ground floor height (solid base before first windows).</summary>
    public float GroundFloorHeight { get; init; }

    /// <summary>
    /// Computes a facade grid for given dimensions and config.
    /// </summary>
    public static FacadeGrid Compute(
        float width, float depth, float height,
        float windowSpacing, float groundFloorHeight, float floorHeight, float edgeMargin)
    {
        var columnsX = Math.Max(0, (int)((width - edgeMargin) / windowSpacing));
        var columnsZ = Math.Max(0, (int)((depth - edgeMargin) / windowSpacing));

        var totalSpanX = columnsX * windowSpacing;
        var totalSpanZ = columnsZ * windowSpacing;

        return new FacadeGrid
        {
            ColumnsX = columnsX,
            ColumnsZ = columnsZ,
            StartOffsetX = (width - totalSpanX) / 2f + windowSpacing / 2f,
            StartOffsetZ = (depth - totalSpanZ) / 2f + windowSpacing / 2f,
            NumFloors = Math.Max(0, (int)((height - groundFloorHeight) / floorHeight)),
            WindowSpacing = windowSpacing,
            FloorHeight = floorHeight,
            GroundFloorHeight = groundFloorHeight
        };
    }

    /// <summary>
    /// Gets the X position of a window column relative to facade center.
    /// </summary>
    /// <param name="column">Column index (0-based).</param>
    /// <param name="facadeWidth">Width of the facade.</param>
    public float GetWindowX(int column, float facadeWidth)
    {
        return -facadeWidth / 2f + StartOffsetX + column * WindowSpacing;
    }

    /// <summary>
    /// Gets the Z position of a window column relative to facade center.
    /// </summary>
    /// <param name="column">Column index (0-based).</param>
    /// <param name="facadeDepth">Depth of the facade.</param>
    public float GetWindowZ(int column, float facadeDepth)
    {
        return -facadeDepth / 2f + StartOffsetZ + column * WindowSpacing;
    }

    /// <summary>
    /// Gets the Y position of a window on a given floor.
    /// </summary>
    /// <param name="floor">Floor index (0-based, 0 = first floor above ground).</param>
    /// <param name="baseY">Base Y position (0 for ground level sections).</param>
    /// <param name="floorOffsetFactor">Vertical position within floor (0.5 = center).</param>
    public float GetWindowY(int floor, float baseY, float floorOffsetFactor = 0.5f)
    {
        return baseY + GroundFloorHeight + floor * FloorHeight + FloorHeight * floorOffsetFactor;
    }

    /// <summary>
    /// Gets the horizontal offset from facade center for a given column.
    /// Useful for neon placement which uses normalized offsets.
    /// </summary>
    /// <param name="column">Column index (0-based).</param>
    public float GetColumnOffsetFromCenter(int column)
    {
        return (column - (ColumnsX - 1) / 2f) * WindowSpacing;
    }

    /// <summary>
    /// Gets number of columns for a given facade direction.
    /// </summary>
    /// <param name="facade">Facade index: 0=front, 1=back, 2=left, 3=right.</param>
    public int GetColumnsForFacade(int facade)
    {
        return facade < 2 ? ColumnsX : ColumnsZ;
    }

    /// <summary>
    /// Gets the start offset for a given facade direction.
    /// </summary>
    /// <param name="facade">Facade index: 0=front, 1=back, 2=left, 3=right.</param>
    public float GetStartOffsetForFacade(int facade)
    {
        return facade < 2 ? StartOffsetX : StartOffsetZ;
    }
}
