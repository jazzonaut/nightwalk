# View Modes

Player camera modes. Three first-class `ViewMode` values plus three overlay controllers that temporarily take over the camera.

> **Engineering decisions:** none specific. The taxonomy (overlays vs enum values) is documented in [CONTEXT.md](../../CONTEXT.md). View modes are a thin state machine over the `ViewMode` enum.

## Taxonomy correction

The README's "View Modes" section conflates two things. The code treats them separately:

**`ViewMode` enum values** (mutually exclusive; one is always current):
- `Walk` — first-person on the ground.
- `Flight` — first-person 6-DOF free movement.
- `BirdsEye` — overhead orbit-look camera.

**Overlay controllers** (independent of `ViewMode`; one or none active at a time):
- **Prism POV** — `PrismPOVController` takes the camera into a placed prism. See [Prism Tool](prism-tool.md).
- **Flyby** — `FlybyCamera` plays a scripted path along the just-completed beam after a sync. See [Beacon Sync](beacon-sync.md).
- **Screensaver** — `ScreensaverController` autonomously flies the camera along streets after idle or `N` key. See below.

The overlays *suspend* the underlying view mode without changing it. When the overlay exits, you return to whichever `ViewMode` you were in.

## Inputs

| Action | Default key | Effect |
|---|---|---|
| `toggle_flight` | `Tab` | Cycle `Walk ↔ Flight` |
| `toggle_birds_eye` | `Q` | Toggle BirdsEye on/off (returns to previous) |
| `toggle_screensaver` | `N` | Toggle Screensaver overlay |
| (per-prism via E) | — | Enter Prism POV (see [Prism Tool](prism-tool.md)) |

## Flight (the enum value)

- 6-DOF free movement via `PlayerMovement.ProcessFlightMovement`.
- Base FOV `flight.base_fov = 75`; expands by `flight.max_fov_increase = 15` at full `SpeedFactor`.
- Sprint multiplier `flight.sprint_multiplier = 5.0` (so 75 m/s peak speed at default `flight.speed = 15`).
- Score drain `flight.score_drain_rate = 100/sec` while `HasMovementInput` (difficulty-scaled).

## Bird's Eye

Overhead camera that orbits high above the player. Wheel zooms; mouse re-pitches.

Tuning — `data/gameplay.toml [view_modes]`:

| Key | Default | Purpose |
|---|---|---|
| `birds_eye_height` | 120.0 | Default height above player |
| `birds_eye_min_height` / `max_height` | 40 / 400 | Zoom bounds |
| `birds_eye_zoom_step` | 10.0 | Wheel zoom step (m) |
| `birds_eye_zoom_speed` | 120.0 | Zoom transition speed |
| `birds_eye_pitch_degrees` | -80 | Default pitch (top-down-ish) |
| `birds_eye_pitch_min_degrees` / `max_degrees` | -88 / -20 | Pitch bounds |
| `birds_eye_look_sensitivity` | 0.7 | Mouse multiplier in BirdsEye |
| `birds_eye_transition_duration` | 0.35 | Smoothing into/out of BirdsEye |

## Flyby

Plays after `BeaconSyncManager.CompleteSync`. Sweeps along the beam path at speed, with FOV expansion and post-process boost.

Tuning — `data/gameplay.toml [flyby_camera]`:

| Key | Default | Purpose |
|---|---|---|
| `base_speed` | 30.0 | Path travel m/s |
| `min_speed_on_turns` | 18.0 | Slowdown on sharp curvature |
| `max_roll_degrees` | 25.0 | Bank angle on turns |
| `roll_smoothing` | 5.0 | Roll responsiveness |
| `look_ahead_t` | 0.02 | Lookahead on path (0-1) |
| `base_fov` / `max_fov_increase` | 75 / 12 | Speed-scaled FOV |
| `enter_transition_duration` / `exit_transition_duration` | 3.0 / 3.0 | Smoothing in/out |
| `vertical_offset` | 2.0 | Camera above beam path |
| `post_process_multiplier` | 1.3 | Effect boost |
| `curvature_threshold` | 0.1 | Curvature gate for slow + roll |
| `min_waypoints` | 2 | Minimum path points to trigger |

## Screensaver

Autonomous camera mode (N key or idle timeout). Flies at low altitude along procedurally-extended waypoints, following roads.

Tuning — `data/gameplay.toml [screensaver]`:

| Key | Default | Purpose |
|---|---|---|
| `waypoints_ahead` | 12 | Lookahead buffer size |
| `min_waypoint_distance` / `max_waypoint_distance` | 25 / 60 | Generation step bounds |
| `turn_chance` | 0.5 | At intersections |
| `low_altitude_min` / `max` | 4 / 40 | Flight band |
| `altitude_change_chance` | 0.3 | Per waypoint |
| `base_speed` | 18.0 | Travel speed |
| `min_speed_multiplier` | 0.6 | On tight turns |
| `speed_smoothing` | 3.0 | Speed lerp rate |
| `max_roll_degrees` | 5.0 | Subtle banking |
| `roll_smoothing` | 3.0 | Roll lerp rate |
| `look_ahead_t` | 0.05 | Lookahead (0-1) |
| `orientation_smoothing` | 4.0 | Orientation lerp rate |
| `curvature_threshold` | 0.1 | Turn-detection threshold |
| `transition_duration` | 5.0 | Enter/exit fade |

## Implementation

- `src/Nightwalk.Core/GameState/ViewMode.cs` — the 3-value enum.
- `src/Nightwalk.Core/GameState/Events/ViewModeChangedEvent.cs` — published on `ViewMode` changes.
- `src/Nightwalk.Godot/Player/PlayerViewModes.cs` — owns the `ViewMode` state and the BirdsEye transition; gates Screensaver activation through `IsScreensaverActive`.
- `src/Nightwalk.Godot/Camera/FlybyCamera.cs`, `FlybyPath.cs` — flyby overlay.
- `src/Nightwalk.Godot/Camera/ScreensaverController.cs`, `ScreensaverWaypointGenerator.cs` — screensaver overlay.
- `src/Nightwalk.Godot/Beacons/PrismPOVController.cs` — POV overlay.

## Integration

| With | How |
|---|---|
| **Movement** | `ViewMode.Flight` flips `PlayerMovement` to `ProcessFlightMovement`; `BirdsEye` locks movement entirely (`IsMovementLocked`) |
| **Game state** | View mode changes are only allowed in `PlayingState` |
| **HUD** | Subscribes to `ViewModeChangedEvent` to show mode indicators |
| **Audio** | Flight enter/exit triggers SFX |
| **Beacon Sync** | `Flyby` is triggered post-sync; the manager holds a `FlybyCamera` reference |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] `ViewMode` enum has exactly `Walk`, `Flight`, `BirdsEye`.
- [x] `Tab` cycles `Walk ↔ Flight`; `Q` toggles BirdsEye; `N` toggles Screensaver.
- [x] BirdsEye locks player movement (`IsMovementLocked`).
- [x] Flight applies score drain while `HasMovementInput`.
- [x] FOV expands with flight speed factor.
- [x] Flyby plays after sync via `BeaconSyncManager.SetFlybyCamera`.
- [x] Overlay controllers (POV / Flyby / Screensaver) suspend the underlying mode and restore on exit.
- [x] `ViewModeChangedEvent` published on enum changes.
- [x] During Screensaver, chunk loading follows the camera, not the player (see [Worldgen](worldgen.md) — `_screensaverController.CameraPosition` overrides the focus).

## Related

- [Player Movement](player-movement.md), [Beacon Sync](beacon-sync.md), [Prism Tool](prism-tool.md), [Worldgen](worldgen.md), [Game State](game-state.md).
- ADR-0007 (Game state) — orthogonal to view modes.
