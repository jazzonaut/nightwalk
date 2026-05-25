namespace Nightwalk.Core.Scheduling;

/// <summary>
/// Interface for accessing registered services.
/// </summary>
public interface IServices
{
    T GetRequired<T>() where T : class;
    T? Get<T>() where T : class;
    bool TryGet<T>(out T? service) where T : class;
    bool Has<T>() where T : class;
}
