using Nightwalk.Core.Config;
using Nightwalk.Core.Difficulty;
using Nightwalk.Core.Events;
using Nightwalk.Core.Scoring.Events;

namespace Nightwalk.Core.Scoring;

/// <summary>
/// Manages game score with combo multipliers.
/// </summary>
public sealed class ScoreService : IScoreService
{
    private readonly IEventBus _eventBus;
    private readonly GameDataService _data;
    private readonly IDifficultyService? _difficultyService;

    private ScoringConfig Config => _data.Gameplay.Scoring;

    private long _score;
    private float _comboMultiplier = 1f;
    private int _comboCount;
    private float _timeSinceLastScore;

    public long Score => _score;
    public float ComboMultiplier => _comboMultiplier;
    public int ComboCount => _comboCount;

    /// <summary>
    /// Whether scoring is disabled (e.g., Observer mode).
    /// </summary>
    public bool IsScoringDisabled => _difficultyService?.IsScoringDisabled ?? false;

    public ScoreService(IEventBus eventBus, GameDataService data, IDifficultyService? difficultyService = null)
    {
        _eventBus = eventBus;
        _data = data;
        _difficultyService = difficultyService;
    }

    public void AddScore(int basePoints, ScoreCategory category)
    {
        // Don't add score if scoring is disabled
        if (IsScoringDisabled) return;

        // Apply combo multiplier
        int actualPoints = (int)(basePoints * _comboMultiplier);
        _score += actualPoints;
        _timeSinceLastScore = 0;

        _eventBus.Publish(new ScoreChangedEvent(
            Points: actualPoints,
            NewTotal: _score,
            Category: category,
            Multiplier: _comboMultiplier
        ));
    }

    public void RemoveScore(int points, ScoreCategory category)
    {
        // Don't remove score if scoring is disabled
        if (IsScoringDisabled) return;

        // Don't apply combo to penalties - use raw points
        int actualPoints = Math.Abs(points);
        long previousScore = _score;
        _score = Math.Max(0, _score - actualPoints);

        // Only publish if score actually changed (skip if already at 0)
        if (_score != previousScore)
        {
            int actualLoss = (int)(previousScore - _score);
            _eventBus.Publish(new ScoreChangedEvent(
                Points: -actualLoss,
                NewTotal: _score,
                Category: category,
                Multiplier: 1f
            ));
        }

        // Fall damage resets combo (penalty for poor landing)
        if (category == ScoreCategory.FallPenalty)
        {
            ResetCombo();
        }
    }

    public void IncrementCombo()
    {
        _comboCount++;
        float newMultiplier = 1f + (_comboCount * Config.ComboIncrement);
        newMultiplier = Math.Min(newMultiplier, Config.ComboMaxMultiplier);

        if (Math.Abs(newMultiplier - _comboMultiplier) > 0.01f)
        {
            _comboMultiplier = newMultiplier;
            _eventBus.Publish(new ComboChangedEvent(_comboMultiplier, _comboCount));
        }

        _timeSinceLastScore = 0;
    }

    public void ResetCombo()
    {
        if (_comboCount > 0 || _comboMultiplier > 1f)
        {
            _comboCount = 0;
            _comboMultiplier = 1f;
            _eventBus.Publish(new ComboChangedEvent(1f, 0));
        }
    }

    public void Reset()
    {
        _score = 0;
        _comboCount = 0;
        _comboMultiplier = 1f;
        _timeSinceLastScore = 0;

        // Only publish combo reset - HUD handles score reset separately via GameHUD.Reset()
        _eventBus.Publish(new ComboChangedEvent(1f, 0));
    }

    public void Update(float delta)
    {
        // Decay combo after delay
        if (_comboMultiplier > 1f)
        {
            _timeSinceLastScore += delta;

            if (_timeSinceLastScore > Config.ComboDecayDelay)
            {
                float decay = Config.ComboDecayRate * delta;
                float newMultiplier = Math.Max(1f, _comboMultiplier - decay);

                // Sync count with multiplier to prevent jump-back on next action
                int newCount = (int)((newMultiplier - 1f) / Config.ComboIncrement);

                if (newMultiplier <= 1f)
                {
                    ResetCombo();
                }
                else
                {
                    _comboMultiplier = newMultiplier;
                    _comboCount = newCount;
                    _eventBus.Publish(new ComboChangedEvent(_comboMultiplier, _comboCount));
                }
            }
        }
    }
}
