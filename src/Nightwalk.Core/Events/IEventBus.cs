namespace Nightwalk.Core.Events;

/// <summary>
/// Interface for pub/sub event communication between systems.
/// </summary>
public interface IEventBus
{
    void Subscribe<T>(Action<T> handler) where T : IDomainEvent;
    void Unsubscribe<T>(Action<T> handler) where T : IDomainEvent;
    void Publish<T>(T domainEvent) where T : IDomainEvent;
    void PublishDeferred<T>(T domainEvent, EventPriority priority = EventPriority.Normal)
        where T : IDomainEvent;
    void ProcessDeferredEvents();
    int DeferredEventCount { get; }
    void ClearAll();
}
