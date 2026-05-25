using Nightwalk.Core.Determinism;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.Scheduling;
using Nightwalk.Core.Systems;

namespace Nightwalk.Core.World;

/// <summary>
/// Fluent builder for world construction with modular components.
/// </summary>
public sealed class WorldBuilder
{
    private readonly List<IWorldModule> _modules = new();

    public WorldBuilder AddModule(IWorldModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        _modules.Add(module);
        return this;
    }

    public WorldBuilder AddModule<T>() where T : IWorldModule, new()
    {
        return AddModule(new T());
    }

    public WorldState Build(WorldConfig config, IEventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(eventBus);

        Log.Debug(LogCategory.World, $"Building world with seed {config.Seed}");

        // Create core infrastructure
        var systemRegistry = new SystemRegistry();
        var rng = new DeterministicRng(config.Seed);
        var services = new ServiceContainer();

        var context = new WorldContext(
            eventBus, systemRegistry, rng, config, services);

        // Register core services
        services.Register<IEventBus>(eventBus);
        services.Register<ISystemRegistry>(systemRegistry);
        services.Register<IRng>(rng);
        services.Register(config);

        // Sort modules by priority and register
        var sortedModules = _modules.OrderBy(m => m.Priority).ToList();
        Log.Debug(LogCategory.World, $"Registering {sortedModules.Count} modules");
        foreach (var module in sortedModules)
        {
            Log.Debug(LogCategory.World, $"  Registering module: {module.GetType().Name} (Priority {module.Priority})");
            module.Register(context);
        }

        // Create WorldState
        var worldState = new WorldState(config, eventBus, systemRegistry);

        // Initialize all systems in sorted order
        systemRegistry.InitializeAll();

        return worldState;
    }
}
