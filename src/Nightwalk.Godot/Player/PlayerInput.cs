using System;
using Godot;
using Nightwalk.Core.Input;

namespace Nightwalk.Godot.Player;

/// <summary>
/// Handles raw input gathering and mouse look for the player.
/// Plain C# class - called from PlayerEntity each frame.
/// </summary>
public sealed class PlayerInput
{
    // Static StringNames to avoid per-frame string allocations
    private static readonly StringName ActionMoveForward = "move_forward";
    private static readonly StringName ActionMoveBack = "move_back";
    private static readonly StringName ActionMoveLeft = "move_left";
    private static readonly StringName ActionMoveRight = "move_right";
    private static readonly StringName ActionRun = "run";
    private static readonly StringName ActionJump = "jump";
    private static readonly StringName ActionToggleFlight = "toggle_flight";
    private static readonly StringName ActionToggleBirdsEye = "toggle_birds_eye";
    private static readonly StringName ActionToggleScreensaver = "toggle_screensaver";
    private static readonly StringName ActionInteract = "interact";

    // Pitch limits (in radians)
    private const float MaxPitch = Mathf.Pi / 2f - 0.1f;  // ~85 degrees
    private const float MinPitch = -Mathf.Pi / 2f + 0.1f;

    private float _mouseSensitivity;
    private float _contentScaleCompensation = 1f;

    /// <summary>
    /// The current input state for this frame.
    /// </summary>
    public PlayerInputState State { get; } = new();

    /// <summary>
    /// When false, player input is ignored (for pause, menus, etc.)
    /// </summary>
    public bool InputEnabled { get; set; } = true;

    /// <summary>
    /// Configures input parameters.
    /// </summary>
    public void Configure(float mouseSensitivity)
    {
        _mouseSensitivity = mouseSensitivity;
    }

    /// <summary>
    /// Updates the content scale compensation factor. Call when resolution changes.
    /// </summary>
    public void UpdateContentScaleCompensation()
    {
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        var mainWindow = sceneTree?.Root;
        if (mainWindow == null)
        {
            _contentScaleCompensation = 1f;
            return;
        }

        var contentScaleSize = mainWindow.ContentScaleSize;
        if (contentScaleSize == Vector2I.Zero)
        {
            _contentScaleCompensation = 1f;
            return;
        }

        var windowSize = DisplayServer.WindowGetSize();
        if (windowSize.X == 0 || contentScaleSize.X == 0)
        {
            _contentScaleCompensation = 1f;
            return;
        }

        _contentScaleCompensation = (float)windowSize.X / contentScaleSize.X;
    }

    /// <summary>
    /// Gets the current mouse sensitivity.
    /// </summary>
    public float MouseSensitivity => _mouseSensitivity;

    /// <summary>
    /// Process a single input event (called from _Input).
    /// Returns true if the event was consumed.
    /// </summary>
    public bool ProcessEvent(
        InputEvent @event,
        Viewport viewport,
        bool isBirdsEyeActive,
        Action<float> onBirdsEyeZoom,
        Action<int> onToolCycle)
    {
        if (!InputEnabled) return false;

        // Handle mouse look when captured
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            // Compensate for content scaling - mouse deltas are scaled down when rendering
            // at lower resolution, so we scale them back up using cached compensation factor
            State.LookDeltaX = mouseMotion.Relative.X * _contentScaleCompensation;
            State.LookDeltaY = mouseMotion.Relative.Y * _contentScaleCompensation;
            viewport.SetInputAsHandled();
            return true;
        }

        // Handle mouse wheel for tool cycling or bird's eye zoom
        if (@event is InputEventMouseButton mouseWheel && mouseWheel.Pressed)
        {
            if (mouseWheel.ButtonIndex == MouseButton.WheelUp)
            {
                if (isBirdsEyeActive)
                    onBirdsEyeZoom(1f);
                else
                    onToolCycle(-1); // Previous tool
                viewport.SetInputAsHandled();
                return true;
            }
            else if (mouseWheel.ButtonIndex == MouseButton.WheelDown)
            {
                if (isBirdsEyeActive)
                    onBirdsEyeZoom(-1f);
                else
                    onToolCycle(1); // Next tool
                viewport.SetInputAsHandled();
                return true;
            }
        }

        // Track jump press (single frame)
        if (@event.IsActionPressed(ActionJump))
        {
            State.JumpPressed = true;
            viewport.SetInputAsHandled();
            return true;
        }

        // Track flight mode toggle (single frame)
        if (@event.IsActionPressed(ActionToggleFlight))
        {
            State.ViewModeToggled = true;
            viewport.SetInputAsHandled();
            return true;
        }

        // Track bird's eye toggle (single frame)
        if (@event.IsActionPressed(ActionToggleBirdsEye))
        {
            State.BirdsEyeToggled = true;
            viewport.SetInputAsHandled();
            return true;
        }

        // Track screensaver toggle (single frame)
        if (@event.IsActionPressed(ActionToggleScreensaver))
        {
            State.ScreensaverToggled = true;
            viewport.SetInputAsHandled();
            return true;
        }

        // Track interact press (single frame)
        if (@event.IsActionPressed(ActionInteract))
        {
            State.InteractPressed = true;
            viewport.SetInputAsHandled();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gather continuous input state (called from _PhysicsProcess).
    /// </summary>
    public void GatherContinuousInput()
    {
        State.MoveForward = Input.GetAxis(ActionMoveBack, ActionMoveForward);
        State.MoveRight = Input.GetAxis(ActionMoveLeft, ActionMoveRight);
        State.RunHeld = Input.IsActionPressed(ActionRun);
        State.JumpHeld = Input.IsActionPressed(ActionJump);
        // Jetpack activates when holding jump while airborne
        State.JetpackHeld = State.JumpHeld;
    }

    /// <summary>
    /// Apply mouse look to body rotation and head pitch.
    /// </summary>
    public void ApplyLook(Node3D body, Node3D head)
    {
        if (head == null) return;

        // Horizontal rotation (yaw) - rotate the whole body
        body.RotateY(-State.LookDeltaX * _mouseSensitivity);

        // Vertical rotation (pitch) - rotate just the head
        // Calculate new pitch and set directly to prevent Y/Z rotation accumulation
        var pitch = head.Rotation.X - State.LookDeltaY * _mouseSensitivity;
        pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
        head.Rotation = new Vector3(pitch, 0, 0);
    }

    /// <summary>
    /// Clear single-frame inputs (called at end of physics frame).
    /// </summary>
    public void ClearFrameInputs()
    {
        State.JumpPressed = false;
        State.ViewModeToggled = false;
        State.BirdsEyeToggled = false;
        State.ScreensaverToggled = false;
        State.InteractPressed = false;
        State.LookDeltaX = 0;
        State.LookDeltaY = 0;
    }
}
