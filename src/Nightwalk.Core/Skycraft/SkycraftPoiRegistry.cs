using System;
using System.Collections.Generic;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;
using Nightwalk.Core.World.Buildings;
using Nightwalk.Core.World.Roads;

namespace Nightwalk.Core.Skycraft;

/// <summary>
/// Holds the deterministically derived Skycraft POIs for every currently loaded chunk.
/// Populated via <see cref="OnChunkLoaded"/> and pruned via <see cref="OnChunkUnloaded"/>.
/// Per ADR-0016 §4: rooftop and road landing POIs are filtered from <c>ChunkData</c>;
/// sky waypoints have no existing geometry and are sampled per chunk by this registry.
/// </summary>
public sealed class SkycraftPoiRegistry
{
    private readonly SkycraftConfig _config;
    private readonly float _chunkSize;

    // POIs per chunk. Index 0..3 mirror SkycraftPoiKind.
    private readonly Dictionary<ChunkCoord, List<SkycraftPoi>[]> _poisByChunk = new();

    // Flat pickable lists by kind across all loaded chunks, kept in sync for fast random picks.
    private readonly List<SkycraftPoi>[] _allByKind = new[]
    {
        new List<SkycraftPoi>(64), // ExpressWaypoint
        new List<SkycraftPoi>(64), // CommuterWaypoint
        new List<SkycraftPoi>(64), // RooftopLanding
        new List<SkycraftPoi>(64), // RoadLanding
    };

    public SkycraftPoiRegistry(SkycraftConfig config, float chunkSize)
    {
        _config = config;
        _chunkSize = chunkSize;
    }

    /// <summary>
    /// Total POIs of a given kind across all currently-loaded chunks.
    /// </summary>
    public int CountOf(SkycraftPoiKind kind) => _allByKind[(int)kind].Count;

    /// <summary>
    /// Whether the chunk owning the given POI is still loaded.
    /// </summary>
    public bool IsValid(in SkycraftPoi poi) => _poisByChunk.ContainsKey(poi.Chunk);

    /// <summary>
    /// Picks a random POI of the given kind. Returns false if none are loaded.
    /// </summary>
    public bool TryPickRandom(SkycraftPoiKind kind, Random rng, out SkycraftPoi poi)
    {
        var list = _allByKind[(int)kind];
        if (list.Count == 0)
        {
            poi = default;
            return false;
        }
        poi = list[rng.Next(list.Count)];
        return true;
    }

    /// <summary>
    /// Random pick from a kind whose Y is below <paramref name="maxY"/>. Lets Commuter
    /// select only rooftops below its cruise band so the approach descent doesn't pass
    /// through a taller neighbouring tower.
    /// </summary>
    public bool TryPickRandomBelow(SkycraftPoiKind kind, float maxY, Random rng, out SkycraftPoi poi)
    {
        var list = _allByKind[(int)kind];
        if (list.Count == 0) { poi = default; return false; }

        // Reservoir sample over the filtered subset to avoid building a temporary list.
        int seen = 0;
        SkycraftPoi chosen = default;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Y >= maxY) continue;
            seen++;
            if (rng.Next(seen) == 0) chosen = list[i];
        }
        if (seen == 0) { poi = default; return false; }
        poi = chosen;
        return true;
    }

    /// <summary>
    /// Populates POIs for a newly-loaded chunk. Idempotent.
    /// </summary>
    public void OnChunkLoaded(ChunkCoord coord, ChunkData data)
    {
        if (_poisByChunk.ContainsKey(coord)) return;

        var perKind = new List<SkycraftPoi>[4]
        {
            new(_config.Poi.ExpressWaypointsPerChunk),
            new(_config.Poi.CommuterWaypointsPerChunk),
            new(4),
            new(4),
        };

        // Salt the chunk seed so we don't collide with other module RNGs.
        var rng = new Random(StableHash.Combine(data.ChunkSeed, 0x5A4F70F1));

        DeriveRooftopLandings(coord, data, rng, perKind[(int)SkycraftPoiKind.RooftopLanding]);
        DeriveRoadLandings(coord, data, rng, perKind[(int)SkycraftPoiKind.RoadLanding]);
        EmitSkyWaypoints(coord, rng, perKind);

        _poisByChunk[coord] = perKind;
        for (int k = 0; k < 4; k++)
        {
            _allByKind[k].AddRange(perKind[k]);
        }
    }

    public void OnChunkUnloaded(ChunkCoord coord)
    {
        if (!_poisByChunk.TryGetValue(coord, out var perKind)) return;
        _poisByChunk.Remove(coord);

        // Rebuild the flat lists for affected kinds. The pure-remove path would need
        // identity matching on every POI; rebuilding once per unload is cheaper and
        // keeps the representation simple. Per-unload cost is O(loaded_chunks ×
        // kinds_with_entries) — small at this game's chunk-streaming budget (a few
        // unloads per frame × ~50 loaded chunks × 4 POI kinds), but not the O(1)
        // the streaming budget alone would imply.
        for (int k = 0; k < 4; k++)
        {
            if (perKind[k].Count == 0) continue;
            _allByKind[k].Clear();
            foreach (var chunk in _poisByChunk.Values)
            {
                _allByKind[k].AddRange(chunk[k]);
            }
        }
    }

    public void Clear()
    {
        _poisByChunk.Clear();
        for (int k = 0; k < 4; k++) _allByKind[k].Clear();
    }

    private void DeriveRooftopLandings(ChunkCoord coord, ChunkData data, Random rng, List<SkycraftPoi> result)
    {
        var rule = _config.Poi;
        foreach (var b in data.Buildings)
        {
            float rooftopArea = b.Width * b.Depth;
            if (rooftopArea < rule.MinRooftopArea) continue;
            if (rng.NextDouble() > rule.RooftopMarkChance) continue;

            // For stepped buildings, land on the topmost section's center.
            float topY = b.Height;
            float cx = b.X;
            float cz = b.Z;
            if (b.Sections != null && b.Sections.Count > 0)
            {
                var top = b.Sections[^1];
                topY = top.EndY;
                cx = b.X + top.OffsetX;
                cz = b.Z + top.OffsetZ;
            }
            result.Add(new SkycraftPoi(cx, topY, cz, SkycraftPoiKind.RooftopLanding, coord));
        }
    }

    private void DeriveRoadLandings(ChunkCoord coord, ChunkData data, Random rng, List<SkycraftPoi> result)
    {
        var rule = _config.Poi;
        var (minX, minZ, maxX, maxZ) = data.GetWorldBounds();

        foreach (var seg in data.Roads)
        {
            if (rng.NextDouble() > rule.RoadMarkChance) continue;

            // Pick a random point along the segment's run inside this chunk.
            float t = (float)rng.NextDouble();
            float x, z;
            if (seg.Direction == RoadDirection.EastWest)
            {
                x = minX + t * (maxX - minX);
                z = seg.Position;
            }
            else
            {
                x = seg.Position;
                z = minZ + t * (maxZ - minZ);
            }
            // y=0 is street level in this project — landings sit on the road surface.
            result.Add(new SkycraftPoi(x, 0f, z, SkycraftPoiKind.RoadLanding, coord));
        }
    }

    private void EmitSkyWaypoints(ChunkCoord coord, Random rng, List<SkycraftPoi>[] perKind)
    {
        var (minX, minZ) = coord.ToWorldPosition(_chunkSize);
        float maxX = minX + _chunkSize;
        float maxZ = minZ + _chunkSize;

        EmitWaypointKind(SkycraftPoiKind.ExpressWaypoint, _config.Express, _config.Poi.ExpressWaypointsPerChunk,
                         coord, minX, maxX, minZ, maxZ, rng, perKind);
        EmitWaypointKind(SkycraftPoiKind.CommuterWaypoint, _config.Commuter, _config.Poi.CommuterWaypointsPerChunk,
                         coord, minX, maxX, minZ, maxZ, rng, perKind);
    }

    private static void EmitWaypointKind(
        SkycraftPoiKind kind, ProfileSettings profile, int count,
        ChunkCoord coord, float minX, float maxX, float minZ, float maxZ,
        Random rng, List<SkycraftPoi>[] perKind)
    {
        var list = perKind[(int)kind];
        for (int i = 0; i < count; i++)
        {
            float x = minX + (float)rng.NextDouble() * (maxX - minX);
            float z = minZ + (float)rng.NextDouble() * (maxZ - minZ);
            float y = profile.AltitudeMin + (float)rng.NextDouble() * (profile.AltitudeMax - profile.AltitudeMin);
            list.Add(new SkycraftPoi(x, y, z, kind, coord));
        }
    }
}
