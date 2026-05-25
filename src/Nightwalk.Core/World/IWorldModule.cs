namespace Nightwalk.Core.World;

/// <summary>
/// Interface for modular world building components.
/// Modules register systems, services, and initialize world state.
/// </summary>
public interface IWorldModule
{
    /// <summary>Lower priority loads first.</summary>
    int Priority => 0;

    /// <summary>Called during world building to register systems and services.</summary>
    void Register(IWorldContext context);
}
