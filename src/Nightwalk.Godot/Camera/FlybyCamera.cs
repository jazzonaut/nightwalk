using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Godot.Util;

namespace Nightwalk.Godot.Camera;

/// <summary>
/// Cinematic flyby camera that follows a smooth spline path.
/// Features banking on turns, FOV changes, and post-processing integration.
/// </summary>
public sealed class FlybyCamera
{
    private FlybyCameraConfig _config;

    private Camera3D? _camera;
    private Node3D? _originalCameraParent;
    private Node3D? _cameraHost;
    private Transform3D _originalCameraLocalTransform;
    private float _originalFov;

    private FlybyPath? _path;
    private float _currentT;
    private float _currentRoll;
    private float _currentFov;
    private float _currentSpeed;

    private bool _isActive;
    private bool _isSkipRequested;

    private SmoothTransition _enterTransition;
    private SmoothTransition _exitTransition;

    /// <summary>
    /// Whether the flyby is currently active (including transitions).
    /// </summary>
    public bool IsActive => _isActive || _enterTransition.IsActive || _exitTransition.IsActive;

    /// <summary>
    /// Whether the flyby is in the main flight phase (past enter transition).
    /// </summary>
    public bool IsFlying => _isActive && !_enterTransition.IsActive;

    /// <summary>
    /// Current speed factor (0-1) for post-processing effects.
    /// </summary>
    public float SpeedFactor { get; private set; }

    /// <summary>
    /// Fired when the flyby completes (or is skipped).
    /// </summary>
    public event Action? OnFlybyComplete;

    public FlybyCamera(FlybyCameraConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Updates the configuration (for hot-reload).
    /// </summary>
    public void Configure(FlybyCameraConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Sets the camera and host node for flyby mode.
    /// </summary>
    public void SetCamera(Camera3D camera, Node3D cameraHost)
    {
        _camera = camera;
        _cameraHost = cameraHost;
    }

    /// <summary>
    /// Starts a flyby along the given waypoints.
    /// </summary>
    /// <param name="waypoints">Ordered list of positions (e.g., beam path).</param>
    /// <returns>True if flyby started successfully.</returns>
    public bool StartFlyby(IReadOnlyList<Vector3> waypoints)
    {
        if (_camera == null || _cameraHost == null || waypoints.Count < _config.MinWaypoints)
            return false;

        if (_isActive)
        {
            Log.Debug(LogCategory.Sim, "FlybyCamera: Already active, ignoring start request");
            return false;
        }

        // Create path with vertical offset
        _path = new FlybyPath(waypoints, _config.VerticalOffset);

        if (_path.TotalLength < 5f)
        {
            Log.Debug(LogCategory.Sim, "FlybyCamera: Path too short, skipping flyby");
            return false;
        }

        _isActive = true;
        _isSkipRequested = false;
        _currentT = 0f;
        _currentRoll = 0f;
        _currentSpeed = _config.BaseSpeed;
        _currentFov = _config.BaseFov;
        SpeedFactor = 0f;

        // Store original camera state
        _originalCameraParent = _camera.GetParent() as Node3D;
        _originalCameraLocalTransform = _camera.Transform;
        _originalFov = _camera.Fov;

        // Calculate starting transform on path
        // Look at a point ahead on the path (not just the tangent) for smoother entry
        var startPos = _path.GetPositionAtT(0f);
        var lookAheadPos = _path.GetPositionAtT(0.15f); // Look 15% into the path

        // If look-ahead is too close to start (degenerate short path), use tangent instead
        Vector3 lookTarget;
        if (startPos.DistanceSquaredTo(lookAheadPos) < 0.01f)
            lookTarget = startPos + _path.GetTangentAtT(0f) * 10f;
        else
            lookTarget = lookAheadPos;

        var startTransform = CalculateTransformLookingAt(startPos, lookTarget);

        // Begin enter transition from current camera position to path start
        _enterTransition.Begin(_camera.GlobalTransform, startTransform, _config.EnterTransitionDuration);
        _exitTransition.Cancel();

        // Reparent camera to host
        _originalCameraParent?.RemoveChild(_camera);
        _cameraHost.AddChild(_camera);

        Log.Info(LogCategory.Sim, $"FlybyCamera: Started flyby, path length={_path.TotalLength:F1}m");
        return true;
    }

    /// <summary>
    /// Requests the flyby to skip to the exit transition.
    /// </summary>
    public void RequestSkip()
    {
        if (!_isActive)
            return;

        _isSkipRequested = true;
        Log.Debug(LogCategory.Sim, "FlybyCamera: Skip requested");
    }

    /// <summary>
    /// Updates the flyby camera. Call each frame.
    /// </summary>
    /// <returns>True if flyby is active or transitioning.</returns>
    public bool Update(float delta)
    {
        if (_camera == null || _path == null)
            return false;

        // Handle enter transition
        if (_enterTransition.IsActive)
        {
            if (_enterTransition.TryStepTransform(delta, out var transform))
            {
                _camera.GlobalTransform = transform;

                // Gradually ramp up FOV during enter
                float blend = _enterTransition.Elapsed / _enterTransition.Duration;
                _camera.Fov = Mathf.Lerp(_originalFov, _config.BaseFov, blend);
            }

            // Ramp up speed factor during enter
            SpeedFactor = Mathf.Lerp(0f, _config.PostProcessMultiplier * 0.5f,
                _enterTransition.Elapsed / _enterTransition.Duration);

            return true;
        }

        // Handle exit transition
        if (_exitTransition.IsActive)
        {
            if (_exitTransition.TryStepTransform(delta, out var transform))
            {
                _camera.GlobalTransform = transform;

                // Restore FOV during exit
                float blend = _exitTransition.Elapsed / _exitTransition.Duration;
                _camera.Fov = Mathf.Lerp(_currentFov, _originalFov, blend);
            }

            // Ramp down speed factor during exit
            SpeedFactor = Mathf.Lerp(SpeedFactor, 0f, delta * 3f);

            // Complete exit when transition finishes
            if (!_exitTransition.IsActive)
            {
                CompleteExit();
            }
            return true;
        }

        // Main flight phase
        if (_isActive)
        {
            // Check for skip request
            if (_isSkipRequested)
            {
                BeginExit();
                return true;
            }

            // Calculate speed based on curvature (slow down on turns)
            float curvature = _path.GetCurvatureAtT(_currentT);
            float curvatureIntensity = Mathf.Clamp(curvature / _config.CurvatureThreshold, 0f, 1f);
            float targetSpeed = Mathf.Lerp(_config.BaseSpeed, _config.MinSpeedOnTurns, curvatureIntensity);
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, delta * 3f);

            // Advance along path
            float distanceDelta = _currentSpeed * delta;
            float tDelta = distanceDelta / _path.TotalLength;
            _currentT += tDelta;

            // Check if reached end
            if (_currentT >= 1f)
            {
                _currentT = 1f;
                BeginExit();
                return true;
            }

            // Get path position and direction
            var position = _path.GetPositionAtT(_currentT);
            var tangent = _path.GetTangentAtT(_currentT);

            // Calculate roll based on turn direction and curvature
            float turnDirection = _path.GetTurnDirectionAtT(_currentT);
            float targetRoll = turnDirection * curvatureIntensity * _config.MaxRollDegrees;
            _currentRoll = Mathf.Lerp(_currentRoll, targetRoll, delta * _config.RollSmoothing);

            // Calculate FOV based on speed
            float speedNormalized = (_currentSpeed - _config.MinSpeedOnTurns) /
                                   (_config.BaseSpeed - _config.MinSpeedOnTurns);
            speedNormalized = Mathf.Clamp(speedNormalized, 0f, 1f);
            float targetFov = _config.BaseFov + _config.MaxFovIncrease * speedNormalized;
            _currentFov = Mathf.Lerp(_currentFov, targetFov, delta * 5f);
            _camera.Fov = _currentFov;

            // Update speed factor for post-processing
            SpeedFactor = speedNormalized * _config.PostProcessMultiplier;

            // Apply transform with roll
            _camera.GlobalTransform = CalculateTransform(position, tangent, _currentRoll);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates camera transform at position looking along tangent with roll.
    /// </summary>
    private Transform3D CalculateTransform(Vector3 position, Vector3 tangent, float rollDegrees)
    {
        // Look ahead slightly for smoother orientation
        float lookAheadT = Mathf.Min(_currentT + _config.LookAheadT, 1f);
        Vector3 lookTarget;

        if (_path != null && _currentT < 0.99f)
        {
            lookTarget = _path.GetPositionAtT(lookAheadT);
        }
        else
        {
            lookTarget = position + tangent * 10f;
        }

        // Create base look-at transform
        var lookDir = (lookTarget - position).Normalized();
        if (lookDir.LengthSquared() < 0.001f)
            lookDir = tangent;

        // Use world up as reference, with fallback for vertical paths
        var up = Vector3.Up;
        if (Mathf.Abs(lookDir.Dot(up)) > 0.99f)
        {
            up = Vector3.Forward;
        }

        var right = lookDir.Cross(up).Normalized();
        up = right.Cross(lookDir).Normalized();

        // Apply roll
        float rollRad = Mathf.DegToRad(rollDegrees);
        var rollRotation = new Basis(lookDir, rollRad);
        up = rollRotation * up;
        right = rollRotation * right;

        // Construct basis: -Z forward (Godot convention), Y up, X right
        var basis = new Basis(right, up, -lookDir);
        return new Transform3D(basis, position);
    }

    /// <summary>
    /// Calculates a simple look-at transform (no roll) for enter/exit transitions.
    /// </summary>
    private static Transform3D CalculateTransformLookingAt(Vector3 position, Vector3 target)
    {
        var lookDir = (target - position).Normalized();
        if (lookDir.LengthSquared() < 0.001f)
            lookDir = Vector3.Forward;

        // Use world up as reference, with fallback for vertical directions
        var up = Vector3.Up;
        if (Mathf.Abs(lookDir.Dot(up)) > 0.99f)
        {
            up = Vector3.Forward;
        }

        var right = lookDir.Cross(up).Normalized();
        up = right.Cross(lookDir).Normalized();

        // Construct basis: -Z forward (Godot convention), Y up, X right
        var basis = new Basis(right, up, -lookDir);
        return new Transform3D(basis, position);
    }

    private void BeginExit()
    {
        if (_camera == null || !_isActive)
            return;

        _isActive = false;

        // Calculate exit target (original camera position)
        Transform3D exitTarget;
        if (_originalCameraParent != null && GodotObject.IsInstanceValid(_originalCameraParent))
        {
            exitTarget = _originalCameraParent.GlobalTransform * _originalCameraLocalTransform;
        }
        else
        {
            exitTarget = _camera.GlobalTransform;
        }

        _exitTransition.Begin(_camera.GlobalTransform, exitTarget, _config.ExitTransitionDuration);
        _enterTransition.Cancel();

        Log.Debug(LogCategory.Sim, "FlybyCamera: Beginning exit transition");
    }

    private void CompleteExit()
    {
        if (_camera == null)
            return;

        // Reparent camera back to original parent
        _cameraHost?.RemoveChild(_camera);
        _originalCameraParent?.AddChild(_camera);

        // Restore original local transform and FOV
        _camera.Transform = _originalCameraLocalTransform;
        _camera.Fov = _originalFov;

        _path = null;
        _originalCameraParent = null;
        SpeedFactor = 0f;

        Log.Info(LogCategory.Sim, "FlybyCamera: Flyby complete");
        OnFlybyComplete?.Invoke();
    }

    /// <summary>
    /// Immediately cancels the flyby without transition.
    /// </summary>
    public void ForceCancel()
    {
        if (_camera == null)
            return;

        _enterTransition.Cancel();
        _exitTransition.Cancel();

        if (_isActive || _originalCameraParent != null)
        {
            // Reparent camera back
            _cameraHost?.RemoveChild(_camera);
            _originalCameraParent?.AddChild(_camera);

            _camera.Transform = _originalCameraLocalTransform;
            _camera.Fov = _originalFov;
        }

        _isActive = false;
        _path = null;
        _originalCameraParent = null;
        SpeedFactor = 0f;

        Log.Debug(LogCategory.Sim, "FlybyCamera: Force cancelled");
    }

    /// <summary>
    /// Resets the controller state.
    /// </summary>
    public void Reset()
    {
        ForceCancel();
    }
}
