using System;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Godot.Rendering.Materials;

namespace Nightwalk.Godot.Rendering;

/// <summary>
/// Manages rain particle effects that follow the player.
/// </summary>
public sealed partial class RainSystem : Node3D
{
    private GpuParticles3D? _rainParticles;
    private GpuParticles3D? _splashParticles;
    private FogVolume? _groundFogVolume;
    private WeatherConfig _config = null!;

    // Cached textures to prevent leaks
    private GradientTexture1D? _rainGradientTexture;
    private GradientTexture1D? _splashGradientTexture;
    private NoiseTexture3D? _fogNoiseTexture;

    /// <summary>
    /// Event fired when rain intensity changes (for audio sync).
    /// </summary>
    public event Action<float>? OnIntensityChanged;

    // Optional position override (for screensaver mode)
    private Vector3? _positionOverride;

    /// <summary>
    /// Sets an override position for rain effects (used during screensaver).
    /// Pass null to return to following the parent node.
    /// </summary>
    public void SetPositionOverride(Vector3? position)
    {
        _positionOverride = position;
    }

    public void Configure(WeatherConfig config)
    {
        _config = config;

        // Recreate everything with new config (only if already in tree)
        if (IsInsideTree())
        {
            Recreate();
        }
    }

    public override void _Ready()
    {
        Recreate();
    }

    private void Recreate()
    {
        ClearEffects();

        if (_config.RainEnabled)
        {
            CreateRainParticles();
        }

        if (_config.SplashEnabled)
        {
            CreateSplashParticles();
        }

        if (_config.GroundMistEnabled)
        {
            CreateGroundMist();
        }
    }

    private void ClearEffects()
    {
        if (_rainParticles != null)
        {
            _rainParticles.QueueFree();
            _rainParticles = null;
        }

        if (_splashParticles != null)
        {
            _splashParticles.QueueFree();
            _splashParticles = null;
        }

        if (_groundFogVolume != null)
        {
            _groundFogVolume.QueueFree();
            _groundFogVolume = null;
        }

        // Dispose cached textures
        _rainGradientTexture?.Dispose();
        _rainGradientTexture = null;

        _splashGradientTexture?.Dispose();
        _splashGradientTexture = null;

        _fogNoiseTexture?.Dispose();
        _fogNoiseTexture = null;
    }

    private void CreateRainParticles()
    {
        _rainParticles = new GpuParticles3D();
        _rainParticles.Name = "RainParticles";
        _rainParticles.Amount = (int)(_config.RainParticleCount * _config.RainIntensity);
        _rainParticles.Lifetime = _config.RainSpawnHeight / _config.RainFallSpeed * 1.5f;
        _rainParticles.Explosiveness = 0.0f;
        _rainParticles.Randomness = 0.2f;
        _rainParticles.VisibilityAabb = new Aabb(
            new Vector3(-_config.RainCoverageRadius, -_config.RainSpawnHeight - 5, -_config.RainCoverageRadius),
            new Vector3(_config.RainCoverageRadius * 2, _config.RainSpawnHeight + 10, _config.RainCoverageRadius * 2)
        );

        // Process material (controls particle behavior)
        var processMat = new ParticleProcessMaterial();

        // Emission shape: large box above player
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        processMat.EmissionBoxExtents = new Vector3(_config.RainCoverageRadius, 0.5f, _config.RainCoverageRadius);

        // Gravity/velocity - rain falls with wind direction
        float windX = 3.0f;  // Slight wind pushing rain
        float windZ = 1.5f;
        processMat.Gravity = new Vector3(windX, -_config.RainFallSpeed, windZ);
        processMat.Direction = new Vector3(windX * 0.1f, -1, windZ * 0.1f).Normalized();
        processMat.Spread = _config.RainSpreadAngle;
        processMat.InitialVelocityMin = _config.RainFallSpeed * (1.0f - _config.RainVelocityVariation);
        processMat.InitialVelocityMax = _config.RainFallSpeed * (1.0f + _config.RainVelocityVariation);

        // Align particles to their velocity direction
        processMat.ParticleFlagAlignY = true;

        // Size variation - more randomness
        processMat.ScaleMin = 0.4f;
        processMat.ScaleMax = 1.4f;

        // Color from config with fade over lifetime
        var rainColor = Color.FromHtml(_config.RainColor);
        rainColor.A = _config.RainAlpha;

        // Color ramp - fade in at start, fade out at end for less uniformity
        var colorRamp = new Gradient();
        var startColor = rainColor;
        startColor.A = 0.0f;
        var midColor = rainColor;
        var endColor = rainColor;
        endColor.A = rainColor.A * 0.3f;
        colorRamp.SetOffset(0, 0.0f);
        colorRamp.SetColor(0, startColor);
        colorRamp.AddPoint(0.1f, midColor);
        colorRamp.AddPoint(0.85f, midColor);
        colorRamp.SetOffset(1, 1.0f);
        colorRamp.SetColor(1, endColor);
        _rainGradientTexture = new GradientTexture1D();
        _rainGradientTexture.Gradient = colorRamp;
        processMat.ColorRamp = _rainGradientTexture;

        _rainParticles.ProcessMaterial = processMat;

        // Draw pass - 3D capsule/cylinder for raindrops (looks correct from all angles)
        var drawMat = new StandardMaterial3D();
        drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        drawMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        drawMat.AlbedoColor = rainColor;
        drawMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // Visible from all sides

        // Use capsule mesh - looks like raindrop from any angle
        var mesh = new CapsuleMesh();
        mesh.Radius = _config.RainDropWidth;
        mesh.Height = _config.RainDropHeight;
        mesh.RadialSegments = 4;  // Low poly for performance
        mesh.Rings = 1;
        mesh.Material = drawMat;

        _rainParticles.DrawPass1 = mesh;
        _rainParticles.Position = new Vector3(0, _config.RainSpawnHeight, 0);

        AddChild(_rainParticles);
    }

    private void CreateSplashParticles()
    {
        _splashParticles = new GpuParticles3D();
        _splashParticles.Name = "SplashParticles";
        _splashParticles.Amount = (int)(_config.SplashParticleCount * _config.RainIntensity);
        _splashParticles.Lifetime = _config.SplashLifetime;
        _splashParticles.Explosiveness = 0.0f;
        _splashParticles.Randomness = 0.5f;
        _splashParticles.VisibilityAabb = new Aabb(
            new Vector3(-_config.RainCoverageRadius, -1, -_config.RainCoverageRadius),
            new Vector3(_config.RainCoverageRadius * 2, 3, _config.RainCoverageRadius * 2)
        );
        _splashParticles.TopLevel = true; // Don't inherit parent Y position

        var processMat = new ParticleProcessMaterial();

        // Emit from ground plane area
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        processMat.EmissionBoxExtents = new Vector3(_config.RainCoverageRadius, 0.1f, _config.RainCoverageRadius);

        // Splashes bounce upward then fall
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = _config.SplashSpreadAngle;
        processMat.InitialVelocityMin = _config.SplashVelocityMin;
        processMat.InitialVelocityMax = _config.SplashVelocityMax;
        processMat.Gravity = new Vector3(0, -_config.SplashGravity, 0);

        // Scale animation - grow then shrink
        processMat.ScaleMin = 0.5f;
        processMat.ScaleMax = 1.5f;

        // Fade out over lifetime
        var colorRamp = new Gradient();
        var splashColor = Color.FromHtml(_config.SplashColor);
        splashColor.A = _config.SplashAlpha;
        var fadeColor = splashColor;
        fadeColor.A = 0.0f;
        colorRamp.SetColor(0, splashColor);
        colorRamp.SetColor(1, fadeColor);
        _splashGradientTexture = new GradientTexture1D();
        _splashGradientTexture.Gradient = colorRamp;
        processMat.ColorRamp = _splashGradientTexture;

        _splashParticles.ProcessMaterial = processMat;

        // Splash mesh - spheres for round appearance from any angle
        var drawMat = new StandardMaterial3D();
        drawMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        drawMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        drawMat.AlbedoColor = splashColor;
        drawMat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;

        var mesh = new SphereMesh();
        mesh.Radius = _config.SplashSize * 0.5f;
        mesh.Height = _config.SplashSize;
        mesh.RadialSegments = 6;
        mesh.Rings = 3;
        mesh.Material = drawMat;

        _splashParticles.DrawPass1 = mesh;

        AddChild(_splashParticles);
    }

    private void CreateGroundMist()
    {
        // Use FogVolume for proper volumetric ground fog
        _groundFogVolume = new FogVolume();
        _groundFogVolume.Name = "GroundFog";

        // Large box covering area around player at ground level
        _groundFogVolume.Size = new Vector3(
            _config.GroundMistCoverageRadius * 2,
            _config.GroundMistHeight,
            _config.GroundMistCoverageRadius * 2
        );
        _groundFogVolume.Shape = RenderingServer.FogVolumeShape.Box;

        // Create fog material with patchy density
        var fogMaterial = new FogMaterial();
        fogMaterial.Density = 0.15f; // Lower intensity
        fogMaterial.Albedo = Color.FromHtml(_config.GroundMistColor);
        fogMaterial.Emission = Color.FromHtml(_config.GroundMistColor) * 0.05f;

        // Height falloff - denser at bottom, fading at top
        fogMaterial.HeightFalloff = 6.0f;

        // Use 3D noise texture for patchy, uneven fog
        _fogNoiseTexture = GenerateFogNoiseTexture();
        fogMaterial.DensityTexture = _fogNoiseTexture;

        _groundFogVolume.Material = fogMaterial;
        _groundFogVolume.TopLevel = true; // Don't inherit parent transform
        _groundFogVolume.Position = new Vector3(0, _config.GroundMistHeight / 2f, 0);

        AddChild(_groundFogVolume);
    }

    public override void _Process(double delta)
    {
        // Use position override if set, otherwise follow parent
        Vector3 followPos;
        if (_positionOverride.HasValue)
        {
            followPos = _positionOverride.Value;
        }
        else
        {
            var parent = GetParent<Node3D>();
            if (parent == null) return;
            followPos = parent.GlobalPosition;
        }

        // Keep ground fog at ground level but follow XZ position
        if (_groundFogVolume != null)
        {
            _groundFogVolume.GlobalPosition = new Vector3(
                followPos.X,
                _config.GroundMistHeight / 2f,
                followPos.Z
            );
        }

        // Keep splashes at ground level while following XZ
        if (_splashParticles != null)
        {
            _splashParticles.GlobalPosition = new Vector3(
                followPos.X,
                0.05f,  // Right at ground level
                followPos.Z
            );
        }

        // Rain particles follow parent transform (already handled by being a child),
        // but need to update position if using override
        if (_positionOverride.HasValue && _rainParticles != null)
        {
            _rainParticles.GlobalPosition = new Vector3(
                followPos.X,
                followPos.Y + _config.RainSpawnHeight,
                followPos.Z
            );
        }
    }

    private static NoiseTexture3D GenerateFogNoiseTexture()
    {
        var noise = new FastNoiseLite();
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular; // More patchy/blobby
        noise.Frequency = 0.08f; // Higher frequency = smaller patches
        noise.FractalOctaves = 2;
        noise.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
        noise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance;

        var noiseTexture = new NoiseTexture3D();
        noiseTexture.Width = 64;
        noiseTexture.Height = 16;
        noiseTexture.Depth = 64;
        noiseTexture.Noise = noise;

        return noiseTexture;
    }

    /// <summary>
    /// Dynamically adjust rain intensity (0.0 to 1.0).
    /// </summary>
    public void SetIntensity(float intensity)
    {
        intensity = Mathf.Clamp(intensity, 0f, 1f);

        if (_rainParticles != null)
        {
            // Amount must be at least 1 (Godot requirement)
            _rainParticles.Amount = Math.Max(1, (int)(_config.RainParticleCount * intensity));
            _rainParticles.Emitting = intensity > 0;
        }

        if (_splashParticles != null)
        {
            _splashParticles.Amount = Math.Max(1, (int)(_config.SplashParticleCount * intensity));
            _splashParticles.Emitting = intensity > 0;
        }

        // Notify audio system of intensity change
        OnIntensityChanged?.Invoke(intensity);
    }

    public override void _ExitTree()
    {
        ClearEffects();
    }
}
