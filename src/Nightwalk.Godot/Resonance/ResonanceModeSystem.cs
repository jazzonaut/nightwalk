using Godot;
using Nightwalk.Core.Collectibles.Events;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.GameMode;
using Nightwalk.Core.Resonance;
using Nightwalk.Core.Resonance.Events;
using Nightwalk.Core.Resonance.Seeker;
using Nightwalk.Core.Resonance.Warden;

namespace Nightwalk.Godot.Resonance;

/// <summary>
/// Manages Godot-side entities for Resonance mode (Warden, Seekers, etc.).
/// Bridges between the Core ResonanceMode and Godot visual entities.
/// </summary>
public sealed partial class ResonanceModeSystem : Node3D
{
    private IEventBus? _eventBus;
    private GameDataService? _data;
    private ResonanceMode? _resonanceMode;

    // Warden components
    private WardenController? _wardenController;
    private WardenCore? _wardenCore;
    private WardenNode? _wardenNode;

    // Seeker components
    private SeekerController? _seekerController;
    private SeekerPool? _seekerPool;

    // Player tracking
    private Node3D? _playerNode;

    // Initialization guard
    private bool _initialized;

    /// <summary>
    /// Initializes the system with required dependencies.
    /// </summary>
    public void Initialize(
        IEventBus eventBus,
        GameDataService data,
        GameModeManager modeManager,
        Node3D playerNode)
    {
        // Guard against double initialization
        if (_initialized) return;
        _initialized = true;

        _eventBus = eventBus;
        _data = data;
        _playerNode = playerNode;

        // Get ResonanceMode from manager
        _resonanceMode = modeManager.GetMode("resonance") as ResonanceMode;

        if (_resonanceMode == null)
        {
            Log.Warn(LogCategory.Sim, "ResonanceMode not found in GameModeManager");
            return;
        }

        // Subscribe to ResonanceMode events
        _resonanceMode.OnWardenSpawnRequested += OnWardenSpawnRequested;
        _resonanceMode.OnWardenDespawnRequested += OnWardenDespawnRequested;
        _resonanceMode.OnWardenVulnerable += OnWardenVulnerable;

        // Create Warden controller
        _wardenController = new WardenController(_eventBus, _data.Resonance.Warden);

        // Create Warden core (for beam targeting)
        _wardenCore = new WardenCore(_data.Resonance.Repel);
        _wardenCore.OnRepelled += OnWardenCoreRepelled;

        // Create Warden node (starts invisible)
        _wardenNode = new WardenNode { Name = "Warden" };
        _wardenNode.Initialize(_wardenController, _data.Resonance.Warden, _wardenCore);
        AddChild(_wardenNode);

        // Create Seeker controller
        _seekerController = new SeekerController(_eventBus, _data.Resonance);
        _seekerController.OnSeekerSpawned += OnSeekerSpawned;
        _seekerController.OnSeekerDespawned += OnSeekerDespawned;
        _seekerController.OnPlayerDetected += OnPlayerDetected;
        _seekerController.OnPlayerLost += OnPlayerLost;

        // Create Seeker pool
        _seekerPool = new SeekerPool { Name = "SeekerPool" };
        _seekerPool.Initialize(_data.Resonance.Seeker);
        AddChild(_seekerPool);

        // Subscribe to collectible events to emit noise pings
        _eventBus.Subscribe<CollectibleCollectedEvent>(OnCollectibleCollected);

        // Subscribe to decoy events for visual markers
        _eventBus.Subscribe<DecoyPingEvent>(OnDecoyPing);

        Log.Info(LogCategory.Sim, "ResonanceModeSystem initialized");
    }

    private void OnCollectibleCollected(CollectibleCollectedEvent e)
    {
        // Only react to collectibles when Resonance mode is active
        if (_resonanceMode?.IsActive != true) return;
        if (_eventBus == null) return;

        // Emit noise ping at collectible location for Seekers to investigate
        _eventBus.Publish(new NoisePingEvent(e.X, e.Y, e.Z));
    }

    private void OnDecoyPing(DecoyPingEvent e)
    {
        // DecoyTool now manages decoy markers directly.
        // This handler just logs for debugging - Seekers respond via SeekerController.
        if (_resonanceMode?.IsActive != true) return;

        Log.Debug(LogCategory.Sim, $"Decoy ping at ({e.X:F0}, {e.Y:F0}, {e.Z:F0})");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_resonanceMode == null || _playerNode == null) return;

        var playerPos = _playerNode.GlobalPosition;

        // Update Warden
        if (_wardenController != null)
        {
            _wardenController.UpdatePlayerPosition(playerPos.X, playerPos.Z);
            _wardenController.Update((float)delta, _resonanceMode.AlertLevel);
        }

        // Update Warden core
        _wardenCore?.Update((float)delta);

        // Update Seekers
        if (_seekerController != null && _wardenController != null && _data != null)
        {
            var wardenPos = _wardenController.Position;
            bool wardenPresent = _wardenController.State != WardenState.Absent;
            _seekerController.UpdateAlertTier(_resonanceMode.AlertLevel);

            // Update line-of-sight for all active seekers before the update
            // Use player center mass for LOS checks
            float playerCenterOffset = _data.Resonance.Player.Height * _data.Resonance.Player.CenterOffsetFraction;
            var playerCenter = new Vector3(playerPos.X, playerPos.Y + playerCenterOffset, playerPos.Z);
            _seekerPool?.UpdateLineOfSight(playerCenter);

            // Pass player center mass position, not feet
            _seekerController.Update(
                (float)delta,
                playerPos.X, playerPos.Y + playerCenterOffset, playerPos.Z,
                wardenPos.X, wardenPos.Y, wardenPos.Z,
                wardenPresent
            );

            // Update collision avoidance after movement (for next frame)
            _seekerPool?.UpdateCollisionAvoidance();
        }
    }

    private void OnWardenSpawnRequested()
    {
        if (_wardenController == null || _playerNode == null) return;

        var playerPos = _playerNode.GlobalPosition;
        _wardenController.RequestSpawn(playerPos.X, playerPos.Y, playerPos.Z);

        Log.Info(LogCategory.Sim, "Warden spawn requested");
    }

    private void OnWardenDespawnRequested()
    {
        _wardenController?.RequestDespawn();
        _seekerController?.RecallAll();
    }

    private void OnWardenVulnerable()
    {
        _wardenController?.SetVulnerable();
        _wardenCore?.SetVulnerable(true);
    }

    private void OnWardenCoreRepelled()
    {
        NotifyWardenRepelled();
    }

    private void OnSeekerSpawned(SeekerInstance instance)
    {
        _seekerPool?.Get(instance);
    }

    private void OnSeekerDespawned(SeekerInstance instance)
    {
        _seekerPool?.Return(instance);
    }

    private void OnPlayerDetected(SeekerInstance instance)
    {
        // Notify ResonanceMode that a seeker started detecting
        // Alert accumulation is handled in ResonanceMode.Tick() with proper delta
        _resonanceMode?.OnSeekerStartedDetecting();
    }

    private void OnPlayerLost()
    {
        _resonanceMode?.OnSeekerStoppedDetecting();
    }

    /// <summary>
    /// Notifies that the Warden was repelled.
    /// </summary>
    public void NotifyWardenRepelled()
    {
        float calmWindow = _data?.Resonance.Repel.CalmWindow ?? 20f;
        _wardenController?.OnRepelled(calmWindow);
        _wardenCore?.SetVulnerable(false);
        _seekerController?.RecallAll();
        _resonanceMode?.OnWardenRepelled();
    }

    /// <summary>
    /// Checks if a beam hit point is on the Warden core and processes contact.
    /// </summary>
    public bool ProcessBeamHit(Vector3 hitPoint, float delta)
    {
        if (_wardenNode == null || _wardenCore == null) return false;

        if (_wardenNode.IsPointInCore(hitPoint))
        {
            _wardenNode.OnBeamContact(delta);
            return true;
        }
        else
        {
            _wardenCore.OnBeamLost();
            return false;
        }
    }

    /// <summary>
    /// Resets the system state.
    /// </summary>
    public void Reset()
    {
        _wardenController?.Reset();
        _wardenCore?.Reset();
        _seekerController?.Reset();
        _seekerPool?.ReturnAll();
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public void Cleanup()
    {
        if (_resonanceMode != null)
        {
            _resonanceMode.OnWardenSpawnRequested -= OnWardenSpawnRequested;
            _resonanceMode.OnWardenDespawnRequested -= OnWardenDespawnRequested;
            _resonanceMode.OnWardenVulnerable -= OnWardenVulnerable;
        }

        if (_wardenCore != null)
        {
            _wardenCore.OnRepelled -= OnWardenCoreRepelled;
        }

        if (_seekerController != null)
        {
            _seekerController.OnSeekerSpawned -= OnSeekerSpawned;
            _seekerController.OnSeekerDespawned -= OnSeekerDespawned;
            _seekerController.OnPlayerDetected -= OnPlayerDetected;
            _seekerController.OnPlayerLost -= OnPlayerLost;
            _seekerController.Cleanup();
        }

        _eventBus?.Unsubscribe<CollectibleCollectedEvent>(OnCollectibleCollected);
        _eventBus?.Unsubscribe<DecoyPingEvent>(OnDecoyPing);

        _initialized = false;

        _seekerPool?.Cleanup();
        _wardenNode?.QueueFree();
    }

    public override void _ExitTree()
    {
        Cleanup();
    }
}
