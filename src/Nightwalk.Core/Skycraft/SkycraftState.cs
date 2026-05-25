namespace Nightwalk.Core.Skycraft;

/// <summary>
/// Top-level state for a Skycraft instance. Per ADR-0016 the landing flow is a
/// three-phase approach (horizontal decel → curved descent → settle), then dwell,
/// then takeoff. Repathing handles target-POI invalidation mid-flight.
/// </summary>
public enum SkycraftState
{
    /// <summary>Slot is free in the pool.</summary>
    Pooled = 0,

    /// <summary>Cruising toward current target POI.</summary>
    Cruising = 1,

    /// <summary>Within approach radius; decelerating horizontally.</summary>
    Approaching = 2,

    /// <summary>Curved arc descent toward settle altitude.</summary>
    Descending = 3,

    /// <summary>Landed; engines dim; backlights pulse.</summary>
    Dwelling = 4,

    /// <summary>Vertical lift to clearance altitude, then re-enter Cruising.</summary>
    TakingOff = 5,
}
