namespace Nightwalk.Core.Config;

/// <summary>
/// Configuration for the screensaver/idle flyby mode.
/// </summary>
public sealed class ScreensaverConfig
{
    // Path generation
    /// <summary>Number of waypoints to maintain ahead of camera.</summary>
    public int WaypointsAhead { get; set; }

    /// <summary>Minimum distance between waypoints in meters.</summary>
    public float MinWaypointDistance { get; set; }

    /// <summary>Maximum distance between waypoints in meters.</summary>
    public float MaxWaypointDistance { get; set; }

    /// <summary>Chance (0-1) to turn at intersections.</summary>
    public float TurnChance { get; set; }

    // Altitude
    /// <summary>Minimum flight altitude in meters.</summary>
    public float LowAltitudeMin { get; set; }

    /// <summary>Maximum flight altitude in meters.</summary>
    public float LowAltitudeMax { get; set; }

    /// <summary>Chance (0-1) to change altitude at each waypoint.</summary>
    public float AltitudeChangeChance { get; set; }

    // Camera speed
    /// <summary>Base camera speed in meters per second.</summary>
    public float BaseSpeed { get; set; }

    /// <summary>Speed multiplier on tight turns (0-1).</summary>
    public float MinSpeedMultiplier { get; set; }

    /// <summary>How fast speed adjusts to target (higher = snappier).</summary>
    public float SpeedSmoothing { get; set; }

    // Camera roll
    /// <summary>Maximum roll angle in degrees when turning.</summary>
    public float MaxRollDegrees { get; set; }

    /// <summary>How fast roll adjusts to target (higher = snappier).</summary>
    public float RollSmoothing { get; set; }

    // Camera orientation
    /// <summary>How far ahead on path to look (0-1, as fraction of path).</summary>
    public float LookAheadT { get; set; }

    /// <summary>How fast camera orientation smooths (higher = snappier).</summary>
    public float OrientationSmoothing { get; set; }

    /// <summary>Curvature threshold for speed/roll response.</summary>
    public float CurvatureThreshold { get; set; }

    // Transitions
    /// <summary>Duration of enter/exit transitions in seconds.</summary>
    public float TransitionDuration { get; set; }
}
