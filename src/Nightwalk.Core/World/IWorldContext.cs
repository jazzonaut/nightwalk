using Nightwalk.Core.Determinism;
using Nightwalk.Core.Events;
using Nightwalk.Core.Systems;

namespace Nightwalk.Core.World;

/// <summary>
/// Context provided to world modules during registration.
/// </summary>
public interface IWorldContext
{
    IEventBus EventBus { get; }
    ISystemRegistry Systems { get; }
    IRng Rng { get; }
    WorldConfig Config { get; }

    // Service registration for sharing between modules
    void RegisterService<T>(T service) where T : class;
    T GetService<T>() where T : class;
    bool TryGetService<T>(out T? service) where T : class;
}
