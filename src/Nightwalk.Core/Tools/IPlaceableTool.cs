using System.Numerics;

namespace Nightwalk.Core.Tools;

/// <summary>
/// Extended interface for tools that place objects in the world.
/// </summary>
public interface IPlaceableTool : ITool
{
    /// <summary>
    /// Maximum placement distance from player.
    /// </summary>
    float MaxPlacementDistance { get; }

    /// <summary>
    /// Validates if a position is valid for placement.
    /// </summary>
    /// <param name="position">World position to validate.</param>
    /// <returns>True if placement is valid at this position.</returns>
    bool CanPlaceAt(Vector3 position);

    /// <summary>
    /// Cancels current placement operation and resets state.
    /// </summary>
    void CancelPlacement();
}
