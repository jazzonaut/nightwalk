using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Godot.Physics;
using Nightwalk.Godot.Util;

namespace Nightwalk.Godot.Beacons;

/// <summary>
/// Handles prism POV mode: entering, exiting, camera positioning, and prism rotation.
/// Works independently of any tool selection.
/// </summary>
public sealed class PrismPOVController
{
    private BeaconSyncManager? _syncManager;
    private PrismSettings _config;

    private Camera3D? _camera;
    private Node3D? _originalCameraParent;
    private Node3D? _cameraHost;
    private Transform3D _originalCameraLocalTransform;

    private PrismNode? _activePrism;
    private bool _isInPOV;

    private SmoothTransition _enterTransition;
    private SmoothTransition _exitTransition;

    // Zoom state
    private float _originalFov;
    private float _currentFov;
    private float _targetFov;

    /// <summary>
    /// Whether currently in POV mode.
    /// </summary>
    public bool IsInPOV => _isInPOV;

    /// <summary>
    /// The prism currently being controlled, if any.
    /// </summary>
    public PrismNode? ActivePrism => _activePrism;

    public PrismPOVController(PrismSettings config)
    {
        _config = config;
    }

    /// <summary>
    /// Updates the configuration (for hot-reload).
    /// </summary>
    public void Configure(PrismSettings config)
    {
        _config = config;
    }

    /// <summary>
    /// Sets the sync manager reference. Call after gameplay systems are initialized.
    /// </summary>
    public void SetSyncManager(BeaconSyncManager syncManager)
    {
        _syncManager = syncManager;
    }

    /// <summary>
    /// Sets the camera and host node for POV mode.
    /// The host node is used to reparent the camera during POV.
    /// </summary>
    public void SetCamera(Camera3D camera, Node3D cameraHost)
    {
        _camera = camera;
        _cameraHost = cameraHost;
    }

    /// <summary>
    /// Attempts to enter POV mode by raycasting from the camera to find a prism.
    /// </summary>
    /// <returns>True if entered POV mode.</returns>
    public bool TryEnterPOV(PhysicsDirectSpaceState3D spaceState, float maxDistance)
    {
        if (_isInPOV || _camera == null || _syncManager == null)
            return false;

        var result = RaycastHelper.CameraRaycast(_camera, spaceState, maxDistance);
        if (!result.HasValue)
            return false;

        var prism = PrismNode.FindFromCollider(result.Value.Collider);
        if (prism == null || !_syncManager.ContainsPrism(prism))
            return false;

        EnterPOV(prism);
        return true;
    }

    /// <summary>
    /// Enters POV mode for the specified prism.
    /// </summary>
    public void EnterPOV(PrismNode prism)
    {
        if (_isInPOV || _camera == null || _cameraHost == null)
            return;

        _activePrism = prism;
        _isInPOV = true;

        // Store original camera state for exit
        _originalCameraParent = _camera.GetParent() as Node3D;
        _originalCameraLocalTransform = _camera.Transform;
        _originalFov = _camera.Fov;
        _currentFov = _camera.Fov;
        _targetFov = _camera.Fov;

        // Clear highlight and make prism transparent
        prism.SetHighlighted(false);
        prism.SetPrismOpacity(_config.PovPrismOpacity);

        // Calculate target POV transform
        var targetTransform = CalculatePOVTransform(prism);

        // Begin enter transition
        var startTransform = _camera.GlobalTransform;
        _enterTransition.Begin(startTransform, targetTransform, _config.PovTransitionDuration);
        _exitTransition.Cancel();

        // Reparent camera to host
        _originalCameraParent?.RemoveChild(_camera);
        _cameraHost.AddChild(_camera);

        Log.Debug(LogCategory.Input, "PrismPOV: Entered POV mode");
    }

    /// <summary>
    /// Exits POV mode and returns camera to original parent.
    /// </summary>
    public void ExitPOV()
    {
        if (!_isInPOV || _camera == null)
            return;

        // Restore prism opacity
        if (_activePrism != null && GodotObject.IsInstanceValid(_activePrism))
        {
            _activePrism.SetPrismOpacity(_config.Transparency);
        }

        // Calculate where the camera should return to
        // Combine parent's current global transform with original local transform
        Transform3D exitTarget;
        if (_originalCameraParent != null && GodotObject.IsInstanceValid(_originalCameraParent))
        {
            exitTarget = _originalCameraParent.GlobalTransform * _originalCameraLocalTransform;
        }
        else
        {
            exitTarget = _camera.GlobalTransform;
        }

        // Begin exit transition
        _exitTransition.Begin(_camera.GlobalTransform, exitTarget, _config.PovTransitionDuration);
        _enterTransition.Cancel();

        // Update beam path
        _syncManager?.UpdateBeamPath();

        _isInPOV = false;
        // Don't clear _activePrism yet - we need it for the transition
        // It will be cleared when transition completes

        Log.Debug(LogCategory.Input, "PrismPOV: Exiting POV mode");
    }

    /// <summary>
    /// Updates the POV controller. Call each frame.
    /// </summary>
    /// <returns>True if POV mode is active or transitioning.</returns>
    public bool Update(float delta)
    {
        if (_camera == null)
            return false;

        // Handle enter transition
        if (_enterTransition.IsActive)
        {
            if (_enterTransition.TryStepTransform(delta, out var transform))
            {
                _camera.GlobalTransform = transform;
            }
            return true;
        }

        // Handle exit transition
        if (_exitTransition.IsActive)
        {
            if (_exitTransition.TryStepTransform(delta, out var transform))
            {
                _camera.GlobalTransform = transform;
            }

            // When exit transition completes, reparent camera back
            if (!_exitTransition.IsActive)
            {
                CompleteExit();
            }
            return true;
        }

        // Active POV mode
        if (_isInPOV && _activePrism != null)
        {
            // Check if prism was deleted
            if (!GodotObject.IsInstanceValid(_activePrism) || _syncManager?.ContainsPrism(_activePrism) != true)
            {
                ForceExit();
                return false;
            }

            // Update camera to follow prism
            _camera.GlobalTransform = CalculatePOVTransform(_activePrism);

            // Smoothly lerp FOV towards target
            if (_config.PovZoomEnabled && Mathf.Abs(_currentFov - _targetFov) > 0.01f)
            {
                _currentFov = Mathf.Lerp(_currentFov, _targetFov, delta * 10f);
                _camera.Fov = _currentFov;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles mouse motion input for prism rotation while in POV mode.
    /// </summary>
    /// <returns>True if input was consumed.</returns>
    public bool HandleMouseMotion(InputEventMouseMotion mm)
    {
        if (!_isInPOV || _activePrism == null || _enterTransition.IsActive || _syncManager == null)
            return false;

        if (!GodotObject.IsInstanceValid(_activePrism) || !_syncManager.ContainsPrism(_activePrism))
        {
            ForceExit();
            return false;
        }

        // Rotate prism based on mouse movement
        var sensitivity = _config.RotationSensitivity * 0.01f;
        var yawDelta = -mm.Relative.X * sensitivity;
        var pitchDelta = -mm.Relative.Y * sensitivity;

        _activePrism.RotatePrism(yawDelta, pitchDelta);

        // Update tracker and beam path
        _syncManager.UpdatePrismRotation(_activePrism);
        _syncManager.UpdateBeamPath();

        return true;
    }

    /// <summary>
    /// Handles mouse wheel input for zoom while in POV mode.
    /// </summary>
    /// <param name="direction">Positive for zoom in, negative for zoom out.</param>
    /// <returns>True if input was consumed.</returns>
    public bool HandleZoom(float direction)
    {
        if (!_isInPOV || _camera == null || !_config.PovZoomEnabled)
            return false;

        if (_enterTransition.IsActive)
            return false;

        // Zoom in = decrease FOV, zoom out = increase FOV
        _targetFov -= direction * _config.PovZoomStep;
        _targetFov = Mathf.Clamp(_targetFov, _config.PovMinFov, _config.PovMaxFov);

        return true;
    }

    private Transform3D CalculatePOVTransform(PrismNode prism)
    {
        // Get beam direction from prism or sync manager
        Vector3 lookDirection;
        var beamInfo = _syncManager?.GetRefractedBeamAfterPrism(prism);

        if (beamInfo.HasValue)
        {
            lookDirection = beamInfo.Value.refractedDirection;
        }
        else
        {
            lookDirection = prism.BeamDirection;
        }

        var prismPos = prism.PrismPosition;
        var offset = _config.PovCameraOffset;
        var verticalOffset = _config.PovCameraVerticalOffset;

        // Position camera behind and slightly above the prism
        var cameraPos = prismPos - lookDirection * offset + Vector3.Up * verticalOffset;

        // Look at a point along the beam direction
        var lookTarget = prismPos + lookDirection * 50f;

        // Create transform: get rotation from LookingAt (relative to camera position), then apply position
        var viewDir = (lookTarget - cameraPos).Normalized();
        // Avoid colinear up/target vectors when the beam is nearly vertical.
        var upVector = Mathf.Abs(viewDir.Dot(Vector3.Up)) > 0.99f
            ? Vector3.Forward
            : Vector3.Up;
        var lookAtBasis = Transform3D.Identity.LookingAt(viewDir, upVector).Basis;
        return new Transform3D(lookAtBasis, cameraPos);
    }

    private void CompleteExit()
    {
        if (_camera == null)
            return;

        // Reparent camera back to original parent
        _cameraHost?.RemoveChild(_camera);
        _originalCameraParent?.AddChild(_camera);

        // Restore original local transform and FOV (critical for proper camera orientation)
        _camera.Transform = _originalCameraLocalTransform;
        _camera.Fov = _originalFov;

        _activePrism = null;
        _originalCameraParent = null;

        Log.Debug(LogCategory.Input, "PrismPOV: Exit complete");
    }

    private void ForceExit()
    {
        // Prism was deleted mid-POV - exit immediately without transition
        if (_camera == null)
            return;

        _enterTransition.Cancel();
        _exitTransition.Cancel();

        // Reparent camera back
        _cameraHost?.RemoveChild(_camera);
        _originalCameraParent?.AddChild(_camera);

        // Restore original local transform and FOV
        _camera.Transform = _originalCameraLocalTransform;
        _camera.Fov = _originalFov;

        _isInPOV = false;
        _activePrism = null;
        _originalCameraParent = null;

        Log.Debug(LogCategory.Input, "PrismPOV: Force exited (prism deleted)");
    }

    /// <summary>
    /// Resets the controller state.
    /// </summary>
    public void Reset()
    {
        if (_isInPOV)
        {
            ForceExit();
        }
        _enterTransition.Cancel();
        _exitTransition.Cancel();
        _activePrism = null;
    }
}
