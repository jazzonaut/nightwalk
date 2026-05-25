# ADR-0004: In-process typed `EventBus` over Godot signals

## Status

Accepted (retrofitted 2026-05)

## Context

Many of Nightwalk's systems need to react to things that happen in other systems without holding direct references to each other: scoring reacts to collectibles and beacon syncs, the HUD reacts to score changes, the city-aliveness service reacts to building climbs, the resonance mode reacts to collection and detection, music and SFX react to almost everything. The question is what mechanism carries these notifications.

Godot's first-class option is **signals** — string-named, declared per-emitter, connected through the editor or via `Connect(...)`. They are inherently engine-bound: signals can only be emitted by `GodotObject` (or descendant) instances, names are stringly typed at the C# call site (unless you use generated source-gen wrappers), and connections require the target to be a `Node`/`Resource` reachable from the scene tree.

Given the cross-assembly split ([ADR-0002](0002-assembly-split.md)), most publishers and subscribers live in `Nightwalk.Core`, which has no access to `GodotObject`. A signal-based design would force all events through the Godot-side assembly, defeating the split.

### Alternatives considered

- **Godot signals** — engine-native, editor-visible, no extra code. Cons: cross-assembly impossible (signals require `GodotObject`); stringly-typed unless you adopt source-gen; no typed payloads (signals carry `Variant`s); connection lifecycle tied to the scene tree, easy to leak when nodes are freed asynchronously.
- **Direct service references** (publisher holds a list of subscriber callbacks) — minimal infrastructure, but every new event type rewrites the same code, and every cross-system relationship requires a manual wire-up in `GameRoot`. Coupling balloons.
- **C# `event` keyword on services** — typed, terse, no extra infrastructure. Cons: no central place to observe traffic, no deferral, no priority handling, no leak diagnostics.
- **Custom `EventBus` with typed `Subscribe<T>`/`Publish<T>` and a deferred priority queue** — slightly more code, but full type safety, single observation point, centralized leak detection, and cross-assembly because it lives in `Core`.

## Decision

Use a custom **`EventBus`** in `Nightwalk.Core.Events`. The bus is the single canonical mechanism for cross-system communication in Nightwalk. Godot signals are reserved for purely intra-node concerns (e.g., a button's `Pressed` signal driving its parent menu) — they do not cross system boundaries.

### Shape

- `IEventBus` exposes:
  - `Subscribe<T>(Action<T>)` / `Unsubscribe<T>(Action<T>)` where `T : IDomainEvent`.
  - `Publish<T>(T)` — immediate, synchronous fan-out to all current subscribers.
  - `PublishDeferred<T>(T, EventPriority)` — queues the event for later draining.
  - `ProcessDeferredEvents()` — drains the queue once, sorted by priority. Events published *during* draining are queued for the *next* drain (loop prevention).
  - `DeferredEventCount`, `ClearAll()`.

- `EventPriority` is `Critical (0)`, `Normal (100)`, `Low (200)` — lower numeric value drains first.

- Events implement the marker interface `IDomainEvent`. Strongly-typed, immutable payloads.

- The implementation is thread-safe (lock-guarded handler registry) with a per-event-type handler cache invalidated by a `_dirtyTypes` set, plus reflection-built wrapper delegates cached after first use.

- Debug builds track subscription sources (`subscriber.GetType().FullName + method name`) for leak detection via `GetSubscriptionStats()` / `LogActiveSubscriptions()`. The tracking is `#if DEBUG`-gated so it does not affect release builds.

### Draining

`ProcessDeferredEvents` is called once per world tick from `WorldState.Tick`, so the deferred queue's natural cadence is the physics tick rate. Immediate `Publish` does not flow through the queue.

### Direct service references coexist with the bus

Not every cross-system interaction goes through the bus, and that's intentional. Two patterns legitimately use direct references instead of events:

- **Pull-on-demand reads** — consumers hold a reference to `GameDataService`, `IDifficultyService`, `GameStateManager`, etc. and read on demand. The data has no event shape (it's a current value, not a change), and broadcasting every internal mutation would force consumers to cache the latest themselves. Examples: `PlayerEnergy._difficultyService.GetJetpackEnergyCost(...)`, `BeaconSyncManager._scoreService.AddScore(...)`.
- **Imperative service calls whose result is then broadcast** — `ScoreService.AddScore(...)`, `BuildingTracker.MarkClimbed(...)`, `CollectibleTracker.MarkCollected(...)` are called directly with the intent; the service then publishes the resulting `*Event` for anyone interested. Splitting the call into "publish a request command" + "service subscribes" buys nothing — the caller has the intent and knows where to send it.

The rule is: **events broadcast facts (something happened); direct calls express intent (do this thing).** Use the bus for facts that multiple unrelated systems care about; use a direct reference when there's a specific service that owns the operation.

### Verified

- `IEventBus`, `EventBus`, `EventPriority`, `IDomainEvent` all live in `Nightwalk.Core.Events` (no Godot dependency).
- `EventBus` constructed first in `GameRoot._Ready()` (`GameRoot.cs:71`).
- `WorldState.Tick` calls `_eventBus.ProcessDeferredEvents()` (`WorldState.cs:58`).
- DEBUG-only subscription tracking exists (`EventBus.cs:17-20`, `42-52`, `66-74`, `147-149`, `153-195`).
- Events used in practice: `GameStateChangedEvent`, `SessionResetRequestedEvent`, `ScoreChangedEvent`, `ComboChangedEvent`, `BuildingClimbedEvent`, `RoadAlivenessChangedEvent`, `ZiplineCreatedEvent`, `ViewModeChangedEvent`, `AlertChangedEvent`, `MaxExposureEvent`, `WardenRepelledEvent`, `CollectibleCollectedEvent` — each is a distinct `IDomainEvent` struct/record.

## Consequences

**Positive:**
- Cross-system communication is typed end-to-end. A handler signature `Action<ScoreChangedEvent>` cannot accidentally receive the wrong payload.
- Publishers and subscribers do not need references to each other, only to `IEventBus`. This is what makes `GameRoot`'s wiring tractable — services receive the bus, not each other.
- The bus lives in `Core` and is callable from both assemblies, which is the only mechanism compatible with the assembly split.
- Deferred publishing with priority lets gameplay code throw events into the queue without worrying about handler timing or recursion. Loop prevention is built in (events published during drain go to the next drain).
- DEBUG-only leak tracking surfaces forgotten `Unsubscribe`s — important because manual DI means most subscriptions are created in setup helpers and torn down in `CleanupGame`/`Reset`.

**Negative:**
- Subscription lifecycle is manual. Every `Subscribe` needs a matching `Unsubscribe`. Forgetting one leaks the subscriber, and (because handlers hold references to their target) potentially the entire object graph. The DEBUG tracker exists exactly to surface this.
- Reflection-based wrapper creation runs on first subscription per type (cached afterward). Cold-path cost only, but it precludes AOT scenarios without further work.
- No "wildcard" or base-type subscription — if you want to react to "any score-related event" you must subscribe to each concrete type.
- Two parallel mechanisms (EventBus + Godot signals on UI) means contributors must learn when each is appropriate. The rule: EventBus for cross-system, signals for intra-node.

**Related:**
- [ADR-0002](0002-assembly-split.md) — the assembly split is the load-bearing reason this exists at all.
- [ADR-0003](0003-manual-di-no-autoloads.md) — `EventBus` is the first service constructed in `GameRoot._Ready()`.
