using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;
using Nightwalk.Core.World.Buildings;

namespace Nightwalk.Core.Beacons;

/// <summary>
/// Generates beacon data deterministically based on building seeds.
/// </summary>
public static class BeaconGenerator
{
    // Purpose codes for StableHash to ensure distinct random sequences
    private const int PurposeSpawnChance = unchecked((int)0xBEAC0001);
    private const int PurposeFrequency = unchecked((int)0xBEAC0002);

    /// <summary>
    /// Checks if a building would have a beacon without generating full data.
    /// Use this for quick eligibility checks (e.g., to exclude from collectible spawning).
    /// </summary>
    public static bool WouldHaveBeacon(BuildingData building, BeaconSettings config)
    {
        if (building.Height < config.MinBuildingHeight)
            return false;

        float roll = StableHash.ToFloat01(building.Seed, PurposeSpawnChance);
        return roll < config.SpawnChance;
    }

    /// <summary>
    /// Determines if a building should have a beacon and generates its ID and frequency.
    /// Note: Position is calculated by BeaconSpawner using BuildingRenderer.GetRooftopCenterPosition()
    /// to account for rooftop features.
    /// </summary>
    /// <param name="coord">Chunk coordinate containing the building.</param>
    /// <param name="buildingIndex">Index of the building within the chunk.</param>
    /// <param name="building">Building data.</param>
    /// <param name="config">Beacon configuration.</param>
    /// <returns>BeaconData with ID and frequency if building has a beacon, null otherwise.</returns>
    public static BeaconData? TryGenerateBeacon(
        ChunkCoord coord,
        int buildingIndex,
        BuildingData building,
        BeaconSettings config)
    {
        if (!WouldHaveBeacon(building, config))
            return null;

        // Determine frequency
        int frequencyIndex = StableHash.ToInt(building.Seed, PurposeFrequency, BeaconFrequency.Count);
        var frequency = new BeaconFrequency(frequencyIndex);

        var id = BeaconId.ForBuilding(coord, buildingIndex);
        // Position is populated by BeaconSpawner using the shared rooftop position calculation
        return new BeaconData(id, default, frequency);
    }
}
