using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Events;
using Nightwalk.Core.GameState;
using Nightwalk.Core.GameState.Events;
using Nightwalk.Godot.Camera;
using Nightwalk.Godot.Ziplines;

namespace Nightwalk.Godot.Player;

/// <summary>
/// Handles view mode cycling and bird's eye camera control.
/// Keeps camera transitions and zoom isolated from player movement logic.
/// </summary>
public sealed class PlayerViewModes
{
    private readonly GameDataService _data;
    private IEventBus? _eventBus;
    private Camera3D? _camera;
    private Node3D? _head;
    private ZiplineRider? _ziplineRider;

    private ViewMode _mode = ViewMode.Walk;
    private bool _birdsEyeActive;
    private bool _transitioningOut;
    private bool _screensaverActive;
    private ScreensaverController? _screensaverController;

    private float _birdsEyeYaw;
    private float _birdsEyePitch;
    private float _birdsEyeHeight;
    private float _birdsEyeTargetHeight;

    private Vector3 _savedHeadRotation;
    private bool _hasSavedHeadRotation;

    private readonly TransformTransition _cameraTransition = new();

    /// <summary>Current active view mode.</summary>
    public ViewMode Mode => _mode;

    /// <summary>True when flight movement should be used.</summary>
    public bool IsFlightModeActive => _mode == ViewMode.Flight;

    /// <summary>True while bird's eye camera is active or transitioning.</summary>
    public bool IsBirdsEyeCameraActive => _birdsEyeActive || _cameraTransition.IsActive;

    /// <summary>True while screensaver mode is active.</summary>
    public bool IsScreensaverActive => _screensaverActive || (_screensaverController?.IsActive ?? false);

    /// <summary>True when player movement should be frozen.</summary>
    public bool IsMovementLocked => _birdsEyeActive || _cameraTransition.IsActive || IsScreensaverActive;

    /// <summary>
    /// Creates a view mode controller using gameplay config.
    /// </summary>
    public PlayerViewModes(GameDataService data)
    {
        _data = data;
    }

    /// <summary>
    /// Sets the event bus for broadcasting mode changes.
    /// </summary>
    public void SetEventBus(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Sets references needed for camera control.
    /// </summary>
    public void SetReferences(Camera3D camera, Node3D head, ZiplineRider ziplineRider)
    {
        _camera = camera;
        _head = head;
        _ziplineRider = ziplineRider;
    }

    /// <summary>
    /// Sets the screensaver controller reference.
    /// </summary>
    public void SetScreensaverController(ScreensaverController controller)
    {
        _screensaverController = controller;
        _screensaverController.OnExitComplete += OnScreensaverExitComplete;
    }

    /// <summary>
    /// Resets view mode state to defaults (used on session reset).
    /// </summary>
    public void Reset(Vector3 playerPosition, float bodyYaw)
    {
        _mode = ViewMode.Walk;
        _birdsEyeActive = false;
        _transitioningOut = false;
        _screensaverActive = false;
        _screensaverController?.ForceCancel();
        _cameraTransition.Cancel();
        _hasSavedHeadRotation = false;
        RestoreCameraToHead();

        _birdsEyeYaw = bodyYaw;
        _birdsEyePitch = Mathf.DegToRad(_data.Gameplay.ViewModes.BirdsEyePitchDegrees);
        _birdsEyeHeight = 0f;
        _birdsEyeTargetHeight = _data.Gameplay.ViewModes.BirdsEyeHeight;
    }

    /// <summary>
    /// Re-applies config constraints after a hot reload.
    /// </summary>
    public void Reconfigure(Vector3 playerPosition)
    {
        var viewConfig = _data.Gameplay.ViewModes;
        float minHeight = viewConfig.BirdsEyeMinHeight;
        float maxHeight = viewConfig.BirdsEyeMaxHeight;

        _birdsEyeTargetHeight = Mathf.Clamp(_birdsEyeTargetHeight, minHeight, maxHeight);
        _birdsEyeHeight = Mathf.Clamp(_birdsEyeHeight, minHeight, maxHeight);

        float minPitch = Mathf.DegToRad(viewConfig.BirdsEyePitchMinDegrees);
        float maxPitch = Mathf.DegToRad(viewConfig.BirdsEyePitchMaxDegrees);
        _birdsEyePitch = Mathf.Clamp(_birdsEyePitch, minPitch, maxPitch);

        if (_birdsEyeActive && _camera != null)
        {
            _camera.GlobalTransform = BuildBirdsEyeTransform(
                playerPosition,
                _birdsEyeHeight,
                _birdsEyeYaw,
                _birdsEyePitch
            );
        }
    }

    /// <summary>
    /// Cycles between Walk and Flight modes (TAB key).
    /// Bird's eye is toggled separately via ToggleBirdsEye.
    /// </summary>
    public void CycleMode(Vector3 playerPosition, float bodyYaw)
    {
        // If in bird's eye, exit to the opposite of what we'd normally go to
        if (_birdsEyeActive)
        {
            SetMode(ViewMode.Walk, playerPosition, bodyYaw);
            return;
        }

        ViewMode next = _mode switch
        {
            ViewMode.Walk => ViewMode.Flight,
            ViewMode.Flight => ViewMode.Walk,
            _ => ViewMode.Walk
        };

        SetMode(next, playerPosition, bodyYaw);
    }

    /// <summary>
    /// Toggles bird's eye view on/off (Q key).
    /// </summary>
    public void ToggleBirdsEye(Vector3 playerPosition, float bodyYaw)
    {
        if (_birdsEyeActive)
        {
            // Exit to previous mode (Walk or Flight)
            SetMode(_mode == ViewMode.BirdsEye ? ViewMode.Walk : _mode, playerPosition, bodyYaw);
        }
        else
        {
            SetMode(ViewMode.BirdsEye, playerPosition, bodyYaw);
        }
    }

    /// <summary>
    /// Toggles screensaver mode on/off (N key).
    /// </summary>
    public void ToggleScreensaver(Vector3 playerPosition)
    {
        if (_screensaverController == null || _camera == null || _head == null)
            return;

        if (IsScreensaverActive)
        {
            // Request exit - will transition back smoothly
            _screensaverController.RequestExit();
        }
        else
        {
            // Exit bird's eye if active
            if (_birdsEyeActive)
            {
                _birdsEyeActive = false;
                _transitioningOut = false;
                _cameraTransition.Cancel();
            }

            // Save head rotation before entering screensaver
            _savedHeadRotation = _head.Rotation;
            _head.Rotation = Vector3.Zero;
            _hasSavedHeadRotation = true;

            // Start screensaver
            _screensaverActive = _screensaverController.Start(playerPosition);
            if (_screensaverActive)
            {
                _ziplineRider?.Detach(applyJump: false);
            }
        }
    }

    private void OnScreensaverExitComplete()
    {
        _screensaverActive = false;

        // Restore head rotation
        if (_hasSavedHeadRotation && _head != null)
        {
            _head.Rotation = _savedHeadRotation;
        }
        _hasSavedHeadRotation = false;
    }

    /// <summary>
    /// Sets a specific view mode and triggers transitions if needed.
    /// </summary>
    public void SetMode(ViewMode mode, Vector3 playerPosition, float bodyYaw)
    {
        if (_mode == mode && !_birdsEyeActive)
            return;

        bool changed = false;
        if (mode == ViewMode.BirdsEye)
        {
            changed = EnterBirdsEye(playerPosition, bodyYaw);
        }
        else if (_birdsEyeActive)
        {
            changed = ExitBirdsEye(mode);
        }
        else
        {
            _mode = mode;
            changed = true;
        }

        if (changed)
        {
            _eventBus?.Publish(new ViewModeChangedEvent(_mode));
        }
    }

    /// <summary>
    /// Adjusts target height for bird's eye zoom (mouse wheel).
    /// </summary>
    public void AdjustBirdsEyeZoom(float wheelDirection)
    {
        if (!IsBirdsEyeCameraActive) return;

        var viewConfig = _data.Gameplay.ViewModes;
        float minHeight = viewConfig.BirdsEyeMinHeight;
        float maxHeight = viewConfig.BirdsEyeMaxHeight;
        _birdsEyeTargetHeight = Mathf.Clamp(
            _birdsEyeTargetHeight - wheelDirection * viewConfig.BirdsEyeZoomStep,
            minHeight,
            maxHeight
        );
    }

    /// <summary>
    /// Updates camera state; returns true when movement should be locked.
    /// </summary>
    public bool Update(float dt, Vector3 playerPosition, float bodyYaw, float lookDeltaX, float lookDeltaY, float mouseSensitivity)
    {
        if (_camera == null || _head == null) return false;

        if (_cameraTransition.IsActive)
        {
            // Transition between the head camera and bird's eye camera.
            var target = _transitioningOut
                ? _head.GlobalTransform
                : BuildBirdsEyeTransform(playerPosition, _birdsEyeTargetHeight, _birdsEyeYaw, _birdsEyePitch);

            if (_cameraTransition.TryStep(dt, target, out var transform))
            {
                _camera.GlobalTransform = transform;
            }

            if (!_cameraTransition.IsActive && _transitioningOut)
            {
                _birdsEyeActive = false;
                _transitioningOut = false;
                RestoreCameraToHead();
            }

            return IsMovementLocked;
        }

        if (!_birdsEyeActive)
            return false;

        // Bird's eye view: tilt only, no player movement.
        ApplyBirdsEyeLook(lookDeltaX, lookDeltaY, mouseSensitivity);
        UpdateBirdsEyeHeight(dt);
        _camera.GlobalTransform = BuildBirdsEyeTransform(playerPosition, _birdsEyeHeight, _birdsEyeYaw, _birdsEyePitch);
        return true;
    }

    private bool EnterBirdsEye(Vector3 playerPosition, float bodyYaw)
    {
        if (_camera == null || _head == null) return false;

        _mode = ViewMode.BirdsEye;
        _birdsEyeActive = true;
        _transitioningOut = false;
        _ziplineRider?.Detach(applyJump: false);

        _savedHeadRotation = _head.Rotation;
        _head.Rotation = Vector3.Zero;
        _hasSavedHeadRotation = true;

        _birdsEyeYaw = bodyYaw;
        _birdsEyePitch = Mathf.DegToRad(_data.Gameplay.ViewModes.BirdsEyePitchDegrees);
        _birdsEyeTargetHeight = Mathf.Clamp(
            _data.Gameplay.ViewModes.BirdsEyeHeight,
            _data.Gameplay.ViewModes.BirdsEyeMinHeight,
            _data.Gameplay.ViewModes.BirdsEyeMaxHeight
        );

        _birdsEyeHeight = _camera.GlobalPosition.Y - playerPosition.Y;
        var target = BuildBirdsEyeTransform(playerPosition, _birdsEyeTargetHeight, _birdsEyeYaw, _birdsEyePitch);
        _cameraTransition.Begin(_camera.GlobalTransform, target, _data.Gameplay.ViewModes.BirdsEyeTransitionDuration);
        return true;
    }

    private bool ExitBirdsEye(ViewMode nextMode)
    {
        if (_camera == null || _head == null) return false;

        _mode = nextMode;
        _transitioningOut = true;
        _cameraTransition.Begin(_camera.GlobalTransform, _head.GlobalTransform, _data.Gameplay.ViewModes.BirdsEyeTransitionDuration);
        return true;
    }

    private void ApplyBirdsEyeLook(float lookDeltaX, float lookDeltaY, float mouseSensitivity)
    {
        var viewConfig = _data.Gameplay.ViewModes;
        float sensitivity = mouseSensitivity * viewConfig.BirdsEyeLookSensitivity;
        _birdsEyeYaw -= lookDeltaX * sensitivity;
        _birdsEyePitch -= lookDeltaY * sensitivity;

        float minPitch = Mathf.DegToRad(viewConfig.BirdsEyePitchMinDegrees);
        float maxPitch = Mathf.DegToRad(viewConfig.BirdsEyePitchMaxDegrees);
        _birdsEyePitch = Mathf.Clamp(_birdsEyePitch, minPitch, maxPitch);
    }

    private void UpdateBirdsEyeHeight(float dt)
    {
        var viewConfig = _data.Gameplay.ViewModes;
        // Smoothly approach target zoom to avoid abrupt height changes.
        _birdsEyeHeight = Mathf.MoveToward(
            _birdsEyeHeight,
            _birdsEyeTargetHeight,
            viewConfig.BirdsEyeZoomSpeed * dt
        );
    }

    private Transform3D BuildBirdsEyeTransform(Vector3 playerPosition, float height, float yaw, float pitch)
    {
        var cameraPosition = playerPosition + Vector3.Up * height;
        var basis = Basis.FromEuler(new Vector3(pitch, yaw, 0f));
        return new Transform3D(basis, cameraPosition);
    }

    private void RestoreCameraToHead()
    {
        if (_camera != null)
        {
            _camera.Position = Vector3.Zero;
            _camera.Rotation = Vector3.Zero;
        }

        if (_hasSavedHeadRotation && _head != null)
        {
            _head.Rotation = _savedHeadRotation;
        }

        _hasSavedHeadRotation = false;
    }

    private sealed class TransformTransition
    {
        private Transform3D _start;
        private Transform3D _target;
        private float _duration;
        private float _elapsed;

        public bool IsActive { get; private set; }

        /// <summary>
        /// Starts a camera transform transition.
        /// </summary>
        public void Begin(Transform3D start, Transform3D target, float duration)
        {
            _start = start;
            _target = target;
            _duration = Mathf.Max(0f, duration);
            _elapsed = 0f;
            IsActive = _duration > 0f;
        }

        /// <summary>
        /// Advances the transition and outputs the interpolated transform.
        /// </summary>
        public bool TryStep(float dt, Transform3D target, out Transform3D transform)
        {
            if (!IsActive)
            {
                transform = default;
                return false;
            }

            _target = target;
            _elapsed += dt;
            float blend = _duration > 0f ? Mathf.Clamp(_elapsed / _duration, 0f, 1f) : 1f;
            float eased = Mathf.SmoothStep(0f, 1f, blend);

            var startRot = _start.Basis.GetRotationQuaternion();
            var targetRot = _target.Basis.GetRotationQuaternion();
            var rot = startRot.Slerp(targetRot, eased);
            var basis = new Basis(rot);
            var origin = _start.Origin.Lerp(_target.Origin, eased);
            transform = new Transform3D(basis, origin);

            if (blend >= 1f)
                IsActive = false;

            return true;
        }

        /// <summary>
        /// Cancels any active transition.
        /// </summary>
        public void Cancel()
        {
            IsActive = false;
        }
    }
}
