using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Nightwalk.Core.Beacons;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Difficulty;
using Nightwalk.Core.Scoring;
using Nightwalk.Godot.Camera;
using Nightwalk.Godot.Audio;
using Nightwalk.Godot.Physics;
using SysVector3 = System.Numerics.Vector3;

namespace Nightwalk.Godot.Beacons;

/// <summary>
/// Manages beacon synchronization visuals and interaction.
/// The BeaconTracker is the source of truth; this class renders that state.
/// </summary>
public sealed partial class BeaconSyncManager : Node3D
{
    private BeaconTracker _tracker = null!;
    private BeaconsConfig _config = null!;
    private IScoreService? _scoreService;
    private IDifficultyService? _difficultyService;
    private Func<BeaconFrequency, IEnumerable<BeaconNode>>? _getBeaconsByFrequency;

    // All visual elements (rebuilt from tracker on reload)
    private readonly List<PrismNode> _prisms = new();
    private readonly List<PrismNode> _activePrisms = new(); // Non-locked prisms for current sync (snapshotted on complete)
    private readonly List<SignalBeamRenderer> _beams = new();

    // Current active sync state (null if no active sync)
    private BeaconNode? _sourceBeacon;
    private SignalBeamRenderer? _activeBeam;

    // Reusable buffers to avoid allocations
    private readonly List<Vector3> _beamPathBuffer = new();
    private readonly HashSet<PrismNode> _visitedPrismsBuffer = new();
    private readonly global::Godot.Collections.Array<Rid> _excludeRidsBuffer = new();

    // Beam update throttling (60Hz max)
    private const double BeamUpdateMinInterval = 1.0 / 60.0;
    private double _lastBeamUpdateTime;
    private bool _beamUpdatePending;

    // Flyby camera for cinematic after sync completion
    private FlybyCamera? _flybyCamera;
    private PrismPOVController? _prismPOV;
    private SfxManager? _sfxManager;


    /// <summary>
    /// Whether a sync is currently in progress.
    /// </summary>
    public bool IsSyncActive => _tracker.IsSyncActive;

    /// <summary>
    /// The source beacon for the current sync, if any.
    /// </summary>
    public BeaconNode? SourceBeacon => _sourceBeacon;

    /// <summary>
    /// Number of active (non-locked) prisms.
    /// </summary>
    public int PrismCount => _activePrisms.Count;

    /// <summary>
    /// Maximum prisms allowed (scaled by difficulty, 0 = unlimited).
    /// </summary>
    public int MaxPrisms => _difficultyService?.GetMaxPrisms(_config.Prism.MaxPrisms) ?? _config.Prism.MaxPrisms;

    /// <summary>
    /// Configures the sync manager.
    /// </summary>
    public void Configure(
        BeaconTracker tracker,
        BeaconsConfig config,
        IScoreService? scoreService,
        Func<BeaconFrequency, IEnumerable<BeaconNode>> getBeaconsByFrequency,
        IDifficultyService? difficultyService = null)
    {
        _tracker = tracker;
        _config = config;
        _scoreService = scoreService;
        _getBeaconsByFrequency = getBeaconsByFrequency;
        _difficultyService = difficultyService;

        Name = "BeaconSyncManager";

        // Rebuild all visuals from tracker (handles reload)
        RestoreFromTracker();
    }

    /// <summary>
    /// Updates the configuration (for hot-reload).
    /// </summary>
    public void Reconfigure(BeaconsConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Sets the flyby camera for cinematic playback after sync completion.
    /// </summary>
    public void SetFlybyCamera(FlybyCamera flybyCamera)
    {
        _flybyCamera = flybyCamera;
    }

    /// <summary>
    /// Sets the prism POV controller (to exit POV before flyby).
    /// </summary>
    public void SetPrismPOVController(PrismPOVController prismPOV)
    {
        _prismPOV = prismPOV;
    }

    /// <summary>
    /// Sets the SFX manager for procedural sounds.
    /// </summary>
    public void SetSfxManager(SfxManager? sfxManager)
    {
        _sfxManager = sfxManager;
    }

    /// <summary>
    /// Rebuilds all visuals from tracker state.
    /// Called on Configure (handles hot-reload and chunk reload).
    /// </summary>
    private void RestoreFromTracker()
    {
        // Clear existing visuals
        foreach (var prism in _prisms) prism.QueueFree();
        _prisms.Clear();
        _activePrisms.Clear();

        foreach (var beam in _beams) beam.QueueFree();
        _beams.Clear();

        _sourceBeacon = null;
        _activeBeam = null;

        // Rebuild completed syncs from tracker
        foreach (var sync in _tracker.Syncs.Where(s => s.IsCompleted))
        {
            RenderCompletedSync(sync);
        }

        // Note: Active prisms are not persisted in tracker - they are lost on reload.
        // Only completed syncs snapshot their prisms for persistence.

        // Rebuild active sync beam if any
        var activeSync = _tracker.ActiveSync;
        if (activeSync != null)
        {
            RenderActiveSyncBeam(activeSync);
        }

        // If there's an active sync, recalculate its beam
        if (_activeBeam != null)
        {
            UpdateBeamPathImmediate();
        }
    }

    /// <summary>
    /// Renders a completed sync's visuals (locked prisms and static beam).
    /// </summary>
    private void RenderCompletedSync(SyncData sync)
    {
        var color = GetFrequencyColor(sync.Frequency);

        // Create locked prisms from snapshot
        foreach (var prismData in sync.Prisms)
        {
            var prism = CreatePrismNode(prismData, color, locked: true);
            _prisms.Add(prism);
        }

        // Create static beam
        var beam = new SignalBeamRenderer();
        beam.Configure(_config.Beacon);
        beam.SetColor(color);
        AddChild(beam);
        _beams.Add(beam);

        if (sync.BeamPath != null)
        {
            beam.UpdatePath(sync.BeamPath.Select(ToGodotVector).ToList());
        }
    }

    /// <summary>
    /// Renders the active sync's beam (prisms are rendered separately).
    /// </summary>
    private void RenderActiveSyncBeam(SyncData sync)
    {
        var color = GetFrequencyColor(sync.Frequency);

        // Create dynamic beam
        var beam = new SignalBeamRenderer();
        beam.Configure(_config.Beacon);
        beam.SetColor(color);
        AddChild(beam);
        _beams.Add(beam);
        _activeBeam = beam;

        // Find source beacon
        if (_getBeaconsByFrequency != null)
        {
            foreach (var beacon in _getBeaconsByFrequency(sync.Frequency))
            {
                if (beacon.Data.Id == sync.SourceBeaconId)
                {
                    _sourceBeacon = beacon;
                    break;
                }
            }
        }
    }

    private PrismNode CreatePrismNode(PrismData data, Color color, bool locked)
    {
        var prism = new PrismNode();
        prism.Configure(_config.Prism);
        prism.SetColor(color);
        AddChild(prism);
        prism.GlobalPosition = ToGodotVector(data.Position);
        prism.SetPrismRotation(ToGodotVector(data.Rotation));
        if (locked) prism.Lock();
        return prism;
    }

    private Color GetFrequencyColor(BeaconFrequency frequency)
    {
        var index = frequency.Index;
        var hex = _config.Beacon.Frequencies.Length > index
            ? _config.Beacon.Frequencies[index]
            : "#FFFFFF";
        return Color.FromHtml(hex);
    }

    private static Vector3 ToGodotVector(SysVector3 v) => new(v.X, v.Y, v.Z);
    private static SysVector3 ToSystemVector(Vector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>
    /// Starts a sync attempt from the given beacon.
    /// Cancels any existing sync first.
    /// </summary>
    /// <returns>The auto-spawned prism, or null if sync couldn't start.</returns>
    public PrismNode? StartSync(BeaconNode beacon, Vector3? playerPosition = null)
    {
        // Cancel existing sync if any
        if (IsSyncActive)
        {
            CancelSync();
        }

        var sync = _tracker.StartSync(beacon.Data.Id, beacon.Data.Frequency);
        if (sync == null)
            return null;

        _sourceBeacon = beacon;
        beacon.SetState(BeaconState.Source, _config.Beacon);

        // Create beam renderer
        var color = GetFrequencyColor(sync.Frequency);
        _activeBeam = new SignalBeamRenderer();
        _activeBeam.Configure(_config.Beacon);
        _activeBeam.SetColor(color);
        AddChild(_activeBeam);
        _beams.Add(_activeBeam);

        // Show marker beams on target beacons
        ShowMarkerBeams(beacon.Data.Frequency);

        // Auto-spawn prism above beacon, facing away from player (horizontal)
        var spawnPos = beacon.GlobalPosition + Vector3.Up * _config.Prism.SpawnHeight;
        Vector3? faceToward = null;
        if (playerPosition.HasValue)
        {
            var awayFromPlayer = spawnPos - playerPosition.Value;
            awayFromPlayer.Y = 0; // Stay horizontal
            if (awayFromPlayer.LengthSquared() > 0.01f)
            {
                faceToward = spawnPos + awayFromPlayer;
            }
        }
        var spawnedPrism = AddPrism(spawnPos, faceToward);

        if (_sfxManager != null)
            _sfxManager.PlayGenerated(SfxSynth.BeaconActivated());

        Log.Info(LogCategory.Sim, $"Beacon sync started: {beacon.Data.Id}, frequency={beacon.Data.Frequency}");

        UpdateBeamPathImmediate();
        return spawnedPrism;
    }

    /// <summary>
    /// Adds a prism at the given position.
    /// </summary>
    public PrismNode? AddPrism(Vector3 position, Vector3? faceToward = null)
    {
        // Check prism limit (0 = unlimited)
        var maxPrisms = MaxPrisms;
        if (maxPrisms > 0 && PrismCount >= maxPrisms)
        {
            Log.Debug(LogCategory.Sim, "Cannot add prism: at limit");
            return null;
        }

        // Check and deduct cost (scaled by difficulty)
        var baseCost = _config.Prism.PlacementCost;
        var cost = _difficultyService?.GetPrismPlacementCost(baseCost) ?? baseCost;
        if (cost > 0 && _scoreService != null)
        {
            if (_scoreService.Score < cost)
            {
                Log.Debug(LogCategory.Sim, "Cannot add prism: insufficient score");
                return null;
            }
            _scoreService.RemoveScore(cost, ScoreCategory.PrismPlacement);
        }

        // Create prism visual (starts white, colored by beam path)
        var prism = new PrismNode();
        prism.Configure(_config.Prism);
        prism.SetColor(Colors.White);
        AddChild(prism);
        prism.GlobalPosition = position;

        // Orient using LookAt for proper 3D orientation (matches preview behavior)
        if (faceToward.HasValue)
        {
            var direction = faceToward.Value - position;
            prism.LookToward(direction);
        }

        _prisms.Add(prism);
        _activePrisms.Add(prism);

        Log.Debug(LogCategory.Sim, $"Prism placed at {position}, count={PrismCount}");

        if (IsSyncActive)
        {
            UpdateBeamPathImmediate();
        }

        return prism;
    }

    /// <summary>
    /// Removes a prism.
    /// </summary>
    public void RemovePrism(PrismNode prism)
    {
        var index = _activePrisms.IndexOf(prism);
        if (index < 0 || prism.IsLocked)
            return;

        _activePrisms.RemoveAt(index);
        _prisms.Remove(prism);

        // Disable collision before freeing to prevent raycast hits on pending-deletion prism
        prism.DisableCollision();
        prism.QueueFree();

        Log.Debug(LogCategory.Sim, $"Prism removed, count={PrismCount}");

        UpdateBeamPathImmediate();
    }

    /// <summary>
    /// Checks if a prism is currently active (non-locked).
    /// </summary>
    public bool ContainsPrism(PrismNode prism)
    {
        return _prisms.Contains(prism) && !prism.IsLocked;
    }

    /// <summary>
    /// Notifies that a prism's rotation has been updated.
    /// (Rotation is captured from the node when sync completes.)
    /// </summary>
    public void UpdatePrismRotation(PrismNode prism)
    {
        // No-op: rotation is read directly from prism nodes when sync completes.
        // This method exists for API compatibility.
    }

    /// <summary>
    /// Gets the refracted beam direction after passing through the specified prism.
    /// </summary>
    public (Vector3 hitPosition, Vector3 refractedDirection)? GetRefractedBeamAfterPrism(PrismNode prism)
    {
        // Calculate beam path (populates _visitedPrismsBuffer with prisms in beam-trace order)
        CalculateBeamPath(_beamPathBuffer);

        // Check if prism is in the beam path
        if (!_visitedPrismsBuffer.Contains(prism))
            return null;

        // Find the prism's position in the beam path by matching coordinates
        // Beam path: [0]=emission point, [1]=first prism hit, [2]=second prism or endpoint, etc.
        var prismPos = prism.PrismPosition;
        for (int i = 1; i < _beamPathBuffer.Count - 1; i++)
        {
            if (_beamPathBuffer[i].DistanceSquaredTo(prismPos) < 0.01f)
            {
                var hitPos = _beamPathBuffer[i];
                var nextPos = _beamPathBuffer[i + 1];
                var direction = (nextPos - hitPos).Normalized();
                return (hitPos, direction);
            }
        }

        return null;
    }

    /// <summary>
    /// Calculates the closest point on the beam to the given position for snap placement.
    /// Returns the snap position if within threshold, null otherwise.
    /// </summary>
    /// <param name="candidatePosition">The candidate position to snap from.</param>
    /// <param name="threshold">Maximum distance from beam to allow snapping.</param>
    /// <returns>The snapped position on the beam, or null if not within threshold.</returns>
    public Vector3? GetBeamSnapPosition(Vector3 candidatePosition, float threshold)
    {
        if (!IsSyncActive)
            return null;

        CalculateBeamPath(_beamPathBuffer);
        if (_beamPathBuffer.Count < 2)
            return null;

        Vector3? closestPoint = null;
        float closestDistance = float.MaxValue;

        // Check each beam segment
        for (int i = 0; i < _beamPathBuffer.Count - 1; i++)
        {
            var segmentStart = _beamPathBuffer[i];
            var segmentEnd = _beamPathBuffer[i + 1];
            var segmentDir = (segmentEnd - segmentStart).Normalized();
            var segmentLength = segmentStart.DistanceTo(segmentEnd);

            // Find closest point on this segment
            var (distance, t) = RaycastHelper.PointToRayDistance(segmentStart, segmentDir, candidatePosition, segmentLength);

            if (distance < closestDistance && distance <= threshold)
            {
                closestDistance = distance;
                closestPoint = segmentStart + segmentDir * t;
            }
        }

        return closestPoint;
    }

    /// <summary>
    /// Finds the closest point on the beam to a camera ray segment for snap placement.
    /// Returns the snap position if within threshold, null otherwise.
    /// </summary>
    /// <remarks>
    /// This uses a segment-to-segment closest-point test between the camera ray segment and each beam segment,
    /// which is stable even when the ray is skewed, parallel, or only overlaps near segment endpoints.
    /// </remarks>
    public Vector3? GetBeamSnapPositionAlongRay(Vector3 rayOrigin, Vector3 rayDir, float maxDistance, float threshold)
    {
        if (!IsSyncActive)
            return null;

        if (rayDir.LengthSquared() < 0.0001f)
            return null;

        CalculateBeamPath(_beamPathBuffer);
        if (_beamPathBuffer.Count < 2)
            return null;

        rayDir = rayDir.Normalized();
        var rayEnd = rayOrigin + rayDir * maxDistance;

        Vector3? closestPoint = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < _beamPathBuffer.Count - 1; i++)
        {
            var segmentStart = _beamPathBuffer[i];
            var segmentEnd = _beamPathBuffer[i + 1];

            var distance = RaycastHelper.SegmentToSegmentDistance(rayOrigin, rayEnd, segmentStart, segmentEnd, out var closestOnBeam);

            if (distance < closestDistance && distance <= threshold)
            {
                closestDistance = distance;
                closestPoint = closestOnBeam;
            }
        }

        return closestPoint;
    }


    /// <summary>
    /// Gets the beam direction at a given position (for orientation snapping).
    /// Returns the direction of the beam segment closest to the position.
    /// </summary>
    /// <param name="position">The position to query.</param>
    /// <param name="threshold">Maximum distance from beam to consider.</param>
    /// <returns>The beam direction at that position, or null if not near any beam.</returns>
    public Vector3? GetBeamDirectionAt(Vector3 position, float threshold)
    {
        if (!IsSyncActive)
            return null;

        CalculateBeamPath(_beamPathBuffer);
        if (_beamPathBuffer.Count < 2)
            return null;

        Vector3? closestDirection = null;
        float closestDistance = float.MaxValue;

        // Check each beam segment
        for (int i = 0; i < _beamPathBuffer.Count - 1; i++)
        {
            var segmentStart = _beamPathBuffer[i];
            var segmentEnd = _beamPathBuffer[i + 1];
            var segmentDir = (segmentEnd - segmentStart).Normalized();
            var segmentLength = segmentStart.DistanceTo(segmentEnd);

            var (distance, _) = RaycastHelper.PointToRayDistance(segmentStart, segmentDir, position, segmentLength);

            if (distance < closestDistance && distance <= threshold)
            {
                closestDistance = distance;
                closestDirection = segmentDir;
            }
        }

        return closestDirection;
    }

    /// <summary>
    /// Cancels the current sync attempt.
    /// </summary>
    public void CancelSync()
    {
        var sync = _tracker.ActiveSync;
        if (sync == null)
            return;

        var frequency = sync.Frequency;

        // Reset source beacon state
        if (_sourceBeacon != null && GodotObject.IsInstanceValid(_sourceBeacon))
        {
            _sourceBeacon.SetState(BeaconState.Idle, _config.Beacon);
        }
        _sourceBeacon = null;

        // Remove active beam
        if (_activeBeam != null)
        {
            _beams.Remove(_activeBeam);
            _activeBeam.QueueFree();
            _activeBeam = null;
        }

        // Reset prism colors to white (prisms persist independently)
        foreach (var prism in _prisms)
        {
            if (!prism.IsLocked)
            {
                prism.SetColor(Colors.White);
            }
        }

        // Hide marker beams
        HideMarkerBeams(frequency);

        // Update tracker
        _tracker.CancelSync();

        Log.Info(LogCategory.Sim, "Beacon sync cancelled");
    }

    /// <summary>
    /// Called each frame to process pending beam updates.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_beamUpdatePending)
        {
            var now = Time.GetTicksMsec() / 1000.0;
            if (now - _lastBeamUpdateTime >= BeamUpdateMinInterval)
            {
                _beamUpdatePending = false;
                UpdateBeamPathImmediate();
            }
        }
    }

    /// <summary>
    /// Updates the beam path based on current prism positions.
    /// Throttled to 60Hz max - may defer update to next frame.
    /// </summary>
    public void UpdateBeamPath()
    {
        var now = Time.GetTicksMsec() / 1000.0;
        if (now - _lastBeamUpdateTime < BeamUpdateMinInterval)
        {
            _beamUpdatePending = true;
            return;
        }

        _beamUpdatePending = false;
        UpdateBeamPathImmediate();
    }

    /// <summary>
    /// Immediately updates the beam path, bypassing throttle.
    /// Use for critical updates (sync start/complete, prism add/remove).
    /// </summary>
    public void UpdateBeamPathImmediate()
    {
        if (_sourceBeacon == null || _activeBeam == null)
            return;

        _lastBeamUpdateTime = Time.GetTicksMsec() / 1000.0;

        var isBroken = CalculateBeamPath(_beamPathBuffer);
        _activeBeam.UpdatePath(_beamPathBuffer, isBroken);

        // Update prism colors based on beam path
        UpdatePrismColors();

        // Check if final beam segment passes near a target beacon
        if (_beamPathBuffer.Count >= 2)
        {
            var segmentStart = _beamPathBuffer[^2];
            var segmentEnd = _beamPathBuffer[^1];
            CheckBeaconHitAlongRay(segmentStart, segmentEnd);
        }
    }

    /// <summary>
    /// Updates prism colors based on whether beam passes through them.
    /// </summary>
    private void UpdatePrismColors()
    {
        var sync = _tracker.ActiveSync;
        var beamColor = sync != null ? GetFrequencyColor(sync.Frequency) : Colors.White;

        foreach (var prism in _prisms)
        {
            if (prism.IsLocked)
                continue;

            var color = _visitedPrismsBuffer.Contains(prism) ? beamColor : Colors.White;
            prism.SetColor(color);
        }
    }

    /// <summary>
    /// Calculates the beam path and returns whether the beam is "broken"
    /// (reached max length without hitting geometry).
    /// </summary>
    private bool CalculateBeamPath(List<Vector3> path)
    {
        path.Clear();

        if (_sourceBeacon == null)
            return true;

        // Start from beacon emission point
        var emissionPoint = _sourceBeacon.GlobalPosition + Vector3.Up * _config.Beacon.Height;
        path.Add(emissionPoint);

        var currentPos = emissionPoint;
        var currentDir = Vector3.Up;
        var prismSnapThreshold = _config.Prism.BeamSnapThreshold;
        var maxIterations = _config.Prism.MaxPrisms;
        var maxBeamLength = _config.Beacon.SignalBeamMaxLength;

        // Track visited prisms to avoid infinite loops
        _visitedPrismsBuffer.Clear();

        // Find prisms along the beam path - snaps to nearby prisms within threshold
        for (int i = 0; i < maxIterations; i++)
        {
            var closestPrism = FindClosestPrismOnBeam(currentPos, currentDir, prismSnapThreshold, maxBeamLength, _visitedPrismsBuffer);
            if (closestPrism == null)
                break;

            _visitedPrismsBuffer.Add(closestPrism);
            var prismPos = closestPrism.PrismPosition;
            path.Add(prismPos);

            // Update direction to the prism's beam direction (refraction)
            var newDir = closestPrism.RefractBeam(currentDir);
            currentDir = newDir;
            currentPos = prismPos;
        }

        // Raycast to find endpoint
        var spaceState = GetWorld3D()?.DirectSpaceState;
        if (spaceState == null)
        {
            path.Add(currentPos + currentDir * maxBeamLength);
            return true; // Broken - no physics state
        }

        // Collect RIDs of all active prisms to exclude from raycast
        _excludeRidsBuffer.Clear();
        foreach (var prism in _prisms)
        {
            var rid = prism.CollisionRid;
            if (rid.IsValid)
                _excludeRidsBuffer.Add(rid);
        }

        // Start raycast well outside any prism collision shape (radius + buffer ~1.5m)
        var raycastStartOffset = _config.Prism.Radius + 0.5f;
        var query = PhysicsRayQueryParameters3D.Create(
            currentPos + currentDir * raycastStartOffset,
            currentPos + currentDir * maxBeamLength
        );
        query.CollideWithAreas = false;
        query.Exclude = _excludeRidsBuffer;

        var result = spaceState.IntersectRay(query);

        if (result.Count == 0)
        {
            path.Add(currentPos + currentDir * maxBeamLength);
            return true; // Broken - no hit
        }

        // Check if we hit a prism (could be one pending removal) - ignore it
        var hitCollider = result["collider"].AsGodotObject();
        if (PrismNode.FindFromCollider(hitCollider) != null)
        {
            // Hit a prism that's not in our active list - extend beam to max length
            path.Add(currentPos + currentDir * maxBeamLength);
            return true; // Broken - hit inactive prism
        }

        path.Add((Vector3)result["position"]);
        return false; // Not broken - hit geometry
    }

    /// <summary>
    /// Finds the closest active prism that the beam passes near.
    /// Uses ray-to-point distance to find prisms within threshold of the beam line.
    /// </summary>
    private PrismNode? FindClosestPrismOnBeam(Vector3 currentPos, Vector3 currentDir, float threshold, float maxDistance, HashSet<PrismNode> visited)
    {
        PrismNode? closest = null;
        float closestT = float.MaxValue; // Distance along beam to prism

        foreach (var prism in _prisms)
        {
            if (prism.IsLocked || visited.Contains(prism))
                continue;

            var prismPos = prism.PrismPosition;

            // Calculate perpendicular distance from prism to beam ray
            var (distanceToRay, t) = RaycastHelper.PointToRayDistance(currentPos, currentDir, prismPos, maxDistance);

            // Skip if prism is too far from the beam line
            if (distanceToRay > threshold)
                continue;

            // Skip if prism is behind us (t <= 0) or beyond max distance
            if (t <= 0.1f)
                continue;

            // Prefer the prism closest along the beam (smallest t)
            if (t < closestT)
            {
                closest = prism;
                closestT = t;
            }
        }

        return closest;
    }

    /// <summary>
    /// Checks if the ray segment passes within threshold distance of a target beacon.
    /// </summary>
    private void CheckBeaconHitAlongRay(Vector3 segmentStart, Vector3 segmentEnd)
    {
        if (_sourceBeacon == null || _getBeaconsByFrequency == null)
            return;

        var frequency = _sourceBeacon.Data.Frequency;
        var threshold = _config.Prism.BeamSnapThreshold;
        var segmentDir = (segmentEnd - segmentStart).Normalized();
        var segmentLength = segmentStart.DistanceTo(segmentEnd);

        foreach (var beacon in _getBeaconsByFrequency(frequency))
        {
            if (beacon == _sourceBeacon || beacon.State == BeaconState.Completed)
                continue;

            var beaconTop = beacon.GlobalPosition + Vector3.Up * _config.Beacon.Height;
            var (distance, _) = RaycastHelper.PointToRayDistance(segmentStart, segmentDir, beaconTop, segmentLength);

            if (distance < threshold)
            {
                CompleteSync(beacon);
                return;
            }
        }
    }

    private void CompleteSync(BeaconNode targetBeacon)
    {
        if (_sourceBeacon == null || _activeBeam == null)
            return;

        var activeSync = _tracker.ActiveSync;
        if (activeSync == null)
            return;

        var frequency = activeSync.Frequency;

        // Calculate final beam path and snap endpoint to target beacon
        // (populates _visitedPrismsBuffer with prisms the beam passed through)
        CalculateBeamPath(_beamPathBuffer);
        var beaconTop = targetBeacon.GlobalPosition + Vector3.Up * _config.Beacon.Height;
        if (_beamPathBuffer.Count > 0)
        {
            _beamPathBuffer[^1] = beaconTop;
        }

        // Update beam visual with snapped path (not broken since we hit the beacon)
        _activeBeam.UpdatePath(_beamPathBuffer, isBroken: false);

        // Calculate reward based on prisms actually used (visited by beam)
        var totalDistance = _activeBeam.TotalDistance;
        var usedPrismCount = _visitedPrismsBuffer.Count;

        // Calculate base points and bonuses
        var basePoints = _config.Rewards.BasePoints;
        var distanceBonus = (int)(totalDistance * _config.Rewards.DistanceBonus);
        var configMaxPrisms = _config.Prism.MaxPrisms;
        // Use difficulty-limited max for bonus calculation (0 = unlimited, fall back to config)
        var effectiveMaxPrisms = _difficultyService?.GetMaxPrisms(configMaxPrisms) ?? configMaxPrisms;
        var maxPrismsForBonus = effectiveMaxPrisms > 0 ? effectiveMaxPrisms : configMaxPrisms;
        var prismBonus = (maxPrismsForBonus - usedPrismCount) * _config.Rewards.PrismBonus;
        var rawTotalPoints = basePoints + distanceBonus + prismBonus;

        // Apply difficulty scaling to the total
        var totalPoints = _difficultyService?.GetBeaconRewardPoints(rawTotalPoints) ?? rawTotalPoints;

        Log.Info(LogCategory.Sim,
            $"Beacon sync complete: distance={totalDistance:F1}m, prisms={usedPrismCount}, points={totalPoints}");

        // Award points
        _scoreService?.AddScore(totalPoints, ScoreCategory.BeaconSync);

        // Mark beacons as completed
        _sourceBeacon.SetState(BeaconState.Completed, _config.Beacon);
        targetBeacon.SetState(BeaconState.Completed, _config.Beacon);

        var beamPath = new List<SysVector3>(_beamPathBuffer.Count);
        foreach (var v in _beamPathBuffer)
        {
            beamPath.Add(ToSystemVector(v));
        }

        // Snapshot only prisms the beam passed through (from _visitedPrismsBuffer)
        var prismDataList = new List<PrismData>(_visitedPrismsBuffer.Count);
        foreach (var prism in _visitedPrismsBuffer)
        {
            prismDataList.Add(new PrismData(
                ToSystemVector(prism.PrismPosition),
                ToSystemVector(prism.PrismRotation)
            ));
        }
        _tracker.CompleteSync(targetBeacon.Data.Id, beamPath, prismDataList, totalDistance, totalPoints);

        // Lock only prisms that were part of the sync; leave others available
        foreach (var prism in _visitedPrismsBuffer)
        {
            prism.Lock();
            _activePrisms.Remove(prism);
        }

        // Clear active state
        _sourceBeacon = null;
        _activeBeam = null;

        // Hide marker beams
        HideMarkerBeams(frequency);

        // Force-cancel POV mode before starting flyby (no transition needed since flyby takes over)
        _prismPOV?.Reset();

        // Trigger flyby camera if available (validation handled by StartFlyby)
        _flybyCamera?.StartFlyby(_beamPathBuffer);
    }

    private void ShowMarkerBeams(BeaconFrequency frequency)
    {
        if (_getBeaconsByFrequency == null)
            return;

        foreach (var beacon in _getBeaconsByFrequency(frequency))
        {
            if (beacon == _sourceBeacon || !GodotObject.IsInstanceValid(beacon))
                continue;

            if (beacon.State != BeaconState.Completed)
            {
                beacon.SetMarkerVisible(true);
                beacon.SetState(BeaconState.Receiving, _config.Beacon);
            }
        }
    }

    private void HideMarkerBeams(BeaconFrequency frequency)
    {
        if (_getBeaconsByFrequency == null)
            return;

        foreach (var beacon in _getBeaconsByFrequency(frequency))
        {
            if (!GodotObject.IsInstanceValid(beacon))
                continue;

            beacon.SetMarkerVisible(false);
            if (beacon.State == BeaconState.Receiving)
            {
                beacon.SetState(BeaconState.Idle, _config.Beacon);
            }
        }
    }

    /// <summary>
    /// Resets the sync manager for a new session.
    /// </summary>
    public void Reset()
    {
        // Clear all visuals
        foreach (var prism in _prisms) prism.QueueFree();
        _prisms.Clear();
        _activePrisms.Clear();

        foreach (var beam in _beams) beam.QueueFree();
        _beams.Clear();

        _sourceBeacon = null;
        _activeBeam = null;

        // Tracker.Reset() is called separately by the game manager
    }
}
