using System;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Godot.Rendering;

/// <summary>
/// Manages visual preset switching, applying changes to post-processing, environment, and weather.
/// </summary>
public sealed class VisualPresetManager
{
    private readonly ShaderMaterial _postProcessMaterial;
    private readonly WorldEnvironment? _worldEnvironment;
    private readonly UserPreferencesService? _preferences;
    private RainSystem? _rainSystem;
    private Action<string>? _onPresetChanged;
    private int _currentPresetIndex;

    public VisualPresetManager(
        ShaderMaterial postProcessMaterial,
        WorldEnvironment? worldEnvironment,
        UserPreferencesService? preferences = null,
        int initialPresetIndex = 0)
    {
        _postProcessMaterial = postProcessMaterial;
        _worldEnvironment = worldEnvironment;
        _preferences = preferences;
        _currentPresetIndex = Math.Clamp(initialPresetIndex, 0, VisualPreset.All.Length - 1);

        // Apply initial preset (without saving to preferences since we're just loading)
        if (_currentPresetIndex != 0)
        {
            ApplyPreset(VisualPreset.All[_currentPresetIndex]);
        }
    }

    /// <summary>
    /// Sets the rain system reference for weather control.
    /// </summary>
    public void SetRainSystem(RainSystem rainSystem)
    {
        _rainSystem = rainSystem;
    }

    /// <summary>
    /// Sets a callback for preset changes (for HUD display).
    /// </summary>
    public void SetOnPresetChanged(Action<string> callback)
    {
        _onPresetChanged = callback;
    }

    /// <summary>
    /// Current preset index (0-8).
    /// </summary>
    public int CurrentPresetIndex => _currentPresetIndex;

    /// <summary>
    /// Current preset name.
    /// </summary>
    public string CurrentPresetName => VisualPreset.All[_currentPresetIndex].Name;

    /// <summary>
    /// Re-applies the current preset without saving to preferences.
    /// Call after hot reload to restore preset values that were overwritten by config.
    /// </summary>
    public void ReapplyCurrentPreset()
    {
        if (_currentPresetIndex == 0)
            return; // Default preset matches config, no need to reapply

        var preset = VisualPreset.All[_currentPresetIndex];
        ApplyPreset(preset);
        Log.Info(LogCategory.Config, $"Reapplied preset after hot reload: {preset.Name}");
    }

    /// <summary>
    /// Switches to the specified preset index (0-8 for keys 1-9).
    /// </summary>
    public void SetPreset(int index, bool saveToPreferences = true)
    {
        if (index < 0 || index >= VisualPreset.All.Length)
            return;

        _currentPresetIndex = index;
        var preset = VisualPreset.All[index];
        ApplyPreset(preset);
        _onPresetChanged?.Invoke(preset.Name);

        // Save to user preferences
        if (saveToPreferences && _preferences != null)
        {
            _preferences.SetPreference("core", "Graphics.VisualPresetIndex", index);
            _preferences.Save();
        }
    }

    private void ApplyPreset(VisualPreset preset)
    {
        ApplyPostProcessing(preset);
        ApplyEnvironment(preset);
        ApplyWeather(preset);
    }

    private void ApplyPostProcessing(VisualPreset preset)
    {
        // Color grading
        _postProcessMaterial.SetShaderParameter("shadow_tint", preset.ShadowTint);
        _postProcessMaterial.SetShaderParameter("highlight_tint", preset.HighlightTint);
        _postProcessMaterial.SetShaderParameter("color_grading_strength", preset.ColorGradingStrength);
        _postProcessMaterial.SetShaderParameter("contrast", preset.Contrast);
        _postProcessMaterial.SetShaderParameter("saturation", preset.Saturation);

        // Vignette
        _postProcessMaterial.SetShaderParameter("vignette_intensity", preset.VignetteIntensity);
        _postProcessMaterial.SetShaderParameter("vignette_softness", preset.VignetteSoftness);
        _postProcessMaterial.SetShaderParameter("vignette_color", preset.VignetteColor);

        // Film grain
        _postProcessMaterial.SetShaderParameter("film_grain_intensity", preset.FilmGrainIntensity);

        // Chromatic aberration
        _postProcessMaterial.SetShaderParameter("chromatic_aberration_strength", preset.ChromaticStrength);
    }

    private void ApplyEnvironment(VisualPreset preset)
    {
        if (_worldEnvironment?.Environment == null)
            return;

        var env = _worldEnvironment.Environment;

        // Fog
        env.FogDensity = preset.FogDensity;
        env.FogLightColor = preset.FogColor;
        env.VolumetricFogDensity = preset.VolumetricFogDensity;

        // Bloom
        env.GlowIntensity = preset.BloomIntensity;
        env.GlowHdrThreshold = preset.BloomThreshold;

        // Ambient
        env.AmbientLightColor = preset.AmbientColor;
        env.AmbientLightEnergy = preset.AmbientEnergy;
    }

    private void ApplyWeather(VisualPreset preset)
    {
        _rainSystem?.SetIntensity(preset.RainIntensity);
    }
}
