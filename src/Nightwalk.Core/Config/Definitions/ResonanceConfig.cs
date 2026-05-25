namespace Nightwalk.Core.Config;

/// <summary>
/// Configuration for Resonance mode mechanics.
/// </summary>
public sealed class ResonanceConfig
{
    public ModeConfig Mode { get; set; } = null!;
    public PlayerConfig Player { get; set; } = null!;
    public AlertConfig Alert { get; set; } = null!;
    public ExposureConfig Exposure { get; set; } = null!;
    public WardenConfig Warden { get; set; } = null!;
    public SeekerConfig Seeker { get; set; } = null!;
    public CounterplayConfig Counterplay { get; set; } = null!;
    public RepelConfig Repel { get; set; } = null!;
}

/// <summary>
/// General mode configuration.
/// </summary>
public sealed class ModeConfig
{
    /// <summary>Default game mode on launch.</summary>
    public string DefaultMode { get; set; } = null!;
}

/// <summary>
/// Player-related configuration for resonance mechanics.
/// </summary>
public sealed class PlayerConfig
{
    /// <summary>Player height for detection targeting.</summary>
    public float Height { get; set; }

    /// <summary>Fraction of height for center mass offset.</summary>
    public float CenterOffsetFraction { get; set; }
}

/// <summary>
/// Alert meter configuration.
/// </summary>
public sealed class AlertConfig
{
    /// <summary>Alert increase per collectible pickup (0-1).</summary>
    public float PickupIncrease { get; set; }

    /// <summary>Alert increase per second while detected by Seeker.</summary>
    public float SeekerDetectionIncrease { get; set; }

    /// <summary>Alert decay rate per second when safe.</summary>
    public float DecayRate { get; set; }

    /// <summary>Seconds before alert decay starts.</summary>
    public float DecayDelay { get; set; }

    /// <summary>Alert level (0-1) that triggers Warden spawn.</summary>
    public float WardenSpawnThreshold { get; set; }

    /// <summary>Alert level (0-1) below which Warden departs.</summary>
    public float WardenDespawnThreshold { get; set; }

    /// <summary>Alert level (0-1) at which Warden becomes vulnerable.</summary>
    public float VulnerabilityThreshold { get; set; }
}

/// <summary>
/// Exposure meter configuration.
/// </summary>
public sealed class ExposureConfig
{
    /// <summary>Exposure increase per second while in Seeker scan cone.</summary>
    public float RiseRate { get; set; }

    /// <summary>Exposure decrease per second when safe.</summary>
    public float FallRate { get; set; }

    /// <summary>Points drained per second while exposed (scaled by exposure level).</summary>
    public float ScoreDrainRate { get; set; }

    /// <summary>Score penalty at 100% exposure.</summary>
    public int MaxExposurePenalty { get; set; }

    /// <summary>Alert increase (0-1) at 100% exposure.</summary>
    public float MaxExposureAlertSpike { get; set; }

    /// <summary>Exposure level below which max exposure can trigger again.</summary>
    public float MaxExposureResetThreshold { get; set; }
}

/// <summary>
/// Warden entity configuration.
/// </summary>
public sealed class WardenConfig
{
    /// <summary>Height above player where Warden spawns.</summary>
    public float SpawnHeight { get; set; }

    /// <summary>Warden drift speed toward player (units per second).</summary>
    public float DriftSpeed { get; set; }

    /// <summary>Duration the core stays exposed when vulnerable.</summary>
    public float VulnerabilityDuration { get; set; }

    /// <summary>Speed at which Warden departs.</summary>
    public float DepartureSpeed { get; set; }

    /// <summary>Warden body scale in meters.</summary>
    public float Scale { get; set; }

    // Spawn/arrival settings
    /// <summary>Extra height at spawn before descending.</summary>
    public float SpawnDescentOffset { get; set; }

    /// <summary>Multiplier for descent speed vs drift speed.</summary>
    public float DescentSpeedMultiplier { get; set; }

    /// <summary>Distance threshold for considering arrived.</summary>
    public float ArrivalThreshold { get; set; }

    /// <summary>Drift speed multiplier when vulnerable.</summary>
    public float VulnerableDriftMultiplier { get; set; }

    /// <summary>Distance above target to consider departed.</summary>
    public float DepartureDistance { get; set; }

    /// <summary>Minimum distance before drift stops.</summary>
    public float DriftMinDistance { get; set; }

    /// <summary>Number of shards in the cluster.</summary>
    public int ShardCount { get; set; }

    /// <summary>Hex color for shard base (outer edge, navy).</summary>
    public string ShardBaseColor { get; set; } = null!;

    /// <summary>Hex color for shard bright (inner core, cyan).</summary>
    public string ShardBrightColor { get; set; } = null!;

    /// <summary>Hex color for vulnerable shard (orange).</summary>
    public string VulnerableColor { get; set; } = null!;

    /// <summary>Cluster radius as fraction of scale.</summary>
    public float ClusterRadius { get; set; }

    /// <summary>Core hit area as fraction of scale.</summary>
    public float CoreHitRadiusFraction { get; set; }

    // Visual animation settings (Godot-side)
    /// <summary>Hover animation speed.</summary>
    public float HoverSpeed { get; set; }

    /// <summary>Hover animation amplitude.</summary>
    public float HoverAmplitude { get; set; }

    /// <summary>Core glow fade-in speed.</summary>
    public float CoreGlowLerpIn { get; set; }

    /// <summary>Core glow fade-out speed.</summary>
    public float CoreGlowLerpOut { get; set; }

    /// <summary>Base value for glow pulse.</summary>
    public float CoreGlowPulseBase { get; set; }

    /// <summary>Amplitude of glow pulse.</summary>
    public float CoreGlowPulseAmplitude { get; set; }

    /// <summary>Frequency of glow pulse.</summary>
    public float CoreGlowPulseFrequency { get; set; }

    /// <summary>Core light energy multiplier.</summary>
    public float CoreLightEnergy { get; set; }
}

/// <summary>
/// Seeker entity configuration.
/// </summary>
public sealed class SeekerConfig
{
    /// <summary>Chance (0-1) to spawn a Seeker per collectible pickup.</summary>
    public float SpawnChance { get; set; }

    /// <summary>Random offset range from Warden when spawning.</summary>
    public float SpawnOffsetRange { get; set; }

    /// <summary>Seeker travel speed at low alert.</summary>
    public float TravelSpeedLow { get; set; }

    /// <summary>Seeker travel speed at high alert.</summary>
    public float TravelSpeedHigh { get; set; }

    /// <summary>Seconds spent scanning at ping location.</summary>
    public float ScanDuration { get; set; }

    /// <summary>Scan cone half-angle in degrees.</summary>
    public float ScanConeAngle { get; set; }

    /// <summary>Scan cone range in meters.</summary>
    public float ScanConeRange { get; set; }

    /// <summary>Radius of patrol orbit while scanning.</summary>
    public float PatrolRadius { get; set; }

    /// <summary>Return speed to Warden vicinity.</summary>
    public float ReturnSpeed { get; set; }

    /// <summary>Maximum Seekers at low alert tier.</summary>
    public int MaxCountLowAlert { get; set; }

    /// <summary>Maximum Seekers at medium alert tier.</summary>
    public int MaxCountMedAlert { get; set; }

    /// <summary>Maximum Seekers at high alert tier.</summary>
    public int MaxCountHighAlert { get; set; }

    /// <summary>Seeker diameter in meters.</summary>
    public float Scale { get; set; }

    /// <summary>Hex color for Seeker rings.</summary>
    public string RingColor { get; set; } = null!;

    /// <summary>Hex color for Seeker scan cone.</summary>
    public string ScanColor { get; set; } = null!;

    // Movement settings (Perlin noise)
    /// <summary>Perlin noise amplitude for organic wandering.</summary>
    public float WanderAmplitude { get; set; }

    /// <summary>Perlin noise frequency for wandering.</summary>
    public float WanderFrequency { get; set; }

    /// <summary>Velocity acceleration rate.</summary>
    public float Acceleration { get; set; }

    /// <summary>Height above target when scanning.</summary>
    public float HoverHeight { get; set; }

    /// <summary>Distance at which wander fades out.</summary>
    public float ProximityFadeDistance { get; set; }

    // Approaching settings
    /// <summary>Distance to activate searchlight.</summary>
    public float ApproachRadius { get; set; }

    /// <summary>Slower speed when approaching target.</summary>
    public float ApproachSpeed { get; set; }

    /// <summary>Distance to consider arrived at target.</summary>
    public float ArrivalDistance { get; set; }

    // Scanning settings
    /// <summary>Orbital angular speed (rad/s).</summary>
    public float OrbitSpeed { get; set; }

    /// <summary>Altitude wave amplitude during scan.</summary>
    public float AltitudeOscillation { get; set; }

    /// <summary>Speed multiplier during scanning.</summary>
    public float ScanSpeedMultiplier { get; set; }

    /// <summary>Minimum scan time before allowing ping redirect.</summary>
    public float MinScanBeforeRedirect { get; set; }

    /// <summary>Sweep pattern time multiplier.</summary>
    public float SweepFrequency { get; set; }

    /// <summary>Sweep radius as fraction of patrol radius.</summary>
    public float SweepRadiusMultiplier { get; set; }

    /// <summary>Vertical sweep range in meters.</summary>
    public float SweepVerticalAmplitude { get; set; }

    /// <summary>Max distance for collectible ping to pull hunters/returners.</summary>
    public float NoisePullMaxDistance { get; set; }

    // Hunting settings
    /// <summary>Seconds searching after losing player.</summary>
    public float HuntDuration { get; set; }

    /// <summary>Speed during hunt mode.</summary>
    public float HuntSpeed { get; set; }

    /// <summary>Spiral search angular speed.</summary>
    public float HuntSpiralAngularSpeed { get; set; }

    /// <summary>Spiral radius expansion rate.</summary>
    public float HuntSpiralExpansion { get; set; }

    // Returning settings
    /// <summary>Distance to Warden to despawn.</summary>
    public float ReturnPoolDistance { get; set; }

    /// <summary>Distance to Warden during recall to despawn.</summary>
    public float RecallPoolDistance { get; set; }

    /// <summary>Maximum recall duration before forced despawn.</summary>
    public float RecallTimeout { get; set; }

    /// <summary>Spotlight brightness.</summary>
    public float LightEnergy { get; set; }

    /// <summary>Volumetric fog intensity for searchlight.</summary>
    public float LightVolumetricEnergy { get; set; }

    // Alert tier thresholds
    /// <summary>Alert level for high tier.</summary>
    public float AlertTierHigh { get; set; }

    /// <summary>Alert level for medium tier.</summary>
    public float AlertTierMedium { get; set; }

    // Detection settings
    /// <summary>Seconds between detection pings.</summary>
    public float DetectionPingCooldown { get; set; }

    /// <summary>Seconds before hunt mode after losing sight.</summary>
    public float DetectionGracePeriod { get; set; }

    /// <summary>Seconds after spawn before dispatching.</summary>
    public float DispatchDelay { get; set; }

    /// <summary>Minimum patrol radius when no ping.</summary>
    public float IdlePatrolMinRadius { get; set; }

    /// <summary>Maximum patrol radius when no ping.</summary>
    public float IdlePatrolMaxRadius { get; set; }

    // Ping scoring settings
    /// <summary>Seconds for recency bonus calculation.</summary>
    public float PingRecencyWindow { get; set; }

    /// <summary>Score weight for recency.</summary>
    public float PingRecencyWeight { get; set; }

    /// <summary>Score weight for priority.</summary>
    public float PingPriorityWeight { get; set; }

    /// <summary>Score penalty per unit distance.</summary>
    public float PingDistancePenalty { get; set; }

    /// <summary>Score penalty per assigned seeker.</summary>
    public float PingAssignedPenalty { get; set; }

    /// <summary>Score difference to trigger redirect.</summary>
    public float PingRedirectThreshold { get; set; }

    /// <summary>Priority for noise pings.</summary>
    public float PingNoisePriority { get; set; }

    /// <summary>Priority for detection pings.</summary>
    public float PingDetectionPriority { get; set; }

    /// <summary>Seconds before noise ping expires.</summary>
    public float PingNoiseExpiry { get; set; }

    /// <summary>Seconds before detection ping expires.</summary>
    public float PingDetectionExpiry { get; set; }

    // Collision avoidance settings
    /// <summary>Force of avoidance steering.</summary>
    public float AvoidanceStrength { get; set; }

    /// <summary>Raycast distance for obstacle detection.</summary>
    public float AvoidanceRayLength { get; set; }

    // Visual animation settings (Godot-side)
    /// <summary>Hover bobbing speed.</summary>
    public float HoverSpeed { get; set; }

    /// <summary>Hover bobbing height.</summary>
    public float HoverAmplitude { get; set; }

    /// <summary>Ring rotation speed.</summary>
    public float RingRotationSpeed { get; set; }

    /// <summary>Speed for snap-to-target aiming.</summary>
    public float AimSnapSpeed { get; set; }

    /// <summary>Speed for sweep aiming.</summary>
    public float AimSweepSpeed { get; set; }

    /// <summary>Speed for player tracking.</summary>
    public float AimTrackSpeed { get; set; }

    /// <summary>Scan light fade speed.</summary>
    public float ScanIntensityLerp { get; set; }
}

/// <summary>
/// Counterplay (spotlight lure) configuration.
/// </summary>
public sealed class CounterplayConfig
{
    /// <summary>Seconds to hold spotlight to create decoy.</summary>
    public float SpotlightLureChargeTime { get; set; }

    /// <summary>How long decoy persists in seconds.</summary>
    public float DecoyDuration { get; set; }

    /// <summary>Priority multiplier for Seekers investigating decoys vs noise pings.</summary>
    public float DecoyPriority { get; set; }

    /// <summary>Maximum number of active decoys.</summary>
    public int MaxActiveDecoys { get; set; }
}

/// <summary>
/// Warden repel configuration.
/// </summary>
public sealed class RepelConfig
{
    /// <summary>Seconds beam must contact core to repel Warden.</summary>
    public float BeamContactDuration { get; set; }

    /// <summary>Rate at which beam progress decays when not hitting.</summary>
    public float BeamContactDecayRate { get; set; }

    /// <summary>Alert reduction (0-1) on successful repel.</summary>
    public float AlertDrop { get; set; }

    /// <summary>Score awarded on successful repel.</summary>
    public int ScoreReward { get; set; }

    /// <summary>Seconds of scan suppression after repel.</summary>
    public float CalmWindow { get; set; }

    /// <summary>Seconds before vulnerability can trigger again after successful repel.</summary>
    public float RepelCooldown { get; set; }
}
