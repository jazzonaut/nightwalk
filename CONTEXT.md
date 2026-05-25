# Nightwalk — Context

Shared vocabulary and domain map for Nightwalk. ADRs and per-system docs lean on the terms defined here; if a name is ambiguous, this file wins.

This is the *domain* context — what the game is, in player- and design-facing terms. Engineering decisions live in `docs/adr/`. System-level reference (tuning tables, signals index, etc.) lives in the README (transitional) and may move to `docs/systems/` later.

---

## Domain map

Nightwalk is a single-player first-person game set in an endless procedurally generated cyberpunk city. The player traverses rooftops by walking, running, jumping, a jetpack, and player-placed ziplines. The world streams in 64m chunks; a coarser 2000m **super-chunk** grid drives cluster-level placement (downtown, cluster influence).

Two interleaved loops drive play:

- **Exploration loop** — find collectibles on rooftops, climb tall buildings, watch the city "come alive" with car trails on adjacent streets.
- **Puzzle loop** — activate a beacon, route its colored signal beam through player-placed prisms to a same-frequency target beacon to complete a sync.

**Game modes** layer additional rules on top of the base loops. `Silence` is the default (exploration + puzzles only). `Resonance` adds tension: collecting raises an Alert meter, which spawns a Warden, which deploys Seekers that detect the player. Detection fills an Exposure meter that drains score. The player must manage exposure and repel the Warden via the beam puzzle.

**Difficulty** (Observer / Explorer / Wanderer / Architect) is orthogonal to game mode and scales costs, rewards, and limits across every system.

---

## Glossary

Terms are listed alphabetically. Bold marks the canonical noun used in code and ADRs.

| Term | Meaning |
|------|---------|
| **Alert** | Tension meter in Resonance mode. Rises with collectibles and detection; thresholds spawn/repel/expose the Warden. `AlertMeter`. |
| **Aliveness** | The city-comes-alive system: roads adjacent to climbed buildings activate car trails. `CityAlivenessService`. |
| **Anchor** | Endpoint of a zipline cable. Cables are placed two-anchors-at-a-time; players are held near anchors without sliding. |
| **Beacon** | Crystalline rooftop pyramid that emits a signal beam at one of N frequencies. Sources and targets are both beacons. `BeaconSyncManager`, `BeaconSpawner`. |
| **Bird's Eye** | Overhead camera mode (Q). One of the three `ViewMode` values: `Walk`, `Flight`, `BirdsEye`. |
| **Chunk** | 64m × 64m streaming unit of the world. `ChunkCoord`, `ChunkManager`. Configured in `data/worldgen.toml` under `[chunks]`. |
| **Cluster** | A super-chunk-level region used to bias generation (downtown influence, building density). `ClusterInfluenceProvider`, `SuperChunkCoord`. |
| **Collectible** | Floating polyhedron pickup on a rooftop or in an empty plot. `CollectibleTracker`, `CollectibleSpawner`. |
| **Combo** | Score multiplier (1.0x to 4.0x by default) that increases per scoring action and decays after an idle window. Owned by `ScoreService`. |
| **Decoy** | Player-deployable distractor tool used in Resonance to manipulate Seekers. `DecoyTool`. |
| **Difficulty** | One of `Observer`, `Explorer`, `Wanderer`, `Architect`. Drives multipliers on costs/rewards/damage. `DifficultyService`. |
| **Downtown** | The central high-density region (default 500m radius) where building heights are scaled up. Defined in `data/worldgen.toml`. |
| **Empty Plot** | A "park" plot (no building) with a fixed-point collectible. `CollectibleLocationType.EmptyPlot`. |
| **Event Bus** | In-process typed pub/sub for cross-system messaging. `IEventBus`, `EventBus`. Distinct from Godot signals. |
| **Exposure** | Resonance meter that fills while Seekers detect the player; high exposure drains score. `ExposureMeter`. |
| **Flight** | A `ViewMode` enabling free 3D movement at the cost of continuous score drain. |
| **Flyby** | A scripted cinematic camera path triggered after sync completion (and similar moments). `FlybyCamera`, `FlybyPath`. Not a `ViewMode` — it temporarily takes over the camera. |
| **Frequency** | Colour-coded channel that identifies a beacon. Same-frequency beacons can sync. `BeaconFrequency`. Count is config-driven. |
| **Game Mode** | First-class top-level mode: `Silence` (default) or `Resonance`. One active at a time; `GameModeManager` owns lifecycle. Orthogonal to difficulty. |
| **Game State** | Lifecycle state: `MainMenu` / `Playing` / `Paused`. Each state advertises capability flags (`ShowsCursor`, `ShowsHUD`, `AllowsGameTick`). `GameStateManager`. |
| **Hot Reload** | Live re-load of TOML config files via `FileSystemWatcher` with debounce, applied atomically on the main thread. |
| **Marker Beam** | Vertical beam visible on candidate target beacons during an active sync. |
| **Prism** | Hexagonal glass tube placed by the player that refracts the signal beam. `PrismNode`, `PrismTool`. |
| **Prism POV** | Camera mode entered while looking at a prism for fine-grained rotation. Owned by `PrismPOVController`. Not a `ViewMode` — overlays the active one. |
| **Resonance** | Game mode that adds Alert, Exposure, Warden, and Seekers on top of base play. `ResonanceMode`. |
| **Screensaver** | Autonomous low-altitude camera path triggered by `N` or idle timeout. `ScreensaverController`. Not a `ViewMode` — overlays the active one. |
| **Seeker** | Resonance NPC that investigates noise pings and reports detection to `ExposureMeter`. `SeekerController`, `SeekerMovement`. |
| **Signal Beam** | The routed beam emitted from an active source beacon through prisms toward a target. `SignalBeamRenderer`. |
| **Silence** | Default game mode — base exploration + puzzles, no Resonance mechanics. |
| **Source** / **Target** | A beacon's role during an active sync. The activated beacon is the source; matching-frequency beacons within range are candidate targets. |
| **Super-Chunk** | 2000m × 2000m coarse grid used for cluster placement. `SuperChunkCoord`. |
| **Sync** | A completed routing of a beacon signal through prisms to a same-frequency target. The sync awards points and triggers a flyby. |
| **System Registry** | Registry of long-lived `ISystem` instances tickable per phase. `SystemRegistry`, `SystemPhase`, `ITickable`. |
| **View Mode** | One of `Walk`, `Flight`, `BirdsEye` (see `ViewMode` enum). Distinct from camera overlays like Prism POV, Flyby, and Screensaver. |
| **Warden** | Resonance antagonist spawned when Alert crosses a threshold; vulnerable above a second threshold; can be repelled via the beam puzzle. `WardenController`, `WardenNode`. |
| **World Module** | Pluggable contributor to world generation. `IWorldModule`, `WorldBuilder`, `WorldContext`. |
| **Zipline** | Player-placed cable between two anchors. Riders are subject to gravity-slide + climb-efficiency physics. `ZiplineTool`, `ZiplineRider`. |

---

## Notes on this file

- This glossary was reconstructed from the codebase (not the README, which is partially stale).
- Terms not in this list are either not yet load-bearing in the domain or are pure implementation details (e.g., "progressive rendering", "stepped building", "weathering" — these belong in per-system docs, not the shared vocabulary).
- When a new domain concept gets introduced, add it here in the same row format and link from the relevant ADR.
