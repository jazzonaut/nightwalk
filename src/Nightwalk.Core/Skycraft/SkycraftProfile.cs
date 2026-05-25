namespace Nightwalk.Core.Skycraft;

/// <summary>
/// Movement profile of a Skycraft. Each profile occupies a distinct altitude band
/// and routing strategy (see ADR-0016).
/// </summary>
public enum SkycraftProfile
{
    /// <summary>High cruise, free sky-waypoint flight, never lands.</summary>
    Express = 0,

    /// <summary>Mid cruise, sky waypoints plus occasional rooftop landings.</summary>
    Commuter = 1,

    /// <summary>Low band, road-corridor routing, frequent landings.</summary>
    Drifter = 2,
}
