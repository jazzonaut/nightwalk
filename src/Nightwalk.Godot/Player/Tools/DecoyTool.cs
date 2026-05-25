using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Resonance.Events;
using Nightwalk.Core.Tools;
using Nightwalk.Godot.Physics;
using Nightwalk.Godot.Resonance;

namespace Nightwalk.Godot.Player.Tools;

/// <summary>
/// Decoy tool that projects a laser pointer onto surfaces.
/// Hold to charge and deploy a decoy ping that distracts Seekers.
/// Right-click a highlighted decoy to remove it.
/// </summary>
public sealed partial class DecoyTool : BaseTool
{
    private static readonly Color DotColor = new(1f, 0.5f, 0.1f); // Dark orange

    /// <inheritdoc/>
    public override string Id => ToolIds.Decoy;

    /// <inheritdoc/>
    public override string DisplayName => "Decoy";

    /// <inheritdoc/>
    public override bool RequiresUpdate => true;

    /// <inheritdoc/>
    public override bool RequiresInput => true;

    // State
    private bool _isCharging;
    private float _chargeTime;
    private Vector3 _lastHitPoint;
    private bool _hasValidTarget;

    // Active decoys
    private readonly List<DecoyPingMarker> _activeDecoys = new();

    // Hover state
    private DecoyPingMarker? _hoveredDecoy;

    // Visuals
    private MeshInstance3D? _laserDot;
    private StandardMaterial3D? _dotMaterial;
    private OmniLight3D? _dotLight;
    private MeshInstance3D? _chargeRing;
    private ShaderMaterial? _chargeMaterial;

    /// <inheritdoc/>
    protected override void OnInitialize()
    {
        base.OnInitialize();
        CreateLaserDot();
        CreateChargeRing();
    }

    private void CreateLaserDot()
    {
        var config = Context.Data.Tools.Decoy;

        var sphereMesh = new SphereMesh
        {
            Radius = config.DotRadius,
            Height = config.DotRadius * 2f,
            RadialSegments = 16,
            Rings = 8
        };

        _dotMaterial = new StandardMaterial3D
        {
            AlbedoColor = DotColor,
            EmissionEnabled = true,
            Emission = DotColor,
            EmissionEnergyMultiplier = config.DotEmission,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };

        _laserDot = new MeshInstance3D
        {
            Name = "LaserDot",
            Mesh = sphereMesh,
            MaterialOverride = _dotMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };

        // Add a small point light to make the dot more visible
        _dotLight = new OmniLight3D
        {
            Name = "DotLight",
            LightColor = DotColor,
            LightEnergy = 2.0f,
            OmniRange = 3.0f,
            OmniAttenuation = 2.0f,
            ShadowEnabled = false
        };
        _laserDot.AddChild(_dotLight);

        AddChild(_laserDot);
    }

    private void CreateChargeRing()
    {
        var ringMesh = new TorusMesh
        {
            InnerRadius = 0.25f,
            OuterRadius = 0.35f,
            Rings = 32,
            RingSegments = 8
        };

        _chargeMaterial = new ShaderMaterial();
        _chargeMaterial.Shader = GD.Load<Shader>("res://shaders/spotlight_charge.gdshader");
        _chargeMaterial.SetShaderParameter("fill_color", new Color(1f, 0.6f, 0.2f, 0.9f));
        _chargeMaterial.SetShaderParameter("bg_color", new Color(0.4f, 0.2f, 0.1f, 0.4f));
        _chargeMaterial.SetShaderParameter("fill_amount", 0f);

        _chargeRing = new MeshInstance3D
        {
            Name = "ChargeRing",
            Mesh = ringMesh,
            MaterialOverride = _chargeMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };

        AddChild(_chargeRing);
    }

    /// <inheritdoc/>
    public override void Enter()
    {
        base.Enter();
        ResetChargeState();
    }

    /// <inheritdoc/>
    public override void Exit()
    {
        base.Exit();
        ResetChargeState();
        ClearHoverHighlight();
        HideVisuals();
    }

    /// <inheritdoc/>
    public override bool HandleInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            return HandleMouseButton(mb);
        }

        return false;
    }

    private bool HandleMouseButton(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.Left)
        {
            _isCharging = mb.Pressed;

            if (!_isCharging)
            {
                // Released - reset charge
                _chargeTime = 0f;
                _hasValidTarget = false;
                if (_chargeRing != null) _chargeRing.Visible = false;
            }

            return false;
        }

        if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            return HandleRightClick();
        }

        return false;
    }

    private bool HandleRightClick()
    {
        // Remove hovered decoy
        if (_hoveredDecoy != null)
        {
            RemoveDecoy(_hoveredDecoy);
            ClearHoverHighlight();
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public override void Update(float delta)
    {
        var config = Context.Data.Tools.Decoy;

        // Check for hover over existing decoys first
        ClearHoverHighlight();
        CheckForHoveredDecoy();

        // If hovering a decoy, hide the laser dot
        if (_hoveredDecoy != null)
        {
            HideVisuals();
            return;
        }

        // Raycast from camera - always show laser dot when tool is active
        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) return;

        var camera = Context.Player.Camera;
        if (camera == null) return;

        var from = camera.GlobalPosition;
        var forward = -camera.GlobalTransform.Basis.Z;
        var to = from + forward * config.Range;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        // Exclude decoy collision layer from placement raycast
        query.CollisionMask = ~DecoyPingMarker.DecoyCollisionLayer;

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var hitPoint = (Vector3)result["position"];
            var hitNormal = (Vector3)result["normal"];

            // Always show laser dot at hit point
            UpdateLaserDot(hitPoint, hitNormal);

            // Only charge when holding button
            if (_isCharging)
            {
                // Check aim stability
                var drift = hitPoint.DistanceTo(_lastHitPoint);
                if (drift < config.AimTolerance && _hasValidTarget)
                {
                    _chargeTime += delta;
                    UpdateChargeRing(hitPoint, hitNormal);

                    // Check if charge complete
                    if (_chargeTime >= config.ChargeTime)
                    {
                        DeployDecoy(hitPoint);
                        _chargeTime = 0f;
                        _hasValidTarget = false;
                    }
                }
                else
                {
                    // New target or first frame
                    _chargeTime = 0f;
                    _hasValidTarget = true;
                    if (_chargeRing != null) _chargeRing.Visible = false;
                }
            }
            else
            {
                // Not charging - hide charge ring but keep dot visible
                _chargeTime = 0f;
                if (_chargeRing != null) _chargeRing.Visible = false;
            }

            _lastHitPoint = hitPoint;
        }
        else
        {
            // No hit - out of range
            _chargeTime = 0f;
            _hasValidTarget = false;
            HideVisuals();
        }
    }

    private void CheckForHoveredDecoy()
    {
        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null) return;

        var camera = Context.Player.Camera;
        if (camera == null) return;

        var config = Context.Data.Tools.Decoy;

        // Raycast only against decoy collision layer
        var result = RaycastHelper.CameraRaycast(
            camera,
            spaceState,
            config.Range,
            DecoyPingMarker.DecoyCollisionLayer
        );

        if (!result.HasValue) return;

        var hitDecoy = DecoyPingMarker.FindFromCollider(result.Value.Collider);
        if (hitDecoy != null && _activeDecoys.Contains(hitDecoy))
        {
            SetHoverHighlight(hitDecoy);
        }
    }

    private void SetHoverHighlight(DecoyPingMarker decoy)
    {
        _hoveredDecoy = decoy;
        decoy.SetHighlighted(true);
    }

    private void ClearHoverHighlight()
    {
        if (_hoveredDecoy != null)
        {
            if (GodotObject.IsInstanceValid(_hoveredDecoy))
            {
                _hoveredDecoy.SetHighlighted(false);
            }
        }
        _hoveredDecoy = null;
    }

    private void UpdateLaserDot(Vector3 position, Vector3 normal)
    {
        if (_laserDot == null) return;

        _laserDot.Visible = true;
        _laserDot.GlobalPosition = position + normal * 0.02f;
    }

    private void UpdateChargeRing(Vector3 position, Vector3 normal)
    {
        if (_chargeRing == null || _chargeMaterial == null) return;

        var config = Context.Data.Tools.Decoy;
        float fill = _chargeTime / config.ChargeTime;

        _chargeRing.Visible = fill > 0.05f;
        _chargeRing.GlobalPosition = position + normal * 0.05f;

        // Orient ring to face along the surface normal
        if (normal.IsNormalized())
        {
            var up = Mathf.Abs(normal.Dot(Vector3.Up)) > 0.9f ? Vector3.Forward : Vector3.Up;
            _chargeRing.LookAt(_chargeRing.GlobalPosition + normal, up);
        }

        _chargeMaterial.SetShaderParameter("fill_amount", fill);
    }

    private void DeployDecoy(Vector3 position)
    {
        var maxDecoys = Context.Data.Resonance.Counterplay.MaxActiveDecoys;
        if (_activeDecoys.Count >= maxDecoys)
        {
            return;
        }

        var duration = Context.Data.Resonance.Counterplay.DecoyDuration;

        // Create marker
        var marker = new DecoyPingMarker { Name = "DecoyMarker" };
        marker.Initialize(duration);
        marker.OnExpired += OnDecoyExpired;

        // Add to scene and track
        GetTree().Root.AddChild(marker);
        marker.GlobalPosition = position;
        _activeDecoys.Add(marker);

        // Emit event so Seekers respond to the decoy
        Context.EventBus.Publish(new DecoyPingEvent(position.X, position.Y, position.Z));
    }

    private void RemoveDecoy(DecoyPingMarker marker)
    {
        marker.OnExpired -= OnDecoyExpired;
        _activeDecoys.Remove(marker);
        marker.QueueFree();
    }

    private void OnDecoyExpired(DecoyPingMarker marker)
    {
        marker.OnExpired -= OnDecoyExpired;
        _activeDecoys.Remove(marker);
        marker.QueueFree();
    }

    private void HideVisuals()
    {
        if (_laserDot != null) _laserDot.Visible = false;
        if (_chargeRing != null) _chargeRing.Visible = false;
    }

    private void ResetChargeState()
    {
        _isCharging = false;
        _chargeTime = 0f;
        _hasValidTarget = false;
    }

    /// <summary>
    /// Clears all active decoys (e.g., on session reset).
    /// </summary>
    public void ClearAllDecoys()
    {
        foreach (var decoy in _activeDecoys)
        {
            decoy.OnExpired -= OnDecoyExpired;
            decoy.QueueFree();
        }
        _activeDecoys.Clear();
    }
}
