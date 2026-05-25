using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Godot.Rendering.Materials;

namespace Nightwalk.Godot.Setup;

/// <summary>
/// Configures the world environment (sky, fog, lighting, etc.)
/// </summary>
public static class EnvironmentSetup
{
    /// <summary>
    /// Configures viewport anti-aliasing and world environment.
    /// </summary>
    /// <param name="root">The root node to find WorldEnvironment in.</param>
    /// <param name="config">Graphics configuration.</param>
    /// <returns>The configured WorldEnvironment, or null if not found.</returns>
    public static WorldEnvironment? Configure(Node root, GraphicsConfig config)
    {
        UpdateViewport(root.GetViewport(), config);
        return ConfigureWorldEnvironment(root, config);
    }

    /// <summary>
    /// Updates an existing WorldEnvironment with new config values. Call on hot reload.
    /// </summary>
    public static void UpdateEnvironment(WorldEnvironment? worldEnvironment, GraphicsConfig config)
    {
        if (worldEnvironment == null) return;

        var env = new global::Godot.Environment();

        ConfigureSky(env, config);
        ConfigureAmbientLight(env, config);
        ConfigureFog(env, config);
        ConfigureVolumetricFog(env, config);
        ConfigureBloom(env, config);
        ConfigureTonemap(env, config);
        ConfigureSsao(env, config);
        ConfigureSsr(env, config);
        ConfigureSsil(env, config);

        worldEnvironment.Environment = env;
    }

    /// <summary>
    /// Applies window settings immediately. Call as early as possible after config loads.
    /// </summary>
    public static void ApplyWindowSettings(GraphicsConfig gfx)
    {
        ApplyWindowMode(gfx.WindowMode, gfx.Resolution);
    }

    /// <summary>
    /// Updates viewport settings (VSync, AA, scaling, VRS). Call on hot reload.
    /// Window mode is handled separately by ApplyWindowSettings.
    /// </summary>
    public static void UpdateViewport(Viewport viewport, GraphicsConfig gfx)
    {
        // VSync
        DisplayServer.WindowSetVsyncMode(gfx.VSync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);

        // 3D Resolution Scaling (FSR)
        viewport.Scaling3DMode = gfx.UpscalingMode switch
        {
            UpscalingMode.Fsr2 => Viewport.Scaling3DModeEnum.Fsr2,
            UpscalingMode.Fsr1 => Viewport.Scaling3DModeEnum.Fsr,
            _ => Viewport.Scaling3DModeEnum.Bilinear
        };
        viewport.Scaling3DScale = gfx.RenderScale;
        viewport.FsrSharpness = gfx.FsrSharpness;

        // Variable Rate Shading
        viewport.VrsMode = gfx.VrsMode switch
        {
            VrsMode.Texture => Viewport.VrsModeEnum.Texture,
            _ => Viewport.VrsModeEnum.Disabled
        };

        // Anti-aliasing
        viewport.Msaa3D = gfx.Msaa switch
        {
            2 => Viewport.Msaa.Msaa2X,
            4 => Viewport.Msaa.Msaa4X,
            8 => Viewport.Msaa.Msaa8X,
            _ => Viewport.Msaa.Disabled
        };
        viewport.ScreenSpaceAA = gfx.Fxaa
            ? Viewport.ScreenSpaceAAEnum.Fxaa
            : Viewport.ScreenSpaceAAEnum.Disabled;

        // FSR 2.2 includes its own temporal AA, so disable separate TAA when using it
        viewport.UseTaa = gfx.UpscalingMode == UpscalingMode.Fsr2 ? false : gfx.Taa;

        Log.Info(LogCategory.Config,
            $"Viewport: VSync={gfx.VSync}, Scale={gfx.RenderScale:P0}, " +
            $"Upscaling={gfx.UpscalingMode}, VRS={gfx.VrsMode}, " +
            $"MSAA={gfx.Msaa}x, FXAA={gfx.Fxaa}, TAA={gfx.Taa}");
    }

    private static void ApplyWindowMode(WindowMode mode, string resolution)
    {
        // Parse resolution
        Vector2I? targetSize = null;
        if (!string.IsNullOrEmpty(resolution) && resolution != "Default")
        {
            var parts = resolution.Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
            {
                targetSize = new Vector2I(w, h);
            }
        }

        // Get current screen the window is on
        var currentScreen = DisplayServer.WindowGetCurrentScreen();
        var screenPos = DisplayServer.ScreenGetPosition(currentScreen);
        var screenSize = DisplayServer.ScreenGetSize(currentScreen);

        // Get the main window for content scale settings
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        var mainWindow = sceneTree?.Root;

        switch (mode)
        {
            case WindowMode.Windowed:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                // Use target size or current window size (don't change if Default)
                var windowedSize = targetSize ?? DisplayServer.WindowGetSize();
                DisplayServer.WindowSetSize(windowedSize);
                // Set render resolution to match window size (no scaling needed in windowed)
                if (mainWindow != null)
                {
                    mainWindow.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
                    mainWindow.ContentScaleSize = Vector2I.Zero;
                }
                // Always center window on current screen
                var centeredPos = screenPos + (screenSize - windowedSize) / 2;
                DisplayServer.WindowSetPosition(centeredPos);
                break;

            case WindowMode.Borderless:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                // Borderless uses screen size for window, but can render at lower resolution
                DisplayServer.WindowSetSize(screenSize);
                DisplayServer.WindowSetPosition(screenPos);
                if (mainWindow != null)
                {
                    if (targetSize.HasValue && targetSize.Value != screenSize)
                    {
                        // Render at lower resolution and scale up
                        mainWindow.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
                        mainWindow.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
                        mainWindow.ContentScaleSize = targetSize.Value;
                    }
                    else
                    {
                        // Native resolution - no scaling
                        mainWindow.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
                        mainWindow.ContentScaleSize = Vector2I.Zero;
                    }
                }
                break;

            case WindowMode.Fullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                if (mainWindow != null)
                {
                    if (targetSize.HasValue && targetSize.Value != screenSize)
                    {
                        // Render at lower resolution and scale up
                        mainWindow.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
                        mainWindow.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
                        mainWindow.ContentScaleSize = targetSize.Value;
                    }
                    else
                    {
                        // Native resolution - no scaling
                        mainWindow.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
                        mainWindow.ContentScaleSize = Vector2I.Zero;
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Gets available screen resolutions for the settings menu.
    /// </summary>
    public static List<string> GetAvailableResolutions()
    {
        var resolutions = new List<string> { "Default" };
        var screenSize = DisplayServer.ScreenGetSize();

        // Common resolutions, filtered to those <= screen size
        var common = new[]
        {
            (3840, 2160), (2560, 1440), (1920, 1080), (1680, 1050),
            (1600, 900), (1440, 900), (1366, 768), (1280, 720),
            (1280, 800), (1024, 768)
        };

        foreach (var (w, h) in common)
        {
            if (w <= screenSize.X && h <= screenSize.Y)
            {
                resolutions.Add($"{w}x{h}");
            }
        }

        // Add current screen size if not in list
        var screenRes = $"{screenSize.X}x{screenSize.Y}";
        if (!resolutions.Contains(screenRes))
        {
            resolutions.Insert(1, screenRes);
        }

        return resolutions;
    }

    private static WorldEnvironment? ConfigureWorldEnvironment(Node root, GraphicsConfig gfx)
    {
        var worldEnvironment = root.GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
        if (worldEnvironment == null) return null;

        UpdateEnvironment(worldEnvironment, gfx);
        return worldEnvironment;
    }

    private static void ConfigureSky(global::Godot.Environment env, GraphicsConfig gfx)
    {
        if (gfx.SkyEnabled)
        {
            var skyMaterial = new ProceduralSkyMaterial
            {
                SkyTopColor = Color.FromHtml(gfx.SkyTopColor),
                SkyHorizonColor = Color.FromHtml(gfx.SkyHorizonColor),
                GroundBottomColor = Color.FromHtml(gfx.SkyGroundColor),
                GroundHorizonColor = Color.FromHtml(gfx.SkyGroundHorizonColor),
                SkyCurve = gfx.SkyCurve,
                SkyEnergyMultiplier = gfx.SkyEnergy,
                GroundEnergyMultiplier = gfx.SkyGroundEnergy,
                SunAngleMax = 0f,
                SunCurve = 0f
            };

            var sky = new Sky { SkyMaterial = skyMaterial };
            env.BackgroundMode = global::Godot.Environment.BGMode.Sky;
            env.Sky = sky;
        }
        else
        {
            env.BackgroundMode = global::Godot.Environment.BGMode.Color;
            env.BackgroundColor = Color.FromHtml(gfx.BackgroundColor);
        }
    }

    private static void ConfigureAmbientLight(global::Godot.Environment env, GraphicsConfig gfx)
    {
        env.AmbientLightSource = global::Godot.Environment.AmbientSource.Color;
        env.AmbientLightColor = Color.FromHtml(gfx.AmbientColor);
        env.AmbientLightEnergy = gfx.AmbientEnergy;
    }

    private static void ConfigureFog(global::Godot.Environment env, GraphicsConfig gfx)
    {
        env.FogEnabled = gfx.FogEnabled;
        if (!gfx.FogEnabled) return;
        env.FogLightColor = Color.FromHtml(gfx.FogColor);
        env.FogDensity = gfx.FogDensity;
        env.FogAerialPerspective = gfx.FogAerialPerspective;

        // Height fog for light pollution effect
        if (gfx.FogHeightEnabled)
        {
            env.FogHeight = gfx.FogHeight;
            env.FogHeightDensity = gfx.FogHeightDensity;
            // Blend fog color with height fog color for light pollution
            var baseColor = Color.FromHtml(gfx.FogColor);
            var heightColor = Color.FromHtml(gfx.FogHeightColor);
            env.FogSunScatter = gfx.FogSunScatter;
            env.FogLightColor = baseColor.Lerp(heightColor, gfx.FogHeightColorBlend);
        }
    }

    private static void ConfigureVolumetricFog(global::Godot.Environment env, GraphicsConfig gfx)
    {
        env.VolumetricFogEnabled = gfx.VolumetricFogEnabled;
        if (!gfx.VolumetricFogEnabled) return;
        env.VolumetricFogDensity = gfx.VolumetricFogDensity;
        env.VolumetricFogAlbedo = Color.FromHtml(gfx.VolumetricFogAlbedo);
        env.VolumetricFogEmission = Color.FromHtml(gfx.VolumetricFogEmission);
        env.VolumetricFogEmissionEnergy = gfx.VolumetricFogEmissionEnergy;
        env.VolumetricFogLength = gfx.VolumetricFogLength;
        env.VolumetricFogAnisotropy = gfx.VolumetricFogAnisotropy;
        env.VolumetricFogGIInject = gfx.VolumetricFogGiInject;
        env.VolumetricFogAmbientInject = gfx.VolumetricFogAmbientInject;
        env.VolumetricFogSkyAffect = gfx.VolumetricFogSkyAffect;
        env.VolumetricFogTemporalReprojectionEnabled = gfx.VolumetricFogTemporalReprojectionEnabled;
        env.VolumetricFogTemporalReprojectionAmount = gfx.VolumetricFogTemporalReprojectionAmount;
    }

    private static void ConfigureBloom(global::Godot.Environment env, GraphicsConfig gfx)
    {
        env.GlowEnabled = gfx.BloomEnabled;
        if (!gfx.BloomEnabled) return;
        env.GlowIntensity = gfx.BloomIntensity;
        env.GlowStrength = gfx.BloomStrength;
        env.GlowBloom = gfx.BloomBloom;
        env.GlowBlendMode = global::Godot.Environment.GlowBlendModeEnum.Softlight;
        env.GlowHdrThreshold = gfx.BloomThreshold;
        env.GlowHdrScale = gfx.BloomHdrScale;
        env.GlowHdrLuminanceCap = gfx.BloomHdrLuminanceCap;
    }

    private static void ConfigureTonemap(global::Godot.Environment env, GraphicsConfig gfx)
    {
        env.TonemapMode = global::Godot.Environment.ToneMapper.Filmic;
        env.TonemapExposure = gfx.TonemapExposure;
        env.TonemapWhite = gfx.TonemapWhite;
    }

    private static void ConfigureSsao(global::Godot.Environment env, GraphicsConfig gfx)
    {
        env.SsaoEnabled = gfx.SsaoEnabled;
        if (!gfx.SsaoEnabled) return;
        env.SsaoRadius = gfx.SsaoRadius;
        env.SsaoIntensity = gfx.SsaoIntensity;
        env.SsaoPower = gfx.SsaoPower;
        env.SsaoDetail = gfx.SsaoDetail;
        env.SsaoHorizon = gfx.SsaoHorizon;
        env.SsaoSharpness = gfx.SsaoSharpness;
        env.SsaoLightAffect = gfx.SsaoLightAffect;
    }

    private static void ConfigureSsr(global::Godot.Environment env, GraphicsConfig gfx)
    {
        env.SsrEnabled = gfx.SsrEnabled;
        if (!gfx.SsrEnabled) return;
        env.SsrMaxSteps = gfx.SsrMaxSteps;
        env.SsrFadeIn = gfx.SsrFadeIn;
        env.SsrFadeOut = gfx.SsrFadeOut;
        env.SsrDepthTolerance = gfx.SsrDepthTolerance;
    }

    private static void ConfigureSsil(global::Godot.Environment env, GraphicsConfig gfx)
    {
        env.SsilEnabled = gfx.SsilEnabled;
        if (!gfx.SsilEnabled) return;

        env.SsilRadius = gfx.SsilRadius;
        env.SsilIntensity = gfx.SsilIntensity;
        env.SsilSharpness = gfx.SsilSharpness;
        env.SsilNormalRejection = gfx.SsilNormalRejection;
    }

    /// <summary>
    /// Creates or updates camera attributes for DOF and auto-exposure.
    /// Apply the returned attributes to your Camera3D node.
    /// </summary>
    public static CameraAttributesPractical ConfigureCameraAttributes(GraphicsConfig gfx, CameraAttributesPractical? existing = null)
    {
        var attr = existing ?? new CameraAttributesPractical();

        // Depth of Field - Far blur
        attr.DofBlurFarEnabled = gfx.DofEnabled;
        if (gfx.DofEnabled)
        {
            attr.DofBlurFarDistance = gfx.DofFarDistance;
            attr.DofBlurFarTransition = gfx.DofFarTransition;
            attr.DofBlurAmount = gfx.DofAmount;

            // Near blur (optional, uses same amount)
            attr.DofBlurNearEnabled = gfx.DofNearDistance > 0;
            attr.DofBlurNearDistance = gfx.DofNearDistance;
            attr.DofBlurNearTransition = gfx.DofNearTransition;
        }
        else
        {
            attr.DofBlurNearEnabled = false;
        }

        // Auto-Exposure
        attr.AutoExposureEnabled = gfx.AutoExposureEnabled;
        if (gfx.AutoExposureEnabled)
        {
            attr.AutoExposureScale = gfx.AutoExposureScale;
            attr.AutoExposureMinSensitivity = gfx.AutoExposureMinSensitivity;
            attr.AutoExposureMaxSensitivity = gfx.AutoExposureMaxSensitivity;
            attr.AutoExposureSpeed = gfx.AutoExposureSpeed;
        }

        return attr;
    }

    /// <summary>
    /// Updates camera attributes on an existing camera. Call on hot reload.
    /// </summary>
    public static void UpdateCameraAttributes(Camera3D? camera, GraphicsConfig gfx)
    {
        if (camera == null) return;

        var attr = camera.Attributes as CameraAttributesPractical;
        attr = ConfigureCameraAttributes(gfx, attr);
        camera.Attributes = attr;

        Log.Info(LogCategory.Config, $"Camera: DOF={gfx.DofEnabled}, AutoExposure={gfx.AutoExposureEnabled}");
    }
}
