using System;
using System.Collections.Generic;
using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Core.Determinism;
using Nightwalk.Core.World.Roads;
using Nightwalk.Godot.Rendering.Effects;

namespace Nightwalk.Godot.Rendering.SubRenderers;

/// <summary>
/// Renders manholes randomly scattered on roads with optional smoke effects.
/// Supports configurable cover patterns: concentric_rings, radial_lines, grid.
/// </summary>
public sealed class ManholeRenderer : ISubRenderer, IPhasedSubRenderer
{
    public ChunkRenderPhase Phase => ChunkRenderPhase.Manholes;

    private const int PurposeManholePlace = 7003;
    private const int PurposeManholeSmokeCheck = 7004;
    private const int PurposeManholeSmokeIntensity = 7005;
    private const int SegmentCount = 20;

    private readonly ManholeConfig _config;
    private readonly SmokeConfig _smokeConfig;
    private readonly RoadConfig _roadConfig;
    private readonly Color _materialColor;

    // Cached materials
    private StandardMaterial3D? _baseMaterial;
    private StandardMaterial3D? _ridgeMaterial;

    // Cached meshes (shared across all manholes - they're identical)
    private ArrayMesh? _cachedBaseMesh;
    private ArrayMesh? _cachedRidgeMesh;

    public ManholeRenderer(ManholeConfig config, SmokeConfig smokeConfig, RoadConfig roadConfig)
    {
        _config = config;
        _smokeConfig = smokeConfig;
        _roadConfig = roadConfig;
        _materialColor = Color.FromHtml(config.MaterialColor);
    }

    public void Render(Node3D parent, ChunkData data, ChunkRenderContext context)
    {
        if (!_config.Enabled || data.Roads.Count == 0) return;

        // Reuse incremental implementation with unlimited budget
        var state = new ChunkRenderState(data.Coord, data)
        {
            ChunkNode = parent,
            Context = context
        };

        const int maxIterations = 100;
        int iterations = 0;
        while (!RenderIncremental(state, long.MaxValue))
        {
            if (++iterations >= maxIterations)
            {
                GD.PushError($"ManholeRenderer.Render exceeded {maxIterations} iterations - likely infinite loop bug");
                break;
            }
        }
    }

    private List<(Vector3 Position, int Seed)> CollectManholePositions(ChunkData data, float chunkWorldX, float chunkWorldZ)
    {
        var positions = new List<(Vector3, int)>();
        if (data.Roads.Count == 0) return positions;

        // Deterministic seed for this chunk
        int chunkSeed = StableHash.Combine(
            (int)(chunkWorldX * 10),
            (int)(chunkWorldZ * 10),
            PurposeManholePlace
        );

        // Determine how many manholes for this chunk
        int range = _config.MaxPerChunk - _config.MinPerChunk;
        int count = _config.MinPerChunk;
        if (range > 0)
        {
            count += (int)(StableHash.ToFloat01(chunkSeed, 0) * (range + 1));
            count = Math.Min(count, _config.MaxPerChunk);
        }

        // Place manholes randomly on roads
        for (int i = 0; i < count; i++)
        {
            int seed = StableHash.Combine(chunkSeed, i);

            // Pick a random road
            int roadIndex = (int)(StableHash.ToFloat01(seed, 1) * data.Roads.Count);
            roadIndex = Math.Min(roadIndex, data.Roads.Count - 1);
            var road = data.Roads[roadIndex];

            // Random position along and across road
            float alongRoad = StableHash.ToFloat01(seed, 2) * data.Size;
            float acrossRoad = (StableHash.ToFloat01(seed, 3) * 2f - 1f) * (road.HalfWidth * 0.7f);

            float manholeX, manholeZ;
            if (road.Direction == RoadDirection.EastWest)
            {
                manholeX = alongRoad;
                manholeZ = road.Position - chunkWorldZ + acrossRoad;
            }
            else
            {
                manholeX = road.Position - chunkWorldX + acrossRoad;
                manholeZ = alongRoad;
            }

            // Validate position is within chunk
            if (manholeX < 0 || manholeX > data.Size || manholeZ < 0 || manholeZ > data.Size)
                continue;

            positions.Add((new Vector3(manholeX, 0, manholeZ), seed));
        }

        return positions;
    }

    private void EnsureCoverMeshes()
    {
        if (_cachedBaseMesh != null) return;

        float radius = _config.CoverDiameter / 2f;
        float depth = _config.CoverDepth;

        // Base disc mesh
        _cachedBaseMesh = new ArrayMesh();
        var baseSt = new SurfaceTool();
        baseSt.Begin(Mesh.PrimitiveType.Triangles);

        Vector3 center = new Vector3(0, depth, 0);
        for (int i = 0; i < SegmentCount; i++)
        {
            float angle1 = (float)i / SegmentCount * Mathf.Tau;
            float angle2 = (float)(i + 1) / SegmentCount * Mathf.Tau;

            Vector3 p1 = new Vector3(Mathf.Cos(angle1) * radius, depth, Mathf.Sin(angle1) * radius);
            Vector3 p2 = new Vector3(Mathf.Cos(angle2) * radius, depth, Mathf.Sin(angle2) * radius);

            baseSt.SetNormal(Vector3.Up);
            baseSt.AddVertex(center);
            baseSt.AddVertex(p1);
            baseSt.AddVertex(p2);
        }

        baseSt.Index();
        baseSt.Commit(_cachedBaseMesh);

        // Ridge mesh (pattern + rim)
        _cachedRidgeMesh = new ArrayMesh();
        var ridgeSt = new SurfaceTool();
        ridgeSt.Begin(Mesh.PrimitiveType.Triangles);

        switch (_config.Pattern.ToLowerInvariant())
        {
            case "concentric_rings":
                AddConcentricRings(ridgeSt, radius, depth);
                break;
            case "radial_lines":
                AddRadialLines(ridgeSt, radius, depth);
                break;
            case "grid":
                AddGridPattern(ridgeSt, radius, depth);
                break;
        }

        AddRimEdge(ridgeSt, radius, depth);

        ridgeSt.Index();
        ridgeSt.Commit(_cachedRidgeMesh);
    }

    private void AddConcentricRings(SurfaceTool st, float radius, float depth)
    {
        float ringDepth = depth + _config.RidgeHeight;
        float ringWidth = _config.GridBarWidth * 3.5f;
        const int ringCount = 4;

        for (int ring = 1; ring <= ringCount; ring++)
        {
            float innerR = radius * ring / (ringCount + 1) - ringWidth / 2f;
            float outerR = radius * ring / (ringCount + 1) + ringWidth / 2f;

            for (int i = 0; i < SegmentCount; i++)
            {
                float angle1 = (float)i / SegmentCount * Mathf.Tau;
                float angle2 = (float)(i + 1) / SegmentCount * Mathf.Tau;

                Vector3 inner1 = new Vector3(Mathf.Cos(angle1) * innerR, ringDepth, Mathf.Sin(angle1) * innerR);
                Vector3 outer1 = new Vector3(Mathf.Cos(angle1) * outerR, ringDepth, Mathf.Sin(angle1) * outerR);
                Vector3 inner2 = new Vector3(Mathf.Cos(angle2) * innerR, ringDepth, Mathf.Sin(angle2) * innerR);
                Vector3 outer2 = new Vector3(Mathf.Cos(angle2) * outerR, ringDepth, Mathf.Sin(angle2) * outerR);

                st.SetNormal(Vector3.Up);
                st.AddVertex(inner1);
                st.AddVertex(outer1);
                st.AddVertex(outer2);

                st.AddVertex(inner1);
                st.AddVertex(outer2);
                st.AddVertex(inner2);
            }
        }
    }

    private void AddRadialLines(SurfaceTool st, float radius, float depth)
    {
        float lineDepth = depth + _config.RidgeHeight;
        float lineWidth = _config.GridBarWidth * 2.5f;
        float innerR = radius * 0.15f;
        float outerR = radius * 0.9f;
        const int spokeCount = 8;

        for (int spoke = 0; spoke < spokeCount; spoke++)
        {
            float angle = (float)spoke / spokeCount * Mathf.Tau;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Perpendicular direction for line width
            float perpCos = Mathf.Cos(angle + Mathf.Pi / 2f);
            float perpSin = Mathf.Sin(angle + Mathf.Pi / 2f);

            Vector3 inner1 = new Vector3(cos * innerR + perpCos * lineWidth, lineDepth, sin * innerR + perpSin * lineWidth);
            Vector3 inner2 = new Vector3(cos * innerR - perpCos * lineWidth, lineDepth, sin * innerR - perpSin * lineWidth);
            Vector3 outer1 = new Vector3(cos * outerR + perpCos * lineWidth, lineDepth, sin * outerR + perpSin * lineWidth);
            Vector3 outer2 = new Vector3(cos * outerR - perpCos * lineWidth, lineDepth, sin * outerR - perpSin * lineWidth);

            st.SetNormal(Vector3.Up);
            st.AddVertex(inner1);
            st.AddVertex(outer1);
            st.AddVertex(outer2);

            st.AddVertex(inner1);
            st.AddVertex(outer2);
            st.AddVertex(inner2);
        }
    }

    private void AddGridPattern(SurfaceTool st, float radius, float depth)
    {
        float gridDepth = depth + _config.RidgeHeight;
        float barWidth = _config.GridBarWidth;
        float gridSpacing = radius * _config.GridSpacing;
        int gridLines = (int)(radius * 2 / gridSpacing);

        // Horizontal bars
        for (int i = -gridLines / 2; i <= gridLines / 2; i++)
        {
            float z = i * gridSpacing;
            float halfWidth = Mathf.Sqrt(Mathf.Max(0, radius * radius - z * z));
            if (halfWidth < barWidth) continue;

            Vector3 a = new Vector3(-halfWidth, gridDepth, z - barWidth);
            Vector3 b = new Vector3(halfWidth, gridDepth, z - barWidth);
            Vector3 c = new Vector3(halfWidth, gridDepth, z + barWidth);
            Vector3 d = new Vector3(-halfWidth, gridDepth, z + barWidth);

            st.SetNormal(Vector3.Up);
            st.AddVertex(a);
            st.AddVertex(b);
            st.AddVertex(c);

            st.AddVertex(a);
            st.AddVertex(c);
            st.AddVertex(d);
        }

        // Vertical bars
        for (int i = -gridLines / 2; i <= gridLines / 2; i++)
        {
            float x = i * gridSpacing;
            float halfHeight = Mathf.Sqrt(Mathf.Max(0, radius * radius - x * x));
            if (halfHeight < barWidth) continue;

            Vector3 a = new Vector3(x - barWidth, gridDepth, -halfHeight);
            Vector3 b = new Vector3(x + barWidth, gridDepth, -halfHeight);
            Vector3 c = new Vector3(x + barWidth, gridDepth, halfHeight);
            Vector3 d = new Vector3(x - barWidth, gridDepth, halfHeight);

            st.SetNormal(Vector3.Up);
            st.AddVertex(a);
            st.AddVertex(b);
            st.AddVertex(c);

            st.AddVertex(a);
            st.AddVertex(c);
            st.AddVertex(d);
        }
    }

    private void AddRimEdge(SurfaceTool st, float radius, float depth)
    {
        float rimWidth = _config.RimWidth;
        float rimDepth = depth + _config.RidgeHeight + 0.003f;
        float innerR = radius - rimWidth;

        for (int i = 0; i < SegmentCount; i++)
        {
            float angle1 = (float)i / SegmentCount * Mathf.Tau;
            float angle2 = (float)(i + 1) / SegmentCount * Mathf.Tau;

            Vector3 inner1 = new Vector3(Mathf.Cos(angle1) * innerR, rimDepth, Mathf.Sin(angle1) * innerR);
            Vector3 outer1 = new Vector3(Mathf.Cos(angle1) * radius, rimDepth, Mathf.Sin(angle1) * radius);
            Vector3 inner2 = new Vector3(Mathf.Cos(angle2) * innerR, rimDepth, Mathf.Sin(angle2) * innerR);
            Vector3 outer2 = new Vector3(Mathf.Cos(angle2) * radius, rimDepth, Mathf.Sin(angle2) * radius);

            st.SetNormal(Vector3.Up);
            st.AddVertex(inner1);
            st.AddVertex(outer1);
            st.AddVertex(outer2);

            st.AddVertex(inner1);
            st.AddVertex(outer2);
            st.AddVertex(inner2);
        }
    }

    private void EnsureMaterial()
    {
        if (_baseMaterial != null) return;

        // Base is darker
        _baseMaterial = new StandardMaterial3D
        {
            AlbedoColor = _materialColor.Darkened(0.3f),
            Roughness = _config.MaterialRoughness,
            Metallic = _config.MaterialMetallic
        };

        // Ridges are lighter, more metallic (worn/polished from traffic)
        _ridgeMaterial = new StandardMaterial3D
        {
            AlbedoColor = _materialColor.Lightened(0.15f),
            Roughness = _config.MaterialRoughness * 0.7f,
            Metallic = Mathf.Min(1.0f, _config.MaterialMetallic + 0.4f)
        };
    }

    public bool RenderIncremental(ChunkRenderState state, long budgetUsec)
    {
        if (state.ChunkNode == null || state.Context == null) return true;
        if (!_config.Enabled || state.Data.Roads.Count == 0) return true;

        var context = state.Context;
        var startTime = Time.GetTicksUsec();

        // Initialize on first call
        if (state.ManholesNode == null)
        {
            state.ManholePositions = CollectManholePositions(state.Data, context.ChunkWorldX, context.ChunkWorldZ);
            if (state.ManholePositions.Count == 0)
                return true;

            EnsureMaterial();
            EnsureCoverMeshes();

            state.ManholesNode = new Node3D { Name = "Manholes" };
            state.ChunkNode.AddChild(state.ManholesNode);
        }

        // Create manholes incrementally
        float coverY = _roadConfig.RoadHeight + 0.001f;
        while (state.ManholeIndex < state.ManholePositions!.Count)
        {
            var (pos, seed) = state.ManholePositions[state.ManholeIndex];

            // Reuse cached meshes for all manholes
            var baseInstance = new MeshInstance3D
            {
                Mesh = _cachedBaseMesh,
                MaterialOverride = _baseMaterial,
                Position = new Vector3(pos.X, coverY, pos.Z)
            };
            state.ManholesNode.AddChild(baseInstance);

            var ridgeInstance = new MeshInstance3D
            {
                Mesh = _cachedRidgeMesh,
                MaterialOverride = _ridgeMaterial,
                Position = new Vector3(pos.X, coverY, pos.Z)
            };
            state.ManholesNode.AddChild(ridgeInstance);

            // Check if this manhole should have smoke
            if (_config.SmokeEnabled)
            {
                float smokeCheck = StableHash.ToFloat01(seed, PurposeManholeSmokeCheck);
                if (smokeCheck < _config.SmokeChance)
                {
                    float scaleT = StableHash.ToFloat01(seed, PurposeManholeSmokeIntensity);
                    scaleT *= scaleT;
                    float emitterScale = Mathf.Lerp(_smokeConfig.EmitterScaleMin, _smokeConfig.EmitterScaleMax, scaleT);

                    var smokePos = new Vector3(pos.X, _roadConfig.RoadHeight, pos.Z);
                    var smoke = SmokeEmitter.Create(smokePos, _smokeConfig, emitterScale);
                    state.ManholesNode.AddChild(smoke);
                }
            }

            state.ManholeIndex++;

            // Check budget every few manholes
            if (state.ManholeIndex % 2 == 0)
            {
                var elapsed = (long)(Time.GetTicksUsec() - startTime);
                if (elapsed >= budgetUsec)
                    return false; // More work needed
            }
        }

        return true; // Phase complete
    }
}
