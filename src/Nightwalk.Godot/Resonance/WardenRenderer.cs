using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;
using System;
using System.Collections.Generic;

namespace Nightwalk.Godot.Resonance;

/// <summary>
/// Visual states for the Warden shard cluster.
/// </summary>
public enum WardenVisualState
{
    Normal,
    Vulnerable,
    Departing
}

/// <summary>
/// Data for a single shard in the cluster.
/// </summary>
internal sealed class ShardData
{
    public MeshInstance3D Instance { get; set; } = null!;
    public ShaderMaterial Material { get; set; } = null!;
    public Vector3 BasePosition { get; set; }
    public Vector3 BaseRotation { get; set; }
    public Vector3 TargetPosition { get; set; }
    public Vector3 TargetRotation { get; set; }
    public Vector3 CurrentPosition { get; set; }
    public Vector3 CurrentRotation { get; set; }
    public Vector3 RotationSpeed { get; set; }  // Random rotation speed per axis
    public float DistanceFromCore { get; set; }
    public bool IsCoreShard { get; set; }
}

/// <summary>
/// Procedurally generates the Warden's visual representation.
/// A cluster of floating metallic obsidian-like shards.
/// </summary>
public sealed partial class WardenRenderer : Node3D
{
    private WardenConfig? _config;
    private readonly List<ShardData> _shards = new();
    private ShardData? _coreShard;

    // Shared resources
    private Shader? _shardShader;

    // Animation state
    private WardenVisualState _currentState = WardenVisualState.Normal;
    private WardenVisualState _targetState = WardenVisualState.Normal;
    private float _stateTransitionProgress = 1f;
    private float _clusterRotation;
    private float _shaderTime;
    private float _pulseTime;
    private float _vulnerableBlend;
    private float _targetVulnerableBlend;

    // Pulse settings
    private const float PulseSpeed = 0.3f;  // Slow, ominous breathing
    private const float PulseAmount = 0.08f;

    // Colors from config
    private Color _baseColor;
    private Color _brightColor;
    private Color _vulnerableColor;

    // Random for deterministic generation
    private Random _random = new(42);

    // Purpose code for StableHash
    private const int PurposeDeparture = unchecked((int)0xDE9A0001);

    /// <summary>
    /// Gets the world position of the core shard tip (for scan projection origin).
    /// </summary>
    public Vector3 CoreShardTipPosition
    {
        get
        {
            if (_coreShard == null || _config == null) return GlobalPosition;
            // Core shard points downward, so tip is below center
            var shardPos = _coreShard.Instance.GlobalPosition;
            return shardPos + Vector3.Down * _config.Scale * 0.08f;
        }
    }

    public void Initialize(WardenConfig config)
    {
        _config = config;
        _random = new Random(42); // Reset for deterministic generation

        // Parse colors
        _baseColor = Color.FromHtml(config.ShardBaseColor);
        _brightColor = Color.FromHtml(config.ShardBrightColor);
        _vulnerableColor = Color.FromHtml(config.VulnerableColor);

        // Load shaders
        _shardShader = GD.Load<Shader>("res://shaders/warden_shard.gdshader");

        CreateShards();
        ComputeStatePositions(WardenVisualState.Normal);
        ApplyPositionsImmediate();
    }

    private void CreateShards()
    {
        if (_config == null) return;

        int shardCount = _config.ShardCount;
        float clusterRadius = _config.Scale * _config.ClusterRadius;

        // Generate Fibonacci sphere positions for even distribution
        var basePositions = GenerateFibonacciSphere(shardCount, clusterRadius);

        // Find the bottommost position index for core shard
        int coreIndex = 0;
        float lowestY = float.MaxValue;
        for (int i = 0; i < basePositions.Count; i++)
        {
            if (basePositions[i].Y < lowestY)
            {
                lowestY = basePositions[i].Y;
                coreIndex = i;
            }
        }

        for (int i = 0; i < shardCount; i++)
        {
            var shard = CreateShard(i, basePositions[i], i == coreIndex);
            _shards.Add(shard);

            if (i == coreIndex)
            {
                _coreShard = shard;
            }
        }
    }

    private List<Vector3> GenerateFibonacciSphere(int count, float radius)
    {
        var positions = new List<Vector3>();
        float goldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;
        float angleIncrement = Mathf.Pi * 2f * goldenRatio;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float inclination = Mathf.Acos(1f - 2f * t);
            float azimuth = angleIncrement * i;

            float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
            float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
            float z = Mathf.Cos(inclination);

            // Add jitter (0.7-1.3x) for chaotic arrangement
            float jitter = 0.7f + (float)_random.NextDouble() * 0.6f;
            positions.Add(new Vector3(x, z, y) * radius * jitter);
        }

        return positions;
    }

    private ShardData CreateShard(int index, Vector3 position, bool isCore)
    {
        if (_config == null) throw new InvalidOperationException("Config not initialized");

        float scale = _config.Scale;

        // Calculate distance from core (0 = core, 1 = outermost)
        float distanceFromCore = position.Length() / (_config.Scale * _config.ClusterRadius * 1.3f);
        distanceFromCore = Mathf.Clamp(distanceFromCore, 0f, 1f);

        // Determine shard size category (8-12 large, rest small)
        bool isLarge = index < 10;
        float sizeMultiplier = isLarge ? 1f : 0.5f + (float)_random.NextDouble() * 0.3f;

        // Create non-uniform BoxMesh (elongated shards)
        float baseLength = scale * 0.06f * sizeMultiplier;
        var shardMesh = new BoxMesh
        {
            Size = new Vector3(
                baseLength * (0.3f + (float)_random.NextDouble() * 0.4f),
                baseLength * (1.5f + (float)_random.NextDouble() * 1.0f), // Elongated
                baseLength * (0.3f + (float)_random.NextDouble() * 0.4f)
            )
        };

        // Create shader material
        var material = new ShaderMaterial
        {
            Shader = _shardShader
        };
        material.SetShaderParameter("base_color", _baseColor);
        material.SetShaderParameter("bright_color", _brightColor);
        material.SetShaderParameter("vulnerable_color", _vulnerableColor);
        material.SetShaderParameter("distance_from_core", distanceFromCore);
        material.SetShaderParameter("vulnerable_blend", 0f);

        // Random rotation (core shard gets special treatment)
        Vector3 rotation;
        if (isCore)
        {
            // Core shard points downward
            rotation = new Vector3(0, (float)_random.NextDouble() * Mathf.Pi * 2f, 0);
        }
        else
        {
            rotation = new Vector3(
                (float)_random.NextDouble() * Mathf.Pi * 2f,
                (float)_random.NextDouble() * Mathf.Pi * 2f,
                (float)_random.NextDouble() * Mathf.Pi * 2f
            );
        }

        var instance = new MeshInstance3D
        {
            Name = isCore ? "CoreShard" : $"Shard_{index}",
            Mesh = shardMesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = position,
            Rotation = rotation
        };

        AddChild(instance);

        // Random rotation speed for each axis (slower for larger shards)
        float rotSpeedMultiplier = isLarge ? 0.15f : 0.25f;
        var rotationSpeed = new Vector3(
            ((float)_random.NextDouble() - 0.5f) * rotSpeedMultiplier,
            ((float)_random.NextDouble() - 0.5f) * rotSpeedMultiplier,
            ((float)_random.NextDouble() - 0.5f) * rotSpeedMultiplier
        );

        return new ShardData
        {
            Instance = instance,
            Material = material,
            BasePosition = position,
            BaseRotation = rotation,
            TargetPosition = position,
            TargetRotation = rotation,
            CurrentPosition = position,
            CurrentRotation = rotation,
            RotationSpeed = rotationSpeed,
            DistanceFromCore = distanceFromCore,
            IsCoreShard = isCore
        };
    }

    private void ComputeStatePositions(WardenVisualState state)
    {
        if (_config == null) return;

        float scale = _config.Scale;

        foreach (var shard in _shards)
        {
            switch (state)
            {
                case WardenVisualState.Normal:
                    shard.TargetPosition = shard.BasePosition;
                    shard.TargetRotation = shard.BaseRotation;
                    break;

                case WardenVisualState.Vulnerable:
                    if (shard.IsCoreShard)
                    {
                        // Core drops down
                        shard.TargetPosition = shard.BasePosition + Vector3.Down * scale * 0.15f;
                    }
                    else
                    {
                        // Others spread apart and shift up
                        shard.TargetPosition = shard.BasePosition * 1.3f + Vector3.Up * scale * 0.1f;
                    }
                    shard.TargetRotation = shard.BaseRotation;
                    break;

                case WardenVisualState.Departing:
                    // Scatter upward randomly
                    var scatterOffset = new Vector3(
                        ((float)_random.NextDouble() - 0.5f) * scale * 0.2f,
                        scale * 0.3f + (float)_random.NextDouble() * scale * 0.2f,
                        ((float)_random.NextDouble() - 0.5f) * scale * 0.2f
                    );
                    shard.TargetPosition = shard.BasePosition + scatterOffset;
                    shard.TargetRotation = shard.BaseRotation + new Vector3(
                        (float)_random.NextDouble() * 0.5f,
                        (float)_random.NextDouble() * 0.5f,
                        (float)_random.NextDouble() * 0.5f
                    );
                    break;
            }
        }
    }

    private void ApplyPositionsImmediate()
    {
        foreach (var shard in _shards)
        {
            shard.CurrentPosition = shard.TargetPosition;
            shard.CurrentRotation = shard.TargetRotation;
            shard.Instance.Position = shard.CurrentPosition;
            shard.Instance.Rotation = shard.CurrentRotation;
        }
    }

    public override void _Process(double delta)
    {
        if (_config == null) return;

        float dt = (float)delta;

        // Update shader time
        _shaderTime += dt;
        foreach (var shard in _shards)
        {
            shard.Material.SetShaderParameter("time", _shaderTime);
        }

        // Rotate entire cluster slowly
        _clusterRotation += dt * 0.15f;
        Rotation = new Vector3(0, _clusterRotation, 0);

        // Update pulse animation
        _pulseTime += dt * PulseSpeed;
        float pulseScale = 1f + Mathf.Sin(_pulseTime * Mathf.Tau) * PulseAmount;

        // Animate state transition
        if (_stateTransitionProgress < 1f)
        {
            _stateTransitionProgress += dt * 2f; // Transition over ~0.5 seconds
            _stateTransitionProgress = Mathf.Min(_stateTransitionProgress, 1f);

            // Ease-out curve
            float t = 1f - Mathf.Pow(1f - _stateTransitionProgress, 3f);

            foreach (var shard in _shards)
            {
                shard.CurrentPosition = shard.CurrentPosition.Lerp(shard.TargetPosition, t * dt * 5f);
                shard.CurrentRotation = shard.CurrentRotation.Lerp(shard.TargetRotation, t * dt * 5f);
            }
        }

        // Apply positions with pulse effect and individual shard rotation
        foreach (var shard in _shards)
        {
            // Pulse moves shards away from center
            var pulseOffset = shard.CurrentPosition.Normalized() * (pulseScale - 1f) * shard.CurrentPosition.Length();
            shard.Instance.Position = shard.CurrentPosition + pulseOffset;

            // Apply individual shard rotation
            shard.CurrentRotation += shard.RotationSpeed * dt;
            shard.Instance.Rotation = shard.CurrentRotation;
        }

        // Animate vulnerable blend
        _vulnerableBlend = Mathf.Lerp(_vulnerableBlend, _targetVulnerableBlend, dt * 4f);
        if (_coreShard != null)
        {
            _coreShard.Material.SetShaderParameter("vulnerable_blend", _vulnerableBlend);
        }
    }

    /// <summary>
    /// Sets the visual state and triggers transition animation.
    /// </summary>
    public void SetVisualState(WardenVisualState state)
    {
        if (state == _targetState) return;

        _targetState = state;
        _currentState = state;
        _stateTransitionProgress = 0f;

        // Reset random for deterministic but varied departing animation
        if (state == WardenVisualState.Departing)
        {
            int seed = StableHash.Combine((int)(_shaderTime * 1000f), PurposeDeparture);
            _random = new Random(seed);
        }

        ComputeStatePositions(state);

        // Update vulnerable blend target
        _targetVulnerableBlend = state == WardenVisualState.Vulnerable ? 1f : 0f;
    }

    /// <summary>
    /// Sets the core shard to vulnerable appearance (orange).
    /// </summary>
    public void SetCoreVulnerable(bool vulnerable)
    {
        _targetVulnerableBlend = vulnerable ? 1f : 0f;
    }
}
