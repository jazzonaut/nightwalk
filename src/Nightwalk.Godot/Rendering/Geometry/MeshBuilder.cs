using System.Collections.Generic;
using Godot;

namespace Nightwalk.Godot.Rendering.Geometry;

/// <summary>
/// Efficient mesh builder that accumulates geometry into arrays and commits once.
/// Avoids per-vertex interop overhead of SurfaceTool.
/// </summary>
public sealed class MeshBuilder
{
    private readonly List<Vector3> _vertices = new();
    private readonly List<Vector3> _normals = new();
    private readonly List<Vector2> _uvs = new();
    private readonly List<Vector2> _uv2s = new();
    private readonly List<Color> _colors = new();
    private readonly List<int> _indices = new();
    private bool _hasUVs;
    private bool _hasUV2s;
    private bool _hasColors;

    /// <summary>
    /// Adds a quad with 4 vertices and 6 indices (2 triangles).
    /// Vertices are in counter-clockwise order when viewed from the normal direction.
    /// </summary>
    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        int baseIndex = _vertices.Count;

        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
        _vertices.Add(d);

        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);

        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 1);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex + 3);
    }

    /// <summary>
    /// Adds a top-facing quad at the specified center position.
    /// More efficient than AddBox for flat surfaces like roads and markings.
    /// </summary>
    public void AddTopQuad(float cx, float cy, float cz, float width, float depth)
    {
        float hw = width / 2f;
        float hd = depth / 2f;

        var a = new Vector3(cx - hw, cy, cz - hd);
        var b = new Vector3(cx + hw, cy, cz - hd);
        var c = new Vector3(cx + hw, cy, cz + hd);
        var d = new Vector3(cx - hw, cy, cz + hd);

        AddQuad(a, b, c, d, Vector3.Up);
    }

    /// <summary>
    /// Adds a full box (6 faces, 24 vertices).
    /// Use AddTopQuad when only the top face is visible.
    /// </summary>
    public void AddBox(float cx, float cy, float cz, float width, float height, float depth)
    {
        float hw = width / 2f;
        float hh = height / 2f;
        float hd = depth / 2f;

        var v0 = new Vector3(cx - hw, cy - hh, cz - hd);
        var v1 = new Vector3(cx + hw, cy - hh, cz - hd);
        var v2 = new Vector3(cx + hw, cy + hh, cz - hd);
        var v3 = new Vector3(cx - hw, cy + hh, cz - hd);
        var v4 = new Vector3(cx - hw, cy - hh, cz + hd);
        var v5 = new Vector3(cx + hw, cy - hh, cz + hd);
        var v6 = new Vector3(cx + hw, cy + hh, cz + hd);
        var v7 = new Vector3(cx - hw, cy + hh, cz + hd);

        AddQuad(v0, v1, v2, v3, new Vector3(0, 0, -1)); // Front (-Z)
        AddQuad(v5, v4, v7, v6, new Vector3(0, 0, 1));  // Back (+Z)
        AddQuad(v4, v0, v3, v7, new Vector3(-1, 0, 0)); // Left (-X)
        AddQuad(v1, v5, v6, v2, new Vector3(1, 0, 0));  // Right (+X)
        AddQuad(v3, v2, v6, v7, new Vector3(0, 1, 0));  // Top (+Y)
        AddQuad(v4, v5, v1, v0, new Vector3(0, -1, 0)); // Bottom (-Y)
    }

    /// <summary>
    /// Adds a quad with UV coordinates.
    /// </summary>
    public void AddQuadWithUV(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
        Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
    {
        _hasUVs = true;
        int baseIndex = _vertices.Count;

        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
        _vertices.Add(d);

        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);

        _uvs.Add(uvA);
        _uvs.Add(uvB);
        _uvs.Add(uvC);
        _uvs.Add(uvD);

        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 1);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex + 3);
    }

    /// <summary>
    /// Adds a quad with the same UV/UV2 applied to all 4 vertices. No color.
    /// Used for window-lit geometry where per-window state is carried in UV/UV2.
    /// </summary>
    public void AddQuadUniformUV2(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
        Vector2 uv, Vector2 uv2)
    {
        _hasUVs = true;
        _hasUV2s = true;
        int baseIndex = _vertices.Count;

        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
        _vertices.Add(d);

        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);

        _uvs.Add(uv);
        _uvs.Add(uv);
        _uvs.Add(uv);
        _uvs.Add(uv);

        _uv2s.Add(uv2);
        _uv2s.Add(uv2);
        _uv2s.Add(uv2);
        _uv2s.Add(uv2);

        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 1);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex + 3);
    }

    /// <summary>
    /// Adds a quad with UV.x = seed on all 4 vertices (UV.y = 0).
    /// Used by weathering shaders that need per-building variation.
    /// </summary>
    public void AddQuadWithSeed(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float seed)
    {
        _hasUVs = true;
        int baseIndex = _vertices.Count;
        var uv = new Vector2(seed, 0f);

        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
        _vertices.Add(d);

        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);

        _uvs.Add(uv);
        _uvs.Add(uv);
        _uvs.Add(uv);
        _uvs.Add(uv);

        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 1);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex + 3);
    }

    /// <summary>
    /// Adds a 6-face box with UV.x = seed on every vertex. Used for building body geometry
    /// so the weathering shader can read per-building seed from UV.
    /// </summary>
    public void AddBoxWithSeed(float cx, float cy, float cz, float width, float height, float depth, float seed)
    {
        float hw = width / 2f;
        float hh = height / 2f;
        float hd = depth / 2f;

        var v0 = new Vector3(cx - hw, cy - hh, cz - hd);
        var v1 = new Vector3(cx + hw, cy - hh, cz - hd);
        var v2 = new Vector3(cx + hw, cy + hh, cz - hd);
        var v3 = new Vector3(cx - hw, cy + hh, cz - hd);
        var v4 = new Vector3(cx - hw, cy - hh, cz + hd);
        var v5 = new Vector3(cx + hw, cy - hh, cz + hd);
        var v6 = new Vector3(cx + hw, cy + hh, cz + hd);
        var v7 = new Vector3(cx - hw, cy + hh, cz + hd);

        // Winding matches AddBox / GeometryBuilder.AddBoxWithSeed exactly.
        AddQuadWithSeed(v0, v1, v2, v3, new Vector3(0, 0, -1), seed); // Front (-Z)
        AddQuadWithSeed(v5, v4, v7, v6, new Vector3(0, 0, 1), seed);  // Back (+Z)
        AddQuadWithSeed(v4, v0, v3, v7, new Vector3(-1, 0, 0), seed); // Left (-X)
        AddQuadWithSeed(v1, v5, v6, v2, new Vector3(1, 0, 0), seed);  // Right (+X)
        AddQuadWithSeed(v3, v2, v6, v7, new Vector3(0, 1, 0), seed);  // Top (+Y)
        AddQuadWithSeed(v4, v5, v1, v0, new Vector3(0, -1, 0), seed); // Bottom (-Y)
    }

    /// <summary>
    /// Adds a quad with UV, UV2, and color data.
    /// </summary>
    public void AddQuadWithUVColor(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
        Vector2 uv, Vector2 uv2, Color color)
    {
        _hasUVs = true;
        _hasUV2s = true;
        _hasColors = true;
        int baseIndex = _vertices.Count;

        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
        _vertices.Add(d);

        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);

        _uvs.Add(uv);
        _uvs.Add(uv);
        _uvs.Add(uv);
        _uvs.Add(uv);

        _uv2s.Add(uv2);
        _uv2s.Add(uv2);
        _uv2s.Add(uv2);
        _uv2s.Add(uv2);

        _colors.Add(color);
        _colors.Add(color);
        _colors.Add(color);
        _colors.Add(color);

        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 1);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex + 3);
    }

    /// <summary>
    /// Adds a plot top quad with per-vertex UVs for edge detection.
    /// </summary>
    public void AddPlotTopQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
        Vector2 uv2, Color edgeColor)
    {
        _hasUVs = true;
        _hasUV2s = true;
        _hasColors = true;
        int baseIndex = _vertices.Count;

        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
        _vertices.Add(d);

        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);

        // Per-vertex UVs for edge detection (0,0), (1,0), (1,1), (0,1)
        _uvs.Add(new Vector2(0, 0));
        _uvs.Add(new Vector2(1, 0));
        _uvs.Add(new Vector2(1, 1));
        _uvs.Add(new Vector2(0, 1));

        _uv2s.Add(uv2);
        _uv2s.Add(uv2);
        _uv2s.Add(uv2);
        _uv2s.Add(uv2);

        _colors.Add(edgeColor);
        _colors.Add(edgeColor);
        _colors.Add(edgeColor);
        _colors.Add(edgeColor);

        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 1);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 2);
        _indices.Add(baseIndex + 3);
    }

    /// <summary>
    /// Returns the mesh arrays ready for ArrayMesh.AddSurfaceFromArrays.
    /// Returns null if no geometry has been added.
    /// </summary>
    public global::Godot.Collections.Array? ToSurfaceArrays()
    {
        if (_vertices.Count == 0)
            return null;

        var arrays = new global::Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = _indices.ToArray();

        if (_hasUVs && _uvs.Count == _vertices.Count)
            arrays[(int)Mesh.ArrayType.TexUV] = _uvs.ToArray();

        if (_hasUV2s && _uv2s.Count == _vertices.Count)
            arrays[(int)Mesh.ArrayType.TexUV2] = _uv2s.ToArray();

        if (_hasColors && _colors.Count == _vertices.Count)
            arrays[(int)Mesh.ArrayType.Color] = _colors.ToArray();

        return arrays;
    }

    /// <summary>
    /// Clears all data for reuse.
    /// </summary>
    public void Clear()
    {
        _vertices.Clear();
        _normals.Clear();
        _uvs.Clear();
        _uv2s.Clear();
        _colors.Clear();
        _indices.Clear();
        _hasUVs = false;
        _hasUV2s = false;
        _hasColors = false;
    }

    /// <summary>
    /// Returns true if no geometry has been added.
    /// </summary>
    public bool IsEmpty => _vertices.Count == 0;
}
