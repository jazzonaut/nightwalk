using Nightwalk.Core.Determinism;
using Nightwalk.Core.Events;
using Nightwalk.Core.Scheduling;
using Nightwalk.Core.Systems;

namespace Nightwalk.Core.World;

/// <summary>
/// Implementation of world context for module registration.
/// </summary>
public sealed class WorldContext : IWorldContext
{
    private readonly ServiceContainer _services;

    public IEventBus EventBus { get; }
    public ISystemRegistry Systems { get; }
    public IRng Rng { get; }
    public WorldConfig Config { get; }

    public WorldContext(
        IEventBus eventBus,
        ISystemRegistry systems,
        IRng rng,
        WorldConfig config,
        ServiceContainer services)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(systems);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(services);

        EventBus = eventBus;
        Systems = systems;
        Rng = rng;
        Config = config;
        _services = services;
    }

    public void RegisterService<T>(T service) where T : class
    {
        _services.Register(service);
    }

    public T GetService<T>() where T : class
    {
        return _services.GetRequired<T>();
    }

    public bool TryGetService<T>(out T? service) where T : class
    {
        return _services.TryGet(out service);
    }
}
