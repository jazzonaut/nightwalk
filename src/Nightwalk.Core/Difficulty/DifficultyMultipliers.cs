namespace Nightwalk.Core.Difficulty;

/// <summary>
/// Contains all multiplier values for a difficulty level.
/// Multipliers scale against the baseline config values (Explorer = 1.0x).
/// </summary>
public sealed class DifficultyMultipliers
{
    // Costs (higher = more expensive)
    public float ZiplinePlacementCost { get; init; } = 1.0f;
    public float PrismPlacementCost { get; init; } = 1.0f;
    public float JetpackEnergyCost { get; init; } = 1.0f;

    // Gains (lower = less reward)
    public float CollectibleBasePoints { get; init; } = 1.0f;
    public float BeaconBasePoints { get; init; } = 1.0f;
    public float ComboMaxMultiplier { get; init; } = 1.0f;

    // Damage (higher = more punishing)
    public float FallDamageThreshold { get; init; } = 1.0f; // Lower = triggers earlier
    public float FallDamagePerVelocity { get; init; } = 1.0f;
    public float FlightScoreDrain { get; init; } = 1.0f;

    // Limits (absolute overrides, -1 = use config)
    public int MaxZiplinesAbsolute { get; init; } = -1; // 0 = unlimited
    public int MaxPrismsAbsolute { get; init; } = -1;
    public float ZiplineMaxDistance { get; init; } = 1.0f;

    // Physical
    public float EnergyRegen { get; init; } = 1.0f;
    public float Gravity { get; init; } = 1.0f;

    // Flags
    public bool DisableFallDamage { get; init; }
    public bool DisableScoring { get; init; }
    public bool FreePlacements { get; init; }
    public bool HideScoreHUD { get; init; }

    /// <summary>
    /// Observer mode - free exploration, no costs, no damage, no score.
    /// </summary>
    public static DifficultyMultipliers Observer { get; } = new()
    {
        ZiplinePlacementCost = 0f,
        PrismPlacementCost = 0f,
        JetpackEnergyCost = 0.3f,
        CollectibleBasePoints = 0f,
        BeaconBasePoints = 0f,
        ComboMaxMultiplier = 1.0f,
        FallDamageThreshold = 1.0f,
        FallDamagePerVelocity = 0f,
        FlightScoreDrain = 0f,
        MaxZiplinesAbsolute = 0, // Unlimited
        MaxPrismsAbsolute = 0,   // Unlimited
        ZiplineMaxDistance = 1.5f, // Extended range
        EnergyRegen = 1.0f, // Faster regen
        Gravity = 1.0f,
        DisableFallDamage = true,
        DisableScoring = true,
        FreePlacements = true,
        HideScoreHUD = true
    };

    /// <summary>
    /// Explorer mode - baseline balanced experience.
    /// </summary>
    public static DifficultyMultipliers Explorer { get; } = new()
    {
        ZiplinePlacementCost = 1.0f,
        PrismPlacementCost = 1.0f,
        JetpackEnergyCost = 1.0f,
        CollectibleBasePoints = 1.0f,
        BeaconBasePoints = 1.0f,
        ComboMaxMultiplier = 1.0f,
        FallDamageThreshold = 1.0f,
        FallDamagePerVelocity = 1.0f,
        FlightScoreDrain = 1.0f,
        MaxZiplinesAbsolute = -1, // Use config
        MaxPrismsAbsolute = -1,
        ZiplineMaxDistance = 1.0f,
        EnergyRegen = 1.0f,
        Gravity = 1.0f,
        DisableFallDamage = false,
        DisableScoring = false,
        FreePlacements = false,
        HideScoreHUD = false
    };

    /// <summary>
    /// Wanderer mode - challenging for experienced players.
    /// </summary>
    public static DifficultyMultipliers Wanderer { get; } = new()
    {
        ZiplinePlacementCost = 1.5f,
        PrismPlacementCost = 1.5f,
        JetpackEnergyCost = 1.2f,
        CollectibleBasePoints = 0.75f,
        BeaconBasePoints = 0.75f,
        ComboMaxMultiplier = 0.85f,
        FallDamageThreshold = 0.85f, // Lower threshold = triggers earlier
        FallDamagePerVelocity = 1.3f,
        FlightScoreDrain = 1.3f,
        MaxZiplinesAbsolute = 4,
        MaxPrismsAbsolute = -1, // Use config
        ZiplineMaxDistance = 0.85f,
        EnergyRegen = 1.0f,
        Gravity = 1.0f,
        DisableFallDamage = false,
        DisableScoring = false,
        FreePlacements = false,
        HideScoreHUD = false
    };

    /// <summary>
    /// Architect mode - brutal mastery challenge.
    /// </summary>
    public static DifficultyMultipliers Architect { get; } = new()
    {
        ZiplinePlacementCost = 2.0f,
        PrismPlacementCost = 2.0f,
        JetpackEnergyCost = 1.5f,
        CollectibleBasePoints = 0.5f,
        BeaconBasePoints = 0.5f,
        ComboMaxMultiplier = 0.7f,
        FallDamageThreshold = 0.7f, // Much lower threshold
        FallDamagePerVelocity = 2.0f,
        FlightScoreDrain = 2.0f,
        MaxZiplinesAbsolute = 2,
        MaxPrismsAbsolute = -1, // Use config
        ZiplineMaxDistance = 0.7f,
        EnergyRegen = 1.0f,
        Gravity = 1.0f,
        DisableFallDamage = false,
        DisableScoring = false,
        FreePlacements = false,
        HideScoreHUD = false
    };

    /// <summary>
    /// Gets the multipliers for a given difficulty level.
    /// </summary>
    public static DifficultyMultipliers ForLevel(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Observer => Observer,
        DifficultyLevel.Explorer => Explorer,
        DifficultyLevel.Wanderer => Wanderer,
        DifficultyLevel.Architect => Architect,
        _ => Explorer
    };
}
