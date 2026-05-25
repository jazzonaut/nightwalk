namespace Nightwalk.Core.Config;

/// <summary>
/// World generation configuration.
/// </summary>
public sealed class WorldgenConfig
{
    public int Seed { get; set; }
    public ChunkConfig Chunks { get; set; } = null!;
    public RoadConfig Roads { get; set; } = null!;
    public BuildingConfig Buildings { get; set; } = null!;
    public FacadeConfig Facades { get; set; } = null!;
    public FacadeWeatheringConfig FacadeWeathering { get; set; } = null!;
    public FacadeDetailConfig FacadeDetails { get; set; } = null!;
    public NeonConfig Neon { get; set; } = null!;
    public WindowConfig Windows { get; set; } = null!;
    public MaterialsConfig Materials { get; set; } = null!;
    public RoadMarkingsConfig RoadMarkings { get; set; } = null!;
    public WetSurfaceConfig WetSurface { get; set; } = null!;
    public StreetLightsConfig StreetLights { get; set; } = null!;
    public ManholeConfig Manholes { get; set; } = null!;
    public SmokeConfig Smoke { get; set; } = null!;
    public OcclusionCullingConfig OcclusionCulling { get; set; } = null!;
}

public sealed class ChunkConfig
{
    public float Size { get; set; }
    public int LoadRadius { get; set; }
    public int UnloadRadius { get; set; }
    public int MaxLoadsPerTick { get; set; }
    public int MaxConcurrentLoads { get; set; }

    // Progressive rendering configuration
    public bool ProgressiveRenderingEnabled { get; set; }
    public int RenderBudgetMicroseconds { get; set; }
    public int MaxConcurrentRenders { get; set; }

    // Debug
    public bool DebugShowBoundaries { get; set; }
}

public sealed class RoadConfig
{
    public float MajorSpacing { get; set; }
    public float MinorSpacing { get; set; }
    public float MajorWidth { get; set; }
    public float MinorWidth { get; set; }
    public float SidewalkWidth { get; set; }
    public float RoadHeight { get; set; }
    public float SidewalkHeight { get; set; }
    public float CurbHeight { get; set; }
    public float CurbWidth { get; set; }
}

public sealed class BuildingConfig
{
    public float EdgeMargin { get; set; }

    // Heights
    public float MinHeight { get; set; }
    public float MaxHeight { get; set; }

    // Fallback values used when cluster system is disabled
    public float DowntownRadius { get; set; }
    public float DowntownHeightMultiplier { get; set; }
    public float EmptyPlotChance { get; set; }
    public float TwoBuildingChance { get; set; }

    // Stepped building configuration
    public float SteppedMinHeight { get; set; }
    public int SteppedMinSections { get; set; }
    public int SteppedMaxSections { get; set; }
    public float SteppedSectionMinHeight { get; set; }
    public float SteppedSetbackMin { get; set; }
    public float SteppedSetbackMax { get; set; }
    public float SteppedBuildingChance { get; set; }
    public float SteppedAsymmetricChance { get; set; }

    // City cluster system (when enabled, overrides the above fallback values)
    public ClusterConfig? Clusters { get; set; }
}

public sealed class FacadeConfig
{
    public float GroundFloorHeight { get; set; }
    public float FloorHeight { get; set; }
    public float WindowWidth { get; set; }
    public float WindowHeight { get; set; }
    public float WindowSpacing { get; set; }
    public float LedgeDepth { get; set; }
    public float LedgeHeight { get; set; }
    public float FacadeInset { get; set; }
    public float RooftopFeatureChance { get; set; }
}

public sealed class NeonConfig
{
    public float PlacementDensity { get; set; }
    public float DensityMultiplier { get; set; }
    public float IntensityMin { get; set; }
    public float IntensityMax { get; set; }
    public List<string> ColorPalette { get; set; } = null!;

    // Large signs (upper facade)
    public float LargeSignChance { get; set; }
    public float LargeSignMinWidth { get; set; }
    public float LargeSignMaxWidth { get; set; }
    public float LargeSignMinHeight { get; set; }
    public float LargeSignMaxHeight { get; set; }
    public float LargeSignMinFloor { get; set; }

    // Vertical strips
    public float CornerStripChance { get; set; }
    public float CornerStripWidth { get; set; }
    public float FullHeightStripChance { get; set; }
    public float FullHeightStripWidth { get; set; }

    // Kanji signs and billboards
    public float KanjiSignChance { get; set; }
    public float KanjiSignMinHeight { get; set; }
    public float BillboardChance { get; set; }
    public float BillboardMinHeight { get; set; }

    // Light casting for broken signs
    public bool NeonLightCastingEnabled { get; set; } = true;
    public float NeonLightRange { get; set; } = 8.0f;
    public float NeonLightEnergy { get; set; } = 2.0f;

    // LOD settings
    public float LodParallaxDistance { get; set; }
    public float LodLightDistance { get; set; }
    public float LodLightFadeLength { get; set; }
    public float LodSmallElementVisibilityRange { get; set; }
    public float LodSmallElementVisibilityMargin { get; set; }
}

public sealed class WindowConfig
{
    public float LitPercentage { get; set; }

    // Procedural variation
    public WindowVariationConfig Variation { get; set; } = null!;
}

public sealed class WindowVariationConfig
{
    // Floor clustering
    public float DarkFloorChance { get; set; }
    public float FullyLitFloorChance { get; set; }
    public float CheckerboardFloorChance { get; set; }

    // Color temperature
    public string WarmColor { get; set; } = null!;
    public string CoolColor { get; set; } = null!;
    public string AccentMagenta { get; set; } = null!;
    public string AccentCyan { get; set; } = null!;
    public float AccentChance { get; set; }

    // Curtains/blinds
    public float CurtainChance { get; set; }
    public float BlindsHalfChance { get; set; }
    public float BlindsClosedChance { get; set; }
}

public sealed class MaterialsConfig
{
    public MaterialConfig Ground { get; set; } = null!;
    public MaterialConfig Road { get; set; } = null!;
    public MaterialConfig Sidewalk { get; set; } = null!;
    public MaterialConfig Building { get; set; } = null!;
    public MaterialConfig BuildingDark { get; set; } = null!;
    public MaterialConfig BuildingBlue { get; set; } = null!;
    public MaterialConfig BuildingPurple { get; set; } = null!;
    public MaterialConfig BuildingWarm { get; set; } = null!;
    public MaterialConfig BuildingWeathered { get; set; } = null!;
    public MaterialConfig WindowUnlit { get; set; } = null!;
    public MaterialConfig WindowLit { get; set; } = null!;
    public MaterialConfig Ledge { get; set; } = null!;
    public MaterialConfig RoadMarking { get; set; } = null!;
}

public sealed class RoadMarkingsConfig
{
    public bool Enabled { get; set; }
    public float CenterLineWidth { get; set; }
    public float LaneLineWidth { get; set; }
    public float DashLength { get; set; }
    public float DashGap { get; set; }
    public float CrosswalkStripeWidth { get; set; }
    public float CrosswalkStripeGap { get; set; }
    public int CrosswalkStripeCount { get; set; }
    public float CrosswalkChance { get; set; }

    // LOD settings
    public float LodVisibilityRange { get; set; } = 150.0f;
    public float LodVisibilityMargin { get; set; } = 30.0f;
}

public sealed class MaterialConfig
{
    public string Color { get; set; } = null!;
    public float Roughness { get; set; }
    public float Metallic { get; set; }
    public string? Emission { get; set; }
    public float EmissionStrength { get; set; }
}

/// <summary>
/// Wet surface shader configuration for roads and sidewalks.
/// </summary>
public sealed class WetSurfaceConfig
{
    // Base wetness
    public float Wetness { get; set; }
    public float WetRoughness { get; set; }
    public float WetDarkening { get; set; }

    // Asphalt texture variation
    public float TextureVariation { get; set; }
    public float TextureScale { get; set; }

    // Puddles
    public bool PuddlesEnabled { get; set; }
    public float PuddleScale { get; set; }
    public float PuddleThreshold { get; set; }
    public float PuddleEdgeSoftness { get; set; }

    // Ripples
    public bool RipplesEnabled { get; set; }
    public float RippleSpeed { get; set; }
    public float RippleScale { get; set; }
    public float RippleStrength { get; set; }
    public float TimeScale { get; set; }

    // Wetness variation
    public float WetnessNoiseScale { get; set; }
    public float WetnessNoiseStrength { get; set; }
}

/// <summary>
/// Street lamp configuration for cyberpunk halo-style lighting.
/// </summary>
public sealed class StreetLightsConfig
{
    public bool Enabled { get; set; }
    public float Spacing { get; set; }
    public string LightColor { get; set; } = null!;
    public float LightEnergy { get; set; }
    public float LightRange { get; set; }
    public float LightHeight { get; set; }
    public float OmniAttenuation { get; set; }
    public bool ShadowEnabled { get; set; }
    public int MaxPerChunk { get; set; }

    // LOD settings
    public float LodLightDistance { get; set; }
    public float LodLightFadeLength { get; set; }
}

/// <summary>
/// Shader-based weathering effects for building facades.
/// </summary>
public sealed class FacadeWeatheringConfig
{
    public bool Enabled { get; set; }

    // Dirt/grime accumulation
    public float DirtIntensity { get; set; }
    public float DirtHeightFalloff { get; set; }
    public float DirtCornerIntensity { get; set; }
    public string DirtColor { get; set; } = null!;

    // Water damage streaks
    public float WaterStreakIntensity { get; set; }
    public float WaterStreakScale { get; set; }
    public float WaterStreakSparsity { get; set; }
    public float WaterStreakStrength { get; set; }
    public string WaterStainColor { get; set; } = null!;

    // Rust stains
    public float RustIntensity { get; set; }
    public string RustColor { get; set; } = null!;
    public float RustDripLength { get; set; }
    public float RustChance { get; set; }
    public float RustStrength { get; set; }

    // Surface wear
    public float RoughnessVariation { get; set; }
    public float WornRoughnessBoost { get; set; }

    // Noise texture settings
    public float NoiseScaleLarge { get; set; }
    public float NoiseScaleDetail { get; set; }
}

/// <summary>
/// Geometry-based facade details (AC units, pipes, etc.).
/// </summary>
public sealed class FacadeDetailConfig
{
    public bool Enabled { get; set; }

    // AC units
    public bool AcUnitsEnabled { get; set; }
    public float AcUnitChance { get; set; }
    public float AcUnitWidth { get; set; }
    public float AcUnitHeight { get; set; }
    public float AcUnitDepth { get; set; }
    public int AcUnitMinFloor { get; set; }

    // Pipes
    public bool PipesEnabled { get; set; }
    public float PipeChance { get; set; }
    public float PipeRadiusMin { get; set; }
    public float PipeRadiusMax { get; set; }
    public int PipeMaxFloors { get; set; }

    // Material for detail geometry
    public MaterialConfig Material { get; set; } = null!;
}

/// <summary>
/// Manhole cover configuration for road intersections.
/// </summary>
public sealed class ManholeConfig
{
    public bool Enabled { get; set; }
    public int MinPerChunk { get; set; }
    public int MaxPerChunk { get; set; }
    public float CoverDiameter { get; set; }
    public float CoverDepth { get; set; }
    public string Pattern { get; set; } = null!;

    // Pattern geometry
    public float GridBarWidth { get; set; }
    public float GridSpacing { get; set; }
    public float RimWidth { get; set; }
    public float RidgeHeight { get; set; }

    // Material
    public string MaterialColor { get; set; } = null!;
    public float MaterialRoughness { get; set; }
    public float MaterialMetallic { get; set; }

    // Smoke
    public bool SmokeEnabled { get; set; }
    public float SmokeChance { get; set; }
}

/// <summary>
/// GPU particle smoke/steam effect configuration.
/// </summary>
public sealed class SmokeConfig
{
    // Appearance
    public string AlbedoColor { get; set; } = null!;

    // Particles
    public int ParticleCount { get; set; }
    public float Lifetime { get; set; }
    public float ParticleSize { get; set; }
    public float ScaleMin { get; set; }
    public float ScaleMax { get; set; }

    // Emitter size variation (applied per-emitter)
    public float EmitterScaleMin { get; set; }
    public float EmitterScaleMax { get; set; }

    // Movement
    public float VelocityMin { get; set; }
    public float VelocityMax { get; set; }
    public float Spread { get; set; }
    public float Gravity { get; set; }

    // Turbulence
    public float TurbulenceStrength { get; set; }
    public float TurbulenceScale { get; set; }

    // LOD
    public float FadeDistanceMin { get; set; }
    public float FadeDistanceMax { get; set; }
}

/// <summary>
/// Occlusion culling configuration for buildings.
/// Uses box occluders to skip rendering objects hidden behind buildings.
/// </summary>
public sealed class OcclusionCullingConfig
{
    /// <summary>
    /// Enable occlusion culling for buildings.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Minimum building height to create an occluder.
    /// Shorter buildings may not provide enough occlusion benefit.
    /// </summary>
    public float MinBuildingHeight { get; set; } = 10.0f;

    /// <summary>
    /// Minimum building footprint area (width * depth) to create an occluder.
    /// Very thin buildings may not be effective occluders.
    /// </summary>
    public float MinFootprintArea { get; set; } = 50.0f;

    /// <summary>
    /// Shrink occluder boxes by this margin on each side to avoid z-fighting
    /// and ensure objects at building edges aren't incorrectly culled.
    /// </summary>
    public float OccluderMargin { get; set; } = 0.5f;

    /// <summary>
    /// Include rooftop features (AC units, elevator shafts) as occluders.
    /// </summary>
    public bool IncludeRooftopFeatures { get; set; } = true;
}
