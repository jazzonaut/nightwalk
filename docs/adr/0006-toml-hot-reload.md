# ADR-0006: TOML configuration with file-watcher hot reload

## Status

Accepted (retrofitted 2026-05)

## Context

Nightwalk has substantial tunable surface: movement physics, scoring and combo curves, beacon and prism mechanics, jetpack and zipline characteristics, world-generation density, beacon-spawn rates, audio levels, weather and post-processing, resonance thresholds, and more. Tuning these in code requires a recompile per iteration, which is slow and breaks designer-style flow. The tuning data also wants to be human-editable text — viewable in source control, diff-able in PRs, comment-able.

Two questions: **format** (how the data is stored) and **lifecycle** (how it gets into the running game and whether changes can take effect without a restart).

### Alternatives considered (format)

- **TOML** — human-friendly, comment-friendly, section-based, well-served by .NET parsers (`Tomlyn`). Pros: explicit about types; no significant-whitespace surprises; clean for nested tables and arrays.
- **JSON** — ubiquitous, but no comments (without lossy hacks), trailing-comma sensitive, ugly for tuning numbers.
- **YAML** — comments fine, but significant whitespace causes silent breakage when copy-pasting; no first-class .NET serializer story.
- **Godot `Resource` (`.tres`) files** — editor-native, but binary-or-text limbo, weak diffing, and tightly couples config to the engine — incompatible with the `Core` assembly ([ADR-0002](0002-assembly-split.md)).

### Alternatives considered (reload)

- **Restart-required** — simplest, but every tweak costs a cold start.
- **Manual reload command** — a key binding that re-parses configs on demand. Better, but interrupts flow.
- **File-watcher hot reload** — the OS notifies on file write; the game re-parses on the next frame. Designer-friendly; tweak-and-see.

## Decision

Use **TOML** for all game configuration, parsed via `Tomlyn`. Configs live in `data/*.toml` (system) with a user-overlay directory for player preferences. Hot reload via `FileSystemWatcher` is enabled in DEBUG builds.

### Config files

Nine TOML files in `data/`:

| File | Owner |
|---|---|
| `core.toml` | `CoreConfig` — graphics, debug, weather, sky effects |
| `physics.toml` | `PhysicsConfig` — movement, jetpack, jump, flight |
| `gameplay.toml` | `GameplayConfig` — scoring, combo, fall damage, view modes, screensaver, energy |
| `worldgen.toml` | `WorldgenConfig` — chunks, downtown, clusters, building/road generation |
| `audio.toml` | `AudioConfig` — music, SFX, synth loops |
| `tools.toml` | `ToolsConfig` — zipline, spotlight, etc. |
| `beacons.toml` | `BeaconsConfig` — beacon, prism, beam, rewards, interaction |
| `aliveness.toml` | `AlivenessConfig` — car trails, window silhouettes |
| `resonance.toml` | `ResonanceConfig` — alert, exposure, repel, mode defaults |

`GameDataService` exposes each as a typed property (`Core`, `Physics`, `Gameplay`, ...). Loading is via `IConfigProvider` (currently `MergingConfigProvider` wrapping `TomlConfigProvider`).

### User preferences overlay

`MergingConfigProvider` combines two providers: a system provider (the `data/*.toml` files) and a user provider (a per-user directory containing optional TOML overrides). `UserPreferencesService` writes user changes (e.g., from the settings menu) into the user directory. The merge happens at load time; user keys override system keys.

### Hot reload

In DEBUG builds, `GameDataService.StartWatching(systemPath, userPath)` registers two `FileSystemWatcher`s:

1. **Detection**: OS file-change events queue the changed config name into `_changedConfigs` and set `_pendingReload = true`. Events within 300ms of the last reload are coalesced — the debounce avoids triple-firing during a multi-write save.
2. **Drain**: `GameRoot._Process` calls `_data.ProcessPendingReload()` each frame. This runs on the main thread (not the watcher's thread), avoiding races with gameplay code.
3. **Atomic apply**: `ReloadAll()` parses *every* config file into temp variables. If any parse fails, no state changes (the previous config remains live). Only when all parses succeed are the typed properties swapped.
4. **Error handling**: `TryReloadAndNotify` catches parse exceptions, logs them, and keeps the previous config. The next file save can recover.
5. **Fan-out**: `OnConfigReloaded(IReadOnlySet<string> changedConfigs)` fires after a successful reload. `GameRoot` listens and re-applies derived state across ~20 subsystems (player movement, tools, audio, HUD, post-processing, sky, beacon manager, screensaver, building highlights, etc.). If `worldgen` changed, the world is fully rebuilt.

The user-directory watcher is suppressed (`_suppressUserWatcher`) while the settings menu is mid-write, preventing a double reload.

### Verified

- `data/` contains 9 `.toml` files matching the typed properties on `GameDataService`.
- `Nightwalk.Core.csproj` references `Tomlyn` 0.19.0.
- `GameDataService.DebounceMs = 300` (`GameDataService.cs:23`).
- `ReloadAll()` loads to temp variables then commits — atomic (`GameDataService.cs:142-165`).
- `StartWatching` is gated by `#if DEBUG` in `GameRoot._Ready()` (`GameRoot.cs:113-120`).
- `OnConfigReloaded` fan-out in `GameRoot.OnConfigReloaded` reconfigures ~20 systems and reloads the world for `worldgen` changes (`GameRoot.cs:319-391`).

## Consequences

**Positive:**
- Tweak-and-see iteration: edit `data/physics.toml`, save, see the change in the running game within one frame after the 300ms debounce. No recompile, no restart.
- Atomic reload means a malformed TOML doesn't half-apply — you keep the working config until you fix the file. The game does not crash on a typo.
- The `MergingConfigProvider` layering means user preferences and system tuning are cleanly separated and independently editable. A settings menu writes user TOML; tuning PRs touch only system TOML.
- TOML is comment-friendly — tuning values can explain themselves.
- `Tomlyn` handles deserialization into POCO config classes (`CoreConfig`, `PhysicsConfig`, etc.) so the rest of the code uses typed access.

**Negative:**
- The `OnConfigReloaded` fan-out is a single ~70-line method in `GameRoot` that must touch every reconfigurable system. Adding a new tunable subsystem means editing this method — easy to forget, with no compile-time guarantee.
- Hot reload is DEBUG-only; release builds get no benefit. Designer builds need to be debug builds (or this constraint needs revisiting).
- `FileSystemWatcher` is notoriously chatty on Windows (sometimes multi-fires per save). The 300ms debounce is a band-aid that works in practice but is not principled.
- A worldgen-config change tears down and rebuilds the entire world (`ReloadWorld` in `GameRoot`), which is expensive and momentarily disruptive. Necessary for correctness but felt by the player/designer.
- Parse failures keep the previous config but only log the error. Without a HUD-level notification, designers may not realise their last edit failed.

**Related:**
- [ADR-0002](0002-assembly-split.md) — `GameDataService`, `IConfigProvider`, `TomlConfigProvider`, `MergingConfigProvider` all live in `Core`; only the watcher I/O touches the OS via `FileSystemWatcher` (still Godot-free, just `System.IO`).
- [ADR-0003](0003-manual-di-no-autoloads.md) — `GameDataService` constructed in `GameRoot._Ready()`; `OnConfigReloaded` subscription wired manually.
