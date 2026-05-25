# Configuration

Nine TOML files in `data/`, parsed via `Tomlyn`, with user-preferences overlay and DEBUG-build hot reload.

> **Engineering decision:** [ADR-0006 — TOML configs with FileSystemWatcher hot reload](../adr/0006-toml-hot-reload.md).

## Files

| File | Owner class | Covers |
|---|---|---|
| `data/core.toml` | `CoreConfig` | Graphics, debug, weather, sky effects |
| `data/physics.toml` | `PhysicsConfig` | Movement, jump, jetpack, flight |
| `data/gameplay.toml` | `GameplayConfig` | Energy, scoring, fall damage, view modes, screensaver, flyby, collectibles, score effects, buildings |
| `data/worldgen.toml` | `WorldgenConfig` | Chunks, downtown, clusters, buildings, roads, neon, materials |
| `data/audio.toml` | `AudioConfig` | Music, SFX, synth loops |
| `data/tools.toml` | `ToolsConfig` | Zipline, spotlight, decoy |
| `data/beacons.toml` | `BeaconsConfig` | Beacon, prism, beam, rewards, interaction |
| `data/aliveness.toml` | `AlivenessConfig` | Car trails, window silhouettes |
| `data/resonance.toml` | `ResonanceConfig` | Mode default, alert, exposure, warden, seeker, counterplay, repel |

> Correction from README: README §1.1 says "8 files" — actual count is 9. `resonance.toml` is the missed file. See [FINDINGS.md](../../FINDINGS.md).

## User overlay

`MergingConfigProvider` combines:

1. **System provider** (`TomlDataLoader` reading `res://data/*.toml`) — the canonical tuning.
2. **User provider** (`UserConfigLoader` reading `<user-data-dir>/*.toml`) — overrides written by the settings menu.

The merge happens at load time, key-by-key, with user values winning. Both directories are watched for changes (in DEBUG builds); writes from `UserPreferencesService.Save` suppress the user-side watcher to avoid double-reloads.

## Hot reload (DEBUG only)

In `GameRoot._Ready`, `#if DEBUG` starts `_data.StartWatching(...)` on both system and user directories. On a file change:

1. `_changedConfigs.Add(name)`, `_pendingReload = true`.
2. `GameRoot._Process` calls `_data.ProcessPendingReload()` each frame.
3. `ReloadAll()` parses *all* configs into temp variables; only if every parse succeeds are the typed properties swapped (atomic).
4. `OnConfigReloaded(IReadOnlySet<string> changed)` fires; `GameRoot.OnConfigReloaded` reconfigures ~20 subsystems.
5. If `"worldgen"` is in the changed set, `GameRoot.ReloadWorld()` tears down and rebuilds the entire world.

Parse failure → log, keep previous config, do not crash.

Debounce: 300ms (`GameDataService.DebounceMs`).

## Implementation

- `src/Nightwalk.Core/Config/IConfigProvider.cs`, `TomlConfigProvider.cs` — interface + base parser.
- `src/Nightwalk.Core/Config/MergingConfigProvider.cs` — system + user merge.
- `src/Nightwalk.Core/Config/TomlMerger.cs` — key-by-key merge logic.
- `src/Nightwalk.Core/Config/UserPreferencesService.cs` — settings-menu writes.
- `src/Nightwalk.Core/Config/GameDataService.cs` — central service. Owns the typed config properties, the watchers, the debounce, and the `OnConfigReloaded` event.
- `src/Nightwalk.Core/Config/Definitions/*Config.cs` — POCOs for each TOML section. `Tomlyn` deserialises into these.
- `src/Nightwalk.Godot/Data/TomlDataLoader.cs`, `UserConfigLoader.cs`, `ConfigConsts.cs` — Godot-side wrappers for `res://` and user-dir paths.

## Integration

The fan-out in `GameRoot.OnConfigReloaded` re-applies config to (in order):

`ConfigureLogging` → `PlayerEntity.Configure` → `PlayerToolManager.ReconfigureTools` → `MusicManager.Configure` → `SfxManager.Configure` → `AmbientManager.Configure` → `SynthLoopManager.Configure` → `RainSystem.Configure` → `GameHUD.Configure` → `PostProcessingSetup.Configure` → `EnvironmentSetup.ApplyWindowSettings / UpdateViewport / UpdateEnvironment / UpdateCameraAttributes` → `SkySystemSetup.Configure` → `PlayerEntity.UpdateContentScaleCompensation` → `BuildingHighlightRenderer.UpdateMaterials` → `CollectibleSpawner.UpdateMaterials` → `CarTrailRenderer.UpdateMaterials` → `BeaconSyncManager.Reconfigure` → `BeaconSpawner.Reconfigure` → `PrismPOVController.Configure` → `ScreensaverController.Configure` → `SettingsMenu.RefreshValues` → `ReloadWorld` (if worldgen) → `VisualPresetManager.ReapplyCurrentPreset`.

Adding a new tunable subsystem means appending one or more `Configure` calls here. There is no compile-time enforcement.

## Acceptance checklist

Verified against code 2026-05-25.

- [x] All nine TOML files load via `GameDataService.LoadAll` (`:127-135`).
- [x] User overlay applied via `MergingConfigProvider`.
- [x] Hot reload runs only in DEBUG builds (`#if DEBUG` in `GameRoot._Ready` and `_Process`).
- [x] 300ms debounce (`GameDataService.DebounceMs`).
- [x] Atomic reload — temp variables, all-or-nothing swap (`ReloadAll` lines 142-165).
- [x] Parse failure logs and keeps previous config (`TryReloadAndNotify`).
- [x] `OnConfigReloaded` fires with the set of changed config names.
- [x] User-watcher suppressed during settings-menu writes (`_suppressUserWatcher`).
- [x] Worldgen change triggers full world rebuild (`ReloadWorld`).

## Related

- [Player Movement](player-movement.md), [Audio](audio.md), [HUD](hud.md), [Worldgen](worldgen.md), [Beacon Sync](beacon-sync.md), every system with tuning.
- ADR-0006 (the engineering decision in full), ADR-0002 (assembly split — config is Core-side).
