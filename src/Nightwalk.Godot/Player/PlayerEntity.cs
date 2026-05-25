using System;
using Godot;
using Nightwalk.Core.Audio;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Difficulty;
using Nightwalk.Core.Events;
using Nightwalk.Core.Scoring;
using Nightwalk.Godot.Audio;
using Nightwalk.Godot.Beacons;
using Nightwalk.Godot.Camera;
using Nightwalk.Godot.Ziplines;

namespace Nightwalk.Godot.Player;

/// <summary>
/// First-person player entity using CharacterBody3D.
/// Orchestrates input, movement, audio, and camera components.
/// </summary>
public partial class PlayerEntity : CharacterBody3D
{
    // FOV config
    private float _baseFov;
    private float _maxFovIncrease;
    private float _currentFov;

    // Components
    private readonly PlayerEnergy _energy = new();
    private readonly PlayerMovement _movement = new();
    private readonly PlayerAudio _audio = new();
    private readonly PlayerFallDamage _fallDamage = new();

    private Camera3D? _camera;
    private Node3D? _head;
    private readonly PlayerInput _input = new();
    private bool _initialized;

    private readonly PlayerViewModes _viewModes;

    // Previous-frame state (for transition detection in audio and fall damage)
    private bool _wasGrounded;
    private bool _wasFlightActive;

    // Game data for hot-reloadable config values
    private GameDataService _data;

    /// <summary>
    /// When false, player input is ignored (for pause, menus, etc.)
    /// </summary>
    public bool InputEnabled
    {
        get => _input.InputEnabled;
        set => _input.InputEnabled = value;
    }

    // Tools
    private PlayerToolManager _toolManager = null!;

    // Zipline riding
    private ZiplineRider _ziplineRider = null!;

    // Prism POV mode
    private PrismPOVController? _prismPOV;

    // Flyby camera
    private FlybyCamera? _flybyCamera;

    // Screensaver controller
    private ScreensaverController? _screensaverController;

    /// <summary>
    /// Current energy level (0 to max).
    /// </summary>
    public float Energy => _energy.Energy;

    /// <summary>
    /// Maximum energy capacity.
    /// </summary>
    public float EnergyMax => _energy.EnergyMax;

    /// <summary>
    /// Energy as percentage (0 to 1).
    /// </summary>
    public float EnergyPercent => _energy.EnergyPercent;

    /// <summary>
    /// Current movement speed (velocity magnitude).
    /// </summary>
    public float CurrentSpeed => Velocity.Length();

    /// <summary>
    /// Maximum possible speed (flight sprint speed).
    /// </summary>
    public float MaxSpeed => _movement.MaxSpeed;

    /// <summary>
    /// Current speed as a factor of max speed (0 to 1, clamped).
    /// </summary>
    public float SpeedFactor => MaxSpeed > 0 ? Mathf.Clamp(CurrentSpeed / MaxSpeed, 0f, 1f) : 0f;

    /// <summary>
    /// Whether flight mode is currently active.
    /// </summary>
    public bool IsFlightModeActive => _viewModes.IsFlightModeActive;

    /// <summary>
    /// Whether the player is currently riding a zipline.
    /// </summary>
    public bool IsRidingZipline => _ziplineRider.IsRiding;

    /// <summary>
    /// The head node that contains the camera. Used for attaching tools.
    /// </summary>
    public Node3D? Head => _head;

    /// <summary>
    /// The player's camera.
    /// </summary>
    public Camera3D? Camera => _camera;

    /// <summary>
    /// Fired when the player presses the interact button.
    /// </summary>
    public event Action? OnInteractPressed;

    public PlayerEntity(GameDataService data)
    {
        _data = data;
        _viewModes = new PlayerViewModes(data);
    }

    /// <summary>
    /// Sets the tool manager reference for input handling.
    /// </summary>
    public void SetToolManager(PlayerToolManager toolManager)
    {
        _toolManager = toolManager;
    }

    /// <summary>
    /// Sets the zipline rider for zipline mechanics.
    /// </summary>
    public void SetZiplineRider(ZiplineRider rider)
    {
        _ziplineRider = rider;
    }

    /// <summary>
    /// Sets the prism POV controller for prism interaction.
    /// </summary>
    public void SetPrismPOVController(PrismPOVController controller)
    {
        _prismPOV = controller;
    }

    /// <summary>
    /// Whether the player is currently in prism POV mode.
    /// </summary>
    public bool IsInPrismPOV => _prismPOV?.IsInPOV ?? false;

    /// <summary>
    /// Sets the flyby camera controller.
    /// </summary>
    public void SetFlybyCamera(FlybyCamera flybyCamera)
    {
        _flybyCamera = flybyCamera;
    }

    /// <summary>
    /// Sets the screensaver controller.
    /// </summary>
    public void SetScreensaverController(ScreensaverController screensaverController)
    {
        _screensaverController = screensaverController;
        _viewModes.SetScreensaverController(screensaverController);
    }

    /// <summary>
    /// Whether a flyby is currently active.
    /// </summary>
    public bool IsInFlyby => _flybyCamera?.IsActive ?? false;

    /// <summary>
    /// Speed factor from flyby camera for post-processing effects.
    /// </summary>
    public float FlybySpeedFactor => _flybyCamera?.SpeedFactor ?? 0f;

    /// <summary>
    /// Whether screensaver mode is currently active.
    /// </summary>
    public bool IsInScreensaver => _screensaverController?.IsActive ?? false;

    public override void _Ready()
    {
        // Create collision shape (capsule for player)
        var collision = new CollisionShape3D { Name = "CollisionShape" };
        var capsule = new CapsuleShape3D();
        capsule.Radius = 0.4f;
        capsule.Height = 3.0f;
        collision.Shape = capsule;
        collision.Position = new Vector3(0, 0.9f, 0); // Center capsule at player height
        AddChild(collision);

        // Find or create head node for camera
        _head = GetNodeOrNull<Node3D>("Head");
        if (_head == null)
        {
            _head = new Node3D { Name = "Head" };
            AddChild(_head);
            _head.Position = new Vector3(0, 2.8f, 0); // Eye height
        }

        // Find or create camera
        _camera = _head.GetNodeOrNull<Camera3D>("Camera3D");
        if (_camera == null)
        {
            _camera = new Camera3D { Name = "Camera3D" };
            _head.AddChild(_camera);
            _camera.Current = true;
        }

        // Apply configured FOV (Configure may have run before _Ready)
        if (_baseFov > 0)
        {
            _camera.Fov = _baseFov;
            _currentFov = _baseFov;
        }

        _initialized = true;
        Log.Info(LogCategory.Entity, "PlayerEntity initialized");
    }

    /// <summary>
    /// Initializes view mode references after all player dependencies are set.
    /// </summary>
    public void InitializeViewModes()
    {
        UpdateViewModeReferences();
    }

    private void UpdateViewModeReferences()
    {
        if (_camera == null || _head == null || _ziplineRider == null)
            return;

        _viewModes.SetReferences(_camera, _head, _ziplineRider);
    }

    /// <summary>
    /// Configures the entity from physics and gameplay config.
    /// </summary>
    public void Configure(IEventBus eventBus)
    {
        // Input
        _input.Configure(_data.Physics.Movement.MouseSensitivity);

        // Movement
        _movement.Configure(_data);

        // FOV
        _baseFov = _data.Physics.Flight.BaseFov;
        _maxFovIncrease = _data.Physics.Flight.MaxFovIncrease;
        _currentFov = _baseFov;

        // Apply base FOV to camera
        if (_camera != null)
        {
            _camera.Fov = _baseFov;
        }

        // Energy
        _energy.Configure(_data);

        // Audio
        _audio.Configure(_data);

        // Fall damage
        _fallDamage.Configure(_data);

        // View modes
        _viewModes.SetEventBus(eventBus);
        var playerPosition = IsInsideTree() ? GlobalPosition : Position;
        _viewModes.Reconfigure(playerPosition);

        Log.Debug(LogCategory.Config, $"PlayerEntity configured: WalkSpeed={_movement.WalkSpeed}, MaxEnergy={_energy.EnergyMax}");
    }

    /// <summary>
    /// Updates mouse input scaling for resolution changes.
    /// Call when window content scale changes.
    /// </summary>
    public void UpdateContentScaleCompensation()
    {
        _input.UpdateContentScaleCompensation();
    }

    /// <summary>
    /// Sets the SFX service for audio feedback.
    /// </summary>
    public void SetSfxService(ISfxService sfx)
    {
        _audio.SetSfxService(sfx);
    }

    /// <summary>
    /// Sets the synth loop manager for player loops (jetpack, etc.).
    /// </summary>
    public void SetSynthLoopManager(SynthLoopManager? synthLoopManager)
    {
        _audio.SetSynthLoopManager(synthLoopManager);
    }

    /// <summary>
    /// Sets the score service for fall damage tracking.
    /// </summary>
    public void SetScoreService(IScoreService scoreService)
    {
        _fallDamage.SetScoreService(scoreService);
    }

    /// <summary>
    /// Sets the difficulty service for scaled damage values.
    /// </summary>
    public void SetDifficultyService(IDifficultyService? difficultyService)
    {
        _fallDamage.SetDifficultyService(difficultyService);
        _energy.SetDifficultyService(difficultyService);
    }

    /// <summary>
    /// Sets the SFX manager for procedural fall damage sounds.
    /// </summary>
    public void SetFallDamageSfxManager(SfxManager? sfxManager)
    {
        _fallDamage.SetSfxManager(sfxManager);
    }

    /// <summary>
    /// Sets the synth loop manager for falling wind sounds.
    /// </summary>
    public void SetFallDamageSynthLoopManager(SynthLoopManager? synthLoopManager)
    {
        _fallDamage.SetSynthLoopManager(synthLoopManager);
    }


    /// <summary>
    /// Resets the player's velocity and movement state.
    /// Used when resetting the game session.
    /// </summary>
    public void ResetVelocity()
    {
        Velocity = Vector3.Zero;
        _viewModes.Reset(GlobalPosition, Rotation.Y);
        _energy.Reset();
        _input.State.Clear();
        _audio.Reset();
        _fallDamage.Reset();

        // Reset state tracking
        _wasGrounded = false;
        _wasFlightActive = false;
        _currentFov = _baseFov;
        if (_camera != null)
        {
            _camera.Fov = _baseFov;
        }

        // Detach from zipline if riding
        _ziplineRider.Detach(applyJump: false);

        // Cancel flyby if active
        _flybyCamera?.ForceCancel();

        // Cancel screensaver if active
        _screensaverController?.ForceCancel();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_initialized) return;

        // Handle screensaver input when active (N or Escape to exit)
        if (_screensaverController?.IsActive == true)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape || key.Keycode == Key.N)
                {
                    _screensaverController.RequestExit();
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
            // Consume all other input during screensaver
            GetViewport().SetInputAsHandled();
            return;
        }

        // Handle flyby camera input when active (Escape to skip)
        if (_flybyCamera?.IsActive == true)
        {
            if (@event is InputEventKey escKey && escKey.Keycode == Key.Escape && escKey.Pressed && !escKey.Echo)
            {
                _flybyCamera.RequestSkip();
                GetViewport().SetInputAsHandled();
                return;
            }
            // Consume all other input during flyby
            GetViewport().SetInputAsHandled();
            return;
        }

        // Handle prism POV input when active
        if (_prismPOV?.IsInPOV == true)
        {
            // Escape exits POV mode
            if (@event is InputEventKey escKey && escKey.Keycode == Key.Escape && escKey.Pressed && !escKey.Echo)
            {
                _prismPOV.ExitPOV();
                GetViewport().SetInputAsHandled();
                return;
            }

            // Mouse motion rotates prism
            if (@event is InputEventMouseMotion mm)
            {
                if (_prismPOV.HandleMouseMotion(mm))
                {
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }

            // Mouse wheel zooms
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                float zoomDir = mb.ButtonIndex switch
                {
                    MouseButton.WheelUp => 1f,
                    MouseButton.WheelDown => -1f,
                    _ => 0f
                };
                if (zoomDir != 0f && _prismPOV.HandleZoom(zoomDir))
                {
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
        }

        _input.ProcessEvent(
            @event,
            GetViewport(),
            _viewModes.IsBirdsEyeCameraActive,
            zoom => _viewModes.AdjustBirdsEyeZoom(zoom),
            direction => _toolManager.CycleTool(direction)
        );
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_initialized || _head == null) return;

        var dt = (float)delta;

        // Skip movement when input is disabled (paused, menus, etc.)
        if (!InputEnabled)
        {
            // Still update POV transitions when input disabled
            _prismPOV?.Update(dt);
            _flybyCamera?.Update(dt);
            _screensaverController?.Update(dt);
            // Clear any pending input state
            _input.State.Clear();
            return;
        }

        // Handle screensaver camera (takes full control during screensaver)
        if (_screensaverController?.IsActive == true)
        {
            _screensaverController.Update(dt);
            // Clear any pending input state
            _input.State.Clear();
            return;
        }

        // Handle flyby camera (takes full control during flyby)
        if (_flybyCamera?.IsActive == true)
        {
            _flybyCamera.Update(dt);
            // Clear any pending input state
            _input.State.Clear();
            return;
        }

        // Gather input
        GatherInput();

        // Handle view mode cycling (TAB - Walk/Flight)
        if (_input.State.ViewModeToggled)
        {
            _viewModes.CycleMode(GlobalPosition, Rotation.Y);
        }

        // Handle bird's eye toggle (Q key)
        if (_input.State.BirdsEyeToggled)
        {
            _viewModes.ToggleBirdsEye(GlobalPosition, Rotation.Y);
        }

        // Handle screensaver toggle (N key)
        if (_input.State.ScreensaverToggled)
        {
            _viewModes.ToggleScreensaver(GlobalPosition);
        }

        // Handle interact press (E key)
        if (_input.State.InteractPressed)
        {
            bool handledInteract = false;

            // Try prism POV toggle first
            if (_prismPOV != null)
            {
                if (_prismPOV.IsInPOV)
                {
                    _prismPOV.ExitPOV();
                    handledInteract = true;
                }
                else
                {
                    // Try to enter POV mode
                    var spaceState = GetWorld3D()?.DirectSpaceState;
                    if (spaceState != null && _prismPOV.TryEnterPOV(spaceState, _data.Beacons.Prism.MaxPlacementDistance))
                    {
                        handledInteract = true;
                    }
                }
            }

            // If holding the zipline tool and hovering a cable, attach on interact.
            if (!handledInteract)
            {
                var hoveredCable = _toolManager.GetHoveredZipline();
                if (hoveredCable != null && _ziplineRider.TryTransferTo(hoveredCable, _head.GlobalPosition, GlobalPosition))
                {
                    handledInteract = true;
                }
            }

            if (!handledInteract)
            {
                OnInteractPressed?.Invoke();
            }
        }

        bool isMovementLocked = _viewModes.Update(
            dt,
            GlobalPosition,
            Rotation.Y,
            _input.State.LookDeltaX,
            _input.State.LookDeltaY,
            _input.MouseSensitivity
        );

        // Birds eye view: freeze movement and only update camera tilt.
        if (isMovementLocked)
        {
            Velocity = Vector3.Zero;
            _input.State.JetpackHeld = false;
            UpdateAudio(dt, IsOnFloor());

            _wasGrounded = IsOnFloor();
            _wasFlightActive = _viewModes.IsFlightModeActive;

            // Update POV (last, to override birds-eye camera)
            _prismPOV?.Update(dt);

            // Clear single-frame inputs
            _input.ClearFrameInputs();
            return;
        }

        // Apply mouse look
        ApplyLook();

        // Get player forward direction for zipline mechanics
        var playerForward = -_head.GlobalTransform.Basis.Z;

        // Handle zipline riding
        if (_ziplineRider.IsRiding)
        {
            // Jump to detach (or transfer to highlighted zipline)
            if (_input.State.JumpPressed)
            {
                var hoveredCable = _toolManager.GetHoveredZipline();
                if (hoveredCable != null && _ziplineRider.TryTransferTo(hoveredCable, _head.GlobalPosition, GlobalPosition))
                {
                    // Successfully transferred to highlighted zipline
                }
                else
                {
                    Velocity = _ziplineRider.Detach(applyJump: true);
                    MoveAndSlide();
                }
            }
            else
            {
                // Process zipline movement - use direct position since cable path is validated during placement
                var newPosition = _ziplineRider.ProcessMovement(dt, _input.State.MoveForward, _input.State.RunHeld, playerForward, GlobalPosition);
                GlobalPosition = newPosition;
                Velocity = Vector3.Zero;
            }
        }
        else
        {
            // Jump to attach to highlighted zipline
            if (_input.State.JumpPressed)
            {
                var hoveredCable = _toolManager.GetHoveredZipline();
                if (hoveredCable != null && _ziplineRider.TryTransferTo(hoveredCable, _head.GlobalPosition, GlobalPosition))
                {
                    // Successfully attached to highlighted zipline
                }
            }

            // Auto-attach to nearby zipline when not in flight mode
            if (!_viewModes.IsFlightModeActive && !_ziplineRider.IsRiding)
            {
                _ziplineRider.TryAttach(_head.GlobalPosition, GlobalPosition, playerForward, dt);
            }

            // Calculate movement based on mode (only if not riding)
            if (!_ziplineRider.IsRiding)
            {
                Vector3 velocity;
                bool isActivelyFlying = false;
                if (_viewModes.IsFlightModeActive)
                {
                    // Check if player has movement input (actively flying vs just hovering)
                    isActivelyFlying = PlayerMovement.HasMovementInput(_input.State);
                    velocity = ProcessFlightMovement(dt);
                }
                else
                {
                    velocity = ProcessNormalMovement(Velocity, dt);

                    // Track downward velocity for fall damage (before MoveAndSlide modifies it)
                    _fallDamage.TrackVelocity(velocity.Y, IsOnFloor());
                }

                Velocity = velocity;
                MoveAndSlide();

                // Update flight score drain
                UpdateFlightDrain(dt, isActivelyFlying);
            }
        }

        // Energy regeneration when not actively jetpacking
        if (!_input.State.JetpackHeld && Velocity.Y >= 0)
        {
            _energy.Regenerate(dt);
        }

        // Update audio
        UpdateAudio(dt, IsOnFloor());

        // Check for fall damage
        UpdateFallDamage(IsOnFloor());

        // Update state tracking for next frame (must be after fall damage check)
        _wasGrounded = IsOnFloor();
        _wasFlightActive = _viewModes.IsFlightModeActive;

        // Update FOV based on speed (smooth interpolation)
        UpdateFov(dt);

        // Update prism POV camera (last, so it overrides other camera changes)
        _prismPOV?.Update(dt);

        // Clear single-frame inputs
        _input.ClearFrameInputs();
    }

    private Vector3 ProcessFlightMovement(float dt)
        => _movement.ProcessFlightMovement(Velocity, dt, _input.State, _head!.GlobalTransform.Basis);

    private Vector3 ProcessNormalMovement(Vector3 velocity, float dt)
        => _movement.ProcessNormalMovement(velocity, dt, IsOnFloor(), _input.State, Transform.Basis, _energy);

    private void UpdateFov(float dt)
    {
        if (_camera == null) return;

        // Skip FOV updates when POV or flyby controls the camera
        if (IsInPrismPOV || IsInFlyby) return;

        // Calculate target FOV based on speed (only increases in flight mode)
        float targetFov;
        if (_viewModes.IsFlightModeActive)
        {
            var fovIncrease = _maxFovIncrease * SpeedFactor;
            targetFov = _baseFov + fovIncrease;
        }
        else
        {
            targetFov = _baseFov;
        }

        // Smoothly interpolate current FOV toward target
        var fovLerpSpeed = 8.0f;  // How fast FOV changes
        _currentFov = Mathf.Lerp(_currentFov, targetFov, fovLerpSpeed * dt);
        _camera.Fov = _currentFov;
    }

    private void GatherInput() => _input.GatherContinuousInput();

    private void ApplyLook() => _input.ApplyLook(this, _head!);


    private void UpdateAudio(float dt, bool isGrounded)
    {
        _audio.Update(
            dt,
            isGrounded,
            _wasGrounded,
            Velocity,
            _viewModes.IsFlightModeActive,
            _wasFlightActive,
            _input.State.JetpackHeld,
            _input.State.RunHeld,
            _energy.HasEnergy,
            _energy.EnergyPercent,
            SpeedFactor,
            _movement.WalkSpeed);
    }

    private void UpdateFallDamage(bool isGrounded)
        => _fallDamage.UpdateFallDamage(isGrounded, _wasGrounded, _viewModes.IsFlightModeActive);

    private void UpdateFlightDrain(float dt, bool isActivelyFlying)
        => _fallDamage.UpdateFlightDrain(dt, isActivelyFlying);
}
