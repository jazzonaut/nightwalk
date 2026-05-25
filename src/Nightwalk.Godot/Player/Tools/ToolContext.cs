using Godot;
using Nightwalk.Core.Config;
using Nightwalk.Core.Difficulty;
using Nightwalk.Core.Events;
using Nightwalk.Core.Scoring;
using Nightwalk.Godot.Audio;
using Nightwalk.Godot.Beacons;
using Nightwalk.Godot.Ziplines;

namespace Nightwalk.Godot.Player.Tools;

/// <summary>
/// Context passed to tools providing access to player state and world systems.
/// </summary>
public sealed class ToolContext
{
    /// <summary>
    /// The player controller.
    /// </summary>
    public required PlayerEntity Player { get; init; }

    /// <summary>
    /// The player's camera.
    /// </summary>
    public required Camera3D Camera { get; init; }

    /// <summary>
    /// The player's head node (camera parent).
    /// </summary>
    public required Node3D Head { get; init; }

    /// <summary>
    /// Physics space state for raycasting.
    /// </summary>
    public required PhysicsDirectSpaceState3D SpaceState { get; init; }

    /// <summary>
    /// Event bus for publishing game events.
    /// </summary>
    public required IEventBus EventBus { get; init; }

    /// <summary>
    /// Game data service for accessing all configuration.
    /// Tools should read from Data.Tools.X for hot-reloadable config values.
    /// </summary>
    public required GameDataService Data { get; init; }

    /// <summary>
    /// Registry for tracking zipline cables.
    /// </summary>
    public required ZiplineRegistry ZiplineRegistry { get; init; }

    /// <summary>
    /// Score service for deducting placement costs. May be null during initialization.
    /// </summary>
    public IScoreService? ScoreService { get; set; }

    /// <summary>
    /// Beacon sync manager for prism tool. May be null if beacons not configured.
    /// </summary>
    public BeaconSyncManager? BeaconSyncManager { get; set; }

    /// <summary>
    /// Difficulty service for scaling values. May be null if not configured.
    /// </summary>
    public IDifficultyService? DifficultyService { get; set; }

    /// <summary>
    /// SFX manager for playing generated sounds. May be null during initialization.
    /// </summary>
    public SfxManager? SfxManager { get; set; }

    /// <summary>
    /// Synth loop manager for continuous procedural audio. May be null during initialization.
    /// </summary>
    public SynthLoopManager? SynthLoopManager { get; set; }
}
