namespace Nightwalk.Core.Scheduling;

/// <summary>
/// Lightweight dependency injection container for sharing services between systems.
/// </summary>
public sealed class ServiceContainer : IServices
{
    private readonly Dictionary<Type, object> _services = new();
    private readonly object _lock = new();

    public void Register<T>(T instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_lock)
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
                throw new InvalidOperationException($"Service {type.Name} already registered");
            _services[type] = instance;
        }
    }

    public void RegisterOrReplace<T>(T instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        lock (_lock) { _services[typeof(T)] = instance; }
    }

    public T GetRequired<T>() where T : class
    {
        return Get<T>() ?? throw new KeyNotFoundException($"Service {typeof(T).Name} not found");
    }

    public T? Get<T>() where T : class
    {
        lock (_lock)
        {
            return _services.TryGetValue(typeof(T), out var service) ? (T)service : null;
        }
    }

    public bool TryGet<T>(out T? service) where T : class
    {
        lock (_lock)
        {
            if (_services.TryGetValue(typeof(T), out var s))
            {
                service = (T)s;
                return true;
            }
            service = null;
            return false;
        }
    }

    public bool Has<T>() where T : class
    {
        lock (_lock) { return _services.ContainsKey(typeof(T)); }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var service in _services.Values)
            {
                (service as IDisposable)?.Dispose();
            }
            _services.Clear();
        }
    }
}
