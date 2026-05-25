using Godot;

namespace Nightwalk.Godot.Player.Tools;

/// <summary>
/// Base class for passive light-emitting tools.
/// </summary>
public abstract partial class BaseLightTool : BaseTool
{
    /// <summary>
    /// The light node for this tool.
    /// </summary>
    protected Light3D? Light { get; private set; }

    /// <summary>
    /// Creates the light node for this tool.
    /// </summary>
    /// <returns>The configured light node.</returns>
    protected abstract Light3D CreateLight();

    /// <summary>
    /// Updates the light properties from current config. Override to apply config values.
    /// </summary>
    protected virtual void UpdateLight(Light3D light) { }

    /// <summary>
    /// Reconfigures the tool from current config. Call on hot reload.
    /// </summary>
    public void Reconfigure()
    {
        if (Light != null) UpdateLight(Light);
    }

    /// <inheritdoc/>
    protected override void OnInitialize()
    {
        Light = CreateLight();
        AddChild(Light);
        Light.Visible = false;
    }

    /// <inheritdoc/>
    public override void Enter()
    {
        base.Enter();
        if (Light != null) Light.Visible = true;
    }

    /// <inheritdoc/>
    public override void Exit()
    {
        base.Exit();
        if (Light != null) Light.Visible = false;
    }
}
