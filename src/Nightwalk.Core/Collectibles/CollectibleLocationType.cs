namespace Nightwalk.Core.Collectibles;

/// <summary>
/// Identifies the type of location where a collectible can spawn.
/// </summary>
public enum CollectibleLocationType : byte
{
    /// <summary>Collectible on a building rooftop (height-based scoring).</summary>
    BuildingRooftop = 0,

    /// <summary>Collectible on an empty plot (fixed scoring).</summary>
    EmptyPlot = 1,

    // Future location types:
    // Alleyway = 2,
    // StreetFixture = 3,
}
