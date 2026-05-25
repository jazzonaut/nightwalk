namespace Nightwalk.Core.Difficulty;

/// <summary>
/// Central service for difficulty-based value scaling.
/// </summary>
public sealed class DifficultyService : IDifficultyService
{
    private DifficultyLevel _level = DifficultyLevel.Explorer;
    private DifficultyMultipliers _multipliers = DifficultyMultipliers.Explorer;

    public DifficultyLevel Level => _level;
    public DifficultyMultipliers Multipliers => _multipliers;

    public void SetDifficulty(DifficultyLevel level)
    {
        _level = level;
        _multipliers = DifficultyMultipliers.ForLevel(level);
    }

    // Flags
    public bool IsScoringDisabled => _multipliers.DisableScoring;
    public bool IsFallDamageDisabled => _multipliers.DisableFallDamage;
    public bool IsScoreHUDHidden => _multipliers.HideScoreHUD;
    public bool ArePlacementsFree => _multipliers.FreePlacements;

    // Scaled costs
    public int GetZiplinePlacementCost(int baseCost)
    {
        if (_multipliers.FreePlacements) return 0;
        return (int)(baseCost * _multipliers.ZiplinePlacementCost);
    }

    public int GetPrismPlacementCost(int baseCost)
    {
        if (_multipliers.FreePlacements) return 0;
        return (int)(baseCost * _multipliers.PrismPlacementCost);
    }

    // Scaled rewards
    public int GetCollectiblePoints(int basePoints)
    {
        if (_multipliers.DisableScoring) return 0;
        return (int)(basePoints * _multipliers.CollectibleBasePoints);
    }

    public int GetBeaconRewardPoints(int basePoints)
    {
        if (_multipliers.DisableScoring) return 0;
        return (int)(basePoints * _multipliers.BeaconBasePoints);
    }

    // Scaled limits (0 = unlimited, -1 = use config value)
    public int GetMaxZiplines(int configValue)
    {
        var absolute = _multipliers.MaxZiplinesAbsolute;
        if (absolute == 0) return 0; // Unlimited
        if (absolute > 0) return absolute; // Use absolute value
        return configValue; // Use config
    }

    public int GetMaxPrisms(int configValue)
    {
        var absolute = _multipliers.MaxPrismsAbsolute;
        if (absolute == 0) return 0; // Unlimited
        if (absolute > 0) return absolute; // Use absolute value
        return configValue; // Use config
    }

    public float GetZiplineMaxDistance(float configValue)
    {
        return configValue * _multipliers.ZiplineMaxDistance;
    }

    // Scaled damage
    public float GetFallDamageThreshold(float configValue)
    {
        // Lower multiplier = lower threshold = earlier damage
        return configValue * _multipliers.FallDamageThreshold;
    }

    public float GetFallDamagePerVelocity(float configValue)
    {
        return configValue * _multipliers.FallDamagePerVelocity;
    }

    public float GetFlightScoreDrain(float configValue)
    {
        return configValue * _multipliers.FlightScoreDrain;
    }

    // Scaled physical
    public float GetEnergyRegen(float configValue)
    {
        return configValue * _multipliers.EnergyRegen;
    }

    public float GetJetpackEnergyCost(float configValue)
    {
        return configValue * _multipliers.JetpackEnergyCost;
    }
}
