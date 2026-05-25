using Nightwalk.Core.Chunks;

namespace Nightwalk.Core.Skycraft;

/// <summary>
/// Point of interest for Skycraft routing. POIs are derived deterministically from
/// chunk geometry (rooftops/roads) or emitted as pure-air waypoints by the world
/// module (see ADR-0016 §4).
/// </summary>
public enum SkycraftPoiKind
{
    /// <summary>Pure-air destination at Express altitude.</summary>
    ExpressWaypoint = 0,

    /// <summary>Pure-air destination at Commuter altitude.</summary>
    CommuterWaypoint = 1,

    /// <summary>Eligible rooftop landing pad (Commuter target).</summary>
    RooftopLanding = 2,

    /// <summary>Eligible road landing zone (Drifter target).</summary>
    RoadLanding = 3,
}

/// <summary>
/// Single POI entry. Position is world-space; the chunk coord lets the controller
/// detect invalidation when the owning chunk unloads.
/// </summary>
public readonly struct SkycraftPoi
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;
    public readonly SkycraftPoiKind Kind;
    public readonly ChunkCoord Chunk;

    public SkycraftPoi(float x, float y, float z, SkycraftPoiKind kind, ChunkCoord chunk)
    {
        X = x;
        Y = y;
        Z = z;
        Kind = kind;
        Chunk = chunk;
    }
}
