using System;
using System.Collections.Generic;
using System.Linq;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.Resonance.Events;

namespace Nightwalk.Core.Resonance.Seeker;

/// <summary>
/// Manages all Seeker instances - spawning, behavior, and pooling.
/// </summary>
public sealed class SeekerController
{
    private readonly IEventBus _eventBus;
    private readonly ResonanceConfig _data;
    private readonly SeekerConfig _config;
    private readonly SeekerMovement _movement;
    private readonly Random _random = new();

    private readonly List<SeekerInstance> _activeSeekers = new();
    private readonly List<NoisePing> _activePings = new();
    private readonly Dictionary<int, NoisePing> _pingLookup = new();

    private float _totalTime;
    private float _currentAlertLevel;

    // Cached Warden position for spawning
    private float _wardenX, _wardenY, _wardenZ;
    private bool _wardenPositionSet;

    // Detection ping throttling
    private float _lastDetectionPingTime;

    // Count of seekers currently detecting the player
    private int _detectingCount;

    public int MaxSeekers { get; private set; }
    public int ActiveCount => _activeSeekers.Count;
    public IReadOnlyList<SeekerInstance> ActiveSeekers => _activeSeekers;

    public event Action<SeekerInstance>? OnSeekerSpawned;
    public event Action<SeekerInstance>? OnSeekerDespawned;
    public event Action<SeekerInstance>? OnPlayerDetected;
    public event Action? OnPlayerLost;

    public SeekerController(IEventBus eventBus, ResonanceConfig data)
    {
        _eventBus = eventBus;
        _data = data;
        _config = data.Seeker;
        _movement = new SeekerMovement(_config);
        MaxSeekers = _config.MaxCountLowAlert;

        _eventBus.Subscribe<NoisePingEvent>(OnNoisePing);
        _eventBus.Subscribe<DecoyPingEvent>(OnDecoyPing);
    }

    /// <summary>
    /// Updates the alert tier and adjusts max seeker count accordingly.
    /// </summary>
    public void UpdateAlertTier(float alertLevel)
    {
        _currentAlertLevel = alertLevel;

        if (alertLevel >= _config.AlertTierHigh)
            MaxSeekers = _config.MaxCountHighAlert;
        else if (alertLevel >= _config.AlertTierMedium)
            MaxSeekers = _config.MaxCountMedAlert;
        else
            MaxSeekers = _config.MaxCountLowAlert;
    }

    private float GetTravelSpeed()
    {
        return MathF.Max(
            _config.TravelSpeedLow,
            _config.TravelSpeedLow + _currentAlertLevel * (_config.TravelSpeedHigh - _config.TravelSpeedLow)
        );
    }

    /// <summary>
    /// Adds a noise ping and optionally spawns a seeker if conditions allow.
    /// </summary>
    public NoisePing AddPing(float x, float y, float z, bool isDecoy = false)
    {
        var type = isDecoy ? PingType.Decoy : PingType.Noise;
        var priority = isDecoy ? _data.Counterplay.DecoyPriority : _config.PingNoisePriority;
        var ping = NoisePing.Create(x, y, z, _totalTime, type, priority);

        _activePings.Add(ping);
        _pingLookup[ping.Id] = ping;

        if (!isDecoy && _random.NextDouble() > _config.SpawnChance)
        {
            return ping;
        }

        if (_activeSeekers.Count < MaxSeekers && _wardenPositionSet)
        {
            SpawnSeekerAtWarden();
        }

        return ping;
    }

    /// <summary>
    /// Emits a high-priority detection ping at the player's location.
    /// Other Seekers will converge on this position.
    /// </summary>
    public void EmitDetectionPing(float playerX, float playerY, float playerZ)
    {
        if (_totalTime - _lastDetectionPingTime < _config.DetectionPingCooldown)
        {
            return;
        }

        _lastDetectionPingTime = _totalTime;

        var ping = NoisePing.Create(playerX, playerY, playerZ, _totalTime, PingType.Detection, _config.PingDetectionPriority);
        _activePings.Add(ping);
        _pingLookup[ping.Id] = ping;

        Log.Debug(LogCategory.Sim, "Detection ping emitted at player location");
    }

    private void SpawnSeekerAtWarden()
    {
        float offsetRange = _config.SpawnOffsetRange;
        float offsetX = (float)(_random.NextDouble() - 0.5) * offsetRange * 2f;
        float offsetZ = (float)(_random.NextDouble() - 0.5) * offsetRange * 2f;
        float offsetY = (float)(_random.NextDouble() - 0.5) * offsetRange;

        var seeker = new SeekerInstance
        {
            Id = Guid.NewGuid(),
            State = SeekerState.Dispatching,
            X = _wardenX + offsetX,
            Y = _wardenY + offsetY,
            Z = _wardenZ + offsetZ,
            SpawnTime = _totalTime,
            NoiseOffset = _movement.GenerateNoiseOffset(),
            AimMode = SeekerAimMode.Off,
            CurrentPingId = -1
        };

        _activeSeekers.Add(seeker);
        OnSeekerSpawned?.Invoke(seeker);

        Log.Debug(LogCategory.Sim, $"Seeker spawned near Warden: {seeker.Id}");
    }

    /// <summary>
    /// Main update loop. Updates all seekers and handles detection events.
    /// </summary>
    public void Update(float delta, float playerX, float playerY, float playerZ,
                       float wardenX, float wardenY, float wardenZ, bool wardenPresent)
    {
        _totalTime += delta;

        _wardenX = wardenX;
        _wardenY = wardenY;
        _wardenZ = wardenZ;

        // Only consider Warden position valid when Warden has actually spawned
        if (wardenPresent)
        {
            _wardenPositionSet = true;
        }

        CleanupPings();

        for (int i = _activeSeekers.Count - 1; i >= 0; i--)
        {
            var seeker = _activeSeekers[i];

            // 1. Update state and movement (this sets aim targets)
            UpdateSeeker(seeker, delta, playerX, playerY, playerZ, wardenX, wardenY, wardenZ);

            // 2. Now check detection with the current aim target
            UpdateDetection(seeker, playerX, playerY, playerZ);

            // 3. Handle detection state transitions (grace period, hunt mode)
            UpdateDetectionState(seeker, delta, playerX, playerY, playerZ);

            // 4. Fire detection events - OnPlayerDetected fires once when first seeker detects
            if (seeker.IsDetectingPlayer && !seeker.WasDetectingPlayer)
            {
                // This seeker just started detecting
                _detectingCount++;

                // Only fire OnPlayerDetected when first seeker detects (count goes 0 -> 1)
                if (_detectingCount == 1)
                {
                    OnPlayerDetected?.Invoke(seeker);
                }

                // Emit detection ping so other seekers converge
                EmitDetectionPing(playerX, playerY, playerZ);
            }
            else if (!seeker.IsDetectingPlayer && seeker.WasDetectingPlayer)
            {
                // This seeker just stopped detecting
                _detectingCount--;
                // Only fire OnPlayerLost when ALL seekers have lost the player
                if (_detectingCount <= 0)
                {
                    _detectingCount = 0;
                    OnPlayerLost?.Invoke();
                }
            }
            // Note: Detection pings are only emitted on state change, not every frame.
            // The EmitDetectionPing method has its own cooldown, but calling it every
            // frame is wasteful. If sustained detection should emit periodic pings,
            // that should be handled separately with explicit timing.

            // Update previous detection state for next frame
            seeker.WasDetectingPlayer = seeker.IsDetectingPlayer;

            // Remove despawned seekers
            if (seeker.State == SeekerState.Pooled)
            {
                // Decrement counter if this seeker was detecting when pooled
                if (seeker.WasDetectingPlayer || seeker.IsDetectingPlayer)
                {
                    _detectingCount--;
                    // Only fire OnPlayerLost when ALL seekers have lost the player
                    if (_detectingCount <= 0)
                    {
                        _detectingCount = 0;
                        OnPlayerLost?.Invoke();
                    }
                }
                // Reset detection state to prevent stale data if instance is reused
                seeker.IsDetectingPlayer = false;
                seeker.WasDetectingPlayer = false;
                _activeSeekers.RemoveAt(i);
                OnSeekerDespawned?.Invoke(seeker);
            }
        }
    }

    /// <summary>
    /// Checks if player is in this seeker's detection cone with line of sight.
    /// Sets IsDetectingPlayer based on current aim target.
    /// </summary>
    private void UpdateDetection(SeekerInstance seeker, float playerX, float playerY, float playerZ)
    {
        // Only check detection during scanning or hunting states
        if (seeker.State != SeekerState.Scanning && seeker.State != SeekerState.Hunting)
        {
            seeker.IsDetectingPlayer = false;
            return;
        }

        bool inCone = IsPlayerInCone(seeker, playerX, playerY, playerZ);
        bool hasLos = seeker.HasLineOfSight;

        seeker.IsDetectingPlayer = inCone && hasLos;
    }

    /// <summary>
    /// Handles grace period and transitions to/from hunt mode.
    /// </summary>
    private void UpdateDetectionState(SeekerInstance seeker, float delta, float playerX, float playerY, float playerZ)
    {
        if (seeker.State != SeekerState.Scanning && seeker.State != SeekerState.Hunting)
        {
            // Reset detection tracking when not in a detection-capable state
            seeker.DetectionLostTime = 0f;
            return;
        }

        if (seeker.IsDetectingPlayer)
        {
            HandleDetecting(seeker, playerX, playerY, playerZ);
        }
        else if (seeker.State == SeekerState.Scanning)
        {
            HandleLostDuringScan(seeker, playerX, playerY, playerZ);
        }
        else if (seeker.State == SeekerState.Hunting)
        {
            HandleHuntTimeout(seeker);
        }
    }

    private void CleanupPings()
    {
        for (int i = _activePings.Count - 1; i >= 0; i--)
        {
            var ping = _activePings[i];
            float age = _totalTime - ping.CreationTime;

            // Detection pings expire faster - player likely moved from that location
            float maxAge = ping.Type == PingType.Detection ? _config.PingDetectionExpiry : _config.PingNoiseExpiry;

            if (age > maxAge || ping.Investigated)
            {
                _pingLookup.Remove(ping.Id);
                _activePings.RemoveAt(i);
                ping.Return();
            }
        }
    }

    private void UpdateSeeker(SeekerInstance seeker, float delta,
                              float playerX, float playerY, float playerZ,
                              float wardenX, float wardenY, float wardenZ)
    {
        switch (seeker.State)
        {
            case SeekerState.Dispatching:
                UpdateDispatching(seeker, wardenX, wardenY, wardenZ);
                break;

            case SeekerState.Traveling:
                UpdateTraveling(seeker, delta);
                break;

            case SeekerState.Approaching:
                UpdateApproaching(seeker, delta);
                break;

            case SeekerState.Scanning:
                UpdateScanning(seeker, delta, playerX, playerY, playerZ);
                break;

            case SeekerState.Hunting:
                UpdateHunting(seeker, delta, playerX, playerY, playerZ);
                break;

            case SeekerState.Returning:
                UpdateReturning(seeker, delta, wardenX, wardenY, wardenZ);
                break;

            case SeekerState.Recalled:
                UpdateRecalled(seeker, delta, wardenX, wardenY, wardenZ);
                break;
        }
    }

    private void UpdateDispatching(SeekerInstance seeker, float wardenX, float wardenY, float wardenZ)
    {
        seeker.AimMode = SeekerAimMode.Off;

        if (_totalTime - seeker.SpawnTime > _config.DispatchDelay)
        {
            AssignBestPing(seeker);

            if (seeker.CurrentPingId < 0)
            {
                // No ping available, patrol near Warden at hover altitude
                float angle = (float)_random.NextDouble() * MathF.PI * 2f;
                float radiusRange = _config.IdlePatrolMaxRadius - _config.IdlePatrolMinRadius;
                float dist = _config.IdlePatrolMinRadius + (float)_random.NextDouble() * radiusRange;
                seeker.TargetX = wardenX + MathF.Cos(angle) * dist;
                seeker.TargetY = wardenY + _config.HoverHeight;
                seeker.TargetZ = wardenZ + MathF.Sin(angle) * dist;
            }

            seeker.State = SeekerState.Traveling;
        }
    }

    private void UpdateTraveling(SeekerInstance seeker, float delta)
    {
        seeker.AimMode = SeekerAimMode.Off;

        _movement.UpdateMovement(
            seeker,
            seeker.TargetX, seeker.TargetY, seeker.TargetZ,
            GetTravelSpeed(), delta, _totalTime);

        CheckForBetterPing(seeker);

        float distToTarget = Distance(seeker.X, seeker.Y, seeker.Z,
                                      seeker.TargetX, seeker.TargetY, seeker.TargetZ);

        if (distToTarget < _config.ApproachRadius)
        {
            seeker.State = SeekerState.Approaching;
            seeker.AimMode = SeekerAimMode.SnapToTarget;
            seeker.AimTargetX = seeker.TargetX;
            seeker.AimTargetY = seeker.TargetY - _config.HoverHeight;
            seeker.AimTargetZ = seeker.TargetZ;
        }
    }

    private void UpdateApproaching(SeekerInstance seeker, float delta)
    {
        seeker.AimMode = SeekerAimMode.SnapToTarget;
        seeker.AimTargetX = seeker.TargetX;
        seeker.AimTargetY = seeker.TargetY - _config.HoverHeight;
        seeker.AimTargetZ = seeker.TargetZ;

        _movement.UpdateMovement(
            seeker,
            seeker.TargetX, seeker.TargetY, seeker.TargetZ,
            _config.ApproachSpeed, delta, _totalTime, wanderFade: 0.3f);

        float distToTarget = Distance(seeker.X, seeker.Y, seeker.Z,
                                      seeker.TargetX, seeker.TargetY, seeker.TargetZ);

        if (distToTarget < _config.ArrivalDistance)
        {
            EnterScanningAtTarget(seeker);
        }
    }

    private void UpdateScanning(SeekerInstance seeker, float delta, float playerX, float playerY, float playerZ)
    {
        bool shouldTrackPlayer = seeker.HasDetectedDuringScan ||
                                 seeker.IsDetectingPlayer ||
                                 (seeker.DetectionLostTime > 0f &&
                                  _totalTime - seeker.DetectionLostTime < _config.DetectionGracePeriod);

        float patrolRadius = _config.PatrolRadius;
        if (shouldTrackPlayer)
        {
            // Keep orbit tight enough to stay within the 3D range check.
            float verticalDelta = MathF.Abs(seeker.Y - playerY);
            float range = _config.ScanConeRange;
            float maxHorizontal = MathF.Sqrt(MathF.Max(0f, range * range - verticalDelta * verticalDelta));
            float reducedRadius = MathF.Max(0f, maxHorizontal - 1f);
            patrolRadius = MathF.Min(patrolRadius, reducedRadius);
        }

        UpdateOrbitMovement(seeker, delta, patrolRadius);
        UpdateScanAimTarget(seeker, shouldTrackPlayer, playerX, playerY, playerZ, patrolRadius);

        if (HandleScanRedirect(seeker, shouldTrackPlayer))
        {
            return;
        }

        HandleScanTimeout(seeker, shouldTrackPlayer);
    }

    private void UpdateHunting(SeekerInstance seeker, float delta, float playerX, float playerY, float playerZ)
    {
        float elapsed = _totalTime - seeker.HuntStartTime;

        // Spiral search pattern from last known position
        float spiralAngle = elapsed * _config.HuntSpiralAngularSpeed;
        float spiralRadius = elapsed * _config.HuntSpiralExpansion;
        float searchX = seeker.LastKnownPlayerX + MathF.Cos(spiralAngle) * spiralRadius;
        float searchZ = seeker.LastKnownPlayerZ + MathF.Sin(spiralAngle) * spiralRadius;

        _movement.UpdateMovement(
            seeker,
            searchX, seeker.LastKnownPlayerY + _config.HoverHeight, searchZ,
            _config.HuntSpeed, delta, _totalTime, wanderFade: 0.5f);

        // Aim at last known position (will be updated by detection check if player found)
        seeker.AimMode = SeekerAimMode.TrackPlayer;
        seeker.AimTargetX = seeker.LastKnownPlayerX;
        seeker.AimTargetY = seeker.LastKnownPlayerY;
        seeker.AimTargetZ = seeker.LastKnownPlayerZ;
    }

    /// <summary>
    /// Advances scan orbit motion and applies movement toward the orbit target.
    /// </summary>
    private void UpdateOrbitMovement(SeekerInstance seeker, float delta, float patrolRadius)
    {
        // Advance orbit angle (continuous orbiting regardless of detection)
        seeker.OrbitAngle += delta * _config.OrbitSpeed;

        // Calculate orbit position
        float orbitX = seeker.PatrolCenterX + MathF.Cos(seeker.OrbitAngle) * patrolRadius;
        float orbitZ = seeker.PatrolCenterZ + MathF.Sin(seeker.OrbitAngle) * patrolRadius;

        // Gentle altitude oscillation around patrol altitude
        float altitudeOscillation = MathF.Sin(seeker.OrbitAngle * 0.5f) * _config.AltitudeOscillation;
        float orbitY = seeker.PatrolAltitude + altitudeOscillation;

        // Move toward orbit position
        _movement.UpdateMovement(
            seeker,
            orbitX, orbitY, orbitZ,
            _config.ApproachSpeed * _config.ScanSpeedMultiplier, delta, _totalTime, wanderFade: 0.2f);
    }

    /// <summary>
    /// Updates scan aim target based on whether the seeker should track the player or sweep.
    /// </summary>
    private void UpdateScanAimTarget(
        SeekerInstance seeker,
        bool shouldTrackPlayer,
        float playerX,
        float playerY,
        float playerZ,
        float patrolRadius)
    {
        // Set aim target based on detection state.
        // Note: IsDetectingPlayer is set AFTER this method returns, so we use the previous frame's value.
        if (shouldTrackPlayer)
        {
            // Track player position
            seeker.AimMode = SeekerAimMode.TrackPlayer;
            seeker.AimTargetX = playerX;
            seeker.AimTargetY = playerY;
            seeker.AimTargetZ = playerZ;
        }
        else
        {
            // Sweep mode - sweep back and forth across the patrol center
            seeker.AimMode = SeekerAimMode.Sweep;

            // Fast sweep pattern that passes through center frequently
            float sweepTime = _totalTime * _config.SweepFrequency;
            float sweepRadius = patrolRadius * _config.SweepRadiusMultiplier;

            // Sweep in a figure-8 / lissajous pattern across the center
            float sweepX = MathF.Sin(sweepTime) * sweepRadius;
            float sweepZ = MathF.Sin(sweepTime * 1.3f) * sweepRadius;

            // Vertical sweep to catch players at different heights
            float sweepY = MathF.Sin(sweepTime * 0.7f) * _config.SweepVerticalAmplitude;

            float groundLevel = seeker.PatrolAltitude - _config.HoverHeight;
            seeker.AimTargetX = seeker.PatrolCenterX + sweepX;
            seeker.AimTargetY = groundLevel + sweepY;
            seeker.AimTargetZ = seeker.PatrolCenterZ + sweepZ;
        }
    }

    /// <summary>
    /// Checks for redirect opportunities during scan; returns true if scanning should exit early.
    /// </summary>
    private bool HandleScanRedirect(SeekerInstance seeker, bool shouldTrackPlayer)
    {
        // Check for better ping (but not while actively tracking player)
        // Also require minimum scan time before allowing redirects - give the seeker
        // a chance to actually sweep the area before chasing other pings
        float scanElapsed = _totalTime - seeker.ScanStartTime;

        if (!shouldTrackPlayer && scanElapsed > _config.MinScanBeforeRedirect)
        {
            CheckForBetterPing(seeker);

            // If redirected to a new ping, exit early
            if (seeker.State != SeekerState.Scanning)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Handles scan timeout transitions when not tracking the player.
    /// </summary>
    private void HandleScanTimeout(SeekerInstance seeker, bool shouldTrackPlayer)
    {
        // Scan timeout (only when not tracking player and haven't detected them this scan)
        // If we detected the player, let UpdateDetectionState handle the hunt transition instead
        if (!shouldTrackPlayer && !seeker.HasDetectedDuringScan && _totalTime - seeker.ScanStartTime > _config.ScanDuration)
        {
            MarkPingInvestigated(seeker.CurrentPingId);
            seeker.CurrentPingId = -1;

            AssignBestPing(seeker);

            if (seeker.CurrentPingId >= 0)
            {
                EnterTraveling(seeker);
            }
            else
            {
                EnterReturning(seeker);
            }
        }
    }

    private void UpdateReturning(SeekerInstance seeker, float delta, float wardenX, float wardenY, float wardenZ)
    {
        seeker.AimMode = SeekerAimMode.Off;

        // Check for new pings while returning
        AssignBestPing(seeker);

        if (seeker.CurrentPingId >= 0)
        {
            EnterTraveling(seeker);
            return;
        }

        _movement.UpdateMovement(
            seeker,
            wardenX, wardenY, wardenZ,
            _config.ReturnSpeed, delta, _totalTime);

        float distToWarden = Distance(seeker.X, seeker.Y, seeker.Z,
                                      wardenX, wardenY, wardenZ);
        if (distToWarden < _config.ReturnPoolDistance)
        {
            seeker.State = SeekerState.Pooled;
        }
    }

    private void UpdateRecalled(SeekerInstance seeker, float delta, float wardenX, float wardenY, float wardenZ)
    {
        seeker.AimMode = SeekerAimMode.Off;

        // Initialize recall start time if not set (safety for direct state assignment)
        if (seeker.RecallStartTime <= 0f)
        {
            seeker.RecallStartTime = _totalTime;
        }

        ReleasePingAssignment(seeker);

        _movement.UpdateMovement(
            seeker,
            wardenX, wardenY, wardenZ,
            _config.ReturnSpeed * 2f, delta, _totalTime, wanderFade: 0f);

        float recallDist = Distance(seeker.X, seeker.Y, seeker.Z, wardenX, wardenY, wardenZ);
        float recallElapsed = _totalTime - seeker.RecallStartTime;

        // Pool seeker if it reaches the warden or times out
        if (recallDist < _config.RecallPoolDistance || recallElapsed > _config.RecallTimeout)
        {
            seeker.State = SeekerState.Pooled;
        }
    }

    /// <summary>
    /// Checks if player is within the seeker's scan cone.
    /// Uses current aim target as cone direction.
    /// </summary>
    private bool IsPlayerInCone(SeekerInstance seeker, float playerX, float playerY, float playerZ)
    {
        float dx = playerX - seeker.X;
        float dy = playerY - seeker.Y;
        float dz = playerZ - seeker.Z;
        float distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        if (distance > _config.ScanConeRange) return false;
        if (distance < 0.1f) return true;

        // Get aim direction
        float aimDx = seeker.AimTargetX - seeker.X;
        float aimDy = seeker.AimTargetY - seeker.Y;
        float aimDz = seeker.AimTargetZ - seeker.Z;
        float aimDist = MathF.Sqrt(aimDx * aimDx + aimDy * aimDy + aimDz * aimDz);

        if (aimDist < 0.1f)
        {
            // Fallback to straight down
            aimDx = 0;
            aimDy = -1;
            aimDz = 0;
        }
        else
        {
            aimDx /= aimDist;
            aimDy /= aimDist;
            aimDz /= aimDist;
        }

        // Direction to player
        float pdx = dx / distance;
        float pdy = dy / distance;
        float pdz = dz / distance;

        // Angle between aim direction and player direction
        float dot = aimDx * pdx + aimDy * pdy + aimDz * pdz;
        float angleFromAim = MathF.Acos(MathF.Min(1f, MathF.Max(-1f, dot))) * 180f / MathF.PI;

        return angleFromAim <= _config.ScanConeAngle;
    }

    #region Ping Management

    private void AssignBestPing(SeekerInstance seeker)
    {
        var (ping, _) = FindBestPing(seeker, excludeCurrentPing: false);
        if (ping != null)
        {
            AssignPingToSeeker(seeker, ping);
        }
        else
        {
            ReleasePingAssignment(seeker);
        }
    }

    /// <summary>
    /// Enters scan state centered on the current target.
    /// </summary>
    private void EnterScanningAtTarget(SeekerInstance seeker)
    {
        seeker.State = SeekerState.Scanning;
        seeker.ScanStartTime = _totalTime;
        seeker.PatrolCenterX = seeker.TargetX;
        seeker.PatrolCenterZ = seeker.TargetZ;
        seeker.PatrolAltitude = seeker.Y;
        seeker.OrbitAngle = 0f;
        seeker.HasDetectedDuringScan = false; // Reset detection tracking for new scan
    }

    /// <summary>
    /// Enters scan state centered on the player's position.
    /// </summary>
    private void EnterScanningAtPlayer(SeekerInstance seeker, float playerX, float playerY, float playerZ)
    {
        seeker.State = SeekerState.Scanning;
        seeker.ScanStartTime = _totalTime;
        seeker.PatrolCenterX = playerX;
        seeker.PatrolCenterZ = playerZ;
        seeker.PatrolAltitude = playerY + _config.HoverHeight;
        seeker.OrbitAngle = 0f;
        seeker.HasDetectedDuringScan = true; // Already detecting when entering scan
    }

    /// <summary>
    /// Enters hunt state and captures the last known player position.
    /// </summary>
    private void EnterHunting(SeekerInstance seeker, float playerX, float playerY, float playerZ)
    {
        seeker.State = SeekerState.Hunting;
        seeker.HuntStartTime = _totalTime;
        seeker.LastKnownPlayerX = playerX;
        seeker.LastKnownPlayerY = playerY;
        seeker.LastKnownPlayerZ = playerZ;
        seeker.DetectionLostTime = 0f;
        seeker.HasDetectedDuringScan = false; // Reset for next scan
    }

    /// <summary>
    /// Enters traveling state and clears aim mode.
    /// </summary>
    private void EnterTraveling(SeekerInstance seeker)
    {
        seeker.State = SeekerState.Traveling;
        seeker.AimMode = SeekerAimMode.Off;
    }

    /// <summary>
    /// Enters returning state and clears aim mode.
    /// </summary>
    private void EnterReturning(SeekerInstance seeker)
    {
        seeker.State = SeekerState.Returning;
        seeker.AimMode = SeekerAimMode.Off;
    }

    /// <summary>
    /// Handles detection behavior when the seeker currently detects the player.
    /// </summary>
    private void HandleDetecting(SeekerInstance seeker, float playerX, float playerY, float playerZ)
    {
        // Currently detecting - reset grace period timer and mark as having detected
        seeker.DetectionLostTime = 0f;
        seeker.HasDetectedDuringScan = true;

        // If in hunt mode, transition back to scanning
        // Center the patrol on the PLAYER, not the seeker - this keeps the seeker orbiting around the player
        if (seeker.State == SeekerState.Hunting)
        {
            EnterScanningAtPlayer(seeker, playerX, playerY, playerZ);
            Log.Debug(LogCategory.Sim, $"Seeker {seeker.Id} re-acquired player, resuming scan");
        }
    }

    /// <summary>
    /// Handles loss of detection during scanning and transitions to hunting if needed.
    /// </summary>
    private void HandleLostDuringScan(SeekerInstance seeker, float playerX, float playerY, float playerZ)
    {
        // Not detecting in scan mode - track how long we've lost them
        if (seeker.DetectionLostTime == 0f && seeker.HasDetectedDuringScan)
        {
            // Just lost detection - start grace period (only if we ever detected)
            seeker.DetectionLostTime = _totalTime;
        }
        else if (seeker.DetectionLostTime > 0f && _totalTime - seeker.DetectionLostTime > _config.DetectionGracePeriod)
        {
            // Grace period expired - enter hunt mode if we had detected the player
            if (seeker.HasDetectedDuringScan)
            {
                EnterHunting(seeker, playerX, playerY, playerZ);
                Log.Debug(LogCategory.Sim, $"Seeker {seeker.Id} lost player, entering hunt mode");
            }
        }
    }

    /// <summary>
    /// Handles the hunt timeout transition to returning.
    /// </summary>
    private void HandleHuntTimeout(SeekerInstance seeker)
    {
        if (_totalTime - seeker.HuntStartTime > _config.HuntDuration)
        {
            EnterReturning(seeker);
            Log.Debug(LogCategory.Sim, $"Seeker {seeker.Id} hunt timeout, returning");
        }
    }

    private void CheckForBetterPing(SeekerInstance seeker)
    {
        bool detecting = seeker.IsDetectingPlayer;

        var (bestPing, bestScore) = FindBestPing(seeker, excludeCurrentPing: true);
        if (bestPing == null) return;

        // While detecting, only allow redirects to real noise pings (not decoys or detection pings).
        if (detecting && bestPing.Type != PingType.Noise)
        {
            (bestPing, bestScore) = FindBestNoisePing(seeker, excludeCurrentPing: true);
            if (bestPing == null) return;
        }

        float currentScore = float.MinValue;
        var currentPing = GetPingById(seeker.CurrentPingId);
        if (currentPing != null)
        {
            currentScore = ScorePing(currentPing, seeker);
        }

        if (bestScore > currentScore + _config.PingRedirectThreshold)
        {
            AssignPingToSeeker(seeker, bestPing);

            if (seeker.State == SeekerState.Scanning)
            {
                if (detecting && bestPing.Type == PingType.Noise)
                {
                    // Shift scan center to the new noise ping without dropping detection.
                    seeker.PatrolCenterX = bestPing.X;
                    seeker.PatrolCenterZ = bestPing.Z;
                    seeker.PatrolAltitude = seeker.Y;
                    seeker.ScanStartTime = _totalTime;
                }
                else
                {
                    seeker.State = SeekerState.Traveling;
                    seeker.AimMode = SeekerAimMode.Off;
                }
            }

            Log.Debug(LogCategory.Sim, $"Seeker {seeker.Id} redirecting to better ping");
        }
    }

    private void ReleasePingAssignment(SeekerInstance seeker)
    {
        if (seeker.CurrentPingId >= 0)
        {
            var ping = GetPingById(seeker.CurrentPingId);
            if (ping != null)
            {
                ping.AssignedSeekers = Math.Max(0, ping.AssignedSeekers - 1);
            }
            seeker.CurrentPingId = -1;
        }
    }

    /// <summary>
    /// Assigns a ping to a seeker without changing its state.
    /// </summary>
    private void AssignPingToSeeker(SeekerInstance seeker, NoisePing ping)
    {
        ReleasePingAssignment(seeker);

        seeker.CurrentPingId = ping.Id;
        seeker.TargetX = ping.X;
        seeker.TargetY = ping.Y + _config.HoverHeight;
        seeker.TargetZ = ping.Z;
        ping.AssignedSeekers++;
    }

    /// <summary>
    /// Assigns a ping and transitions the seeker to traveling.
    /// </summary>
    private void RedirectSeekerToPing(SeekerInstance seeker, NoisePing ping)
    {
        AssignPingToSeeker(seeker, ping);
        EnterTraveling(seeker);
    }

    private void MarkPingInvestigated(int pingId)
    {
        var ping = GetPingById(pingId);
        if (ping != null)
        {
            ping.Investigated = true;
            ping.AssignedSeekers = 0;
        }
    }

    private NoisePing? GetPingById(int id)
    {
        if (id < 0) return null;
        return _pingLookup.GetValueOrDefault(id);
    }

    private (NoisePing? ping, float score) FindBestPing(SeekerInstance seeker, bool excludeCurrentPing)
    {
        return FindBestPing(seeker, excludeCurrentPing, _ => true);
    }

    private (NoisePing? ping, float score) FindBestNoisePing(SeekerInstance seeker, bool excludeCurrentPing)
    {
        return FindBestPing(seeker, excludeCurrentPing, ping => ping.Type == PingType.Noise);
    }

    private (NoisePing? ping, float score) FindBestPing(
        SeekerInstance seeker,
        bool excludeCurrentPing,
        Func<NoisePing, bool> include)
    {
        NoisePing? best = null;
        float bestScore = float.MinValue;

        foreach (var ping in _activePings)
        {
            if (!include(ping)) continue;
            if (ping.Investigated) continue;
            if (excludeCurrentPing && ping.Id == seeker.CurrentPingId) continue;

            float score = ScorePing(ping, seeker);

            if (score > bestScore)
            {
                bestScore = score;
                best = ping;
            }
        }

        return (best, bestScore);
    }

    private float ScorePing(NoisePing ping, SeekerInstance seeker)
    {
        float age = _totalTime - ping.CreationTime;
        float recencyBonus = MathF.Max(0f, _config.PingRecencyWindow - age) * _config.PingRecencyWeight;
        float priorityBonus = ping.Priority * _config.PingPriorityWeight;
        float dist = Distance(seeker.X, seeker.Y, seeker.Z, ping.X, ping.Y, ping.Z);
        float distPenalty = dist * _config.PingDistancePenalty;
        float assignedPenalty = ping.AssignedSeekers * _config.PingAssignedPenalty;

        return recencyBonus + priorityBonus - distPenalty - assignedPenalty;
    }

    #endregion

    #region Utility

    private static float Distance(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float dz = z2 - z1;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    #endregion

    #region Event Handlers

    private void OnNoisePing(NoisePingEvent e)
    {
        var ping = AddPing(e.X, e.Y, e.Z, e.IsDecoy);

        if (e.IsDecoy)
        {
            return;
        }

        // Collectible pings should pull seekers out of hunt/return.
        foreach (var seeker in _activeSeekers)
        {
            if (seeker.State != SeekerState.Hunting && seeker.State != SeekerState.Returning)
            {
                continue;
            }

            float dist = Distance(seeker.X, seeker.Y, seeker.Z, ping.X, ping.Y, ping.Z);
            if (dist > _config.NoisePullMaxDistance)
            {
                continue;
            }

            RedirectSeekerToPing(seeker, ping);
        }
    }

    private void OnDecoyPing(DecoyPingEvent e)
    {
        AddPing(e.X, e.Y, e.Z, true);
    }

    #endregion

    #region Public API

    public void RecallAll()
    {
        foreach (var seeker in _activeSeekers)
        {
            if (seeker.State != SeekerState.Pooled)
            {
                seeker.State = SeekerState.Recalled;
                seeker.AimMode = SeekerAimMode.Off;
                seeker.RecallStartTime = _totalTime;
            }
        }
    }

    public void Reset()
    {
        foreach (var seeker in _activeSeekers)
        {
            OnSeekerDespawned?.Invoke(seeker);
        }
        _activeSeekers.Clear();

        foreach (var ping in _activePings)
        {
            ping.Return();
        }
        _activePings.Clear();
        _pingLookup.Clear();

        NoisePing.ResetPool();

        _totalTime = 0f;
        _currentAlertLevel = 0f;
        _lastDetectionPingTime = 0f;
        _wardenPositionSet = false;
        _detectingCount = 0;
    }

    public void Cleanup()
    {
        _eventBus.Unsubscribe<NoisePingEvent>(OnNoisePing);
        _eventBus.Unsubscribe<DecoyPingEvent>(OnDecoyPing);
    }

    #endregion
}

/// <summary>
/// Represents a single Seeker instance.
/// </summary>
public sealed class SeekerInstance
{
    public Guid Id { get; init; }
    public SeekerState State { get; set; }

    // Position
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    // Movement target
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float TargetZ { get; set; }

    // Velocity (used by SeekerMovement)
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float VelocityZ { get; set; }

    // Patrol orbit state
    public float PatrolCenterX { get; set; }
    public float PatrolCenterZ { get; set; }
    public float PatrolAltitude { get; set; }
    public float OrbitAngle { get; set; }

    // Timing
    public float SpawnTime { get; set; }
    public float ScanStartTime { get; set; }
    public float HuntStartTime { get; set; }
    public float DetectionLostTime { get; set; }
    public float RecallStartTime { get; set; }

    // Perlin noise offset for movement variation
    public float NoiseOffset { get; set; }

    // Ping assignment
    public int CurrentPingId { get; set; } = -1;

    // Detection state
    public bool IsDetectingPlayer { get; set; }

    // Previous frame's detection state (for tracking individual transitions)
    public bool WasDetectingPlayer { get; set; }

    // Tracks if player was ever detected during this scan (for hunt mode transition)
    public bool HasDetectedDuringScan { get; set; }

    // Line of sight (set by Godot layer via raycast)
    public bool HasLineOfSight { get; set; } = true;

    // Aim system
    public SeekerAimMode AimMode { get; set; }
    public float AimTargetX { get; set; }
    public float AimTargetY { get; set; }
    public float AimTargetZ { get; set; }

    // Hunt mode - last known player position
    public float LastKnownPlayerX { get; set; }
    public float LastKnownPlayerY { get; set; }
    public float LastKnownPlayerZ { get; set; }

    // Collision avoidance (set by Godot layer)
    public float AvoidanceX { get; set; }
    public float AvoidanceY { get; set; }
    public float AvoidanceZ { get; set; }
}
