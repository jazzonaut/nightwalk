using Godot;
using Nightwalk.Core.Tools;

namespace Nightwalk.Godot.Player.Tools;

/// <summary>
/// Spotlight tool with focused directional beam.
/// </summary>
public sealed partial class SpotlightTool : BaseLightTool
{
    /// <inheritdoc/>
    public override string Id => ToolIds.Spotlight;

    /// <inheritdoc/>
    public override string DisplayName => "Spotlight";

    /// <inheritdoc/>
    protected override Light3D CreateLight()
    {
        var light = new SpotLight3D { Name = "SpotlightLight" };
        UpdateLight(light);
        return light;
    }

    /// <inheritdoc/>
    protected override void UpdateLight(Light3D light)
    {
        if (light is not SpotLight3D spot) return;
        var config = Context.Data.Tools.Spotlight;
        spot.LightColor = Color.FromHtml(config.Color);
        spot.LightEnergy = config.Energy;
        spot.SpotRange = config.Range;
        spot.SpotAngle = config.SpotAngle;
        spot.SpotAttenuation = config.Attenuation;
        spot.Position = new Vector3(config.OffsetX, config.OffsetY, config.OffsetZ);
    }
}
