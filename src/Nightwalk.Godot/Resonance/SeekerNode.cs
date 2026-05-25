using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Resonance.Seeker;

namespace Nightwalk.Godot.Resonance;

/// <summary>
/// Godot node representing a Seeker entity.
/// An armillary sphere with rotating rings and a volumetric searchlight.
/// </summary>
public sealed partial class SeekerNode : Node3D
{
    private SeekerConfig? _config;
    private SeekerInstance? _instance;

    // Visual components
    private SeekerRenderer? _renderer;
    private Node3D? _scanPivot; // Pivot for aiming the searchlight
    private SpotLight3D? _scanLight;

    // Animation state
    private float _rotationAngle;
    private float _hoverPhase;
    private float _scanIntensity;

    // Movement tracking
    private Vector3 _velocity;


    /// <summary>
    /// The Seeker instance this node represents.
    /// </summary>
    public SeekerInstance? Instance => _instance;

    /// <summary>
    /// Initializes the Seeker node with config.
    /// </summary>
    public void Initialize(SeekerConfig config)
    {
        _config = config;

        // Create renderer
        _renderer = new SeekerRenderer();
        _renderer.Initialize(config);
        AddChild(_renderer);

        // Create a pivot for aiming the searchlight in any direction
        _scanPivot = new Node3D { Name = "ScanPivot" };
        AddChild(_scanPivot);

        // Create volumetric scan light (no hardcoded rotation - controlled by aim system)
        // Strip alpha from color - lights should be fully opaque
        var lightColor = Color.FromHtml(config.ScanColor);
        lightColor.A = 1f;

        _scanLight = new SpotLight3D
        {
            Name = "ScanLight",
            LightColor = lightColor,
            LightEnergy = 0f,  // Controlled by state
            SpotRange = config.ScanConeRange,
            SpotAngle = config.ScanConeAngle,
            SpotAttenuation = 0.01f,  // Very sharp radial falloff
            SpotAngleAttenuation = 0.05f,  // Crisp cone edge
            ShadowEnabled = true,  // Enable for shadow hiding mechanic
            LightVolumetricFogEnergy = config.LightVolumetricEnergy,
        };
        _scanPivot.AddChild(_scanLight);

        Visible = false;
    }

    /// <summary>
    /// Assigns a Seeker instance to this node.
    /// </summary>
    public void AssignInstance(SeekerInstance instance)
    {
        _instance = instance;
        Visible = true;
        _hoverPhase = 0f;
        _velocity = Vector3.Zero;
        GlobalPosition = new Vector3(instance.X, instance.Y, instance.Z);
    }

    /// <summary>
    /// Returns this node to the pool.
    /// </summary>
    public void ReturnToPool()
    {
        _instance = null;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_instance == null || _config == null) return;

        float dt = (float)delta;

        // Update velocity from instance
        _velocity = new Vector3(_instance.VelocityX, _instance.VelocityY, _instance.VelocityZ);

        // Hover animation (local offset, doesn't accumulate)
        _hoverPhase += dt * _config.HoverSpeed;
        float hoverY = Mathf.Sin(_hoverPhase) * _config.HoverAmplitude;

        // Set position with hover offset
        GlobalPosition = new Vector3(_instance.X, _instance.Y + hoverY, _instance.Z);

        // Rotate rings
        _rotationAngle += dt * _config.RingRotationSpeed;
        _renderer?.SetRotation(_rotationAngle);

        // Update searchlight aim based on instance's aim mode
        UpdateSearchlightAim(dt);

        // Update scan visuals based on state
        UpdateScanVisuals(dt);

        // Perform line-of-sight check (updates instance.HasLineOfSight)
        UpdateLineOfSight();

        // Highlight when detecting player
        if (_instance.IsDetectingPlayer)
        {
            _renderer?.SetAlertColor();
        }
        else
        {
            _renderer?.SetNormalColor();
        }
    }

    private void UpdateSearchlightAim(float delta)
    {
        if (_instance == null || _config == null || _scanPivot == null || _scanLight == null) return;

        if (_instance.AimMode == SeekerAimMode.Off)
        {
            _scanLight.LightEnergy = 0f;
            return;
        }

        Vector3 aimTarget = new(_instance.AimTargetX, _instance.AimTargetY, _instance.AimTargetZ);
        Vector3 toTarget = aimTarget - GlobalPosition;

        if (toTarget.LengthSquared() < 0.01f)
        {
            // Target too close, aim straight down
            toTarget = Vector3.Down;
        }

        float lerpSpeed = _instance.AimMode switch
        {
            SeekerAimMode.SnapToTarget => _config.AimSnapSpeed,
            SeekerAimMode.Hold => 0f,
            SeekerAimMode.Sweep => _config.AimSweepSpeed,
            SeekerAimMode.TrackPlayer => _config.AimTrackSpeed,
            _ => 0f
        };

        if (lerpSpeed > 0f)
        {
            // Calculate target rotation to look at aim target
            Vector3 forward = toTarget.Normalized();

            // Build rotation from forward direction
            // Use Basis.LookingAt which handles looking in any direction including up/down
            Vector3 up = Mathf.Abs(forward.Dot(Vector3.Up)) > 0.99f
                ? Vector3.Forward
                : Vector3.Up;

            var targetBasis = Basis.LookingAt(forward, up);
            var targetTransform = new Transform3D(targetBasis, _scanPivot.GlobalPosition);

            _scanPivot.GlobalTransform = _scanPivot.GlobalTransform.InterpolateWith(
                targetTransform, delta * lerpSpeed);
        }

        // Set light energy based on scan intensity
        _scanLight.LightEnergy = _scanIntensity * _config.LightEnergy;
    }

    private void UpdateScanVisuals(float delta)
    {
        if (_instance == null || _config == null) return;

        // Determine if searchlight should be active based on state
        bool shouldBeActive = _instance.AimMode != SeekerAimMode.Off &&
            (_instance.State == SeekerState.Approaching ||
             _instance.State == SeekerState.Scanning ||
             _instance.State == SeekerState.Hunting);

        float targetIntensity = shouldBeActive ? 1f : 0f;
        _scanIntensity = Mathf.Lerp(_scanIntensity, targetIntensity, delta * _config.ScanIntensityLerp);
    }

    private void UpdateLineOfSight()
    {
        if (_instance == null) return;

        // Get player position from ResonanceModeSystem (we need access to it)
        // For now, we'll use a simple approach: the Core layer passes player position
        // and we do the raycast here

        // This will be called by the SeekerPool with player position
        // For now, default to true (LOS exists)
        // The actual raycast is done in CheckLineOfSight()
    }

    /// <summary>
    /// Checks line of sight to the given player position.
    /// Updates instance.HasLineOfSight.
    /// </summary>
    public void CheckLineOfSight(Vector3 playerPos)
    {
        if (_instance == null) return;

        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
        {
            _instance.HasLineOfSight = true;
            return;
        }

        // Raycast toward player but stop slightly before to avoid hitting player's own collider
        Vector3 toPlayer = playerPos - GlobalPosition;
        float distance = toPlayer.Length();
        if (distance < 1f)
        {
            _instance.HasLineOfSight = true;
            return;
        }

        // Stop 1 meter before player position
        Vector3 targetPos = GlobalPosition + toPlayer.Normalized() * (distance - 1f);

        var query = PhysicsRayQueryParameters3D.Create(GlobalPosition, targetPos);
        query.CollisionMask = uint.MaxValue; // Check all layers
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;

        var result = space.IntersectRay(query);

        // No obstacles = has line of sight
        _instance.HasLineOfSight = result.Count == 0;
    }

    /// <summary>
    /// Updates collision avoidance steering vector in the instance.
    /// </summary>
    public void UpdateCollisionAvoidance()
    {
        if (_instance == null || _config == null) return;

        if (_velocity.LengthSquared() < 0.01f)
        {
            _instance.AvoidanceX = 0f;
            _instance.AvoidanceY = 0f;
            _instance.AvoidanceZ = 0f;
            return;
        }

        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
        {
            _instance.AvoidanceX = 0f;
            _instance.AvoidanceY = 0f;
            _instance.AvoidanceZ = 0f;
            return;
        }

        Vector3 moveDir = _velocity.Normalized();
        Vector3 rayEnd = GlobalPosition + moveDir * _config.AvoidanceRayLength;

        var query = PhysicsRayQueryParameters3D.Create(GlobalPosition, rayEnd);
        query.CollisionMask = uint.MaxValue;
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;

        var result = space.IntersectRay(query);
        if (result.Count > 0)
        {
            Vector3 normal = (Vector3)result["normal"];
            Vector3 avoidance = normal * _config.AvoidanceStrength;
            _instance.AvoidanceX = avoidance.X;
            _instance.AvoidanceY = avoidance.Y;
            _instance.AvoidanceZ = avoidance.Z;
        }
        else
        {
            _instance.AvoidanceX = 0f;
            _instance.AvoidanceY = 0f;
            _instance.AvoidanceZ = 0f;
        }
    }
}
