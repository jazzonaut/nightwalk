using Godot;
using Nightwalk.Core.Events;
using Nightwalk.Core.Resonance.Events;

namespace Nightwalk.Godot.Resonance;

/// <summary>
/// Applies visual effects based on Resonance mode state.
/// - Sky color shift at high Alert
/// - Screen vignette during Exposure
/// </summary>
public sealed partial class ResonanceVisualEffects : CanvasLayer
{
    private IEventBus? _eventBus;

    // Vignette overlay
    private ColorRect? _vignetteRect;
    private ShaderMaterial? _vignetteMaterial;

    // Environment reference for sky tinting
    private WorldEnvironment? _worldEnv;
    private Color _originalAmbientColor;
    private Color _originalSkyColor;

    // Current values for smooth transitions
    private float _currentAlert;
    private float _currentExposure;
    private float _targetAlert;
    private float _targetExposure;

    // Visual parameters
    private const float AlertTransitionSpeed = 2f;
    private const float ExposureTransitionSpeed = 4f;
    private const float MaxSkyTintStrength = 0.7f;
    private const float MaxVignetteStrength = 0.6f;

    public override void _Ready()
    {
        Layer = 100; // Above HUD
        CreateVignetteOverlay();
    }

    /// <summary>
    /// Initializes the visual effects with event bus and world environment.
    /// </summary>
    public void Initialize(IEventBus eventBus, WorldEnvironment? worldEnv)
    {
        _eventBus = eventBus;
        _worldEnv = worldEnv;

        // Store original environment colors
        if (_worldEnv?.Environment != null)
        {
            _originalAmbientColor = _worldEnv.Environment.AmbientLightColor;
            if (_worldEnv.Environment.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
            {
                _originalSkyColor = sky.SkyTopColor;
            }
        }

        // Subscribe to events
        _eventBus.Subscribe<AlertChangedEvent>(OnAlertChanged);
        _eventBus.Subscribe<ExposureChangedEvent>(OnExposureChanged);
    }

    private void CreateVignetteOverlay()
    {
        _vignetteMaterial = new ShaderMaterial();
        _vignetteMaterial.Shader = GD.Load<Shader>("res://shaders/resonance_vignette.gdshader");
        _vignetteMaterial.SetShaderParameter("vignette_intensity", 0f);
        _vignetteMaterial.SetShaderParameter("vignette_color", new Color(0.8f, 0.1f, 0.1f, 1f));
        _vignetteMaterial.SetShaderParameter("pulse_speed", 3f);

        _vignetteRect = new ColorRect
        {
            Name = "VignetteOverlay",
            Material = _vignetteMaterial,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _vignetteRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_vignetteRect);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Smooth transitions
        _currentAlert = Mathf.Lerp(_currentAlert, _targetAlert, dt * AlertTransitionSpeed);
        _currentExposure = Mathf.Lerp(_currentExposure, _targetExposure, dt * ExposureTransitionSpeed);

        // Update sky tint based on alert
        UpdateSkyTint();

        // Update vignette based on exposure
        UpdateVignette();
    }

    private void UpdateSkyTint()
    {
        if (_worldEnv?.Environment == null) return;

        // Only tint when alert is above 50%, then ramp up to full strength
        float tintStrength = Mathf.Max(0f, (_currentAlert - 0.5f) * 2f) * MaxSkyTintStrength;

        // Shift ambient toward dark red
        var alertTint = new Color(0.6f, 0.2f, 0.2f);
        var newAmbient = _originalAmbientColor.Lerp(alertTint, tintStrength);
        _worldEnv.Environment.AmbientLightColor = newAmbient;

        // Also tint procedural sky if present
        if (_worldEnv.Environment.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
        {
            var skyTint = new Color(0.3f, 0.1f, 0.15f);
            sky.SkyTopColor = _originalSkyColor.Lerp(skyTint, tintStrength);
        }
    }

    private void UpdateVignette()
    {
        if (_vignetteMaterial == null) return;

        // Vignette appears when exposed, stronger when actively detected
        float intensity = _currentExposure * MaxVignetteStrength;
        _vignetteMaterial.SetShaderParameter("vignette_intensity", intensity);
    }

    private void OnAlertChanged(AlertChangedEvent e)
    {
        _targetAlert = e.NewValue;
    }

    private void OnExposureChanged(ExposureChangedEvent e)
    {
        _targetExposure = e.NewValue;
    }

    /// <summary>
    /// Resets visual effects to default state.
    /// </summary>
    public void Reset()
    {
        _currentAlert = 0f;
        _targetAlert = 0f;
        _currentExposure = 0f;
        _targetExposure = 0f;

        // Restore original environment
        if (_worldEnv?.Environment != null)
        {
            _worldEnv.Environment.AmbientLightColor = _originalAmbientColor;
            if (_worldEnv.Environment.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
            {
                sky.SkyTopColor = _originalSkyColor;
            }
        }

        // Clear vignette
        _vignetteMaterial?.SetShaderParameter("vignette_intensity", 0f);
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public void Cleanup()
    {
        if (_eventBus != null)
        {
            _eventBus.Unsubscribe<AlertChangedEvent>(OnAlertChanged);
            _eventBus.Unsubscribe<ExposureChangedEvent>(OnExposureChanged);
        }

        Reset();
    }

    public override void _ExitTree()
    {
        Cleanup();
    }
}
