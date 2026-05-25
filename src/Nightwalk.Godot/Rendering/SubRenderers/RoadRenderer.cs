using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.World.Roads;
using Nightwalk.Godot.Rendering;
using Nightwalk.Godot.Rendering.Constants;
using Nightwalk.Godot.Rendering.Geometry;

namespace Nightwalk.Godot.Rendering.SubRenderers;

/// <summary>
/// Renders roads, road markings, and crosswalks for a chunk.
/// </summary>
public sealed class RoadRenderer : ISubRenderer, IPhasedSubRenderer
{
    public ChunkRenderPhase Phase => ChunkRenderPhase.Roads;
    private readonly RoadConfig _roadConfig;
    private readonly RoadMarkingsConfig _markingsConfig;

    public RoadRenderer(RoadConfig roadConfig, RoadMarkingsConfig markingsConfig)
    {
        _roadConfig = roadConfig;
        _markingsConfig = markingsConfig;
    }

    public void Render(Node3D parent, ChunkData data, ChunkRenderContext context)
    {
        // Roads are fast to render (limited road count, simple geometry)
        // so we don't need full incremental support, just call directly
        RenderRoads(parent, data, context);
    }

    private void RenderRoads(Node3D parent, ChunkData data, ChunkRenderContext context)
    {
        if (data.Roads.Count == 0) return;

        var roadBuilder = new MeshBuilder();
        var markingsBuilder = new MeshBuilder();

        var roadHeight = _roadConfig.RoadHeight;
        var markingHeight = roadHeight + RenderingConstants.RoadMarkingHeightOffset;

        // Separate roads by direction
        var ewRoads = new List<RoadSegment>(data.Roads.Count);
        var nsRoads = new List<RoadSegment>(data.Roads.Count);
        foreach (var road in data.Roads)
        {
            if (road.Direction == RoadDirection.EastWest)
                ewRoads.Add(road);
            else if (road.Direction == RoadDirection.NorthSouth)
                nsRoads.Add(road);
        }

        // Process EastWest roads (full length)
        foreach (var road in ewRoads)
        {
            var localZ = road.Position - data.Coord.Z * data.Size;
            roadBuilder.AddTopQuad(data.Size / 2f, roadHeight, localZ, data.Size, road.Width);

            if (_markingsConfig.Enabled)
            {
                CreateRoadMarkings(markingsBuilder, data, 0, data.Size, localZ, markingHeight, isEastWest: true, nsRoads);
            }
        }

        // Process NorthSouth roads (segmented to avoid overlap)
        foreach (var road in nsRoads)
        {
            var localX = road.Position - data.Coord.X * data.Size;
            CreateRoadSegments(roadBuilder, ewRoads, data.Coord.Z * data.Size, data.Size, roadHeight, localX, road.Width);

            if (_markingsConfig.Enabled)
            {
                CreateRoadMarkings(markingsBuilder, data, 0, data.Size, localX, markingHeight, isEastWest: false, ewRoads);
            }
        }

        // Add crosswalks at intersections
        if (_markingsConfig.Enabled)
        {
            foreach (var ewRoad in ewRoads)
            {
                foreach (var nsRoad in nsRoads)
                {
                    var localX = nsRoad.Position - data.Coord.X * data.Size;
                    var localZ = ewRoad.Position - data.Coord.Z * data.Size;
                    CreateCrosswalks(markingsBuilder, nsRoad.Position, ewRoad.Position, localX, localZ, nsRoad.HalfWidth, ewRoad.HalfWidth, markingHeight);
                }
            }
        }

        // Create road mesh
        var roadArrays = roadBuilder.ToSurfaceArrays();
        if (roadArrays != null)
        {
            var roadMesh = new ArrayMesh();
            roadMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, roadArrays);
            roadMesh.SurfaceSetMaterial(0, context.WetRoadMaterial);

            var roadInstance = context.NodePool.GetMeshInstance("Roads");
            roadInstance.Mesh = roadMesh;
            parent.AddChild(roadInstance);
        }

        // Create road markings mesh with visibility LOD
        var markingsArrays = markingsBuilder.ToSurfaceArrays();
        if (markingsArrays != null)
        {
            var markingsMesh = new ArrayMesh();
            markingsMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, markingsArrays);
            markingsMesh.SurfaceSetMaterial(0, context.RoadMarkingMaterial);

            var markingsInstance = context.NodePool.GetMeshInstance("RoadMarkings");
            markingsInstance.Mesh = markingsMesh;

            // Apply visibility range LOD - markings fade out at distance
            markingsInstance.VisibilityRangeEnd = _markingsConfig.LodVisibilityRange;
            markingsInstance.VisibilityRangeEndMargin = _markingsConfig.LodVisibilityMargin;
            markingsInstance.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;

            parent.AddChild(markingsInstance);
        }
    }

    private void CreateRoadMarkings(MeshBuilder builder, ChunkData data, float start, float end, float roadCenter, float markingHeight, bool isEastWest, List<RoadSegment> crossingRoads)
    {
        var dashLength = _markingsConfig.DashLength;
        var dashGap = _markingsConfig.DashGap;
        var lineWidth = _markingsConfig.CenterLineWidth * RenderingConstants.CenterLineWidthMultiplier;

        float worldOffsetX = data.Coord.X * data.Size;
        float worldOffsetZ = data.Coord.Z * data.Size;
        float worldOffset = isEastWest ? worldOffsetX : worldOffsetZ;

        // Calculate crosswalk zone extension
        var stripeCount = _markingsConfig.CrosswalkStripeCount;
        var stripeWidth = _markingsConfig.CrosswalkStripeWidth;
        var stripeGap = _markingsConfig.CrosswalkStripeGap;
        float crosswalkDepth = stripeCount * stripeWidth + (stripeCount - 1) * stripeGap;
        float crosswalkExtension = crosswalkDepth + dashLength;

        // Build gap ranges
        var gapRanges = new List<(float start, float end)>(crossingRoads.Count);
        foreach (var crossingRoad in crossingRoads)
        {
            var localPos = crossingRoad.Position - worldOffset;

            float worldX, worldZ;
            if (isEastWest)
            {
                worldX = crossingRoad.Position;
                worldZ = roadCenter + worldOffsetZ;
            }
            else
            {
                worldX = roadCenter + worldOffsetX;
                worldZ = crossingRoad.Position;
            }

            var (hasCrosswalk, isNorthSouth) = GetCrosswalkInfo(worldX, worldZ);
            bool crosswalkBlocksMarkings = hasCrosswalk && (isEastWest ? !isNorthSouth : isNorthSouth);

            if (crosswalkBlocksMarkings)
            {
                gapRanges.Add((localPos - crossingRoad.HalfWidth - crosswalkExtension,
                               localPos + crossingRoad.HalfWidth + crosswalkExtension));
            }
            else
            {
                gapRanges.Add((localPos - crossingRoad.HalfWidth, localPos + crossingRoad.HalfWidth));
            }
        }
        gapRanges.Sort((a, b) => a.start.CompareTo(b.start));

        // Create dashed line
        float dashCycle = dashLength + dashGap;
        float worldStart = worldOffset + start;
        float alignedStart = Mathf.Floor(worldStart / dashCycle) * dashCycle - worldOffset;

        while (alignedStart < start)
            alignedStart += dashCycle;

        float pos = alignedStart;
        int gapIndex = 0;

        while (pos < end)
        {
            float dashStart = pos;
            float dashEnd = Mathf.Min(pos + dashLength, end);

            while (gapIndex < gapRanges.Count && gapRanges[gapIndex].end <= dashStart)
                gapIndex++;

            bool inGap = gapIndex < gapRanges.Count &&
                         dashStart < gapRanges[gapIndex].end &&
                         dashEnd > gapRanges[gapIndex].start;

            if (!inGap && dashEnd > dashStart)
            {
                float dashCenter = (dashStart + dashEnd) / 2f;
                float dashSize = dashEnd - dashStart;

                if (isEastWest)
                    builder.AddTopQuad(dashCenter, markingHeight, roadCenter, dashSize, lineWidth);
                else
                    builder.AddTopQuad(roadCenter, markingHeight, dashCenter, lineWidth, dashSize);
            }

            pos += dashCycle;
        }
    }

    private (bool hasCrosswalk, bool isNorthSouth) GetCrosswalkInfo(float worldX, float worldZ)
    {
        int gridX = (int)MathF.Round(worldX / RenderingConstants.IntersectionGridSpacing);
        int gridZ = (int)MathF.Round(worldZ / RenderingConstants.IntersectionGridSpacing);
        var rng = new Random(gridX + gridZ * RenderingConstants.IntersectionSeedZMultiplier);
        bool hasCrosswalk = rng.NextDouble() < _markingsConfig.CrosswalkChance;
        bool isNorthSouth = rng.NextDouble() > 0.5;
        return (hasCrosswalk, isNorthSouth);
    }

    private void CreateCrosswalks(MeshBuilder builder, float worldX, float worldZ, float localX, float localZ, float nsRoadHalfWidth, float ewRoadHalfWidth, float markingHeight)
    {
        var (hasCrosswalks, doNorthSouth) = GetCrosswalkInfo(worldX, worldZ);
        if (!hasCrosswalks) return;

        var stripeWidth = _markingsConfig.CrosswalkStripeWidth;
        var stripeGap = _markingsConfig.CrosswalkStripeGap;
        var stripeCount = _markingsConfig.CrosswalkStripeCount;

        float crosswalkWidth = stripeCount * stripeWidth + (stripeCount - 1) * stripeGap;

        if (doNorthSouth)
        {
            float roadWidth = nsRoadHalfWidth * 2f - RenderingConstants.CrosswalkRoadWidthMargin;
            int actualStripeCount = (int)(roadWidth / (stripeWidth + stripeGap));
            float totalStripesWidth = actualStripeCount * stripeWidth + (actualStripeCount - 1) * stripeGap;
            float startOffset = -totalStripesWidth / 2f + stripeWidth / 2f;
            float stripeDepth = crosswalkWidth;

            float northZ = localZ - ewRoadHalfWidth - stripeDepth / 2f - RenderingConstants.CrosswalkPositionOffset;
            for (int i = 0; i < actualStripeCount; i++)
            {
                float stripeX = localX + startOffset + i * (stripeWidth + stripeGap);
                builder.AddTopQuad(stripeX, markingHeight, northZ, stripeWidth, stripeDepth);
            }

            float southZ = localZ + ewRoadHalfWidth + stripeDepth / 2f + RenderingConstants.CrosswalkPositionOffset;
            for (int i = 0; i < actualStripeCount; i++)
            {
                float stripeX = localX + startOffset + i * (stripeWidth + stripeGap);
                builder.AddTopQuad(stripeX, markingHeight, southZ, stripeWidth, stripeDepth);
            }
        }
        else
        {
            float roadWidth = ewRoadHalfWidth * 2f - RenderingConstants.CrosswalkRoadWidthMargin;
            int actualStripeCount = (int)(roadWidth / (stripeWidth + stripeGap));
            float totalStripesWidth = actualStripeCount * stripeWidth + (actualStripeCount - 1) * stripeGap;
            float startOffset = -totalStripesWidth / 2f + stripeWidth / 2f;
            float stripeDepth = crosswalkWidth;

            float eastX = localX + nsRoadHalfWidth + stripeDepth / 2f + RenderingConstants.CrosswalkPositionOffset;
            for (int i = 0; i < actualStripeCount; i++)
            {
                float stripeZ = localZ + startOffset + i * (stripeWidth + stripeGap);
                builder.AddTopQuad(eastX, markingHeight, stripeZ, stripeDepth, stripeWidth);
            }

            float westX = localX - nsRoadHalfWidth - stripeDepth / 2f - RenderingConstants.CrosswalkPositionOffset;
            for (int i = 0; i < actualStripeCount; i++)
            {
                float stripeZ = localZ + startOffset + i * (stripeWidth + stripeGap);
                builder.AddTopQuad(westX, markingHeight, stripeZ, stripeDepth, stripeWidth);
            }
        }
    }

    private void CreateRoadSegments(MeshBuilder builder, List<RoadSegment> crossingRoads, float worldOffset, float chunkSize, float roadHeight, float roadCenterX, float roadWidth)
    {
        var gapRanges = new List<(float start, float end)>(crossingRoads.Count);
        foreach (var crossingRoad in crossingRoads)
        {
            var localPos = crossingRoad.Position - worldOffset;
            var gapHalf = crossingRoad.HalfWidth;
            gapRanges.Add((localPos - gapHalf, localPos + gapHalf));
        }
        gapRanges.Sort((a, b) => a.start.CompareTo(b.start));

        float currentStart = 0;

        foreach (var (gapStart, gapEnd) in gapRanges)
        {
            if (gapStart > currentStart)
            {
                float length = gapStart - currentStart;
                if (length > RenderingConstants.MinRoadSegmentLength)
                {
                    float centerZ = (currentStart + gapStart) / 2f;
                    builder.AddTopQuad(roadCenterX, roadHeight, centerZ, roadWidth, length);
                }
            }
            currentStart = Mathf.Max(currentStart, gapEnd);
        }

        if (currentStart < chunkSize)
        {
            float length = chunkSize - currentStart;
            if (length > RenderingConstants.MinRoadSegmentLength)
            {
                float centerZ = (currentStart + chunkSize) / 2f;
                builder.AddTopQuad(roadCenterX, roadHeight, centerZ, roadWidth, length);
            }
        }
    }

    public bool RenderIncremental(ChunkRenderState state, long budgetUsec)
    {
        if (state.ChunkNode == null || state.Context == null) return true;

        // Roads are fast (limited count, simple geometry) - no incremental needed
        RenderRoads(state.ChunkNode, state.Data, state.Context);
        return true;
    }
}
