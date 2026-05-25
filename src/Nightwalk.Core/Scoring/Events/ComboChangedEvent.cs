using Nightwalk.Core.Events;

namespace Nightwalk.Core.Scoring.Events;

/// <summary>
/// Fired when the combo multiplier changes.
/// </summary>
public sealed record ComboChangedEvent(
    float Multiplier,
    int ComboCount
) : IDomainEvent
{
    public ulong Tick => 0;
}
