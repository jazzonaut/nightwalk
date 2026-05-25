using Godot;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Rendering.Effects;

/// <summary>
/// Smoke effect using GPUParticles3D for realistic rising smoke.
/// </summary>
public sealed partial class SmokeEmitter : Node3D
{
    private static ParticleProcessMaterial? _sharedMaterial;
    private static ShaderMaterial? _sharedMeshMaterial;
    private static Shader? _smokeShader;
    private static float _baseParticleSize;

    /// <summary>
    /// Clears cached resources to force recreation on next use. Call on config hot-reload.
    /// </summary>
    public static void ClearCache()
    {
        _sharedMaterial = null;
        _sharedMeshMaterial = null;
        // Keep shader - it's loaded from file, not config-dependent
    }

    /// <summary>
    /// Creates a smoke emitter at the specified position.
    /// </summary>
    public static SmokeEmitter Create(Vector3 position, SmokeConfig config, float emitterScale = 1.0f)
    {
        var emitter = new SmokeEmitter();
        emitter.Position = position;
        emitter.Initialize(config, emitterScale);
        return emitter;
    }

    private void Initialize(SmokeConfig config, float emitterScale)
    {
        Name = "SmokeEmitter";

        EnsureSharedResources(config);

        // Create per-instance mesh with scaled particle size (billboards don't inherit node scale)
        float scaledSize = _baseParticleSize * emitterScale;
        var mesh = new QuadMesh
        {
            Size = new Vector2(scaledSize, scaledSize)
        };
        mesh.SurfaceSetMaterial(0, _sharedMeshMaterial);

        // Scale visibility bounds
        float aabbScale = Mathf.Max(1.0f, emitterScale);
        var aabbSize = new Vector3(10 * aabbScale, 60 * emitterScale, 10 * aabbScale);

        // Scale lifetime so smaller smoke is shorter, larger smoke is taller
        float scaledLifetime = config.Lifetime * emitterScale;

        // Scale particle count to maintain similar visual density across scales
        int scaledAmount = Mathf.Max(10, (int)(config.ParticleCount * emitterScale));

        var particles = new GpuParticles3D
        {
            Amount = scaledAmount,
            Lifetime = scaledLifetime,
            Preprocess = scaledLifetime / 3.0,
            Explosiveness = 0.0f,
            Randomness = 0.3f,
            ProcessMaterial = _sharedMaterial,
            DrawPass1 = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TransformAlign = GpuParticles3D.TransformAlignEnum.Disabled,
            Position = new Vector3(0, 0.1f * emitterScale, 0),
            VisibilityAabb = new Aabb(new Vector3(-5 * aabbScale, 0, -5 * aabbScale), aabbSize),

            // LOD - stop processing particles at distance (shader handles visual fade)
            VisibilityRangeEnd = config.FadeDistanceMax
        };

        // Uniform scale affects emission sphere and particle movement in local space
        particles.Scale = new Vector3(emitterScale, emitterScale, emitterScale);

        AddChild(particles);
    }

    private static void EnsureSharedResources(SmokeConfig config)
    {
        if (_sharedMaterial != null) return;

        var albedo = Color.FromHtml(config.AlbedoColor);

        // Scale curve - start small, grow larger as they rise
        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0, 0.3f));
        scaleCurve.AddPoint(new Vector2(0.3f, 0.8f));
        scaleCurve.AddPoint(new Vector2(0.6f, 1.2f));
        scaleCurve.AddPoint(new Vector2(1, 1.8f));
        var scaleCurveTexture = new CurveTexture { Curve = scaleCurve };

        // Alpha - small delay to clear ground, then gradual fade in
        var alphaCurve = new Curve();
        alphaCurve.AddPoint(new Vector2(0, 0));         // Start invisible
        alphaCurve.AddPoint(new Vector2(0.01f, 0));    // Brief delay to clear ground
        alphaCurve.AddPoint(new Vector2(0.05f, 0.06f)); // Fade in more gradually
        alphaCurve.AddPoint(new Vector2(0.12f, 0.12f)); // Peak later
        alphaCurve.AddPoint(new Vector2(0.3f, 0.10f));
        alphaCurve.AddPoint(new Vector2(0.6f, 0.06f));
        alphaCurve.AddPoint(new Vector2(1, 0));
        var alphaCurveTexture = new CurveTexture { Curve = alphaCurve };

        _sharedMaterial = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.1f,

            Direction = new Vector3(0, 1, 0),
            Spread = config.Spread,
            InitialVelocityMin = config.VelocityMin,
            InitialVelocityMax = config.VelocityMax,

            Gravity = new Vector3(0, config.Gravity, 0),

            TurbulenceEnabled = true,
            TurbulenceNoiseStrength = config.TurbulenceStrength,
            TurbulenceNoiseScale = config.TurbulenceScale,
            TurbulenceNoiseSpeed = new Vector3(0.09f, 0.15f, 0.09f),
            TurbulenceInfluenceOverLife = alphaCurveTexture,

            ScaleMin = config.ScaleMin,
            ScaleMax = config.ScaleMax,
            ScaleCurve = scaleCurveTexture,

            AlphaCurve = alphaCurveTexture,

            AngleMin = 0f,
            AngleMax = 360f,
            AngularVelocityMin = -5f,
            AngularVelocityMax = 5f
        };

        var smokeTexture = CreateCloudTexture();

        // Store base size for per-instance scaling
        _baseParticleSize = config.ParticleSize;

        EnsureSmokeShader();

        _sharedMeshMaterial = new ShaderMaterial
        {
            Shader = _smokeShader
        };
        _sharedMeshMaterial.SetShaderParameter("smoke_texture", smokeTexture);
        _sharedMeshMaterial.SetShaderParameter("smoke_color", new Vector3(albedo.R, albedo.G, albedo.B));
        _sharedMeshMaterial.SetShaderParameter("fade_distance_min", config.FadeDistanceMin);
        _sharedMeshMaterial.SetShaderParameter("fade_distance_max", config.FadeDistanceMax);
    }

    private static void EnsureSmokeShader()
    {
        if (_smokeShader != null) return;

        _smokeShader = GD.Load<Shader>("res://src/Nightwalk.Godot/Rendering/Shaders/smoke.gdshader");
    }

    /// <summary>
    /// Creates a cloud-like noise texture for smoke particles.
    /// </summary>
    private static Texture2D CreateCloudTexture()
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = 0.02f,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 2,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f
        };

        var image = Image.CreateEmpty(128, 128, false, Image.Format.Rgba8);

        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                // Distance from center (0-1)
                float dx = (x - 64) / 64.0f;
                float dy = (y - 64) / 64.0f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Radial falloff - soft edge
                float radial = Mathf.Clamp(1.0f - dist, 0, 1);
                radial = radial * radial * (3 - 2 * radial); // Smoothstep

                // Sample noise for subtle variation
                float n = noise.GetNoise2D(x, y);
                n = (n + 1) * 0.5f; // Normalize to 0-1

                // Mostly radial, with 60% noise influence
                float alpha = radial * (0.3f + n * 0.7f);
                alpha = Mathf.Clamp(alpha, 0, 1);

                image.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }
}
