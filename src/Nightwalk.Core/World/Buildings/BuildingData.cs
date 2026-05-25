namespace Nightwalk.Core.World.Buildings;

/// <summary>
/// Represents a single section of a stepped building.
/// Each section has its own footprint that may be smaller than the section below.
/// </summary>
public sealed class BuildingSection
{
    /// <summary>Height at which this section starts (relative to ground).</summary>
    public float StartY { get; set; }

    /// <summary>Height at which this section ends.</summary>
    public float EndY { get; set; }

    /// <summary>Width of this section (may be smaller than base).</summary>
    public float Width { get; set; }

    /// <summary>Depth of this section (may be smaller than base).</summary>
    public float Depth { get; set; }

    /// <summary>X offset from building center (for asymmetric setbacks).</summary>
    public float OffsetX { get; set; }

    /// <summary>Z offset from building center (for asymmetric setbacks).</summary>
    public float OffsetZ { get; set; }

    /// <summary>Pre-computed window grid for this section.</summary>
    public FacadeGrid Grid { get; set; }
}

/// <summary>
/// Data for a single building within a chunk.
/// Position is in world coordinates.
/// </summary>
public sealed class BuildingData
{
    /// <summary>
    /// World X position of building center.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// World Z position of building center.
    /// </summary>
    public float Z { get; set; }

    /// <summary>
    /// Building width (along X axis).
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// Building depth (along Z axis).
    /// </summary>
    public float Depth { get; set; }

    /// <summary>
    /// Building height (Y axis).
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    /// Seed for deterministic details (windows, features).
    /// </summary>
    public int Seed { get; set; }

    /// <summary>
    /// Distance from world origin (affects height in downtown).
    /// </summary>
    public float DistanceFromCenter { get; set; }

    /// <summary>
    /// Building sections from bottom to top. If null, building is a simple box.
    /// </summary>
    public List<BuildingSection>? Sections { get; set; }

    /// <summary>
    /// Whether this building has a stepped profile.
    /// </summary>
    public bool IsStepped => Sections != null && Sections.Count > 1;

    /// <summary>
    /// Pre-computed window grid for the base building (used when Sections is null).
    /// </summary>
    public FacadeGrid Grid { get; set; }

    // Cluster aesthetic parameters (applied during generation)

    /// <summary>
    /// Neon density multiplier from cluster influence (1.0 = default).
    /// </summary>
    public float NeonDensityMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Window lit percentage from cluster influence.
    /// </summary>
    public float WindowLitPercentage { get; set; } = 0.15f;

    /// <summary>
    /// Gets the facade grid for a specific height, handling stepped buildings.
    /// </summary>
    public FacadeGrid GetGridAtHeight(float y)
    {
        if (Sections == null || Sections.Count == 0)
            return Grid;

        foreach (var section in Sections)
        {
            if (y >= section.StartY && y <= section.EndY)
                return section.Grid;
        }

        // Above all sections, use topmost
        return Sections[^1].Grid;
    }
}
