namespace Nightwalk.Core.Systems;

/// <summary>
/// Interface for objects that can be ticked each frame.
/// </summary>
public interface ITickable
{
    void Tick(ulong currentTick);
}
