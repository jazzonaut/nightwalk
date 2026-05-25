using Godot;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Resonance;

/// <summary>
/// Procedurally generates the Seeker's visual representation.
/// An armillary sphere with offset rotating rings around a dark core.
/// </summary>
public sealed partial class SeekerRenderer : Node3D
{
    private SeekerConfig? _config;

    // Mesh instances
    private MeshInstance3D? _core;
    private MeshInstance3D? _ring1;
    private MeshInstance3D? _ring2;
    private MeshInstance3D? _ring3;
    private MeshInstance3D? _iris;

    // Materials
    private StandardMaterial3D? _coreMaterial;
    private StandardMaterial3D? _ringMaterial;
    private StandardMaterial3D? _alertRingMaterial;

    private bool _isAlert;

    public void Initialize(SeekerConfig config)
    {
        _config = config;

        CreateMaterials();
        CreateCore();
        CreateRings();
    }

    private void CreateMaterials()
    {
        if (_config == null) return;

        // Dark core material
        _coreMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.05f, 0.02f, 0.05f),
            Metallic = 0.9f,
            Roughness = 0.1f,
            EmissionEnabled = true,
            Emission = new Color(0.1f, 0f, 0.1f),
            EmissionEnergyMultiplier = 0.5f
        };

        // Ring material (purple glow)
        _ringMaterial = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml(_config.RingColor),
            Metallic = 0.8f,
            Roughness = 0.2f,
            EmissionEnabled = true,
            Emission = Color.FromHtml(_config.RingColor),
            EmissionEnergyMultiplier = 2f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        // Alert ring material (red glow when detecting)
        _alertRingMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.2f, 0.2f),
            Metallic = 0.8f,
            Roughness = 0.2f,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.2f, 0.2f),
            EmissionEnergyMultiplier = 4f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
    }

    private void CreateCore()
    {
        if (_config == null) return;

        float scale = _config.Scale;

        // Dark sphere core
        var coreMesh = new SphereMesh
        {
            Radius = scale * 0.15f,
            Height = scale * 0.3f,
            RadialSegments = 16,
            Rings = 8
        };

        _core = new MeshInstance3D
        {
            Name = "Core",
            Mesh = coreMesh,
            MaterialOverride = _coreMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };

        AddChild(_core);

        // Iris/eye in the center
        var irisMesh = new SphereMesh
        {
            Radius = scale * 0.08f,
            Height = scale * 0.16f,
            RadialSegments = 12,
            Rings = 6
        };

        var irisMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0f, 0.3f),
            EmissionEnergyMultiplier = 3f
        };

        _iris = new MeshInstance3D
        {
            Name = "Iris",
            Mesh = irisMesh,
            MaterialOverride = irisMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0, scale * 0.1f) // Facing forward/down
        };

        AddChild(_iris);
    }

    private void CreateRings()
    {
        if (_config == null) return;

        float scale = _config.Scale;

        // Create three offset rings at different angles
        _ring1 = CreateRing(scale * 0.4f, scale * 0.02f);
        _ring1.Name = "Ring1";
        _ring1.RotationDegrees = new Vector3(20, 0, 0);
        AddChild(_ring1);

        _ring2 = CreateRing(scale * 0.35f, scale * 0.015f);
        _ring2.Name = "Ring2";
        _ring2.RotationDegrees = new Vector3(-15, 60, 10);
        AddChild(_ring2);

        _ring3 = CreateRing(scale * 0.45f, scale * 0.02f);
        _ring3.Name = "Ring3";
        _ring3.RotationDegrees = new Vector3(5, 120, -20);
        AddChild(_ring3);
    }

    private MeshInstance3D CreateRing(float radius, float thickness)
    {
        var torusMesh = new TorusMesh
        {
            InnerRadius = radius - thickness,
            OuterRadius = radius + thickness,
            Rings = 24,
            RingSegments = 12
        };

        return new MeshInstance3D
        {
            Mesh = torusMesh,
            MaterialOverride = _ringMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
    }

    /// <summary>
    /// Updates ring rotations based on the given time-based angle.
    /// Each ring rotates on a different axis at different speeds for visual interest.
    /// </summary>
    public void SetRotation(float angle)
    {
        // Use the angle parameter for time-based rotation (multiplied by different factors per ring)
        if (_ring1 != null)
            _ring1.Rotation = new Vector3(_ring1.Rotation.X, angle * 1.0f, _ring1.Rotation.Z);
        if (_ring2 != null)
            _ring2.Rotation = new Vector3(angle * 0.7f, _ring2.Rotation.Y, _ring2.Rotation.Z);
        if (_ring3 != null)
            _ring3.Rotation = new Vector3(_ring3.Rotation.X, _ring3.Rotation.Y, angle * 0.5f);
    }

    /// <summary>
    /// Sets rings to alert color (detecting player).
    /// </summary>
    public void SetAlertColor()
    {
        if (_isAlert) return;
        _isAlert = true;

        _ring1?.SetMaterialOverride(_alertRingMaterial);
        _ring2?.SetMaterialOverride(_alertRingMaterial);
        _ring3?.SetMaterialOverride(_alertRingMaterial);
    }

    /// <summary>
    /// Sets rings to normal color.
    /// </summary>
    public void SetNormalColor()
    {
        if (!_isAlert) return;
        _isAlert = false;

        _ring1?.SetMaterialOverride(_ringMaterial);
        _ring2?.SetMaterialOverride(_ringMaterial);
        _ring3?.SetMaterialOverride(_ringMaterial);
    }
}
