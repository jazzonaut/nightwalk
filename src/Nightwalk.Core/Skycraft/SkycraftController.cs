using System;
using System.Collections.Generic;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Diagnostics;
using Nightwalk.Core.Events;
using Nightwalk.Core.Skycraft.Events;
using Nightwalk.Core.World.Roads;

namespace Nightwalk.Core.Skycraft;

/// <summary>
/// Pool-managed ambient air-traffic simulation. Owns the active Skycraft set,
/// drives the per-profile state machine, and publishes lifecycle events.
/// Pure Core — no engine deps. The Godot-side renderer reads instance positions
/// each frame via <see cref="ActiveInstances"/> and listens to spawn/despawn/landing events.
/// </summary>
public sealed class SkycraftController
{
    private readonly IEventBus _eventBus;
    private readonly SkycraftPoiRegistry _registry;
    private readonly ChunkManager _chunkManager;
    private SkycraftConfig _config;
    private readonly Random _rng = new();

    // Active list (live Skycraft) plus a free-list pool (returned slots).
    private readonly List<SkycraftInstance> _active = new(64);
    private readonly Stack<SkycraftInstance> _pool = new();
    private readonly int[] _activePerProfile = new int[3];

    private float _playerX, _playerY, _playerZ;
    private float _spawnCheckTimer;
    private int _effectivePoolCap;

    public IReadOnlyList<SkycraftInstance> ActiveInstances => _active;

    public SkycraftController(IEventBus eventBus, SkycraftPoiRegistry registry, ChunkManager chunkManager, SkycraftConfig config)
    {
        _eventBus = eventBus;
        _registry = registry;
        _chunkManager = chunkManager;
        _config = config;
        RecomputePoolCap();
    }

    /// <summary>
    /// Applies a new config (e.g. after TOML hot reload or settings-slider change).
    /// Pool cap is recomputed; active Skycraft are kept until they naturally despawn,
    /// then sub-budgets re-fill at the new density.
    /// </summary>
    public void Reconfigure(SkycraftConfig config)
    {
        _config = config;
        RecomputePoolCap();
    }

    private void RecomputePoolCap()
    {
        int density = Math.Clamp(_config.Density, 0, 100);
        _effectivePoolCap = (int)Math.Round(_config.Pool.MaxPoolSize * density / 100.0);
    }

    /// <summary>
    /// Returns the per-profile sub-budget (rounded), the sum can be less than the
    /// effective pool cap because of integer rounding.
    /// </summary>
    private int BudgetFor(SkycraftProfile profile)
    {
        if (_effectivePoolCap == 0) return 0;
        float ratio = profile switch
        {
            SkycraftProfile.Express => _config.Mix.Express,
            SkycraftProfile.Commuter => _config.Mix.Commuter,
            SkycraftProfile.Drifter => _config.Mix.Drifter,
            _ => 0f,
        };
        return Math.Max(0, (int)Math.Round(_effectivePoolCap * ratio));
    }

    /// <summary>
    /// Per-tick update. Call from the Godot side every physics tick while
    /// <c>AllowsGameTick</c>.
    /// </summary>
    public void Update(float delta, float playerX, float playerY, float playerZ)
    {
        _playerX = playerX; _playerY = playerY; _playerZ = playerZ;

        // Density=0 — no spawns, single-pass despawn of anything still active.
        if (_effectivePoolCap == 0)
        {
            ForceDespawnAll();
            return;
        }

        _spawnCheckTimer += delta;
        if (_spawnCheckTimer >= _config.Pool.SpawnCheckInterval)
        {
            _spawnCheckTimer = 0f;
            TryFillSubBudgets();
        }

        // Iterate in reverse for safe in-place removal.
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var s = _active[i];
            TickInstance(s, delta);

            if (ShouldDespawn(s))
            {
                DespawnAt(i);
            }
        }
    }

    /// <summary>
    /// Releases the entire active set. Called on session reset.
    /// </summary>
    public void Reset()
    {
        ForceDespawnAll();
        _spawnCheckTimer = 0f;
    }

    // ---- spawn / despawn ---------------------------------------------------

    private void TryFillSubBudgets()
    {
        // Per tick, allow up to MaxSpawnsPerTickPerProfile spawn attempts so a freshly
        // raised density (slider change, first session entry) converges quickly. The
        // cap prevents a one-frame interop spike when the pool is very large.
        const int MaxSpawnsPerTickPerProfile = 4;
        for (int p = 0; p < 3; p++)
        {
            var profile = (SkycraftProfile)p;
            int budget = BudgetFor(profile);
            int deficit = budget - _activePerProfile[p];
            if (deficit <= 0) continue;

            int attempts = Math.Min(deficit, MaxSpawnsPerTickPerProfile);
            for (int i = 0; i < attempts; i++)
            {
                int before = _activePerProfile[p];
                TrySpawn(profile);
                // If a spawn failed (no POIs yet), stop attempting this profile this tick.
                if (_activePerProfile[p] == before) break;
            }
        }
    }

    private void TrySpawn(SkycraftProfile profile)
    {
        // Pick an initial target POI; if none are available yet (early world load,
        // no marked POIs) we silently skip and try again on the next interval.
        if (!TryPickTargetForCruise(profile, out var target)) return;

        var inst = RentFromPool();
        inst.Id = SkycraftId.NewId();
        inst.Profile = profile;
        inst.State = SkycraftState.Cruising;
        inst.HasRider = false;
        inst.LandingImmunity = false;
        inst.DwellRemaining = 0f;
        inst.ScaleJitter = 1f + ((float)_rng.NextDouble() * 2f - 1f) * _config.Render.ScaleJitter;
        inst.AccentHueShiftDegrees = ((float)_rng.NextDouble() * 2f - 1f) * _config.Render.AccentHueJitter;

        // Spawn at the far edge of the active region in a random direction toward player vicinity.
        float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
        float radius = _config.Pool.SpawnRadius;
        inst.X = _playerX + MathF.Cos(angle) * radius;
        inst.Z = _playerZ + MathF.Sin(angle) * radius;
        var p = GetProfileSettings(profile);
        inst.CruiseY = p.AltitudeMin + (float)_rng.NextDouble() * (p.AltitudeMax - p.AltitudeMin);
        inst.Y = inst.CruiseY;
        inst.Vx = inst.Vy = inst.Vz = 0f;

        AssignTarget(inst, target);

        _active.Add(inst);
        _activePerProfile[(int)profile]++;
        _eventBus.Publish(new SkycraftSpawnedEvent(inst.Id, inst.Profile));
    }

    private void DespawnAt(int index)
    {
        var inst = _active[index];
        _eventBus.Publish(new SkycraftDespawningEvent(inst.Id, inst.Profile));
        _activePerProfile[(int)inst.Profile]--;
        _active.RemoveAt(index);
        ReturnToPool(inst);
    }

    private void ForceDespawnAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            DespawnAt(i);
        }
    }

    private SkycraftInstance RentFromPool()
        => _pool.Count > 0 ? _pool.Pop() : new SkycraftInstance();

    private void ReturnToPool(SkycraftInstance inst)
    {
        inst.Reset();
        _pool.Push(inst);
    }

    private bool ShouldDespawn(SkycraftInstance s)
    {
        // Never despawn while a rider is aboard. The hitching surface is reserved;
        // landing/takeoff/despawn logic reads HasRider and refuses to orphan a rider.
        if (s.HasRider) return false;

        float dx = s.X - _playerX;
        float dz = s.Z - _playerZ;
        float distSq = dx * dx + dz * dz;
        float despawnSq = _config.Pool.DespawnRadius * _config.Pool.DespawnRadius;
        // Asymmetric hysteresis: spawn at SpawnRadius, despawn past the larger DespawnRadius.
        // When a sub-budget shrinks (e.g. density slider drop), the over-budget instance is
        // collected here naturally as soon as it drifts past DespawnRadius — no eager evict.
        return distSq > despawnSq;
    }

    // ---- state machine -----------------------------------------------------

    private void TickInstance(SkycraftInstance s, float delta)
    {
        // Validate target: if its chunk unloaded mid-flight, repath (unless a rider is aboard).
        if (s.HasTarget && !_registry.IsValid(s.Target))
        {
            if (s.HasRider)
            {
                // Rider aboard — never repath. The Skycraft will continue toward the
                // last known target position. This is acceptable because riders are
                // a future feature; today HasRider is always false.
            }
            else if (s.HasTurnPending)
            {
                // Mid-arc: reassigning now would clear HasTurnPending and the next
                // road-walk tick would hard-lock perpendicular to the lane, snapping
                // position from the arc circle onto the centerline. Defer the repath
                // (~150ms arc duration); the next tick re-checks IsValid from a clean
                // lane position once the arc completes.
            }
            else if (!TryPickTargetForState(s, out var replacement))
            {
                // No replacement POIs anywhere — keep last position; despawn will
                // collect it once it drifts out of the active region.
                s.HasTarget = false;
                if (s.State is SkycraftState.Approaching or SkycraftState.Descending)
                {
                    // Without a target, the approach/descent ticks would keep reading
                    // the stale s.Target. Resume cruise so the craft coasts on its
                    // current velocity until the despawn radius collects it.
                    s.State = SkycraftState.Cruising;
                }
            }
            else
            {
                AssignTarget(s, replacement);
                if (s.State is SkycraftState.Approaching or SkycraftState.Descending)
                {
                    // Abort approach; resume cruise to the new target.
                    s.State = SkycraftState.Cruising;
                }
            }
        }

        switch (s.State)
        {
            case SkycraftState.Cruising:      TickCruising(s, delta); break;
            case SkycraftState.Approaching:   TickApproaching(s, delta); break;
            case SkycraftState.Descending:    TickDescending(s, delta); break;
            case SkycraftState.Dwelling:      TickDwelling(s, delta); break;
            case SkycraftState.TakingOff:     TickTakingOff(s, delta); break;
        }
    }

    private void TickCruising(SkycraftInstance s, float delta)
    {
        if (!s.HasTarget) { CoastForward(s, delta); return; }

        var p = GetProfileSettings(s.Profile);

        // Drifter and Commuter cruise along the road network. Per ADR-0016 §3
        // collision-freedom is structural; we extend that property to Commuter by
        // routing it along the road grid too. Express stays on free-flight above
        // all rooftops.
        if (UsesGridRouting(s.Profile))
        {
            // Off-road (initial spawn, post-takeoff): fly diagonally toward the
            // nearest road at cruise altitude rather than teleporting onto its
            // lane. The transit ends with a sub-meter snap when the craft is
            // within half a lane of the centerline+lane position.
            if (!s.OnRoad)
            {
                TickFlyToRoad(s, p, delta);
                return;
            }
            if (!s.HasTargetRoad)
            {
                ResolveTargetRoad(s);
            }

            TickRoadWalk(s, p, delta);

            // Cruise ends only when we're on the target's road AND within
            // ApproachRadius along the cruise axis. "On target's road" uses
            // the resolved target road (not the target's literal coords) so
            // it works for rooftop landings where target sits over a plot,
            // not a road.
            bool onTargetRoad = s.HasTargetRoad
                && s.RoadDir == s.TargetRoadDir
                && MathF.Abs(s.RoadPos - s.TargetRoadPos) < 6f;

            if (onTargetRoad)
            {
                float cruiseAxisDist = s.RoadDir == RoadDirection.EastWest
                    ? MathF.Abs(s.X - s.Target.X)
                    : MathF.Abs(s.Z - s.Target.Z);
                if (cruiseAxisDist < _config.Landing.ApproachRadius)
                {
                    MaybeBeginApproachOrPickNext(s, p);
                }
            }
            return;
        }

        // Hold cruise altitude until inside the approach radius, then let the
        // approach phase descend toward the target's own Y (e.g. rooftop or road).
        float distToTargetXZ = MathF.Sqrt(HorizontalDistSq(s.X, s.Z, s.Target.X, s.Target.Z));
        bool inApproach = distToTargetXZ < _config.Landing.ApproachRadius * 1.5f;
        float cruiseTargetY = inApproach
            ? s.Target.Y + _config.Landing.TakeoffClearance
            : Math.Clamp(s.Y, p.AltitudeMin, p.AltitudeMax);

        MoveToward(s, s.Target.X, cruiseTargetY, s.Target.Z, p.Speed, p.Acceleration, delta);

        float horizDistSq = HorizontalDistSq(s.X, s.Z, s.Target.X, s.Target.Z);
        if (horizDistSq < _config.Landing.ApproachRadius * _config.Landing.ApproachRadius)
        {
            MaybeBeginApproachOrPickNext(s, p);
        }
    }

    /// <summary>
    /// Common arrival logic shared by the free-flight and road-walk cruise paths.
    /// Either transitions to the approach state (landing) or picks the next POI.
    /// </summary>
    private void MaybeBeginApproachOrPickNext(SkycraftInstance s, ProfileSettings p)
    {
        bool canLand = !s.LandingImmunity
                      && (s.Target.Kind == SkycraftPoiKind.RooftopLanding
                          || s.Target.Kind == SkycraftPoiKind.RoadLanding)
                      && _rng.NextDouble() < p.LandingChance;

        if (canLand)
        {
            s.State = SkycraftState.Approaching;
            return;
        }

        s.LandingImmunity = false;
        if (TryPickTargetForCruise(s.Profile, out var next))
        {
            AssignTarget(s, next);
        }
    }

    private void TickApproaching(SkycraftInstance s, float delta)
    {
        // Decelerate horizontally and align altitude above target.
        float targetY = s.Target.Y + _config.Landing.TakeoffClearance * 0.5f;
        var p = GetProfileSettings(s.Profile);
        MoveToward(s, s.Target.X, targetY, s.Target.Z, p.Speed * 0.4f, p.Acceleration, delta);

        // Once horizontally close and roughly aligned vertically, begin descent.
        float horizDistSq = HorizontalDistSq(s.X, s.Z, s.Target.X, s.Target.Z);
        if (horizDistSq < 4f * 4f && MathF.Abs(s.Y - targetY) < 4f)
        {
            s.State = SkycraftState.Descending;
        }
    }

    private void TickDescending(SkycraftInstance s, float delta)
    {
        // Curved-arc descent: stay horizontally over the target, drop at descent speed
        // until we reach settle altitude above target.
        float settleY = s.Target.Y + _config.Landing.SettleAltitude;

        // Horizontal: snap toward target with light gain (already very close).
        float kx = MathF.Min(8f * delta, 1f);
        s.X += (s.Target.X - s.X) * kx;
        s.Z += (s.Target.Z - s.Z) * kx;

        float dy = settleY - s.Y;
        float step = _config.Landing.DescentSpeed * delta;
        s.Y += MathF.Sign(dy) * MathF.Min(MathF.Abs(dy), step);
        s.Vx = s.Vy = s.Vz = 0f;
        UpdateForwardFromVelocityOrTarget(s);

        if (MathF.Abs(dy) < 0.2f)
        {
            // Touchdown: enter dwell.
            s.X = s.Target.X; s.Y = settleY; s.Z = s.Target.Z;
            s.State = SkycraftState.Dwelling;
            s.DwellRemaining = SampleDwellSeconds(s.Profile);
            _eventBus.Publish(new SkycraftLandedEvent(s.Id, s.Profile, s.Target.Kind, s.X, s.Y, s.Z));
        }
    }

    private void TickDwelling(SkycraftInstance s, float delta)
    {
        s.DwellRemaining -= delta;
        if (s.DwellRemaining <= 0f)
        {
            // Defer target pick until takeoff completes. Picking here would route
            // through AssignTarget's road-grid path while the craft is still at
            // landing altitude, which used to teleport the craft horizontally to
            // the nearest road's lane and then climb vertically from the wrong
            // position. Clearing OnRoad/HasTarget now means the climb happens
            // straight up; the post-takeoff cruise re-engages the road grid via
            // TickFlyToRoad (a smooth diagonal transit at cruise altitude).
            s.HasTarget = false;
            s.HasTurnPending = false;
            s.OnRoad = false;
            s.State = SkycraftState.TakingOff;
            // Climb to the per-instance cruise altitude, not s.Y + clearance — for low-band
            // profiles (Drifter 5-15m) a fixed 30m clearance overshoots cruise and the craft
            // visibly descends back to band during the next cruise tick. Re-sample CruiseY so
            // each post-landing trip picks a fresh altitude within the band.
            var pt = GetProfileSettings(s.Profile);
            s.CruiseY = pt.AltitudeMin + (float)_rng.NextDouble() * (pt.AltitudeMax - pt.AltitudeMin);
            s.TakeoffHeightTarget = MathF.Max(s.Y, s.CruiseY);
            s.LandingImmunity = true;
            _eventBus.Publish(new SkycraftTakingOffEvent(s.Id, s.Profile));
        }
    }

    private void TickTakingOff(SkycraftInstance s, float delta)
    {
        float step = _config.Landing.TakeoffSpeed * delta;
        s.Y += step;
        UpdateForwardFromVelocityOrTarget(s);
        if (s.Y >= s.TakeoffHeightTarget)
        {
            // Pick the next target now that we're at cruise altitude. AssignTarget
            // only records the target + resolves its road; the off-road horizontal
            // transit happens in TickCruising → TickFlyToRoad on the next tick.
            // If no POIs are available the craft enters Cruising with HasTarget=false
            // and CoastForward keeps it adrift until the despawn radius collects it.
            if (TryPickTargetForCruise(s.Profile, out var next))
            {
                AssignTarget(s, next);
            }
            s.State = SkycraftState.Cruising;
        }
    }

    // ---- movement helpers --------------------------------------------------

    private static float HorizontalDistSq(float x1, float z1, float x2, float z2)
    {
        float dx = x2 - x1; float dz = z2 - z1;
        return dx * dx + dz * dz;
    }

    /// <summary>
    /// Steer velocity toward a target with capped speed and acceleration.
    /// Pure kinematic — no physics queries (per ADR-0016 §3 structural collision-freedom).
    /// </summary>
    private void MoveToward(SkycraftInstance s, float tx, float ty, float tz, float maxSpeed, float accel, float delta)
    {
        float dx = tx - s.X;
        float dy = ty - s.Y;
        float dz = tz - s.Z;
        float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        if (dist < 1e-3f)
        {
            s.Vx *= 0.9f; s.Vy *= 0.9f; s.Vz *= 0.9f;
        }
        else
        {
            float dvx = (dx / dist) * maxSpeed - s.Vx;
            float dvy = (dy / dist) * maxSpeed - s.Vy;
            float dvz = (dz / dist) * maxSpeed - s.Vz;
            float dvMag = MathF.Sqrt(dvx * dvx + dvy * dvy + dvz * dvz);
            float cap = accel * delta;
            if (dvMag > cap && dvMag > 0f)
            {
                float k = cap / dvMag;
                dvx *= k; dvy *= k; dvz *= k;
            }
            s.Vx += dvx; s.Vy += dvy; s.Vz += dvz;
        }

        s.X += s.Vx * delta;
        s.Y += s.Vy * delta;
        s.Z += s.Vz * delta;
        UpdateForwardFromVelocityOrTarget(s);
    }

    private void CoastForward(SkycraftInstance s, float delta)
    {
        s.X += s.Vx * delta;
        s.Y += s.Vy * delta;
        s.Z += s.Vz * delta;
    }

    private static void UpdateForwardFromVelocityOrTarget(SkycraftInstance s)
    {
        // Hover craft are yaw-only: pitch must stay zero so a descending velocity
        // (approach/descent target Y below current Y) doesn't tip the body nose-down.
        // The road-walk and arc paths already write ForwardY=0; this is the catch-all
        // for free-flight cruise, approach, and takeoff where MoveToward feeds Y.
        float vxz = MathF.Sqrt(s.Vx * s.Vx + s.Vz * s.Vz);
        if (vxz > 0.1f)
        {
            s.ForwardX = s.Vx / vxz;
            s.ForwardY = 0f;
            s.ForwardZ = s.Vz / vxz;
            return;
        }
        if (s.HasTarget)
        {
            float dx = s.Target.X - s.X;
            float dz = s.Target.Z - s.Z;
            float dmag = MathF.Sqrt(dx * dx + dz * dz);
            if (dmag > 1e-3f)
            {
                s.ForwardX = dx / dmag;
                s.ForwardY = 0f;
                s.ForwardZ = dz / dmag;
            }
            // Pure-vertical motion (target directly above/below): keep last forward,
            // so a Skycraft descending onto the spot under it maintains its cruise yaw.
        }
    }

    private float SampleDwellSeconds(SkycraftProfile profile)
    {
        var l = _config.Landing;
        var (min, max) = profile switch
        {
            SkycraftProfile.Commuter => (l.CommuterDwellMin, l.CommuterDwellMax),
            SkycraftProfile.Drifter => (l.DrifterDwellMin, l.DrifterDwellMax),
            _ => (1f, 1f),
        };
        return min + (float)_rng.NextDouble() * (max - min);
    }

    // ---- POI selection -----------------------------------------------------

    private bool TryPickTargetForCruise(SkycraftProfile profile, out SkycraftPoi poi)
    {
        // Per profile:
        //  - Express: sky waypoints only (high cruise, never lands).
        //  - Commuter: sky waypoints, or rooftops strictly below cruise band so the
        //    approach descent can't graze a taller neighbour.
        //  - Drifter: road landings only (intermediate-waypoint planning handles routing).
        switch (profile)
        {
            case SkycraftProfile.Express:
                return PickFromKinds(out poi, SkycraftPoiKind.ExpressWaypoint);

            case SkycraftProfile.Commuter:
            {
                // Try a sky waypoint first; if that's empty, fall back to a rooftop
                // landing that's below the cruise band.
                if (_registry.CountOf(SkycraftPoiKind.CommuterWaypoint) > 0
                    && _rng.NextDouble() < 0.6)
                {
                    return _registry.TryPickRandom(SkycraftPoiKind.CommuterWaypoint, _rng, out poi);
                }
                float safeMaxY = _config.Commuter.AltitudeMin - 5f;
                if (_registry.TryPickRandomBelow(SkycraftPoiKind.RooftopLanding, safeMaxY, _rng, out poi))
                {
                    return true;
                }
                return _registry.TryPickRandom(SkycraftPoiKind.CommuterWaypoint, _rng, out poi);
            }

            case SkycraftProfile.Drifter:
                return PickFromKinds(out poi, SkycraftPoiKind.RoadLanding);

            default:
                return Fail(out poi);
        }
    }

    private bool TryPickTargetForState(SkycraftInstance s, out SkycraftPoi poi)
        => TryPickTargetForCruise(s.Profile, out poi);

    /// <summary>
    /// Assigns a new target POI. For grid-following profiles, resolves the target's
    /// nearest road so the cruise knows where to terminate and plans a turn if the
    /// craft is already on a road. Express skips both — it free-flies to its target.
    ///
    /// Critically does NOT teleport the craft onto a road. Off-road crafts (initial
    /// spawn at far edge, post-takeoff at cruise altitude over a rooftop) engage the
    /// road grid on the next cruise tick via <see cref="TickFlyToRoad"/> — a smooth
    /// diagonal transit at cruise altitude — rather than popping sideways here.
    ///
    /// <see cref="SkycraftInstance.MovingPositive"/> is therefore left untouched. The
    /// snap point inside TickFlyToRoad recomputes it from the target direction; for
    /// mid-cruise reassignments (no fly-to-road needed) the existing MovingPositive
    /// stays in place, and a U-loop is routed via two perpendicular turns at upcoming
    /// intersections (see <see cref="TickRoadWalk"/> note).
    /// </summary>
    private void AssignTarget(SkycraftInstance s, SkycraftPoi target)
    {
        s.Target = target;
        s.HasTarget = true;
        s.HasTurnPending = false;

        if (!UsesGridRouting(s.Profile)) return;

        ResolveTargetRoad(s);
        if (s.OnRoad) PlanTurnTowardTarget(s);
    }

    /// <summary>
    /// Profiles whose cruise follows the road grid (axis-locked road-walk). Express
    /// cruises above all rooftops so it doesn't need grid routing for collision
    /// avoidance.
    /// </summary>
    private static bool UsesGridRouting(SkycraftProfile profile)
        => profile == SkycraftProfile.Drifter || profile == SkycraftProfile.Commuter;

    // ---- Drifter road-walk -------------------------------------------------

    /// <summary>
    /// Axis-locked road cruise — used by Drifter and Commuter. On any given tick
    /// the Skycraft has velocity along exactly one horizontal axis (the cruise
    /// axis) and zero velocity on the perpendicular axis. The perpendicular
    /// position is hard-snapped to <c>RoadPos + LaneOffsetPerp</c> each tick,
    /// not lerped, so the Skycraft cannot slip off the road between frames.
    /// Diagonal flight is structurally impossible.
    /// </summary>
    // Smooth-turn tuning — designer-tunable via [turn] in data/skycraft.toml. Read through
    // properties (not cached fields) so hot reload of the section applies on the next tick.
    private float TurnRadius => _config.Turn.Radius;
    private float TurnDecelDistance => _config.Turn.DecelDistance;
    private float TurnCurveSpeedFactor => _config.Turn.CurveSpeedFactor;

    private void TickRoadWalk(SkycraftInstance s, ProfileSettings p, float delta)
    {
        float cruiseY = s.CruiseY;

        if (s.HasTurnPending)
        {
            TickTurningCruise(s, p, delta, cruiseY);
            return;
        }

        // === Normal axis-locked cruise (no pending turn) ===
        float lanePerp = LaneOffsetPerp(s);

        // Hard-lock perpendicular axis: position to centerline+lane, velocity to 0.
        // Any non-zero perpendicular velocity would produce diagonal motion off the road.
        if (s.RoadDir == RoadDirection.EastWest)
        {
            s.Z = s.RoadPos + lanePerp;
            s.Vz = 0f;
            s.ForwardX = s.MovingPositive ? 1f : -1f;
            s.ForwardY = 0f;
            s.ForwardZ = 0f;
        }
        else
        {
            s.X = s.RoadPos + lanePerp;
            s.Vx = 0f;
            s.ForwardX = 0f;
            s.ForwardY = 0f;
            s.ForwardZ = s.MovingPositive ? 1f : -1f;
        }

        // Accelerate cruise-axis velocity toward target speed (capped).
        float targetVel = (s.MovingPositive ? 1f : -1f) * p.Speed;
        float cap = p.Acceleration * delta;
        if (s.RoadDir == RoadDirection.EastWest)
        {
            float dv = targetVel - s.Vx;
            if (MathF.Abs(dv) > cap) dv = MathF.Sign(dv) * cap;
            s.Vx += dv;
            s.X += s.Vx * delta;
        }
        else
        {
            float dv = targetVel - s.Vz;
            if (MathF.Abs(dv) > cap) dv = MathF.Sign(dv) * cap;
            s.Vz += dv;
            s.Z += s.Vz * delta;
        }

        // Y axis: lerp toward cruise altitude. Independent of cruise axis.
        float dy = cruiseY - s.Y;
        float yStep = p.Speed * delta * 0.5f;
        s.Y += MathF.Sign(dy) * MathF.Min(MathF.Abs(dy), yStep);
        s.Vy = 0f;

        PlanTurnTowardTarget(s);
        // No instant-reverse on overshoot: a snap-flip of MovingPositive swaps
        // the right-hand lane (≈4m perpendicular jump). Instead we trust
        // PlanTurnTowardTarget to plan a U-loop via two perpendicular turns at
        // upcoming intersections. If no turns appear in lookahead (e.g. distant
        // unloaded chunks), the Skycraft will simply cruise forward and despawn
        // at the active-region edge — acceptable for an ambient system.
    }

    /// <summary>
    /// Two-phase smooth turn that preserves right-hand traffic across the arc:
    ///   1. <b>Decel approach</b> — Skycraft cruises in the old lane toward curve
    ///      entry. Speed lerps from <c>p.Speed</c> down to curve speed over
    ///      <see cref="TurnDecelDistance"/>. Perpendicular position stays in the lane.
    ///   2. <b>Quarter-circle arc</b> — runs from old-road lane to new-road lane.
    ///      Because the entry and exit are both off-centerline (in their respective
    ///      lanes), the arc radius differs by ±laneOffset depending on whether the
    ///      turn is left (CCW, wider) or right (CW, tighter). Exit position equals
    ///      the new road's lane exactly, so resumed axis-locked cruise has no snap.
    /// </summary>
    private void TickTurningCruise(SkycraftInstance s, ProfileSettings p, float delta, float cruiseY)
    {
        RoadDirection newDir = s.RoadDir == RoadDirection.EastWest
            ? RoadDirection.NorthSouth
            : RoadDirection.EastWest;
        float newRoadPos = s.RoadDir == RoadDirection.EastWest ? s.TurnAtX : s.TurnAtZ;
        bool newMovingPos = newDir == RoadDirection.EastWest
            ? s.Target.X > s.TurnAtX
            : s.Target.Z > s.TurnAtZ;

        float oldAxisDir = s.MovingPositive ? 1f : -1f;
        float newAxisDir = newMovingPos ? 1f : -1f;

        // Lane offsets at entry (old road) and exit (new road) — right-hand traffic.
        float oldLane = ComputeLaneOffset(s.RoadDir, s.MovingPositive, s.RoadHalfWidth);
        float newRoadHalfWidth = s.NextRoadHalfWidth > 0f ? s.NextRoadHalfWidth : s.RoadHalfWidth;
        float newLane = ComputeLaneOffset(newDir, newMovingPos, newRoadHalfWidth);
        float laneMag = MathF.Abs(oldLane);

        // Enter and exit tangents drive arc direction (CCW = left turn, CW = right turn).
        float enterTX = s.RoadDir == RoadDirection.EastWest ? oldAxisDir : 0f;
        float enterTZ = s.RoadDir == RoadDirection.EastWest ? 0f : oldAxisDir;
        float exitTX = newDir == RoadDirection.EastWest ? newAxisDir : 0f;
        float exitTZ = newDir == RoadDirection.EastWest ? 0f : newAxisDir;
        float arcDir = (enterTX * exitTZ - enterTZ * exitTX) >= 0f ? 1f : -1f;

        // A right-hand-traffic left turn sweeps wider (entry lane is far from corner,
        // exit lane is close); right turn sweeps tighter. Effective radius adjusts.
        float effectiveRadius = arcDir > 0f
            ? TurnRadius + laneMag
            : MathF.Max(1f, TurnRadius - laneMag);

        // Entry/exit on their respective lanes, TurnRadius back from corner along
        // their respective cruise axes.
        float entryX, entryZ, exitX, exitZ;
        if (s.RoadDir == RoadDirection.EastWest)
        {
            entryX = s.TurnAtX - oldAxisDir * TurnRadius;
            entryZ = s.RoadPos + oldLane;
            exitX = s.TurnAtX + newLane;
            exitZ = s.RoadPos + newAxisDir * TurnRadius;
        }
        else
        {
            entryX = s.RoadPos + oldLane;
            entryZ = s.TurnAtZ - oldAxisDir * TurnRadius;
            exitX = s.RoadPos + newAxisDir * TurnRadius;
            exitZ = s.TurnAtZ + newLane;
        }

        // Center sits perpendicular to enter tangent, on the arc side (left if CCW,
        // right if CW), at distance effectiveRadius.
        float perpEntryX, perpEntryZ;
        if (arcDir > 0f) { perpEntryX = -enterTZ; perpEntryZ = enterTX; }
        else { perpEntryX = enterTZ; perpEntryZ = -enterTX; }
        float centerX = entryX + effectiveRadius * perpEntryX;
        float centerZ = entryZ + effectiveRadius * perpEntryZ;

        bool atEntry = s.RoadDir == RoadDirection.EastWest
            ? (s.MovingPositive ? s.X >= entryX : s.X <= entryX)
            : (s.MovingPositive ? s.Z >= entryZ : s.Z <= entryZ);

        float curveSpeed = p.Speed * TurnCurveSpeedFactor;

        // --------- Phase 1: decel on the old lane (no lateral drift) ---------
        if (!atEntry)
        {
            float distToEntry = s.RoadDir == RoadDirection.EastWest
                ? (s.MovingPositive ? entryX - s.X : s.X - entryX)
                : (s.MovingPositive ? entryZ - s.Z : s.Z - entryZ);

            float speedTarget = distToEntry >= TurnDecelDistance
                ? p.Speed
                : curveSpeed + (p.Speed - curveSpeed) * (distToEntry / TurnDecelDistance);

            // Stay locked on the old lane throughout decel.
            if (s.RoadDir == RoadDirection.EastWest)
            {
                s.Z = s.RoadPos + oldLane;
                s.Vz = 0f;
                s.ForwardX = oldAxisDir;
                s.ForwardY = 0f;
                s.ForwardZ = 0f;
            }
            else
            {
                s.X = s.RoadPos + oldLane;
                s.Vx = 0f;
                s.ForwardX = 0f;
                s.ForwardY = 0f;
                s.ForwardZ = oldAxisDir;
            }

            float brakeCap = p.Acceleration * 2f * delta;
            float targetVel = oldAxisDir * speedTarget;
            if (s.RoadDir == RoadDirection.EastWest)
            {
                float dv = targetVel - s.Vx;
                if (MathF.Abs(dv) > brakeCap) dv = MathF.Sign(dv) * brakeCap;
                s.Vx += dv;
                s.X += s.Vx * delta;
            }
            else
            {
                float dv = targetVel - s.Vz;
                if (MathF.Abs(dv) > brakeCap) dv = MathF.Sign(dv) * brakeCap;
                s.Vz += dv;
                s.Z += s.Vz * delta;
            }

            float dy = cruiseY - s.Y;
            float yStep = p.Speed * delta * 0.5f;
            s.Y += MathF.Sign(dy) * MathF.Min(MathF.Abs(dy), yStep);
            s.Vy = 0f;
            return;
        }

        // --------- Phase 2: quarter-circle arc, lane-to-lane ---------
        float entryAngle = MathF.Atan2(entryZ - centerZ, entryX - centerX);
        float curAngle = MathF.Atan2(s.Z - centerZ, s.X - centerX);

        float angStep = (curveSpeed * delta) / effectiveRadius * arcDir;
        float nextAngle = curAngle + angStep;
        float nextSwept = NormalizeRadians(nextAngle - entryAngle) * arcDir;

        const float QuarterTurn = MathF.PI * 0.5f;

        if (nextSwept >= QuarterTurn - 0.005f)
        {
            // Land exactly on the new lane — no post-turn snap.
            s.X = exitX;
            s.Z = exitZ;
            s.RoadDir = newDir;
            s.RoadPos = newRoadPos;
            s.RoadHalfWidth = newRoadHalfWidth;
            s.MovingPositive = newMovingPos;
            s.HasTurnPending = false;

            if (newDir == RoadDirection.EastWest)
            {
                s.Vx = newAxisDir * curveSpeed;
                s.Vz = 0f;
                s.ForwardX = newAxisDir;
                s.ForwardY = 0f;
                s.ForwardZ = 0f;
            }
            else
            {
                s.Vz = newAxisDir * curveSpeed;
                s.Vx = 0f;
                s.ForwardX = 0f;
                s.ForwardY = 0f;
                s.ForwardZ = newAxisDir;
            }

            PlanTurnTowardTarget(s);
            return;
        }

        // Mid-arc: position is analytic on the circle; velocity stays tangent.
        s.X = centerX + effectiveRadius * MathF.Cos(nextAngle);
        s.Z = centerZ + effectiveRadius * MathF.Sin(nextAngle);

        float tx, tz;
        if (arcDir > 0f)
        {
            tx = -MathF.Sin(nextAngle);
            tz = MathF.Cos(nextAngle);
        }
        else
        {
            tx = MathF.Sin(nextAngle);
            tz = -MathF.Cos(nextAngle);
        }
        s.Vx = tx * curveSpeed;
        s.Vy = 0f;
        s.Vz = tz * curveSpeed;
        s.ForwardX = tx;
        s.ForwardY = 0f;
        s.ForwardZ = tz;

        float dyArc = cruiseY - s.Y;
        float yStepArc = p.Speed * delta * 0.5f;
        s.Y += MathF.Sign(dyArc) * MathF.Min(MathF.Abs(dyArc), yStepArc);
    }

    // Constant lane offset, sized to fit even the narrowest road. Using a constant
    // (rather than a fraction of road half-width) keeps the turn arc geometry exact
    // when turning between roads of different widths — a circular arc with
    // axis-aligned tangents at both ends requires equal lane offsets at entry and
    // exit, which only holds when the offset is constant.

    /// <summary>
    /// Right-hand-traffic lane offset, on the right of the motion direction.
    /// Clamped to fit inside the road surface (with a small margin from the curb).
    /// </summary>
    private float ComputeLaneOffset(RoadDirection dir, bool movingPositive, float halfWidth)
    {
        if (halfWidth <= 0.5f) return 0f;
        float mag = MathF.Min(_config.Turn.LaneOffsetMagnitude, halfWidth - 0.5f);
        if (dir == RoadDirection.EastWest)
            return movingPositive ? -mag : mag;
        else
            return movingPositive ? mag : -mag;
    }

    private static float NormalizeRadians(float a)
    {
        while (a > MathF.PI) a -= 2f * MathF.PI;
        while (a < -MathF.PI) a += 2f * MathF.PI;
        return a;
    }

    /// <summary>
    /// Right-hand-traffic lane offset for the current instance's road and direction.
    /// </summary>
    private float LaneOffsetPerp(SkycraftInstance s)
        => ComputeLaneOffset(s.RoadDir, s.MovingPositive, s.RoadHalfWidth);

    /// <summary>
    /// Picks the best perpendicular intersection ahead given the target's road.
    /// Two cases:
    ///   • <b>Perpendicular</b> (current road ⟂ target's road): the target road
    ///     crosses ours at exactly one point. Want a turn at that crossing —
    ///     scored by how close the perpendicular segment's position is to
    ///     <c>TargetRoadPos</c>.
    ///   • <b>Parallel</b> (current road ∥ target's road): need two turns. The
    ///     first turn should land on a perpendicular road close to target's
    ///     cruise-axis coord so the subsequent cruise (along the perpendicular
    ///     axis to target's road's perpendicular position) is short.
    /// If no <c>TargetRoadDir</c> is resolved yet, no turn is queued.
    /// </summary>
    private void PlanTurnTowardTarget(SkycraftInstance s)
    {
        if (s.HasTurnPending) return;
        if (!s.HasTarget) return;
        if (!s.HasTargetRoad) return;

        // Already on target's road? If target is still ahead on the cruise axis,
        // cruise straight to it (no turn needed). If target is *behind* us — we
        // overshot or repath put it behind — fall through to plan a turn at the
        // next intersection so we can loop back via the grid.
        bool onTargetRoad = s.RoadDir == s.TargetRoadDir
            && MathF.Abs(s.RoadPos - s.TargetRoadPos) < 6f;
        if (onTargetRoad)
        {
            float curCruise = s.RoadDir == RoadDirection.EastWest ? s.X : s.Z;
            float targetCruise = s.RoadDir == RoadDirection.EastWest ? s.Target.X : s.Target.Z;
            bool targetAhead = s.MovingPositive ? targetCruise > curCruise : targetCruise < curCruise;
            if (targetAhead) return;
            // else: fall through. The fall-through path's parallel-case score still uses
            // target's cruise coord; the best intersection ahead becomes the first leg
            // of the U-loop, after which the next PlanTurn pass routes back to target.
        }

        const float Lookahead = 160f;

        // Score axis: what does seg.Position need to be close to?
        //   Parallel case: target's cruise coord — so the new road's position
        //     (on the original cruise axis) is near target.X (or .Z).
        //   Perpendicular case: TargetRoadPos — pick the perpendicular segment
        //     that IS target's road.
        bool parallel = s.RoadDir == s.TargetRoadDir;
        float scoreCoord = parallel
            ? (s.RoadDir == RoadDirection.EastWest ? s.Target.X : s.Target.Z)
            : s.TargetRoadPos;

        float bestScore = float.MaxValue;
        float bestX = 0f, bestZ = 0f;
        float bestHalfWidth = 0f;
        bool found = false;

        // Probe 4 steps ahead so we cover ~1.5–2 chunks along the cruise axis.
        // Each step queries one chunk; road lists per chunk are tiny.
        for (int step = 1; step <= 4; step++)
        {
            float probeDist = step * (Lookahead / 4f);
            float probeX = s.X, probeZ = s.Z;
            if (s.RoadDir == RoadDirection.EastWest)
                probeX = s.X + (s.MovingPositive ? 1f : -1f) * probeDist;
            else
                probeZ = s.Z + (s.MovingPositive ? 1f : -1f) * probeDist;

            var chunk = _chunkManager.GetChunkAtPosition(probeX, probeZ);
            if (chunk == null) continue;

            // Confirm our current road actually exists in this probe chunk before
            // treating any of its perpendicular segments as a real crossing. Without
            // this we'd plan a turn arc through empty space if our road terminated
            // before the probe chunk.
            bool ourRoadInChunk = false;
            foreach (var seg in chunk.Roads)
            {
                if (seg.Direction == s.RoadDir && MathF.Abs(seg.Position - s.RoadPos) < 0.5f)
                {
                    ourRoadInChunk = true;
                    break;
                }
            }
            if (!ourRoadInChunk) continue;

            foreach (var seg in chunk.Roads)
            {
                if (seg.Direction == s.RoadDir) continue; // not a perpendicular crossing

                // Intersection of our current road and this perpendicular segment.
                // Our current road's perpendicular coord = s.RoadPos.
                // The perpendicular segment's perpendicular coord = seg.Position.
                // For an EW road (us) crossing a NS road (them): intersection is at
                // (seg.Position, s.RoadPos). And vice versa.
                float ix, iz;
                if (s.RoadDir == RoadDirection.EastWest)
                {
                    ix = seg.Position;   // NS road's X
                    iz = s.RoadPos;      // our EW road's Z
                }
                else
                {
                    ix = s.RoadPos;      // our NS road's X
                    iz = seg.Position;   // EW road's Z
                }

                // Must lie ahead in cruise direction. Floor at TurnRadius+1 so the entry
                // point (which sits TurnRadius back from the corner along the cruise axis)
                // is still ahead of us — otherwise atEntry is true on the next tick and
                // Phase 1's decel is skipped, sending the craft through the 90° arc at
                // full cruise speed.
                float along = s.RoadDir == RoadDirection.EastWest
                    ? (s.MovingPositive ? (ix - s.X) : (s.X - ix))
                    : (s.MovingPositive ? (iz - s.Z) : (s.Z - iz));
                if (along < TurnRadius + 1f || along > Lookahead) continue;

                // Score: seg.Position is the perpendicular road's perpendicular coord.
                // After committing the turn, RoadPos becomes seg.Position. We want this
                // to match scoreCoord (target's cruise coord in parallel case, target's
                // road perpendicular position in perpendicular case).
                float gap = MathF.Abs(seg.Position - scoreCoord);

                if (gap < bestScore)
                {
                    bestScore = gap;
                    bestX = ix; bestZ = iz;
                    bestHalfWidth = seg.HalfWidth;
                    found = true;
                }
            }
        }

        if (found)
        {
            s.TurnAtX = bestX;
            s.TurnAtZ = bestZ;
            s.NextRoadHalfWidth = bestHalfWidth;
            s.HasTurnPending = true;
        }
    }

    /// <summary>
    /// Finds the nearest road segment to an arbitrary world position by scanning a
    /// 3×3 chunk window. Used by <see cref="TickFlyToRoad"/> for the off-road
    /// transit target and by <see cref="ResolveTargetRoad"/> for the cruise
    /// destination.
    /// </summary>
    private bool TryFindNearestRoad(float x, float z,
        out RoadDirection dir, out float pos, out float halfWidth)
    {
        float chunkSize = _chunkManager.ChunkSize;
        RoadSegment? best = null;
        float bestDist = float.MaxValue;

        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            var chunk = _chunkManager.GetChunkAtPosition(x + dx * chunkSize * 0.5f, z + dz * chunkSize * 0.5f);
            if (chunk == null) continue;
            foreach (var seg in chunk.Roads)
            {
                float d = seg.Direction == RoadDirection.EastWest
                    ? MathF.Abs(z - seg.Position)
                    : MathF.Abs(x - seg.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = seg;
                }
            }
        }

        if (best == null)
        {
            dir = RoadDirection.EastWest;
            pos = 0f;
            halfWidth = 0f;
            return false;
        }

        dir = best.Value.Direction;
        pos = best.Value.Position;
        halfWidth = best.Value.HalfWidth;
        return true;
    }

    /// <summary>
    /// Off-road cruise transit: fly diagonally toward the nearest road's lane at
    /// cruise altitude using free-flight 3D, then engage road-walk once within
    /// half a lane of the target lane point. Used at initial spawn (far-edge
    /// position outside the road grid) and after takeoff from a landing (rooftop
    /// or road), bridging from arbitrary world position to the road grid without
    /// a teleport.
    ///
    /// Safety note: this is the one path where a grid-routed craft moves off the
    /// road structure. For Commuter post-rooftop-takeoff the craft is at cruise
    /// altitude (110–150m) above the rooftop POI (which is already constrained
    /// below cruiseMin-5m), and the diagonal hop to the nearest road is short
    /// (typically &lt; 20m horizontal). Taller neighbours can in principle be
    /// clipped — same caveat the ADR records for Express (downtown super-towers).
    /// </summary>
    private void TickFlyToRoad(SkycraftInstance s, ProfileSettings p, float delta)
    {
        if (!TryFindNearestRoad(s.X, s.Z, out var dir, out var pos, out var hw))
        {
            // No road in the 3x3 chunk window — chunks haven't streamed yet.
            // Coast on the current velocity; the despawn radius collects this
            // craft if chunks never load.
            CoastForward(s, delta);
            return;
        }

        // Lane offset is derived from the direction the craft will be heading
        // once it engages road-walk: tentativeMovingPos points toward the target
        // along the road's cruise axis. This way the fly-to-road target lane is
        // the *correct* lane for the upcoming cruise, with no post-snap hop.
        bool tentativeMovingPos = s.HasTarget
            && (dir == RoadDirection.EastWest ? s.Target.X > s.X : s.Target.Z > s.Z);
        float lane = ComputeLaneOffset(dir, tentativeMovingPos, hw);
        float roadX = s.X, roadZ = s.Z;
        if (dir == RoadDirection.EastWest)
            roadZ = pos + lane;
        else
            roadX = pos + lane;

        // Engage road-walk once within half a lane of the target lane point. Y
        // alignment is not required — TickRoadWalk's own Y-lerp pulls altitude
        // to cruiseY over subsequent ticks.
        const float SnapThreshold = 0.5f;
        float perpDistSq = HorizontalDistSq(s.X, s.Z, roadX, roadZ);
        if (perpDistSq < SnapThreshold * SnapThreshold)
        {
            s.RoadDir = dir;
            s.RoadPos = pos;
            s.RoadHalfWidth = hw;
            s.MovingPositive = tentativeMovingPos;
            s.OnRoad = true;
            // Absorb the sub-meter delta so the first TickRoadWalk hard-lock is invisible.
            if (dir == RoadDirection.EastWest) s.Z = pos + lane;
            else s.X = pos + lane;
            ResolveTargetRoad(s);
            PlanTurnTowardTarget(s);
            return;
        }

        MoveToward(s, roadX, s.CruiseY, roadZ, p.Speed, p.Acceleration, delta);
    }

    /// <summary>
    /// Records the road segment closest to the target POI. The cruise will end on
    /// this road (not at the target's literal coords) — for road landings the POI
    /// is already on a road so this matches; for rooftop landings it's the road
    /// bordering the plot, and the approach phase covers the short final leg.
    /// </summary>
    private void ResolveTargetRoad(SkycraftInstance s)
    {
        if (TryFindNearestRoad(s.Target.X, s.Target.Z, out var dir, out var pos, out _))
        {
            s.TargetRoadDir = dir;
            s.TargetRoadPos = pos;
            s.HasTargetRoad = true;
        }
        else
        {
            s.HasTargetRoad = false;
        }
    }

    private bool PickFromKinds(out SkycraftPoi poi, params SkycraftPoiKind[] kinds)
    {
        // Pick a kind whose pool is non-empty; if multiple have entries, weight by population.
        int total = 0;
        foreach (var k in kinds) total += _registry.CountOf(k);
        if (total == 0) { poi = default; return false; }

        int pick = _rng.Next(total);
        foreach (var k in kinds)
        {
            int n = _registry.CountOf(k);
            if (pick < n)
            {
                return _registry.TryPickRandom(k, _rng, out poi);
            }
            pick -= n;
        }
        poi = default; return false;
    }

    private static bool Fail(out SkycraftPoi poi) { poi = default; return false; }

    private ProfileSettings GetProfileSettings(SkycraftProfile profile) => profile switch
    {
        SkycraftProfile.Express => _config.Express,
        SkycraftProfile.Commuter => _config.Commuter,
        SkycraftProfile.Drifter => _config.Drifter,
        _ => _config.Commuter,
    };

    // ---- debug / introspection --------------------------------------------

    public int ActiveCount => _active.Count;
    public int EffectivePoolCap => _effectivePoolCap;

    public void LogStats()
    {
        Log.Debug(LogCategory.Sim, $"Skycraft active={_active.Count} cap={_effectivePoolCap} " +
            $"E={_activePerProfile[0]} C={_activePerProfile[1]} D={_activePerProfile[2]}");
    }
}
