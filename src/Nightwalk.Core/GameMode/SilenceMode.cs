namespace Nightwalk.Core.GameMode;

/// <summary>
/// Default game mode with no additional mechanics.
/// Pure exploration without Resonance tension systems.
/// </summary>
public sealed class SilenceMode : IGameMode
{
    public string Id => "silence";
    public string DisplayName => "Silence";
    public string Description => "Pure exploration. No watchers.";

    public void Enter()
    {
        // Nothing to initialize - this mode has no special mechanics
    }

    public void Exit()
    {
        // Nothing to clean up
    }

    public void Tick(double delta)
    {
        // No per-tick logic
    }

    public void Process(double delta)
    {
        // No per-frame logic
    }

    public void Reset()
    {
        // Nothing to reset
    }
}
