using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.Systems;

namespace Nightwalk.Core.World;

/// <summary>
/// Represents the complete state of the game world.
/// Manages the tick loop and coordinates all systems.
/// </summary>
public sealed class WorldState
{
    public ulong CurrentTick { get; private set; }
    public ISystemRegistry Systems => _systemRegistry;
    public IEventBus Events => _eventBus;
    public WorldConfig Config { get; }

    private readonly ISystemRegistry _systemRegistry;
    private readonly IEventBus _eventBus;

    public WorldState(
        WorldConfig config,
        IEventBus eventBus,
        ISystemRegistry systemRegistry)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(systemRegistry);

        Config = config;
        _eventBus = eventBus;
        _systemRegistry = systemRegistry;
    }

    /// <summary>
    /// Advances the world simulation by one tick.
    /// </summary>
    public void Tick(ulong targetTick)
    {
        CurrentTick = targetTick;

        // Tick all registered systems in Phase/Priority order
        _systemRegistry.TickAll(CurrentTick);

        // Process any deferred events at end of tick
        _eventBus.ProcessDeferredEvents();
    }

    /// <summary>
    /// Shuts down the world and all systems.
    /// </summary>
    public void Shutdown()
    {
        Log.Info(LogCategory.World, $"WorldState shutting down at tick {CurrentTick}");
        _systemRegistry.ShutdownAll();
        _eventBus.ClearAll();
        Log.Debug(LogCategory.World, "WorldState shutdown complete");
    }
}
