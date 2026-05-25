namespace Nightwalk.Core.Systems;

/// <summary>
/// Defines the execution phase of a system within the tick loop.
/// Systems are executed in phase order, then by priority within each phase.
/// </summary>
public enum SystemPhase
{
    /// <summary>Early phase for input processing and command queuing.</summary>
    Input = 0,

    /// <summary>Pre-physics phase for systems that need to run before physics.</summary>
    PrePhysics = 100,

    /// <summary>Main physics phase for movement, collision, and physics simulation.</summary>
    Physics = 200,

    /// <summary>Post-physics phase for systems reacting to physics results.</summary>
    PostPhysics = 300,

    /// <summary>Combat phase for weapons, projectiles, and damage processing.</summary>
    Combat = 400,

    /// <summary>Effects phase for explosions, particles, and visual effects.</summary>
    Effects = 500,

    /// <summary>Gameplay phase for game logic, pickups, and state changes.</summary>
    Gameplay = 600,

    /// <summary>Late phase for health regeneration, death processing, and cleanup.</summary>
    Late = 700,

    /// <summary>Final phase for cleanup and end-of-tick processing.</summary>
    Cleanup = 800
}
