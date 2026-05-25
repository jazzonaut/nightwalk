using System;
using Godot;

namespace Nightwalk.Godot.Audio;

/// <summary>
/// Type of continuous synthesized loop.
/// </summary>
public enum SynthLoopType
{
    Falling,
    Jetpack
}

/// <summary>
/// Real-time audio synthesis player using AudioStreamGenerator.
/// Generates continuous sound loops with parameter modulation.
/// </summary>
public partial class SynthLoopPlayer : Node
{
    private const int SampleRate = 44100;
    private const float BufferLength = 0.1f; // 100ms buffer
    private const float TwoPi = MathF.PI * 2f;

    private AudioStreamPlayer _player = null!;
    private AudioStreamGenerator _generator = null!;
    private AudioStreamGeneratorPlayback? _playback;

    private SynthLoopType _loopType;
    private bool _isPlaying;
    private float _phase;
    private float _noisePhase;
    private float _vibrationPhase;
    private Random _rng = new();

    // Current parameter values (0-1 normalized)
    private float _velocity;
    private float _targetVelocity;
    private float _thrust;
    private float _targetThrust;

    // Fade state
    private float _fadeVolume = 1f;
    private float _fadeTarget = 1f;
    private float _fadeRate;

    // Noise state for continuity
    private float _lastNoise;

    /// <summary>
    /// Current velocity parameter (0-1). Affects cutoff and volume for falling wind.
    /// </summary>
    public float Velocity
    {
        get => _velocity;
        set => _targetVelocity = MathF.Max(0f, MathF.Min(1f, value));
    }

    /// <summary>
    /// Current thrust parameter (0-1). Affects pitch, volume, and noise for jetpack.
    /// </summary>
    public float Thrust
    {
        get => _thrust;
        set => _targetThrust = MathF.Max(0f, MathF.Min(1f, value));
    }

    public bool IsPlaying => _isPlaying;

    public override void _Ready()
    {
        _generator = new AudioStreamGenerator
        {
            MixRate = SampleRate,
            BufferLength = BufferLength
        };

        _player = new AudioStreamPlayer
        {
            Stream = _generator,
            Bus = "SFX"
        };
        AddChild(_player);
    }

    /// <summary>
    /// Starts the synthesis loop with the specified type.
    /// </summary>
    public void StartLoop(SynthLoopType type, float fadeInTime = 0f)
    {
        _loopType = type;
        _isPlaying = true;
        _phase = 0f;
        _noisePhase = 0f;
        _vibrationPhase = 0f;
        _lastNoise = 0f;
        if (fadeInTime > 0f)
        {
            _fadeVolume = 0f;
            _fadeTarget = 1f;
            _fadeRate = 1f / fadeInTime;
        }
        else
        {
            _fadeVolume = 1f;
            _fadeTarget = 1f;
            _fadeRate = 0f;
        }

        // Reset parameters based on type
        if (type == SynthLoopType.Falling)
        {
            _velocity = 0.3f;
            _targetVelocity = 0.3f;
        }
        else
        {
            _thrust = 0.4f;
            _targetThrust = 0.4f;
        }

        _player.Play();
        _playback = (AudioStreamGeneratorPlayback)_player.GetStreamPlayback();
    }

    /// <summary>
    /// Stops the loop with optional fade out.
    /// </summary>
    public void StopLoop(float fadeTime = 0f)
    {
        if (!_isPlaying) return;

        if (fadeTime <= 0f)
        {
            StopImmediate();
        }
        else
        {
            _fadeTarget = 0f;
            _fadeRate = 1f / fadeTime;
        }
    }

    private void StopImmediate()
    {
        _isPlaying = false;
        _player.Stop();
        _playback = null;
    }

    public override void _Process(double delta)
    {
        if (!_isPlaying || _playback == null) return;

        // Smooth parameter interpolation
        float dt = (float)delta;
        float smoothRate = 8f * dt;
        _velocity = Lerp(_velocity, _targetVelocity, smoothRate);
        _thrust = Lerp(_thrust, _targetThrust, smoothRate);

        // Fade handling
        if (_fadeRate > 0f)
        {
            _fadeVolume = MoveToward(_fadeVolume, _fadeTarget, _fadeRate * dt);
            if (_fadeVolume <= 0f)
            {
                StopImmediate();
                return;
            }
        }

        PushFrames();
    }

    private void PushFrames()
    {
        if (_playback == null) return;

        int framesAvailable = _playback.GetFramesAvailable();
        if (framesAvailable == 0) return;

        var frames = new Vector2[framesAvailable];
        float invSampleRate = 1f / SampleRate;

        for (int i = 0; i < framesAvailable; i++)
        {
            float sample = _loopType switch
            {
                SynthLoopType.Falling => GenerateFallingSample(invSampleRate),
                SynthLoopType.Jetpack => GenerateJetpackSample(invSampleRate),
                _ => 0f
            };

            sample *= _fadeVolume;
            frames[i] = new Vector2(sample, sample); // Mono -> Stereo
        }

        _playback.PushBuffer(frames);
    }


    /// <summary>
    /// Generates falling wind sound: deep air pressure + smooth mid wash.
    /// Less white-noise, more cinematic fall.
    /// </summary>
    private float GenerateFallingSample(float dt)
    {
        // Deep pressure fall: low, heavy air with minimal hiss
        float volume = 0.12f + _velocity * 0.32f; // 0.12 - 0.44

        // Slow pressure oscillation
        float pressureFreq = 0.6f + _velocity * 1.8f;
        _phase += pressureFreq * dt * TwoPi;
        if (_phase > TwoPi) _phase -= TwoPi;
        float pressure = MathF.Sin(_phase) * 0.28f;

        // Sub pulse for weight
        float subFreq = 28f + _velocity * 18f;
        _vibrationPhase += subFreq * dt * TwoPi;
        if (_vibrationPhase > TwoPi) _vibrationPhase -= TwoPi;
        float sub = MathF.Sin(_vibrationPhase) * 0.16f;
        sub += MathF.Sin(_vibrationPhase * 0.5f) * 0.06f;

        // Very soft air texture (heavily smoothed)
        float noise = (float)_rng.NextDouble() * 2f - 1f;
        float smoothing = 0.985f - _velocity * 0.08f;
        _lastNoise = _lastNoise * smoothing + noise * (1f - smoothing);
        float air = _lastNoise * 0.04f;

        float mix = pressure + sub + air;
        mix = MathF.Tanh(mix * 1.1f); // gentle softening
        return mix * volume;
    }

   /// <summary>
    /// Generates a jetpack hover sound: deep rumble + airy thrust hiss.
    /// </summary>
    private float GenerateJetpackSample(float dt)
    {
        float volume = 0.12f + _thrust * 0.28f;

        // Low rumble (softer, less engine-like)
        float rumbleFreq = 35f + _thrust * 15f;
        _vibrationPhase += rumbleFreq * dt * TwoPi;
        if (_vibrationPhase > TwoPi) _vibrationPhase -= TwoPi;
        float rumble = MathF.Sin(_vibrationPhase) * 0.4f;
        rumble += MathF.Sin(_vibrationPhase * 2f) * 0.12f;
        rumble += MathF.Sin(_vibrationPhase * 3f) * 0.04f; // subtle grit

        // Smooth energy hum (clean, non-hissy)
        float humFreq = 90f + _thrust * 35f;
        _noisePhase += humFreq * dt * TwoPi;
        if (_noisePhase > TwoPi) _noisePhase -= TwoPi;
        float hum = MathF.Sin(_noisePhase) * 0.12f;
        hum += MathF.Sin(_noisePhase * 2f) * 0.04f;
        hum += MathF.Sin(_noisePhase * 3f) * 0.015f;

        // Gentle wobble to keep it alive
        _phase += (2f + _thrust * 3f) * dt * TwoPi;
        if (_phase > TwoPi) _phase -= TwoPi;
        float wobble = MathF.Sin(_phase) * 0.06f + 0.94f;
        // Subtle flutter for a more organic feel.
        float flutterPhase = _phase * 7.5f;
        float flutter = MathF.Sin(flutterPhase) * 0.04f + 0.96f;

        float mix = rumble + hum;
        // Gentle saturation to avoid pure sine-wave feel.
        mix = MathF.Tanh(mix * 1.1f);
        return mix * volume * wobble * flutter;
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * MathF.Min(1f, t);
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }

    public override void _ExitTree()
    {
        StopImmediate();
    }
}
