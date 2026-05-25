# Nightwalk

A first-person cyberpunk rooftop-exploration game set in an endless procedurally generated night city. Single-player. Built in **Godot 4.6** with **C# (.NET 8)**.

The city stretches forever — rain-slicked rooftops, neon signage, aurora in the sky, lightning over distant skylines. You traverse with walking, running, jumping, a jetpack, and player-placed ziplines. Beacons on tall rooftops emit colored signal beams; you place prisms to route a source beacon's beam to a same-frequency target. Climbing buildings brings the streets below "alive" with animated car trails. Collecting glowing polyhedrons fills the score and grows a combo multiplier.

Two **game modes** layer over this base:

- **Silence** — pure exploration + puzzles. Default.
- **Resonance** — tension: collecting raises an Alert meter, which spawns a Warden, which deploys Seekers that scan for you. Detection drains score. Repel the Warden by holding the beacon beam on its exposed core.

Four **difficulty** levels (`Observer`, `Explorer`, `Wanderer`, `Architect`) scale costs, rewards, damage, and limits, orthogonal to game mode.

## Documentation map

- **[CONTEXT.md](CONTEXT.md)** — domain map + glossary. Read this first to learn the vocabulary (Beacon, Prism, Frequency, Sync, Alert, Exposure, Warden, Seeker, Chunk, Cluster, Game Mode, …).
- **[docs/conventions/](docs/conventions/)** — how new code should look. Read before adding or refactoring:
  - [naming.md](docs/conventions/naming.md) — file/folder layout, namespaces, type suffixes (`*Event`, `*Id`, `*Tracker`, `*Setup`/`*Result`, …), member casing, capability-flag naming.
  - [patterns.md](docs/conventions/patterns.md) — recurring code shapes: assembly split, manual DI in `GameRoot`, `*Setup`/`*Result` helpers, `EventBus` lifecycle, state-machine capability flags, config pipeline, session reset, the `null!` field pattern.
- **[docs/adr/](docs/adr/)** — architecture decision records. The "why" behind the codebase shape. Start with [docs/adr/index.md](docs/adr/index.md) for a grouped overview, or jump straight to:
  - [ADR-0001](docs/adr/0001-csharp-over-gdscript.md) — C# over GDScript
  - [ADR-0002](docs/adr/0002-assembly-split.md) — Two-assembly split (`Nightwalk.Core` vs `Nightwalk.Godot`)
  - [ADR-0003](docs/adr/0003-manual-di-no-autoloads.md) — Manual DI in `GameRoot`; no Godot autoloads
  - [ADR-0004](docs/adr/0004-event-bus.md) — Custom `EventBus` over Godot signals
  - [ADR-0006](docs/adr/0006-toml-hot-reload.md) — TOML configs with file-watcher hot reload
  - [ADR-0007](docs/adr/0007-state-machine-capability-flags.md) — Game state machine with capability flags
  - [ADR-0008](docs/adr/0008-game-mode-framework.md) — Pluggable game-mode framework
  - [ADR-0009](docs/adr/0009-deterministic-seeded-generation.md) — Deterministic seeded generation
  - [ADR-0010](docs/adr/0010-difficulty-multiplier-service.md) — Difficulty as multiplier service
  - [ADR-0011](docs/adr/0011-beacon-beam-routing.md) — Beacon beam routing algorithm
  - [ADR-0012](docs/adr/0012-zipline-physics.md) — Zipline physics
  - [ADR-0013](docs/adr/0013-progressive-chunk-rendering.md) — Progressive chunk rendering
  - [ADR-0014](docs/adr/0014-csharp-godot-interop-cost.md) — C# ↔ Godot interop cost model (primary perf discipline)
  - [ADR-0015](docs/adr/0015-meshbuilder-for-chunk-geometry.md) — `MeshBuilder` over `SurfaceTool` for chunk geometry
- **[docs/systems/](docs/systems/)** — per-system reference: tuning tables, scene/node footprints, integration points, acceptance checklists, code-verified 2026-05.
- **[docs/agents/](docs/agents/)** — instructions for AI/agent collaborators (issue tracker, triage labels, domain doc conventions).
- **[FINDINGS.md](FINDINGS.md)** — triage list of code/docs inconsistencies surfaced during the doc-migration audit.

## Build & run

Requirements:

- Godot 4.6 with .NET (mono) build.
- .NET 8 SDK.

Open `project.godot` in Godot. Build the C# project (Godot will invoke `dotnet build` on `Nightwalk.csproj`). Run the main scene (`scenes/Main.tscn`).

Configuration lives in `data/*.toml` (nine files). Edit them and re-run; in **DEBUG builds**, file changes hot-reload at runtime — see [ADR-0006](docs/adr/0006-toml-hot-reload.md).

## Project layout

```
.
├── assets/             Runtime assets — music, textures, models
├── data/               TOML configuration (9 files; hot-reloadable in DEBUG)
├── docs/               ADRs + per-system reference + agent guidance
├── scenes/             Godot scenes — Main.tscn, ShaderTest.tscn
├── shaders/            GLSL shaders
└── src/
    ├── Nightwalk.Core/   Pure C# — no Godot dependencies. Testable.
    └── Nightwalk.Godot/  Engine-bound C# — scenes, rendering, input, audio, tools.
```

## Status

This README and the documentation in `docs/` were rebuilt 2026-05 against the current codebase. Significant subsystems added since the previous README (notably **Resonance**, the **Cluster / super-chunk** generation layer, the **GameMode** framework) are now covered.

If you find anything stale, surface it in [FINDINGS.md](FINDINGS.md) rather than fixing it silently — the migration left a triage backlog that may include the case in question.
