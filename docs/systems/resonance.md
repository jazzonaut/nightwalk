# Resonance

A game mode that layers tension and stealth on the base loop. Collecting raises an Alert meter; high Alert spawns a Warden; the Warden deploys Seekers that detect the player; detection fills an Exposure meter that drains score. Reaching a vulnerable Warden with the beacon beam repels it.

> **Engineering decisions:** [ADR-0008 — pluggable game-mode framework](../adr/0008-game-mode-framework.md). The Resonance loop *itself* is gameplay design, not an engineering decision per se — most of this doc is reference.
>
> **Note:** README does **not** mention Resonance at all — the README pre-dates this subsystem. The vocabulary lives in [CONTEXT.md](../../CONTEXT.md); historical gaps are tracked in [FINDINGS.md](../../FINDINGS.md).

## Player-facing summary

Pick "Resonance" on the New Game screen. Now every collectible you grab raises an Alert meter at the top of the HUD. Cross the spawn threshold and a Warden manifests overhead. Seekers descend, scan with light cones, and "ping" your location. While detected, an Exposure meter fills and your score drains. Hide in shadow, distract Seekers with decoy pings, or push Alert past the vulnerability threshold and beam the Warden's exposed core to repel it — score reward, Alert drops, a calm window opens.

## The loop

```
Explore ──► Collect ──► Alert rises ──► Warden spawns
                                              │
                                              ▼
                                       Seekers deploy
                                              │
                                              ▼
                                       Player detected ──► Exposure rises ──► Score drains
                                              │
                                       Max exposure ──► Big penalty + Alert spike
                                              │
                                              ▼
                                  Alert ≥ VulnerabilityThreshold
                                              │
                                       ▼
                             Warden core exposed
                                       │
                              Beam hits core for BeamContactDuration
                                       │
                                       ▼
                            WardenRepelled
                              · score reward
                              · Alert drops
                              · CalmWindow seconds free
                                       │
                                       ▼
                            Loop repeats
```

## Meters

### Alert (`AlertMeter`)

A 0-1 meter that rises on:
- Each collectible pickup: `+PickupIncrease`.
- Per second while detected by Seeker: `+SeekerDetectionIncrease * dt`.

Decays at `DecayRate` per second once `DecayDelay` seconds pass without an increase.

Thresholds:
- `WardenSpawnThreshold` — spawns Warden when crossed upward.
- `WardenDespawnThreshold` — Warden departs when Alert falls below.
- `VulnerabilityThreshold` — Warden becomes repel-able when crossed upward.

### Exposure (`ExposureMeter`)

A 0-1 meter that rises while one or more Seekers actively detect the player (`RiseRate * dt`), falls when no detector is active (`FallRate * dt`). High exposure drains score at `ScoreDrainRate * dt * exposureLevel`.

At 100% exposure:
- `MaxExposurePenalty` (large one-time score deduction).
- `MaxExposureAlertSpike` (Alert jump).
- Cannot re-trigger until exposure falls below `MaxExposureResetThreshold`.

## Actors

### Warden

A floating shard cluster that drifts toward the player. Visuals: `ShardCount` shards in a `ClusterRadius` cluster, base/bright/vulnerable colors. Behaviour:

- **Spawn**: appears at `SpawnHeight + SpawnDescentOffset` above player; descends at `DriftSpeed * DescentSpeedMultiplier`.
- **Drift**: at `DriftSpeed` toward player; stops at `DriftMinDistance`.
- **Vulnerable**: when Alert ≥ `VulnerabilityThreshold`. Core exposed for `VulnerabilityDuration` seconds; drift speed multiplied by `VulnerableDriftMultiplier`.
- **Departure**: at `DepartureSpeed` until `DepartureDistance` above target.

### Seekers

Floating orb-with-scan-cone NPCs that orbit the Warden and investigate noise pings.

States in code: travel, approaching, scanning, hunting, returning, recalling. Many tunables (in `data/resonance.toml [seeker]`):

- Spawn: `SpawnChance` per collectible pickup; max counts vary with Alert tier (`MaxCountLowAlert`/`MedAlert`/`HighAlert`); spawn offset from Warden (`SpawnOffsetRange`).
- Travel: low/high speeds tied to Alert level; `ScanConeAngle`, `ScanConeRange`.
- Wander: Perlin noise–based organic movement (`WanderAmplitude`, `WanderFrequency`, `ProximityFadeDistance`).
- Approaching: slower at `ApproachSpeed`, transitions at `ArrivalDistance`.
- Scanning: orbital `OrbitSpeed`, `AltitudeOscillation`, sweep patterns (`SweepFrequency`, `SweepRadiusMultiplier`, `SweepVerticalAmplitude`).
- Hunting (after losing sight): `HuntDuration`, `HuntSpeed`, spiral search (`HuntSpiralAngularSpeed`, `HuntSpiralExpansion`).
- Returning: pool distances, recall timeout.
- Detection: `DetectionPingCooldown`, `DetectionGracePeriod`, `DispatchDelay`, `IdlePatrolMin/MaxRadius`.
- Ping scoring (decides which ping a Seeker chases): recency vs priority vs distance vs already-assigned penalties, plus `PingRedirectThreshold` to allow re-targeting; priorities for noise vs detection pings; expiry windows.
- Collision avoidance: `AvoidanceStrength`, `AvoidanceRayLength`.

## Counterplay

Player can hold the **spotlight** tool on a surface to charge a **decoy** ping that pulls Seekers away:

- `SpotlightLureChargeTime` — seconds to hold.
- `DecoyDuration` — how long the decoy persists.
- `DecoyPriority` — priority multiplier vs noise pings.
- `MaxActiveDecoys` — concurrent cap.

(See `DecoyTool.cs` in [Player Movement](player-movement.md) tools list. The decoy tool is a real player ability, not just a stub.)

## Repel

When Warden is vulnerable, the beacon signal beam can hit the Warden's core (`CoreHitRadiusFraction` of scale around the center):

- Sustained beam contact for `BeamContactDuration` seconds repels the Warden.
- Beam progress decays at `BeamContactDecayRate` per second when not hitting.
- Successful repel:
  - `AlertDrop` (Alert reduction, 0-1).
  - `ScoreReward` (score award via `ScoreCategory.WardenRepelled`).
  - `CalmWindow` seconds of scan suppression (Seekers stand down).
  - `RepelCooldown` seconds before another vulnerability can trigger.
- Publishes `WardenRepelledEvent`.

## Visual effects

`ResonanceVisualEffects` applies a sky-tint and post-process vignette tied to Alert level — the world gets slightly darker / redder as tension rises. Resets on mode exit.

## Tuning — `data/resonance.toml`

All values live in `data/resonance.toml`. Sections:

- `[mode]` — `default_mode = "silence"` (the *default*; players choose at New Game).
- `[player]` — `height`, `center_offset_fraction` (for detection targeting).
- `[alert]` — `pickup_increase`, `seeker_detection_increase`, `decay_rate`, `decay_delay`, `warden_spawn_threshold`, `warden_despawn_threshold`, `vulnerability_threshold`.
- `[exposure]` — `rise_rate`, `fall_rate`, `score_drain_rate`, `max_exposure_penalty`, `max_exposure_alert_spike`, `max_exposure_reset_threshold`.
- `[warden]` — see Actors above.
- `[seeker]` — see Actors above (~60 keys).
- `[counterplay]` — `spotlight_lure_charge_time`, `decoy_duration`, `decoy_priority`, `max_active_decoys`.
- `[repel]` — `beam_contact_duration`, `beam_contact_decay_rate`, `alert_drop`, `score_reward`, `calm_window`, `repel_cooldown`.

Full POCO definitions in `src/Nightwalk.Core/Config/Definitions/ResonanceConfig.cs` (~430 lines).

## Implementation

- `src/Nightwalk.Core/GameMode/ResonanceMode.cs` — the mode lifecycle, meters, score-drain accumulation, Warden lifecycle event dispatch.
- `src/Nightwalk.Core/Resonance/AlertMeter.cs`, `ExposureMeter.cs` — meter state classes.
- `src/Nightwalk.Core/Resonance/Warden/WardenController.cs` — Core-side Warden state machine.
- `src/Nightwalk.Core/Resonance/Seeker/SeekerController.cs`, `SeekerMovement.cs` — Core-side Seeker logic + movement.
- `src/Nightwalk.Core/Resonance/Events/*.cs` — `AlertChangedEvent`, `MaxExposureEvent`, `WardenRepelledEvent`, etc.
- `src/Nightwalk.Godot/Resonance/ResonanceModeSystem.cs` — Godot bridge; spawns/despawns nodes in response to `OnWarden*Requested` events from `ResonanceMode`.
- `src/Nightwalk.Godot/Resonance/WardenNode.cs`, `WardenRenderer.cs` — Warden visuals.
- `src/Nightwalk.Godot/Resonance/ResonanceVisualEffects.cs` — sky/vignette tint.
- `src/Nightwalk.Godot/Resonance/DecoyPingMarker.cs` — decoy visualisation.
- `src/Nightwalk.Godot/Player/Tools/DecoyTool.cs` — the spotlight-charge → decoy tool.

## Integration

| With | How |
|---|---|
| **Game Mode** | `ResonanceMode` is a registered `IGameMode`; enter/exit drives all of the above |
| **Scoring** | `ScoreCategory.ExposureDrain`, `MaxExposure`, `WardenRepelled` |
| **Beacon Sync** | The signal beam is the repel mechanism — sustained contact with Warden core |
| **Collectibles** | `CollectibleCollectedEvent` raises Alert + spawns noise ping for Seekers |
| **HUD** | Alert + Exposure meters visible only while Resonance is active |
| **Sky Effects** | `ResonanceVisualEffects` overlays sky tint and vignette |
| **Audio** | Detection/scanning/repel SFX (procedural via `SfxSynth`) |

## Acceptance checklist

Verified against code 2026-05-25.

- [x] Registered as `IGameMode` with `Id = "resonance"`.
- [x] `Enter` subscribes to `CollectibleCollectedEvent`, `AlertChangedEvent`, `MaxExposureEvent`.
- [x] `Exit` unsubscribes and requests Warden despawn.
- [x] Collectible pickup raises Alert by `PickupIncrease`.
- [x] Seeker detection raises Alert by `SeekerDetectionIncrease * dt`.
- [x] Warden spawn/despawn/vulnerability thresholds checked in `OnAlertChanged`.
- [x] Exposure drains score with fractional accumulator (`_accumulatedScoreDrain`) so only whole-point events fire.
- [x] Max exposure publishes `MaxExposureEvent` and applies penalty + alert spike.
- [x] `OnWardenRepelled` drops alert, awards `ScoreReward`, publishes `WardenRepelledEvent`.
- [x] Decoy tool charges via spotlight hold.
- [x] Visual effects (`ResonanceVisualEffects`) apply tint scaled by Alert.

## Related

- [Game Mode](game-mode.md), [Scoring](scoring.md), [Beacon Sync](beacon-sync.md), [Collectibles](collectibles.md), [Sky Effects](sky-effects.md), [HUD](hud.md), [Player Movement](player-movement.md) (decoy tool).
- ADR-0008 (Game mode framework), ADR-0004 (EventBus), ADR-0010 (Difficulty — orthogonal).
