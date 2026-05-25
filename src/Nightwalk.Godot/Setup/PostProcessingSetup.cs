using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Godot.Rendering.Materials;

namespace Nightwalk.Godot.Setup;

/// <summary>
/// Result of post-processing setup.
/// </summary>
public sealed class PostProcessingResult
{
    public required CanvasLayer Layer { get; init; }
    public required ShaderMaterial Material { get; init; }

    /// <summary>
    /// Updates post-processing parameters from config. Call on hot reload.
    /// </summary>
    public void Configure(GraphicsConfig config)
    {
        PostProcessingSetup.UpdateMaterial(Material, config);
    }

    public void Cleanup()
    {
        Layer.QueueFree();
    }
}

/// <summary>
/// Configures post-processing effects (color grading, vignette, film grain, etc.)
/// </summary>
public static class PostProcessingSetup
{
    private const string ShaderPath = "res://shaders/post_processing.gdshader";

    /// <summary>
    /// Creates and configures post-processing effects.
    /// </summary>
    /// <param name="parent">The parent node to add the canvas layer to.</param>
    /// <param name="config">Graphics configuration.</param>
    /// <returns>The created post-processing layer and material.</returns>
    public static PostProcessingResult Configure(Node parent, GraphicsConfig config)
    {
        // Create CanvasLayer at high layer to render on top
        var layer = new CanvasLayer
        {
            Name = "PostProcessing",
            Layer = 100
        };
        parent.AddChild(layer);

        // Create full-screen ColorRect
        var rect = new ColorRect
        {
            Name = "PostProcessEffect",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Load and configure shader
        var shader = GD.Load<Shader>(ShaderPath);
        var material = new ShaderMaterial { Shader = shader };

        UpdateMaterial(material, config);

        rect.Material = material;
        layer.AddChild(rect);

        Log.Info(LogCategory.Config, "Post-processing effects initialized");

        return new PostProcessingResult
        {
            Layer = layer,
            Material = material
        };
    }

    /// <summary>
    /// Updates all shader parameters on the material from config.
    /// </summary>
    public static void UpdateMaterial(ShaderMaterial material, GraphicsConfig config)
    {
        ConfigureColorGrading(material, config);
        ConfigureVignette(material, config);
        ConfigureFilmGrain(material, config);
        ConfigureChromaticAberration(material, config);
    }

    private static void ConfigureColorGrading(ShaderMaterial material, GraphicsConfig gfx)
    {
        material.SetShaderParameter("color_grading_enabled", gfx.ColorGradingEnabled);
        material.SetShaderParameter("shadow_tint", Color.FromHtml(gfx.ColorGradingShadowTint));
        material.SetShaderParameter("highlight_tint", Color.FromHtml(gfx.ColorGradingHighlightTint));
        material.SetShaderParameter("color_grading_strength", gfx.ColorGradingStrength);
        material.SetShaderParameter("contrast", gfx.ColorGradingContrast);
        material.SetShaderParameter("saturation", gfx.ColorGradingSaturation);
    }

    private static void ConfigureVignette(ShaderMaterial material, GraphicsConfig gfx)
    {
        material.SetShaderParameter("vignette_enabled", gfx.VignetteEnabled);
        material.SetShaderParameter("vignette_intensity", gfx.VignetteIntensity);
        material.SetShaderParameter("vignette_softness", gfx.VignetteSoftness);
        material.SetShaderParameter("vignette_color", Color.FromHtml(gfx.VignetteColor));
    }

    private static void ConfigureFilmGrain(ShaderMaterial material, GraphicsConfig gfx)
    {
        material.SetShaderParameter("film_grain_enabled", gfx.FilmGrainEnabled);
        material.SetShaderParameter("film_grain_intensity", gfx.FilmGrainIntensity);
        material.SetShaderParameter("film_grain_speed", gfx.FilmGrainSpeed);
    }

    private static void ConfigureChromaticAberration(ShaderMaterial material, GraphicsConfig gfx)
    {
        material.SetShaderParameter("chromatic_aberration_enabled", gfx.ChromaticAberrationEnabled);
        material.SetShaderParameter("chromatic_aberration_strength", gfx.ChromaticAberrationStrength);
        material.SetShaderParameter("chromatic_aberration_falloff", gfx.ChromaticAberrationFalloff);
    }
}
