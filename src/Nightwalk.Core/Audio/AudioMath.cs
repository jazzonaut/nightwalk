using System;

namespace Nightwalk.Core.Audio;

/// <summary>
/// Utility methods for audio calculations.
/// </summary>
public static class AudioMath
{
    private const float SilenceThreshold = 0.0001f;
    private const float SilenceDb = -80f;

    /// <summary>
    /// Converts a linear volume value (0-1) to decibels.
    /// </summary>
    /// <param name="linear">Linear volume where 0 = silent, 1 = full volume.</param>
    /// <returns>Volume in decibels (-80dB for silence, 0dB for full volume).</returns>
    public static float LinearToDb(float linear)
    {
        return linear > SilenceThreshold
            ? 20f * MathF.Log10(linear)
            : SilenceDb;
    }

    /// <summary>
    /// Converts a decibel value to linear volume (0-1).
    /// </summary>
    /// <param name="db">Volume in decibels.</param>
    /// <returns>Linear volume where 0 = silent, 1 = full volume.</returns>
    public static float DbToLinear(float db)
    {
        return db <= SilenceDb ? 0f : MathF.Pow(10f, db / 20f);
    }
}
