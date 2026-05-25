using Nightwalk.Core.Events;

namespace Nightwalk.Core.Scoring.Events;

/// <summary>
/// Fired when the score changes. Points may be positive (score added) or negative (penalty applied).
/// </summary>
public sealed record ScoreChangedEvent(
    long Points,
    long NewTotal,
    ScoreCategory Category,
    float Multiplier
) : IDomainEvent
{
    public ulong Tick => 0;
}
