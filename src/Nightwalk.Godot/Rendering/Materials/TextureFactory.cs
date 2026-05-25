using System.Collections.Generic;
using Godot;
using Nightwalk.Godot.Rendering.Constants;

namespace Nightwalk.Godot.Rendering.Materials;

/// <summary>
/// Factory for creating procedural noise textures used in rendering.
/// Centralizes texture generation that was previously duplicated.
/// Caches textures by frequency to avoid duplicates across materials.
/// </summary>
public static class TextureFactory
{
    // Cache noise textures to avoid creating duplicates across materials
    private static readonly Dictionary<float, NoiseTexture2D> WeatheringNoiseCache = new();
    private static readonly Dictionary<float, NoiseTexture2D> DetailNoiseCache = new();
    private static NoiseTexture2D? _puddleNoiseCache;
    private static NoiseTexture2D? _rippleNormalCache;
    /// <summary>
    /// Gets or creates a cached puddle noise texture for wet surface effects.
    /// </summary>
    public static NoiseTexture2D CreatePuddleNoiseTexture()
    {
        if (_puddleNoiseCache != null)
            return _puddleNoiseCache;

        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = RenderingConstants.PuddleNoiseFrequency,
            FractalOctaves = RenderingConstants.PuddleNoiseOctaves,
            FractalLacunarity = RenderingConstants.PuddleNoiseLacunarity,
            FractalGain = RenderingConstants.PuddleNoiseGain
        };

        _puddleNoiseCache = new NoiseTexture2D
        {
            Width = RenderingConstants.NoiseTextureSize,
            Height = RenderingConstants.NoiseTextureSize,
            Seamless = true,
            Noise = noise
        };

        return _puddleNoiseCache;
    }

    /// <summary>
    /// Gets or creates a cached ripple normal texture for animated water effects.
    /// </summary>
    public static NoiseTexture2D CreateRippleNormalTexture()
    {
        if (_rippleNormalCache != null)
            return _rippleNormalCache;

        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = RenderingConstants.RippleNoiseFrequency,
            FractalOctaves = RenderingConstants.RippleNoiseOctaves
        };

        _rippleNormalCache = new NoiseTexture2D
        {
            Width = RenderingConstants.NoiseTextureSize,
            Height = RenderingConstants.NoiseTextureSize,
            Seamless = true,
            AsNormalMap = true,
            BumpStrength = RenderingConstants.RippleBumpStrength,
            Noise = noise
        };

        return _rippleNormalCache;
    }

    /// <summary>
    /// Gets or creates a cached weathering noise texture for facade dirt/grime effects.
    /// </summary>
    public static NoiseTexture2D CreateWeatheringNoiseTexture(float frequency)
    {
        if (WeatheringNoiseCache.TryGetValue(frequency, out var cached))
            return cached;

        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = frequency,
            FractalOctaves = 3,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f
        };

        var texture = new NoiseTexture2D
        {
            Width = RenderingConstants.NoiseTextureSize,
            Height = RenderingConstants.NoiseTextureSize,
            Seamless = true,
            Noise = noise
        };

        WeatheringNoiseCache[frequency] = texture;
        return texture;
    }

    /// <summary>
    /// Gets or creates a cached detail noise texture for facade weathering streaks and variation.
    /// </summary>
    public static NoiseTexture2D CreateDetailNoiseTexture(float frequency)
    {
        if (DetailNoiseCache.TryGetValue(frequency, out var cached))
            return cached;

        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = frequency,
            FractalOctaves = 2
        };

        var texture = new NoiseTexture2D
        {
            Width = RenderingConstants.NoiseTextureSize,
            Height = RenderingConstants.NoiseTextureSize,
            Seamless = true,
            Noise = noise
        };

        DetailNoiseCache[frequency] = texture;
        return texture;
    }
}
