namespace Nightwalk.Core.Difficulty;

/// <summary>
/// Service for accessing difficulty-scaled game values.
/// </summary>
public interface IDifficultyService
{
    /// <summary>
    /// Current difficulty level.
    /// </summary>
    DifficultyLevel Level { get; }

    /// <summary>
    /// Current multipliers for the difficulty level.
    /// </summary>
    DifficultyMultipliers Multipliers { get; }

    /// <summary>
    /// Sets the difficulty level.
    /// </summary>
    void SetDifficulty(DifficultyLevel level);

    // Flags
    bool IsScoringDisabled { get; }
    bool IsFallDamageDisabled { get; }
    bool IsScoreHUDHidden { get; }
    bool ArePlacementsFree { get; }

    // Scaled costs
    int GetZiplinePlacementCost(int baseCost);
    int GetPrismPlacementCost(int baseCost);

    // Scaled rewards
    int GetCollectiblePoints(int basePoints);
    int GetBeaconRewardPoints(int basePoints);

    // Scaled limits
    int GetMaxZiplines(int configValue);
    int GetMaxPrisms(int configValue);
    float GetZiplineMaxDistance(float configValue);

    // Scaled damage
    float GetFallDamageThreshold(float configValue);
    float GetFallDamagePerVelocity(float configValue);
    float GetFlightScoreDrain(float configValue);

    // Scaled physical
    float GetEnergyRegen(float configValue);
    float GetJetpackEnergyCost(float configValue);
}
