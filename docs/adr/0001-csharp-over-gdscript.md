# ADR-0001: C# over GDScript

## Status

Accepted (retrofitted 2026-05)

## Context

Nightwalk is a single-player Godot game with substantial non-trivial logic: procedural world generation with deterministic RNG, an in-process event bus, configuration hot-reload, scoring and combo machinery, a difficulty multiplier service, and a game-mode framework (Silence/Resonance). A significant fraction of the game's behavior is data-orientated and benefits from running independently of the engine (for unit-testability, but also for clean separation of "what the game does" from "how Godot draws it").

Godot 4.x supports three first-class scripting paths:

### Alternatives considered

- **GDScript** — Godot's native language. Built-in, fast iteration, no toolchain. Weakly typed (gradual typing exists but is incomplete), no first-class generics, limited access to external NuGet/managed libraries, harder to unit-test outside the editor.
- **C# (via Godot.NET.Sdk)** — Strongly typed, generics, `record`/`struct` value types, full .NET ecosystem (NuGet, Roslyn), can have pure non-Godot assemblies that compile and run on the standard .NET host (enables real unit tests).
- **C++ via GDExtension** — Maximum performance, but adds significant build complexity, slow iteration, and overkill for a single-player game whose hot paths are dominated by rendering (already C++ in the engine) and chunk generation (manageable in managed code).

## Decision

Use **C# (.NET 8)** as the sole scripting language. No `.gd` files. Engine integration via `Godot.NET.Sdk/4.6.3`. The codebase splits into two assemblies (see [ADR-0002](0002-assembly-split.md)), of which the `Nightwalk.Core` assembly is pure-managed and has zero Godot references.

Verified:
- Zero `.gd` files in the project (`**/*.gd` returns nothing).
- `Nightwalk.csproj` uses `Godot.NET.Sdk/4.6.3`, `TargetFramework=net8.0`.
- `Nightwalk.Core.csproj` uses plain `Microsoft.NET.Sdk` — no Godot SDK reference.
- `project.godot` declares `config/features=PackedStringArray("4.6", "C#", "Forward Plus")`.

## Consequences

**Positive:**
- The `Nightwalk.Core` assembly can be unit-tested under a normal .NET runner (no Godot editor needed). This is the foundational enabler for ADRs 0004 (EventBus), 0006 (TOML configs), and 0007 (state machine), all of which live in `Nightwalk.Core`.
- Strong typing across the event bus — every event is a distinct C# type.
- Access to NuGet packages — `Tomlyn` for TOML parsing is a concrete example.
- Easier refactoring (Roslyn-aware tooling).

**Negative:**
- Mono build required; cold builds are slower than GDScript hot-reload.
- Newcomers familiar with GDScript-first Godot tutorials face a learning gap.
- Some Godot examples and addons assume GDScript; integrating them requires reimplementation.
- The "testability" benefit is currently latent — there is no `tests/` directory at the time of this ADR. The decision *enables* tests but they have not been written.
- **Interop is the dominant runtime cost.** Every Godot call from C# crosses the managed↔native boundary. This consequence is named and given a discipline in [ADR-0014](0014-csharp-godot-interop-cost.md); concrete techniques apply in [ADR-0015](0015-meshbuilder-for-chunk-geometry.md) (chunk-geometry case). New contributors should read those before tuning hot-path code.
