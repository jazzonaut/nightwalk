using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Godot.Rendering.Constants;

namespace Nightwalk.Godot.Rendering.Materials;

/// <summary>
/// Factory for creating and caching materials used in chunk rendering.
/// Centralizes material creation logic previously scattered in ChunkRenderer.
/// </summary>
public sealed class MaterialFactory
{
    private readonly Dictionary<string, StandardMaterial3D> _neonMaterials = new();

    // Cached shader templates
    private Shader? _kanjiShader;
    private Shader? _billboardShader;
    private ShaderMaterial? _kanjiMaterialTemplate;
    private ShaderMaterial? _billboardMaterialTemplate;

    /// <summary>
    /// Creates a standard material from configuration.
    /// </summary>
    public static StandardMaterial3D CreateMaterial(MaterialConfig config)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml(config.Color),
            Roughness = config.Roughness,
            Metallic = config.Metallic
        };

        if (!string.IsNullOrEmpty(config.Emission) && config.EmissionStrength > 0)
        {
            material.EmissionEnabled = true;
            material.Emission = Color.FromHtml(config.Emission);
            material.EmissionEnergyMultiplier = config.EmissionStrength;
        }

        return material;
    }

    /// <summary>
    /// Creates the unlit window material with appropriate settings.
    /// </summary>
    public static StandardMaterial3D CreateWindowUnlitMaterial(MaterialConfig config)
    {
        var material = CreateMaterial(config);
        material.MetallicSpecular = 0.0f;
        material.Roughness = Mathf.Max(material.Roughness, RenderingConstants.UnlitWindowMinRoughness);
        return material;
    }

    /// <summary>
    /// Creates a shader-based lit window material with procedural variation.
    /// </summary>
    public static ShaderMaterial CreateWindowLitMaterial(MaterialConfig config, WindowConfig windows)
    {
        var shader = GD.Load<Shader>("res://shaders/window_lit.gdshader");
        var material = new ShaderMaterial { Shader = shader };

        material.SetShaderParameter("albedo", Color.FromHtml(config.Color));
        material.SetShaderParameter("roughness", config.Roughness);
        material.SetShaderParameter("metallic", config.Metallic);

        if (!string.IsNullOrEmpty(config.Emission))
        {
            material.SetShaderParameter("emission_color", Color.FromHtml(config.Emission));
        }
        material.SetShaderParameter("emission_strength", config.EmissionStrength);

        var v = windows.Variation;
        material.SetShaderParameter("warm_color", Color.FromHtml(v.WarmColor));
        material.SetShaderParameter("cool_color", Color.FromHtml(v.CoolColor));
        material.SetShaderParameter("accent_magenta", Color.FromHtml(v.AccentMagenta));
        material.SetShaderParameter("accent_cyan", Color.FromHtml(v.AccentCyan));
        material.SetShaderParameter("accent_chance", v.AccentChance);
        material.SetShaderParameter("curtain_chance", v.CurtainChance);
        material.SetShaderParameter("blinds_half_chance", v.BlindsHalfChance);
        material.SetShaderParameter("blinds_closed_chance", v.BlindsClosedChance);

        return material;
    }

    /// <summary>
    /// Creates a wet surface shader material for roads or sidewalks.
    /// Unifies CreateWetSurfaceMaterial and CreateWetSidewalkMaterial.
    /// </summary>
    public static ShaderMaterial CreateWetSurfaceMaterial(
        string shaderPath,
        MaterialConfig baseConfig,
        WetSurfaceConfig wetConfig)
    {
        var shader = GD.Load<Shader>(shaderPath);
        var material = new ShaderMaterial { Shader = shader };

        material.SetShaderParameter("albedo", Color.FromHtml(baseConfig.Color));
        material.SetShaderParameter("base_roughness", baseConfig.Roughness);

        material.SetShaderParameter("wetness", wetConfig.Wetness);
        material.SetShaderParameter("wet_roughness", wetConfig.WetRoughness);
        material.SetShaderParameter("wet_darkening", wetConfig.WetDarkening);

        material.SetShaderParameter("texture_variation", wetConfig.TextureVariation);
        material.SetShaderParameter("texture_scale", wetConfig.TextureScale);

        material.SetShaderParameter("puddles_enabled", wetConfig.PuddlesEnabled);
        material.SetShaderParameter("puddle_scale", wetConfig.PuddleScale);
        material.SetShaderParameter("puddle_threshold", wetConfig.PuddleThreshold);
        material.SetShaderParameter("puddle_edge_softness", wetConfig.PuddleEdgeSoftness);

        material.SetShaderParameter("ripples_enabled", wetConfig.RipplesEnabled);
        material.SetShaderParameter("ripple_speed", wetConfig.RippleSpeed);
        material.SetShaderParameter("ripple_scale", wetConfig.RippleScale);
        material.SetShaderParameter("ripple_strength", wetConfig.RippleStrength);
        material.SetShaderParameter("time_scale", wetConfig.TimeScale);

        material.SetShaderParameter("wetness_noise_scale", wetConfig.WetnessNoiseScale);
        material.SetShaderParameter("wetness_noise_strength", wetConfig.WetnessNoiseStrength);

        material.SetShaderParameter("puddle_noise", TextureFactory.CreatePuddleNoiseTexture());
        material.SetShaderParameter("ripple_normal", TextureFactory.CreateRippleNormalTexture());

        return material;
    }

    /// <summary>
    /// Creates a wet road material.
    /// </summary>
    public static ShaderMaterial CreateWetRoadMaterial(MaterialConfig baseConfig, WetSurfaceConfig wetConfig)
    {
        return CreateWetSurfaceMaterial("res://shaders/wet_surface.gdshader", baseConfig, wetConfig);
    }

    /// <summary>
    /// Creates a wet sidewalk material.
    /// </summary>
    public static ShaderMaterial CreateWetSidewalkMaterial(MaterialConfig baseConfig, WetSurfaceConfig wetConfig)
    {
        return CreateWetSurfaceMaterial("res://shaders/wet_sidewalk.gdshader", baseConfig, wetConfig);
    }

    /// <summary>
    /// Creates a weathered facade shader material with procedural dirt, streaks, and rust.
    /// </summary>
    public static ShaderMaterial CreateWeatheredFacadeMaterial(
        MaterialConfig baseConfig,
        FacadeWeatheringConfig weathering,
        FacadeConfig facades)
    {
        var shader = GD.Load<Shader>("res://shaders/weathered_facade.gdshader");
        var material = new ShaderMaterial { Shader = shader };

        // Base surface properties
        material.SetShaderParameter("albedo", Color.FromHtml(baseConfig.Color));
        material.SetShaderParameter("base_roughness", baseConfig.Roughness);
        material.SetShaderParameter("metallic", baseConfig.Metallic);

        // Dirt/grime parameters
        material.SetShaderParameter("dirt_intensity", weathering.DirtIntensity);
        material.SetShaderParameter("dirt_height_falloff", weathering.DirtHeightFalloff);
        material.SetShaderParameter("dirt_corner_intensity", weathering.DirtCornerIntensity);
        material.SetShaderParameter("dirt_color", Color.FromHtml(weathering.DirtColor));

        // Water damage parameters
        material.SetShaderParameter("water_streak_intensity", weathering.WaterStreakIntensity);
        material.SetShaderParameter("water_streak_scale", weathering.WaterStreakScale);
        material.SetShaderParameter("water_streak_sparsity", weathering.WaterStreakSparsity);
        material.SetShaderParameter("water_streak_strength", weathering.WaterStreakStrength);
        material.SetShaderParameter("water_stain_color", Color.FromHtml(weathering.WaterStainColor));

        // Rust parameters
        material.SetShaderParameter("rust_intensity", weathering.RustIntensity);
        material.SetShaderParameter("rust_color", Color.FromHtml(weathering.RustColor));
        material.SetShaderParameter("rust_drip_length", weathering.RustDripLength);
        material.SetShaderParameter("rust_chance", weathering.RustChance);
        material.SetShaderParameter("rust_strength", weathering.RustStrength);

        // Roughness variation
        material.SetShaderParameter("roughness_variation", weathering.RoughnessVariation);
        material.SetShaderParameter("worn_roughness_boost", weathering.WornRoughnessBoost);

        // Floor heights for rust pattern alignment
        material.SetShaderParameter("floor_height", facades.FloorHeight);
        material.SetShaderParameter("ground_floor_height", facades.GroundFloorHeight);

        // Noise textures
        material.SetShaderParameter("weathering_noise", TextureFactory.CreateWeatheringNoiseTexture(weathering.NoiseScaleLarge));
        material.SetShaderParameter("detail_noise", TextureFactory.CreateDetailNoiseTexture(weathering.NoiseScaleDetail));

        return material;
    }

    /// <summary>
    /// Creates the lamp pole material with appropriate dark metallic appearance.
    /// </summary>
    public static StandardMaterial3D CreateLampPoleMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = RenderingConstants.LampPoleAlbedoColor,
            Roughness = RenderingConstants.LampPoleMaterialRoughness,
            Metallic = RenderingConstants.LampPoleMaterialMetallic
        };
    }

    /// <summary>
    /// Creates the lamp ring material with emission.
    /// </summary>
    public static StandardMaterial3D CreateLampRingMaterial(Color lightColor)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = lightColor,
            EmissionEnabled = true,
            Emission = lightColor,
            EmissionEnergyMultiplier = RenderingConstants.LampRingEmissionMultiplier,
            Roughness = RenderingConstants.LampRingMaterialRoughness,
            Metallic = RenderingConstants.LampRingMaterialMetallic
        };
    }

    /// <summary>
    /// Gets or creates a cached neon material by color and intensity.
    /// </summary>
    public StandardMaterial3D GetOrCreateNeonMaterial(string color, float intensity)
    {
        var key = $"{color}_{intensity:F1}";

        if (_neonMaterials.TryGetValue(key, out var existing))
            return existing;

        var parsedColor = Color.FromHtml(color);
        var material = new StandardMaterial3D
        {
            AlbedoColor = parsedColor,
            EmissionEnabled = true,
            Emission = parsedColor,
            EmissionEnergyMultiplier = intensity,
            Roughness = RenderingConstants.NeonMaterialRoughness,
            Metallic = 0f
        };

        _neonMaterials[key] = material;
        return material;
    }

    /// <summary>
    /// Gets the cached kanji shader material template, creating if needed.
    /// Sets shared (non-instance) parameters from RenderingConstants.
    /// </summary>
    public ShaderMaterial GetKanjiMaterialTemplate()
    {
        if (_kanjiMaterialTemplate != null)
            return _kanjiMaterialTemplate;

        _kanjiShader ??= GD.Load<Shader>("res://shaders/kanji_sign.gdshader");
        _kanjiMaterialTemplate = new ShaderMaterial { Shader = _kanjiShader };

        // Set shared parameters explicitly (instance uniforms are set per-mesh)
        _kanjiMaterialTemplate.SetShaderParameter("vertical_text", true);
        _kanjiMaterialTemplate.SetShaderParameter("flicker_enabled", true);

        return _kanjiMaterialTemplate;
    }

    /// <summary>
    /// Gets the cached billboard shader material template, creating if needed.
    /// Sets shared (non-instance) parameters from RenderingConstants.
    /// </summary>
    public ShaderMaterial GetBillboardMaterialTemplate()
    {
        if (_billboardMaterialTemplate != null)
            return _billboardMaterialTemplate;

        _billboardShader ??= GD.Load<Shader>("res://shaders/billboard_sign.gdshader");
        _billboardMaterialTemplate = new ShaderMaterial { Shader = _billboardShader };

        // Set shared parameters explicitly (instance uniforms are set per-mesh)
        _billboardMaterialTemplate.SetShaderParameter("frame_color", RenderingConstants.BillboardFrameColor);
        _billboardMaterialTemplate.SetShaderParameter("frame_width", RenderingConstants.BillboardFrameWidth);
        _billboardMaterialTemplate.SetShaderParameter("has_frame", true);
        _billboardMaterialTemplate.SetShaderParameter("animate", true);
        _billboardMaterialTemplate.SetShaderParameter("scroll_speed", RenderingConstants.BillboardScrollSpeed);

        return _billboardMaterialTemplate;
    }

    /// <summary>
    /// Clears all cached neon materials.
    /// Note: Materials are Godot RefCounted resources - they will be freed
    /// automatically when no longer referenced by any nodes.
    /// </summary>
    public void ClearNeonMaterialCache()
    {
        // Don't dispose - materials may still be referenced by nodes queued for freeing.
        // Godot's RefCounted system handles cleanup when references reach zero.
        _neonMaterials.Clear();
    }

    /// <summary>
    /// Clears cached shader templates.
    /// Note: Templates are Godot RefCounted resources - they will be freed
    /// automatically when no longer referenced.
    /// </summary>
    public void DisposeTemplates()
    {
        // Don't dispose - just clear references. Godot handles RefCounted cleanup.
        _kanjiMaterialTemplate = null;
        _billboardMaterialTemplate = null;
        _kanjiShader = null;
        _billboardShader = null;
    }
}
