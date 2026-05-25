using System.Collections.Generic;
using Godot;
using GodotArray = Godot.Collections.Array;

namespace Nightwalk.Godot.Skycraft;

/// <summary>
/// Welds a set of <see cref="PrimitiveMesh"/> pieces (each placed by a <see cref="Transform3D"/>
/// and tagged with a surface index) into a single multi-surface <see cref="ArrayMesh"/>.
///
/// Per ADR-0016 §Visual identity the renderer expects exactly one <see cref="Mesh"/> resource
/// per profile, so this is where we get to be visually richer than three primitives without
/// breaking the MultiMesh contract: many small chassis/trim/canopy pieces collapse into one
/// Mesh with one surface per material category.
/// </summary>
internal sealed class SkycraftMeshComposer
{
    private readonly Dictionary<int, Surface> _surfaces = new();

    public void Add(PrimitiveMesh primitive, Transform3D xform, int surfaceIndex)
    {
        var arrays = primitive.GetMeshArrays();
        var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var norms = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        var inds = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();

        if (!_surfaces.TryGetValue(surfaceIndex, out var surf))
        {
            surf = new Surface();
            _surfaces[surfaceIndex] = surf;
        }

        int baseIdx = surf.Vertices.Count;
        var basis = xform.Basis;
        // Inverse-transpose handles non-uniform scale correctly; pieces here mostly use uniform
        // scale, but the canopy uses non-uniform stretch and we don't want to special-case.
        var normalBasis = basis.Inverse().Transposed();

        bool hasUv = uvs.Length == verts.Length;
        for (int i = 0; i < verts.Length; i++)
        {
            surf.Vertices.Add(xform * verts[i]);
            surf.Normals.Add((normalBasis * norms[i]).Normalized());
            surf.UVs.Add(hasUv ? uvs[i] : Vector2.Zero);
        }
        for (int i = 0; i < inds.Length; i++)
            surf.Indices.Add(baseIdx + inds[i]);
    }

    public ArrayMesh Build(IReadOnlyDictionary<int, Material> materials)
    {
        var mesh = new ArrayMesh();
        var ordered = new List<int>(_surfaces.Keys);
        ordered.Sort();
        foreach (var idx in ordered)
        {
            var surf = _surfaces[idx];
            var arrays = new GodotArray();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = surf.Vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = surf.Normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = surf.UVs.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = surf.Indices.ToArray();

            int surfaceIdx = mesh.GetSurfaceCount();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            if (materials.TryGetValue(idx, out var mat) && mat != null)
                mesh.SurfaceSetMaterial(surfaceIdx, mat);
        }
        return mesh;
    }

    private sealed class Surface
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Vector2> UVs = new();
        public readonly List<int> Indices = new();
    }
}
