using Nightwalk.Core.World.Roads;

namespace Nightwalk.Core.Skycraft;

/// <summary>
/// Pool-owned mutable state for a single Skycraft. The pool resets every field on
/// reuse — no stale identity carryover, per ADR-0016 §Hitching.
/// </summary>
public sealed class SkycraftInstance
{
    public SkycraftId Id;
    public SkycraftProfile Profile;
    public SkycraftState State;

    // World-space position
    public float X, Y, Z;

    // World-space velocity (m/s)
    public float Vx, Vy, Vz;

    // Forward direction (unit vector; renderer uses this for orientation)
    public float ForwardX = 0f, ForwardY = 0f, ForwardZ = 1f;

    // Current target POI
    public SkycraftPoi Target;
    public bool HasTarget;

    // Drifter road-walk state (mirrors ScreensaverWaypointGenerator). The Drifter is
    // "on" a specific RoadSegment (direction + perpendicular position); it cruises
    // along that axis and only turns at real intersections where a perpendicular
    // RoadSegment exists. Guarantees the cruise path stays on actual road geometry.
    public RoadDirection RoadDir;
    public float RoadPos;
    public float RoadHalfWidth;       // copied from the snapped RoadSegment for lane-offset math
    public bool OnRoad;
    public bool MovingPositive;

    // Target's nearest road segment — recorded at AssignTarget so the cruise knows
    // which road to end on. For targets that already sit on a road (Drifter's road
    // landings) this matches the target's coords; for rooftop landings or sky
    // waypoints this is the road bordering the plot under the target.
    public RoadDirection TargetRoadDir;
    public float TargetRoadPos;
    public bool HasTargetRoad;

    // Position of the next intersection we plan to turn at, set when a turn is
    // committed. The Skycraft decelerates toward this point along its current road,
    // arcs through a quarter-circle, then accelerates on the new road.
    // Stored as raw coords because intersections live on the road grid, not on POI
    // markers. NextRoadHalfWidth captures the new road's half-width so the lane
    // offset on the post-curve road is correct.
    public float TurnAtX;
    public float TurnAtZ;
    public float NextRoadHalfWidth;
    public bool HasTurnPending;

    // Whether the next POI arrival is allowed to convert into a landing.
    // After a landing+takeoff cycle, immunity is set so the immediate next
    // POI cannot land (prevents serial landings).
    public bool LandingImmunity;

    // Per-instance cruise altitude, sampled from the profile's [AltitudeMin, AltitudeMax]
    // band on spawn and re-sampled at each takeoff. Road-walk, fly-to-road, and takeoff
    // climb all target this Y so traffic spreads across the band instead of converging
    // on the midpoint.
    public float CruiseY;

    // Landing state timing
    public float DwellRemaining;       // Seconds left in Dwelling
    public float TakeoffHeightTarget;  // Y to reach during TakingOff before re-cruising

    // Per-instance visual jitter (set once on spawn; consumed by renderer)
    public float ScaleJitter = 1f;
    public float AccentHueShiftDegrees = 0f;

    // Reserved surface for "hitch a ride" — no code writes this today.
    // Landing/takeoff/despawn logic reads it and refuses to orphan a rider.
    public bool HasRider;

    /// <summary>
    /// Clears every field including <see cref="Id"/>. The slot is about to return to the
    /// pool; a fresh Id is assigned on the next rent so stale references to the old Id
    /// can't accidentally hit a recycled slot.
    /// </summary>
    public void Reset()
    {
        Id = default;
        Profile = SkycraftProfile.Express;
        State = SkycraftState.Pooled;
        X = Y = Z = 0f;
        Vx = Vy = Vz = 0f;
        ForwardX = 0f; ForwardY = 0f; ForwardZ = 1f;
        Target = default;
        HasTarget = false;
        RoadDir = RoadDirection.EastWest;
        RoadPos = 0f;
        RoadHalfWidth = 0f;
        OnRoad = false;
        MovingPositive = true;
        TurnAtX = 0f;
        TurnAtZ = 0f;
        NextRoadHalfWidth = 0f;
        HasTurnPending = false;
        LandingImmunity = false;
        CruiseY = 0f;
        DwellRemaining = 0f;
        TakeoffHeightTarget = 0f;
        ScaleJitter = 1f;
        AccentHueShiftDegrees = 0f;
        HasRider = false;
    }
}
