using Nightwalk.Core.Config;
using Nightwalk.Core.Difficulty;

namespace Nightwalk.Godot.Player;

/// <summary>
/// Manages player energy: capacity, consumption, regeneration.
/// Plain C# class - pure state management.
/// </summary>
public sealed class PlayerEnergy
{
    private float _energy;
    private float _energyMax;
    private float _baseRegenRate;
    private float _baseJetpackCost;
    private IDifficultyService? _difficultyService;

    /// <summary>
    /// Current energy level (0 to max).
    /// </summary>
    public float Energy => _energy;

    /// <summary>
    /// Maximum energy capacity.
    /// </summary>
    public float EnergyMax => _energyMax;

    /// <summary>
    /// Energy as percentage (0 to 1).
    /// </summary>
    public float EnergyPercent => _energyMax > 0 ? _energy / _energyMax : 0;

    /// <summary>
    /// Check if there is energy available.
    /// </summary>
    public bool HasEnergy => _energy > 0;

    /// <summary>
    /// Sets the difficulty service for scaled energy values.
    /// </summary>
    public void SetDifficultyService(IDifficultyService? difficultyService)
    {
        _difficultyService = difficultyService;
    }

    /// <summary>
    /// Configures energy parameters from game data.
    /// Called on initial setup and hot reload.
    /// </summary>
    public void Configure(GameDataService data)
    {
        var previousMax = _energyMax;
        _energyMax = data.Gameplay.Energy.MaxEnergy;
        _baseRegenRate = data.Gameplay.Energy.RegenRate;
        _baseJetpackCost = data.Physics.Jetpack.EnergyCostPerSecond;

        // Only set to max on initial configuration (when previousMax is 0)
        // On hot reload, clamp current energy to new max instead
        if (previousMax == 0)
        {
            _energy = _energyMax;
        }
        else
        {
            _energy = System.Math.Min(_energy, _energyMax);
        }
    }

    /// <summary>
    /// Consume energy for jetpack use.
    /// Returns the amount of energy consumed (0 if no energy available).
    /// </summary>
    public float ConsumeJetpack(float dt)
    {
        if (_energy <= 0) return 0;

        var jetpackCost = _difficultyService?.GetJetpackEnergyCost(_baseJetpackCost) ?? _baseJetpackCost;
        float consumed = jetpackCost * dt;
        float actualConsumed = System.Math.Min(consumed, _energy);
        _energy = System.Math.Max(0, _energy - consumed);
        return actualConsumed;
    }

    /// <summary>
    /// Regenerate energy over time.
    /// </summary>
    public void Regenerate(float dt)
    {
        if (_energy < _energyMax)
        {
            var regenRate = _difficultyService?.GetEnergyRegen(_baseRegenRate) ?? _baseRegenRate;
            _energy = System.Math.Min(_energyMax, _energy + regenRate * dt);
        }
    }

    /// <summary>
    /// Reset energy to max (for session reset).
    /// </summary>
    public void Reset()
    {
        _energy = _energyMax;
    }
}
