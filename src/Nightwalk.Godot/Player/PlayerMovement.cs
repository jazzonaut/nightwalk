using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Input;

namespace Nightwalk.Godot.Player;

/// <summary>
/// Handles all player movement calculations: walking, running, jumping, jetpack, flight.
/// Plain C# class - pure calculation, no Godot node inheritance.
/// </summary>
public sealed class PlayerMovement
{
    // Movement config
    private float _walkSpeed;
    private float _runSpeed;
    private float _acceleration;
    private float _friction;
    private float _gravity;
    private float _jumpForce;

    // Jetpack config
    private float _jetpackForce;
    private float _jetpackMaxVerticalSpeed;
    private float _jetpackHoverGravityScale;

    // Flight mode config
    private float _flightSpeed;
    private float _flightSprintMultiplier;

    /// <summary>
    /// Gets the walk speed for audio calculations.
    /// </summary>
    public float WalkSpeed => _walkSpeed;

    /// <summary>
    /// Maximum possible speed (flight sprint speed).
    /// </summary>
    public float MaxSpeed => _flightSpeed * _flightSprintMultiplier;

    /// <summary>
    /// Configures movement parameters from game data.
    /// </summary>
    public void Configure(GameDataService data)
    {
        // Movement
        _walkSpeed = data.Physics.Movement.WalkSpeed;
        _runSpeed = data.Physics.Movement.RunSpeed;
        _acceleration = data.Physics.Movement.Acceleration;
        _friction = data.Physics.Movement.Friction;
        _gravity = data.Physics.Movement.Gravity;
        _jumpForce = data.Physics.Jump.Force;

        // Jetpack
        _jetpackForce = data.Physics.Jetpack.AscendForce;
        _jetpackMaxVerticalSpeed = data.Physics.Jetpack.MaxVerticalSpeed;
        _jetpackHoverGravityScale = data.Physics.Jetpack.HoverGravityScale;

        // Flight
        _flightSpeed = data.Physics.Flight.Speed;
        _flightSprintMultiplier = data.Physics.Flight.SprintMultiplier;
    }

    /// <summary>
    /// Process normal movement (walk/run/jump/jetpack).
    /// Returns the new velocity.
    /// </summary>
    public Vector3 ProcessNormalMovement(
        Vector3 velocity,
        float dt,
        bool isGrounded,
        PlayerInputState input,
        Basis bodyBasis,
        PlayerEnergy energy)
    {
        // Handle jump from ground
        if (input.JumpPressed && isGrounded)
        {
            velocity.Y = _jumpForce;
            Log.Debug(LogCategory.Input, "Jump!");
        }
        // Handle jetpack when airborne
        else if (!isGrounded && input.JetpackHeld && energy.HasEnergy)
        {
            // Consume energy
            energy.ConsumeJetpack(dt);

            // Apply upward thrust
            velocity.Y += _jetpackForce * dt;

            // Clamp to max vertical speed
            velocity.Y = Mathf.Min(velocity.Y, _jetpackMaxVerticalSpeed);
        }
        // Apply gravity (reduced if holding jump with no energy - hover mode)
        else if (!isGrounded)
        {
            var gravityScale = (input.JetpackHeld && !energy.HasEnergy) ? _jetpackHoverGravityScale : 1.0f;
            velocity.Y -= _gravity * gravityScale * dt;
        }

        // Get movement direction relative to camera (horizontal only)
        var inputDir = new Vector2(input.MoveRight, -input.MoveForward).Normalized();
        var direction = (bodyBasis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

        // Calculate target speed
        var targetSpeed = input.RunHeld ? _runSpeed : _walkSpeed;

        // Apply movement with acceleration/friction
        if (direction != Vector3.Zero)
        {
            velocity.X = Mathf.MoveToward(velocity.X, direction.X * targetSpeed, _acceleration * dt);
            velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * targetSpeed, _acceleration * dt);
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, _friction * dt);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, _friction * dt);
        }

        return velocity;
    }

    /// <summary>
    /// Process flight movement (free 3D movement).
    /// Returns the new velocity.
    /// </summary>
    public Vector3 ProcessFlightMovement(
        Vector3 velocity,
        float dt,
        PlayerInputState input,
        Basis headBasis)
    {
        // Get input direction in local space
        var inputDir = new Vector2(input.MoveRight, -input.MoveForward);

        if (inputDir.LengthSquared() < 0.001f)
        {
            // No input - stop movement with friction
            return velocity.MoveToward(Vector3.Zero, _friction * dt);
        }

        inputDir = inputDir.Normalized();

        // Get the camera's forward and right vectors (includes pitch from head)
        var forward = -headBasis.Z;  // Camera looks down -Z
        var right = headBasis.X;

        // Calculate 3D movement direction based on camera orientation
        var direction = (forward * -inputDir.Y + right * inputDir.X).Normalized();

        // Apply movement with acceleration toward target velocity
        var speed = input.RunHeld ? _flightSpeed * _flightSprintMultiplier : _flightSpeed;
        var targetVelocity = direction * speed;

        return new Vector3(
            Mathf.MoveToward(velocity.X, targetVelocity.X, _acceleration * dt),
            Mathf.MoveToward(velocity.Y, targetVelocity.Y, _acceleration * dt),
            Mathf.MoveToward(velocity.Z, targetVelocity.Z, _acceleration * dt)
        );
    }

    /// <summary>
    /// Check if player has movement input (for flight drain).
    /// </summary>
    public static bool HasMovementInput(PlayerInputState input)
    {
        var inputDir = new Vector2(input.MoveRight, -input.MoveForward);
        return inputDir.LengthSquared() >= 0.001f;
    }
}
