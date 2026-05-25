using Nightwalk.Core.Collectibles.Events;
using Nightwalk.Core.Events;

namespace Nightwalk.Core.Collectibles;

/// <summary>
/// Tracks which collectibles have been picked up by the player.
/// </summary>
public sealed class CollectibleTracker
{
    private readonly IEventBus _eventBus;
    private readonly HashSet<CollectibleId> _collectedIds = new();

    public int TotalCollected => _collectedIds.Count;

    public CollectibleTracker(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Checks if a collectible has been collected.
    /// </summary>
    public bool IsCollected(CollectibleId id) => _collectedIds.Contains(id);

    /// <summary>
    /// Marks a collectible as collected. Returns true if this was the first collection.
    /// </summary>
    public bool MarkCollected(CollectibleId id, int pointsAwarded, float x = 0f, float y = 0f, float z = 0f)
    {
        if (!_collectedIds.Add(id))
            return false;

        _eventBus.Publish(new CollectibleCollectedEvent(
            id,
            pointsAwarded,
            _collectedIds.Count,
            x, y, z
        ));

        return true;
    }

    /// <summary>
    /// Gets all collectible IDs that have been collected.
    /// </summary>
    public IReadOnlySet<CollectibleId> GetCollectedIds() => _collectedIds;

    /// <summary>
    /// Resets all tracking (for session reset).
    /// </summary>
    public void Reset()
    {
        _collectedIds.Clear();
    }
}
