namespace Nightwalk.Core.Systems;

/// <summary>
/// Interface for game systems that process entities each tick.
/// </summary>
public interface ISystem : ITickable
{
    /// <summary>The execution phase of this system.</summary>
    SystemPhase Phase { get; }

    /// <summary>Priority within the phase. Lower executes first.</summary>
    int Priority => 0;

    /// <summary>Dependencies on other systems (must be in same phase).</summary>
    IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

    /// <summary>Whether this system is currently enabled.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Called once when the system is registered and initialized.</summary>
    void Initialize();

    /// <summary>Called when the system is being shut down.</summary>
    void Shutdown();
}
