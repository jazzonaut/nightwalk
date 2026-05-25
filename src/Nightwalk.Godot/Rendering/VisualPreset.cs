using Godot;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Rendering;

/// <summary>
/// Defines a visual preset with all configurable rendering parameters.
/// </summary>
public sealed class VisualPreset
{
    public string Name { get; init; } = "";

    // Post-processing: Color Grading
    public Color ShadowTint { get; init; } = new(0.85f, 0.9f, 1.0f);
    public Color HighlightTint { get; init; } = new(1.0f, 0.95f, 0.85f);
    public float ColorGradingStrength { get; init; } = 0.25f;
    public float Contrast { get; init; } = 1.05f;
    public float Saturation { get; init; } = 1.08f;

    // Post-processing: Vignette
    public float VignetteIntensity { get; init; } = 0.35f;
    public float VignetteSoftness { get; init; } = 0.45f;
    public Color VignetteColor { get; init; } = new(0f, 0f, 0.03f);

    // Post-processing: Film Grain
    public float FilmGrainIntensity { get; init; } = 0.06f;

    // Post-processing: Chromatic Aberration
    public float ChromaticStrength { get; init; } = 0.002f;

    // Environment: Fog
    public float FogDensity { get; init; } = 0.006f;
    public Color FogColor { get; init; } = new(0.1f, 0.14f, 0.27f);
    public float VolumetricFogDensity { get; init; } = 0.008f;

    // Environment: Bloom
    public float BloomIntensity { get; init; } = 2.0f;
    public float BloomThreshold { get; init; } = 0.6f;

    // Environment: Ambient
    public Color AmbientColor { get; init; } = new(0.1f, 0.15f, 0.21f);
    public float AmbientEnergy { get; init; } = 0.4f;

    // Weather
    public float RainIntensity { get; init; } = 1.0f;

    /// <summary>
    /// Creates the default preset from config values.
    /// </summary>
    public static VisualPreset FromConfig(GraphicsConfig gfx, WeatherConfig weather)
    {
        return new VisualPreset
        {
            Name = "Default",
            ShadowTint = Color.FromHtml(gfx.ColorGradingShadowTint),
            HighlightTint = Color.FromHtml(gfx.ColorGradingHighlightTint),
            ColorGradingStrength = gfx.ColorGradingStrength,
            Contrast = gfx.ColorGradingContrast,
            Saturation = gfx.ColorGradingSaturation,
            VignetteIntensity = gfx.VignetteIntensity,
            VignetteSoftness = gfx.VignetteSoftness,
            VignetteColor = Color.FromHtml(gfx.VignetteColor),
            FilmGrainIntensity = gfx.FilmGrainIntensity,
            ChromaticStrength = gfx.ChromaticAberrationStrength,
            FogDensity = gfx.FogDensity,
            FogColor = Color.FromHtml(gfx.FogColor),
            VolumetricFogDensity = gfx.VolumetricFogDensity,
            BloomIntensity = gfx.BloomIntensity,
            BloomThreshold = gfx.BloomThreshold,
            AmbientColor = Color.FromHtml(gfx.AmbientColor),
            AmbientEnergy = gfx.AmbientEnergy,
            RainIntensity = weather.RainIntensity
        };
    }

    /// <summary>
    /// Initializes the default preset (index 0) from config values.
    /// Call this at startup before using presets.
    /// </summary>
    public static void InitializeDefault(GraphicsConfig gfx, WeatherConfig weather)
    {
        All[0] = FromConfig(gfx, weather);
    }

    /// <summary>
    /// All available presets indexed 0-8 (keys 1-9).
    /// Index 0 (Default) is initialized from config at runtime via InitializeDefault().
    /// </summary>
    public static VisualPreset[] All =
    [
        // Default - placeholder, replaced at runtime by InitializeDefault()
        new() { Name = "Default" },

        // Clean - Minimal effects, clear visibility
        new()
        {
            Name = "Clean",
            ShadowTint = new Color(1.0f, 1.0f, 1.0f),
            HighlightTint = new Color(1.0f, 1.0f, 1.0f),
            ColorGradingStrength = 0.0f,
            Contrast = 1.0f,
            Saturation = 1.0f,
            VignetteIntensity = 0.0f,
            VignetteSoftness = 0.5f,
            VignetteColor = new Color(0f, 0f, 0f),
            FilmGrainIntensity = 0.0f,
            ChromaticStrength = 0.0f,
            FogDensity = 0.003f,
            FogColor = new Color(0.15f, 0.18f, 0.22f),
            VolumetricFogDensity = 0.004f,
            BloomIntensity = 1.0f,
            BloomThreshold = 0.8f,
            AmbientColor = new Color(0.2f, 0.22f, 0.25f),
            AmbientEnergy = 0.5f,
            RainIntensity = 0.0f
        },

        // Blade Runner - Blue shadows, orange highlights, foggy
        new()
        {
            Name = "Blade Runner",
            ShadowTint = new Color(0.6f, 0.75f, 1.0f),
            HighlightTint = new Color(1.0f, 0.85f, 0.6f),
            ColorGradingStrength = 0.45f,
            Contrast = 1.18f,
            Saturation = 1.15f,
            VignetteIntensity = 0.35f,
            VignetteSoftness = 0.45f,
            VignetteColor = new Color(0.02f, 0.01f, 0.05f),
            FilmGrainIntensity = 0.05f,
            ChromaticStrength = 0.002f,
            FogDensity = 0.012f,
            FogColor = new Color(0.08f, 0.12f, 0.2f),
            VolumetricFogDensity = 0.015f,
            BloomIntensity = 2.2f,
            BloomThreshold = 0.55f,
            AmbientColor = new Color(0.1f, 0.15f, 0.25f),
            AmbientEnergy = 0.4f,
            RainIntensity = 1.0f
        },

        // Toxic - Green tint, acid rain aesthetic
        new()
        {
            Name = "Nightfall",
            ShadowTint = new Color(0.25f, 0.48f, 0.3f),
            HighlightTint = new Color(0.65f, 1.0f, 0.6f),
            ColorGradingStrength = 0.6f,
            Contrast = 1.22f,
            Saturation = 1.2f,
            VignetteIntensity = 0.45f,
            VignetteSoftness = 0.32f,
            VignetteColor = new Color(0.02f, 0.06f, 0.02f),
            FilmGrainIntensity = 0.08f,
            ChromaticStrength = 0.004f,
            FogDensity = 0.012f,
            FogColor = new Color(0.06f, 0.18f, 0.08f),
            VolumetricFogDensity = 0.015f,
            BloomIntensity = 2.8f,
            BloomThreshold = 0.45f,
            AmbientColor = new Color(0.08f, 0.2f, 0.1f),
            AmbientEnergy = 0.5f,
            RainIntensity = 2.2f
        },

        // Burning Sky - Dramatic warm sunset tones
        new()
        {
            Name = "Burning Sky",
            ShadowTint = new Color(0.65f, 0.42f, 0.35f),
            HighlightTint = new Color(1.0f, 0.8f, 0.55f),
            ColorGradingStrength = 0.42f,
            Contrast = 1.13f,
            Saturation = 1.2f,
            VignetteIntensity = 0.32f,
            VignetteSoftness = 0.43f,
            VignetteColor = new Color(0.1f, 0.02f, 0f),
            FilmGrainIntensity = 0.05f,
            ChromaticStrength = 0.002f,
            FogDensity = 0.007f,
            FogColor = new Color(0.32f, 0.18f, 0.1f),
            VolumetricFogDensity = 0.01f,
            BloomIntensity = 2.4f,
            BloomThreshold = 0.5f,
            AmbientColor = new Color(0.3f, 0.2f, 0.12f),
            AmbientEnergy = 0.58f,
            RainIntensity = 2.0f
        },

        // Synthwave - Orange/purple gradient, vaporwave aesthetic
        new()
        {
            Name = "Synthwave",
            ShadowTint = new Color(0.3f, 0.12f, 0.85f),
            HighlightTint = new Color(1.0f, 0.55f, 0.45f),
            ColorGradingStrength = 0.6f,
            Contrast = 1.14f,
            Saturation = 1.45f,
            VignetteIntensity = 0.42f,
            VignetteSoftness = 0.38f,
            VignetteColor = new Color(0.18f, 0f, 0.14f),
            FilmGrainIntensity = 0.05f,
            ChromaticStrength = 0.004f,
            FogDensity = 0.007f,
            FogColor = new Color(0.36f, 0.08f, 0.3f),
            VolumetricFogDensity = 0.009f,
            BloomIntensity = 3.1f,
            BloomThreshold = 0.45f,
            AmbientColor = new Color(0.28f, 0.08f, 0.22f),
            AmbientEnergy = 0.5f,
            RainIntensity = 2.0f
        },

        // Cinematic - Film-like warm tones, heavy vignette
        new()
        {
            Name = "Cinematic",
            ShadowTint = new Color(0.82f, 0.72f, 0.62f),
            HighlightTint = new Color(1.0f, 0.97f, 0.88f),
            ColorGradingStrength = 0.4f,
            Contrast = 1.18f,
            Saturation = 0.82f,
            VignetteIntensity = 0.6f,
            VignetteSoftness = 0.28f,
            VignetteColor = new Color(0f, 0f, 0f),
            FilmGrainIntensity = 0.1f,
            ChromaticStrength = 0.002f,
            FogDensity = 0.006f,
            FogColor = new Color(0.09f, 0.08f, 0.06f),
            VolumetricFogDensity = 0.008f,
            BloomIntensity = 1.5f,
            BloomThreshold = 0.8f,
            AmbientColor = new Color(0.14f, 0.11f, 0.09f),
            AmbientEnergy = 0.38f,
            RainIntensity = 2.0f
        },

        // Downpour - Heavy rain, low visibility, stormy
        new()
        {
            Name = "Downpour",
            ShadowTint = new Color(0.6f, 0.7f, 0.85f),
            HighlightTint = new Color(0.85f, 0.92f, 1.0f),
            ColorGradingStrength = 0.35f,
            Contrast = 0.9f,
            Saturation = 0.65f,
            VignetteIntensity = 0.5f,
            VignetteSoftness = 0.48f,
            VignetteColor = new Color(0.02f, 0.04f, 0.06f),
            FilmGrainIntensity = 0.1f,
            ChromaticStrength = 0.0025f,
            FogDensity = 0.02f,
            FogColor = new Color(0.1f, 0.12f, 0.16f),
            VolumetricFogDensity = 0.025f,
            BloomIntensity = 1.6f,
            BloomThreshold = 0.75f,
            AmbientColor = new Color(0.08f, 0.1f, 0.13f),
            AmbientEnergy = 0.25f,
            RainIntensity = 2.4f
        },

        // Noir - Black and white, high contrast, film noir style
        new()
        {
            Name = "Noir",
            ShadowTint = new Color(0.85f, 0.88f, 1.0f),
            HighlightTint = new Color(1.0f, 1.0f, 0.92f),
            ColorGradingStrength = 0.45f,
            Contrast = 1.35f,
            Saturation = 0.38f,
            VignetteIntensity = 0.7f,
            VignetteSoftness = 0.3f,
            VignetteColor = new Color(0f, 0f, 0f),
            FilmGrainIntensity = 0.14f,
            ChromaticStrength = 0.0012f,
            FogDensity = 0.009f,
            FogColor = new Color(0.06f, 0.06f, 0.1f),
            VolumetricFogDensity = 0.012f,
            BloomIntensity = 1.2f,
            BloomThreshold = 0.85f,
            AmbientColor = new Color(0.08f, 0.08f, 0.1f),
            AmbientEnergy = 0.3f,
            RainIntensity = 2.0f
        }
    ];
}
