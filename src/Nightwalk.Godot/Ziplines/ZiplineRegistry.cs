using System.Collections.Generic;
using Godot;

namespace Nightwalk.Godot.Ziplines;

/// <summary>
/// Registry tracking all active zipline cables in the world.
/// Used for proximity detection and riding mechanics.
/// </summary>
public sealed class ZiplineRegistry
{
    private readonly List<ZiplineEntry> _entries = new();

    /// <summary>
    /// Number of registered ziplines.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Registers a cable with its anchors.
    /// </summary>
    public void Register(ZiplineCable cable, ZiplineAnchor anchor1, ZiplineAnchor anchor2)
    {
        if (IsRegistered(cable))
            return;

        _entries.Add(new ZiplineEntry { Cable = cable, Anchor1 = anchor1, Anchor2 = anchor2 });
    }

    /// <summary>
    /// Removes and destroys the oldest zipline (cable + anchors).
    /// </summary>
    public void RemoveOldest()
    {
        if (_entries.Count == 0) return;
        RemoveZipline(_entries[0].Cable);
    }

    /// <summary>
    /// Removes and destroys a specific zipline by its cable.
    /// </summary>
    public void RemoveZipline(ZiplineCable cable)
    {
        var index = _entries.FindIndex(e => e.Cable == cable);
        if (index >= 0)
        {
            var entry = _entries[index];
            _entries.RemoveAt(index);

            entry.Anchor1?.QueueFree();
            entry.Anchor2?.QueueFree();
            entry.Cable?.QueueFree();
        }
    }

    /// <summary>
    /// Removes and destroys all ziplines.
    /// </summary>
    public void ClearAll()
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            _entries.RemoveAt(i);

            if (IsInstanceValid(entry.Anchor1))
                entry.Anchor1.QueueFree();
            if (IsInstanceValid(entry.Anchor2))
                entry.Anchor2.QueueFree();
            if (IsInstanceValid(entry.Cable))
                entry.Cable.QueueFree();
        }
    }

    /// <summary>
    /// Checks if a cable is currently registered.
    /// </summary>
    public bool IsRegistered(ZiplineCable cable)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Cable == cable)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Finds the nearest cable to a position within a maximum distance.
    /// Also prunes any invalid cable references.
    /// </summary>
    /// <param name="position">World position to check.</param>
    /// <param name="maxDistance">Maximum distance to consider.</param>
    /// <returns>Tuple of (cable, t parameter, distance) or null if none found.</returns>
    public (ZiplineCable cable, float t, float distance)? FindNearestCable(Vector3 position, float maxDistance)
    {
        var result = FindNearestEntry(position, maxDistance);
        if (result.HasValue)
        {
            return (result.Value.entry.Cable, result.Value.t, result.Value.distance);
        }
        return null;
    }

    /// <summary>
    /// Finds the nearest entry to a position within a maximum distance.
    /// Also prunes any invalid cable references.
    /// Note: Uses linear search which is acceptable for typical zipline counts (under ~100).
    /// For very large counts, spatial partitioning (octree/grid) would be needed.
    /// </summary>
    public (ZiplineEntry entry, float t, float distance)? FindNearestEntry(Vector3 position, float maxDistance)
    {
        ZiplineEntry? bestEntry = null;
        float bestT = 0f;
        float bestDistance = maxDistance;

        // Prune invalid entries while iterating
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (!IsInstanceValid(entry.Cable))
            {
                // Free orphaned anchors if they still exist
                if (IsInstanceValid(entry.Anchor1))
                    entry.Anchor1.QueueFree();
                if (IsInstanceValid(entry.Anchor2))
                    entry.Anchor2.QueueFree();
                _entries.RemoveAt(i);
                continue;
            }

            var (t, distance) = entry.Cable.FindClosestT(position);
            if (distance < bestDistance)
            {
                bestEntry = entry;
                bestT = t;
                bestDistance = distance;
            }
        }

        if (bestEntry.HasValue)
        {
            return (bestEntry.Value, bestT, bestDistance);
        }

        return null;
    }

    private static bool IsInstanceValid(GodotObject obj)
    {
        return GodotObject.IsInstanceValid(obj);
    }
}
