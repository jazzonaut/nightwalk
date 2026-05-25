using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Godot.Util;
using GameDataService = Nightwalk.Core.Config.GameDataService;

namespace Nightwalk.Godot.Ziplines;

/// <summary>
/// Handles zipline riding mechanics: attachment, movement along cable, and detachment.
/// </summary>
public sealed class ZiplineRider
{
    private readonly ZiplineRegistry _registry;
    private readonly GameDataService _data;

    private ZiplineConfig Config => _data.Tools.Zipline;

    // Current riding state
    private ZiplineCable? _currentCable;
    private float _cableT;
    private float _cableVelocity;

    private SmoothTransition _transition;

    // Cooldown to prevent instant re-attachment to the same cable after detaching
    private ZiplineCable? _cooldownCable;
    private float _cooldownTimer;

    /// <summary>
    /// Whether the player is currently riding a zipline.
    /// </summary>
    public bool IsRiding => _currentCable != null && _registry.IsRegistered(_currentCable);

    /// <summary>
    /// Current position on the cable (for external queries).
    /// </summary>
    public Vector3 CurrentPosition => _currentCable?.GetPositionAtT(_cableT) ?? Vector3.Zero;

    public ZiplineRider(ZiplineRegistry registry, GameDataService data)
    {
        _registry = registry;
        _data = data;
    }

    /// <summary>
    /// Checks for nearby cables and attaches if within range.
    /// Should be called each frame when not riding.
    /// </summary>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="startPosition">Player body position for transition blending.</param>
    /// <param name="playerForward">Player's facing direction (horizontal).</param>
    /// <param name="dt">Delta time for cooldown.</param>
    /// <returns>True if attached to a cable.</returns>
    public bool TryAttach(Vector3 playerPosition, Vector3 startPosition, Vector3 playerForward, float dt)
    {
        if (IsRiding) return false;

        // Update cooldown timer; also clear if cable was deleted
        if (_cooldownCable != null)
        {
            if (!_registry.IsRegistered(_cooldownCable))
            {
                _cooldownCable = null;
                _cooldownTimer = 0f;
            }
            else if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= dt;
                if (_cooldownTimer <= 0f)
                {
                    _cooldownCable = null;
                }
            }
        }

        var nearest = _registry.FindNearestCable(playerPosition, Config.AttachmentDistance);
        if (nearest == null) return false;

        var (cable, t, _) = nearest.Value;

        // Skip if this specific cable is on cooldown
        if (cable == _cooldownCable && _cooldownTimer > 0f)
            return false;

        AttachToCable(cable, t, startPosition);
        return true;
    }

    /// <summary>
    /// Attaches to a specific cable at the given t position.
    /// </summary>
    private void AttachToCable(ZiplineCable cable, float t, Vector3 startPosition)
    {
        _currentCable = cable;
        _cableT = t;
        _cableVelocity = 0f;
        _cooldownCable = null;
        _cooldownTimer = 0f;
        BeginTransition(startPosition, cable, t);
    }

    private void BeginTransition(Vector3 startPosition, ZiplineCable cable, float t)
    {
        _transition.Begin(startPosition, cable.GetPositionAtT(t), Config.AttachTransitionDuration);
    }

    /// <summary>
    /// Attempts to transfer directly to a specific cable (for jump-to-highlighted).
    /// </summary>
    /// <param name="targetCable">The cable to transfer to.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="startPosition">Player body position for transition blending.</param>
    /// <returns>True if transfer succeeded, false if too far.</returns>
    public bool TryTransferTo(ZiplineCable targetCable, Vector3 playerPosition, Vector3 startPosition)
    {
        if (targetCable == null || targetCable == _currentCable || !_registry.IsRegistered(targetCable))
            return false;

        var (t, distance) = targetCable.FindClosestT(playerPosition);
        if (distance > Config.TransferDistance)
            return false;

        // Detach from current without jump force
        _currentCable = null;
        _cableVelocity = 0f;

        // Attach to new cable (no cooldown for intentional transfers)
        AttachToCable(targetCable, t, startPosition);
        return true;
    }

    /// <summary>
    /// Detaches from the current cable.
    /// </summary>
    /// <param name="applyJump">Whether to apply jump force on detach.</param>
    /// <returns>Velocity to apply to player after detaching.</returns>
    public Vector3 Detach(bool applyJump)
    {
        if (!IsRiding) return Vector3.Zero;

        // Calculate exit velocity
        var tangent = _currentCable!.GetTangentAtT(_cableT);
        var exitVelocity = tangent * _cableVelocity;

        if (applyJump)
        {
            exitVelocity.Y += Config.DetachJumpForce;
        }

        // Set cooldown for this specific cable only when jumping off intentionally
        // (prevents accidental re-grab; no cooldown needed for resets or cable deletion)
        if (applyJump)
        {
            _cooldownCable = _currentCable;
            _cooldownTimer = Config.DetachCooldown;
        }

        _currentCable = null;
        _cableT = 0f;
        _cableVelocity = 0f;
        _transition.Cancel();

        return exitVelocity;
    }

    /// <summary>
    /// Processes zipline movement for one physics frame.
    /// </summary>
    /// <param name="dt">Delta time.</param>
    /// <param name="moveInput">Forward/backward input (-1 to 1, from W/S keys).</param>
    /// <param name="playerForward">Player's current facing direction.</param>
    /// <param name="currentPosition">Current player position (returned if cable removed mid-ride).</param>
    /// <returns>New world position for the player.</returns>
    public Vector3 ProcessMovement(float dt, float moveInput, bool runHeld, Vector3 playerForward, Vector3 currentPosition)
    {
        // Check if cable was removed while riding
        if (_currentCable != null && !_registry.IsRegistered(_currentCable))
        {
            Detach(applyJump: false);
            return currentPosition; // Stay where we are instead of teleporting to origin
        }

        if (!IsRiding) return currentPosition;

        if (_transition.TryStepPosition(dt, out var position))
            return position;

        var cable = _currentCable!;

        // Get cable tangent at current position
        var tangent = cable.GetTangentAtT(_cableT);

        // Calculate slope angle (negative Y = going down)
        // slopeAngle > 0 means cable goes DOWN in the +tangent direction
        float slopeAngle = Mathf.Asin(Mathf.Clamp(-tangent.Y, -1f, 1f));

        // Determine which direction the player wants to move
        var horizontalTangent = new Vector3(tangent.X, 0, tangent.Z).Normalized();
        var horizontalForward = new Vector3(playerForward.X, 0, playerForward.Z).Normalized();
        int facingDirection = horizontalTangent.Dot(horizontalForward) >= 0 ? 1 : -1;

        int inputDirection = 0;
        if (Mathf.Abs(moveInput) > 0.1f)
            inputDirection = moveInput > 0 ? facingDirection : -facingDirection;

        float cableLength = cable.Length;
        float holdThresholdT = 0f;
        // Convert the absolute hold distance (meters) into a normalized t-range on the cable.
        if (cableLength > 0.01f)
        {
            holdThresholdT = Mathf.Clamp(Config.AnchorHoldDistance / cableLength, 0f, 0.49f);
        }
        float holdOffsetT = 0f;
        if (cableLength > 0.01f)
        {
            holdOffsetT = Mathf.Clamp(Config.AnchorHoldOffset / cableLength, 0f, holdThresholdT);
        }
        bool nearStart = _cableT <= holdThresholdT;
        bool nearEnd = _cableT >= 1f - holdThresholdT;
        // Hold still at anchors unless the player inputs movement away from the anchor.
        if (nearStart && inputDirection <= 0 || nearEnd && inputDirection >= 0)
        {
            _cableVelocity = 0f;
            _cableT = nearStart ? holdOffsetT : 1f - holdOffsetT;
            var holdPosition = cable.GetPositionAtT(_cableT);
            if (!_transition.IsActive && currentPosition.DistanceTo(holdPosition) > 0.01f)
            {
                _transition.Begin(currentPosition, holdPosition, Config.AttachTransitionDuration);
            }
            if (_transition.TryStepPosition(dt, out var holdStepPosition))
                return holdStepPosition;
            return holdPosition;
        }

        // Calculate gravity effect (positive = accelerates in +tangent direction)
        float gravityAccel = Config.GravitySlideAcceleration * Mathf.Sin(slopeAngle);

        var speedMultiplier = runHeld ? Config.RunSpeedMultiplier : 1f;
        var maxSlideSpeed = Config.MaxSlideSpeed * speedMultiplier;
        var climbSpeed = Config.ClimbSpeed * speedMultiplier;

        // Process input
        if (Mathf.Abs(moveInput) > 0.1f)
        {
            // Player is providing input
            // Check if climbing against gravity
            bool isClimbing = inputDirection > 0 && slopeAngle > 0.1f ||
                              inputDirection < 0 && slopeAngle < -0.1f;

            if (isClimbing)
            {
                // Climbing: speed inversely proportional to slope
                float slopeRatio = Mathf.Abs(slopeAngle) / (Mathf.Pi / 2f);
                float climbEfficiency = Mathf.Max(Config.MinClimbEfficiency, 1f - slopeRatio);
                float targetSpeed = climbSpeed * climbEfficiency * inputDirection;

                _cableVelocity = Mathf.MoveToward(_cableVelocity, targetSpeed, Config.ClimbAcceleration * dt);
            }
            else
            {
                // Moving with gravity or on flat cable
                float targetSpeed = climbSpeed * inputDirection;

                // Add gravity assist when going downhill
                if (inputDirection > 0 && slopeAngle > 0 || inputDirection < 0 && slopeAngle < 0)
                {
                    targetSpeed = Mathf.Sign(targetSpeed) * maxSlideSpeed;
                }

                _cableVelocity = Mathf.MoveToward(_cableVelocity, targetSpeed, Config.ClimbAcceleration * dt);
            }
        }
        else
        {
            // No input - apply gravity sliding
            _cableVelocity += gravityAccel * dt;
        }

        // Clamp to max slide speed
        _cableVelocity = Mathf.Clamp(_cableVelocity, -maxSlideSpeed, maxSlideSpeed);

        // Apply friction/drag when not accelerating
        if (Mathf.Abs(moveInput) < 0.1f && Mathf.Abs(gravityAccel) < 0.5f)
        {
            _cableVelocity *= 0.98f; // Light friction on flat sections
        }

        // Convert velocity to t-delta
        if (cableLength > 0.1f)
        {
            float tDelta = _cableVelocity * dt / cableLength;
            _cableT += tDelta;
        }

        // Clamp to cable ends and stop
        if (_cableT <= 0f)
        {
            _cableT = 0f;
            _cableVelocity = Mathf.Max(0f, _cableVelocity); // Can only move away from start
        }
        else if (_cableT >= 1f)
        {
            _cableT = 1f;
            _cableVelocity = Mathf.Min(0f, _cableVelocity); // Can only move away from end
        }

        return cable.GetPositionAtT(_cableT);
    }
}
