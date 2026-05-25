using Godot;
using Nightwalk.Core.Chunks;
using Nightwalk.Core.Config;
using Nightwalk.Godot.Rendering;
using Nightwalk.Godot.Rendering.Geometry;

namespace Nightwalk.Godot.Rendering.SubRenderers;

/// <summary>
/// Renders raised plot surfaces for building plots.
/// Creates sidewalk-height pavements with curb edge detection.
/// </summary>
public sealed class PlotPavementRenderer : ISubRenderer, IPhasedSubRenderer
{
    public ChunkRenderPhase Phase => ChunkRenderPhase.Pavement;
    private readonly RoadConfig _roadConfig;

    public PlotPavementRenderer(RoadConfig roadConfig)
    {
        _roadConfig = roadConfig;
    }

    public void Render(Node3D parent, ChunkData data, ChunkRenderContext context)
    {
        if (data.Plots.Count == 0) return;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        float plotTop = _roadConfig.SidewalkHeight + _roadConfig.CurbHeight;
        float plotHeight = plotTop;
        float plotY = plotTop / 2f;

        foreach (var plot in data.Plots)
        {
            float localMinX = plot.MinX - context.ChunkWorldX;
            float localMaxX = plot.MaxX - context.ChunkWorldX;
            float localMinZ = plot.MinZ - context.ChunkWorldZ;
            float localMaxZ = plot.MaxZ - context.ChunkWorldZ;

            float width = localMaxX - localMinX;
            float depth = localMaxZ - localMinZ;
            float centerX = (localMinX + localMaxX) / 2f;
            float centerZ = (localMinZ + localMaxZ) / 2f;

            AddPlotBox(st, centerX, plotY, centerZ, width, plotHeight, depth,
                plot.HasRoadMinX, plot.HasRoadMaxX, plot.HasRoadMinZ, plot.HasRoadMaxZ);
        }

        var pavementMesh = GeometryBuilder.CommitOptimized(st);
        if (pavementMesh.GetSurfaceCount() > 0)
        {
            var arrayMesh = new ArrayMesh();
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, pavementMesh.SurfaceGetArrays(0));
            arrayMesh.SurfaceSetMaterial(0, context.WetSidewalkMaterial);

            var meshInstance = context.NodePool.GetMeshInstance("PlotPavements");
            meshInstance.Mesh = arrayMesh;
            parent.AddChild(meshInstance);
        }
    }

    private static void AddPlotBox(SurfaceTool st, float cx, float cy, float cz, float width, float height, float depth,
        bool hasRoadMinX, bool hasRoadMaxX, bool hasRoadMinZ, bool hasRoadMaxZ)
    {
        var hw = width / 2f;
        var hh = height / 2f;
        var hd = depth / 2f;

        var v0 = new Vector3(cx - hw, cy - hh, cz - hd);
        var v1 = new Vector3(cx + hw, cy - hh, cz - hd);
        var v2 = new Vector3(cx + hw, cy + hh, cz - hd);
        var v3 = new Vector3(cx - hw, cy + hh, cz - hd);
        var v4 = new Vector3(cx - hw, cy - hh, cz + hd);
        var v5 = new Vector3(cx + hw, cy - hh, cz + hd);
        var v6 = new Vector3(cx + hw, cy + hh, cz + hd);
        var v7 = new Vector3(cx - hw, cy + hh, cz + hd);

        var uv2 = new Vector2(width, depth);

        var edgeColor = new Color(
            hasRoadMinX ? 1f : 0f,
            hasRoadMaxX ? 1f : 0f,
            hasRoadMinZ ? 1f : 0f,
            hasRoadMaxZ ? 1f : 0f
        );

        var curbUV = new Vector2(0, -1);
        AddQuadWithUVColor(st, v0, v1, v2, v3, new Vector3(0, 0, -1), curbUV, uv2, edgeColor);
        AddQuadWithUVColor(st, v5, v4, v7, v6, new Vector3(0, 0, 1), curbUV, uv2, edgeColor);
        AddQuadWithUVColor(st, v4, v0, v3, v7, new Vector3(-1, 0, 0), curbUV, uv2, edgeColor);
        AddQuadWithUVColor(st, v1, v5, v6, v2, new Vector3(1, 0, 0), curbUV, uv2, edgeColor);

        AddPlotTopQuad(st, v3, v2, v6, v7, new Vector3(0, 1, 0), uv2, edgeColor);

        AddQuadWithUVColor(st, v4, v5, v1, v0, new Vector3(0, -1, 0), curbUV, uv2, edgeColor);
    }

    private static void AddPlotTopQuad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uv2, Color edgeColor)
    {
        var uvA = new Vector2(0, 0);
        var uvB = new Vector2(1, 0);
        var uvC = new Vector2(1, 1);
        var uvD = new Vector2(0, 1);

        st.SetColor(edgeColor);
        st.SetUV(uvA);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetColor(edgeColor);
        st.SetUV(uvB);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(b);
        st.SetColor(edgeColor);
        st.SetUV(uvC);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(c);

        st.SetColor(edgeColor);
        st.SetUV(uvA);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetColor(edgeColor);
        st.SetUV(uvC);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(c);
        st.SetColor(edgeColor);
        st.SetUV(uvD);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(d);
    }

    private static void AddQuadWithUVColor(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uv, Vector2 uv2, Color color)
    {
        st.SetColor(color);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetColor(color);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(b);
        st.SetColor(color);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(c);

        st.SetColor(color);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetColor(color);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(c);
        st.SetColor(color);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(d);
    }

    public bool RenderIncremental(ChunkRenderState state, long budgetUsec)
    {
        if (state.ChunkNode == null || state.Context == null) return true;

        Render(state.ChunkNode, state.Data, state.Context);
        return true; // Always completes in one call
    }
}
