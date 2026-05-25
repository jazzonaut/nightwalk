namespace Nightwalk.Core.Difficulty;

/// <summary>
/// Difficulty levels for the game.
/// </summary>
public enum DifficultyLevel
{
    /// <summary>
    /// Free exploration mode - no costs, no damage, no score.
    /// </summary>
    Observer,

    /// <summary>
    /// Balanced baseline experience - all multipliers at 1.0x.
    /// </summary>
    Explorer,

    /// <summary>
    /// Challenging mode for experienced players.
    /// </summary>
    Wanderer,

    /// <summary>
    /// Brutal mastery mode.
    /// </summary>
    Architect
}
