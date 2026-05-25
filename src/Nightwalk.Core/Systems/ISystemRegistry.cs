namespace Nightwalk.Core.Systems;

/// <summary>
/// Interface for system registration and execution.
/// </summary>
public interface ISystemRegistry
{
    int Count { get; }

    void Register<T>(T system) where T : class, ISystem;
    T? Get<T>() where T : class, ISystem;
    T GetRequired<T>() where T : class, ISystem;
    bool TryGet<T>(out T? system) where T : class, ISystem;
    bool Has<T>() where T : class, ISystem;
    bool Unregister<T>() where T : class, ISystem;

    void InitializeAll();
    void TickAll(ulong currentTick);
    void ShutdownAll();

    IReadOnlyList<ISystem> GetAllSystems();
}
