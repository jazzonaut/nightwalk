# ADR-0009: Deterministic seeded generation (Xorshift32 + `StableHash`)

## Status

Accepted (retrofitted 2026-05)

## Context

Nightwalk is an endless procedurally generated city. Players see a different city per world seed, but within a seed:

- The same chunk must generate the same buildings, roads, and neon placements every time it's loaded — there's no persistence, so re-entering an area is a re-generation, and a player traveling back to where they were must see the same place.
- Generation runs partly on first chunk load and partly later (e.g., building facade details when a chunk re-enters view), and these must agree.
- Tests, if/when they exist, want reproducible inputs.
- Crash repro requires being able to load the exact city the user saw — a world seed plus chunk coords must be sufficient.

The naïve approach — `System.Random` somewhere — fails on two counts:

1. `System.Random` is *not* required to be the same across runtime versions or platforms. Microsoft has changed its algorithm.
2. `HashCode.Combine` (the ergonomic .NET hash combiner) is **randomized per process**, deliberately, to prevent hash-flooding. Using it for seeds means every game launch produces a different "deterministic" world.

### Alternatives considered

- **`System.Random` + `HashCode.Combine`** — easy but non-portable and process-randomised. Disqualifying.
- **`System.Random` + custom hash** — fixes the seeding randomization but `Random` itself is allowed to change. Still risky.
- **Mersenne Twister or similar named PRNG** — well-distributed, portable, but heavier than necessary for a 32-bit game seed; pulling in a NuGet for one component bloats `Core`.
- **Hand-rolled Xorshift32 + hand-rolled FNV-1a-based 32-bit hash combiner** — small, fast, deterministic across platforms and .NET versions, zero allocations, no dependencies. Quality is sufficient for cosmetic generation (not cryptographic, not statistical analysis). About 90 lines total.

## Decision

Two cooperating utilities in `Nightwalk.Core.Determinism`:

### `DeterministicRng` — Xorshift32 PRNG

- 32-bit state, Xorshift32 algorithm (`state ^= state << 13; state ^= state >> 17; state ^= state << 5`).
- Seed of `0` is replaced with `1` to avoid the degenerate zero state.
- Surface: `NextInt()`, `NextInt(max)`, `NextInt(min,max)`, `NextFloat()`, `NextFloat(min,max)`, `NextBool()`, `NextDouble()`, plus convenience helpers `Choose<T>(IReadOnlyList<T>)`, `Chance(probability)`, `Shuffle<T>(IList<T>)`.
- Factory: `DeterministicRng.FromCoordinates(baseSeed, x, z)` — combines via `StableHash` and constructs an RNG. The usage pattern across the codebase.

### `StableHash` — FNV-1a 32-bit hash combiner

- Constants: `FnvOffsetBasis = 2166136261u`, `FnvPrime = 16777619u`.
- Overloads: `Combine(a,b)`, `Combine(a,b,c)`, `Combine(a,b,c,d)`.
- One-shot conveniences: `ToFloat01(...)`, `ToInt(..., maxExclusive)` for cases where a single deterministic value is all you need (no need for an RNG instance).

### Usage pattern

The dominant pattern is **per-chunk seed derivation**:

```
chunkSeed = StableHash.Combine(worldSeed, chunkCoord.X, chunkCoord.Z)
var rng = new DeterministicRng(chunkSeed)
```

This is exactly what `ChunkData` does in its constructor (`ChunkData.cs:40`), and what callers like `BuildingGenerator.cs:62`, `NeonPlacementGenerator.cs:113`, and `CollectibleSpawner.cs:136` follow. Sub-features inside a chunk derive their own RNG either by combining further (`StableHash.Combine(chunkSeed, featureKey)`) or by chaining `DeterministicRng` instances. Each derivation is independent of frame timing, draw order, or process state.

`DeterministicRng` instances are constructed *locally* — they are not long-lived shared state. A generator takes a chunk seed, builds an RNG, generates its contribution, and discards the RNG. This makes each generation step independently reproducible.

### Verified

- `DeterministicRng` is used in 8+ generators (BuildingGenerator, NeonPlacementGenerator, BeaconGenerator, CollectibleSpawner, ClusterInfluenceProvider, DistantCitySilhouette, ManholeRenderer, BuildingRenderer, FacadeDetailGenerator, WardenRenderer).
- `StableHash.Combine` is used by `ChunkData`, `ClusterInfluenceProvider`, `CollectibleSpawner`, and `SuperChunkCoord.GetSeed`.
- `DeterministicRng.FromCoordinates` exists but the more common pattern is `new DeterministicRng(StableHash.Combine(...))` directly — small inconsistency, see "Consequences".
- Both types live in `Nightwalk.Core.Determinism`, no Godot dependencies.
- `StableHash` documents the FNV constants and reasons explicitly: "Avoids HashCode.Combine which is randomized per process."

## Consequences

**Positive:**
- World seed + chunk coordinates → identical chunk content across runs, processes, and platforms.
- Generators do not need to be ordered relative to one another. `BuildingGenerator` and `NeonPlacementGenerator` both derive their own RNG from the chunk seed; they don't share state, can't desync, and run in any order.
- Sub-features (e.g., the choice of polyhedron for a collectible) can derive their own seed by combining further values — independence preserves reproducibility.
- Zero allocations in the hash path (FNV is stateless), low allocation in the RNG path (one struct-sized PRNG per use).
- The `Core`-side location means generators in `Core` (most of them) don't need to know about Godot's `RandomNumberGenerator`.

**Negative:**
- Xorshift32 has 32-bit state and is not cryptographically random. Fine for cosmetic generation; would not be appropriate for any security purpose. The comment in `DeterministicRng.cs:6` is honest about this.
- `StableHash.Combine` only takes `int` inputs — string-keyed seeding requires the caller to hash the string to an int first.
- The two construction patterns (`new DeterministicRng(StableHash.Combine(...))` vs `DeterministicRng.FromCoordinates(...)`) coexist. Both produce identical results but the inconsistency is a minor style smell.
- `DeterministicRng.FromCoordinates` only takes `(seed, x, z)` — extending it to more dimensions requires either a new factory or falling back to the explicit `Combine` form.
- Chunk seeds are derived once in `ChunkData`'s constructor; if `Size` or any other field changes the derivation, *all* existing chunks change. This is correct (the chunk is logically a different chunk) but a player who saw a building somewhere is going to see something different after the change. There's no migration story.

**Related:**
- [ADR-0002](0002-assembly-split.md) — determinism lives in `Core`; the load-bearing part is exclusively `Core` types.

**Note:** `DistantCitySilhouette.cs:67` constructs `new DeterministicRng(42)` — a literal seed, not derived from anything. Probably intentional (the silhouette is stable across sessions regardless of world seed), but it bypasses the chunk-seed pattern. Worth noting; not necessarily wrong.
