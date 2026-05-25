# Audio

Three layers: music (sampled tracks crossfaded), SFX (procedurally synthesised at runtime), and ambient/synth loops (background atmosphere).

> **Engineering decisions:** none captured as ADRs, but the **SFX layer is fully procedural — no sample files**. `SfxSynth` generates `AudioStreamWav` instances at runtime per sound type (collectible, jump, jetpack, etc.). The `[[sfx.sounds]]` TOML entries are all commented out. Worth flagging as a non-obvious choice.

## Player-facing summary

Background music drifts between three ambient cyberpunk tracks. Sound effects (footsteps, jetpack, pickups, beacon activation, sync completion) are entirely synthesised — no audio files for SFX. Ambient layers add city hum and rain crossfade.

## Layers

### Music

Sample-based. Three tracks under `assets/audio/music/`:

| ID | Path |
|---|---|
| `neon_rooftop_drift_01` | `res://assets/audio/music/neon_rooftop_drift_01.mp3` |
| `neon_rooftop_drift_02` | `res://assets/audio/music/neon_rooftop_drift_02.mp3` |
| `neon_rooftop_drift_03` | `res://assets/audio/music/neon_rooftop_drift_03.mp3` |

All in `category = "ambient"`. `master_volume = 0.05` (very quiet), `crossfade_duration = 2.0s`. `loop_enabled = true`. Implementation: `MusicManager.cs` / `MusicPlayer.cs`.

### SFX (procedural)

Implementation: `src/Nightwalk.Godot/Audio/SfxSynth.cs` — static methods that build `AudioStreamWav` instances at 44.1kHz with envelope shaping, sub-bass, taps, noise textures, FM/AM modulation. Examples found in the source: `Collectible(rng)` — digital blip with sub-bass and "tap" timing. Per-sound logic; no sample files.

`SfxManager` (Godot side) plays the synthesised streams; `Play(SfxId)` is the entry point. `SfxId` is a `Core`-side enum listing sound categories.

The TOML entries for `[[sfx.sounds]]` are **all commented out** in `data/audio.toml`. The active config is just `[sfx]` master volume, `[sfx.player]` footstep interval + run pitch, and `[sfx.environment]` rain crossfade + city ambient volume.

### Ambient / synth loops

- `AmbientManager` — background city hum (`city_ambient_volume = 0.3` default).
- `SynthLoopManager` — continuous synth loops for ongoing effects (jetpack, flight, etc.).
- Rain volume crossfades at `rain_crossfade_duration = 1.5s` per intensity change.

## Tuning — `data/audio.toml`

Verified 2026-05-25.

### `[music]`

| Key | Default |
|---|---|
| `enabled` | true |
| `master_volume` | 0.05 |
| `crossfade_duration` | 2.0 |
| `loop_enabled` | true |
| `[[music.tracks]]` | 3 ambient tracks |

### `[sfx]` / `[sfx.player]` / `[sfx.environment]`

| Key | Default |
|---|---|
| `sfx.enabled` | true |
| `sfx.master_volume` | 1.0 |
| `sfx.player.footstep_interval` | 0.35 |
| `sfx.player.run_pitch_multiplier` | 1.15 |
| `sfx.environment.rain_crossfade_duration` | 1.5 |
| `sfx.environment.city_ambient_volume` | 0.3 |

The commented `[[sfx.sounds]]` template suggests a future path-based SFX system was considered. Not currently used.

## Implementation

- `src/Nightwalk.Core/Audio/SfxId.cs` — sound category enum.
- `src/Nightwalk.Core/Audio/AudioMath.cs` — gain/pitch helpers.
- `src/Nightwalk.Core/Audio/MusicTrackInfo.cs` — `Core`-side track metadata.
- `src/Nightwalk.Core/Audio/ISfxService.cs` — `Core`-facing interface.
- `src/Nightwalk.Godot/Audio/MusicManager.cs`, `MusicPlayer.cs` — music playback + crossfade.
- `src/Nightwalk.Godot/Audio/SfxSynth.cs` — procedural SFX generator (the load-bearing class).
- `src/Nightwalk.Godot/Audio/AmbientManager.cs` — city hum + rain.
- `src/Nightwalk.Godot/Audio/SynthLoopManager.cs` — continuous loops (jetpack, flight).
- `src/Nightwalk.Godot/Setup/AudioSystemSetup.cs` — wiring in `GameRoot`.
- `src/Nightwalk.Godot/Player/PlayerAudio.cs` — player-driven SFX (footsteps, jetpack transitions).
- `src/Nightwalk.Godot/Player/PlayerFallDamage.cs` — fall SFX triggers.

## Integration

| With | How |
|---|---|
| **Player Movement** | `PlayerAudio` reads grounded / flight / jetpack transitions; plays the appropriate synth |
| **Collectibles / Beacons / Tools** | `SfxManager.Play(SfxId.<...>)` on key events |
| **Configuration** | Hot reload re-applies `Music`, `Sfx`, `Ambient`, `SynthLoop` configs |
| **Session reset** | `SfxManager.StopAll()` + `SynthLoopManager.StopAll()` on reset |
| **UI** | Preset switch plays `SfxId.PresetSwitch` |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Three ambient music tracks load from `assets/audio/music/`.
- [x] Music crossfades over `crossfade_duration` seconds.
- [x] SFX is procedural (`SfxSynth`); no sample files referenced in code.
- [x] `[[sfx.sounds]]` TOML entries are all commented out.
- [x] Footstep interval and run pitch multiplier read from config.
- [x] City ambient + rain crossfade configurable.
- [x] Hot reload re-applies to music/SFX/ambient/synth managers.
- [x] Session reset stops all loops.

## Related

- [Player Movement](player-movement.md), [Configuration](configuration.md).
- ADR-0002 (Core/Godot split — `ISfxService` is Core; `SfxSynth` is Godot because `AudioStreamWav` is engine type).
