using System;
using Nightwalk.Core.Config;

namespace Nightwalk.Core.Resonance.Seeker;

/// <summary>
/// Handles Seeker movement with Perlin noise-based organic wandering.
/// </summary>
public sealed class SeekerMovement
{
    private readonly SeekerConfig _config;
    private readonly Random _random = new();

    // Simplex noise parameters
    private readonly int[] _permutation;

    public SeekerMovement(SeekerConfig config)
    {
        _config = config;
        _permutation = GeneratePermutation();
    }

    /// <summary>
    /// Gets a wander offset using Perlin noise for organic movement.
    /// </summary>
    public (float x, float z) GetWanderOffset(SeekerInstance seeker, float time)
    {
        // Use seeker's unique noise offset for variation
        float sampleX = time * _config.WanderFrequency + seeker.NoiseOffset;
        float sampleZ = time * _config.WanderFrequency + seeker.NoiseOffset + 100f;

        float noiseX = Noise2D(sampleX, 0f);
        float noiseZ = Noise2D(sampleZ, 0f);

        return (noiseX * _config.WanderAmplitude, noiseZ * _config.WanderAmplitude);
    }

    /// <summary>
    /// Updates seeker velocity and position using smooth acceleration.
    /// </summary>
    public void UpdateMovement(
        SeekerInstance seeker,
        float targetX, float targetY, float targetZ,
        float speed, float delta, float time,
        float wanderFade = 1f)
    {
        float dx = targetX - seeker.X;
        float dy = targetY - seeker.Y;
        float dz = targetZ - seeker.Z;
        float distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        if (distance < 0.1f)
        {
            // Already at target, stop
            seeker.VelocityX = Lerp(seeker.VelocityX, 0f, delta * _config.Acceleration);
            seeker.VelocityY = Lerp(seeker.VelocityY, 0f, delta * _config.Acceleration);
            seeker.VelocityZ = Lerp(seeker.VelocityZ, 0f, delta * _config.Acceleration);
            return;
        }

        // Normalized direction
        float dirX = dx / distance;
        float dirY = dy / distance;
        float dirZ = dz / distance;

        // Get wander offset (fades as we approach target)
        var (wanderX, wanderZ) = GetWanderOffset(seeker, time);
        float proximityFade = MathF.Min(1f, distance / _config.ProximityFadeDistance);
        wanderX *= proximityFade * wanderFade;
        wanderZ *= proximityFade * wanderFade;

        // Target velocity toward destination plus wander plus collision avoidance
        float targetVelX = dirX * speed + wanderX + seeker.AvoidanceX;
        float targetVelY = dirY * speed + seeker.AvoidanceY;
        float targetVelZ = dirZ * speed + wanderZ + seeker.AvoidanceZ;

        // Smooth acceleration
        seeker.VelocityX = Lerp(seeker.VelocityX, targetVelX, delta * _config.Acceleration);
        seeker.VelocityY = Lerp(seeker.VelocityY, targetVelY, delta * _config.Acceleration);
        seeker.VelocityZ = Lerp(seeker.VelocityZ, targetVelZ, delta * _config.Acceleration);

        // Update position
        seeker.X += seeker.VelocityX * delta;
        seeker.Y += seeker.VelocityY * delta;
        seeker.Z += seeker.VelocityZ * delta;
    }

    /// <summary>
    /// Generates a unique noise offset for a new seeker.
    /// </summary>
    public float GenerateNoiseOffset()
    {
        return (float)_random.NextDouble() * 1000f;
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * MathF.Min(1f, t);
    }

    #region Simplex Noise Implementation

    private int[] GeneratePermutation()
    {
        var perm = new int[512];
        var p = new int[256];

        for (int i = 0; i < 256; i++)
            p[i] = i;

        // Fisher-Yates shuffle
        for (int i = 255; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (int i = 0; i < 512; i++)
            perm[i] = p[i & 255];

        return perm;
    }

    private float Noise2D(float x, float y)
    {
        // 2D Perlin noise
        int xi = (int)MathF.Floor(x) & 255;
        int yi = (int)MathF.Floor(y) & 255;

        float xf = x - MathF.Floor(x);
        float yf = y - MathF.Floor(y);

        float u = Fade(xf);
        float v = Fade(yf);

        int aa = _permutation[_permutation[xi] + yi];
        int ab = _permutation[_permutation[xi] + yi + 1];
        int ba = _permutation[_permutation[xi + 1] + yi];
        int bb = _permutation[_permutation[xi + 1] + yi + 1];

        float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);

        return Lerp(x1, x2, v);
    }

    private static float Fade(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    private static float Grad(int hash, float x, float y)
    {
        int h = hash & 3;
        return h switch
        {
            0 => x + y,
            1 => -x + y,
            2 => x - y,
            _ => -x - y
        };
    }

    #endregion
}
