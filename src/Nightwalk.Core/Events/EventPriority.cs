namespace Nightwalk.Core.Events;

/// <summary>
/// Priority for deferred event processing.
/// Lower values are processed first.
/// </summary>
public enum EventPriority
{
    /// <summary>Critical events like player death, game state.</summary>
    Critical = 0,

    /// <summary>Most gameplay events.</summary>
    Normal = 100,

    /// <summary>Visual/audio triggers.</summary>
    Low = 200
}
