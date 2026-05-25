# ADR-0016: Skycraft ambient air-traffic system

## Status

Accepted (2026-05-25)

## Context

The night cyberpunk city has, until now, two layers of ambient life: the road-`Aliveness` shader (car-trail streaks on roofs-adjacent roads) and Resonance NPCs (Wardens, Seekers) that exist only in `Resonance` mode. Both leave the sky empty and `Silence` mode visually flat — no flying entities, no headlights moving against the skyline, no rooftop activity.

`Skycraft` are ambient sci-fi flying vehicles that traverse the city between waypoints, occasionally landing on rooftops and roads. They are **not** a gameplay system — they don't affect score, can't detect the player, and don't participate in Resonance. Their purpose is atmospheric: make the sky and rooftops feel inhabited.

Several architectural choices below would surprise a reader who pattern-matches against `Seeker` (the closest existing system) or against the chunk-tied conventions used by `Buildings`/`Collectibles`/`Beacons`. This ADR records the deliberate divergences and why.

The system also reserves design surface for a future "hitch a ride" mechanic — explicitly out of scope for this ADR but constrained against here so it stays cheap to add later.

## Decision

`Skycraft` is built as a `Core`-side pool-managed simulation with a `Godot`-side hybrid renderer. Three movement profiles (`Express` / `Commuter` / `Drifter`) each occupy a distinct altitude band and routing strategy. Total population is exposed to the player via a single `SkycraftDensity` setting (0–100) under the `Gameplay` settings tab.

### Five load-bearing decisions

#### 1. Pool-managed population, not chunk-tied

A single `SkycraftController` owns a fixed-size pool of `SkycraftInstance`s. Spawns happen at the active region's far edge; despawns happen past a slightly larger radius (asymmetric hysteresis prevents flicker at the boundary). The pool is sliced into **per-profile sub-budgets** — distribution ratios live in `data/skycraft.toml` (e.g. `Express=0.3 / Commuter=0.5 / Drifter=0.2`), the player-facing density slider scales the total.

This diverges from the rest of the codebase, where `Buildings`, `Collectibles`, and `Beacons` are owned by the chunk that contains them and live or die with that chunk.

**Why:** Skycraft are designed to traverse long distances. A `Commuter` heading 800m to a distant rooftop crosses 12+ chunks during one flight. A chunk-tied population model would evict it mid-flight when its origin chunk unloaded, even though it's perfectly visible. Pool ownership also caps interop cost predictably (key per [ADR-0014](0014-csharp-godot-interop-cost.md) — `MaxTime` spikes matter most, and a hard pool cap bounds the per-frame transform-write count regardless of how many chunks happen to be loaded).

**Alternatives considered:**

- **Chunk-tied** — mirrors `CollectibleSpawner`. Rejected for the traversal/eviction problem and for variable interop cost.
- **Persistent global simulation** — every Skycraft has a position tracked across the whole world even when far from the player. Massive overkill for an ambient system; player can't tell whether a 2km-away Skycraft is the "same" one as before.

#### 2. Hybrid near-tier `Node3D` / far-tier `MultiMesh` rendering with shadowless near-tier spotlights

Within a configurable threshold (default ~150m) each Skycraft is a full `Node3D` with a real `SpotLight3D` for the headlight cone and `OmniLight3D` instances for backlights. Beyond the threshold, it's an instance in a single shared `MultiMeshInstance3D` per profile — no faked headlight cone. A hysteresis band on the swap prevents flicker.

At far range the **trim emissive** on the body mesh (a thin accent seam + faint underbelly glow, see §Visual identity) carries the profile signal; the headlight cone payoff is purely a near-tier cinematic effect. Adding a separate fake-cone MultiMesh would double the per-frame far-tier transform writes for a marginal gain — a cone-pixel against the city haze reads no differently than an emissive seam at the same scale.

Near-tier spotlights have **shadows disabled**.

**Why (hybrid):** Naïve per-Skycraft `Node3D` for the full pool would push ~N transforms across the interop boundary every frame *plus* maintain N+ real lights — both costs scale linearly with pool size and would dominate frame time at higher density settings ([ADR-0014](0014-csharp-godot-interop-cost.md)). Pure `MultiMesh` for all distances would be cheapest but kills the "headlight pool sweeping across a building's side" payoff that sells the system visually. The hybrid pays the per-`Node3D` cost only where it visibly matters and falls back to a single batched draw call for the long tail.

**Why (no shadows on near-tier):** Project memory enshrines that `SeekerNode` spotlight shadows are gameplay-critical (Seekers must cast visible cone-shadows for stealth play). That guidance is **Seeker-specific**. Skycraft headlight shadows are *ambient* — they add no information, no gameplay read, and would torpedo frame time once multiple near-tier Skycraft are simultaneously visible. The shadow disable is recorded here explicitly because the natural temptation is to cargo-cult from `SeekerRenderer`.

**Alternatives considered:**

- **Single-path `Node3D`** — simplest but doesn't scale. Rejected on perf.
- **Single-path `MultiMesh`** — cheapest but loses the near-field "real light spill" effect that makes the system feel cinematic. Rejected on visual quality.
- **Alpha fade or impostor swap at the tier boundary** — emissive lights against a dark sky make alpha fades very visible; impostor switching adds complexity for no gain over distance-driven readability.

#### 3. Drifters route along the existing `RoadSegment` graph; collision-freedom is structural, not runtime-checked

The three profiles occupy disjoint altitude bands:

- **Express** — high cruise, max building height + ~50m clearance. Free flight between sky waypoints.
- **Commuter** — mid cruise, ~60–90m. Sky waypoints in this band; rooftop POIs trigger landing approach (sec. 4 below).
- **Drifter** — low band, ~5–15m above road surface. **Travels exclusively along road corridors**, using the existing `RoadSegment` graph (the same data structure `CarTrailRenderer` already consumes). Transitions between segments happen at intersections.

No profile performs per-frame collision raycasts or physics queries against buildings.

**Why:** Per-entity avoidance scales linearly with pool size in interop cost. Designing routes that *can't* collide is free at runtime. Express clears all buildings by construction; Commuter's POIs are above-band by construction (the landing arc only descends below band when over a rooftop the Skycraft is targeting); Drifters by definition only fly where buildings are absent, because roads are the gaps between buildings.

**Bonus:** Drifters become the airborne sibling of the existing road-aliveness layer — cars on the road surface, drifters slightly above, both following the same arterial pattern. Visual cohesion comes for free.

**Alternatives considered:**

- **Free flight + per-frame avoidance** (steering with obstacle detection) — natural-looking but linear in interop cost.
- **Drifters fly free between buildings off-road, with avoidance** — would require avoidance for the only profile that needs it. The Drifters-on-roads choice eliminates the need entirely.

#### 4. POI source: derive landing zones from existing geometry, module-generate sky waypoints

Three kinds of POI exist:

- **Rooftop landing pads** — a deterministic subset of buildings, marked via the existing chunk seed during world generation. Subset selection is rule-based (e.g. minimum rooftop area, no occupied rooftop) to keep landings looking intentional.
- **Road landing zones** — a deterministic subset of `RoadSegment`s, similarly marked.
- **Sky waypoints** — pure-air destinations at Express and Commuter altitudes. These do not exist in the world geometry; they are emitted per chunk by `SkycraftPoiRegistry.EmitSkyWaypoints` (called from `OnChunkLoaded`) using its own density rules in `data/skycraft.toml`.

**Why split:** Landing surfaces already exist in `ChunkData.Buildings` and `ChunkData.Roads`. Building parallel "landing-pad" generation would duplicate that. Sky waypoints, by contrast, have no existing geometry to derive from, and the desired density of sky traffic should be independently tunable from building density.

**Trade-off recorded:** Derived landing zones inherit the building/road distribution. Dense downtown = many landing options; sparse outskirts = few. This reads as "busy city, busy traffic" — usually what you want — but it does mean Skycraft density per area is coupled to city density, not uniform.

#### 5. Resonance: ignored

Skycraft do not couple to `GameModeManager`. Population, behavior, and visuals are identical in `Silence` and `Resonance`. No `IGameMode` subscription, no detection coupling, no "the city goes quiet when Wardens spawn" effect.

**Why:** The ambient/gameplay split is already a load-bearing principle — Seekers/Warden/Exposure are Resonance-only by design; Skycraft are ambient. Coupling them erodes that boundary. The "city goes quiet" idea also undermines the system's stated goal — make the city feel alive — at the exact moment (Resonance tension) when alive-feeling matters most.

A `ResonanceTint` config knob (cosmetic colour shift only, no behavioural change) may be added later as pure tuning. If it ever ships, it lives in TOML, not in code coupling between the two systems.

### Landing behaviour (state machine)

Per profile, on arrival at a POI there is a probabilistic chance of converting the arrival into a landing: `Express=0`, `Commuter≈0.15`, `Drifter≈0.4`. After a landing+takeoff cycle, the next POI arrival has immunity to prevent serial landings.

Landings are only attempted at *marked* landing POIs (rooftops and road segments flagged by `SkycraftPoiRegistry` on chunk load), not any roof or road. Approach is three-phase: horizontal deceleration toward the POI, curved arc descent to ~5m above target, final vertical settle. Headlight cone narrows and tilts down during descent.

Dwell is profile-tuned and randomised within a range (`Commuter` 4–10s, `Drifter` 10–30s). During dwell, engines dim, backlights pulse slowly, headlights are off or low. Takeoff lifts vertically to a clearance altitude before selecting the next POI.

### Spawn / despawn

Spawns happen at the *far* edge of the active region (default ~500m from the player) — at that range a Skycraft is sub-pixel against the existing field of city lights and atmospheric haze, so no fade tech is needed. Despawn radius is larger (default ~700m) for hysteresis. Bird's Eye view is the one case where this edge can be visible from above; acceptable cost.

### Target invalidation

When a Skycraft's target POI exists in a chunk that unloads mid-flight, the Skycraft **repaths** — picks a new POI from currently-loaded chunks — rather than despawning. Repath is gated by the future `HasRider` flag (sec. *Hitching* below): a Skycraft with a rider never repaths its target.

### Hitching: reserved surface (not built today)

The following constraints are honoured today specifically so a future "press E to board a landed Skycraft" feature is cheap to add:

- Every `SkycraftInstance` has a stable `Id` (Guid). Pooled instances reset all state on reuse — no stale identity carryover.
- Every Skycraft mesh defines a `RiderAnchor` transform marker at a sensible "stand on the hull" offset. Today nothing reads it; tomorrow the player camera attaches to it.
- The state machine publishes `SkycraftLandedEvent`, `SkycraftTakingOffEvent`, and `SkycraftDespawningEvent` via the `EventBus`. Hitching, telemetry, future scoring, etc. consume these.
- `SkycraftInstance.HasRider` is a single boolean. Today no code writes it. Landing/takeoff/despawn logic *reads* it and refuses to orphan a rider (despawn guard near the player; repath disabled).
- No control surrender, camera attach, dismount mechanics, or UI exist for hitching today. Adding them later is purely additive.

### Visual identity (architectural commitments only)

Specific silhouettes and exact accent colours live in `data/skycraft.toml` and `docs/systems/skycraft.md`. The architectural rules recorded here:

- **Three meshes total — one per profile.** No mesh variants within a profile. Three is the asset budget for the entire system. Profiles are distinguished by *shape family* (Express = dart-like, Commuter = ovoid-like, Drifter = disc-like) so they read at a glance even far enough away to be a silhouette.
- **All within-profile variation is material-only.** Per-instance hue jitter on the accent emissive (~±15° HSV), per-instance scale jitter (~±5%). No body colour variants, no mesh swaps. Cost stays in the per-instance MultiMesh transform/colour buffer at the far tier and the per-instance material at the near tier; geometry is shared.
- **Profile accent emissive is a redundant ID signal.** Each profile is identified by *both* its silhouette and a profile-specific accent colour (a single thin emissive seam/strip on the body, plus a faint underbelly glow in the same hue). At long range silhouette dominates; at very long range or against busy skylines the colour resolves first. Cyan-family for high altitude, warm for mid, magenta-family for low — matched to the altitude band's mood.
- **Headlight and backlight colours are unified across profiles.** Cool white-blue cone forward, warm red/amber pulse aft. Fits the consistent "this is the same technology on every Skycraft" reading; the *profile* signal is the accent colour, not the running lights.
- **Animated vehicle details are profile-tied to landing.** Commuter landing skids extend during approach and retract on takeoff; Drifter canopy is static. Animations are mesh-blendshape or armature work, not separate meshes — preserves the three-mesh rule.

These rules exist because the renderer tier-swap (sec. 2) only works cleanly if the far tier (`MultiMesh`) can express all per-instance variation through its packed transform + colour buffer. New meshes or per-instance mesh-level differences would force every Skycraft into the near-tier path and torpedo the perf budget.

### Settings exposure

A single property `SkycraftDensity: int` lives on `SkycraftConfig` with:

```csharp
[UserConfigurable(SettingsCategory.Gameplay, SettingsControlType.Slider)]
[SettingsRange(0, 100)]
[SettingsDisplay("Skycraft Density", ...)]
public int Density { get; set; }
```

Picked up automatically by `SettingsControlFactory` and rendered as a slider under the `Gameplay` tab. Per-profile ratios are *not* user-facing — they live in `data/skycraft.toml` as designer-tunable knobs. At `Density=0` the system goes fully dormant (no spawns, no per-frame work beyond a no-op tick); at `Density=100` the pool sits at its tuned upper bound (validated in playtesting).

## Consequences

**Positive:**

- The sky and rooftops no longer feel empty in `Silence` mode. Three altitude layers populate consistently because per-profile sub-budgets guarantee a visual mix.
- Per-frame interop cost is bounded by the pool size, not by chunk loading. `MaxTime` spikes from streaming events don't compound with Skycraft transform writes.
- Reuses existing data wherever possible: `RoadSegment` for Drifter routing, `ChunkData.Buildings` rooftops for landing pads. No parallel world-data infrastructure.
- The hybrid renderer keeps the "real light spilling on the side of a building" cinematic moment while letting 40+ Skycraft remain affordable.
- The hitching feature stays a small future increment instead of a rewrite — markers, events, and the `HasRider` flag are in place from day one.
- `SkycraftDensity=0` is a hard off-switch. Players who don't want the system pay zero perf cost and see zero entities.

**Negative:**

- Two rendering paths to maintain. A bug in the near→far swap (e.g. light not destroyed when promoting to MultiMesh) leaks resources silently.
- The repath-on-invalidation behaviour can look like a Skycraft "changed its mind" mid-flight when chunks unload. Reads as "ambient", but a careful player could notice the heading change.
- Drifters routing strictly along road corridors gives less spatial variety than free-flight between buildings would. The fix (real avoidance) is expensive enough to be a separate decision.
- Per-profile sub-budgets are three separate counters to balance instead of one. Misconfigured ratios produce a sky that's all Express or all Drifter and looks wrong even though density is "right".
- Profile-tied altitude bands make the system sensitive to extreme building heights — a building taller than the Express clearance margin would be flown through. Defended only by the world generator's height cap; if that cap changes, this clearance margin needs to follow.
- Bird's Eye view can show edge spawns/despawns at the active radius. Documented; not fixed.
- "Marked landing POIs" need rules tuned per chunk to avoid both starvation (no landable rooftops nearby) and saturation (landings everywhere). This is playtest work.

**Related:**

- [ADR-0014](0014-csharp-godot-interop-cost.md) — the cost model that forces the pool cap, the hybrid renderer, and the structural-collision-freedom decisions.
- [ADR-0008](0008-game-mode-framework.md) — `GameModeManager` exists, but Skycraft deliberately does not subscribe to it (see sec. 5).
- [ADR-0007](0007-state-machine-capability-flags.md) — `AllowsGameTick` automatically pauses the Skycraft tick when game is paused; no per-system pause handling needed.
- [ADR-0009](0009-deterministic-seeded-generation.md) — `SkycraftPoiRegistry` seeds its per-chunk RNG with `StableHash.Combine(ChunkSeed, salt)`, matching the determinism contract used by other world modules; marked landing POIs and sky-waypoint positions are deterministic per chunk. The runtime `SkycraftController` RNG (movement jitter, landing-chance rolls, hue jitter) is deliberately non-deterministic — Skycraft are ambient and not part of save state.
- [ADR-0015](0015-meshbuilder-for-chunk-geometry.md) — Skycraft do not use `MeshBuilder` (they're not chunk geometry); the far-tier `MultiMesh` is a separate per-instance-buffer path, not a `SurfaceTool` path.
- Aliveness `CarTrailRenderer` — independent system, but Drifters share the road-corridor data with it and should read visually as a cohesive vertical traffic stack.
