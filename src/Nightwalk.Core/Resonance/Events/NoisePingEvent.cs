using Nightwalk.Core.Events;

namespace Nightwalk.Core.Resonance.Events;

/// <summary>
/// Published when the player makes noise (e.g., collecting a collectible).
/// Seekers may investigate this location.
/// </summary>
public sealed record NoisePingEvent(
    float X,
    float Y,
    float Z,
    bool IsDecoy = false
) : IDomainEvent
{
    public ulong Tick => 0;
}
