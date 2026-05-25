using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Resonance;
using Nightwalk.Core.Resonance.Warden;

namespace Nightwalk.Godot.Resonance;

/// <summary>
/// Godot node representing the Warden entity.
/// A massive (100-200m) atmospheric presence that observes the player.
/// </summary>
public sealed partial class WardenNode : Node3D
{
    private WardenController? _controller;
    private WardenConfig? _config;

    // Visual components
    private WardenRenderer? _renderer;
    private OmniLight3D? _coreLight;

    // Core logic for beam repelling
    private WardenCore? _wardenCore;

    // Animation state
    private float _hoverOffset;
    private float _coreGlow;
    private bool _isVulnerable;

    /// <summary>
    /// Gets the core shard position for beam targeting.
    /// </summary>
    public Vector3 CorePosition => _renderer?.CoreShardTipPosition ?? GlobalPosition;

    /// <summary>
    /// Checks if a world position is within the core's hit area.
    /// Only returns true when vulnerable.
    /// </summary>
    public bool IsPointInCore(Vector3 worldPoint)
    {
        if (_wardenCore == null || !_wardenCore.IsVulnerable || _config == null) return false;

        float coreRadius = _config.Scale * _config.CoreHitRadiusFraction;
        float distance = CorePosition.DistanceTo(worldPoint);
        return distance < coreRadius;
    }

    /// <summary>
    /// Notifies the core of beam contact for repel progress.
    /// </summary>
    public void OnBeamContact(float delta)
    {
        _wardenCore?.OnBeamContact(delta);
    }

    /// <summary>
    /// Initializes the Warden with its controller and config.
    /// </summary>
    public void Initialize(WardenController controller, WardenConfig config, WardenCore core)
    {
        _controller = controller;
        _config = config;
        _wardenCore = core;

        // Create renderer
        _renderer = new WardenRenderer();
        _renderer.Initialize(config);
        AddChild(_renderer);

        // Create core light (for vulnerability glow - uses vulnerable orange color)
        _coreLight = new OmniLight3D
        {
            Name = "CoreLight",
            LightColor = Color.FromHtml(config.VulnerableColor),
            LightEnergy = 0f,
            OmniRange = config.Scale * 0.5f,
            ShadowEnabled = false
        };
        AddChild(_coreLight);

        // Subscribe to controller events
        _controller.OnBecameVulnerable += OnBecameVulnerable;
        _controller.OnVulnerabilityEnded += OnVulnerabilityEnded;

        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_controller == null || _config == null) return;

        var state = _controller.State;

        // Update visibility
        Visible = state != WardenState.Absent;

        if (!Visible) return;

        // Update position from controller with hover animation
        var pos = _controller.Position;
        _hoverOffset += (float)delta * _config.HoverSpeed;
        float hoverY = Mathf.Sin(_hoverOffset) * _config.HoverAmplitude;
        GlobalPosition = new Vector3(pos.X, pos.Y + hoverY, pos.Z);

        // Update core light position to follow core shard
        if (_renderer != null && _coreLight != null)
        {
            _coreLight.GlobalPosition = _renderer.CoreShardTipPosition;
        }

        // Update vulnerability visuals
        UpdateVulnerabilityVisuals((float)delta);

        // Update renderer state for Departing
        if (state == WardenState.Departing)
        {
            _renderer?.SetVisualState(WardenVisualState.Departing);
        }
    }

    private void UpdateVulnerabilityVisuals(float delta)
    {
        if (_config == null) return;

        if (_isVulnerable)
        {
            // Pulse the core glow
            _coreGlow = Mathf.Lerp(_coreGlow, 1f, delta * _config.CoreGlowLerpIn);
            _coreGlow = _config.CoreGlowPulseBase + _config.CoreGlowPulseAmplitude * Mathf.Sin(_hoverOffset * _config.CoreGlowPulseFrequency);

            if (_coreLight != null)
            {
                _coreLight.LightEnergy = _coreGlow * _config.CoreLightEnergy;
            }
        }
        else
        {
            _coreGlow = Mathf.Lerp(_coreGlow, 0f, delta * _config.CoreGlowLerpOut);

            if (_coreLight != null)
            {
                _coreLight.LightEnergy = _coreGlow * _config.CoreLightEnergy;
            }
        }
    }

    private void OnBecameVulnerable()
    {
        _isVulnerable = true;
        _renderer?.SetVisualState(WardenVisualState.Vulnerable);
        _renderer?.SetCoreVulnerable(true);
    }

    private void OnVulnerabilityEnded()
    {
        _isVulnerable = false;
        _renderer?.SetVisualState(WardenVisualState.Normal);
        _renderer?.SetCoreVulnerable(false);
    }

    public override void _ExitTree()
    {
        if (_controller != null)
        {
            _controller.OnBecameVulnerable -= OnBecameVulnerable;
            _controller.OnVulnerabilityEnded -= OnVulnerabilityEnded;
        }
    }
}
