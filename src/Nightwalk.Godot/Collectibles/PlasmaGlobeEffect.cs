using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Collectibles;

/// <summary>
/// Creates a plasma globe effect - bolts dance and drift from center outward.
/// </summary>
public sealed partial class PlasmaGlobeEffect : Node3D
{
    private class BoltData
    {
        // Current direction (animated)
        public Vector3 Direction;
        public float Length;

        // Animation parameters
        public float DriftSpeedTheta;  // Horizontal drift
        public float DriftSpeedPhi;    // Vertical drift
        public float Phase;            // Offset for sine waves

        // Jagged path noise
        public float NoiseOffset;
        public float NoiseSpeed;

        public BoltData(Vector3 direction, float length, Random random)
        {
            Direction = direction;
            Length = length;

            // Random drift speeds (slow, organic movement)
            DriftSpeedTheta = ((float)random.NextDouble() - 0.5f) * 2f;
            DriftSpeedPhi = ((float)random.NextDouble() - 0.5f) * 1.5f;
            Phase = (float)random.NextDouble() * Mathf.Tau;
            NoiseOffset = (float)random.NextDouble() * 1000f;
            NoiseSpeed = 3f + (float)random.NextDouble() * 2f;
        }
    }

    private readonly List<BoltData> _bolts = new();
    private readonly Color _color;
    private readonly float _duration;
    private readonly float _thickness;
    private readonly float _baseAlpha;
    private readonly float _growDuration;
    private readonly float _maxLength;
    private readonly Action? _onComplete;

    private MeshInstance3D? _meshInstance;
    private StandardMaterial3D? _material;
    private ImmediateMesh? _mesh;

    private float _elapsed;
    private float _time;

    private PlasmaGlobeEffect(CollectiblesConfig config, Color color, Action? onComplete)
    {
        _color = color;
        _duration = config.ParticleDuration;
        _thickness = config.EffectThickness;
        _baseAlpha = config.EffectAlpha;
        _growDuration = config.EffectGrowDuration;
        _maxLength = config.ParticleSpeed;
        _onComplete = onComplete;
    }

    public static PlasmaGlobeEffect Create(CollectiblesConfig config, Color color, Action? onComplete = null)
    {
        var effect = new PlasmaGlobeEffect(config, color, onComplete)
        {
            Name = "PlasmaGlobe"
        };

        int boltCount = Math.Min(config.ParticleCount, 24);
        float maxLength = config.ParticleSpeed;

        var random = new Random();

        for (int i = 0; i < boltCount; i++)
        {
            // Start with random directions on sphere
            float theta = (float)(random.NextDouble() * Math.PI * 2);
            float phi = (float)(Math.Acos(2 * random.NextDouble() - 1));

            var direction = new Vector3(
                Mathf.Sin(phi) * Mathf.Cos(theta),
                Mathf.Sin(phi) * Mathf.Sin(theta),
                Mathf.Cos(phi)
            ).Normalized();

            float length = maxLength * (0.7f + (float)random.NextDouble() * 0.3f);

            effect._bolts.Add(new BoltData(direction, length, random));
        }

        // Single mesh for all bolts
        effect._material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        effect._mesh = new ImmediateMesh();
        effect._meshInstance = new MeshInstance3D
        {
            Mesh = effect._mesh,
            MaterialOverride = effect._material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        effect.AddChild(effect._meshInstance);

        return effect;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _elapsed += dt;
        _time += dt;

        float progress = _elapsed / _duration;

        if (progress >= 1f)
        {
            _onComplete?.Invoke();
            QueueFree();
            return;
        }

        // Growth: bolts extend outward
        float growth = Mathf.Min(1f, _elapsed / _growDuration);
        growth = Mathf.SmoothStep(0f, 1f, growth);

        // Animate bolt directions (plasma drift)
        foreach (var bolt in _bolts)
        {
            // Convert current direction to spherical coords
            float theta = Mathf.Atan2(bolt.Direction.Y, bolt.Direction.X);
            float phi = Mathf.Acos(bolt.Direction.Z);

            // Drift with sine waves for organic movement
            theta += bolt.DriftSpeedTheta * dt;
            phi += Mathf.Sin(_time * 1.5f + bolt.Phase) * bolt.DriftSpeedPhi * dt;

            // Clamp phi to valid range
            phi = Mathf.Clamp(phi, 0.1f, Mathf.Pi - 0.1f);

            // Convert back to direction
            bolt.Direction = new Vector3(
                Mathf.Sin(phi) * Mathf.Cos(theta),
                Mathf.Sin(phi) * Mathf.Sin(theta),
                Mathf.Cos(phi)
            ).Normalized();
        }

        // Fade out
        float fadeStart = 0.5f;
        float alpha;
        if (progress < fadeStart)
        {
            alpha = _baseAlpha;
        }
        else
        {
            float fadeProgress = (progress - fadeStart) / (1f - fadeStart);
            alpha = _baseAlpha * (1f - fadeProgress * fadeProgress);
        }

        // Update material
        if (_material != null)
        {
            var color = _color;
            color.A = alpha;
            _material.AlbedoColor = color;
            _material.EmissionEnergyMultiplier = 8f * (alpha / _baseAlpha);
        }

        RebuildMesh(growth);
    }

    private void RebuildMesh(float growth)
    {
        if (_mesh == null) return;

        _mesh.ClearSurfaces();
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

        foreach (var bolt in _bolts)
        {
            float currentLength = bolt.Length * growth;
            if (currentLength < 0.01f) continue;

            DrawBolt(bolt, currentLength);
        }

        _mesh.SurfaceEnd();
    }

    private void DrawBolt(BoltData bolt, float currentLength)
    {
        const int segments = 8;

        // Find perpendicular vectors for thickness and jaggedness
        Vector3 perp1 = bolt.Direction.Cross(Vector3.Up);
        if (perp1.LengthSquared() < 0.01f)
            perp1 = bolt.Direction.Cross(Vector3.Right);
        perp1 = perp1.Normalized();
        Vector3 perp2 = bolt.Direction.Cross(perp1).Normalized();

        Vector3? prevPoint = null;
        float noiseTime = _time * bolt.NoiseSpeed + bolt.NoiseOffset;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float dist = t * currentLength;

            // Base position along bolt direction
            Vector3 point = bolt.Direction * dist;

            // Add animated jaggedness (more in middle, less at ends)
            float jaggedAmount = currentLength * 0.15f;
            float edgeFalloff = 4f * t * (1f - t); // 0 at ends, 1 in middle

            // Use sine waves with different frequencies for organic movement
            float noiseX = Mathf.Sin(noiseTime + t * 5f) * Mathf.Cos(noiseTime * 0.7f + t * 3f);
            float noiseY = Mathf.Sin(noiseTime * 1.3f + t * 4f) * Mathf.Cos(noiseTime * 0.5f + t * 6f);

            point += (perp1 * noiseX + perp2 * noiseY) * jaggedAmount * edgeFalloff;

            if (prevPoint.HasValue)
            {
                // Taper thickness towards end
                float taper = 1f - t * 0.7f;
                float thick = _thickness * taper;

                AddSegment(prevPoint.Value, point, perp1 * thick, perp2 * thick);
            }

            prevPoint = point;
        }
    }

    private void AddSegment(Vector3 start, Vector3 end, Vector3 perp1, Vector3 perp2)
    {
        if (_mesh == null) return;

        // Cross-shaped ribbons
        _mesh.SurfaceAddVertex(start + perp1);
        _mesh.SurfaceAddVertex(end + perp1);
        _mesh.SurfaceAddVertex(start - perp1);

        _mesh.SurfaceAddVertex(start - perp1);
        _mesh.SurfaceAddVertex(end + perp1);
        _mesh.SurfaceAddVertex(end - perp1);

        _mesh.SurfaceAddVertex(start + perp2);
        _mesh.SurfaceAddVertex(end + perp2);
        _mesh.SurfaceAddVertex(start - perp2);

        _mesh.SurfaceAddVertex(start - perp2);
        _mesh.SurfaceAddVertex(end + perp2);
        _mesh.SurfaceAddVertex(end - perp2);
    }

    public override void _ExitTree()
    {
        _material?.Dispose();
    }
}
