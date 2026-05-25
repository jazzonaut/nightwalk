using Godot;
using Nightwalk.Core.Buildings;
using Nightwalk.Core.Buildings.Events;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.Scoring;

namespace Nightwalk.Godot.Buildings;

/// <summary>
/// Service that detects when player climbs buildings and awards score.
/// </summary>
public sealed class BuildingClimbService
{
    private readonly BuildingTracker _tracker;
    private readonly RooftopDetector _detector;

    private BuildingId? _lastRooftopId;

    public BuildingTracker Tracker => _tracker;

    public BuildingClimbService(
        IEventBus eventBus,
        ChunkManager chunkManager,
        GameDataService data)
    {
        _tracker = new BuildingTracker(eventBus);
        _detector = new RooftopDetector(chunkManager, data);
    }

    /// <summary>
    /// Updates the service with the current player position.
    /// Call this every frame when game is active.
    /// </summary>
    public void Update(Vector3 playerPosition, bool isOnGround)
    {
        // Only check when player is on a surface (not falling/flying)
        if (!isOnGround)
        {
            _lastRooftopId = null;
            return;
        }

        var result = _detector.GetRooftopAt(playerPosition.X, playerPosition.Y, playerPosition.Z);

        if (result == null)
        {
            _lastRooftopId = null;
            return;
        }

        var (buildingId, building) = result.Value;

        // Don't re-trigger for the same rooftop while standing on it
        if (_lastRooftopId == buildingId)
            return;

        _lastRooftopId = buildingId;

        // Try to mark as climbed (returns false if already climbed)
        // Note: Points are now awarded via collectible pickup, not rooftop detection
        if (_tracker.MarkClimbed(buildingId, building.Height))
        {
            Log.Debug(LogCategory.Sim, $"Building visited: {buildingId}, height={building.Height:F1}m, total={_tracker.TotalClimbed}");
        }
    }

    /// <summary>
    /// Resets all tracking (for session reset).
    /// </summary>
    public void Reset()
    {
        _tracker.Reset();
        _lastRooftopId = null;
    }
}
