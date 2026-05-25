using Nightwalk.Core.Diagnostics;

namespace Nightwalk.Core.Events;

/// <summary>
/// Pub/sub event bus for decoupled communication between systems.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly Dictionary<Type, Action<IDomainEvent>[]> _handlerCache = new();
    private readonly HashSet<Type> _dirtyTypes = new();
    private readonly List<QueuedEvent> _deferredEvents = new();
    private readonly List<QueuedEvent> _toProcess = new();
    private readonly object _lock = new();

#if DEBUG
    // Debug-mode subscription tracking for leak detection
    private readonly Dictionary<Type, List<string>> _subscriptionSources = new();
#endif

    public int DeferredEventCount
    {
        get { lock (_lock) { return _deferredEvents.Count; } }
    }

    public void Subscribe<T>(Action<T> handler) where T : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_lock)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }
            list.Add(handler);
            _dirtyTypes.Add(type); // Invalidate cache

#if DEBUG
            // Track subscription source for leak detection
            if (!_subscriptionSources.TryGetValue(type, out var sources))
            {
                sources = new List<string>();
                _subscriptionSources[type] = sources;
            }
            var target = handler.Target?.GetType().FullName ?? "static";
            var method = handler.Method.Name;
            sources.Add($"{target}.{method}");
#endif
        }
    }

    public void Unsubscribe<T>(Action<T> handler) where T : IDomainEvent
    {
        lock (_lock)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list))
            {
                list.Remove(handler);
                _dirtyTypes.Add(type);

#if DEBUG
                // Remove tracking entry
                if (_subscriptionSources.TryGetValue(type, out var sources) && sources.Count > 0)
                {
                    var target = handler.Target?.GetType().FullName ?? "static";
                    var method = handler.Method.Name;
                    sources.Remove($"{target}.{method}");
                }
#endif
            }
        }
    }

    public void Publish<T>(T domainEvent) where T : IDomainEvent
    {
        var handlers = GetHandlers(typeof(T));
        if (handlers == null)
            return;

        foreach (var handler in handlers)
        {
            try
            {
                handler(domainEvent);
            }
            catch (Exception ex)
            {
                Log.Error(LogCategory.Sim, $"Event handler exception for {typeof(T).Name}", ex);
            }
        }
    }

    public void PublishDeferred<T>(T domainEvent, EventPriority priority = EventPriority.Normal)
        where T : IDomainEvent
    {
        lock (_lock)
        {
            _deferredEvents.Add(new QueuedEvent(domainEvent, typeof(T), priority));
        }
    }

    /// <summary>
    /// Processes all deferred events, publishing them in priority order.
    /// Note: Events published via PublishDeferred during this call will be queued
    /// for the NEXT ProcessDeferredEvents call, not the current one. This is by design
    /// to prevent infinite loops and ensure predictable event ordering.
    /// </summary>
    public void ProcessDeferredEvents()
    {
        lock (_lock)
        {
            if (_deferredEvents.Count == 0)
                return;

            // Sort by priority (lower number = higher priority)
            _deferredEvents.Sort((a, b) => ((int)a.Priority).CompareTo((int)b.Priority));

            // Reuse _toProcess list to avoid allocation
            // Note: Events published during processing go into _deferredEvents (now empty)
            // and will be processed in the next call - this prevents infinite loops
            _toProcess.Clear();
            _toProcess.AddRange(_deferredEvents);
            _deferredEvents.Clear();
        }

        // Publish events outside the lock
        foreach (var evt in _toProcess)
        {
            PublishTyped(evt.Event, evt.EventType);
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _handlers.Clear();
            _handlerCache.Clear();
            _dirtyTypes.Clear();
            _deferredEvents.Clear();
            _toProcess.Clear();
#if DEBUG
            _subscriptionSources.Clear();
#endif
        }
    }

#if DEBUG
    /// <summary>
    /// Returns subscription statistics for leak detection. Debug builds only.
    /// </summary>
    public Dictionary<string, List<string>> GetSubscriptionStats()
    {
        lock (_lock)
        {
            var stats = new Dictionary<string, List<string>>();
            foreach (var (type, sources) in _subscriptionSources)
            {
                if (sources.Count > 0)
                {
                    stats[type.Name] = new List<string>(sources);
                }
            }
            return stats;
        }
    }

    /// <summary>
    /// Logs all active subscriptions for debugging. Debug builds only.
    /// </summary>
    public void LogActiveSubscriptions()
    {
        var stats = GetSubscriptionStats();
        if (stats.Count == 0)
        {
            Log.Info(LogCategory.Sim, "EventBus: No active subscriptions");
            return;
        }

        Log.Info(LogCategory.Sim, $"EventBus: {stats.Count} event types with active subscriptions:");
        foreach (var (eventType, subscribers) in stats)
        {
            Log.Info(LogCategory.Sim, $"  {eventType}: {subscribers.Count} subscriber(s)");
            foreach (var subscriber in subscribers)
            {
                Log.Info(LogCategory.Sim, $"    - {subscriber}");
            }
        }
    }
#endif

    private void PublishTyped(IDomainEvent domainEvent, Type eventType)
    {
        var handlers = GetHandlers(eventType);
        if (handlers == null)
            return;

        foreach (var handler in handlers)
        {
            try
            {
                handler(domainEvent);
            }
            catch (Exception ex)
            {
                Log.Error(LogCategory.Sim, $"Event handler exception for {eventType.Name}", ex);
            }
        }
    }

    private Action<IDomainEvent>[]? GetHandlers(Type eventType)
    {
        lock (_lock)
        {
            if (_dirtyTypes.Contains(eventType) || !_handlerCache.TryGetValue(eventType, out var handlers))
            {
                if (!_handlers.TryGetValue(eventType, out var list) || list.Count == 0)
                    return null;

                handlers = BuildHandlerCache(eventType, list);
                _handlerCache[eventType] = handlers;
                _dirtyTypes.Remove(eventType);
            }
            return handlers;
        }
    }

    private static Action<IDomainEvent>[] BuildHandlerCache(Type eventType, List<Delegate> handlers)
    {
        var result = new Action<IDomainEvent>[handlers.Count];
        for (int i = 0; i < handlers.Count; i++)
        {
            result[i] = CreateWrapper(eventType, handlers[i]);
        }
        return result;
    }

    /// <summary>
    /// Creates a wrapper delegate using reflection. This is only called once per event type
    /// when building the handler cache (first subscription), not on every publish.
    /// The result is cached in _handlerCache to avoid repeated reflection.
    /// For maximum performance, consider source generators for compile-time wrapper generation.
    /// </summary>
    private static Action<IDomainEvent> CreateWrapper(Type eventType, Delegate handler)
    {
        var method = typeof(EventBus)
            .GetMethod(nameof(Wrap), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var generic = method!.MakeGenericMethod(eventType);
        return (Action<IDomainEvent>)generic.Invoke(null, new object[] { handler })!;
    }

    private static Action<IDomainEvent> Wrap<T>(Action<T> handler) where T : IDomainEvent
    {
        return evt => handler((T)evt);
    }

    // Internal helper class for deferred events
    private sealed class QueuedEvent
    {
        public IDomainEvent Event { get; }
        public Type EventType { get; }
        public EventPriority Priority { get; }

        public QueuedEvent(IDomainEvent evt, Type type, EventPriority priority)
        {
            Event = evt;
            EventType = type;
            Priority = priority;
        }
    }
}
