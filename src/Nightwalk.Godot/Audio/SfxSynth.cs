using System;
using Godot;

namespace Nightwalk.Godot.Audio;

/// <summary>
/// Procedural sound effect synthesizer for generating one-shot sounds.
/// Returns AudioStreamWav instances that can be played through the SfxManager.
/// </summary>
public static class SfxSynth
{
    private const int SampleRate = 44100;
    private const float TwoPi = MathF.PI * 2f;

    /// <summary>
    /// Generates a collectible pickup sound: digital blip with sub-bass pulse.
    /// Cyberpunk style - glitchy, resonant, satisfying.
    /// </summary>
    public static AudioStreamWav Collectible(Random? rng = null)
    {
        rng ??= Random.Shared;

        float duration = 0.75f + (float)rng.NextDouble() * 0.08f;
        int sampleCount = (int)(SampleRate * duration);
        var samples = new float[sampleCount];

        float subFreq = 52f + (float)rng.NextDouble() * 10f;
        float phaseSub = 0f;
        float tapPhase = 0f;
        float glowPhase = 0f;
        float prevNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;

            // Short low base
            phaseSub += TwoPi * subFreq / SampleRate;
            float sub = MathF.Sin(phaseSub) * 0.35f * ExpDecay(t, 0.32f);

            // Tiny noise puff for texture
            float noise = ((float)rng.NextDouble() * 2f - 1f);
            float smoothNoise = (prevNoise * 0.9f + noise * 0.1f);
            prevNoise = smoothNoise;
            float puff = smoothNoise * 0.08f * ExpDecay(t, 0.18f);

            // Two soft taps ("seal")
            float tap1 = 0.14f;
            float tap2 = 0.28f;
            float tap = 0f;
            if ((t > tap1 && t < tap1 + 0.05f) || (t > tap2 && t < tap2 + 0.05f))
            {
                float tapT = t > tap2 ? (t - tap2) : (t - tap1);
                float tapBase = t > tap2 ? 170f : 185f;
                float tapProgress = Mathf.Clamp(tapT / 0.05f, 0f, 1f);
                float tapFreq = Mathf.Lerp(tapBase * 1.04f, tapBase * 0.96f, tapProgress);
                tapPhase += TwoPi * tapFreq / SampleRate;
                float tapGain = t > tap2 ? 0.065f : 0.08f;
                float tapTone = MathF.Sin(tapPhase) * tapGain * ExpDecay(tapT, 0.028f);
                float tapNoise = ((float)rng.NextDouble() * 2f - 1f) * 0.012f * ExpDecay(tapT, 0.01f);
                tap = tapTone + tapNoise;
            }

            // Tiny glow after the second tap
            float glow = 0f;
            if (t > tap2 && t < tap2 + 0.12f)
            {
                float glowT = t - tap2;
                float glowFreq = 420f;
                glowPhase += TwoPi * glowFreq / SampleRate;
                glow = MathF.Sin(glowPhase) * 0.035f * ExpDecay(glowT, 0.08f);
            }

            // Smooth in, short tail after second tap
            float attack = Mathf.Clamp(t / 0.08f, 0f, 1f);
            attack = attack * attack * (3f - 2f * attack);
            float tail = t > tap2 ? ExpDecay(t - tap2, 0.38f) : 1f;

            samples[i] = ((sub + puff) * attack * tail + tap + glow) * 3.5f;
        }

        return MakeWav(samples);
    }

    /// <summary>
    /// Generates a beacon activation sound: larger, slower version of Collectible.
    /// </summary>
    public static AudioStreamWav BeaconActivated(Random? rng = null)
    {
        rng ??= Random.Shared;

        float duration = 0.95f + (float)rng.NextDouble() * 0.1f;
        int sampleCount = (int)(SampleRate * duration);
        var samples = new float[sampleCount];

        float subFreq = 48f + (float)rng.NextDouble() * 10f;
        float phaseSub = 0f;
        float tapPhase = 0f;
        float glowPhase = 0f;
        float prevNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;

            // Low base with longer decay
            phaseSub += TwoPi * subFreq / SampleRate;
            float sub = MathF.Sin(phaseSub) * 0.38f * ExpDecay(t, 0.45f);

            // Gentle noise puff for texture
            float noise = ((float)rng.NextDouble() * 2f - 1f);
            float smoothNoise = (prevNoise * 0.9f + noise * 0.1f);
            prevNoise = smoothNoise;
            float puff = smoothNoise * 0.06f * ExpDecay(t, 0.25f);

            // Two slower taps
            float tap1 = 0.22f;
            float tap2 = 0.46f;
            float tap = 0f;
            if ((t > tap1 && t < tap1 + 0.06f) || (t > tap2 && t < tap2 + 0.06f))
            {
                float tapT = t > tap2 ? (t - tap2) : (t - tap1);
                float tapBase = t > tap2 ? 160f : 175f;
                float tapProgress = Mathf.Clamp(tapT / 0.06f, 0f, 1f);
                float tapFreq = Mathf.Lerp(tapBase * 1.03f, tapBase * 0.97f, tapProgress);
                tapPhase += TwoPi * tapFreq / SampleRate;
                float tapGain = t > tap2 ? 0.06f : 0.075f;
                float tapTone = MathF.Sin(tapPhase) * tapGain * ExpDecay(tapT, 0.035f);
                float tapNoise = ((float)rng.NextDouble() * 2f - 1f) * 0.01f * ExpDecay(tapT, 0.012f);
                tap = tapTone + tapNoise;
            }

            // Soft glow after second tap
            float glow = 0f;
            if (t > tap2 && t < tap2 + 0.16f)
            {
                float glowT = t - tap2;
                float glowFreq = 360f;
                glowPhase += TwoPi * glowFreq / SampleRate;
                glow = MathF.Sin(glowPhase) * 0.03f * ExpDecay(glowT, 0.12f);
            }

            float attack = Mathf.Clamp(t / 0.1f, 0f, 1f);
            attack = attack * attack * (3f - 2f * attack);
            float tail = t > tap2 ? ExpDecay(t - tap2, 0.5f) : 1f;

            samples[i] = ((sub + puff) * attack * tail + tap + glow) * 3.5f;
        }

        return MakeWav(samples);
    }

    /// <summary>
    /// Generates an anchor placement sound: magnetic lock engaging.
    /// Cyberpunk style - chunky impact with electronic confirm.
    /// </summary>
    public static AudioStreamWav AnchorPlace(Random? rng = null)
    {
        rng ??= Random.Shared;

        float duration = 0.25f + (float)rng.NextDouble() * 0.04f;
        int sampleCount = (int)(SampleRate * duration);
        var samples = new float[sampleCount];

        float impactFreq = 65f + (float)rng.NextDouble() * 15f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float progress = i / (float)sampleCount;

            // Chunky magnetic lock impact
            float dropFreq = impactFreq * (1.3f - progress * 0.4f);
            float impact = MathF.Sin(TwoPi * dropFreq * t) * 0.55f * ExpDecay(t, 0.045f);
            // Sub harmonic for weight
            impact += MathF.Sin(TwoPi * dropFreq * 0.5f * t) * 0.3f * ExpDecay(t, 0.05f);
            // Upper harmonic for definition
            impact += MathF.Sin(TwoPi * dropFreq * 2f * t) * 0.15f * ExpDecay(t, 0.03f);

            // Electronic confirmation tone (delayed)
            float beep = 0f;
            float beepStart = 0.06f;
            if (progress > beepStart && progress < beepStart + 0.12f)
            {
                float beepT = (progress - beepStart) / 0.12f;
                float beepFreq = 320f;
                beep = MathF.Sin(TwoPi * beepFreq * t) * 0.3f;
                beep += MathF.Sin(TwoPi * beepFreq * 2f * t) * 0.12f; // Octave
                float beepEnv = beepT < 0.08f ? beepT / 0.08f : ExpDecay(beepT - 0.08f, 0.04f);
                beep *= beepEnv;
            }

            // Short click transient at start
            float click = 0f;
            if (progress < 0.01f)
            {
                click = MathF.Sin(TwoPi * 1500f * t) * 0.25f * (1f - progress / 0.01f);
            }

            samples[i] = impact + beep + click;
        }

        return MakeWav(samples);
    }

    /// <summary>
    /// Generates a prism placement sound: anchor lock with a crystalline shimmer.
    /// Based on AnchorPlace but brighter and more "prismatic".
    /// </summary>
    public static AudioStreamWav PrismPlace(Random? rng = null)
    {
        rng ??= Random.Shared;

        float duration = 0.68f + (float)rng.NextDouble() * 0.08f;
        int sampleCount = (int)(SampleRate * duration);
        var samples = new float[sampleCount];

        float thumpFreq = 54f + (float)rng.NextDouble() * 10f;
        float toneStart = 280f + (float)rng.NextDouble() * 25f;
        float toneEnd = 220f + (float)rng.NextDouble() * 20f;
        float thumpPhase = 0f;
        float tonePhase = 0f;
        float shimmerPhase = 0f;
        float prevNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float progress = i / (float)sampleCount;

            // Soft low thump (short, not punchy)
            thumpPhase += TwoPi * thumpFreq / SampleRate;
            float thump = MathF.Sin(thumpPhase) * 0.2f * ExpDecay(t, 0.16f);

            // Smooth confirm tone (gentle downward sweep)
            float toneFreq = Mathf.Lerp(toneStart, toneEnd, MathF.Sqrt(progress));
            tonePhase += TwoPi * toneFreq / SampleRate;
            float tone = MathF.Sin(tonePhase) * 0.12f * ExpDecay(t, 0.28f);

            // Light shimmer bed (subtle, late)
            float shimmer = 0f;
            if (t > 0.06f)
            {
                float st = t - 0.06f;
                shimmerPhase += TwoPi * 420f / SampleRate;
                float toneShimmer = MathF.Sin(shimmerPhase) * 0.01f;
                float noise = ((float)rng.NextDouble() * 2f - 1f);
                float smoothNoise = prevNoise * 0.92f + noise * 0.08f;
                prevNoise = smoothNoise;
                float noiseShimmer = smoothNoise * 0.01f;
                shimmer = (toneShimmer + noiseShimmer) * ExpDecay(st, 0.18f);
            }

            // Slow envelope for softer start/end
            float attack = Mathf.Clamp(progress / 0.2f, 0f, 1f);
            attack = attack * attack * (3f - 2f * attack);
            float tail = Mathf.Clamp((1f - progress) / 0.55f, 0f, 1f);
            tail = tail * tail * (3f - 2f * tail);

            samples[i] = (thump + tone + shimmer) * attack * tail * 1.45f;
        }

        return MakeWav(samples);
    }

    /// <summary>
    /// Generates a cable creation sound: energy tether locking in.
    /// Cyberpunk style - tension and lock.
    /// </summary>
    public static AudioStreamWav CableCreated(Random? rng = null)
    {
        rng ??= Random.Shared;

        float duration = 0.4f;
        int sampleCount = (int)(SampleRate * duration);
        var samples = new float[sampleCount];

        float baseFreq = 45f + (float)rng.NextDouble() * 10f;

        // Use phase accumulator for clean frequency changes
        float phase1 = 0f;
        float phase2 = 0f;
        float phase3 = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float progress = i / (float)sampleCount;

            // Master envelope - everything goes through this
            float masterEnv;
            if (progress < 0.015f)
                masterEnv = progress / 0.015f;
            else if (progress < 0.5f)
                masterEnv = 1f;
            else
                masterEnv = ExpDecay((progress - 0.5f) * 2f, 0.06f);

            // Rising tone using phase accumulator (no clicks)
            float riseFreq = 60f + progress * 100f;
            phase1 += riseFreq / SampleRate * TwoPi;
            float rise = MathF.Sin(phase1) * 0.35f;
            // Smooth envelope for rise - builds then decays
            float riseEnv = progress < 0.4f
                ? (0.5f + progress * 1.25f)
                : ExpDecay((progress - 0.4f) * 3f, 0.08f);
            rise *= riseEnv;

            // Sub bass
            phase2 += baseFreq / SampleRate * TwoPi;
            float sub = MathF.Sin(phase2) * 0.45f;
            sub += MathF.Sin(phase2 * 2f) * 0.2f;

            // Lock hit at ~50%
            float hit = 0f;
            if (progress > 0.45f)
            {
                float hitT = progress - 0.45f;
                phase3 += 75f / SampleRate * TwoPi;
                hit = MathF.Sin(phase3) * 0.5f * ExpDecay(hitT * 5f, 0.04f);
                hit += MathF.Sin(phase3 * 2f) * 0.25f * ExpDecay(hitT * 5f, 0.03f);
                hit += MathF.Sin(phase3 * 4f) * 0.1f * ExpDecay(hitT * 6f, 0.02f);
            }

            samples[i] = (rise + sub + hit) * masterEnv;
        }

        return MakeWav(samples);
    }

    /// <summary>
    /// Generates a beacon sync sound: data uplink connection confirmation.
    /// Cyberpunk style - resonant digital tones with sub-bass presence.
    /// </summary>
    public static AudioStreamWav BeaconSync(Random? rng = null)
    {
        rng ??= Random.Shared;

        float duration = 1.6f + (float)rng.NextDouble() * 0.15f;
        int sampleCount = (int)(SampleRate * duration);
        var samples = new float[sampleCount];

        // Based on BeaconActivated, but longer with early repeats and a slower start
        float subFreq = 48f + (float)rng.NextDouble() * 10f;
        float phaseSub = 0f;
        float tapPhase = 0f;
        float glowPhase = 0f;
        float prevNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;

            // Low base with longer decay
            phaseSub += TwoPi * subFreq / SampleRate;
            float sub = MathF.Sin(phaseSub) * 0.38f * ExpDecay(t, 0.75f);

            // Gentle noise puff for texture
            float noise = ((float)rng.NextDouble() * 2f - 1f);
            float smoothNoise = (prevNoise * 0.9f + noise * 0.1f);
            prevNoise = smoothNoise;
            float puff = smoothNoise * 0.06f * ExpDecay(t, 0.45f);

            // Taps with early repeats and later echoes
            float tap1 = 0.22f;
            float tap1b = 0.34f; // early repeat
            float tap2 = 0.46f;
            float tap2b = 0.58f; // early repeat
            float tap3 = 0.86f;  // later repeat
            float tap4 = 1.14f;  // faint tail repeat
            float tap = 0f;

            if ((t > tap1 && t < tap1 + 0.06f) || (t > tap1b && t < tap1b + 0.05f) ||
                (t > tap2 && t < tap2 + 0.06f) || (t > tap2b && t < tap2b + 0.05f) ||
                (t > tap3 && t < tap3 + 0.05f) || (t > tap4 && t < tap4 + 0.05f))
            {
                float tapT = t > tap4 ? (t - tap4)
                    : (t > tap3 ? (t - tap3)
                        : (t > tap2b ? (t - tap2b)
                            : (t > tap2 ? (t - tap2)
                                : (t > tap1b ? (t - tap1b) : (t - tap1)))));

                float tapBase = t > tap4 ? 150f
                    : (t > tap3 ? 158f
                        : (t > tap2b ? 168f
                            : (t > tap2 ? 160f
                                : (t > tap1b ? 178f : 175f))));

                float tapProgress = Mathf.Clamp(tapT / 0.06f, 0f, 1f);
                float tapFreq = Mathf.Lerp(tapBase * 1.03f, tapBase * 0.97f, tapProgress);
                tapPhase += TwoPi * tapFreq / SampleRate;
                float tapGain = t > tap4 ? 0.03f
                    : (t > tap3 ? 0.045f
                        : (t > tap2b ? 0.055f
                            : (t > tap2 ? 0.06f
                                : (t > tap1b ? 0.065f : 0.075f))));
                float tapTone = MathF.Sin(tapPhase) * tapGain * ExpDecay(tapT, 0.035f);
                float tapNoise = ((float)rng.NextDouble() * 2f - 1f) * 0.01f * ExpDecay(tapT, 0.012f);
                tap = tapTone + tapNoise;
            }

            // Soft glow after second tap (longer for grandeur)
            float glow = 0f;
            if (t > tap2 && t < tap2 + 0.35f)
            {
                float glowT = t - tap2;
                float glowFreq = 360f;
                glowPhase += TwoPi * glowFreq / SampleRate;
                glow = MathF.Sin(glowPhase) * 0.03f * ExpDecay(glowT, 0.22f);
            }

            // Slower start and long tail
            float attack = Mathf.Clamp(t / 0.25f, 0f, 1f);
            attack = attack * attack * (3f - 2f * attack);
            float tail = t > tap2 ? ExpDecay(t - tap2, 0.95f) : 1f;

            samples[i] = ((sub + puff) * attack * tail + tap + glow) * 3.5f;
        }

        return MakeWav(samples);
    }

    /// <summary>
    /// Generates a fall penalty sound: system damage alert with distortion.
    /// Cyberpunk style - glitchy warning, bass impact, digital corruption.
    /// </summary>
    public static AudioStreamWav FallPenalty(Random? rng = null)
    {
        rng ??= Random.Shared;

        float duration = 0.32f + (float)rng.NextDouble() * 0.05f;
        int sampleCount = (int)(SampleRate * duration);
        var samples = new float[sampleCount];

        float baseFreq = 80f + (float)rng.NextDouble() * 15f;
        float phase = 0f;
        float phase2 = 0f;
        float phaseSub = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float progress = i / (float)sampleCount;

            // Deep impact thud
            float subFreq = 45f + 10f * (1f - progress);
            phaseSub += TwoPi * subFreq / SampleRate;
            float thud = MathF.Sin(phaseSub) * 0.7f * ExpDecay(t, 0.14f);

            // Low-mid body (subtle, keeps it from being pure sub)
            float midFreq = baseFreq * (1.2f - progress * 0.4f);
            phase += TwoPi * midFreq / SampleRate;
            phase2 += TwoPi * (midFreq * 1.01f) / SampleRate;
            float mid = (MathF.Sin(phase) * 0.22f + MathF.Sin(phase2) * 0.12f);
            mid *= ExpDecay(t, 0.12f);

            // Tiny click at start (not noisy)
            float crack = 0f;
            if (t < 0.015f)
            {
                float crackEnv = ExpDecay(t, 0.004f);
                crack = ((float)rng.NextDouble() * 2f - 1f) * 0.06f * crackEnv;
            }

            // Fast decay envelope (deep hit)
            float attack = Mathf.Clamp(progress / 0.03f, 0f, 1f);
            attack = attack * attack * (3f - 2f * attack);
            float tail = Mathf.Clamp((1f - progress) / 0.75f, 0f, 1f);
            tail = tail * tail * (3f - 2f * tail);

            samples[i] = (thud + mid + crack) * attack * tail * 1.2f;
        }

        return MakeWav(samples);
    }

    /// <summary>
    /// Creates an AudioStreamWav from float samples (-1 to 1 range).
    /// </summary>
    private static AudioStreamWav MakeWav(float[] samples)
    {
        var wav = new AudioStreamWav();
        wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
        wav.MixRate = SampleRate;
        wav.Stereo = false;
        wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;

        // Convert to 16-bit PCM
        var pcmData = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short pcm = FloatToPcm16(samples[i]);
            pcmData[i * 2] = (byte)(pcm & 0xFF);
            pcmData[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        wav.Data = pcmData;
        return wav;
    }

    /// <summary>
    /// Converts a float sample (-1 to 1) to 16-bit PCM.
    /// </summary>
    private static short FloatToPcm16(float sample)
    {
        // Clamp to -1..1 range
        sample = MathF.Max(-1f, MathF.Min(1f, sample));
        // Scale to short range
        return (short)(sample * 32767f);
    }

    /// <summary>
    /// Exponential decay envelope.
    /// </summary>
    /// <param name="t">Time in seconds</param>
    /// <param name="halfLife">Time for amplitude to halve</param>
    private static float ExpDecay(float t, float halfLife)
    {
        return MathF.Exp(-0.693f * t / halfLife); // ln(2) ≈ 0.693
    }
}
