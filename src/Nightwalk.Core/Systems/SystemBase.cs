namespace Nightwalk.Core.Systems;

/// <summary>
/// Base class for game systems providing default implementations.
/// </summary>
public abstract class SystemBase : ISystem
{
    public abstract SystemPhase Phase { get; }
    public virtual int Priority => 0;
    public virtual IReadOnlyList<Type> Dependencies => Array.Empty<Type>();
    public bool IsEnabled { get; set; } = true;

    public virtual void Initialize() { }
    public abstract void Tick(ulong currentTick);
    public virtual void Shutdown() { }
}
