using Godot;

namespace Nightwalk.Godot.Resonance;

/// <summary>
/// Visual marker for a decoy ping location.
/// Shows where the player created a lure for Seekers.
/// </summary>
public sealed partial class DecoyPingMarker : Node3D
{
    // Decoy collision layer (layer 17, bit 16)
    public const uint DecoyCollisionLayer = 1u << 16;

    private static readonly Color BaseColor = new(1f, 0.5f, 0.1f); // Dark orange
    private static readonly Color HighlightColor = new(1f, 0.8f, 0.3f); // Brighter orange

    private MeshInstance3D? _ring;
    private MeshInstance3D? _pillar;
    private StandardMaterial3D? _material;
    private OmniLight3D? _light;
    private StaticBody3D? _collider;

    private float _lifetime;
    private float _maxLifetime;
    private float _pulseTime;
    private float _rotationAngle;
    private bool _isHighlighted;

    /// <summary>
    /// Whether this decoy has expired.
    /// </summary>
    public bool IsExpired => _lifetime >= _maxLifetime;

    /// <summary>
    /// Event fired when the decoy expires naturally.
    /// </summary>
    public event System.Action<DecoyPingMarker>? OnExpired;

    public void Initialize(float duration)
    {
        _maxLifetime = duration;
        _lifetime = 0f;

        CreateVisuals();
        CreateCollider();
    }

    private void CreateVisuals()
    {
        // Dark orange glow material
        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(BaseColor.R, BaseColor.G, BaseColor.B, 0.8f),
            EmissionEnabled = true,
            Emission = BaseColor,
            EmissionEnergyMultiplier = 2f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        // Ground ring
        var ringMesh = new TorusMesh
        {
            InnerRadius = 0.8f,
            OuterRadius = 1f,
            Rings = 32,
            RingSegments = 8
        };

        _ring = new MeshInstance3D
        {
            Name = "Ring",
            Mesh = ringMesh,
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = new Vector3(90, 0, 0)
        };
        AddChild(_ring);

        // Vertical pillar/beam
        var pillarMesh = new CylinderMesh
        {
            TopRadius = 0.05f,
            BottomRadius = 0.1f,
            Height = 5f,
            RadialSegments = 8
        };

        _pillar = new MeshInstance3D
        {
            Name = "Pillar",
            Mesh = pillarMesh,
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 2.5f, 0)
        };
        AddChild(_pillar);

        // Point light
        _light = new OmniLight3D
        {
            Name = "Light",
            LightColor = BaseColor,
            LightEnergy = 2f,
            OmniRange = 5f,
            ShadowEnabled = false,
            Position = new Vector3(0, 1f, 0)
        };
        AddChild(_light);
    }

    private void CreateCollider()
    {
        // Create collision body for raycast detection
        _collider = new StaticBody3D
        {
            Name = "Collider",
            CollisionLayer = DecoyCollisionLayer,
            CollisionMask = 0 // Don't collide with anything
        };

        var shape = new SphereShape3D { Radius = 1.2f };
        var collisionShape = new CollisionShape3D
        {
            Name = "Shape",
            Shape = shape,
            Position = new Vector3(0, 1f, 0)
        };
        _collider.AddChild(collisionShape);

        // Store reference to this marker on the collider
        _collider.SetMeta("decoy_marker", this);

        AddChild(_collider);
    }

    /// <summary>
    /// Sets the highlighted state (when hovered by DecoyTool).
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        _isHighlighted = highlighted;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var color = _isHighlighted ? HighlightColor : BaseColor;

        if (_material != null && GodotObject.IsInstanceValid(_material))
        {
            _material.Emission = color;
        }

        if (_light != null && GodotObject.IsInstanceValid(_light))
        {
            _light.LightColor = color;
        }
    }

    public override void _Process(double delta)
    {
        _lifetime += (float)delta;
        _pulseTime += (float)delta * 4f;

        // Fade out as lifetime expires
        float remaining = 1f - (_lifetime / _maxLifetime);
        float pulse = 0.7f + 0.3f * Mathf.Sin(_pulseTime * Mathf.Tau);

        var baseColor = _isHighlighted ? HighlightColor : BaseColor;

        if (_material != null && GodotObject.IsInstanceValid(_material))
        {
            var color = new Color(baseColor.R, baseColor.G, baseColor.B, remaining * pulse * 0.8f);
            _material.AlbedoColor = color;
            _material.EmissionEnergyMultiplier = remaining * pulse * 2f;
        }

        if (_light != null && GodotObject.IsInstanceValid(_light))
        {
            _light.LightEnergy = remaining * pulse * 2f;
        }

        // Rotate ring (time-based, not frame-rate dependent)
        _rotationAngle += (float)delta * 2f;
        if (_ring != null)
        {
            _ring.Rotation = new Vector3(90f * Mathf.Pi / 180f, _rotationAngle, 0);
        }

        // Notify on expiry (don't self-destruct - let owner handle it)
        if (_lifetime >= _maxLifetime)
        {
            OnExpired?.Invoke(this);
        }
    }

    /// <summary>
    /// Finds the DecoyPingMarker from a collider hit.
    /// </summary>
    public static DecoyPingMarker? FindFromCollider(GodotObject? collider)
    {
        if (collider is Node node && node.HasMeta("decoy_marker"))
        {
            return node.GetMeta("decoy_marker").As<DecoyPingMarker>();
        }
        return null;
    }
}
