namespace Nightwalk.Core.World.Buildings;

/// <summary>
/// Represents a plot of land within a city block.
/// Plots are rendered as pavement and may contain buildings or be empty (parks).
/// </summary>
public readonly struct PlotData
{
    public float MinX { get; init; }
    public float MinZ { get; init; }
    public float MaxX { get; init; }
    public float MaxZ { get; init; }

    // Which edges border roads (for curb rendering)
    public bool HasRoadMinX { get; init; }
    public bool HasRoadMaxX { get; init; }
    public bool HasRoadMinZ { get; init; }
    public bool HasRoadMaxZ { get; init; }

    public float Width => MaxX - MinX;
    public float Depth => MaxZ - MinZ;
    public float CenterX => (MinX + MaxX) / 2f;
    public float CenterZ => (MinZ + MaxZ) / 2f;
}
