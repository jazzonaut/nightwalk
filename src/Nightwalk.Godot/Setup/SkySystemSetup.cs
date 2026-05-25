using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Godot.Rendering.Sky;

namespace Nightwalk.Godot.Setup;

/// <summary>
/// Result of sky system setup.
/// </summary>
public sealed class SkySystemResult
{
    public SkyDomeManager? SkyDome { get; init; }
    public DistantCitySilhouette? CitySilhouette { get; init; }

    /// <summary>
    /// Sets the node that sky elements should follow (typically the player).
    /// </summary>
    public void SetFollowTarget(Node3D target)
    {
        SkyDome?.SetFollowTarget(target);
        CitySilhouette?.SetFollowTarget(target);
    }

    /// <summary>
    /// Reconfigures all sky effects from updated config (hot-reload support).
    /// </summary>
    public void Configure(SkyEffectsConfig config)
    {
        SkyDome?.Configure(config);
        CitySilhouette?.Configure(config);
    }

    public void Cleanup()
    {
        SkyDome?.QueueFree();
        CitySilhouette?.QueueFree();
    }
}

/// <summary>
/// Configures the animated sky effects system (aurora, lightning, light pollution, electrical atmosphere).
/// </summary>
public static class SkySystemSetup
{
    /// <summary>
    /// Creates and configures the sky dome effects system.
    /// </summary>
    /// <param name="parent">The parent node to add the sky dome to.</param>
    /// <param name="config">Sky effects configuration.</param>
    /// <returns>The setup result containing the sky dome manager.</returns>
    public static SkySystemResult Configure(Node parent, SkyEffectsConfig config)
    {
        SkyDomeManager? skyDome = null;
        DistantCitySilhouette? silhouette = null;

        if (config.Enabled)
        {
            skyDome = SkyDomeManager.Create(config);
            parent.AddChild(skyDome);
            Log.Info(LogCategory.Config, "Sky dome effects initialized");
        }

        // Create distant city silhouettes if enabled (horizon masking)
        if (config.SilhouettesEnabled)
        {
            silhouette = DistantCitySilhouette.Create(config);
            parent.AddChild(silhouette);
            Log.Info(LogCategory.Config, "Distant city silhouettes initialized");
        }

        return new SkySystemResult
        {
            SkyDome = skyDome,
            CitySilhouette = silhouette
        };
    }
}
