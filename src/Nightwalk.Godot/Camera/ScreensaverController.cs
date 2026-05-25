using System;
using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Godot.Util;

namespace Nightwalk.Godot.Camera;

/// <summary>
/// Orchestrates the screensaver/idle flyby mode.
/// Owns the waypoint generator and drives the flyby camera with continuous path updates.
/// </summary>
public sealed class ScreensaverController
{
    private ScreensaverConfig _config;
    private readonly ScreensaverWaypointGenerator _waypointGenerator;

    private Camera3D? _camera;
    private Node3D? _cameraHost;
    private Node3D? _originalCameraParent;
    private Transform3D _originalCameraLocalTransform;
    private float _originalFov;

    private FlybyPath? _currentPath;
    private float _currentT;
    private float _currentRoll;
    private float _currentSpeed;
    private int _lastWaypointIndex;

    // Smoothed camera orientation
    private Vector3 _smoothedLookDirection = Vector3.Forward;
    private Vector3 _smoothedUp = Vector3.Up;

    private bool _isActive;
    private bool _exitRequested;

    private SmoothTransition _enterTransition;
    private SmoothTransition _exitTransition;

    /// <summary>
    /// Whether screensaver mode is currently active (including transitions).
    /// </summary>
    public bool IsActive => _isActive || _enterTransition.IsActive || _exitTransition.IsActive;

    /// <summary>
    /// Current camera position for chunk loading coordination.
    /// </summary>
    public Vector3 CameraPosition { get; private set; }

    /// <summary>
    /// Fired when screensaver exits (for restoring player view).
    /// </summary>
    public event Action? OnExitComplete;

    public ScreensaverController(ScreensaverConfig config)
    {
        _config = config;
        _waypointGenerator = new ScreensaverWaypointGenerator(config);
    }

    /// <summary>
    /// Updates the configuration (for hot-reload).
    /// </summary>
    public void Configure(ScreensaverConfig config)
    {
        _config = config;
        _waypointGenerator.Configure(config);
    }

    /// <summary>
    /// Sets the camera and host node for screensaver mode.
    /// </summary>
    public void SetCamera(Camera3D camera, Node3D cameraHost)
    {
        _camera = camera;
        _cameraHost = cameraHost;
    }

    /// <summary>
    /// Sets the chunk manager for waypoint generation.
    /// </summary>
    public void SetChunkManager(ChunkManager chunkManager)
    {
        _waypointGenerator.SetChunkManager(chunkManager);
    }

    /// <summary>
    /// Starts screensaver mode from the given position.
    /// </summary>
    public bool Start(Vector3 startPosition)
    {
        if (_camera == null || _cameraHost == null)
            return false;

        if (_isActive)
        {
            Log.Debug(LogCategory.Sim, "ScreensaverController: Already active, ignoring start");
            return false;
        }

        // Initialize waypoint generator (uses street-level altitude)
        _waypointGenerator.Initialize(startPosition);

        if (_waypointGenerator.WaypointCount < 2)
        {
            Log.Debug(LogCategory.Sim, "ScreensaverController: Not enough waypoints generated");
            return false;
        }

        // Create initial path
        _currentPath = new FlybyPath(_waypointGenerator.Waypoints, 0f);

        if (_currentPath.TotalLength < 5f)
        {
            Log.Debug(LogCategory.Sim, "ScreensaverController: Path too short");
            return false;
        }

        _isActive = true;
        _exitRequested = false;
        _currentT = 0f;
        _currentRoll = 0f;
        _currentSpeed = _config.BaseSpeed;
        _lastWaypointIndex = 0;

        // Initialize smoothed look direction from path
        var initialTangent = _currentPath.GetTangentAtT(0f);
        _smoothedLookDirection = initialTangent.Normalized();
        _smoothedUp = Vector3.Up;

        // Store original camera state
        _originalCameraParent = _camera.GetParent() as Node3D;
        _originalCameraLocalTransform = _camera.Transform;
        _originalFov = _camera.Fov;

        // Calculate starting transform
        var startPos = _currentPath.GetPositionAtT(0f);
        var lookAheadPos = _currentPath.GetPositionAtT(0.1f);
        var startTransform = CalculateTransformLookingAt(startPos, lookAheadPos);

        // Begin enter transition
        _enterTransition.Begin(_camera.GlobalTransform, startTransform, _config.TransitionDuration);
        _exitTransition.Cancel();

        // Reparent camera to host
        _originalCameraParent?.RemoveChild(_camera);
        _cameraHost.AddChild(_camera);

        CameraPosition = startPos;

        Log.Info(LogCategory.Sim, $"ScreensaverController: Started, path length={_currentPath.TotalLength:F1}m");
        return true;
    }

    /// <summary>
    /// Requests the screensaver to exit with a smooth transition.
    /// </summary>
    public void RequestExit()
    {
        if (!_isActive && !_enterTransition.IsActive)
            return;

        _exitRequested = true;
        Log.Debug(LogCategory.Sim, "ScreensaverController: Exit requested");
    }

    /// <summary>
    /// Updates the screensaver camera. Call each frame.
    /// </summary>
    /// <returns>True if screensaver is active or transitioning.</returns>
    public bool Update(float delta)
    {
        if (_camera == null || _currentPath == null)
            return false;

        // Handle enter transition
        if (_enterTransition.IsActive)
        {
            if (_enterTransition.TryStepTransform(delta, out var transform))
            {
                _camera.GlobalTransform = transform;
                CameraPosition = transform.Origin;
            }
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
                _camera.Fov = Mathf.Lerp(_camera.Fov, _originalFov, blend);
            }

            if (!_exitTransition.IsActive)
            {
                CompleteExit();
            }
            return true;
        }

        // Check for exit request
        if (_exitRequested)
        {
            BeginExit();
            return true;
        }

        // Main flight phase
        if (_isActive)
        {
            UpdateFlight(delta);
            return true;
        }

        return false;
    }

    private void UpdateFlight(float delta)
    {
        if (_currentPath == null || _camera == null)
            return;

        // Calculate speed based on path curvature
        float curvature = _currentPath.GetCurvatureAtT(_currentT);
        float curvatureIntensity = Mathf.Clamp(curvature / _config.CurvatureThreshold, 0f, 1f);
        float minSpeed = _config.BaseSpeed * _config.MinSpeedMultiplier;
        float targetSpeed = Mathf.Lerp(_config.BaseSpeed, minSpeed, curvatureIntensity);
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, delta * _config.SpeedSmoothing);

        // Advance along path
        float distanceDelta = _currentSpeed * delta;
        float tDelta = distanceDelta / _currentPath.TotalLength;
        _currentT += tDelta;

        // Check if we've passed waypoints and need to refresh
        CheckAndRefreshWaypoints();

        // Get path position and direction
        var position = _currentPath.GetPositionAtT(_currentT);
        var tangent = _currentPath.GetTangentAtT(_currentT);

        // Calculate roll based on turn direction
        float turnDirection = _currentPath.GetTurnDirectionAtT(_currentT);
        float targetRoll = turnDirection * curvatureIntensity * _config.MaxRollDegrees;
        _currentRoll = Mathf.Lerp(_currentRoll, targetRoll, delta * _config.RollSmoothing);

        // Apply transform with smoothed orientation
        _camera.GlobalTransform = CalculateSmoothedTransform(position, tangent, _currentRoll, delta);
        CameraPosition = position;
    }

    private void CheckAndRefreshWaypoints()
    {
        if (_currentPath == null)
            return;

        // Estimate which waypoint we're near based on T
        // This is approximate since the path is smoothed
        int waypointCount = _waypointGenerator.WaypointCount;
        int estimatedIndex = (int)(_currentT * (waypointCount - 1));

        // If we've passed waypoints, refresh the buffer
        int passedCount = estimatedIndex - _lastWaypointIndex;
        if (passedCount > 0)
        {
            // Keep at least 2 waypoints behind current position for smooth path
            int toRemove = Mathf.Max(0, passedCount - 2);

            if (toRemove > 0 && _waypointGenerator.RefreshWaypoints(toRemove))
            {
                // Rebuild path with updated waypoints
                RebuildPath();
                _lastWaypointIndex = 0;
            }
        }
    }

    private void RebuildPath()
    {
        if (_camera == null || _waypointGenerator.WaypointCount < 2)
            return;

        var currentPosition = _camera.GlobalPosition;

        // Create new path from updated waypoints
        var newPath = new FlybyPath(_waypointGenerator.Waypoints, 0f);

        // Find closest T on new path to current position
        _currentT = FindClosestT(newPath, currentPosition);
        _currentPath = newPath;

        Log.Debug(LogCategory.Sim, $"ScreensaverController: Rebuilt path, new T={_currentT:F3}");
    }

    private float FindClosestT(FlybyPath path, Vector3 position)
    {
        // Sample path to find closest point
        const int samples = 50;
        float bestT = 0f;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            var pathPos = path.GetPositionAtT(t);
            float distSq = position.DistanceSquaredTo(pathPos);

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestT = t;
            }
        }

        // Refine with binary search around best sample
        float searchRange = 1f / samples;
        for (int i = 0; i < 5; i++)
        {
            float tLow = Mathf.Max(0f, bestT - searchRange);
            float tHigh = Mathf.Min(1f, bestT + searchRange);

            var posLow = path.GetPositionAtT(tLow);
            var posHigh = path.GetPositionAtT(tHigh);

            if (position.DistanceSquaredTo(posLow) < position.DistanceSquaredTo(posHigh))
            {
                bestT = (bestT + tLow) / 2f;
            }
            else
            {
                bestT = (bestT + tHigh) / 2f;
            }

            searchRange /= 2f;
        }

        return bestT;
    }

    private Transform3D CalculateSmoothedTransform(Vector3 position, Vector3 tangent, float rollDegrees, float delta)
    {
        // Look ahead for smoother anticipation of turns
        float lookAheadT = Mathf.Min(_currentT + _config.LookAheadT, 1f);
        Vector3 lookTarget;

        if (_currentPath != null && _currentT < 0.95f)
        {
            lookTarget = _currentPath.GetPositionAtT(lookAheadT);
        }
        else
        {
            lookTarget = position + tangent * 20f;
        }

        var targetLookDir = (lookTarget - position).Normalized();
        if (targetLookDir.LengthSquared() < 0.001f)
            targetLookDir = tangent.Normalized();

        // Ensure smoothed direction is valid before slerp
        if (_smoothedLookDirection.LengthSquared() < 0.5f)
            _smoothedLookDirection = targetLookDir;
        else
            _smoothedLookDirection = _smoothedLookDirection.Normalized();

        // Smoothly interpolate look direction to avoid snapping
        float t = Mathf.Clamp(delta * _config.OrientationSmoothing, 0f, 1f);
        _smoothedLookDirection = _smoothedLookDirection.Lerp(targetLookDir, t).Normalized();

        // Ensure valid look direction
        if (_smoothedLookDirection.LengthSquared() < 0.5f)
            _smoothedLookDirection = targetLookDir;

        // Calculate up vector
        var targetUp = Vector3.Up;
        if (Mathf.Abs(_smoothedLookDirection.Dot(targetUp)) > 0.99f)
        {
            targetUp = Vector3.Forward;
        }

        // Ensure smoothed up is valid before lerp
        if (_smoothedUp.LengthSquared() < 0.5f)
            _smoothedUp = targetUp;
        else
            _smoothedUp = _smoothedUp.Normalized();

        // Smooth the up vector too
        _smoothedUp = _smoothedUp.Lerp(targetUp, t).Normalized();

        var right = _smoothedLookDirection.Cross(_smoothedUp).Normalized();
        var up = right.Cross(_smoothedLookDirection).Normalized();

        // Apply roll
        float rollRad = Mathf.DegToRad(rollDegrees);
        var rollRotation = new Basis(_smoothedLookDirection, rollRad);
        up = rollRotation * up;
        right = rollRotation * right;

        // Construct basis: -Z forward (Godot convention), Y up, X right
        var basis = new Basis(right, up, -_smoothedLookDirection);
        return new Transform3D(basis, position);
    }

    private static Transform3D CalculateTransformLookingAt(Vector3 position, Vector3 target)
    {
        var lookDir = (target - position).Normalized();
        if (lookDir.LengthSquared() < 0.001f)
            lookDir = Vector3.Forward;

        var up = Vector3.Up;
        if (Mathf.Abs(lookDir.Dot(up)) > 0.99f)
        {
            up = Vector3.Forward;
        }

        var right = lookDir.Cross(up).Normalized();
        up = right.Cross(lookDir).Normalized();

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

        _exitTransition.Begin(_camera.GlobalTransform, exitTarget, _config.TransitionDuration);
        _enterTransition.Cancel();

        Log.Debug(LogCategory.Sim, "ScreensaverController: Beginning exit transition");
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

        _currentPath = null;
        _originalCameraParent = null;

        Log.Info(LogCategory.Sim, "ScreensaverController: Exit complete");
        OnExitComplete?.Invoke();
    }

    /// <summary>
    /// Immediately cancels the screensaver without transition.
    /// </summary>
    public void ForceCancel()
    {
        if (_camera == null)
            return;

        bool wasActive = _isActive || _enterTransition.IsActive || _exitTransition.IsActive;

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
        _exitRequested = false;
        _currentPath = null;
        _originalCameraParent = null;

        Log.Debug(LogCategory.Sim, "ScreensaverController: Force cancelled");

        // Notify listeners so they can clear their state (e.g., PlayerViewModes._screensaverActive)
        if (wasActive)
        {
            OnExitComplete?.Invoke();
        }
    }

    /// <summary>
    /// Resets the controller state.
    /// </summary>
    public void Reset()
    {
        ForceCancel();
    }
}
