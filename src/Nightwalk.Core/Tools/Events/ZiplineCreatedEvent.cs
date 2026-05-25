using System.Numerics;
using Nightwalk.Core.Events;

namespace Nightwalk.Core.Tools.Events;

/// <summary>
/// Event published when a zipline is created.
/// </summary>
public sealed record ZiplineCreatedEvent(
    Vector3 Anchor1,
    Vector3 Anchor2
) : IDomainEvent
{
    public ulong Tick => 0;
}
