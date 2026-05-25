using System;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Godot.Rendering.Sky;

/// <summary>
/// Manages animated sky effects rendered on an inverted dome mesh.
/// Includes aurora borealis, lightning flashes, light pollution, and electrical atmosphere (ember particles).
/// </summary>
public sealed partial class SkyDomeManager : Node3D
{
    private const string SkyDomeShaderPath = "res://shaders/sky_dome.gdshader";

    private MeshInstance3D? _domeMesh;
    private ShaderMaterial? _domeMaterial;
    private DirectionalLight3D? _lightningLight;
    private GpuParticles3D? _emberParticles;

    private SkyEffectsConfig _config = null!;
    private Random _random = new();

    // Follow target (camera/player) so dome stays centered on viewer
    private Node3D? _followTarget;

    // Lightning state
    private float _lightningTimer;
    private float _lightningFlashTimer;
    private bool _isLightningActive;
    private Vector2 _lightningCenter;

    // Multi-stroke lightning
    private int _strokesRemaining;
    private float _strokeGapTimer;
    private float _currentStrokeDuration;
    private float _currentStrokeIntensity;
    private int _currentFlashPattern;

    // Cached textures
    private GradientTexture1D? _emberGradientTexture;
    private GradientTexture2D? _emberSoftEdgeTexture;

    // Ember spawn height settings (cached for _Process)
    private float _emberSpawnHeight;
    private float _emberMinSpawnHeight;

    /// <summary>
    /// Creates and initializes a SkyDomeManager with the given configuration.
    /// </summary>
    public static SkyDomeManager Create(SkyEffectsConfig config)
    {
        var manager = new SkyDomeManager
        {
            Name = "SkyDomeManager"
        };
        manager._config = config;
        return manager;
    }

    /// <summary>
    /// Sets the target node that the sky dome should follow (typically the player/camera).
    /// </summary>
    public void SetFollowTarget(Node3D target)
    {
        _followTarget = target;
    }

    /// <summary>
    /// Reconfigures all sky effects from updated config (hot-reload support).
    /// </summary>
    public void Configure(SkyEffectsConfig config)
    {
        _config = config;

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

        if (!_config.Enabled)
        {
            return;
        }

        CreateDomeMesh();
        CreateLightningLight();

        if (_config.ElectricalAtmosphereEnabled && _config.EmberParticleCount > 0)
        {
            CreateEmberParticles();
        }

        // Reset lightning timer
        _lightningTimer = GetRandomLightningInterval();
        _lightningFlashTimer = 0f;
        _isLightningActive = false;

        Log.Info(LogCategory.Config, "Sky dome effects initialized");
    }

    private void ClearEffects()
    {
        _domeMesh?.QueueFree();
        _domeMesh = null;
        _domeMaterial = null;

        _lightningLight?.QueueFree();
        _lightningLight = null;

        _emberParticles?.QueueFree();
        _emberParticles = null;

        _emberGradientTexture?.Dispose();
        _emberGradientTexture = null;

        _emberSoftEdgeTexture?.Dispose();
        _emberSoftEdgeTexture = null;
    }

    private void CreateDomeMesh()
    {
        _domeMesh = new MeshInstance3D { Name = "SkyDome" };

        float domeRadius = _config.DomeRadius;
        int domeSegments = _config.DomeSegments;

        // Create inverted sphere mesh
        var sphereMesh = new SphereMesh
        {
            Radius = domeRadius,
            Height = domeRadius * 2,
            RadialSegments = domeSegments,
            Rings = domeSegments / 2
        };

        // Load and configure shader material
        var shader = GD.Load<Shader>(SkyDomeShaderPath);
        _domeMaterial = new ShaderMaterial { Shader = shader };

        UpdateDomeMaterialParams();

        sphereMesh.Material = _domeMaterial;
        _domeMesh.Mesh = sphereMesh;

        // No shadows, no collision
        _domeMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        AddChild(_domeMesh);
    }

    private void UpdateDomeMaterialParams()
    {
        if (_domeMaterial == null) return;

        // Aurora parameters
        _domeMaterial.SetShaderParameter("aurora_enabled", _config.AuroraEnabled);
        _domeMaterial.SetShaderParameter("aurora_primary_color", Color.FromHtml(_config.AuroraPrimaryColor));
        _domeMaterial.SetShaderParameter("aurora_secondary_color", Color.FromHtml(_config.AuroraSecondaryColor));
        _domeMaterial.SetShaderParameter("aurora_intensity", _config.AuroraIntensity);
        _domeMaterial.SetShaderParameter("aurora_speed", _config.AuroraSpeed);
        _domeMaterial.SetShaderParameter("aurora_band_count", _config.AuroraBandCount);

        // Lightning parameters
        _domeMaterial.SetShaderParameter("lightning_enabled", _config.LightningEnabled);
        _domeMaterial.SetShaderParameter("lightning_flash", 0f);
        _domeMaterial.SetShaderParameter("lightning_intensity", _config.LightningWorldIllumination);

        // Light pollution parameters
        _domeMaterial.SetShaderParameter("light_pollution_enabled", _config.LightPollutionEnabled);
        _domeMaterial.SetShaderParameter("light_pollution_intensity", _config.LightPollutionIntensity);
        _domeMaterial.SetShaderParameter("light_pollution_warm_color", Color.FromHtml(_config.LightPollutionWarmColor));
        _domeMaterial.SetShaderParameter("light_pollution_cool_color", Color.FromHtml(_config.LightPollutionCoolColor));
        _domeMaterial.SetShaderParameter("light_pollution_height", _config.LightPollutionHeight);
        _domeMaterial.SetShaderParameter("light_pollution_fade_up", _config.LightPollutionFadeUp);
        _domeMaterial.SetShaderParameter("light_pollution_fade_down", _config.LightPollutionFadeDown);
        _domeMaterial.SetShaderParameter("light_pollution_edge_noise", _config.LightPollutionEdgeNoise);
    }

    private void CreateLightningLight()
    {
        if (!_config.LightningEnabled) return;

        _lightningLight = new DirectionalLight3D
        {
            Name = "LightningLight",
            LightColor = Color.FromHtml(_config.LightningColor),
            LightEnergy = 0f, // Off by default
            LightVolumetricFogEnergy = _config.LightningVolumetricFogEnergy,
            ShadowEnabled = false, // No shadows for brief flash
            Visible = false
        };

        // Point downward
        _lightningLight.Rotation = new Vector3(Mathf.DegToRad(-90), 0, 0);

        AddChild(_lightningLight);
    }

    private void CreateEmberParticles()
    {
        float spawnRadius = _config.EmberSpawnRadius;
        float spawnHeight = _config.EmberSpawnHeight;

        // Cache for _Process to enforce minimum spawn height
        _emberSpawnHeight = spawnHeight;
        _emberMinSpawnHeight = _config.EmberMinSpawnHeight;

        _emberParticles = new GpuParticles3D
        {
            Name = "EmberParticles",
            Amount = _config.EmberParticleCount,
            Lifetime = _config.EmberLifetime,
            Preprocess = Mathf.Min(_config.EmberLifetime, 5f), // Pre-simulate up to 5s to avoid startup lag
            LocalCoords = true, // Particles move with emitter so they follow player
            Explosiveness = 0f,
            Randomness = 0.5f,
            VisibilityAabb = new Aabb(
                new Vector3(-spawnRadius * 1.5f, -spawnHeight * 0.5f, -spawnRadius * 1.5f),
                new Vector3(spawnRadius * 3f, spawnHeight * 2.5f, spawnRadius * 3f)
            )
        };

        var processMat = new ParticleProcessMaterial();

        // Emission box around player
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        processMat.EmissionBoxExtents = new Vector3(spawnRadius, spawnHeight, spawnRadius);

        // Upward drift with turbulence
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = _config.EmberSpread;
        processMat.InitialVelocityMin = _config.EmberVelocityMin;
        processMat.InitialVelocityMax = _config.EmberVelocityMax;
        processMat.Gravity = new Vector3(0, _config.EmberGravity, 0);

        // Turbulence for organic movement
        processMat.TurbulenceEnabled = true;
        processMat.TurbulenceNoiseStrength = _config.EmberTurbulence;
        processMat.TurbulenceNoiseSpeedRandom = 0.5f;
        processMat.TurbulenceNoiseScale = 2f;

        // Size
        processMat.ScaleMin = _config.EmberSizeMin;
        processMat.ScaleMax = _config.EmberSizeMax;

        // Scale curve - grow in, hold, shrink out
        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0f, 0f));      // Start at 0
        scaleCurve.AddPoint(new Vector2(0.15f, 1f));   // Grow to full by 15%
        scaleCurve.AddPoint(new Vector2(0.7f, 1f));    // Hold until 70%
        scaleCurve.AddPoint(new Vector2(1f, 0f));      // Shrink to 0 by end
        processMat.ScaleCurve = new CurveTexture { Curve = scaleCurve };

        // Simple color (no alpha fade needed since we use scale)
        var emberColor = Color.FromHtml(_config.EmberColor);
        var colorRamp = new Gradient();
        colorRamp.SetColor(0, emberColor);
        colorRamp.SetColor(1, emberColor);
        _emberGradientTexture = new GradientTexture1D { Gradient = colorRamp };
        processMat.ColorRamp = _emberGradientTexture;

        _emberParticles.ProcessMaterial = processMat;

        // Create radial gradient texture for soft edges
        // Smooth falloff from opaque center to transparent edges
        var softGradient = new Gradient();
        softGradient.InterpolationMode = Gradient.InterpolationModeEnum.Cubic;
        softGradient.SetColor(0, Colors.White);
        softGradient.SetOffset(0, 0f);
        softGradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
        softGradient.SetOffset(1, 1f);

        _emberSoftEdgeTexture = new GradientTexture2D
        {
            Gradient = softGradient,
            Width = 64,
            Height = 64,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f) // To top edge for proper circular coverage
        };

        // Draw pass - soft-edged billboard quads with fade in/out
        var drawMat = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = emberColor,
            AlbedoTexture = _emberSoftEdgeTexture,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = false, // Particle colors are linear
            EmissionEnabled = true,
            Emission = emberColor,
            EmissionEnergyMultiplier = _config.EmberGlow
        };

        var mesh = new QuadMesh
        {
            Size = new Vector2(0.2f, 0.2f),
            Material = drawMat
        };

        _emberParticles.DrawPass1 = mesh;
        _emberParticles.Position = new Vector3(0, spawnHeight * 0.5f, 0);

        AddChild(_emberParticles);
    }

    public override void _Process(double delta)
    {
        if (!_config.Enabled) return;

        // Keep dome centered on follow target so player can never escape it
        if (_followTarget != null)
        {
            GlobalPosition = _followTarget.GlobalPosition;

            // Adjust ember particles to respect minimum spawn height
            if (_emberParticles != null)
            {
                // Default local Y positions the center of the emission box
                float defaultLocalY = _emberSpawnHeight * 0.5f;

                // Calculate the minimum local Y needed to keep box bottom at min height
                // Box bottom in world = GlobalPosition.Y + localY - _emberSpawnHeight
                // We want: GlobalPosition.Y + localY - _emberSpawnHeight >= _emberMinSpawnHeight
                float minLocalY = _emberMinSpawnHeight - GlobalPosition.Y + _emberSpawnHeight;

                float localY = Mathf.Max(defaultLocalY, minLocalY);
                _emberParticles.Position = new Vector3(0, localY, 0);
            }
        }

        ProcessLightning((float)delta);
    }

    private void ProcessLightning(float delta)
    {
        if (!_config.LightningEnabled) return;

        if (_isLightningActive)
        {
            // Flash stroke is active
            _lightningFlashTimer -= delta;

            float flashProgress = 1f - (_lightningFlashTimer / _currentStrokeDuration);
            float flashIntensity = CalculateFlashIntensity(flashProgress, _currentFlashPattern) * _currentStrokeIntensity;

            // Update shader
            _domeMaterial?.SetShaderParameter("lightning_flash", flashIntensity);
            _domeMaterial?.SetShaderParameter("lightning_center", _lightningCenter);

            // Update directional light
            if (_lightningLight != null)
            {
                _lightningLight.Visible = flashIntensity > 0.01f;
                _lightningLight.LightEnergy = flashIntensity * _config.LightningWorldIllumination;
            }

            if (_lightningFlashTimer <= 0)
            {
                // Stroke finished
                _isLightningActive = false;

                // Reset flash
                _domeMaterial?.SetShaderParameter("lightning_flash", 0f);
                if (_lightningLight != null)
                {
                    _lightningLight.Visible = false;
                    _lightningLight.LightEnergy = 0f;
                }

                // Check for more strokes
                if (_strokesRemaining > 0)
                {
                    // Random gap before next stroke
                    float gapRange = _config.LightningStrokeGapMax - _config.LightningStrokeGapMin;
                    _strokeGapTimer = _config.LightningStrokeGapMin + (float)_random.NextDouble() * gapRange;
                }
                else
                {
                    // Lightning event finished, wait for next
                    _lightningTimer = GetRandomLightningInterval();
                }
            }
        }
        else if (_strokesRemaining > 0)
        {
            // Waiting between strokes
            _strokeGapTimer -= delta;

            if (_strokeGapTimer <= 0)
            {
                TriggerStroke(isFollowUp: true);
            }
        }
        else
        {
            // Waiting for next lightning event
            _lightningTimer -= delta;

            if (_lightningTimer <= 0)
            {
                TriggerLightning();
            }
        }
    }

    private void TriggerLightning()
    {
        // Decide total stroke count (1 to max)
        int maxStrokes = Math.Max(1, _config.LightningMaxStrokes);
        int totalStrokes;

        if (maxStrokes > 1 && (float)_random.NextDouble() < _config.LightningMultiStrokeChance)
        {
            // Multi-stroke: 2 to max strokes
            totalStrokes = _random.Next(2, maxStrokes + 1);
        }
        else
        {
            totalStrokes = 1; // Single stroke
        }

        // _strokesRemaining tracks how many strokes are LEFT (including current one about to start)
        _strokesRemaining = totalStrokes;

        // Random position in upper sky
        _lightningCenter = new Vector2(
            (float)_random.NextDouble() * 0.6f + 0.2f, // 0.2 to 0.8 horizontal
            (float)_random.NextDouble() * 0.35f + 0.55f  // 0.55 to 0.9 vertical (upper sky)
        );

        Log.Debug(LogCategory.Config, $"Lightning triggered at ({_lightningCenter.X:F2}, {_lightningCenter.Y:F2}) with {totalStrokes} stroke(s)");

        TriggerStroke(isFollowUp: false);
    }

    private void TriggerStroke(bool isFollowUp)
    {
        _isLightningActive = true;
        _strokesRemaining--;  // Consume this stroke

        // Randomize stroke duration based on config range
        float durationRange = _config.LightningStrokeDurationMax - _config.LightningStrokeDurationMin;
        float durationMult = _config.LightningStrokeDurationMin + (float)_random.NextDouble() * durationRange;
        _currentStrokeDuration = _config.LightningDuration * durationMult;
        _lightningFlashTimer = _currentStrokeDuration;

        // Follow-up strokes are usually dimmer
        if (isFollowUp)
        {
            float intensityRange = _config.LightningFollowUpIntensityMax - _config.LightningFollowUpIntensityMin;
            _currentStrokeIntensity = _config.LightningFollowUpIntensityMin + (float)_random.NextDouble() * intensityRange;

            // Drift position slightly for follow-up strokes
            float drift = _config.LightningPositionDrift;
            _lightningCenter += new Vector2(
                ((float)_random.NextDouble() - 0.5f) * drift,
                ((float)_random.NextDouble() - 0.5f) * drift * 0.5f
            );
            _lightningCenter = new Vector2(
                Mathf.Clamp(_lightningCenter.X, 0.1f, 0.9f),
                Mathf.Clamp(_lightningCenter.Y, 0.5f, 0.95f)
            );
        }
        else
        {
            float intensityRange = _config.LightningFirstStrokeIntensityMax - _config.LightningFirstStrokeIntensityMin;
            _currentStrokeIntensity = _config.LightningFirstStrokeIntensityMin + (float)_random.NextDouble() * intensityRange;
        }

        // Random flash pattern
        _currentFlashPattern = _random.Next(4);
    }

    private float CalculateFlashIntensity(float progress, int pattern)
    {
        // Different flash patterns for variety
        return pattern switch
        {
            0 => CalculateFlashPattern0(progress), // Classic: quick flash, dip, secondary, fade
            1 => CalculateFlashPattern1(progress), // Sharp: instant on, quick fade
            2 => CalculateFlashPattern2(progress), // Flicker: multiple rapid peaks
            3 => CalculateFlashPattern3(progress), // Rolling: slow build, sustained, slow fade
            _ => CalculateFlashPattern0(progress)
        };
    }

    // Classic pattern: quick flash, dip, secondary flash, fade
    private static float CalculateFlashPattern0(float progress)
    {
        if (progress < 0.1f)
            return Mathf.Lerp(0f, 1f, progress / 0.1f);
        else if (progress < 0.2f)
            return Mathf.Lerp(1f, 0.3f, (progress - 0.1f) / 0.1f);
        else if (progress < 0.35f)
            return Mathf.Lerp(0.3f, 0.7f, (progress - 0.2f) / 0.15f);
        else
            return Mathf.Lerp(0.7f, 0f, (progress - 0.35f) / 0.65f);
    }

    // Sharp pattern: instant on, quick exponential fade
    private static float CalculateFlashPattern1(float progress)
    {
        if (progress < 0.05f)
            return Mathf.Lerp(0f, 1f, progress / 0.05f);
        else
            return Mathf.Pow(1f - (progress - 0.05f) / 0.95f, 2f);
    }

    // Flicker pattern: multiple rapid peaks
    private static float CalculateFlashPattern2(float progress)
    {
        if (progress < 0.08f)
            return Mathf.Lerp(0f, 1f, progress / 0.08f);
        else if (progress < 0.15f)
            return Mathf.Lerp(1f, 0.2f, (progress - 0.08f) / 0.07f);
        else if (progress < 0.22f)
            return Mathf.Lerp(0.2f, 0.8f, (progress - 0.15f) / 0.07f);
        else if (progress < 0.30f)
            return Mathf.Lerp(0.8f, 0.15f, (progress - 0.22f) / 0.08f);
        else if (progress < 0.40f)
            return Mathf.Lerp(0.15f, 0.5f, (progress - 0.30f) / 0.10f);
        else
            return Mathf.Lerp(0.5f, 0f, (progress - 0.40f) / 0.60f);
    }

    // Rolling pattern: slow build, sustained bright, slow fade
    private static float CalculateFlashPattern3(float progress)
    {
        if (progress < 0.25f)
            return Mathf.Lerp(0f, 0.9f, progress / 0.25f);
        else if (progress < 0.5f)
            return 0.8f + Mathf.Sin((progress - 0.25f) * 25f) * 0.15f; // Sustained with slight flicker
        else
            return Mathf.Lerp(0.85f, 0f, (progress - 0.5f) / 0.5f);
    }

    private float GetRandomLightningInterval()
    {
        // Base interval from config, with configurable randomization
        float baseInterval = _config.LightningFrequency;
        float variation = _config.LightningIntervalVariation;
        float minMult = 1f - variation * 0.5f; // e.g., variation=1.5 gives 0.25-1.75
        float multiplier = minMult + (float)_random.NextDouble() * variation;
        return baseInterval * Mathf.Max(0.1f, multiplier);
    }

    public override void _ExitTree()
    {
        ClearEffects();
    }
}
