# ADR-0002: Two-assembly split — `Nightwalk.Core` vs `Nightwalk.Godot`

## Status

Accepted (retrofitted 2026-05)

## Context

Given [ADR-0001](0001-csharp-over-gdscript.md) (C# as sole language), the next question is how to organize the C# code itself. A Godot C# project will, by default, place everything in a single assembly built with `Godot.NET.Sdk`. That assembly transitively references `GodotSharp.dll`, which means every type — even pure data structures and algorithms — links to the Godot runtime and can't be loaded without it.

Nightwalk's logic is heavily mixed: some of it is genuinely engine-bound (rendering, input, scene-tree wiring) and some of it is purely data/algorithmic (event bus, scoring, difficulty multipliers, TOML config, deterministic RNG, world generation math, game-mode state machines). Co-locating the two means you can't unit-test the second category without spinning up a headless Godot environment, and there's no compile-time barrier preventing engine types from leaking into algorithmic code.

### Alternatives considered

- **Single assembly** (the default) — simplest, no project plumbing, but no enforced separation. Anyone can write `using Godot;` from anywhere. Testing the pure logic requires the engine.
- **Two assemblies: `Core` (pure managed) + `Godot` (engine-bound, references `Core`)** — enforces the "no Godot in pure logic" rule at the compiler level. `Core` can be loaded by xUnit/NUnit/console runners. `Godot` is the only assembly that knows about scenes, nodes, signals, etc.
- **Three+ assemblies (e.g., per-system splits like `Core`, `Worldgen`, `Rendering`)** — more rigorous, but for a project of this size the overhead (csproj plumbing, cross-references, NuGet duplication risk) outweighs the marginal isolation benefit.

## Decision

Two assemblies:

- **`Nightwalk.Core`** — `Microsoft.NET.Sdk`, `net8.0`, no Godot SDK reference. Holds events, commands, config, scoring, difficulty, game mode, game state, deterministic RNG, world data structures, beacon and collectible bookkeeping, resonance mechanics (Alert/Exposure/Warden/Seeker controllers).
- **`Nightwalk.Godot`** — `Godot.NET.Sdk/4.6.3`, `net8.0`, references `Nightwalk.Core`. Holds `GameRoot`, scene nodes, renderers, audio, input, tools, HUD, and any code that needs `Node`, `Vector3`, `Camera3D`, etc.

The `Core` assembly is required to remain free of Godot references.

Verified:
- `src/Nightwalk.Core/Nightwalk.Core.csproj` uses `<Project Sdk="Microsoft.NET.Sdk">` — no Godot SDK.
- `src/Nightwalk.Core/**/*.cs` contains **zero** `using Godot` directives and zero references to `Godot.*` types (grep returns no matches across 148 files).
- `Nightwalk.csproj` (the Godot assembly) declares `<ProjectReference Include="src\Nightwalk.Core\Nightwalk.Core.csproj" />`.
- File counts at time of writing: 148 in Core, 117 in Godot.

## Consequences

**Positive:**
- The compiler enforces the boundary. You cannot accidentally use `Godot.Vector3` from `Core` — the type doesn't exist there.
- `Core` is loadable in a standard .NET process, making unit tests possible (though see ADR-0001 — no tests yet exist).
- Refactoring is safer: changing a `Core` type forces you to touch every Godot-side consumer through the project reference, with full IDE rename support.
- New contributors get an immediate signal about where things belong — file path encodes intent.

**Negative:**
- Cross-cutting types (e.g., a 3D position) need a Godot-free representation in `Core` (`float X, Y, Z` tuples or custom structs) and conversion at the boundary. Some duplication.
- The Godot side can't `using` Core types implicitly — explicit conversions add boilerplate.
- Two `csproj` files to keep in sync.
- The "no Godot in Core" rule is enforced by tooling, but adding a NuGet dependency or accidentally pulling in a Godot-bound transitive package would break it silently. Periodic verification (or a build-time guard) is required to preserve the invariant.

**Related:**
- This split is the substrate for ADRs 0004 (EventBus), 0006 (TOML configs), and 0007 (state machine) — all live in `Core`.
- [ADR-0003](0003-manual-di-no-autoloads.md) (manual DI) is the mechanism by which `Core` services are instantiated and handed to `Godot`-side consumers.
