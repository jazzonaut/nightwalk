namespace Nightwalk.Core.Scoring;

/// <summary>
/// Interface for the scoring system.
/// </summary>
public interface IScoreService
{
    /// <summary>
    /// Current total score.
    /// </summary>
    long Score { get; }

    /// <summary>
    /// Current combo multiplier (1.0 = no combo).
    /// </summary>
    float ComboMultiplier { get; }

    /// <summary>
    /// Current combo count.
    /// </summary>
    int ComboCount { get; }

    /// <summary>
    /// Whether scoring is disabled (e.g., Observer mode).
    /// </summary>
    bool IsScoringDisabled { get; }

    /// <summary>
    /// Adds points to the score (multiplied by current combo).
    /// </summary>
    /// <param name="basePoints">Base points before multiplier.</param>
    /// <param name="category">Score category for tracking.</param>
    void AddScore(int basePoints, ScoreCategory category);

    /// <summary>
    /// Removes points from the score (not affected by combo).
    /// </summary>
    /// <param name="points">Points to remove (positive value).</param>
    /// <param name="category">Score category for tracking.</param>
    void RemoveScore(int points, ScoreCategory category);

    /// <summary>
    /// Increments the combo counter.
    /// </summary>
    void IncrementCombo();

    /// <summary>
    /// Resets the combo to 1x.
    /// </summary>
    void ResetCombo();

    /// <summary>
    /// Resets all scoring state (for new session).
    /// </summary>
    void Reset();

    /// <summary>
    /// Updates combo decay (call each frame).
    /// </summary>
    /// <param name="delta">Time since last frame.</param>
    void Update(float delta);
}
