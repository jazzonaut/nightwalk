using Nightwalk.Core.Config.Attributes;

namespace Nightwalk.Core.Config;

/// <summary>
/// Core game configuration.
/// </summary>
public sealed class CoreConfig
{
    public DebugConfig Debug { get; set; } = null!;
    public GraphicsConfig Graphics { get; set; } = null!;
    public WeatherConfig Weather { get; set; } = null!;
    public SkyEffectsConfig SkyEffects { get; set; } = null!;
}

public sealed class DebugConfig
{
    [UserConfigurable(SettingsCategory.Gameplay, SettingsControlType.Toggle, Order = 10)]
    [SettingsDisplay(DisplayName = "Show FPS", Description = "Display frames per second counter on screen")]
    public bool ShowFps { get; set; }

    [UserConfigurable(SettingsCategory.Gameplay, SettingsControlType.Toggle, Order = 11)]
    [SettingsDisplay(DisplayName = "Hide Game Mode Selection", Description = "Hide the game mode cards on the New Game screen. Last-selected mode is kept.")]
    public bool HideGameModeSelection { get; set; }

    public bool LoggingEnabled { get; set; }
    public string[] LoggingCategories { get; set; } = [];
}

/// <summary>
/// Window display mode.
/// </summary>
public enum WindowMode
{
    Windowed = 0,
    Borderless = 1,
    Fullscreen = 2
}

/// <summary>
/// 3D resolution upscaling mode.
/// </summary>
public enum UpscalingMode
{
    Bilinear = 0,
    Fsr1 = 1,
    Fsr2 = 2
}

/// <summary>
/// Variable Rate Shading mode.
/// </summary>
public enum VrsMode
{
    Disabled = 0,
    Texture = 1
}

public sealed class GraphicsConfig
{
    // Display
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Dropdown, Order = 1)]
    [SettingsDisplay(DisplayName = "Window Mode", Description = "How the game window is displayed")]
    public WindowMode WindowMode { get; set; }

    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Dropdown, Order = 2)]
    [SettingsDisplay(DisplayName = "Resolution", Description = "Screen resolution. Default uses your desktop resolution")]
    public string Resolution { get; set; } = "Default";

    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 5)]
    [SettingsDisplay(DisplayName = "VSync", Description = "Synchronizes frame rate to monitor refresh rate to prevent screen tearing")]
    public bool VSync { get; set; }

    // 3D Resolution Scaling (FSR)
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Dropdown, Order = 6)]
    [SettingsDisplay(DisplayName = "Upscaling", Description = "3D upscaling method. FSR 2.2 provides best quality with built-in anti-aliasing")]
    public UpscalingMode UpscalingMode { get; set; }

    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Slider, Order = 7)]
    [SettingsRange(0.5, 1.0, Step = 0.05)]
    [SettingsDisplay(DisplayName = "Render Scale", ValueFormat = "{0:P0}", Description = "3D render resolution. Lower values improve performance but reduce quality")]
    public float RenderScale { get; set; } = 1.0f;

    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Slider, Order = 8)]
    [SettingsRange(0.0, 2.0, Step = 0.1)]
    [SettingsDisplay(DisplayName = "FSR Sharpness", ValueFormat = "{0:F1}", Description = "Sharpness for FSR modes. Lower values are sharper")]
    public float FsrSharpness { get; set; } = 0.2f;

    // Variable Rate Shading
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Dropdown, Order = 9)]
    [SettingsDisplay(DisplayName = "Variable Rate Shading", Description = "Reduces shading work in less detailed areas. Can improve performance")]
    public VrsMode VrsMode { get; set; }

    // Anti-aliasing
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Dropdown, Order = 10)]
    [SettingsOptions("Off", 0, "2x MSAA", 2, "4x MSAA", 4, "8x MSAA", 8)]
    [SettingsDisplay(DisplayName = "Anti-Aliasing", Description = "Reduces jagged edges on geometry. Higher values improve quality but reduce performance")]
    public int Msaa { get; set; }  // 0=off, 2=2x, 4=4x, 8=8x

    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 11)]
    [SettingsDisplay(DisplayName = "FXAA", Description = "Fast approximate anti-aliasing with low performance cost")]
    public bool Fxaa { get; set; }

    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 12)]
    [SettingsDisplay(DisplayName = "TAA", Description = "Temporal anti-aliasing that smooths edges over multiple frames")]
    public bool Taa { get; set; }

    // Background
    public string BackgroundColor { get; set; } = null!;

    // Sky gradient
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 15)]
    [SettingsDisplay(DisplayName = "Sky", Description = "Enable procedural sky gradient rendering")]
    public bool SkyEnabled { get; set; }
    public string SkyTopColor { get; set; } = null!;
    public string SkyHorizonColor { get; set; } = null!;
    public string SkyGroundColor { get; set; } = null!;
    public string SkyGroundHorizonColor { get; set; } = null!;
    public float SkyCurve { get; set; }
    public float SkyEnergy { get; set; }
    public float SkyGroundEnergy { get; set; }

    // Ambient lighting
    public string AmbientColor { get; set; } = null!;
    public float AmbientEnergy { get; set; }

    // Standard fog
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 25)]
    [SettingsDisplay(DisplayName = "Distance Fog", Description = "Atmospheric fog that increases with distance from camera")]
    public bool FogEnabled { get; set; }
    public float FogDensity { get; set; }
    public string FogColor { get; set; } = null!;
    public float FogAerialPerspective { get; set; }

    // Height fog (for light pollution effect)
    public bool FogHeightEnabled { get; set; }
    public float FogHeight { get; set; }
    public float FogHeightDensity { get; set; }
    public string FogHeightColor { get; set; } = null!;
    public float FogSunScatter { get; set; }
    public float FogHeightColorBlend { get; set; }

    // Volumetric fog
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 30)]
    [SettingsDisplay(DisplayName = "Volumetric Fog", Description = "3D fog with realistic light scattering. Has noticeable performance impact")]
    public bool VolumetricFogEnabled { get; set; }

    public float VolumetricFogDensity { get; set; }
    public float VolumetricFogLength { get; set; }
    public float VolumetricFogAnisotropy { get; set; }
    public string VolumetricFogAlbedo { get; set; } = null!;
    public string VolumetricFogEmission { get; set; } = null!;
    public float VolumetricFogEmissionEnergy { get; set; }
    public float VolumetricFogGiInject { get; set; }
    public float VolumetricFogAmbientInject { get; set; }
    public float VolumetricFogSkyAffect { get; set; }
    public bool VolumetricFogTemporalReprojectionEnabled { get; set; }
    public float VolumetricFogTemporalReprojectionAmount { get; set; }

    // Bloom/Glow
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 20)]
    [SettingsDisplay(DisplayName = "Bloom", Description = "Adds a soft glow around bright areas")]
    public bool BloomEnabled { get; set; }

    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Slider, Order = 21)]
    [SettingsRange(0.0, 3.0, Step = 0.1)]
    [SettingsDisplay(DisplayName = "Bloom Intensity", ValueFormat = "{0:F1}", Description = "Strength of the bloom glow effect")]
    public float BloomIntensity { get; set; }

    public float BloomStrength { get; set; }
    public float BloomBloom { get; set; }
    public float BloomThreshold { get; set; }
    public float BloomHdrScale { get; set; }
    public float BloomHdrLuminanceCap { get; set; }

    // Tonemapping
    public float TonemapExposure { get; set; }
    public float TonemapWhite { get; set; }

    public float VignetteIntensity { get; set; }

    // SSAO
    public bool SsaoEnabled { get; set; }
    public float SsaoRadius { get; set; }
    public float SsaoIntensity { get; set; }
    public float SsaoPower { get; set; }
    public float SsaoDetail { get; set; }
    public float SsaoHorizon { get; set; }
    public float SsaoSharpness { get; set; }
    public float SsaoLightAffect { get; set; }

    // SSR (Screen-Space Reflections)
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 40)]
    [SettingsDisplay(DisplayName = "Screen-Space Reflections (SSR)", Description = "Real-time reflections on shiny surfaces. Moderate performance impact")]
    public bool SsrEnabled { get; set; }

    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Slider, Order = 41)]
    [SettingsRange(32, 2048, Step = 32)]
    [SettingsDisplay(DisplayName = "SSR Max Steps", Description = "Reflection quality. Higher values are more accurate but slower")]
    public int SsrMaxSteps { get; set; }
    public float SsrFadeIn { get; set; }
    public float SsrFadeOut { get; set; }
    public float SsrDepthTolerance { get; set; }

    // SSIL (Screen-Space Indirect Lighting)
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 42)]
    [SettingsDisplay(DisplayName = "Screen-Space Indirect Lighting (SSIL)", Description = "Adds subtle bounced lighting. Complements SSAO. Moderate performance impact")]
    public bool SsilEnabled { get; set; }
    public float SsilRadius { get; set; }
    public float SsilIntensity { get; set; }
    public float SsilSharpness { get; set; }
    public float SsilNormalRejection { get; set; }

    // Depth of Field
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 45)]
    [SettingsDisplay(DisplayName = "Depth of Field", Description = "Blurs distant objects for cinematic focus effect")]
    public bool DofEnabled { get; set; }
    public float DofFarDistance { get; set; }
    public float DofFarTransition { get; set; }
    public float DofNearDistance { get; set; }
    public float DofNearTransition { get; set; }
    public float DofAmount { get; set; }

    // Auto-Exposure
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 46)]
    [SettingsDisplay(DisplayName = "Auto-Exposure", Description = "Camera adapts to lighting conditions automatically. Moderate performance impact")]
    public bool AutoExposureEnabled { get; set; }
    public float AutoExposureScale { get; set; }
    public float AutoExposureMinSensitivity { get; set; }
    public float AutoExposureMaxSensitivity { get; set; }
    public float AutoExposureSpeed { get; set; }

    // Post-processing: Color Grading
    public bool ColorGradingEnabled { get; set; }
    public string ColorGradingShadowTint { get; set; } = null!;
    public string ColorGradingHighlightTint { get; set; } = null!;
    public float ColorGradingStrength { get; set; }
    public float ColorGradingContrast { get; set; }
    public float ColorGradingSaturation { get; set; }

    // Post-processing: Vignette
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 53)]
    [SettingsDisplay(DisplayName = "Vignette", Description = "Darkens screen corners for a cinematic look")]
    public bool VignetteEnabled { get; set; }
    public float VignetteSoftness { get; set; }
    public string VignetteColor { get; set; } = null!;

    // Post-processing: Film Grain
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 50)]
    [SettingsDisplay(DisplayName = "Film Grain", Description = "Adds subtle noise for a cinematic film look")]
    public bool FilmGrainEnabled { get; set; }
    public float FilmGrainIntensity { get; set; }
    public float FilmGrainSpeed { get; set; }

    // Post-processing: Chromatic Aberration
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 51)]
    [SettingsDisplay(DisplayName = "Chromatic Aberration", Description = "Adds color fringing at screen edges for a lens effect")]
    public bool ChromaticAberrationEnabled { get; set; }
    public float ChromaticAberrationStrength { get; set; }
    public float ChromaticAberrationFalloff { get; set; }

    // Visual preset (saved to user preferences)
    public int VisualPresetIndex { get; set; }
}

/// <summary>
/// Sky effects configuration for aurora, stars, lightning, and electrical atmosphere.
/// </summary>
public sealed class SkyEffectsConfig
{
    public bool Enabled { get; set; }

    // Sky dome geometry
    public float DomeRadius { get; set; }
    public int DomeSegments { get; set; }

    // Aurora
    public bool AuroraEnabled { get; set; }
    public string AuroraPrimaryColor { get; set; } = null!;
    public string AuroraSecondaryColor { get; set; } = null!;
    public float AuroraIntensity { get; set; }
    public float AuroraSpeed { get; set; }
    public float AuroraBandCount { get; set; }

    // Lightning
    public bool LightningEnabled { get; set; }
    public float LightningFrequency { get; set; }
    public float LightningDuration { get; set; }
    public float LightningWorldIllumination { get; set; }

    // Lightning multi-stroke
    public float LightningMultiStrokeChance { get; set; }
    public int LightningMaxStrokes { get; set; }
    public float LightningStrokeDurationMin { get; set; }
    public float LightningStrokeDurationMax { get; set; }
    public float LightningFirstStrokeIntensityMin { get; set; }
    public float LightningFirstStrokeIntensityMax { get; set; }
    public float LightningFollowUpIntensityMin { get; set; }
    public float LightningFollowUpIntensityMax { get; set; }
    public float LightningStrokeGapMin { get; set; }
    public float LightningStrokeGapMax { get; set; }
    public float LightningPositionDrift { get; set; }
    public float LightningIntervalVariation { get; set; }

    // Electrical atmosphere - floating ember particles
    public bool ElectricalAtmosphereEnabled { get; set; }
    public int EmberParticleCount { get; set; }
    public string EmberColor { get; set; } = null!;
    public float EmberLifetime { get; set; }
    public float EmberSpawnRadius { get; set; }
    public float EmberSpawnHeight { get; set; }
    public float EmberMinSpawnHeight { get; set; }
    public float EmberVelocityMin { get; set; }
    public float EmberVelocityMax { get; set; }
    public float EmberSpread { get; set; }
    public float EmberGravity { get; set; }
    public float EmberTurbulence { get; set; }
    public float EmberSizeMin { get; set; }
    public float EmberSizeMax { get; set; }
    public float EmberGlow { get; set; }

    // Lightning
    public string LightningColor { get; set; } = null!;
    public float LightningVolumetricFogEnergy { get; set; }

    // Light pollution
    public bool LightPollutionEnabled { get; set; }
    public float LightPollutionIntensity { get; set; }
    public string LightPollutionWarmColor { get; set; } = null!;
    public string LightPollutionCoolColor { get; set; } = null!;
    public float LightPollutionHeight { get; set; }
    public float LightPollutionFadeUp { get; set; }
    public float LightPollutionFadeDown { get; set; }
    public float LightPollutionEdgeNoise { get; set; }

    // Distant city silhouettes
    public bool SilhouettesEnabled { get; set; }
    public float SilhouetteInnerRadius { get; set; }
    public float SilhouetteOuterRadius { get; set; }
    public int SilhouetteBuildingCount { get; set; }
    public float SilhouetteMinHeight { get; set; }
    public float SilhouetteMaxHeight { get; set; }
    public float SilhouetteMinWidth { get; set; }
    public float SilhouetteMaxWidth { get; set; }
    public string SilhouetteColor { get; set; } = null!;

    // Silhouette variation multipliers
    public float SilhouetteInnerEdgeHeightBoost { get; set; }
    public float SilhouetteTowerChance { get; set; }
    public float SilhouetteTowerHeightMin { get; set; }
    public float SilhouetteTowerHeightMax { get; set; }
    public float SilhouetteTowerWidthMult { get; set; }
    public float SilhouetteSpireChance { get; set; }
    public float SilhouetteSpireHeightMult { get; set; }
    public float SilhouetteSpireWidthMult { get; set; }
}

/// <summary>
/// Weather configuration for rain and atmosphere effects.
/// </summary>
public sealed class WeatherConfig
{
    // Rain particles
    [UserConfigurable(SettingsCategory.Gameplay, SettingsControlType.Toggle, Order = 20)]
    [SettingsDisplay(DisplayName = "Rain Effects", Description = "Enable rain and splash particle effects")]
    public bool RainEnabled { get; set; }

    [UserConfigurable(SettingsCategory.Gameplay, SettingsControlType.Slider, Order = 21)]
    [SettingsRange(0.0, 5.0, Step = 0.1)]
    [SettingsDisplay(DisplayName = "Rain Intensity", ValueFormat = "{0:F1}", Description = "Amount of rain particles and visual intensity")]
    public float RainIntensity { get; set; }
    public int RainParticleCount { get; set; }
    public float RainCoverageRadius { get; set; }
    public float RainFallSpeed { get; set; }
    public float RainSpawnHeight { get; set; }
    public string RainColor { get; set; } = null!;
    public float RainAlpha { get; set; }
    public float RainDropWidth { get; set; }
    public float RainDropHeight { get; set; }
    public float RainSpreadAngle { get; set; }
    public float RainVelocityVariation { get; set; }

    // Splash particles
    public bool SplashEnabled { get; set; }
    public int SplashParticleCount { get; set; }
    public float SplashLifetime { get; set; }
    public float SplashVelocityMin { get; set; }
    public float SplashVelocityMax { get; set; }
    public float SplashSpreadAngle { get; set; }
    public string SplashColor { get; set; } = null!;
    public float SplashAlpha { get; set; }
    public float SplashSize { get; set; }
    public float SplashGravity { get; set; }

    // Ground mist - swirling vapor at street level
    [UserConfigurable(SettingsCategory.Graphics, SettingsControlType.Toggle, Order = 52)]
    [SettingsDisplay(DisplayName = "Ground Mist", Description = "Low-lying fog particles at street level")]
    public bool GroundMistEnabled { get; set; }
    public int GroundMistParticleCount { get; set; }
    public float GroundMistCoverageRadius { get; set; }
    public float GroundMistHeight { get; set; }
    public string GroundMistColor { get; set; } = null!;
}
