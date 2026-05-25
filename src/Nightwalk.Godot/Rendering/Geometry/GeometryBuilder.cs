using Godot;

namespace Nightwalk.Godot.Rendering.Geometry;

/// <summary>
/// Direction for window/face geometry generation.
/// </summary>
public enum FaceDirection
{
    /// <summary>Facing +Z direction.</summary>
    Front,
    /// <summary>Facing -Z direction.</summary>
    Back,
    /// <summary>Facing -X direction.</summary>
    Left,
    /// <summary>Facing +X direction.</summary>
    Right
}

/// <summary>
/// Helper methods for building geometry primitives.
/// Replaces duplicate AddWindowQuad* methods with unified parameterized versions.
/// </summary>
public static class GeometryBuilder
{
    /// <summary>
    /// Adds a box to the SurfaceTool at the given center position with explicit normals.
    /// </summary>
    public static void AddBox(SurfaceTool st, float cx, float cy, float cz, float width, float height, float depth)
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

        AddQuad(st, v0, v1, v2, v3, new Vector3(0, 0, -1)); // Front (-Z)
        AddQuad(st, v5, v4, v7, v6, new Vector3(0, 0, 1));  // Back (+Z)
        AddQuad(st, v4, v0, v3, v7, new Vector3(-1, 0, 0)); // Left (-X)
        AddQuad(st, v1, v5, v6, v2, new Vector3(1, 0, 0));  // Right (+X)
        AddQuad(st, v3, v2, v6, v7, new Vector3(0, 1, 0));  // Top (+Y)
        AddQuad(st, v4, v5, v1, v0, new Vector3(0, -1, 0)); // Bottom (-Y)
    }

    /// <summary>
    /// Adds a box with a seed value stored in UV.x for weathering shaders.
    /// </summary>
    public static void AddBoxWithSeed(SurfaceTool st, float cx, float cy, float cz, float width, float height, float depth, float seed)
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

        AddQuadWithSeed(st, v0, v1, v2, v3, new Vector3(0, 0, -1), seed); // Front (-Z)
        AddQuadWithSeed(st, v5, v4, v7, v6, new Vector3(0, 0, 1), seed);  // Back (+Z)
        AddQuadWithSeed(st, v4, v0, v3, v7, new Vector3(-1, 0, 0), seed); // Left (-X)
        AddQuadWithSeed(st, v1, v5, v6, v2, new Vector3(1, 0, 0), seed);  // Right (+X)
        AddQuadWithSeed(st, v3, v2, v6, v7, new Vector3(0, 1, 0), seed);  // Top (+Y)
        AddQuadWithSeed(st, v4, v5, v1, v0, new Vector3(0, -1, 0), seed); // Bottom (-Y)
    }

    /// <summary>
    /// Adds a quad with 4 vertices and explicit normal.
    /// </summary>
    public static void AddQuad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetNormal(normal);
        st.AddVertex(b);
        st.SetNormal(normal);
        st.AddVertex(c);

        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetNormal(normal);
        st.AddVertex(c);
        st.SetNormal(normal);
        st.AddVertex(d);
    }

    /// <summary>
    /// Adds a quad with 4 vertices, explicit normal, and a seed value stored in UV.x.
    /// Used for weathering shaders that need per-building variation.
    /// </summary>
    public static void AddQuadWithSeed(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float seed)
    {
        var uv = new Vector2(seed, 0);
        st.SetUV(uv);
        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetUV(uv);
        st.SetNormal(normal);
        st.AddVertex(b);
        st.SetUV(uv);
        st.SetNormal(normal);
        st.AddVertex(c);

        st.SetUV(uv);
        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetUV(uv);
        st.SetNormal(normal);
        st.AddVertex(c);
        st.SetUV(uv);
        st.SetNormal(normal);
        st.AddVertex(d);
    }

    /// <summary>
    /// Adds a quad with UV and UV2 data.
    /// </summary>
    public static void AddQuadWithUV(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uv, Vector2 uv2)
    {
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(b);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(c);

        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(c);
        st.SetUV(uv);
        st.SetUV2(uv2);
        st.SetNormal(normal);
        st.AddVertex(d);
    }

    /// <summary>
    /// Adds a window quad facing the specified direction.
    /// Replaces AddWindowQuadFront/Back/Left/Right methods.
    /// </summary>
    public static void AddWindowQuad(SurfaceTool st, float cx, float cy, float cz, float width, float height, FaceDirection direction)
    {
        var hw = width / 2f;
        var hh = height / 2f;

        var (vertices, normal) = GetWindowQuadVertices(cx, cy, cz, hw, hh, direction);
        AddQuad(st, vertices[0], vertices[1], vertices[2], vertices[3], normal);
    }

    /// <summary>
    /// Adds a lit window quad with UV data facing the specified direction.
    /// Replaces AddWindowQuadFrontWithUV/BackWithUV/LeftWithUV/RightWithUV methods.
    /// </summary>
    public static void AddWindowQuadWithUV(SurfaceTool st, float cx, float cy, float cz, float width, float height,
        float uvX, float uvY, float uv2X, float uv2Y, FaceDirection direction)
    {
        var hw = width / 2f;
        var hh = height / 2f;

        var (vertices, normal) = GetWindowQuadVertices(cx, cy, cz, hw, hh, direction);
        var uv = new Vector2(uvX, uvY);
        var uv2 = new Vector2(uv2X, uv2Y);
        AddQuadWithUV(st, vertices[0], vertices[1], vertices[2], vertices[3], normal, uv, uv2);
    }

    /// <summary>
    /// Gets the vertices and normal for a window quad based on direction.
    /// </summary>
    private static (Vector3[] vertices, Vector3 normal) GetWindowQuadVertices(
        float cx, float cy, float cz, float hw, float hh, FaceDirection direction)
    {
        return direction switch
        {
            FaceDirection.Front => (
                new[]
                {
                    new Vector3(cx + hw, cy - hh, cz),
                    new Vector3(cx - hw, cy - hh, cz),
                    new Vector3(cx - hw, cy + hh, cz),
                    new Vector3(cx + hw, cy + hh, cz)
                },
                new Vector3(0, 0, 1)
            ),
            FaceDirection.Back => (
                new[]
                {
                    new Vector3(cx - hw, cy - hh, cz),
                    new Vector3(cx + hw, cy - hh, cz),
                    new Vector3(cx + hw, cy + hh, cz),
                    new Vector3(cx - hw, cy + hh, cz)
                },
                new Vector3(0, 0, -1)
            ),
            FaceDirection.Right => (
                new[]
                {
                    new Vector3(cx, cy - hh, cz - hw),
                    new Vector3(cx, cy - hh, cz + hw),
                    new Vector3(cx, cy + hh, cz + hw),
                    new Vector3(cx, cy + hh, cz - hw)
                },
                new Vector3(1, 0, 0)
            ),
            FaceDirection.Left => (
                new[]
                {
                    new Vector3(cx, cy - hh, cz + hw),
                    new Vector3(cx, cy - hh, cz - hw),
                    new Vector3(cx, cy + hh, cz - hw),
                    new Vector3(cx, cy + hh, cz + hw)
                },
                new Vector3(-1, 0, 0)
            ),
            _ => throw new System.ArgumentOutOfRangeException(nameof(direction))
        };
    }

    /// <summary>
    /// Commits a SurfaceTool with index generation for vertex deduplication.
    /// </summary>
    public static ArrayMesh CommitOptimized(SurfaceTool st)
    {
        st.Index();
        return st.Commit();
    }

    // ============================================================================
    // MeshBuilder overloads (faster path — avoids per-vertex SurfaceTool marshalling)
    // ============================================================================

    /// <summary>
    /// Adds a box to a MeshBuilder. Marshalling-free alternative to the SurfaceTool overload.
    /// </summary>
    public static void AddBox(MeshBuilder mb, float cx, float cy, float cz, float width, float height, float depth)
    {
        mb.AddBox(cx, cy, cz, width, height, depth);
    }

    /// <summary>
    /// Adds a box with seed UV to a MeshBuilder. Marshalling-free alternative.
    /// </summary>
    public static void AddBoxWithSeed(MeshBuilder mb, float cx, float cy, float cz, float width, float height, float depth, float seed)
    {
        mb.AddBoxWithSeed(cx, cy, cz, width, height, depth, seed);
    }

    /// <summary>
    /// Adds an unlit window quad facing the specified direction to a MeshBuilder.
    /// </summary>
    public static void AddWindowQuad(MeshBuilder mb, float cx, float cy, float cz, float width, float height, FaceDirection direction)
    {
        var hw = width / 2f;
        var hh = height / 2f;
        var (vertices, normal) = GetWindowQuadVertices(cx, cy, cz, hw, hh, direction);
        mb.AddQuad(vertices[0], vertices[1], vertices[2], vertices[3], normal);
    }

    /// <summary>
    /// Adds a lit window quad with UV+UV2 data to a MeshBuilder. The shader uses UV for warm/cool/accent
    /// hashing and UV2 for per-window curtain/blinds state.
    /// </summary>
    public static void AddWindowQuadWithUV(MeshBuilder mb, float cx, float cy, float cz, float width, float height,
        float uvX, float uvY, float uv2X, float uv2Y, FaceDirection direction)
    {
        var hw = width / 2f;
        var hh = height / 2f;
        var (vertices, normal) = GetWindowQuadVertices(cx, cy, cz, hw, hh, direction);
        var uv = new Vector2(uvX, uvY);
        var uv2 = new Vector2(uv2X, uv2Y);
        mb.AddQuadUniformUV2(vertices[0], vertices[1], vertices[2], vertices[3], normal, uv, uv2);
    }
}
