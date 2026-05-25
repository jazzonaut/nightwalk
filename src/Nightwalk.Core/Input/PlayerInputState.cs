namespace Nightwalk.Core.Input;

/// <summary>
/// Represents the player's input state for a single frame.
/// Updated by the platform-specific input adapter.
/// </summary>
public sealed class PlayerInputState
{
    /// <summary>
    /// Forward/backward movement input (-1 to 1).
    /// Positive = forward, negative = backward.
    /// </summary>
    public float MoveForward { get; set; }

    /// <summary>
    /// Left/right strafe input (-1 to 1).
    /// Positive = right, negative = left.
    /// </summary>
    public float MoveRight { get; set; }

    /// <summary>
    /// Mouse delta X this frame (horizontal look).
    /// </summary>
    public float LookDeltaX { get; set; }

    /// <summary>
    /// Mouse delta Y this frame (vertical look).
    /// </summary>
    public float LookDeltaY { get; set; }

    /// <summary>
    /// Jump button pressed this frame.
    /// </summary>
    public bool JumpPressed { get; set; }

    /// <summary>
    /// Jump button held down.
    /// </summary>
    public bool JumpHeld { get; set; }

    /// <summary>
    /// Run button held down.
    /// </summary>
    public bool RunHeld { get; set; }

    /// <summary>
    /// Jetpack/fly button held down (typically same as jump when airborne).
    /// </summary>
    public bool JetpackHeld { get; set; }

    /// <summary>
    /// View mode toggle pressed this frame (TAB key).
    /// Cycles between Walk and Flight modes.
    /// </summary>
    public bool ViewModeToggled { get; set; }

    /// <summary>
    /// Bird's eye view toggle pressed this frame (Q key).
    /// </summary>
    public bool BirdsEyeToggled { get; set; }

    /// <summary>
    /// Interact button pressed this frame (E key).
    /// </summary>
    public bool InteractPressed { get; set; }

    /// <summary>
    /// Screensaver mode toggle pressed this frame (N key).
    /// </summary>
    public bool ScreensaverToggled { get; set; }

    /// <summary>
    /// Resets all input to default state.
    /// </summary>
    public void Clear()
    {
        MoveForward = 0;
        MoveRight = 0;
        LookDeltaX = 0;
        LookDeltaY = 0;
        JumpPressed = false;
        JumpHeld = false;
        RunHeld = false;
        JetpackHeld = false;
        ViewModeToggled = false;
        BirdsEyeToggled = false;
        InteractPressed = false;
        ScreensaverToggled = false;
    }

    /// <summary>
    /// Returns true if there is any movement input.
    /// </summary>
    public bool HasMovementInput => MoveForward != 0 || MoveRight != 0;
}
