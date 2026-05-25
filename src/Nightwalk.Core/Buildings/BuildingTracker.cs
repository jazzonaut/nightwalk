using Nightwalk.Core.Buildings.Events;
using Nightwalk.Core.Events;

namespace Nightwalk.Core.Buildings;

/// <summary>
/// Tracks which buildings have been climbed by the player.
/// </summary>
public sealed class BuildingTracker
{
    private readonly IEventBus _eventBus;
    private readonly HashSet<BuildingId> _climbedBuildings = new();

    public int TotalClimbed => _climbedBuildings.Count;

    public BuildingTracker(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Checks if a building has been climbed.
    /// </summary>
    public bool IsClimbed(BuildingId buildingId) => _climbedBuildings.Contains(buildingId);

    /// <summary>
    /// Marks a building as climbed. Returns true if this was the first climb.
    /// </summary>
    public bool MarkClimbed(BuildingId buildingId, float buildingHeight)
    {
        if (!_climbedBuildings.Add(buildingId))
            return false;

        _eventBus.Publish(new BuildingClimbedEvent(
            buildingId,
            buildingHeight,
            _climbedBuildings.Count
        ));

        return true;
    }

    /// <summary>
    /// Gets all climbed building IDs (for rendering highlights).
    /// </summary>
    public IReadOnlySet<BuildingId> GetClimbedBuildings() => _climbedBuildings;

    /// <summary>
    /// Resets all tracking (for session reset).
    /// </summary>
    public void Reset()
    {
        _climbedBuildings.Clear();
    }
}
