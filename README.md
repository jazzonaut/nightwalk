# Nightwalk

A first-person cyberpunk exploration game set in an endless procedurally generated night city. Single-player. Built in [Godot 4.6](https://godotengine.org/) with C# (.NET 10).

![Nightwalk screenshot](docs/media/screenshot01.jpg)

[More screenshots →](docs/screenshots.md)

> Hobby project, very much in progress. Expect rough edges and missing content. Bug reports welcome via [Issues](https://github.com/jazzonaut/nightwalk/issues).

## What you do

The city is endless, it's always night, and it's usually raining. You wander - through alleys at street level, across rooftops, however you like. Movement is most of the game: walking, running, jumping, holding space to hover for a bit, popping the jetpack to fly, or stringing a zipline between two ledges to ride up/down. Above you, aurora and the occasional lightning flash over distant skylines. Climb high and the streets below come to life with car trails.

Honestly, right now it's essentially a walking sim - most of the fun is just being in the city, looking up. There are a couple of optional things to do if you want a bit of structure:

- Tall rooftops have **beacons** that fire a colored beam of light. Some are sources, others are targets at the same frequency. You carry **prisms** and drop them along the way to bend a source beam into a matching target.
- Glowing polyhedrons are scattered around. Picking them up raises your score and grows a combo multiplier.

Four **difficulty levels** - `Observer`, `Explorer`, `Wanderer`, `Architect` - change costs, rewards, damage, and limits. If you just want to sightsee, pick **Observer**: no costs, no damage, no score - pure wandering.

## Play it

Pre-built binaries (Windows / Linux / macOS) will appear on the [Releases page](https://github.com/jazzonaut/nightwalk/releases). Until then, run from source - see [Build & run](#build--run) below.

## Controls

Keyboard + mouse only for now. No in-game rebinding UI yet.

| Action | Key |
| --- | --- |
| Look | Mouse |
| Move | `W` `A` `S` `D` |
| Run | `Shift` (hold) |
| Jump / jetpack / hover | `Space` - tap to jump, in the air, hold to jetpack up while you have energy, then hover with reduced gravity once it runs out |
| Free-flight mode | `Tab` (toggle) - separate from the jetpack, lets you fly around freely |
| Cycle tool | Mouse wheel (zooms instead while in birds-eye) |
| Birds-eye view | `Q` (toggle) |
| Interact (beacons, pickups, attach to zipline) | `E` |
| Screensaver / cinematic | `N` (toggle) |
| Pause | `Esc` |

## Tools

You carry a small kit and cycle through it with the mouse wheel. Left-click uses the current tool (place, fire). `E` is for interacting with things in the world - activating beacons, picking up polyhedrons, attaching to a zipline.

- **Zipline** - drop two anchors and a cable strings between them. Ride it by attaching, hold `Shift` to go faster, jump off to detach. Costs a little score to place and there's a cap on how many you can have out at once, so you can't completely web up the skyline.
- **Prism** - drop a prism and rotate it to bend a beacon's beam. The puzzle part - used to route a source beacon's beam into a matching target on a faraway rooftop.
- **Spotlight** - a held flashlight with a focused white beam. Mostly for poking around in alleys and corners where the city's own lighting doesn't reach.

## Graphics tuning

Defaults assume a mid-range desktop GPU (volumetric fog, SSR, FSR 2.2 upscaling at 0.85 render scale). If it runs poorly on your machine, open **Settings** in-game (`Esc` → Settings) and try the knobs below in roughly this order, the earlier ones tend to help more:

1. Lower **render scale** to 0.65 or 0.5. FSR 2.2 handles the upscale.
2. Turn **MSAA** off, leave FXAA on.
3. Disable **SSR**, **SSAO**, **SSIL**.
4. Reduce **rain** and **ground mist** particle counts, and **distant silhouette** building count.
5. Disable **volumetric fog**. This is the heaviest single effect, but it does a lot of the night-city atmosphere, so try the others first.

## Visual presets

Nine bundled looks, cycled with number keys `1`–`9` in-game. Purely a visual choice - they don't change performance the way the tuning knobs above do.

| Key | Preset | Notes |
| --- | --- | --- |
| `1` | Default | The shipped look, tuned in `data/core.toml`. |
| `2` | Clean | Minimal grading, less fog. Easier to see at distance. |
| `3` | Blade Runner | Blue shadows, orange highlights, heavier fog. |
| `4` | Nightfall | Green tint, denser fog, heavier rain. |
| `5` | Burning Sky | Warm sunset tones over the night city. |
| `6` | Synthwave | Purple/orange, high saturation. |
| `7` | Cinematic | Warm, desaturated, heavy vignette. |
| `8` | Downpour | Stormy: low contrast, heavy rain and fog. |
| `9` | Noir | Near-monochrome, high contrast. |

## Status

Solo hobby project. No commercial plans, no roadmap. If you find a bug, [open an issue](https://github.com/jazzonaut/nightwalk/issues/new/choose). Contributions welcome - see [CONTRIBUTING.md](CONTRIBUTING.md).

---

## For developers

### Build & run

Requirements:

- Godot 4.6 with .NET (mono) build.
- .NET 10 SDK.

Open `project.godot` in Godot. Build the C# project (Godot will invoke `dotnet build` on `Nightwalk.csproj`). Run the main scene (`scenes/Main.tscn`).

Configuration lives in `data/*.toml` (nine files). Edit them and re-run. In **DEBUG builds**, file changes hot-reload at runtime - see [ADR-0006](docs/adr/0006-toml-hot-reload.md).

### Project layout

```
.
├── assets/             Runtime assets - music, textures, models
├── data/               TOML configuration (9 files; hot-reloadable in DEBUG)
├── docs/               ADRs + per-system reference + agent guidance
├── scenes/             Godot scenes - Main.tscn, ShaderTest.tscn
├── shaders/            GLSL shaders
└── src/
    ├── Nightwalk.Core/   Pure C# - no Godot dependencies. Testable.
    └── Nightwalk.Godot/  Engine-bound C# - scenes, rendering, input, audio, tools.
```

### Documentation map

- **[CONTEXT.md](CONTEXT.md)** - domain map + glossary. Read this first to learn the vocabulary (Beacon, Prism, Frequency, Sync, Alert, Exposure, Warden, Seeker, Chunk, Cluster, Game Mode, …).
- **[docs/conventions/](docs/conventions/)** - how new code should look. Read before adding or refactoring:
  - [naming.md](docs/conventions/naming.md) - file/folder layout, namespaces, type suffixes (`*Event`, `*Id`, `*Tracker`, `*Setup`/`*Result`, …), member casing, capability-flag naming.
  - [patterns.md](docs/conventions/patterns.md) - recurring code shapes: assembly split, manual DI in `GameRoot`, `*Setup`/`*Result` helpers, `EventBus` lifecycle, state-machine capability flags, config pipeline, session reset, the `null!` field pattern.
- **[docs/adr/](docs/adr/)** - architecture decision records. The "why" behind the codebase shape. Start with [docs/adr/index.md](docs/adr/index.md) for a grouped overview.
- **[docs/systems/](docs/systems/)** - per-system reference: tuning tables, scene/node footprints, integration points, acceptance checklists, code-verified 2026-05.
- **[docs/agents/](docs/agents/)** - instructions for AI/agent collaborators (issue tracker, triage labels, domain doc conventions).

Copyright (C) 2026 jazzonaut.
