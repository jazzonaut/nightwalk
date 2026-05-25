using Godot;
using Nightwalk.Core.Config;

namespace Nightwalk.Godot.Ziplines;

/// <summary>
/// Visual representation of a zipline anchor point.
/// </summary>
public sealed partial class ZiplineAnchor : Node3D
{
    private MeshInstance3D? _mesh;
    private OmniLight3D? _glow;
    private StandardMaterial3D? _material;
    private Color _originalColor;

    /// <summary>
    /// Configures the anchor visual.
    /// </summary>
    /// <param name="config">Zipline configuration.</param>
    public void Configure(ZiplineConfig config)
    {
        // Create glowing sphere marker
        _mesh = new MeshInstance3D { Name = "AnchorMesh" };
        var sphere = new SphereMesh { Radius = config.AnchorRadius };
        _mesh.Mesh = sphere;

        _originalColor = Color.FromHtml(config.AnchorColor);
        _material = new StandardMaterial3D
        {
            AlbedoColor = _originalColor,
            EmissionEnabled = true,
            Emission = _originalColor,
            EmissionEnergyMultiplier = config.AnchorEmission,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _mesh.MaterialOverride = _material;
        _mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_mesh);

        // Add point light for glow effect
        _glow = new OmniLight3D
        {
            Name = "AnchorGlow",
            LightColor = _originalColor,
            LightEnergy = config.AnchorLightEnergy,
            OmniRange = config.AnchorLightRange,
            OmniAttenuation = 2.0f
        };
        AddChild(_glow);
    }

    /// <summary>
    /// Sets the anchor to highlighted state with the specified color.
    /// </summary>
    public void SetHighlighted(Color highlightColor)
    {
        if (_material != null)
        {
            _material.AlbedoColor = highlightColor;
            _material.Emission = highlightColor;
        }
        if (_glow != null)
        {
            _glow.LightColor = highlightColor;
        }
    }

    /// <summary>
    /// Restores the anchor to its original color.
    /// </summary>
    public void ClearHighlight()
    {
        if (_material != null)
        {
            _material.AlbedoColor = _originalColor;
            _material.Emission = _originalColor;
        }
        if (_glow != null)
        {
            _glow.LightColor = _originalColor;
        }
    }
}
